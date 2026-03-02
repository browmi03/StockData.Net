using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using StockData.Net;
using StockData.Net.Models;
using StockData.Net.Providers;

namespace StockData.Net.Tests;

[TestClass]
public class YahooFinanceProviderTests
{
    private Mock<IYahooFinanceClient> _mockClient = null!;
    private YahooFinanceProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockClient = new Mock<IYahooFinanceClient>();
        _provider = new YahooFinanceProvider(_mockClient.Object);
    }

    [TestMethod]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        // Act & Assert
        try
        {
            _ = new YahooFinanceProvider(null!);
            Assert.Fail("Expected ArgumentNullException was not thrown");
        }
        catch (ArgumentNullException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public void ProviderId_ReturnsYahooFinance()
    {
        // Assert
        Assert.AreEqual("yahoo_finance", _provider.ProviderId);
    }

    [TestMethod]
    public void ProviderName_ReturnsYahooFinance()
    {
        // Assert
        Assert.AreEqual("Yahoo Finance", _provider.ProviderName);
    }

    [TestMethod]
    public void Version_ReturnsValidVersion()
    {
        // Assert
        Assert.IsNotNull(_provider.Version);
        StringAssert.StartsWith(_provider.Version, "1.");
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_WithValidTicker_CallsClient()
    {
        // Arrange
        var expectedResult = "historical data";
        _mockClient.Setup(c => c.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _provider.GetHistoricalPricesAsync("AAPL", "1mo", "1d");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockClient.Verify(c => c.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStockInfoAsync_WithValidTicker_CallsClient()
    {
        // Arrange
        var expectedResult = "stock info";
        _mockClient.Setup(c => c.GetStockInfoAsync("MSFT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _provider.GetStockInfoAsync("MSFT");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockClient.Verify(c => c.GetStockInfoAsync("MSFT", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetNewsAsync_WithValidTicker_CallsClient()
    {
        // Arrange
        var expectedResult = "news data";
        _mockClient.Setup(c => c.GetNewsAsync("GOOGL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _provider.GetNewsAsync("GOOGL");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockClient.Verify(c => c.GetNewsAsync("GOOGL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetMarketNewsAsync_CallsClient()
    {
        // Arrange
        var expectedResult = "market news data";
        _mockClient.Setup(c => c.GetMarketNewsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _provider.GetMarketNewsAsync();

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockClient.Verify(c => c.GetMarketNewsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetStockActionsAsync_WithValidTicker_CallsClient()
    {
        // Arrange
        var expectedResult = "stock actions";
        _mockClient.Setup(c => c.GetStockActionsAsync("TSLA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _provider.GetStockActionsAsync("TSLA");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockClient.Verify(c => c.GetStockActionsAsync("TSLA", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetFinancialStatementAsync_WithValidParameters_CallsClient()
    {
        // Arrange
        var expectedResult = "financial statement";
        _mockClient.Setup(c => c.GetFinancialStatementAsync("AMZN", FinancialStatementType.IncomeStatement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _provider.GetFinancialStatementAsync("AMZN", FinancialStatementType.IncomeStatement);

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockClient.Verify(c => c.GetFinancialStatementAsync("AMZN", FinancialStatementType.IncomeStatement, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetHolderInfoAsync_WithValidParameters_CallsClient()
    {
        // Arrange
        var expectedResult = "holder info";
        _mockClient.Setup(c => c.GetHolderInfoAsync("NVDA", HolderType.MajorHolders, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _provider.GetHolderInfoAsync("NVDA", HolderType.MajorHolders);

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockClient.Verify(c => c.GetHolderInfoAsync("NVDA", HolderType.MajorHolders, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetOptionExpirationDatesAsync_WithValidTicker_CallsClient()
    {
        // Arrange
        var expectedResult = "expiration dates";
        _mockClient.Setup(c => c.GetOptionExpirationDatesAsync("META", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _provider.GetOptionExpirationDatesAsync("META");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockClient.Verify(c => c.GetOptionExpirationDatesAsync("META", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetOptionChainAsync_WithValidParameters_CallsClient()
    {
        // Arrange
        var expirationDate = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd");
        var expectedResult = "option chain";
        _mockClient.Setup(c => c.GetOptionChainAsync("SPY", expirationDate, OptionType.Calls, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _provider.GetOptionChainAsync("SPY", expirationDate, OptionType.Calls);

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockClient.Verify(c => c.GetOptionChainAsync("SPY", expirationDate, OptionType.Calls, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetRecommendationsAsync_WithValidParameters_CallsClient()
    {
        // Arrange
        var expectedResult = "recommendations";
        _mockClient.Setup(c => c.GetRecommendationsAsync("DIS", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _provider.GetRecommendationsAsync("DIS", RecommendationType.Recommendations, 12);

        // Assert
        Assert.AreEqual(expectedResult, result);
        _mockClient.Verify(c => c.GetRecommendationsAsync("DIS", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task GetHealthStatusAsync_WhenClientSucceeds_ReturnsTrue()
    {
        // Arrange
        _mockClient.Setup(c => c.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("valid response");

        // Act
        var result = await _provider.GetHealthStatusAsync();

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task GetHealthStatusAsync_WhenClientReturnsError_ReturnsFalse()
    {
        // Arrange
        _mockClient.Setup(c => c.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Error: something went wrong");

        // Act
        var result = await _provider.GetHealthStatusAsync();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetHealthStatusAsync_WhenClientThrows_ReturnsFalse()
    {
        // Arrange
        _mockClient.Setup(c => c.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _provider.GetHealthStatusAsync();

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_WithEmptyTicker_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _provider.GetHistoricalPricesAsync("");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public async Task GetStockInfoAsync_WithWhitespaceTicker_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _provider.GetStockInfoAsync("   ");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public async Task GetNewsAsync_WithTooLongTicker_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _provider.GetNewsAsync("TOOLONGTICKER");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public async Task GetStockActionsAsync_WithInvalidCharacters_ThrowsArgumentException()
    {
        // Act & Assert
        try
        {
            await _provider.GetStockActionsAsync("AAPL@");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public async Task GetStockInfoAsync_WithValidTickerContainingDot_Succeeds()
    {
        // Arrange
        _mockClient.Setup(c => c.GetStockInfoAsync("BRK.A", It.IsAny<CancellationToken>()))
            .ReturnsAsync("stock info");

        // Act
        var result = await _provider.GetStockInfoAsync("BRK.A");

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task GetStockInfoAsync_WithValidTickerContainingHyphen_Succeeds()
    {
        // Arrange
        _mockClient.Setup(c => c.GetStockInfoAsync("BRK-A", It.IsAny<CancellationToken>()))
            .ReturnsAsync("stock info");

        // Act
        var result = await _provider.GetStockInfoAsync("BRK-A");

        // Assert
        Assert.IsNotNull(result);
    }

    // Error Handling Tests

    [TestMethod]
    public async Task GetStockInfoAsync_WhenNetworkError_PropagatesHttpRequestException()
    {
        // Arrange
        var networkException = new HttpRequestException("Network error");
        _mockClient.Setup(c => c.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(networkException);

        // Act & Assert
        try
        {
            await _provider.GetStockInfoAsync("AAPL");
            Assert.Fail("Expected HttpRequestException was not thrown");
        }
        catch (HttpRequestException)
        {
            // Expected exception
        }
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_WhenClientReturnsNotFoundError_ReturnsErrorMessage()
    {
        // Arrange
        _mockClient.Setup(c => c.GetHistoricalPricesAsync("INVALID", "1mo", "1d", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Company ticker INVALID not found.");

        // Act
        var result = await _provider.GetHistoricalPricesAsync("INVALID", "1mo", "1d");

        // Assert
        StringAssert.Contains(result, "not found");
    }

    [TestMethod]
    public async Task GetStockInfoAsync_WhenRateLimitError_ReturnsErrorMessage()
    {
        // Arrange
        var errorMessage = "Error: Rate limit exceeded";
        _mockClient.Setup(c => c.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(errorMessage);

        // Act
        var result = await _provider.GetStockInfoAsync("AAPL");

        // Assert
        StringAssert.Contains(result, "Rate limit");
    }

    [TestMethod]
    public async Task GetNewsAsync_WhenServerError_ReturnsErrorMessage()
    {
        // Arrange
        var errorMessage = "Error: Server returned status code 500";
        _mockClient.Setup(c => c.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(errorMessage);

        // Act
        var result = await _provider.GetNewsAsync("AAPL");

        // Assert
        StringAssert.Contains(result, "Error");
    }

    [TestMethod]
    public async Task GetStockInfoAsync_WhenParsingError_ReturnsErrorMessage()
    {
        // Arrange
        var errorMessage = "Error: getting stock information for AAPL: Invalid JSON";
        _mockClient.Setup(c => c.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(errorMessage);

        // Act
        var result = await _provider.GetStockInfoAsync("AAPL");

        // Assert
        StringAssert.Contains(result, "Error");
        StringAssert.Contains(result, "JSON");
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_CancellationTokenPropagatedToClient()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var testData = "historical data";
        _mockClient.Setup(c => c.GetHistoricalPricesAsync("AAPL", "1mo", "1d", cts.Token))
            .ReturnsAsync(testData);

        // Act
        var result = await _provider.GetHistoricalPricesAsync("AAPL", "1mo", "1d", cts.Token);

        // Assert
        _mockClient.Verify(c => c.GetHistoricalPricesAsync("AAPL", "1mo", "1d", cts.Token), Times.Once);
    }

    [TestMethod]
    public async Task GetStockActionsAsync_WhenExceptionThrown_InnerExceptionPreserved()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");
        var outerException = new HttpRequestException("Request failed", innerException);
        _mockClient.Setup(c => c.GetStockActionsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(outerException);

        // Act & Assert
        try
        {
            await _provider.GetStockActionsAsync("AAPL");
            Assert.Fail("Expected exception was not thrown");
        }
        catch (HttpRequestException ex)
        {
            Assert.IsNotNull(ex.InnerException);
            Assert.IsInstanceOfType(ex.InnerException, typeof(InvalidOperationException));
            Assert.AreEqual("Inner error", ex.InnerException.Message);
        }
    }
}
