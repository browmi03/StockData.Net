namespace StockData.Net.Providers;

/// <summary>
/// Translates symbols between canonical/provider-specific formats using in-memory dictionaries.
/// </summary>
public sealed class SymbolTranslator : ISymbolTranslator
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> CanonicalMappings =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["VIX"] = ProviderMap("^VIX", "@VX"),
            ["GSPC"] = ProviderMap("^GSPC", "@SPX"),
            ["DJI"] = ProviderMap("^DJI", "@DJI"),
            ["IXIC"] = ProviderMap("^IXIC", "@IXIC"),
            ["RUT"] = ProviderMap("^RUT", "@RUT"),
            ["NDX"] = ProviderMap("^NDX", "@NDX"),
            ["NYA"] = ProviderMap("^NYA", "@NYA"),
            ["OEX"] = ProviderMap("^OEX", "@OEX"),
            ["MID"] = ProviderMap("^MID", "@MID"),
            ["FTSE"] = ProviderMap("^FTSE", "@FTSE"),
            ["GDAXI"] = ProviderMap("^GDAXI", "@DAX"),
            ["N225"] = ProviderMap("^N225", "@N225"),
            ["HSI"] = ProviderMap("^HSI", "@HSI"),
            ["SSEC"] = ProviderMap("^SSEC", "@SSEC"),
            ["AXJO"] = ProviderMap("^AXJO", "@AXJO"),
            ["KS11"] = ProviderMap("^KS11", "@KS11"),
            ["BSESN"] = ProviderMap("^BSESN", "@BSESN"),
            ["SOX"] = ProviderMap("^SOX", "@SOX"),
            ["XOI"] = ProviderMap("^XOI", "@XOI"),
            ["HUI"] = ProviderMap("^HUI", "@HUI"),
            ["XAU"] = ProviderMap("^XAU", "@XAU"),
            ["VXN"] = ProviderMap("^VXN", "@VXN"),
            ["RVX"] = ProviderMap("^RVX", "@RVX"),
            ["TNX"] = ProviderMap("^TNX", "@TNX"),
            ["TYX"] = ProviderMap("^TYX", "@TYX"),
            ["FVX"] = ProviderMap("^FVX", "@FVX"),
            ["IRX"] = ProviderMap("^IRX", "@IRX")
        };

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _canonicalMappings;
    private readonly IReadOnlyDictionary<string, string> _reverseIndex;

    public SymbolTranslator()
    {
        _canonicalMappings = CanonicalMappings;
        _reverseIndex = BuildReverseIndex(_canonicalMappings);
    }

    public string Translate(string symbol, string providerId)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            throw new ArgumentException("Symbol cannot be null, empty, or whitespace.", nameof(symbol));
        }

        if (string.IsNullOrWhiteSpace(providerId))
        {
            throw new ArgumentException("Provider id cannot be null, empty, or whitespace.", nameof(providerId));
        }

        var normalizedSymbol = symbol.ToUpperInvariant();
        var normalizedProviderId = providerId.ToUpperInvariant();

        if (!_reverseIndex.TryGetValue(normalizedSymbol, out var canonicalSymbol))
        {
            return symbol;
        }

        var providerMappings = _canonicalMappings[canonicalSymbol];

        return providerMappings.TryGetValue(normalizedProviderId, out var translated)
            ? translated
            : symbol;
    }

    private static IReadOnlyDictionary<string, string> ProviderMap(string yahoo, string finviz)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["YAHOO_FINANCE"] = yahoo,
            ["FINVIZ"] = finviz
        };
    }

    private static IReadOnlyDictionary<string, string> BuildReverseIndex(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> canonicalMappings)
    {
        var reverseIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (canonicalSymbol, providerMappings) in canonicalMappings)
        {
            AddOrThrow(reverseIndex, canonicalSymbol.ToUpperInvariant(), canonicalSymbol);

            foreach (var mappedSymbol in providerMappings.Values)
            {
                AddOrThrow(reverseIndex, mappedSymbol.ToUpperInvariant(), canonicalSymbol);
            }
        }

        return reverseIndex;
    }

    private static void AddOrThrow(IDictionary<string, string> reverseIndex, string key, string canonicalSymbol)
    {
        if (reverseIndex.TryGetValue(key, out var existingCanonical) &&
            !string.Equals(existingCanonical, canonicalSymbol, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Ambiguous mapping detected for symbol '{key}'. It maps to both '{existingCanonical}' and '{canonicalSymbol}'.");
        }

        reverseIndex[key] = canonicalSymbol;
    }
}