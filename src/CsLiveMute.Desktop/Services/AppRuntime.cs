using CsLiveMute.Core.Gsi;
using CsLiveMute.Core.Models;
using CsLiveMute.Desktop.Infrastructure;
using CsLiveMute.Desktop.Models;

namespace CsLiveMute.Desktop.Services;

public sealed class AppRuntime : IAsyncDisposable
{
    private readonly AppSettingsStore _settingsStore;
    private AppSettings _settings = AppSettings.CreateDefault();
    private BridgeTelemetrySnapshot _bridgeSnapshot = BridgeTelemetrySnapshot.Empty;
    private LocalBridgeHost? _host;

    public AppRuntime(AppSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        CurrentSnapshot = BuildSnapshot();
    }

    public event EventHandler<RuntimeSnapshot>? SnapshotChanged;

    public RuntimeSnapshot CurrentSnapshot { get; private set; }

    public async Task InitializeAsync()
    {
        _settings = await _settingsStore.LoadAsync();
        _host = new LocalBridgeHost(_settings);
        _host.TelemetryChanged += OnTelemetryChanged;
        await _host.StartAsync();
        PublishSnapshot();
    }

    public async Task SetEnabledAsync(bool enabled)
    {
        if (_settings.Enabled == enabled)
        {
            return;
        }

        _settings = _settings with { Enabled = enabled };
        await _settingsStore.SaveAsync(_settings);
        _host?.UpdateSettings(_settings);
        PublishSnapshot();
    }

    public async Task RegenerateTokenAsync()
    {
        _settings = _settings with { AuthToken = AuthTokenGenerator.Create() };
        await _settingsStore.SaveAsync(_settings);
        _host?.UpdateSettings(_settings);
        PublishSnapshot();
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is not null)
        {
            _host.TelemetryChanged -= OnTelemetryChanged;
            await _host.DisposeAsync();
        }
    }

    private void OnTelemetryChanged(object? sender, BridgeTelemetrySnapshot snapshot)
    {
        _bridgeSnapshot = snapshot;
        PublishSnapshot();
    }

    private void PublishSnapshot()
    {
        CurrentSnapshot = BuildSnapshot();
        SnapshotChanged?.Invoke(this, CurrentSnapshot);
    }

    private RuntimeSnapshot BuildSnapshot()
    {
        return new RuntimeSnapshot(
            _settings,
            GsiConfigBuilder.Build(_settings),
            _bridgeSnapshot.CombatModeActive,
            _bridgeSnapshot.GsiConnected,
            _bridgeSnapshot.LastGsiReceivedAt,
            _bridgeSnapshot.LastRoundPhase,
            _bridgeSnapshot.GsiMessage,
            _bridgeSnapshot.ExtensionConnected,
            _bridgeSnapshot.Browser,
            _bridgeSnapshot.ExtensionVersion,
            _bridgeSnapshot.ConnectedTabs,
            _bridgeSnapshot.SupportedTabs,
            _bridgeSnapshot.LastMedia,
            _bridgeSnapshot.LastError);
    }
}
