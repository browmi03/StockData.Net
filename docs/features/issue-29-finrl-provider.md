# Feature: Alpaca Markets Provider (FinRL Data Source)

<!--
  Template owner: Product Manager
  Output directory: docs/features/
  Filename convention: issue-29-finrl-provider.md
  GitHub Issue: #29
-->

## Overview

Add Alpaca Markets as a supported data provider in the StockData.Net MCP server. Alpaca Markets is one of the primary data sources used by the FinRL (Financial Reinforcement Learning) library and provides free and paid access to historical market data (OHLCV), real-time quotes, market news, and portfolio management capabilities via a REST API. This integration enables users to leverage FinRL-compatible data sources within our provider selection architecture.

## Problem Statement

Users working with quantitative finance and reinforcement learning frameworks like FinRL need access to the same data sources that those libraries use. FinRL itself is Python-based and wraps multiple data sources including Yahoo Finance, Alpaca, and Quandl. Direct integration with FinRL would require Python interop (subprocess calls or HTTP wrappers), introducing significant complexity, deployment challenges, and performance overhead.

Instead, integrating **Alpaca Markets directly** as a native .NET provider solves the core need: giving users access to FinRL-compatible historical market data through a clean, performant, tier-aware provider that follows our existing architecture patterns. Alpaca offers both free (with real-time IEX data) and paid tiers, making it accessible to a wide range of users while supporting advanced use cases.

This addresses:

- **FinRL ecosystem compatibility**: Users can work with the same data sources FinRL uses
- **Quantitative research workflows**: Historical OHLCV data is essential for backtesting trading strategies
- **Provider diversity**: Adds redundancy and choice for users concerned about API rate limits or data quality from specific sources
- **Free tier accessibility**: Alpaca's free tier provides substantial functionality without requiring paid API keys

## User Stories

### User Story 1: As a quantitative researcher, I want to configure Alpaca API credentials in the MCP server so that I can access Alpaca's market data

> 1.1 Given I have created an Alpaca account and obtained API credentials (key and secret key), when I add `apiKey` and `secretKey` to `appsettings.local.json` in the `DataProviders` section, then the AlpacaProvider is successfully initialized at server startup with HTTPS-enforced configuration
>
> 1.2 Given I want to use Alpaca's free tier, when I provide API credentials from a free Alpaca account, then the provider initializes successfully and marks itself as tier "free" with appropriate capability restrictions (no extended historical data beyond Alpaca's free tier limits)
>
> 1.3 Given I want to use Alpaca's paid tier, when I provide API credentials from an Alpaca Unlimited account, then the provider initializes successfully and marks itself as tier "paid" with full historical data access capabilities
>
> 1.4 Given the `apiKey` or `secretKey` is missing from configuration, when the server starts, then the AlpacaProvider logs a warning "Alpaca provider not configured" and is excluded from the available provider pool
>
> 1.5 Given invalid Alpaca API credentials are provided in configuration, when the server attempts a health check on startup, then the provider marks itself as unavailable, logs the authentication error, and returns health status "unhealthy" with reason "invalid_credentials"

### User Story 2: As a quantitative researcher, I want to retrieve historical OHLCV price data from Alpaca so that I can backtest trading strategies using FinRL-compatible data

> 2.1 Given Alpaca provider is configured with valid credentials, when I request historical prices for a valid symbol (e.g., "AAPL") with period "1mo" and interval "1d", then the provider returns a JSON array of OHLCV bars with fields: `date`, `open`, `high`, `low`, `close`, `volume`, and metadata including `sourceProvider: "alpaca"`
>
> 2.2 Given Alpaca provider is configured, when I request historical prices with various periods ("1d", "5d", "1mo", "3mo", "6mo", "1y", "2y", "5y"), then the provider correctly converts each period into appropriate Alpaca API date range parameters (start and end dates)
>
> 2.3 Given Alpaca provider is configured, when I request historical prices with various intervals ("1m", "5m", "15m", "1h", "1d"), then the provider correctly maps each interval to Alpaca's timeframe parameter (e.g., "1Min", "5Min", "15Min", "1Hour", "1Day")
>
> 2.4 Given I request historical data for an invalid symbol (e.g., "INVALID_SYMBOL"), when Alpaca API returns a 404 or symbol not found error, then the provider throws a `ProviderException` with message "Symbol 'INVALID_SYMBOL' not found" and HTTP status 404
>
> 2.5 Given I am using the free tier and request intraday data (e.g., interval "1m") older than 15 minutes, when Alpaca API denies access with a subscription error, then the provider throws a `ProviderException` with message "Intraday data for this period requires a paid Alpaca subscription" and includes tier upgrade guidance

