namespace StockData.Net.Resilience;

/// <summary>
/// Exception thrown when a circuit breaker is open and rejecting requests
/// </summary>
public class CircuitBreakerOpenException : Exception
{
    /// <summary>
    /// The provider ID whose circuit breaker is open
    /// </summary>
    public string ProviderId { get; }

    /// <summary>
    /// When the circuit breaker will attempt to transition to half-open
    /// </summary>
    public DateTime RetryAfter { get; }

    public CircuitBreakerOpenException(string providerId, DateTime retryAfter)
        : base($"Circuit breaker is open for provider '{providerId}'. Retry after {retryAfter:O}.")
    {
        ProviderId = providerId;
        RetryAfter = retryAfter;
    }

    public CircuitBreakerOpenException(string providerId, DateTime retryAfter, string message)
        : base(message)
    {
        ProviderId = providerId;
        RetryAfter = retryAfter;
    }

    public CircuitBreakerOpenException(string providerId, DateTime retryAfter, string message, Exception innerException)
        : base(message, innerException)
    {
        ProviderId = providerId;
        RetryAfter = retryAfter;
    }
}
