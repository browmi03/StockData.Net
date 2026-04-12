using StockData.Net.Models.SocialMedia;

namespace StockData.Net.Providers.SocialMedia;

public interface ISocialMediaProvider
{
    string ProviderId { get; }

    string ProviderName { get; }

    ProviderCategory Category => ProviderCategory.SocialMedia;

    Task<SocialFeedResult> GetPostsAsync(SocialFeedRequest request, CancellationToken ct = default);

    SocialFeedCapabilities GetSupportedCapabilities(ProviderTier tier);
}