### User Story 3: As a quantitative researcher, I want to retrieve real-time and delayed stock quotes from Alpaca so that I can get current market prices for analysis

> 3.1 Given Alpaca provider is configured, when I request stock info for a valid symbol (e.g., "MSFT"), then the provider calls Alpaca's latest quote endpoint and returns JSON with fields: `symbol`, `price`, `bidPrice`, `askPrice`, `bidSize`, `askSize`, `timestamp`, `exchange`, and `sourceProvider: "alpaca"`
>
> 3.2 Given I am using the free tier, when I request stock info, then the provider returns IEX (Investors Exchange) real-time data with appropriate exchange attribution
>
> 3.3 Given I am using the paid tier, when I request stock info, then the provider returns SIP (Securities Information Processor) consolidated data with higher accuracy and broader exchange coverage
>
> 3.4 Given I request stock info for a symbol that is not currently trading (e.g., delisted or invalid), when Alpaca API returns an error, then the provider throws a `ProviderException` with message "Quote unavailable for symbol '[SYMBOL]'" and HTTP status 404

### User Story 4: As a quantitative researcher, I want to retrieve market news from Alpaca so that I can incorporate sentiment analysis into my models

> 4.1 Given Alpaca provider is configured, when I request news for a specific ticker (e.g., "TSLA"), then the provider calls Alpaca's news endpoint and returns formatted text with article headlines, summaries, timestamps, and source URLs
>
> 4.2 Given Alpaca provider is configured, when I request general market news without specifying a ticker, then the provider returns the latest market-wide news articles with timestamps and related tickers
>
> 4.3 Given Alpaca API returns no news for the requested symbol, when I request news, then the provider returns an empty result with message "No news available for symbol '[SYMBOL]'" without throwing an error
>
> 4.4 Given Alpaca API rate limits are exceeded, when I request news, then the provider throws a `ProviderException` with message "Alpaca rate limit exceeded. Retry after [X] seconds" and includes retry-after information if provided by Alpaca

### User Story 5: As a quantitative researcher, I want the Alpaca provider to integrate with the MCP server's provider selection system so that I can explicitly request Alpaca data or let it be selected automatically

> 5.1 Given Alpaca provider is registered in the DI container, when I query the `list_providers` MCP tool, then Alpaca appears in the provider list with `providerId: "alpaca"`, `providerName: "Alpaca Markets"`, tier information (free or paid based on credentials), and supported data types
>
> 5.2 Given I make a natural language request specifying Alpaca (e.g., "get alpaca data on AAPL"), when the router interprets the request, then the system routes exclusively to the Alpaca provider and returns results with `serviceKey: "alpaca"`
>
> 5.3 Given I make a request without specifying a provider and Alpaca is configured as the default provider in `appsettings.json`, when the router selects a provider, then Alpaca is chosen and the result includes `serviceKey: "alpaca"`
>
> 5.4 Given Alpaca provider is configured but currently unhealthy (e.g., API down), when the router attempts provider selection with Alpaca as a candidate, then the router skips Alpaca and logs "Alpaca provider unhealthy, excluding from selection" unless Alpaca was explicitly requested by the user (in which case the error propagates to the user)

### User Story 6: As a system administrator, I want the Alpaca provider to implement health checks and observability so that I can monitor its availability and diagnose issues

