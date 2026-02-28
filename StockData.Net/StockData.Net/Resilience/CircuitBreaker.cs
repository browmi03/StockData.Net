using Microsoft.Extensions.Logging;
using StockData.Net.Configuration;

namespace StockData.Net.Resilience;

/// <summary>
/// Circuit breaker implementation for provider resilience
/// Three states: Closed (normal), Open (blocking), Half-Open (testing recovery)
/// </summary>
public class CircuitBreaker
{
    private readonly CircuitBreakerConfiguration _configuration;
    private readonly ILogger<CircuitBreaker> _logger;
    private readonly string _providerId;
    private readonly object _stateLock = new object();

    private CircuitBreakerState _state = CircuitBreakerState.Closed;
    private int _consecutiveFailures = 0;
    private int _successCount = 0;
    private int _failureCount = 0;
    private DateTime? _lastOpenedAt = null;
    private DateTime? _lastHalfOpenAt = null;
    private DateTime _lastStateTransition = DateTime.UtcNow;
    private int _stateTransitionCount = 0;
    private bool _halfOpenTestInProgress = false;

    public CircuitBreaker(
        string providerId,
        CircuitBreakerConfiguration configuration,
        ILogger<CircuitBreaker> logger)
    {
        _providerId = providerId ?? throw new ArgumentNullException(nameof(providerId));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes an operation with circuit breaker protection
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        if (!_configuration.Enabled)
        {
            // Circuit breaker disabled, execute directly
            return await operation(cancellationToken);
        }

        // Check if we should attempt the operation
        CheckAndUpdateState();

        lock (_stateLock)
        {
            if (_state == CircuitBreakerState.Open)
            {
                var retryAfter = _lastOpenedAt!.Value.AddSeconds(_configuration.HalfOpenAfterSeconds);
                _logger.LogWarning(
                    "Circuit breaker is open for provider {ProviderId}. Retry after {RetryAfter}",
                    _providerId, retryAfter);
                throw new CircuitBreakerOpenException(_providerId, retryAfter);
            }

            if (_state == CircuitBreakerState.HalfOpen)
            {
                if (_halfOpenTestInProgress)
                {
                    // Another test request is in progress, reject this one
                    var retryAfter = DateTime.UtcNow.AddSeconds(5);
                    _logger.LogDebug(
                        "Circuit breaker is half-open for provider {ProviderId} with test in progress. Retry after {RetryAfter}",
                        _providerId, retryAfter);
                    throw new CircuitBreakerOpenException(_providerId, retryAfter,
                        $"Circuit breaker is half-open for provider '{_providerId}' with test request in progress");
                }

                _halfOpenTestInProgress = true;
            }
        }

        try
        {
            // Apply timeout if configured
            if (_configuration.TimeoutSeconds > 0)
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(_configuration.TimeoutSeconds));

                var result = await operation(timeoutCts.Token);
                OnSuccess();
                return result;
            }
            else
            {
                var result = await operation(cancellationToken);
                OnSuccess();
                return result;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // User cancellation, don't count as failure
            lock (_stateLock)
            {
                _halfOpenTestInProgress = false;
            }
            throw;
        }
        catch (Exception ex)
        {
            OnFailure(ex);
            throw;
        }
    }

    /// <summary>
    /// Gets the current state of the circuit breaker
    /// </summary>
    public CircuitBreakerState GetState()
    {
        CheckAndUpdateState();
        lock (_stateLock)
        {
            return _state;
        }
    }

    /// <summary>
    /// Gets current metrics
    /// </summary>
    public CircuitBreakerMetrics GetMetrics()
    {
        lock (_stateLock)
        {
            return new CircuitBreakerMetrics
            {
                State = _state,
                ConsecutiveFailures = _consecutiveFailures,
                SuccessCount = _successCount,
                FailureCount = _failureCount,
                LastOpenedAt = _lastOpenedAt,
                LastHalfOpenAt = _lastHalfOpenAt,
                LastStateTransition = _lastStateTransition,
                StateTransitionCount = _stateTransitionCount
            };
        }
    }

    /// <summary>
    /// Manually resets the circuit breaker to closed state
    /// </summary>
    public void Reset()
    {
        lock (_stateLock)
        {
            _logger.LogInformation("Manually resetting circuit breaker for provider {ProviderId}", _providerId);
            TransitionTo(CircuitBreakerState.Closed);
            _consecutiveFailures = 0;
            _halfOpenTestInProgress = false;
        }
    }

    private void CheckAndUpdateState()
    {
        lock (_stateLock)
        {
            if (_state == CircuitBreakerState.Open && _lastOpenedAt.HasValue)
            {
                var halfOpenTime = _lastOpenedAt.Value.AddSeconds(_configuration.HalfOpenAfterSeconds);
                if (DateTime.UtcNow >= halfOpenTime)
                {
                    TransitionTo(CircuitBreakerState.HalfOpen);
                    _lastHalfOpenAt = DateTime.UtcNow;
                    _halfOpenTestInProgress = false;
                }
            }
        }
    }

    private void OnSuccess()
    {
        lock (_stateLock)
        {
            _successCount++;
            _consecutiveFailures = 0;
            _halfOpenTestInProgress = false;

            if (_state == CircuitBreakerState.HalfOpen)
            {
                _logger.LogInformation(
                    "Test request succeeded in half-open state for provider {ProviderId}. Closing circuit",
                    _providerId);
                TransitionTo(CircuitBreakerState.Closed);
            }
            else if (_state == CircuitBreakerState.Closed)
            {
                _logger.LogDebug("Request succeeded for provider {ProviderId}", _providerId);
            }
        }
    }

    private void OnFailure(Exception exception)
    {
        lock (_stateLock)
        {
            _failureCount++;
            _consecutiveFailures++;
            _halfOpenTestInProgress = false;

            _logger.LogWarning(exception,
                "Request failed for provider {ProviderId}. Consecutive failures: {ConsecutiveFailures}",
                _providerId, _consecutiveFailures);

            if (_state == CircuitBreakerState.HalfOpen)
            {
                _logger.LogWarning(
                    "Test request failed in half-open state for provider {ProviderId}. Re-opening circuit",
                    _providerId);
                TransitionTo(CircuitBreakerState.Open);
                _lastOpenedAt = DateTime.UtcNow;
            }
            else if (_state == CircuitBreakerState.Closed &&
                     _consecutiveFailures >= _configuration.FailureThreshold)
            {
                _logger.LogError(
                    "Failure threshold ({Threshold}) reached for provider {ProviderId}. Opening circuit",
                    _configuration.FailureThreshold, _providerId);
                TransitionTo(CircuitBreakerState.Open);
                _lastOpenedAt = DateTime.UtcNow;
            }
        }
    }

    private void TransitionTo(CircuitBreakerState newState)
    {
        if (_state != newState)
        {
            var oldState = _state;
            _state = newState;
            _lastStateTransition = DateTime.UtcNow;
            _stateTransitionCount++;

            _logger.LogInformation(
                "Circuit breaker state transition for provider {ProviderId}: {OldState} -> {NewState}",
                _providerId, oldState, newState);
        }
    }
}
