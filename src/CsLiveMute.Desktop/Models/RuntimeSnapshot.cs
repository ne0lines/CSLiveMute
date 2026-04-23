using CsLiveMute.Core.Models;
using CsLiveMute.Core.Protocol;

namespace CsLiveMute.Desktop.Models;

public sealed record RuntimeSnapshot(
    AppSettings Settings,
    string GsiConfig,
    bool CombatModeActive,
    bool GsiConnected,
    DateTimeOffset? LastGsiReceivedAt,
    string? LastRoundPhase,
    string? GsiMessage,
    bool ExtensionConnected,
    string? Browser,
    string? ExtensionVersion,
    int ConnectedTabs,
    int SupportedTabs,
    MediaSnapshotMessage? LastMedia,
    string? LastError);