> 6.1 Given Alpaca provider is configured with valid credentials, when the system performs a health check, then the provider calls Alpaca's account endpoint (or a lightweight status endpoint) and returns health status "healthy" if successful
>
> 6.2 Given Alpaca provider encounters network failures or timeout, when the health check executes, then the provider returns health status "unhealthy" with reason "network_timeout" and logs the error details
>
> 6.3 Given Alpaca provider encounters authentication errors, when the health check executes, then the provider returns health status "unhealthy" with reason "invalid_credentials" and logs the error
>
> 6.4 Given Alpaca provider encounters rate limiting errors during normal operation, when an API call is made, then the provider logs the rate limit details (current limit, reset time) and bubbles the error to the caller with retry guidance
>
> 6.5 Given Alpaca provider executes any API call, when the call completes (success or failure), then the provider logs structured telemetry including: request type, symbol, latency, HTTP status, provider tier, and error details if applicable

### User Story 7: As a quantitative researcher, I want the Alpaca provider to report which data types it supports so that I can understand its capabilities compared to other providers

> 7.1 Given Alpaca provider is queried for supported data types, when `GetSupportedDataTypes()` is called, then the provider returns a list including at minimum: "historical_prices", "stock_info", "news", "market_news"
>
> 7.2 Given Alpaca provider is using the free tier, when `GetSupportedDataTypes()` is called, then the returned list clearly marks tier restrictions (e.g., "historical_prices" with note "limited to IEX data")
>
> 7.3 Given Alpaca provider is using the paid tier, when `GetSupportedDataTypes()` is called, then the returned list includes full capabilities without tier restrictions
>
> 7.4 Given Alpaca provider does not support certain data types from `IStockDataProvider` (e.g., options data, financial statements), when those methods are called, then the provider throws `NotSupportedException` with message "[Method] is not supported by Alpaca Markets. Supported data types: [list]"

## Requirements

### Functional Requirements

1. **FR1**: The system shall implement a new `AlpacaProvider` class that implements the `IStockDataProvider` interface
2. **FR2**: The system shall create a corresponding `IAlpacaClient` interface and `AlpacaClient` implementation in `StockData.Net/Clients/Alpaca/`
3. **FR3**: The `AlpacaClient` shall communicate with Alpaca Markets REST API v2 using HTTPS exclusively
4. **FR4**: The system shall register `AlpacaProvider` in the DI container in `StockData.Net.McpServer/Program.cs`
4. **FR5**: The system shall read Alpaca API credentials (`apiKey`, `secretKey`) from `appsettings.local.json` using the `SecretValue` pattern for secure storage
6. **FR6**: The system shall support both Alpaca paper trading (sandbox) and live (production) API endpoints through configuration
7. **FR7**: The system shall implement rate limiting awareness and respect Alpaca's API rate limits (200 requests per minute for free tier, higher for paid tiers)
8. **FR8**: The system shall map the following `IStockDataProvider` methods to Alpaca API endpoints:
   - `GetHistoricalPricesAsync` → `/v2/stocks/{symbol}/bars`
   - `GetStockInfoAsync` → `/v2/stocks/{symbol}/quotes/latest`
   - `GetNewsAsync` → `/v1beta1/news?symbols={symbol}`
   - `GetMarketNewsAsync` → `/v1beta1/news`
   - `GetHealthStatusAsync` → `/v2/account` (for credential validation)
9. **FR9**: The system shall throw `NotSupportedException` for unsupported methods: `GetStockActionsAsync`, `GetFinancialStatementAsync`, `GetHolderInfoAsync`, `GetOptionExpirationDatesAsync`, `GetOptionChainAsync`, `GetRecommendationsAsync`
10. **FR10**: The system shall detect tier level (free vs paid) by inspecting the Alpaca account configuration during initialization and set provider metadata accordingly
11. **FR11**: The system shall include comprehensive logging for all Alpaca API interactions (request, response, latency, errors)
12. **FR12**: The system shall include the Alpaca provider in the `list_providers` MCP tool output with accurate capability metadata

### Non-Functional Requirements

- **Performance**: Historical price requests shall complete within 2 seconds for typical queries (1 year daily data). Real-time quote requests shall complete within 500ms under normal network conditions.
- **Security**:
  - API credentials shall be stored using the `SecretValue` type and never logged
  - All communication with Alpaca API shall use HTTPS with TLS 1.2 or higher
  - The system shall validate API credentials on startup and mark the provider as unavailable if invalid
  - API keys shall be masked in logs (show only first 4 and last 4 characters)
