using System.Text.Json.Serialization;

namespace StockData.Net.Clients.Polygon;

public record PolygonQuote(double Price, long Timestamp);
public record PolygonAggregateBar(long Timestamp, double Open, double High, double Low, double Close, long Volume);
public record PolygonNewsItem(string Id, string Title, string Publisher, string Url, string Summary, DateTimeOffset PublishedUtc, List<string> Tickers);

internal sealed class PolygonQuoteResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("results")]
    public PolygonQuoteResult? Results { get; init; }
}

internal sealed class PolygonQuoteResult
{
    [JsonPropertyName("p")]
    public double Price { get; init; }

    [JsonPropertyName("t")]
    public long TimestampMs { get; init; }
}

internal sealed class PolygonAggregatesResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("results")]
    public List<PolygonAggregateResult>? Results { get; init; }
}

internal sealed class PolygonAggregateResult
{
    [JsonPropertyName("t")]
    public long TimestampMs { get; init; }

    [JsonPropertyName("o")]
    public double Open { get; init; }

    [JsonPropertyName("h")]
    public double High { get; init; }

    [JsonPropertyName("l")]
    public double Low { get; init; }

    [JsonPropertyName("c")]
    public double Close { get; init; }

    [JsonPropertyName("v")]
    public double Volume { get; init; }
}

internal sealed class PolygonNewsResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("results")]
    public List<PolygonNewsResult>? Results { get; init; }
}

internal sealed class PolygonNewsResult
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("article_url")]
    public string? ArticleUrl { get; init; }

    [JsonPropertyName("published_utc")]
    public string? PublishedUtc { get; init; }

    [JsonPropertyName("tickers")]
    public List<string>? Tickers { get; init; }

    [JsonPropertyName("publisher")]
    public PolygonPublisher? Publisher { get; init; }
}

internal sealed class PolygonPublisher
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
