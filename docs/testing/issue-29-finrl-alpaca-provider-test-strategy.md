# Test Strategy: Alpaca Markets Provider (FinRL Data Source)

<!--
  Template owner: Test Architect
  Output directory: docs/testing/
  Filename convention: issue-29-finrl-alpaca-provider-test-strategy.md
  Related Issue: #29
-->

## Document Info

- **Feature Spec**: [docs/features/issue-29-finrl-provider.md](../features/issue-29-finrl-provider.md)
- **Architecture**: [Stock Data Aggregation Architecture](../architecture/stock-data-aggregation-canonical-architecture.md)
- **Security**: [Security Summary](../security/security-summary.md)
- **Coding Standards**: [Testing Standards](../coding-standards/testing.md)
- **Status**: Draft
- **Last Updated**: 2026-03-22

---

## Test Strategy Overview

Issue #29 adds Alpaca Markets as a new `IStockDataProvider` implementation, consisting of three primary components: `IAlpacaClient` / `AlpacaClient` (HTTP transport), `AlpacaProvider` (business logic and response mapping), and DI registration plus MCP tool integration. The provider supports historical prices (OHLCV), real-time quotes, ticker news, and market news, while throwing `NotSupportedException` (or `TierAwareNotSupportedException`) for unsupported endpoints such as options, financial statements, and holder info.

The test strategy follows the project's established patterns:

- **Unit tests** use MSTest v2 with Moq (for `AlpacaProvider` tests mocking `IAlpacaClient`) and a `StubHttpMessageHandler` (for `AlpacaClient` tests mocking HTTP responses). Tests use `GivenCondition_WhenAction_ThenExpectedResult` naming and Arrange-Act-Assert layout.
- **MCP server tests** validate that Alpaca appears in `list_providers` output and that provider-selection routing works correctly when Alpaca is requested.
- **Integration tests** call the real Alpaca API using paper-trading (sandbox) credentials gated by `ALPACA_API_KEY_ID` / `ALPACA_API_SECRET` environment variables. Tests skip gracefully via `Assert.Inconclusive` when credentials are absent.

All seven user stories (34 Given/When/Then scenarios) from the feature spec are mapped to specific test cases below. Coverage targets are ≥80% line coverage for `AlpacaProvider` and `AlpacaClient`, and 100% coverage of all GWT scenarios.

---

## Scope

### In Scope

- `IAlpacaClient` interface and `AlpacaClient` implementation — HTTP interactions, authentication headers, HTTPS enforcement, request URI construction, response deserialization, error mapping
- `AlpacaProvider` — `IStockDataProvider` implementation, period/interval mapping, response JSON formatting, `NotSupportedException` for unsupported methods, tier-aware capability reporting
- Configuration loading — `AlpacaApiKeyId`, `AlpacaApiSecret`, `AlpacaBaseUrl` from `appsettings.local.json` via `SecretValue`
- DI registration in `Program.cs`
- MCP tool integration — `list_providers` metadata, provider selection routing for "alpaca" keyword
- Health check — `/v2/account` endpoint validation, healthy/unhealthy status, credential validation
- Tier detection — free vs paid account, capability restrictions
- Error handling — invalid symbols, authentication errors, rate limiting, network failures, unsupported endpoints
- Retry logic — exponential backoff for transient failures (5xx, network timeout)

### Out of Scope

- Trading/order execution (Alpaca trading API not included per feature spec)
- WebSocket/streaming data
- Cryptocurrency data
- Options data (deferred to future enhancement)
- Other FinRL data sources (Quandl, Binance, etc.)
- Direct Python/FinRL interop
- Load testing and performance benchmarking beyond basic response-time assertions
- UI or Claude Desktop end-to-end testing (manual only)

---

## Test Levels

### Unit Tests

- **Target**: `AlpacaClient` (HTTP transport), `AlpacaProvider` (business logic), configuration loading, tier detection
- **Coverage goal**: ≥80% line coverage for `AlpacaProvider` and `AlpacaClient`; 100% for error paths and unsupported-method paths
- **Framework**: MSTest v2
- **Mocking strategy**: `StubHttpMessageHandler` for `AlpacaClient` tests (matching `FinnhubClientTests` pattern); Moq for `AlpacaProvider` tests mocking `IAlpacaClient` (matching `FinnhubProviderTests` pattern)
- **Location**: `StockData.Net.Tests/Clients/AlpacaClientTests.cs`, `StockData.Net.Tests/Providers/AlpacaProviderTests.cs`

### Integration Tests — MCP Server (Mocked Providers)

- **Target**: `list_providers` includes Alpaca with correct metadata; provider selection routes to Alpaca when requested; MCP tool calls produce correct JSON schema
- **Coverage goal**: Alpaca appears in provider list with accurate `id`, `displayName`, `aliases`, `supportedDataTypes`, and tier annotation
- **Framework**: MSTest v2 with mocked `IStockDataProvider` instances
- **Location**: `StockData.Net.McpServer.Tests/`

### Integration Tests — Live API

- **Target**: Real Alpaca API calls for historical prices, quotes, news, health check, and error scenarios
- **Coverage goal**: All six live-API acceptance criteria (AC5–AC8, AC10, AC11) validated against real Alpaca sandbox responses
- **Framework**: MSTest v2, `[TestCategory("Integration")]` + `[TestCategory("LiveAPI")]`
- **Environment**: CI with `ALPACA_API_KEY` and `ALPACA_SECRET_KEY` secrets; local with `secrets.json` or user secrets; paper-trading (sandbox) base URL
- **Flakiness control**: Wrap in `ExecuteLiveApiTestAsync` helper (matching existing pattern in `FinnhubIntegrationTests` and `AlphaVantageIntegrationTests`) to skip on credential failure or rate limiting without failing the build
- **Location**: `StockData.Net.IntegrationTests/AlpacaIntegrationTests.cs`

### Performance Tests

- **Tools**: Inline `System.Diagnostics.Stopwatch` in dedicated MSTest methods
- **Criteria**: Historical price request completes within 2 seconds (P95); quote request completes within 500ms (P95) — validated in integration tests with real API when credentials are available

---

## Given/When/Then Scenario Coverage

### Traceability Matrix