- **Reliability**:
  - The provider shall implement exponential backoff retry logic for transient failures (network timeouts, 5xx errors) up to 3 attempts
  - The provider shall handle Alpaca API rate limiting gracefully by parsing `Retry-After` headers and propagating retry guidance to callers
  - The provider shall gracefully degrade when Alpaca API is unavailable (mark as unhealthy, allow other providers to serve requests)
- **Maintainability**:
  - The implementation shall follow the existing provider pattern (separate client interface, provider class, models)
  - Code shall adhere to project C# coding standards including XML documentation, nullable reference types, and async best practices
  - Unit tests shall achieve >80% code coverage for the Alpaca provider and client
- **Observability**:
  - All API calls shall be logged with structured telemetry (operation, symbol, duration, status)
  - Errors shall be logged with full context (request parameters, error message, HTTP status, correlation ID if available)
  - Health check results shall be logged and cached (5-minute cache) to avoid excessive health check API calls

## Acceptance Criteria

<!--
  Each criterion must be objectively testable (pass/fail).
  Evidence requirements define what proves each criterion passes.
-->

- [ ] **[Blocking]** AC1: `AlpacaProvider` class exists and implements all methods of `IStockDataProvider` interface — Evidence: Code review shows class declaration and all interface methods implemented (even if throwing `NotSupportedException` for unsupported methods)

- [ ] **[Blocking]** AC2: `IAlpacaClient` interface and `AlpacaClient` implementation exist in `StockData.Net/Clients/Alpaca/` — Evidence: Files exist and `AlpacaClient` implements `IAlpacaClient` with methods for historical prices, quotes, and news

- [ ] **[Blocking]** AC3: `AlpacaProvider` is registered in DI container in `Program.cs` — Evidence: Code review shows `services.AddSingleton<IStockDataProvider, AlpacaProvider>()` or equivalent registration

- [ ] **[Blocking]** AC4: Configuration section for Alpaca exists in `appsettings.local.json` template with fields `apiKey`, `secretKey`, and optional `baseUrl` — Evidence: Configuration template includes these fields with placeholder values and comments

- [ ] **[Blocking]** AC5: `GetHistoricalPricesAsync` returns valid OHLCV JSON for a known symbol (e.g., "AAPL") over a 1-month period — Evidence: Integration test shows successful API call returning properly structured JSON with date, open, high, low, close, volume fields

- [ ] **[Blocking]** AC6: `GetStockInfoAsync` returns current quote information for a valid symbol — Evidence: Integration test shows successful API call returning quote with price, bid, ask, timestamp

- [ ] **[Blocking]** AC7: `GetNewsAsync` returns news articles for a specific ticker — Evidence: Integration test shows API call returning formatted news with headlines and timestamps

- [ ] **[Blocking]** AC8: `GetMarketNewsAsync` returns general market news — Evidence: Integration test shows API call returning market-wide news articles

- [ ] **[Blocking]** AC9: Provider throws `NotSupportedException` for unsupported methods (financial statements, options, etc.) with clear error message listing supported data types — Evidence: Unit tests verify each unsupported method throws correctly with expected message format

- [ ] **[Blocking]** AC10: Provider handles invalid symbol gracefully by throwing `ProviderException` with 404 status — Evidence: Integration test with invalid symbol (e.g., "INVALID123") results in proper exception

- [ ] **[Blocking]** AC11: Provider handles authentication errors (invalid API key) by marking itself unhealthy during health check — Evidence: Integration test with invalid credentials shows health status "unhealthy" with reason "invalid_credentials"

- [ ] **[Blocking]** AC12: Provider appears in `list_providers` MCP tool output with correct metadata — Evidence: Manual test or integration test of `list_providers` shows Alpaca with `providerId: "alpaca"`, tier information, and supported data types list

- [ ] **[Blocking]** AC13: Provider selection via natural language (e.g., "get alpaca data on AAPL") routes to Alpaca provider — Evidence: Integration test shows request with "alpaca" keyword in prompt results in `serviceKey: "alpaca"` in response

