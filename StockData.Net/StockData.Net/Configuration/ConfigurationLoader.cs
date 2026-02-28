using System.Text.Json;
using System.Text.RegularExpressions;

namespace StockData.Net.Configuration;

/// <summary>
/// Interface for loading MCP configuration
/// </summary>
public interface IConfigurationLoader
{
    /// <summary>
    /// Loads configuration from a JSON file or returns default configuration
    /// </summary>
    /// <param name="configPath">Path to configuration file (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Loaded or default configuration</returns>
    Task<McpConfiguration> LoadConfigurationAsync(string? configPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default configuration with Yahoo Finance as the only provider
    /// </summary>
    /// <returns>Default configuration</returns>
    McpConfiguration GetDefaultConfiguration();
}

/// <summary>
/// Configuration loader implementation
/// </summary>
public class ConfigurationLoader : IConfigurationLoader
{
    private static readonly Regex EnvVarPattern = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigurationLoader()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true
        };
    }

    public async Task<McpConfiguration> LoadConfigurationAsync(string? configPath = null, CancellationToken cancellationToken = default)
    {
        // If no config path provided or file doesn't exist, return default
        if (string.IsNullOrWhiteSpace(configPath) || !File.Exists(configPath))
        {
            if (!string.IsNullOrWhiteSpace(configPath))
            {
                Console.Error.WriteLine($"Configuration file not found: {configPath}. Using default configuration.");
            }
            return GetDefaultConfiguration();
        }

        try
        {
            // Read and parse JSON
            var json = await File.ReadAllTextAsync(configPath, cancellationToken);
            
            // Expand environment variables
            json = ExpandEnvironmentVariables(json);
            
            // Deserialize configuration
            var config = JsonSerializer.Deserialize<McpConfiguration>(json, _jsonOptions);
            
            if (config == null)
            {
                Console.Error.WriteLine("Failed to deserialize configuration. Using default configuration.");
                return GetDefaultConfiguration();
            }

            // Validate configuration
            ValidateConfiguration(config);
            
            return config;
        }
        catch (Exception ex)
        {
            var sanitizedMessage = SanitizeMessage(ex.Message);
            Console.Error.WriteLine($"Error loading configuration: {sanitizedMessage}. Using default configuration.");
            return GetDefaultConfiguration();
        }
    }

    public McpConfiguration GetDefaultConfiguration()
    {
        return new McpConfiguration
        {
            Version = "1.0",
            Providers = new List<ProviderConfiguration>
            {
                new ProviderConfiguration
                {
                    Id = "yahoo_finance",
                    Type = "YahooFinanceProvider",
                    Enabled = true,
                    Priority = 1,
                    Settings = new Dictionary<string, string>(),
                    HealthCheck = new HealthCheckConfiguration
                    {
                        Enabled = true,
                        IntervalSeconds = 300,
                        TimeoutSeconds = 10
                    }
                }
            },
            Routing = new RoutingConfiguration
            {
                DefaultStrategy = "PrimaryWithFailover",
                DataTypeRouting = new Dictionary<string, DataTypeRouting>
                {
                    ["HistoricalPrices"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "yahoo_finance",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    },
                    ["StockInfo"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "yahoo_finance",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    },
                    ["News"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "yahoo_finance",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    },
                    ["MarketNews"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "yahoo_finance",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    },
                    ["StockActions"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "yahoo_finance",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    },
                    ["FinancialStatement"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "yahoo_finance",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    },
                    ["HolderInfo"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "yahoo_finance",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    },
                    ["OptionExpirationDates"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "yahoo_finance",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    },
                    ["OptionChain"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "yahoo_finance",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    },
                    ["Recommendations"] = new DataTypeRouting
                    {
                        PrimaryProviderId = "yahoo_finance",
                        FallbackProviderIds = new List<string>(),
                        TimeoutSeconds = 30
                    }
                }
            },
            NewsDeduplication = new NewsDeduplicationConfiguration
            {
                Enabled = true,
                SimilarityThreshold = 0.85,
                TimestampWindowHours = 24,
                CompareContent = false,
                MaxArticlesForComparison = 200
            },
            CircuitBreaker = new CircuitBreakerConfiguration
            {
                Enabled = true,
                FailureThreshold = 5,
                HalfOpenAfterSeconds = 60,
                TimeoutSeconds = 30
            },
            Performance = new PerformanceConfiguration
            {
                MaxConcurrentRequests = 10,
                ConnectionPoolSize = 10,
                IdleConnectionTimeoutSeconds = 90
            }
        };
    }

    private string ExpandEnvironmentVariables(string json)
    {
        return EnvVarPattern.Replace(json, match =>
        {
            var varName = match.Groups[1].Value;
            var value = Environment.GetEnvironmentVariable(varName);
            
            if (string.IsNullOrEmpty(value))
            {
                var sanitizedMessage = SanitizeMessage($"Environment variable '{varName}' is not set");
                throw new InvalidOperationException(sanitizedMessage);
            }
            
            return value;
        });
    }

    /// <summary>
    /// Sanitizes error messages by redacting sensitive information
    /// </summary>
    private static string SanitizeMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            return message;
        }

        // Redact sequences of 16+ alphanumeric characters (likely API keys/tokens)
        var redactedMessage = Regex.Replace(message, @"\b[A-Za-z0-9]{16,}\b", "[REDACTED]");
        
        return redactedMessage;
    }

    /// <summary>
    /// Determines if a configuration key contains sensitive information
    /// </summary>
    private static bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var lowerKey = key.ToLowerInvariant();
        return lowerKey.Contains("key") || 
               lowerKey.Contains("secret") || 
               lowerKey.Contains("token") || 
               lowerKey.Contains("password") || 
               lowerKey.Contains("credential");
    }

    private void ValidateConfiguration(McpConfiguration config)
    {
        if (config.Providers == null || config.Providers.Count == 0)
        {
            throw new InvalidOperationException("Configuration must have at least one provider");
        }

        foreach (var provider in config.Providers)
        {
            if (string.IsNullOrWhiteSpace(provider.Id))
            {
                throw new InvalidOperationException("Provider ID cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(provider.Type))
            {
                throw new InvalidOperationException($"Provider '{provider.Id}' must have a Type specified");
            }
        }

        // Check for duplicate provider IDs
        var duplicateIds = config.Providers
            .GroupBy(p => p.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Any())
        {
            throw new InvalidOperationException($"Duplicate provider ID detected: {string.Join(", ", duplicateIds)}");
        }

        // Validate routing references
        if (config.Routing?.DataTypeRouting != null)
        {
            var providerIds = config.Providers.Select(p => p.Id).ToHashSet();
            
            foreach (var routing in config.Routing.DataTypeRouting.Values)
            {
                if (!string.IsNullOrWhiteSpace(routing.PrimaryProviderId) && 
                    !providerIds.Contains(routing.PrimaryProviderId))
                {
                    throw new InvalidOperationException(
                        $"Routing references unknown provider '{routing.PrimaryProviderId}'");
                }

                foreach (var fallbackId in routing.FallbackProviderIds ?? new List<string>())
                {
                    if (!providerIds.Contains(fallbackId))
                    {
                        throw new InvalidOperationException(
                            $"Routing references unknown fallback provider '{fallbackId}'");
                    }
                }
            }
        }

        if (config.NewsDeduplication != null)
        {
            if (config.NewsDeduplication.SimilarityThreshold < 0.50 ||
                config.NewsDeduplication.SimilarityThreshold > 0.99)
            {
                throw new InvalidOperationException(
                    "NewsDeduplication.SimilarityThreshold must be between 0.50 and 0.99");
            }

            if (config.NewsDeduplication.TimestampWindowHours < 1 ||
                config.NewsDeduplication.TimestampWindowHours > 168)
            {
                throw new InvalidOperationException(
                    "NewsDeduplication.TimestampWindowHours must be between 1 and 168");
            }

            if (config.NewsDeduplication.MaxArticlesForComparison < 10 ||
                config.NewsDeduplication.MaxArticlesForComparison > 1000)
            {
                throw new InvalidOperationException(
                    "NewsDeduplication.MaxArticlesForComparison must be between 10 and 1000");
            }
        }
    }
}
