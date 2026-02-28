using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Diagnostics;
using StockData.Net.Configuration;
using StockData.Net.Deduplication;

namespace StockData.Net.Tests.Deduplication;

[TestClass]
public class NewsDeduplicatorTests
{
    private readonly NewsDeduplicationConfiguration _config = new()
    {
        Enabled = true,
        SimilarityThreshold = 0.85,
        TimestampWindowHours = 24,
        CompareContent = false,
        MaxArticlesForComparison = 200
    };

    [TestMethod]
    public async Task DeduplicateAsync_DuplicateArticles_MergesAndTracksSources()
    {
        var deduplicator = new NewsDeduplicator();

        var providerResponses = new Dictionary<string, string>
        {
            ["provider_a"] = BuildArticle("Apple Earnings Beat Expectations", "Reuters", "https://example.com/a", "2026-02-27 10:00:00"),
            ["provider_b"] = BuildArticle("Apple Earnings Beat Expectations", "Bloomberg", "https://example.com/b", "2026-02-27 09:30:00")
        };

        var result = await deduplicator.DeduplicateAsync(providerResponses, _config);

        Assert.AreEqual(1, CountArticles(result));
        Assert.IsTrue(result.Contains("Sources: Bloomberg, Reuters"));
        Assert.IsTrue(result.Contains("Merged Count: 1"));
        Assert.IsTrue(result.Contains("Published: 2026-02-27 09:30:00"));
    }

    [TestMethod]
    public async Task DeduplicateAsync_SourceAttribution_DoesNotExposeProviderIds()
    {
        var deduplicator = new NewsDeduplicator();

        var providerResponses = new Dictionary<string, string>
        {
            ["internal_provider_alpha"] = BuildArticle("Apple Earnings Beat Expectations", "Reuters", "https://example.com/a", "2026-02-27 10:00:00"),
            ["internal_provider_beta"] = BuildArticle("Apple Earnings Beat Expectations", "Bloomberg", "https://example.com/b", "2026-02-27 09:30:00")
        };

        var result = await deduplicator.DeduplicateAsync(providerResponses, _config);

        Assert.IsFalse(result.Contains("internal_provider_alpha", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.Contains("internal_provider_beta", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(result.Contains("Sources: Bloomberg, Reuters"));
    }

    [TestMethod]
    public async Task DeduplicateAsync_UniqueArticles_PreservesAllArticles()
    {
        var deduplicator = new NewsDeduplicator();

        var providerResponses = new Dictionary<string, string>
        {
            ["provider_a"] = BuildArticle("Apple Announces New Product", "Reuters", "https://example.com/a", "2026-02-27 10:00:00"),
            ["provider_b"] = BuildArticle("Microsoft Cloud Revenue Expands", "Bloomberg", "https://example.com/b", "2026-02-27 09:30:00")
        };

        var result = await deduplicator.DeduplicateAsync(providerResponses, _config);

        Assert.AreEqual(2, CountArticles(result));
        Assert.IsTrue(result.Contains("Apple Announces New Product"));
        Assert.IsTrue(result.Contains("Microsoft Cloud Revenue Expands"));
    }

    [TestMethod]
    public async Task DeduplicateAsync_ThresholdBoundary_RespectsConfiguredThreshold()
    {
        var strategy = new LevenshteinSimilarityStrategy();
        var deduplicator = new NewsDeduplicator(strategy);

        var titleA = "Apple launches iPhone 16 globally";
        var titleB = "Apple launches iPhone 16 worldwide";

        var articleA = new NewsArticle { Title = titleA };
        var articleB = new NewsArticle { Title = titleB };
        var similarity = strategy.CalculateSimilarity(articleA, articleB, _config);

        var providerResponses = new Dictionary<string, string>
        {
            ["provider_a"] = BuildArticle(titleA, "Reuters", "https://example.com/a", "2026-02-27 10:00:00"),
            ["provider_b"] = BuildArticle(titleB, "Bloomberg", "https://example.com/b", "2026-02-27 09:30:00")
        };

        var atThresholdConfig = CloneConfig(similarity);
        var mergedResult = await deduplicator.DeduplicateAsync(providerResponses, atThresholdConfig);

        Assert.AreEqual(1, CountArticles(mergedResult));

        if (similarity < 1.0)
        {
            var aboveThresholdConfig = CloneConfig(Math.Min(1.0, similarity + 0.01));
            var separatedResult = await deduplicator.DeduplicateAsync(providerResponses, aboveThresholdConfig);
            Assert.AreEqual(2, CountArticles(separatedResult));
        }
    }

    [TestMethod]
    public async Task DeduplicateAsync_RespectsMaxArticlesForComparisonLimit()
    {
        var deduplicator = new NewsDeduplicator();
        var config = CloneConfig(_config.SimilarityThreshold);
        config.MaxArticlesForComparison = 2;

        var providerResponses = new Dictionary<string, string>
        {
            ["provider_a"] = string.Join("\n\n", new[]
            {
                BuildArticle("A", "P1", "https://example.com/1", "2026-02-27 10:00:00"),
                BuildArticle("B", "P1", "https://example.com/2", "2026-02-27 09:00:00"),
                BuildArticle("C", "P1", "https://example.com/3", "2026-02-27 08:00:00")
            })
        };

        var result = await deduplicator.DeduplicateAsync(providerResponses, config);

        Assert.AreEqual(2, CountArticles(result));
    }

    [TestMethod]
    public async Task DeduplicateAsync_ProcessesHundredArticlesWithinPerformanceBudget()
    {
        var deduplicator = new NewsDeduplicator();
        var providerResponses = new Dictionary<string, string>
        {
            ["provider_a"] = BuildBatchArticles("A", 50),
            ["provider_b"] = BuildBatchArticles("B", 50)
        };

        var stopwatch = Stopwatch.StartNew();
        var result = await deduplicator.DeduplicateAsync(providerResponses, _config);
        stopwatch.Stop();

        Assert.IsFalse(string.IsNullOrWhiteSpace(result));
        Assert.IsTrue(stopwatch.ElapsedMilliseconds < 500,
            $"Deduplication took {stopwatch.ElapsedMilliseconds}ms, expected < 500ms");
    }

    private NewsDeduplicationConfiguration CloneConfig(double threshold)
    {
        return new NewsDeduplicationConfiguration
        {
            Enabled = true,
            SimilarityThreshold = threshold,
            TimestampWindowHours = _config.TimestampWindowHours,
            CompareContent = _config.CompareContent,
            MaxArticlesForComparison = _config.MaxArticlesForComparison
        };
    }

    private static int CountArticles(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
        {
            return 0;
        }

        return response
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.StartsWith("Title:", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildArticle(string title, string publisher, string url, string published)
    {
        return $"Title: {title}\nPublisher: {publisher}\nPublished: {published}\nURL: {url}";
    }

    private static string BuildBatchArticles(string prefix, int count)
    {
        var articles = Enumerable.Range(1, count)
            .Select(i => BuildArticle(
                $"{prefix} headline {i}",
                $"Publisher {prefix}",
                $"https://example.com/{prefix.ToLowerInvariant()}/{i}",
                $"2026-02-27 10:{i % 60:00}:00"));

        return string.Join("\n\n", articles);
    }
}
