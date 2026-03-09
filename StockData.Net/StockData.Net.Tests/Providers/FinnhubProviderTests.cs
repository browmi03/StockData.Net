using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StockData.Net.Clients.Finnhub;
using StockData.Net.Models;
using StockData.Net.Providers;
using System.Text.Json;

namespace StockData.Net.Tests.Providers;

[TestClass]
public class FinnhubProviderTests
{
    private Mock<IFinnhubClient> _client = null!;
    private FinnhubProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        _client = new Mock<IFinnhubClient>(MockBehavior.Strict);
        _provider = new FinnhubProvider(_client.Object, NullLogger<FinnhubProvider>.Instance);
    }

    [TestMethod]
    public async Task GetMarketNewsAsync_ThrowsTierAwareNotSupportedException_WithNoPaidTierFlag()
    {
        TierAwareNotSupportedException ex;
        try
        {
            await _provider.GetMarketNewsAsync();
            Assert.Fail("Expected TierAwareNotSupportedException was not thrown.");
            return;
        }
        catch (TierAwareNotSupportedException thrown)
        {
            ex = thrown;
        }

        Assert.AreEqual("finnhub", ex.ProviderId);
        Assert.AreEqual("GetMarketNewsAsync", ex.MethodName);
        Assert.IsFalse(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetStockActionsAsync_ThrowsTierAwareNotSupportedException_WithPaidTierFlag()
    {
        TierAwareNotSupportedException ex;
        try
        {
            await _provider.GetStockActionsAsync("AAPL");
            Assert.Fail("Expected TierAwareNotSupportedException was not thrown.");
            return;
        }
        catch (TierAwareNotSupportedException thrown)
        {
            ex = thrown;
        }

        Assert.AreEqual("finnhub", ex.ProviderId);
        Assert.AreEqual("GetStockActionsAsync", ex.MethodName);
        Assert.IsTrue(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetFinancialStatementAsync_ThrowsTierAwareNotSupportedException_WithPaidTierFlag()
    {
        TierAwareNotSupportedException ex;
        try
        {
            await _provider.GetFinancialStatementAsync("AAPL", FinancialStatementType.IncomeStatement);
            Assert.Fail("Expected TierAwareNotSupportedException was not thrown.");
            return;
        }
        catch (TierAwareNotSupportedException thrown)
        {
            ex = thrown;
        }

        Assert.AreEqual("finnhub", ex.ProviderId);
        Assert.AreEqual("GetFinancialStatementAsync", ex.MethodName);
        Assert.IsTrue(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetHolderInfoAsync_ThrowsTierAwareNotSupportedException_WithPaidTierFlag()
    {
        TierAwareNotSupportedException ex;
        try
        {
            await _provider.GetHolderInfoAsync("AAPL", HolderType.MajorHolders);
            Assert.Fail("Expected TierAwareNotSupportedException was not thrown.");
            return;
        }
        catch (TierAwareNotSupportedException thrown)
        {
            ex = thrown;
        }

        Assert.AreEqual("finnhub", ex.ProviderId);
        Assert.AreEqual("GetHolderInfoAsync", ex.MethodName);
        Assert.IsTrue(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task TierAwareExceptions_UseDistinctMessageFormats_ForPaidAndUnavailableFeatures()
    {
        TierAwareNotSupportedException noTierException;
        try
        {
            await _provider.GetMarketNewsAsync();
            Assert.Fail("Expected TierAwareNotSupportedException was not thrown.");
            return;
        }
        catch (TierAwareNotSupportedException thrown)
        {
            noTierException = thrown;
        }

        Assert.IsFalse(noTierException.Message.Contains("free tier", StringComparison.Ordinal));
        Assert.IsFalse(noTierException.Message.Contains("paid subscription", StringComparison.Ordinal));

        TierAwareNotSupportedException paidTierException;
        try
        {
            await _provider.GetStockActionsAsync("AAPL");
            Assert.Fail("Expected TierAwareNotSupportedException was not thrown.");
            return;
        }
        catch (TierAwareNotSupportedException thrown)
        {
            paidTierException = thrown;
        }

        StringAssert.Contains(paidTierException.Message, "on the free tier");
        StringAssert.Contains(paidTierException.Message, "paid subscription");
    }

    [TestMethod]
    public async Task GetOptionExpirationDatesAsync_ThrowsTierAwareNotSupportedException_WithNoPaidTierFlag()
    {
        TierAwareNotSupportedException ex;
        try
        {
            await _provider.GetOptionExpirationDatesAsync("AAPL");
            Assert.Fail("Expected TierAwareNotSupportedException was not thrown.");
            return;
        }
        catch (TierAwareNotSupportedException thrown)
        {
            ex = thrown;
        }

        Assert.AreEqual("finnhub", ex.ProviderId);
        Assert.AreEqual("GetOptionExpirationDatesAsync", ex.MethodName);
        Assert.IsFalse(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetOptionChainAsync_ThrowsTierAwareNotSupportedException_WithNoPaidTierFlag()
    {
        TierAwareNotSupportedException ex;
        try
        {
            await _provider.GetOptionChainAsync("AAPL", "2026-12-18", OptionType.Calls);
            Assert.Fail("Expected TierAwareNotSupportedException was not thrown.");
            return;
        }
        catch (TierAwareNotSupportedException thrown)
        {
            ex = thrown;
        }

        Assert.AreEqual("finnhub", ex.ProviderId);
        Assert.AreEqual("GetOptionChainAsync", ex.MethodName);
        Assert.IsFalse(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetRecommendationsAsync_ThrowsTierAwareNotSupportedException_WithNoPaidTierFlag()
    {
        TierAwareNotSupportedException ex;
        try
        {
            await _provider.GetRecommendationsAsync("AAPL", RecommendationType.Recommendations);
            Assert.Fail("Expected TierAwareNotSupportedException was not thrown.");
            return;
        }
        catch (TierAwareNotSupportedException thrown)
        {
            ex = thrown;
        }

        Assert.AreEqual("finnhub", ex.ProviderId);
        Assert.AreEqual("GetRecommendationsAsync", ex.MethodName);
        Assert.IsFalse(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_PassesResolvedResolutionToClient()
    {
        // Arrange
        _client.Setup(c => c.GetHistoricalPricesAsync(
                "AAPL",
                "60",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FinnhubCandle>
            {
                new(DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 100, 110, 95, 105, 1_000_000)
            });

        // Act
        var json = await _provider.GetHistoricalPricesAsync("AAPL", "1mo", "1h");

        // Assert
        _client.Verify(c => c.GetHistoricalPricesAsync(
            "AAPL",
            "60",
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
        StringAssert.Contains(json, "SourceProvider");
        StringAssert.Contains(json, "finnhub");
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_WithEmptyOrNullTicker_ThrowsArgumentException()
    {
        try
        {
            await _provider.GetHistoricalPricesAsync(string.Empty);
            Assert.Fail("Expected ArgumentException for empty ticker was not thrown.");
            return;
        }
        catch (ArgumentException)
        {
        }

        string? nullTicker = null;
        try
        {
            await _provider.GetHistoricalPricesAsync(nullTicker!);
            Assert.Fail("Expected ArgumentException for null ticker was not thrown.");
        }
        catch (ArgumentException)
        {
        }
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_WithUnknownSymbolReturningNoCandles_ReturnsEmptyResult()
    {
        _client.Setup(c => c.GetHistoricalPricesAsync(
                "UNKNOWN",
                "D",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<FinnhubCandle>());

        var json = await _provider.GetHistoricalPricesAsync("UNKNOWN", "1mo", "1d");
        using var document = JsonDocument.Parse(json);

        Assert.AreEqual(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.AreEqual(0, document.RootElement.GetArrayLength());
    }
}