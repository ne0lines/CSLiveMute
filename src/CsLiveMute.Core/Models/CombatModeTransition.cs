namespace CsLiveMute.Core.Models;

public sealed record CombatModeTransition(bool Changed, bool IsActive, string? RoundPhase)
{
    public static CombatModeTransition Unchanged(bool isActive, string? roundPhase)
    {
        return new CombatModeTransition(false, isActive, roundPhase);
    }
}
