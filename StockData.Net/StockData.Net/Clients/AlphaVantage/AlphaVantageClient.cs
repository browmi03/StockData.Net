using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using StockData.Net.Configuration;
using StockData.Net.Security;

namespace StockData.Net.Clients.AlphaVantage;

public sealed class AlphaVantageClient : IAlphaVantageClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SecretValue _apiKey;
    private readonly TokenBucketRateLimiter _rateLimiter;

    public AlphaVantageClient(HttpClient httpClient, SecretValue apiKey, RateLimitConfiguration? rateLimit = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));

        if (_httpClient.BaseAddress is not null && _httpClient.BaseAddress.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("AlphaVantage client requires HTTPS base address to enforce TLS transport.");
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

    public async Task<AlphaVantageQuote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        ValidateSymbol(symbol);

        try
        {
            await AcquireRateLimitPermitAsync(cancellationToken);

            var encodedSymbol = Uri.EscapeDataString(symbol.Trim().ToUpperInvariant());
            var apiKey = Uri.EscapeDataString(_apiKey.ExposeSecret());
            var requestUri = $"query?function=GLOBAL_QUOTE&symbol={encodedSymbol}&apikey={apiKey}";

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<AlphaVantageQuoteResponse>(stream, JsonOptions, cancellationToken);
            if (payload is null)
            {
                return null;
            }

            ThrowIfRateLimited(payload.Note ?? payload.Information);

            if (payload.GlobalQuote is null)
            {
                return null;
            }

            var price = ParseDouble(payload.GlobalQuote.Price);
            var change = ParseDouble(payload.GlobalQuote.Change);
            var percentChange = ParsePercent(payload.GlobalQuote.ChangePercent);
            var timestamp = ParseTradingDay(payload.GlobalQuote.LatestTradingDay);

            return new AlphaVantageQuote(
                Price: price,
                Change: change,
                PercentChange: percentChange,
                Timestamp: timestamp);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            throw new InvalidOperationException($"AlphaVantage quote request failed: {sanitizedMessage}", ex);
        }
    }

    public async Task<List<AlphaVantageCandle>> GetHistoricalPricesAsync(
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
            var requestUri = $"query?function=TIME_SERIES_DAILY_ADJUSTED&symbol={encodedSymbol}&outputsize=full&apikey={apiKey}";

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<AlphaVantageTimeSeriesResponse>(stream, JsonOptions, cancellationToken);
            if (payload is null)
            {
                return [];
            }

            ThrowIfRateLimited(payload.Note ?? payload.Information);

            if (payload.TimeSeriesDaily is null || payload.TimeSeriesDaily.Count == 0)
            {
                return [];
            }

            var fromDate = from.ToUniversalTime().Date;
            var toDate = to.ToUniversalTime().Date;

            var items = new List<AlphaVantageCandle>(payload.TimeSeriesDaily.Count);
            foreach (var (dateText, bar) in payload.TimeSeriesDaily)
            {
                if (!DateTime.TryParseExact(dateText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsedDate))
                {
                    continue;
                }

                var utcDate = parsedDate.Date;
                if (utcDate < fromDate || utcDate > toDate)
                {
                    continue;
                }

                items.Add(new AlphaVantageCandle(
                    Timestamp: new DateTimeOffset(utcDate).ToUnixTimeSeconds(),
                    Open: ParseDouble(bar.Open),
                    High: ParseDouble(bar.High),
                    Low: ParseDouble(bar.Low),
                    Close: ParseDouble(bar.Close),
                    Volume: ParseLong(bar.Volume)));
            }

            items.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            return items;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            throw new InvalidOperationException($"AlphaVantage historical request failed: {sanitizedMessage}", ex);
        }
    }

    public async Task<List<AlphaVantageNewsItem>> GetNewsAsync(
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
            var requestUri = $"query?function=NEWS_SENTIMENT&tickers={encodedSymbol}&limit=50&apikey={apiKey}";

            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return [];
            }

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var payload = await JsonSerializer.DeserializeAsync<AlphaVantageNewsResponse>(stream, JsonOptions, cancellationToken);
            if (payload is null)
            {
                return [];
            }

            ThrowIfRateLimited(payload.Note ?? payload.Information);

            if (payload.Feed is null || payload.Feed.Count == 0)
            {
                return [];
            }

            var fromUnix = new DateTimeOffset(from.ToUniversalTime()).ToUnixTimeSeconds();
            var toUnix = new DateTimeOffset(to.ToUniversalTime()).ToUnixTimeSeconds();

            var items = new List<AlphaVantageNewsItem>(payload.Feed.Count);
            foreach (var row in payload.Feed)
            {
                if (string.IsNullOrWhiteSpace(row.Url))
                {
                    continue;
                }

                var publishedTs = ParseNewsTimestamp(row.TimePublished);
                if (publishedTs < fromUnix || publishedTs > toUnix)
                {
                    continue;
                }

                var tickers = row.TickerSentiment?
                    .Select(x => x.Ticker)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? [];

                items.Add(new AlphaVantageNewsItem(
                    Title: row.Title ?? string.Empty,
                    Source: row.Source ?? string.Empty,
                    Url: row.Url,
                    Summary: row.Summary ?? string.Empty,
                    Timestamp: publishedTs,
                    RelatedTickers: tickers));
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
            throw new InvalidOperationException($"AlphaVantage news request failed: {sanitizedMessage}", ex);
        }
    }

    private async Task AcquireRateLimitPermitAsync(CancellationToken cancellationToken)
    {
        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException("AlphaVantage API rate limit exceeded.");
        }
    }

    private static void ThrowIfRateLimited(string? noteOrInformation)
    {
        if (!string.IsNullOrWhiteSpace(noteOrInformation) &&
            (noteOrInformation.Contains("frequency", StringComparison.OrdinalIgnoreCase) ||
             noteOrInformation.Contains("limit", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(noteOrInformation);
        }
    }

    private static long ParseTradingDay(string? tradingDay)
    {
        if (DateTime.TryParseExact(tradingDay, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date))
        {
            return new DateTimeOffset(date).ToUnixTimeSeconds();
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static long ParseNewsTimestamp(string? input)
    {
        if (DateTime.TryParseExact(input, "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return new DateTimeOffset(parsed).ToUnixTimeSeconds();
        }

        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    private static double ParseDouble(string? value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0d;
    }

    private static double ParsePercent(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0d;
        }

        var normalized = value.Replace("%", string.Empty, StringComparison.Ordinal);
        return ParseDouble(normalized);
    }

    private static long ParseLong(string? value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return (long)Math.Round(ParseDouble(value), MidpointRounding.AwayFromZero);
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
