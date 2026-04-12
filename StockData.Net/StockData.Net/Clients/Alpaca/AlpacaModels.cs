namespace StockData.Net.Clients.Alpaca;

public record AlpacaBar(
    DateTime Timestamp,
    double Open,
    double High,
    double Low,
    double Close,
    long Volume,
    long TradeCount,
    double VWAP);

public record AlpacaQuote(
    double AskPrice,
    long AskSize,
    double BidPrice,
    long BidSize,
    DateTime Timestamp,
    string? Country = null);

public record AlpacaNewsArticle(
    string Id,
    string Headline,
    string Summary,
    string Url,
    string Source,
    DateTime CreatedAt,
    List<string> Symbols,
    string? Country = null);
