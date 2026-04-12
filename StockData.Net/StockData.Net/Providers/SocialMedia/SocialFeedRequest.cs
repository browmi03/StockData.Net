namespace StockData.Net.Providers.SocialMedia;

public sealed class SocialFeedRequest
{
    public IReadOnlyList<string> Handles { get; init; } = Array.Empty<string>();

    public string? Query { get; init; }

    public int MaxResults { get; init; } = 10;

    public int LookbackHours { get; init; } = 24;

    public ProviderTier Tier { get; init; } = ProviderTier.Free;
}
