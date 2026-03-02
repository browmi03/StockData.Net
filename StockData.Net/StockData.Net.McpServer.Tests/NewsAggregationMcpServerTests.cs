using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using StockData.Net.Configuration;
using StockData.Net.McpServer;
using StockData.Net.McpServer.Models;
using StockData.Net.Providers;

namespace StockData.Net.McpServer.Tests;

[TestClass]
public class NewsAggregationMcpServerTests
{
    [TestMethod]
    public async Task HandleRequestAsync_GetYahooFinanceNews_WithAggregationEnabled_ReturnsDeduplicatedResponse()
    {
        var primary = CreateProvider("primary_provider");
        var secondary = CreateProvider("secondary_provider");

        var newsDate1 = DateTime.UtcNow.AddDays(-2).ToString("yyyy-MM-dd HH:mm:ss");
        var newsDate2 = DateTime.UtcNow.AddDays(-2).AddMinutes(-30).ToString("yyyy-MM-dd HH:mm:ss");

        primary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildArticle("Apple Earnings Beat Expectations", "Reuters", "https://example.com/a", newsDate1));
        secondary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildArticle("Apple Earnings Beat Expectations", "Bloomberg", "https://example.com/b", newsDate2));

        var router = new StockDataProviderRouter(CreateConfiguration(), new[] { primary.Object, secondary.Object });
        var server = new StockDataMcpServer(router);

        var requestJson = JsonDocument.Parse(@"{
            ""name"": ""get_yahoo_finance_news"",
            ""arguments"": {
                ""ticker"": ""AAPL""
            }
        }");

        var response = await server.HandleRequestAsync(new McpRequest
        {
            Id = 1001,
            Method = "tools/call",
            Params = requestJson.RootElement
        }, CancellationToken.None);

        Assert.IsNull(response.Error);
        var responseJson = JsonSerializer.Serialize(response.Result);
        Assert.Contains("Apple Earnings Beat Expectations", responseJson);
        Assert.Contains("Sources: Bloomberg, Reuters", responseJson);
    }

    private static McpConfiguration CreateConfiguration()
    {
        return new McpConfiguration
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
                Enabled = true,
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

    private static string BuildArticle(string title, string publisher, string url, string published)
    {
        return $"Title: {title}\nPublisher: {publisher}\nPublished: {published}\nURL: {url}";
    }
}
