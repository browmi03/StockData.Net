# Feature: Enhanced MCP Error Handling and Provider Tier Awareness

<!-- 
  Template owner: Product Manager
  Output directory: docs/features/
  Filename: issue-17-mcp-error-handling.md
  Related Issue: GitHub Issue #17
-->

## Overview

Fix VS Code MCP client-side error ("Cannot read properties of null (reading 'task')") when Finnhub provider throws `NotSupportedException`, implement tier-aware error messaging to distinguish free vs. paid API limitations, and rename the `get_yahoo_finance_news` MCP tool to the provider-agnostic `get_finance_news`.

## Problem Statement

When VS Code Copilot calls MCP tools that trigger `NotSupportedException` from the Finnhub provider (free tier), VS Code returns a cryptic client-side error instead of a proper error message. This occurs because the `ClassifyError` method in `StockDataProviderRouter.cs` does not handle `NotSupportedException`, causing it to be classified as `ProviderErrorType.Unknown`. This triggers unnecessary failover attempts across all providers, and when all providers fail, VS Code cannot parse the aggregated error response.

Additionally:

- The `GetHistoricalPricesAsync` method IS implemented in Finnhub but fails in practice - needs investigation and fix
- Error messages don't indicate when a feature is available on paid tiers but not on free tiers
- The MCP tool `get_yahoo_finance_news` is misleading since the system now supports multiple providers (not just Yahoo Finance)

Users affected include:

- VS Code users invoking MCP tools that aren't supported by free-tier providers
- Developers debugging provider failures without clear error context
- Users evaluating whether to upgrade to paid API tiers

## User Stories

### User Story 1: As a VS Code user, I want clear error messages when I call unsupported operations so that I understand why the request failed

> 1.1 Given Finnhub free tier does not support GetMarketNewsAsync, when I invoke the `get_market_news` MCP tool with Finnhub as the provider, then I receive a clear error message indicating the provider does not support this operation
>
> 1.2 Given all configured providers do not support a specific operation, when I invoke the corresponding MCP tool, then I receive an error listing which providers were attempted and why each failed
>
> 1.3 Given a provider throws NotSupportedException, when the router classifies the error, then it is classified as `ProviderErrorType.InvalidRequest` and the router throws immediately without attempting failover
>
> 1.4 Given VS Code receives an InvalidRequest error from the MCP server, when it displays the error to the user, then the error message is properly formatted and does not cause a client-side parsing error

### User Story 2: As a developer evaluating API tiers, I want error messages to indicate when features are available on paid tiers so that I can make informed upgrade decisions

> 2.1 Given Finnhub does not support GetFinancialStatementAsync on the free tier but the paid tier does, when I invoke `get_financial_statement`, then the error message states: "Provider 'finnhub' does not support GetFinancialStatementAsync on the free tier. This feature is available with a paid subscription."
>
> 2.2 Given Finnhub does not support GetOptionChainAsync on any tier, when I invoke `get_option_chain`, then the error message states: "Provider 'finnhub' does not support GetOptionChainAsync."
>
> 2.3 Given Yahoo Finance supports all operations, when I invoke any MCP tool with Yahoo Finance configured as the provider, then the request succeeds without tier-related error messages
>
> 2.4 Given I receive a tier-aware error message, when I examine the message, then I can clearly distinguish between "not supported at all" vs. "not supported on free tier"

### User Story 3: As a VS Code user, I want GetHistoricalPricesAsync to work reliably with Finnhub so that I can retrieve historical stock data

> 3.1 Given Finnhub provider implements GetHistoricalPricesAsync, when I invoke `get_historical_stock_prices` with a valid ticker and date range, then I receive historical price data without encountering NotSupportedException
>
> 3.2 Given Finnhub API returns an empty response due to invalid symbol or date range, when I invoke `get_historical_stock_prices`, then I receive a clear validation error (not NotSupportedException)
>
> 3.3 Given Finnhub rate limit is exceeded, when I invoke `get_historical_stock_prices`, then the error is classified as `ProviderErrorType.RateLimitExceeded` and failover is attempted
>
> 3.4 Given Finnhub returns HTTP 404 for an invalid symbol, when I invoke `get_historical_stock_prices`, then I receive an appropriate "symbol not found" error message

