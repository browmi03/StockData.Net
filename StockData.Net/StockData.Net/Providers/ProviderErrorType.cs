namespace StockData.Net.Providers;

/// <summary>
/// Types of errors that can occur with providers
/// </summary>
public enum ProviderErrorType
{
    /// <summary>
    /// Network or connection error
    /// </summary>
    NetworkError,

    /// <summary>
    /// Request timeout
    /// </summary>
    Timeout,

    /// <summary>
    /// Provider returned an error response (4xx, 5xx)
    /// </summary>
    ServiceError,

    /// <summary>
    /// Rate limit exceeded
    /// </summary>
    RateLimitExceeded,

    /// <summary>
    /// Data format or parsing error
    /// </summary>
    DataError,

    /// <summary>
    /// Authentication or authorization error
    /// </summary>
    AuthenticationError,

    /// <summary>
    /// Unknown or unclassified error
    /// </summary>
    Unknown
}
