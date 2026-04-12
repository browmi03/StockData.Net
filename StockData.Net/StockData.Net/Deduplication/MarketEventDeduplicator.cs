using StockData.Net.Configuration;
using StockData.Net.Models.Events;

namespace StockData.Net.Deduplication;

public class MarketEventDeduplicator
{
    private readonly LevenshteinSimilarityStrategy _strategy = new();
    private const double TitleSimilarityThreshold = 0.85;
    private static readonly TimeSpan TimeProximityWindow = TimeSpan.FromHours(1);
    private static readonly NewsDeduplicationConfiguration StrategyConfig = new()
    {
        SimilarityThreshold = TitleSimilarityThreshold
    };

    public IReadOnlyList<MarketEvent> Deduplicate(IReadOnlyList<MarketEvent> events)
    {
        if (events.Count <= 1)
        {
            return events;
        }

        var deduplicated = new List<MarketEvent>(events.Count);

        for (var i = 0; i < events.Count; i++)
        {
            var current = events[i];
            var duplicateFound = false;

            for (var j = 0; j < deduplicated.Count; j++)
            {
                var candidate = deduplicated[j];
                if (!IsWithinTimeWindow(candidate.EventTime, current.EventTime))
                {
                    continue;
                }

                var similarity = _strategy.CalculateSimilarity(
                    new NewsArticle { Title = candidate.Title },
                    new NewsArticle { Title = current.Title },
                    StrategyConfig);

                if (similarity >= TitleSimilarityThreshold)
                {
                    duplicateFound = true;
                    break;
                }
            }

            if (!duplicateFound)
            {
                deduplicated.Add(current);
            }
        }

        return deduplicated;
    }

    private static bool IsWithinTimeWindow(DateTimeOffset left, DateTimeOffset right)
    {
        return (left - right).Duration() <= TimeProximityWindow;
    }
}