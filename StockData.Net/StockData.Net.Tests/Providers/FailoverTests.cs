using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using Moq;
using StockData.Net.Configuration;
using StockData.Net.Models;
using StockData.Net.Providers;

namespace StockData.Net.Tests.Providers;

[TestClass]
public class FailoverTests
{
    private Mock<IStockDataProvider> _primaryProvider = null!;
    private Mock<IStockDataProvider> _fallbackProvider1 = null!;
    private Mock<IStockDataProvider> _fallbackProvider2 = null!;
    private McpConfiguration _config = null!;
    private StockDataProviderRouter _router = null!;

    [TestInitialize]
    public void Setup()
    {
        _primaryProvider = CreateMockProvider("primary_provider", "Primary Provider");
        _fallbackProvider1 = CreateMockProvider("fallback_1", "Fallback Provider 1");
        _fallbackProvider2 = CreateMockProvider("fallback_2", "Fallback Provider 2");

        _config = new McpConfiguration
        {
            Version = "1.0",
            Providers = new List<ProviderConfiguration>
            {
                new ProviderConfiguration
                {
                    Id = "primary_provider",
                    Type = "TestProvider",
                    Enabled = true,
                    Priority = 1,
                    HealthCheck = new HealthCheckConfiguration { Enabled = false }
                },
                new ProviderConfiguration
                {
                    Id = "fallback_1",
                    Type = "TestProvider",
                    Enabled = true,
                    Priority = 2,
                    HealthCheck = new HealthCheckConfiguration { Enabled = false }
                },
                new ProviderConfiguration
                {
                    Id = "fallback_2",
                    Type = "TestProvider",
                    Enabled = true,
                    Priority = 3,
                    HealthCheck = new HealthCheckConfiguration { Enabled = false }
                }
            },
            Routing = new RoutingConfiguration
            {
                DefaultStrategy = "PrimaryWithFailover",
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["StockInfo"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "primary_provider",
                        FallbackProviderIds = new List<string> { "fallback_1", "fallback_2" },
                        TimeoutSeconds = 30
                    }
                }
            },
            CircuitBreaker = new CircuitBreakerConfiguration
            {
                Enabled = false // Disable for most tests to isolate failover logic
            }
        };

        _router = new StockDataProviderRouter(
            _config,
            new[] { _primaryProvider.Object, _fallbackProvider1.Object, _fallbackProvider2.Object });
    }

    private Mock<IStockDataProvider> CreateMockProvider(string id, string name)
    {
        var mock = new Mock<IStockDataProvider>();
        mock.Setup(p => p.ProviderId).Returns(id);
        mock.Setup(p => p.ProviderName).Returns(name);
        mock.Setup(p => p.Version).Returns("1.0.0");
        mock.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return mock;
    }

    [TestMethod]
    public async Task Failover_PrimarySucceeds_UsesOnlyPrimary()
    {
        // Arrange
        var expectedResult = "primary data";
        _primaryProvider.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetStockInfoAsync("AAPL");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _primaryProvider.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        _fallbackProvider1.Verify(p => p.GetStockInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _fallbackProvider2.Verify(p => p.GetStockInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Failover_PrimaryFails_FallbackSucceeds()
    {
        // Arrange
        var expectedResult = "fallback data";
        _primaryProvider.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Primary failed"));
        _fallbackProvider1.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetStockInfoAsync("AAPL");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _primaryProvider.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        _fallbackProvider1.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        _fallbackProvider2.Verify(p => p.GetStockInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Failover_PrimaryAndFirstFallbackFail_SecondFallbackSucceeds()
    {
        // Arrange
        var expectedResult = "second fallback data";
        _primaryProvider.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Primary failed"));
        _fallbackProvider1.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("First fallback failed"));
        _fallbackProvider2.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetStockInfoAsync("AAPL");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _primaryProvider.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        _fallbackProvider1.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        _fallbackProvider2.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Failover_AllProvidersFail_ThrowsProviderFailoverException()
    {
        // Arrange
        _primaryProvider.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Primary failed"));
        _fallbackProvider1.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Fallback 1 failed"));
        _fallbackProvider2.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Fallback 2 failed"));

        // Act & Assert
        try
        {
            await _router.GetStockInfoAsync("AAPL");
            Assert.Fail("Expected ProviderFailoverException was not thrown");
        }
        catch (ProviderFailoverException ex)
        {
            Assert.AreEqual("StockInfo", ex.DataType);
            Assert.HasCount(3, ex.ProviderErrors);
            Assert.IsTrue(ex.ProviderErrors.ContainsKey("primary_provider"));
            Assert.IsTrue(ex.ProviderErrors.ContainsKey("fallback_1"));
            Assert.IsTrue(ex.ProviderErrors.ContainsKey("fallback_2"));
            Assert.HasCount(3, ex.AttemptedProviders);
        }
    }

    [TestMethod]
    public async Task Failover_PrimaryTimeout_FallbackSucceeds()
    {
        // Arrange
        var expectedResult = "fallback data";
        _primaryProvider.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Request timed out"));
        _fallbackProvider1.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _router.GetStockInfoAsync("AAPL");

        // Assert
        Assert.AreEqual(expectedResult, result);
        _fallbackProvider1.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Failover_CompletesFastEnough()
    {
        // Arrange - Simulate failures with small delays
        _primaryProvider.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Primary failed"));
        _fallbackProvider1.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("success");

        // Act
        var startTime = DateTime.UtcNow;
        var result = await _router.GetStockInfoAsync("AAPL");
        var duration = DateTime.UtcNow - startTime;

        // Assert - Should complete well under 5 seconds (NFR-2 requirement)
        Assert.AreEqual("success", result);
        Assert.IsLessThan(5, duration.TotalSeconds, $"Failover took {duration.TotalSeconds}s, expected < 5s");
    }

    [TestMethod]
    public async Task Failover_CancellationToken_StopsFailoverChain()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        _primaryProvider.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ThrowsAsync(new Exception("Primary failed"));
        _fallbackProvider1.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("should not reach");

        // Act & Assert
        try
        {
            await _router.GetStockInfoAsync("AAPL", cts.Token);
            Assert.Fail("Expected OperationCanceledException was not thrown");
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Verify fallback was not called due to cancellation
        _fallbackProvider1.Verify(p => p.GetStockInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task Failover_MultipleDataTypes_UsesCorrectProviderChain()
    {
        // Arrange - Different routing for different data types
        _config.Routing.DataTypeRouting["HistoricalPrices"] = new DataTypeRouting
        {
            PrimaryProviderId = "fallback_1",
            FallbackProviderIds = new List<string> { "primary_provider" },
            TimeoutSeconds = 30
        };

        var router = new StockDataProviderRouter(
            _config,
            new[] { _primaryProvider.Object, _fallbackProvider1.Object, _fallbackProvider2.Object });

        _fallbackProvider1.Setup(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback_1 data");

        // Act
        var result = await router.GetHistoricalPricesAsync("AAPL", "1mo", "1d");

        // Assert
        Assert.AreEqual("fallback_1 data", result);
        _fallbackProvider1.Verify(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()), Times.Once);
        _primaryProvider.Verify(p => p.GetHistoricalPricesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