### User Story 4: As a VS Code user, I want to invoke the finance news tool using a provider-agnostic name so that I understand it works with multiple providers

> 4.1 Given the MCP server is running, when I list available tools, then I see `get_finance_news` (not `get_yahoo_finance_news`) in the tools list
>
> 4.2 Given I invoke `get_finance_news` with a valid ticker, when the request is routed to any configured provider (Yahoo Finance, Alpha Vantage, or Finnhub), then I receive news results for that ticker
>
> 4.3 Given legacy clients may still use `get_yahoo_finance_news`, when I invoke the old tool name (if backward compatibility is implemented), then the request succeeds with a deprecation warning
>
> 4.4 Given the tool description for `get_finance_news`, when I read the description, then it clearly indicates support for multiple providers and does not reference Yahoo Finance exclusively

## Requirements

### Functional Requirements

1. The system shall classify `NotSupportedException` as `ProviderErrorType.InvalidRequest` in the `ClassifyError` method
2. The system shall throw immediately (without failover) when `ProviderErrorType.InvalidRequest` is encountered
3. The system shall include tier information in `NotSupportedException` messages for Finnhub provider operations
4. The system shall distinguish between "not supported on free tier" vs. "not supported at all" in error messages
5. The system shall investigate and fix any issues causing `GetHistoricalPricesAsync` to fail when invoked through Finnhub provider
6. The system shall rename the `get_yahoo_finance_news` MCP tool to `get_finance_news`
7. The system shall update the tool description for `get_finance_news` to reflect multi-provider support
8. The system shall ensure MCP error responses are properly formatted and parseable by VS Code client
9. The system shall maintain all test coverage for modified error handling logic

### Non-Functional Requirements

- **Reliability**: Error classification must be 100% deterministic - same exception type always produces same classification
- **Usability**: Error messages must be actionable - users should understand what went wrong and what actions they can take
- **Clarity**: Tier-aware messages must clearly distinguish free tier limitations from fundamental unsupported operations
- **Maintainability**: Tier information should be centralized (not scattered across individual provider methods)
- **Backward Compatibility**: Consider whether to support `get_yahoo_finance_news` as an alias with deprecation warning
- **Performance**: Error classification adds < 1ms overhead per request
- **Testability**: All new error handling paths must have corresponding unit tests

## Acceptance Criteria

- [ ] **[Blocking]** `ClassifyError` method handles `NotSupportedException` and returns `ProviderErrorType.InvalidRequest` — Evidence: Unit test verifies NotSupportedException → InvalidRequest classification
- [ ] **[Blocking]** Router throws immediately without failover when encountering `ProviderErrorType.InvalidRequest` — Evidence: Integration test confirms no failover attempts when NotSupportedException is thrown
- [ ] **[Blocking]** Finnhub provider throws tier-aware NotSupportedException for operations unsupported on free tier — Evidence: Error messages include "on the free tier. This feature is available with a paid subscription."
- [ ] **[Blocking]** Finnhub provider throws standard NotSupportedException for operations unsupported on all tiers — Evidence: Error messages do NOT include tier language for operations like GetOptionChainAsync
- [ ] **[Blocking]** `get_historical_stock_prices` works reliably with Finnhub provider — Evidence: Integration test successfully retrieves historical data for valid ticker/date range
- [ ] **[Blocking]** MCP tool renamed from `get_yahoo_finance_news` to `get_finance_news` — Evidence: `tools/list` response contains `get_finance_news` only
- [ ] **[Blocking]** Tool description updated to reflect multi-provider support — Evidence: Description does not exclusively reference Yahoo Finance
- [ ] **[Blocking]** VS Code client can parse error responses without generating "Cannot read properties of null" errors — Evidence: Manual testing with VS Code Copilot confirms proper error display
- [ ] **[Non-blocking]** Code coverage for error handling paths remains ≥ 85% — Evidence: Coverage report shows ≥85% line coverage in StockDataProviderRouter and provider classes
- [ ] **[Non-blocking]** All existing tests pass without modification — Evidence: CI/CD pipeline shows 443 tests passing (437 passed, 6 skipped)

