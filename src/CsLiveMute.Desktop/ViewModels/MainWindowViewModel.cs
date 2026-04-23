using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CsLiveMute.Core.Models;
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
    private string _modeStatusPrimary = "Stream mode selected";
    private string _modeStatusSecondary = "Mute browser audio during live rounds.";
    private string _mediaStatusPrimary = "No supported media detected yet";
    private string _mediaStatusSecondary = "Start a browser video or stream to let the app control it.";
    private string _maskedAuthToken = "••••••••";
    private string _gsiConfigSnippet = string.Empty;
    private string _footerPrimary = "Local bridge is ready";
    private string _footerSecondary = "POST CS2 GSI to http://127.0.0.1:3000/gsi and choose Stream or Video before you play.";
    private string _roundStateBadge = "ROUND · IDLE";
    private int _port = 3000;
    private bool _suppressModeUpdate;
    private bool _isStreamModeSelected = true;
    private bool _isVideoModeSelected;

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

    public Brush ModeStatusBrush
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

    public string ModeStatusPrimary
    {
        get => _modeStatusPrimary;
        private set => SetProperty(ref _modeStatusPrimary, value);
    }

    public string ModeStatusSecondary
    {
        get => _modeStatusSecondary;
        private set => SetProperty(ref _modeStatusSecondary, value);
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

    public bool IsStreamModeSelected
    {
        get => _isStreamModeSelected;
        set
        {
            if (!SetProperty(ref _isStreamModeSelected, value) || _suppressModeUpdate || !value)
            {
                return;
            }

            _suppressModeUpdate = true;
            IsVideoModeSelected = false;
            _suppressModeUpdate = false;
            _ = _runtime.SetControlModeAsync(MediaControlMode.StreamMute);
        }
    }

    public bool IsVideoModeSelected
    {
        get => _isVideoModeSelected;
        set
        {
            if (!SetProperty(ref _isVideoModeSelected, value) || _suppressModeUpdate || !value)
            {
                return;
            }

            _suppressModeUpdate = true;
            IsStreamModeSelected = false;
            _suppressModeUpdate = false;
            _ = _runtime.SetControlModeAsync(MediaControlMode.VideoPause);
        }
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
        _suppressModeUpdate = true;
        IsStreamModeSelected = snapshot.Settings.ControlMode == MediaControlMode.StreamMute;
        IsVideoModeSelected = snapshot.Settings.ControlMode == MediaControlMode.VideoPause;
        _suppressModeUpdate = false;

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
            HeroSubtitle = snapshot.Settings.ControlMode == MediaControlMode.StreamMute
                ? "Current browser audio is muted for the live round"
                : "Current browser media is paused for the live round";
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

        ModeStatusPrimary = $"{snapshot.Settings.ControlMode.ToDisplayName()} mode selected";
        ModeStatusSecondary = snapshot.Settings.ControlMode == MediaControlMode.StreamMute
            ? "Use this for Twitch, Kick or any live stream."
            : "Use this for YouTube VOD or other normal videos.";
        ModeStatusBrush = snapshot.Settings.Enabled ? BrushGreen : BrushSlate;

        var lastMedia = snapshot.LastMedia;
        if (lastMedia is null || !lastMedia.SessionDetected)
        {
            MediaStatusPrimary = "No supported browser media detected";
            MediaStatusSecondary = lastMedia?.Detail ?? "Start Chrome, Edge, Brave or Firefox media to let the app control it.";
            MediaStatusBrush = BrushSlate;
        }
        else
        {
            MediaStatusPrimary = FormatMediaPrimary(lastMedia);
            MediaStatusSecondary = lastMedia.Detail ?? lastMedia.Title ?? "No title reported";
            MediaStatusBrush = lastMedia.ChangeApplied ? BrushGreen : lastMedia.IsPlaying ? BrushInk : BrushSlate;
        }

        FooterPrimary = snapshot.CombatModeActive
            ? "Combat mode is active"
            : "Local control is ready";
        FooterSecondary = snapshot.CombatModeActive
            ? snapshot.Settings.ControlMode == MediaControlMode.StreamMute
                ? "The app will restore only the browser audio sessions it muted itself."
                : "The app will only resume media if it paused it itself."
            : "POST CS2 GSI to http://127.0.0.1:3000/gsi and choose Stream or Video before you play.";
    }

    private static string FormatMediaPrimary(MediaAutomationSnapshot snapshot)
    {
        var platform = snapshot.SourceApp ?? "Browser media";
        return snapshot.Action switch
        {
            "muted" => $"{platform} audio muted",
            "already-muted" => $"{platform} audio already muted",
            "unmuted" => $"{platform} audio restored",
            "paused" => $"{platform} playback paused",
            "already-paused" => $"{platform} playback already paused",
            "resumed" => $"{platform} playback resumed",
            "already-playing" => $"{platform} playback already restored",
            "ready" => $"{platform} session ready",
            _ => $"{platform} session detected"
        };
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