| Spec Scenario | Description | Test Case ID(s) | Test Level | Status |
| --- | --- | --- | --- | --- |
| 1.1 | Valid credentials → provider initializes with HTTPS | TC-C-001, TC-C-002 | Unit | Not Started |
| 1.2 | Free tier credentials → tier "free" with restrictions | TC-P-012, TC-I-008 | Unit, Integration | Not Started |
| 1.3 | Paid tier credentials → tier "paid" with full access | TC-P-013 | Unit | Not Started |
| 1.4 | Missing credentials → provider excluded, warning logged | TC-P-001 | Unit | Not Started |
| 1.5 | Invalid credentials → health check returns unhealthy | TC-C-010, TC-I-007 | Unit, Integration | Not Started |
| 2.1 | Valid symbol + period/interval → OHLCV JSON array | TC-P-002, TC-I-001 | Unit, Integration | Not Started |
| 2.2 | Period mapping ("1d","5d","1mo",etc.) → date range | TC-C-003 | Unit | Not Started |
| 2.3 | Interval mapping ("1m","5m","15m","1h","1d") → timeframe | TC-C-004 | Unit | Not Started |
| 2.4 | Invalid symbol → ProviderException 404 | TC-C-005, TC-P-003, TC-I-004 | Unit, Integration | Not Started |
| 2.5 | Free tier + restricted intraday → ProviderException with upgrade guidance | TC-C-006, TC-P-014 | Unit | Not Started |
| 3.1 | Valid symbol → quote JSON with bid/ask/price | TC-P-004, TC-I-002 | Unit, Integration | Not Started |
| 3.2 | Free tier → IEX exchange attribution | TC-P-015 | Unit | Not Started |
| 3.3 | Paid tier → SIP consolidated data | TC-P-016 | Unit | Not Started |
| 3.4 | Invalid/delisted symbol → ProviderException 404 | TC-P-005, TC-I-005 | Unit, Integration | Not Started |
| 4.1 | Ticker news → formatted articles with headlines | TC-P-006, TC-I-003 | Unit, Integration | Not Started |
| 4.2 | Market news (no ticker) → market-wide articles | TC-P-007, TC-I-006 | Unit, Integration | Not Started |
| 4.3 | No news for symbol → empty result without error | TC-P-008 | Unit | Not Started |
| 4.4 | Rate limit exceeded → ProviderException with retry info | TC-C-007 | Unit | Not Started |
| 5.1 | list_providers includes Alpaca with correct metadata | TC-S-001 | MCP Server | Not Started |
| 5.2 | Natural language "alpaca" → routes to Alpaca provider | TC-S-002 | MCP Server | Not Started |
| 5.3 | Alpaca is default provider → selected automatically | TC-S-003 | MCP Server | Not Started |
| 5.4 | Alpaca unhealthy → router skips unless explicitly requested | TC-P-009 | Unit | Not Started |
| 6.1 | Valid credentials → health check "healthy" | TC-C-008, TC-I-007 | Unit, Integration | Not Started |
| 6.2 | Network failure → health check "unhealthy" + reason | TC-C-009 | Unit | Not Started |
| 6.3 | Auth error → health check "unhealthy" + "invalid_credentials" | TC-C-010 | Unit | Not Started |
| 6.4 | Rate limiting during operation → logged with retry guidance | TC-C-007 | Unit | Not Started |
| 6.5 | All API calls → structured telemetry logged | TC-P-010 | Unit | Not Started |
| 7.1 | GetSupportedDataTypes → includes expected data types | TC-P-011 | Unit | Not Started |
| 7.2 | Free tier → data types marked with tier restrictions | TC-P-012 | Unit | Not Started |
| 7.3 | Paid tier → full capabilities without restrictions | TC-P-013 | Unit | Not Started |
| 7.4 | Unsupported method → NotSupportedException with message | TC-P-017 through TC-P-022 | Unit | Not Started |

### Acceptance Criteria Coverage

| AC | Description | Test Case ID(s) | Test Level |
| --- | --- | --- | --- |
| AC1 | AlpacaProvider implements IStockDataProvider | TC-P-000 | Unit |
| AC2 | IAlpacaClient / AlpacaClient exist in Clients/Alpaca/ | TC-C-000 | Unit |
| AC3 | AlpacaProvider registered in DI | TC-S-001 | MCP Server |
| AC4 | Configuration section with apiKey, secretKey | TC-P-001 | Unit |
| AC5 | GetHistoricalPricesAsync returns OHLCV JSON | TC-P-002, TC-I-001 | Unit, Integration |
| AC6 | GetStockInfoAsync returns quote info | TC-P-004, TC-I-002 | Unit, Integration |
| AC7 | GetNewsAsync returns news articles | TC-P-006, TC-I-003 | Unit, Integration |
| AC8 | GetMarketNewsAsync returns market news | TC-P-007, TC-I-006 | Unit, Integration |
| AC9 | Unsupported methods throw NotSupportedException | TC-P-017 through TC-P-022 | Unit |
| AC10 | Invalid symbol → ProviderException 404 | TC-P-003, TC-I-004 | Unit, Integration |
| AC11 | Invalid credentials → health unhealthy | TC-C-010, TC-I-007 | Unit, Integration |
| AC12 | list_providers shows Alpaca with metadata | TC-S-001 | MCP Server |
| AC13 | Natural language "alpaca" routes correctly | TC-S-002 | MCP Server |
| AC14 | Credentials use SecretValue, not logged | TC-C-001, TC-C-011 | Unit |
| AC15 | All API calls use HTTPS | TC-C-002 | Unit |
| AC16 | Unit tests with >80% coverage | All TC-C-*, TC-P-* | Unit |
| AC17 | Integration tests exist | All TC-I-* | Integration |
| AC18 | README updated (manual review) | — | Manual |
| AC19 | Exponential backoff for transient failures | TC-C-012 | Unit |
| AC20 | Tier detection (free vs paid) | TC-P-012, TC-P-013, TC-I-008 | Unit, Integration |
| AC21 | Health check cached 5 minutes | TC-P-023 | Unit |

---

## Test Cases

### AlpacaClient Unit Tests (`StockData.Net.Tests/Clients/AlpacaClientTests.cs`)

These tests mock HTTP responses using `StubHttpMessageHandler` (matching the `FinnhubClientTests` pattern) to validate URI construction, authentication headers, response deserialization, and error mapping.

