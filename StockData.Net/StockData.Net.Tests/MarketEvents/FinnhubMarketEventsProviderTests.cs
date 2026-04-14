using Moq;
using StockData.Net.Clients.Finnhub;
using StockData.Net.Models.Events;
using StockData.Net.Providers;

namespace StockData.Net.Tests.MarketEvents;

[TestClass]
public class FinnhubMarketEventsProviderTests
{
    [TestMethod]
    public async Task GivenEconomicCalendarItems_WhenMapping_ThenCreatesScheduledFinnhubEvents()
    {
        var client = new Mock<IFinnhubClient>();
        client.Setup(c => c.GetEconomicCalendarAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new FinnhubEconomicCalendarEvent("2026-04-12", "FOMC Rate Decision", "18:00", 3, "US", null, null, null, null)
            ]);
        client.Setup(c => c.GetMarketNewsAsync("general", It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<MarketNewsItem>());

        var provider = new FinnhubMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinnhubMarketEventsProvider>.Instance);

        var result = await provider.GetEventsAsync(new MarketEventsQuery
        {
            EventType = EventType.Scheduled,
            FromDate = new DateOnly(2026, 4, 12),
            ToDate = new DateOnly(2026, 4, 12)
        });

        Assert.HasCount(1, result);
        Assert.AreEqual("scheduled", result[0].EventType);
        Assert.AreEqual("Finnhub", result[0].Source);
        Assert.AreEqual("high", result[0].ImpactLevel);
        Assert.AreEqual("fed", result[0].Category);
        Assert.IsNotNull(result[0].EventId, "EventId must not be null");
        Assert.IsFalse(string.IsNullOrEmpty(result[0].EventId), "EventId must not be empty");
        Assert.IsNotNull(result[0].Title, "Title must not be null");
        Assert.IsFalse(string.IsNullOrEmpty(result[0].Title), "Title must not be empty");
        Assert.IsNotNull(result[0].AffectedMarkets, "AffectedMarkets must not be null");
        Assert.AreEqual(TimeSpan.Zero, result[0].EventTime.Offset);
    }

    [TestMethod]
    public async Task GivenImpactValuesOneTwoThree_WhenMapping_ThenMapsLowMediumHigh()
    {
        var client = new Mock<IFinnhubClient>();
        client.Setup(c => c.GetEconomicCalendarAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new FinnhubEconomicCalendarEvent("2026-04-12", "Event 1", "09:00", 1, "US", null, null, null, null),
                new FinnhubEconomicCalendarEvent("2026-04-12", "Event 2", "10:00", 2, "US", null, null, null, null),
                new FinnhubEconomicCalendarEvent("2026-04-12", "Event 3", "11:00", 3, "US", null, null, null, null)
            ]);

        var provider = new FinnhubMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinnhubMarketEventsProvider>.Instance);
        var result = await provider.GetEventsAsync(new MarketEventsQuery { EventType = EventType.Scheduled, FromDate = new DateOnly(2026, 4, 12), ToDate = new DateOnly(2026, 4, 12) });

        CollectionAssert.AreEqual(new[] { "low", "medium", "high" }, result.Select(item => item.ImpactLevel).ToArray());
    }

    [TestMethod]
    public async Task GivenDateWithoutTime_WhenMapping_ThenDefaultsToUtcMidnight()
    {
        var client = new Mock<IFinnhubClient>();
        client.Setup(c => c.GetEconomicCalendarAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new FinnhubEconomicCalendarEvent("2026-04-15", "Treasury Auction", null, 2, "US", null, null, null, null)
            ]);

        var provider = new FinnhubMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinnhubMarketEventsProvider>.Instance);
        var result = await provider.GetEventsAsync(new MarketEventsQuery { EventType = EventType.Scheduled, FromDate = new DateOnly(2026, 4, 15), ToDate = new DateOnly(2026, 4, 15) });

        Assert.AreEqual(new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero), result[0].EventTime);
        Assert.AreEqual(TimeSpan.Zero, result[0].EventTime.Offset, "EventTime must be UTC (offset = 0)");
        Assert.IsTrue(result[0].EventTime.ToString("o").EndsWith("+00:00", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GivenBreakingNews_WhenMapping_ThenCreatesBreakingEvents()
    {
        var now = DateTimeOffset.UtcNow;
        var client = new Mock<IFinnhubClient>();
        client.Setup(c => c.GetMarketNewsAsync("general", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MarketNewsItem(42, "general", now.ToUnixTimeSeconds(), "Fed comments move markets", string.Empty, "SPY", "Reuters", "Summary", "https://example.com")
            ]);
        client.Setup(c => c.GetEconomicCalendarAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var provider = new FinnhubMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinnhubMarketEventsProvider>.Instance);
        var result = await provider.GetEventsAsync(new MarketEventsQuery { EventType = EventType.Breaking, FromDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-1)), ToDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(1)) });

        Assert.HasCount(1, result);
        Assert.AreEqual("breaking", result[0].EventType);
        Assert.AreEqual("finnhub-news-42", result[0].EventId);
        Assert.AreEqual("medium", result[0].ImpactLevel);
    }

    [TestMethod]
    public async Task GivenEconomicCalendarFails_WhenFetchingAllEvents_ThenStillReturnsBreakingNews()
    {
        var now = DateTimeOffset.UtcNow;
        var client = new Mock<IFinnhubClient>();
        client.Setup(c => c.GetEconomicCalendarAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("calendar temporarily unavailable"));
        client.Setup(c => c.GetMarketNewsAsync("general", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MarketNewsItem(100, "general", now.ToUnixTimeSeconds(), "Market crisis escalates", string.Empty, "SPY", "Reuters", "Summary", "https://example.com")
            ]);

        var provider = new FinnhubMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinnhubMarketEventsProvider>.Instance);
        var result = await provider.GetEventsAsync(new MarketEventsQuery
        {
            EventType = EventType.All,
            FromDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-1)),
            ToDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(1))
        });

        Assert.HasCount(1, result);
        Assert.AreEqual("breaking", result[0].EventType);
        Assert.AreEqual("high", result[0].ImpactLevel);
    }

    [TestMethod]
    public async Task GivenBreakingNewsFails_WhenFetchingAllEvents_ThenStillReturnsScheduledEvents()
    {
        var client = new Mock<IFinnhubClient>();
        client.Setup(c => c.GetEconomicCalendarAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new FinnhubEconomicCalendarEvent("2026-04-12", "GDP release", "18:00", 2, "US", null, null, null, null)
            ]);
        client.Setup(c => c.GetMarketNewsAsync("general", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("news unavailable"));

        var provider = new FinnhubMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinnhubMarketEventsProvider>.Instance);
        var result = await provider.GetEventsAsync(new MarketEventsQuery
        {
            EventType = EventType.All,
            FromDate = new DateOnly(2026, 4, 12),
            ToDate = new DateOnly(2026, 4, 12)
        });

        Assert.HasCount(1, result);
        Assert.AreEqual("scheduled", result[0].EventType);
    }

    [TestMethod]
    public async Task GivenEconomicEvent_WhenMapped_ThenEventIdIncludesDateComponent()
    {
        var client = new Mock<IFinnhubClient>();
        client.Setup(c => c.GetEconomicCalendarAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new FinnhubEconomicCalendarEvent("2026-05-01", "CPI report", "08:30", 2, "US", null, null, null, null)
            ]);

        var provider = new FinnhubMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinnhubMarketEventsProvider>.Instance);
        var result = await provider.GetEventsAsync(new MarketEventsQuery
        {
            EventType = EventType.Scheduled,
            FromDate = new DateOnly(2026, 5, 1),
            ToDate = new DateOnly(2026, 5, 1)
        });

        StringAssert.Contains(result[0].EventId, "2026-05-01");
    }

    [TestMethod]
    [DataRow("Emergency crisis response announced", "high")]
    [DataRow("Fed rate outlook updated", "medium")]
    [DataRow("Company opens new office", "low")]
    public async Task GivenBreakingHeadline_WhenMapping_ThenInfersImpactLevel(string headline, string expectedImpact)
    {
        var now = DateTimeOffset.UtcNow;
        var client = new Mock<IFinnhubClient>();
        client.Setup(c => c.GetMarketNewsAsync("general", It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new MarketNewsItem(500, "general", now.ToUnixTimeSeconds(), headline, string.Empty, "SPY", "Reuters", "Summary", "https://example.com")
            ]);

        var provider = new FinnhubMarketEventsProvider(client.Object, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinnhubMarketEventsProvider>.Instance);
        var result = await provider.GetEventsAsync(new MarketEventsQuery
        {
            EventType = EventType.Breaking,
            FromDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(-1)),
            ToDate = DateOnly.FromDateTime(now.UtcDateTime.AddDays(1))
        });

        Assert.AreEqual(expectedImpact, result[0].ImpactLevel);
    }
}
