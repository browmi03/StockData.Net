using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StockData.Net;
using StockData.Net.Configuration;
using StockData.Net.Deduplication;
using StockData.Net.McpServer;
using StockData.Net.Providers;

var builder = Host.CreateApplicationBuilder(args);

// Load configuration
var configLoader = new ConfigurationLoader();
var configPath = args.Length > 0 ? args[0] : null;
var config = await configLoader.LoadConfigurationAsync(configPath);

// Register configuration
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<IConfigurationLoader>(configLoader);

// Register Yahoo Finance client (creates its own HttpClient with proper cookie handling)
builder.Services.AddSingleton<IYahooFinanceClient, YahooFinanceClient>();

// Register providers
builder.Services.AddSingleton<IStockDataProvider, YahooFinanceProvider>();

// Register news deduplication components
builder.Services.AddSingleton<INewsDeduplicationStrategy, LevenshteinSimilarityStrategy>();
builder.Services.AddSingleton<NewsDeduplicator>();

// Register router with all providers
builder.Services.AddSingleton<StockDataProviderRouter>(sp =>
{
    var configuration = sp.GetRequiredService<McpConfiguration>();
    var providers = sp.GetServices<IStockDataProvider>();
    var deduplicator = sp.GetRequiredService<NewsDeduplicator>();
    var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<StockDataProviderRouter>>();
    return new StockDataProviderRouter(configuration, providers, logger, deduplicator);
});

// Register MCP server
builder.Services.AddSingleton<StockDataMcpServer>();

var host = builder.Build();

// Get the MCP server and run it
var mcpServer = host.Services.GetRequiredService<StockDataMcpServer>();
await mcpServer.RunAsync();
