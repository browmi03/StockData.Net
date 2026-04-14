using StockData.Net.Configuration;
using StockData.Net.Deduplication;
using StockData.Net.Models;
using StockData.Net.Resilience;
using StockData.Net.Security;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace StockData.Net.Providers;

/// <summary>
/// Routes stock data requests to appropriate providers with failover support
/// Phase 2: Full failover chain with circuit breaker and health monitoring
/// </summary>
public class StockDataProviderRouter
{
    private readonly McpConfiguration _configuration;
    private readonly Dictionary<string, IStockDataProvider> _providers;
    private readonly Dictionary<string, CircuitBreaker> _circuitBreakers;
    private readonly ProviderHealthMonitor _healthMonitor;
    private readonly NewsDeduplicator _newsDeduplicator;
    private readonly ISymbolTranslator? _symbolTranslator;
    private readonly ILogger<StockDataProviderRouter>? _logger;

    public StockDataProviderRouter(
        McpConfiguration configuration,
        IEnumerable<IStockDataProvider> providers,
        ILogger<StockDataProviderRouter>? logger = null,
        NewsDeduplicator? newsDeduplicator = null,
        ISymbolTranslator? symbolTranslator = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _providers = providers?.ToDictionary(p => p.ProviderId, p => p) 
            ?? throw new ArgumentNullException(nameof(providers));
        _logger = logger;
        _newsDeduplicator = newsDeduplicator ?? new NewsDeduplicator();
        _symbolTranslator = symbolTranslator;

        // Initialize circuit breakers for each provider (without logging for now)
        _circuitBreakers = new Dictionary<string, CircuitBreaker>();
        foreach (var providerId in _providers.Keys)
        {
            // Create a stub logger that does nothing
            var stubLogger = new StubLogger<CircuitBreaker>();
            _circuitBreakers[providerId] = new CircuitBreaker(
                providerId,
                _configuration.CircuitBreaker,
                stubLogger);
        }

        // Initialize health monitor (without logging for now)
        var stubHealthLogger = new StubLogger<ProviderHealthMonitor>();
        _healthMonitor = new ProviderHealthMonitor(
            _configuration.Providers
                .FirstOrDefault(p => p.Enabled)?.HealthCheck 
                ?? new HealthCheckConfiguration(),
            stubHealthLogger,
            async (providerId, ct) =>
            {
                if (_providers.TryGetValue(providerId, out var provider))
                {
                    return await provider.GetHealthStatusAsync(ct);
                }
                return false;
            });

        // Start health monitoring
        _ = _healthMonitor.StartAsync();
    }

