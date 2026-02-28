using System.Globalization;
using System.Net.Http.Json;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StockData.Net.Models;

namespace StockData.Net;

/// <summary>
/// Client for accessing Yahoo Finance data
/// </summary>
public class YahooFinanceClient : IYahooFinanceClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly bool _supportsCookies;
    private string? _crumb;
    private readonly SemaphoreSlim _authLock = new(1, 1);
    private const string BaseUrl = "https://query2.finance.yahoo.com";
    private const string CookieUrl = "https://fc.yahoo.com";
    private const string CrumbUrl = "https://query2.finance.yahoo.com/v1/test/getcrumb";
    private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

    public YahooFinanceClient(HttpClient? httpClient = null)
    {
        if (httpClient == null)
        {
            var cookieContainer = new CookieContainer();

            // Create HttpClientHandler with security hardening
            var handler = new HttpClientHandler
            {
                // Enforce TLS 1.2 and 1.3
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                MaxResponseHeadersLength = 64, // KB
                UseCookies = true,
                CookieContainer = cookieContainer
            };

            _httpClient = new HttpClient(handler)
            {
                // Set 30-second timeout
                Timeout = TimeSpan.FromSeconds(30),
                // Set 10MB max response buffer size
                MaxResponseContentBufferSize = 10 * 1024 * 1024
            };
            _supportsCookies = true;
        }
        else
        {
            _httpClient = httpClient;
            _supportsCookies = false; // Injected client may not support cookies
        }

        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Acquires Yahoo Finance session cookies and crumb token for authenticated API access.
    /// </summary>
    private async Task EnsureAuthenticatedAsync(CancellationToken cancellationToken)
    {
        if (_crumb != null) return;
        if (!_supportsCookies) return;

        await _authLock.WaitAsync(cancellationToken);
        try
        {
            if (_crumb != null) return; // Double-check after acquiring lock

            // Step 1: GET fc.yahoo.com to acquire session cookies (response status may be 404, that's OK)
            try
            {
                var cookieResponse = await _httpClient.GetAsync(CookieUrl, cancellationToken);
                // Ignore status code — cookies are set regardless
            }
            catch (HttpRequestException)
            {
                // Ignore — the important thing is the cookies were set
            }

            // Step 2: GET crumb endpoint with the cookies
            var crumbResponse = await _httpClient.GetAsync(CrumbUrl, cancellationToken);
            crumbResponse.EnsureSuccessStatusCode();
            var crumb = await crumbResponse.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(crumb))
            {
                throw new InvalidOperationException("Yahoo Finance returned an empty crumb token.");
            }

            _crumb = crumb;
        }
        finally
        {
            _authLock.Release();
        }
    }

    /// <summary>
    /// Appends the crumb query parameter to a URL.
    /// </summary>
    private string AppendCrumb(string url)
    {
        if (_crumb == null) return url;
        var separator = url.Contains('?') ? '&' : '?';
        return $"{url}{separator}crumb={Uri.EscapeDataString(_crumb)}";
    }

    /// <summary>
    /// Sends a GET request with crumb authentication and automatic 401 retry.
    /// </summary>
    private async Task<HttpResponseMessage> AuthenticatedGetAsync(string url, CancellationToken cancellationToken)
    {
        await EnsureAuthenticatedAsync(cancellationToken);
        var response = await _httpClient.GetAsync(AppendCrumb(url), cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Crumb may have expired — clear and re-authenticate once
            _crumb = null;
            await EnsureAuthenticatedAsync(cancellationToken);
            response = await _httpClient.GetAsync(AppendCrumb(url), cancellationToken);
        }

        return response;
    }

    public async Task<string> GetHistoricalPricesAsync(string ticker, string period = "1mo", string interval = "1d", CancellationToken cancellationToken = default)
    {
        try
        {
            var (startTime, endTime) = GetTimeRange(period);
            var url = $"{BaseUrl}/v8/finance/chart/{ticker}?interval={interval}&period1={startTime}&period2={endTime}";
            
            var response = await AuthenticatedGetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonData = JsonNode.Parse(content);
            
            var result = jsonData?["chart"]?["result"]?[0];
            if (result == null)
            {
                return $"Company ticker {ticker} not found.";
            }

            var timestamps = result["timestamp"]?.AsArray();
            var quotes = result["indicators"]?["quote"]?[0];
            var adjClose = result["indicators"]?["adjclose"]?[0]?["adjclose"]?.AsArray();

            if (timestamps == null || quotes == null)
            {
                return $"No historical data available for {ticker}";
            }

            var historicalData = new List<object>();
            for (int i = 0; i < timestamps.Count; i++)
            {
                var timestamp = timestamps[i]?.GetValue<long>() ?? 0;
                var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;

                historicalData.Add(new
                {
                    Date = date.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Open = quotes["open"]?[i]?.GetValue<double?>(),
                    High = quotes["high"]?[i]?.GetValue<double?>(),
                    Low = quotes["low"]?[i]?.GetValue<double?>(),
                    Close = quotes["close"]?[i]?.GetValue<double?>(),
                    Volume = quotes["volume"]?[i]?.GetValue<long?>(),
                    AdjClose = adjClose?[i]?.GetValue<double?>()
                });
            }

            return JsonSerializer.Serialize(historicalData, _jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: getting historical stock prices for {ticker}: {ex.Message}";
        }
    }

    public async Task<string> GetStockInfoAsync(string ticker, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseUrl}/v10/finance/quoteSummary/{ticker}?modules=summaryDetail,price,defaultKeyStatistics,financialData,calendarEvents,summaryProfile,recommendationTrend,earnings,earningsHistory,earningsTrend,industryTrend,indexTrend,sectorTrend,esgScores,incomeStatementHistory,cashflowStatementHistory,balanceSheetHistory,netSharePurchaseActivity";
            
            var response = await AuthenticatedGetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonData = JsonNode.Parse(content);
            
            var result = jsonData?["quoteSummary"]?["result"]?[0];
            if (result == null)
            {
                return $"Company ticker {ticker} not found.";
            }

            // Flatten the nested structure for easier consumption
            var info = new Dictionary<string, object?>();
            foreach (var module in result.AsObject())
            {
                if (module.Value is JsonObject moduleObj)
                {
                    foreach (var prop in moduleObj)
                    {
                        var key = $"{module.Key}_{prop.Key}";
                        info[key] = ExtractValue(prop.Value);
                    }
                }
            }

            return JsonSerializer.Serialize(info, _jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: getting stock information for {ticker}: {ex.Message}";
        }
    }

    public async Task<string> GetNewsAsync(string ticker, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseUrl}/v1/finance/search?q={ticker}&newsCount=10";
            
            var response = await AuthenticatedGetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonData = JsonNode.Parse(content);
            
            var news = jsonData?["news"]?.AsArray();
            if (news == null || news.Count == 0)
            {
                return $"No news found for company that searched with {ticker} ticker.";
            }

            var newsList = new List<string>();
            foreach (var item in news)
            {
                var title = item?["title"]?.GetValue<string>() ?? "";
                var publisher = item?["publisher"]?.GetValue<string>() ?? "";
                var link = item?["link"]?.GetValue<string>() ?? "";
                var providerPublishTime = item?["providerPublishTime"]?.GetValue<long>();
                
                var publishTime = providerPublishTime.HasValue 
                    ? DateTimeOffset.FromUnixTimeSeconds(providerPublishTime.Value).DateTime.ToString("yyyy-MM-dd HH:mm:ss")
                    : "Unknown";

                newsList.Add($"Title: {title}\nPublisher: {publisher}\nPublished: {publishTime}\nURL: {link}");
            }

            return string.Join("\n\n", newsList);
        }
        catch (Exception ex)
        {
            return $"Error: getting news for {ticker}: {ex.Message}";
        }
    }

    public async Task<string> GetMarketNewsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Use trending endpoint for general market news
            var url = $"{BaseUrl}/v1/finance/trending/US?count=20";
            
            var response = await AuthenticatedGetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonData = JsonNode.Parse(content);
            
            var quotes = jsonData?["finance"]?["result"]?[0]?["quotes"]?.AsArray();
            
            if (quotes == null || quotes.Count == 0)
            {
                // Fallback: get general financial news
                return await GetGeneralFinancialNewsAsync(cancellationToken);
            }

            // Extract tickers from trending quotes for news context
            var tickers = quotes
                .Take(5) // Get top 5 trending tickers
                .Select(q => q?["symbol"]?.GetValue<string>())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            // Get news for trending tickers to provide market context
            var newsUrl = $"{BaseUrl}/v1/finance/search?q={string.Join(" ", tickers)}&newsCount=15";
            var newsResponse = await AuthenticatedGetAsync(newsUrl, cancellationToken);
            newsResponse.EnsureSuccessStatusCode();
            
            var newsContent = await newsResponse.Content.ReadAsStringAsync(cancellationToken);
            var newsData = JsonNode.Parse(newsContent);
            
            var news = newsData?["news"]?.AsArray();
            if (news == null || news.Count == 0)
            {
                return "No general market news available at this time.";
            }

            var newsList = new List<string>();
            foreach (var item in news)
            {
                var title = item?["title"]?.GetValue<string>() ?? "";
                var publisher = item?["publisher"]?.GetValue<string>() ?? "";
                var link = item?["link"]?.GetValue<string>() ?? "";
                var providerPublishTime = item?["providerPublishTime"]?.GetValue<long>();
                
                var publishTime = providerPublishTime.HasValue 
                    ? DateTimeOffset.FromUnixTimeSeconds(providerPublishTime.Value).DateTime.ToString("yyyy-MM-dd HH:mm:ss")
                    : "Unknown";

                // Extract related tickers if available
                var relatedTickers = item?["relatedTickers"]?.AsArray()
                    ?.Select(t => t?.GetValue<string>())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList() ?? new List<string?>();

                var tickerInfo = relatedTickers.Count > 0 
                    ? $"\nRelated Tickers: {string.Join(", ", relatedTickers!)}"
                    : "";

                newsList.Add($"Title: {title}\nPublisher: {publisher}\nPublished: {publishTime}{tickerInfo}\nURL: {link}");
            }

            return string.Join("\n\n", newsList);
        }
        catch (Exception ex)
        {
            return $"Error: getting market news: {ex.Message}";
        }
    }

    private async Task<string> GetGeneralFinancialNewsAsync(CancellationToken cancellationToken)
    {
        // Fallback: search for general market/financial terms
        var searchTerms = new[] { "market", "stocks", "economy", "S&P", "nasdaq" };
        var url = $"{BaseUrl}/v1/finance/search?q={string.Join(" ", searchTerms)}&newsCount=15";
        
        var response = await AuthenticatedGetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonData = JsonNode.Parse(content);
        
        var news = jsonData?["news"]?.AsArray();
        if (news == null || news.Count == 0)
        {
            return "No general market news available at this time.";
        }

        var newsList = new List<string>();
        foreach (var item in news)
        {
            var title = item?["title"]?.GetValue<string>() ?? "";
            var publisher = item?["publisher"]?.GetValue<string>() ?? "";
            var link = item?["link"]?.GetValue<string>() ?? "";
            var providerPublishTime = item?["providerPublishTime"]?.GetValue<long>();
            
            var publishTime = providerPublishTime.HasValue 
                ? DateTimeOffset.FromUnixTimeSeconds(providerPublishTime.Value).DateTime.ToString("yyyy-MM-dd HH:mm:ss")
                : "Unknown";

            var relatedTickers = item?["relatedTickers"]?.AsArray()
                ?.Select(t => t?.GetValue<string>())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList() ?? new List<string?>();

            var tickerInfo = relatedTickers.Count > 0 
                ? $"\nRelated Tickers: {string.Join(", ", relatedTickers!)}"
                : "";

            newsList.Add($"Title: {title}\nPublisher: {publisher}\nPublished: {publishTime}{tickerInfo}\nURL: {link}");
        }

        return string.Join("\n\n", newsList);
    }

    public async Task<string> GetStockActionsAsync(string ticker, CancellationToken cancellationToken = default)
    {
        try
        {
            await EnsureAuthenticatedAsync(cancellationToken);

            // Get dividends
            var dividendsUrl = AppendCrumb($"{BaseUrl}/v8/finance/chart/{ticker}?period1=0&period2={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}&interval=1d&events=div");
            var splitsUrl = AppendCrumb($"{BaseUrl}/v8/finance/chart/{ticker}?period1=0&period2={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}&interval=1d&events=split");
            
            var dividendsTask = _httpClient.GetStringAsync(dividendsUrl, cancellationToken);
            var splitsTask = _httpClient.GetStringAsync(splitsUrl, cancellationToken);
            
            await Task.WhenAll(dividendsTask, splitsTask);
            
            var dividendsData = JsonNode.Parse(await dividendsTask);
            var splitsData = JsonNode.Parse(await splitsTask);
            
            var actions = new List<object>();
            
            // Process dividends
            var dividends = dividendsData?["chart"]?["result"]?[0]?["events"]?["dividends"]?.AsObject();
            if (dividends != null)
            {
                foreach (var div in dividends)
                {
                    var timestamp = div.Value?["date"]?.GetValue<long>() ?? 0;
                    var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                    var amount = div.Value?["amount"]?.GetValue<double>() ?? 0;
                    
                    actions.Add(new
                    {
                        Date = date.ToString("yyyy-MM-dd"),
                        Dividends = amount,
                        StockSplits = (double?)null
                    });
                }
            }
            
            // Process splits
            var splits = splitsData?["chart"]?["result"]?[0]?["events"]?["splits"]?.AsObject();
            if (splits != null)
            {
                foreach (var split in splits)
                {
                    var timestamp = split.Value?["date"]?.GetValue<long>() ?? 0;
                    var date = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime;
                    var numerator = split.Value?["numerator"]?.GetValue<double>() ?? 1;
                    var denominator = split.Value?["denominator"]?.GetValue<double>() ?? 1;
                    var ratio = numerator / denominator;
                    
                    actions.Add(new
                    {
                        Date = date.ToString("yyyy-MM-dd"),
                        Dividends = (double?)null,
                        StockSplits = ratio
                    });
                }
            }

            return JsonSerializer.Serialize(actions.OrderBy(a => ((dynamic)a).Date), _jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: getting stock actions for {ticker}: {ex.Message}";
        }
    }

    public async Task<string> GetFinancialStatementAsync(string ticker, FinancialStatementType statementType, CancellationToken cancellationToken = default)
    {
        try
        {
            var (module, isQuarterly) = statementType switch
            {
                FinancialStatementType.IncomeStatement => ("incomeStatementHistory", false),
                FinancialStatementType.QuarterlyIncomeStatement => ("incomeStatementHistoryQuarterly", true),
                FinancialStatementType.BalanceSheet => ("balanceSheetHistory", false),
                FinancialStatementType.QuarterlyBalanceSheet => ("balanceSheetHistoryQuarterly", true),
                FinancialStatementType.CashFlow => ("cashflowStatementHistory", false),
                FinancialStatementType.QuarterlyCashFlow => ("cashflowStatementHistoryQuarterly", true),
                _ => throw new ArgumentException($"Invalid financial statement type: {statementType}")
            };

            var url = $"{BaseUrl}/v10/finance/quoteSummary/{ticker}?modules={module}";
            
            var response = await AuthenticatedGetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonData = JsonNode.Parse(content);
            
            var result = jsonData?["quoteSummary"]?["result"]?[0]?[module];
            if (result == null)
            {
                return $"Company ticker {ticker} not found.";
            }

            // Determine the correct property name based on the statement type
            string propertyName = statementType switch
            {
                FinancialStatementType.IncomeStatement or FinancialStatementType.QuarterlyIncomeStatement => "incomeStatementHistory",
                FinancialStatementType.BalanceSheet or FinancialStatementType.QuarterlyBalanceSheet => "balanceSheetStatements",
                FinancialStatementType.CashFlow or FinancialStatementType.QuarterlyCashFlow => "cashflowStatements",
                _ => "cashflowStatements"
            };

            var statements = result[propertyName]?.AsArray();

            if (statements == null)
            {
                return $"No financial statement data available for {ticker}";
            }

            var resultList = new List<Dictionary<string, object?>>();
            foreach (var statement in statements)
            {
                var dict = new Dictionary<string, object?>();
                var endDate = statement?["endDate"]?["fmt"]?.GetValue<string>() ?? "Unknown";
                dict["date"] = endDate;

                if (statement is JsonObject statementObj)
                {
                    foreach (var prop in statementObj)
                    {
                        if (prop.Key != "endDate" && prop.Key != "maxAge")
                        {
                            dict[prop.Key] = ExtractValue(prop.Value);
                        }
                    }
                }
                
                resultList.Add(dict);
            }

            return JsonSerializer.Serialize(resultList, _jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: getting financial statement for {ticker}: {ex.Message}";
        }
    }

    public async Task<string> GetHolderInfoAsync(string ticker, HolderType holderType, CancellationToken cancellationToken = default)
    {
        try
        {
            var module = holderType switch
            {
                HolderType.MajorHolders => "majorHoldersBreakdown",
                HolderType.InstitutionalHolders => "institutionOwnership",
                HolderType.MutualFundHolders => "fundOwnership",
                HolderType.InsiderTransactions => "insiderTransactions",
                HolderType.InsiderPurchases => "insiderTransactions",
                HolderType.InsiderRosterHolders => "insiderHolders",
                _ => throw new ArgumentException($"Invalid holder type: {holderType}")
            };

            var url = $"{BaseUrl}/v10/finance/quoteSummary/{ticker}?modules={module}";
            
            var response = await AuthenticatedGetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonData = JsonNode.Parse(content);
            
            var result = jsonData?["quoteSummary"]?["result"]?[0]?[module];
            if (result == null)
            {
                return $"Company ticker {ticker} not found.";
            }

            // Handle different structures for different holder types
            if (holderType == HolderType.MajorHolders)
            {
                var majorHolders = new List<object>();
                if (result is JsonObject obj)
                {
                    foreach (var prop in obj)
                    {
                        majorHolders.Add(new
                        {
                            metric = prop.Key,
                            value = ExtractValue(prop.Value)
                        });
                    }
                }
                return JsonSerializer.Serialize(majorHolders, _jsonOptions);
            }
            else
            {
                var holders = result["ownershipList"]?.AsArray() ?? result["transactions"]?.AsArray();
                if (holders == null)
                {
                    return $"No holder information available for {ticker}";
                }

                var holderList = new List<Dictionary<string, object?>>();
                foreach (var holder in holders)
                {
                    var dict = new Dictionary<string, object?>();
                    if (holder is JsonObject holderObj)
                    {
                        foreach (var prop in holderObj)
                        {
                            dict[prop.Key] = ExtractValue(prop.Value);
                        }
                    }
                    holderList.Add(dict);
                }
                
                return JsonSerializer.Serialize(holderList, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            return $"Error: getting holder info for {ticker}: {ex.Message}";
        }
    }

    public async Task<string> GetOptionExpirationDatesAsync(string ticker, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseUrl}/v7/finance/options/{ticker}";
            
            var response = await AuthenticatedGetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonData = JsonNode.Parse(content);
            
            var result = jsonData?["optionChain"]?["result"]?[0];
            if (result == null)
            {
                return $"Company ticker {ticker} not found.";
            }

            var expirationDates = result["expirationDates"]?.AsArray();
            if (expirationDates == null)
            {
                return $"No options data available for {ticker}";
            }

            var dates = expirationDates
                .Select(d => DateTimeOffset.FromUnixTimeSeconds(d?.GetValue<long>() ?? 0).DateTime.ToString("yyyy-MM-dd"))
                .ToList();

            return JsonSerializer.Serialize(dates, _jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: getting option expiration dates for {ticker}: {ex.Message}";
        }
    }

    public async Task<string> GetOptionChainAsync(string ticker, string expirationDate, OptionType optionType, CancellationToken cancellationToken = default)
    {
        try
        {
            // Convert date to Unix timestamp
            if (!DateTime.TryParse(expirationDate, out var date))
            {
                return $"Error: Invalid date format. Please use YYYY-MM-DD format.";
            }

            var timestamp = new DateTimeOffset(date.Date.AddHours(16)).ToUnixTimeSeconds(); // 4 PM ET
            var url = $"{BaseUrl}/v7/finance/options/{ticker}?date={timestamp}";
            
            var response = await AuthenticatedGetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonData = JsonNode.Parse(content);
            
            var result = jsonData?["optionChain"]?["result"]?[0];
            if (result == null)
            {
                return $"Company ticker {ticker} not found.";
            }

            var options = result["options"]?[0];
            var optionArray = optionType == OptionType.Calls 
                ? options?["calls"]?.AsArray() 
                : options?["puts"]?.AsArray();

            if (optionArray == null || optionArray.Count == 0)
            {
                return $"No options available for the date {expirationDate}. You can use GetOptionExpirationDatesAsync to get the available expiration dates.";
            }

            var optionList = new List<Dictionary<string, object?>>();
            foreach (var option in optionArray)
            {
                var dict = new Dictionary<string, object?>();
                if (option is JsonObject optionObj)
                {
                    foreach (var prop in optionObj)
                    {
                        dict[prop.Key] = ExtractValue(prop.Value);
                    }
                }
                optionList.Add(dict);
            }

            return JsonSerializer.Serialize(optionList, _jsonOptions);
        }
        catch (Exception ex)
        {
            return $"Error: getting option chain for {ticker}: {ex.Message}";
        }
    }

    public async Task<string> GetRecommendationsAsync(string ticker, RecommendationType recommendationType, int monthsBack = 12, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseUrl}/v10/finance/quoteSummary/{ticker}?modules=recommendationTrend,upgradeDowngradeHistory";
            
            var response = await AuthenticatedGetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonData = JsonNode.Parse(content);
            
            var result = jsonData?["quoteSummary"]?["result"]?[0];
            if (result == null)
            {
                return $"Company ticker {ticker} not found.";
            }

            if (recommendationType == RecommendationType.Recommendations)
            {
                var trends = result["recommendationTrend"]?["trend"]?.AsArray();
                if (trends == null)
                {
                    return $"No recommendations available for {ticker}";
                }

                var recommendations = new List<Dictionary<string, object?>>();
                foreach (var trend in trends)
                {
                    var dict = new Dictionary<string, object?>();
                    if (trend is JsonObject trendObj)
                    {
                        foreach (var prop in trendObj)
                        {
                            dict[prop.Key] = ExtractValue(prop.Value);
                        }
                    }
                    recommendations.Add(dict);
                }
                
                return JsonSerializer.Serialize(recommendations, _jsonOptions);
            }
            else // UpgradesDowngrades
            {
                var history = result["upgradeDowngradeHistory"]?["history"]?.AsArray();
                if (history == null)
                {
                    return $"No upgrades/downgrades available for {ticker}";
                }

                var cutoffDate = DateTime.UtcNow.AddMonths(-monthsBack);
                var upgrades = new List<Dictionary<string, object?>>();

                foreach (var item in history)
                {
                    var epochTime = item?["epochGradeDate"]?.GetValue<long>();
                    if (epochTime.HasValue)
                    {
                        var gradeDate = DateTimeOffset.FromUnixTimeSeconds(epochTime.Value).DateTime;
                        if (gradeDate >= cutoffDate)
                        {
                            var dict = new Dictionary<string, object?>();
                            if (item is JsonObject itemObj)
                            {
                                foreach (var prop in itemObj)
                                {
                                    dict[prop.Key] = ExtractValue(prop.Value);
                                }
                                dict["GradeDate"] = gradeDate.ToString("yyyy-MM-dd");
                            }
                            upgrades.Add(dict);
                        }
                    }
                }

                // Sort by date descending and get most recent per firm
                var latestByFirm = upgrades
                    .GroupBy(u => u.ContainsKey("firm") ? u["firm"]?.ToString() : null)
                    .Select(g => g.OrderByDescending(u => u.ContainsKey("GradeDate") ? u["GradeDate"] : null).First())
                    .ToList();

                return JsonSerializer.Serialize(latestByFirm, _jsonOptions);
            }
        }
        catch (Exception ex)
        {
            return $"Error: getting recommendations for {ticker}: {ex.Message}";
        }
    }

    // Helper methods
    private static (long startTime, long endTime) GetTimeRange(string period)
    {
        var endTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var startTime = period switch
        {
            "1d" => DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
            "5d" => DateTimeOffset.UtcNow.AddDays(-5).ToUnixTimeSeconds(),
            "1mo" => DateTimeOffset.UtcNow.AddMonths(-1).ToUnixTimeSeconds(),
            "3mo" => DateTimeOffset.UtcNow.AddMonths(-3).ToUnixTimeSeconds(),
            "6mo" => DateTimeOffset.UtcNow.AddMonths(-6).ToUnixTimeSeconds(),
            "1y" => DateTimeOffset.UtcNow.AddYears(-1).ToUnixTimeSeconds(),
            "2y" => DateTimeOffset.UtcNow.AddYears(-2).ToUnixTimeSeconds(),
            "5y" => DateTimeOffset.UtcNow.AddYears(-5).ToUnixTimeSeconds(),
            "10y" => DateTimeOffset.UtcNow.AddYears(-10).ToUnixTimeSeconds(),
            "ytd" => new DateTimeOffset(new DateTime(DateTime.UtcNow.Year, 1, 1)).ToUnixTimeSeconds(),
            "max" => 0,
            _ => DateTimeOffset.UtcNow.AddMonths(-1).ToUnixTimeSeconds()
        };
        
        return (startTime, endTime);
    }

    private static object? ExtractValue(JsonNode? node)
    {
        if (node == null) return null;

        // Handle objects with 'raw' and 'fmt' properties
        if (node is JsonObject obj && obj.ContainsKey("raw"))
        {
            var raw = obj["raw"];
            return raw switch
            {
                JsonValue value when value.TryGetValue<long>(out var l) => l,
                JsonValue value when value.TryGetValue<double>(out var d) => d,
                JsonValue value when value.TryGetValue<string>(out var s) => s,
                JsonValue value when value.TryGetValue<bool>(out var b) => b,
                _ => raw?.ToJsonString()
            };
        }

        // Handle primitive values
        return node switch
        {
            JsonValue value when value.TryGetValue<long>(out var l) => l,
            JsonValue value when value.TryGetValue<double>(out var d) => d,
            JsonValue value when value.TryGetValue<string>(out var s) => s,
            JsonValue value when value.TryGetValue<bool>(out var b) => b,
            _ => node.ToJsonString()
        };
    }
}
