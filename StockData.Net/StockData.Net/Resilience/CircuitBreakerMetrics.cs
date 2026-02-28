namespace StockData.Net.Resilience;

/// <summary>
/// Metrics for circuit breaker monitoring
/// </summary>
public class CircuitBreakerMetrics
{
    /// <summary>
    /// Current state of the circuit breaker
    /// </summary>
    public CircuitBreakerState State { get; set; }

    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Number of successful requests
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Total number of failed requests
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Timestamp when circuit was last opened
    /// </summary>
    public DateTime? LastOpenedAt { get; set; }

    /// <summary>
    /// Timestamp when circuit transitioned to half-open
    /// </summary>
    public DateTime? LastHalfOpenAt { get; set; }

    /// <summary>
    /// Timestamp of last state transition
    /// </summary>
    public DateTime LastStateTransition { get; set; }

    /// <summary>
    /// Number of state transitions
    /// </summary>
    public int StateTransitionCount { get; set; }
}
