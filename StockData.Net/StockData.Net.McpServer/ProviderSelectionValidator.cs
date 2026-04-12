using System.Text.RegularExpressions;
using StockData.Net.Configuration;
using StockData.Net.McpServer.Models;
using StockData.Net.Providers;
using StockData.Net.Providers.SocialMedia;

namespace StockData.Net.McpServer;

public sealed class ProviderSelectionValidator
{
    private static readonly Regex AllowedProviderPattern =
        new("^[a-zA-Z0-9_ ]{1,50}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

    private static readonly Dictionary<string, (string CanonicalId, string DisplayName)> ProviderMetadata =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["yahoo_finance"] = (
                "yahoo",
                "Yahoo Finance"),
            ["alphavantage"] = (
                "alphavantage",
                "Alpha Vantage"),
            ["finnhub"] = (
                "finnhub",
                "Finnhub"),
            ["alpaca"] = (
                "alpaca",
                "Alpaca Markets"),
            ["xtwitter"] = (
                "xtwitter",
                "X / Twitter")
        };

    private readonly McpConfiguration _configuration;
    private readonly HashSet<string> _registeredProviders;
    private readonly Dictionary<string, string> _aliases;
    private readonly Dictionary<string, IStockDataProvider> _providersById;
    private readonly Dictionary<string, ISocialMediaProvider> _socialProvidersById;

    public ProviderSelectionValidator(McpConfiguration configuration, IEnumerable<string> registeredProviders)
        : this(configuration, registeredProviders, null)
    {
    }

    public ProviderSelectionValidator(
        McpConfiguration configuration,
        IEnumerable<string> registeredProviders,
        IEnumerable<IStockDataProvider>? providers)
        : this(configuration, registeredProviders, providers, null)
    {
    }

