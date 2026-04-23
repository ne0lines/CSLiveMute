using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CsLiveMute.Core.Protocol;
using CsLiveMute.Desktop.Infrastructure;
using CsLiveMute.Desktop.Models;
using CsLiveMute.Desktop.Services;

namespace CsLiveMute.Desktop.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private static readonly Brush BrushGreen = CreateBrush("#22C55E");
    private static readonly Brush BrushIndigo = CreateBrush("#4C55E8");
    private static readonly Brush BrushAmber = CreateBrush("#F59E0B");
    private static readonly Brush BrushSlate = CreateBrush("#64748B");
    private static readonly Brush BrushInk = CreateBrush("#121D30");

    private readonly AppRuntime _runtime;
    private RuntimeSnapshot _snapshot;
    private bool _suppressToggleUpdate;
    private bool _isServiceEnabled;
    private Brush _heroDotBrush = BrushAmber;
    private Brush _gameStatusBrush = BrushAmber;
    private Brush _browserStatusBrush = BrushAmber;
    private Brush _mediaStatusBrush = BrushSlate;
    private string _heroTitle = "Service Active";
    private string _heroSubtitle = "Waiting for CS2 events";
    private string _gameStatusPrimary = "Waiting for CS2 GSI";
    private string _gameStatusSecondary = "No authenticated payload received yet";
    private string _browserStatusPrimary = "Waiting for Chromium extension";
    private string _browserStatusSecondary = "Load the unpacked extension to sync tabs";
    private string _mediaStatusPrimary = "No supported media detected yet";
    private string _mediaStatusSecondary = "YouTube, Twitch and Kick will report here";
    private string _maskedAuthToken = "••••••••";
    private string _gsiConfigSnippet = string.Empty;
    private string _footerPrimary = "Local bridge is ready";
    private string _footerSecondary = "POST CS2 GSI to http://127.0.0.1:3000/gsi and connect the extension to ws://127.0.0.1:3000/bridge";
    private string _roundStateBadge = "ROUND · IDLE";
    private int _port = 3000;

    public MainWindowViewModel(AppRuntime runtime)
    {
        _runtime = runtime;
        _snapshot = runtime.CurrentSnapshot;
        _runtime.SnapshotChanged += OnSnapshotChanged;

        CopyTokenCommand = new RelayCommand(_ => Clipboard.SetText(_snapshot.Settings.AuthToken));
        CopyConfigCommand = new RelayCommand(_ => Clipboard.SetText(_snapshot.GsiConfig));
        RegenerateTokenCommand = new RelayCommand(async _ => await _runtime.RegenerateTokenAsync());

        ApplySnapshot(_snapshot);
    }

    public ICommand CopyTokenCommand { get; }

    public ICommand CopyConfigCommand { get; }

    public ICommand RegenerateTokenCommand { get; }

    public bool IsServiceEnabled
    {
        get => _isServiceEnabled;
        set
        {
            if (SetProperty(ref _isServiceEnabled, value) && !_suppressToggleUpdate)
            {
                _ = _runtime.SetEnabledAsync(value);
            }
        }
    }

    public Brush HeroDotBrush
    {
        get => _heroDotBrush;
        private set => SetProperty(ref _heroDotBrush, value);
    }

    public Brush GameStatusBrush
    {
        get => _gameStatusBrush;
        private set => SetProperty(ref _gameStatusBrush, value);
    }

    public Brush BrowserStatusBrush
    {
        get => _browserStatusBrush;
        private set => SetProperty(ref _browserStatusBrush, value);
    }

    public Brush MediaStatusBrush
    {
        get => _mediaStatusBrush;
        private set => SetProperty(ref _mediaStatusBrush, value);
    }

    public string HeroTitle
    {
        get => _heroTitle;
        private set => SetProperty(ref _heroTitle, value);
    }

    public string HeroSubtitle
    {
        get => _heroSubtitle;
        private set => SetProperty(ref _heroSubtitle, value);
    }

    public string GameStatusPrimary
    {
        get => _gameStatusPrimary;
        private set => SetProperty(ref _gameStatusPrimary, value);
    }

    public string GameStatusSecondary
    {
        get => _gameStatusSecondary;
        private set => SetProperty(ref _gameStatusSecondary, value);
    }

    public string BrowserStatusPrimary
    {
        get => _browserStatusPrimary;
        private set => SetProperty(ref _browserStatusPrimary, value);
    }

    public string BrowserStatusSecondary
    {
        get => _browserStatusSecondary;
        private set => SetProperty(ref _browserStatusSecondary, value);
    }

    public string MediaStatusPrimary
    {
        get => _mediaStatusPrimary;
        private set => SetProperty(ref _mediaStatusPrimary, value);
    }

    public string MediaStatusSecondary
    {
        get => _mediaStatusSecondary;
        private set => SetProperty(ref _mediaStatusSecondary, value);
    }

    public string MaskedAuthToken
    {
        get => _maskedAuthToken;
        private set => SetProperty(ref _maskedAuthToken, value);
    }

    public string GsiConfigSnippet
    {
        get => _gsiConfigSnippet;
        private set => SetProperty(ref _gsiConfigSnippet, value);
    }

    public string FooterPrimary
    {
        get => _footerPrimary;
        private set => SetProperty(ref _footerPrimary, value);
    }

    public string FooterSecondary
    {
        get => _footerSecondary;
        private set => SetProperty(ref _footerSecondary, value);
    }

    public string RoundStateBadge
    {
        get => _roundStateBadge;
        private set => SetProperty(ref _roundStateBadge, value);
    }

    public int Port
    {
        get => _port;
        private set => SetProperty(ref _port, value);
    }

    public void Dispose()
    {
        _runtime.SnapshotChanged -= OnSnapshotChanged;
    }

    private void OnSnapshotChanged(object? sender, RuntimeSnapshot snapshot)
    {
        Application.Current.Dispatcher.Invoke(() => ApplySnapshot(snapshot));
    }

    private void ApplySnapshot(RuntimeSnapshot snapshot)
    {
        _snapshot = snapshot;
        _suppressToggleUpdate = true;
        IsServiceEnabled = snapshot.Settings.Enabled;
        _suppressToggleUpdate = false;

        Port = snapshot.Settings.Port;
        MaskedAuthToken = MaskToken(snapshot.Settings.AuthToken);
        GsiConfigSnippet = snapshot.GsiConfig;
        RoundStateBadge = $"ROUND · {FormatPhase(snapshot.LastRoundPhase)}";

        if (!snapshot.Settings.Enabled)
        {
            HeroTitle = "Service Paused";
            HeroSubtitle = "Automation is currently disabled";
            HeroDotBrush = BrushSlate;
        }
        else if (snapshot.CombatModeActive)
        {
            HeroTitle = "Round Live";
            HeroSubtitle = "Streams muted and VOD playback paused";
            HeroDotBrush = BrushIndigo;
        }
        else if (snapshot.GsiConnected)
        {
            HeroTitle = "Service Active";
            HeroSubtitle = "Listening for the next live round";
            HeroDotBrush = BrushGreen;
        }
        else
        {
            HeroTitle = "Service Active";
            HeroSubtitle = "Waiting for CS2 events";
            HeroDotBrush = BrushAmber;
        }

        GameStatusPrimary = snapshot.GsiConnected
            ? $"Connected to CS2 ({FormatPhase(snapshot.LastRoundPhase)})"
            : "Waiting for CS2 GSI";
        GameStatusSecondary = snapshot.LastError is not null && !snapshot.GsiConnected
            ? snapshot.LastError
            : snapshot.LastGsiReceivedAt is not null
                ? $"Last event {snapshot.LastGsiReceivedAt.Value.LocalDateTime:T}"
                : "No authenticated payload received yet";
        GameStatusBrush = snapshot.GsiConnected ? BrushGreen : BrushAmber;

        BrowserStatusPrimary = snapshot.ExtensionConnected
            ? $"{snapshot.Browser ?? "Chromium"} bridge connected"
            : "Waiting for Chromium extension";
        BrowserStatusSecondary = snapshot.ExtensionConnected
            ? $"{snapshot.ConnectedTabs} active tabs across {snapshot.SupportedTabs} supported tabs"
            : "Load the unpacked extension in Chrome, Edge or Brave";
        BrowserStatusBrush = snapshot.ExtensionConnected ? BrushGreen : BrushAmber;

        var lastMedia = snapshot.LastMedia;
        if (lastMedia is null)
        {
            MediaStatusPrimary = "No supported media detected yet";
            MediaStatusSecondary = "YouTube, Twitch and Kick snapshots will appear here";
            MediaStatusBrush = BrushSlate;
        }
        else
        {
            MediaStatusPrimary = FormatMediaPrimary(lastMedia);
            MediaStatusSecondary = lastMedia.Title ?? "No title reported";
            MediaStatusBrush = lastMedia.IsPlaying ? BrushGreen : BrushInk;
        }

        FooterPrimary = snapshot.CombatModeActive
            ? "Combat mode is active"
            : snapshot.ExtensionConnected
                ? "Bridge is synced with the browser"
                : "Local bridge is ready";
        FooterSecondary = snapshot.CombatModeActive
            ? "Only media that the extension changed will be restored after the round ends."
            : snapshot.ExtensionConnected
                ? "The extension watches supported tabs and reports live/VOD state back to the desktop app."
                : "POST CS2 GSI to http://127.0.0.1:3000/gsi and connect the extension to ws://127.0.0.1:3000/bridge";
    }

    private static string FormatMediaPrimary(MediaSnapshotMessage snapshot)
    {
        var platform = snapshot.Platform ?? "Browser media";
        if (snapshot.IsLive)
        {
            return $"{platform} live stream ({snapshot.Action ?? "mute"})";
        }

        return $"{platform} video ({snapshot.Action ?? "pause"})";
    }

    private static string FormatPhase(string? phase)
    {
        if (string.IsNullOrWhiteSpace(phase))
        {
            return "IDLE";
        }

        return phase.Replace('_', ' ').ToUpperInvariant();
    }

    private static string MaskToken(string token)
    {
        if (token.Length <= 8)
        {
            return token;
        }

        return $"{new string('•', Math.Max(0, token.Length - 6))}{token[^6..]}";
    }

    private static Brush CreateBrush(string value)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        brush.Freeze();
        return brush;
    }
}
