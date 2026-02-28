using StockData.Net.Configuration;

namespace StockData.Net.Deduplication;

/// <summary>
/// Calculates similarity between two news articles.
/// </summary>
public interface INewsDeduplicationStrategy
{
    /// <summary>
    /// Gets strategy name for diagnostics.
    /// </summary>
    string StrategyName { get; }

    /// <summary>
    /// Calculates similarity from 0.0 to 1.0.
    /// </summary>
    double CalculateSimilarity(
        NewsArticle article1,
        NewsArticle article2,
        NewsDeduplicationConfiguration config);
}
