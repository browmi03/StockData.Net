using System.Text.Json;
using System.Text.RegularExpressions;
using StockData.Net.Deduplication;
using StockData.Net.McpServer;
using StockData.Net.Models.Events;
using StockData.Net.Providers;

namespace StockData.Net.Tests.MarketEvents;

[TestClass]
public class MarketEventsResilienceTests
{
    [TestMethod]
    public async Task GivenPrimaryProviderFails_WhenSecondarySucceeds_ThenReturnsSecondaryEvents()
    {
        const string primaryErrorMessage = "Primary provider failed";

        var handler = new MarketEventsToolHandler(
        [
            new ThrowingProvider("primary", new InvalidOperationException(primaryErrorMessage)),
            new StaticProvider("secondary", [new MarketEvent { EventId = "s1", Title = "Event", EventType = "breaking", Category = "institutional", EventTime = DateTimeOffset.UtcNow, Source = "AlphaVantage" }])
        ],
        new MarketEventDeduplicator());

        var response = await handler.HandleAsync(ParseArguments("{}"));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(response));
        var text = responseDocument.RootElement.GetProperty("content")[0].GetProperty("text").GetString();

        StringAssert.Contains(text!, "s1");
        Assert.IsFalse(text.Contains(primaryErrorMessage, StringComparison.Ordinal), "Fallback response must not include primary provider error details");
    }

    [TestMethod]
    public async Task GivenAllProvidersFail_WhenHandlingRequest_ThenThrowsProviderFailoverExceptionWithInvestorFriendlyMessage()
    {
        var handler = new MarketEventsToolHandler(
        [
            new ThrowingProvider("finnhub", new InvalidOperationException("token SECRET123456 error")),
            new ThrowingProvider("alphavantage", new InvalidOperationException("downstream error"))
        ],
        new MarketEventDeduplicator());

        var ex = await Assert.ThrowsExactlyAsync<ProviderFailoverException>(() => handler.HandleAsync(ParseArguments("{}")));
        var errorText = ex.Message;

        StringAssert.Contains(errorText, "market events");
        Assert.IsFalse(errorText.Contains("SECRET123456", StringComparison.Ordinal));
        Assert.IsFalse(Regex.IsMatch(errorText, @"HTTP \d{3}|StatusCode: \d{3}|status code \d{3}", RegexOptions.IgnoreCase),
            "Error must not contain HTTP status codes");
        Assert.IsFalse(errorText.Contains("at ", StringComparison.Ordinal) || errorText.Contains("StackTrace", StringComparison.OrdinalIgnoreCase),
            "Error must not contain stack trace fragments");
    }

    private static JsonElement ParseArguments(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class ThrowingProvider : IMarketEventsProvider
    {
        private readonly Exception _error;

        public ThrowingProvider(string providerId, Exception error)
        {
            ProviderId = providerId;
            _error = error;
        }

        public string ProviderId { get; }
        public string ProviderName => ProviderId;

        public Task<IReadOnlyList<MarketEvent>> GetEventsAsync(MarketEventsQuery query, CancellationToken cancellationToken = default)
            => throw _error;
    }

    private sealed class StaticProvider : IMarketEventsProvider
    {
        private readonly IReadOnlyList<MarketEvent> _events;

        public StaticProvider(string providerId, IReadOnlyList<MarketEvent> events)
        {
            ProviderId = providerId;
            _events = events;
        }

        public string ProviderId { get; }
        public string ProviderName => ProviderId;

        public Task<IReadOnlyList<MarketEvent>> GetEventsAsync(MarketEventsQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult(_events);
    }
}
