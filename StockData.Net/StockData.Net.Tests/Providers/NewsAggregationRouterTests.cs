using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using StockData.Net.Configuration;
using StockData.Net.Deduplication;
using StockData.Net.Providers;

namespace StockData.Net.Tests.Providers;

[TestClass]
public class NewsAggregationRouterTests
{
    [TestMethod]
    public async Task GetNewsAsync_AggregationEnabled_DeduplicatesAndTracksSources()
    {
        var primary = CreateProvider("primary_provider");
        var secondary = CreateProvider("secondary_provider");

        var newsDate1 = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd HH:mm:ss");
        var newsDate2 = DateTime.UtcNow.AddDays(-2).AddMinutes(-30).ToString("yyyy-MM-dd HH:mm:ss");

        primary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildArticle("Apple Earnings Beat Expectations", "Reuters", "https://example.com/a", newsDate1));
        secondary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildArticle("Apple Earnings Beat Expectations", "Bloomberg", "https://example.com/b", newsDate2));

        var router = CreateRouter(primary.Object, secondary.Object, deduplicationEnabled: true);

        var result = await router.GetNewsAsync("AAPL");

        Assert.AreEqual(1, CountArticles(result));
        Assert.Contains("Sources: Bloomberg, Reuters", result);
        primary.Verify(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        secondary.Verify(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetNewsAsync_AggregationEnabledOneProviderFails_ReturnsSuccessfulResults()
    {
        var primary = CreateProvider("primary_provider");
        var secondary = CreateProvider("secondary_provider");

        var newsDate = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd HH:mm:ss");

        primary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildArticle("Apple Earnings Beat Expectations", "Reuters", "https://example.com/a", newsDate));
        secondary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("secondary unavailable"));

        var router = CreateRouter(primary.Object, secondary.Object, deduplicationEnabled: true);

        var result = await router.GetNewsAsync("AAPL");

        Assert.AreEqual(1, CountArticles(result));
        Assert.Contains("Apple Earnings Beat Expectations", result);
    }

    [TestMethod]
    public async Task GetNewsAsync_AggregationEnabledDeduplicationDisabled_ReturnsRawMergedItems()
    {
        var primary = CreateProvider("primary_provider");
        var secondary = CreateProvider("secondary_provider");

        var newsDate1 = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd HH:mm:ss");
        var newsDate2 = DateTime.UtcNow.AddDays(-2).AddMinutes(-30).ToString("yyyy-MM-dd HH:mm:ss");

        primary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildArticle("Apple Earnings Beat Expectations", "Reuters", "https://example.com/a", newsDate1));
        secondary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildArticle("Apple Earnings Beat Expectations", "Bloomberg", "https://example.com/b", newsDate2));

        var router = CreateRouter(primary.Object, secondary.Object, deduplicationEnabled: false);

        var result = await router.GetNewsAsync("AAPL");

        Assert.AreEqual(2, CountArticles(result));
        Assert.DoesNotContain("Sources:", result);
    }

    [TestMethod]
    public async Task GetNewsAsync_AggregationEnabledAllProvidersFail_ThrowsProviderFailoverException()
    {
        var primary = CreateProvider("primary_provider");
        var secondary = CreateProvider("secondary_provider");

        primary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("primary unavailable"));
        secondary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("secondary unavailable"));

        var router = CreateRouter(primary.Object, secondary.Object, deduplicationEnabled: true);

        try
        {
            await router.GetNewsAsync("AAPL");
            Assert.Fail("Expected ProviderFailoverException was not thrown");
        }
        catch (ProviderFailoverException)
        {
            // Expected
        }
    }

    [TestMethod]
    public async Task GetNewsAsync_DeduplicatorThrows_FallsBackToRawMergedItems()
    {
        var primary = CreateProvider("primary_provider");
        var secondary = CreateProvider("secondary_provider");

        var newsDate1 = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd HH:mm:ss");
        var newsDate2 = DateTime.UtcNow.AddDays(-2).AddMinutes(-30).ToString("yyyy-MM-dd HH:mm:ss");

        primary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildArticle("Apple Earnings Beat Expectations", "Reuters", "https://example.com/a", newsDate1));
        secondary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildArticle("Apple Earnings Beat Expectations", "Bloomberg", "https://example.com/b", newsDate2));

        var failingDeduplicator = new NewsDeduplicator(new ThrowingSimilarityStrategy());
        var router = CreateRouter(primary.Object, secondary.Object, deduplicationEnabled: true, deduplicator: failingDeduplicator);

        var result = await router.GetNewsAsync("AAPL");

        Assert.AreEqual(2, CountArticles(result));
        Assert.DoesNotContain("Sources:", result);
    }

    private static StockDataProviderRouter CreateRouter(
        IStockDataProvider primary,
        IStockDataProvider secondary,
        bool deduplicationEnabled,
        NewsDeduplicator? deduplicator = null)
    {
        var config = new McpConfiguration
        {
            Providers = new List<ProviderConfiguration>
            {
                new()
                {
                    Id = "primary_provider",
                    Type = "TestProvider",
                    Enabled = true,
                    Priority = 1,
                    HealthCheck = new HealthCheckConfiguration { Enabled = false }
                },
                new()
                {
                    Id = "secondary_provider",
                    Type = "TestProvider",
                    Enabled = true,
                    Priority = 2,
                    HealthCheck = new HealthCheckConfiguration { Enabled = false }
                }
            },
            Routing = new RoutingConfiguration
            {
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["News"] = new()
                    {
                        PrimaryProviderId = "primary_provider",
                        FallbackProviderIds = new List<string> { "secondary_provider" },
                        AggregateResults = true,
                        TimeoutSeconds = 30
                    }
                }
            },
            NewsDeduplication = new NewsDeduplicationConfiguration
            {
                Enabled = deduplicationEnabled,
                SimilarityThreshold = 0.85,
                TimestampWindowHours = 24,
                CompareContent = false,
                MaxArticlesForComparison = 200
            },
            CircuitBreaker = new CircuitBreakerConfiguration
            {
                Enabled = false
            }
        };

        return new StockDataProviderRouter(config, new[] { primary, secondary }, null, deduplicator);
    }

    private static Mock<IStockDataProvider> CreateProvider(string providerId)
    {
        var provider = new Mock<IStockDataProvider>();
        provider.Setup(p => p.ProviderId).Returns(providerId);
        provider.Setup(p => p.ProviderName).Returns(providerId);
        provider.Setup(p => p.Version).Returns("1.0.0");
        provider.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return provider;
    }

    private static int CountArticles(string response)
    {
        return response
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(line => line.StartsWith("Title:", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildArticle(string title, string publisher, string url, string published)
    {
        return $"Title: {title}\nPublisher: {publisher}\nPublished: {published}\nURL: {url}";
    }

    private sealed class ThrowingSimilarityStrategy : INewsDeduplicationStrategy
    {
        public string StrategyName => "Throwing";

        public double CalculateSimilarity(NewsArticle article1, NewsArticle article2, NewsDeduplicationConfiguration config)
        {
            throw new InvalidOperationException("Simulated deduplication failure");
        }
    }
}
