using System.Text.Json;
using Microsoft.Extensions.Logging;
using StockData.Net.Clients.Finnhub;
using StockData.Net.Models;
using StockData.Net.Security;

namespace StockData.Net.Providers;

/// <summary>
/// Finnhub implementation of IStockDataProvider.
/// </summary>
public sealed class FinnhubProvider : IStockDataProvider
{
    private static readonly IReadOnlySet<string> FreeCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "stock_info",
        "news",
        "market_news",
        "recommendations"
    };

    private static readonly IReadOnlySet<string> PaidCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "stock_info",
        "news",
        "market_news",
        "recommendations",
        "historical_prices",
        "stock_actions"
    };

    private readonly StockData.Net.Clients.Finnhub.IFinnhubClient _client;
    private readonly ILogger<FinnhubProvider> _logger;

    public string ProviderId => "finnhub";
    public string ProviderName => "Finnhub";
    public string Version => "1.0.0";

    public FinnhubProvider(StockData.Net.Clients.Finnhub.IFinnhubClient client, ILogger<FinnhubProvider> logger)
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
        return await ExecuteSafelyAsync(async () =>
        {
            ValidateTicker(ticker);

            var (from, to) = ResolveDateWindow(period);
            var resolution = ResolveResolution(interval);
            var candles = await _client.GetHistoricalPricesAsync(ticker, resolution, from.UtcDateTime, to.UtcDateTime, cancellationToken);
            return JsonSerializer.Serialize(MapHistoricalPrices(candles));
        }, nameof(GetHistoricalPricesAsync));
    }

    public async Task<string> GetStockInfoAsync(string ticker, CancellationToken cancellationToken = default)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            ValidateTicker(ticker);

            var quote = await _client.GetQuoteAsync(ticker, cancellationToken)
                ?? throw new InvalidOperationException($"Finnhub quote is unavailable for '{ticker}'.");

            var payload = new
            {
                symbol = ticker,
                companyName = ticker,
                price = quote.CurrentPrice,
                open = quote.Open,
                high = quote.High,
                low = quote.Low,
                previousClose = quote.PreviousClose,
                change = quote.Change,
                percentChange = quote.PercentChange,
                timestamp = DateTimeOffset.FromUnixTimeSeconds(quote.Timestamp).UtcDateTime,
                sourceProvider = ProviderId,
                country = InferCountryFromTicker(ticker)
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

    public async Task<string> GetMarketNewsAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            var items = await _client.GetMarketNewsAsync("general", cancellationToken);
            return MapMarketNews(items);
        }, nameof(GetMarketNewsAsync));
    }

    public Task<string> GetStockActionsAsync(string ticker, CancellationToken cancellationToken = default)
    {
        throw new TierAwareNotSupportedException(ProviderId, nameof(GetStockActionsAsync), availableOnPaidTier: true);
    }

    public Task<string> GetFinancialStatementAsync(
        string ticker,
        FinancialStatementType statementType,
        CancellationToken cancellationToken = default)
    {
        throw new TierAwareNotSupportedException(ProviderId, nameof(GetFinancialStatementAsync), availableOnPaidTier: true);
    }

    public Task<string> GetHolderInfoAsync(
        string ticker,
        HolderType holderType,
        CancellationToken cancellationToken = default)
    {
        throw new TierAwareNotSupportedException(ProviderId, nameof(GetHolderInfoAsync), availableOnPaidTier: true);
    }

    public Task<string> GetOptionExpirationDatesAsync(string ticker, CancellationToken cancellationToken = default)
    {
        throw new TierAwareNotSupportedException(ProviderId, nameof(GetOptionExpirationDatesAsync), availableOnPaidTier: false);
    }

    public Task<string> GetOptionChainAsync(
        string ticker,
        string expirationDate,
        OptionType optionType,
        CancellationToken cancellationToken = default)
    {
        throw new TierAwareNotSupportedException(ProviderId, nameof(GetOptionChainAsync), availableOnPaidTier: false);
    }

    public async Task<string> GetRecommendationsAsync(
        string ticker,
        RecommendationType recommendationType,
        int monthsBack = 12,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            ValidateTicker(ticker);
            var trends = await _client.GetRecommendationTrendsAsync(ticker, cancellationToken);
            var payload = trends.Select(trend => new
            {
                trend.Symbol,
                trend.Period,
                trend.StrongBuy,
                trend.Buy,
                trend.Hold,
                trend.Sell,
                trend.StrongSell,
                SourceProvider = ProviderId
            });

            return JsonSerializer.Serialize(payload);
        }, nameof(GetRecommendationsAsync));
    }

    public IReadOnlySet<string> GetSupportedDataTypes(string tier)
    {
        return string.Equals(tier, "paid", StringComparison.OrdinalIgnoreCase)
            ? PaidCapabilities
            : FreeCapabilities;
    }

    public async Task<bool> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var quote = await _client.GetQuoteAsync("AAPL", cancellationToken);
            return quote is not null && quote.CurrentPrice > 0d;
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
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            _logger.LogWarning("Finnhub operation {Operation} failed: {Reason}", operationName, sanitizedMessage);
            throw new InvalidOperationException($"Finnhub {operationName} failed: {sanitizedMessage}", ex);
        }
    }

    private static List<object> MapHistoricalPrices(List<FinnhubCandle> candles)
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
                SourceProvider = "finnhub"
            });
        }

        return rows;
    }

    private static string MapNews(List<FinnhubNewsItem> items, string ticker)
    {
        if (items.Count == 0)
        {
            return $"No news found for company that searched with {ticker} ticker.";
        }

        var blocks = new List<string>(items.Count);

        foreach (var item in items)
        {
            var published = item.Datetime > 0
                ? DateTimeOffset.FromUnixTimeSeconds(item.Datetime).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")
                : "Unknown";

            var relatedTickers = item.RelatedTickers.Count > 0
                ? $"\nRelated Tickers: {string.Join(", ", item.RelatedTickers)}"
                : string.Empty;

            blocks.Add($"Title: {item.Headline}\nPublisher: {item.Source}\nPublished: {published}{relatedTickers}\nURL: {item.Url}");
        }

        return string.Join("\n\n", blocks);
    }

    private static string MapMarketNews(IEnumerable<MarketNewsItem> items)
    {
        var itemList = items.ToList();
        if (itemList.Count == 0)
        {
            return "No market news found.";
        }

        var blocks = new List<string>(itemList.Count);

        foreach (var item in itemList)
        {
            var published = item.Datetime > 0
                ? DateTimeOffset.FromUnixTimeSeconds(item.Datetime).UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss")
                : "Unknown";

            var relatedTickers = string.IsNullOrWhiteSpace(item.Related)
                ? string.Empty
                : $"\nRelated Tickers: {item.Related}";

            blocks.Add($"Title: {item.Headline}\nPublisher: {item.Source}\nPublished: {published}{relatedTickers}\nURL: {item.Url}");
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

    private static string ResolveResolution(string interval)
    {
        return interval.ToLowerInvariant() switch
        {
            "1m" => "1",
            "5m" => "5",
            "15m" => "15",
            "30m" => "30",
            "60m" or "1h" => "60",
            "1d" => "D",
            "1wk" => "W",
            "1mo" => "M",
            _ => "D"
        };
    }

    private static void ValidateTicker(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            throw new ArgumentException("Ticker symbol cannot be empty or whitespace.", nameof(ticker));
        }

        if (ticker.Length > 10)
        {
            throw new ArgumentException("Ticker symbol cannot exceed 10 characters.", nameof(ticker));
        }

        var symbolBody = ticker.StartsWith("^", StringComparison.Ordinal) ? ticker[1..] : ticker;
        if (!symbolBody.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-'))
        {
            throw new ArgumentException("Ticker symbol contains invalid characters.", nameof(ticker));
        }
    }

    private static string InferCountryFromTicker(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
            return "Unknown";

        // Check for exchange suffixes
        var upperTicker = ticker.ToUpperInvariant();
        
        // Canadian exchanges
        if (upperTicker.EndsWith(".TO") || upperTicker.EndsWith(".V") || upperTicker.EndsWith(".CN"))
            return "Canada";
        
        // Australian exchange
        if (upperTicker.EndsWith(".AX"))
            return "Australia";
        
        // London exchange
        if (upperTicker.EndsWith(".L"))
            return "United Kingdom";
        
        // German exchanges
        if (upperTicker.EndsWith(".DE") || upperTicker.EndsWith(".F") || upperTicker.EndsWith(".ETR") || upperTicker.EndsWith(".DB"))
            return "Germany";
        
        // French exchange
        if (upperTicker.EndsWith(".PA"))
            return "France";
        
        // Hong Kong exchange
        if (upperTicker.EndsWith(".HK"))
            return "Hong Kong";
        
        // Japanese exchange
        if (upperTicker.EndsWith(".T") || upperTicker.EndsWith(".TYO"))
            return "Japan";
        
        // Default to US for most cases (NASDAQ, NYSE, etc.)
        return "United States";
    }
}
