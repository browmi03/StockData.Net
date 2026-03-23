# Feature: Provider Free and Paid Tier Handling

<!--
  Template owner: Product Manager
  Output directory: docs/features/
  Filename convention: issue-32-provider-free-paid-tier-handling.md
  Related Issue: #32
-->

## Document Info

- **Status**: Complete
- **Last Updated**: 2026-03-19

## Overview

The StockData.Net MCP server must gracefully handle API provider subscription tier limitations (free vs. paid) for Finnhub and AlphaVantage providers. Users—who are investors, not developers—need transparent fallback behavior when a provider cannot fulfill a request due to tier restrictions, along with clear, actionable error messages when all providers fail. The system must also fix code bugs causing null reference errors on free-tier-supported endpoints.

## Problem Statement

Users of the StockData.Net MCP server access financial data through AI assistants (like Claude Desktop). When they request information that cannot be fulfilled by their configured providers due to subscription tier limitations (free vs. paid plans), they currently receive:

- Cryptic null reference errors (`Cannot read properties of null`)
- No indication which provider was used or why the request failed
- No guidance on how to resolve the issue

This affects investors who:

- Use free API keys and encounter silent failures on legitimately supported endpoints (code bugs)
- Use free API keys and call endpoints not available on their tier (need graceful handling)
- Cannot determine which provider served their request when fallback occurs
- Receive no actionable guidance when all providers fail

The system must distinguish between code bugs (Category A: endpoints available on free tier but crashing) and genuine tier limitations (Category B: endpoints not available on free tier), fixing the former and gracefully handling the latter with investor-friendly messages.

## User Stories

### User Story 1: As an investor using a free-tier API key, I want to successfully retrieve data from endpoints supported on my tier so that I can access financial information without errors

> 1.1 **Happy Path — Free-tier supported endpoint with free-tier configured provider**
>
> Given I have Finnhub configured with `tier: "free"` and `get_stock_info` is supported on the free tier
> When I request stock information for AAPL using `get_stock_info`
> Then I receive valid stock data from Finnhub without errors
> And the response indicates Finnhub was the data source
>
> 1.2 **Happy Path — Free-tier supported endpoint without explicit tier configuration**
>
> Given I have AlphaVantage configured without specifying the `tier` property (system defaults to `"free"`)
> When I request company news for AAPL using `get_finance_news`
> Then I receive valid news data from AlphaVantage without errors
> And the response indicates AlphaVantage was the data source
>
> 1.3 **Edge Case — Multiple free-tier providers, first succeeds**
>
> Given I have both Finnhub and AlphaVantage configured with `tier: "free"` and both support `get_stock_info`
> When I request stock information for AAPL
> Then I receive data from the first available provider (based on priority order)
> And the response indicates which provider was used
>
> 1.4 **Error Recovery — Code bug fixed for free-tier endpoint**
>
> Given AlphaVantage previously had a null reference error on `get_finance_news` despite the endpoint being available on free tier
> When I request finance news for AAPL
> Then I receive valid news data without null reference errors
> And the data is correctly parsed from the NEWS_SENTIMENT endpoint

### User Story 2: As an investor using a free-tier API key, I want the system to automatically try alternative providers when my primary provider cannot fulfill a request so that I get data without manual intervention

> 2.1 **Happy Path — Silent fallback to next provider**
>
> Given I have providers configured in order: [Finnhub (free), AlphaVantage (free), Yahoo]
> And Finnhub free tier does not support `get_historical_stock_prices`
> When I request historical prices for AAPL
> Then the system silently skips Finnhub and tries AlphaVantage
> And I receive historical price data from AlphaVantage or Yahoo
> And the response indicates which provider successfully served the request
>
> 2.2 **Happy Path — Fallback after provider-specific error**
>
> Given I have providers configured: [Finnhub (free), Yahoo]
> And Finnhub free tier returns an error for `get_stock_actions`
> When I request stock actions for AAPL
> Then the system automatically falls back to Yahoo without showing the Finnhub error
> And I receive stock actions data from Yahoo
> And the response indicates Yahoo was the data source
>
> 2.3 **Edge Case — All providers attempted before final failure**
>
> Given I have three providers configured: [Finnhub (free), AlphaVantage (free), Yahoo]
> And all providers are unavailable due to network issues
> When I request stock information
> Then the system attempts all three providers in order
> And only after all fail, I receive an error message listing each provider and reason for failure
>
> 2.4 **Error Scenario — First provider has code bug, fallback succeeds**
>
> Given Finnhub has a null reference bug on `get_recommendations` (which is actually supported on free tier)
> And AlphaVantage does not have recommendation data (no such endpoint)
> And Yahoo has working recommendations
> When I request recommendations for AAPL
> Then the system attempts Finnhub (fails due to bug), skips or fails AlphaVantage, succeeds with Yahoo
> And I receive recommendation data from Yahoo
> And the bug is logged for engineering but hidden from user