## Out of Scope

What we are explicitly NOT doing in this feature:

- Implementing automatic tier detection or dynamic feature checking via Finnhub API
- Adding configuration options for per-provider tier settings
- Creating a provider capability registry or feature matrix
- Implementing backward compatibility alias for `get_yahoo_finance_news` (unless user explicitly requests it)
- Modifying error handling for other exception types beyond NotSupportedException
- Adding retry logic for operations that fail due to tier limitations
- Building a UI to display tier upgrade prompts in VS Code
- Investigating or fixing other Finnhub provider methods beyond GetHistoricalPricesAsync

## Dependencies

- **Depends on**: Existing StockDataProviderRouter, FinnhubProvider, and StockDataMcpServer classes
- **Blocks**: None - this is a bug fix and improvement to existing functionality
- **External dependency risks**: Finnhub API behavior may change; free tier limitations are not officially documented by Finnhub
- **Quarantine policy**: If Finnhub API is unavailable during testing, tests that specifically target Finnhub integration should be marked as quarantined (not failed) with clear indication of external dependency failure

## Technical Considerations

### Root Cause Analysis

The `ClassifyError` switch expression in `StockDataProviderRouter.cs` handles `ArgumentException`, `TaskCanceledException`, `HttpRequestException`, and `UnauthorizedAccessException`, but has no case for `NotSupportedException`, causing it to fall through to `ProviderErrorType.Unknown`.

See: `StockData.Net/Providers/StockDataProviderRouter.cs` - `ClassifyError` method

When Finnhub throws `NotSupportedException`, it falls through to `ProviderErrorType.Unknown`. The router then attempts failover instead of immediately throwing. After all providers fail, the `ProviderFailoverException` contains aggregated error data that VS Code's MCP client cannot parse, resulting in the "Cannot read properties of null (reading 'task')" error.

### Investigation Points for GetHistoricalPricesAsync

The `FinnhubClient.GetHistoricalPricesAsync` method (lines 99-151 in FinnhubClient.cs) appears fully implemented:

- Validates symbol and date range
- Makes HTTP GET request to Finnhub stock/candle endpoint
- Parses FinnhubCandleResponse
- Returns empty list on 404 or invalid response status

Potential failure causes to investigate:

1. Finnhub API endpoint might require paid tier for historical data (despite having implementation)
2. Rate limiting on free tier may be more aggressive for historical data
3. Date range validation might be too strict or conflict with Finnhub expectations
4. Response parsing might fail on edge cases (empty data, weekends, invalid symbols)

### Tier-Aware Error Message Strategy

Recommend creating a `TierAwareNotSupportedException` class (or using exception data properties) that extends `NotSupportedException` with `ProviderId`, `MethodName`, and `AvailableOnPaidTier` metadata.

Message format should vary by tier flag:

- `AvailableOnPaidTier = true`: "Provider '{id}' does not support {method} on the free tier. This feature is available with a paid subscription."
- `AvailableOnPaidTier = false`: "Provider '{id}' does not support {method}."

See: `StockData.Net/Providers/TierAwareNotSupportedException.cs`

### MCP Tool Renaming Considerations

Files requiring changes:

1. `StockDataMcpServer.cs` - Tool definition (line ~140) and tool call handler (line ~280)
2. `StockData.Net.McpServer.Tests/McpServerTests.cs` - Test cases referencing tool name
3. Documentation files referencing the tool name

Backward compatibility options (if desired):

- Register both tool names pointing to same implementation
- Add deprecation warning in tool description for old name
- Log usage of deprecated name
- Plan removal timeline for deprecated name

## Implementation Phases

### Phase 1: Critical Bug Fixes (High Priority)

- Add `NotSupportedException` case to `ClassifyError` method returning `ProviderErrorType.InvalidRequest`
- Update `ExecuteWithFailoverAsync` to immediately throw on `InvalidRequest` classification (already implemented, verify behavior)
- Add unit tests for NotSupportedException error classification
- Add integration tests confirming no failover on NotSupportedException

### Phase 2: Tier-Aware Error Messaging (High Priority)

