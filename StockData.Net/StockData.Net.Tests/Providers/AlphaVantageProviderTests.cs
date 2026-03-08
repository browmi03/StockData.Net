using System.Text.Json;
using Microsoft.Extensions.Logging;
using NSubstitute;
using StockData.Net.Clients.AlphaVantage;
using StockData.Net.Models;
using StockData.Net.Providers;

namespace StockData.Net.Tests.Providers;

[TestClass]
public class AlphaVantageProviderTests
{
    private IAlphaVantageClient _client = null!;
    private ILogger<AlphaVantageProvider> _logger = null!;
    private AlphaVantageProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        _client = Substitute.For<IAlphaVantageClient>();
        _logger = Substitute.For<ILogger<AlphaVantageProvider>>();
        _provider = new AlphaVantageProvider(_client, _logger);
    }

    [TestMethod]
    public void Constructor_NullClient_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new AlphaVantageProvider(null!, _logger));
    }

    [TestMethod]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new AlphaVantageProvider(_client, null!));
    }

    [TestMethod]
    public async Task GetStockInfoAsync_ValidQuote_ReturnsSerializedPayload()
    {
        _client.GetQuoteAsync("AAPL", Arg.Any<CancellationToken>())
            .Returns(new AlphaVantageQuote(188.42, 1.2, 0.8, 1_700_000_000));

        var result = await _provider.GetStockInfoAsync("AAPL");

        using var document = JsonDocument.Parse(result);
        Assert.AreEqual("AAPL", document.RootElement.GetProperty("symbol").GetString());
        Assert.AreEqual(188.42d, document.RootElement.GetProperty("price").GetDouble(), 0.0001d);
        Assert.AreEqual("alphavantage", document.RootElement.GetProperty("sourceProvider").GetString());
    }

    [TestMethod]
    public async Task GetStockInfoAsync_NullQuote_ThrowsInvalidOperationException()
    {
        _client.GetQuoteAsync("AAPL", Arg.Any<CancellationToken>()).Returns((AlphaVantageQuote?)null);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _provider.GetStockInfoAsync("AAPL"));
        StringAssert.Contains(ex.Message, "AlphaVantage GetStockInfoAsync failed");
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_ValidResponse_MapsCandlesToJson()
    {
        _client.GetHistoricalPricesAsync("MSFT", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([
                new AlphaVantageCandle(1_700_000_000, 10, 11, 9, 10.5, 1000),
                new AlphaVantageCandle(1_700_086_400, 10.5, 12, 10, 11.5, 1200)
            ]);

        var result = await _provider.GetHistoricalPricesAsync("MSFT", "1mo", "1d");

        using var doc = JsonDocument.Parse(result);
        Assert.AreEqual(2, doc.RootElement.GetArrayLength());
        Assert.AreEqual(11.5d, doc.RootElement[1].GetProperty("Close").GetDouble(), 0.0001d);
        Assert.AreEqual("alphavantage", doc.RootElement[0].GetProperty("SourceProvider").GetString());
    }

    [TestMethod]
    public async Task GetNewsAsync_EmptyNewsList_ReturnsNoNewsMessage()
    {
        _client.GetNewsAsync("TSLA", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _provider.GetNewsAsync("TSLA");

        StringAssert.Contains(result, "No news found");
        StringAssert.Contains(result, "TSLA ticker");
    }

    [TestMethod]
    public async Task GetNewsAsync_TimestampGreaterThanZero_FormatsPublishedDate()
    {
        _client.GetNewsAsync("NVDA", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([
                new AlphaVantageNewsItem("Headline", "Reuters", "https://example.com/a", "Summary", 1_700_000_000, ["NVDA", "QQQ"])
            ]);

        var result = await _provider.GetNewsAsync("NVDA");

        StringAssert.Contains(result, "Title: Headline");
        StringAssert.Contains(result, "Publisher: Reuters");
        Assert.IsFalse(result.Contains("Published: Unknown", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetNewsAsync_NonPositiveTimestamp_UsesUnknownPublishedValue()
    {
        _client.GetNewsAsync("NVDA", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns([
                new AlphaVantageNewsItem("Headline", "Reuters", "https://example.com/a", "Summary", 0, ["NVDA"])
            ]);

        var result = await _provider.GetNewsAsync("NVDA");

        StringAssert.Contains(result, "Published: Unknown");
    }

    [TestMethod]
    public async Task GetMarketNewsAsync_Always_ThrowsNotSupportedException()
    {
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => _provider.GetMarketNewsAsync());
    }

    [TestMethod]
    public async Task GetStockActionsAsync_Always_ThrowsNotSupportedException()
    {
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => _provider.GetStockActionsAsync("AAPL"));
    }

    [TestMethod]
    public async Task GetFinancialStatementAsync_Always_ThrowsNotSupportedException()
    {
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => _provider.GetFinancialStatementAsync("AAPL", FinancialStatementType.BalanceSheet));
    }

    [TestMethod]
    public async Task GetHolderInfoAsync_Always_ThrowsNotSupportedException()
    {
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => _provider.GetHolderInfoAsync("AAPL", HolderType.MajorHolders));
    }

    [TestMethod]
    public async Task GetOptionExpirationDatesAsync_Always_ThrowsNotSupportedException()
    {
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => _provider.GetOptionExpirationDatesAsync("AAPL"));
    }

    [TestMethod]
    public async Task GetOptionChainAsync_Always_ThrowsNotSupportedException()
    {
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => _provider.GetOptionChainAsync("AAPL", "2026-12-18", OptionType.Calls));
    }

    [TestMethod]
    public async Task GetRecommendationsAsync_Always_ThrowsNotSupportedException()
    {
        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => _provider.GetRecommendationsAsync("AAPL", RecommendationType.Recommendations, 12));
    }

    [TestMethod]
    public async Task GetHealthStatusAsync_HealthyQuote_ReturnsTrue()
    {
        _client.GetQuoteAsync("AAPL", Arg.Any<CancellationToken>())
            .Returns(new AlphaVantageQuote(1.0, 0.0, 0.0, 1_700_000_000));

        var result = await _provider.GetHealthStatusAsync();

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task GetHealthStatusAsync_ClientThrows_ReturnsFalse()
    {
        _client.GetQuoteAsync("AAPL", Arg.Any<CancellationToken>())
            .Returns<Task<AlphaVantageQuote?>>(x => throw new HttpRequestException("upstream down"));

        var result = await _provider.GetHealthStatusAsync();

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task GetStockInfoAsync_OperationCanceledException_PassesThrough()
    {
        _client.GetQuoteAsync("AAPL", Arg.Any<CancellationToken>())
            .Returns<Task<AlphaVantageQuote?>>(x => throw new OperationCanceledException("cancelled"));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => _provider.GetStockInfoAsync("AAPL"));
    }

    [TestMethod]
    public async Task GetStockInfoAsync_ClientThrows_WrapsExceptionWithSanitizedMessage()
    {
        _client.GetQuoteAsync("AAPL", Arg.Any<CancellationToken>())
            .Returns<Task<AlphaVantageQuote?>>(x => throw new InvalidOperationException("token ABCD1234EFGH5678 should not leak"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _provider.GetStockInfoAsync("AAPL"));
        StringAssert.Contains(ex.Message, "AlphaVantage GetStockInfoAsync failed");
        Assert.IsFalse(ex.Message.Contains("ABCD1234EFGH5678", StringComparison.Ordinal));
        StringAssert.Contains(ex.Message, "[REDACTED]");
    }

    [TestMethod]
    [DataRow("1d")]
    [DataRow("5d")]
    [DataRow("1mo")]
    [DataRow("3mo")]
    [DataRow("6mo")]
    [DataRow("1y")]
    [DataRow("2y")]
    [DataRow("5y")]
    [DataRow("10y")]
    [DataRow("ytd")]
    [DataRow("max")]
    public async Task GetHistoricalPricesAsync_ValidPeriod_ResolvesDateWindowAndCallsClient(string period)
    {
        DateTime from = default;
        DateTime to = default;

        _client.GetHistoricalPricesAsync("AAPL", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                from = callInfo.ArgAt<DateTime>(1);
                to = callInfo.ArgAt<DateTime>(2);
                return [];
            });

        var _ = await _provider.GetHistoricalPricesAsync("AAPL", period, "1d");

        Assert.IsTrue(to >= from);
        Assert.IsLessThan(2d, (DateTime.UtcNow - to).TotalMinutes, $"Expected 'to' to be within 2 minutes of now but got {(DateTime.UtcNow - to).TotalMinutes:F4} minutes.");
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_UnknownPeriod_DefaultsToOneMonthWindow()
    {
        DateTime from = default;
        DateTime to = default;

        _client.GetHistoricalPricesAsync("AAPL", Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                from = callInfo.ArgAt<DateTime>(1);
                to = callInfo.ArgAt<DateTime>(2);
                return [];
            });

        var _ = await _provider.GetHistoricalPricesAsync("AAPL", "unknown-period", "1d");

        var spanDays = (to - from).TotalDays;
        Assert.IsTrue(spanDays > 26 && spanDays < 33, $"Expected ~1 month window but got {spanDays:F2} days.");
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public async Task GetStockInfoAsync_NullOrWhitespaceTicker_ThrowsWrappedInvalidOperationException(string? ticker)
    {
        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _provider.GetStockInfoAsync(ticker!));
        Assert.IsInstanceOfType<ArgumentException>(ex.InnerException);
    }

    [TestMethod]
    public async Task GetStockInfoAsync_TickerTooLong_ThrowsWrappedInvalidOperationException()
    {
        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _provider.GetStockInfoAsync("ABCDEFGHIJKLMNOP"));
        Assert.IsInstanceOfType<ArgumentException>(ex.InnerException);
    }

    [TestMethod]
    public async Task GetStockInfoAsync_InvalidTickerCharacters_ThrowsWrappedInvalidOperationException()
    {
        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => _provider.GetStockInfoAsync("AAPL:"));
        Assert.IsInstanceOfType<ArgumentException>(ex.InnerException);
    }

    [TestMethod]
    public async Task GetStockInfoAsync_CaretPrefixTicker_IsAllowed()
    {
        _client.GetQuoteAsync("^VIX", Arg.Any<CancellationToken>())
            .Returns(new AlphaVantageQuote(20.5, 0.4, 2.0, 1_700_000_001));

        var result = await _provider.GetStockInfoAsync("^VIX");

        using var document = JsonDocument.Parse(result);
        Assert.AreEqual("^VIX", document.RootElement.GetProperty("symbol").GetString());
    }
}
