namespace StockData.Net.Deduplication;

/// <summary>
/// Provider-agnostic news article model for aggregation and deduplication.
/// </summary>
public class NewsArticle
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string ProviderId { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> RelatedTickers { get; set; } = new();
    public List<ArticleSource> Sources { get; set; } = new();
    public bool IsMerged { get; set; }
    public int MergedCount { get; set; }
}

/// <summary>
/// Tracks provider contribution for a merged article.
/// </summary>
public class ArticleSource
{
    public string ProviderId { get; set; } = string.Empty;
    public string OriginalUrl { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
}
