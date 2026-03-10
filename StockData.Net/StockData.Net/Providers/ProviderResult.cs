namespace StockData.Net.Providers;

/// <summary>
/// Contains provider response payload and the provider that fulfilled the request.
/// </summary>
public sealed record ProviderResult(string Result, string ProviderId);
