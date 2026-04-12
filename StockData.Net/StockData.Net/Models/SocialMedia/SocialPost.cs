namespace StockData.Net.Models.SocialMedia;

public sealed class SocialPost
{
    public string PostId { get; init; } = string.Empty;

    public string AuthorHandle { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public DateTimeOffset PostedAt { get; init; }

    public string Url { get; init; } = string.Empty;

    public string SourcePlatform { get; init; } = "X";

    public IReadOnlyList<string> MatchedKeywords { get; init; } = Array.Empty<string>();

    public DateTimeOffset? CachedAt { get; init; }

    public int RetweetCount { get; init; }
}