---

#### TC-C-000: `GivenAlpacaClientClass_WhenInspected_ThenImplementsIAlpacaClientInterface`

- **Scenario**: AC2
- **Level**: Unit
- **Priority**: Critical
- **Input**: Reflection or compilation check
- **Expected Result**: `AlpacaClient` implements `IAlpacaClient`
- **Pass Criteria**: Code compiles; `typeof(IAlpacaClient).IsAssignableFrom(typeof(AlpacaClient))`

---

#### TC-C-001: `GivenValidCredentials_WhenCreatingClient_ThenAuthHeadersAreSet`

- **Scenario**: 1.1, AC14
- **Level**: Unit
- **Priority**: Critical
- **Input**: `AlpacaClient` constructed with `SecretValue("test-key-id")` and `SecretValue("test-secret")`; make a stub request
- **Expected Result**: HTTP request includes `APCA-API-KEY-ID` and `APCA-API-SECRET-KEY` headers with correct values
- **Pass Criteria**: `StubHttpMessageHandler` captures request; `Assert.AreEqual("test-key-id", request.Headers["APCA-API-KEY-ID"])`; `Assert.AreEqual("test-secret", request.Headers["APCA-API-SECRET-KEY"])`

---

#### TC-C-002: `GivenNonHttpsBaseAddress_WhenCreatingClient_ThenThrowsInvalidOperationException`

- **Scenario**: 1.1, AC15
- **Level**: Unit
- **Priority**: Critical
- **Input**: `HttpClient` with `BaseAddress = new Uri("http://api.alpaca.test/")`
- **Expected Result**: Constructor throws `InvalidOperationException` with message containing "requires HTTPS"
- **Pass Criteria**: `Assert.ThrowsExactly<InvalidOperationException>(() => new AlpacaClient(httpClient, ...)); StringAssert.Contains(ex.Message, "requires HTTPS")`

---

#### TC-C-003: `GivenVariousPeriods_WhenRequestingHistoricalPrices_ThenDateRangesAreCorrectlyCalculated`

- **Scenario**: 2.2
- **Level**: Unit
- **Priority**: High
- **Input**: Parametrized test with periods `"1d"`, `"5d"`, `"1mo"`, `"3mo"`, `"6mo"`, `"1y"`, `"2y"`, `"5y"`
- **Expected Result**: Each period maps to the correct start/end date parameters in the request URI (e.g., "1mo" → start date 1 month ago from today)
- **Pass Criteria**: `StubHttpMessageHandler` captures request URI; `StringAssert.Contains(requestedUri, expectedStartParam)`

---

#### TC-C-004: `GivenVariousIntervals_WhenRequestingHistoricalPrices_ThenTimeframeIsMappedCorrectly`

- **Scenario**: 2.3
- **Level**: Unit
- **Priority**: High
- **Input**: Parametrized test with intervals `"1m"` → `"1Min"`, `"5m"` → `"5Min"`, `"15m"` → `"15Min"`, `"1h"` → `"1Hour"`, `"1d"` → `"1Day"`
- **Expected Result**: Request URI contains the correct Alpaca timeframe parameter
- **Pass Criteria**: `StringAssert.Contains(requestedUri, expectedTimeframe)` for each mapping

---

#### TC-C-005: `GivenInvalidSymbol_WhenRequestingHistoricalPrices_ThenThrowsProviderException`

- **Scenario**: 2.4
- **Level**: Unit
- **Priority**: Critical
- **Input**: Stub returns `HttpStatusCode.NotFound` (404)
- **Expected Result**: Client throws `ProviderException` with message containing "not found" and HTTP status 404
- **Pass Criteria**: `Assert.ThrowsExactlyAsync<ProviderException>(...); StringAssert.Contains(ex.Message, "not found")`

---

#### TC-C-006: `GivenFreeTierSubscriptionError_WhenRequestingRestrictedData_ThenThrowsProviderExceptionWithUpgradeGuidance`

- **Scenario**: 2.5
- **Level**: Unit
- **Priority**: High
- **Input**: Stub returns `HttpStatusCode.UnprocessableEntity` (422) with subscription error body
- **Expected Result**: Client throws `ProviderException` with message containing "paid Alpaca subscription" and tier upgrade guidance
- **Pass Criteria**: `StringAssert.Contains(ex.Message, "paid Alpaca subscription")`

---

#### TC-C-007: `GivenRateLimitExceeded_WhenMakingApiCall_ThenThrowsProviderExceptionWithRetryInfo`

- **Scenario**: 4.4, 6.4
- **Level**: Unit
- **Priority**: High
- **Input**: Stub returns `HttpStatusCode.TooManyRequests` (429) with `Retry-After: 30` header
- **Expected Result**: Client throws `ProviderException` with message containing "rate limit exceeded" and retry-after information
- **Pass Criteria**: `StringAssert.Contains(ex.Message, "rate limit"); StringAssert.Contains(ex.Message, "30")`

---

#### TC-C-008: `GivenHealthyApi_WhenCheckingHealth_ThenReturnsHealthyStatus`

- **Scenario**: 6.1
- **Level**: Unit
- **Priority**: Critical
- **Input**: Stub returns `HttpStatusCode.OK` with valid account JSON from `/v2/account`
- **Expected Result**: Health check returns status "healthy"
- **Pass Criteria**: `Assert.AreEqual("healthy", result.Status)`

---

#### TC-C-009: `GivenNetworkTimeout_WhenCheckingHealth_ThenReturnsUnhealthyWithReason`

- **Scenario**: 6.2
- **Level**: Unit
- **Priority**: High
- **Input**: Stub throws `TaskCanceledException` (simulating timeout)
- **Expected Result**: Health check returns status "unhealthy" with reason "network_timeout"
- **Pass Criteria**: `Assert.AreEqual("unhealthy", result.Status); Assert.AreEqual("network_timeout", result.Reason)`

---

#### TC-C-010: `GivenInvalidCredentials_WhenCheckingHealth_ThenReturnsUnhealthyWithInvalidCredentials`

