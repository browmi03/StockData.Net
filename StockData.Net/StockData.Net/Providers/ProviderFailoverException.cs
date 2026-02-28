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
        List<string> attemptedProviders)
        : base(BuildMessage(dataType, providerErrors, attemptedProviders))
    {
        DataType = dataType;
        ProviderErrors = providerErrors;
        AttemptedProviders = attemptedProviders;
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
