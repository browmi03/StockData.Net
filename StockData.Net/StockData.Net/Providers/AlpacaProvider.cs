using System.Text.Json;
using Microsoft.Extensions.Logging;
using StockData.Net.Clients.Alpaca;
using StockData.Net.Models;
using StockData.Net.Security;

namespace StockData.Net.Providers;

public sealed class AlpacaProvider : IStockDataProvider
{
    private static readonly IReadOnlySet<string> FreeCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "historical_prices",
        "stock_info"
    };

    private static readonly IReadOnlySet<string> PaidCapabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "historical_prices",
        "stock_info",
        "news",
        "market_news"
    };

    private readonly IAlpacaClient _client;
    private readonly ILogger<AlpacaProvider> _logger;

    public string ProviderId => "alpaca";
    public string ProviderName => "Alpaca Markets";
    public string Version => "1.0.0";

    public AlpacaProvider(IAlpacaClient client, ILogger<AlpacaProvider> logger)
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
            var timeframe = ResolveTimeframe(interval);
            var bars = await _client.GetHistoricalBarsAsync(ticker, from.UtcDateTime, to.UtcDateTime, timeframe, cancellationToken);
            return JsonSerializer.Serialize(MapHistoricalPrices(bars));
        }, nameof(GetHistoricalPricesAsync));
    }

    public async Task<string> GetStockInfoAsync(string ticker, CancellationToken cancellationToken = default)
    {
        return await ExecuteSafelyAsync(async () =>
        {
            ValidateTicker(ticker);

            var quote = await _client.GetLatestQuoteAsync(ticker, cancellationToken)
                ?? throw new InvalidOperationException($"Alpaca quote is unavailable for '{ticker}'.");

            var payload = new
            {
                symbol = ticker,
                bidPrice = quote.BidPrice,
                askPrice = quote.AskPrice,
                midPrice = (quote.BidPrice + quote.AskPrice) / 2d,
                timestamp = quote.Timestamp,
                sourceProvider = ProviderId,
                country = InferCountryFromSymbol(ticker)
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
            var items = await _client.GetMarketNewsAsync(cancellationToken);
            return MapMarketNews(items);
        }, nameof(GetMarketNewsAsync));
    }

    public Task<string> GetStockActionsAsync(string ticker, CancellationToken cancellationToken = default)
        => Task.FromException<string>(new TierAwareNotSupportedException(ProviderId, nameof(GetStockActionsAsync), availableOnPaidTier: false));

    public Task<string> GetFinancialStatementAsync(
        string ticker,
        FinancialStatementType statementType,
        CancellationToken cancellationToken = default)
        => Task.FromException<string>(new TierAwareNotSupportedException(ProviderId, nameof(GetFinancialStatementAsync), availableOnPaidTier: false));

    public Task<string> GetHolderInfoAsync(
        string ticker,
        HolderType holderType,
        CancellationToken cancellationToken = default)
        => Task.FromException<string>(new TierAwareNotSupportedException(ProviderId, nameof(GetHolderInfoAsync), availableOnPaidTier: false));

    public Task<string> GetOptionExpirationDatesAsync(string ticker, CancellationToken cancellationToken = default)
        => Task.FromException<string>(new TierAwareNotSupportedException(ProviderId, nameof(GetOptionExpirationDatesAsync), availableOnPaidTier: false));

    public Task<string> GetOptionChainAsync(
        string ticker,
        string expirationDate,
        OptionType optionType,
        CancellationToken cancellationToken = default)
        => Task.FromException<string>(new TierAwareNotSupportedException(ProviderId, nameof(GetOptionChainAsync), availableOnPaidTier: false));

    public Task<string> GetRecommendationsAsync(
        string ticker,
        RecommendationType recommendationType,
        int monthsBack = 12,
        CancellationToken cancellationToken = default)
        => Task.FromException<string>(new TierAwareNotSupportedException(ProviderId, nameof(GetRecommendationsAsync), availableOnPaidTier: false));

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
            return await _client.GetHealthStatusAsync(cancellationToken);
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
            _logger.LogWarning("Alpaca operation {Operation} failed: {Reason}", operationName, sanitizedMessage);
            throw new InvalidOperationException($"Alpaca {operationName} failed: {sanitizedMessage}", ex);
        }
    }

    private static List<object> MapHistoricalPrices(List<AlpacaBar> bars)
    {
        var rows = new List<object>(bars.Count);
        foreach (var bar in bars)
        {
            rows.Add(new
            {
                Date = bar.Timestamp,
                Open = bar.Open,
                High = bar.High,
                Low = bar.Low,
                Close = bar.Close,
                Volume = bar.Volume,
                SourceProvider = "alpaca"
            });
        }

        return rows;
    }

    private static string MapNews(List<AlpacaNewsArticle> items, string ticker)
    {
        if (items.Count == 0)
        {
            return $"No news found for company that searched with {ticker} ticker.";
        }

        var blocks = new List<string>(items.Count);
        foreach (var item in items)
        {
            var published = item.CreatedAt == default
                ? "Unknown"
                : item.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");

            var relatedTickers = item.Symbols.Count > 0
                ? $"\nRelated Tickers: {string.Join(", ", item.Symbols)}"
                : string.Empty;

            blocks.Add($"Title: {item.Headline}\nPublisher: {item.Source}\nPublished: {published}{relatedTickers}\nURL: {item.Url}\nSummary: {item.Summary}");
        }

        return string.Join("\n\n", blocks);
    }

    private static string MapMarketNews(List<AlpacaNewsArticle> items)
    {
        if (items.Count == 0)
        {
            return "No market news found.";
        }

        var blocks = new List<string>(items.Count);
        foreach (var item in items)
        {
            var published = item.CreatedAt == default
                ? "Unknown"
                : item.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss");

            var relatedTickers = item.Symbols.Count > 0
                ? $"\nRelated Tickers: {string.Join(", ", item.Symbols)}"
                : string.Empty;

            blocks.Add($"Title: {item.Headline}\nPublisher: {item.Source}\nPublished: {published}{relatedTickers}\nURL: {item.Url}\nSummary: {item.Summary}");
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

    private static string ResolveTimeframe(string interval)
    {
        return interval.ToLowerInvariant() switch
        {
            "1m" => "1Min",
            "5m" => "5Min",
            "15m" => "15Min",
            "30m" => "30Min",
            "60m" or "1h" => "1Hour",
            "1d" => "1Day",
            _ => "1Day"
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

    private static string InferCountryFromSymbol(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            return "Unknown";
        }

        // Remove any prefix like "^" for indices
        var cleanSymbol = ticker.StartsWith("^", StringComparison.Ordinal) ? ticker[1..] : ticker;
        
        // Check for common country suffixes in ticker symbols
        if (cleanSymbol.EndsWith(".TO", StringComparison.OrdinalIgnoreCase))
        {
            return "Canada";
        }
        else if (cleanSymbol.EndsWith(".L", StringComparison.OrdinalIgnoreCase))
        {
            return "United Kingdom";
        }
        else if (cleanSymbol.EndsWith(".AX", StringComparison.OrdinalIgnoreCase))
        {
            return "Australia";
        }
        else if (cleanSymbol.EndsWith(".DE", StringComparison.OrdinalIgnoreCase))
        {
            return "Germany";
        }
        else if (cleanSymbol.EndsWith(".PA", StringComparison.OrdinalIgnoreCase))
        {
            return "France";
        }
        else if (cleanSymbol.EndsWith(".AS", StringComparison.OrdinalIgnoreCase))
        {
            return "Netherlands";
        }
        else if (cleanSymbol.EndsWith(".SW", StringComparison.OrdinalIgnoreCase))
        {
            return "Switzerland";
        }
        else if (cleanSymbol.EndsWith(".HK", StringComparison.OrdinalIgnoreCase))
        {
            return "Hong Kong";
        }
        else if (cleanSymbol.EndsWith(".T", StringComparison.OrdinalIgnoreCase))
        {
            return "Japan";
        }
        else if (cleanSymbol.EndsWith(".KS", StringComparison.OrdinalIgnoreCase))
        {
            return "South Korea";
        }
        else if (cleanSymbol.EndsWith(".SS", StringComparison.OrdinalIgnoreCase))
        {
            return "China";
        }
        else if (cleanSymbol.EndsWith(".SZ", StringComparison.OrdinalIgnoreCase))
        {
            return "China";
        }
        else if (cleanSymbol.Contains('.'))
        {
            // Has a dot but not a recognized suffix
            return "International";
        }
        else
        {
            // No dot, likely a US ticker
            return "United States";
        }
    }
}