    // Stub logger for cases where no real logger is provided
    private class StubLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => false;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, 
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }

    /// <summary>
    /// Gets historical stock prices
    /// </summary>
    public async Task<string> GetHistoricalPricesAsync(string ticker, string period = "1mo", string interval = "1d", CancellationToken cancellationToken = default)
    {
        var result = await GetHistoricalPricesWithProviderAsync(ticker, period, interval, null, cancellationToken);
        return result.Result;
    }

    public async Task<ProviderResult> GetHistoricalPricesWithProviderAsync(
        string ticker,
        string period = "1mo",
        string interval = "1d",
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithProviderSelectionAsync(
            "HistoricalPrices",
            providerId,
            async (provider, ct) =>
            {
                var translatedSymbol = _symbolTranslator?.Translate(ticker, provider.ProviderId) ?? ticker;
                return await provider.GetHistoricalPricesAsync(translatedSymbol, period, interval, ct);
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets stock information
    /// </summary>
    public async Task<string> GetStockInfoAsync(string ticker, CancellationToken cancellationToken = default)
    {
        var result = await GetStockInfoWithProviderAsync(ticker, null, cancellationToken);
        return result.Result;
    }

    public async Task<ProviderResult> GetStockInfoWithProviderAsync(
        string ticker,
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithProviderSelectionAsync(
            "StockInfo",
            providerId,
            async (provider, ct) =>
            {
                var translatedSymbol = _symbolTranslator?.Translate(ticker, provider.ProviderId) ?? ticker;
                return await provider.GetStockInfoAsync(translatedSymbol, ct);
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets news for a specific ticker
    /// </summary>
    public async Task<string> GetNewsAsync(string ticker, CancellationToken cancellationToken = default)
    {
        var result = await GetNewsWithProviderAsync(ticker, null, cancellationToken);
        return result.Result;
    }

    public async Task<ProviderResult> GetNewsWithProviderAsync(
        string ticker,
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            return await ExecuteWithProviderSelectionAsync(
                "News",
                providerId,
                async (provider, ct) =>
                {
                    var translatedSymbol = _symbolTranslator?.Translate(ticker, provider.ProviderId) ?? ticker;
                    return await provider.GetNewsAsync(translatedSymbol, ct);
                },
                cancellationToken);
        }

        if (ShouldAggregateResults("News"))
        {
            return await ExecuteWithAggregationResultAsync(
                "News",
                async (provider, ct) =>
                {
                    var translatedSymbol = _symbolTranslator?.Translate(ticker, provider.ProviderId) ?? ticker;
                    return await provider.GetNewsAsync(translatedSymbol, ct);
                },
                cancellationToken);
        }

        return await ExecuteWithFailoverResultAsync(
            "News",
            async (provider, ct) =>
            {
                var translatedSymbol = _symbolTranslator?.Translate(ticker, provider.ProviderId) ?? ticker;
                return await provider.GetNewsAsync(translatedSymbol, ct);
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets general market news
    /// </summary>
    public async Task<string> GetMarketNewsAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetMarketNewsWithProviderAsync(null, cancellationToken);
        return result.Result;
    }

    public async Task<ProviderResult> GetMarketNewsWithProviderAsync(
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(providerId))
        {
            return await ExecuteWithProviderSelectionAsync(
                "MarketNews",
                providerId,
                async (provider, ct) => await provider.GetMarketNewsAsync(ct),
                cancellationToken);
        }

        if (ShouldAggregateResults("MarketNews"))
        {
            return await ExecuteWithAggregationResultAsync(
                "MarketNews",
                async (provider, ct) => await provider.GetMarketNewsAsync(ct),
                cancellationToken);
        }

        return await ExecuteWithFailoverResultAsync(
            "MarketNews",
            async (provider, ct) => await provider.GetMarketNewsAsync(ct),
            cancellationToken);
    }

    /// <summary>
    /// Gets stock actions (dividends and splits)
    /// </summary>
    public async Task<string> GetStockActionsAsync(string ticker, CancellationToken cancellationToken = default)
    {
        var result = await GetStockActionsWithProviderAsync(ticker, null, cancellationToken);
        return result.Result;
    }

    public async Task<ProviderResult> GetStockActionsWithProviderAsync(
        string ticker,
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithProviderSelectionAsync(
            "StockActions",
            providerId,
            async (provider, ct) =>
            {
                var translatedSymbol = _symbolTranslator?.Translate(ticker, provider.ProviderId) ?? ticker;
                return await provider.GetStockActionsAsync(translatedSymbol, ct);
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets financial statements
    /// </summary>
    public async Task<string> GetFinancialStatementAsync(string ticker, FinancialStatementType statementType, CancellationToken cancellationToken = default)
    {
        var result = await GetFinancialStatementWithProviderAsync(ticker, statementType, null, cancellationToken);
        return result.Result;
    }

    public async Task<ProviderResult> GetFinancialStatementWithProviderAsync(
        string ticker,
        FinancialStatementType statementType,
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithProviderSelectionAsync(
            "FinancialStatement",
            providerId,
            async (provider, ct) =>
            {
                var translatedSymbol = _symbolTranslator?.Translate(ticker, provider.ProviderId) ?? ticker;
                return await provider.GetFinancialStatementAsync(translatedSymbol, statementType, ct);
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets holder information
    /// </summary>
    public async Task<string> GetHolderInfoAsync(string ticker, HolderType holderType, CancellationToken cancellationToken = default)
    {
        var result = await GetHolderInfoWithProviderAsync(ticker, holderType, null, cancellationToken);
        return result.Result;
    }

    public async Task<ProviderResult> GetHolderInfoWithProviderAsync(
        string ticker,
        HolderType holderType,
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithProviderSelectionAsync(
            "HolderInfo",
            providerId,
            async (provider, ct) =>
            {
                var translatedSymbol = _symbolTranslator?.Translate(ticker, provider.ProviderId) ?? ticker;
                return await provider.GetHolderInfoAsync(translatedSymbol, holderType, ct);
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets option expiration dates
    /// </summary>
    public async Task<string> GetOptionExpirationDatesAsync(string ticker, CancellationToken cancellationToken = default)
    {
        var result = await GetOptionExpirationDatesWithProviderAsync(ticker, null, cancellationToken);
        return result.Result;
    }

    public async Task<ProviderResult> GetOptionExpirationDatesWithProviderAsync(
        string ticker,
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithProviderSelectionAsync(
            "OptionExpirationDates",
            providerId,
            async (provider, ct) =>
            {
                var translatedSymbol = _symbolTranslator?.Translate(ticker, provider.ProviderId) ?? ticker;
                return await provider.GetOptionExpirationDatesAsync(translatedSymbol, ct);
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets option chain
    /// </summary>
    public async Task<string> GetOptionChainAsync(string ticker, string expirationDate, OptionType optionType, CancellationToken cancellationToken = default)
    {
        var result = await GetOptionChainWithProviderAsync(ticker, expirationDate, optionType, null, cancellationToken);
        return result.Result;
    }

    public async Task<ProviderResult> GetOptionChainWithProviderAsync(
        string ticker,
        string expirationDate,
        OptionType optionType,
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithProviderSelectionAsync(
            "OptionChain",
            providerId,
            async (provider, ct) =>
            {
                var translatedSymbol = _symbolTranslator?.Translate(ticker, provider.ProviderId) ?? ticker;
                return await provider.GetOptionChainAsync(translatedSymbol, expirationDate, optionType, ct);
            },
            cancellationToken);
    }

    /// <summary>
    /// Gets analyst recommendations
    /// </summary>
    public async Task<string> GetRecommendationsAsync(string ticker, RecommendationType recommendationType, int monthsBack = 12, CancellationToken cancellationToken = default)
    {
        var result = await GetRecommendationsWithProviderAsync(ticker, recommendationType, monthsBack, null, cancellationToken);
        return result.Result;
    }

    public async Task<ProviderResult> GetRecommendationsWithProviderAsync(
        string ticker,
        RecommendationType recommendationType,
        int monthsBack = 12,
        string? providerId = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteWithProviderSelectionAsync(
            "Recommendations",
            providerId,
            async (provider, ct) =>
            {
                var translatedSymbol = _symbolTranslator?.Translate(ticker, provider.ProviderId) ?? ticker;
                return await provider.GetRecommendationsAsync(translatedSymbol, recommendationType, monthsBack, ct);
            },
            cancellationToken);
    }

    public IReadOnlyCollection<string> GetRegisteredProviderIds()
    {
        return _providers.Keys.ToList().AsReadOnly();
    }

    public IReadOnlyCollection<IStockDataProvider> GetRegisteredProviders()
    {
        return _providers.Values.ToList().AsReadOnly();
    }

    private async Task<ProviderResult> ExecuteWithProviderSelectionAsync(
        string dataType,
        string? explicitProviderId,
        Func<IStockDataProvider, CancellationToken, Task<string>> operation,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(explicitProviderId))
        {
            return await ExecuteWithExplicitProviderAsync(dataType, explicitProviderId, operation, cancellationToken);
        }

        return await ExecuteWithFailoverResultAsync(dataType, operation, cancellationToken);
    }

    private async Task<ProviderResult> ExecuteWithExplicitProviderAsync(
        string dataType,
        string explicitProviderId,
        Func<IStockDataProvider, CancellationToken, Task<string>> operation,
        CancellationToken cancellationToken)
    {
        var normalizedProviderId = NormalizeProviderId(explicitProviderId);
        var startTime = Stopwatch.GetTimestamp();

        if (!_providers.TryGetValue(normalizedProviderId, out var provider))
        {
            throw new InvalidOperationException($"Provider '{explicitProviderId}' is not available.");
        }

        _logger?.LogInformation(
            "Provider selection audit: method=explicit dataType={DataType} provider={ProviderId}",
            dataType,
            normalizedProviderId);

        var requestStart = Stopwatch.GetTimestamp();

        try
        {
            var response = await operation(provider, cancellationToken);

            var requestDuration = Stopwatch.GetElapsedTime(requestStart);
            var totalDuration = Stopwatch.GetElapsedTime(startTime);
            _logger?.LogInformation(
                "Explicit provider request succeeded for {DataType} with provider {ProviderId} in {Duration}ms (total: {TotalDuration}ms)",
                dataType,
                normalizedProviderId,
                requestDuration.TotalMilliseconds,
                totalDuration.TotalMilliseconds);

            return new ProviderResult(response, normalizedProviderId);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(
                ex,
                "Provider selection audit: method=explicit dataType={DataType} provider={ProviderId} outcome=failed",
                dataType,
                normalizedProviderId);
            throw;
        }
    }

    /// <summary>
    /// Executes a provider operation with failover chain support
    /// </summary>
    private async Task<string> ExecuteWithFailoverAsync(
        string dataType,
        Func<IStockDataProvider, CancellationToken, Task<string>> operation,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteWithFailoverResultAsync(dataType, operation, cancellationToken);
        return result.Result;
    }

    private async Task<ProviderResult> ExecuteWithFailoverResultAsync(
        string dataType,
        Func<IStockDataProvider, CancellationToken, Task<string>> operation,
        CancellationToken cancellationToken)
    {
        var startTime = Stopwatch.GetTimestamp();
        var providerChain = GetProviderChain(dataType);
        var attemptedProviders = new List<string>();
        var providerErrors = new Dictionary<string, Exception>();
        var tierFailures = new List<TierFailureDetail>();

        _logger?.LogDebug("Executing {DataType} with provider chain: {ProviderChain}",
            dataType, string.Join(" → ", providerChain));

        foreach (var providerId in providerChain)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            attemptedProviders.Add(providerId);

            // Check if provider is healthy
            var healthStatus = _healthMonitor.GetHealthStatus(providerId);
            if (!healthStatus.IsHealthy)
            {
                _logger?.LogWarning(
                    "Skipping unhealthy provider {ProviderId} for {DataType} (failures: {ConsecutiveFailures})",
                    providerId, dataType, healthStatus.ConsecutiveFailures);
                continue;
            }

            if (!_providers.TryGetValue(providerId, out var provider))
            {
                _logger?.LogWarning("Provider {ProviderId} not found in registry", providerId);
                continue;
            }

            var configuredTier = GetProviderTier(providerId);
            if (!IsProviderTierCapable(provider, configuredTier, dataType))
            {
                _logger?.LogInformation(
                    "Skipping provider {ProviderId} for {DataType}: unsupported on tier {Tier}",
                    providerId,
                    dataType,
                    configuredTier);

                tierFailures.Add(new TierFailureDetail(
                    provider.ProviderName,
                    ToCapabilityKey(dataType),
                    configuredTier,
                    GetProviderUpgradeUrl(provider.ProviderId)));
                continue;
            }

            if (!_circuitBreakers.TryGetValue(providerId, out var circuitBreaker))
            {
                _logger?.LogWarning("Circuit breaker not found for provider {ProviderId}", providerId);
                continue;
            }

            try
            {
                _logger?.LogInformation("Attempting {DataType} request with provider {ProviderId}",
                    dataType, providerId);

                var requestStart = Stopwatch.GetTimestamp();

                var result = await circuitBreaker.ExecuteAsync(
                    async ct => await operation(provider, ct),
                    cancellationToken);

                var requestDuration = Stopwatch.GetElapsedTime(requestStart);
                _healthMonitor.RecordSuccess(providerId, requestDuration);

                var totalDuration = Stopwatch.GetElapsedTime(startTime);
                _logger?.LogInformation(
                    "{DataType} request succeeded with provider {ProviderId} in {Duration}ms (total: {TotalDuration}ms)",
                    dataType, providerId, requestDuration.TotalMilliseconds, totalDuration.TotalMilliseconds);

                return new ProviderResult(result, providerId);
            }
            catch (CircuitBreakerOpenException ex)
            {
                _logger?.LogWarning(
                    "Circuit breaker is open for provider {ProviderId}, trying next provider",
                    providerId);
                providerErrors[providerId] = ex;
                _healthMonitor.RecordFailure(providerId, ProviderErrorType.ServiceError);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // User cancellation, don't try other providers
                throw;
            }
            catch (Exception ex)
            {
                var errorType = ClassifyError(ex);
                if (errorType == ProviderErrorType.InvalidRequest)
                {
                    throw;
                }

                var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);

                _logger?.LogWarning(
                    "Provider {ProviderId} failed for {DataType} with error type {ErrorType}, trying next provider. ExceptionType: {ExceptionType}. Reason: {Reason}",
                    providerId,
                    dataType,
                    errorType,
                    ex.GetType().Name,
                    sanitizedMessage);
                providerErrors[providerId] = ex;
                if (errorType != ProviderErrorType.NotSupported)
                {
                    _healthMonitor.RecordFailure(providerId, errorType);
                }
            }
        }

        // All providers failed
        var totalFailoverTime = Stopwatch.GetElapsedTime(startTime);
        _logger?.LogError(
            "All providers failed for {DataType} after {Duration}ms. Attempted: {AttemptedProviders}",
            dataType, totalFailoverTime.TotalMilliseconds, string.Join(" → ", attemptedProviders));

        throw new ProviderFailoverException(dataType, providerErrors, attemptedProviders, tierFailures);
    }

    /// <summary>
    /// Executes a provider operation by aggregating successful provider responses.
    /// </summary>
    private async Task<string> ExecuteWithAggregationAsync(
        string dataType,
        Func<IStockDataProvider, CancellationToken, Task<string>> operation,
        CancellationToken cancellationToken)
    {
        var result = await ExecuteWithAggregationResultAsync(dataType, operation, cancellationToken);
        return result.Result;
    }

    private async Task<ProviderResult> ExecuteWithAggregationResultAsync(
        string dataType,
        Func<IStockDataProvider, CancellationToken, Task<string>> operation,
        CancellationToken cancellationToken)
    {
        var startTime = Stopwatch.GetTimestamp();
        var providerChain = GetProviderChain(dataType);
        var attemptedProviders = new List<string>();
        var providerErrors = new Dictionary<string, Exception>();
        var tierFailures = new List<TierFailureDetail>();
        var executionTasks = new List<Task<AggregationProviderResult>>();

        _logger?.LogDebug("Executing {DataType} aggregation with provider chain: {ProviderChain}",
            dataType, string.Join(" → ", providerChain));

        foreach (var providerId in providerChain)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attemptedProviders.Add(providerId);

            var healthStatus = _healthMonitor.GetHealthStatus(providerId);
            if (!healthStatus.IsHealthy)
            {
                _logger?.LogWarning(
                    "Skipping unhealthy provider {ProviderId} for aggregated {DataType} (failures: {ConsecutiveFailures})",
                    providerId, dataType, healthStatus.ConsecutiveFailures);
                continue;
            }

            if (!_providers.TryGetValue(providerId, out var provider))
            {
                _logger?.LogWarning("Provider {ProviderId} not found in registry", providerId);
                continue;
            }

            var configuredTier = GetProviderTier(providerId);
            if (!IsProviderTierCapable(provider, configuredTier, dataType))
            {
                _logger?.LogInformation(
                    "Skipping provider {ProviderId} for aggregated {DataType}: unsupported on tier {Tier}",
                    providerId,
                    dataType,
                    configuredTier);

                tierFailures.Add(new TierFailureDetail(
                    provider.ProviderName,
                    ToCapabilityKey(dataType),
                    configuredTier,
                    GetProviderUpgradeUrl(provider.ProviderId)));
                continue;
            }

            if (!_circuitBreakers.TryGetValue(providerId, out var circuitBreaker))
            {
                _logger?.LogWarning("Circuit breaker not found for provider {ProviderId}", providerId);
                continue;
            }

            executionTasks.Add(ExecuteAggregationProviderRequestAsync(
                providerId,
                provider,
                circuitBreaker,
                dataType,
                operation,
                cancellationToken));
        }

        if (executionTasks.Count == 0)
        {
            throw new ProviderFailoverException(dataType, providerErrors, attemptedProviders, tierFailures);
        }

        var results = await Task.WhenAll(executionTasks);
        var successfulResponses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in results)
        {
            if (result.IsTerminal && result.Error != null)
            {
                throw result.Error;
            }

            if (result.Success)
            {
                successfulResponses[result.ProviderId] = result.Response;
            }
            else if (result.Error != null)
            {
                providerErrors[result.ProviderId] = result.Error;
            }
        }

        if (successfulResponses.Count == 0)
        {
            var totalFailoverTime = Stopwatch.GetElapsedTime(startTime);
            _logger?.LogError(
                "All providers failed for aggregated {DataType} after {Duration}ms. Attempted: {AttemptedProviders}",
                dataType, totalFailoverTime.TotalMilliseconds, string.Join(" → ", attemptedProviders));

            throw new ProviderFailoverException(dataType, providerErrors, attemptedProviders, tierFailures);
        }

        if (providerErrors.Count > 0)
        {
            _logger?.LogWarning(
                "Aggregated {DataType} completed with partial failures. Success: {SuccessCount}, Failures: {FailureCount}",
                dataType,
                successfulResponses.Count,
                providerErrors.Count);
        }

        var orderedSuccessfulResponses = providerChain
            .Where(id => successfulResponses.ContainsKey(id))
            .ToDictionary(id => id, id => successfulResponses[id], StringComparer.OrdinalIgnoreCase);

        var selectedProviderId = orderedSuccessfulResponses.Keys.FirstOrDefault() ?? providerChain.First();

        if (_configuration.NewsDeduplication.Enabled)
        {
            try
            {
                var deduplicated = await _newsDeduplicator.DeduplicateAsync(
                    orderedSuccessfulResponses,
                    _configuration.NewsDeduplication,
                    cancellationToken);
                return new ProviderResult(deduplicated, selectedProviderId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (TimeoutException ex)
            {
                var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
                _logger?.LogWarning(
                    "News deduplication timed out for {DataType}. Returning raw aggregated responses. ExceptionType: {ExceptionType}. Reason: {Reason}",
                    dataType,
                    ex.GetType().Name,
                    sanitizedMessage);
                return new ProviderResult(MergeRawNewsResponses(orderedSuccessfulResponses.Values), selectedProviderId);
            }
            catch (Exception ex)
            {
                var sanitizedMessage = SensitiveDataSanitizer.Sanitize(ex.Message);
                _logger?.LogWarning(
                    "News deduplication failed for {DataType}. Returning raw aggregated responses. ExceptionType: {ExceptionType}. Reason: {Reason}",
                    dataType,
                    ex.GetType().Name,
                    sanitizedMessage);
                return new ProviderResult(MergeRawNewsResponses(orderedSuccessfulResponses.Values), selectedProviderId);
            }
        }

        return new ProviderResult(MergeRawNewsResponses(orderedSuccessfulResponses.Values), selectedProviderId);
    }

    private async Task<AggregationProviderResult> ExecuteAggregationProviderRequestAsync(
        string providerId,
        IStockDataProvider provider,
        CircuitBreaker circuitBreaker,
        string dataType,
        Func<IStockDataProvider, CancellationToken, Task<string>> operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var requestStart = Stopwatch.GetTimestamp();
            var result = await circuitBreaker.ExecuteAsync(
                async ct => await operation(provider, ct),
                cancellationToken);

            _healthMonitor.RecordSuccess(providerId, Stopwatch.GetElapsedTime(requestStart));

            return AggregationProviderResult.Succeeded(providerId, result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var errorType = ClassifyError(ex);
            if (errorType == ProviderErrorType.InvalidRequest)
            {
                return AggregationProviderResult.Terminal(providerId, ex);
            }

            if (errorType != ProviderErrorType.NotSupported)
            {
                _healthMonitor.RecordFailure(providerId, errorType);
            }
            _logger?.LogWarning(
                "Provider {ProviderId} failed during aggregated {DataType} with error type {ErrorType}",
                providerId,
                dataType,
                errorType);
            return AggregationProviderResult.Failed(providerId, ex);
        }
    }

    private bool ShouldAggregateResults(string dataType)
    {
        return _configuration.Routing?.DataTypeRouting != null
               && _configuration.Routing.DataTypeRouting.TryGetValue(dataType, out var routing)
               && routing.AggregateResults;
    }

    private static string MergeRawNewsResponses(IEnumerable<string> responses)
    {
        var blocks = responses
            .Where(response => !string.IsNullOrWhiteSpace(response))
            .Select(response => response.Trim())
            .Where(response => response.Length > 0)
            .ToList();

        return blocks.Count == 0 ? string.Empty : string.Join("\n\n", blocks);
    }

    private sealed class AggregationProviderResult
    {
        public string ProviderId { get; private init; } = string.Empty;
        public bool Success { get; private init; }
        public bool IsTerminal { get; private init; }
        public string Response { get; private init; } = string.Empty;
        public Exception? Error { get; private init; }

        public static AggregationProviderResult Succeeded(string providerId, string response) => new()
        {
            ProviderId = providerId,
            Success = true,
            Response = response
        };

        public static AggregationProviderResult Failed(string providerId, Exception error) => new()
        {
            ProviderId = providerId,
            Success = false,
            Error = error
        };

        public static AggregationProviderResult Terminal(string providerId, Exception error) => new()
        {
            ProviderId = providerId,
            Success = false,
            IsTerminal = true,
            Error = error
        };
    }

    /// <summary>
    /// Gets the ordered list of providers to try for a given data type
    /// </summary>
    private List<string> GetProviderChain(string dataType)
    {
        var chain = new List<string>();
        var enabledProviderIds = _configuration.Providers
            .Where(p => p.Enabled)
            .Select(p => NormalizeProviderId(p.Id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Check if specific routing exists for this data type
        if (_configuration.Routing?.DataTypeRouting != null &&
            _configuration.Routing.DataTypeRouting.TryGetValue(dataType, out var routing))
        {
            if (!string.IsNullOrWhiteSpace(routing.PrimaryProviderId))
            {
                chain.Add(NormalizeProviderId(routing.PrimaryProviderId));
            }

            if (routing.FallbackProviderIds != null)
            {
                chain.AddRange(routing.FallbackProviderIds
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Select(NormalizeProviderId));
            }
        }

        // If no specific routing or chain is empty, use all enabled providers by priority
        if (chain.Count == 0)
        {
            chain.AddRange(_configuration.Providers
                .Where(p => p.Enabled)
                .OrderBy(p => p.Priority)
                .Select(p => NormalizeProviderId(p.Id)));
        }

        // Filter to only providers we actually have registered and remove duplicates
        return chain
            .Where(id => enabledProviderIds.Contains(id))
            .Where(id => _providers.ContainsKey(id))
            .Distinct()
            .ToList();
    }

    private static string NormalizeProviderId(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return providerId;
        }

        return providerId.Trim().ToLowerInvariant() switch
        {
            "alpha_vantage" => "alphavantage",
            "alpha-vantage" => "alphavantage",
            _ => providerId
        };
    }

    /// <summary>
    /// Classifies an exception to a provider error type
    /// </summary>
    private ProviderErrorType ClassifyError(Exception exception)
    {
        return exception switch
        {
            ArgumentException => ProviderErrorType.InvalidRequest,
            TierAwareNotSupportedException => ProviderErrorType.NotSupported,
            NotSupportedException => ProviderErrorType.NotSupported,
            TaskCanceledException or TimeoutException => ProviderErrorType.Timeout,
            HttpRequestException httpEx when httpEx.StatusCode.HasValue && ((int)httpEx.StatusCode.Value == 401 || (int)httpEx.StatusCode.Value == 403)
                => ProviderErrorType.AuthenticationError,
            HttpRequestException httpEx when httpEx.StatusCode.HasValue && (int)httpEx.StatusCode.Value == 429 
                => ProviderErrorType.RateLimitExceeded,
            HttpRequestException => ProviderErrorType.NetworkError,
            UnauthorizedAccessException => ProviderErrorType.AuthenticationError,
            _ => ProviderErrorType.Unknown
        };
    }

    private string GetProviderTier(string providerId)
    {
        var providerConfig = _configuration.Providers.FirstOrDefault(
            p => string.Equals(NormalizeProviderId(p.Id), providerId, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(providerConfig?.Tier)
            ? "free"
            : providerConfig.Tier;
    }

    private bool IsProviderTierCapable(IStockDataProvider provider, string tier, string dataType)
    {
        var supportedDataTypes = provider.GetSupportedDataTypes(tier);
        if (supportedDataTypes == null || supportedDataTypes.Count == 0)
        {
            // Legacy fallback for providers/tests that do not declare capability sets.
            return true;
        }

        var capabilityKey = ToCapabilityKey(dataType);
        return supportedDataTypes.Contains(capabilityKey, StringComparer.OrdinalIgnoreCase);
    }

    private static string ToCapabilityKey(string dataType)
    {
        return dataType switch
        {
            "HistoricalPrices" => "historical_prices",
            "StockInfo" => "stock_info",
            "News" => "news",
            "MarketNews" => "market_news",
            "StockActions" => "stock_actions",
            "FinancialStatement" => "financial_statement",
            "HolderInfo" => "holder_info",
            "OptionExpirationDates" => "option_expiration_dates",
            "OptionChain" => "option_chain",
            "Recommendations" => "recommendations",
            _ => dataType.Trim().ToLowerInvariant()
        };
    }

    private static string GetProviderUpgradeUrl(string providerId)
    {
        return NormalizeProviderId(providerId) switch
        {
            "finnhub" => ProviderUpgradeUrls.FinnhubPricing,
            "alphavantage" => ProviderUpgradeUrls.AlphaVantagePremium,
            _ => string.Empty
        };
    }

    /// <summary>
    /// Gets health status for all providers
    /// </summary>
    public async Task<Dictionary<string, bool>> GetProvidersHealthAsync(CancellationToken cancellationToken = default)
    {
        var healthStatus = new Dictionary<string, bool>();
        
        foreach (var provider in _providers.Values)
        {
            try
            {
                var isHealthy = await provider.GetHealthStatusAsync(cancellationToken);
                healthStatus[provider.ProviderId] = isHealthy;
            }
            catch
            {
                healthStatus[provider.ProviderId] = false;
            }
        }
        
        return healthStatus;
    }

    /// <summary>
    /// Gets detailed health status including metrics for all providers
    /// </summary>
    public Dictionary<string, ProviderHealthStatus> GetDetailedHealthStatus()
    {
        return _healthMonitor.GetAllHealthStatus();
    }

    /// <summary>
    /// Gets circuit breaker metrics for a specific provider
    /// </summary>
    public CircuitBreakerMetrics? GetCircuitBreakerMetrics(string providerId)
    {
        return _circuitBreakers.TryGetValue(providerId, out var cb) 
            ? cb.GetMetrics() 
            : null;
    }
}