### User Story 3: As an investor using a free-tier API key, I want clear, actionable error messages when no provider can fulfill my request so that I understand why it failed and what I can do about it

> 3.1 **Paid-tier limitation — All free providers lack the endpoint**
>
> Given I have only free-tier providers configured: [Finnhub (free), AlphaVantage (free)]
> And Finnhub free tier does not support `get_historical_stock_prices` (OHLC)
> And AlphaVantage free tier only supports limited TIME_SERIES_DAILY
> When I request detailed historical OHLC prices
> Then I receive a friendly error message: "Historical OHLC prices are not available with your current subscription. Finnhub: This feature requires a paid subscription. Consider upgrading at <https://finnhub.io/pricing>. AlphaVantage: Limited to 100 daily points on free tier. Yahoo: Not configured."
>
> 3.2 **Clear per-provider failure breakdown**
>
> Given I have three providers: [Finnhub (free), AlphaVantage (free), Yahoo]
> And Finnhub rate limit is exceeded, AlphaVantage is not configured with an API key, Yahoo is experiencing downtime
> When I request stock data
> Then I receive an error listing: "Unable to retrieve data from any provider. Finnhub: Rate limit exceeded (60 calls/minute). Please try again later. AlphaVantage: API key not configured. Yahoo: Service temporarily unavailable."
>
> 3.3 **Actionable guidance for tier upgrade**
>
> Given I have Finnhub configured with `tier: "free"`
> And I request `get_stock_actions` which requires paid tier on Finnhub
> And no other provider can fulfill the request
> Then I receive a message: "Stock actions are not available with your current Finnhub subscription. Consider upgrading to access dividend and split data at <https://finnhub.io/pricing>."
>
> 3.4 **Edge Case — Endpoint not supported by any provider**
>
> Given I request recommendations from AlphaVantage (which has no recommendation endpoint at all)
> And AlphaVantage is the only configured provider
> When the request is made
> Then I receive: "Recommendations are not available from AlphaVantage. This provider does not offer this data type. Consider adding Finnhub or Yahoo as alternative providers."

### User Story 4: As an investor with a paid-tier API key, I want to access all endpoints supported by my subscription without tier restriction warnings so that I get the full value of my paid plan

> 4.1 **Happy Path — Paid-tier endpoint with paid configuration**
>
> Given I have Finnhub configured with `tier: "paid"`
> And `get_historical_stock_prices` requires paid tier on Finnhub
> When I request historical OHLC prices for AAPL
> Then I receive complete historical price data from Finnhub
> And no tier limitation message is shown
> And the response indicates Finnhub was the data source
>
> 4.2 **Happy Path — Mixed tier configuration (one paid, others free)**
>
> Given I have providers: [Finnhub (paid), AlphaVantage (free), Yahoo]
> When I request any data type
> Then Finnhub is prioritized and can serve all its supported paid endpoints
> And fallback to free-tier providers works for other requests
> And no tier limitation warnings appear for Finnhub responses
>
> 4.3 **Edge Case — Paid tier but endpoint still fails (API error, not tier)**
>
> Given I have Finnhub configured with `tier: "paid"`
> When I request data and Finnhub returns a 500 server error
> Then the system falls back to the next provider
> And the error message distinguishes between tier limitation and API failure: "Finnhub: Service error (not a subscription issue). Trying next provider..."
>
> 4.4 **Error Scenario — Paid tier misconfigured as free**
>
> Given I actually have a paid Finnhub subscription but configured `tier: "free"`
> When I request a paid-tier-only endpoint
> Then the system treats it as unsupported on free tier and falls back
> And I may receive a tier upgrade message inappropriately
> And the documentation warns users to configure their tier accurately

### User Story 5: As an investor, I want the `list_providers` tool to accurately reflect what my configured providers can do with my current subscription tier so that I know which data sources are available to me

