namespace StockData.Net.Configuration;

/// <summary>
/// Root configuration for the multi-source MCP server
/// </summary>
public class McpConfiguration
{
    /// <summary>
    /// Configuration schema version
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// List of configured data providers
    /// </summary>
    public List<ProviderConfiguration> Providers { get; set; } = new();

    /// <summary>
    /// Routing configuration for data requests
    /// </summary>
    public RoutingConfiguration Routing { get; set; } = new();

    /// <summary>
    /// News deduplication settings
    /// </summary>
    public NewsDeduplicationConfiguration NewsDeduplication { get; set; } = new();

    /// <summary>
    /// Circuit breaker configuration
    /// </summary>
    public CircuitBreakerConfiguration CircuitBreaker { get; set; } = new();

    /// <summary>
    /// Performance tuning options
    /// </summary>
    public PerformanceConfiguration Performance { get; set; } = new();
}

/// <summary>
/// Configuration for an individual data provider
/// </summary>
public class ProviderConfiguration
{
    /// <summary>
    /// Unique identifier for this provider instance
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Provider type (e.g., "YahooFinanceProvider")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether this provider is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Priority for provider selection (lower is higher priority)
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// Provider-specific settings (API keys, endpoints, etc.)
    /// </summary>
    public Dictionary<string, string> Settings { get; set; } = new();

    /// <summary>
    /// Health check configuration
    /// </summary>
    public HealthCheckConfiguration HealthCheck { get; set; } = new();
}

/// <summary>
/// Routing configuration for different data types
/// </summary>
public class RoutingConfiguration
{
    /// <summary>
    /// Default routing strategy when not specified per data type
    /// </summary>
    public string DefaultStrategy { get; set; } = "PrimaryWithFailover";

    /// <summary>
    /// Provider routing rules per data type
    /// </summary>
    public Dictionary<string, DataTypeRouting> DataTypeRouting { get; set; } = new();
}

/// <summary>
/// Routing configuration for a specific data type
/// </summary>
public class DataTypeRouting
{
    /// <summary>
    /// Primary provider ID for this data type
    /// </summary>
    public string PrimaryProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Fallback provider IDs in order of preference
    /// </summary>
    public List<string> FallbackProviderIds { get; set; } = new();

    /// <summary>
    /// Whether to aggregate results from multiple providers
    /// </summary>
    public bool AggregateResults { get; set; } = false;

    /// <summary>
    /// Timeout in seconds for this data type
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// News deduplication configuration
/// </summary>
public class NewsDeduplicationConfiguration
{
    /// <summary>
    /// Whether deduplication is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Similarity threshold for considering articles as duplicates (0.0 - 1.0)
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.85;

    /// <summary>
    /// Time window in hours for comparing article timestamps
    /// </summary>
    public int TimestampWindowHours { get; set; } = 24;

    /// <summary>
    /// Whether to compare article content in addition to titles
    /// </summary>
    public bool CompareContent { get; set; } = false;

    /// <summary>
    /// Maximum number of articles to compare (performance limit)
    /// </summary>
    public int MaxArticlesForComparison { get; set; } = 200;
}

/// <summary>
/// Circuit breaker configuration
/// </summary>
public class CircuitBreakerConfiguration
{
    /// <summary>
    /// Whether circuit breaker is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of consecutive failures before opening circuit
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Seconds to wait before attempting half-open state
    /// </summary>
    public int HalfOpenAfterSeconds { get; set; } = 60;

    /// <summary>
    /// Timeout in seconds for individual requests
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Performance tuning configuration
/// </summary>
public class PerformanceConfiguration
{
    /// <summary>
    /// Maximum number of concurrent requests per provider
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 10;

    /// <summary>
    /// Connection pool size for HTTP clients
    /// </summary>
    public int ConnectionPoolSize { get; set; } = 10;

    /// <summary>
    /// Idle connection timeout in seconds
    /// </summary>
    public int IdleConnectionTimeoutSeconds { get; set; } = 90;
}

/// <summary>
/// Health check configuration for a provider
/// </summary>
public class HealthCheckConfiguration
{
    /// <summary>
    /// Whether health checks are enabled for this provider
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Interval in seconds between health checks
    /// </summary>
    public int IntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Timeout in seconds for health check requests
    /// </summary>
    public int TimeoutSeconds { get; set; } = 10;
}
