using Moq;
using StockData.Net.Configuration;
using StockData.Net.Providers;

namespace StockData.Net.Tests.Providers;

[TestClass]
public class Adr002InvalidRequestTests
{
    [TestMethod]
    public async Task ClassifyError_ArgumentException_BehavesAsInvalidRequest()
    {
        var (router, primary, fallback) = CreateStockInfoRouter();
        var exception = new ArgumentException("ticker is invalid");

        primary.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>())).ThrowsAsync(exception);

        ArgumentException thrown;
        try
        {
            await router.GetStockInfoAsync("AAPL");
            Assert.Fail("Expected ArgumentException was not thrown");
            return;
        }
        catch (ArgumentException ex)
        {
            thrown = ex;
        }

        Assert.AreSame(exception, thrown);
        fallback.Verify(p => p.GetStockInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteWithFailover_InvalidRequest_DoesNotContinueFailover()
    {
        var (router, primary, fallback) = CreateStockInfoRouter();

        primary.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("invalid ticker"));

        try
        {
            await router.GetStockInfoAsync("AAPL");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }

        primary.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        fallback.Verify(p => p.GetStockInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteWithFailover_InvalidRequest_DoesNotRecordHealthFailure()
    {
        var (router, primary, _) = CreateStockInfoRouter();
        primary.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("invalid ticker"));

        try
        {
            await router.GetStockInfoAsync("AAPL");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }

        var health = router.GetDetailedHealthStatus();
        Assert.IsTrue(health.TryGetValue("primary_provider", out var primaryHealth));
        Assert.AreEqual(0, primaryHealth.FailedRequests);
        Assert.IsFalse(primaryHealth.ErrorTypeBreakdown.ContainsKey(ProviderErrorType.InvalidRequest));
    }

    [TestMethod]
    public async Task ExecuteWithFailover_InvalidRequest_RethrowsOriginalException()
    {
        var (router, primary, _) = CreateStockInfoRouter();
        var exception = new ArgumentException("original invalid request message");
        primary.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>())).ThrowsAsync(exception);

        ArgumentException thrown;
        try
        {
            await router.GetStockInfoAsync("AAPL");
            Assert.Fail("Expected ArgumentException was not thrown");
            return;
        }
        catch (ArgumentException ex)
        {
            thrown = ex;
        }

        Assert.AreSame(exception, thrown);
        Assert.AreEqual("original invalid request message", thrown.Message);
    }

    [TestMethod]
    public async Task ExecuteWithAggregation_InvalidRequest_IsTerminal()
    {
        var (router, primary, fallback) = CreateAggregatedNewsRouter();

        primary.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("invalid news symbol"));
        fallback.Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("Title: fallback\nPublisher: provider");

        try
        {
            await router.GetNewsAsync("AAPL");
            Assert.Fail("Expected ArgumentException was not thrown");
        }
        catch (ArgumentException)
        {
            // Expected
        }

        var health = router.GetDetailedHealthStatus();
        Assert.IsTrue(health.TryGetValue("primary_provider", out var primaryHealth));
        Assert.AreEqual(0, primaryHealth.FailedRequests);
    }

    [TestMethod]
    public async Task ClassifyError_HttpRequestException_StillTriggersFailover()
    {
        var (router, primary, fallback) = CreateStockInfoRouter();

        primary.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("network", null));
        fallback.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback success");

        var result = await router.GetStockInfoAsync("AAPL");

        Assert.AreEqual("fallback success", result);
        fallback.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);

        var health = router.GetDetailedHealthStatus();
        Assert.IsTrue(health.TryGetValue("primary_provider", out var primaryHealth));
        Assert.IsTrue(primaryHealth.ErrorTypeBreakdown.TryGetValue(ProviderErrorType.NetworkError, out var count));
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public async Task ExecuteWithFailover_NetworkError_StillContinuesFailover()
    {
        var (router, primary, fallback) = CreateStockInfoRouter();

        primary.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("timeout"));
        fallback.Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback after timeout");

        var result = await router.GetStockInfoAsync("AAPL");

        Assert.AreEqual("fallback after timeout", result);
        primary.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        fallback.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static (StockDataProviderRouter Router, Mock<IStockDataProvider> Primary, Mock<IStockDataProvider> Fallback)
        CreateStockInfoRouter()
    {
        var primary = CreateProvider("primary_provider");
        var fallback = CreateProvider("fallback_provider");

        var config = new McpConfiguration
        {
            Providers =
            [
                new ProviderConfiguration { Id = "primary_provider", Type = "Test", Enabled = true, Priority = 1, HealthCheck = new HealthCheckConfiguration { Enabled = false } },
                new ProviderConfiguration { Id = "fallback_provider", Type = "Test", Enabled = true, Priority = 2, HealthCheck = new HealthCheckConfiguration { Enabled = false } }
            ],
            Routing = new RoutingConfiguration
            {
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["StockInfo"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "primary_provider",
                        FallbackProviderIds = new List<string> { "fallback_provider" }
                    }
                }
            },
            CircuitBreaker = new CircuitBreakerConfiguration { Enabled = false }
        };

        return (new StockDataProviderRouter(config, new[] { primary.Object, fallback.Object }), primary, fallback);
    }

    private static (StockDataProviderRouter Router, Mock<IStockDataProvider> Primary, Mock<IStockDataProvider> Fallback)
        CreateAggregatedNewsRouter()
    {
        var primary = CreateProvider("primary_provider");
        var fallback = CreateProvider("fallback_provider");

        var config = new McpConfiguration
        {
            Providers =
            [
                new ProviderConfiguration { Id = "primary_provider", Type = "Test", Enabled = true, Priority = 1, HealthCheck = new HealthCheckConfiguration { Enabled = false } },
                new ProviderConfiguration { Id = "fallback_provider", Type = "Test", Enabled = true, Priority = 2, HealthCheck = new HealthCheckConfiguration { Enabled = false } }
            ],
            Routing = new RoutingConfiguration
            {
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["News"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "primary_provider",
                        FallbackProviderIds = new List<string> { "fallback_provider" },
                        AggregateResults = true
                    }
                }
            },
            NewsDeduplication = new NewsDeduplicationConfiguration
            {
                Enabled = false
            },
            CircuitBreaker = new CircuitBreakerConfiguration { Enabled = false }
        };

        return (new StockDataProviderRouter(config, new[] { primary.Object, fallback.Object }), primary, fallback);
    }

    private static Mock<IStockDataProvider> CreateProvider(string providerId)
    {
        var provider = new Mock<IStockDataProvider>();
        provider.Setup(p => p.ProviderId).Returns(providerId);
        provider.Setup(p => p.ProviderName).Returns(providerId);
        provider.Setup(p => p.Version).Returns("1.0.0");
        provider.Setup(p => p.GetHealthStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        return provider;
    }
}