> 5.1 **Accurate capability reporting for free tier**
>
> Given I have Finnhub configured with `tier: "free"`
> When I call `list_providers`
> Then the output shows Finnhub supports: `get_stock_info`, `get_finance_news`, `get_recommendations`, `get_market_news`
> And it does NOT list `get_historical_stock_prices` or `get_stock_actions` as supported
> And a note indicates "Free tier — some features unavailable"
>
> 5.2 **Accurate capability reporting for paid tier**
>
> Given I have Finnhub configured with `tier: "paid"`
> When I call `list_providers`
> Then the output shows Finnhub supports all available endpoints including `get_historical_stock_prices` and `get_stock_actions`
> And a note indicates "Paid tier — full feature access"
>
> 5.3 **Default to free-tier capabilities when tier not specified**
>
> Given I have AlphaVantage configured without a `tier` property
> When I call `list_providers`
> Then the output shows AlphaVantage capabilities based on free-tier limitations
> And a note indicates "Tier not specified — showing free tier capabilities"
>
> 5.4 **Mixed tier configuration display**
>
> Given I have providers: [Finnhub (paid), AlphaVantage (free), Yahoo (n/a)]
> When I call `list_providers`
> Then the output clearly differentiates capabilities per provider with tier annotations
> And Yahoo shows full capabilities (no tier restrictions for Yahoo)

## Requirements

### Functional Requirements

1. The system shall support a `tier` configuration property for each provider with values `"free"` or `"paid"`
2. The system shall default to `"free"` tier behavior when `tier` is not specified
3. The system shall attempt providers in configured priority order until one succeeds
4. The system shall silently skip providers that cannot fulfill a request due to tier limitations (during fallback)
5. The system shall return a clear indication of which provider successfully served the request
6. The system shall provide per-provider failure reasons when all providers fail, written for non-technical investors
7. The system shall fix Category A code bugs (null reference errors on free-tier-supported endpoints):
   - Finnhub: `get_recommendations`, `get_market_news`
   - AlphaVantage: `get_finance_news`, `get_historical_stock_prices`, `get_stock_actions`, `get_market_news`
