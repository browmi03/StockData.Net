using Microsoft.Extensions.Logging;
using StockData.Net.Configuration;
using System.Collections.Concurrent;

namespace StockData.Net.Providers;

/// <summary>
/// Monitors health of providers and maintains health metrics
/// </summary>
public class ProviderHealthMonitor
{
    private readonly HealthCheckConfiguration _configuration;
    private readonly ILogger<ProviderHealthMonitor>? _logger;
    private readonly ConcurrentDictionary<string, ProviderHealthData> _healthData = new();
    private readonly object _healthCheckLock = new object();
    private Timer? _healthCheckTimer;
    private readonly Func<string, CancellationToken, Task<bool>>? _healthCheckFunc;

    private class ProviderHealthData
    {
        public bool IsHealthy { get; set; } = true;
        public int ConsecutiveFailures { get; set; } = 0;
        public Queue<RequestRecord> RecentRequests { get; } = new();
        public DateTime LastHealthCheckAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSuccessAt { get; set; }
        public ConcurrentDictionary<ProviderErrorType, int> ErrorTypeCounts { get; } = new();
        public object Lock { get; } = new object();
    }

    private class RequestRecord
    {
        public bool IsSuccess { get; set; }
        public long ResponseTimeMs { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public ProviderHealthMonitor(
        HealthCheckConfiguration configuration,
        ILogger<ProviderHealthMonitor>? logger = null,
        Func<string, CancellationToken, Task<bool>>? healthCheckFunc = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger;
        _healthCheckFunc = healthCheckFunc;
    }

    /// <summary>
    /// Starts background health monitoring
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_configuration.Enabled)
        {
            _logger?.LogInformation("Provider health monitoring is disabled");
            return Task.CompletedTask;
        }

        _logger?.LogInformation(
            "Starting provider health monitoring with interval {IntervalSeconds}s",
            _configuration.IntervalSeconds);

        var interval = TimeSpan.FromSeconds(_configuration.IntervalSeconds);
        _healthCheckTimer = new Timer(
            _ => PerformHealthChecks(),
            null,
            interval,
            interval);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops background health monitoring
    /// </summary>
    public Task StopAsync()
    {
        _healthCheckTimer?.Dispose();
        _healthCheckTimer = null;
        _logger?.LogInformation("Stopped provider health monitoring");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets health status for a specific provider
    /// </summary>
    public ProviderHealthStatus GetHealthStatus(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new ArgumentNullException(nameof(providerId));

        var data = _healthData.GetOrAdd(providerId, _ => new ProviderHealthData());

        lock (data.Lock)
        {
            // Clean up old records (older than 5 minutes)
            var cutoff = DateTime.UtcNow.AddMinutes(-5);
            while (data.RecentRequests.Count > 0 && data.RecentRequests.Peek().Timestamp < cutoff)
            {
                data.RecentRequests.Dequeue();
            }

            var recentRequests = data.RecentRequests.ToList();
            var totalRequests = recentRequests.Count;
            var successfulRequests = recentRequests.Count(r => r.IsSuccess);
            var failedRequests = totalRequests - successfulRequests;
            var errorRate = totalRequests > 0 ? (double)failedRequests / totalRequests : 0.0;
            var avgResponseTime = recentRequests.Count > 0
                ? recentRequests.Average(r => r.ResponseTimeMs)
                : 0.0;

            return new ProviderHealthStatus
            {
                ProviderId = providerId,
                IsHealthy = data.IsHealthy,
                ConsecutiveFailures = data.ConsecutiveFailures,
                ErrorRate = errorRate,
                AverageResponseTimeMs = avgResponseTime,
                TotalRequests = totalRequests,
                SuccessfulRequests = successfulRequests,
                FailedRequests = failedRequests,
                LastHealthCheckAt = data.LastHealthCheckAt,
                LastSuccessAt = data.LastSuccessAt,
                ErrorTypeBreakdown = new Dictionary<ProviderErrorType, int>(
                    data.ErrorTypeCounts.Select(kvp => new KeyValuePair<ProviderErrorType, int>(kvp.Key, kvp.Value)))
            };
        }
    }

    /// <summary>
    /// Records a successful request
    /// </summary>
    public void RecordSuccess(string providerId, TimeSpan responseTime)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return;

        var data = _healthData.GetOrAdd(providerId, _ => new ProviderHealthData());

        lock (data.Lock)
        {
            data.ConsecutiveFailures = 0;
            data.LastSuccessAt = DateTime.UtcNow;

            // Mark as healthy if it was previously unhealthy
            if (!data.IsHealthy)
            {
                data.IsHealthy = true;
                _logger?.LogInformation("Provider {ProviderId} is now healthy", providerId);
            }

            data.RecentRequests.Enqueue(new RequestRecord
            {
                IsSuccess = true,
                ResponseTimeMs = (long)responseTime.TotalMilliseconds,
                Timestamp = DateTime.UtcNow
            });

            // Keep only last 100 requests
            while (data.RecentRequests.Count > 100)
            {
                data.RecentRequests.Dequeue();
            }
        }
    }

    /// <summary>
    /// Records a failed request
    /// </summary>
    public void RecordFailure(string providerId, ProviderErrorType errorType)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            return;

        var data = _healthData.GetOrAdd(providerId, _ => new ProviderHealthData());

        lock (data.Lock)
        {
            data.ConsecutiveFailures++;
            data.ErrorTypeCounts.AddOrUpdate(errorType, 1, (_, count) => count + 1);

            data.RecentRequests.Enqueue(new RequestRecord
            {
                IsSuccess = false,
                ResponseTimeMs = 0,
                Timestamp = DateTime.UtcNow
            });

            // Keep only last 100 requests
            while (data.RecentRequests.Count > 100)
            {
                data.RecentRequests.Dequeue();
            }

            // Mark as unhealthy if threshold exceeded (default 3 consecutive failures)
            const int UnhealthyThreshold = 3;
            if (data.ConsecutiveFailures >= UnhealthyThreshold && data.IsHealthy)
            {
                data.IsHealthy = false;
                _logger?.LogWarning(
                    "Provider {ProviderId} marked as unhealthy after {ConsecutiveFailures} consecutive failures",
                    providerId, data.ConsecutiveFailures);
            }
        }
    }

    /// <summary>
    /// Gets health status for all tracked providers
    /// </summary>
    public Dictionary<string, ProviderHealthStatus> GetAllHealthStatus()
    {
        return _healthData.Keys.ToDictionary(
            providerId => providerId,
            providerId => GetHealthStatus(providerId));
    }

    private void PerformHealthChecks()
    {
        if (_healthCheckFunc == null)
        {
            _logger?.LogDebug("No health check function provided, skipping health checks");
            return;
        }

        lock (_healthCheckLock)
        {
            foreach (var providerId in _healthData.Keys)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(
                            TimeSpan.FromSeconds(_configuration.TimeoutSeconds));

                        var isHealthy = await _healthCheckFunc(providerId, cts.Token);

                        var data = _healthData[providerId];
                        lock (data.Lock)
                        {
                            data.LastHealthCheckAt = DateTime.UtcNow;
                            if (isHealthy && !data.IsHealthy)
                            {
                                data.IsHealthy = true;
                                data.ConsecutiveFailures = 0;
                                _logger?.LogInformation(
                                    "Provider {ProviderId} health check passed - marked as healthy",
                                    providerId);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex,
                            "Health check failed for provider {ProviderId}",
                            providerId);

                        RecordFailure(providerId, ProviderErrorType.ServiceError);
                    }
                });
            }
        }
    }
}
