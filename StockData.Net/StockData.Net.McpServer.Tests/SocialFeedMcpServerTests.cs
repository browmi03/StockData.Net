using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using StockData.Net.Configuration;
using StockData.Net.Deduplication;
using StockData.Net.Models.Events;
using StockData.Net.McpServer;
using StockData.Net.McpServer.Models;
using StockData.Net.Providers;
using StockData.Net.Providers.SocialMedia;

namespace StockData.Net.McpServer.Tests;

[TestClass]
public class SocialFeedMcpServerTests
{
    [TestMethod]
    public async Task GivenMockedXClient_WhenCallingGetSocialFeed_ThenReturnsExpectedPayload()
    {
        var server = CreateServerWithSocialProvider();

        var requestJson = JsonDocument.Parse("""
        {
          "name": "get_social_feed",
          "arguments": {
            "handles": ["Reuters"],
            "query": "$AAPL",
            "max_results": 10,
            "lookback_hours": 24,
            "provider": "xtwitter"
          }
        }
        """);

        var response = await server.HandleRequestAsync(new McpRequest
        {
            Id = 51,
            Method = "tools/call",
            Params = requestJson.RootElement
        }, CancellationToken.None);

        Assert.IsNull(response.Error);
        var text = ExtractText(response.Result);
        StringAssert.Contains(text, "\"postId\":\"123\"");
        StringAssert.Contains(text, "\"authorHandle\":\"Reuters\"");
    }

    [TestMethod]
    public void GivenProviderSelectionValidator_WhenValidatingSocialCategory_ThenRoutesOnlySocialProviders()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        config.Providers.Add(new ProviderConfiguration
        {
            Id = "xtwitter",
            Type = "XTwitterProvider",
            Enabled = true,
            Tier = "free"
        });
        config.ProviderSelection.Aliases["x"] = "xtwitter";

        var stockProvider = CreateStockProvider("yahoo_finance");
        var socialProvider = new FakeSocialProvider();

        var validator = new ProviderSelectionValidator(
            config,
            new[] { "yahoo_finance", "xtwitter" },
            new[] { stockProvider.Object },
            new[] { socialProvider });

        var socialResult = validator.ValidateForCategory("x", ProviderCategory.SocialMedia);
        var crossCategory = validator.ValidateForCategory("yahoo", ProviderCategory.SocialMedia);

