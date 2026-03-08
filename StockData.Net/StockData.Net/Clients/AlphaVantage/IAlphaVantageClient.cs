namespace StockData.Net.Clients.AlphaVantage;

public interface IAlphaVantageClient
{
    Task<AlphaVantageQuote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default);
    Task<List<AlphaVantageCandle>> GetHistoricalPricesAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<List<AlphaVantageNewsItem>> GetNewsAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
