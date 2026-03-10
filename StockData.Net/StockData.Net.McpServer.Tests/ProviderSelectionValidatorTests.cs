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
        Assert.Contains("not currently available", result.ErrorMessage);
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
}