- [ ] **[Blocking]** AC14: All API credentials are handled using `SecretValue` type and not logged in plain text — Evidence: Code review confirms `SecretValue` usage; log inspection shows masked credentials (e.g., "Alpa**************jK9x")

- [ ] **[Blocking]** AC15: All Alpaca API calls use HTTPS — Evidence: Code review shows base URL uses `https://` scheme; integration test network trace confirms TLS usage

- [ ] **[Blocking]** AC16: Unit tests exist for `AlpacaProvider` and `AlpacaClient` with >80% code coverage — Evidence: Test coverage report shows ≥80% line coverage for Alpaca-related classes

- [ ] **[Blocking]** AC17: Integration tests exist for Alpaca provider covering success and error scenarios — Evidence: Integration test file exists (e.g., `AlpacaIntegrationTests.cs`) with tests for valid requests, invalid symbols, authentication errors, and rate limiting

- [ ] **[Blocking]** AC18: README is updated with Alpaca configuration instructions and credential acquisition steps — Evidence: README includes section on Alpaca with links to Alpaca signup, API key generation, and configuration example

- [ ] **[Non-blocking]** AC19: Provider implements exponential backoff retry logic for transient failures — Evidence: Unit tests simulate transient failures (e.g., network timeout) and verify retry attempts with exponential delays

- [ ] **[Non-blocking]** AC20: Provider detects and reports tier level (free vs paid) based on account type — Evidence: Integration test with free account shows tier "free" in provider metadata; test with paid account shows tier "paid"

- [ ] **[Non-blocking]** AC21: Health check is cached for 5 minutes to reduce API call overhead — Evidence: Code review shows caching logic; unit test verifies subsequent health checks within 5 minutes reuse cached result

## Out of Scope

The following items are explicitly **out of scope** for this feature:

- **Direct Python/FinRL integration**: No subprocess calls, Python interop, or HTTP wrappers for FinRL library itself. This feature integrates with Alpaca's REST API directly using native .NET.
- **Trading/order execution**: This feature focuses on **data retrieval only**. Alpaca's trading API (order placement, portfolio management) is not included.
- **Advanced portfolio analytics**: Features like FinRL's portfolio environment, reward functions, or reinforcement learning agent interfaces are not part of this integration.
- **Other FinRL data sources**: This feature adds **Alpaca only**. Other data sources wrapped by FinRL (e.g., Quandl, Binance) are not included. Each would require a separate feature specification.
- **Complex financial statement parsing**: Alpaca does not provide financial statements (income statement, balance sheet, cash flow). These remain unsupported.
- **Options data**: Alpaca does provide options data in their API, but implementing it is deferred to a future enhancement to keep scope manageable.
- **Cryptocurrency data**: While Alpaca supports crypto APIs, this feature focuses on equities only. Crypto support can be added in a future iteration.
- **WebSocket/streaming data**: This feature uses Alpaca's REST API for historical and snapshot data. Real-time streaming via WebSocket is out of scope.
- **Custom indicator calculations**: Any derived indicators (RSI, MACD, Bollinger Bands) are left to client applications or future feature enhancements.

## Dependencies

