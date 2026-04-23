using CsLiveMute.Core.Models;

namespace CsLiveMute.Desktop.Models;

public sealed record RuntimeSnapshot(
    AppSettings Settings,
    string GsiConfig,
    bool CombatModeActive,
    bool GsiConnected,
    DateTimeOffset? LastGsiReceivedAt,
    string? LastRoundPhase,
    string? GsiMessage,
    MediaAutomationSnapshot? LastMedia,
    string? LastError);
