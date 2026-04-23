using System.Text.Json.Serialization;

namespace CsLiveMute.Core.Protocol;

public abstract record BridgeMessage([property: JsonPropertyName("type")] string Type);

public sealed record HelloMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("browser")] string? Browser,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("active")] bool? Active = null)
    : BridgeMessage(BridgeMessageTypes.Hello);

public sealed record ExtensionStatusMessage(
    [property: JsonPropertyName("browser")] string? Browser,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("connectedTabs")] int ConnectedTabs,
    [property: JsonPropertyName("supportedTabs")] int SupportedTabs,
    [property: JsonPropertyName("combatModeActive")] bool CombatModeActive)
    : BridgeMessage(BridgeMessageTypes.Status);

public sealed record CombatModeChangedMessage(
    [property: JsonPropertyName("active")] bool Active,
    [property: JsonPropertyName("roundPhase")] string? RoundPhase,
    [property: JsonPropertyName("sentAt")] DateTimeOffset SentAt)
    : BridgeMessage(BridgeMessageTypes.CombatModeChanged);

public sealed record MediaSnapshotMessage(
    [property: JsonPropertyName("platform")] string? Platform,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("tabId")] int? TabId,
    [property: JsonPropertyName("siteKind")] string? SiteKind,
    [property: JsonPropertyName("isLive")] bool IsLive,
    [property: JsonPropertyName("isPlaying")] bool IsPlaying,
    [property: JsonPropertyName("action")] string? Action)
    : BridgeMessage(BridgeMessageTypes.MediaSnapshot);

public sealed record HeartbeatMessage(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("sentAt")] DateTimeOffset SentAt)
    : BridgeMessage(BridgeMessageTypes.Heartbeat);
