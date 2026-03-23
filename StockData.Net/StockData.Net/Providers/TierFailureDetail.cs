namespace StockData.Net.Providers;

public record TierFailureDetail(string ProviderName, string DataType, string ConfiguredTier, string UpgradeUrl);
