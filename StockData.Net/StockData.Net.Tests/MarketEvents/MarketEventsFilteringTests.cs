using System.Text.Json;
using StockData.Net.Deduplication;
using StockData.Net.McpServer;
using StockData.Net.Models.Events;
using StockData.Net.Providers;

namespace StockData.Net.Tests.MarketEvents;

[TestClass]
public class MarketEventsFilteringTests
{
    [TestMethod]
    public async Task GivenCategoryFed_WhenFiltering_ThenReturnsOnlyFedEvents()
    {
        var handler = CreateHandler();
        var response = await handler.HandleAsync(ParseArguments("{\"category\":\"fed\"}"));
        var events = ParseEvents(response);

        Assert.AreEqual(2, events.GetArrayLength());
        Assert.AreEqual("fed", events[0].GetProperty("category").GetString());
        Assert.AreEqual("fed", events[1].GetProperty("category").GetString());
    }

    [TestMethod]
    public async Task GivenImpactHigh_WhenFiltering_ThenReturnsOnlyHighImpactAndExcludesNull()
    {
        var handler = CreateHandler();
        var response = await handler.HandleAsync(ParseArguments("{\"impact_level\":\"high\"}"));
        var events = ParseEvents(response);

        Assert.AreEqual(1, events.GetArrayLength());
        Assert.AreEqual("high", events[0].GetProperty("impactLevel").GetString());
    }

    [TestMethod]
    public async Task GivenCategoryAndImpact_WhenFiltering_ThenUsesAndLogic()
    {
        var handler = CreateHandler();
        var response = await handler.HandleAsync(ParseArguments("{\"category\":\"fed\",\"impact_level\":\"high\"}"));
        var events = ParseEvents(response);

        Assert.AreEqual(1, events.GetArrayLength());
        Assert.AreEqual("fed", events[0].GetProperty("category").GetString());
        Assert.AreEqual("high", events[0].GetProperty("impactLevel").GetString());
    }

    private static MarketEventsToolHandler CreateHandler()
    {
        return new MarketEventsToolHandler([
            new StaticProvider([
                new MarketEvent { EventId = "1", Title = "Fed", EventType = "scheduled", Category = "fed", ImpactLevel = "high", EventTime = DateTimeOffset.UtcNow, Source = "P1" },
                new MarketEvent { EventId = "2", Title = "Geo", EventType = "breaking", Category = "geopolitical", ImpactLevel = "medium", EventTime = DateTimeOffset.UtcNow, Source = "P1" },
                new MarketEvent { EventId = "3", Title = "Unknown", EventType = "breaking", Category = "fed", ImpactLevel = null, EventTime = DateTimeOffset.UtcNow, Source = "P1" }
            ])
        ], new MarketEventDeduplicator());
    }

    private static JsonElement ParseArguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement ParseEvents(object response)
    {
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(response));
        var text = responseDocument.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
        using var eventsDocument = JsonDocument.Parse(text!);
        return eventsDocument.RootElement.GetProperty("events").Clone();
    }

    private sealed class StaticProvider : IMarketEventsProvider
    {
        private readonly IReadOnlyList<MarketEvent> _events;

        public StaticProvider(IReadOnlyList<MarketEvent> events)
        {
            _events = events;
        }

        public string ProviderId => "static";
        public string ProviderName => "Static";

        public Task<IReadOnlyList<MarketEvent>> GetEventsAsync(MarketEventsQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(_events);
    }
}