    public ProviderSelectionValidator(
        McpConfiguration configuration,
        IEnumerable<string> registeredProviders,
        IEnumerable<IStockDataProvider>? providers,
        IEnumerable<ISocialMediaProvider>? socialProviders)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _registeredProviders = new HashSet<string>(registeredProviders ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        _aliases = BuildAliasMap(configuration);
        _providersById = (providers ?? Enumerable.Empty<IStockDataProvider>())
            .GroupBy(provider => provider.ProviderId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        _socialProvidersById = (socialProviders ?? Enumerable.Empty<ISocialMediaProvider>())
            .GroupBy(provider => provider.ProviderId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public ProviderValidationResult Validate(string? provider)
    {
        return ValidateForCategory(provider, ProviderCategory.FinancialData);
    }

    public ProviderValidationResult ValidateForCategory(string? provider, ProviderCategory category)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return ProviderValidationResult.NoSelection();
        }

        var normalizedInput = provider.Trim();
        if (normalizedInput.Length > 50 || !AllowedProviderPattern.IsMatch(normalizedInput))
        {
            return ProviderValidationResult.Invalid(
                $"Provider '{provider}' is not available. Supported providers: {string.Join(", ", GetSupportedServiceKeys())}");
        }

        if (!_aliases.TryGetValue(normalizedInput, out var providerId))
        {
            return ProviderValidationResult.Invalid(
                $"Provider '{provider}' is not available. Supported providers: {string.Join(", ", GetSupportedServiceKeys())}");
        }

        if (!_registeredProviders.Contains(providerId))
        {
            return ProviderValidationResult.Invalid($"Provider '{normalizedInput}' is not currently available.");
        }

        var configuredProvider = _configuration.Providers.FirstOrDefault(
            p => string.Equals(p.Id, providerId, StringComparison.OrdinalIgnoreCase));
        if (configuredProvider is not null)
        {
            var providerCategory = InferCategory(configuredProvider.Type);
            if (providerCategory != category)
            {
                return ProviderValidationResult.Invalid($"Provider '{normalizedInput}' is not valid for {category} requests.");
            }
        }

        return ProviderValidationResult.Valid(providerId);
    }

    public string GetServiceKeyForProviderId(string providerId)
    {
        var explicitAlias = _configuration.ProviderSelection.Aliases
            .Where(kvp => string.Equals(kvp.Value, providerId, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key)
            .Where(alias => !alias.Contains(' '))
            .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(explicitAlias))
        {
            return explicitAlias;
        }

        return providerId;
    }

    public string GetTierForProviderId(string providerId)
    {
        var provider = _configuration.Providers.FirstOrDefault(
            p => string.Equals(p.Id, providerId, StringComparison.OrdinalIgnoreCase));
        if (provider == null || string.IsNullOrWhiteSpace(provider.Tier))
        {
            return "free";
        }

        return provider.Tier;
    }

    public string? ResolveDefaultProviderForDataType(string dataType)
    {
        if (_configuration.ProviderSelection.DefaultProvider.TryGetValue(dataType, out var explicitDefault)
            && !string.IsNullOrWhiteSpace(explicitDefault))
        {
            return explicitDefault;
        }

        if (_configuration.Routing.DataTypeRouting.TryGetValue(dataType, out var routing)
            && !string.IsNullOrWhiteSpace(routing.PrimaryProviderId))
        {
            return routing.PrimaryProviderId;
        }

        return "yahoo_finance";
    }

    public IReadOnlyList<ProviderInfo> GetAvailableProviders()
    {
        if (_registeredProviders.Count == 0)
        {
            return Array.Empty<ProviderInfo>();
        }

        var providers = _registeredProviders
            .Select(providerId =>
            {
                var metadata = ProviderMetadata.TryGetValue(providerId, out var providerMetadata)
                    ? providerMetadata
                    : (CanonicalId: providerId, DisplayName: providerId);

                var tier = GetTierForProviderId(providerId);
                var category = ResolveProviderCategory(providerId);
                var supportedDataTypes = category == ProviderCategory.SocialMedia
                    ? GetSupportedSocialDataTypes(providerId)
                    : GetSupportedDataTypes(providerId, tier);

                var aliases = _aliases
                    .Where(kvp => string.Equals(kvp.Value, providerId, StringComparison.OrdinalIgnoreCase))
                    .Select(kvp => kvp.Key)
                    .Where(alias => !alias.Contains(' '))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(alias => alias, StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new ProviderInfo
                {
                    Id = metadata.CanonicalId,
                    DisplayName = metadata.DisplayName,
                    Aliases = aliases,
                    SupportedDataTypes = supportedDataTypes,
                    Category = category.ToString()
                };
            })
            .OrderBy(provider => provider.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return providers;
    }

    private List<string> GetSupportedServiceKeys()
    {
        return _configuration.ProviderSelection.Aliases
            .Where(kvp => !kvp.Key.Contains(' '))
            .Select(kvp => kvp.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, string> BuildAliasMap(McpConfiguration configuration)
    {
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var alias in configuration.ProviderSelection.Aliases)
        {
            aliases[alias.Key.Trim()] = alias.Value.Trim();
        }

        foreach (var provider in configuration.Providers)
        {
            if (!string.IsNullOrWhiteSpace(provider.Id))
            {
                aliases.TryAdd(provider.Id.Trim(), provider.Id.Trim());
            }
        }

        return aliases;
    }

    private string[] GetSupportedDataTypes(string providerId, string tier)
    {
        if (_providersById.TryGetValue(providerId, out var provider))
        {
            return provider.GetSupportedDataTypes(tier)
                .OrderBy(capability => capability, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // Fallback for tests/legacy call sites that only pass provider IDs.
        IEnumerable<string> fallbackCapabilities = providerId.ToLowerInvariant() switch
        {
            "yahoo_finance" => new[]
            {
                "historical_prices",
                "stock_info",
                "news",
                "market_news",
                "stock_actions",
                "financial_statement",
                "holder_info",
                "option_expiration_dates",
                "option_chain",
                "recommendations"
            },
            "finnhub" => string.Equals(tier, "paid", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    "stock_info",
                    "news",
                    "market_news",
                    "recommendations",
                    "historical_prices",
                    "stock_actions"
                }
                : new[]
                {
                    "stock_info",
                    "news",
                    "market_news",
                    "recommendations"
                },
            "alphavantage" => new[]
            {
                "historical_prices",
                "stock_info",
                "news",
                "market_news",
                "stock_actions"
            },
            "alpaca" => string.Equals(tier, "paid", StringComparison.OrdinalIgnoreCase)
                ? new[]
                {
                    "historical_prices",
                    "stock_info",
                    "news",
                    "market_news"
                }
                : new[]
                {
                    "historical_prices",
                    "stock_info"
                },
            _ => Array.Empty<string>()
        };

        return fallbackCapabilities
            .OrderBy(capability => capability, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string[] GetSupportedSocialDataTypes(string providerId)
    {
        if (_socialProvidersById.ContainsKey(providerId))
        {
            return new[] { "social_feed" };
        }

        return providerId.Equals("xtwitter", StringComparison.OrdinalIgnoreCase)
            ? new[] { "social_feed" }
            : Array.Empty<string>();
    }

    private ProviderCategory ResolveProviderCategory(string providerId)
    {
        if (_socialProvidersById.ContainsKey(providerId))
        {
            return ProviderCategory.SocialMedia;
        }

        var configuredProvider = _configuration.Providers.FirstOrDefault(
            p => string.Equals(p.Id, providerId, StringComparison.OrdinalIgnoreCase));
        if (configuredProvider is not null)
        {
            return InferCategory(configuredProvider.Type);
        }

        return ProviderCategory.FinancialData;
    }

    private static ProviderCategory InferCategory(string? providerType)
    {
        if (!string.IsNullOrWhiteSpace(providerType)
            && providerType.Contains("x", StringComparison.OrdinalIgnoreCase)
            && providerType.Contains("twitter", StringComparison.OrdinalIgnoreCase))
        {
            return ProviderCategory.SocialMedia;
        }

        return ProviderCategory.FinancialData;
    }
}

public sealed record ProviderValidationResult(bool IsValid, bool IsExplicitSelection, string? ResolvedProviderId, string? ErrorMessage)
{
    public static ProviderValidationResult NoSelection() => new(true, false, null, null);

    public static ProviderValidationResult Valid(string providerId) => new(true, true, providerId, null);

    public static ProviderValidationResult Invalid(string error) => new(false, true, null, error);
}
