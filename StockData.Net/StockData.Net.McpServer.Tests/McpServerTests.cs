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
        Assert.IsTrue(resultJson.Contains("protocolVersion"));
        Assert.IsTrue(resultJson.Contains("StockData-mcp"));
    }

    [TestMethod]
    public async Task HandleRequestAsync_ToolsList_ReturnsAll10Tools()
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
        Assert.IsTrue(resultJson.Contains("get_historical_stock_prices"));
        Assert.IsTrue(resultJson.Contains("get_stock_info"));
        Assert.IsTrue(resultJson.Contains("get_yahoo_finance_news"));
        Assert.IsTrue(resultJson.Contains("get_market_news"));
        Assert.IsTrue(resultJson.Contains("get_stock_actions"));
        Assert.IsTrue(resultJson.Contains("get_financial_statement"));
        Assert.IsTrue(resultJson.Contains("get_holder_info"));
        Assert.IsTrue(resultJson.Contains("get_option_expiration_dates"));
        Assert.IsTrue(resultJson.Contains("get_option_chain"));
        Assert.IsTrue(resultJson.Contains("get_recommendations"));
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
        Assert.IsTrue(response.Error.Message.Contains("Unknown method"));
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
        Assert.IsTrue(resultJson.Contains("Date"));
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
    public async Task HandleToolCallAsync_GetYahooFinanceNews_Success()
    {
        // Arrange
        var testData = "Title: Breaking News\nPublisher: Reuters";
        _mockProvider
            .Setup(p => p.GetNewsAsync("GOOGL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(testData);

        var paramsJson = JsonDocument.Parse(@"{
            ""name"": ""get_yahoo_finance_news"",
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
        Assert.IsTrue(response.Error.Message.Contains("Missing params"));
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
        Assert.IsTrue(response.Error.Message.Contains("Unknown tool"));
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
                      response.Error.Message.Contains("ticker"));
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
        Assert.IsTrue(response.Error.Message.Contains("Invalid financial statement type"));
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
        Assert.IsTrue(response.Error.Message.Contains("Invalid holder type"));
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
        Assert.IsTrue(response.Error.Message.Contains("Invalid option type"));
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
        Assert.IsTrue(response.Error.Message.Contains("Invalid recommendation type"));
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

    #endregion

    #region Helper Methods

    private async Task<McpResponse> InvokeHandleRequestAsync(McpRequest request)
    {
        // Now we can call the internal method directly thanks to InternalsVisibleTo
        return await _server.HandleRequestAsync(request, CancellationToken.None);
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
        Assert.IsTrue(result.Contains("Error"));
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
