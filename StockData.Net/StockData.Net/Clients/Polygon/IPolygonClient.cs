namespace StockData.Net.Clients.Polygon;

public interface IPolygonClient
{
    Task<PolygonQuote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default);
    Task<List<PolygonAggregateBar>> GetHistoricalPricesAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);
    Task<List<PolygonNewsItem>> GetNewsAsync(string symbol, DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
