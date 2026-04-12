namespace StockData.Net.Models.SocialMedia;

public sealed class SocialFeedResult
{
    public IReadOnlyList<SocialPost> Posts { get; init; } = Array.Empty<SocialPost>();

    public IReadOnlyList<HandleError> Errors { get; init; } = Array.Empty<HandleError>();

    public string? TierAdvisory { get; init; }
}