- **Scenario**: 1.5, 6.3
- **Level**: Unit
- **Priority**: Critical
- **Input**: Stub returns `HttpStatusCode.Unauthorized` (401) or `HttpStatusCode.Forbidden` (403)
- **Expected Result**: Health check returns status "unhealthy" with reason "invalid_credentials"
- **Pass Criteria**: `Assert.AreEqual("unhealthy", result.Status); Assert.AreEqual("invalid_credentials", result.Reason)`

---

#### TC-C-011: `GivenSecretValueCredentials_WhenLoggingApiCall_ThenCredentialsAreMasked`

- **Scenario**: AC14
- **Level**: Unit
- **Priority**: High
- **Input**: Client constructed with `SecretValue` credentials; make a stub request with a mock `ILogger`
- **Expected Result**: Log output does not contain the raw API key; masked format shows only first 4 and last 4 characters
- **Pass Criteria**: Log entries do not contain the full key string; log entries contain masked format (e.g., `"test****cret"`)

---

#### TC-C-012: `GivenTransientServerError_WhenMakingApiCall_ThenRetriesWithExponentialBackoff`

- **Scenario**: AC19
- **Level**: Unit
- **Priority**: Medium
- **Input**: Stub returns `HttpStatusCode.ServiceUnavailable` (503) for first two calls, then `HttpStatusCode.OK` with valid response on third call
- **Expected Result**: Client retries up to 3 times with increasing delays; third call succeeds
- **Pass Criteria**: `StubHttpMessageHandler` records 3 total requests; final response is the success payload

---

#### TC-C-013: `GivenBarsEndpoint_WhenRequestingHistoricalPrices_ThenUsesCorrectApiPath`

- **Scenario**: FR8
- **Level**: Unit
- **Priority**: High
- **Input**: Request historical prices for "AAPL" with valid parameters
- **Expected Result**: Request URI matches `/v2/stocks/AAPL/bars` pattern
- **Pass Criteria**: `StringAssert.Contains(requestedUri, "/v2/stocks/AAPL/bars")`

---

#### TC-C-014: `GivenLatestQuoteEndpoint_WhenRequestingQuote_ThenUsesCorrectApiPath`

- **Scenario**: FR8
- **Level**: Unit
- **Priority**: High
- **Input**: Request quote for "MSFT"
- **Expected Result**: Request URI matches `/v2/stocks/MSFT/quotes/latest`
- **Pass Criteria**: `StringAssert.Contains(requestedUri, "/v2/stocks/MSFT/quotes/latest")`

---

#### TC-C-015: `GivenNewsEndpoint_WhenRequestingTickerNews_ThenUsesCorrectApiPath`

- **Scenario**: FR8
- **Level**: Unit
- **Priority**: High
- **Input**: Request news for "TSLA"
- **Expected Result**: Request URI matches `/v1beta1/news` with `symbols=TSLA` parameter
- **Pass Criteria**: `StringAssert.Contains(requestedUri, "/v1beta1/news"); StringAssert.Contains(requestedUri, "symbols=TSLA")`

---

#### TC-C-016: `GivenNewsEndpoint_WhenRequestingMarketNews_ThenUsesCorrectApiPath`

- **Scenario**: FR8
- **Level**: Unit
- **Priority**: High
- **Input**: Request market news (no specific symbol)
- **Expected Result**: Request URI matches `/v1beta1/news` without symbol filter
- **Pass Criteria**: `StringAssert.Contains(requestedUri, "/v1beta1/news")`

---

### AlpacaProvider Unit Tests (`StockData.Net.Tests/Providers/AlpacaProviderTests.cs`)

These tests mock `IAlpacaClient` using Moq (matching `FinnhubProviderTests` pattern) to validate provider business logic, response formatting, and error handling.

---

#### TC-P-000: `GivenAlpacaProviderClass_WhenInspected_ThenImplementsIStockDataProviderInterface`

- **Scenario**: AC1
- **Level**: Unit
- **Priority**: Critical
- **Input**: Reflection check
- **Expected Result**: `AlpacaProvider` implements `IStockDataProvider`
- **Pass Criteria**: `Assert.IsTrue(typeof(IStockDataProvider).IsAssignableFrom(typeof(AlpacaProvider)))`

---

#### TC-P-001: `GivenMissingCredentials_WhenProviderInitializes_ThenProviderIsExcludedFromPool`

- **Scenario**: 1.4, AC4
- **Level**: Unit
- **Priority**: Critical
- **Input**: Configuration with empty/null `apiKey` or `secretKey`
- **Expected Result**: Provider is not registered; a warning is logged containing "Alpaca provider not configured"
- **Pass Criteria**: Provider is not in the registered provider set; mock logger verifies warning message

---

#### TC-P-002: `GivenValidSymbol_WhenGetHistoricalPricesAsync_ThenReturnsOhlcvJsonArray`

- **Scenario**: 2.1, AC5
- **Level**: Unit
- **Priority**: Critical
- **Input**: Mock client returns list of OHLCV bar objects for "AAPL" with period "1mo", interval "1d"
- **Expected Result**: Provider returns JSON array; each element has `date`, `open`, `high`, `low`, `close`, `volume`, `sourceProvider: "alpaca"` fields
- **Pass Criteria**: `JsonDocument.Parse(result)` succeeds; root is array; first element has all required properties; `sourceProvider` equals `"alpaca"`

---

#### TC-P-003: `GivenClientThrowsNotFound_WhenGetHistoricalPricesAsync_ThenThrowsProviderException`

- **Scenario**: 2.4, AC10
- **Level**: Unit
- **Priority**: Critical
- **Input**: Mock client throws `ProviderException` with 404 for "INVALID_SYMBOL"
- **Expected Result**: Provider propagates the `ProviderException` with message "Symbol 'INVALID_SYMBOL' not found"
- **Pass Criteria**: `Assert.ThrowsExactlyAsync<ProviderException>(...); StringAssert.Contains(ex.Message, "INVALID_SYMBOL")`

---

#### TC-P-004: `GivenValidSymbol_WhenGetStockInfoAsync_ThenReturnsQuoteJson`

- **Scenario**: 3.1, AC6
- **Level**: Unit
- **Priority**: Critical
- **Input**: Mock client returns quote with price, bid, ask, timestamp for "MSFT"
- **Expected Result**: Provider returns JSON with `symbol`, `price`, `bidPrice`, `askPrice`, `bidSize`, `askSize`, `timestamp`, `exchange`, `sourceProvider: "alpaca"` fields
- **Pass Criteria**: `JsonDocument.Parse(result)` succeeds; all required fields present; `sourceProvider` equals `"alpaca"`

