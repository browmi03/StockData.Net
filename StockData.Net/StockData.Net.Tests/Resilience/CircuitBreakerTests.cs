using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using StockData.Net.Configuration;
using StockData.Net.Resilience;

namespace StockData.Net.Tests.Resilience;

[TestClass]
public class CircuitBreakerTests
{
    private CircuitBreakerConfiguration _config = null!;
    private CircuitBreaker _circuitBreaker = null!;
    private StubLogger<CircuitBreaker> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _config = new CircuitBreakerConfiguration
        {
            Enabled = true,
            FailureThreshold = 3,
            HalfOpenAfterSeconds = 2,
            TimeoutSeconds = 5
        };
        _logger = new StubLogger<CircuitBreaker>();
        _circuitBreaker = new CircuitBreaker("test_provider", _config, _logger);
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenOperationSucceeds_ReturnsResult()
    {
        // Arrange
        var expectedResult = "success";

        // Act
        var result = await _circuitBreaker.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return expectedResult;
        });

        // Assert
        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(CircuitBreakerState.Closed, _circuitBreaker.GetState());
    }

    [TestMethod]
    public async Task ExecuteAsync_AfterMultipleFailures_OpensCircuit()
    {
        // Act & Assert - Execute failures up to threshold
        for (int i = 0; i < _config.FailureThreshold; i++)
        {
            try
            {
                await _circuitBreaker.ExecuteAsync<string>(ct => throw new Exception($"Failure {i + 1}"));
                Assert.Fail("Expected exception was not thrown");
            }
            catch (Exception ex) when (ex is not CircuitBreakerOpenException)
            {
                // Expected failure
            }
        }

        // Verify circuit is now open
        Assert.AreEqual(CircuitBreakerState.Open, _circuitBreaker.GetState());

        // Next attempt should throw CircuitBreakerOpenException
        try
        {
            await _circuitBreaker.ExecuteAsync<string>(ct => Task.FromResult("should not execute"));
            Assert.Fail("Expected CircuitBreakerOpenException was not thrown");
        }
        catch (CircuitBreakerOpenException ex)
        {
            Assert.AreEqual("test_provider", ex.ProviderId);
        }
    }

    [TestMethod]
    public async Task ExecuteAsync_InHalfOpenState_AllowsOneTestRequest()
    {
        // Arrange - Open the circuit
        for (int i = 0; i < _config.FailureThreshold; i++)
        {
            try
            {
                await _circuitBreaker.ExecuteAsync<string>(ct => throw new Exception($"Failure {i + 1}"));
            }
            catch (Exception ex) when (ex is not CircuitBreakerOpenException) { }
        }

        // Wait for half-open transition
        await Task.Delay(TimeSpan.FromSeconds(_config.HalfOpenAfterSeconds + 0.5));

        // Act - Test request should succeed
        var result = await _circuitBreaker.ExecuteAsync(ct => Task.FromResult("success"));

        // Assert
        Assert.AreEqual("success", result);
        Assert.AreEqual(CircuitBreakerState.Closed, _circuitBreaker.GetState());
    }

    [TestMethod]
    public async Task ExecuteAsync_InHalfOpenState_WhenTestFails_ReopensCircuit()
    {
        // Arrange - Open the circuit
        for (int i = 0; i < _config.FailureThreshold; i++)
        {
            try
            {
                await _circuitBreaker.ExecuteAsync<string>(ct => throw new Exception($"Failure {i + 1}"));
            }
            catch (Exception ex) when (ex is not CircuitBreakerOpenException) { }
        }

        // Wait for half-open transition
        await Task.Delay(TimeSpan.FromSeconds(_config.HalfOpenAfterSeconds + 0.5));

        // Act - Test request fails
        try
        {
            await _circuitBreaker.ExecuteAsync<string>(ct => throw new Exception("Test failure"));
            Assert.Fail("Expected exception was not thrown");
        }
        catch (Exception ex) when (ex is not CircuitBreakerOpenException)
        {
            // Expected
        }

        // Assert - Circuit should be open again
        Assert.AreEqual(CircuitBreakerState.Open, _circuitBreaker.GetState());
    }

    [TestMethod]
    public async Task ExecuteAsync_InHalfOpenState_BlocksConcurrentRequests()
    {
        // Arrange - Open the circuit
        for (int i = 0; i < _config.FailureThreshold; i++)
        {
            try
            {
                await _circuitBreaker.ExecuteAsync<string>(ct => throw new Exception($"Failure {i + 1}"));
            }
            catch (Exception ex) when (ex is not CircuitBreakerOpenException) { }
        }

        // Wait for half-open transition
        await Task.Delay(TimeSpan.FromSeconds(_config.HalfOpenAfterSeconds + 0.5));

        // Act - Start a test request that takes time
        var testRequestTask = _circuitBreaker.ExecuteAsync(async ct =>
        {
            await Task.Delay(500, ct);
            return "success";
        });

        // Try another request while first is in progress
        await Task.Delay(50); // Let first request start

        try
        {
            await _circuitBreaker.ExecuteAsync<string>(ct => Task.FromResult("should block"));
            Assert.Fail("Expected CircuitBreakerOpenException was not thrown");
        }
        catch (CircuitBreakerOpenException)
        {
            // Expected - concurrent requests should be blocked
        }

        // Wait for first request to complete
        await testRequestTask;
    }

    [TestMethod]
    public async Task ExecuteAsync_WithTimeout_ThrowsAfterTimeout()
    {
        // Arrange
        var slowConfig = new CircuitBreakerConfiguration
        {
            Enabled = true,
            FailureThreshold = 3,
            HalfOpenAfterSeconds = 60,
            TimeoutSeconds = 1
        };
        var cb = new CircuitBreaker("test", slowConfig, _logger);

        // Act & Assert
        try
        {
            await cb.ExecuteAsync(async ct =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return "should timeout";
            });
            Assert.Fail("Expected OperationCanceledException was not thrown");
        }
        catch (OperationCanceledException)
        {
            // Expected timeout
        }
    }

    [TestMethod]
    public async Task GetMetrics_ReturnsAccurateMetrics()
    {
        // Arrange & Act
        await _circuitBreaker.ExecuteAsync(ct => Task.FromResult("success"));
        
        try
        {
            await _circuitBreaker.ExecuteAsync<string>(ct => throw new Exception("failure"));
        }
        catch { }

        // Assert
        var metrics = _circuitBreaker.GetMetrics();
        Assert.IsNotNull(metrics);
        Assert.AreEqual(CircuitBreakerState.Closed, metrics.State);
        Assert.AreEqual(1, metrics.ConsecutiveFailures);
        Assert.AreEqual(1, metrics.SuccessCount);
        Assert.AreEqual(1, metrics.FailureCount);
    }

    [TestMethod]
    public async Task Reset_ClearsCircuitState()
    {
        // Arrange - Open the circuit
        for (int i = 0; i < _config.FailureThreshold; i++)
        {
            try
            {
                await _circuitBreaker.ExecuteAsync<string>(ct => throw new Exception($"Failure {i + 1}"));
            }
            catch (Exception ex) when (ex is not CircuitBreakerOpenException) { }
        }

        Assert.AreEqual(CircuitBreakerState.Open, _circuitBreaker.GetState());

        // Act
        _circuitBreaker.Reset();

        // Assert
        Assert.AreEqual(CircuitBreakerState.Closed, _circuitBreaker.GetState());
        var metrics = _circuitBreaker.GetMetrics();
        Assert.AreEqual(0, metrics.ConsecutiveFailures);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithDisabledCircuitBreaker_AlwaysExecutes()
    {
        // Arrange
        var disabledConfig = new CircuitBreakerConfiguration
        {
            Enabled = false,
            FailureThreshold = 3,
            HalfOpenAfterSeconds = 60,
            TimeoutSeconds = 30
        };
        var cb = new CircuitBreaker("test", disabledConfig, _logger);

        // Act - Execute many failures
        for (int i = 0; i < 10; i++)
        {
            try
            {
                await cb.ExecuteAsync<string>(ct => throw new Exception($"Failure {i + 1}"));
            }
            catch (Exception ex) when (ex is not CircuitBreakerOpenException)
            {
                // Expected failures, but circuit should never open
            }
        }

        // Assert - Should still be able to execute
        var result = await cb.ExecuteAsync(ct => Task.FromResult("success"));
        Assert.AreEqual("success", result);
    }

    [TestMethod]
    [DataRow(5, 60)]
    [DataRow(3, 30)]
    [DataRow(10, 120)]
    public async Task ExecuteAsync_RespectsConfigurationThresholds(int failureThreshold, int halfOpenSeconds)
    {
        // Arrange
        var customConfig = new CircuitBreakerConfiguration
        {
            Enabled = true,
            FailureThreshold = failureThreshold,
            HalfOpenAfterSeconds = halfOpenSeconds,
            TimeoutSeconds = 30
        };
        var cb = new CircuitBreaker("test", customConfig, _logger);

        // Act - Execute failures up to threshold - 1
        for (int i = 0; i < failureThreshold - 1; i++)
        {
            try
            {
                await cb.ExecuteAsync<string>(ct => throw new Exception($"Failure {i + 1}"));
            }
            catch (Exception ex) when (ex is not CircuitBreakerOpenException) { }
        }

        // Assert - Should still be closed
        Assert.AreEqual(CircuitBreakerState.Closed, cb.GetState());

        // Act - One more failure should open it
        try
        {
            await cb.ExecuteAsync<string>(ct => throw new Exception("Final failure"));
        }
        catch (Exception ex) when (ex is not CircuitBreakerOpenException) { }

        // Assert - Now it should be open
        Assert.AreEqual(CircuitBreakerState.Open, cb.GetState());
    }

    // Stub logger for testing
    private class StubLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
