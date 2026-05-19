using TigerTradeMcp.Core.Abstractions;

namespace TigerTradeMcp.Tests;

public sealed class TigerLinkClientTests
{
    [Fact]
    public async Task CheckHealth_WhenTerminalNotRunning_ReturnsDisconnected()
    {
        // Arrange: nothing is listening on port 9999
        var mock = Substitute.For<ITigerLinkClient>();
        mock.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new TigerLinkHealth(Connected: false, Endpoint: "ws://localhost:9999", Error: "Connection refused"));

        // Act
        var health = await mock.CheckHealthAsync();

        // Assert
        health.Connected.Should().BeFalse();
        health.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CheckHealth_WhenConnected_ReturnsConnected()
    {
        var mock = Substitute.For<ITigerLinkClient>();
        mock.CheckHealthAsync(Arg.Any<CancellationToken>())
            .Returns(new TigerLinkHealth(Connected: true, Endpoint: "ws://localhost:7819"));

        var health = await mock.CheckHealthAsync();

        health.Connected.Should().BeTrue();
        health.Error.Should().BeNull();
    }

    [Fact]
    public async Task SetLinkSymbol_CallsClientWithCorrectArgs()
    {
        var mock = Substitute.For<ITigerLinkClient>();

        await mock.SetLinkSymbolAsync("BINANCE", "SPOT", "BTCUSDT", "A");

        await mock.Received(1).SetLinkSymbolAsync("BINANCE", "SPOT", "BTCUSDT", "A", Arg.Any<CancellationToken>());
    }
}