---

#### TC-P-005: `GivenInvalidSymbol_WhenGetStockInfoAsync_ThenThrowsProviderException`

- **Scenario**: 3.4
- **Level**: Unit
- **Priority**: High
- **Input**: Mock client throws `ProviderException` for delisted/invalid symbol
- **Expected Result**: Provider throws `ProviderException` with message "Quote unavailable for symbol"
- **Pass Criteria**: `StringAssert.Contains(ex.Message, "Quote unavailable")`

---

#### TC-P-006: `GivenValidTicker_WhenGetNewsAsync_ThenReturnsFormattedArticles`

- **Scenario**: 4.1, AC7
- **Level**: Unit
- **Priority**: Critical
- **Input**: Mock client returns list of news items for "TSLA"
- **Expected Result**: Provider returns formatted text with `Title:`, `Published:`, `Summary:`, and `URL:` fields
- **Pass Criteria**: `StringAssert.Contains(result, "Title:"); StringAssert.Contains(result, "Published:"); StringAssert.Contains(result, "URL:")`

---

#### TC-P-007: `GivenNoTicker_WhenGetMarketNewsAsync_ThenReturnsMarketWideArticles`

- **Scenario**: 4.2, AC8
- **Level**: Unit
- **Priority**: Critical
- **Input**: Mock client returns list of market-wide news items
- **Expected Result**: Provider returns formatted text with headlines and timestamps
- **Pass Criteria**: `Assert.IsFalse(string.IsNullOrWhiteSpace(result)); StringAssert.Contains(result, "Title:")`

---

#### TC-P-008: `GivenNoNewsForSymbol_WhenGetNewsAsync_ThenReturnsEmptyResultMessage`

- **Scenario**: 4.3
- **Level**: Unit
- **Priority**: Medium
- **Input**: Mock client returns empty list for "OBSCURE_TICKER"
- **Expected Result**: Provider returns message "No news available for symbol 'OBSCURE_TICKER'" without throwing
- **Pass Criteria**: `StringAssert.Contains(result, "No news available")`

---

#### TC-P-009: `GivenUnhealthyProvider_WhenRouterSelectsProviders_ThenAlpacaIsSkippedUnlessExplicit`

- **Scenario**: 5.4
- **Level**: Unit
- **Priority**: High
- **Input**: Mock Alpaca provider returns unhealthy health check; router has other available providers
- **Expected Result**: Router skips Alpaca and uses next healthy provider; if Alpaca was explicitly requested, error propagates
- **Pass Criteria**: When not explicit: result `sourceProvider` is not `"alpaca"`; when explicit: exception is thrown

---

#### TC-P-010: `GivenApiCallCompletes_WhenLogging_ThenStructuredTelemetryIsEmitted`

- **Scenario**: 6.5
- **Level**: Unit
- **Priority**: Medium
- **Input**: Mock client returns success for any call; mock `ILogger` captures log events
- **Expected Result**: Log entry contains: request type, symbol, latency, HTTP status, provider tier
- **Pass Criteria**: Mock logger verifies structured log call with expected fields

---

#### TC-P-011: `GivenAlpacaProvider_WhenGetSupportedDataTypes_ThenReturnsExpectedTypes`

- **Scenario**: 7.1
- **Level**: Unit
- **Priority**: Critical
- **Input**: Call `GetSupportedDataTypes()` on configured `AlpacaProvider`
- **Expected Result**: List includes `"historical_prices"`, `"stock_info"`, `"news"`, `"market_news"` at minimum
- **Pass Criteria**: `CollectionAssert.Contains(result, "historical_prices"); CollectionAssert.Contains(result, "stock_info"); CollectionAssert.Contains(result, "news"); CollectionAssert.Contains(result, "market_news")`

---

#### TC-P-012: `GivenFreeTierAccount_WhenGetSupportedDataTypes_ThenTierRestrictionsAreIndicated`

- **Scenario**: 1.2, 7.2, AC20
- **Level**: Unit
- **Priority**: High
- **Input**: Provider configured as tier "free"
- **Expected Result**: Supported data types include tier restriction notes (e.g., "limited to IEX data")
- **Pass Criteria**: Provider metadata `Tier` equals `"free"`; returned data types annotation marks IEX limitation

---

#### TC-P-013: `GivenPaidTierAccount_WhenGetSupportedDataTypes_ThenFullCapabilitiesReturned`

- **Scenario**: 1.3, 7.3
- **Level**: Unit
- **Priority**: High
- **Input**: Provider configured as tier "paid"
- **Expected Result**: Supported data types list has no tier restriction annotations
- **Pass Criteria**: Provider metadata `Tier` equals `"paid"`; data types do not contain "limited" or restriction text

---

#### TC-P-014: `GivenFreeTier_WhenRequestingRestrictedIntradayData_ThenThrowsProviderExceptionWithUpgradeGuidance`

- **Scenario**: 2.5
- **Level**: Unit
- **Priority**: High
- **Input**: Provider configured as free tier; mock client throws subscription error for intraday request
- **Expected Result**: `ProviderException` message includes "requires a paid Alpaca subscription"
- **Pass Criteria**: `StringAssert.Contains(ex.Message, "paid Alpaca subscription")`

---

#### TC-P-015: `GivenFreeTier_WhenGetStockInfoAsync_ThenReturnsIexExchangeAttribution`

- **Scenario**: 3.2
- **Level**: Unit
- **Priority**: Medium
- **Input**: Provider configured as free tier; mock client returns quote with IEX exchange
- **Expected Result**: Response JSON `exchange` field indicates IEX source
- **Pass Criteria**: `JsonDocument` parse; `exchange` property value indicates IEX

---

#### TC-P-016: `GivenPaidTier_WhenGetStockInfoAsync_ThenReturnsSipConsolidatedData`

- **Scenario**: 3.3
- **Level**: Unit
- **Priority**: Medium
- **Input**: Provider configured as paid tier; mock client returns quote with SIP data
- **Expected Result**: Response JSON reflects consolidated multi-exchange data
- **Pass Criteria**: Quote metadata does not constrain to single exchange

