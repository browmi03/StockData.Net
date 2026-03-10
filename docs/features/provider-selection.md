# Feature: Provider Selection via Natural Language

## Overview

Enable users to explicitly select financial data providers through natural language requests (e.g., "get yahoo data on AAPL") while maintaining a seamless conversational interface. The system will interpret provider intent from user requests, apply that selection to all data operations, return metadata identifying which service fulfilled the request, and provide clear error feedback when invalid providers are specified.

## Problem Statement

Users need visibility and control over which financial data provider services their requests, driven by factors such as:
- **Data quality preferences**: Different providers may have varying data accuracy or freshness
- **Cost management**: Understanding which tier/provider is being used for budget tracking
- **Troubleshooting**: Isolating data discrepancies to specific providers
- **Provider-specific features**: Certain data may only be available from specific providers

Currently, the system abstracts provider selection completely, giving users no way to explicitly choose or identify which provider delivered their data. This creates a "black box" experience that prevents users from making informed decisions about data sources and troubleshooting data issues.

## User Stories

### User Story 1: As a financial analyst, I want to explicitly select a data provider using natural language so that I can control which service provides my data

> 1.1 Given I specify "yahoo" in my request (e.g., "get yahoo data on AAPL"), when the system processes my stock data request, then the system routes the request exclusively to the Yahoo Finance provider and returns data with `serviceKey: "yahoo"`
>
> 1.2 Given I specify "alpha vantage" in my request (e.g., "get alpha vantage prices for MSFT"), when the system processes my request, then the system routes the request exclusively to the Alpha Vantage provider and returns data with `serviceKey: "alphavantage"`
>
> 1.3 Given I specify "finnhub" in my request (e.g., "get finnhub quote for TSLA"), when the system processes my request, then the system routes the request exclusively to the Finnhub provider and returns data with `serviceKey: "finnhub"`
>
> 1.4 Given I specify a provider name using various phrasings (e.g., "from yahoo", "via alpha vantage", "using finnhub"), when the system interprets my request, then the natural language processor correctly identifies the provider intent and applies it

### User Story 2: As a financial analyst, I want the system to use a default provider when I don't specify one so that I can make quick requests without always stating a preference

> 2.1 Given I make a stock data request without specifying a provider (e.g., "get price for AAPL"), when the system processes my request, then the system routes the request to the default provider configured in `appsettings.json` and returns data with the appropriate `serviceKey`
>
> 2.2 Given multiple request types (stock info, prices, financial statements, news) without provider specification, when the system processes each request, then each uses the configured default provider for that data type
>
> 2.3 Given the default provider configuration is missing or malformed in `appsettings.json`, when the system starts up, then the system logs a configuration error and falls back to a hardcoded safe default (e.g., Yahoo Finance)

### User Story 3: As a financial analyst, I want clear error feedback when I specify an invalid provider so that I know what went wrong and can correct my request

> 3.1 Given I specify a provider name that doesn't exist (e.g., "get bloomberg data on AAPL"), when the system validates the provider, then the system returns an error message: "Provider 'bloomberg' is not configured. Available providers: yahoo, alphavantage, finnhub" with HTTP status 400 (Bad Request)
>
> 3.2 Given I specify a provider that exists but is not configured with API credentials, when the system attempts to use that provider, then the system returns an error message: "Provider 'alphavantage' is not available. Missing required API credentials." with HTTP status 503 (Service Unavailable)
>
> 3.3 Given I specify a provider that is valid and configured but the API call fails, when the provider returns an error, then the system returns a clear error message identifying the provider and the specific failure (e.g., "Yahoo Finance API error: Rate limit exceeded") with no automatic fallback to other providers

### User Story 4: As a financial analyst, I want every response to include metadata about which provider fulfilled my request so that I can track data sources and costs

> 4.1 Given any successful data request (explicit or default provider), when the system returns results, then the response includes a `serviceKey` field identifying the provider that fulfilled the request (e.g., `"serviceKey": "yahoo"`)
>
> 4.2 Given a request for aggregated data from a single provider, when the system returns results, then the response includes a single `serviceKey` value for that provider
>
> 4.3 Given the `serviceKey` is included in responses, when I review my request history, then I can programmatically identify which provider was used for each request

### User Story 5: As a financial analyst, I want visibility into provider tier and cost implications so that I can make informed decisions about data source selection

