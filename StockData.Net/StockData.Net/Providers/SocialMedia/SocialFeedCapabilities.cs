namespace StockData.Net.Providers.SocialMedia;

public sealed class SocialFeedCapabilities
{
    public int MaxResults { get; init; }

    public int MaxLookbackHours { get; init; }

    public int RateLimitWindowSeconds { get; init; }
}
