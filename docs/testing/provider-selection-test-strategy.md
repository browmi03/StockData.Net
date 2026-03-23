# Test Strategy: Provider Selection via Natural Language

## Document Info

- **Feature Spec**: [Provider Selection](../features/provider-selection.md)
- **Architecture**: [Stock Data Aggregation Architecture](../architecture/stock-data-aggregation-canonical-architecture.md)
- **Security Design**: [Security Summary](../security/security-summary.md)
- **Status**: In Review
- **Last Updated**: 2026-03-09

---

## Test Strategy Overview

The Provider Selection feature enables users to specify financial data providers through natural language (e.g., "get yahoo data on AAPL") and returns metadata (`serviceKey`, `tier`) identifying the fulfilling provider. The test strategy validates six user stories encompassing 20 Given/When/Then scenarios across three providers (Yahoo Finance, Finnhub, Alpha Vantage), covering NLP intent parsing, provider routing, validation, metadata assembly, fallback bypass, and configuration handling.

Testing follows the established project patterns: MSTest v2 with Moq, `GivenCondition_WhenAction_ThenExpectedResult` naming, and strict isolation of external API calls to integration test suites. All provider interactions in unit and MCP server tests are mocked at the `IStockDataProvider` interface boundary.

---

## Scope

### In Scope

- Natural language provider intent parsing from user requests
- Provider name validation (recognized, configured, credentialed)
- Routing to explicitly selected providers for all 10 data operations
- Default provider selection from `appsettings.json` when none specified
- `serviceKey` and `tier` metadata in all responses
- Fallback chain and circuit breaker bypass for explicit provider selection
- Error messages for invalid, unconfigured, and failing providers
- Logging of provider selection decisions
- Rate limit metadata propagation (where available)
- Configuration validation at startup (missing/malformed defaults)

### Out of Scope

- Multi-provider aggregation from a single request (feature explicitly excludes this)
- Dynamic provider switching mid-conversation
- Provider recommendation engine
- Per-user provider preferences
- Advanced NLP for ambiguous provider names (e.g., "get apple data")
- UI testing (no UI exists)
- Load testing against live external APIs

---

## Test Levels

### Unit Tests

- **Target**: NLP provider intent parser, provider name validator, metadata assembler, routing logic with explicit provider, configuration loader extensions
- **Coverage goal**: ≥ 90% line coverage for provider selection logic; 100% for validation and error paths
- **Framework**: MSTest v2
- **Mocking strategy**: Moq — mock `IStockDataProvider` for all provider interactions; mock `ILogger` for logging verification; use in-memory `McpConfiguration` objects for configuration tests

### Integration Tests

- **Target**: End-to-end flow from MCP tool call → NLP parsing → provider selection → router invocation → response with metadata; configuration loading from `appsettings.json`
- **Coverage goal**: All 10 MCP tools with explicit provider, default provider, and invalid provider scenarios
- **Framework**: MSTest v2 with mocked HTTP handlers for provider API calls
- **Environment**: Local (mocked HTTP); CI pipeline

### MCP Server Tests

- **Target**: MCP protocol-level tool invocations with provider parameter; error response format; metadata presence in tool call results
- **Coverage goal**: Every MCP tool validates `serviceKey` and `tier` in response; error responses include available providers list
- **Framework**: MSTest v2 against `StockDataMcpServer` with mocked dependencies
- **Environment**: Local; CI pipeline

### Performance Tests

- **Target**: NLP provider intent parsing latency; provider validation overhead
- **Tools**: `System.Diagnostics.Stopwatch` with warmup iterations
- **Criteria**: Provider validation < 10ms; NLP parsing < 50ms per request (per non-functional requirements)

### Security Tests

- **Target**: Error messages do not expose API keys or sensitive configuration; provider name input sanitization; injection prevention in NLP parsing
- **Tools**: MSTest with dedicated security test cases
- **Criteria**: No secrets in error messages; malicious input in provider names handled gracefully

---

## Given/When/Then Scenario Coverage

### Traceability Matrix

