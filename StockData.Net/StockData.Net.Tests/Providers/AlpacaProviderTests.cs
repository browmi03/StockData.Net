using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StockData.Net.Clients.Alpaca;
using StockData.Net.Models;
using StockData.Net.Providers;

namespace StockData.Net.Tests.Providers;

[TestClass]
public class AlpacaProviderTests
{
    private Mock<IAlpacaClient> _client = null!;
    private AlpacaProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        _client = new Mock<IAlpacaClient>(MockBehavior.Strict);
        _provider = new AlpacaProvider(_client.Object, NullLogger<AlpacaProvider>.Instance);
    }

    [TestMethod]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new AlpacaProvider(null!, NullLogger<AlpacaProvider>.Instance));
    }

    [TestMethod]
    public void ProviderMetadata_ReturnsExpectedValues()
    {
        Assert.AreEqual("alpaca", _provider.ProviderId);
        Assert.AreEqual("Alpaca Markets", _provider.ProviderName);
        Assert.AreEqual("1.0.0", _provider.Version);
    }

    [TestMethod]
    public void GetSupportedDataTypes_Free_ReturnsExpectedSet()
    {
        var result = _provider.GetSupportedDataTypes("free");

        CollectionAssert.AreEquivalent(new[] { "historical_prices", "stock_info" }, result.ToArray());
    }

    [TestMethod]
    public void GetSupportedDataTypes_Paid_ReturnsExpectedSet()
    {
        var result = _provider.GetSupportedDataTypes("paid");

        CollectionAssert.AreEquivalent(new[] { "historical_prices", "stock_info", "news", "market_news" }, result.ToArray());
    }

    [TestMethod]
    public async Task GetStockActionsAsync_ThrowsTierAwareNotSupportedException()
    {
        var ex = await Assert.ThrowsExactlyAsync<TierAwareNotSupportedException>(() => _provider.GetStockActionsAsync("AAPL"));
        Assert.AreEqual("alpaca", ex.ProviderId);
        Assert.IsFalse(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetFinancialStatementAsync_ThrowsTierAwareNotSupportedException()
    {
        var ex = await Assert.ThrowsExactlyAsync<TierAwareNotSupportedException>(() => _provider.GetFinancialStatementAsync("AAPL", FinancialStatementType.IncomeStatement));
        Assert.AreEqual("alpaca", ex.ProviderId);
        Assert.IsFalse(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetHolderInfoAsync_ThrowsTierAwareNotSupportedException()
    {
        var ex = await Assert.ThrowsExactlyAsync<TierAwareNotSupportedException>(() => _provider.GetHolderInfoAsync("AAPL", HolderType.MajorHolders));
        Assert.AreEqual("alpaca", ex.ProviderId);
        Assert.IsFalse(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetOptionExpirationDatesAsync_ThrowsTierAwareNotSupportedException()
    {
        var ex = await Assert.ThrowsExactlyAsync<TierAwareNotSupportedException>(() => _provider.GetOptionExpirationDatesAsync("AAPL"));
        Assert.AreEqual("alpaca", ex.ProviderId);
        Assert.IsFalse(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetOptionChainAsync_ThrowsTierAwareNotSupportedException()
    {
        var ex = await Assert.ThrowsExactlyAsync<TierAwareNotSupportedException>(() => _provider.GetOptionChainAsync("AAPL", "2026-12-18", OptionType.Calls));
        Assert.AreEqual("alpaca", ex.ProviderId);
        Assert.IsFalse(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetRecommendationsAsync_ThrowsTierAwareNotSupportedException()
    {
        var ex = await Assert.ThrowsExactlyAsync<TierAwareNotSupportedException>(() => _provider.GetRecommendationsAsync("AAPL", RecommendationType.Recommendations));
        Assert.AreEqual("alpaca", ex.ProviderId);
        Assert.IsFalse(ex.AvailableOnPaidTier);
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_CallsClientAndReturnsJson()
    {
        _client.Setup(c => c.GetHistoricalBarsAsync(
                "AAPL",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                "1Day",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlpacaBar>
            {
                new(new DateTime(2026, 3, 20, 0, 0, 0, DateTimeKind.Utc), 10, 11, 9, 10.5, 1000, 123, 10.2)
            });

        var result = await _provider.GetHistoricalPricesAsync("AAPL", "1mo", "1d");

        _client.Verify(c => c.GetHistoricalBarsAsync(
            "AAPL",
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            "1Day",
            It.IsAny<CancellationToken>()), Times.Once);

        using var doc = JsonDocument.Parse(result);
        Assert.AreEqual(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.AreEqual("alpaca", doc.RootElement[0].GetProperty("SourceProvider").GetString());
    }

    [TestMethod]
    public async Task GetStockInfoAsync_CallsClientAndReturnsFormattedJson()
    {
        _client.Setup(c => c.GetLatestQuoteAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AlpacaQuote(AskPrice: 101.2, AskSize: 20, BidPrice: 101.0, BidSize: 15, Timestamp: new DateTime(2026, 3, 22, 15, 0, 0, DateTimeKind.Utc)));

        var result = await _provider.GetStockInfoAsync("AAPL");

        _client.Verify(c => c.GetLatestQuoteAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);

        using var doc = JsonDocument.Parse(result);
        Assert.AreEqual("AAPL", doc.RootElement.GetProperty("symbol").GetString());
        Assert.AreEqual(101.1d, doc.RootElement.GetProperty("midPrice").GetDouble(), 0.0001d);
        Assert.AreEqual("alpaca", doc.RootElement.GetProperty("sourceProvider").GetString());
    }

    [TestMethod]
    public async Task GetNewsAsync_ValidTicker_ReturnsFormattedNews()
    {
        _client.Setup(c => c.GetNewsAsync(
                "AAPL",
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlpacaNewsArticle>
            {
                new(
                    Id: "news-1",
                    Headline: "Apple launches new product",
                    Summary: "Apple announced a major product launch.",
                    Url: "https://example.com/apple-news",
                    Source: "Reuters",
                    CreatedAt: new DateTime(2026, 3, 22, 16, 30, 0, DateTimeKind.Utc),
                    Symbols: ["AAPL", "QQQ"])
            });

        var result = await _provider.GetNewsAsync("AAPL");

        _client.Verify(c => c.GetNewsAsync(
            "AAPL",
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);

        StringAssert.Contains(result, "Title: Apple launches new product");
        StringAssert.Contains(result, "Publisher: Reuters");
        StringAssert.Contains(result, "Related Tickers: AAPL, QQQ");
        StringAssert.Contains(result, "URL: https://example.com/apple-news");
    }

    [TestMethod]
    public async Task GetMarketNewsAsync_ReturnsFormattedMarketNews()
    {
        _client.Setup(c => c.GetMarketNewsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlpacaNewsArticle>
            {
                new(
                    Id: "market-1",
                    Headline: "Markets rally on CPI data",
                    Summary: "Major indexes rose after inflation data cooled.",
                    Url: "https://example.com/market-news",
                    Source: "Bloomberg",
                    CreatedAt: new DateTime(2026, 3, 22, 14, 0, 0, DateTimeKind.Utc),
                    Symbols: ["SPY", "DIA"])
            });

        var result = await _provider.GetMarketNewsAsync();

        _client.Verify(c => c.GetMarketNewsAsync(It.IsAny<CancellationToken>()), Times.Once);

        StringAssert.Contains(result, "Title: Markets rally on CPI data");
        StringAssert.Contains(result, "Publisher: Bloomberg");
        StringAssert.Contains(result, "Related Tickers: SPY, DIA");
        StringAssert.Contains(result, "URL: https://example.com/market-news");
    }

    [TestMethod]
    public async Task GetHealthStatusAsync_DelegatesToClient()
    {
        _client.Setup(c => c.GetHealthStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _provider.GetHealthStatusAsync();

        Assert.IsTrue(result);
        _client.Verify(c => c.GetHealthStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
