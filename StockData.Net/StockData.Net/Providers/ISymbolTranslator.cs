namespace StockData.Net.Providers;

/// <summary>
/// Translates market symbols to provider-specific formats.
/// </summary>
public interface ISymbolTranslator
{
    string Translate(string symbol, string providerId);
}