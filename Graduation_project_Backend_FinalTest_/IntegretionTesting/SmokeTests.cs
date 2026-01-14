using System.Net;
using FluentAssertions;
using Xunit;

[Collection("db")]
public class SmokeTests
{
    private readonly DatabaseFixture _fx;

    public SmokeTests(DatabaseFixture fx) => _fx = fx;

    [Fact]
    public async Task UnknownRoute_Returns404()
    {
        await _fx.ResetAsync();

        var res = await _fx.Client.GetAsync("/route-not-exist");
        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
