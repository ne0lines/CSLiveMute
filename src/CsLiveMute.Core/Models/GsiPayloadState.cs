namespace CsLiveMute.Core.Models;

public sealed record GsiPayloadState(
    bool IsAuthenticated,
    string? Token,
    string? ProviderName,
    string? MapPhase,
    string? RoundPhase,
    string RawPayload)
{
    public bool IsRoundLive => string.Equals(RoundPhase, "live", StringComparison.OrdinalIgnoreCase);
}
