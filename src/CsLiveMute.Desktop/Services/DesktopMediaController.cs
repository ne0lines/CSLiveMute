using System.Diagnostics;
using System.Runtime.InteropServices;
using CsLiveMute.Core.Models;
using CsLiveMute.Desktop.Models;
using NAudio.CoreAudioApi;

namespace CsLiveMute.Desktop.Services;

public sealed class DesktopMediaController
{
    private static readonly HashSet<string> SupportedBrowsers = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome",
        "msedge",
        "firefox",
        "brave",
        "opera",
        "opera_gx"
    };

    private readonly object _sync = new();
    private List<MutedAudioSessionState> _mutedSessions = [];
    private PausedMediaSessionState? _pausedSession;
    private MediaControlMode? _activeMode;
    private const byte VkMediaPlayPause = 0xB3;
    private const uint KeyEventKeyUp = 0x0002;

    public async Task<MediaAutomationSnapshot> RefreshAsync(MediaControlMode mode, CancellationToken cancellationToken = default)
    {
        var candidate = await FindPreferredSessionAsync(cancellationToken);
        if (candidate is null)
        {
            return MediaAutomationSnapshot.CreateIdle(mode, "No supported browser media session detected yet.");
        }

        return CreateSnapshot(
            mode,
            candidate,
            action: "ready",
            changeApplied: false,
            detail: mode == MediaControlMode.StreamMute
                ? "This mode will mute browser audio during live rounds."
                : "This mode will pause the current browser media during live rounds.");
    }

    public async Task<MediaAutomationSnapshot> ApplyAsync(MediaControlMode mode, CancellationToken cancellationToken = default)
    {
        return mode switch
        {
            MediaControlMode.StreamMute => await ApplyStreamMuteAsync(cancellationToken),
            MediaControlMode.VideoPause => await ApplyVideoPauseAsync(cancellationToken),
            _ => MediaAutomationSnapshot.CreateIdle(mode, "Unsupported control mode.")
        };
    }

    public async Task<MediaAutomationSnapshot> RestoreAsync(MediaControlMode mode, CancellationToken cancellationToken = default)
    {
        MediaControlMode? activeMode;
        lock (_sync)
        {
            activeMode = _activeMode;
        }

        return activeMode switch
        {
            MediaControlMode.StreamMute => await RestoreStreamMuteAsync(mode, cancellationToken),
            MediaControlMode.VideoPause => await RestoreVideoPauseAsync(mode, cancellationToken),
            _ => (await RefreshAsync(mode, cancellationToken)) with
            {
                Detail = "Nothing to restore."
            }
        };
    }

    private async Task<MediaAutomationSnapshot> ApplyStreamMuteAsync(CancellationToken cancellationToken)
    {
        var candidate = await FindPreferredSessionAsync(cancellationToken);
        if (candidate is null)
        {
            lock (_sync)
            {
                _mutedSessions = [];
                _pausedSession = null;
                _activeMode = MediaControlMode.StreamMute;
            }

            return MediaAutomationSnapshot.CreateIdle(MediaControlMode.StreamMute, "Start a browser stream before the round goes live.");
        }

        var mutedSessions = MuteBrowserAudioSessions(candidate.ProcessName);
        lock (_sync)
        {
            _mutedSessions = mutedSessions;
            _pausedSession = null;
            _activeMode = MediaControlMode.StreamMute;
        }

        return CreateSnapshot(
            MediaControlMode.StreamMute,
            candidate,
            action: mutedSessions.Count > 0 ? "muted" : "already-muted",
            changeApplied: mutedSessions.Count > 0,
            detail: mutedSessions.Count > 0
                ? $"Muted {candidate.SourceApp} audio for this round."
                : $"{candidate.SourceApp} audio was already muted.");
    }

    private async Task<MediaAutomationSnapshot> RestoreStreamMuteAsync(MediaControlMode mode, CancellationToken cancellationToken)
    {
        List<MutedAudioSessionState> trackedSessions;
        lock (_sync)
        {
            trackedSessions = [.. _mutedSessions];
            _mutedSessions = [];
            _activeMode = null;
        }

        var restoredCount = RestoreMutedAudioSessions(trackedSessions);
        var candidate = await FindPreferredSessionAsync(cancellationToken);
        if (candidate is null)
        {
            return MediaAutomationSnapshot.CreateIdle(
                mode,
                restoredCount > 0
                    ? "Restored browser audio after the round."
                    : "No muted browser audio needed restoration.");
        }

        return CreateSnapshot(
            mode,
            candidate,
            action: restoredCount > 0 ? "unmuted" : "no-op",
            changeApplied: restoredCount > 0,
            detail: restoredCount > 0
                ? $"Restored {candidate.SourceApp} audio after the round."
                : $"No tracked {candidate.SourceApp} audio session needed restoration.");
    }

    private async Task<MediaAutomationSnapshot> ApplyVideoPauseAsync(CancellationToken cancellationToken)
    {
        var candidate = await FindPreferredSessionAsync(cancellationToken);
        if (candidate is null)
        {
            lock (_sync)
            {
                _pausedSession = null;
                _mutedSessions = [];
                _activeMode = MediaControlMode.VideoPause;
            }

            return MediaAutomationSnapshot.CreateIdle(MediaControlMode.VideoPause, "Start a browser video before the round goes live.");
        }

        if (!candidate.IsPlaying)
        {
            lock (_sync)
            {
                _pausedSession = new PausedMediaSessionState(candidate.ProcessName, candidate.SourceApp);
                _mutedSessions = [];
                _activeMode = MediaControlMode.VideoPause;
            }

            return CreateSnapshot(
                MediaControlMode.VideoPause,
                candidate,
                action: "already-paused",
                changeApplied: false,
                detail: $"{candidate.SourceApp} is already paused.");
        }

        SendPlayPauseKey();
        lock (_sync)
        {
            _pausedSession = new PausedMediaSessionState(candidate.ProcessName, candidate.SourceApp);
            _mutedSessions = [];
            _activeMode = MediaControlMode.VideoPause;
        }

        return CreateSnapshot(
            MediaControlMode.VideoPause,
            candidate,
            action: "paused",
            changeApplied: true,
            detail: $"Sent play/pause to {candidate.SourceApp} for the live round.");
    }

    private async Task<MediaAutomationSnapshot> RestoreVideoPauseAsync(MediaControlMode mode, CancellationToken cancellationToken)
    {
        PausedMediaSessionState? pausedSession;
        lock (_sync)
        {
            pausedSession = _pausedSession;
            _pausedSession = null;
            _activeMode = null;
        }

        if (pausedSession is null)
        {
            return (await RefreshAsync(mode, cancellationToken)) with
            {
                Detail = "Nothing to resume."
            };
        }

        var candidate = await FindMatchingSessionAsync(pausedSession, cancellationToken);

        if (candidate is null)
        {
            return MediaAutomationSnapshot.CreateIdle(mode, "The paused browser session is no longer available.");
        }

        if (candidate.IsPlaying)
        {
            return CreateSnapshot(
                mode,
                candidate,
                action: "already-playing",
                changeApplied: false,
                detail: $"{candidate.SourceApp} playback was already restored.");
        }

        SendPlayPauseKey();
        return CreateSnapshot(
            mode,
            candidate,
            action: "resumed",
            changeApplied: true,
            detail: $"Sent play/pause to {candidate.SourceApp} after the round.");
    }

    private static List<MutedAudioSessionState> MuteBrowserAudioSessions(string processName)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = device.AudioSessionManager.Sessions;
        var mutedSessions = new List<MutedAudioSessionState>();

        for (var index = 0; index < sessions.Count; index++)
        {
            var session = sessions[index];
            var pid = (int)session.GetProcessID;
            if (pid <= 0)
            {
                continue;
            }

            string sessionProcessName;
            try
            {
                sessionProcessName = Process.GetProcessById(pid).ProcessName;
            }
            catch
            {
                continue;
            }

            if (!string.Equals(sessionProcessName, processName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (session.SimpleAudioVolume.Mute)
            {
                continue;
            }

            session.SimpleAudioVolume.Mute = true;
            mutedSessions.Add(new MutedAudioSessionState(session.GetSessionInstanceIdentifier, pid, processName));
        }

        return mutedSessions;
    }

    private static int RestoreMutedAudioSessions(IReadOnlyCollection<MutedAudioSessionState> trackedSessions)
    {
        if (trackedSessions.Count == 0)
        {
            return 0;
        }

        var byId = trackedSessions
            .Where(item => !string.IsNullOrWhiteSpace(item.SessionInstanceId))
            .ToDictionary(item => item.SessionInstanceId!, StringComparer.OrdinalIgnoreCase);
        var byProcessId = trackedSessions
            .Where(item => string.IsNullOrWhiteSpace(item.SessionInstanceId))
            .Select(item => item.ProcessId)
            .ToHashSet();

        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = device.AudioSessionManager.Sessions;
        var restoredCount = 0;

        for (var index = 0; index < sessions.Count; index++)
        {
            var session = sessions[index];
            var pid = (int)session.GetProcessID;
            var instanceId = session.GetSessionInstanceIdentifier;
            var shouldRestore = (!string.IsNullOrWhiteSpace(instanceId) && byId.ContainsKey(instanceId))
                                || byProcessId.Contains(pid);

            if (!shouldRestore || !session.SimpleAudioVolume.Mute)
            {
                continue;
            }

            session.SimpleAudioVolume.Mute = false;
            restoredCount++;
        }

        return restoredCount;
    }

    private static Task<SessionCandidate?> FindPreferredSessionAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(FindPreferredBrowserAudioSession(null, cancellationToken));
    }

    private static Task<SessionCandidate?> FindMatchingSessionAsync(PausedMediaSessionState pausedSession, CancellationToken cancellationToken)
    {
        return Task.FromResult(FindPreferredBrowserAudioSession(pausedSession.ProcessName, cancellationToken));
    }

    private static SessionCandidate? FindPreferredBrowserAudioSession(string? preferredProcessName, CancellationToken cancellationToken)
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        var sessions = device.AudioSessionManager.Sessions;
        SessionCandidate? fallback = null;
        SessionCandidate? preferredFallback = null;

        for (var index = 0; index < sessions.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var session = sessions[index];
            var pid = (int)session.GetProcessID;
            if (pid <= 0)
            {
                continue;
            }

            string processName;
            string? title;
            try
            {
                var process = Process.GetProcessById(pid);
                processName = process.ProcessName;
                title = string.IsNullOrWhiteSpace(session.DisplayName) ? process.MainWindowTitle : session.DisplayName;
            }
            catch
            {
                continue;
            }

            if (!SupportedBrowsers.Contains(processName))
            {
                continue;
            }

            var candidate = new SessionCandidate(
                processName,
                FormatProcessName(processName),
                string.IsNullOrWhiteSpace(title) ? null : title,
                string.Equals(session.State.ToString(), "AudioSessionStateActive", StringComparison.OrdinalIgnoreCase),
                session.SimpleAudioVolume.Mute);

            if (candidate.IsPlaying && string.Equals(candidate.ProcessName, preferredProcessName, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }

            if (candidate.IsPlaying)
            {
                fallback ??= candidate;
            }

            if (string.Equals(candidate.ProcessName, preferredProcessName, StringComparison.OrdinalIgnoreCase))
            {
                preferredFallback ??= candidate;
            }

            fallback ??= candidate;
        }

        return preferredFallback ?? fallback;
    }

    private static string FormatProcessName(string processName)
    {
        return processName.ToLowerInvariant() switch
        {
            "chrome" => "Chrome",
            "msedge" => "Edge",
            "firefox" => "Firefox",
            "brave" => "Brave",
            "opera" => "Opera",
            "opera_gx" => "Opera GX",
            _ => processName
        };
    }

    private static MediaAutomationSnapshot CreateSnapshot(MediaControlMode mode, SessionCandidate candidate, string action, bool changeApplied, string detail)
    {
        return new MediaAutomationSnapshot(
            mode,
            true,
            candidate.SourceApp,
            string.IsNullOrWhiteSpace(candidate.Title) ? "Unknown title" : candidate.Title,
            candidate.IsPlaying,
            changeApplied,
            action,
            detail);
    }

    private static void SendPlayPauseKey()
    {
        keybd_event(VkMediaPlayPause, 0, 0, 0);
        keybd_event(VkMediaPlayPause, 0, KeyEventKeyUp, 0);
    }

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, nuint dwExtraInfo);

    private sealed record SessionCandidate(
        string ProcessName,
        string SourceApp,
        string? Title,
        bool IsPlaying,
        bool IsMuted);

    private sealed record PausedMediaSessionState(string ProcessName, string SourceApp);

    private sealed record MutedAudioSessionState(string? SessionInstanceId, int ProcessId, string ProcessName);
}
