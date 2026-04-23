using CsLiveMute.Core.Gsi;

namespace CsLiveMute.Core.Tests;

public sealed class GsiPayloadParserTests
{
    [Fact]
    public void ValidPayloadParsesRoundState()
    {
        const string payload = """
        {
          "provider": { "name": "Counter-Strike 2" },
          "auth": { "token": "expected-token" },
          "round": { "phase": "live" },
          "map": { "phase": "live" }
        }
        """;

        var parsed = GsiPayloadParser.TryParse(payload, "expected-token", out var state, out var error);

        Assert.True(parsed);
        Assert.Null(error);
        Assert.True(state.IsAuthenticated);
        Assert.Equal("live", state.RoundPhase);
        Assert.Equal("Counter-Strike 2", state.ProviderName);
    }

    [Fact]
    public void AuthMismatchReturnsParsedButUnauthenticatedState()
    {
        const string payload = """
        {
          "auth": { "token": "wrong-token" },
          "round": { "phase": "live" }
        }
        """;

        var parsed = GsiPayloadParser.TryParse(payload, "expected-token", out var state, out var error);

        Assert.True(parsed);
        Assert.False(state.IsAuthenticated);
        Assert.NotNull(error);
    }

    [Fact]
    public void InvalidJsonReturnsParserFailure()
    {
        const string payload = "{ invalid json";

        var parsed = GsiPayloadParser.TryParse(payload, "expected-token", out _, out var error);

        Assert.False(parsed);
        Assert.NotNull(error);
    }
}
