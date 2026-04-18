using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using StockData.Net.Clients.AlphaVantage;
using StockData.Net.Models.Events;

namespace StockData.Net.Providers;

public sealed class AlphaVantageMarketEventsProvider : IMarketEventsProvider
{
    private const string MacroTopics = "ECONOMY_MACRO,ECONOMY_MONETARY,ECONOMY_FISCAL,GOVERNMENT_AND_POLITICS";

    private readonly IAlphaVantageClient _client;
    private readonly ILogger<AlphaVantageMarketEventsProvider> _logger;

    public string ProviderId => "alphavantage";
    public string ProviderName => "AlphaVantage";

    public AlphaVantageMarketEventsProvider(
        IAlphaVantageClient client,
        ILogger<AlphaVantageMarketEventsProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<MarketEvent>> GetEventsAsync(
        MarketEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.EventType == EventType.Scheduled)
        {
            return [];
        }

        var from = query.FromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = query.ToDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        _logger.LogDebug("Fetching macro market events from AlphaVantage for {FromDate} to {ToDate}", query.FromDate, query.ToDate);
        var items = await _client.GetMacroNewsSentimentAsync(MacroTopics, from, to, cancellationToken);
        var mapped = items.Select(MapBreakingEvent).ToList();
        _logger.LogDebug("AlphaVantage returned {Count} market events", mapped.Count);
        return mapped;
    }

    private static MarketEvent MapBreakingEvent(AlphaVantageMacroNewsItem item)
    {
        return new MarketEvent
        {
            EventId = $"alphavantage-news-{ComputeStableHash(item.Url)}",
            Title = item.Title,
            Description = string.IsNullOrWhiteSpace(item.Summary) ? null : item.Summary,
            EventType = "breaking",
            Category = InferCategory(item.Topics),
            ImpactLevel = InferImpactFromSentiment(item.OverallSentimentLabel),
            EventTime = ParseEventTime(item.TimePublished),
            Source = "AlphaVantage",
            SourceUrl = string.IsNullOrWhiteSpace(item.Url) ? null : item.Url,
            AffectedMarkets = item.RelatedTickers,
            Sentiment = MapSentiment(item.OverallSentimentLabel)
        };
    }

    private static string? InferImpactFromSentiment(string? sentimentLabel) =>
        sentimentLabel?.Trim() switch
        {
            "Bullish" or "Bearish" => "high",
            "Somewhat-Bullish" or "Somewhat-Bearish" => "medium",
            "Neutral" => "low",
            _ => null
        };

    private static string ComputeStableHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes[..8]).ToLowerInvariant();
    }

    private static DateTimeOffset ParseEventTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DateTimeOffset.UtcNow;
        }

        if (DateTimeOffset.TryParseExact(
                value,
                "yyyyMMdd'T'HHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.UtcNow;
    }

    private static string InferCategory(IReadOnlyList<string> topics)
    {
        if (topics.Any(topic => topic.Equals("ECONOMY_MONETARY", StringComparison.OrdinalIgnoreCase)))
        {
            return "fed";
        }

        if (topics.Any(topic => topic.Equals("ECONOMY_FISCAL", StringComparison.OrdinalIgnoreCase)))
        {
            return "treasury";
        }

        if (topics.Any(topic => topic.Equals("GOVERNMENT_AND_POLITICS", StringComparison.OrdinalIgnoreCase)))
        {
            return "geopolitical";
        }

        return "institutional";
    }

    private static string? MapSentiment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim() switch
        {
            "Bullish" => "positive",
            "Somewhat-Bullish" => "positive",
            "Bearish" => "negative",
            "Somewhat-Bearish" => "negative",
            "Neutral" => "neutral",
            _ => null
        };
    }
}