| Spec Scenario | Description | Test Case ID(s) | Test Level | Status |
| --- | --- | --- | --- | --- |
| 1.1 | Explicit "yahoo" routes to Yahoo Finance, returns `serviceKey: "yahoo"` | TC-001, TC-002 | Unit, Integration | Not Started |
| 1.2 | Explicit "alpha vantage" routes to Alpha Vantage, returns `serviceKey: "alphavantage"` | TC-003, TC-004 | Unit, Integration | Not Started |
| 1.3 | Explicit "finnhub" routes to Finnhub, returns `serviceKey: "finnhub"` | TC-005, TC-006 | Unit, Integration | Not Started |
| 1.4 | Various phrasings ("from yahoo", "via alpha vantage", "using finnhub") correctly parsed | TC-007 | Unit | Not Started |
| 2.1 | No provider specified → default from `appsettings.json`, `serviceKey` matches default | TC-008, TC-009 | Unit, Integration | Not Started |
| 2.2 | Multiple request types without provider → each uses configured default for that data type | TC-010 | Integration | Not Started |
| 2.3 | Missing/malformed default config → logs error, falls back to Yahoo Finance | TC-011 | Unit | Not Started |
| 3.1 | Invalid provider name → error with available providers list, HTTP 400 | TC-012, TC-013 | Unit, MCP Server | Not Started |
| 3.2 | Valid provider without API credentials → error message, HTTP 503 | TC-014 | Unit, Integration | Not Started |
| 3.3 | Valid provider API call fails → provider-specific error, no failover | TC-015, TC-016 | Unit, Integration | Not Started |
| 4.1 | All successful responses include `serviceKey` | TC-017 | Integration | Not Started |
| 4.2 | Single-provider aggregated data includes single `serviceKey` | TC-018 | Integration | Not Started |
| 4.3 | `serviceKey` is programmatically identifiable in response JSON | TC-019 | Unit | Not Started |
| 5.1 | All successful responses include `tier` field | TC-020 | Integration | Not Started |
| 5.2 | Rate limit metadata included when available | TC-021 | Integration | Not Started |
| 5.3 | Paid-tier provider selection shows correct tier | TC-022 | Unit | Not Started |
| 6.1 | Explicit provider fails → NO fallback, error returned | TC-023, TC-024 | Unit, Integration | Not Started |
| 6.2 | Circuit breaker bypassed for explicit provider | TC-025 | Unit | Not Started |
| 6.3 | Default provider retains standard fallback/circuit breaker behavior | TC-026 | Unit | Not Started |

---

## Test Cases

### TC-001: Explicit Yahoo Provider Selection — Unit

- **Scenario**: 1.1
- **Level**: Unit
- **Priority**: Critical
- **Preconditions**: NLP parser and provider router instantiated with mocked Yahoo Finance provider
- **Input**: Request text "get yahoo data on AAPL"
- **Expected Result**: NLP parser extracts provider intent `"yahoo"`; router invocation targets `yahoo_finance` provider; response metadata contains `serviceKey: "yahoo"`
- **Pass Criteria**: Mocked Yahoo Finance provider receives exactly one invocation; `serviceKey` field equals `"yahoo"`

### TC-002: Explicit Yahoo Provider Selection — Integration

- **Scenario**: 1.1
- **Level**: Integration
- **Priority**: Critical
- **Preconditions**: Full MCP server stack with mocked HTTP for Yahoo Finance API
- **Input**: MCP `tools/call` with `get_stock_info`, ticker "AAPL", provider "yahoo"
- **Expected Result**: Response JSON includes `serviceKey: "yahoo"` and valid stock data
- **Pass Criteria**: Response is successful; `serviceKey` and `tier` fields present and correct

### TC-003: Explicit Alpha Vantage Provider Selection — Unit

- **Scenario**: 1.2
- **Level**: Unit
- **Priority**: Critical
- **Preconditions**: NLP parser and provider router with mocked Alpha Vantage provider
- **Input**: Request text "get alpha vantage prices for MSFT"
- **Expected Result**: NLP parser extracts `"alphavantage"`; router targets Alpha Vantage provider; response metadata contains `serviceKey: "alphavantage"`
- **Pass Criteria**: Mocked Alpha Vantage provider invoked once; `serviceKey` equals `"alphavantage"`

### TC-004: Explicit Alpha Vantage Provider Selection — Integration

- **Scenario**: 1.2
- **Level**: Integration
- **Priority**: Critical
- **Preconditions**: Full stack with mocked HTTP for Alpha Vantage API
- **Input**: MCP `tools/call` with `get_historical_stock_prices`, ticker "MSFT", provider "alphavantage"
- **Expected Result**: Response includes `serviceKey: "alphavantage"` and valid price data
- **Pass Criteria**: Correct provider invoked; metadata fields present

### TC-005: Explicit Finnhub Provider Selection — Unit

