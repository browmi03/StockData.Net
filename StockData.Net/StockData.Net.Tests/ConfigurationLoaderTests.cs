using Microsoft.VisualStudio.TestTools.UnitTesting;
using StockData.Net.Configuration;

namespace StockData.Net.Tests;

[TestClass]
public class ConfigurationLoaderTests
{
    private ConfigurationLoader _loader = null!;
    private string _testConfigDirectory = null!;

    [TestInitialize]
    public void Setup()
    {
        _loader = new ConfigurationLoader();
        _testConfigDirectory = Path.Combine(Path.GetTempPath(), "YahooFinanceTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testConfigDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testConfigDirectory))
        {
            Directory.Delete(_testConfigDirectory, true);
        }
    }

    [TestMethod]
    public void GetDefaultConfiguration_ReturnsValidConfiguration()
    {
        // Act
        var config = _loader.GetDefaultConfiguration();

        // Assert
        Assert.IsNotNull(config);
        Assert.AreEqual("1.0", config.Version);
        Assert.HasCount(1, config.Providers);
        Assert.AreEqual("yahoo_finance", config.Providers[0].Id);
        Assert.AreEqual("YahooFinanceProvider", config.Providers[0].Type);
        Assert.IsTrue(config.Providers[0].Enabled);
        Assert.IsNotNull(config.Routing);
        Assert.IsNotNull(config.NewsDeduplication);
        Assert.IsNotNull(config.CircuitBreaker);
        Assert.IsNotNull(config.Performance);
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithNoFile_ThrowsInvalidOperationException()
    {
        await AssertThrowsAsync<InvalidOperationException>(async () =>
            await _loader.LoadConfigurationAsync(null));
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testConfigDirectory, "nonexistent.json");

        await AssertThrowsAsync<FileNotFoundException>(async () =>
            await _loader.LoadConfigurationAsync(nonExistentPath));
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithValidFile_LoadsConfiguration()
    {
        // Arrange
        var configPath = Path.Combine(_testConfigDirectory, "config.json");
        var configJson = @"{
            ""version"": ""1.0"",
            ""providers"": [
                {
                    ""id"": ""test_provider"",
                    ""type"": ""TestProvider"",
                    ""enabled"": true,
                    ""priority"": 1,
                    ""settings"": {},
                    ""healthCheck"": {
                        ""enabled"": true,
                        ""intervalSeconds"": 300,
                        ""timeoutSeconds"": 10
                    }
                }
            ],
            ""routing"": {
                ""defaultStrategy"": ""PrimaryWithFailover"",
                ""dataTypeRouting"": {}
            },
            ""newsDeduplication"": {
                ""enabled"": true,
                ""similarityThreshold"": 0.85,
                ""timestampWindowHours"": 24,
                ""compareContent"": false,
                ""maxArticlesForComparison"": 200
            },
            ""circuitBreaker"": {
                ""enabled"": true,
                ""failureThreshold"": 5,
                ""halfOpenAfterSeconds"": 60,
                ""timeoutSeconds"": 30
            },
            ""performance"": {
                ""maxConcurrentRequests"": 10,
                ""connectionPoolSize"": 10,
                ""idleConnectionTimeoutSeconds"": 90
            }
        }";
        await File.WriteAllTextAsync(configPath, configJson);

        // Act
        var config = await _loader.LoadConfigurationAsync(configPath);

        // Assert
        Assert.IsNotNull(config);
        Assert.AreEqual("1.0", config.Version);
        Assert.HasCount(1, config.Providers);
        Assert.AreEqual("test_provider", config.Providers[0].Id);
        Assert.AreEqual("TestProvider", config.Providers[0].Type);
        Assert.IsTrue(config.NewsDeduplication.Enabled);
        Assert.AreEqual(0.85, config.NewsDeduplication.SimilarityThreshold);
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithInvalidJson_ThrowsInvalidOperationException()
    {
        // Arrange
        var configPath = Path.Combine(_testConfigDirectory, "invalid.json");
        await File.WriteAllTextAsync(configPath, "{ invalid json }");

        await AssertThrowsAsync<InvalidOperationException>(async () =>
            await _loader.LoadConfigurationAsync(configPath));
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithEnvironmentVariablePlaceholders_KeepsLiteralValue()
    {
        // Arrange
        var testVarName = "TEST_API_KEY_" + Guid.NewGuid().ToString("N");
        var testVarValue = "test_key_12345";
        Environment.SetEnvironmentVariable(testVarName, testVarValue);

        try
        {
            var configPath = Path.Combine(_testConfigDirectory, "config_with_env.json");
            var configJson = $@"{{
                ""version"": ""1.0"",
                ""providers"": [
                    {{
                        ""id"": ""test_provider"",
                        ""type"": ""TestProvider"",
                        ""enabled"": true,
                        ""priority"": 1,
                        ""settings"": {{
                            ""apiKey"": ""${{{testVarName}}}""
                        }},
                        ""healthCheck"": {{
                            ""enabled"": true,
                            ""intervalSeconds"": 300,
                            ""timeoutSeconds"": 10
                        }}
                    }}
                ],
                ""routing"": {{
                    ""defaultStrategy"": ""PrimaryWithFailover"",
                    ""dataTypeRouting"": {{}}
                }},
                ""newsDeduplication"": {{
                    ""enabled"": true,
                    ""similarityThreshold"": 0.85,
                    ""timestampWindowHours"": 24,
                    ""compareContent"": false,
                    ""maxArticlesForComparison"": 200
                }},
                ""circuitBreaker"": {{
                    ""enabled"": true,
                    ""failureThreshold"": 5,
                    ""halfOpenAfterSeconds"": 60,
                    ""timeoutSeconds"": 30
                }},
                ""performance"": {{
                    ""maxConcurrentRequests"": 10,
                    ""connectionPoolSize"": 10,
                    ""idleConnectionTimeoutSeconds"": 90
                }}
            }}";
            await File.WriteAllTextAsync(configPath, configJson);

            // Act
            var config = await _loader.LoadConfigurationAsync(configPath);

            // Assert
            Assert.IsNotNull(config);
            Assert.AreEqual("test_provider", config.Providers[0].Id);
            Assert.AreEqual($"${{{testVarName}}}", config.Providers[0].Settings["apiKey"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable(testVarName, null);
        }
    }

    [TestMethod]
    public void GetDefaultConfiguration_HasAllDataTypeRoutings()
    {
        // Act
        var config = _loader.GetDefaultConfiguration();

        // Assert
        Assert.IsTrue(config.Routing.DataTypeRouting.ContainsKey("HistoricalPrices"));
        Assert.IsTrue(config.Routing.DataTypeRouting.ContainsKey("StockInfo"));
        Assert.IsTrue(config.Routing.DataTypeRouting.ContainsKey("News"));
        Assert.IsTrue(config.Routing.DataTypeRouting.ContainsKey("MarketNews"));
        Assert.IsTrue(config.Routing.DataTypeRouting.ContainsKey("StockActions"));
        Assert.IsTrue(config.Routing.DataTypeRouting.ContainsKey("FinancialStatement"));
        Assert.IsTrue(config.Routing.DataTypeRouting.ContainsKey("HolderInfo"));
        Assert.IsTrue(config.Routing.DataTypeRouting.ContainsKey("OptionExpirationDates"));
        Assert.IsTrue(config.Routing.DataTypeRouting.ContainsKey("OptionChain"));
        Assert.IsTrue(config.Routing.DataTypeRouting.ContainsKey("Recommendations"));
    }

    [TestMethod]
    public void GetDefaultConfiguration_NewsDeduplicationEnabled()
    {
        // Act
        var config = _loader.GetDefaultConfiguration();

        // Assert
        Assert.IsTrue(config.NewsDeduplication.Enabled);
        Assert.AreEqual(0.85, config.NewsDeduplication.SimilarityThreshold);
        Assert.AreEqual(24, config.NewsDeduplication.TimestampWindowHours);
        Assert.IsFalse(config.NewsDeduplication.CompareContent);
        Assert.AreEqual(200, config.NewsDeduplication.MaxArticlesForComparison);
    }

    [TestMethod]
    public void GetDefaultConfiguration_CircuitBreakerEnabled()
    {
        // Act
        var config = _loader.GetDefaultConfiguration();

        // Assert
        Assert.IsTrue(config.CircuitBreaker.Enabled);
        Assert.AreEqual(5, config.CircuitBreaker.FailureThreshold);
        Assert.AreEqual(60, config.CircuitBreaker.HalfOpenAfterSeconds);
        Assert.AreEqual(30, config.CircuitBreaker.TimeoutSeconds);
    }

    // Configuration Validation Tests

    [TestMethod]
    public async Task LoadConfigurationAsync_WithMissingEnvironmentVariable_LoadsLiteralValue()
    {
        // Arrange
        var nonExistentVar = "NON_EXISTENT_VAR_" + Guid.NewGuid().ToString("N");
        var configPath = Path.Combine(_testConfigDirectory, "config_missing_var.json");
        var configJson = $@"{{
            ""version"": ""1.0"",
            ""providers"": [
                {{
                    ""id"": ""test_provider"",
                    ""type"": ""TestProvider"",
                    ""enabled"": true,
                    ""priority"": 1,
                    ""settings"": {{
                        ""apiKey"": ""${{{nonExistentVar}}}""
                    }},
                    ""healthCheck"": {{
                        ""enabled"": true,
                        ""intervalSeconds"": 300,
                        ""timeoutSeconds"": 10
                    }}
                }}
            ],
            ""routing"": {{
                ""defaultStrategy"": ""PrimaryWithFailover"",
                ""dataTypeRouting"": {{}}
            }}
        }}";
        await File.WriteAllTextAsync(configPath, configJson);

        // Act
        var config = await _loader.LoadConfigurationAsync(configPath);

        // Assert
        Assert.IsNotNull(config);
        Assert.AreEqual("test_provider", config.Providers[0].Id);
        Assert.AreEqual($"${{{nonExistentVar}}}", config.Providers[0].Settings["apiKey"]);
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithMultipleProviders_LoadsSuccessfully()
    {
        // Arrange
        var configPath = Path.Combine(_testConfigDirectory, "multi_provider_config.json");
        var configJson = @"{
            ""version"": ""1.0"",
            ""providers"": [
                {
                    ""id"": ""yahoo_finance"",
                    ""type"": ""YahooFinanceProvider"",
                    ""enabled"": true,
                    ""priority"": 1,
                    ""settings"": {},
                    ""healthCheck"": {
                        ""enabled"": true,
                        ""intervalSeconds"": 300,
                        ""timeoutSeconds"": 10
                    }
                },
                {
                    ""id"": ""alpha_vantage"",
                    ""type"": ""AlphaVantageProvider"",
                    ""enabled"": true,
                    ""priority"": 2,
                    ""settings"": {},
                    ""healthCheck"": {
                        ""enabled"": true,
                        ""intervalSeconds"": 300,
                        ""timeoutSeconds"": 10
                    }
                }
            ],
            ""routing"": {
                ""defaultStrategy"": ""PrimaryWithFailover"",
                ""dataTypeRouting"": {
                    ""StockInfo"": {
                        ""primaryProviderId"": ""yahoo_finance"",
                        ""fallbackProviderIds"": [""alpha_vantage""],
                        ""timeoutSeconds"": 30
                    }
                }
            },
            ""newsDeduplication"": {
                ""enabled"": true,
                ""similarityThreshold"": 0.85,
                ""timestampWindowHours"": 24,
                ""compareContent"": false,
                ""maxArticlesForComparison"": 200
            },
            ""circuitBreaker"": {
                ""enabled"": true,
                ""failureThreshold"": 5,
                ""halfOpenAfterSeconds"": 60,
                ""timeoutSeconds"": 30
            },
            ""performance"": {
                ""maxConcurrentRequests"": 10,
                ""connectionPoolSize"": 10,
                ""idleConnectionTimeoutSeconds"": 90
            }
        }";
        await File.WriteAllTextAsync(configPath, configJson);

        // Act
        var config = await _loader.LoadConfigurationAsync(configPath);

        // Assert
        Assert.IsNotNull(config);
        Assert.HasCount(2, config.Providers);
        Assert.AreEqual("yahoo_finance", config.Providers[0].Id);
        Assert.AreEqual("alpha_vantage", config.Providers[1].Id);
        Assert.AreEqual("alpha_vantage", config.Routing.DataTypeRouting["StockInfo"].FallbackProviderIds[0]);
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithDuplicateProviderIds_ThrowsInvalidOperationException()
    {
        // Arrange
        var configPath = Path.Combine(_testConfigDirectory, "duplicate_provider_config.json");
        var configJson = @"{
            ""version"": ""1.0"",
            ""providers"": [
                {
                    ""id"": ""yahoo_finance"",
                    ""type"": ""YahooFinanceProvider"",
                    ""enabled"": true,
                    ""priority"": 1,
                    ""settings"": {},
                    ""healthCheck"": {
                        ""enabled"": true,
                        ""intervalSeconds"": 300,
                        ""timeoutSeconds"": 10
                    }
                },
                {
                    ""id"": ""yahoo_finance"",
                    ""type"": ""YahooFinanceProvider2"",
                    ""enabled"": true,
                    ""priority"": 2,
                    ""settings"": {},
                    ""healthCheck"": {
                        ""enabled"": true,
                        ""intervalSeconds"": 300,
                        ""timeoutSeconds"": 10
                    }
                }
            ],
            ""routing"": {
                ""defaultStrategy"": ""PrimaryWithFailover"",
                ""dataTypeRouting"": {}
            }
        }";
        await File.WriteAllTextAsync(configPath, configJson);

        await AssertThrowsAsync<InvalidOperationException>(async () =>
            await _loader.LoadConfigurationAsync(configPath));
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithInvalidPrimaryProviderReference_ThrowsInvalidOperationException()
    {
        // Arrange
        var configPath = Path.Combine(_testConfigDirectory, "invalid_provider_ref_config.json");
        var configJson = @"{
            ""version"": ""1.0"",
            ""providers"": [
                {
                    ""id"": ""yahoo_finance"",
                    ""type"": ""YahooFinanceProvider"",
                    ""enabled"": true,
                    ""priority"": 1,
                    ""settings"": {},
                    ""healthCheck"": {
                        ""enabled"": true,
                        ""intervalSeconds"": 300,
                        ""timeoutSeconds"": 10
                    }
                }
            ],
            ""routing"": {
                ""defaultStrategy"": ""PrimaryWithFailover"",
                ""dataTypeRouting"": {
                    ""StockInfo"": {
                        ""primaryProviderId"": ""non_existent_provider"",
                        ""fallbackProviderIds"": [],
                        ""timeoutSeconds"": 30
                    }
                }
            }
        }";
        await File.WriteAllTextAsync(configPath, configJson);

        await AssertThrowsAsync<InvalidOperationException>(async () =>
            await _loader.LoadConfigurationAsync(configPath));
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithInvalidDedupSimilarityThreshold_ThrowsInvalidOperationException()
    {
        var configPath = Path.Combine(_testConfigDirectory, "invalid_dedup_threshold_config.json");
        var configJson = @"{
            ""version"": ""1.0"",
            ""providers"": [
                {
                    ""id"": ""yahoo_finance"",
                    ""type"": ""YahooFinanceProvider"",
                    ""enabled"": true,
                    ""priority"": 1,
                    ""settings"": {},
                    ""healthCheck"": {
                        ""enabled"": true,
                        ""intervalSeconds"": 300,
                        ""timeoutSeconds"": 10
                    }
                }
            ],
            ""routing"": {
                ""defaultStrategy"": ""PrimaryWithFailover"",
                ""dataTypeRouting"": {}
            },
            ""newsDeduplication"": {
                ""enabled"": true,
                ""similarityThreshold"": 0.2,
                ""timestampWindowHours"": 24,
                ""compareContent"": false,
                ""maxArticlesForComparison"": 200
            }
        }";

        await File.WriteAllTextAsync(configPath, configJson);

        await AssertThrowsAsync<InvalidOperationException>(async () =>
            await _loader.LoadConfigurationAsync(configPath));
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithInvalidDedupTimestampWindow_ThrowsInvalidOperationException()
    {
        var configPath = Path.Combine(_testConfigDirectory, "invalid_dedup_window_config.json");
        var configJson = @"{
            ""version"": ""1.0"",
            ""providers"": [
                {
                    ""id"": ""yahoo_finance"",
                    ""type"": ""YahooFinanceProvider"",
                    ""enabled"": true,
                    ""priority"": 1,
                    ""settings"": {},
                    ""healthCheck"": {
                        ""enabled"": true,
                        ""intervalSeconds"": 300,
                        ""timeoutSeconds"": 10
                    }
                }
            ],
            ""routing"": {
                ""defaultStrategy"": ""PrimaryWithFailover"",
                ""dataTypeRouting"": {}
            },
            ""newsDeduplication"": {
                ""enabled"": true,
                ""similarityThreshold"": 0.85,
                ""timestampWindowHours"": 0,
                ""compareContent"": false,
                ""maxArticlesForComparison"": 200
            }
        }";

        await File.WriteAllTextAsync(configPath, configJson);

        await AssertThrowsAsync<InvalidOperationException>(async () =>
            await _loader.LoadConfigurationAsync(configPath));
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithInvalidDedupMaxArticles_ThrowsInvalidOperationException()
    {
        var configPath = Path.Combine(_testConfigDirectory, "invalid_dedup_max_articles_config.json");
        var configJson = @"{
            ""version"": ""1.0"",
            ""providers"": [
                {
                    ""id"": ""yahoo_finance"",
                    ""type"": ""YahooFinanceProvider"",
                    ""enabled"": true,
                    ""priority"": 1,
                    ""settings"": {},
                    ""healthCheck"": {
                        ""enabled"": true,
                        ""intervalSeconds"": 300,
                        ""timeoutSeconds"": 10
                    }
                }
            ],
            ""routing"": {
                ""defaultStrategy"": ""PrimaryWithFailover"",
                ""dataTypeRouting"": {}
            },
            ""newsDeduplication"": {
                ""enabled"": true,
                ""similarityThreshold"": 0.85,
                ""timestampWindowHours"": 24,
                ""compareContent"": false,
                ""maxArticlesForComparison"": 5000
            }
        }";

        await File.WriteAllTextAsync(configPath, configJson);

        await AssertThrowsAsync<InvalidOperationException>(async () =>
            await _loader.LoadConfigurationAsync(configPath));
    }

    [TestMethod]
    public async Task LoadConfigurationAsync_WithFinnhubRateLimitAndResilience_ParsesProviderSettings()
    {
        var configPath = Path.Combine(_testConfigDirectory, "finnhub_config.json");
        var configJson = @"{
            ""version"": ""1.0"",
            ""providers"": [
                {
                    ""id"": ""finnhub"",
                    ""type"": ""FinnhubProvider"",
                    ""enabled"": true,
                    ""priority"": 2,
                    ""settings"": {
                        ""apiKey"": ""<injected-from-secrets>"",
                        ""baseUrl"": ""https://finnhub.io/api/v1""
                    },
                    ""rateLimit"": {
                        ""enabled"": true,
                        ""requestsPerMinute"": 60,
                        ""burstLimit"": 10
                    },
                    ""resilience"": {
                        ""circuitBreakerEnabled"": true,
                        ""failureThreshold"": 5,
                        ""halfOpenAfterSeconds"": 60,
                        ""timeoutSeconds"": 15,
                        ""retryCount"": 1,
                        ""retryBaseDelayMs"": 500
                    },
                    ""healthCheck"": {
                        ""enabled"": true,
                        ""intervalSeconds"": 300,
                        ""timeoutSeconds"": 10
                    }
                }
            ],
            ""routing"": {
                ""defaultStrategy"": ""PrimaryWithFailover"",
                ""dataTypeRouting"": {}
            }
        }";

        await File.WriteAllTextAsync(configPath, configJson);

        var config = await _loader.LoadConfigurationAsync(configPath);
        var provider = config.Providers.Single(p => p.Id == "finnhub");

        Assert.IsTrue(provider.RateLimit.Enabled);
        Assert.AreEqual(60, provider.RateLimit.RequestsPerMinute);
        Assert.AreEqual(10, provider.RateLimit.BurstLimit);
        Assert.IsTrue(provider.Resilience.CircuitBreakerEnabled);
        Assert.AreEqual(1, provider.Resilience.RetryCount);
        Assert.AreEqual(500, provider.Resilience.RetryBaseDelayMs);
    }

    private static async Task<TException> AssertThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException ex)
        {
            return ex;
        }

        throw new AssertFailedException($"Expected exception of type {typeof(TException).Name} was not thrown.");
    }
}
