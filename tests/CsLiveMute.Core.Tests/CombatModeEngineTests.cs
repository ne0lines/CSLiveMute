using CsLiveMute.Core.Gsi;

namespace CsLiveMute.Core.Tests;

public sealed class CombatModeEngineTests
{
    [Fact]
    public void LiveRoundActivatesCombatMode()
    {
        var engine = new CombatModeEngine();

        var transition = engine.Evaluate(true, "live");

        Assert.True(transition.Changed);
        Assert.True(transition.IsActive);
    }

    [Fact]
    public void RepeatedLiveRoundDoesNotRetriggerStateChange()
    {
        var engine = new CombatModeEngine();
        engine.Evaluate(true, "live");

        var transition = engine.Evaluate(true, "live");

        Assert.False(transition.Changed);
        Assert.True(transition.IsActive);
    }

    [Fact]
    public void LeavingLiveRoundDisablesCombatMode()
    {
        var engine = new CombatModeEngine();
        engine.Evaluate(true, "live");

        var transition = engine.Evaluate(true, "freezetime");

        Assert.True(transition.Changed);
        Assert.False(transition.IsActive);
    }

    [Fact]
    public void DisabledServiceKeepsCombatModeOff()
    {
        var engine = new CombatModeEngine();

        var transition = engine.Evaluate(false, "live");

        Assert.False(transition.Changed);
        Assert.False(transition.IsActive);
    }
}
