namespace StockData.Net.Providers;

/// <summary>
/// Health status information for a provider
/// </summary>
public class ProviderHealthStatus
{
    /// <summary>
    /// Provider identifier
    /// </summary>
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the provider is currently healthy
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Error rate (0.0 to 1.0) in the rolling window
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTimeMs { get; set; }

    /// <summary>
    /// Total number of requests tracked
    /// </summary>
    public int TotalRequests { get; set; }

    /// <summary>
    /// Total number of successful requests
    /// </summary>
    public int SuccessfulRequests { get; set; }

    /// <summary>
    /// Total number of failed requests
    /// </summary>
    public int FailedRequests { get; set; }

    /// <summary>
    /// Last time health check was performed
    /// </summary>
    public DateTime LastHealthCheckAt { get; set; }

    /// <summary>
    /// Last time a successful request completed
    /// </summary>
    public DateTime? LastSuccessAt { get; set; }

    /// <summary>
    /// Error type breakdown
    /// </summary>
    public Dictionary<ProviderErrorType, int> ErrorTypeBreakdown { get; set; } = new();
}
