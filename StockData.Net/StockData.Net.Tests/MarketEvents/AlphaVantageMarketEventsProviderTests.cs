using Moq;
using StockData.Net.Clients.AlphaVantage;
using StockData.Net.Models.Events;
using StockData.Net.Providers;

namespace StockData.Net.Tests.MarketEvents;

[TestClass]
public class AlphaVantageMarketEventsProviderTests
{
    [TestMethod]
    public async Task GivenNewsSentimentItems_WhenMapping_ThenCreatesBreakingAlphaVantageEvents()
    {
        var client = new Mock<IAlphaVantageClient>();
        client.Setup(c => c.GetMacroNewsSentimentAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AlphaVantageMacroNewsItem(
                    "Fed policy update",
                    "Alpha",
                    "https://example.com/a",
                    "Summary",
                    "20260412T180000",
                    "Bullish",
                    ["ECONOMY_MONETARY"],
                    ["SPY", "TLT"])
            ]);

        var provider = new AlphaVantageMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<AlphaVantageMarketEventsProvider>.Instance);

        var result = await provider.GetEventsAsync(new MarketEventsQuery
        {
            EventType = EventType.Breaking,
            FromDate = new DateOnly(2026, 4, 10),
            ToDate = new DateOnly(2026, 4, 13)
        });

        Assert.HasCount(1, result);
        Assert.AreEqual("breaking", result[0].EventType);
        Assert.AreEqual("AlphaVantage", result[0].Source);
        Assert.AreEqual("positive", result[0].Sentiment);
        Assert.AreEqual("fed", result[0].Category);
        Assert.AreEqual("high", result[0].ImpactLevel);
    }

    [TestMethod]
    public async Task GivenSentimentValues_WhenMapping_ThenMapsPositiveNegativeNeutral()
    {
        var client = new Mock<IAlphaVantageClient>();
        client.Setup(c => c.GetMacroNewsSentimentAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AlphaVantageMacroNewsItem("A", "Alpha", "https://example.com/1", "", "20260412T180000", "Bullish", ["ECONOMY_MACRO"], []),
                new AlphaVantageMacroNewsItem("B", "Alpha", "https://example.com/2", "", "20260412T180000", "Bearish", ["ECONOMY_MACRO"], []),
                new AlphaVantageMacroNewsItem("C", "Alpha", "https://example.com/3", "", "20260412T180000", "Neutral", ["ECONOMY_MACRO"], [])
            ]);

        var provider = new AlphaVantageMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<AlphaVantageMarketEventsProvider>.Instance);
        var result = await provider.GetEventsAsync(new MarketEventsQuery { EventType = EventType.Breaking, FromDate = new DateOnly(2026, 4, 12), ToDate = new DateOnly(2026, 4, 12) });

        CollectionAssert.AreEqual(new[] { "positive", "negative", "neutral" }, result.Select(item => item.Sentiment).ToArray());
    }

    [TestMethod]
    public async Task GivenMissingSentiment_WhenMapping_ThenKeepsNullSentiment()
    {
        var client = new Mock<IAlphaVantageClient>();
        client.Setup(c => c.GetMacroNewsSentimentAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AlphaVantageMacroNewsItem("A", "Alpha", "https://example.com/1", "", "20260412T180000", null, ["ECONOMY_MACRO"], [])
            ]);

        var provider = new AlphaVantageMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<AlphaVantageMarketEventsProvider>.Instance);
        var result = await provider.GetEventsAsync(new MarketEventsQuery { EventType = EventType.Breaking, FromDate = new DateOnly(2026, 4, 12), ToDate = new DateOnly(2026, 4, 12) });

        Assert.IsNull(result[0].Sentiment);
        Assert.IsNull(result[0].ImpactLevel);
    }

    [TestMethod]
    [DataRow("Bullish", "high")]
    [DataRow("Bearish", "high")]
    [DataRow("Somewhat-Bullish", "medium")]
    [DataRow("Somewhat-Bearish", "medium")]
    [DataRow("Neutral", "low")]
    public async Task GivenSentimentLabel_WhenMapping_ThenInfersImpactLevel(string sentiment, string expectedImpact)
    {
        var client = new Mock<IAlphaVantageClient>();
        client.Setup(c => c.GetMacroNewsSentimentAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new AlphaVantageMacroNewsItem("Event", "Alpha", "https://example.com/impact", "", "20260412T180000", sentiment, ["ECONOMY_MACRO"], [])
            ]);

        var provider = new AlphaVantageMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<AlphaVantageMarketEventsProvider>.Instance);
        var result = await provider.GetEventsAsync(new MarketEventsQuery
        {
            EventType = EventType.Breaking,
            FromDate = new DateOnly(2026, 4, 12),
            ToDate = new DateOnly(2026, 4, 12)
        });

        Assert.AreEqual(expectedImpact, result[0].ImpactLevel);
    }
}
