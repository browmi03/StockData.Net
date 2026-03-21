using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Microsoft.Extensions.Logging.Abstractions;
using StockData.Net.Configuration;
using StockData.Net.Models;
using StockData.Net.Providers;
using StockData.Net.Clients.Finnhub;
using StockData.Net.Clients.AlphaVantage;
using NSubstitute;

namespace StockData.Net.Tests;

[TestClass]
public class StockDataProviderRouterTests
{
    private Mock<IStockDataProvider> _mockProvider = null!;
    private McpConfiguration _config = null!;
    private StockDataProviderRouter _router = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockProvider = new Mock<IStockDataProvider>();
        _mockProvider.Setup(p => p.ProviderId).Returns("test_provider");
        _mockProvider.Setup(p => p.ProviderName).Returns("Test Provider");
        _mockProvider.Setup(p => p.Version).Returns("1.0.0");
        _mockProvider.Setup(p => p.GetSupportedDataTypes(It.IsAny<string>())).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
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

        _config = new McpConfiguration
        {
            Version = "1.0",
            Providers = new List<ProviderConfiguration>
            {
                new ProviderConfiguration
                {
                    Id = "test_provider",
                    Type = "TestProvider",
                    Enabled = true,
                    Priority = 1
                }
            },
            Routing = new RoutingConfiguration
            {
                DefaultStrategy = "PrimaryWithFailover",
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["HistoricalPrices"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "test_provider",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    }
                }
            }
        };

        _router = new StockDataProviderRouter(_config, new[] { _mockProvider.Object });
    }

    [TestMethod]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            _ = new StockDataProviderRouter(null!, new[] { _mockProvider.Object });
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public void Constructor_WithNullProviders_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            _ = new StockDataProviderRouter(_config, null!);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public void Constructor_WithEmptyProviders_CreatesRouterSuccessfully()
    {
        // Act
        var router = new StockDataProviderRouter(_config, Enumerable.Empty<IStockDataProvider>());

        // Assert
        Assert.IsNotNull(router);
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_CallsPrimaryProvider()
    {
        // Arrange
        var expectedResult = "historical data";
        _mockProvider.Setup(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetHistoricalPricesAsync("AAPL", "1mo", "1d");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockProvider.Verify(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStockInfoAsync_CallsPrimaryProvider()
    {
        // Arrange
        var expectedResult = "stock info";
        _mockProvider.Setup(p => p.GetStockInfoAsync("MSFT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetStockInfoAsync("MSFT");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockProvider.Verify(p => p.GetStockInfoAsync("MSFT", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetNewsAsync_CallsPrimaryProvider()
    {
        // Arrange
        var expectedResult = "news data";
        _mockProvider.Setup(p => p.GetNewsAsync("GOOGL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetNewsAsync("GOOGL");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockProvider.Verify(p => p.GetNewsAsync("GOOGL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetMarketNewsAsync_CallsPrimaryProvider()
    {
        // Arrange
        var expectedResult = "market news";
        _mockProvider.Setup(p => p.GetMarketNewsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetMarketNewsAsync();

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockProvider.Verify(p => p.GetMarketNewsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStockActionsAsync_CallsPrimaryProvider()
    {
        // Arrange
        var expectedResult = "stock actions";
        _mockProvider.Setup(p => p.GetStockActionsAsync("TSLA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetStockActionsAsync("TSLA");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockProvider.Verify(p => p.GetStockActionsAsync("TSLA", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetFinancialStatementAsync_CallsPrimaryProvider()
    {
        // Arrange
        var expectedResult = "financial statement";
        _mockProvider.Setup(p => p.GetFinancialStatementAsync("AMZN", FinancialStatementType.IncomeStatement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetFinancialStatementAsync("AMZN", FinancialStatementType.IncomeStatement);

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockProvider.Verify(p => p.GetFinancialStatementAsync("AMZN", FinancialStatementType.IncomeStatement, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetHolderInfoAsync_CallsPrimaryProvider()
    {
        // Arrange
        var expectedResult = "holder info";
        _mockProvider.Setup(p => p.GetHolderInfoAsync("NVDA", HolderType.MajorHolders, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetHolderInfoAsync("NVDA", HolderType.MajorHolders);

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockProvider.Verify(p => p.GetHolderInfoAsync("NVDA", HolderType.MajorHolders, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetOptionExpirationDatesAsync_CallsPrimaryProvider()
    {
        // Arrange
        var expectedResult = "expiration dates";
        _mockProvider.Setup(p => p.GetOptionExpirationDatesAsync("META", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetOptionExpirationDatesAsync("META");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockProvider.Verify(p => p.GetOptionExpirationDatesAsync("META", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetOptionChainAsync_CallsPrimaryProvider()
    {
        // Arrange
        var expirationDate = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd");
        var expectedResult = "option chain";
        _mockProvider.Setup(p => p.GetOptionChainAsync("SPY", expirationDate, OptionType.Calls, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetOptionChainAsync("SPY", expirationDate, OptionType.Calls);

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockProvider.Verify(p => p.GetOptionChainAsync("SPY", expirationDate, OptionType.Calls, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetRecommendationsAsync_CallsPrimaryProvider()
    {
        // Arrange
        var expectedResult = "recommendations";
        _mockProvider.Setup(p => p.GetRecommendationsAsync("DIS", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetRecommendationsAsync("DIS", RecommendationType.Recommendations, 12);

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockProvider.Verify(p => p.GetRecommendationsAsync("DIS", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetProvidersHealthAsync_ReturnsHealthStatusForAllProviders()
    {
        // Arrange
        _mockProvider.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _router.GetProvidersHealthAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ContainsKey("test_provider"));
        Assert.IsTrue(result["test_provider"]);
    }

    [TestMethod]
    public async Task GetProvidersHealthAsync_WhenProviderUnhealthy_ReturnsFalse()
    {
        // Arrange
        _mockProvider.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _router.GetProvidersHealthAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ContainsKey("test_provider"));
        Assert.IsFalse(result["test_provider"]);
    }

    [TestMethod]
    public async Task GetProvidersHealthAsync_WhenProviderThrows_ReturnsFalse()
    {
        // Arrange
        _mockProvider.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Health check failed"));

        // Act
        var result = await _router.GetProvidersHealthAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ContainsKey("test_provider"));
        Assert.IsFalse(result["test_provider"]);
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_WithConfiguredRouting_UsesConfiguredProvider()
    {
        // Arrange
        var expectedResult = "historical data";
        _mockProvider.Setup(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetHistoricalPricesAsync("AAPL", "1mo", "1d");

        // Assert
        Assert.AreEqual(expectedResult, result);
    }

    [TestMethod]
    public async Task GetMarketNewsAsync_WithoutSpecificRouting_UsesPrimaryProvider()
    {
        // Arrange
        var expectedResult = "market news";
        _mockProvider.Setup(p => p.GetMarketNewsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetMarketNewsAsync();

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockProvider.Verify(p => p.GetMarketNewsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStockInfoAsync_WhenPrimaryThrowsNotSupportedException_FailsOverToFallbackProvider()
    {
        // Arrange
        var (router, primary, fallback) = CreateStockInfoRouterWithFallback();
        primary.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>())).ThrowsAsync(new NotSupportedException("Operation not supported"));
        fallback.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>())).ReturnsAsync("fallback");

        var result = await router.GetStockInfoAsync("AAPL");

        Assert.AreEqual("fallback", result);
        primary.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        fallback.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStockInfoAsync_WhenPrimaryThrowsTierAwareNotSupportedException_FailsOverToFallbackProvider()
    {
        // Arrange
        var (router, primary, fallback) = CreateStockInfoRouterWithFallback();
        primary.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TierAwareNotSupportedException("finnhub", "GetStockInfoAsync", availableOnPaidTier: false));
        fallback.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>())).ReturnsAsync("fallback");

        var result = await router.GetStockInfoAsync("AAPL");

        Assert.AreEqual("fallback", result);
        primary.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        fallback.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStockInfoAsync_WhenArgumentExceptionEncountered_DoesNotAttemptFailover()
    {
        // Arrange
        var (router, primary, fallback) = CreateStockInfoRouterWithFallback();
        primary.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("invalid ticker"));

        // Act & Assert
        try
        {
            await router.GetStockInfoAsync("AAPL");
            Assert.Fail("Expected ArgumentException was not thrown.");
        }
        catch (ArgumentException)
        {
            // Expected
        }

        primary.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        fallback.Verify(p => p.GetStockInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GivenProviderTierCapabilityMatrix_WhenRoutingByTier_ThenExpectedProviderIsUsed()
    {
        var (router, primary, fallback) = CreateStockInfoRouterWithFallback(primaryTier: "free");
        primary.Setup(p => p.GetSupportedDataTypes("free")).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "stock_info" });
        fallback.Setup(p => p.GetSupportedDataTypes("free")).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "stock_info" });
        primary.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>())).ReturnsAsync("primary");

        var result = await router.GetStockInfoAsync("AAPL");

        Assert.AreEqual("primary", result);
    }

    [TestMethod]
    public async Task GivenUnsupportedTierCapability_WhenRouting_ThenProviderIsSkippedWithoutFailure()
    {
        var primary = new Mock<IStockDataProvider>();
        primary.Setup(p => p.ProviderId).Returns("primary_provider");
        primary.Setup(p => p.ProviderName).Returns("Primary");
        primary.Setup(p => p.Version).Returns("1.0.0");
        primary.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        primary.Setup(p => p.GetSupportedDataTypes("free")).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "stock_info" });

        var fallback = new Mock<IStockDataProvider>();
        fallback.Setup(p => p.ProviderId).Returns("fallback_provider");
        fallback.Setup(p => p.ProviderName).Returns("Fallback");
        fallback.Setup(p => p.Version).Returns("1.0.0");
        fallback.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        fallback.Setup(p => p.GetSupportedDataTypes("free")).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "market_news" });
        fallback.Setup(p => p.GetMarketNewsAsync(It.IsAny<CancellationToken>())).ReturnsAsync("market-news");

        var config = new McpConfiguration
        {
            Providers =
            [
                new ProviderConfiguration { Id = "primary_provider", Type = "Test", Enabled = true, Priority = 1, Tier = "free", HealthCheck = new HealthCheckConfiguration { Enabled = false } },
                new ProviderConfiguration { Id = "fallback_provider", Type = "Test", Enabled = true, Priority = 2, Tier = "free", HealthCheck = new HealthCheckConfiguration { Enabled = false } }
            ],
            Routing = new RoutingConfiguration
            {
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["MarketNews"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "primary_provider",
                        FallbackProviderIds = new List<string> { "fallback_provider" }
                    }
                }
            },
            CircuitBreaker = new CircuitBreakerConfiguration { Enabled = false }
        };

        var router = new StockDataProviderRouter(config, new[] { primary.Object, fallback.Object });

        var result = await router.GetMarketNewsAsync();
        Assert.AreEqual("market-news", result);

        primary.Verify(p => p.GetMarketNewsAsync(It.IsAny<CancellationToken>()), Times.Never);
        fallback.Verify(p => p.GetMarketNewsAsync(It.IsAny<CancellationToken>()), Times.Once);

        var health = router.GetDetailedHealthStatus();
        Assert.IsTrue(health.TryGetValue("primary_provider", out var primaryHealth));
        Assert.AreEqual(0, primaryHealth.FailedRequests);
    }

    [TestMethod]
    public void GivenProviderCapabilityMatrix_WhenQueriedByTier_ThenExpectedCapabilitiesAreReturned()
    {
        var finnhubClient = new Mock<IFinnhubClient>(MockBehavior.Strict);
        var finnhub = new FinnhubProvider(finnhubClient.Object, NullLogger<FinnhubProvider>.Instance);

        var alphaClient = Substitute.For<IAlphaVantageClient>();
        var alpha = new AlphaVantageProvider(alphaClient, Substitute.For<Microsoft.Extensions.Logging.ILogger<AlphaVantageProvider>>());

        var yahooClient = new Mock<IYahooFinanceClient>();
        var yahoo = new YahooFinanceProvider(yahooClient.Object);

        var finnhubFree = finnhub.GetSupportedDataTypes("free");
        var finnhubPaid = finnhub.GetSupportedDataTypes("paid");
        var alphaFree = alpha.GetSupportedDataTypes("free");
        var yahooFree = yahoo.GetSupportedDataTypes("free");
        var yahooPaid = yahoo.GetSupportedDataTypes("paid");

        CollectionAssert.Contains(finnhubFree.ToArray(), "market_news");
        CollectionAssert.Contains(finnhubFree.ToArray(), "recommendations");
        CollectionAssert.DoesNotContain(finnhubFree.ToArray(), "historical_prices");

        CollectionAssert.Contains(finnhubPaid.ToArray(), "historical_prices");
        CollectionAssert.Contains(finnhubPaid.ToArray(), "stock_actions");
        CollectionAssert.Contains(finnhubPaid.ToArray(), "market_news");
        CollectionAssert.Contains(finnhubPaid.ToArray(), "recommendations");

        CollectionAssert.Contains(alphaFree.ToArray(), "historical_prices");
        CollectionAssert.Contains(alphaFree.ToArray(), "stock_actions");
        CollectionAssert.Contains(alphaFree.ToArray(), "market_news");

        CollectionAssert.AreEquivalent(yahooFree.ToArray(), yahooPaid.ToArray());
        CollectionAssert.Contains(yahooFree.ToArray(), "historical_prices");
        CollectionAssert.Contains(yahooFree.ToArray(), "stock_actions");
        CollectionAssert.Contains(yahooFree.ToArray(), "market_news");
        CollectionAssert.Contains(yahooFree.ToArray(), "recommendations");
    }

    [TestMethod]
    public async Task GivenProviderDoesNotSupportHistoricalPricesOnFreeTier_WhenRouting_ThenProviderIsSkippedAndFailureCountRemainsZero()
    {
        var primary = new Mock<IStockDataProvider>();
        primary.Setup(p => p.ProviderId).Returns("primary_provider");
        primary.Setup(p => p.ProviderName).Returns("Primary");
        primary.Setup(p => p.Version).Returns("1.0.0");
        primary.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        primary.Setup(p => p.GetSupportedDataTypes("free")).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "stock_info" });

        var fallback = new Mock<IStockDataProvider>();
        fallback.Setup(p => p.ProviderId).Returns("fallback_provider");
        fallback.Setup(p => p.ProviderName).Returns("Fallback");
        fallback.Setup(p => p.Version).Returns("1.0.0");
        fallback.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        fallback.Setup(p => p.GetSupportedDataTypes("free")).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "historical_prices" });
        fallback.Setup(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>())).ReturnsAsync("fallback-historical");

        var config = new McpConfiguration
        {
            Providers =
            [
                new ProviderConfiguration { Id = "primary_provider", Type = "Test", Enabled = true, Priority = 1, Tier = "free", HealthCheck = new HealthCheckConfiguration { Enabled = false } },
                new ProviderConfiguration { Id = "fallback_provider", Type = "Test", Enabled = true, Priority = 2, Tier = "free", HealthCheck = new HealthCheckConfiguration { Enabled = false } }
            ],
            Routing = new RoutingConfiguration
            {
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["HistoricalPrices"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "primary_provider",
                        FallbackProviderIds = new List<string> { "fallback_provider" }
                    }
                }
            },
            CircuitBreaker = new CircuitBreakerConfiguration { Enabled = false }
        };

        var router = new StockDataProviderRouter(config, new[] { primary.Object, fallback.Object });

        var result = await router.GetHistoricalPricesAsync("AAPL", "1mo", "1d");
        Assert.AreEqual("fallback-historical", result);

        primary.Verify(p => p.GetHistoricalPricesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fallback.Verify(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()), Times.Once);

        var health = router.GetDetailedHealthStatus();
        Assert.IsTrue(health.TryGetValue("primary_provider", out var primaryHealth));
        Assert.AreEqual(0, primaryHealth.FailedRequests);
    }

    [TestMethod]
    public async Task GivenPrimaryThrowsTierAwareNotSupportedException_WhenHistoricalPricesRequested_ThenRouterFailsOverToNextProvider()
    {
        var primary = new Mock<IStockDataProvider>();
        primary.Setup(p => p.ProviderId).Returns("primary_provider");
        primary.Setup(p => p.ProviderName).Returns("Primary");
        primary.Setup(p => p.Version).Returns("1.0.0");
        primary.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        primary.Setup(p => p.GetSupportedDataTypes("free")).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "historical_prices" });
        primary.Setup(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TierAwareNotSupportedException("finnhub", "GetHistoricalPricesAsync", availableOnPaidTier: true));

        var fallback = new Mock<IStockDataProvider>();
        fallback.Setup(p => p.ProviderId).Returns("fallback_provider");
        fallback.Setup(p => p.ProviderName).Returns("Fallback");
        fallback.Setup(p => p.Version).Returns("1.0.0");
        fallback.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        fallback.Setup(p => p.GetSupportedDataTypes("free")).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "historical_prices" });
        fallback.Setup(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>())).ReturnsAsync("fallback-result");

        var config = new McpConfiguration
        {
            Providers =
            [
                new ProviderConfiguration { Id = "primary_provider", Type = "Test", Enabled = true, Priority = 1, Tier = "free", HealthCheck = new HealthCheckConfiguration { Enabled = false } },
                new ProviderConfiguration { Id = "fallback_provider", Type = "Test", Enabled = true, Priority = 2, Tier = "free", HealthCheck = new HealthCheckConfiguration { Enabled = false } }
            ],
            Routing = new RoutingConfiguration
            {
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["HistoricalPrices"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "primary_provider",
                        FallbackProviderIds = new List<string> { "fallback_provider" }
                    }
                }
            },
            CircuitBreaker = new CircuitBreakerConfiguration { Enabled = false }
        };

        var router = new StockDataProviderRouter(config, new[] { primary.Object, fallback.Object });

        var result = await router.GetHistoricalPricesAsync("AAPL", "1mo", "1d");
        Assert.AreEqual("fallback-result", result);

        primary.Verify(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()), Times.Once);
        fallback.Verify(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStockInfoWithProviderAsync_WhenExplicitProviderSelected_UsesOnlyRequestedProvider()
    {
        var (router, primary, fallback) = CreateStockInfoRouterWithFallback();
        fallback.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>())).ReturnsAsync("fallback-result");

        var result = await router.GetStockInfoWithProviderAsync("AAPL", "fallback_provider");

        Assert.AreEqual("fallback-result", result.Result);
        Assert.AreEqual("fallback_provider", result.ProviderId);
        fallback.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        primary.Verify(p => p.GetStockInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task GetStockInfoWithProviderAsync_WhenExplicitProviderMissing_ThrowsInvalidOperationException()
    {
        var (router, _, _) = CreateStockInfoRouterWithFallback();

        InvalidOperationException exception;
        try
        {
            await router.GetStockInfoWithProviderAsync("AAPL", "unknown_provider");
            Assert.Fail("Expected InvalidOperationException was not thrown.");
            return;
        }
        catch (InvalidOperationException ex)
        {
            exception = ex;
        }

        Assert.Contains("not available", exception.Message);
    }

    [TestMethod]
    public async Task ExecuteWithExplicitProviderAsync_WhenCircuitBreakerOpen_StillInvokesProvider()
    {
        var provider = new Mock<IStockDataProvider>();
        provider.Setup(p => p.ProviderId).Returns("cb_provider");
        provider.Setup(p => p.ProviderName).Returns("Circuit Breaker Provider");
        provider.Setup(p => p.Version).Returns("1.0.0");
        provider.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var config = new McpConfiguration
        {
            Version = "1.0",
            Providers =
            [
                new ProviderConfiguration { Id = "cb_provider", Type = "Test", Enabled = true, Priority = 1, HealthCheck = new HealthCheckConfiguration { Enabled = false } }
            ],
            Routing = new RoutingConfiguration
            {
                DefaultStrategy = "PrimaryWithFailover",
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["StockInfo"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "cb_provider",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    }
                }
            },
            CircuitBreaker = new CircuitBreakerConfiguration
            {
                Enabled = true,
                FailureThreshold = 1,
                HalfOpenAfterSeconds = 3600,
                TimeoutSeconds = 30
            }
        };

        var router = new StockDataProviderRouter(config, new[] { provider.Object });

        // First request fails and opens the circuit breaker.
        provider
            .SetupSequence(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("simulated upstream failure"))
            .ReturnsAsync("explicit-provider-success");

        try
        {
            await router.GetStockInfoAsync("AAPL");
            Assert.Fail("Expected ProviderFailoverException was not thrown.");
        }
        catch (ProviderFailoverException)
        {
            // Expected
        }

        // This explicit-provider request should bypass the circuit breaker and still invoke the provider.
        var result = await router.GetStockInfoWithProviderAsync("AAPL", "cb_provider");

        Assert.AreEqual("explicit-provider-success", result.Result);
        Assert.AreEqual("cb_provider", result.ProviderId);
        provider.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    private static (StockDataProviderRouter Router, Mock<IStockDataProvider> Primary, Mock<IStockDataProvider> Fallback)
        CreateStockInfoRouterWithFallback(string primaryTier = "free", string fallbackTier = "free")
    {
        var primary = new Mock<IStockDataProvider>();
        primary.Setup(p => p.ProviderId).Returns("primary_provider");
        primary.Setup(p => p.ProviderName).Returns("Primary");
        primary.Setup(p => p.Version).Returns("1.0.0");
        primary.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        primary.Setup(p => p.GetSupportedDataTypes(It.IsAny<string>())).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "stock_info",
            "news",
            "market_news"
        });

        var fallback = new Mock<IStockDataProvider>();
        fallback.Setup(p => p.ProviderId).Returns("fallback_provider");
        fallback.Setup(p => p.ProviderName).Returns("Fallback");
        fallback.Setup(p => p.Version).Returns("1.0.0");
        fallback.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        fallback.Setup(p => p.GetSupportedDataTypes(It.IsAny<string>())).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "stock_info",
            "news",
            "market_news"
        });

        var config = new McpConfiguration
        {
            Version = "1.0",
            Providers =
            [
                new ProviderConfiguration { Id = "primary_provider", Type = "Test", Enabled = true, Priority = 1, Tier = primaryTier, HealthCheck = new HealthCheckConfiguration { Enabled = false } },
                new ProviderConfiguration { Id = "fallback_provider", Type = "Test", Enabled = true, Priority = 2, Tier = fallbackTier, HealthCheck = new HealthCheckConfiguration { Enabled = false } }
            ],
            Routing = new RoutingConfiguration
            {
                DefaultStrategy = "PrimaryWithFailover",
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["StockInfo"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "primary_provider",
                        FallbackProviderIds = new List<string> { "fallback_provider" },
                        TimeoutSeconds = 30
                    }
                }
            },
            CircuitBreaker = new CircuitBreakerConfiguration { Enabled = false }
        };

        return (new StockDataProviderRouter(config, new[] { primary.Object, fallback.Object }), primary, fallback);
    }
}
