using StockData.Net.Models.SocialMedia;
using StockData.Net.Providers;
using StockData.Net.Providers.SocialMedia;

namespace StockData.Net.Tests.Providers.SocialMedia;

[TestClass]
public class SocialMediaRouterTests
{
    [TestMethod]
    public async Task GivenLookbackExceedsCapability_WhenRequestingPosts_ThenClampsAndSetsTierAdvisory()
    {
        var provider = new FakeSocialProvider(maxLookbackHours: 24);
        var router = new SocialMediaRouter(new[] { provider }, new SocialFeedCache());

        var result = await router.GetPostsAsync(new SocialFeedRequest
        {
            Query = "inflation",
            MaxResults = 10,
            LookbackHours = 72,
            Tier = ProviderTier.Free
        }, "xtwitter");

        Assert.IsNotNull(result.TierAdvisory);
        Assert.AreEqual(24, provider.LastRequest!.LookbackHours);
    }

    [TestMethod]
    public async Task GivenMaxResultsExceedsCapability_WhenRequestingPosts_ThenClampsAndSetsTierAdvisory()
    {
        var provider = new FakeSocialProvider(maxResults: 5);
        var router = new SocialMediaRouter(new[] { provider }, new SocialFeedCache());

        var result = await router.GetPostsAsync(new SocialFeedRequest
        {
            Query = "inflation",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        }, "xtwitter");

        Assert.IsNotNull(result.TierAdvisory);
        StringAssert.Contains(result.TierAdvisory, "max_results clamped to 5");
        Assert.AreEqual(5, provider.LastRequest!.MaxResults);
    }

    [TestMethod]
    public async Task GivenInvalidHandle_WhenRequestingPosts_ThenThrowsValidationError()
    {
        var provider = new FakeSocialProvider();
        var router = new SocialMediaRouter(new[] { provider }, new SocialFeedCache());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => router.GetPostsAsync(new SocialFeedRequest
        {
            Handles = new[] { "bad/handle" },
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        }, "xtwitter"));
    }

    [TestMethod]
    public async Task GivenNoHandlesAndNoQuery_WhenRequestingPosts_ThenThrowsValidationError()
    {
        var provider = new FakeSocialProvider();
        var router = new SocialMediaRouter(new[] { provider }, new SocialFeedCache());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => router.GetPostsAsync(new SocialFeedRequest
        {
            Handles = Array.Empty<string>(),
            Query = null,
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        }, "xtwitter"));
    }

    [TestMethod]
    public async Task GivenOutOfRangeMaxResults_WhenRequestingPosts_ThenThrowsValidationError()
    {
        var provider = new FakeSocialProvider();
        var router = new SocialMediaRouter(new[] { provider }, new SocialFeedCache());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => router.GetPostsAsync(new SocialFeedRequest
        {
            Query = "fed",
            MaxResults = 101,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        }, "xtwitter"));
    }

    [TestMethod]
    public async Task GivenWhitespaceOnlyQuery_WhenRequestingPosts_ThenThrowsValidationErrorBeforeProviderCall()
    {
        var provider = new FakeSocialProvider();
        var router = new SocialMediaRouter(new[] { provider }, new SocialFeedCache());

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => router.GetPostsAsync(new SocialFeedRequest
        {
            Query = "   ",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        }, "xtwitter"));

        Assert.AreEqual(0, provider.CallCount);
    }

    [TestMethod]
    public async Task GivenSameRequestShape_WhenFetchingTwice_ThenSecondCallHitsCache()
    {
        var provider = new FakeSocialProvider();
        var router = new SocialMediaRouter(new[] { provider }, new SocialFeedCache());

        var request = new SocialFeedRequest
        {
            Handles = new[] { "Reuters" },
            Query = "rate hike",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        };

        await router.GetPostsAsync(request, "xtwitter");
        await router.GetPostsAsync(new SocialFeedRequest
        {
            Handles = new[] { "reuters", "REUTERS" },
            Query = "  rate hike ",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        }, "xtwitter");

        Assert.AreEqual(1, provider.CallCount);
    }

    [TestMethod]
    public async Task GivenCacheHit_WhenFetchingPosts_ThenLiveResultHasNullCachedAtAndCachedResultHasValue()
    {
        var provider = new FakeSocialProvider();
        var router = new SocialMediaRouter(new[] { provider }, new SocialFeedCache());

        var request = new SocialFeedRequest
        {
            Handles = new[] { "Reuters" },
            Query = "rate hike",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        };

        var live = await router.GetPostsAsync(request, "xtwitter");
        var cached = await router.GetPostsAsync(request, "xtwitter");

        Assert.IsTrue(live.Posts.All(post => post.CachedAt is null));
        Assert.IsTrue(cached.Posts.All(post => post.CachedAt is not null));
        Assert.AreEqual(1, provider.CallCount);
    }

    private sealed class FakeSocialProvider : ISocialMediaProvider
    {
        private readonly int _maxLookbackHours;
        private readonly int _maxResults;

        public FakeSocialProvider(int maxLookbackHours = 168, int maxResults = 10)
        {
            _maxLookbackHours = maxLookbackHours;
            _maxResults = maxResults;
        }

        public string ProviderId => "xtwitter";
        public string ProviderName => "X / Twitter";
        public ProviderCategory Category => ProviderCategory.SocialMedia;
        public SocialFeedRequest? LastRequest { get; private set; }
        public int CallCount { get; private set; }

        public Task<SocialFeedResult> GetPostsAsync(SocialFeedRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            CallCount++;
            return Task.FromResult(new SocialFeedResult
            {
                Posts = new[]
                {
                    new SocialPost
                    {
                        PostId = "1",
                        AuthorHandle = "reuters",
                        Content = "rate hike in focus",
                        PostedAt = DateTimeOffset.UtcNow,
                        Url = "https://x.com/reuters/status/1",
                        MatchedKeywords = request.Query is null
                            ? Array.Empty<string>()
                            : new[] { request.Query }
                    }
                }
            });
        }

        public SocialFeedCapabilities GetSupportedCapabilities(ProviderTier tier)
        {
            return new SocialFeedCapabilities
            {
                MaxResults = _maxResults,
                MaxLookbackHours = _maxLookbackHours,
                RateLimitWindowSeconds = 900
            };
        }
    }
}
