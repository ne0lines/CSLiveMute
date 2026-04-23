using CsLiveMute.Core.Protocol;

namespace CsLiveMute.Desktop.Models;

public sealed record BridgeTelemetrySnapshot(
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
    string? LastError)
{
    public static BridgeTelemetrySnapshot Empty { get; } = new(
        false,
        false,
        null,
        null,
        "Waiting for CS2 GSI",
        false,
        null,
        null,
        0,
        0,
        null,
        null);
}
