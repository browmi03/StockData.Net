using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using StockData.Net.Configuration;
using StockData.Net.Security;

namespace StockData.Net.Clients.Polygon;

public sealed class PolygonClient : IPolygonClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SecretValue _apiKey;
    private readonly TokenBucketRateLimiter _rateLimiter;

    public PolygonClient(HttpClient httpClient, SecretValue apiKey, RateLimitConfiguration? rateLimit = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));

        if (_httpClient.BaseAddress is not null && _httpClient.BaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Polygon client requires HTTPS base address to enforce TLS transport.");
        }

        var requestsPerMinute = ResolveRequestsPerMinute(rateLimit, 5);

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

    public async Task<PolygonQuote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);

        try
        {
            await AcquireRateLimitPermitAsync(cancellationToken);

            var encodedSymbol = Uri.EscapeDataString(symbol.Trim().ToUpperInvariant());
            var apiKey = Uri.EscapeDataString(_apiKey.ExposeSecret());
            var requestUri = $"v2/last/trade/{encodedSymbol}?apiKey={apiKey}";

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<PolygonQuoteResponse>(stream, JsonOptions, cancellationToken);
            if (payload is null || !string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase) || payload.Results is null)
            {
                return null;
            }

            return new PolygonQuote(
                Price: payload.Results.Price,
                Timestamp: NormalizeUnixSeconds(payload.Results.TimestampMs));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            throw new InvalidOperationException($"Polygon quote request failed: {sanitizedMessage}", ex);
        }
    }

    public async Task<List<PolygonAggregateBar>> GetHistoricalPricesAsync(
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
            var fromDate = from.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var toDate = to.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var requestUri = $"v2/aggs/ticker/{encodedSymbol}/range/1/day/{fromDate}/{toDate}?adjusted=true&sort=asc&limit=5000&apiKey={apiKey}";

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<PolygonAggregatesResponse>(stream, JsonOptions, cancellationToken);
            if (payload is null || !string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase) || payload.Results is null)
            {
                return [];
            }

            var bars = new List<PolygonAggregateBar>(payload.Results.Count);
            foreach (var row in payload.Results)
            {
                bars.Add(new PolygonAggregateBar(
                    Timestamp: NormalizeUnixSeconds(row.TimestampMs),
                    Open: row.Open,
                    High: row.High,
                    Low: row.Low,
                    Close: row.Close,
                    Volume: Convert.ToInt64(Math.Round(row.Volume, MidpointRounding.AwayFromZero))));
            }

            return bars;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            throw new InvalidOperationException($"Polygon historical request failed: {sanitizedMessage}", ex);
        }
    }

    public async Task<List<PolygonNewsItem>> GetNewsAsync(
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
            var publishedGte = Uri.EscapeDataString(from.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            var publishedLte = Uri.EscapeDataString(to.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            var requestUri = $"v2/reference/news?ticker={encodedSymbol}&published_utc.gte={publishedGte}&published_utc.lte={publishedLte}&limit=50&order=desc&sort=published_utc&apiKey={apiKey}";

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<PolygonNewsResponse>(stream, JsonOptions, cancellationToken);
            if (payload is null || !string.Equals(payload.Status, "OK", StringComparison.OrdinalIgnoreCase) || payload.Results is null)
            {
                return [];
            }

            var items = new List<PolygonNewsItem>(payload.Results.Count);
            foreach (var row in payload.Results)
            {
                if (string.IsNullOrWhiteSpace(row.ArticleUrl))
                {
                    continue;
                }

                items.Add(new PolygonNewsItem(
                    Id: row.Id ?? string.Empty,
                    Title: row.Title ?? string.Empty,
                    Publisher: row.Publisher?.Name ?? string.Empty,
                    Url: row.ArticleUrl,
                    Summary: row.Description ?? string.Empty,
                    PublishedUtc: ParsePublishedUtc(row.PublishedUtc),
                    Tickers: row.Tickers ?? []));
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
            throw new InvalidOperationException($"Polygon news request failed: {sanitizedMessage}", ex);
        }
    }

    private async Task AcquireRateLimitPermitAsync(CancellationToken cancellationToken)
    {
        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException("Polygon API rate limit exceeded.");
        }
    }

    private static DateTimeOffset ParsePublishedUtc(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return DateTimeOffset.UtcNow;
        }

        return DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : DateTimeOffset.UtcNow;
    }

    private static long NormalizeUnixSeconds(long timestamp)
    {
        if (timestamp <= 0)
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        return timestamp > 9_999_999_999 ? timestamp / 1000 : timestamp;
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
