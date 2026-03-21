namespace StockData.Net.Providers;

/// <summary>
/// Exception thrown when all providers in a failover chain fail
/// </summary>
public class ProviderFailoverException : Exception
{
    /// <summary>
    /// The data type that was being requested
    /// </summary>
    public string DataType { get; }

    /// <summary>
    /// Tier-related provider failures captured during pre-flight capability checks.
    /// </summary>
    public IReadOnlyList<TierFailureDetail> TierFailures { get; init; } = Array.Empty<TierFailureDetail>();

    /// <summary>
    /// Exceptions from each provider that was attempted
    /// </summary>
    public Dictionary<string, Exception> ProviderErrors { get; }

    /// <summary>
    /// The order in which providers were attempted
    /// </summary>
    public List<string> AttemptedProviders { get; }

    public ProviderFailoverException(
        string dataType,
        Dictionary<string, Exception> providerErrors,
        List<string> attemptedProviders,
        IReadOnlyList<TierFailureDetail>? tierFailures = null)
        : base(BuildMessage(dataType, providerErrors, attemptedProviders))
    {
        DataType = dataType;
        ProviderErrors = providerErrors;
        AttemptedProviders = attemptedProviders;
        TierFailures = tierFailures ?? Array.Empty<TierFailureDetail>();
    }

    private static string BuildMessage(
        string dataType,
        Dictionary<string, Exception> providerErrors,
        List<string> attemptedProviders)
    {
        return $"All providers failed for data type '{dataType}'. " +
               $"Attempted providers: {attemptedProviders.Count}. " +
               $"Error count: {providerErrors.Count}.";
    }
}
