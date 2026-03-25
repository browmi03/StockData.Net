namespace StockData.Net.Clients.Alpaca;

public interface IAlpacaClient
{
    Task<List<AlpacaBar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime from,
        DateTime to,
        string timeframe = "1Day",
        CancellationToken ct = default);

    Task<AlpacaQuote?> GetLatestQuoteAsync(string symbol, CancellationToken ct = default);

    Task<List<AlpacaNewsArticle>> GetNewsAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken ct = default);

    Task<List<AlpacaNewsArticle>> GetMarketNewsAsync(CancellationToken ct = default);

    Task<bool> GetHealthStatusAsync(CancellationToken ct = default);
}