- **Scenario**: 1.3
- **Level**: Unit
- **Priority**: Critical
- **Preconditions**: NLP parser and provider router with mocked Finnhub provider
- **Input**: Request text "get finnhub quote for TSLA"
- **Expected Result**: NLP parser extracts `"finnhub"`; router targets Finnhub provider; `serviceKey: "finnhub"`
- **Pass Criteria**: Mocked Finnhub provider invoked once; `serviceKey` equals `"finnhub"`

### TC-006: Explicit Finnhub Provider Selection — Integration

- **Scenario**: 1.3
- **Level**: Integration
- **Priority**: Critical
- **Preconditions**: Full stack with mocked HTTP for Finnhub API
- **Input**: MCP `tools/call` with `get_stock_info`, ticker "TSLA", provider "finnhub"
- **Expected Result**: Response includes `serviceKey: "finnhub"` and valid quote data
- **Pass Criteria**: Correct provider invoked; metadata fields present

### TC-007: Natural Language Phrasing Variations — Unit

- **Scenario**: 1.4
- **Level**: Unit
- **Priority**: High
- **Preconditions**: NLP parser instantiated
- **Input**: Matrix of phrasings per provider (12+ combinations):

| Phrasing Pattern | Input Example | Expected Provider |
| --- | --- | --- |
| "get {provider} data on {ticker}" | "get yahoo data on AAPL" | yahoo |
| "from {provider} get {ticker}" | "from alpha vantage get MSFT" | alphavantage |
| "using {provider} show {ticker}" | "using finnhub show TSLA quote" | finnhub |
| "via {provider}" | "get prices for GOOGL via yahoo" | yahoo |
| "{provider} {ticker}" | "yahoo AAPL" | yahoo |
| "use {provider} for {ticker}" | "use finnhub for AMZN" | finnhub |

- **Expected Result**: NLP parser correctly extracts provider intent for each phrasing
- **Pass Criteria**: ≥ 95% of phrasing variations correctly parsed (per success metrics)

### TC-008: Default Provider When None Specified — Unit

- **Scenario**: 2.1
- **Level**: Unit
- **Priority**: Critical
- **Preconditions**: Configuration loaded with default provider set to `yahoo_finance`
- **Input**: Request "get price for AAPL" (no provider specified)
- **Expected Result**: Router uses `yahoo_finance` (configured default); response includes `serviceKey` matching default
- **Pass Criteria**: Default provider invoked; no other provider attempted first

### TC-009: Default Provider When None Specified — Integration

- **Scenario**: 2.1
- **Level**: Integration
- **Priority**: Critical
- **Preconditions**: Full stack with `appsettings.json` default provider configured
- **Input**: MCP `tools/call` with `get_stock_info`, ticker "AAPL", no provider parameter
- **Expected Result**: Response `serviceKey` matches the primary provider ID from `appsettings.json` routing configuration
- **Pass Criteria**: `serviceKey` matches configured default

### TC-010: Default Provider Per Data Type — Integration

- **Scenario**: 2.2
- **Level**: Integration
- **Priority**: High
- **Preconditions**: Configuration with per-data-type primary providers
- **Input**: Sequential calls to `get_stock_info`, `get_historical_stock_prices`, `get_finance_news`, `get_financial_statement` without provider
- **Expected Result**: Each call routes to the configured `primaryProviderId` for that data type
- **Pass Criteria**: Each response `serviceKey` matches the data-type-specific default from configuration

### TC-011: Missing Default Configuration Fallback — Unit

- **Scenario**: 2.3
- **Level**: Unit
- **Priority**: High
- **Preconditions**: Configuration with empty or malformed routing section
- **Input**: System startup with malformed `appsettings.json` routing
- **Expected Result**: System logs configuration error; falls back to Yahoo Finance as hardcoded default
- **Pass Criteria**: Logger receives error-level message; routing proceeds with `yahoo_finance`

### TC-012: Invalid Provider Name — Unit

- **Scenario**: 3.1
- **Level**: Unit
- **Priority**: Critical
- **Preconditions**: Provider validator with known providers list
- **Input**: Provider name "bloomberg"
- **Expected Result**: Validation returns error: "Provider 'bloomberg' is not configured. Available providers: yahoo, alphavantage, finnhub"
- **Pass Criteria**: Error message matches expected format; no provider invoked; no system crash

### TC-013: Invalid Provider Name — MCP Server Error Format

- **Scenario**: 3.1
- **Level**: MCP Server
- **Priority**: Critical
- **Preconditions**: MCP server with all providers registered
- **Input**: MCP `tools/call` with provider "reuters"
- **Expected Result**: MCP error response with code indicating bad request; message lists available providers
- **Pass Criteria**: Error response format conforms to MCP protocol; available providers listed

