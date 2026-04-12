using System.Net;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StockData.Net.Models.SocialMedia;
using StockData.Net.Security;

namespace StockData.Net.Providers.SocialMedia;

public sealed class XTwitterProvider : ISocialMediaProvider
{
    private const string BearerTokenConfigKey = "X_BEARER_TOKEN";

    private readonly HttpClient _httpClient;
    private readonly int _configuredRateLimitWindowSeconds;
    private readonly int _configuredMaxResults;
    private readonly int _configuredMaxLookbackHours;

    public XTwitterProvider(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(configuration);

        _httpClient = httpClientFactory.CreateClient("xtwitter");
        if (_httpClient.BaseAddress is null || !string.Equals(_httpClient.BaseAddress.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("X API base address must use HTTPS.");
        }

        var token = configuration[BearerTokenConfigKey]?.Trim();
        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Token is read when the provider is created; process restart is required after token rotation.
        _configuredRateLimitWindowSeconds = ReadInt(configuration, "XTwitter:rateLimitWindowSeconds", 900, 30, 86400);
        _configuredMaxResults = ReadInt(configuration, "XTwitter:maxResults", 10, 1, 100);
        _configuredMaxLookbackHours = ReadInt(configuration, "XTwitter:maxLookbackHours", 24, 1, 168);
    }

    public string ProviderId => "xtwitter";

    public string ProviderName => "X / Twitter";

    public SocialFeedCapabilities GetSupportedCapabilities(ProviderTier tier)
    {
        if (tier == ProviderTier.Paid)
        {
            return new SocialFeedCapabilities
            {
                MaxResults = 100,
                MaxLookbackHours = 168,
                RateLimitWindowSeconds = _configuredRateLimitWindowSeconds
            };
        }

        return new SocialFeedCapabilities
        {
            MaxResults = _configuredMaxResults,
            MaxLookbackHours = _configuredMaxLookbackHours,
            RateLimitWindowSeconds = _configuredRateLimitWindowSeconds
        };
    }

    public async Task<SocialFeedResult> GetPostsAsync(SocialFeedRequest request, CancellationToken ct = default)
    {
        EnsureTokenConfigured();

        var posts = new List<SocialPost>();
        var errors = new List<HandleError>();

        if (request.Handles.Count > 0)
        {
            foreach (var handle in request.Handles)
            {
                try
                {
                    var query = BuildSearchQuery(new[] { handle }, request.Query);
                    var handlePosts = await SearchAsync(query, request.MaxResults, request.LookbackHours, request.Query, ct).ConfigureAwait(false);
                    posts.AddRange(handlePosts);
                }
                catch (XRateLimitException)
                {
                    throw;
                }
                catch (SocialMediaServiceUnavailableException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors.Add(new HandleError
                    {
                        Handle = handle,
                        ErrorMessage = SensitiveDataSanitizer.Sanitize(ex.Message)
                    });
                }
            }
        }
        else
        {
            var query = BuildSearchQuery(Array.Empty<string>(), request.Query);
            var queryPosts = await SearchAsync(query, request.MaxResults, request.LookbackHours, request.Query, ct).ConfigureAwait(false);
            posts.AddRange(queryPosts);
        }

        var orderedPosts = posts
            .OrderByDescending(post => post.PostedAt)
            .ToArray();

        return new SocialFeedResult
        {
            Posts = orderedPosts,
            Errors = errors
        };
    }

    private async Task<IReadOnlyList<SocialPost>> SearchAsync(
        string searchQuery,
        int maxResults,
        int lookbackHours,
        string? originalQuery,
        CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow.AddHours(-lookbackHours).ToUniversalTime();
        var requestUri = BuildRequestUri(searchQuery, maxResults, startTime);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            var resetAt = TryGetRateLimitReset(response.Headers);
            throw new XRateLimitException("X API rate limit reached. Please try again later.", resetAt);
        }

        if ((int)response.StatusCode >= 500)
        {
            throw new SocialMediaServiceUnavailableException("Social media service is temporarily unavailable. Please try again shortly.");
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("X API authentication failed. Verify X_BEARER_TOKEN.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            throw new InvalidOperationException("X API rejected the request. Verify handle and query parameters.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("Requested X resource was not found.");
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return MapPosts(payload, originalQuery);
    }

    private static string BuildSearchQuery(IReadOnlyList<string> handles, string? query)
    {
        var normalizedQuery = (query ?? string.Empty).Trim();
        if (handles.Count == 0)
        {
            return normalizedQuery;
        }

        var handleFilter = string.Join(" OR ", handles.Select(handle => $"from:{handle}"));
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return $"({handleFilter})";
        }

        return $"({handleFilter}) {normalizedQuery}";
    }

    private static string BuildRequestUri(string searchQuery, int maxResults, DateTimeOffset startTime)
    {
        var encodedQuery = UrlEncoder.Default.Encode(searchQuery);
        var encodedStartTime = UrlEncoder.Default.Encode(startTime.ToString("O"));

        return $"2/tweets/search/recent?query={encodedQuery}&max_results={maxResults}&start_time={encodedStartTime}&tweet.fields=author_id,created_at,text,public_metrics&expansions=author_id&user.fields=username";
    }

    private static IReadOnlyList<SocialPost> MapPosts(string payload, string? originalQuery)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<SocialPost>();
        }