> 5.1 Given any successful data request, when the system returns results, then the response includes a `tier` field indicating the service tier (e.g., `"tier": "free"` or `"tier": "premium"`)
>
> 5.2 Given a provider has rate limits or cost implications, when the system returns results, then the response metadata includes relevant cost indicators (e.g., `"rateLimitRemaining": 450`)
>
> 5.3 Given I explicitly select a premium-tier provider, when the system returns results, then the tier metadata clearly indicates the cost tier (e.g., `"tier": "premium"`)

### User Story 6: As a system administrator, I want provider selection to bypass fallback chains so that explicit provider requests have predictable, transparent behavior

> 6.1 Given I explicitly select a provider that fails, when the provider returns an error, then the system does NOT automatically fall back to alternative providers and instead returns the error from the selected provider
>
> 6.2 Given circuit breaker logic is configured for automatic failover, when I explicitly select a provider, then the circuit breaker logic is bypassed and the explicit provider is always attempted
>
> 6.3 Given I do NOT explicitly select a provider, when the default provider is used, then standard fallback and circuit breaker logic MAY apply (behavior unchanged from current system)

## Requirements

### Functional Requirements

1. The system shall parse natural language requests to detect explicit provider selection (e.g., "yahoo", "alpha vantage", "finnhub", "from X", "via X", "using X")
2. The system shall support explicit provider selection for all data operations: stock info, prices, historical data, financial statements, news aggregation, and any other data retrieval operations
3. The system shall validate provider names at runtime before attempting data retrieval
4. The system shall return a clear error message when an invalid or unavailable provider is specified
5. The system shall include a `serviceKey` field in all response metadata identifying which provider fulfilled the request
6. The system shall include a `tier` field in response metadata indicating the service tier (free, premium, etc.)
7. The system shall use the default provider configured in `appsettings.json` when no explicit provider is specified
8. The system shall bypass fallback chains and circuit breaker logic when a provider is explicitly selected
9. The system shall return provider-specific errors without automatic failover when an explicitly selected provider fails
10. The system shall log all provider selection decisions (explicit or default) for auditing and troubleshooting

### Non-Functional Requirements

- **Performance**: Provider validation must complete within 10ms; natural language parsing for provider intent must not add more than 50ms to request processing time
- **Security**: Provider API credentials must remain secure; error messages must not expose sensitive configuration details or API keys
- **Usability**: Natural language provider selection must support common phrasings ("get yahoo data", "from alpha vantage", "using finnhub"); error messages must be clear and actionable
- **Maintainability**: Provider mappings (natural language → provider key) must be configurable; adding new providers must not require code changes to the NLP layer
- **Reliability**: Invalid provider selection must never cause system crashes; graceful error handling must always return meaningful error messages

## Acceptance Criteria

<!-- 
  Each criterion must be objectively testable (pass/fail with no interpretation).
  Specify what evidence is required to declare each criterion passed.
  Mark each as Blocking or Non-blocking.
-->

- [ ] **[Blocking]** Natural language provider selection works for all supported providers — Evidence: Integration tests demonstrate successful routing for requests like "get yahoo data on AAPL", "from alpha vantage get MSFT", "using finnhub show TSLA quote" with correct provider invocation
- [ ] **[Blocking]** Default provider configuration from `appsettings.json` is used when no provider is specified — Evidence: Integration tests confirm that requests without provider specification route to the default provider configured in `appsettings.json`; response `serviceKey` matches the configured default
- [ ] **[Blocking]** Invalid provider selection returns clear error with available providers — Evidence: Test cases for invalid provider names (e.g., "bloomberg", "reuters") return HTTP 400 with error message listing valid providers; no system crash occurs
- [ ] **[Blocking]** All responses include `serviceKey` metadata — Evidence: Integration tests verify that 100% of successful responses (across all data operation types) contain a `serviceKey` field with a valid provider identifier
- [ ] **[Blocking]** All responses include `tier` metadata — Evidence: Integration tests verify that 100% of successful responses contain a `tier` field indicating service tier (free, premium, etc.)
- [ ] **[Blocking]** Explicitly selected providers bypass fallback chains — Evidence: Integration tests demonstrate that when a provider is explicitly selected and fails, no fallback to alternative providers occurs; error is returned directly
- [ ] **[Blocking]** Explicitly selected providers bypass circuit breaker logic — Evidence: Integration tests confirm that circuit breaker state does not prevent explicitly selected provider attempts; explicit provider requests always attempt the selected provider
- [ ] **[Blocking]** Provider validation occurs at runtime before data retrieval — Evidence: Unit tests demonstrate validation logic runs before provider invocation; logs show validation events
- [ ] **[Blocking]** Provider-specific errors are returned without automatic failover — Evidence: Integration tests with mocked provider failures confirm that explicit provider selection returns provider-specific error messages without triggering fallback chains
- [ ] **[Non-blocking]** Multiple natural language phrasings are supported ("from X", "via X", "using X", "get X data") — Evidence: Integration tests cover at least 4 different phrasing patterns per provider with 95%+ success rate
- [ ] **[Non-blocking]** Rate limit information is included in response metadata when available — Evidence: Integration tests with providers that expose rate limit info (e.g., Alpha Vantage) confirm `rateLimitRemaining` or equivalent field is present in responses
- [ ] **[Non-blocking]** Logging captures all provider selection decisions — Evidence: Log analysis shows provider selection events (explicit and default) for all test scenarios with timestamp, provider, and selection method (explicit/default)

