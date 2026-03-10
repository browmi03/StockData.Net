using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using StockData.Net;
using StockData.Net.Configuration;
using StockData.Net.Models;
using StockData.Net.McpServer.Models;
using StockData.Net.Providers;
using StockData.Net.Security;

[assembly: InternalsVisibleTo("StockData.Net.McpServer.Tests")]

namespace StockData.Net.McpServer;

/// <summary>
/// Yahoo Finance MCP Server implementation
/// </summary>
public class StockDataMcpServer
{
    private readonly StockDataProviderRouter _router;
    private readonly ProviderSelectionValidator _providerValidator;
    private readonly ILogger<StockDataMcpServer>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private const string ServerName = "StockData-mcp";
    private const string ServerVersion = "1.0.0";

    public StockDataMcpServer(StockDataProviderRouter router)
        : this(router, new ConfigurationLoader().GetDefaultConfiguration(), null)
    {
    }

    public StockDataMcpServer(
        StockDataProviderRouter router,
        McpConfiguration configuration,
        ILogger<StockDataMcpServer>? logger = null)
    {
        _router = router;
        _providerValidator = new ProviderSelectionValidator(configuration, _router.GetRegisteredProviderIds());
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Console.Error.WriteLine("Starting Yahoo Finance MCP Server...");
        
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var line = await Console.In.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                var request = JsonSerializer.Deserialize<McpRequest>(line, _jsonOptions);
                if (request == null)
                {
                    continue;
                }

                var response = await HandleRequestAsync(request, cancellationToken);
                var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                await Console.Out.WriteLineAsync(responseJson);
                await Console.Out.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {SensitiveDataSanitizer.Sanitize(ex.Message)}");
            }
        }
    }

    internal async Task<McpResponse> HandleRequestAsync(McpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = request.Method switch
            {
                "initialize" => HandleInitialize(),
                "tools/list" => HandleToolsList(),
                "tools/call" => await HandleToolCallAsync(request.Params, cancellationToken),
                _ => throw new Exception($"Unknown method: {request.Method}")
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = result
            };
        }
        catch (Exception ex)
        {
            var providerAware = ex as ProviderAwareException;
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32603,
                    Message = SensitiveDataSanitizer.Sanitize(ex.Message),
                    Data = providerAware == null
                        ? null
                        : new
                        {
                            serviceKey = _providerValidator.GetServiceKeyForProviderId(providerAware.ProviderId),
                            tier = _providerValidator.GetTierForProviderId(providerAware.ProviderId)
                        }
                }
            };
        }
    }

    private object HandleInitialize()
    {
        return new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { }
            },
            serverInfo = new
            {
                name = ServerName,
                version = ServerVersion
            }
        };
    }

    private object HandleToolsList()
    {
        var tools = new[]
        {
            CreateToolDefinition(
                "get_historical_stock_prices",
                "Get historical stock prices for a given ticker symbol. Include Date, Open, High, Low, Close, Volume, Adj Close. Use provider when the user explicitly asks for a source (yahoo, alphavantage, finnhub).",
                new
                {
                    type = "object",
                    properties = new
                    {
                        ticker = new { type = "string", description = "The ticker symbol of the stock to get historical prices for, e.g. \"AAPL\"" },
                        period = new { type = "string", description = "Valid periods: 1d,5d,1mo,3mo,6mo,1y,2y,5y,10y,ytd,max. Default is \"1mo\"", @default = "1mo" },
                        interval = new { type = "string", description = "Valid intervals: 1m,2m,5m,15m,30m,60m,90m,1h,1d,5d,1wk,1mo,3mo. Default is \"1d\"", @default = "1d" },
                        provider = new { type = "string", description = "Optional provider override. Valid values: yahoo, alphavantage, finnhub." }
                    },
                    required = new[] { "ticker" }
                }),
            CreateToolDefinition(
                "get_stock_info",
                "Get stock information for a given ticker symbol. Include Stock Price & Trading Info, Company Information, Financial Metrics, and more. Use provider when the user explicitly asks for a source.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        ticker = new { type = "string", description = "The ticker symbol of the stock to get information for, e.g. \"AAPL\"" },
                        provider = new { type = "string", description = "Optional provider override. Valid values: yahoo, alphavantage, finnhub." }
                    },
                    required = new[] { "ticker" }
                }),
            CreateToolDefinition(
                "get_finance_news",
                "Get news for a given ticker symbol using configured finance data providers. Use provider when the user explicitly asks for a source.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        ticker = new { type = "string", description = "The ticker symbol of the stock to get news for, e.g. \"AAPL\"" },
                        provider = new { type = "string", description = "Optional provider override. Valid values: yahoo, alphavantage, finnhub." }
                    },
                    required = new[] { "ticker" }
                }),
            CreateToolDefinition(
                "get_market_news",
                "Get general market news without requiring a specific ticker. Returns title, summary, publish time, URL, and related tickers. Use provider when explicitly requested.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        provider = new { type = "string", description = "Optional provider override. Valid values: yahoo, alphavantage, finnhub." }
                    }
                }),
            CreateToolDefinition(
                "get_stock_actions",
                "Get stock dividends and stock splits for a given ticker symbol. Use provider when the user explicitly asks for a source.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        ticker = new { type = "string", description = "The ticker symbol of the stock to get stock actions for, e.g. \"AAPL\"" },
                        provider = new { type = "string", description = "Optional provider override. Valid values: yahoo, alphavantage, finnhub." }
                    },
                    required = new[] { "ticker" }
                }),
            CreateToolDefinition(
                "get_financial_statement",
                "Get financial statements for a ticker symbol. financial_type: income_stmt, quarterly_income_stmt, balance_sheet, quarterly_balance_sheet, cashflow, quarterly_cashflow. Use provider when explicitly requested.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        ticker = new { type = "string", description = "The ticker symbol of the stock to get financial statement for, e.g. \"AAPL\"" },
                        financial_type = new { type = "string", description = "The type of financial statement to get. You can choose from the following financial statement types: income_stmt, quarterly_income_stmt, balance_sheet, quarterly_balance_sheet, cashflow, quarterly_cashflow." },
                        provider = new { type = "string", description = "Optional provider override. Valid values: yahoo, alphavantage, finnhub." }
                    },
                    required = new[] { "ticker", "financial_type" }
                }),
            CreateToolDefinition(
                "get_holder_info",
                "Get holder information for a ticker symbol. holder_type: major_holders, institutional_holders, mutualfund_holders, insider_transactions, insider_purchases, insider_roster_holders. Use provider when explicitly requested.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        ticker = new { type = "string", description = "The ticker symbol of the stock to get holder information for, e.g. \"AAPL\"" },
                        holder_type = new { type = "string", description = "The type of holder information to get. You can choose from the following holder types: major_holders, institutional_holders, mutualfund_holders, insider_transactions, insider_purchases, insider_roster_holders." },
                        provider = new { type = "string", description = "Optional provider override. Valid values: yahoo, alphavantage, finnhub." }
                    },
                    required = new[] { "ticker", "holder_type" }
                }),
            CreateToolDefinition(
                "get_option_expiration_dates",
                "Fetch the available options expiration dates for a given ticker symbol.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        ticker = new { type = "string", description = "The ticker symbol of the stock to get option expiration dates for, e.g. \"AAPL\"" },
                        provider = new { type = "string", description = "Optional provider override. Valid values: yahoo, alphavantage, finnhub." }
                    },
                    required = new[] { "ticker" }
                }),
            CreateToolDefinition(
                "get_option_chain",
                "Fetch the option chain for a given ticker symbol, expiration date, and option type.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        ticker = new { type = "string", description = "The ticker symbol of the stock to get option chain for, e.g. \"AAPL\"" },
                        expiration_date = new { type = "string", description = "The expiration date for the options chain (format: 'YYYY-MM-DD')" },
                        option_type = new { type = "string", description = "The type of option to fetch ('calls' or 'puts')" },
                        provider = new { type = "string", description = "Optional provider override. Valid values: yahoo, alphavantage, finnhub." }
                    },
                    required = new[] { "ticker", "expiration_date", "option_type" }
                }),
            CreateToolDefinition(
                "get_recommendations",
                "Get recommendations or upgrades/downgrades for a ticker symbol. months_back default is 12. Use provider when explicitly requested.",
                new
                {
                    type = "object",
                    properties = new
                    {
                        ticker = new { type = "string", description = "The ticker symbol of the stock to get recommendations for, e.g. \"AAPL\"" },
                        recommendation_type = new { type = "string", description = "The type of recommendation to get. You can choose from the following recommendation types: recommendations, upgrades_downgrades." },
                        months_back = new { type = "integer", description = "The number of months back to get upgrades/downgrades for, default is 12.", @default = 12 },
                        provider = new { type = "string", description = "Optional provider override. Valid values: yahoo, alphavantage, finnhub." }
                    },
                    required = new[] { "ticker", "recommendation_type" }
                })
        };

        return new { tools };
    }

    internal async Task<object> HandleToolCallAsync(JsonElement? paramsElement, CancellationToken cancellationToken)
    {
        if (paramsElement == null)
        {
            throw new Exception("Missing params");
        }

        var toolName = paramsElement.Value.GetProperty("name").GetString();
        var arguments = paramsElement.Value.GetProperty("arguments");

        if (string.IsNullOrEmpty(toolName))
        {
            throw new Exception("Missing tool name");
        }

        var requestedProvider = GetOptionalString(arguments, "provider", string.Empty);
        var validation = _providerValidator.Validate(requestedProvider);
        if (!validation.IsValid)
        {
            throw new Exception(validation.ErrorMessage ?? "Provider selection is invalid.");
        }

        var selectionMode = validation.IsExplicitSelection ? "explicit" : "default";
        _logger?.LogInformation(
            "Provider selection audit: tool={ToolName} method={SelectionMode} requestedProvider={RequestedProvider} resolvedProvider={ResolvedProvider}",
            toolName,
            selectionMode,
            string.IsNullOrWhiteSpace(requestedProvider) ? "<none>" : requestedProvider,
            validation.ResolvedProviderId ?? "<router>");

        var explicitProviderId = validation.IsExplicitSelection
            ? validation.ResolvedProviderId
            : _providerValidator.ResolveDefaultProviderForDataType(MapToolToDataType(toolName));

        ProviderResult result;

        try
        {
            result = toolName switch
            {
                "get_historical_stock_prices" => await _router.GetHistoricalPricesWithProviderAsync(
                    GetRequiredString(arguments, "ticker"),
                    GetOptionalString(arguments, "period", "1mo"),
                    GetOptionalString(arguments, "interval", "1d"),
                    explicitProviderId,
                    cancellationToken),

                "get_stock_info" => await _router.GetStockInfoWithProviderAsync(
                    GetRequiredString(arguments, "ticker"),
                    explicitProviderId,
                    cancellationToken),

                "get_finance_news" => await _router.GetNewsWithProviderAsync(
                    GetRequiredString(arguments, "ticker"),
                    explicitProviderId,
                    cancellationToken),

                "get_market_news" => await _router.GetMarketNewsWithProviderAsync(
                    explicitProviderId,
                    cancellationToken),

                "get_stock_actions" => await _router.GetStockActionsWithProviderAsync(
                    GetRequiredString(arguments, "ticker"),
                    explicitProviderId,
                    cancellationToken),

                "get_financial_statement" => await _router.GetFinancialStatementWithProviderAsync(
                    GetRequiredString(arguments, "ticker"),
                    ParseFinancialType(GetRequiredString(arguments, "financial_type")),
                    explicitProviderId,
                    cancellationToken),

                "get_holder_info" => await _router.GetHolderInfoWithProviderAsync(
                    GetRequiredString(arguments, "ticker"),
                    ParseHolderType(GetRequiredString(arguments, "holder_type")),
                    explicitProviderId,
                    cancellationToken),

                "get_option_expiration_dates" => await _router.GetOptionExpirationDatesWithProviderAsync(
                    GetRequiredString(arguments, "ticker"),
                    explicitProviderId,
                    cancellationToken),

                "get_option_chain" => await _router.GetOptionChainWithProviderAsync(
                    GetRequiredString(arguments, "ticker"),
                    GetRequiredString(arguments, "expiration_date"),
                    ParseOptionType(GetRequiredString(arguments, "option_type")),
                    explicitProviderId,
                    cancellationToken),

                "get_recommendations" => await _router.GetRecommendationsWithProviderAsync(
                    GetRequiredString(arguments, "ticker"),
                    ParseRecommendationType(GetRequiredString(arguments, "recommendation_type")),
                    GetOptionalInt(arguments, "months_back", 12),
                    explicitProviderId,
                    cancellationToken),

                _ => throw new Exception($"Unknown tool: {toolName}")
            };
        }
        catch (Exception ex)
        {
            var providerId = validation.ResolvedProviderId;
            if (!string.IsNullOrWhiteSpace(providerId))
            {
                throw new ProviderAwareException(providerId, ex.Message, ex);
            }

            throw;
        }

        var serviceKey = _providerValidator.GetServiceKeyForProviderId(result.ProviderId);
        var tier = _providerValidator.GetTierForProviderId(result.ProviderId);

        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = result.Result
                }
            },
            _meta = new
            {
                serviceKey,
                tier
            }
        };
    }

    private static Tool CreateToolDefinition(string name, string description, object inputSchema)
    {
        var schemaJson = JsonSerializer.Serialize(inputSchema);
        return new Tool
        {
            Name = name,
            Description = description,
            InputSchema = JsonDocument.Parse(schemaJson).RootElement
        };
    }

    private static string GetRequiredString(JsonElement arguments, string key)
    {
        if (arguments.TryGetProperty(key, out var value))
        {
            return value.GetString() ?? throw new Exception($"Missing required parameter: {key}");
        }
        throw new Exception($"Missing required parameter: {key}");
    }

    private static string GetOptionalString(JsonElement arguments, string key, string defaultValue)
    {
        if (arguments.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null)
        {
            return value.GetString() ?? defaultValue;
        }
        return defaultValue;
    }

    private static int GetOptionalInt(JsonElement arguments, string key, int defaultValue)
    {
        if (arguments.TryGetProperty(key, out var value) && value.ValueKind != JsonValueKind.Null)
        {
            return value.GetInt32();
        }
        return defaultValue;
    }

    private static string MapToolToDataType(string toolName)
    {
        return toolName switch
        {
            "get_historical_stock_prices" => "HistoricalPrices",
            "get_stock_info" => "StockInfo",
            "get_finance_news" => "News",
            "get_market_news" => "MarketNews",
            "get_stock_actions" => "StockActions",
            "get_financial_statement" => "FinancialStatement",
            "get_holder_info" => "HolderInfo",
            "get_option_expiration_dates" => "OptionExpirationDates",
            "get_option_chain" => "OptionChain",
            "get_recommendations" => "Recommendations",
            _ => "StockInfo"
        };
    }

    private static FinancialStatementType ParseFinancialType(string type)
    {
        return type.ToLower() switch
        {
            "income_stmt" => FinancialStatementType.IncomeStatement,
            "quarterly_income_stmt" => FinancialStatementType.QuarterlyIncomeStatement,
            "balance_sheet" => FinancialStatementType.BalanceSheet,
            "quarterly_balance_sheet" => FinancialStatementType.QuarterlyBalanceSheet,
            "cashflow" => FinancialStatementType.CashFlow,
            "quarterly_cashflow" => FinancialStatementType.QuarterlyCashFlow,
            _ => throw new Exception($"Invalid financial statement type: {type}")
        };
    }

    private static HolderType ParseHolderType(string type)
    {
        return type.ToLower() switch
        {
            "major_holders" => HolderType.MajorHolders,
            "institutional_holders" => HolderType.InstitutionalHolders,
            "mutualfund_holders" => HolderType.MutualFundHolders,
            "insider_transactions" => HolderType.InsiderTransactions,
            "insider_purchases" => HolderType.InsiderPurchases,
            "insider_roster_holders" => HolderType.InsiderRosterHolders,
            _ => throw new Exception($"Invalid holder type: {type}")
        };
    }

    private static OptionType ParseOptionType(string type)
    {
        return type.ToLower() switch
        {
            "calls" => OptionType.Calls,
            "puts" => OptionType.Puts,
            _ => throw new Exception($"Invalid option type: {type}. Must be 'calls' or 'puts'.")
        };
    }

    private static RecommendationType ParseRecommendationType(string type)
    {
        return type.ToLower() switch
        {
            "recommendations" => RecommendationType.Recommendations,
            "upgrades_downgrades" => RecommendationType.UpgradesDowngrades,
            _ => throw new Exception($"Invalid recommendation type: {type}")
        };
    }
}

internal sealed class ProviderAwareException : Exception
{
    public string ProviderId { get; }

    public ProviderAwareException(string providerId, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderId = providerId;
    }
}