        var usersById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("includes", out var includes)
            && includes.TryGetProperty("users", out var users)
            && users.ValueKind == JsonValueKind.Array)
        {
            foreach (var user in users.EnumerateArray())
            {
                var id = user.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
                var username = user.TryGetProperty("username", out var usernameProperty) ? usernameProperty.GetString() : null;
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(username))
                {
                    usersById[id] = username;
                }
            }
        }

        var mapped = new List<SocialPost>();
        var query = string.IsNullOrWhiteSpace(originalQuery) ? null : originalQuery;

        foreach (var tweet in data.EnumerateArray())
        {
            var id = tweet.TryGetProperty("id", out var idProperty) ? idProperty.GetString() : null;
            var text = tweet.TryGetProperty("text", out var textProperty) ? textProperty.GetString() : string.Empty;
            var authorId = tweet.TryGetProperty("author_id", out var authorIdProperty) ? authorIdProperty.GetString() : null;
            var createdAtRaw = tweet.TryGetProperty("created_at", out var createdAtProperty) ? createdAtProperty.GetString() : null;

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(authorId) || string.IsNullOrWhiteSpace(createdAtRaw))
            {
                continue;
            }

            if (!DateTimeOffset.TryParse(createdAtRaw, out var createdAt))
            {
                continue;
            }

            usersById.TryGetValue(authorId, out var username);
            username ??= "unknown";

            var retweetCount = 0;
            if (tweet.TryGetProperty("public_metrics", out var metrics)
                && metrics.ValueKind == JsonValueKind.Object
                && metrics.TryGetProperty("retweet_count", out var retweetCountProperty)
                && retweetCountProperty.TryGetInt32(out var parsedRetweetCount))
            {
                retweetCount = parsedRetweetCount;
            }

            var matchedKeywords = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(query)
                && !string.IsNullOrWhiteSpace(text)
                && text.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                matchedKeywords = new[] { query };
            }

            mapped.Add(new SocialPost
            {
                PostId = id,
                AuthorHandle = username,
                Content = text ?? string.Empty,
                PostedAt = createdAt.ToUniversalTime(),
                Url = $"https://x.com/{username}/status/{id}",
                SourcePlatform = "X",
                MatchedKeywords = matchedKeywords,
                RetweetCount = retweetCount
            });
        }

        return mapped;
    }

    private void EnsureTokenConfigured()
    {
        var hasToken = !string.IsNullOrWhiteSpace(_httpClient.DefaultRequestHeaders.Authorization?.Parameter);
        if (!hasToken)
        {
            throw new InvalidOperationException("X_BEARER_TOKEN is not configured.");
        }
    }

    private static DateTimeOffset? TryGetRateLimitReset(HttpResponseHeaders headers)
    {
        if (headers.TryGetValues("x-rate-limit-reset", out var values))
        {
            var raw = values.FirstOrDefault();
            if (long.TryParse(raw, out var unixSeconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
            }
        }

        return null;
    }

    private static int ReadInt(IConfiguration configuration, string key, int fallback, int min, int max)
    {
        var rawValue = configuration[key];
        if (!int.TryParse(rawValue, out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }
}
