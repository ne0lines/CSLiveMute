using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using CsLiveMute.Core.Gsi;
using CsLiveMute.Core.Models;
using CsLiveMute.Core.Protocol;
using CsLiveMute.Desktop.Models;

namespace CsLiveMute.Desktop.Services;

public sealed class LocalBridgeHost : IAsyncDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpListener _listener = new();
    private readonly ConcurrentDictionary<Guid, ConnectedSession> _sessions = new();
    private readonly CombatModeEngine _combatModeEngine = new();
    private readonly object _sync = new();

    private CancellationTokenSource? _lifetime;
    private Task? _listenLoop;
    private Task? _heartbeatLoop;
    private AppSettings _settings;
    private BridgeTelemetrySnapshot _snapshot = BridgeTelemetrySnapshot.Empty;

    public LocalBridgeHost(AppSettings settings)
    {
        _settings = settings;
    }

    public event EventHandler<BridgeTelemetrySnapshot>? TelemetryChanged;

    public async Task StartAsync()
    {
        _listener.Prefixes.Add($"http://127.0.0.1:{_settings.Port}/");
        _listener.Start();
        _lifetime = new CancellationTokenSource();
        _listenLoop = Task.Run(() => ListenAsync(_lifetime.Token));
        _heartbeatLoop = Task.Run(() => BroadcastHeartbeatLoopAsync(_lifetime.Token));
        await Task.Yield();
        EmitTelemetry();
    }

    public void UpdateSettings(AppSettings settings)
    {
        CombatModeTransition transition;
        lock (_sync)
        {
            _settings = settings;
            transition = _combatModeEngine.Evaluate(_settings.Enabled, _snapshot.LastRoundPhase);
            _snapshot = _snapshot with
            {
                CombatModeActive = transition.IsActive,
                LastError = null
            };
        }

        EmitTelemetry();
        if (transition.Changed)
        {
            _ = BroadcastAsync(new CombatModeChangedMessage(transition.IsActive, transition.RoundPhase, DateTimeOffset.UtcNow));
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_lifetime is not null)
        {
            _lifetime.Cancel();
        }

        foreach (var session in _sessions.Values)
        {
            try
            {
                if (session.Socket.State == WebSocketState.Open)
                {
                    await session.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down", CancellationToken.None);
                }
            }
            catch
            {
            }
        }

        _listener.Close();

        if (_listenLoop is not null)
        {
            await _listenLoop;
        }

        if (_heartbeatLoop is not null)
        {
            await _heartbeatLoop;
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext? context = null;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (cancellationToken.IsCancellationRequested || !_listener.IsListening)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (context is not null)
            {
                _ = Task.Run(() => HandleContextAsync(context, cancellationToken), cancellationToken);
            }
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        if (context.Request.HttpMethod == "POST" && path.Equals("/gsi", StringComparison.OrdinalIgnoreCase))
        {
            await HandleGsiAsync(context);
            return;
        }

        if (path.Equals("/bridge", StringComparison.OrdinalIgnoreCase))
        {
            await HandleBridgeAsync(context, cancellationToken);
            return;
        }

        context.Response.StatusCode = (int)HttpStatusCode.NotFound;
        context.Response.Close();
    }

    private async Task HandleGsiAsync(HttpListenerContext context)
    {
        string payload;
        using (var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
        {
            payload = await reader.ReadToEndAsync();
        }

        if (!GsiPayloadParser.TryParse(payload, _settings.AuthToken, out var state, out var error))
        {
            UpdateSnapshot(snapshot => snapshot with
            {
                GsiConnected = false,
                GsiMessage = "Malformed GSI payload",
                LastError = error
            });

            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.Close();
            return;
        }

        if (!state.IsAuthenticated)
        {
            UpdateSnapshot(snapshot => snapshot with
            {
                GsiConnected = false,
                GsiMessage = "Auth token mismatch",
                LastError = error
            });

            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Response.Close();
            return;
        }

        CombatModeTransition transition;
        lock (_sync)
        {
            transition = _combatModeEngine.Evaluate(_settings.Enabled, state.RoundPhase);
            _snapshot = _snapshot with
            {
                CombatModeActive = transition.IsActive,
                GsiConnected = true,
                LastGsiReceivedAt = DateTimeOffset.UtcNow,
                LastRoundPhase = state.RoundPhase,
                GsiMessage = "Receiving CS2 events",
                LastError = null
            };
        }

        EmitTelemetry();
        if (transition.Changed)
        {
            await BroadcastAsync(new CombatModeChangedMessage(transition.IsActive, transition.RoundPhase, DateTimeOffset.UtcNow));
        }

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Close();
    }

    private async Task HandleBridgeAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        if (!context.Request.IsWebSocketRequest)
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.Close();
            return;
        }

        var socketContext = await context.AcceptWebSocketAsync(null);
        var session = new ConnectedSession(Guid.NewGuid(), socketContext.WebSocket);
        _sessions[session.Id] = session;
        UpdateSnapshot(snapshot => snapshot with { ExtensionConnected = true, LastError = null });

        await SendAsync(session.Socket, new HelloMessage("desktop", "cs-live-mute", typeof(LocalBridgeHost).Assembly.GetName().Version?.ToString(), _snapshot.CombatModeActive), cancellationToken);
        await SendAsync(session.Socket, new CombatModeChangedMessage(_snapshot.CombatModeActive, _snapshot.LastRoundPhase, DateTimeOffset.UtcNow), cancellationToken);

        try
        {
            await ReceiveLoopAsync(session, cancellationToken);
        }
        finally
        {
            _sessions.TryRemove(session.Id, out _);
            UpdateSnapshot(snapshot => snapshot with
            {
                ExtensionConnected = !_sessions.IsEmpty,
                Browser = !_sessions.IsEmpty ? snapshot.Browser : null,
                ExtensionVersion = !_sessions.IsEmpty ? snapshot.ExtensionVersion : null,
                ConnectedTabs = !_sessions.IsEmpty ? snapshot.ConnectedTabs : 0,
                SupportedTabs = !_sessions.IsEmpty ? snapshot.SupportedTabs : 0
            });
        }
    }

    private async Task ReceiveLoopAsync(ConnectedSession session, CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];

        while (session.Socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await session.Socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await session.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                    return;
                }

                stream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var message = Encoding.UTF8.GetString(stream.ToArray());
            await HandleClientMessageAsync(session, message, cancellationToken);
        }
    }

    private async Task HandleClientMessageAsync(ConnectedSession session, string message, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(message);
        if (!document.RootElement.TryGetProperty("type", out var typeElement))
        {
            return;
        }

        var type = typeElement.GetString();
        switch (type)
        {
            case BridgeMessageTypes.Hello:
            {
                var hello = JsonSerializer.Deserialize<HelloMessage>(message, SerializerOptions);
                if (hello is not null)
                {
                    session.Browser = hello.Browser;
                    session.Version = hello.Version;
                    UpdateSnapshot(snapshot => snapshot with
                    {
                        ExtensionConnected = true,
                        Browser = hello.Browser,
                        ExtensionVersion = hello.Version,
                        LastError = null
                    });
                }

                break;
            }
            case BridgeMessageTypes.Status:
            {
                var status = JsonSerializer.Deserialize<ExtensionStatusMessage>(message, SerializerOptions);
                if (status is not null)
                {
                    session.Browser = status.Browser ?? session.Browser;
                    session.Version = status.Version ?? session.Version;
                    UpdateSnapshot(snapshot => snapshot with
                    {
                        ExtensionConnected = true,
                        Browser = session.Browser,
                        ExtensionVersion = session.Version,
                        ConnectedTabs = status.ConnectedTabs,
                        SupportedTabs = status.SupportedTabs,
                        LastError = null
                    });
                }

                break;
            }
            case BridgeMessageTypes.MediaSnapshot:
            {
                var snapshotMessage = JsonSerializer.Deserialize<MediaSnapshotMessage>(message, SerializerOptions);
                if (snapshotMessage is not null)
                {
                    UpdateSnapshot(snapshot => snapshot with { LastMedia = snapshotMessage, LastError = null });
                }

                break;
            }
            case BridgeMessageTypes.Heartbeat:
            {
                await SendAsync(session.Socket, new HeartbeatMessage("desktop", DateTimeOffset.UtcNow), cancellationToken);
                break;
            }
        }
    }

    private async Task BroadcastHeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await BroadcastAsync(new HeartbeatMessage("desktop", DateTimeOffset.UtcNow), cancellationToken);
        }
    }

    private Task BroadcastAsync(BridgeMessage message, CancellationToken cancellationToken = default)
    {
        var tasks = _sessions.Values.Select(session => SendAsync(session.Socket, message, cancellationToken));
        return Task.WhenAll(tasks);
    }

    private static async Task SendAsync(WebSocket socket, BridgeMessage message, CancellationToken cancellationToken)
    {
        if (socket.State != WebSocketState.Open)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(message, SerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(payload);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private void UpdateSnapshot(Func<BridgeTelemetrySnapshot, BridgeTelemetrySnapshot> update)
    {
        lock (_sync)
        {
            _snapshot = update(_snapshot);
        }

        EmitTelemetry();
    }

    private void EmitTelemetry()
    {
        BridgeTelemetrySnapshot snapshot;
        lock (_sync)
        {
            snapshot = _snapshot;
        }

        TelemetryChanged?.Invoke(this, snapshot);
    }

    private sealed class ConnectedSession
    {
        public ConnectedSession(Guid id, WebSocket socket)
        {
            Id = id;
            Socket = socket;
        }

        public Guid Id { get; }

        public WebSocket Socket { get; }

        public string? Browser { get; set; }

        public string? Version { get; set; }
    }
}
