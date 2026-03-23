using StockData.Net.Configuration;
using StockData.Net.McpServer;

namespace StockData.Net.McpServer.Tests;

[TestClass]
public class ProviderSelectionValidatorTests
{
    [TestMethod]
    public void Validate_WithKnownAlias_ReturnsResolvedProviderId()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var validator = new ProviderSelectionValidator(config, new[] { "yahoo_finance", "finnhub", "alphavantage" });

        var result = validator.Validate("alpha vantage");

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.IsExplicitSelection);
        Assert.AreEqual("alphavantage", result.ResolvedProviderId);
    }

    [TestMethod]
    public void Validate_WithAlpacaAlias_ReturnsResolvedProviderId()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        config.ProviderSelection.Aliases["alpaca"] = "alpaca";
        var validator = new ProviderSelectionValidator(config, new[] { "yahoo_finance", "finnhub", "alphavantage", "alpaca" });

        var result = validator.Validate("alpaca");

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.IsExplicitSelection);
        Assert.AreEqual("alpaca", result.ResolvedProviderId);
    }

    [TestMethod]
    public void Validate_WithAlpacaMarketsAlias_ReturnsResolvedProviderId()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        config.ProviderSelection.Aliases["alpaca markets"] = "alpaca";
        var validator = new ProviderSelectionValidator(config, new[] { "yahoo_finance", "finnhub", "alphavantage", "alpaca" });

        var result = validator.Validate("alpaca markets");

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.IsExplicitSelection);
        Assert.AreEqual("alpaca", result.ResolvedProviderId);
    }

    [TestMethod]
    public void Validate_WithFinrlAlias_ReturnsResolvedProviderId()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        config.ProviderSelection.Aliases["finrl"] = "alpaca";
        var validator = new ProviderSelectionValidator(config, new[] { "yahoo_finance", "finnhub", "alphavantage", "alpaca" });

        var result = validator.Validate("finrl");

        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.IsExplicitSelection);
        Assert.AreEqual("alpaca", result.ResolvedProviderId);
    }

    [TestMethod]
    public void Validate_WithInvalidCharacters_ReturnsInvalid()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var validator = new ProviderSelectionValidator(config, new[] { "yahoo_finance" });

        var result = validator.Validate("yahoo;drop table");

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.IsExplicitSelection);
        Assert.IsNotNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Validate_WithKnownButUnregisteredProvider_ReturnsUnavailable()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var validator = new ProviderSelectionValidator(config, new[] { "yahoo_finance" });

        var result = validator.Validate("finnhub");

        Assert.IsFalse(result.IsValid);
        Assert.IsNotNull(result.ErrorMessage);
        StringAssert.Contains(result.ErrorMessage, "not currently available");
    }

    [TestMethod]
    public void ResolveDefaultProviderForDataType_UsesConfiguredOverride()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        config.ProviderSelection.DefaultProvider["StockInfo"] = "finnhub";

        var validator = new ProviderSelectionValidator(config, new[] { "yahoo_finance", "finnhub" });

        var provider = validator.ResolveDefaultProviderForDataType("StockInfo");

        Assert.AreEqual("finnhub", provider);
    }

    [TestMethod]
    public void GetAvailableProviders_AllProviders_ReturnsList()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var validator = new ProviderSelectionValidator(config, new[] { "yahoo_finance", "alphavantage", "finnhub" });

        var providers = validator.GetAvailableProviders();

        Assert.HasCount(3, providers);
    }

    [TestMethod]
    public void GetAvailableProviders_MissingProvider_IsExcluded()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var validator = new ProviderSelectionValidator(config, new[] { "yahoo_finance", "alphavantage" });

        var providers = validator.GetAvailableProviders();

        Assert.HasCount(2, providers);
        Assert.IsFalse(providers.Any(provider => string.Equals(provider.Id, "finnhub", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void GetAvailableProviders_NoProviders_ReturnsEmpty()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var validator = new ProviderSelectionValidator(config, Array.Empty<string>());

        var providers = validator.GetAvailableProviders();

        Assert.IsNotNull(providers);
        Assert.IsEmpty(providers);
    }

    [TestMethod]
    public void GetAvailableProviders_WhenFinnhubTierIsFree_ExcludesHistoricalPricesAndStockActions()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var finnhub = config.Providers.First(p => string.Equals(p.Id, "finnhub", StringComparison.OrdinalIgnoreCase));
        finnhub.Tier = "free";

        var validator = new ProviderSelectionValidator(config, new[] { "finnhub" });

        var providers = validator.GetAvailableProviders();
        var listedFinnhub = providers.Single(p => string.Equals(p.Id, "finnhub", StringComparison.OrdinalIgnoreCase));

        CollectionAssert.DoesNotContain(listedFinnhub.SupportedDataTypes, "historical_prices");
        CollectionAssert.DoesNotContain(listedFinnhub.SupportedDataTypes, "stock_actions");
    }

    [TestMethod]
    public void GetAvailableProviders_WhenFinnhubTierIsPaid_IncludesHistoricalPricesAndStockActions()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var finnhub = config.Providers.First(p => string.Equals(p.Id, "finnhub", StringComparison.OrdinalIgnoreCase));
        finnhub.Tier = "paid";

        var validator = new ProviderSelectionValidator(config, new[] { "finnhub" });

        var providers = validator.GetAvailableProviders();
        var listedFinnhub = providers.Single(p => string.Equals(p.Id, "finnhub", StringComparison.OrdinalIgnoreCase));

        CollectionAssert.Contains(listedFinnhub.SupportedDataTypes, "historical_prices");
        CollectionAssert.Contains(listedFinnhub.SupportedDataTypes, "stock_actions");
    }
}
