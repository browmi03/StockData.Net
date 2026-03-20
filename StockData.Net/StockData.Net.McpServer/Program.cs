using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StockData.Net;
using StockData.Net.Clients.AlphaVantage;
using StockData.Net.Clients.Finnhub;
using StockData.Net.Configuration;
using StockData.Net.Deduplication;
using StockData.Net.McpServer;
using StockData.Net.Providers;
using StockData.Net.Security;

// Load .env file from the application directory so ${VAR} placeholders resolve correctly.
// Explicitly loads from AppContext.BaseDirectory to ensure the .env file is found
// regardless of the process working directory (e.g., when launched by VS Code MCP client).
var envPath = Path.Combine(AppContext.BaseDirectory, ".env");
if (File.Exists(envPath))
{
    DotNetEnv.Env.Load(envPath);
}

var builder = Host.CreateApplicationBuilder(args);

// Load configuration
var configLoader = new ConfigurationLoader();
var configPath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "appsettings.json");
var config = await configLoader.LoadConfigurationAsync(configPath);

// Register configuration
builder.Services.AddSingleton(config);
builder.Services.AddSingleton<IConfigurationLoader>(configLoader);

// Register Yahoo Finance client (creates its own HttpClient with proper cookie handling)
builder.Services.AddSingleton<IYahooFinanceClient, YahooFinanceClient>();

// Register base provider
builder.Services.AddSingleton<IStockDataProvider, YahooFinanceProvider>();

RegisterFinnhubProvider(builder.Services, config);
RegisterAlphaVantageProvider(builder.Services, config);

// Register symbol translation
builder.Services.AddSingleton<ISymbolTranslator, SymbolTranslator>();

// Register news deduplication components
builder.Services.AddSingleton<INewsDeduplicationStrategy, LevenshteinSimilarityStrategy>();
builder.Services.AddSingleton<NewsDeduplicator>();

// Register router with all providers
builder.Services.AddSingleton<StockDataProviderRouter>(sp =>
{
    var configuration = sp.GetRequiredService<McpConfiguration>();
    var providers = sp.GetServices<IStockDataProvider>();
    var deduplicator = sp.GetRequiredService<NewsDeduplicator>();
    var symbolTranslator = sp.GetService<ISymbolTranslator>();
    var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<StockDataProviderRouter>>();
    return new StockDataProviderRouter(configuration, providers, logger, deduplicator, symbolTranslator);
});

// Register MCP server
builder.Services.AddSingleton<StockDataMcpServer>(sp =>
{
    var router = sp.GetRequiredService<StockDataProviderRouter>();
    var configuration = sp.GetRequiredService<McpConfiguration>();
    var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<StockDataMcpServer>>();
    return new StockDataMcpServer(router, configuration, logger);
});

var host = builder.Build();

// Get the MCP server and run it
var mcpServer = host.Services.GetRequiredService<StockDataMcpServer>();
await mcpServer.RunAsync();

static void RegisterFinnhubProvider(IServiceCollection services, McpConfiguration config)
{
    var providerConfig = GetProviderConfiguration(config, "finnhub");
    if (providerConfig is null || !providerConfig.Enabled)
    {
        return;
    }

    var apiKey = ResolveProviderApiKey(providerConfig, "FINNHUB_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Console.Error.WriteLine("[Startup] Finnhub provider is enabled but API key is missing. Skipping registration.");
        return;
    }

    var baseUrl = ResolveBaseUrl(providerConfig, "https://finnhub.io/api/v1/");

    services.AddSingleton<IFinnhubClient>(_ =>
        new FinnhubClient(
            new HttpClient { BaseAddress = new Uri(baseUrl) },
            new SecretValue(apiKey),
            providerConfig.RateLimit));
    services.AddSingleton<IStockDataProvider, FinnhubProvider>();
}

static void RegisterAlphaVantageProvider(IServiceCollection services, McpConfiguration config)
{
    var providerConfig = GetProviderConfiguration(config, "alphavantage");
    if (providerConfig is null || !providerConfig.Enabled)
    {
        return;
    }

    var apiKey = ResolveProviderApiKey(providerConfig, "ALPHAVANTAGE_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        Console.Error.WriteLine("[Startup] AlphaVantage provider is enabled but API key is missing. Skipping registration.");
        return;
    }

    var baseUrl = ResolveBaseUrl(providerConfig, "https://www.alphavantage.co/");

    services.AddSingleton<IAlphaVantageClient>(_ =>
        new AlphaVantageClient(
            new HttpClient { BaseAddress = new Uri(baseUrl) },
            new SecretValue(apiKey),
            providerConfig.RateLimit));
    services.AddSingleton<IStockDataProvider, AlphaVantageProvider>();
}

static ProviderConfiguration? GetProviderConfiguration(McpConfiguration config, string providerId)
{
    return config.Providers.FirstOrDefault(
        p => string.Equals(p.Id, providerId, StringComparison.OrdinalIgnoreCase));
}

static string ResolveBaseUrl(ProviderConfiguration providerConfig, string fallback)
{
    if (providerConfig.Settings.TryGetValue("baseUrl", out var baseUrl) && !string.IsNullOrWhiteSpace(baseUrl))
    {
        return EnsureTrailingSlash(baseUrl);
    }

    return EnsureTrailingSlash(fallback);
}

static string? ResolveProviderApiKey(ProviderConfiguration providerConfig, string fallbackEnvironmentVariable)
{
    if (providerConfig.Settings.TryGetValue("apiKey", out var configured) && !string.IsNullOrWhiteSpace(configured))
    {
        var resolved = ResolveEnvironmentPlaceholder(configured);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            return resolved;
        }
    }

    return Environment.GetEnvironmentVariable(fallbackEnvironmentVariable);
}

static string EnsureTrailingSlash(string url)
{
    return url.EndsWith("/", StringComparison.Ordinal) ? url : $"{url}/";
}

static string? ResolveEnvironmentPlaceholder(string value)
{
    var trimmed = value.Trim();
    if (trimmed.StartsWith("${", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal) && trimmed.Length > 3)
    {
        var variableName = trimmed[2..^1];
        return Environment.GetEnvironmentVariable(variableName);
    }

    return trimmed;
}
