namespace StockData.Net.Clients.Finnhub;

public interface IFinnhubClient
{
    Task<FinnhubQuote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default);
    Task<List<FinnhubCandle>> GetHistoricalPricesAsync(string symbol, string resolution, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<List<FinnhubNewsItem>> GetNewsAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<IEnumerable<MarketNewsItem>> GetMarketNewsAsync(string category, CancellationToken cancellationToken = default);
    Task<IEnumerable<RecommendationTrend>> GetRecommendationTrendsAsync(string symbol, CancellationToken cancellationToken = default);
}
