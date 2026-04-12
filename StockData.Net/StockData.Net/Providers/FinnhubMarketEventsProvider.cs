using Microsoft.Extensions.Logging;
using StockData.Net.Clients.Finnhub;
using StockData.Net.Models.Events;

namespace StockData.Net.Providers;

public sealed class FinnhubMarketEventsProvider : IMarketEventsProvider
{
    private static readonly string[] CentralBankKeywords = ["ecb", "boj", "snb", "boe", "rba", "central bank"];
    private static readonly string[] TreasuryKeywords = ["treasury", "deficit", "debt ceiling"];
    private static readonly string[] GeopoliticalKeywords = ["geopolit", "war", "sanction", "military"];
    private static readonly string[] RegulatoryKeywords = ["sec", "cftc", "fdic", "regulatory"];

    private readonly IFinnhubClient _client;
    private readonly ILogger<FinnhubMarketEventsProvider> _logger;

    public string ProviderId => "finnhub";
    public string ProviderName => "Finnhub";

    public FinnhubMarketEventsProvider(
        IFinnhubClient client,
        ILogger<FinnhubMarketEventsProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<MarketEvent>> GetEventsAsync(
        MarketEventsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching market events from Finnhub for {FromDate} to {ToDate}", query.FromDate, query.ToDate);
        var results = new List<MarketEvent>();

        if (query.EventType is EventType.All or EventType.Scheduled)
        {
            var economicEvents = await _client.GetEconomicCalendarAsync(query.FromDate, query.ToDate, cancellationToken);
            results.AddRange(economicEvents.Select(MapEconomicEvent));
        }

        if (query.EventType is EventType.All or EventType.Breaking)
        {
            var news = await _client.GetMarketNewsAsync("general", cancellationToken);
            var from = query.FromDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            var to = query.ToDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

            foreach (var item in news)
            {
                var eventTime = DateTimeOffset.FromUnixTimeSeconds(item.Datetime).UtcDateTime;
                if (eventTime < from || eventTime > to)
                {
                    continue;
                }

                results.Add(MapBreakingEvent(item));
            }
        }

        _logger.LogDebug("Finnhub returned {Count} market events", results.Count);
        return results;
    }

    private static MarketEvent MapEconomicEvent(FinnhubEconomicCalendarEvent source)
    {
        var title = string.IsNullOrWhiteSpace(source.Event) ? "Economic calendar event" : source.Event.Trim();
        var eventTime = ParseEconomicEventTime(source.Date, source.Time);

        return new MarketEvent
        {
            EventId = $"finnhub-eco-{title.ToLowerInvariant()}",
            Title = title,
            Description = null,
            EventType = "scheduled",
            Category = InferCategory(title),
            ImpactLevel = MapImpact(source.Impact),
            EventTime = eventTime,
            Source = "Finnhub",
            SourceUrl = null,
            AffectedMarkets = [],
            Sentiment = null
        };
    }

    private static MarketEvent MapBreakingEvent(MarketNewsItem source)
    {
        var title = string.IsNullOrWhiteSpace(source.Headline) ? "Breaking market event" : source.Headline.Trim();

        return new MarketEvent
        {
            EventId = $"finnhub-news-{source.Id}",
            Title = title,
            Description = string.IsNullOrWhiteSpace(source.Summary) ? null : source.Summary,
            EventType = "breaking",
            Category = InferCategory(title),
            ImpactLevel = null,
            EventTime = DateTimeOffset.FromUnixTimeSeconds(source.Datetime),
            Source = "Finnhub",
            SourceUrl = string.IsNullOrWhiteSpace(source.Url) ? null : source.Url,
            AffectedMarkets = [],
            Sentiment = null
        };
    }

    private static DateTimeOffset ParseEconomicEventTime(string dateText, string? timeText)
    {
        if (!DateOnly.TryParse(dateText, out var date))
        {
            return DateTimeOffset.UtcNow;
        }

        if (string.IsNullOrWhiteSpace(timeText)
            || timeText.Equals("00:00", StringComparison.OrdinalIgnoreCase)
            || timeText.Equals("0:00", StringComparison.OrdinalIgnoreCase)
            || timeText.Equals("00:00:00", StringComparison.OrdinalIgnoreCase))
        {
            return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        }

        var formats = new[] { "H:mm", "HH:mm", "H:mm:ss", "HH:mm:ss" };
        if (TimeOnly.TryParseExact(timeText, formats, out var parsedTime))
        {
            return new DateTimeOffset(date.ToDateTime(parsedTime, DateTimeKind.Utc));
        }

        return new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
    }

    private static string? MapImpact(int? impact)
    {
        return impact switch
        {
            1 => "low",
            2 => "medium",
            3 => "high",
            _ => null
        };
    }

    private static string InferCategory(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "institutional";
        }

        var input = text.ToLowerInvariant();

        if (input.Contains("fed", StringComparison.Ordinal)
            || input.Contains("fomc", StringComparison.Ordinal)
            || input.Contains("federal reserve", StringComparison.Ordinal))
        {
            return "fed";
        }

        if (CentralBankKeywords.Any(keyword => input.Contains(keyword, StringComparison.Ordinal)))
        {
            return "central_bank";
        }

        if (TreasuryKeywords.Any(keyword => input.Contains(keyword, StringComparison.Ordinal)))
        {
            return "treasury";
        }

        if (GeopoliticalKeywords.Any(keyword => input.Contains(keyword, StringComparison.Ordinal)))
        {
            return "geopolitical";
        }

        if (RegulatoryKeywords.Any(keyword => input.Contains(keyword, StringComparison.Ordinal)))
        {
            return "regulatory";
        }

        return "institutional";
    }
}