## Out of Scope

- **Multi-provider aggregation**: This feature does NOT support requests that aggregate data from multiple providers simultaneously (e.g., "compare yahoo and alpha vantage prices for AAPL")
- **Dynamic provider switching**: This feature does NOT support switching providers mid-conversation based on data quality or availability
- **Provider recommendation engine**: The system will NOT recommend the "best" provider for a given request based on historical performance or cost
- **Provider performance metrics**: This feature does NOT track or expose provider response times, uptime, or data quality scores
- **User-level provider preferences**: This feature does NOT support per-user default provider configuration; defaults are system-wide
- **Provider cost tracking**: While tier information is exposed, this feature does NOT calculate or track cumulative API costs or usage quotas
- **Graceful degradation with fallback**: When an explicit provider is selected, the system does NOT attempt fallback chains or alternative providers on failure
- **Advanced NLP for ambiguous provider names**: Edge cases like "get apple data" (company name vs. potential provider confusion) are out of scope; only clear provider intent is supported

## Dependencies

- **Depends on**: 
  - Existing provider abstraction layer (`IStockDataProvider` interface and implementations)
  - `appsettings.json` configuration infrastructure
  - Natural language processing capability in the MCP server to parse provider intent
  - Provider credential validation logic
- **Blocks**: 
  - Future features that require provider-specific data source attribution
  - Cost tracking and budget management features that depend on `serviceKey` and `tier` metadata
- **External dependency risks**: 
  - AI/NLP model accuracy for provider intent recognition (acceptable failure mode: user rephrases request)
  - Provider API availability and credential validity (acceptable failure mode: clear error message)
- **Quarantine policy**: 
  - Tests that depend on external provider APIs must be isolated to integration test suites
  - Unit tests must mock provider validation and selection logic
  - External provider failures during testing must not block CI/CD pipelines (quarantine to nightly or manual test runs)

## Technical Considerations

- **Natural language parsing**: The MCP server will need logic to parse user requests and extract provider intent. Consider regex patterns, keyword matching, or lightweight NLP models. Ensure the parsing layer is maintainable and extensible for new providers.
  
- **Provider validation**: Runtime validation must check (1) provider name is recognized, (2) provider has valid API credentials, (3) provider is enabled in configuration. Consider caching validation results for performance.

- **Response metadata structure**: Define a consistent schema for `serviceKey`, `tier`, and optional fields like `rateLimitRemaining`. Ensure backward compatibility if metadata is added to existing response structures.

- **Configuration schema**: Extend `appsettings.json` to include default provider mappings per data operation type and provider tier information. Validate schema at startup.

- **Circuit breaker interaction**: Ensure circuit breaker logic can be selectively bypassed. Consider a flag or parameter that indicates "explicit provider selection" to disable automatic failover.

- **Logging and observability**: Log provider selection decisions, validation results, and routing events for debugging and auditing. Include correlation IDs to trace requests end-to-end.

- **Error message standardization**: Define clear, user-friendly error templates for invalid provider names, missing credentials, and provider-specific failures. Ensure errors are actionable (e.g., list available providers).

## Implementation Phases (if applicable)

### Phase 1: MVP (Core Provider Selection)

- Natural language parsing for explicit provider selection (support "yahoo", "alpha vantage", "finnhub" with basic phrasings)
- Provider name validation at runtime
- `serviceKey` metadata in responses
- Default provider configuration from `appsettings.json`
- Error handling for invalid provider names
- Bypass fallback chains for explicit provider selection
- Integration tests for happy path and error cases

### Phase 2: Enhanced Metadata and Usability

- `tier` metadata in responses
- Rate limit information in response metadata (where available)
- Support for additional natural language phrasings ("from X", "via X", "using X")
- Improved error messages with suggestions (e.g., "Did you mean 'alphavantage'?")
- Logging and observability enhancements for provider selection decisions
- Administrator documentation for adding new providers to NLP mapping

