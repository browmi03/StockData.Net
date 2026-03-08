using System.Text.Json.Serialization;

namespace StockData.Net.Clients.AlphaVantage;

public record AlphaVantageQuote(double Price, double Change, double PercentChange, long Timestamp);
public record AlphaVantageCandle(long Timestamp, double Open, double High, double Low, double Close, long Volume);
public record AlphaVantageNewsItem(string Title, string Source, string Url, string Summary, long Timestamp, List<string> RelatedTickers);

internal sealed class AlphaVantageQuoteResponse
{
    [JsonPropertyName("Global Quote")]
    public AlphaVantageQuoteResult? GlobalQuote { get; init; }

    [JsonPropertyName("Note")]
    public string? Note { get; init; }

    [JsonPropertyName("Information")]
    public string? Information { get; init; }
}

internal sealed class AlphaVantageQuoteResult
{
    [JsonPropertyName("05. price")]
    public string? Price { get; init; }

    [JsonPropertyName("09. change")]
    public string? Change { get; init; }

    [JsonPropertyName("10. change percent")]
    public string? ChangePercent { get; init; }

    [JsonPropertyName("07. latest trading day")]
    public string? LatestTradingDay { get; init; }
}

internal sealed class AlphaVantageTimeSeriesResponse
{
    [JsonPropertyName("Time Series (Daily)")]
    public Dictionary<string, AlphaVantageDailyBar>? TimeSeriesDaily { get; init; }

    [JsonPropertyName("Note")]
    public string? Note { get; init; }

    [JsonPropertyName("Information")]
    public string? Information { get; init; }
}

internal sealed class AlphaVantageDailyBar
{
    [JsonPropertyName("1. open")]
    public string? Open { get; init; }

    [JsonPropertyName("2. high")]
    public string? High { get; init; }

    [JsonPropertyName("3. low")]
    public string? Low { get; init; }

    [JsonPropertyName("4. close")]
    public string? Close { get; init; }

    [JsonPropertyName("6. volume")]
    public string? Volume { get; init; }
}

internal sealed class AlphaVantageNewsResponse
{
    [JsonPropertyName("feed")]
    public List<AlphaVantageNewsEntry>? Feed { get; init; }

    [JsonPropertyName("Note")]
    public string? Note { get; init; }

    [JsonPropertyName("Information")]
    public string? Information { get; init; }
}

internal sealed class AlphaVantageNewsEntry
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("time_published")]
    public string? TimePublished { get; init; }

    [JsonPropertyName("ticker_sentiment")]
    public List<AlphaVantageTickerSentiment>? TickerSentiment { get; init; }
}

internal sealed class AlphaVantageTickerSentiment
{
    [JsonPropertyName("ticker")]
    public string? Ticker { get; init; }
}