---

#### TC-P-017: `GivenAlpacaProvider_WhenGetStockActionsAsync_ThenThrowsNotSupportedException`

- **Scenario**: 7.4, AC9, FR9
- **Level**: Unit
- **Priority**: Critical
- **Input**: Call `GetStockActionsAsync("AAPL")`
- **Expected Result**: Throws `NotSupportedException` (or `TierAwareNotSupportedException`) with message listing supported data types
- **Pass Criteria**: Exception thrown; message contains "not supported by Alpaca Markets"

---

#### TC-P-018: `GivenAlpacaProvider_WhenGetFinancialStatementAsync_ThenThrowsNotSupportedException`

- **Scenario**: 7.4, AC9, FR9
- **Level**: Unit
- **Priority**: Critical
- **Input**: Call `GetFinancialStatementAsync("AAPL", FinancialStatementType.IncomeStatement)`
- **Expected Result**: Throws `NotSupportedException` with message listing supported data types
- **Pass Criteria**: Exception thrown; message contains "not supported by Alpaca Markets"

---

#### TC-P-019: `GivenAlpacaProvider_WhenGetHolderInfoAsync_ThenThrowsNotSupportedException`

- **Scenario**: 7.4, AC9, FR9
- **Level**: Unit
- **Priority**: Critical
- **Input**: Call `GetHolderInfoAsync("AAPL", HolderType.MajorHolders)`
- **Expected Result**: Throws `NotSupportedException` with message listing supported data types
- **Pass Criteria**: Exception thrown; message contains "not supported by Alpaca Markets"

---

#### TC-P-020: `GivenAlpacaProvider_WhenGetOptionExpirationDatesAsync_ThenThrowsNotSupportedException`

- **Scenario**: 7.4, AC9, FR9
- **Level**: Unit
- **Priority**: Critical
- **Input**: Call `GetOptionExpirationDatesAsync("AAPL")`
- **Expected Result**: Throws `NotSupportedException` with message listing supported data types
- **Pass Criteria**: Exception thrown; message contains "not supported by Alpaca Markets"

---

#### TC-P-021: `GivenAlpacaProvider_WhenGetOptionChainAsync_ThenThrowsNotSupportedException`

- **Scenario**: 7.4, AC9, FR9
- **Level**: Unit
- **Priority**: Critical
- **Input**: Call `GetOptionChainAsync("AAPL", "2026-12-18", OptionType.Calls)`
- **Expected Result**: Throws `NotSupportedException` with message listing supported data types
- **Pass Criteria**: Exception thrown; message contains "not supported by Alpaca Markets"

---

#### TC-P-022: `GivenAlpacaProvider_WhenGetRecommendationsAsync_ThenThrowsNotSupportedException`

- **Scenario**: 7.4, AC9, FR9
- **Level**: Unit
- **Priority**: Critical
- **Input**: Call `GetRecommendationsAsync("AAPL", RecommendationType.Recommendations)`
- **Expected Result**: Throws `NotSupportedException` with message listing supported data types
- **Pass Criteria**: Exception thrown; message contains "not supported by Alpaca Markets"

---

#### TC-P-023: `GivenHealthCheckWithinCachePeriod_WhenCheckingHealthAgain_ThenCachedResultReturned`

- **Scenario**: AC21
- **Level**: Unit
- **Priority**: Medium
- **Input**: First health check returns "healthy"; immediately call health check again within 5-minute window
- **Expected Result**: Mock client's health endpoint is called only once; second call returns cached result
- **Pass Criteria**: `_client.Verify(c => c.CheckHealthAsync(...), Times.Once)` after two `GetHealthStatusAsync` calls

---

#### TC-P-024: `GivenNullClient_WhenConstructingProvider_ThenThrowsArgumentNullException`

- **Scenario**: Defensive construction
- **Level**: Unit
- **Priority**: High
- **Input**: `new AlpacaProvider(null!, logger)`
- **Expected Result**: Throws `ArgumentNullException`
- **Pass Criteria**: `Assert.ThrowsExactly<ArgumentNullException>(...)`

---

#### TC-P-025: `GivenNullLogger_WhenConstructingProvider_ThenThrowsArgumentNullException`

- **Scenario**: Defensive construction
- **Level**: Unit
- **Priority**: High
- **Input**: `new AlpacaProvider(client, null!)`
- **Expected Result**: Throws `ArgumentNullException`
- **Pass Criteria**: `Assert.ThrowsExactly<ArgumentNullException>(...)`

---

### MCP Server Tests (`StockData.Net.McpServer.Tests/`)

---

#### TC-S-001: `GivenAlpacaProviderRegistered_WhenCallingListProviders_ThenAlpacaAppearsWithCorrectMetadata`

- **Scenario**: 5.1, AC3, AC12
- **Level**: MCP Server (mocked providers)
- **Priority**: Critical
- **Input**: Server constructed with mock Alpaca provider (ProviderId = "alpaca"); call `list_providers`
- **Expected Result**: Response includes provider entry with `id: "alpaca"`, `displayName: "Alpaca Markets"`, tier information, and `supportedDataTypes` containing `"historical_prices"`, `"stock_info"`, `"news"`, `"market_news"`
- **Pass Criteria**: Deserialize response; find entry with `id == "alpaca"`; verify `displayName`, `supportedDataTypes` array, tier information

---

#### TC-S-002: `GivenAlpacaProviderRegistered_WhenNaturalLanguageRequestMentionsAlpaca_ThenRoutesToAlpacaProvider`

- **Scenario**: 5.2, AC13
- **Level**: MCP Server
- **Priority**: Critical
- **Input**: Request with prompt containing "get alpaca data on AAPL"; Alpaca mock provider registered
- **Expected Result**: Router selects Alpaca; response `serviceKey` equals `"alpaca"`
- **Pass Criteria**: `Assert.AreEqual("alpaca", response.ServiceKey)` or response JSON contains `"sourceProvider": "alpaca"`

---

#### TC-S-003: `GivenAlpacaIsDefaultProvider_WhenRequestDoesNotSpecifyProvider_ThenAlpacaIsSelected`

- **Scenario**: 5.3
- **Level**: MCP Server
- **Priority**: High
- **Input**: Configuration sets Alpaca as default provider; request without explicit provider specification
- **Expected Result**: Router selects Alpaca automatically
- **Pass Criteria**: Response contains `sourceProvider: "alpaca"`