8. The system shall gracefully handle Category B genuine tier limitations:
   - Finnhub Free: No OHLC historical prices, no dividends/stock actions
   - AlphaVantage Free: No recommendations endpoint (doesn't exist), limited TIME_SERIES_DAILY
9. The system shall include upgrade guidance in error messages for tier-limited failures, including provider pricing URLs
10. The `list_providers` tool shall filter capabilities based on the configured `tier` for each provider
11. The system shall distinguish between tier limitation errors and API/network errors in fallback logic

### Non-Functional Requirements

- **Usability**: Error messages must use investor-friendly language, avoiding technical jargon (no "null reference", "API rate limit 429", etc.). Use plain language like "rate limit exceeded", "not available with your subscription".
- **Performance**: Fallback between providers should add minimal latency (<100ms per provider attempt)
- **Reliability**: The system must not crash on null responses; all provider responses must be validated before parsing
- **Transparency**: Users must always know which provider served their request (include in response metadata or message)
- **Maintainability**: Provider tier capability matrices must be defined in a central, easily updatable configuration (not hardcoded across multiple files)

## Acceptance Criteria

<!--
  Each criterion must be objectively testable (pass/fail).
  Evidence required for PASS status is explicitly defined.
-->

### Category A: Code Bug Fixes

- [ ] **[Blocking]** AC-1: Finnhub `get_recommendations` returns valid data on free tier for AAPL
  - **Evidence**: Integration test calling Finnhub.GetRecommendations("AAPL") with free-tier key returns parsed recommendation trends without null errors
  
- [ ] **[Blocking]** AC-2: Finnhub `get_market_news` returns valid data on free tier
  - **Evidence**: Integration test calling Finnhub.GetMarketNews() with free-tier key returns news articles without null errors

- [ ] **[Blocking]** AC-3: AlphaVantage `get_finance_news` returns valid data on free tier for AAPL
  - **Evidence**: Integration test calling AlphaVantage.GetFinanceNews("AAPL") with free-tier key returns parsed news from NEWS_SENTIMENT endpoint without null errors

- [ ] **[Blocking]** AC-4: AlphaVantage `get_historical_stock_prices` returns valid data on free tier for AAPL (compact mode, last 100 points)
  - **Evidence**: Integration test calling AlphaVantage.GetHistoricalStockPrices("AAPL") with free-tier key returns up to 100 daily price points from TIME_SERIES_DAILY without null errors

- [ ] **[Blocking]** AC-5: AlphaVantage `get_stock_actions` returns valid data on free tier for AAPL
  - **Evidence**: Integration test calling AlphaVantage.GetStockActions("AAPL") with free-tier key returns dividends/splits from DIVIDENDS/SPLITS endpoints without null errors

- [ ] **[Blocking]** AC-6: AlphaVantage `get_market_news` returns valid data on free tier
  - **Evidence**: Integration test calling AlphaVantage.GetMarketNews() with free-tier key returns market news without null errors

### Category B: Tier Configuration and Fallback

- [ ] **[Blocking]** AC-7: Configuration accepts `tier: "free"` or `tier: "paid"` per provider
  - **Evidence**: appsettings.json schema validation allows `"tier": "free"` and `"tier": "paid"` in provider config; system loads config without errors

- [ ] **[Blocking]** AC-8: When `tier` is not specified, system defaults to `"free"` behavior
  - **Evidence**: Provider configured without `tier` property exhibits free-tier limitations (e.g., Finnhub without tier does not attempt OHLC historical prices)

- [ ] **[Blocking]** AC-9: System attempts providers in configured order and stops at first success
  - **Evidence**: Integration test with providers [Finnhub, AlphaVantage, Yahoo]: when Finnhub succeeds, AlphaVantage and Yahoo are not called (verified via logging/mock)

- [ ] **[Blocking]** AC-10: System silently skips provider when tier limitation prevents fulfillment
  - **Evidence**: Free-tier Finnhub configured first, request for `get_historical_stock_prices` skips Finnhub without returning error to user, succeeds with Yahoo, response indicates Yahoo was used

- [ ] **[Blocking]** AC-11: Response metadata indicates which provider successfully served the request
  - **Evidence**: MCP tool response includes `"provider": "Yahoo"` or similar field; manual testing via Claude Desktop shows "Data provided by Yahoo Finance"

### Category C: Error Handling and User Messaging

- [ ] **[Blocking]** AC-12: When all providers fail, return per-provider failure reasons in investor-friendly language
  - **Evidence**: Integration test where all providers mock different failures (rate limit, auth error, tier limitation) returns error message listing each provider with friendly explanation; no technical jargon like "429" or "null reference"

- [ ] **[Blocking]** AC-13: Tier limitation errors include upgrade guidance with pricing URL
  - **Evidence**: Finnhub free-tier fails on `get_stock_actions`, error message contains "Consider upgrading at <https://finnhub.io/pricing>"

- [ ] **[Blocking]** AC-14: Paid-tier configured provider does not show tier limitation warnings on successful requests
  - **Evidence**: Integration test with Finnhub `tier: "paid"`, request for `get_historical_stock_prices` succeeds without any "upgrade" or "subscription" language in response

- [ ] **[Non-blocking]** AC-15: Error messages distinguish between tier limitation and API/network errors
  - **Evidence**: Mock Finnhub to return 500 error (not tier issue); error message says "Service error" not "subscription issue"

### Category D: `list_providers` Accuracy

- [ ] **[Blocking]** AC-16: `list_providers` shows only free-tier capabilities for providers configured with `tier: "free"`
  - **Evidence**: Call `list_providers` with Finnhub `tier: "free"` returns capability list excluding `get_historical_stock_prices` and `get_stock_actions`

- [ ] **[Blocking]** AC-17: `list_providers` shows full capabilities for providers configured with `tier: "paid"`
  - **Evidence**: Call `list_providers` with Finnhub `tier: "paid"` returns capability list including `get_historical_stock_prices` and `get_stock_actions`

- [ ] **[Non-blocking]** AC-18: `list_providers` includes tier annotation per provider
  - **Evidence**: Output includes text like "Finnhub (Free tier)" or "Finnhub (Paid tier)" next to each provider name

### Category E: Documentation and Testing

- [ ] **[Blocking]** AC-19: Free-tier capability matrix documented for Finnhub
  - **Evidence**: Documentation file lists all tools and marks free-tier availability for Finnhub; matches test results

- [ ] **[Blocking]** AC-20: Free-tier capability matrix documented for AlphaVantage
  - **Evidence**: Documentation file lists all tools and marks free-tier availability for AlphaVantage; matches test results

- [ ] **[Blocking]** AC-21: Integration tests validate all Category A bug fixes with live free-tier API keys
  - **Evidence**: CI/CD build runs integration tests with live Finnhub and AlphaVantage free-tier keys; all 6 Category A tests pass

- [ ] **[Non-blocking]** AC-22: Integration tests cover fallback scenarios with mocked tier limitations
  - **Evidence**: Test suite includes mocked tests for AC-9, AC-10, AC-11 fallback scenarios; all pass

## Out of Scope

- **New provider integrations**: No new providers (e.g., Polygon.io, IEX Cloud) will be added in this issue
- **Paid tier testing with live paid keys**: We will not validate paid-tier behavior with actual paid subscriptions; paid-tier handling will be based on configuration and mocked tests
- **Real-time rate limit tracking**: No implementation of rate limit counters or predictive rate limit avoidance; providers will fail when rate limit is hit and fallback will occur
- **Endpoint-specific tier metadata**: No formal schema for defining tier restrictions per endpoint (this may be considered in a future architecture improvement); initial implementation will use code-based capability checks
- **User-configurable fallback order**: Provider priority is defined in appsettings.json and cannot be changed at runtime per request
- **Historical refactoring of Yahoo provider**: Yahoo provider is already functional and out of scope for this issue

## Dependencies

### Depends On

- **API provider documentation**: Finnhub and AlphaVantage public documentation must be consulted to confirm free-tier endpoint availability
- **Live free-tier API keys**: Integration tests require valid free-tier keys for Finnhub and AlphaVantage (already available in development environment)

### Blocks

- None (no known downstream features are blocked by this work)

### Related Issues

- Issue #17 (MCP Error Handling): This issue leverages the error handling architecture established in #17 for user-friendly error messages

---

## Technical Considerations

### High-Level Implementation Notes (for Architecture/Development teams)

1. **Configuration Schema**: The `appsettings.json` provider configuration should be extended to include an optional `tier` property. Example:

   ```json
   "Providers": [
     {
       "Name": "Finnhub",
       "ApiKey": "...",
       "Tier": "free"
     }
   ]
   ```

2. **Capability Matrix**: Consider a central configuration or metadata structure that maps (Provider, Tier, Tool) -> Supported (bool). This avoids scattering tier checks across provider implementations.

3. **Null Safety**: All provider response parsing must validate for null before accessing properties. Use null-conditional operators and provide meaningful defaults.

4. **Error Message Localization**: While not required for MVP, error message strings should be centralized (constants or resource files) to support future localization.

5. **Provider Pricing URLs**: Hardcode pricing URLs as constants in the relevant provider classes:
   - Finnhub: `https://finnhub.io/pricing`
   - AlphaVantage: `https://www.alphavantage.co/premium/`

6. **Logging for Silent Fallback**: When a provider is silently skipped due to tier limitation, log at INFO level for debugging, but do not surface to end user.

7. **Response Metadata**: Extend MCP tool responses to include a `provider` field indicating the source of the data. Example:

   ```json
   {
     "symbol": "AAPL",
     "price": 150.25,
     "_metadata": {
       "provider": "Yahoo",
       "timestamp": "2026-03-20T10:30:00Z"
     }
   }
   ```

8. **Testing Strategy**:
   - Unit tests: Mock provider responses to test tier logic and fallback paths
   - Integration tests: Use live free-tier keys for Category A bug validation
   - Manual tests: Use Claude Desktop to verify investor-facing error messages are clear

9. **Performance**: Implement a timeout per provider attempt (e.g., 5 seconds) to avoid slow cascading failures delaying the full fallback chain.

## Success Metrics

- **Code Reliability**: Zero null reference errors in production logs for the 6 Category A endpoints (30-day post-deployment)
- **Fallback Effectiveness**: >95% of requests succeed when at least one capable provider is configured (measured via telemetry)
- **User Clarity**: Post-deployment user feedback survey (if available) shows >80% understand error messages and know how to resolve issues
- **Support Reduction**: Reduction in support inquiries related to "provider not working" or "unclear error messages" (compare 30 days pre vs. 30 days post)

## Implementation Phases (Suggested)

While the Product Manager does not dictate implementation, the Orchestration agent may find this phasing helpful:

### Phase 1: Category A Bug Fixes (Highest Priority)

- Fix null reference errors on free-tier-supported endpoints
- Add null safety checks to all provider response parsing
- Validate with integration tests using live free-tier keys

### Phase 2: Tier Configuration and Fallback

- Implement `tier` configuration property
- Implement provider fallback logic with silent skipping
- Add response metadata to indicate provider used

### Phase 3: Error Messaging

- Implement per-provider failure aggregation
- Add investor-friendly error message formatting
- Include upgrade guidance and pricing URLs

### Phase 4: `list_providers` Enhancement

- Filter capabilities based on configured tier
- Add tier annotations to output

### Phase 5: Documentation and Testing

- Document free-tier capability matrices
- Expand integration test coverage for fallback scenarios
- Update user guide with tier configuration instructions

---

## Specification Sign-off

- **Product Manager**: Specification complete and ready for handoff to Orchestration agent
- **Date**: 2026-03-20
- **Expected Delivery**: The Orchestration agent will coordinate architecture, security, testing, development, and documentation to deliver this feature.
