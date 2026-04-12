using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using StockData.Net.Configuration;
using StockData.Net.Security;

namespace StockData.Net.Clients.Alpaca;

public sealed class AlpacaClient : IAlpacaClient
{
    private const string ApiKeyIdHeader = "APCA-API-KEY-ID";
    private const string ApiSecretKeyHeader = "APCA-API-SECRET-KEY";

    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "api.alpaca.markets",
        "paper-api.alpaca.markets",
        "data.alpaca.markets"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SecretValue _apiKeyId;
    private readonly SecretValue _apiSecretKey;
    private readonly TokenBucketRateLimiter _rateLimiter;

    public AlpacaClient(HttpClient httpClient, SecretValue apiKeyId, SecretValue apiSecretKey, RateLimitConfiguration? rateLimit = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKeyId = apiKeyId ?? throw new ArgumentNullException(nameof(apiKeyId));
        _apiSecretKey = apiSecretKey ?? throw new ArgumentNullException(nameof(apiSecretKey));

        if (_httpClient.BaseAddress is not null && _httpClient.BaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Alpaca client requires HTTPS base address to enforce TLS transport.");
        }

        if (_httpClient.BaseAddress is not null && !AllowedHosts.Contains(_httpClient.BaseAddress.Host))
        {
            throw new InvalidOperationException(
                $"Alpaca base URL host '{_httpClient.BaseAddress.Host}' is not in the allowed list. " +
                "Only api.alpaca.markets, paper-api.alpaca.markets, and data.alpaca.markets are permitted.");
        }

        var requestsPerMinute = ResolveRequestsPerMinute(rateLimit, 200);

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

    public async Task<List<AlpacaBar>> GetHistoricalBarsAsync(
        string symbol,
        DateTime from,
        DateTime to,
        string timeframe = "1Day",
        CancellationToken ct = default)
    {
        ValidateSymbol(symbol);
        if (string.IsNullOrWhiteSpace(timeframe))
        {
            throw new ArgumentException("Timeframe cannot be empty or whitespace.", nameof(timeframe));
        }

        if (from > to)
        {
            throw new ArgumentException("From date cannot be after To date.", nameof(from));
        }

        try
        {
            var encodedSymbol = Uri.EscapeDataString(symbol.Trim().ToUpperInvariant());
            var encodedTimeframe = Uri.EscapeDataString(timeframe.Trim());
            var fromUtc = from.ToUniversalTime();
            var toUtc = to.ToUniversalTime();

            var requestUri = $"stocks/{encodedSymbol}/bars?timeframe={encodedTimeframe}&start={fromUtc:O}&end={toUtc:O}&limit=1000&feed=iex";
            using var response = await SendGetAsync(requestUri, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            await EnsureSuccessOrThrowAsync(response, "historical request", ct);

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<AlpacaBarsResponse>(stream, JsonOptions, ct);
            if (payload?.Bars is null || payload.Bars.Count == 0)
            {
                return [];
            }

            var bars = new List<AlpacaBar>(payload.Bars.Count);
            foreach (var row in payload.Bars)
            {
                bars.Add(new AlpacaBar(
                    Timestamp: row.Timestamp,
                    Open: row.Open,
                    High: row.High,
                    Low: row.Low,
                    Close: row.Close,
                    Volume: row.Volume,
                    TradeCount: row.TradeCount,
                    VWAP: row.VWAP));
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
            throw new InvalidOperationException($"Alpaca historical request failed: {sanitizedMessage}", ex);
        }
    }

    public async Task<AlpacaQuote?> GetLatestQuoteAsync(string symbol, CancellationToken ct = default)
    {
        ValidateSymbol(symbol);

        try
        {
            var encodedSymbol = Uri.EscapeDataString(symbol.Trim().ToUpperInvariant());
            var requestUri = $"stocks/{encodedSymbol}/quotes/latest?feed=iex";
            using var response = await SendGetAsync(requestUri, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            await EnsureSuccessOrThrowAsync(response, "quote request", ct);

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            var payload = await JsonSerializer.DeserializeAsync<AlpacaLatestQuoteResponse>(stream, JsonOptions, ct);
            if (payload?.Quote is null)
            {
                return null;
            }

            return new AlpacaQuote(
                AskPrice: payload.Quote.AskPrice,
                AskSize: payload.Quote.AskSize,
                BidPrice: payload.Quote.BidPrice,
                BidSize: payload.Quote.BidSize,
                Timestamp: payload.Quote.Timestamp,
                Country: payload.Quote.Country);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            throw new InvalidOperationException($"Alpaca quote request failed: {sanitizedMessage}", ex);
        }
    }

    public async Task<List<AlpacaNewsArticle>> GetNewsAsync(
        string symbol,
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        ValidateSymbol(symbol);
        if (from > to)
        {
            throw new ArgumentException("From date cannot be after To date.", nameof(from));
        }

        var encodedSymbol = Uri.EscapeDataString(symbol.Trim().ToUpperInvariant());
        var fromUtc = from.ToUniversalTime();
        var toUtc = to.ToUniversalTime();

        var newsBaseUri = ResolveNewsBaseUri();
        var requestUri = new Uri(newsBaseUri, $"news?symbols={encodedSymbol}&start={fromUtc:O}&end={toUtc:O}&limit=50");
        return await GetNewsInternalAsync(requestUri, "news request", ct);
    }

    public async Task<List<AlpacaNewsArticle>> GetMarketNewsAsync(CancellationToken ct = default)
    {
        var newsBaseUri = ResolveNewsBaseUri();
        var requestUri = new Uri(newsBaseUri, "news?limit=50");
        return await GetNewsInternalAsync(requestUri, "market news request", ct);
    }

    public async Task<bool> GetHealthStatusAsync(CancellationToken ct = default)
    {
        try
        {
            var quote = await GetLatestQuoteAsync("AAPL", ct);
            return quote is not null && quote.BidPrice > 0d && quote.AskPrice > 0d;
        }
        catch
        {
            return false;
        }
    }

    private async Task<List<AlpacaNewsArticle>> GetNewsInternalAsync(Uri requestUri, string operationName, CancellationToken ct)
    {
        try
        {
            using var response = await SendGetAsync(requestUri, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            await EnsureSuccessOrThrowAsync(response, operationName, ct);

            var body = await response.Content.ReadAsStringAsync(ct);
            var payload = JsonSerializer.Deserialize<AlpacaNewsResponse>(body, JsonOptions);

            if (payload?.News is not null)
            {
                return MapNews(payload.News);
            }

            var listPayload = JsonSerializer.Deserialize<List<AlpacaNewsResponseItem>>(body, JsonOptions);
            return listPayload is null ? [] : MapNews(listPayload);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            throw new InvalidOperationException($"Alpaca {operationName} failed: {sanitizedMessage}", ex);
        }
    }

    private static List<AlpacaNewsArticle> MapNews(List<AlpacaNewsResponseItem> news)
    {
        var items = new List<AlpacaNewsArticle>(news.Count);
        foreach (var row in news)
        {
            items.Add(new AlpacaNewsArticle(
                Id: row.Id ?? string.Empty,
                Headline: row.Headline ?? string.Empty,
                Summary: row.Summary ?? string.Empty,
                Url: row.Url ?? string.Empty,
                Source: row.Source ?? string.Empty,
                CreatedAt: row.CreatedAt,
                Symbols: row.Symbols ?? [],
                Country: row.Country));
        }

        return items;
    }

    private async Task<HttpResponseMessage> SendGetAsync(string requestUri, CancellationToken ct)
    {
        await AcquireRateLimitPermitAsync(ct);

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add(ApiKeyIdHeader, _apiKeyId.ExposeSecret());
        request.Headers.Add(ApiSecretKeyHeader, _apiSecretKey.ExposeSecret());

        return await _httpClient.SendAsync(request, ct);
    }

    private async Task<HttpResponseMessage> SendGetAsync(Uri requestUri, CancellationToken ct)
    {
        await AcquireRateLimitPermitAsync(ct);

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Add(ApiKeyIdHeader, _apiKeyId.ExposeSecret());
        request.Headers.Add(ApiSecretKeyHeader, _apiSecretKey.ExposeSecret());

        return await _httpClient.SendAsync(request, ct);
    }

    private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, string operationName, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        var sanitizedBody = SensitiveDataSanitizer.Sanitize(body);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta?.TotalSeconds;
            var retryText = retryAfter.HasValue
                ? $" Retry after {retryAfter.Value:0} seconds."
                : string.Empty;
            throw new InvalidOperationException($"Alpaca API rate limit exceeded.{retryText} {sanitizedBody}".Trim());
        }

        if ((int)response.StatusCode >= 500)
        {
            throw new InvalidOperationException($"Alpaca API service error ({(int)response.StatusCode}). {sanitizedBody}".Trim());
        }

        throw new InvalidOperationException($"Alpaca API {operationName} failed with status {(int)response.StatusCode}. {sanitizedBody}".Trim());
    }

    private async Task AcquireRateLimitPermitAsync(CancellationToken ct)
    {
        using var lease = await _rateLimiter.AcquireAsync(1, ct);
        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException("Alpaca API rate limit exceeded.");
        }
    }

    private static void ValidateSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be empty or whitespace.", nameof(symbol));
        }

        if (symbol.Length > 10)
        {
            throw new ArgumentException("Symbol cannot exceed 10 characters.", nameof(symbol));
        }

        var symbolBody = symbol.StartsWith("^", StringComparison.Ordinal) ? symbol[1..] : symbol;
        if (!symbolBody.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-'))
        {
            throw new ArgumentException("Symbol contains invalid characters.", nameof(symbol));
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

    private Uri ResolveNewsBaseUri()
    {
        if (_httpClient.BaseAddress is null)
        {
            throw new InvalidOperationException("Alpaca client requires base address to build news endpoint URI.");
        }

        return new Uri(_httpClient.BaseAddress, "../v1beta1/");
    }
}
