# Test Strategy: Provider Free and Paid Tier Handling

<!--
  Template owner: Test Architect
  Output directory: docs/testing/
  Filename convention: issue-32-tier-handling-test-strategy.md
  Related Issue: #32
-->

## Document Info

- **Feature Spec**: [docs/features/issue-32-provider-free-paid-tier-handling.md](../features/issue-32-provider-free-paid-tier-handling.md)
- **Architecture**: [Stock Data Aggregation Architecture](../architecture/stock-data-aggregation-canonical-architecture.md)
- **Status**: Complete
- **Last Updated**: 2026-03-20

---

## Test Strategy Overview

Issue #32 introduces two categories of change: Category A fixes six code bugs where free-tier-supported endpoints crash with null reference errors, and Category B adds explicit `tier` configuration with silent fallback, investor-friendly error messages, and tier-filtered `list_providers` output.

The test strategy separates these cleanly:

- **Category A (Bug Fixes)**: Validated by live-API integration tests in `StockData.Net.IntegrationTests` using the free-tier keys already available in CI. These tests are the ground truth for correctness.
- **Category B (Tier Behavior)**: Validated primarily by unit tests with Moq/NSubstitute mocks. No paid API key is required — paid-tier logic is exercised by configuring `tier: "paid"` and asserting mock-verified routing behavior. A small set of manual-only tests cover end-to-end paid-tier validation.
- **Error Messaging**: Tested with light structural assertions (contains pricing URL, no technical jargon) rather than fragile exact-match comparisons.

All tests follow project conventions: MSTest v2, `GivenCondition_WhenAction_ThenExpectedResult` naming, Arrange-Act-Assert layout, Moq or NSubstitute for mocking.

> **Configuration note**: Valid tier values are `"free"` and `"paid"` (case-insensitive) only. Legacy values `"premium"` and `"enterprise"` are not accepted and cause a startup configuration error. See ADR-003 in `docs/architecture/decisions/adr-issue-32-tier-config.md`.

---

## Scope

### In Scope

- Finnhub `GetRecommendationsAsync` and `GetMarketNewsAsync` bug fixes (Category A)
- AlphaVantage `GetFinanceNewsAsync`, `GetHistoricalPricesAsync`, `GetStockActionsAsync`, and `GetMarketNewsAsync` bug fixes (Category A)
- `tier: "free"` / `tier: "paid"` property in `ProviderConfiguration`
- Silent fallback when a provider throws `TierAwareNotSupportedException`
- Per-provider failure reason aggregation when all providers fail
- Investor-friendly error message content (no technical jargon, includes pricing URLs)
- Provider attribution in successful responses
- `list_providers` capability filtering based on configured tier
- Tier annotation in `list_providers` output
- Configuration validation accepting `"paid"` as a valid tier value

### Out of Scope

