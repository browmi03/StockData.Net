namespace StockData.Net.Providers;

/// <summary>
/// Represents a provider capability limitation with optional paid-tier availability metadata.
/// </summary>
public sealed class TierAwareNotSupportedException : NotSupportedException
{
    public string ProviderId { get; }
    public string MethodName { get; }
    public bool AvailableOnPaidTier { get; }

    public TierAwareNotSupportedException(string providerId, string methodName, bool availableOnPaidTier)
        : base(FormatMessage(providerId, methodName, availableOnPaidTier))
    {
        ProviderId = providerId;
        MethodName = methodName;
        AvailableOnPaidTier = availableOnPaidTier;
    }

    private static string FormatMessage(string providerId, string methodName, bool availableOnPaidTier)
    {
        return availableOnPaidTier
            ? $"Provider '{providerId}' does not support {methodName} on the free tier. This feature is available with a paid subscription."
            : $"Provider '{providerId}' does not support {methodName}.";
    }
}