### TC-014: Valid Provider Missing API Credentials — Unit

- **Scenario**: 3.2
- **Level**: Unit
- **Priority**: High
- **Preconditions**: Provider registered but credentials missing/empty in configuration
- **Input**: Explicit selection of provider with missing API key
- **Expected Result**: Error: "Provider '{name}' is not available. Missing required API credentials."
- **Pass Criteria**: Error message matches specification; no API call attempted; no secrets exposed

### TC-015: Explicit Provider API Failure — Unit

- **Scenario**: 3.3
- **Level**: Unit
- **Priority**: Critical
- **Preconditions**: Mocked provider configured to throw on API call
- **Input**: Explicit selection of provider that will fail
- **Expected Result**: Error identifies provider and specific failure; no fallback to other providers
- **Pass Criteria**: Only the explicitly selected provider invoked; error includes provider name and failure reason

### TC-016: Explicit Provider API Failure — Integration

- **Scenario**: 3.3
- **Level**: Integration
- **Priority**: Critical
- **Preconditions**: Mocked HTTP returning 429 (rate limit) for selected provider
- **Input**: Explicit selection of rate-limited provider
- **Expected Result**: Error: "Yahoo Finance API error: Rate limit exceeded" (or similar); no fallback
- **Pass Criteria**: Single provider attempt; provider-specific error in response

### TC-017: ServiceKey Present in All Successful Responses — Integration

- **Scenario**: 4.1
- **Level**: Integration
- **Priority**: Critical
- **Preconditions**: Full stack with mocked providers
- **Input**: Calls to all 10 MCP tools with both explicit and default providers
- **Expected Result**: Every successful response contains a non-empty `serviceKey` field
- **Pass Criteria**: 100% of responses include valid `serviceKey`

### TC-018: Single ServiceKey for Aggregated Data — Integration

- **Scenario**: 4.2
- **Level**: Integration
- **Priority**: High
- **Preconditions**: Aggregation enabled for news; explicit provider selected
- **Input**: News request with explicit provider selection
- **Expected Result**: Response contains single `serviceKey` (not multiple)
- **Pass Criteria**: Exactly one `serviceKey` value in response

### TC-019: ServiceKey Programmatically Parseable — Unit

- **Scenario**: 4.3
- **Level**: Unit
- **Priority**: Medium
- **Preconditions**: Response metadata model
- **Input**: JSON response from any tool call
- **Expected Result**: `serviceKey` field is a top-level string in response JSON, deserializable by standard JSON parsers
- **Pass Criteria**: `JsonSerializer.Deserialize` successfully extracts `serviceKey` from response

### TC-020: Tier Metadata in All Responses — Integration

- **Scenario**: 5.1
- **Level**: Integration
- **Priority**: Critical
- **Preconditions**: Providers configured with tier information
- **Input**: Calls to all 10 MCP tools
- **Expected Result**: Every successful response includes `tier` field (e.g., `"free"`, `"paid"`)
- **Pass Criteria**: 100% of responses include non-empty `tier` field

### TC-021: Rate Limit Metadata When Available — Integration

- **Scenario**: 5.2
- **Level**: Integration
- **Priority**: Medium
- **Preconditions**: Provider with rate limit tracking (e.g., Alpha Vantage configured with 5 req/min)
- **Input**: Request to Alpha Vantage
- **Expected Result**: Response metadata includes `rateLimitRemaining` or equivalent
- **Pass Criteria**: Rate limit field present when provider reports it

### TC-022: Paid Tier Indicator — Unit

- **Scenario**: 5.3
- **Level**: Unit
- **Priority**: Medium
- **Preconditions**: Provider with `tier: "paid"` in configuration
- **Input**: Explicit selection of paid-tier provider
- **Expected Result**: Response `tier` field shows `"paid"`
- **Pass Criteria**: `tier` value matches provider configuration

### TC-023: Explicit Provider Failure — No Fallback — Unit

- **Scenario**: 6.1
- **Level**: Unit
- **Priority**: Critical
- **Preconditions**: Router with two providers; mocked primary throws exception
- **Input**: Explicit selection of failing provider
- **Expected Result**: Error returned from selected provider; second provider never invoked
- **Pass Criteria**: Mock verification confirms zero invocations on fallback provider

### TC-024: Explicit Provider Failure — No Fallback — Integration

