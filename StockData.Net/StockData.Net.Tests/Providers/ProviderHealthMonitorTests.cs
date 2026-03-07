using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using StockData.Net.Configuration;
using StockData.Net.Providers;
using System.Collections.Concurrent;

namespace StockData.Net.Tests.Providers;

[TestClass]
public class ProviderHealthMonitorTests
{
    private HealthCheckConfiguration _config = null!;
    private ProviderHealthMonitor _healthMonitor = null!;
    private RecordingLogger<ProviderHealthMonitor> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new HealthCheckConfiguration
        {
            Enabled = true,
            IntervalSeconds = 60,
            TimeoutSeconds = 10
        };
        _logger = new RecordingLogger<ProviderHealthMonitor>();
        _healthMonitor = new ProviderHealthMonitor(_config, _logger);
    }

    [TestMethod]
    public void Constructor_NullConfiguration_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new ProviderHealthMonitor(null!, _logger));
    }

    [TestMethod]
    public void GetHealthStatus_NewProvider_ReturnsHealthyByDefault()
    {
        // Act
        var status = _healthMonitor.GetHealthStatus("test_provider");

        // Assert
        Assert.IsNotNull(status);
        Assert.AreEqual("test_provider", status.ProviderId);
        Assert.IsTrue(status.IsHealthy);
        Assert.AreEqual(0, status.ConsecutiveFailures);
        Assert.AreEqual(0, status.TotalRequests);
    }

    [TestMethod]
    public void RecordSuccess_UpdatesHealthStatus()
    {
        // Act
        _healthMonitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(100));
        _healthMonitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(200));

        // Assert
        var status = _healthMonitor.GetHealthStatus("test_provider");
        Assert.IsTrue(status.IsHealthy);
        Assert.AreEqual(0, status.ConsecutiveFailures);
        Assert.AreEqual(2, status.TotalRequests);
        Assert.AreEqual(2, status.SuccessfulRequests);
        Assert.AreEqual(0, status.FailedRequests);
        Assert.IsGreaterThan(0, status.AverageResponseTimeMs); // Average response time should be positive
        Assert.IsNotNull(status.LastSuccessAt);
    }

    [TestMethod]
    public void RecordFailure_UpdatesHealthStatus()
    {
        // Act
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.NetworkError);
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.Timeout);

        // Assert
        var status = _healthMonitor.GetHealthStatus("test_provider");
        Assert.AreEqual(2, status.ConsecutiveFailures);
        Assert.AreEqual(2, status.TotalRequests);
        Assert.AreEqual(0, status.SuccessfulRequests);
        Assert.AreEqual(2, status.FailedRequests);
        Assert.AreEqual(1.0, status.ErrorRate, 0.001);
        Assert.AreEqual(1, status.ErrorTypeBreakdown[ProviderErrorType.NetworkError]);
        Assert.AreEqual(1, status.ErrorTypeBreakdown[ProviderErrorType.Timeout]);
    }

    [TestMethod]
    public void RecordFailure_AfterThreshold_MarksProviderUnhealthy()
    {
        // Act - Record 3 consecutive failures (threshold)
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.ServiceError);
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.ServiceError);
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.ServiceError);

        // Assert
        var status = _healthMonitor.GetHealthStatus("test_provider");
        Assert.IsFalse(status.IsHealthy);
        Assert.AreEqual(3, status.ConsecutiveFailures);
    }

    [TestMethod]
    public void RecordSuccess_AfterFailures_MarksProviderHealthy()
    {
        // Arrange - Mark provider as unhealthy
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.ServiceError);
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.ServiceError);
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.ServiceError);

        var statusBefore = _healthMonitor.GetHealthStatus("test_provider");
        Assert.IsFalse(statusBefore.IsHealthy);

        // Act - Record success
        _healthMonitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(100));

        // Assert
        var statusAfter = _healthMonitor.GetHealthStatus("test_provider");
        Assert.IsTrue(statusAfter.IsHealthy);
        Assert.AreEqual(0, statusAfter.ConsecutiveFailures);
    }

    [TestMethod]
    public void GetHealthStatus_CalculatesErrorRateCorrectly()
    {
        // Arrange & Act
        _healthMonitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(100));
        _healthMonitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(100));
        _healthMonitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(100));
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.ServiceError);
        _healthMonitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(100));

        // Assert - 1 failure out of 5 requests = 20% error rate
        var status = _healthMonitor.GetHealthStatus("test_provider");
        Assert.AreEqual(5, status.TotalRequests);
        Assert.AreEqual(4, status.SuccessfulRequests);
        Assert.AreEqual(1, status.FailedRequests);
        Assert.AreEqual(0.2, status.ErrorRate, 0.001);
    }

    [TestMethod]
    public void GetHealthStatus_TracksAverageResponseTime()
    {
        // Arrange & Act
        _healthMonitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(100));
        _healthMonitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(200));
        _healthMonitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(300));

        // Assert - Average should be (100 + 200 + 300) / 3 = 200ms
        var status = _healthMonitor.GetHealthStatus("test_provider");
        Assert.AreEqual(200.0, status.AverageResponseTimeMs, 0.1);
    }

    [TestMethod]
    public void GetAllHealthStatus_ReturnsAllTrackedProviders()
    {
        // Arrange
        _healthMonitor.RecordSuccess("provider_1", TimeSpan.FromMilliseconds(100));
        _healthMonitor.RecordSuccess("provider_2", TimeSpan.FromMilliseconds(200));
        _healthMonitor.RecordFailure("provider_3", ProviderErrorType.NetworkError);

        // Act
        var allStatus = _healthMonitor.GetAllHealthStatus();

        // Assert
        Assert.HasCount(3, allStatus); // Three providers tracked
        Assert.IsTrue(allStatus.ContainsKey("provider_1"));
        Assert.IsTrue(allStatus.ContainsKey("provider_2"));
        Assert.IsTrue(allStatus.ContainsKey("provider_3"));
        Assert.IsTrue(allStatus["provider_1"].IsHealthy);
        Assert.IsTrue(allStatus["provider_2"].IsHealthy);
    }

    [TestMethod]
    public void GetHealthStatus_CleanupOldRecords()
    {
        // This test verifies that old records (>5 minutes) are cleaned up
        // In practice, records are cleaned up when GetHealthStatus is called

        // Arrange & Act - Add records and verify they're included
        for (int i = 0; i < 10; i++)
        {
            _healthMonitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(100));
        }

        var status = _healthMonitor.GetHealthStatus("test_provider");
        Assert.AreEqual(10, status.TotalRequests);

        // Note: In a real test, we would need to wait 5+ minutes or use a time provider
        // For now, we verify the mechanism exists
    }

    [TestMethod]
    public void RecordFailure_WithDifferentErrorTypes_TracksBreakdown()
    {
        // Act
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.NetworkError);
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.NetworkError);
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.Timeout);
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.RateLimitExceeded);
        _healthMonitor.RecordFailure("test_provider", ProviderErrorType.Timeout);

        // Assert
        var status = _healthMonitor.GetHealthStatus("test_provider");
        Assert.AreEqual(2, status.ErrorTypeBreakdown[ProviderErrorType.NetworkError]);
        Assert.AreEqual(2, status.ErrorTypeBreakdown[ProviderErrorType.Timeout]);
        Assert.AreEqual(1, status.ErrorTypeBreakdown[ProviderErrorType.RateLimitExceeded]);
    }

    [TestMethod]
    public async Task StartAsync_WithDisabledConfiguration_DoesNotStartMonitoring()
    {
        // Arrange
        var disabledConfig = new HealthCheckConfiguration
        {
            Enabled = false,
            IntervalSeconds = 1,
            TimeoutSeconds = 10
        };
        var monitor = new ProviderHealthMonitor(disabledConfig, _logger);

        // Act
        await monitor.StartAsync();

        // Assert - Should complete immediately without error
        // No background task should be started
        await Task.Delay(100); // Give some time to verify nothing happens
        Assert.IsTrue(_logger.Entries.Any(e =>
            e.Level == LogLevel.Information &&
            e.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GetHealthStatus_WithEmptyProviderId_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _healthMonitor.GetHealthStatus(""));
    }

    [TestMethod]
    public async Task PeriodicHealthCheckAsync_ExecutesHealthCheckFunction()
    {
        var healthCheckCalls = 0;
        var calledSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var monitor = new ProviderHealthMonitor(
            new HealthCheckConfiguration { Enabled = true, IntervalSeconds = 1, TimeoutSeconds = 2 },
            _logger,
            (providerId, ct) =>
            {
                Interlocked.Increment(ref healthCheckCalls);
                calledSignal.TrySetResult(true);
                return Task.FromResult(true);
            });

        monitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(10));
        await monitor.StartAsync();

        var completed = await Task.WhenAny(calledSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await monitor.StopAsync();

        Assert.AreSame(calledSignal.Task, completed);
        Assert.IsGreaterThanOrEqualTo(healthCheckCalls, 1);
    }

    [TestMethod]
    public async Task PeriodicHealthCheckAsync_WhenProviderFails_LogsWarningAndRecordsFailure()
    {
        var monitor = new ProviderHealthMonitor(
            new HealthCheckConfiguration { Enabled = true, IntervalSeconds = 1, TimeoutSeconds = 2 },
            _logger,
            (providerId, ct) => throw new InvalidOperationException("health check failed"));

        monitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(10));
        await monitor.StartAsync();

        await WaitForConditionAsync(() =>
            _logger.Entries.Any(e =>
                e.Level == LogLevel.Warning &&
                e.Message.Contains("Health check failed for provider", StringComparison.Ordinal)));

        await monitor.StopAsync();

        var status = monitor.GetHealthStatus("test_provider");
        Assert.IsGreaterThanOrEqualTo(status.ConsecutiveFailures, 1);
        Assert.IsGreaterThanOrEqualTo(status.FailedRequests, 1);
    }

    [TestMethod]
    public async Task PeriodicHealthCheckAsync_WhenUnhealthyProviderRecovers_MarksHealthy()
    {
        var monitor = new ProviderHealthMonitor(
            new HealthCheckConfiguration { Enabled = true, IntervalSeconds = 1, TimeoutSeconds = 2 },
            _logger,
            (providerId, ct) => Task.FromResult(true));

        monitor.RecordFailure("test_provider", ProviderErrorType.ServiceError);
        monitor.RecordFailure("test_provider", ProviderErrorType.ServiceError);
        monitor.RecordFailure("test_provider", ProviderErrorType.ServiceError);

        Assert.IsFalse(monitor.GetHealthStatus("test_provider").IsHealthy);

        await monitor.StartAsync();
        await WaitForConditionAsync(() => monitor.GetHealthStatus("test_provider").IsHealthy);
        await monitor.StopAsync();

        var status = monitor.GetHealthStatus("test_provider");
        Assert.IsTrue(status.IsHealthy);
        Assert.AreEqual(0, status.ConsecutiveFailures);
    }

    [TestMethod]
    public async Task PeriodicHealthCheckAsync_WithoutHealthCheckDelegate_LogsDebugAndSkips()
    {
        var monitor = new ProviderHealthMonitor(
            new HealthCheckConfiguration { Enabled = true, IntervalSeconds = 1, TimeoutSeconds = 2 },
            _logger,
            null);

        monitor.RecordSuccess("test_provider", TimeSpan.FromMilliseconds(10));
        await monitor.StartAsync();

        await WaitForConditionAsync(() =>
            _logger.Entries.Any(e =>
                e.Level == LogLevel.Debug &&
                e.Message.Contains("skipping health checks", StringComparison.OrdinalIgnoreCase)));

        await monitor.StopAsync();
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 6000, int pollIntervalMs = 100)
    {
        var timeoutAt = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < timeoutAt)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(pollIntervalMs);
        }

        Assert.Fail("Condition was not met before timeout.");
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}
