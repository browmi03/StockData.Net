using System.Text.Json;
using StockData.Net.Configuration;
using StockData.Net.Models.SocialMedia;
using StockData.Net.Providers;
using StockData.Net.Providers.SocialMedia;
using StockData.Net.Security;

namespace StockData.Net.McpServer;

public sealed class SocialFeedToolHandler
{
    private readonly SocialMediaRouter _router;
    private readonly McpConfiguration _configuration;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public SocialFeedToolHandler(SocialMediaRouter router, McpConfiguration configuration)
    {
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<object> HandleAsync(JsonElement arguments, CancellationToken ct = default)
    {
        try
        {
            var handles = ParseHandles(arguments);
            var query = GetOptionalString(arguments, "query");
            var maxResults = GetOptionalInt(arguments, "max_results", 10);
            var lookbackHours = GetOptionalInt(arguments, "lookback_hours", 24);
            var requestedProvider = GetOptionalString(arguments, "provider") ?? "xtwitter";

            if (handles.Count == 0 && string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentException("At least one of handles or query must be provided.");
            }

            if (maxResults < 1 || maxResults > 100)
            {
                throw new ArgumentException("max_results must be between 1 and 100.");
            }

            if (lookbackHours < 1 || lookbackHours > 168)
            {
                throw new ArgumentException("lookback_hours must be between 1 and 168.");
            }

            var tier = ResolveTier(requestedProvider);
            var request = new SocialFeedRequest
            {
                Handles = handles,
                Query = query,
                MaxResults = maxResults,
                LookbackHours = lookbackHours,
                Tier = tier
            };

            var result = await _router.GetPostsAsync(request, requestedProvider, ct).ConfigureAwait(false);
            var sanitizedResult = new SocialFeedResult
            {
                Posts = result.Posts,
                Errors = result.Errors
                    .Select(error => new HandleError
                    {
                        Handle = error.Handle,
                        ErrorMessage = SensitiveDataSanitizer.Sanitize(error.ErrorMessage)
                    })
                    .ToArray(),
                TierAdvisory = string.IsNullOrWhiteSpace(result.TierAdvisory)
                    ? null
                    : SensitiveDataSanitizer.Sanitize(result.TierAdvisory)
            };

            var text = JsonSerializer.Serialize(sanitizedResult, _jsonOptions);
            return CreateTextResponse(text);
        }
        catch (XRateLimitException ex)
        {
            var safeMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            var payload = JsonSerializer.Serialize(new
            {
                posts = Array.Empty<SocialPost>(),
                errors = new[]
                {
                    new HandleError
                    {
                        Handle = "provider",
                        ErrorMessage = ex.ResetAtUtc.HasValue
                            ? $"{safeMessage} Reset at {ex.ResetAtUtc.Value.UtcDateTime:O}."
                            : safeMessage
                    }
                }
            }, _jsonOptions);
            return CreateTextResponse(payload);
        }
        catch (SocialMediaServiceUnavailableException ex)
        {
            var safeMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            var payload = JsonSerializer.Serialize(new
            {
                posts = Array.Empty<SocialPost>(),
                errors = new[]
                {
                    new HandleError
                    {
                        Handle = "provider",
                        ErrorMessage = safeMessage
                    }
                }
            }, _jsonOptions);
            return CreateTextResponse(payload);
        }
        catch (Exception ex)
        {
            var safeMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
            var payload = JsonSerializer.Serialize(new
            {
                posts = Array.Empty<SocialPost>(),
                errors = new[]
                {
                    new HandleError
                    {
                        Handle = "validation",
                        ErrorMessage = safeMessage
                    }
                }
            }, _jsonOptions);
            return CreateTextResponse(payload);
        }
    }

    private static object CreateTextResponse(string text)
    {
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text
                }
            }
        };
    }

    private ProviderTier ResolveTier(string requestedProvider)
    {
        var provider = _configuration.Providers.FirstOrDefault(
            entry => string.Equals(entry.Id, requestedProvider, StringComparison.OrdinalIgnoreCase));

        var configuredTier = provider?.Tier ?? "free";
        return string.Equals(configuredTier, "paid", StringComparison.OrdinalIgnoreCase)
            ? ProviderTier.Paid
            : ProviderTier.Free;
    }

    private static IReadOnlyList<string> ParseHandles(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("handles", out var handlesElement)
            || handlesElement.ValueKind == JsonValueKind.Null
            || handlesElement.ValueKind == JsonValueKind.Undefined)
        {
            return Array.Empty<string>();
        }

        if (handlesElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("handles must be an array of strings.");
        }

        var handles = new List<string>();
        foreach (var handleElement in handlesElement.EnumerateArray())
        {
            if (handleElement.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Each handle must be a string.");
            }

            var value = handleElement.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                handles.Add(value);
            }
        }

        return handles;
    }

    private static string? GetOptionalString(JsonElement arguments, string key)
    {
        if (arguments.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null)
        {
            return value.GetString();
        }

        return null;
    }

    private static int GetOptionalInt(JsonElement arguments, string key, int defaultValue)
    {
        if (arguments.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var parsed))
            {
                throw new ArgumentException($"{key} must be an integer.");
            }

            return parsed;
        }

        return defaultValue;
    }
}
