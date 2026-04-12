namespace StockData.Net.Clients.AlphaVantage;

public interface IAlphaVantageClient
{
    Task<AlphaVantageQuote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default);
    Task<List<AlphaVantageCandle>> GetHistoricalPricesAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<List<AlphaVantageNewsItem>> GetNewsAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlphaVantageMacroNewsItem>> GetMacroNewsSentimentAsync(string topics, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IEnumerable<NewsItem>> GetMarketNewsAsync(CancellationToken cancellationToken = default);
    Task<StockActionsResult> GetStockActionsAsync(string symbol, CancellationToken cancellationToken = default);
}
