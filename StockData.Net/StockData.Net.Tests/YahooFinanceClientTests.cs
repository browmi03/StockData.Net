using System.Net;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using StockData.Net;
using StockData.Net.Models;

namespace StockData.Net.Tests;

[TestClass]
public class YahooFinanceClientTests
{
    private Mock<HttpMessageHandler> _mockHttpHandler;
    private HttpClient _httpClient;
    private YahooFinanceClient _client;

    [TestInitialize]
    public void Setup()
    {
        _mockHttpHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _client = new YahooFinanceClient(_httpClient);
    }

    [TestCleanup]
    public void Cleanup()
    {
        _httpClient?.Dispose();
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_ValidTicker_ReturnsJsonData()
    {
        // Arrange
        var mockResponse = new
        {
            chart = new
            {
                result = new[]
                {
                    new
                    {
                        timestamp = new[] { 1609459200L, 1609545600L },
                        indicators = new
                        {
                            quote = new[]
                            {
                                new
                                {
                                    open = new double?[] { 100.0, 101.0 },
                                    high = new double?[] { 105.0, 106.0 },
                                    low = new double?[] { 99.0, 100.5 },
                                    close = new double?[] { 102.0, 103.0 },
                                    volume = new long?[] { 1000000L, 1100000L }
                                }
                            },
                            adjclose = new[]
                            {
                                new
                                {
                                    adjclose = new double?[] { 102.0, 103.0 }
                                }
                            }
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetHistoricalPricesAsync("AAPL", "1mo", "1d");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("\"Open\":100"));
        Assert.IsTrue(result.Contains("\"Close\":102"));
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_InvalidTicker_ReturnsErrorMessage()
    {
        // Arrange
        var mockResponse = new { chart = new { result = (object[]?)null } };
        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetHistoricalPricesAsync("INVALID", "1mo", "1d");

        // Assert
        Assert.IsTrue(result.Contains("not found"));
    }

    [TestMethod]
    public async Task GetStockInfoAsync_ValidTicker_ReturnsJsonData()
    {
        // Arrange
        var mockResponse = new
        {
            quoteSummary = new
            {
                result = new[]
                {
                    new
                    {
                        price = new
                        {
                            regularMarketPrice = new { raw = 150.0, fmt = "150.00" },
                            currency = new { raw = "USD", fmt = "USD" }
                        },
                        summaryDetail = new
                        {
                            previousClose = new { raw = 149.0, fmt = "149.00" },
                            open = new { raw = 150.5, fmt = "150.50" }
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetStockInfoAsync("AAPL");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("regularMarketPrice"));
    }

    [TestMethod]
    public async Task GetNewsAsync_ValidTicker_ReturnsNewsArticles()
    {
        // Arrange
        var mockResponse = new
        {
            news = new[]
            {
                new
                {
                    title = "Apple announces new product",
                    publisher = "Tech News",
                    link = "https://example.com/article1",
                    providerPublishTime = 1609459200L
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetNewsAsync("AAPL");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("Apple announces new product"));
        Assert.IsTrue(result.Contains("Tech News"));
    }

    [TestMethod]
    public async Task GetNewsAsync_NoNewsAvailable_ReturnsNoNewsMessage()
    {
        // Arrange
        var mockResponse = new { news = Array.Empty<object>() };
        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetNewsAsync("AAPL");

        // Assert
        Assert.IsTrue(result.Contains("No news found"));
    }

    [TestMethod]
    public async Task GetStockActionsAsync_ValidTicker_ReturnsDividendsAndSplits()
    {
        // Arrange
        var dividendsResponse = new
        {
            chart = new
            {
                result = new[]
                {
                    new
                    {
                        events = new
                        {
                            dividends = new Dictionary<string, object>
                            {
                                ["1609459200"] = new { date = 1609459200L, amount = 0.25 }
                            }
                        }
                    }
                }
            }
        };

        var splitsResponse = new
        {
            chart = new
            {
                result = new[]
                {
                    new
                    {
                        events = new
                        {
                            splits = new Dictionary<string, object>
                            {
                                ["1609459200"] = new { date = 1609459200L, numerator = 2.0, denominator = 1.0 }
                            }
                        }
                    }
                }
            }
        };

        var callCount = 0;
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var response = callCount == 1 
                    ? JsonSerializer.Serialize(dividendsResponse)
                    : JsonSerializer.Serialize(splitsResponse);
                
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(response)
                };
            });

        // Act
        var result = await _client.GetStockActionsAsync("AAPL");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("Dividends") || result.Contains("StockSplits"));
    }

    [TestMethod]
    public async Task GetFinancialStatementAsync_IncomeStatement_ReturnsFinancialData()
    {
        // Arrange
        var mockResponse = new
        {
            quoteSummary = new
            {
                result = new[]
                {
                    new
                    {
                        incomeStatementHistory = new
                        {
                            incomeStatementHistory = new[]
                            {
                                new
                                {
                                    endDate = new { fmt = DateTime.Now.AddYears(-1).ToString("yyyy-MM-dd") },
                                    totalRevenue = new { raw = 1000000000L },
                                    netIncome = new { raw = 100000000L }
                                }
                            }
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetFinancialStatementAsync("AAPL", FinancialStatementType.IncomeStatement);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("date") || result.Contains("Revenue") || result.Contains("[]"));
    }

    [TestMethod]
    public async Task GetHolderInfoAsync_InstitutionalHolders_ReturnsHolderData()
    {
        // Arrange
        var mockResponse = new
        {
            quoteSummary = new
            {
                result = new[]
                {
                    new
                    {
                        institutionOwnership = new
                        {
                            ownershipList = new[]
                            {
                                new
                                {
                                    organization = "Vanguard Group",
                                    pctHeld = new { raw = 0.08, fmt = "8.00%" }
                                }
                            }
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetHolderInfoAsync("AAPL", HolderType.InstitutionalHolders);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("Vanguard") || result.Contains("organization"));
    }

    [TestMethod]
    public async Task GetOptionExpirationDatesAsync_ValidTicker_ReturnsDatesList()
    {
        // Arrange
        var mockResponse = new
        {
            optionChain = new
            {
                result = new[]
                {
                    new
                    {
                        expirationDates = new[] { 1640995200L, 1643587200L }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetOptionExpirationDatesAsync("AAPL");

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("2022") || result.Contains("2021"));
    }

    [TestMethod]
    public async Task GetOptionChainAsync_ValidParameters_ReturnsOptionsData()
    {
        // Arrange
        var mockResponse = new
        {
            optionChain = new
            {
                result = new[]
                {
                    new
                    {
                        options = new[]
                        {
                            new
                            {
                                calls = new[]
                                {
                                    new
                                    {
                                        strike = new { raw = 150.0 },
                                        lastPrice = new { raw = 5.0 },
                                        bid = new { raw = 4.9 },
                                        ask = new { raw = 5.1 }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var expirationDate = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd");
        var result = await _client.GetOptionChainAsync("AAPL", expirationDate, OptionType.Calls);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("strike") || result.Contains("150"));
    }

    [TestMethod]
    public async Task GetRecommendationsAsync_Recommendations_ReturnsAnalystData()
    {
        // Arrange
        var mockResponse = new
        {
            quoteSummary = new
            {
                result = new[]
                {
                    new
                    {
                        recommendationTrend = new
                        {
                            trend = new[]
                            {
                                new
                                {
                                    period = "0m",
                                    strongBuy = 10,
                                    buy = 15,
                                    hold = 5,
                                    sell = 1,
                                    strongSell = 0
                                }
                            }
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetRecommendationsAsync("AAPL", RecommendationType.Recommendations);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("strongBuy") || result.Contains("buy"));
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_HttpException_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _client.GetHistoricalPricesAsync("AAPL");

        // Assert
        Assert.IsTrue(result.Contains("Error"));
        Assert.IsTrue(result.Contains("Network error"));
    }

    [TestMethod]
    [DataRow("1d")]
    [DataRow("5d")]
    [DataRow("1mo")]
    [DataRow("3mo")]
    [DataRow("6mo")]
    [DataRow("1y")]
    [DataRow("2y")]
    [DataRow("5y")]
    [DataRow("10y")]
    [DataRow("ytd")]
    [DataRow("max")]
    public async Task GetHistoricalPricesAsync_DifferentPeriods_AcceptsAllValidPeriods(string period)
    {
        // Arrange
        var mockResponse = new
        {
            chart = new
            {
                result = new[]
                {
                    new
                    {
                        timestamp = new[] { 1609459200L },
                        indicators = new
                        {
                            quote = new[]
                            {
                                new
                                {
                                    open = new double?[] { 100.0 },
                                    high = new double?[] { 105.0 },
                                    low = new double?[] { 99.0 },
                                    close = new double?[] { 102.0 },
                                    volume = new long?[] { 1000000L }
                                }
                            },
                            adjclose = new[]
                            {
                                new
                                {
                                    adjclose = new double?[] { 102.0 }
                                }
                            }
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetHistoricalPricesAsync("AAPL", period);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsFalse(result.Contains("Error"));
    }

    [TestMethod]
    public async Task GetOptionChainAsync_InvalidDateFormat_ReturnsError()
    {
        // Act
        var result = await _client.GetOptionChainAsync("AAPL", "invalid-date", OptionType.Calls);

        // Assert
        Assert.IsTrue(result.Contains("Error") || result.Contains("Invalid"));
    }

    [TestMethod]
    public async Task GetMarketNewsAsync_Success_ReturnsMarketNews()
    {
        // Arrange
        var mockResponse = new
        {
            finance = new
            {
                result = new[]
                {
                    new
                    {
                        quotes = new[]
                        {
                            new { symbol = "AAPL" },
                            new { symbol = "MSFT" },
                            new { symbol = "GOOGL" }
                        }
                    }
                }
            }
        };

        var newsResponse = new
        {
            news = new[]
            {
                new
                {
                    title = "Market Update",
                    publisher = "Bloomberg",
                    link = "https://example.com/news1",
                    providerPublishTime = 1609459200L,
                    relatedTickers = new[] { "AAPL", "MSFT" }
                }
            }
        };

        var callCount = 0;
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var response = callCount == 1
                    ? JsonSerializer.Serialize(mockResponse)
                    : JsonSerializer.Serialize(newsResponse);

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(response)
                };
            });

        // Act
        var result = await _client.GetMarketNewsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue( result.Contains("Market Update") || result.Contains("Title"));
    }

    [TestMethod]
    public async Task GetMarketNewsAsync_NoTrendingQuotes_FallsBackToGeneralNews()
    {
        // Arrange
        var trendingResponse = new
        {
            finance = new
            {
                result = new[]
                {
                    new
                    {
                        quotes = Array.Empty<object>()
                    }
                }
            }
        };

        var newsResponse = new
        {
            news = new[]
            {
                new
                {
                    title = "General Market News",
                    publisher = "Reuters",
                    link = "https://example.com/news",
                    providerPublishTime = 1609459200L
                }
            }
        };

        var callCount = 0;
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                var response = callCount == 1
                    ? JsonSerializer.Serialize(trendingResponse)
                    : JsonSerializer.Serialize(newsResponse);

                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(response)
                };
            });

        // Act
        var result = await _client.GetMarketNewsAsync();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("General Market News") || result.Contains("Title"));
    }

    [TestMethod]
    public async Task GetMarketNewsAsync_HttpException_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection error"));

        // Act
        var result = await _client.GetMarketNewsAsync();

        // Assert
        Assert.IsTrue(result.Contains("Error"));
        Assert.IsTrue(result.Contains("market news") || result.Contains("Connection error"));
    }

    [TestMethod]
    public async Task GetStockInfoAsync_HttpException_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Timeout"));

        // Act
        var result = await _client.GetStockInfoAsync("AAPL");

        // Assert
        Assert.IsTrue(result.Contains("Error"));
        Assert.IsTrue(result.Contains("AAPL"));
    }

    [TestMethod]
    public async Task GetNewsAsync_HttpException_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network failure"));

        // Act
        var result = await _client.GetNewsAsync("GOOGL");

        // Assert
        Assert.IsTrue(result.Contains("Error"));
        Assert.IsTrue(result.Contains("GOOGL") || result.Contains("news"));
    }

    [TestMethod]
    public async Task GetStockActionsAsync_HttpException_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service unavailable"));

        // Act
        var result = await _client.GetStockActionsAsync("TSLA");

        // Assert
        Assert.IsTrue(result.Contains("Error"));
        Assert.IsTrue(result.Contains("TSLA") || result.Contains("stock actions"));
    }

    [TestMethod]
    public async Task GetFinancialStatementAsync_HttpException_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Internal server error"));

        // Act
        var result = await _client.GetFinancialStatementAsync("AMD", FinancialStatementType.BalanceSheet);

        // Assert
        Assert.IsTrue(result.Contains("Error"));
        Assert.IsTrue(result.Contains("AMD"));
    }

    [TestMethod]
    public async Task GetHolderInfoAsync_HttpException_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection timeout"));

        // Act
        var result = await _client.GetHolderInfoAsync("NFLX", HolderType.MajorHolders);

        // Assert
        Assert.IsTrue(result.Contains("Error"));
        Assert.IsTrue(result.Contains("NFLX"));
    }

    [TestMethod]
    public async Task GetOptionExpirationDatesAsync_HttpException_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Bad gateway"));

        // Act
        var result = await _client.GetOptionExpirationDatesAsync("INTC");

        // Assert
        Assert.IsTrue(result.Contains("Error"));
        Assert.IsTrue(result.Contains("INTC"));
    }

    [TestMethod]
    public async Task GetOptionChainAsync_HttpException_ReturnsErrorMessage()
    {
        // Arrange
        var expirationDate = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd");
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Gateway timeout"));

        // Act
        var result = await _client.GetOptionChainAsync("META", expirationDate, OptionType.Puts);

        // Assert
        Assert.IsTrue(result.Contains("Error"));
        Assert.IsTrue(result.Contains("META"));
    }

    [TestMethod]
    public async Task GetRecommendationsAsync_HttpException_ReturnsErrorMessage()
    {
        // Arrange
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Service error"));

        // Act
        var result = await _client.GetRecommendationsAsync("NVDA", RecommendationType.UpgradesDowngrades);

        // Assert
        Assert.IsTrue(result.Contains("Error"));
        Assert.IsTrue(result.Contains("NVDA"));
    }

    [TestMethod]
    public async Task GetRecommendationsAsync_UpgradesDowngrades_FiltersAndSorts()
    {
        // Arrange
        var currentDate = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var oldDate = DateTimeOffset.UtcNow.AddMonths(-24).ToUnixTimeSeconds();

        var mockResponse = new
        {
            quoteSummary = new
            {
                result = new[]
                {
                    new
                    {
                        upgradeDowngradeHistory = new
                        {
                            history = new[]
                            {
                                new
                                {
                                    epochGradeDate = currentDate,
                                    firm = "Goldman Sachs",
                                    toGrade = "Buy",
                                    fromGrade = "Neutral",
                                    action = "up"
                                },
                                new
                                {
                                    epochGradeDate = oldDate,
                                    firm = "Morgan Stanley",
                                    toGrade = "Sell",
                                    fromGrade = "Hold",
                                    action = "down"
                                }
                            }
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetRecommendationsAsync("AAPL", RecommendationType.UpgradesDowngrades, 12);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("Goldman Sachs")); // Recent should be included
        Assert.IsFalse(result.Contains("Morgan Stanley")); // Old should be filtered out
    }

    [TestMethod]
    public async Task GetStockInfoAsync_InvalidTicker_ReturnsNotFound()
    {
        // Arrange
        var mockResponse = new { quoteSummary = new { result = (object[]?)null } };
        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetStockInfoAsync("INVALID123");

        // Assert
        Assert.IsTrue(result.Contains("not found"));
    }

    [TestMethod]
    public async Task GetFinancialStatementAsync_InvalidTicker_ReturnsNotFound()
    {
        // Arrange
        var mockResponse = new { quoteSummary = new { result = (object[]?)null } };
        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetFinancialStatementAsync("INVALID", FinancialStatementType.CashFlow);

        // Assert
        Assert.IsTrue(result.Contains("not found"));
    }

    [TestMethod]
    public async Task GetHolderInfoAsync_InvalidTicker_ReturnsNotFound()
    {
        // Arrange
        var mockResponse = new { quoteSummary = new { result = (object[]?)null } };
        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetHolderInfoAsync("BADTICKER", HolderType.MajorHolders);

        // Assert
        Assert.IsTrue(result.Contains("not found"));
    }

    [TestMethod]
    public async Task GetOptionExpirationDatesAsync_InvalidTicker_ReturnsNotFound()
    {
        // Arrange
        var mockResponse = new { optionChain = new { result = (object[]?)null } };
        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetOptionExpirationDatesAsync("NOTREAL");

        // Assert
        Assert.IsTrue(result.Contains("not found"));
    }

    [TestMethod]
    public async Task GetOptionChainAsync_InvalidTicker_ReturnsNotFound()
    {
        // Arrange
        var expirationDate = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd");
        var mockResponse = new { optionChain = new { result = (object[]?)null } };
        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetOptionChainAsync("FAKE", expirationDate, OptionType.Calls);

        // Assert
        Assert.IsTrue(result.Contains("not found"));
    }

    [TestMethod]
    public async Task GetRecommendationsAsync_InvalidTicker_ReturnsNotFound()
    {
        // Arrange
        var mockResponse = new { quoteSummary = new { result = (object[]?)null } };
        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetRecommendationsAsync("NOTATICKER", RecommendationType.Recommendations);

        // Assert
        Assert.IsTrue(result.Contains("not found"));
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_NoData_ReturnsNoDataMessage()
    {
        // Arrange
        var mockResponse = new
        {
            chart = new
            {
                result = new[]
                {
                    new
                    {
                        timestamp = (int[]?)null,
                        indicators = new
                        {
                            quote = (object[]?)null
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetHistoricalPricesAsync("AAPL");

        // Assert
        Assert.IsTrue(result.Contains("No historical data"));
    }

    [TestMethod]
    public async Task GetOptionChainAsync_NoOptions_ReturnsNoOptionsMessage()
    {
        // Arrange
        var mockResponse = new
        {
            optionChain = new
            {
                result = new[]
                {
                    new
                    {
                        options = new[]
                        {
                            new
                            {
                                calls = Array.Empty<object>(),
                                puts = Array.Empty<object>()
                            }
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var expirationDate = DateTime.UtcNow.AddMonths(2).ToString("yyyy-MM-dd");
        var result = await _client.GetOptionChainAsync("AAPL", expirationDate, OptionType.Calls);

        // Assert
        Assert.IsTrue(result.Contains("No options available") || result.Contains("GetOptionExpirationDatesAsync"));
    }

    [TestMethod]
    public async Task YahooFinanceClient_ConstructorWithoutHttpClient_CreatesDefaultClient()
    {
        // Act
        var client = new YahooFinanceClient();

        // Assert
        Assert.IsNotNull(client);
    }

    [TestMethod]
    [DataRow(FinancialStatementType.QuarterlyIncomeStatement)]
    [DataRow(FinancialStatementType.QuarterlyBalanceSheet)]
    [DataRow(FinancialStatementType.QuarterlyCashFlow)]
    public async Task GetFinancialStatementAsync_QuarterlyStatements_Success(FinancialStatementType statementType)
    {
        // Arrange
        var mockResponse = new
        {
            quoteSummary = new
            {
                result = new[]
                {
                    new
                    {
                        incomeStatementHistoryQuarterly = new
                        {
                            incomeStatementHistory = new[]
                            {
                                new
                                {
                                    endDate = new { fmt = DateTime.Now.AddYears(-1).ToString("yyyy-MM-dd") },
                                    totalRevenue = new { raw = 50000000L }
                                }
                            }
                        },
                        balanceSheetHistoryQuarterly = new
                        {
                            balanceSheetStatements = new[]
                            {
                                new
                                {
                                    endDate = new { fmt = DateTime.Now.AddYears(-1).ToString("yyyy-MM-dd") },
                                    totalAssets = new { raw = 100000000L }
                                }
                            }
                        },
                        cashflowStatementHistoryQuarterly = new
                        {
                            cashflowStatements = new[]
                            {
                                new
                                {
                                    endDate = new { fmt = DateTime.Now.AddYears(-1).ToString("yyyy-MM-dd") },
                                    operatingCashflow = new { raw = 20000000L }
                                }
                            }
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetFinancialStatementAsync("AAPL", statementType);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("date") || result.Contains("2023"));
    }

    [TestMethod]
    public async Task GetHolderInfoAsync_MajorHolders_ReturnsMetricValuePairs()
    {
        // Arrange
        var mockResponse = new
        {
            quoteSummary = new
            {
                result = new[]
                {
                    new
                    {
                        majorHoldersBreakdown = new
                        {
                            insidersPercentHeld = new { raw = 0.05, fmt = "5.00%" },
                            institutionsPercentHeld = new { raw = 0.60, fmt = "60.00%" }
                        }
                    }
                }
            }
        };

        SetupMockHttpResponse(HttpStatusCode.OK, JsonSerializer.Serialize(mockResponse));

        // Act
        var result = await _client.GetHolderInfoAsync("AAPL", HolderType.MajorHolders);

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.Contains("insidersPercentHeld") || result.Contains("metric"));
    }

    private void SetupMockHttpResponse(HttpStatusCode statusCode, string content)
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
    }
}
