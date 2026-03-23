using Microsoft.Extensions.Configuration;
using StockData.Net.Clients.Alpaca;
using StockData.Net.Security;

namespace StockData.Net.IntegrationTests;

[TestClass]
public class AlpacaIntegrationTests
{
    private IAlpacaClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        var apiKey = ResolveApiKey();
        var secretKey = ResolveSecretKey();

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            Assert.Inconclusive("Alpaca credentials are not configured. Set ALPACA_API_KEY and ALPACA_SECRET_KEY.");
        }

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://data.alpaca.markets/v2/")
        };

        _client = new AlpacaClient(httpClient, new SecretValue(apiKey!), new SecretValue(secretKey!));
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetHistoricalBarsAsync_LiveAapl_ReturnsData()
    {
        await ExecuteLiveApiTestAsync(nameof(GetHistoricalBarsAsync_LiveAapl_ReturnsData), async () =>
        {
            var to = DateTime.UtcNow;
            var from = to.AddDays(-10);
            var bars = await _client.GetHistoricalBarsAsync("AAPL", from, to, "1Day");

            Assert.IsNotEmpty(bars);
        });
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetLatestQuoteAsync_LiveAapl_ReturnsQuote()
    {
        await ExecuteLiveApiTestAsync(nameof(GetLatestQuoteAsync_LiveAapl_ReturnsQuote), async () =>
        {
            var quote = await _client.GetLatestQuoteAsync("AAPL");

            Assert.IsNotNull(quote);
            Assert.IsGreaterThan(quote.BidPrice, 0d);
            Assert.IsGreaterThan(quote.AskPrice, 0d);
        });
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetNewsAsync_AAPL_ReturnsNews()
    {
        await ExecuteLiveApiTestAsync(nameof(GetNewsAsync_AAPL_ReturnsNews), async () =>
        {
            var to = DateTime.UtcNow;
            var from = to.AddDays(-7);
            var news = await _client.GetNewsAsync("AAPL", from, to);

            Assert.IsNotNull(news);
            Assert.IsNotEmpty(news);
        });
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetMarketNewsAsync_ReturnsNews()
    {
        await ExecuteLiveApiTestAsync(nameof(GetMarketNewsAsync_ReturnsNews), async () =>
        {
            var news = await _client.GetMarketNewsAsync();

            Assert.IsNotNull(news);
            Assert.IsNotEmpty(news);
        });
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetLatestQuoteAsync_InvalidSymbol_ReturnsNull()
    {
        await ExecuteLiveApiTestAsync(nameof(GetLatestQuoteAsync_InvalidSymbol_ReturnsNull), async () =>
        {
            var quote = await _client.GetLatestQuoteAsync("ZZZZZZZ999");

            Assert.IsNull(quote);
        });
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetHealthStatusAsync_Live_ReturnsTrue()
    {
        await ExecuteLiveApiTestAsync(nameof(GetHealthStatusAsync_Live_ReturnsTrue), async () =>
        {
            var healthy = await _client.GetHealthStatusAsync();

            Assert.IsTrue(healthy);
        });
    }

    private static async Task ExecuteLiveApiTestAsync(string testName, Func<Task> testAction)
    {
        try
        {
            await testAction();
        }
        catch (Exception ex) when (IsCredentialOrRateLimitFailure(ex))
        {
            Assert.Inconclusive($"Skipping {testName}: Alpaca credentials appear invalid or rate-limited. {ex.Message}");
        }
    }

    private static bool IsCredentialOrRateLimitFailure(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains("401", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("403", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("429", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveApiKey()
    {
        var env = Environment.GetEnvironmentVariable("ALPACA_API_KEY");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        var builder = new ConfigurationBuilder()
            .AddUserSecrets<AlpacaIntegrationTests>(optional: true);

        var secretsPath = FindOptionalSecretsFile();
        if (secretsPath != null)
        {
            builder.AddJsonFile(secretsPath, optional: true, reloadOnChange: false);
        }

        var config = builder.Build();
        return config["Alpaca:ApiKey"]
             ?? config["Providers:Alpaca:ApiKey"];
    }

    private static string? ResolveSecretKey()
    {
        var env = Environment.GetEnvironmentVariable("ALPACA_SECRET_KEY");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        var builder = new ConfigurationBuilder()
            .AddUserSecrets<AlpacaIntegrationTests>(optional: true);

        var secretsPath = FindOptionalSecretsFile();
        if (secretsPath != null)
        {
            builder.AddJsonFile(secretsPath, optional: true, reloadOnChange: false);
        }

        var config = builder.Build();
        return config["Alpaca:SecretKey"]
             ?? config["Providers:Alpaca:SecretKey"];
    }

    private static string? FindOptionalSecretsFile()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "secrets.json");
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