---

### Integration Tests — Live API (`StockData.Net.IntegrationTests/AlpacaIntegrationTests.cs`)

All integration tests follow the established pattern from `FinnhubIntegrationTests` and `AlphaVantageIntegrationTests`: credentials resolved from environment variables (`ALPACA_API_KEY_ID`, `ALPACA_API_SECRET`) or user secrets; `Assert.Inconclusive` when credentials are absent; `ExecuteLiveApiTestAsync` wrapper to catch unauthorized/rate-limit errors gracefully.

---

#### TC-I-001: `GivenAlpacaCredentials_WhenGetHistoricalPricesAsync_ThenReturnsOhlcvArray`

- **Scenario**: 2.1, AC5
- **Level**: Integration (Live API)
- **Priority**: Critical
- **Category**: `[TestCategory("Integration")], [TestCategory("LiveAPI")]`
- **Input**: `GetHistoricalPricesAsync("AAPL", "1mo", "1d")` with real Alpaca sandbox credentials
- **Expected Result**: JSON array with at least 1 element; each element has `date`, `open`, `high`, `low`, `close`, `volume`; `sourceProvider` equals `"alpaca"`
- **Pass Criteria**: `JsonDocument.Parse(result)` succeeds; `document.RootElement.ValueKind == JsonValueKind.Array`; `document.RootElement.GetArrayLength() > 0`; required properties present

---

#### TC-I-002: `GivenAlpacaCredentials_WhenGetStockInfoAsync_ThenReturnsQuoteSchema`

- **Scenario**: 3.1, AC6
- **Level**: Integration (Live API)
- **Priority**: Critical
- **Category**: `[TestCategory("Integration")], [TestCategory("LiveAPI")]`
- **Input**: `GetStockInfoAsync("MSFT")` with real Alpaca sandbox credentials
- **Expected Result**: JSON with `symbol`, `price`, `timestamp`, `sourceProvider` fields
- **Pass Criteria**: `Assert.AreEqual("MSFT", document.RootElement.GetProperty("symbol").GetString()); Assert.AreEqual("alpaca", document.RootElement.GetProperty("sourceProvider").GetString())`

---

#### TC-I-003: `GivenAlpacaCredentials_WhenGetNewsAsync_ThenReturnsFormattedContent`

- **Scenario**: 4.1, AC7
- **Level**: Integration (Live API)
- **Priority**: Critical
- **Category**: `[TestCategory("Integration")], [TestCategory("LiveAPI")]`
- **Input**: `GetNewsAsync("AAPL")` with real Alpaca sandbox credentials
- **Expected Result**: Non-empty string with `Title:`, `Published:`, `URL:` sections
- **Pass Criteria**: `Assert.IsFalse(string.IsNullOrWhiteSpace(result)); StringAssert.Contains(result, "Title:"); StringAssert.Contains(result, "URL:")`

---

#### TC-I-004: `GivenAlpacaCredentials_WhenRequestingInvalidSymbol_ThenThrowsProviderException`

- **Scenario**: 2.4, AC10
- **Level**: Integration (Live API)
- **Priority**: Critical
- **Category**: `[TestCategory("Integration")], [TestCategory("LiveAPI")]`
- **Input**: `GetHistoricalPricesAsync("INVALID123XYZ", "1mo", "1d")` with real Alpaca credentials
- **Expected Result**: `ProviderException` thrown with 404 or symbol-not-found indication
- **Pass Criteria**: `Assert.ThrowsExactlyAsync<ProviderException>(...)` or inconclusive if credentials missing

---

#### TC-I-005: `GivenAlpacaCredentials_WhenRequestingQuoteForInvalidSymbol_ThenThrowsProviderException`

- **Scenario**: 3.4
- **Level**: Integration (Live API)
- **Priority**: High
- **Category**: `[TestCategory("Integration")], [TestCategory("LiveAPI")]`
- **Input**: `GetStockInfoAsync("INVALID123XYZ")` with real Alpaca credentials
- **Expected Result**: `ProviderException` with "Quote unavailable" message
- **Pass Criteria**: Exception message contains "unavailable" or 404 indication

---

#### TC-I-006: `GivenAlpacaCredentials_WhenGetMarketNewsAsync_ThenReturnsContent`

- **Scenario**: 4.2, AC8
- **Level**: Integration (Live API)
- **Priority**: Critical
- **Category**: `[TestCategory("Integration")], [TestCategory("LiveAPI")]`
- **Input**: `GetMarketNewsAsync()` with real Alpaca sandbox credentials
- **Expected Result**: Non-empty formatted string with news articles
- **Pass Criteria**: `Assert.IsFalse(string.IsNullOrWhiteSpace(result)); StringAssert.Contains(result, "Title:")`

---

#### TC-I-007: `GivenAlpacaCredentials_WhenHealthCheckWithValidCredentials_ThenReturnsHealthy`

- **Scenario**: 6.1, AC11 (healthy path)
- **Level**: Integration (Live API)
- **Priority**: Critical
- **Category**: `[TestCategory("Integration")], [TestCategory("LiveAPI")]`
- **Input**: `GetHealthStatusAsync()` with valid Alpaca sandbox credentials
- **Expected Result**: Health status "healthy"
- **Pass Criteria**: `Assert.AreEqual("healthy", result.Status)` or inconclusive if credentials missing

---

#### TC-I-008: `GivenFreeTierAlpacaAccount_WhenInitialized_ThenTierIsDetectedAsFree`

- **Scenario**: 1.2, AC20
- **Level**: Integration (Live API)
- **Priority**: High
- **Category**: `[TestCategory("Integration")], [TestCategory("LiveAPI")]`
- **Input**: Provider initialized with free-tier Alpaca sandbox credentials
- **Expected Result**: Provider reports tier as "free"
- **Pass Criteria**: Provider metadata `Tier` equals `"free"` or inconclusive if credentials missing

---

## Test Data

### Mock Response Fixtures

All mock HTTP responses are defined inline within test methods (matching the `FinnhubClientTests` and `AlphaVantageClientTests` pattern using `StubHttpMessageHandler`). No external fixture files required.

**Historical Prices (Bars) Response**:

