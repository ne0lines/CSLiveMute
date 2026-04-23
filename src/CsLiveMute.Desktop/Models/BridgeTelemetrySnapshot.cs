namespace CsLiveMute.Desktop.Models;

public sealed record BridgeTelemetrySnapshot(
    bool CombatModeActive,
    bool GsiConnected,
    DateTimeOffset? LastGsiReceivedAt,
    string? LastRoundPhase,
    string? GsiMessage,
    MediaAutomationSnapshot? LastMedia,
    string? LastError)
{
    public static BridgeTelemetrySnapshot Empty { get; } = new(
        false,
        false,
        null,
        null,
        "Waiting for CS2 GSI",
        null,
        null);
}
