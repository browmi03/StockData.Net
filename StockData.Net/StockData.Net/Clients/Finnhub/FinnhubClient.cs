using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using StockData.Net.Configuration;
using StockData.Net.Security;

namespace StockData.Net.Clients.Finnhub;

public sealed class FinnhubClient : IFinnhubClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SecretValue _apiKey;
    private readonly TokenBucketRateLimiter _rateLimiter;

    public FinnhubClient(HttpClient httpClient, SecretValue apiKey, RateLimitConfiguration? rateLimit = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));

        if (_httpClient.BaseAddress is not null && _httpClient.BaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Finnhub client requires HTTPS base address to enforce TLS transport.");
        }

        var requestsPerMinute = ResolveRequestsPerMinute(rateLimit, 60);

        _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = requestsPerMinute,
            TokensPerPeriod = requestsPerMinute,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            AutoReplenishment = true,
            QueueLimit = 100,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    }

    public async Task<FinnhubQuote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);

        try
        {
            await AcquireRateLimitPermitAsync(cancellationToken);

            var encodedSymbol = Uri.EscapeDataString(symbol.Trim().ToUpperInvariant());
            var apiKey = Uri.EscapeDataString(_apiKey.ExposeSecret());
            var requestUri = $"quote?symbol={encodedSymbol}&token={apiKey}";

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<FinnhubQuoteResponse>(stream, JsonOptions, cancellationToken);
            if (payload is null)
            {
                return null;
            }

            return new FinnhubQuote(
                CurrentPrice: payload.CurrentPrice,
                Change: payload.Change,
                PercentChange: payload.PercentChange,
                High: payload.High,
                Low: payload.Low,
                Open: payload.Open,
                PreviousClose: payload.PreviousClose,
                Timestamp: NormalizeUnixSeconds(payload.Timestamp));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            throw new InvalidOperationException($"Finnhub quote request failed: {sanitizedMessage}", ex);
        }
    }

    public async Task<List<FinnhubCandle>> GetHistoricalPricesAsync(
        string symbol,
        string resolution,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);
        if (string.IsNullOrWhiteSpace(resolution))
        {
            throw new ArgumentException("Resolution cannot be empty or whitespace.", nameof(resolution));
        }

        if (from > to)
        {
            throw new ArgumentException("From date cannot be after To date.", nameof(from));
        }

        try
        {
            await AcquireRateLimitPermitAsync(cancellationToken);

            var encodedSymbol = Uri.EscapeDataString(symbol.Trim().ToUpperInvariant());
            var apiKey = Uri.EscapeDataString(_apiKey.ExposeSecret());
            var fromUnix = new DateTimeOffset(from.ToUniversalTime()).ToUnixTimeSeconds();
            var toUnix = new DateTimeOffset(to.ToUniversalTime()).ToUnixTimeSeconds();
            var encodedResolution = Uri.EscapeDataString(resolution.Trim().ToUpperInvariant());

            var requestUri =
                $"stock/candle?symbol={encodedSymbol}&resolution={encodedResolution}&from={fromUnix}&to={toUnix}&token={apiKey}";

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<FinnhubCandleResponse>(stream, JsonOptions, cancellationToken);

            if (payload is null ||
                !string.Equals(payload.Status, "ok", StringComparison.OrdinalIgnoreCase) ||
                payload.Timestamp is null || payload.Open is null || payload.High is null || payload.Low is null || payload.Close is null || payload.Volume is null)
            {
                return [];
            }

            var count = new[]
            {
                payload.Timestamp.Count,
                payload.Open.Count,
                payload.High.Count,
                payload.Low.Count,
                payload.Close.Count,
                payload.Volume.Count
            }.Min();

            var candles = new List<FinnhubCandle>(count);
            for (var i = 0; i < count; i++)
            {
                candles.Add(new FinnhubCandle(
                    Timestamp: NormalizeUnixSeconds(payload.Timestamp[i]),
                    Open: payload.Open[i],
                    High: payload.High[i],
                    Low: payload.Low[i],
                    Close: payload.Close[i],
                    Volume: Convert.ToInt64(Math.Round(payload.Volume[i], MidpointRounding.AwayFromZero))));
            }

            return candles;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            throw new InvalidOperationException($"Finnhub historical request failed: {sanitizedMessage}", ex);
        }
    }

    public async Task<List<FinnhubNewsItem>> GetNewsAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);
        if (from > to)
        {
            throw new ArgumentException("From date cannot be after To date.", nameof(from));
        }

        try
        {
            await AcquireRateLimitPermitAsync(cancellationToken);

            var encodedSymbol = Uri.EscapeDataString(symbol.Trim().ToUpperInvariant());
            var apiKey = Uri.EscapeDataString(_apiKey.ExposeSecret());
            var fromDate = from.ToUniversalTime().Date;
            var toDate = to.ToUniversalTime().Date;
            var requestUri =
                $"company-news?symbol={encodedSymbol}&from={fromDate:yyyy-MM-dd}&to={toDate:yyyy-MM-dd}&token={apiKey}";

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<List<FinnhubNewsResponse>>(stream, JsonOptions, cancellationToken);
            if (payload is null || payload.Count == 0)
            {
                return [];
            }

            var items = new List<FinnhubNewsItem>(payload.Count);
            foreach (var entry in payload)
            {
                items.Add(new FinnhubNewsItem(
                    Id: entry.Id,
                    Headline: entry.Headline ?? string.Empty,
                    Source: entry.Source ?? string.Empty,
                    Url: entry.Url ?? string.Empty,
                    Summary: entry.Summary ?? string.Empty,
                    Datetime: NormalizeUnixSeconds(entry.Datetime),
                    RelatedTickers: ParseRelatedTickers(entry.Related)));
            }

            return items;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            throw new InvalidOperationException($"Finnhub news request failed: {sanitizedMessage}", ex);
        }
    }

    public async Task<IEnumerable<MarketNewsItem>> GetMarketNewsAsync(string category, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException("Category cannot be empty or whitespace.", nameof(category));
        }

        try
        {
            await AcquireRateLimitPermitAsync(cancellationToken);

            var encodedCategory = Uri.EscapeDataString(category.Trim().ToLowerInvariant());
            var apiKey = Uri.EscapeDataString(_apiKey.ExposeSecret());
            var requestUri = $"news?category={encodedCategory}&token={apiKey}";

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<List<FinnhubMarketNewsResponse>>(stream, JsonOptions, cancellationToken);
            if (payload is null || payload.Count == 0)
            {
                return [];
            }

            return payload.Select(entry => new MarketNewsItem(
                Id: entry.Id,
                Category: entry.Category ?? string.Empty,
                Datetime: NormalizeUnixSeconds(entry.Datetime),
                Headline: entry.Headline ?? string.Empty,
                Image: entry.Image ?? string.Empty,
                Related: entry.Related ?? string.Empty,
                Source: entry.Source ?? string.Empty,
                Summary: entry.Summary ?? string.Empty,
                Url: entry.Url ?? string.Empty));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            throw new InvalidOperationException($"Finnhub market news request failed: {sanitizedMessage}", ex);
        }
    }

    public async Task<IEnumerable<RecommendationTrend>> GetRecommendationTrendsAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);

        try
        {
            await AcquireRateLimitPermitAsync(cancellationToken);

            var encodedSymbol = Uri.EscapeDataString(symbol.Trim().ToUpperInvariant());
            var apiKey = Uri.EscapeDataString(_apiKey.ExposeSecret());
            var requestUri = $"stock/recommendation?symbol={encodedSymbol}&token={apiKey}";

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<List<FinnhubRecommendationResponse>>(stream, JsonOptions, cancellationToken);
            if (payload is null || payload.Count == 0)
            {
                return [];
            }

            return payload.Select(entry => new RecommendationTrend(
                Buy: entry.Buy,
                Hold: entry.Hold,
                Period: entry.Period ?? string.Empty,
                Sell: entry.Sell,
                StrongBuy: entry.StrongBuy,
                StrongSell: entry.StrongSell,
                Symbol: entry.Symbol ?? symbol.ToUpperInvariant()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            throw new InvalidOperationException($"Finnhub recommendation request failed: {sanitizedMessage}", ex);
        }
    }

    private async Task AcquireRateLimitPermitAsync(CancellationToken cancellationToken)
    {
        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException("Finnhub API rate limit exceeded.");
        }
    }

    private static long NormalizeUnixSeconds(long timestamp)
    {
        if (timestamp <= 0)
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        return timestamp > 9_999_999_999 ? timestamp / 1000 : timestamp;
    }

    private static List<string> ParseRelatedTickers(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return [];
        }

        return input
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void ValidateSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be empty or whitespace.", nameof(symbol));
        }
    }

    private static int ResolveRequestsPerMinute(RateLimitConfiguration? rateLimit, int fallback)
    {
        if (rateLimit is null)
        {
            return fallback;
        }

        return rateLimit.RequestsPerMinute > 0 ? rateLimit.RequestsPerMinute : fallback;
    }
}