- **Depends on**:
  - Existing provider selection architecture (from provider-selection.md feature)
  - `IStockDataProvider` interface contract
  - `SecretValue` security infrastructure for API key management
  - Tier handling system (from issue #32) for free vs paid tier detection and capability restrictions
  - DI container registration pattern used by other providers
  - Error handling architecture (from issue #17) for consistent exception handling
  
- **Blocks**:
  - None. This feature is independent and additive. Existing providers continue to function unchanged.

- **External dependencies**:
  - **Alpaca Markets API availability**: Feature requires Alpaca API to be operational. Service outages will cause provider health checks to fail and fallback logic to activate.
  - **Alpaca API versioning**: Feature uses Alpaca API v2 (stocks) and v1beta1 (news). Future breaking changes in Alpaca API may require code updates.
  - **User credentials**: Users must create Alpaca accounts and generate API keys independently. Free tier is sufficient for basic usage.

## Technical Considerations

### High-Level Technical Notes

- **API Design**: Alpaca uses REST API v2 for stock data with standard OAuth2 authentication (key + secret). Client should use `HttpClient` with authentication headers (`APCA-API-KEY-ID`, `APCA-API-SECRET-KEY`).

- **Date/Time Handling**: Alpaca API expects RFC3339 timestamps. Ensure proper conversion from period/interval parameters (e.g., "1mo") to ISO 8601 date ranges. Alpaca returns timestamps in UTC.

- **Rate Limiting**:
  - Free tier: 200 requests per minute
  - Paid tier: Higher limits depending on subscription
  - Implement client-side rate limiter or rely on Polly retry policies with backoff
  - Parse `X-RateLimit-*` headers if available for proactive throttling

- **Data Quality Differences**:
  - Free tier: IEX (Investors Exchange) data — real-time but single exchange
  - Paid tier: SIP (Securities Information Processor) data — consolidated multi-exchange with higher accuracy
  - Document these differences in provider metadata and user-facing docs

- **Error Handling**: Alpaca API returns standard HTTP status codes:
  - 400: Invalid request parameters (bad symbol, invalid date range)
  - 401/403: Authentication or authorization failure
  - 404: Symbol not found
  - 422: Unprocessable entity (e.g., subscription tier restriction)
  - 429: Rate limit exceeded
  - 500/502/503: Alpaca server errors (transient, retry)

  Map these to appropriate `ProviderException` types with clear user-facing messages.

- **Tier Detection Strategy**: During initialization, call Alpaca's `/v2/account` endpoint to inspect account configuration. The `account.status` field and other properties indicate subscription level. Cache this for performance.

- **Testing Strategy**:
  - Unit tests: Mock `IAlpacaClient` to test provider logic without external calls
  - Integration tests: Use Alpaca paper trading (sandbox) credentials for automated tests to avoid rate limits on live API
  - Manual testing: Validate with real Alpaca accounts (both free and paid) before release

- **Backward Compatibility**: This feature adds a new provider without modifying existing ones. No breaking changes to existing functionality.

- **Migration Path**: Users currently using Yahoo Finance or other providers can adopt Alpaca incrementally by adding credentials and using natural language provider selection.

- **Performance Optimization**: Consider implementing a local cache for frequently requested data (e.g., recent price data) with short TTL (5 minutes) to reduce API calls and improve response time.

- **Future Extensibility**:
  - Options data support can be added by implementing `GetOptionExpirationDatesAsync` and `GetOptionChainAsync` using Alpaca's `/v2/options/contracts` endpoint
  - Crypto support can be added with minimal changes by creating a separate crypto-specific provider or extending `AlpacaProvider` with crypto methods
  - WebSocket streaming can be added as a separate feature for real-time data feeds

## Success Metrics

Success will be measured by:

- **Configuration success rate**: % of users who successfully configure Alpaca credentials without errors (target: >95%)
- **API reliability**: % of Alpaca provider API calls that succeed (target: >99% for valid requests)
- **Performance**: P95 response time for historical price requests <2s, quote requests <500ms
- **Adoption**: % of users who use Alpaca vs other providers in the first 30 days (baseline metric, no target)
- **Error rate**: % of requests that fail due to Alpaca-specific errors (authentication, rate limiting, etc.) (target: <1% for properly configured instances)

## Implementation Phases

Given the scope of this feature, consider a phased implementation:

### Phase 1 (MVP): Core Historical Data

- Implement `AlpacaClient` and `AlpacaProvider` skeleton
- Support `GetHistoricalPricesAsync` only
- Basic authentication and error handling
- Unit tests and integration tests for historical prices
- Configuration documentation

### Phase 2: Quotes and News

- Implement `GetStockInfoAsync` for real-time quotes
- Implement `GetNewsAsync` and `GetMarketNewsAsync`
- Integration with provider selection system
- Complete test coverage

### Phase 3: Tier Handling and Observability

- Implement tier detection logic (free vs paid)
- Add health checks and telemetry
- Implement rate limiting awareness
- Performance optimizations (caching, retry logic)

### Phase 4: Documentation and Polish

- Update README and user guides
- Add troubleshooting documentation
- Code review and refinement
- Release preparation

Each phase should be tested and validated before proceeding to the next.
