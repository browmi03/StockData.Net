using System.Text.RegularExpressions;
using StockData.Net.Configuration;
using StockData.Net.McpServer.Models;

namespace StockData.Net.McpServer;

public sealed class ProviderSelectionValidator
{
    private static readonly Regex AllowedProviderPattern =
        new("^[a-zA-Z0-9_ ]{1,50}$", RegexOptions.Compiled, TimeSpan.FromMilliseconds(50));

    private static readonly Dictionary<string, (string CanonicalId, string DisplayName, string[] SupportedDataTypes)> ProviderMetadata =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["yahoo_finance"] = (
                "yahoo",
                "Yahoo Finance",
                new[]
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
                }),
            ["alphavantage"] = (
                "alphavantage",
                "Alpha Vantage",
                new[]
                {
                    "historical_prices",
                    "stock_info",
                    "news"
                }),
            ["finnhub"] = (
                "finnhub",
                "Finnhub",
                new[]
                {
                    "historical_prices",
                    "stock_info",
                    "news",
                    "market_news"
                })
        };

    private readonly McpConfiguration _configuration;
    private readonly HashSet<string> _registeredProviders;
    private readonly Dictionary<string, string> _aliases;

    public ProviderSelectionValidator(McpConfiguration configuration, IEnumerable<string> registeredProviders)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _registeredProviders = new HashSet<string>(registeredProviders ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        _aliases = BuildAliasMap(configuration);
    }

    public ProviderValidationResult Validate(string? provider)
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
                    : (CanonicalId: providerId, DisplayName: providerId, SupportedDataTypes: Array.Empty<string>());

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
                    SupportedDataTypes = metadata.SupportedDataTypes
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
}

public sealed record ProviderValidationResult(bool IsValid, bool IsExplicitSelection, string? ResolvedProviderId, string? ErrorMessage)
{
    public static ProviderValidationResult NoSelection() => new(true, false, null, null);

    public static ProviderValidationResult Valid(string providerId) => new(true, true, providerId, null);

    public static ProviderValidationResult Invalid(string error) => new(false, true, null, error);
}
