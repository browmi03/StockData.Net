using Moq;
using StockData.Net.Configuration;
using StockData.Net.Models;
using StockData.Net.Providers;

namespace StockData.Net.Tests;

[TestClass]
public class RouterTranslationIntegrationTests
{
    private const string ProviderId = "yahoo_finance";

    [TestMethod]
    public async Task Router_CanonicalIndexName_TranslatesAndCallsProvider()
    {
        var provider = CreateProvider(ProviderId);
        provider.Setup(p => p.GetStockInfoAsync("^VIX", It.IsAny<CancellationToken>())).ReturnsAsync("ok");

        var router = CreateRouter(new[] { provider.Object }, new SymbolTranslator());

        var result = await router.GetStockInfoAsync("VIX");

        Assert.AreEqual("ok", result);
        provider.Verify(p => p.GetStockInfoAsync("^VIX", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Router_CanonicalAndYahooFormat_ProduceIdenticalProviderCalls()
    {
        var provider = CreateProvider(ProviderId);
        provider.Setup(p => p.GetStockInfoAsync("^VIX", It.IsAny<CancellationToken>())).ReturnsAsync("ok");
        var router = CreateRouter(new[] { provider.Object }, new SymbolTranslator());

        var canonical = await router.GetStockInfoAsync("VIX");
        var yahoo = await router.GetStockInfoAsync("^VIX");

        Assert.AreEqual("ok", canonical);
        Assert.AreEqual("ok", yahoo);
        provider.Verify(p => p.GetStockInfoAsync("^VIX", It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [TestMethod]
    public async Task Router_NullTranslator_PassesThroughUnchanged()
    {
        var provider = CreateProvider(ProviderId);
        provider.Setup(p => p.GetStockInfoAsync("VIX", It.IsAny<CancellationToken>())).ReturnsAsync("raw");

        var router = CreateRouter(new[] { provider.Object }, null);

        var result = await router.GetStockInfoAsync("VIX");

        Assert.AreEqual("raw", result);
        provider.Verify(p => p.GetStockInfoAsync("VIX", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task Router_TranslationHappensAfterProviderSelection_ForEachProvider()
    {
        var primary = CreateProvider("primary_provider");
        var fallback = CreateProvider("fallback_provider");
        var translator = new Mock<ISymbolTranslator>();

        translator.Setup(t => t.Translate("VIX", "primary_provider")).Returns("^VIX");
        translator.Setup(t => t.Translate("VIX", "fallback_provider")).Returns("^VIX_FB");

        primary.Setup(p => p.GetStockInfoAsync("^VIX", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("primary failed"));
        fallback.Setup(p => p.GetStockInfoAsync("^VIX_FB", It.IsAny<CancellationToken>()))
            .ReturnsAsync("fallback result");

        var router = CreateRouter(new[] { primary.Object, fallback.Object }, translator.Object, includeFallback: true);

        var result = await router.GetStockInfoAsync("VIX");

        Assert.AreEqual("fallback result", result);
        translator.Verify(t => t.Translate("VIX", "primary_provider"), Times.Once);
        translator.Verify(t => t.Translate("VIX", "fallback_provider"), Times.Once);
    }

    [TestMethod]
    public async Task Router_AllTickerMethods_TranslateInputBeforeProviderCall()
    {
        var provider = CreateProvider(ProviderId);
        var translator = new Mock<ISymbolTranslator>();
        translator.Setup(t => t.Translate("VIX", ProviderId)).Returns("^VIX");

        provider.Setup(p => p.GetHistoricalPricesAsync("^VIX", "1mo", "1d", It.IsAny<CancellationToken>())).ReturnsAsync("hist");
        provider.Setup(p => p.GetStockInfoAsync("^VIX", It.IsAny<CancellationToken>())).ReturnsAsync("info");
        provider.Setup(p => p.GetNewsAsync("^VIX", It.IsAny<CancellationToken>())).ReturnsAsync("Title: sample\nPublisher: p");
        provider.Setup(p => p.GetStockActionsAsync("^VIX", It.IsAny<CancellationToken>())).ReturnsAsync("actions");
        provider.Setup(p => p.GetFinancialStatementAsync("^VIX", FinancialStatementType.IncomeStatement, It.IsAny<CancellationToken>())).ReturnsAsync("fs");
        provider.Setup(p => p.GetHolderInfoAsync("^VIX", HolderType.MajorHolders, It.IsAny<CancellationToken>())).ReturnsAsync("holders");
        provider.Setup(p => p.GetOptionExpirationDatesAsync("^VIX", It.IsAny<CancellationToken>())).ReturnsAsync("[]");
        provider.Setup(p => p.GetOptionChainAsync("^VIX", "2026-12-18", OptionType.Calls, It.IsAny<CancellationToken>())).ReturnsAsync("[]");
        provider.Setup(p => p.GetRecommendationsAsync("^VIX", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>())).ReturnsAsync("[]");

        var router = CreateRouter(new[] { provider.Object }, translator.Object);

        await router.GetHistoricalPricesAsync("VIX", "1mo", "1d");
        await router.GetStockInfoAsync("VIX");
        await router.GetNewsAsync("VIX");
        await router.GetStockActionsAsync("VIX");
        await router.GetFinancialStatementAsync("VIX", FinancialStatementType.IncomeStatement);
        await router.GetHolderInfoAsync("VIX", HolderType.MajorHolders);
        await router.GetOptionExpirationDatesAsync("VIX");
        await router.GetOptionChainAsync("VIX", "2026-12-18", OptionType.Calls);
        await router.GetRecommendationsAsync("VIX", RecommendationType.Recommendations);

        translator.Verify(t => t.Translate("VIX", ProviderId), Times.Exactly(9));
    }

    private static StockDataProviderRouter CreateRouter(
        IStockDataProvider[] providers,
        ISymbolTranslator? translator,
        bool includeFallback = false)
    {
        var config = new McpConfiguration
        {
            Providers = providers.Select((p, index) => new ProviderConfiguration
            {
                Id = p.ProviderId,
                Type = "TestProvider",
                Enabled = true,
                Priority = index + 1,
                HealthCheck = new HealthCheckConfiguration { Enabled = false }
            }).ToList(),
            Routing = new RoutingConfiguration
            {
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["StockInfo"] = new DataTypeRouting
                    {
                        PrimaryProviderId = providers[0].ProviderId,
                        FallbackProviderIds = includeFallback && providers.Length > 1
                            ? new List<string> { providers[1].ProviderId }
                            : new List<string>()
                    },
                    ["HistoricalPrices"] = new DataTypeRouting { PrimaryProviderId = providers[0].ProviderId },
                    ["News"] = new DataTypeRouting { PrimaryProviderId = providers[0].ProviderId },
                    ["StockActions"] = new DataTypeRouting { PrimaryProviderId = providers[0].ProviderId },
                    ["FinancialStatement"] = new DataTypeRouting { PrimaryProviderId = providers[0].ProviderId },
                    ["HolderInfo"] = new DataTypeRouting { PrimaryProviderId = providers[0].ProviderId },
                    ["OptionExpirationDates"] = new DataTypeRouting { PrimaryProviderId = providers[0].ProviderId },
                    ["OptionChain"] = new DataTypeRouting { PrimaryProviderId = providers[0].ProviderId },
                    ["Recommendations"] = new DataTypeRouting { PrimaryProviderId = providers[0].ProviderId }
                }
            },
            CircuitBreaker = new CircuitBreakerConfiguration { Enabled = false }
        };

        return new StockDataProviderRouter(config, providers, null, null, translator);
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