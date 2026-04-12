using Microsoft.Extensions.Configuration;
using StockData.Net.Clients.AlphaVantage;
using StockData.Net.Clients.Finnhub;
using StockData.Net.Models.Events;
using StockData.Net.Providers;
using StockData.Net.Security;

namespace StockData.Net.IntegrationTests;

[TestClass]
public class Issue26MarketMovingEventsIntegrationTests
{
    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GivenFinnhubApiKey_WhenFetchingEvents_ThenReturnsUtcEventData()
    {
        var apiKey = ResolveApiKey("FINNHUB_API_KEY", "Finnhub:ApiKey", "Providers:Finnhub:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.Inconclusive("FINNHUB_API_KEY is not configured.");
        }

        var client = new FinnhubClient(new HttpClient { BaseAddress = new Uri("https://finnhub.io/api/v1/") }, new SecretValue(apiKey));
        var provider = new FinnhubMarketEventsProvider(client, Microsoft.Extensions.Logging.Abstractions.NullLogger<FinnhubMarketEventsProvider>.Instance);

        var from = DateOnly.FromDateTime(DateTime.UtcNow);
        var to = from.AddDays(7);
        var events = await provider.GetEventsAsync(new MarketEventsQuery { EventType = EventType.All, FromDate = from, ToDate = to });

        Assert.IsNotEmpty(events, "Expected at least one Finnhub market event.");
        var first = events[0];
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.EventId));
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.Title));
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.Category));
        Assert.IsFalse(string.IsNullOrWhiteSpace(first.Source));
        Assert.IsNotNull(first.AffectedMarkets, "AffectedMarkets must not be null");
        Assert.AreEqual(TimeSpan.Zero, first.EventTime.Offset);
    }

    [TestMethod]
    [TestCategory("Integration")]
    [TestCategory("LiveAPI")]
    public async Task GivenAlphaVantageApiKey_WhenFetchingBreakingEvents_ThenReturnsAtLeastOneBreakingEvent()
    {
        var apiKey = ResolveApiKey("ALPHAVANTAGE_API_KEY", "AlphaVantage:ApiKey", "Providers:AlphaVantage:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Assert.Inconclusive("ALPHAVANTAGE_API_KEY is not configured.");
        }

        var client = new AlphaVantageClient(new HttpClient { BaseAddress = new Uri("https://www.alphavantage.co/") }, new SecretValue(apiKey));
        var provider = new AlphaVantageMarketEventsProvider(client, Microsoft.Extensions.Logging.Abstractions.NullLogger<AlphaVantageMarketEventsProvider>.Instance);

        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));
        var to = DateOnly.FromDateTime(DateTime.UtcNow);
        var events = await provider.GetEventsAsync(new MarketEventsQuery { EventType = EventType.Breaking, FromDate = from, ToDate = to });

        Assert.IsTrue(events.Any(item => item.EventType == "breaking"));
        var firstBreaking = events.First(item => item.EventType == "breaking");
        Assert.IsFalse(string.IsNullOrWhiteSpace(firstBreaking.Category));
        Assert.IsFalse(string.IsNullOrWhiteSpace(firstBreaking.Source));
        Assert.IsNotNull(firstBreaking.AffectedMarkets, "AffectedMarkets must not be null");
    }

    private static string? ResolveApiKey(string envName, string configKey, string fallbackConfigKey)
    {
        var env = Environment.GetEnvironmentVariable(envName);
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        var builder = new ConfigurationBuilder().AddUserSecrets<Issue26MarketMovingEventsIntegrationTests>(optional: true);
        var secretsPath = FindOptionalSecretsFile();
        if (secretsPath != null)
        {
            builder.AddJsonFile(secretsPath, optional: true, reloadOnChange: false);
        }

        var config = builder.Build();
        return config[configKey] ?? config[fallbackConfigKey];
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