        Assert.IsTrue(socialResult.IsValid);
        Assert.AreEqual("xtwitter", socialResult.ResolvedProviderId);
        Assert.IsFalse(crossCategory.IsValid);
    }

    [TestMethod]
    public async Task GivenSocialFeatureAdded_WhenCallingExistingTools_ThenRegressionBehaviorRemains()
    {
        var stockProvider = CreateStockProvider("yahoo_finance");
        stockProvider.Setup(provider => provider.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"ticker\":\"AAPL\"}");

        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var router = new StockDataProviderRouter(config, new[] { stockProvider.Object });
        var marketEvents = new[] { new FakeMarketEventsProvider() };
        var marketEventsHandler = new MarketEventsToolHandler(marketEvents, new MarketEventDeduplicator());

        var server = new StockDataMcpServer(router, config, null, marketEventsHandler);

        var stockCall = JsonDocument.Parse("""
        {
          "name": "get_stock_info",
          "arguments": {
            "ticker": "AAPL"
          }
        }
        """);

        var stockResponse = await server.HandleRequestAsync(new McpRequest
        {
            Id = 201,
            Method = "tools/call",
            Params = stockCall.RootElement
        }, CancellationToken.None);

        Assert.IsNull(stockResponse.Error);
        StringAssert.Contains(ExtractText(stockResponse.Result), "AAPL");

        var listProvidersCall = JsonDocument.Parse("""
        {
          "name": "list_providers",
          "arguments": {}
        }
        """);

        var listResponse = await server.HandleRequestAsync(new McpRequest
        {
            Id = 202,
            Method = "tools/call",
            Params = listProvidersCall.RootElement
        }, CancellationToken.None);

        Assert.IsNull(listResponse.Error);
        StringAssert.Contains(ExtractText(listResponse.Result), "providers");

        var toolsResponse = await server.HandleRequestAsync(new McpRequest
        {
            Id = 203,
            Method = "tools/list"
        }, CancellationToken.None);

        Assert.IsNull(toolsResponse.Error);
        var toolsJson = JsonSerializer.Serialize(toolsResponse.Result);
        StringAssert.Contains(toolsJson, "get_market_events");
        StringAssert.Contains(toolsJson, "get_stock_info");
        StringAssert.Contains(toolsJson, "list_providers");
    }

    [TestMethod]
    public async Task GivenSocialProviderRateLimited_WhenCallingGetSocialFeed_ThenReturnsInvestorFriendlyMessage()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        config.Providers.Add(new ProviderConfiguration
        {
            Id = "xtwitter",
            Type = "XTwitterProvider",
            Enabled = true,
            Tier = "free"
        });

        var stockProvider = CreateStockProvider("yahoo_finance");
        var router = new StockDataProviderRouter(config, new[] { stockProvider.Object });
        var socialProvider = new RateLimitedSocialProvider();
        var socialRouter = new SocialMediaRouter(new[] { socialProvider }, new SocialFeedCache());
        var socialHandler = new SocialFeedToolHandler(socialRouter, config);

        var server = new StockDataMcpServer(
            router,
            config,
            null,
            new MarketEventsToolHandler(Array.Empty<IMarketEventsProvider>(), new MarketEventDeduplicator()),
            socialHandler,
            new[] { socialProvider });

        var requestJson = JsonDocument.Parse("""
        {
          "name": "get_social_feed",
          "arguments": {
            "query": "fed",
            "max_results": 10,
            "lookback_hours": 24,
            "provider": "xtwitter"
          }
        }
        """);

        var response = await server.HandleRequestAsync(new McpRequest
        {
            Id = 301,
            Method = "tools/call",
            Params = requestJson.RootElement
        }, CancellationToken.None);

        Assert.IsNull(response.Error);
        var text = ExtractText(response.Result);
        StringAssert.Contains(text, "rate limit reached");
        StringAssert.Contains(text, "Please try again later");
        Assert.IsFalse(text.Contains("System.", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GivenSocialFeatureAdded_WhenCallingGetMarketEvents_ThenReturnsEvents()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        config.Providers.Add(new ProviderConfiguration
        {
            Id = "xtwitter",
            Type = "XTwitterProvider",
            Enabled = true,
            Tier = "free"
        });

        var stockProvider = CreateStockProvider("yahoo_finance");
        var router = new StockDataProviderRouter(config, new[] { stockProvider.Object });
        var marketEventsHandler = new MarketEventsToolHandler(new[] { new FakeMarketEventsProvider() }, new MarketEventDeduplicator());

        var server = new StockDataMcpServer(router, config, null, marketEventsHandler);

        var requestJson = JsonDocument.Parse("""
        {
          "name": "get_market_events",
          "arguments": {
            "category": "all",
            "event_type": "all",
            "impact_level": "all"
          }
        }
        """);

        var response = await server.HandleRequestAsync(new McpRequest
        {
            Id = 401,
            Method = "tools/call",
            Params = requestJson.RootElement
        }, CancellationToken.None);

        Assert.IsNull(response.Error);
        var text = ExtractText(response.Result);
        StringAssert.Contains(text, "Test Event");
        StringAssert.Contains(text, "scheduled");
    }

    private static StockDataMcpServer CreateServerWithSocialProvider()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        config.Providers.Add(new ProviderConfiguration
        {
            Id = "xtwitter",
            Type = "XTwitterProvider",
            Enabled = true,
            Tier = "free",
            Settings = new Dictionary<string, string>
            {
                ["baseUrl"] = "https://api.twitter.com/"
            }
        });
        config.ProviderSelection.Aliases["xtwitter"] = "xtwitter";
        config.ProviderSelection.Aliases["x"] = "xtwitter";

        var stockProvider = CreateStockProvider("yahoo_finance");
        var router = new StockDataProviderRouter(config, new[] { stockProvider.Object });

        var payload = """
        {
          "data": [
            {
              "id": "123",
              "text": "Breaking $AAPL move",
              "created_at": "2026-04-12T14:30:00Z",
              "author_id": "456",
              "public_metrics": { "retweet_count": 5 }
            }
          ],
          "includes": {
            "users": [
              { "id": "456", "username": "Reuters" }
            ]
          }
        }
        """;

        var httpClient = new HttpClient(new StubHttpMessageHandler(payload))
        {
            BaseAddress = new Uri("https://api.twitter.com/")
        };

        var factory = new StubHttpClientFactory(httpClient);
        var runtimeConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["X_BEARER_TOKEN"] = "token123",
                ["XTwitter:rateLimitWindowSeconds"] = "900",
                ["XTwitter:maxResults"] = "10",
                ["XTwitter:maxLookbackHours"] = "168"
            })
            .Build();

        var socialProvider = new XTwitterProvider(factory, runtimeConfiguration);
        var socialRouter = new SocialMediaRouter(new[] { socialProvider }, new SocialFeedCache());
        var socialHandler = new SocialFeedToolHandler(socialRouter, config);

        return new StockDataMcpServer(
            router,
            config,
            null,
            new MarketEventsToolHandler(Array.Empty<IMarketEventsProvider>(), new MarketEventDeduplicator()),
            socialHandler,
            new[] { socialProvider });
    }

    private static Mock<IStockDataProvider> CreateStockProvider(string id)
    {
        var provider = new Mock<IStockDataProvider>();
        provider.Setup(p => p.ProviderId).Returns(id);
        provider.Setup(p => p.ProviderName).Returns(id);
        provider.Setup(p => p.Version).Returns("1.0.0");
        provider.Setup(p => p.GetSupportedDataTypes(It.IsAny<string>())).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "historical_prices",
            "stock_info",
            "news",
            "market_news",
            "stock_actions",
            "financial_statement",
            "holder_info",
            "option_expiration_dates",
            "option_chain",
            "recommendations"
        });
        provider.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        provider.Setup(p => p.GetStockInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync("{\"ok\":true}");
        return provider;
    }

    private static string ExtractText(object? responseResult)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(responseResult));
        return json.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;

        public StubHttpClientFactory(HttpClient client)
        {
            _client = client;
        }

        public HttpClient CreateClient(string name)
        {
            return _client;
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _payload;

        public StubHttpMessageHandler(string payload)
        {
            _payload = payload;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_payload, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class FakeSocialProvider : ISocialMediaProvider
    {
        public string ProviderId => "xtwitter";
        public string ProviderName => "X / Twitter";

        public Task<StockData.Net.Models.SocialMedia.SocialFeedResult> GetPostsAsync(SocialFeedRequest request, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public SocialFeedCapabilities GetSupportedCapabilities(ProviderTier tier)
        {
            return new SocialFeedCapabilities
            {
                MaxResults = 10,
                MaxLookbackHours = 168,
                RateLimitWindowSeconds = 900
            };
        }
    }

    private sealed class FakeMarketEventsProvider : IMarketEventsProvider
    {
        public string ProviderId => "events";
        public string ProviderName => "events";

        public Task<IReadOnlyList<MarketEvent>> GetEventsAsync(MarketEventsQuery query, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MarketEvent> events = new[]
            {
                new MarketEvent
                {
                    Title = "Test Event",
                    EventType = "scheduled",
                    Category = "fed",
                    ImpactLevel = "high",
                    EventTime = DateTimeOffset.UtcNow
                }
            };
            return Task.FromResult(events);
        }
    }

    private sealed class RateLimitedSocialProvider : ISocialMediaProvider
    {
        public string ProviderId => "xtwitter";
        public string ProviderName => "X / Twitter";

        public Task<StockData.Net.Models.SocialMedia.SocialFeedResult> GetPostsAsync(SocialFeedRequest request, CancellationToken ct = default)
        {
            throw new XRateLimitException("X API rate limit reached. Please try again later.");
        }

        public SocialFeedCapabilities GetSupportedCapabilities(ProviderTier tier)
        {
            return new SocialFeedCapabilities
            {
                MaxResults = 10,
                MaxLookbackHours = 24,
                RateLimitWindowSeconds = 900
            };
        }
    }
}