```json
{
  "bars": [
    {"t":"2026-03-01T00:00:00Z","o":150.0,"h":155.0,"l":149.0,"c":154.0,"v":1000000},
    {"t":"2026-03-02T00:00:00Z","o":154.0,"h":158.0,"l":153.0,"c":157.0,"v":1200000}
  ]
}
```

**Latest Quote Response**:

```json
{
  "quote": {"ap":155.50,"as":200,"bp":155.45,"bs":100,"t":"2026-03-22T15:30:00Z","x":"V"}
}
```

**News Response**:

```json
{
  "news": [
    {"id":1,"headline":"AAPL Earnings Beat","summary":"Apple reported...","author":"Reuters","created_at":"2026-03-22T10:00:00Z","url":"https://example.com/article","symbols":["AAPL"]}
  ]
}
```

**Account (Health Check) Response**:

```json
{
  "id":"test-account-id","account_number":"PA1234","status":"ACTIVE","currency":"USD"
}
```

### `StubHttpMessageHandler` Pattern

Reuse the same `StubHttpMessageHandler` inner class from `FinnhubClientTests`:

```csharp
private sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(responder(request));
}
```

### Test Isolation

- Each unit test constructs its own `AlpacaClient` or `AlpacaProvider` instance via `[TestInitialize]`
- No shared mutable state between test cases
- No file system or network I/O in unit tests
- Integration tests create a new provider in `[TestInitialize]` and skip via `Assert.Inconclusive` when credentials are absent
- Integration tests use Alpaca paper-trading (sandbox) endpoint to avoid rate limit impact on production

### Credential Resolution (Integration Tests)

Follow the established pattern from `AlphaVantageIntegrationTests`:

1. Check environment variable `ALPACA_API_KEY_ID` / `ALPACA_API_SECRET`
2. Fall back to user secrets / `secrets.json`
3. Check for `Providers:Alpaca:ApiKeyId` / `Providers:Alpaca:ApiSecret` in config
4. If placeholder (`missing-from-github-secrets`), skip with `Assert.Inconclusive`

---

## CI/CD Integration

### Test Stages

| Pipeline Stage | Tests Executed | Gate Policy |
| --- | --- | --- |
| PR Build | All unit tests (TC-C-*, TC-P-*) + MCP server tests (TC-S-*) | Must pass; no merge if any fail |
| CI Build (main) | All unit tests + MCP server tests + integration tests (TC-I-*) | Must pass (integration tests may be inconclusive if secrets not configured) |
| Release Build | All tests including integration | Must pass; inconclusive integration tests are acceptable |

### Gate Policy

- All unit tests must pass with zero failures
- Integration tests that return `Inconclusive` (missing credentials) do not block the build
- Integration tests that fail with actual errors (not credential issues) block the build
- Coverage report must show ≥80% line coverage for `AlpacaProvider` and `AlpacaClient`

### Flaky Test Policy

- Integration tests are wrapped in `ExecuteLiveApiTestAsync` to handle transient API failures gracefully
- Rate-limit failures result in `Assert.Inconclusive`, not test failure
- If an integration test is consistently flaky (>3 failures in 7 days), it is quarantined with `[Ignore("Quarantined: ...")]` and a tracking issue is created

---

## Coverage Targets

| Metric | Target |
| --- | --- |
| Line coverage — `AlpacaClient` | ≥80% |
| Line coverage — `AlpacaProvider` | ≥80% |
| GWT scenario coverage | 100% (all 34 scenarios mapped) |
| Acceptance criteria coverage | 100% (AC1–AC21 mapped) |
| Critical path coverage | 100% (all happy-path flows tested at unit + integration level) |
| Unsupported method coverage | 100% (all 6 unsupported methods tested) |

---

## New Test File / Class Placement

| Test Class | File | Covers |
| --- | --- | --- |
| `AlpacaClientTests` (new) | `StockData.Net.Tests/Clients/AlpacaClientTests.cs` | TC-C-000 through TC-C-016 |
| `AlpacaProviderTests` (new) | `StockData.Net.Tests/Providers/AlpacaProviderTests.cs` | TC-P-000 through TC-P-025 |
| Existing `ListProvidersToolTests` | `StockData.Net.McpServer.Tests/ListProvidersToolTests.cs` | TC-S-001 (extend existing tests) |
| Existing `StockDataMcpServerTests` | `StockData.Net.McpServer.Tests/McpServerTests.cs` | TC-S-002, TC-S-003 (extend existing tests) |
| `AlpacaIntegrationTests` (new) | `StockData.Net.IntegrationTests/AlpacaIntegrationTests.cs` | TC-I-001 through TC-I-008 |

---

## Implementation Prerequisites

The following must exist before tests can be written:

1. `IAlpacaClient` interface in `StockData.Net/Clients/Alpaca/IAlpacaClient.cs`
2. `AlpacaClient` class in `StockData.Net/Clients/Alpaca/AlpacaClient.cs`
3. `AlpacaProvider` class in `StockData.Net/Providers/AlpacaProvider.cs`
4. Alpaca response model records (bars, quote, news, account) in `StockData.Net/Models/`
5. DI registration in `StockData.Net.McpServer/Program.cs`
6. Configuration entries for `AlpacaApiKeyId`, `AlpacaApiSecret`, `AlpacaBaseUrl` in `appsettings.json` template

Tests can be written test-first (TDD) against the interfaces before the implementation classes exist.

---

## Related Documents

- Feature Specification: [docs/features/issue-29-finrl-provider.md](../features/issue-29-finrl-provider.md)
- Architecture Overview: [docs/architecture/stock-data-aggregation-canonical-architecture.md](../architecture/stock-data-aggregation-canonical-architecture.md)
- Security Summary: [docs/security/security-summary.md](../security/security-summary.md)
- Coding Standards — Testing: [docs/coding-standards/testing.md](../coding-standards/testing.md)
- Coding Standards — C#: [docs/coding-standards/csharp.md](../coding-standards/csharp.md)
- Tier Handling Test Strategy: [docs/testing/issue-32-tier-handling-test-strategy.md](issue-32-tier-handling-test-strategy.md)
- List Providers Test Strategy: [docs/testing/list-providers-tool-test-strategy.md](list-providers-tool-test-strategy.md)
