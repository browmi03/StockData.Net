using System.Text.Json;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using StockData.Net.Deduplication;
using StockData.Net.Models.Events;
using StockData.Net.Providers;
using StockData.Net.Security;

namespace StockData.Net.McpServer;

public sealed class MarketEventsToolHandler
{
    private static readonly string[] ValidCategories = ["fed", "treasury", "geopolitical", "regulatory", "central_bank", "institutional", "all"];
    private static readonly string[] ValidEventTypes = ["scheduled", "breaking", "all"];
    private static readonly string[] ValidImpactLevels = ["high", "medium", "low", "all"];

    private readonly IReadOnlyList<IMarketEventsProvider> _providers;
    private readonly MarketEventDeduplicator _deduplicator;
    private readonly ILogger<MarketEventsToolHandler>? _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public MarketEventsToolHandler(
        IEnumerable<IMarketEventsProvider> providers,
        MarketEventDeduplicator deduplicator,
        ILogger<MarketEventsToolHandler>? logger = null)
    {
        _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
        _deduplicator = deduplicator ?? throw new ArgumentNullException(nameof(deduplicator));
        _logger = logger;
    }

    public async Task<object> HandleAsync(JsonElement arguments, CancellationToken cancellationToken = default)
    {
        var query = ParseAndValidateQuery(arguments);
        _logger?.LogInformation("Handling get_market_events request for {FromDate} to {ToDate}", query.FromDate, query.ToDate);

        var attempts = _providers.Select(async provider =>
        {
            try
            {
                var events = await provider.GetEventsAsync(query, cancellationToken);
                return new ProviderAttempt(provider.ProviderId, events, null);
            }
            catch (Exception ex)
            {
                var reason = SensitiveDataSanitizer.Sanitize(ex.Message);
                reason = ScrubProviderStatusDetails(reason);
                return new ProviderAttempt(provider.ProviderId, [], new InvalidOperationException(reason));
            }
        }).ToArray();

        var results = await Task.WhenAll(attempts);

        var succeeded = results.Where(result => result.Error is null).ToList();
        if (succeeded.Count == 0)
        {
            var providerErrors = results
                .Where(result => result.Error is not null)
                .ToDictionary(result => result.ProviderId, result => result.Error!, StringComparer.OrdinalIgnoreCase);
            var attemptedProviders = results.Select(result => result.ProviderId).ToList();
            throw new ProviderFailoverException("market events", providerErrors, attemptedProviders);
        }

        var merged = succeeded
            .SelectMany(result => result.Events)
            .ToList();

        var deduplicated = _deduplicator.Deduplicate(merged);
        var filtered = ApplyFilters(deduplicated, query);
        _logger?.LogInformation("Market events aggregation complete. ProvidersSucceeded={ProvidersSucceeded} TotalEvents={TotalEvents} FilteredEvents={FilteredEvents}", succeeded.Count, merged.Count, filtered.Count);

        var scheduled = filtered
            .Where(item => string.Equals(item.EventType, "scheduled", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.EventTime)
            .ToList();

        var breaking = filtered
            .Where(item => string.Equals(item.EventType, "breaking", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.EventTime)
            .ToList();

        var ordered = scheduled.Concat(breaking).ToList();
        var text = ordered.Count == 0
            ? JsonSerializer.Serialize(new
            {
                events = ordered,
                message = "No market events found for the requested date range and filters"
            }, _jsonOptions)
            : JsonSerializer.Serialize(new
            {
                events = ordered
            }, _jsonOptions);

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

    private static MarketEventsQuery ParseAndValidateQuery(JsonElement arguments)
    {
        var categoryInput = GetOptionalString(arguments, "category", "all");
        EnsureMaxLength(categoryInput, "category", 50);
        var category = ParseCategory(categoryInput);

        var eventTypeInput = GetOptionalString(arguments, "event_type", "all");
        EnsureMaxLength(eventTypeInput, "event_type", 50);
        var eventType = ParseEventType(eventTypeInput);

        var impactLevelInput = GetOptionalString(arguments, "impact_level", "all");
        EnsureMaxLength(impactLevelInput, "impact_level", 50);
        var impactLevel = ParseImpactLevel(impactLevelInput);

        var fromDateInput = GetOptionalString(arguments, "from_date", DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd"));
        EnsureMaxLength(fromDateInput, "from_date", 20);
        var fromDate = ParseDate(fromDateInput, "from_date");

        var toDateDefault = fromDate.AddDays(7);
        var toDateInput = GetOptionalString(arguments, "to_date", toDateDefault.ToString("yyyy-MM-dd"));
        EnsureMaxLength(toDateInput, "to_date", 20);
        var toDate = ParseDate(toDateInput, "to_date");

        if (fromDate > toDate)
        {
            throw new Exception("from_date must be earlier than or equal to to_date");
        }

        if (toDate.DayNumber - fromDate.DayNumber > 30)
        {
            throw new Exception("Date range cannot exceed 30 days");
        }

        return new MarketEventsQuery
        {
            Category = category,
            EventType = eventType,
            ImpactLevel = impactLevel,
            FromDate = fromDate,
            ToDate = toDate
        };
    }

    private static IReadOnlyList<MarketEvent> ApplyFilters(IReadOnlyList<MarketEvent> events, MarketEventsQuery query)
    {
        IEnumerable<MarketEvent> filtered = events;

        if (query.Category != EventCategory.All)
        {
            var expected = ToCategoryString(query.Category);
            filtered = filtered.Where(item => string.Equals(item.Category, expected, StringComparison.OrdinalIgnoreCase));
        }

        if (query.EventType != EventType.All)
        {
            var expected = query.EventType == EventType.Scheduled ? "scheduled" : "breaking";
            filtered = filtered.Where(item => string.Equals(item.EventType, expected, StringComparison.OrdinalIgnoreCase));
        }

        if (query.ImpactLevel != ImpactLevel.All)
        {
            var expected = query.ImpactLevel.ToString().ToLowerInvariant();
            filtered = filtered.Where(item => string.Equals(item.ImpactLevel, expected, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToList();
    }

    private static string GetOptionalString(JsonElement arguments, string key, string defaultValue)
    {
        if (arguments.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null)
        {
            return value.GetString() ?? defaultValue;
        }

        return defaultValue;
    }

    private static void EnsureMaxLength(string value, string paramName, int maxLength)
    {
        if (value.Length > maxLength)
        {
            throw new Exception($"Parameter '{paramName}' exceeds maximum allowed length.");
        }
    }

    private static string ScrubProviderStatusDetails(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        return Regex.Replace(
            message,
            @"HTTP\s+\d{3}|StatusCode:\s*\d{3}|status\s+code\s+\d{3}",
            "[provider error]",
            RegexOptions.IgnoreCase);
    }

    private static DateOnly ParseDate(string input, string parameterName)
    {
        if (!DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            throw new Exception($"{parameterName} must use ISO 8601 date format yyyy-MM-dd");
        }

        return value;
    }

    private static EventCategory ParseCategory(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "all" => EventCategory.All,
            "fed" => EventCategory.Fed,
            "treasury" => EventCategory.Treasury,
            "geopolitical" => EventCategory.Geopolitical,
            "regulatory" => EventCategory.Regulatory,
            "central_bank" => EventCategory.CentralBank,
            "institutional" => EventCategory.Institutional,
            _ => throw new Exception($"Invalid category. Valid values: {string.Join(", ", ValidCategories)}")
        };
    }

    private static EventType ParseEventType(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "all" => EventType.All,
            "scheduled" => EventType.Scheduled,
            "breaking" => EventType.Breaking,
            _ => throw new Exception($"Invalid event_type. Valid values: {string.Join(", ", ValidEventTypes)}")
        };
    }

    private static ImpactLevel ParseImpactLevel(string value)
    {
        return value.Trim().ToLowerInvariant() switch
        {
            "all" => ImpactLevel.All,
            "high" => ImpactLevel.High,
            "medium" => ImpactLevel.Medium,
            "low" => ImpactLevel.Low,
            _ => throw new Exception($"Invalid impact_level. Valid values: {string.Join(", ", ValidImpactLevels)}")
        };
    }

    private static string ToCategoryString(EventCategory category)
    {
        return category switch
        {
            EventCategory.Fed => "fed",
            EventCategory.Treasury => "treasury",
            EventCategory.Geopolitical => "geopolitical",
            EventCategory.Regulatory => "regulatory",
            EventCategory.CentralBank => "central_bank",
            EventCategory.Institutional => "institutional",
            _ => "all"
        };
    }

    private sealed record ProviderAttempt(string ProviderId, IReadOnlyList<MarketEvent> Events, Exception? Error);
}