- Create `TierAwareNotSupportedException` class (or alternative implementation)
- Update Finnhub provider methods to throw tier-aware exceptions:
   - GetMarketNewsAsync: not available (free Finnhub endpoint, not yet integrated in client)
   - GetStockActionsAsync: paid tier available (Premium - splits/dividends)
  - GetFinancialStatementAsync: paid tier available
  - GetHolderInfoAsync: paid tier available
  - GetOptionExpirationDatesAsync: not available on any tier
  - GetOptionChainAsync: not available on any tier
   - GetRecommendationsAsync: not available (free Finnhub endpoint, not yet integrated in client)
- Add unit tests verifying tier-aware error message content
- Update error message sanitization to preserve tier information

### Phase 3: GetHistoricalPricesAsync Investigation (High Priority)

- Write comprehensive integration test for Finnhub GetHistoricalPricesAsync
- Test with multiple tickers, date ranges, and edge cases
- If failures found, identify root cause (API requirements, rate limits, parsing issues)
- Implement fix and verify with integration tests
- Document any Finnhub-specific limitations or requirements

### Phase 4: MCP Tool Rename (Medium Priority)

- Rename `get_yahoo_finance_news` to `get_finance_news` in tool definition
- Update tool description to mention multi-provider support
- Update tool call handler to use new name
- Update all test cases to use new tool name
- Search codebase for documentation references and update
- Consider backward compatibility approach (if needed)

### Phase 5: Verification & Documentation (Medium Priority)

- Manual testing with VS Code to confirm proper error display
- Verify all acceptance criteria are met
- Update relevant documentation (API docs, error handling guide)
- Add release notes entry

## Success Metrics

- **Error Rate Reduction**: VS Code client-side "Cannot read properties of null" errors reduced to zero for NotSupportedException cases
- **Support Ticket Reduction**: User inquiries about cryptic error messages reduced by 80%
- **Test Coverage Maintained**: Code coverage remains ≥ 85% for router and provider classes
- **Upgrade Clarity**: Users can identify paid tier features from error messages (measured by user feedback or support tickets)
- **Tool Discoverability**: Users understand `get_finance_news` supports multiple providers (measured by correct usage patterns)

## Work Tracking

### GitHub Issues Approach

Create the following issues under milestone "Issue #17: Enhanced Error Handling":

1. **Issue: Add NotSupportedException handling to ClassifyError**
   - Labels: `bug`, `priority:high`, `component:routing`
   - Description: Update ClassifyError method to map NotSupportedException → InvalidRequest
   - Acceptance: Unit tests pass, no failover on NotSupportedException

2. **Issue: Implement tier-aware error messages for Finnhub provider**
   - Labels: `enhancement`, `priority:high`, `component:providers`
   - Description: Create TierAwareNotSupportedException and update Finnhub unsupported operations
   - Acceptance: Error messages distinguish free tier limitations from unsupported operations

3. **Issue: Investigate and fix GetHistoricalPricesAsync failures**
   - Labels: `bug`, `priority:high`, `component:providers`, `provider:finnhub`
   - Description: Debug why GetHistoricalPricesAsync fails despite being implemented
   - Acceptance: Integration test successfully retrieves historical data from Finnhub

4. **Issue: Rename get_yahoo_finance_news to get_finance_news**
   - Labels: `enhancement`, `priority:medium`, `component:mcp-server`
   - Description: Rename tool to reflect multi-provider support
   - Acceptance: Tool list shows get_finance_news with updated description

5. **Issue: Verify VS Code error display and update documentation**
   - Labels: `testing`, `priority:medium`, `documentation`
   - Description: Manual verification with VS Code and documentation updates
   - Acceptance: All acceptance criteria verified, docs updated

### Milestone Definition

- **Milestone Name**: "Issue #17: Enhanced Error Handling"
- **Due Date**: TBD by team
- **Required Deliverables**:
  - NotSupportedException classification implemented and tested
  - Tier-aware error messages implemented for Finnhub
  - GetHistoricalPricesAsync working reliably
  - get_finance_news tool renamed and functional
  - All blocking acceptance criteria verified
  - Documentation updated

