using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StockData.Net.Clients.AlphaVantage;
using StockData.Net.Providers;
using StockData.Net.Security;

namespace StockData.Net.IntegrationTests;

[TestClass]
public class AlphaVantageIntegrationTests
{
    private AlphaVantageProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey) || IsPlaceholderApiKey(apiKey))
        {
            Assert.Inconclusive("AlphaVantage API key not configured. Set 'AlphaVantage:ApiKey' or 'Providers:AlphaVantage:ApiKey' in user-secrets or secrets.json.");
        }

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://www.alphavantage.co/")
        };

        var client = new AlphaVantageClient(httpClient, new SecretValue(apiKey!));
        _provider = new AlphaVantageProvider(client, Microsoft.Extensions.Logging.Abstractions.NullLogger<AlphaVantageProvider>.Instance);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetStockInfoAsync_LiveAapl_ReturnsExpectedSchema()
    {
        await ExecuteLiveApiTestAsync(nameof(GetStockInfoAsync_LiveAapl_ReturnsExpectedSchema), async () =>
        {
            var result = await _provider.GetStockInfoAsync("AAPL");
            var document = JsonDocument.Parse(result);

            Assert.AreEqual("AAPL", document.RootElement.GetProperty("symbol").GetString());
            Assert.IsTrue(document.RootElement.TryGetProperty("price", out _));
            Assert.IsTrue(document.RootElement.TryGetProperty("timestamp", out _));
            Assert.AreEqual("alphavantage", document.RootElement.GetProperty("sourceProvider").GetString());
        });
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetHistoricalPricesAsync_LiveAapl_ReturnsArray()
    {
        await ExecuteLiveApiTestAsync(nameof(GetHistoricalPricesAsync_LiveAapl_ReturnsArray), async () =>
        {
            var result = await _provider.GetHistoricalPricesAsync("AAPL", "1mo", "1d");
            var document = JsonDocument.Parse(result);

            Assert.AreEqual(JsonValueKind.Array, document.RootElement.ValueKind);
            Assert.IsGreaterThan(document.RootElement.GetArrayLength(), 0);
        });
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetNewsAsync_LiveAapl_ReturnsContent()
    {
        await ExecuteLiveApiTestAsync(nameof(GetNewsAsync_LiveAapl_ReturnsContent), async () =>
        {
            var result = await _provider.GetNewsAsync("AAPL");

            Assert.IsFalse(string.IsNullOrWhiteSpace(result));

            var blocks = result.Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.IsGreaterThan(blocks.Length, 0);

            var first = blocks[0];
            StringAssert.Contains(first, "Title:");
            StringAssert.Contains(first, "Publisher:");
            StringAssert.Contains(first, "Published:");
            StringAssert.Contains(first, "URL:");
        });
    }

    private static async Task ExecuteLiveApiTestAsync(string testName, Func<Task> testAction)
    {
        try
        {
            await testAction();
        }
        catch (Exception ex) when (IsCredentialFailure(ex))
        {
            Assert.Inconclusive($"Skipping {testName}: AlphaVantage API key appears missing/invalid or rate-limited. {ex.Message}");
        }
    }

    private static bool IsCredentialFailure(Exception exception)
    {
        for (var current = exception; current != null; current = current.InnerException)
        {
            if (current.Message.Contains("401", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("403", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("api key", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("frequency", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("Invalid API call", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPlaceholderApiKey(string? apiKey)
    {
        return !string.IsNullOrWhiteSpace(apiKey)
            && apiKey.Contains("missing-from-github-secrets", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveApiKey()
    {
        var builder = new ConfigurationBuilder()
            .AddUserSecrets<AlphaVantageIntegrationTests>(optional: true);

        var secretsPath = FindOptionalSecretsFile();
        if (secretsPath != null)
        {
            builder.AddJsonFile(secretsPath, optional: true, reloadOnChange: false);
        }

        var config = builder.Build();

        return config["AlphaVantage:ApiKey"]
             ?? config["Providers:AlphaVantage:ApiKey"];
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
