using System.IO;
using System.Net;
using CsLiveMute.Core.Gsi;
using CsLiveMute.Core.Models;
using CsLiveMute.Desktop.Models;

namespace CsLiveMute.Desktop.Services;

public sealed class LocalBridgeHost : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CombatModeEngine _combatModeEngine = new();
    private readonly DesktopMediaController _mediaController = new();
    private readonly object _sync = new();

    private CancellationTokenSource? _lifetime;
    private Task? _listenLoop;
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
        await Task.Yield();
        EmitTelemetry();
        _ = RefreshMediaSnapshotAsync();
    }

    public void UpdateSettings(AppSettings settings)
    {
        CombatModeTransition transition;
        var previousSettings = _settings;
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
        if (previousSettings.ControlMode != settings.ControlMode && _snapshot.CombatModeActive)
        {
            _ = SwitchControlModeAsync(previousSettings.ControlMode, settings.ControlMode);
        }
        else if (transition.Changed)
        {
            _ = ApplyCombatModeTransitionAsync(transition);
        }
        else
        {
            _ = RefreshMediaSnapshotAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_lifetime is not null)
        {
            _lifetime.Cancel();
        }

        _listener.Close();

        if (_listenLoop is not null)
        {
            await _listenLoop;
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
            await ApplyCombatModeTransitionAsync(transition);
        }
        else
        {
            await RefreshMediaSnapshotAsync();
        }

        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.Close();
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

    private async Task ApplyCombatModeTransitionAsync(CombatModeTransition transition)
    {
        try
        {
            var mediaSnapshot = transition.IsActive
                ? await _mediaController.ApplyAsync(_settings.ControlMode)
                : await _mediaController.RestoreAsync(_settings.ControlMode);

            UpdateSnapshot(snapshot => snapshot with
            {
                LastMedia = mediaSnapshot,
                LastError = null
            });
        }
        catch (Exception exception)
        {
            UpdateSnapshot(snapshot => snapshot with { LastError = exception.Message });
        }
    }

    private async Task SwitchControlModeAsync(MediaControlMode previousMode, MediaControlMode nextMode)
    {
        try
        {
            await _mediaController.RestoreAsync(previousMode);
            var mediaSnapshot = await _mediaController.ApplyAsync(nextMode);
            UpdateSnapshot(snapshot => snapshot with
            {
                LastMedia = mediaSnapshot,
                LastError = null
            });
        }
        catch (Exception exception)
        {
            UpdateSnapshot(snapshot => snapshot with { LastError = exception.Message });
        }
    }

    private async Task RefreshMediaSnapshotAsync()
    {
        try
        {
            var mediaSnapshot = await _mediaController.RefreshAsync(_settings.ControlMode);
            UpdateSnapshot(snapshot => snapshot with
            {
                LastMedia = mediaSnapshot,
                LastError = snapshot.LastError
            });
        }
        catch (Exception exception)
        {
            UpdateSnapshot(snapshot => snapshot with { LastError = exception.Message });
        }
    }
}
