using System.Text.RegularExpressions;
using StockData.Net.Models.SocialMedia;

namespace StockData.Net.Providers.SocialMedia;

public sealed class SocialMediaRouter
{
    private static readonly Regex HandlePattern = new("^[A-Za-z0-9_]{1,15}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

    private readonly Dictionary<string, ISocialMediaProvider> _providers;
    private readonly SocialFeedCache _cache;

    public SocialMediaRouter(IEnumerable<ISocialMediaProvider> providers, SocialFeedCache cache)
    {
        var socialProviders = providers?.ToArray() ?? throw new ArgumentNullException(nameof(providers));
        _providers = socialProviders
            .ToDictionary(provider => provider.ProviderId, provider => provider, StringComparer.OrdinalIgnoreCase);
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<SocialFeedResult> GetPostsAsync(
        SocialFeedRequest request,
        string? requestedProviderId,
        CancellationToken ct = default)
    {
        var validatedHandles = ValidateAndNormalizeHandles(request.Handles);
        var normalizedQuery = ValidateAndNormalizeQuery(request.Query);

        if (validatedHandles.Count == 0 && string.IsNullOrWhiteSpace(normalizedQuery))
        {
            throw new ArgumentException("At least one of handles or query must be provided.");
        }

        if (request.MaxResults < 1 || request.MaxResults > 100)
        {
            throw new ArgumentException("max_results must be between 1 and 100.");
        }

        if (request.LookbackHours < 1 || request.LookbackHours > 168)
        {
            throw new ArgumentException("lookback_hours must be between 1 and 168.");
        }

        var provider = ResolveProvider(requestedProviderId);
        var capabilities = provider.GetSupportedCapabilities(request.Tier);

        var effectiveLookbackHours = request.LookbackHours;
        var advisoryParts = new List<string>();

        if (effectiveLookbackHours > capabilities.MaxLookbackHours)
        {
            effectiveLookbackHours = capabilities.MaxLookbackHours;
            advisoryParts.Add($"lookback_hours was clamped to {capabilities.MaxLookbackHours} for the configured {request.Tier.ToString().ToLowerInvariant()} tier.");
        }

        var effectiveMaxResults = request.MaxResults;
        if (effectiveMaxResults > capabilities.MaxResults)
        {
            effectiveMaxResults = capabilities.MaxResults;
            advisoryParts.Add($"max_results clamped to {capabilities.MaxResults} (free tier limit). Upgrade to X Basic tier for up to 100 results.");
        }

        var effectiveRequest = new SocialFeedRequest
        {
            Handles = validatedHandles,
            Query = normalizedQuery,
            MaxResults = effectiveMaxResults,
            LookbackHours = effectiveLookbackHours,
            Tier = request.Tier
        };

        var cacheKey = SocialFeedCache.BuildCacheKey(
            provider.ProviderId,
            effectiveRequest.Handles,
            effectiveRequest.Query,
            effectiveRequest.MaxResults,
            effectiveRequest.LookbackHours,
            effectiveRequest.Tier);

        if (_cache.TryGet(cacheKey, out var cachedResult))
        {
            return new SocialFeedResult
            {
                Posts = cachedResult.Posts
                    .Select(post => new SocialPost
                    {
                        PostId = post.PostId,
                        AuthorHandle = post.AuthorHandle,
                        Content = post.Content,
                        PostedAt = post.PostedAt,
                        Url = post.Url,
                        SourcePlatform = post.SourcePlatform,
                        MatchedKeywords = post.MatchedKeywords,
                        CachedAt = DateTimeOffset.UtcNow,
                        RetweetCount = post.RetweetCount
                    })
                    .ToArray(),
                Errors = cachedResult.Errors,
                TierAdvisory = MergeTierAdvisory(string.Join(" ", advisoryParts), cachedResult.TierAdvisory)
            };
        }

        var liveResult = await provider.GetPostsAsync(effectiveRequest, ct).ConfigureAwait(false);
        var orderedPosts = liveResult.Posts
            .OrderByDescending(post => post.PostedAt)
            .Select(post => new SocialPost
            {
                PostId = post.PostId,
                AuthorHandle = post.AuthorHandle,
                Content = post.Content,
                PostedAt = post.PostedAt,
                Url = post.Url,
                SourcePlatform = post.SourcePlatform,
                MatchedKeywords = post.MatchedKeywords,
                CachedAt = null,
                RetweetCount = post.RetweetCount
            })
            .ToArray();

        var response = new SocialFeedResult
        {
            Posts = orderedPosts,
            Errors = liveResult.Errors,
            TierAdvisory = MergeTierAdvisory(string.Join(" ", advisoryParts), liveResult.TierAdvisory)
        };

        _cache.Set(cacheKey, response, TimeSpan.FromSeconds(capabilities.RateLimitWindowSeconds));
        return response;
    }

    private static string? MergeTierAdvisory(string? clampAdvisory, string? providerAdvisory)
    {
        var parts = new[] { clampAdvisory, providerAdvisory }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();

        if (parts.Length == 0)
        {
            return null;
        }

        return string.Join(" ", parts);
    }

    private ISocialMediaProvider ResolveProvider(string? requestedProviderId)
    {
        if (_providers.Count == 0)
        {
            throw new InvalidOperationException("No social media providers are registered.");
        }

        if (!string.IsNullOrWhiteSpace(requestedProviderId))
        {
            if (_providers.TryGetValue(requestedProviderId, out var explicitProvider))
            {
                return explicitProvider;
            }

            throw new InvalidOperationException($"Provider '{requestedProviderId}' is not available.");
        }

        if (_providers.TryGetValue("xtwitter", out var defaultProvider))
        {
            return defaultProvider;
        }

        return _providers.Values.First();
    }

    private static IReadOnlyList<string> ValidateAndNormalizeHandles(IReadOnlyList<string> handles)
    {
        var normalized = new List<string>();

        foreach (var handle in handles)
        {
            if (string.IsNullOrWhiteSpace(handle))
            {
                continue;
            }

            var trimmed = handle.Trim().TrimStart('@');
            if (!HandlePattern.IsMatch(trimmed))
            {
                throw new ArgumentException($"Invalid handle '{handle}'. Handles may contain only letters, digits, and underscores with max length 15.");
            }

            normalized.Add(trimmed);
        }

        return normalized;
    }

    private static string? ValidateAndNormalizeQuery(string? query)
    {
        if (query is null)
        {
            return null;
        }

        var normalized = query.Trim();
        if (normalized.Length == 0)
        {
            throw new ArgumentException("query must not be blank if provided.");
        }

        if (normalized.Length > 256)
        {
            throw new ArgumentException("query exceeds maximum length of 256 characters.");
        }

        return normalized;
    }
}
