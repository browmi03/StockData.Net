using System.Text.Json;
using Moq;
using StockData.Net.Deduplication;
using StockData.Net.McpServer;
using StockData.Net.Models.Events;
using StockData.Net.Providers;

namespace StockData.Net.Tests.MarketEvents;

[TestClass]
public class MarketEventsInputValidationTests
{
    [TestMethod]
    public async Task GivenInvalidCategory_WhenHandlingRequest_ThenReturnsValidationErrorWithAllowedValues()
    {
        var spyProvider = new CapturingMarketEventsProvider();
        var handler = CreateHandler(spyProvider);

        var ex = await Assert.ThrowsExactlyAsync<Exception>(() => handler.HandleAsync(ParseArguments("{\"category\":\"earnings\"}")));

        StringAssert.Contains(ex.Message, "Invalid category");
        StringAssert.Contains(ex.Message, "fed");
        StringAssert.Contains(ex.Message, "treasury");
        StringAssert.Contains(ex.Message, "geopolitical");
        StringAssert.Contains(ex.Message, "regulatory");
        StringAssert.Contains(ex.Message, "central_bank");
        StringAssert.Contains(ex.Message, "institutional");
        StringAssert.Contains(ex.Message, "all");
        Assert.AreEqual(0, spyProvider.CallCount);
    }

    [TestMethod]
    public async Task GivenInvalidImpactLevel_WhenHandlingRequest_ThenReturnsValidationErrorWithAllowedValues()
    {
        var handler = CreateHandler(new CapturingMarketEventsProvider());

        var ex = await Assert.ThrowsExactlyAsync<Exception>(() => handler.HandleAsync(ParseArguments("{\"impact_level\":\"critical\"}")));

        StringAssert.Contains(ex.Message, "Invalid impact_level");
        StringAssert.Contains(ex.Message, "high");
        StringAssert.Contains(ex.Message, "medium");
        StringAssert.Contains(ex.Message, "low");
        StringAssert.Contains(ex.Message, "all");
    }

    [TestMethod]
    public async Task GivenFromDateAfterToDate_WhenHandlingRequest_ThenReturnsValidationError()
    {
        var spyProvider = new CapturingMarketEventsProvider();
        var handler = CreateHandler(spyProvider);

        var ex = await Assert.ThrowsExactlyAsync<Exception>(() => handler.HandleAsync(ParseArguments("{\"from_date\":\"2026-04-20\",\"to_date\":\"2026-04-10\"}")));

        Assert.AreEqual("from_date must be earlier than or equal to to_date", ex.Message);
        Assert.AreEqual(0, spyProvider.CallCount);
    }

    [TestMethod]
    [TestCategory("AC-21")]
    public void BothProviders_ImplementIMarketEventsProvider()
    {
        Assert.IsTrue(typeof(FinnhubMarketEventsProvider).IsAssignableTo(typeof(IMarketEventsProvider)),
            "FinnhubMarketEventsProvider must implement IMarketEventsProvider");
        Assert.IsTrue(typeof(AlphaVantageMarketEventsProvider).IsAssignableTo(typeof(IMarketEventsProvider)),
            "AlphaVantageMarketEventsProvider must implement IMarketEventsProvider");
    }

    [TestMethod]
    public async Task GivenNoParameters_WhenHandlingRequest_ThenUsesDefaultDateWindowAndAllFilters()
    {
        var provider = new CapturingMarketEventsProvider();
        var handler = CreateHandler(provider);

        await handler.HandleAsync(ParseArguments("{}"));

        Assert.IsNotNull(provider.LastQuery);
        Assert.AreEqual(EventCategory.All, provider.LastQuery!.Category);
        Assert.AreEqual(ImpactLevel.All, provider.LastQuery.ImpactLevel);
        Assert.AreEqual(EventType.All, provider.LastQuery.EventType);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.AreEqual(today, provider.LastQuery.FromDate);
        Assert.AreEqual(today.AddDays(7), provider.LastQuery.ToDate);
    }

    [TestMethod]
    public async Task GivenDateWindowLongerThanThirtyDays_WhenHandlingRequest_ThenReturnsValidationError()
    {
        var handler = CreateHandler(new CapturingMarketEventsProvider());

        var ex = await Assert.ThrowsExactlyAsync<Exception>(() => handler.HandleAsync(ParseArguments("{\"from_date\":\"2026-04-01\",\"to_date\":\"2026-05-05\"}")));

        StringAssert.Contains(ex.Message, "30 days");
    }

    private static MarketEventsToolHandler CreateHandler(params IMarketEventsProvider[] providers)
    {
        return new MarketEventsToolHandler(providers, new MarketEventDeduplicator());
    }

    private static JsonElement ParseArguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class CapturingMarketEventsProvider : IMarketEventsProvider
    {
        public string ProviderId => "test";
        public string ProviderName => "Test";
        public MarketEventsQuery? LastQuery { get; private set; }
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<MarketEvent>> GetEventsAsync(MarketEventsQuery query, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastQuery = query;
            return Task.FromResult<IReadOnlyList<MarketEvent>>([]);
        }
    }
}
