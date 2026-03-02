using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using StockData.Net.Configuration;
using StockData.Net.Models;
using StockData.Net.Providers;

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
    public void Constructor_WithEmptyProviders_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            _ = new StockDataProviderRouter(_config, Enumerable.Empty<IStockDataProvider>());
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected exception
        }
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
}