## Success Metrics

- **Provider selection accuracy**: 95%+ of explicit provider requests are correctly routed to the intended provider (measured via integration test suite and user feedback)
- **Error clarity**: 90%+ of users encountering invalid provider errors can successfully correct their request without additional support (measured via follow-up request success rate)
- **Metadata completeness**: 100% of responses include `serviceKey` and `tier` fields (measured via automated tests and production monitoring)
- **Performance impact**: Natural language provider parsing adds <50ms to request processing time (measured via performance benchmarks)
- **Developer adoption**: New provider integrations can be added and configured in <1 hour by developers unfamiliar with the feature (measured via onboarding time for new providers)

## Work Tracking

### GitHub Issues and Milestone

**Milestone:** `Provider Selection v1.0`

**Issues:**

1. **[feature:provider-selection] [priority:high] Implement natural language provider intent parsing**
   - Description: Add logic to the MCP server to parse user requests and extract explicit provider selection (e.g., "get yahoo data on AAPL" → provider="yahoo"). Support basic phrasings for yahoo, alphavantage, and finnhub.
   - Acceptance: Integration tests demonstrate correct provider extraction for at least 3 phrasings per provider.
   - Labels: `feature:provider-selection`, `priority:high`, `component:mcp-server`

2. **[feature:provider-selection] [priority:high] Implement runtime provider validation**
   - Description: Add validation logic to check (1) provider name is recognized, (2) provider has valid API credentials, (3) provider is enabled. Return clear error messages for invalid providers.
   - Acceptance: Unit tests demonstrate validation logic for valid, invalid, and misconfigured providers; integration tests confirm error messages match specification.
   - Labels: `feature:provider-selection`, `priority:high`, `component:validation`

3. **[feature:provider-selection] [priority:high] Add serviceKey metadata to all responses**
   - Description: Extend response models for all data operations to include a `serviceKey` field identifying which provider fulfilled the request.
   - Acceptance: Integration tests verify `serviceKey` is present in 100% of successful responses across all data operation types.
   - Labels: `feature:provider-selection`, `priority:high`, `component:models`

4. **[feature:provider-selection] [priority:high] Add tier metadata to all responses**
   - Description: Extend response models to include a `tier` field indicating service tier (free, premium, etc.). Map providers to tiers in configuration.
   - Acceptance: Integration tests verify `tier` is present in 100% of successful responses; configuration mapping is documented.
   - Labels: `feature:provider-selection`, `priority:high`, `component:models`

5. **[feature:provider-selection] [priority:high] Implement default provider configuration in appsettings.json**
   - Description: Extend `appsettings.json` schema to support default provider configuration per data operation type. Load and validate configuration at startup.
   - Acceptance: Integration tests confirm default provider routing when no explicit provider is specified; configuration validation tests confirm error handling for malformed config.
   - Labels: `feature:provider-selection`, `priority:high`, `component:configuration`

6. **[feature:provider-selection] [priority:high] Bypass fallback chains and circuit breaker for explicit provider selection**
   - Description: Modify request routing logic to disable fallback chains and circuit breaker logic when a provider is explicitly selected. Return provider-specific errors without automatic failover.
   - Acceptance: Integration tests with mocked provider failures confirm no fallback occurs for explicit provider selection; default provider requests retain existing fallback behavior.
   - Labels: `feature:provider-selection`, `priority:high`, `component:routing`

7. **[feature:provider-selection] [priority:medium] Add logging for provider selection decisions**
   - Description: Implement logging for all provider selection events (explicit and default), validation results, and routing decisions. Include correlation IDs for traceability.
   - Acceptance: Log analysis confirms provider selection events are logged for all test scenarios; logs are structured and queryable.
   - Labels: `feature:provider-selection`, `priority:medium`, `component:observability`

8. **[feature:provider-selection] [priority:medium] Integration test suite for provider selection**
   - Description: Create comprehensive integration tests covering all user stories and acceptance criteria, including happy path, error cases, and edge cases.
   - Acceptance: Test suite achieves 95%+ code coverage for provider selection logic; all blocking acceptance criteria are verified by automated tests.
   - Labels: `feature:provider-selection`, `priority:medium`, `component:testing`

9. **[feature:provider-selection] [priority:low] Documentation for provider selection feature**
   - Description: Create user-facing documentation explaining how to use explicit provider selection, available providers, and error handling. Create developer documentation for adding new providers.
   - Acceptance: User documentation includes at least 5 examples; developer documentation enables adding a new provider in <1 hour.
   - Labels: `feature:provider-selection`, `priority:low`, `component:documentation`