- **Scenario**: 6.1
- **Level**: Integration
- **Priority**: Critical
- **Preconditions**: Full stack; primary provider HTTP returns 500
- **Input**: MCP tool call with explicit provider
- **Expected Result**: Error response from selected provider only
- **Pass Criteria**: No fallback HTTP calls observed; error identifies the explicitly selected provider

### TC-025: Circuit Breaker Bypassed for Explicit Selection — Unit

- **Scenario**: 6.2
- **Level**: Unit
- **Priority**: Critical
- **Preconditions**: Circuit breaker in Open state for the selected provider
- **Input**: Explicit selection of provider with open circuit breaker
- **Expected Result**: Request still sent to selected provider (bypasses circuit breaker); provider-specific result or error returned
- **Pass Criteria**: Provider invoked despite open circuit breaker state

### TC-026: Default Provider Retains Fallback — Unit

- **Scenario**: 6.3
- **Level**: Unit
- **Priority**: High
- **Preconditions**: Router with default provider and fallback chain; mocked default throws exception
- **Input**: Request without explicit provider (uses default)
- **Expected Result**: After default provider fails, system falls back to next provider in chain (existing behavior preserved)
- **Pass Criteria**: Fallback provider invoked after default provider failure

---

## Test Categories

### Happy Path Tests

- TC-001 through TC-006: Explicit provider selection for each supported provider
- TC-008, TC-009: Default provider routing
- TC-010: Per-data-type default provider
- TC-017: `serviceKey` in all responses
- TC-020: `tier` in all responses
- TC-019: Programmatic `serviceKey` access

### Edge Case Tests

- TC-007: Natural language phrasing variations (boundary of NLP parsing)
- TC-011: Missing/malformed configuration fallback
- TC-018: Aggregated data with single `serviceKey`
- TC-022: Paid tier indicator
- TC-026: Default provider retains fallback chain behavior

### Error Handling Tests

- TC-012, TC-013: Invalid provider name
- TC-014: Provider missing API credentials
- TC-015, TC-016: Provider API failure without fallback
- TC-023, TC-024: Explicit provider failure with no fallback
- TC-025: Circuit breaker bypass for explicit selection

### Security Test Cases

- SEC-001: Error messages for invalid providers must not expose API keys or internal configuration paths
- SEC-002: Provider name input must be sanitized against injection (SQL, command, path traversal patterns)
- SEC-003: NLP parser must not interpret embedded code or scripts in provider name field
- SEC-004: Credential validation errors must not reveal which credentials are configured

### Performance Test Cases

- PERF-001: NLP provider intent parsing completes in < 50ms (measure with `Stopwatch` over 1000 iterations)
- PERF-002: Provider name validation completes in < 10ms
- PERF-003: Metadata assembly (`serviceKey`, `tier`) adds < 5ms overhead per response

---

## Test Data

### Test Data Requirements

| Data Set | Description | Source | Sensitivity |
| --- | --- | --- | --- |
| Stock symbols | AAPL, MSFT, TSLA, GOOGL, AMZN, NVDA, META, SPY, DIS | Hardcoded well-known tickers | No PII |
| Provider names (valid) | yahoo, alphavantage, finnhub | Mapped from feature spec | No PII |
| Provider names (invalid) | bloomberg, reuters, iex, morningstar, empty string, null | Edge cases for validation | No PII |
| NLP phrasings | "get yahoo data on AAPL", "from alpha vantage get MSFT", "using finnhub show TSLA quote", "via yahoo", etc. | Derived from feature spec scenario 1.4 | No PII |
| Configuration JSON | Valid `appsettings.json`, malformed routing, missing credentials | Generated for each test scenario | Contains mock API keys only |
| Provider responses | Mocked JSON responses matching each provider's format | Generated based on real response schemas | No PII |

### Test Data Management

- **Creation**: Test data is embedded in test classes via `[TestInitialize]` or `[DataRow]` attributes; configuration objects constructed in-memory
- **Cleanup**: No persistent state — all tests use fresh mock objects per test method
- **Isolation**: Each test creates its own `McpConfiguration`, mock providers, and router instance; no shared mutable state between tests

---

## Test Infrastructure

### Test Environment

- **Local**: `dotnet test` from solution root; all provider APIs mocked — no credentials or network required
- **CI**: GitHub Actions pipeline; tests run in the existing `StockData.Net.Tests` and `StockData.Net.McpServer.Tests` projects
- **Quarantine**: Integration tests that require live API access (if added) must be tagged with `[TestCategory("ExternalAPI")]` and excluded from CI gate

