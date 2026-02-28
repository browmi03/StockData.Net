using System.Text.Json;

namespace StockData.Net;

/// <summary>
/// Interface for Yahoo Finance API client
/// </summary>
public interface IYahooFinanceClient
{
    /// <summary>
    /// Gets historical stock prices for a ticker
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., "AAPL")</param>
    /// <param name="period">Valid periods: 1d, 5d, 1mo, 3mo, 6mo, 1y, 2y, 5y, 10y, ytd, max</param>
    /// <param name="interval">Valid intervals: 1m, 2m, 5m, 15m, 30m, 60m, 90m, 1h, 1d, 5d, 1wk, 1mo, 3mo</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON string containing historical price data</returns>
    Task<string> GetHistoricalPricesAsync(string ticker, string period = "1mo", string interval = "1d", CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comprehensive stock information for a ticker
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., "AAPL")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON string containing stock information</returns>
    Task<string> GetStockInfoAsync(string ticker, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets news articles for a ticker
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., "AAPL")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Formatted string containing news articles</returns>
    Task<string> GetNewsAsync(string ticker, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets general market news without requiring a specific ticker
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Formatted string containing market news with timestamps and related tickers</returns>
    Task<string> GetMarketNewsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets stock actions (dividends and splits) for a ticker
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., "AAPL")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON string containing stock actions</returns>
    Task<string> GetStockActionsAsync(string ticker, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets financial statements for a ticker
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., "AAPL")</param>
    /// <param name="statementType">Type of financial statement</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON string containing financial statement</returns>
    Task<string> GetFinancialStatementAsync(string ticker, Models.FinancialStatementType statementType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets holder information for a ticker
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., "AAPL")</param>
    /// <param name="holderType">Type of holder information</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON string containing holder information</returns>
    Task<string> GetHolderInfoAsync(string ticker, Models.HolderType holderType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available option expiration dates for a ticker
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., "AAPL")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON string containing expiration dates</returns>
    Task<string> GetOptionExpirationDatesAsync(string ticker, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets option chain for a ticker
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., "AAPL")</param>
    /// <param name="expirationDate">Expiration date (format: YYYY-MM-DD)</param>
    /// <param name="optionType">Type of option (calls or puts)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON string containing option chain</returns>
    Task<string> GetOptionChainAsync(string ticker, string expirationDate, Models.OptionType optionType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets analyst recommendations for a ticker
    /// </summary>
    /// <param name="ticker">Stock ticker symbol (e.g., "AAPL")</param>
    /// <param name="recommendationType">Type of recommendation data</param>
    /// <param name="monthsBack">Number of months back to retrieve (for upgrades/downgrades)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>JSON string containing recommendations</returns>
    Task<string> GetRecommendationsAsync(string ticker, Models.RecommendationType recommendationType, int monthsBack = 12, CancellationToken cancellationToken = default);
}
