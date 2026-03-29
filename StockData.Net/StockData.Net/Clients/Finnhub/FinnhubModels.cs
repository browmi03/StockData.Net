using System.Text.Json.Serialization;

namespace StockData.Net.Clients.Finnhub;

public record FinnhubQuote(double CurrentPrice, double Change, double PercentChange, double High, double Low, double Open, double PreviousClose, long Timestamp, string Country);
public record FinnhubCandle(long Timestamp, double Open, double High, double Low, double Close, long Volume);
public record FinnhubNewsItem(long Id, string Headline, string Source, string Url, string Summary, long Datetime, List<string> RelatedTickers);
public record MarketNewsItem(long Id, string Category, long Datetime, string Headline, string Image, string Related, string Source, string Summary, string Url);
public record RecommendationTrend(int Buy, int Hold, string Period, int Sell, int StrongBuy, int StrongSell, string Symbol);

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

internal sealed class FinnhubMarketNewsResponse
{
	[JsonPropertyName("id")]
	public long Id { get; init; }

	[JsonPropertyName("category")]
	public string? Category { get; init; }

	[JsonPropertyName("datetime")]
	public long Datetime { get; init; }

	[JsonPropertyName("headline")]
	public string? Headline { get; init; }

	[JsonPropertyName("image")]
	public string? Image { get; init; }

	[JsonPropertyName("related")]
	public string? Related { get; init; }

	[JsonPropertyName("source")]
	public string? Source { get; init; }

	[JsonPropertyName("summary")]
	public string? Summary { get; init; }

	[JsonPropertyName("url")]
	public string? Url { get; init; }
}

internal sealed class FinnhubRecommendationResponse
{
	[JsonPropertyName("buy")]
	public int Buy { get; init; }

	[JsonPropertyName("hold")]
	public int Hold { get; init; }

	[JsonPropertyName("period")]
	public string? Period { get; init; }

	[JsonPropertyName("sell")]
	public int Sell { get; init; }

	[JsonPropertyName("strongBuy")]
	public int StrongBuy { get; init; }

	[JsonPropertyName("strongSell")]
	public int StrongSell { get; init; }

	[JsonPropertyName("symbol")]
	public string? Symbol { get; init; }
}
