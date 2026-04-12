using StockData.Net.Models.SocialMedia;
using StockData.Net.Providers;
using StockData.Net.Providers.SocialMedia;

namespace StockData.Net.Tests.Providers.SocialMedia;

[TestClass]
public class SocialFeedCacheTests
{
    [TestMethod]
    public void BuildCacheKey_GivenCaseAndOrderVariance_ReturnsSameKey()
    {
        var keyA = SocialFeedCache.BuildCacheKey(
            "xtwitter",
            new[] { "Reuters", "fedreserve", "reuters" },
            "  RATE HIKE ",
            10,
            24,
            ProviderTier.Free);

        var keyB = SocialFeedCache.BuildCacheKey(
            "xtwitter",
            new[] { "fedreserve", "REUTERS" },
            "rate hike",
            10,
            24,
            ProviderTier.Free);

        Assert.AreEqual(keyA, keyB);
    }

    [TestMethod]
    public void TryGet_GivenExpiredEntry_ReturnsFalse()
    {
        var cache = new SocialFeedCache();
        cache.Set("k", new SocialFeedResult { Posts = Array.Empty<SocialPost>() }, TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);

        var found = cache.TryGet("k", out _);

        Assert.IsFalse(found);
    }

    [TestMethod]
    public void TryGet_GivenActiveEntry_ReturnsTrue()
    {
        var cache = new SocialFeedCache();
        var expected = new SocialFeedResult
        {
            Posts = new[] { new SocialPost { PostId = "1" } }
        };
        cache.Set("k", expected, TimeSpan.FromMinutes(1));

        var found = cache.TryGet("k", out var actual);

        Assert.IsTrue(found);
        Assert.AreEqual("1", actual.Posts[0].PostId);
    }
}
