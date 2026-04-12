using System.Text.Json.Serialization;

namespace StockData.Net.Clients.Alpaca;

internal sealed class AlpacaBarsResponse
{
    [JsonPropertyName("bars")]
    public List<AlpacaBarResponseItem>? Bars { get; init; }

    [JsonPropertyName("next_page_token")]
    public string? NextPageToken { get; init; }
}

internal sealed class AlpacaBarResponseItem
{
    [JsonPropertyName("t")]
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("o")]
    public double Open { get; init; }

    [JsonPropertyName("h")]
    public double High { get; init; }

    [JsonPropertyName("l")]
    public double Low { get; init; }

    [JsonPropertyName("c")]
    public double Close { get; init; }

    [JsonPropertyName("v")]
    public long Volume { get; init; }

    [JsonPropertyName("n")]
    public long TradeCount { get; init; }

    [JsonPropertyName("vw")]
    public double VWAP { get; init; }
}

internal sealed class AlpacaLatestQuoteResponse
{
    [JsonPropertyName("quote")]
    public AlpacaQuoteResponseItem? Quote { get; init; }
}

internal sealed class AlpacaQuoteResponseItem
{
    [JsonPropertyName("ap")]
    public double AskPrice { get; init; }

    [JsonPropertyName("as")]
    public long AskSize { get; init; }

    [JsonPropertyName("bp")]
    public double BidPrice { get; init; }

    [JsonPropertyName("bs")]
    public long BidSize { get; init; }

    [JsonPropertyName("t")]
    public DateTime Timestamp { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }
}

internal sealed class AlpacaNewsResponse
{
    [JsonPropertyName("news")]
    public List<AlpacaNewsResponseItem>? News { get; init; }

    [JsonPropertyName("next_page_token")]
    public string? NextPageToken { get; init; }
}

internal sealed class AlpacaNewsResponseItem
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("headline")]
    public string? Headline { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("symbols")]
    public List<string>? Symbols { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }
}
