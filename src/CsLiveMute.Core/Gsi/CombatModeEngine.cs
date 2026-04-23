using CsLiveMute.Core.Models;

namespace CsLiveMute.Core.Gsi;

public sealed class CombatModeEngine
{
    public bool IsActive { get; private set; }

    public CombatModeTransition Evaluate(bool serviceEnabled, string? roundPhase)
    {
        var nextState = serviceEnabled && string.Equals(roundPhase, "live", StringComparison.OrdinalIgnoreCase);
        if (nextState == IsActive)
        {
            return CombatModeTransition.Unchanged(IsActive, roundPhase);
        }

        IsActive = nextState;
        return new CombatModeTransition(true, IsActive, roundPhase);
    }
}