### Test Frameworks and Tools

| Purpose | Tool | Version |
| --- | --- | --- |
| Unit testing | MSTest v2 | Current (per .csproj) |
| Mocking | Moq | Current (per .csproj) |
| MCP server testing | MSTest v2 (in-process) | Current |
| Code coverage | Coverlet (via `dotnet test --collect`) | Current |
| Performance measurement | `System.Diagnostics.Stopwatch` | .NET built-in |

### CI/CD Integration

- **Test stages**: Unit and MCP server tests run on every PR; integration tests run on merge to main
- **Gate policy**: All unit and MCP server tests must pass before merge; coverage must not regress below current baselines
- **Parallel execution**: `[assembly: DoNotParallelize]` in test projects with shared state; all other tests may run in parallel
- **Flaky test policy**: Any test that fails intermittently must be investigated within 48 hours; if unresolved, quarantined to `[TestCategory("Flaky")]` and excluded from gate

---

## Coverage Metrics

| Metric | Target | Measurement |
| --- | --- | --- |
| Line coverage (provider selection logic) | ≥ 90% | Coverlet via `dotnet test --collect:"XPlat Code Coverage"` |
| Branch coverage (validation & error paths) | ≥ 80% | Coverlet |
| GWT scenario coverage | 100% (20/20 scenarios) | Traceability matrix above |
| Critical path coverage (explicit selection + validation + metadata) | 100% | TC-001 through TC-006, TC-012, TC-017, TC-020 |
| All 10 MCP tools tested with provider selection | 100% | TC-017, TC-020 integration sweep |

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| NLP parsing accuracy varies with phrasing diversity | Users receive wrong provider or no provider match | TC-007 covers 12+ phrasing patterns; acceptance threshold set at 95%; log unrecognized patterns for iterative improvement |
| Provider ID mismatch between config and code | Explicit selection silently fails or routes to wrong provider | TC-001–TC-006 validate end-to-end ID mapping; configuration validation test (TC-011) catches mismatches at startup |
| `serviceKey`/`tier` metadata breaks backward compatibility | Existing consumers of response JSON may fail on schema change | TC-019 validates JSON structure; integration tests deserialize responses into strongly-typed models |
| Circuit breaker bypass logic introduces regression in default failover | Default (non-explicit) provider requests lose failover protection | TC-026 explicitly validates that default provider retains existing fallback chain behavior |
| External API rate limits affect integration test reliability | CI pipeline flakes due to provider API throttling | All unit and MCP server tests use mocked providers; external API tests quarantined to manual/nightly runs |
| Provider credential validation false positives | Valid providers rejected due to credential check timing or format issues | TC-014 covers missing credentials; startup validation tests confirm credential format checks run before routing |
| NLP parser vulnerable to injection via provider name field | Potential security vulnerability in parsing layer | SEC-002 and SEC-003 test sanitization; provider name validated against allowlist before any processing |

---

## Testability Concerns

The following acceptance criteria or scenarios present testability challenges:

1. **Scenario 5.2 (Rate limit metadata)**: Rate limit information availability depends on individual provider API responses. Providers that do not expose rate limits in response headers cannot be tested for this field. Mitigation: Test only providers known to return rate limit data (Alpha Vantage has documented limits); validate field is present when provider populates it, absent otherwise.

2. **Scenario 4.3 (Programmatic `serviceKey` identification in request history)**: "Request history" is not a current system concept. Testing that `serviceKey` is programmatically identifiable reduces to verifying it exists as a parseable field in response JSON (covered by TC-019). The "history" aspect requires a consumer logging responses, which is outside system scope.

3. **Scenario 6.2 (Circuit breaker bypass)**: Requires placing the circuit breaker in Open state before test execution. Currently, `CircuitBreaker` state is internal. Test must trigger enough failures to open the circuit, then attempt explicit selection. This may require exposing circuit breaker state for testability or using reflection.

4. **Non-functional: NLP parsing < 50ms**: Performance is environment-dependent. CI runners may have different latency profiles. Mitigation: Use generous thresholds in CI (100ms) and stricter local benchmarks (50ms); performance tests are non-blocking in CI.

---

## Related Documents

- Feature Specification: [Provider Selection](../features/provider-selection.md)
- Architecture Overview: [Stock Data Aggregation Architecture](../architecture/stock-data-aggregation-canonical-architecture.md)
- Security Design: [Security Summary](../security/security-summary.md)
- System-Wide Test Strategy: [Testing Summary](../testing/testing-summary.md)
