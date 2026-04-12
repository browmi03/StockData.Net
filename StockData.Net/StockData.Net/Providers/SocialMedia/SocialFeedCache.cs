using StockData.Net.Models.SocialMedia;

namespace StockData.Net.Providers.SocialMedia;

public sealed class SocialFeedCache
{
    private sealed record CacheEntry(SocialFeedResult Result, DateTimeOffset ExpiresAtUtc);

    private readonly object _gate = new();
    private readonly Dictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);

    public bool TryGet(string key, out SocialFeedResult result)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.ExpiresAtUtc > DateTimeOffset.UtcNow)
                {
                    result = entry.Result;
                    return true;
                }

                _cache.Remove(key);
            }
        }

        result = new SocialFeedResult();
        return false;
    }

    public void Set(string key, SocialFeedResult result, TimeSpan ttl)
    {
        var expiresAtUtc = DateTimeOffset.UtcNow.Add(ttl);

        lock (_gate)
        {
            _cache[key] = new CacheEntry(result, expiresAtUtc);
        }
    }

    public static string BuildCacheKey(
        string providerId,
        IReadOnlyList<string> handles,
        string? query,
        int maxResults,
        int lookbackHours,
        ProviderTier tier)
    {
        var normalizedHandles = handles
            .Where(handle => !string.IsNullOrWhiteSpace(handle))
            .Select(handle => handle.Trim().TrimStart('@').ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(handle => handle, StringComparer.Ordinal)
            .ToArray();

        var handlesKey = string.Join("|", normalizedHandles);
        var queryKey = (query ?? string.Empty).Trim().ToLowerInvariant();

        return string.Join(
            "::",
            providerId.Trim().ToLowerInvariant(),
            handlesKey,
            queryKey,
            maxResults,
            lookbackHours,
            tier.ToString().ToLowerInvariant());
    }
}
