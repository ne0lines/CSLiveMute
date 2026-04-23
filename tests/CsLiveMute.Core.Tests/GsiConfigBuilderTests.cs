using CsLiveMute.Core.Gsi;
using CsLiveMute.Core.Models;

namespace CsLiveMute.Core.Tests;

public sealed class GsiConfigBuilderTests
{
    [Fact]
    public void ConfigIncludesLocalEndpointAndToken()
    {
        var settings = new AppSettings
        {
            Enabled = true,
            Port = 3000,
            AuthToken = "test-token"
        };

        var config = GsiConfigBuilder.Build(settings);

        Assert.Contains("http://127.0.0.1:3000/gsi", config);
        Assert.Contains("\"token\"       \"test-token\"", config);
        Assert.Contains("\"round\"       \"1\"", config);
    }
}
