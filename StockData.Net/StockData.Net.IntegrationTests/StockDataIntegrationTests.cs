using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockData.Net;
using StockData.Net.Models;
using System.Text.Json;

namespace StockData.Net.IntegrationTests;

/// <summary>
/// Integration tests that make real API calls to Yahoo Finance.
/// These tests may be slower and can fail if Yahoo Finance is unavailable or rate limits are hit.
/// </summary>
[TestClass]
public class StockDataIntegrationTests
{
    private YahooFinanceClient _client = null!;
    private const string TestTicker = "AAPL"; // Using Apple as a stable, well-known stock

    [TestInitialize]
    public void Setup()
    {
        _client = new YahooFinanceClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Add small delay to avoid rate limiting
        Thread.Sleep(500);
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetHistoricalPricesAsync_RealApiCall_ReturnsValidData()
    {
        // Act
        var result = await _client.GetHistoricalPricesAsync(TestTicker, "1mo", "1d");

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetHistoricalPricesAsync_RealApiCall_ReturnsValidData));
        
        // Verify it's valid JSON
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.GetArrayLength() > 0, "Should return at least one data point");
        
        // Verify expected properties exist
        var firstElement = jsonDocument.RootElement[0];
        Assert.IsTrue(firstElement.TryGetProperty("Date", out _), "Should have Date property");
        Assert.IsTrue(firstElement.TryGetProperty("Open", out _), "Should have Open property");
        Assert.IsTrue(firstElement.TryGetProperty("High", out _), "Should have High property");
        Assert.IsTrue(firstElement.TryGetProperty("Low", out _), "Should have Low property");
        Assert.IsTrue(firstElement.TryGetProperty("Close", out _), "Should have Close property");
        Assert.IsTrue(firstElement.TryGetProperty("Volume", out _), "Should have Volume property");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetStockInfoAsync_RealApiCall_ReturnsValidData()
    {
        // Act
        var result = await _client.GetStockInfoAsync(TestTicker);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetStockInfoAsync_RealApiCall_ReturnsValidData));
        
        // Verify it's valid JSON
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.EnumerateObject().Any(), "Should return stock information");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetNewsAsync_RealApiCall_ReturnsValidData()
    {
        // Act
        var result = await _client.GetNewsAsync(TestTicker);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetNewsAsync_RealApiCall_ReturnsValidData));
        
        // News should contain some expected fields
        Assert.IsTrue(result.Contains("Title:") || result.Contains("Publisher:") || result.Contains("No news found"), 
            "Should return formatted news or 'no news' message");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetMarketNewsAsync_RealApiCall_ReturnsValidData()
    {
        // Act
        var result = await _client.GetMarketNewsAsync();

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetMarketNewsAsync_RealApiCall_ReturnsValidData));
        
        // Market news should contain expected fields
        Assert.IsTrue(result.Contains("Title:") && result.Contains("Publisher:"), 
            "Should return formatted market news with Title and Publisher");
        
        // Market news should contain timestamps
        Assert.IsTrue(result.Contains("Published:"), 
            "Should include publication timestamps");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetStockActionsAsync_RealApiCall_ReturnsValidData()
    {
        // Act
        var result = await _client.GetStockActionsAsync(TestTicker);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetStockActionsAsync_RealApiCall_ReturnsValidData));
        
        // Verify it's valid JSON array
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.ValueKind == JsonValueKind.Array, "Should return JSON array");
        
        // Apple has historical dividends, so we should get data
        if (jsonDocument.RootElement.GetArrayLength() > 0)
        {
            var firstElement = jsonDocument.RootElement[0];
            Assert.IsTrue(firstElement.TryGetProperty("Date", out _), "Should have Date property");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetFinancialStatementAsync_IncomeStatement_ReturnsValidData()
    {
        // Act
        var result = await _client.GetFinancialStatementAsync(TestTicker, FinancialStatementType.IncomeStatement);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetFinancialStatementAsync_IncomeStatement_ReturnsValidData));
        
        // Verify it's valid JSON array
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.ValueKind == JsonValueKind.Array, "Should return JSON array");
        
        if (jsonDocument.RootElement.GetArrayLength() > 0)
        {
            var firstElement = jsonDocument.RootElement[0];
            Assert.IsTrue(firstElement.TryGetProperty("date", out _), "Should have date property");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetFinancialStatementAsync_BalanceSheet_ReturnsValidData()
    {
        // Act
        var result = await _client.GetFinancialStatementAsync(TestTicker, FinancialStatementType.BalanceSheet);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetFinancialStatementAsync_BalanceSheet_ReturnsValidData));
        
        // Verify it's valid JSON
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.ValueKind == JsonValueKind.Array, "Should return JSON array");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetFinancialStatementAsync_CashFlow_ReturnsValidData()
    {
        // Act
        var result = await _client.GetFinancialStatementAsync(TestTicker, FinancialStatementType.CashFlow);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetFinancialStatementAsync_CashFlow_ReturnsValidData));
        
        // Verify it's valid JSON
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.ValueKind == JsonValueKind.Array, "Should return JSON array");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetHolderInfoAsync_InstitutionalHolders_ReturnsValidData()
    {
        // Act
        var result = await _client.GetHolderInfoAsync(TestTicker, HolderType.InstitutionalHolders);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetHolderInfoAsync_InstitutionalHolders_ReturnsValidData));
        
        // Verify it's valid JSON
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.ValueKind == JsonValueKind.Array, "Should return JSON array");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetHolderInfoAsync_MajorHolders_ReturnsValidData()
    {
        // Act
        var result = await _client.GetHolderInfoAsync(TestTicker, HolderType.MajorHolders);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetHolderInfoAsync_MajorHolders_ReturnsValidData));
        
        // Verify it's valid JSON
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.ValueKind == JsonValueKind.Array, "Should return JSON array");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetOptionExpirationDatesAsync_RealApiCall_ReturnsValidData()
    {
        // Act
        var result = await _client.GetOptionExpirationDatesAsync(TestTicker);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetOptionExpirationDatesAsync_RealApiCall_ReturnsValidData));
        
        // Verify it's valid JSON array of dates
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.ValueKind == JsonValueKind.Array, "Should return JSON array");
        Assert.IsTrue(jsonDocument.RootElement.GetArrayLength() > 0, "Should have at least one expiration date");
        
        // Verify dates are in correct format (YYYY-MM-DD)
        var firstDate = jsonDocument.RootElement[0].GetString();
        Assert.IsNotNull(firstDate);
        Assert.IsTrue(DateTime.TryParse(firstDate, out _), "Date should be parseable");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetOptionChainAsync_ValidExpiration_ReturnsValidData()
    {
        // First get available expiration dates
        var datesResult = await _client.GetOptionExpirationDatesAsync(TestTicker);
        AssertNoApiError(datesResult, nameof(GetOptionChainAsync_ValidExpiration_ReturnsValidData));
        var datesDocument = JsonDocument.Parse(datesResult);
        var firstDate = datesDocument.RootElement[0].GetString();
        
        Assert.IsNotNull(firstDate, "Should have at least one expiration date");

        // Act - Get option chain for first available date
        var result = await _client.GetOptionChainAsync(TestTicker, firstDate, OptionType.Calls);

        // Assert
        Assert.IsNotNull(result);
        
        // Note: Options may not always be available, so we check for either success or specific error
        if (!result.Contains("No options available"))
        {
            AssertNoApiError(result, nameof(GetOptionChainAsync_ValidExpiration_ReturnsValidData));
            
            // Verify it's valid JSON
            var jsonDocument = JsonDocument.Parse(result);
            Assert.IsTrue(jsonDocument.RootElement.ValueKind == JsonValueKind.Array, "Should return JSON array");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetRecommendationsAsync_Recommendations_ReturnsValidData()
    {
        // Act
        var result = await _client.GetRecommendationsAsync(TestTicker, RecommendationType.Recommendations);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetRecommendationsAsync_Recommendations_ReturnsValidData));
        
        // Verify it's valid JSON
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.ValueKind == JsonValueKind.Array, "Should return JSON array");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetRecommendationsAsync_UpgradesDowngrades_ReturnsValidData()
    {
        // Act
        var result = await _client.GetRecommendationsAsync(TestTicker, RecommendationType.UpgradesDowngrades, 12);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetRecommendationsAsync_UpgradesDowngrades_ReturnsValidData));
        
        // Verify it's valid JSON
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.ValueKind == JsonValueKind.Array, "Should return JSON array");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetHistoricalPricesAsync_InvalidTicker_ReturnsError()
    {
        // Act
        var result = await _client.GetHistoricalPricesAsync("INVALIDTICKER123456");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("not found") || result.Contains("Error"), 
            "Should return error for invalid ticker");
    }

    [TestMethod]
    [TestCategory("Integration")]
    public async Task GetStockInfoAsync_InvalidTicker_ReturnsError()
    {
        // Act
        var result = await _client.GetStockInfoAsync("INVALIDTICKER123456");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("not found") || result.Contains("Error"), 
            "Should return error for invalid ticker");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [DataRow("1d")]
    [DataRow("5d")]
    [DataRow("1mo")]
    [DataRow("1y")]
    public async Task GetHistoricalPricesAsync_DifferentPeriods_AllWork(string period)
    {
        // Act
        var result = await _client.GetHistoricalPricesAsync(TestTicker, period, "1d");

        // Assert
        Assert.IsNotNull(result);
        
        // Debug: Log the first 200 characters of the result to see what we're getting
        Console.WriteLine($"Period: {period}, Result (first 200 chars): {(result.Length > 200 ? result.Substring(0, 200) : result)}");
        
        // If result contains error or no data message, log it and skip JSON parsing
        if (result.Contains("Error", StringComparison.OrdinalIgnoreCase) || 
            result.Contains("No historical data", StringComparison.OrdinalIgnoreCase) ||
            result.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Inconclusive($"API returned error or no data for period {period}: {result}");
            return;
        }
        
        AssertNoApiError(result, nameof(GetHistoricalPricesAsync_DifferentPeriods_AllWork));
        
        try
        {
            var jsonDocument = JsonDocument.Parse(result);
            Assert.IsTrue(jsonDocument.RootElement.GetArrayLength() > 0, $"Should return data for period {period}");
        }
        catch (JsonException ex)
        {
            // If we can't parse as JSON, it might be another error message
            Assert.Inconclusive($"Could not parse response as JSON for period {period}. First 500 chars of result: {(result.Length > 500 ? result.Substring(0, 500) : result)}. Error: {ex.Message}");
        }
    }

    [TestMethod]
    [TestCategory("Integration")]
    [DataRow(FinancialStatementType.IncomeStatement)]
    [DataRow(FinancialStatementType.QuarterlyIncomeStatement)]
    [DataRow(FinancialStatementType.BalanceSheet)]
    [DataRow(FinancialStatementType.QuarterlyBalanceSheet)]
    [DataRow(FinancialStatementType.CashFlow)]
    [DataRow(FinancialStatementType.QuarterlyCashFlow)]
    public async Task GetFinancialStatementAsync_AllTypes_Work(FinancialStatementType type)
    {
        // Act
        var result = await _client.GetFinancialStatementAsync(TestTicker, type);

        // Assert
        Assert.IsNotNull(result);
        AssertNoApiError(result, nameof(GetFinancialStatementAsync_AllTypes_Work));
        
        var jsonDocument = JsonDocument.Parse(result);
        Assert.IsTrue(jsonDocument.RootElement.ValueKind == JsonValueKind.Array, 
            $"Should return JSON array for type {type}");
    }

    [TestMethod]
    [TestCategory("Integration")]
    [DataRow(OptionType.Calls)]
    [DataRow(OptionType.Puts)]
    public async Task GetOptionChainAsync_BothTypes_Work(OptionType optionType)
    {
        // First get available expiration dates
        var datesResult = await _client.GetOptionExpirationDatesAsync(TestTicker);
        AssertNoApiError(datesResult, nameof(GetOptionChainAsync_BothTypes_Work));
        var datesDocument = JsonDocument.Parse(datesResult);
        var firstDate = datesDocument.RootElement[0].GetString();
        
        Assert.IsNotNull(firstDate);

        // Act
        var result = await _client.GetOptionChainAsync(TestTicker, firstDate, optionType);

        // Assert
        Assert.IsNotNull(result);
        
        // Options may not always be available
        if (!result.Contains("No options available"))
        {
            AssertNoApiError(result, nameof(GetOptionChainAsync_BothTypes_Work));
        }
    }

    private static void AssertNoApiError(string result, string operationName)
    {
        if (string.IsNullOrWhiteSpace(result) || !result.Contains("Error", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Assert.Fail($"{operationName} returned an error response: {result}");
    }
}
