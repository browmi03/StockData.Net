using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using StockData.Net;
using StockData.Net.Configuration;
using StockData.Net.Models;
using StockData.Net.McpServer;
using StockData.Net.McpServer.Models;
using StockData.Net.Providers;

namespace StockData.Net.McpServer.Tests;

[TestClass]
public class StockDataMcpServerTests
{
    private Mock<IYahooFinanceClient> _mockClient = null!;
    private Mock<IStockDataProvider> _mockProvider = null!;
    private StockDataProviderRouter _router = null!;
    private StockDataMcpServer _server = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockClient = new Mock<IYahooFinanceClient>();
        _mockProvider = new Mock<IStockDataProvider>();
        _mockProvider.Setup(p => p.ProviderId).Returns("yahoo_finance");
        _mockProvider.Setup(p => p.ProviderName).Returns("Yahoo Finance");
        _mockProvider.Setup(p => p.Version).Returns("1.0.0");
        _mockProvider.Setup(p => p.GetSupportedDataTypes(It.IsAny<string>())).Returns(new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "historical_prices",
            "stock_info",
            "news",
            "market_news",
            "stock_actions",
            "financial_statement",
            "holder_info",
            "option_expiration_dates",
            "option_chain",
            "recommendations"
        });
        
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        _router = new StockDataProviderRouter(config, new[] { _mockProvider.Object });
        _server = new StockDataMcpServer(_router);
    }

    [TestMethod]
    public void Constructor_WithValidRouter_CreatesInstance()
    {
        // Arrange & Act
        var server = new StockDataMcpServer(_router);

        // Assert
        Assert.IsNotNull(server);
    }

    #region HandleRequestAsync Tests

    [TestMethod]
    public async Task HandleRequestAsync_Initialize_ReturnsCorrectProtocolVersion()
    {
        // Arrange
        var request = new McpRequest
        {
            Id = 1,
            Method = "initialize"
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual(1, response.Id);
        Assert.IsNull(response.Error);
        Assert.IsNotNull(response.Result);
        
        var resultJson = JsonSerializer.Serialize(response.Result);
        // Protocol version should match the MCP specification date format "YYYY-MM-DD"
        StringAssert.Contains(resultJson, "protocolVersion");
        StringAssert.Contains(resultJson, "StockData-mcp");
    }

    [TestMethod]
    public async Task HandleRequestAsync_ToolsList_ReturnsAll11Tools()
    {
        // Arrange
        var request = new McpRequest
        {
            Id = 2,
            Method = "tools/list"
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual(2, response.Id);
        Assert.IsNull(response.Error);
        Assert.IsNotNull(response.Result);
        
        var resultJson = JsonSerializer.Serialize(response.Result);
        StringAssert.Contains(resultJson, "get_historical_stock_prices");
        StringAssert.Contains(resultJson, "get_stock_info");
        StringAssert.Contains(resultJson, "get_finance_news");
        StringAssert.Contains(resultJson, "get_market_news");
        StringAssert.Contains(resultJson, "get_stock_actions");
        StringAssert.Contains(resultJson, "get_financial_statement");
        StringAssert.Contains(resultJson, "get_holder_info");
        StringAssert.Contains(resultJson, "get_option_expiration_dates");
        StringAssert.Contains(resultJson, "get_option_chain");
        StringAssert.Contains(resultJson, "get_recommendations");
        StringAssert.Contains(resultJson, "list_providers");
        Assert.DoesNotContain("get_yahoo_finance_news", resultJson);
    }

    [TestMethod]
    public async Task ListProviders_ToolRegistered()
    {
        var request = new McpRequest
        {
            Id = 203,
            Method = "tools/list"
        };

        var response = await InvokeHandleRequestAsync(request);

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        Assert.IsNotNull(response.Result);

        var resultJson = JsonSerializer.Serialize(response.Result);
        StringAssert.Contains(resultJson, "list_providers");
    }

    [TestMethod]
    public async Task ListProviders_AllProviders_ReturnsThree()
    {
        var server = CreateServerWithProviders("yahoo_finance", "alphavantage", "finnhub");
        var response = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);
        var providers = ParseProvidersFromToolResponse(response);

        Assert.AreEqual(3, providers.GetArrayLength());
    }

    [TestMethod]
    public async Task ListProviders_PartialProviders_ReturnsTwoProviders()
    {
        var server = CreateServerWithProviders("yahoo_finance", "alphavantage");
        var response = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);
        var providers = ParseProvidersFromToolResponse(response).EnumerateArray().ToList();

        Assert.HasCount(2, providers);

        var providerIds = providers
            .Select(provider => provider.GetProperty("id").GetString())
            .Where(providerId => providerId != null)
            .Cast<string>()
            .ToArray();

        CollectionAssert.DoesNotContain(providerIds, "finnhub");
    }

    [TestMethod]
    public async Task ListProviders_NoProviders_ReturnsEmptyArray()
    {
        var server = CreateServerWithProviders(Array.Empty<string>());
        var response = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);
        var providers = ParseProvidersFromToolResponse(response).EnumerateArray().ToList();

        Assert.HasCount(0, providers);

        var textContent = ExtractTextContent(response);
        StringAssert.Contains(textContent, "\"providers\":[]");
    }

    [TestMethod]
    public async Task ListProviders_ToolDescription_IsCorrect()
    {
        var response = await InvokeHandleRequestAsync(new McpRequest
        {
            Id = 204,
            Method = "tools/list"
        });

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        Assert.IsNotNull(response.Result);

        using var toolsDocument = JsonDocument.Parse(JsonSerializer.Serialize(response.Result));
        var tools = toolsDocument.RootElement.GetProperty("tools").EnumerateArray().ToList();
        var listProvidersTool = tools.FirstOrDefault(tool => string.Equals(tool.GetProperty("name").GetString(), "list_providers", StringComparison.Ordinal));

        Assert.AreNotEqual(JsonValueKind.Undefined, listProvidersTool.ValueKind);

        var description = listProvidersTool.GetProperty("description").GetString() ?? string.Empty;
        StringAssert.Contains(description, "stock data providers currently available");
    }

    [TestMethod]
    public async Task ListProviders_InputSchema_HasNoProperties()
    {
        var response = await InvokeHandleRequestAsync(new McpRequest
        {
            Id = 205,
            Method = "tools/list"
        });

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        Assert.IsNotNull(response.Result);

        using var toolsDocument = JsonDocument.Parse(JsonSerializer.Serialize(response.Result));
        var tools = toolsDocument.RootElement.GetProperty("tools").EnumerateArray().ToList();
        var listProvidersTool = tools.FirstOrDefault(tool => string.Equals(tool.GetProperty("name").GetString(), "list_providers", StringComparison.Ordinal));

        Assert.AreNotEqual(JsonValueKind.Undefined, listProvidersTool.ValueKind);

        var inputSchema = listProvidersTool.GetProperty("inputSchema");
        Assert.AreEqual("object", inputSchema.GetProperty("type").GetString());

        var properties = inputSchema.GetProperty("properties").EnumerateObject().ToList();
        Assert.HasCount(0, properties);
    }

    [TestMethod]
    public async Task ListProviders_OnlyYahooHasOptionChain()
    {
        var server = CreateServerWithProviders("yahoo_finance", "alphavantage", "finnhub");
        var response = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);
        var providers = ParseProvidersFromToolResponse(response);

        var yahoo = FindProviderById(providers, "yahoo");
        var alphaVantage = FindProviderById(providers, "alphavantage");
        var finnhub = FindProviderById(providers, "finnhub");

        var yahooTypes = yahoo.GetProperty("supportedDataTypes").EnumerateArray().Select(item => item.GetString()).Where(value => value != null).Cast<string>().ToArray();
        var alphaVantageTypes = alphaVantage.GetProperty("supportedDataTypes").EnumerateArray().Select(item => item.GetString()).Where(value => value != null).Cast<string>().ToArray();
        var finnhubTypes = finnhub.GetProperty("supportedDataTypes").EnumerateArray().Select(item => item.GetString()).Where(value => value != null).Cast<string>().ToArray();

        CollectionAssert.Contains(yahooTypes, "option_chain");
        CollectionAssert.DoesNotContain(alphaVantageTypes, "option_chain");
        CollectionAssert.DoesNotContain(finnhubTypes, "option_chain");
    }

    [TestMethod]
    public async Task ListProviders_YfinanceAlias_AcceptedByValidator()
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var validator = new ProviderSelectionValidator(config, new[] { "yahoo_finance", "alphavantage", "finnhub" });
        var server = CreateServerWithProviders("yahoo_finance", "alphavantage", "finnhub");
        var response = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);
        var providers = ParseProvidersFromToolResponse(response);
        var yahoo = FindProviderById(providers, "yahoo");

        var aliases = yahoo.GetProperty("aliases").EnumerateArray().Select(alias => alias.GetString()).Where(alias => alias != null).Cast<string>().ToArray();
        CollectionAssert.Contains(aliases, "yfinance");

        var validation = validator.Validate("yfinance");
        Assert.IsTrue(validation.IsValid);
        Assert.IsNull(validation.ErrorMessage);
    }

    [TestMethod]
    public async Task ListProviders_Idempotent_ReturnsSameResults()
    {
        var server = CreateServerWithProviders("yahoo_finance", "alphavantage", "finnhub");

        var firstResponse = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);
        var secondResponse = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);

        var firstResult = ExtractTextContent(firstResponse);
        var secondResult = ExtractTextContent(secondResponse);

        Assert.AreEqual(firstResult, secondResult);
    }

    [TestMethod]
    public async Task ListProviders_OtherToolDescriptions_ReferenceListProviders()
    {
        var response = await InvokeHandleRequestAsync(new McpRequest
        {
            Id = 206,
            Method = "tools/list"
        });

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        Assert.IsNotNull(response.Result);

        using var toolsDocument = JsonDocument.Parse(JsonSerializer.Serialize(response.Result));
        var tools = toolsDocument.RootElement.GetProperty("tools").EnumerateArray().ToList();

        foreach (var tool in tools)
        {
            var inputSchema = tool.GetProperty("inputSchema");
            if (!inputSchema.TryGetProperty("properties", out var properties)
                || !properties.TryGetProperty("provider", out _))
            {
                continue;
            }

            var description = tool.GetProperty("description").GetString() ?? string.Empty;
            StringAssert.Contains(description, "list_providers");
        }
    }

    [TestMethod]
    public async Task ListProviders_YahooAliasesIncludeYfinance()
    {
        var server = CreateServerWithProviders("yahoo_finance", "alphavantage", "finnhub");
        var response = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);
        var providers = ParseProvidersFromToolResponse(response);
        var yahoo = FindProviderById(providers, "yahoo");

        Assert.AreNotEqual(JsonValueKind.Undefined, yahoo.ValueKind);

        var aliases = yahoo.GetProperty("aliases").EnumerateArray().Select(alias => alias.GetString()).Where(alias => alias != null).Cast<string>().ToArray();
        CollectionAssert.Contains(aliases, "yahoo");
        CollectionAssert.Contains(aliases, "yfinance");
    }

    [TestMethod]
    public async Task ListProviders_YahooHasTenSupportedDataTypes()
    {
        var expectedTypes = new[]
        {
            "historical_prices",
            "stock_info",
            "news",
            "market_news",
            "stock_actions",
            "financial_statement",
            "holder_info",
            "option_expiration_dates",
            "option_chain",
            "recommendations"
        };

        var server = CreateServerWithProviders("yahoo_finance", "alphavantage", "finnhub");
        var response = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);
        var providers = ParseProvidersFromToolResponse(response);
        var yahoo = FindProviderById(providers, "yahoo");

        var supportedDataTypes = yahoo.GetProperty("supportedDataTypes").EnumerateArray().Select(item => item.GetString()).Where(value => value != null).Cast<string>().ToArray();
        CollectionAssert.AreEquivalent(expectedTypes, supportedDataTypes);
    }

    [TestMethod]
    public async Task ListProviders_AlphaVantageMetadata_IsCorrect()
    {
        var server = CreateServerWithProviders("yahoo_finance", "alphavantage", "finnhub");
        var response = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);
        var providers = ParseProvidersFromToolResponse(response);
        var alphaVantage = FindProviderById(providers, "alphavantage");

        Assert.AreEqual("Alpha Vantage", alphaVantage.GetProperty("displayName").GetString());

        var aliases = alphaVantage.GetProperty("aliases").EnumerateArray().Select(alias => alias.GetString()).Where(alias => alias != null).Cast<string>().ToArray();
        CollectionAssert.Contains(aliases, "alphavantage");
        CollectionAssert.Contains(aliases, "alpha_vantage");

        var supportedDataTypes = alphaVantage.GetProperty("supportedDataTypes").EnumerateArray().Select(item => item.GetString()).Where(value => value != null).Cast<string>().ToArray();
        CollectionAssert.AreEquivalent(new[] { "historical_prices", "stock_info", "news", "market_news", "stock_actions" }, supportedDataTypes);
    }

    [TestMethod]
    public async Task ListProviders_FinnhubMetadata_IsCorrect()
    {
        var server = CreateServerWithProviders("yahoo_finance", "alphavantage", "finnhub");
        var response = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);
        var providers = ParseProvidersFromToolResponse(response);
        var finnhub = FindProviderById(providers, "finnhub");

        Assert.AreEqual("Finnhub", finnhub.GetProperty("displayName").GetString());

        var aliases = finnhub.GetProperty("aliases").EnumerateArray().Select(alias => alias.GetString()).Where(alias => alias != null).Cast<string>().ToArray();
        CollectionAssert.Contains(aliases, "finnhub");

        var supportedDataTypes = finnhub.GetProperty("supportedDataTypes").EnumerateArray().Select(item => item.GetString()).Where(value => value != null).Cast<string>().ToArray();
        CollectionAssert.AreEquivalent(new[] { "stock_info", "news", "market_news", "recommendations" }, supportedDataTypes);
    }

    [TestMethod]
    public async Task ListProviders_ZeroArgumentCall_DoesNotThrow()
    {
        var server = CreateServerWithProviders("yahoo_finance", "alphavantage", "finnhub");
        var response = await InvokeListProvidersAsync(server, includeArgumentsProperty: false);

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        Assert.IsNotNull(response.Result);
    }

    [TestMethod]
    public async Task ListProviders_ResponseIsValidJson()
    {
        var server = CreateServerWithProviders("yahoo_finance", "alphavantage", "finnhub");
        var response = await InvokeListProvidersAsync(server, includeArgumentsProperty: true);

        var textContent = ExtractTextContent(response);
        using var payload = JsonDocument.Parse(textContent);

        Assert.IsTrue(payload.RootElement.TryGetProperty("providers", out var providers));
        Assert.AreEqual(JsonValueKind.Array, providers.ValueKind);
    }

    [TestMethod]
    public async Task HandleRequestAsync_ToolsList_GetFinanceNewsDescription_IsNotYahooExclusive()
    {
        var request = new McpRequest
        {
            Id = 201,
            Method = "tools/list"
        };

        var response = await InvokeHandleRequestAsync(request);

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        Assert.IsNotNull(response.Result);

        var resultJson = JsonSerializer.Serialize(response.Result);
        var description = ExtractToolDescription(resultJson, "get_finance_news");

        Assert.IsFalse(string.IsNullOrWhiteSpace(description));
        Assert.IsFalse(description.Contains("Yahoo Finance", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task HandleRequestAsync_ToolsList_IncludesProviderParameter()
    {
        var request = new McpRequest
        {
            Id = 202,
            Method = "tools/list"
        };

        var response = await InvokeHandleRequestAsync(request);

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);

        var resultJson = JsonSerializer.Serialize(response.Result);
        StringAssert.Contains(resultJson, "\"provider\"");
        StringAssert.Contains(resultJson, "alphavantage");
        StringAssert.Contains(resultJson, "finnhub");
        StringAssert.Contains(resultJson, "yahoo");
    }

    [TestMethod]
    public async Task HandleRequestAsync_UnknownMethod_ReturnsError()
    {
        // Arrange
        var request = new McpRequest
        {
            Id = 3,
            Method = "unknown_method"
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.AreEqual(3, response.Id);
        Assert.IsNotNull(response.Error);
        Assert.AreEqual(-32603, response.Error.Code);
        StringAssert.Contains(response.Error.Message, "Unknown method");
    }

    #endregion

    #region HandleToolCallAsync Tests

    [TestMethod]
    public async Task HandleToolCallAsync_GetHistoricalStockPrices_Success()
    {
        // Arrange
        var testDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
        var testData = $"[{{\"Date\":\"{testDate}\",\"Open\":150.0}}]";
        _mockProvider
            .Setup(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_historical_stock_prices"",
            ""arguments"": {
                ""ticker"": ""AAPL"",
                ""period"": ""1mo"",
                ""interval"": ""1d""
            }
        }");

        var request = new McpRequest
        {
            Id = 10,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        var resultJson = JsonSerializer.Serialize(response.Result);
        // The response is wrapped in {content: [{type: "text", text: "..."}]}
        StringAssert.Contains(resultJson, "Date");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetStockInfo_Success()
    {
        // Arrange
        var testData = "{\"symbol\":\"MSFT\",\"price\":380.0}";
        _mockProvider
            .Setup(p => p.GetStockInfoAsync("MSFT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_stock_info"",
            ""arguments"": {
                ""ticker"": ""MSFT""
            }
        }");

        var request = new McpRequest
        {
            Id = 11,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetStockInfoAsync("MSFT", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetStockInfo_WithExplicitProvider_ReturnsMetadata()
    {
        _mockProvider
            .Setup(p => p.GetStockInfoAsync("MSFT", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"symbol\":\"MSFT\",\"price\":380.0}");

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_stock_info"",
            ""arguments"": {
                ""ticker"": ""MSFT"",
                ""provider"": ""yahoo""
            }
        }");

        var request = new McpRequest
        {
            Id = 1121,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        var response = await InvokeHandleRequestAsync(request);

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);

        var resultJson = JsonSerializer.Serialize(response.Result);
        StringAssert.Contains(resultJson, "serviceKey");
        StringAssert.Contains(resultJson, "yahoo");
        StringAssert.Contains(resultJson, "tier");
        StringAssert.Contains(resultJson, "free");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetStockInfo_WithoutProvider_UsesConfiguredDefaultProvider()
    {
        var yahoo = CreateProviderMock("yahoo_finance");
        var finnhub = CreateProviderMock("finnhub");

        yahoo
            .Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"symbol\":\"AAPL\",\"provider\":\"yahoo\"}");
        finnhub
            .Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"symbol\":\"AAPL\",\"provider\":\"finnhub\"}");

        var config = new ConfigurationLoader().GetDefaultConfiguration();
        config.ProviderSelection.DefaultProvider["StockInfo"] = "finnhub";

        var router = new StockDataProviderRouter(config, new[] { yahoo.Object, finnhub.Object });
        var server = new StockDataMcpServer(router, config);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_stock_info"",
            ""arguments"": {
                ""ticker"": ""AAPL""
            }
        }");

        var response = await server.HandleRequestAsync(new McpRequest
        {
            Id = 1122,
            Method = "tools/call",
            Params = paramsJson.RootElement
        }, CancellationToken.None);

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);

        var resultJson = JsonSerializer.Serialize(response.Result);
        StringAssert.Contains(resultJson, "serviceKey");
        StringAssert.Contains(resultJson, "finnhub");

        finnhub.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
        yahoo.Verify(p => p.GetStockInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    [DataRow("get_stock_info")]
    [DataRow("get_historical_stock_prices")]
    [DataRow("get_financial_statement")]
    [DataRow("get_news")]
    [DataRow("get_company_info")]
    [DataRow("get_stock_quote")]
    [DataRow("get_stock_dividends")]
    [DataRow("get_stock_splits")]
    [DataRow("get_earnings")]
    [DataRow("get_analyst_recommendations")]
    public async Task HandleToolCallAsync_AllTools_WithExplicitProvider_ReturnMetadata(string acceptanceToolName)
    {
        var implementedToolName = MapAcceptanceToolToImplementedTool(acceptanceToolName);

        ConfigureProviderBehaviorForTool(_mockProvider, implementedToolName);

        using var paramsJson = BuildToolCallParams(implementedToolName, includeProvider: true, provider: "yahoo");
        var request = new McpRequest
        {
            Id = 1123,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        var response = await InvokeHandleRequestAsync(request);

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);

        var resultJson = JsonSerializer.Serialize(response.Result);
        StringAssert.Contains(resultJson, "serviceKey");
        StringAssert.Contains(resultJson, "yahoo");
        StringAssert.Contains(resultJson, "tier");
        StringAssert.Contains(resultJson, "free");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetStockInfo_WithAlphaVantageProvider_ReturnsCorrectMetadata()
    {
        var yahoo = CreateProviderMock("yahoo_finance");
        var alphaVantage = CreateProviderMock("alphavantage");
        var finnhub = CreateProviderMock("finnhub");

        alphaVantage
            .Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"symbol\":\"AAPL\",\"provider\":\"alphavantage\"}");

        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var router = new StockDataProviderRouter(config, new[] { yahoo.Object, alphaVantage.Object, finnhub.Object });
        var server = new StockDataMcpServer(router, config);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_stock_info"",
            ""arguments"": {
                ""ticker"": ""AAPL"",
                ""provider"": ""alphavantage""
            }
        }");

        var response = await server.HandleRequestAsync(new McpRequest
        {
            Id = 1124,
            Method = "tools/call",
            Params = paramsJson.RootElement
        }, CancellationToken.None);

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);

        var resultJson = JsonSerializer.Serialize(response.Result);
        StringAssert.Contains(resultJson, "serviceKey");
        StringAssert.Contains(resultJson, "alphavantage");
        StringAssert.Contains(resultJson, "tier");
        StringAssert.Contains(resultJson, "free");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetStockInfo_WithFinnhubProvider_ReturnsCorrectMetadata()
    {
        var yahoo = CreateProviderMock("yahoo_finance");
        var alphaVantage = CreateProviderMock("alphavantage");
        var finnhub = CreateProviderMock("finnhub");

        finnhub
            .Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync("{\"symbol\":\"AAPL\",\"provider\":\"finnhub\"}");

        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var router = new StockDataProviderRouter(config, new[] { yahoo.Object, alphaVantage.Object, finnhub.Object });
        var server = new StockDataMcpServer(router, config);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_stock_info"",
            ""arguments"": {
                ""ticker"": ""AAPL"",
                ""provider"": ""finnhub""
            }
        }");

        var response = await server.HandleRequestAsync(new McpRequest
        {
            Id = 1125,
            Method = "tools/call",
            Params = paramsJson.RootElement
        }, CancellationToken.None);

        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);

        var resultJson = JsonSerializer.Serialize(response.Result);
        StringAssert.Contains(resultJson, "serviceKey");
        StringAssert.Contains(resultJson, "finnhub");
        StringAssert.Contains(resultJson, "tier");
        StringAssert.Contains(resultJson, "free");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetStockInfo_WithCaretTicker_Success()
    {
        // Arrange
        var testData = "{\"symbol\":\"^VIX\",\"price\":18.5}";
        _mockProvider
            .Setup(p => p.GetStockInfoAsync("^VIX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_stock_info"",
            ""arguments"": {
                ""ticker"": ""^VIX""
            }
        }");

        var request = new McpRequest
        {
            Id = 111,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetStockInfoAsync("^VIX", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetHistoricalStockPrices_WithCaretTicker_Success()
    {
        // Arrange
        var testData = "[{\"Date\":\"2026-03-01T00:00:00\",\"Open\":18.0}]";
        _mockProvider
            .Setup(p => p.GetHistoricalPricesAsync("^VIX", "5d", "1h", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_historical_stock_prices"",
            ""arguments"": {
                ""ticker"": ""^VIX"",
                ""period"": ""5d"",
                ""interval"": ""1h""
            }
        }");

        var request = new McpRequest
        {
            Id = 112,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetHistoricalPricesAsync("^VIX", "5d", "1h", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetFinanceNews_Success()
    {
        // Arrange
        var testData = "Title: Breaking News\nPublisher: Reuters";
        _mockProvider
            .Setup(p => p.GetNewsAsync("GOOGL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_finance_news"",
            ""arguments"": {
                ""ticker"": ""GOOGL""
            }
        }");

        var request = new McpRequest
        {
            Id = 12,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetNewsAsync("GOOGL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetMarketNews_Success()
    {
        // Arrange
        var testData = "Market news content";
        _mockProvider
            .Setup(p => p.GetMarketNewsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_market_news"",
            ""arguments"": {}
        }");

        var request = new McpRequest
        {
            Id = 13,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetMarketNewsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetStockActions_Success()
    {
        // Arrange
        var testDate = DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd");
        var testData = $"[{{\"Date\":\"{testDate}\",\"Dividends\":0.25}}]";
        _mockProvider
            .Setup(p => p.GetStockActionsAsync("TSLA", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_stock_actions"",
            ""arguments"": {
                ""ticker"": ""TSLA""
            }
        }");

        var request = new McpRequest
        {
            Id = 14,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetStockActionsAsync("TSLA", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetFinancialStatement_Success()
    {
        // Arrange
        var testDate = DateTime.Now.AddYears(-1).ToString("yyyy-MM-dd");
        var testData = $"[{{\"date\":\"{testDate}\",\"totalRevenue\":100000000}}]";
        _mockProvider
            .Setup(p => p.GetFinancialStatementAsync("NVDA", FinancialStatementType.IncomeStatement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_financial_statement"",
            ""arguments"": {
                ""ticker"": ""NVDA"",
                ""financial_type"": ""income_stmt""
            }
        }");

        var request = new McpRequest
        {
            Id = 15,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetFinancialStatementAsync("NVDA", FinancialStatementType.IncomeStatement, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetHolderInfo_Success()
    {
        // Arrange
        var testData = "[{\"organization\":\"BlackRock\",\"pctHeld\":0.07}]";
        _mockProvider
            .Setup(p => p.GetHolderInfoAsync("META", HolderType.InstitutionalHolders, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_holder_info"",
            ""arguments"": {
                ""ticker"": ""META"",
                ""holder_type"": ""institutional_holders""
            }
        }");

        var request = new McpRequest
        {
            Id = 16,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetHolderInfoAsync("META", HolderType.InstitutionalHolders, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetOptionExpirationDates_Success()
    {
        // Arrange
        var date1 = DateTime.Now.AddDays(14).ToString("yyyy-MM-dd");
        var date2 = DateTime.Now.AddDays(42).ToString("yyyy-MM-dd");
        var testData = $"[\"{date1}\",\"{date2}\"]";
        _mockProvider
            .Setup(p => p.GetOptionExpirationDatesAsync("AMD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_option_expiration_dates"",
            ""arguments"": {
                ""ticker"": ""AMD""
            }
        }");

        var request = new McpRequest
        {
            Id = 17,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetOptionExpirationDatesAsync("AMD", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetOptionChain_Success()
    {
        // Arrange
        var expirationDate = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd");
        var testData = "[{\"strike\":150.0,\"lastPrice\":5.0}]";
        _mockProvider
            .Setup(p => p.GetOptionChainAsync("INTC", expirationDate, OptionType.Calls, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse($@"{{
            ""name"": ""get_option_chain"",
            ""arguments"": {{
                ""ticker"": ""INTC"",
                ""expiration_date"": ""{expirationDate}"",
                ""option_type"": ""calls""
            }}
        }}");

        var request = new McpRequest
        {
            Id = 18,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetOptionChainAsync("INTC", expirationDate, OptionType.Calls, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_GetRecommendations_Success()
    {
        // Arrange
        var testData = "[{\"period\":\"0m\",\"strongBuy\":12,\"buy\":18}]";
        _mockProvider
            .Setup(p => p.GetRecommendationsAsync("NFLX", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_recommendations"",
            ""arguments"": {
                ""ticker"": ""NFLX"",
                ""recommendation_type"": ""recommendations"",
                ""months_back"": 12
            }
        }");

        var request = new McpRequest
        {
            Id = 19,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetRecommendationsAsync("NFLX", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [TestMethod]
    public async Task HandleToolCallAsync_MissingParams_ReturnsError()
    {
        // Arrange
        var request = new McpRequest
        {
            Id = 20,
            Method = "tools/call",
            Params = null
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Error);
        Assert.AreEqual(-32603, response.Error.Code);
        StringAssert.Contains(response.Error.Message, "Missing params");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_UnknownTool_ReturnsError()
    {
        // Arrange
        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""unknown_tool"",
            ""arguments"": {}
        }");

        var request = new McpRequest
        {
            Id = 21,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Error);
        StringAssert.Contains(response.Error.Message, "Unknown tool");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_InvalidProvider_ReturnsSupportedProvidersError()
    {
        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_stock_info"",
            ""arguments"": {
                ""ticker"": ""AAPL"",
                ""provider"": ""bloomberg""
            }
        }");

        var request = new McpRequest
        {
            Id = 221,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        var response = await InvokeHandleRequestAsync(request);

        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Error);
        StringAssert.Contains(response.Error.Message, "Supported providers");
        StringAssert.Contains(response.Error.Message, "yahoo");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_ExplicitProviderFailure_IncludesProviderMetadataInErrorData()
    {
        _mockProvider
            .Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("upstream timeout"));

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_stock_info"",
            ""arguments"": {
                ""ticker"": ""AAPL"",
                ""provider"": ""yahoo""
            }
        }");

        var request = new McpRequest
        {
            Id = 222,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        var response = await InvokeHandleRequestAsync(request);

        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Error);
        Assert.IsNotNull(response.Error.Data);

        var dataJson = JsonSerializer.Serialize(response.Error.Data);
        StringAssert.Contains(dataJson, "serviceKey");
        StringAssert.Contains(dataJson, "yahoo");
        StringAssert.Contains(dataJson, "tier");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_LegacyYahooFinanceNewsTool_ReturnsUnknownToolError()
    {
        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_yahoo_finance_news"",
            ""arguments"": {
                ""ticker"": ""AAPL""
            }
        }");

        var request = new McpRequest
        {
            Id = 212,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        var response = await InvokeHandleRequestAsync(request);

        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Error);
        StringAssert.Contains(response.Error.Message, "Unknown tool");
        StringAssert.Contains(response.Error.Message, "get_yahoo_finance_news");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_TierAwareNotSupportedException_ReturnsFormattedErrorResponse()
    {
        // Arrange
        _mockProvider
            .Setup(p => p.GetMarketNewsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TierAwareNotSupportedException("finnhub", "GetMarketNewsAsync", availableOnPaidTier: true));

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_market_news"",
            ""arguments"": {}
        }");

        var request = new McpRequest
        {
            Id = 211,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Result);
        Assert.IsNotNull(response.Error);
        Assert.AreEqual(-32603, response.Error.Code);
        StringAssert.Contains(response.Error.Message, "Provider 'finnhub' does not support GetMarketNewsAsync on the free tier.");
        StringAssert.Contains(response.Error.Message, "paid subscription");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_MissingRequiredParameter_ReturnsError()
    {
        // Arrange
        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_stock_info"",
            ""arguments"": {}
        }");

        var request = new McpRequest
        {
            Id = 22,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Error);
        Assert.IsTrue(response.Error.Message.Contains("Missing required parameter") || 
                      response.Error.Message.Contains("ticker")); // Multiple conditions in OR
    }

    [TestMethod]
    public async Task HandleToolCallAsync_InvalidFinancialType_ReturnsError()
    {
        // Arrange
        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_financial_statement"",
            ""arguments"": {
                ""ticker"": ""AAPL"",
                ""financial_type"": ""invalid_type""
            }
        }");

        var request = new McpRequest
        {
            Id = 23,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Error);
        StringAssert.Contains(response.Error.Message, "Invalid financial statement type");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_InvalidHolderType_ReturnsError()
    {
        // Arrange
        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_holder_info"",
            ""arguments"": {
                ""ticker"": ""AAPL"",
                ""holder_type"": ""invalid_holder""
            }
        }");

        var request = new McpRequest
        {
            Id = 24,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Error);
        StringAssert.Contains(response.Error.Message, "Invalid holder type");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_InvalidOptionType_ReturnsError()
    {
        // Arrange
        var expirationDate = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd");
        var paramsJson = JsonDocument.Parse($@"{{
            ""name"": ""get_option_chain"",
            ""arguments"": {{
                ""ticker"": ""AAPL"",
                ""expiration_date"": ""{expirationDate}"",
                ""option_type"": ""invalid""
            }}
        }}");

        var request = new McpRequest
        {
            Id = 25,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Error);
        StringAssert.Contains(response.Error.Message, "Invalid option type");
    }

    [TestMethod]
    public async Task HandleToolCallAsync_InvalidRecommendationType_ReturnsError()
    {
        // Arrange
        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_recommendations"",
            ""arguments"": {
                ""ticker"": ""AAPL"",
                ""recommendation_type"": ""invalid_type""
            }
        }");

        var request = new McpRequest
        {
            Id = 26,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNotNull(response.Error);
        StringAssert.Contains(response.Error.Message, "Invalid recommendation type");
    }

    #endregion

    #region Parameter Parsing Tests

    [TestMethod]
    [DataRow("income_stmt", FinancialStatementType.IncomeStatement)]
    [DataRow("quarterly_income_stmt", FinancialStatementType.QuarterlyIncomeStatement)]
    [DataRow("balance_sheet", FinancialStatementType.BalanceSheet)]
    [DataRow("quarterly_balance_sheet", FinancialStatementType.QuarterlyBalanceSheet)]
    [DataRow("cashflow", FinancialStatementType.CashFlow)]
    [DataRow("quarterly_cashflow", FinancialStatementType.QuarterlyCashFlow)]
    public async Task ParseFinancialType_AllValidTypes_ParseCorrectly(string typeString, FinancialStatementType expected)
    {
        // Arrange
        _mockProvider
            .Setup(p => p.GetFinancialStatementAsync(It.IsAny<string>(), expected, It.IsAny<CancellationToken>()))
            .ReturnsAsync("[]");

        var paramsJson = JsonDocument.Parse($@"{{
            ""name"": ""get_financial_statement"",
            ""arguments"": {{
                ""ticker"": ""TEST"",
                ""financial_type"": ""{typeString}""
            }}
        }}");

        var request = new McpRequest
        {
            Id = 30,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetFinancialStatementAsync("TEST", expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow("major_holders", HolderType.MajorHolders)]
    [DataRow("institutional_holders", HolderType.InstitutionalHolders)]
    [DataRow("mutualfund_holders", HolderType.MutualFundHolders)]
    [DataRow("insider_transactions", HolderType.InsiderTransactions)]
    [DataRow("insider_purchases", HolderType.InsiderPurchases)]
    [DataRow("insider_roster_holders", HolderType.InsiderRosterHolders)]
    public async Task ParseHolderType_AllValidTypes_ParseCorrectly(string typeString, HolderType expected)
    {
        // Arrange
        _mockProvider
            .Setup(p => p.GetHolderInfoAsync(It.IsAny<string>(), expected, It.IsAny<CancellationToken>()))
            .ReturnsAsync("[]");

        var paramsJson = JsonDocument.Parse($@"{{
            ""name"": ""get_holder_info"",
            ""arguments"": {{
                ""ticker"": ""TEST"",
                ""holder_type"": ""{typeString}""
            }}
        }}");

        var request = new McpRequest
        {
            Id = 31,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetHolderInfoAsync("TEST", expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow("calls", OptionType.Calls)]
    [DataRow("puts", OptionType.Puts)]
    public async Task ParseOptionType_AllValidTypes_ParseCorrectly(string typeString, OptionType expected)
    {
        // Arrange
        var expirationDate = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd");
        _mockProvider
            .Setup(p => p.GetOptionChainAsync(It.IsAny<string>(), It.IsAny<string>(), expected, It.IsAny<CancellationToken>()))
            .ReturnsAsync("[]");

        var paramsJson = JsonDocument.Parse($@"{{
            ""name"": ""get_option_chain"",
            ""arguments"": {{
                ""ticker"": ""TEST"",
                ""expiration_date"": ""{expirationDate}"",
                ""option_type"": ""{typeString}""
            }}
        }}");

        var request = new McpRequest
        {
            Id = 32,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetOptionChainAsync("TEST", expirationDate, expected, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow("recommendations", RecommendationType.Recommendations)]
    [DataRow("upgrades_downgrades", RecommendationType.UpgradesDowngrades)]
    public async Task ParseRecommendationType_AllValidTypes_ParseCorrectly(string typeString, RecommendationType expected)
    {
        // Arrange
        _mockProvider
            .Setup(p => p.GetRecommendationsAsync(It.IsAny<string>(), expected, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("[]");

        var paramsJson = JsonDocument.Parse($@"{{
            ""name"": ""get_recommendations"",
            ""arguments"": {{
                ""ticker"": ""TEST"",
                ""recommendation_type"": ""{typeString}""
            }}
        }}");

        var request = new McpRequest
        {
            Id = 33,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetRecommendationsAsync("TEST", expected, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_OptionalParameters_UseDefaults()
    {
        // Arrange
        _mockProvider
            .Setup(p => p.GetHistoricalPricesAsync("TEST", "1mo", "1d", It.IsAny<CancellationToken>()))
            .ReturnsAsync("[]");

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_historical_stock_prices"",
            ""arguments"": {
                ""ticker"": ""TEST""
            }
        }");

        var request = new McpRequest
        {
            Id = 34,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetHistoricalPricesAsync("TEST", "1mo", "1d", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task HandleToolCallAsync_OptionalIntParameter_Default()
    {
        // Arrange
        _mockProvider
            .Setup(p => p.GetRecommendationsAsync("TEST", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync("[]");

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_recommendations"",
            ""arguments"": {
                ""ticker"": ""TEST"",
                ""recommendation_type"": ""recommendations""
            }
        }");

        var request = new McpRequest
        {
            Id = 35,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        // Act
        var response = await InvokeHandleRequestAsync(request);

        // Assert
        Assert.IsNotNull(response);
        Assert.IsNull(response.Error);
        _mockProvider.Verify(p => p.GetRecommendationsAsync("TEST", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public void FormatInvestorFriendlyMessage_DoesNotIncludeRawExceptionData()
    {
        var failover = new ProviderFailoverException(
            "StockInfo",
            new Dictionary<string, Exception>
            {
                ["finnhub"] = new InvalidOperationException("Unhandled exception at System.Service call in C:\\temp\\server.cs:42 apikey=SECRETVALUE123 ---")
            },
            new List<string> { "finnhub" },
            Array.Empty<TierFailureDetail>());

        var message = StockDataMcpServer.FormatInvestorFriendlyMessage(failover);

        Assert.IsFalse(message.Contains("at ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(message.Contains(" in ", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(message.Contains("---", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(message.Contains("apikey=", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(message.Contains("token=", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void FormatInvestorFriendlyMessage_WhenSensitiveApiKeyPresent_DoesNotLeakKeyValue()
    {
        var failover = new ProviderFailoverException(
            "StockInfo",
            new Dictionary<string, Exception>
            {
                ["finnhub"] = new InvalidOperationException("request failed apikey=SECRETVALUE123"),
                ["alphavantage"] = new InvalidOperationException("request failed token=SECRETVALUE123")
            },
            new List<string> { "finnhub", "alphavantage" },
            Array.Empty<TierFailureDetail>());

        var message = StockDataMcpServer.FormatInvestorFriendlyMessage(failover);

        Assert.IsFalse(message.Contains("SECRETVALUE123", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FormatInvestorFriendlyMessage_WhenStackTraceFragmentsPresent_DoesNotIncludeThem()
    {
        var failover = new ProviderFailoverException(
            "StockInfo",
            new Dictionary<string, Exception>
            {
                ["finnhub"] = new InvalidOperationException("at System.Net.Http.HttpClient.SendAsync()\n   at StockData.Net.Clients.Finnhub.FinnhubClient.GetQuoteAsync()")
            },
            new List<string> { "finnhub" },
            Array.Empty<TierFailureDetail>());

        var message = StockDataMcpServer.FormatInvestorFriendlyMessage(failover);

        Assert.IsFalse(message.Contains("at System.", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(message.Contains("at StockData.", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(message.Contains("\n   at ", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void FormatInvestorFriendlyMessage_WhenTierFailuresPresent_UsesProviderUpgradeUrlConstants()
    {
        var failover = new ProviderFailoverException(
            "HistoricalPrices",
            new Dictionary<string, Exception>(),
            new List<string> { "finnhub", "alphavantage" },
            new[]
            {
                new TierFailureDetail("Finnhub", "historical_prices", "free", ProviderUpgradeUrls.FinnhubPricing),
                new TierFailureDetail("Alpha Vantage", "historical_prices", "free", ProviderUpgradeUrls.AlphaVantagePremium)
            });

        var message = StockDataMcpServer.FormatInvestorFriendlyMessage(failover);

        StringAssert.Contains(message, ProviderUpgradeUrls.FinnhubPricing);
        StringAssert.Contains(message, ProviderUpgradeUrls.AlphaVantagePremium);
    }

    [TestMethod]
    public void ProviderUpgradeUrls_ConstantsUseHttps()
    {
        Assert.IsTrue(ProviderUpgradeUrls.FinnhubPricing.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(ProviderUpgradeUrls.AlphaVantagePremium.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void FormatInvestorFriendlyMessage_SanitizesSensitiveExceptionContent()
    {
        var failover = new ProviderFailoverException(
            "StockInfo",
            new Dictionary<string, Exception>
            {
                ["finnhub"] = new InvalidOperationException("Auth failed for token SECRET98765VALUE")
            },
            new List<string> { "finnhub" },
            Array.Empty<TierFailureDetail>());

        var message = StockDataMcpServer.FormatInvestorFriendlyMessage(failover);

        Assert.IsFalse(message.Contains("SECRET98765VALUE", StringComparison.Ordinal));
        StringAssert.Contains(message, "[REDACTED]");
    }

    #endregion

    #region Helper Methods

    private async Task<McpResponse> InvokeHandleRequestAsync(McpRequest request)
    {
        // Now we can call the internal method directly thanks to InternalsVisibleTo
        return await _server.HandleRequestAsync(request, CancellationToken.None);
    }

    private static StockDataMcpServer CreateServerWithProviders(params string[] providerIds)
    {
        var config = new ConfigurationLoader().GetDefaultConfiguration();
        var providers = providerIds.Select(CreateProviderMock).Select(mock => mock.Object).ToArray();
        var router = new StockDataProviderRouter(config, providers);
        return new StockDataMcpServer(router, config);
    }

    private static async Task<McpResponse> InvokeListProvidersAsync(StockDataMcpServer server, bool includeArgumentsProperty)
    {
        var paramsPayload = includeArgumentsProperty
            ? "{\"name\":\"list_providers\",\"arguments\":{}}"
            : "{\"name\":\"list_providers\"}";

        using var paramsJson = JsonDocument.Parse(paramsPayload);

        var request = new McpRequest
        {
            Id = 300,
            Method = "tools/call",
            Params = paramsJson.RootElement
        };

        return await server.HandleRequestAsync(request, CancellationToken.None);
    }

    private static JsonElement ParseProvidersFromToolResponse(McpResponse response)
    {
        var textContent = ExtractTextContent(response);
        using var payload = JsonDocument.Parse(textContent);
        return payload.RootElement.GetProperty("providers").Clone();
    }

    private static string ExtractTextContent(McpResponse response)
    {
        Assert.IsNotNull(response.Result);

        using var wrapper = JsonDocument.Parse(JsonSerializer.Serialize(response.Result));
        return wrapper.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    private static JsonElement FindProviderById(JsonElement providers, string providerId)
    {
        foreach (var provider in providers.EnumerateArray())
        {
            if (string.Equals(provider.GetProperty("id").GetString(), providerId, StringComparison.OrdinalIgnoreCase))
            {
                return provider;
            }
        }

        return default;
    }

    private static string ExtractToolDescription(string toolsListJson, string toolName)
    {
        using var document = JsonDocument.Parse(toolsListJson);
        var tools = document.RootElement.GetProperty("tools");

        foreach (var tool in tools.EnumerateArray())
        {
            if (string.Equals(tool.GetProperty("name").GetString(), toolName, StringComparison.Ordinal))
            {
                return tool.GetProperty("description").GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string MapAcceptanceToolToImplementedTool(string acceptanceToolName)
    {
        return acceptanceToolName switch
        {
            "get_news" => "get_finance_news",
            "get_company_info" => "get_holder_info",
            "get_stock_quote" => "get_stock_info",
            "get_stock_dividends" => "get_stock_actions",
            "get_stock_splits" => "get_stock_actions",
            "get_earnings" => "get_financial_statement",
            "get_analyst_recommendations" => "get_recommendations",
            _ => acceptanceToolName
        };
    }

    private static JsonDocument BuildToolCallParams(string implementedToolName, bool includeProvider, string provider)
    {
        var arguments = new Dictionary<string, object?>();

        switch (implementedToolName)
        {
            case "get_historical_stock_prices":
                arguments["ticker"] = "AAPL";
                arguments["period"] = "1mo";
                arguments["interval"] = "1d";
                break;
            case "get_stock_info":
                arguments["ticker"] = "AAPL";
                break;
            case "get_finance_news":
                arguments["ticker"] = "AAPL";
                break;
            case "get_market_news":
                break;
            case "get_stock_actions":
                arguments["ticker"] = "AAPL";
                break;
            case "get_financial_statement":
                arguments["ticker"] = "AAPL";
                arguments["financial_type"] = "income_stmt";
                break;
            case "get_holder_info":
                arguments["ticker"] = "AAPL";
                arguments["holder_type"] = "major_holders";
                break;
            case "get_option_expiration_dates":
                arguments["ticker"] = "AAPL";
                break;
            case "get_option_chain":
                arguments["ticker"] = "AAPL";
                arguments["expiration_date"] = "2026-12-19";
                arguments["option_type"] = "calls";
                break;
            case "get_recommendations":
                arguments["ticker"] = "AAPL";
                arguments["recommendation_type"] = "recommendations";
                arguments["months_back"] = 12;
                break;
            default:
                throw new InvalidOperationException($"Unsupported tool for test: {implementedToolName}");
        }

        if (includeProvider)
        {
            arguments["provider"] = provider;
        }

        var payload = new
        {
            name = implementedToolName,
            arguments
        };

        return JsonDocument.Parse(JsonSerializer.Serialize(payload));
    }

    private static void ConfigureProviderBehaviorForTool(Mock<IStockDataProvider> provider, string implementedToolName)
    {
        switch (implementedToolName)
        {
            case "get_historical_stock_prices":
                provider
                    .Setup(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()))
                    .ReturnsAsync("[]");
                break;
            case "get_stock_info":
                provider
                    .Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
                    .ReturnsAsync("{}");
                break;
            case "get_finance_news":
                provider
                    .Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
                    .ReturnsAsync("[]");
                break;
            case "get_market_news":
                provider
                    .Setup(p => p.GetMarketNewsAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync("[]");
                break;
            case "get_stock_actions":
                provider
                    .Setup(p => p.GetStockActionsAsync("AAPL", It.IsAny<CancellationToken>()))
                    .ReturnsAsync("[]");
                break;
            case "get_financial_statement":
                provider
                    .Setup(p => p.GetFinancialStatementAsync("AAPL", FinancialStatementType.IncomeStatement, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("[]");
                break;
            case "get_holder_info":
                provider
                    .Setup(p => p.GetHolderInfoAsync("AAPL", HolderType.MajorHolders, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("[]");
                break;
            case "get_option_expiration_dates":
                provider
                    .Setup(p => p.GetOptionExpirationDatesAsync("AAPL", It.IsAny<CancellationToken>()))
                    .ReturnsAsync("[]");
                break;
            case "get_option_chain":
                provider
                    .Setup(p => p.GetOptionChainAsync("AAPL", "2026-12-19", OptionType.Calls, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("[]");
                break;
            case "get_recommendations":
                provider
                    .Setup(p => p.GetRecommendationsAsync("AAPL", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>()))
                    .ReturnsAsync("[]");
                break;
            default:
                throw new InvalidOperationException($"Unsupported tool for behavior setup: {implementedToolName}");
        }
    }

    private static Mock<IStockDataProvider> CreateProviderMock(string providerId)
    {
        var provider = new Mock<IStockDataProvider>();
        provider.Setup(p => p.ProviderId).Returns(providerId);
        provider.Setup(p => p.ProviderName).Returns(providerId);
        provider.Setup(p => p.Version).Returns("1.0.0");
        provider.Setup(p => p.GetSupportedDataTypes(It.IsAny<string>())).Returns((string tier) =>
        {
            return providerId.ToLowerInvariant() switch
            {
                "yahoo_finance" => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "historical_prices",
                    "stock_info",
                    "news",
                    "market_news",
                    "stock_actions",
                    "financial_statement",
                    "holder_info",
                    "option_expiration_dates",
                    "option_chain",
                    "recommendations"
                },
                "alphavantage" => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "historical_prices",
                    "stock_info",
                    "news",
                    "market_news",
                    "stock_actions"
                },
                "finnhub" when string.Equals(tier, "paid", StringComparison.OrdinalIgnoreCase) => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "stock_info",
                    "news",
                    "market_news",
                    "recommendations",
                    "historical_prices",
                    "stock_actions"
                },
                "finnhub" => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "stock_info",
                    "news",
                    "market_news",
                    "recommendations"
                },
                _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "stock_info"
                }
            };
        });

        provider
            .Setup(p => p.GetStockInfoAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("{}");

        return provider;
    }

    #endregion

    #region Router Integration Tests (Original Tests)

    [TestMethod]
    public async Task YahooFinanceRouter_GetHistoricalPrices_IntegrationWithMcp()
    {
        // Arrange
        var testDate = DateTime.Now.AddDays(-30).ToString("yyyy-MM-dd");
        var testData = $"[{{\"Date\":\"{testDate}\",\"Open\":150.0,\"Close\":155.0}}]";
        _mockProvider
            .Setup(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetHistoricalPricesAsync("AAPL", "1mo", "1d");

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task YahooFinanceRouter_GetStockInfo_IntegrationWithMcp()
    {
        // Arrange
        var testData = "{\"symbol\":\"AAPL\",\"price\":150.0}";
        _mockProvider
            .Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetStockInfoAsync("AAPL");

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task YahooFinanceRouter_GetNews_IntegrationWithMcp()
    {
        // Arrange
        var testData = "Title: Test News\nPublisher: Test Publisher\nURL: https://example.com";
        _mockProvider
            .Setup(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetNewsAsync("AAPL");

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetNewsAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task YahooFinanceRouter_GetStockActions_IntegrationWithMcp()
    {
        // Arrange
        var testDate = DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd");
        var testData = $"[{{\"Date\":\"{testDate}\",\"Dividends\":0.25}}]";
        _mockProvider
            .Setup(p => p.GetStockActionsAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetStockActionsAsync("AAPL");

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetStockActionsAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task YahooFinanceRouter_GetFinancialStatement_IntegrationWithMcp()
    {
        // Arrange
        var testDate = DateTime.Now.AddYears(-1).ToString("yyyy-MM-dd");
        var testData = $"[{{\"date\":\"{testDate}\",\"totalRevenue\":1000000}}]";
        _mockProvider
            .Setup(p => p.GetFinancialStatementAsync("AAPL", FinancialStatementType.IncomeStatement, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetFinancialStatementAsync("AAPL", FinancialStatementType.IncomeStatement);

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetFinancialStatementAsync("AAPL", FinancialStatementType.IncomeStatement, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task YahooFinanceRouter_GetHolderInfo_IntegrationWithMcp()
    {
        // Arrange
        var testData = "[{\"organization\":\"Vanguard\",\"pctHeld\":0.08}]";
        _mockProvider
            .Setup(p => p.GetHolderInfoAsync("AAPL", HolderType.InstitutionalHolders, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetHolderInfoAsync("AAPL", HolderType.InstitutionalHolders);

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetHolderInfoAsync("AAPL", HolderType.InstitutionalHolders, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task YahooFinanceRouter_GetOptionExpirationDates_IntegrationWithMcp()
    {
        // Arrange
        var date1 = DateTime.Now.AddDays(14).ToString("yyyy-MM-dd");
        var date2 = DateTime.Now.AddDays(42).ToString("yyyy-MM-dd");
        var testData = $"[\"{date1}\",\"{date2}\"]";
        _mockProvider
            .Setup(p => p.GetOptionExpirationDatesAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetOptionExpirationDatesAsync("AAPL");

        // Assert
        Assert.AreEqual(testData,result);
        _mockProvider.Verify(p => p.GetOptionExpirationDatesAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task YahooFinanceRouter_GetOptionChain_IntegrationWithMcp()
    {
        // Arrange
        var expirationDate = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd");
        var testData = "[{\"strike\":150.0,\"lastPrice\":5.0}]";
        _mockProvider
            .Setup(p => p.GetOptionChainAsync("AAPL", expirationDate, OptionType.Calls, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetOptionChainAsync("AAPL", expirationDate, OptionType.Calls);

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetOptionChainAsync("AAPL", expirationDate, OptionType.Calls, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task YahooFinanceRouter_GetRecommendations_IntegrationWithMcp()
    {
        // Arrange
        var testData = "[{\"period\":\"0m\",\"strongBuy\":10,\"buy\":15}]";
        _mockProvider
            .Setup(p => p.GetRecommendationsAsync("AAPL", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetRecommendationsAsync("AAPL", RecommendationType.Recommendations, 12);

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetRecommendationsAsync("AAPL", RecommendationType.Recommendations, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow(FinancialStatementType.IncomeStatement)]
    [DataRow(FinancialStatementType.QuarterlyIncomeStatement)]
    [DataRow(FinancialStatementType.BalanceSheet)]
    [DataRow(FinancialStatementType.QuarterlyBalanceSheet)]
    [DataRow(FinancialStatementType.CashFlow)]
    [DataRow(FinancialStatementType.QuarterlyCashFlow)]
    public async Task YahooFinanceRouter_AllFinancialStatementTypes_Work(FinancialStatementType type)
    {
        // Arrange
        var testDate = DateTime.Now.AddYears(-1).ToString("yyyy-MM-dd");
        var testData = $"[{{\"date\":\"{testDate}\",\"type\":\"{type}\"}}]";
        _mockProvider
            .Setup(p => p.GetFinancialStatementAsync("AAPL", type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetFinancialStatementAsync("AAPL", type);

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetFinancialStatementAsync("AAPL", type, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow(HolderType.MajorHolders)]
    [DataRow(HolderType.InstitutionalHolders)]
    [DataRow(HolderType.MutualFundHolders)]
    [DataRow(HolderType.InsiderTransactions)]
    [DataRow(HolderType.InsiderPurchases)]
    [DataRow(HolderType.InsiderRosterHolders)]
    public async Task YahooFinanceRouter_AllHolderTypes_Work(HolderType type)
    {
        // Arrange
        var testData = $"[{{\"type\":\"{type}\"}}]";
        _mockProvider
            .Setup(p => p.GetHolderInfoAsync("AAPL", type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetHolderInfoAsync("AAPL", type);

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetHolderInfoAsync("AAPL", type, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow(OptionType.Calls)]
    [DataRow(OptionType.Puts)]
    public async Task YahooFinanceRouter_AllOptionTypes_Work(OptionType type)
    {
        // Arrange
        var expirationDate = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd");
        var testData = $"[{{\"type\":\"{type}\"}}]";
        _mockProvider
            .Setup(p => p.GetOptionChainAsync("AAPL", expirationDate, type, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetOptionChainAsync("AAPL", expirationDate, type);

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetOptionChainAsync("AAPL", expirationDate, type, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow(RecommendationType.Recommendations)]
    [DataRow(RecommendationType.UpgradesDowngrades)]
    public async Task YahooFinanceRouter_AllRecommendationTypes_Work(RecommendationType type)
    {
        // Arrange
        var testData = $"[{{\"type\":\"{type}\"}}]";
        _mockProvider
            .Setup(p => p.GetRecommendationsAsync("AAPL", type, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetRecommendationsAsync("AAPL", type, 12);

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetRecommendationsAsync("AAPL", type, 12, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task YahooFinanceRouter_ErrorHandling_ReturnsErrorMessage()
    {
        // Arrange
        var errorMessage = "Error: Network failure";
        _mockProvider
            .Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(errorMessage);

        // Act
        var result = await _router.GetStockInfoAsync("AAPL");

        // Assert
        Assert.AreEqual(errorMessage, result);
        StringAssert.Contains(result, "Error");
    }

    [TestMethod]
    public async Task YahooFinanceRouter_DefaultParameters_UsedCorrectly()
    {
        // Arrange
        var testData = "[{\"data\":\"test\"}]";
        _mockProvider
            .Setup(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act - Call without optional parameters
        var result = await _router.GetHistoricalPricesAsync("AAPL");

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetHistoricalPricesAsync("AAPL", "1mo", "1d", It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    public async Task YahooFinanceRouter_WithCancellationToken_HandledCorrectly()
    {
        // Arrange
        var testData = "[{\"data\":\"test\"}]";
        var cts = new CancellationTokenSource();
        _mockProvider
            .Setup(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        // Act
        var result = await _router.GetStockInfoAsync("AAPL", cts.Token);

        // Assert
        Assert.AreEqual(testData, result);
        _mockProvider.Verify(p => p.GetStockInfoAsync("AAPL", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
