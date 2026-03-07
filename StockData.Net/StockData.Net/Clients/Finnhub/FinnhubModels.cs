using System.Text.Json.Serialization;

namespace StockData.Net.Clients.Finnhub;

public record FinnhubQuote(double CurrentPrice, double Change, double PercentChange, double High, double Low, double Open, double PreviousClose, long Timestamp);
public record FinnhubCandle(long Timestamp, double Open, double High, double Low, double Close, long Volume);
public record FinnhubNewsItem(long Id, string Headline, string Source, string Url, string Summary, long Datetime, List<string> RelatedTickers);

internal sealed class FinnhubQuoteResponse
{
	[JsonPropertyName("c")]
	public double CurrentPrice { get; init; }

	[JsonPropertyName("d")]
	public double Change { get; init; }

	[JsonPropertyName("dp")]
	public double PercentChange { get; init; }

	[JsonPropertyName("h")]
	public double High { get; init; }

	[JsonPropertyName("l")]
	public double Low { get; init; }

	[JsonPropertyName("o")]
	public double Open { get; init; }

	[JsonPropertyName("pc")]
	public double PreviousClose { get; init; }

	[JsonPropertyName("t")]
	public long Timestamp { get; init; }
}

internal sealed class FinnhubCandleResponse
{
	[JsonPropertyName("c")]
	public List<double>? Close { get; init; }

	[JsonPropertyName("h")]
	public List<double>? High { get; init; }

	[JsonPropertyName("l")]
	public List<double>? Low { get; init; }

	[JsonPropertyName("o")]
	public List<double>? Open { get; init; }

	[JsonPropertyName("t")]
	public List<long>? Timestamp { get; init; }

	[JsonPropertyName("v")]
	public List<double>? Volume { get; init; }

	[JsonPropertyName("s")]
	public string? Status { get; init; }
}

internal sealed class FinnhubNewsResponse
{
	[JsonPropertyName("id")]
	public long Id { get; init; }

	[JsonPropertyName("headline")]
	public string? Headline { get; init; }

	[JsonPropertyName("source")]
	public string? Source { get; init; }

	[JsonPropertyName("url")]
	public string? Url { get; init; }

	[JsonPropertyName("summary")]
	public string? Summary { get; init; }

	[JsonPropertyName("datetime")]
	public long Datetime { get; init; }

	[JsonPropertyName("related")]
	public string? Related { get; init; }
}