- Yahoo Finance tier restrictions (Yahoo has no tiered subscription model)
- Options, holders, and financial statements endpoints (not addressed in Issue #32)
- UI or Claude Desktop automated testing (manual only)
- Load testing provider fallback chains
- Security testing not directly related to this issue's changes is out of scope. The five security test cases below (TC-SEC-001 through TC-SEC-005) are in scope as required by the security design for this issue.

---

## Test Levels

### Unit Tests

- **Target**: `ConfigurationLoader` tier validation; `StockDataProviderRouter` fallback logic; `TierAwareNotSupportedException` message content; provider capability matrix filtering; `list_providers` tier annotation
- **Coverage goal**: ≥ 90% line coverage for all new tier-handling code paths; 100% for error-message generation and capability filtering
- **Framework**: MSTest v2
- **Mocking strategy**: Moq for `IStockDataProvider` in router tests; NSubstitute for client interfaces in provider tests (matching per-file conventions — Moq in `FinnhubProviderTests`, NSubstitute in `AlphaVantageProviderTests`)
- **Location**: `StockData.Net.Tests/` (subfolders for providers, configuration, router)

### Integration Tests — Mocked HTTP

- **Target**: MCP server tool calls that exercise tier-aware routing end-to-end without live network I/O; `list_providers` response schema
- **Coverage goal**: All MCP tools that have tier-limited Finnhub equivalents exercise fallback; `list_providers` returns correct schema for free and paid configurations
- **Framework**: MSTest v2 with mocked `IStockDataProvider` instances
- **Location**: `StockData.Net.McpServer.Tests/`

### Integration Tests — Live API

- **Target**: All six Category A bug-fix endpoints called with real free-tier API keys
- **Coverage goal**: 100% of Category A acceptance criteria (AC-1 through AC-6) produce parseable, non-null JSON responses
- **Framework**: MSTest v2, `[TestCategory("Integration")]` + `[TestCategory("LiveAPI")]`
- **Environment**: CI with `FINNHUB_API_KEY` and `ALPHAVANTAGE_API_KEY` secrets; local with `secrets.json` or user secrets
- **Flakiness control**: Wrap in `ExecuteLiveApiTestAsync` (existing helper) to skip on credential failure or rate-limit without failing the build
- **Location**: `StockData.Net.IntegrationTests/`

### Manual Tests

- **Target**: Paid-tier end-to-end validation; Claude Desktop provider attribution display; mixed-tier `list_providers` output
- **Coverage goal**: AC-14, AC-11 (Claude Desktop), and end-to-end paid-tier scenarios
- **Execution**: Developer-run before release with a paid Finnhub key; results recorded in PR description

---

## Given/When/Then Scenario Coverage

### Traceability Matrix

| Spec Scenario | Description | Test Case ID(s) | Test Level | Status |
| --- | --- | --- | --- | --- |
| 1.1 | Free-tier endpoint succeeds with free-tier configured provider | TC-I-004 | Integration (Live) | Deferred (live key required) |
| 1.2 | Free-tier endpoint succeeds with no explicit tier config (defaults to free) | TC-U-002, TC-I-003 | Unit, Integration (Live) | Implemented |
| 1.3 | Multiple free-tier providers — first succeeds | TC-U-003 | Unit | Implemented |
| 1.4 | Category A bug — null ref fixed for free-tier endpoint | TC-I-001 through TC-I-006 | Integration (Live) | Deferred (live key required) |
| 2.1 | Silent fallback: free-tier Finnhub skipped, AlphaVantage/Yahoo serves request | TC-U-004, TC-I-007 | Unit, Integration | Implemented |
| 2.2 | Fallback after provider tier error — secondary provider serves request | TC-U-004a | Unit | Implemented |
| 2.3 | All providers fail — error lists each with reason | TC-U-005 | Unit | Implemented |
| 2.4 | Code bug on primary causes fallback, secondary succeeds | TC-U-004, TC-I-001 | Unit, Integration (Live) | Implemented |
| 3.1 | All free providers lack endpoint — investor-friendly error with pricing URL | TC-U-006, TC-S-004 | Unit, MCP Server | Implemented |
| 3.2 | Per-provider failure breakdown (rate limit, auth, downtime) | TC-U-005 | Unit | Implemented |
| 3.3 | Actionable tier upgrade guidance with Finnhub pricing URL | TC-U-006 | Unit | Implemented |
| 3.4 | Endpoint not supported by provider at all — clear message | TC-U-005b | Unit | Implemented |
| 4.1 | Paid-tier configured — paid endpoint succeeds without warnings | TC-U-007, TC-M-001 | Unit, Manual | Implemented |
| 4.2 | Mixed tier config — paid provider prioritised, free fallback works | TC-U-007a | Unit | Implemented |
| 4.3 | Paid-tier provider returns 500 — falls back, error distinguishes tier vs API | TC-U-008 | Unit | Implemented |
| 4.4 | Paid subscription misconfigured as free — documented warning behaviour | TC-U-009 | Unit | Implemented |
| 5.1 | `list_providers` free tier — excludes paid-only endpoints | TC-U-010, TC-S-001 | Unit, MCP Server | Implemented |
| 5.2 | `list_providers` paid tier — shows all endpoints | TC-U-011, TC-S-002 | Unit, MCP Server | Implemented |
| 5.3 | `list_providers` no tier set — shows free-tier capabilities with note | TC-U-012, TC-S-003 | Unit, MCP Server | Implemented |
| 5.4 | `list_providers` mixed tier — per-provider differentiation | TC-S-003 | MCP Server | Implemented |

### Acceptance Criteria Coverage

| AC | Description | Test Case ID(s) | Test Level |
| --- | --- | --- | --- |
| AC-1 | Finnhub `get_recommendations` returns data on free tier | TC-I-001 | Integration (Live) |
| AC-2 | Finnhub `get_market_news` returns data on free tier | TC-I-002 | Integration (Live) |
| AC-3 | AlphaVantage `get_finance_news` returns data on free tier | TC-I-003 | Integration (Live) |
| AC-4 | AlphaVantage `get_historical_stock_prices` returns data on free tier | TC-I-004 | Integration (Live) |
| AC-5 | AlphaVantage `get_stock_actions` returns data on free tier | TC-I-005 | Integration (Live) |
| AC-6 | AlphaVantage `get_market_news` returns data on free tier | TC-I-006 | Integration (Live) |
| AC-7 | Config accepts `tier: "free"` and `tier: "paid"` | TC-U-001 | Unit |
| AC-8 | Missing tier defaults to `"free"` | TC-U-002 | Unit |
| AC-9 | Providers tried in order, stops at first success | TC-U-003 | Unit |
| AC-10 | Silent tier skip — user does not see tier error, sees next-provider result | TC-U-004, TC-I-007 | Unit, Integration |
| AC-11 | Response attributes which provider served the request | TC-I-008, TC-M-002 | Integration (Live), Manual |
| AC-12 | All-fail returns per-provider failure reasons in investor-friendly language | TC-U-005, TC-S-004 | Unit, MCP Server |
| AC-13 | Tier limitation error includes pricing URL | TC-U-006 | Unit |
| AC-14 | Paid-tier successful response contains no upgrade/subscription language | TC-U-007, TC-M-001 | Unit, Manual |
| AC-15 | 500 server error produces "Service error" not "subscription issue" | TC-U-008 | Unit |
| AC-16 | `list_providers` free tier omits paid-only endpoints | TC-U-010, TC-S-001 | Unit, MCP Server |
| AC-17 | `list_providers` paid tier includes all endpoints | TC-U-011, TC-S-002 | Unit, MCP Server |
| AC-18 | `list_providers` shows tier annotation | TC-U-012, TC-S-003 | Unit, MCP Server |

---

## Test Cases

### Unit Tests

#### TC-U-001: Tier Config Accepts "free" and "paid" Values

- **Scenario**: AC-7
- **Level**: Unit
- **Priority**: Critical
- **Location**: `StockData.Net.Tests/ConfigurationLoaderTests.cs`
- **Input**: `ProviderConfiguration` with `Tier = "free"` and separately with `Tier = "paid"`
- **Expected Result**: `ConfigurationLoader.ValidateProviderTiers` accepts both values without throwing; provider tier is preserved in loaded config
- **Pass Criteria**: No exception thrown; loaded `ProviderConfiguration.Tier` equals input value

#### TC-U-002: Missing Tier Defaults to "free"

- **Scenario**: AC-8, Spec 1.2
- **Level**: Unit
- **Priority**: Critical
- **Location**: `StockData.Net.Tests/ConfigurationLoaderTests.cs`
- **Input**: `appsettings.json` fragment with a provider entry that has no `tier` field
- **Expected Result**: `ConfigurationLoader` sets `Tier = "free"` on the resulting `ProviderConfiguration`
- **Pass Criteria**: Loaded `ProviderConfiguration.Tier` equals `"free"`

#### TC-U-002b: Invalid Tier Value Causes Startup Configuration Failure

- **Scenario**: SEC-32-3 / ST-32-3
- **Level**: Unit
- **Priority**: High
- **Location**: `StockData.Net.Tests/ConfigurationLoaderTests.cs`
- **Name**: `GivenProviderTierIsLegacyValue_WhenLoadingConfiguration_ThenConfigurationExceptionThrown`
- **Input**: `appsettings.json` fragment with `tier: "premium"` for a provider; separately with `tier: "enterprise"`
- **Expected Result**: `ConfigurationLoader` throws `ConfigurationException` during host build; process does not start with an invalid tier value
- **Pass Criteria**: `Assert.ThrowsException<ConfigurationException>` during host build for both `"premium"` and `"enterprise"` inputs; exception message identifies the invalid tier value and the provider name

#### TC-U-003: Router Stops at First Successful Provider

- **Scenario**: AC-9, Spec 1.3
- **Level**: Unit
- **Priority**: Critical
- **Location**: `StockData.Net.Tests/StockDataProviderRouterTests.cs`
- **Input**: Two mocked providers; first returns valid JSON; second is never expected to be called
- **Expected Result**: Router returns result from first provider; second provider mock receives zero invocations
- **Pass Criteria**: Return value matches first provider response; `_mockSecondProvider.Verify(...)` reports zero calls

#### TC-U-004: Router Silently Skips Tier-Limited Provider and Falls Back

- **Scenario**: AC-10, Spec 2.1, 2.2
- **Level**: Unit
- **Priority**: Critical
- **Location**: `StockData.Net.Tests/StockDataProviderRouterTests.cs`
- **Name**: `GivenFirstProviderThrowsTierAwareException_WhenRouting_ThenSecondProviderServesRequest`
- **Input**: Primary provider mock throws `TierAwareNotSupportedException`; secondary provider mock returns valid JSON
- **Expected Result**: Router returns secondary provider's result; no tier error surfaced to caller
- **Pass Criteria**: Return value matches secondary provider response; returned string contains secondary provider's `sourceProvider` attribution

#### TC-U-004a: Router Skips Multiple Tier-Limited Providers Before Succeeding

- **Scenario**: Spec 2.1
- **Level**: Unit
- **Priority**: High
- **Location**: `StockData.Net.Tests/StockDataProviderRouterTests.cs`
- **Input**: First two providers throw `TierAwareNotSupportedException`; third returns valid JSON
- **Expected Result**: Router returns third provider's result
- **Pass Criteria**: Both first and second provider mocks were called; third provider called once; result is valid

#### TC-U-005: Router Returns Per-Provider Failure Reasons When All Fail

- **Scenario**: AC-12, Spec 2.3, 3.2
- **Level**: Unit
- **Priority**: Critical
- **Location**: `StockData.Net.Tests/StockDataProviderRouterTests.cs`
- **Name**: `GivenAllProvidersFail_WithDistinctErrors_WhenRouting_ThenAggregatedInvestorFriendlyErrorReturned`
- **Input**: Three provider mocks each throwing different exception types (rate limit, missing key, `TierAwareNotSupportedException`)
- **Expected Result**: Exception or error result contains one entry per provider; each entry uses investor-friendly phrasing
- **Pass Criteria**: Error message: does NOT contain "429", "null reference", or "NullReferenceException"; DOES contain each configured provider name; DOES include a reason per provider

#### TC-U-005b: Provider With No Endpoint Support Returns Descriptive Message

- **Scenario**: Spec 3.4
- **Level**: Unit
- **Priority**: Medium
- **Location**: `StockData.Net.Tests/Providers/AlphaVantageProviderTests.cs`
- **Input**: Call `GetRecommendationsAsync` on `AlphaVantageProvider` (AlphaVantage has no recommendations endpoint)
- **Expected Result**: Provider throws or returns a message explaining this data type is unavailable from AlphaVantage, not a tier limitation
- **Pass Criteria**: Error type or message distinguishes "not supported" from "requires paid tier"

#### TC-U-006: Tier Limitation Error Contains Pricing URL

- **Scenario**: AC-13, Spec 3.1, 3.3
- **Level**: Unit
- **Priority**: Critical
- **Location**: `StockData.Net.Tests/Providers/FinnhubProviderTests.cs`
- **Name**: `GivenFreeTierFinnhub_WhenGetStockActionsThrowsTierError_ThenMessageContainsPricingUrl`
- **Input**: `FinnhubProvider.GetStockActionsAsync("AAPL")` (paid-tier only)
- **Expected Result**: `TierAwareNotSupportedException.Message` contains `finnhub.io/pricing`
- **Pass Criteria**: `StringAssert.Contains(ex.Message, "finnhub.io/pricing")`

#### TC-U-007: Paid-Tier Successful Response Contains No Upgrade Language

- **Scenario**: AC-14, Spec 4.1
- **Level**: Unit
- **Priority**: Critical
- **Location**: `StockData.Net.Tests/StockDataProviderRouterTests.cs`
- **Name**: `GivenPaidTierProvider_WhenRequestSucceeds_ThenResponseContainsNoTierWarning`
- **Input**: Provider mock returns valid JSON; `ProviderConfiguration.Tier = "paid"`
- **Expected Result**: Response text does not contain "upgrade", "subscription", or "paid tier" language
- **Pass Criteria**: `StringAssert.DoesNotContain(response, "upgrade")` and similar checks on investor-warning keywords

#### TC-U-007a: Mixed Tier Config — Paid Provider Takes Priority

- **Scenario**: Spec 4.2
- **Level**: Unit
- **Priority**: High
- **Location**: `StockData.Net.Tests/StockDataProviderRouterTests.cs`
- **Input**: Two providers configured; first has `Tier = "paid"`, second has `Tier = "free"`; request is for a paid-tier endpoint
- **Expected Result**: First (paid) provider is called; second (free) is not, because first succeeds
- **Pass Criteria**: Paid provider mock invoked once; free provider mock not invoked

#### TC-U-008: 500 Server Error Distinguished from Tier Limitation

- **Scenario**: AC-15, Spec 4.3
- **Level**: Unit
- **Priority**: High
- **Location**: `StockData.Net.Tests/StockDataProviderRouterTests.cs`
- **Name**: `GivenPaidTierProvider_WhenProviderReturns500_ThenFallbackErrorSaysServiceError`
- **Input**: Primary provider mock throws a generic `HttpRequestException` (simulating 500); secondary provider mock returns data
- **Expected Result**: If all providers fail, error message includes "service error" or "temporarily unavailable", not "subscription" or "upgrade"
- **Pass Criteria**: Error message does NOT contain "subscription" or "upgrade"; DOES contain "service" or "unavailable"

#### TC-U-009: Misconfigured Tier (Paid Subscription as Free) Falls Through

- **Scenario**: Spec 4.4
- **Level**: Unit
- **Priority**: Low
- **Location**: `StockData.Net.Tests/StockDataProviderRouterTests.cs`
- **Input**: Provider with real paid access configured as `tier: "free"`; request for paid-only endpoint
- **Expected Result**: Provider throws `TierAwareNotSupportedException` (tier guard fires before API call); fallback occurs
- **Pass Criteria**: `TierAwareNotSupportedException` is thrown by the provider; router falls back to next configured provider

#### TC-U-010: Provider Capability Matrix — Free Tier Finnhub Excludes Paid Endpoints

- **Scenario**: AC-16, Spec 5.1
- **Level**: Unit
- **Priority**: Critical
- **Location**: `StockData.Net.Tests/` (new test class for capability matrix logic)
- **Input**: Finnhub provider capability query with `tier: "free"`
- **Expected Result**: Returned capability set includes `get_stock_info`, `get_finance_news`, `get_recommendations`, `get_market_news`; does NOT include `get_historical_stock_prices` (OHLC) or `get_stock_actions`
- **Pass Criteria**: Capability list membership assertions pass for all included and excluded endpoints

#### TC-U-011: Provider Capability Matrix — Paid Tier Finnhub Includes All Endpoints

- **Scenario**: AC-17, Spec 5.2
- **Level**: Unit
- **Priority**: Critical
- **Location**: `StockData.Net.Tests/` (capability matrix test class)
- **Input**: Finnhub provider capability query with `tier: "paid"`
- **Expected Result**: Returned capability set includes `get_historical_stock_prices` and `get_stock_actions`
- **Pass Criteria**: All paid-tier endpoint names present in capability list

#### TC-U-012: Provider Capability Matrix — No Tier Defaults to Free Tier View

- **Scenario**: AC-18, Spec 5.3
- **Level**: Unit
- **Priority**: High
- **Location**: `StockData.Net.Tests/` (capability matrix test class)
- **Input**: AlphaVantage configured without `tier` field
- **Expected Result**: Capability set reflects free-tier limits; annotation present ("Tier not specified — showing free tier capabilities")
- **Pass Criteria**: Capability set matches free-tier matrix; tier annotation string is non-empty

---

#### Security Test Cases

> These five test cases are required by the security design for this issue (`docs/security/issue-32-tier-handling-security.md`). They are gating tests that must pass before merge. See blocking items BLK-1, BLK-2, and BLK-3 in the security document.

#### TC-SEC-001: API Key Sanitization in Error Messages

- **Scenario**: SEC-32-1 / BLK-1 / ST-32-1
- **Level**: Unit
- **Priority**: Critical (Blocking)
- **Location**: `StockData.Net.Tests/` (error message formatter tests)
- **Name**: `GivenErrorMessageContainsApiKeyPattern_WhenFormatInvestorFriendlyMessage_ThenKeyIsRedacted`
- **Input**: Simulated HTTP exception message containing an API key pattern (e.g., `"Invalid request: token=ABCD1234EF"`)
- **Expected Result**: The formatted error message returned by `FormatInvestorFriendlyMessage` does NOT contain the key value `ABCD1234EF`
- **Pass Criteria**: `StringAssert.DoesNotContain(formattedMessage, "ABCD1234EF")`

#### TC-SEC-002: No Stack Trace or Class Names in Error Messages

- **Scenario**: SEC-32-1 / BLK-1 / ST-32-2
- **Level**: Unit
- **Priority**: Critical (Blocking)
- **Location**: `StockData.Net.Tests/` (error message formatter tests)
- **Name**: `GivenExceptionWithStackTrace_WhenFormatInvestorFriendlyMessage_ThenNoStackTraceFragmentsInOutput`
- **Input**: `Exception` whose message and stack trace contain `"at System."` and C# class name fragments
- **Expected Result**: Formatted error message contains none of: C# method names, class names, or stack trace fragments (e.g., `"at System."`, `"Exception in"`)
- **Pass Criteria**: Regex negative-match assertion against the formatted message for patterns `@"at\s+\w+\.\w+"`, `@"Exception\s+in"`, and `@"System\."` — all must return no matches

#### TC-SEC-003: Upgrade URL Is a Hardcoded Constant

- **Scenario**: SEC-32-4 / BLK-3 / ST-32-4
- **Level**: Unit
- **Priority**: Critical (Blocking)
- **Location**: `StockData.Net.Tests/` (error message formatter tests or `ProviderUpgradeUrlsTests.cs`)
- **Name**: `GivenFinnhubTierLimitationError_WhenFormattingMessage_ThenUpgradeUrlIsExactConstant`
- **Input**: Tier limitation error for Finnhub; separately for AlphaVantage; `appsettings.json` may contain arbitrary values that must not influence the URL
- **Expected Result**: Finnhub upgrade URL exactly equals `https://finnhub.io/pricing`; AlphaVantage upgrade URL exactly equals `https://www.alphavantage.co/premium/`
- **Pass Criteria**: `Assert.AreEqual("https://finnhub.io/pricing", extractedFinnhubUrl)`; `Assert.AreEqual("https://www.alphavantage.co/premium/", extractedAlphaVantageUrl)`

#### TC-SEC-004: AlphaVantage Throws TierAwareNotSupportedException for Unsupported Method

- **Scenario**: SEC-32-2 / BLK-2 / ST-32-5
- **Level**: Unit
- **Priority**: Critical (Blocking)
- **Location**: `StockData.Net.Tests/Providers/AlphaVantageProviderTests.cs`
- **Name**: `GivenFreeTierAlphaVantage_WhenGetFinancialStatementAsync_ThenThrowsTierAwareNotSupportedException`
- **Input**: `AlphaVantageProvider.GetFinancialStatementAsync()` called (AlphaVantage has no financial statements endpoint; not available on any tier)
- **Expected Result**: `TierAwareNotSupportedException` is thrown, NOT a bare `NotSupportedException`
- **Pass Criteria**: `Assert.ThrowsException<TierAwareNotSupportedException>(() => provider.GetFinancialStatementAsync(...))`; verify the thrown type is exactly `TierAwareNotSupportedException` and not a subclass masquerading as `NotSupportedException`

#### TC-SEC-005: Tier-Skip Does Not Advance Circuit Breaker

- **Scenario**: SEC-32-2 / BLK-2 / ST-32-6
- **Level**: Unit
- **Priority**: Critical (Blocking)
- **Location**: `StockData.Net.Tests/StockDataProviderRouterTests.cs`
- **Name**: `GivenAlphaVantageThrowsTierAwareException_WhenRouting_ThenCircuitBreakerFailureCounterRemainsZero`
- **Input**: `AlphaVantageProvider` mock throws `TierAwareNotSupportedException`; router processes the fallback chain
- **Expected Result**: After the tier skip, the circuit breaker failure counter for AlphaVantage remains at `0`; circuit breaker state remains `Closed`
- **Pass Criteria**: Assert `circuitBreaker.FailureCount == 0` after the routing attempt; assert circuit breaker state is `Closed` (not `HalfOpen` or `Open`)

---

### MCP Server Tests

#### TC-S-001: `list_providers` for Free-Tier Finnhub Excludes Paid Endpoints

- **Scenario**: AC-16, Spec 5.1
- **Level**: MCP Server
- **Priority**: Critical
- **Location**: `StockData.Net.McpServer.Tests/McpServerTests.cs`
- **Input**: MCP `tools/call` for `list_providers`; config has Finnhub with `tier: "free"`
- **Expected Result**: Response body contains Finnhub section that lists free-tier tools only; `get_historical_stock_prices` and `get_stock_actions` are absent from Finnhub's capability list
- **Pass Criteria**: Response JSON or text does not include paid-only endpoint names in Finnhub section

#### TC-S-002: `list_providers` for Paid-Tier Finnhub Includes All Endpoints

- **Scenario**: AC-17, Spec 5.2
- **Level**: MCP Server
- **Priority**: Critical
- **Location**: `StockData.Net.McpServer.Tests/McpServerTests.cs`
- **Input**: MCP `tools/call` for `list_providers`; config has Finnhub with `tier: "paid"`
- **Expected Result**: Response body contains Finnhub section that lists paid-tier endpoints including `get_historical_stock_prices` and `get_stock_actions`
- **Pass Criteria**: Both paid-only endpoint names appear in Finnhub section of response

#### TC-S-003: `list_providers` Shows Tier Annotation Per Provider

- **Scenario**: AC-18, Spec 5.3, 5.4
- **Level**: MCP Server
- **Priority**: High
- **Location**: `StockData.Net.McpServer.Tests/McpServerTests.cs`
- **Input**: MCP `tools/call` for `list_providers`; mixed config: Finnhub paid, AlphaVantage free, Yahoo (no tier)
- **Expected Result**: Response contains tier labels adjacent to each provider name (e.g., "Finnhub (Paid tier)", "AlphaVantage (Free tier)")
- **Pass Criteria**: `StringAssert.Contains` checks for relevant tier annotation strings in response

#### TC-S-004: All-Provider Failure Returns Investor-Friendly Aggregated Error via MCP Tool

- **Scenario**: AC-12, Spec 3.1
- **Level**: MCP Server
- **Priority**: Critical
- **Location**: `StockData.Net.McpServer.Tests/McpServerTests.cs`
- **Input**: MCP `tools/call` for `get_historical_stock_prices`; all provider mocks throw (one `TierAwareNotSupportedException`, others `HttpRequestException`)
- **Expected Result**: MCP response is a non-exception text result listing per-provider failure reasons; no "429", no "TierAwareNotSupportedException" in user-facing text
- **Pass Criteria**: Response content includes each provider name and a reason; response does not contain technical exception type names or HTTP status codes

---

### Integration Tests — Live API

> All live-API integration tests use the `ExecuteLiveApiTestAsync` wrapper to handle credential failures gracefully as `Inconclusive`, not `Failed`. Tests are tagged `[TestCategory("Integration")]` and `[TestCategory("LiveAPI")]`.

#### TC-I-001: Finnhub `GetRecommendationsAsync` Returns Valid Data on Free Tier

- **Scenario**: AC-1, Spec 1.4, 2.4
- **Level**: Integration (Live API)
- **Priority**: Critical (Blocking)
- **Location**: `StockData.Net.IntegrationTests/FinnhubIntegrationTests.cs`
- **Name**: `GetRecommendationsAsync_LiveAapl_FreeTier_ReturnsNonEmptyResult`
- **Preconditions**: `FINNHUB_API_KEY` set to a valid free-tier key
- **Input**: `_provider.GetRecommendationsAsync("AAPL", RecommendationType.Recommendations)`
- **Expected Result**: Returns non-empty JSON or formatted string result; no `NullReferenceException` or `TierAwareNotSupportedException`
- **Pass Criteria**: Result is non-null and non-whitespace; can be parsed or printed without exception

#### TC-I-002: Finnhub `GetMarketNewsAsync` Returns Valid Data on Free Tier

- **Scenario**: AC-2
- **Level**: Integration (Live API)
- **Priority**: Critical (Blocking)
- **Location**: `StockData.Net.IntegrationTests/FinnhubIntegrationTests.cs`
- **Name**: `GetMarketNewsAsync_LiveFreeTier_ReturnsNewsArticles`
- **Preconditions**: `FINNHUB_API_KEY` set to a valid free-tier key
- **Input**: `_provider.GetMarketNewsAsync()`
- **Expected Result**: Returns non-empty result with at least one article block containing a title and URL
- **Pass Criteria**: Result is non-null; split by `"\n\n"` yields at least one block containing "Title:"

#### TC-I-003: AlphaVantage `GetFinanceNewsAsync` Returns Valid Data on Free Tier

- **Scenario**: AC-3, Spec 1.2
- **Level**: Integration (Live API)
- **Priority**: Critical (Blocking)
- **Location**: `StockData.Net.IntegrationTests/AlphaVantageIntegrationTests.cs`
- **Name**: `GetNewsAsync_LiveAapl_FreeTier_ReturnsNewsSentimentData`
- **Preconditions**: `ALPHAVANTAGE_API_KEY` set to a valid free-tier key
- **Input**: `_provider.GetNewsAsync("AAPL")`
- **Expected Result**: Returns non-empty result with at least one article block containing Title, Publisher, and URL
- **Pass Criteria**: Result is non-null; blocks contain expected field labels

#### TC-I-004: AlphaVantage `GetHistoricalPricesAsync` Returns Up to 100 Daily Points on Free Tier

- **Scenario**: AC-4, Spec 1.1
- **Level**: Integration (Live API)
- **Priority**: Critical (Blocking)
- **Location**: `StockData.Net.IntegrationTests/AlphaVantageIntegrationTests.cs`
- **Name**: `GetHistoricalPricesAsync_LiveAapl_FreeTier_ReturnsCompactDailyPoints`
- **Preconditions**: `ALPHAVANTAGE_API_KEY` set to a valid free-tier key
- **Input**: `_provider.GetHistoricalPricesAsync("AAPL", "3mo", "1d")`
- **Expected Result**: Returns JSON array with between 1 and 100 elements; each element has OHLCV fields and `SourceProvider: "alphavantage"`
- **Pass Criteria**: `doc.RootElement.GetArrayLength()` is between 1 and 100; no parse exceptions

#### TC-I-005: AlphaVantage `GetStockActionsAsync` Returns Dividends/Splits on Free Tier

- **Scenario**: AC-5
- **Level**: Integration (Live API)
- **Priority**: Critical (Blocking)
- **Location**: `StockData.Net.IntegrationTests/AlphaVantageIntegrationTests.cs`
- **Name**: `GetStockActionsAsync_LiveAapl_FreeTier_ReturnsDividendsOrSplits`
- **Preconditions**: `ALPHAVANTAGE_API_KEY` set to a valid free-tier key
- **Input**: `_provider.GetStockActionsAsync("AAPL")`
- **Expected Result**: Returns non-empty JSON or formatted result containing dividend or split records; no null reference error
- **Pass Criteria**: Result is non-null and non-whitespace; returns without exception

#### TC-I-006: AlphaVantage `GetMarketNewsAsync` Returns Valid Data on Free Tier

- **Scenario**: AC-6
- **Level**: Integration (Live API)
- **Priority**: Critical (Blocking)
- **Location**: `StockData.Net.IntegrationTests/AlphaVantageIntegrationTests.cs`
- **Name**: `GetMarketNewsAsync_LiveFreeTier_ReturnsMarketNews`
- **Preconditions**: `ALPHAVANTAGE_API_KEY` set to a valid free-tier key
- **Input**: `_provider.GetMarketNewsAsync()`
- **Expected Result**: Returns non-empty result with at least one article entry
- **Pass Criteria**: Result is non-null and non-whitespace; returns without exception

#### TC-I-007: Full Fallback — Free-Tier Finnhub Skipped, Yahoo Serves Request

- **Scenario**: AC-10, Spec 2.1
- **Level**: Integration (mocked providers, no live API)
- **Priority**: Critical
- **Location**: `StockData.Net.McpServer.Tests/McpServerTests.cs`
- **Input**: Router configured with [FinnhubProvider mock (throws `TierAwareNotSupportedException` for historical prices), Yahoo mock (returns valid JSON)]; call `GetHistoricalPricesAsync`
- **Expected Result**: Yahoo mock is invoked exactly once; result content matches Yahoo's response; Finnhub error is not surfaced to caller
- **Pass Criteria**: Return value contains Yahoo attribution; Finnhub mock verify shows it was attempted; Yahoo mock verify shows exactly one call

#### TC-I-008: Successful Response Contains Provider Attribution

- **Scenario**: AC-11
- **Level**: Integration (Live API)
- **Priority**: Critical
- **Location**: `StockData.Net.IntegrationTests/FinnhubIntegrationTests.cs` and `AlphaVantageIntegrationTests.cs`
- **Input**: Any successful live API call (reuse existing `GetStockInfoAsync` tests as proxy)
- **Expected Result**: Response JSON contains `sourceProvider` field matching the provider ID
- **Pass Criteria**: `document.RootElement.GetProperty("sourceProvider").GetString()` equals `"finnhub"` or `"alphavantage"` respectively (already validated in existing `GetStockInfoAsync_LiveAapl_ReturnsExpectedSchema` tests — verify these tests explicitly check `sourceProvider`)

---

### Manual Tests

#### TC-M-001: Paid-Tier Endpoint Returns Data Without Tier Warning

- **Scenario**: AC-14, Spec 4.1
- **Level**: Manual
- **Priority**: Critical
- **Preconditions**: Developer has a valid Finnhub paid-tier key; `appsettings.json` configured with `"tier": "paid"` for Finnhub
- **Steps**: Call `get_historical_stock_prices` for AAPL via MCP client
- **Expected Result**: Response contains OHLCV data; no "upgrade", "subscription", or "paid tier" language in response text
- **Pass Criteria**: Manual visual inspection of response content

#### TC-M-002: Claude Desktop Shows Provider Attribution

- **Scenario**: AC-11
- **Level**: Manual
- **Priority**: High
- **Preconditions**: MCP server running; Claude Desktop connected
- **Steps**: Ask Claude "get stock info on AAPL"; observe response
- **Expected Result**: Response includes a statement indicating which provider served the data (e.g., "Data provided by Yahoo Finance")
- **Pass Criteria**: Manual visual inspection confirms provider is clearly identified

#### TC-M-003: Mixed-Tier `list_providers` in Claude Desktop

- **Scenario**: Spec 5.4
- **Level**: Manual
- **Priority**: Medium
- **Preconditions**: Config set to Finnhub paid, AlphaVantage free, Yahoo enabled
- **Steps**: Ask Claude to call `list_providers`
- **Expected Result**: Output clearly differentiates Finnhub (Paid) vs AlphaVantage (Free); Finnhub shows full endpoint list; AlphaVantage shows limited list; Yahoo shows full list with no tier label
- **Pass Criteria**: Manual visual inspection

---

## Test Data

- **Ticker symbols**: Use `"AAPL"` for all live-API tests (high liquidity, reliable data across all providers and endpoints)
- **API keys**: Resolved from environment variable → user secrets → `secrets.json` (existing pattern in `FinnhubIntegrationTests` and `AlphaVantageIntegrationTests`)
- **Mock responses**: Inline JSON literals in unit tests; designed to be minimal but schema-valid per provider
- **Configuration objects**: Constructed inline using `new McpConfiguration { ... }` — no file I/O in unit tests
- **Isolation**: `[assembly: DoNotParallelize]` already set in `MSTestSettings.cs` for integration tests; unit tests have no shared state

---

## CI/CD Integration

| Stage | Tests | Gate Policy |
| --- | --- | --- |
| PR validation (every push) | Unit + MCP Server tests | All must pass; no `Inconclusive` treated as failure |
| PR validation | Integration (Live API) | `Inconclusive` on missing credentials is acceptable; actual failures block merge |
| Release gate | All test levels including manual checklist | Manual test sign-off required in PR description |

- **Flaky test policy**: Live-API tests that fail due to rate limiting or network instability produce `Assert.Inconclusive` (not `Assert.Fail`). The `ExecuteLiveApiTestAsync` wrapper handles this — use it for all Category A tests. Tests that are deterministically flaky (intermittent parsing errors) must be fixed before merge.
- **Paid-tier tests**: No paid-tier live-API tests run in CI. `TC-M-001` is manual. Any test that needs a paid key must be tagged `[TestCategory("ManualOnly")]` and excluded from the CI test filter.

---

## Coverage Targets

| Metric | Target |
| --- | --- |
| Line coverage — new tier-handling logic | ≥ 90% |
| Line coverage — error message generation | 100% |
| Line coverage — capability matrix filtering | 100% |
| GWT scenario coverage | 100% (all 20 scenarios mapped) |
| Acceptance criteria coverage | 100% of Blocking ACs (AC-1 through AC-17) |
| Category A bug fixes validated by live-API test | 6 / 6 |
| Security test cases (TC-SEC-001 through TC-SEC-005) | 100% |
| Invalid tier value startup rejection (TC-U-002b) | 100% |

> **AC-19 and AC-20** are documentation review criteria validated by the capability matrices in the architecture document (`docs/architecture/stock-data-aggregation-canonical-architecture.md`). Pass/fail is determined by documentation reviewer sign-off, not automated tests.

---

## Coverage Gaps and Risks

| Gap / Risk | Severity | Mitigation |
| --- | --- | --- |
| Paid-tier live-API tests require a paid key not available in CI | Medium | Mock-based unit tests cover routing logic; manual test TC-M-001 covers end-to-end. Document requirement for paid key in release checklist. |
| Category A live-API tests are rate-limited in CI | Medium | Tests use `ExecuteLiveApiTestAsync` to degrade to `Inconclusive`; run sequentially via `DoNotParallelize`; stagger with 1-second delays if needed. |
| `tier: "premium"` or `"enterprise"` in config must cause startup failure | High | TC-U-002b validates this. `ValidateProviderTiers` must reject any tier value not in the `{ "free", "paid" }` allow-list with a descriptive `ConfigurationException`. |
| `list_providers` capability filtering requires a new provider capability matrix | High | TC-U-010/TC-U-011 will be blocked until the capability matrix class is implemented. Mark `Not Started` until implementation exists. |
| `GetRecommendationsAsync` on Finnhub currently throws `TierAwareNotSupportedException` annotated `availableOnPaidTier: false` — the feature spec contradicts this | High | TC-I-001 validates the fix. The implementation must update this method and its exception flag (or remove the exception entirely for the free-tier case). Existing `FinnhubProviderTests` that assert the exception is thrown will need to be updated. |
| Error message investor-friendliness is subjective | Low | Tests use keyword exclusion (no "429", "NullReferenceException") and keyword inclusion (provider name, pricing URL) rather than exact-match assertion. |

---

## Related Documents

- Feature Specification: [docs/features/issue-32-provider-free-paid-tier-handling.md](../features/issue-32-provider-free-paid-tier-handling.md)
- Architecture: [docs/architecture/stock-data-aggregation-canonical-architecture.md](../architecture/stock-data-aggregation-canonical-architecture.md)
- Coding Standards — Testing: [docs/coding-standards/testing.md](../coding-standards/testing.md)
- Existing Provider Selection Test Strategy: [docs/testing/provider-selection-test-strategy.md](provider-selection-test-strategy.md)
- List Providers Test Strategy: [docs/testing/list-providers-tool-test-strategy.md](list-providers-tool-test-strategy.md)
