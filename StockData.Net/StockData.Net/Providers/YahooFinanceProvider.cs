using StockData.Net.Models;

namespace StockData.Net.Providers;

/// <summary>
/// Yahoo Finance implementation of IStockDataProvider
/// Wraps the existing YahooFinanceClient to conform to the provider abstraction
/// </summary>
public class YahooFinanceProvider : IStockDataProvider
{
    private readonly IYahooFinanceClient _client;

    public string ProviderId => "yahoo_finance";
    public string ProviderName => "Yahoo Finance";
    public string Version => "1.0.0";

    public YahooFinanceProvider(IYahooFinanceClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<string> GetHistoricalPricesAsync(string ticker, string period = "1mo", string interval = "1d", CancellationToken cancellationToken = default)
    {
        ValidateTicker(ticker);
        return _client.GetHistoricalPricesAsync(ticker, period, interval, cancellationToken);
    }

    public Task<string> GetStockInfoAsync(string ticker, CancellationToken cancellationToken = default)
    {
        ValidateTicker(ticker);
        return _client.GetStockInfoAsync(ticker, cancellationToken);
    }

    public Task<string> GetNewsAsync(string ticker, CancellationToken cancellationToken = default)
    {
        ValidateTicker(ticker);
        return _client.GetNewsAsync(ticker, cancellationToken);
    }

    public Task<string> GetMarketNewsAsync(CancellationToken cancellationToken = default)
    {
        return _client.GetMarketNewsAsync(cancellationToken);
    }

    public Task<string> GetStockActionsAsync(string ticker, CancellationToken cancellationToken = default)
    {
        ValidateTicker(ticker);
        return _client.GetStockActionsAsync(ticker, cancellationToken);
    }

    public Task<string> GetFinancialStatementAsync(string ticker, FinancialStatementType statementType, CancellationToken cancellationToken = default)
    {
        ValidateTicker(ticker);
        return _client.GetFinancialStatementAsync(ticker, statementType, cancellationToken);
    }

    public Task<string> GetHolderInfoAsync(string ticker, HolderType holderType, CancellationToken cancellationToken = default)
    {
        ValidateTicker(ticker);
        return _client.GetHolderInfoAsync(ticker, holderType, cancellationToken);
    }

    public Task<string> GetOptionExpirationDatesAsync(string ticker, CancellationToken cancellationToken = default)
    {
        ValidateTicker(ticker);
        return _client.GetOptionExpirationDatesAsync(ticker, cancellationToken);
    }

    public Task<string> GetOptionChainAsync(string ticker, string expirationDate, OptionType optionType, CancellationToken cancellationToken = default)
    {
        ValidateTicker(ticker);
        return _client.GetOptionChainAsync(ticker, expirationDate, optionType, cancellationToken);
    }

    public Task<string> GetRecommendationsAsync(string ticker, RecommendationType recommendationType, int monthsBack = 12, CancellationToken cancellationToken = default)
    {
        ValidateTicker(ticker);
        return _client.GetRecommendationsAsync(ticker, recommendationType, monthsBack, cancellationToken);
    }

    public async Task<bool> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Perform a lightweight health check using a simple API call
            // Use a well-known ticker to check if the API is responsive
            var result = await _client.GetStockInfoAsync("AAPL", cancellationToken);
            
            // Check if result indicates success (not an error message)
            return !result.StartsWith("Error:", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void ValidateTicker(string ticker)
    {
        if (string.IsNullOrWhiteSpace(ticker))
        {
            throw new ArgumentException("Ticker symbol cannot be empty or whitespace", nameof(ticker));
        }

        if (ticker.Length > 10)
        {
            throw new ArgumentException("Ticker symbol cannot exceed 10 characters", nameof(ticker));
        }

        // Allow alphanumeric characters, dots, and hyphens
        if (!ticker.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-'))
        {
            throw new ArgumentException("Ticker symbol contains invalid characters", nameof(ticker));
        }
    }
}
