using System.Text.Json;
using Microsoft.Extensions.Logging;
using StockData.Net.Clients.Polygon;
using StockData.Net.Models;
using StockData.Net.Security;

namespace StockData.Net.Providers;

/// <summary>
/// Polygon implementation of IStockDataProvider.
/// </summary>
public sealed class PolygonProvider : IStockDataProvider
{
    private readonly IPolygonClient _client;
    private readonly ILogger<PolygonProvider> _logger;

    public string ProviderId => "polygon";
    public string ProviderName => "Polygon.io";
    public string Version => "1.0.0";

    public PolygonProvider(IPolygonClient client, ILogger<PolygonProvider> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GetHistoricalPricesAsync(
        string ticker,
        string period = "1mo",
        string interval = "1d",
        CancellationToken cancellationToken = default)
    {
        _ = interval;

        return await ExecuteSafelyAsync(async () =>
        {
            ValidateTicker(ticker);

            var (from, to) = ResolveDateWindow(period);
            var candles = await _client.GetHistoricalPricesAsync(ticker, from.UtcDateTime, to.UtcDateTime, cancellationToken);
            return JsonSerializer.Serialize(MapHistoricalPrices(candles));
        }, nameof(GetHistoricalPricesAsync));
    }

    public async Task<string> GetStockInfoAsync(string ticker, CancellationToken cancellationToken = default)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            ValidateTicker(ticker);

            var quote = await _client.GetQuoteAsync(ticker, cancellationToken)
                ?? throw new InvalidOperationException($"Polygon quote is unavailable for '{ticker}'.");

            var payload = new
            {
                symbol = ticker,
                companyName = ticker,
                price = quote.Price,
                timestamp = DateTimeOffset.FromUnixTimeSeconds(quote.Timestamp).UtcDateTime,
                sourceProvider = ProviderId
            };

            return JsonSerializer.Serialize(payload);
        }, nameof(GetStockInfoAsync));
    }

    public async Task<string> GetNewsAsync(string ticker, CancellationToken cancellationToken = default)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            ValidateTicker(ticker);

            var to = DateTimeOffset.UtcNow;
            var from = to.AddDays(-14);
            var items = await _client.GetNewsAsync(ticker, from.UtcDateTime, to.UtcDateTime, cancellationToken);

            return MapNews(items, ticker);
        }, nameof(GetNewsAsync));
    }

    public Task<string> GetMarketNewsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException($"Provider '{ProviderId}' does not support {nameof(GetMarketNewsAsync)}");
    }

    public Task<string> GetStockActionsAsync(string ticker, CancellationToken cancellationToken = default)
        => Task.FromException<string>(new NotSupportedException($"Provider '{ProviderId}' does not support {nameof(GetStockActionsAsync)}"));

    public Task<string> GetFinancialStatementAsync(
        string ticker,
        FinancialStatementType statementType,
        CancellationToken cancellationToken = default)
        => Task.FromException<string>(new NotSupportedException($"Provider '{ProviderId}' does not support {nameof(GetFinancialStatementAsync)}"));

    public Task<string> GetHolderInfoAsync(
        string ticker,
        HolderType holderType,
        CancellationToken cancellationToken = default)
        => Task.FromException<string>(new NotSupportedException($"Provider '{ProviderId}' does not support {nameof(GetHolderInfoAsync)}"));

    public Task<string> GetOptionExpirationDatesAsync(string ticker, CancellationToken cancellationToken = default)
        => Task.FromException<string>(new NotSupportedException($"Provider '{ProviderId}' does not support {nameof(GetOptionExpirationDatesAsync)}"));

    public Task<string> GetOptionChainAsync(
        string ticker,
        string expirationDate,
        OptionType optionType,
        CancellationToken cancellationToken = default)
        => Task.FromException<string>(new NotSupportedException($"Provider '{ProviderId}' does not support {nameof(GetOptionChainAsync)}"));

    public Task<string> GetRecommendationsAsync(
        string ticker,
        RecommendationType recommendationType,
        int monthsBack = 12,
        CancellationToken cancellationToken = default)
        => Task.FromException<string>(new NotSupportedException($"Provider '{ProviderId}' does not support {nameof(GetRecommendationsAsync)}"));

    public async Task<bool> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var quote = await _client.GetQuoteAsync("AAPL", cancellationToken);
            return quote is not null && quote.Price > 0d;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> ExecuteSafelyAsync(Func<Task<string>> operation, string operationName)
    {
        try
        {
            return await operation();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            _logger.LogWarning("Polygon operation {Operation} failed: {Reason}", operationName, sanitizedMessage);
            throw new InvalidOperationException($"Polygon {operationName} failed: {sanitizedMessage}", ex);
        }
    }

    private static List<object> MapHistoricalPrices(List<PolygonAggregateBar> candles)
    {
        var rows = new List<object>(candles.Count);

        foreach (var candle in candles)
        {
            rows.Add(new
            {
                Date = DateTimeOffset.FromUnixTimeSeconds(candle.Timestamp).UtcDateTime,
                Open = candle.Open,
                High = candle.High,
                Low = candle.Low,
                Close = candle.Close,
                Volume = candle.Volume,
                SourceProvider = "polygon"
            });
        }

        return rows;
    }

    private static string MapNews(List<PolygonNewsItem> items, string ticker)
    {
        if (items.Count == 0)
        {
            return $"No news found for company that searched with {ticker} ticker.";
        }

        var blocks = new List<string>(items.Count);

        foreach (var item in items)
        {
            var published = item.PublishedUtc.ToString("yyyy-MM-dd HH:mm:ss");
            var relatedTickers = item.Tickers.Count > 0
                ? $"\nRelated Tickers: {string.Join(", ", item.Tickers)}"
                : string.Empty;

            blocks.Add($"Title: {item.Title}\nPublisher: {item.Publisher}\nPublished: {published}{relatedTickers}\nURL: {item.Url}");
        }

        return string.Join("\n\n", blocks);
    }

    private static (DateTimeOffset From, DateTimeOffset To) ResolveDateWindow(string period)
    {
        var to = DateTimeOffset.UtcNow;
        var from = period.ToLowerInvariant() switch
        {
            "1d" => to.AddDays(-1),
            "5d" => to.AddDays(-5),
            "1mo" => to.AddMonths(-1),
            "3mo" => to.AddMonths(-3),
            "6mo" => to.AddMonths(-6),
            "1y" => to.AddYears(-1),
            "2y" => to.AddYears(-2),
            "5y" => to.AddYears(-5),
            "10y" => to.AddYears(-10),
            "ytd" => new DateTimeOffset(new DateTime(to.Year, 1, 1), TimeSpan.Zero),
            "max" => to.AddYears(-20),
            _ => to.AddMonths(-1)
        };

        return (from, to);
    }

    private static void ValidateTicker(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            throw new ArgumentException("Ticker symbol cannot be empty or whitespace.", nameof(ticker));
        }

        if (ticker.Length > 20)
        {
            throw new ArgumentException("Ticker symbol cannot exceed 20 characters.", nameof(ticker));
        }

        var symbolBody = ticker.StartsWith("^", StringComparison.Ordinal) ? ticker[1..] : ticker;
        if (!symbolBody.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == ':'))
        {
            throw new ArgumentException("Ticker symbol contains invalid characters.", nameof(ticker));
        }
    }
}
