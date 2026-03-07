using System.Text.Json;
using Microsoft.Extensions.Configuration;
using StockData.Net.Clients.Finnhub;
using StockData.Net.Providers;
using StockData.Net.Security;
using TypedFinnhubClient = StockData.Net.Clients.Finnhub.FinnhubClient;

namespace StockData.Net.IntegrationTests;

[TestClass]
public class FinnhubIntegrationTests
{
    private FinnhubProvider _provider = null!;

    [TestInitialize]
    public void Setup()
    {
        var apiKey = ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.Inconclusive("Finnhub API key not configured. Set 'Finnhub:ApiKey' or 'Providers:Finnhub:ApiKey' in user-secrets or secrets.json.");
        }

        var httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://finnhub.io/api/v1/")
        };

        var client = new TypedFinnhubClient(
            httpClient,
            new SecretValue(apiKey!));

        _provider = new FinnhubProvider(client, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinnhubProvider>.Instance);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetStockInfoAsync_LiveAapl_ReturnsExpectedSchema()
    {
        var result = await _provider.GetStockInfoAsync("AAPL");
        var document = JsonDocument.Parse(result);

        Assert.AreEqual("AAPL", document.RootElement.GetProperty("symbol").GetString());
        Assert.IsTrue(document.RootElement.TryGetProperty("price", out _));
        Assert.IsTrue(document.RootElement.TryGetProperty("timestamp", out _));
        Assert.AreEqual("finnhub", document.RootElement.GetProperty("sourceProvider").GetString());
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetHistoricalPricesAsync_LiveAapl_ReturnsArray()
    {
        var result = await _provider.GetHistoricalPricesAsync("AAPL", "1mo", "1d");
        var document = JsonDocument.Parse(result);

        Assert.AreEqual(JsonValueKind.Array, document.RootElement.ValueKind);
        Assert.IsGreaterThan(document.RootElement.GetArrayLength(), 0);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GetNewsAsync_LiveAapl_ReturnsContent()
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
    }

    private static string? ResolveApiKey()
    {
        var builder = new ConfigurationBuilder()
            .AddUserSecrets<FinnhubIntegrationTests>(optional: true);

        var secretsPath = FindOptionalSecretsFile();
        if (secretsPath != null)
        {
            builder.AddJsonFile(secretsPath, optional: true, reloadOnChange: false);
        }

        var config = builder.Build();

        return config["Finnhub:ApiKey"]
             ?? config["Providers:Finnhub:ApiKey"];
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