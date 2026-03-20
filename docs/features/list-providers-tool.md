# Feature: list_providers MCP Tool

## Overview

Add a new zero-argument MCP tool named `list_providers` that returns the set of stock data providers currently registered and available in the server. The tool exposes each provider's identifier, display name, aliases, and supported data types, enabling clients to programmatically discover valid provider options at runtime.

## Problem Statement

Currently, there is no programmatic way for MCP clients to discover which stock data providers are registered, configured, and available for use. This creates several issues:

- **No runtime discovery**: Clients cannot query the server to determine which providers are available; they must rely on static documentation or hard-coded lists
- **Stale tool descriptions**: Valid provider names are duplicated across every tool's parameter description string. When a provider is added or removed, all tool descriptions must be manually updated
- **Misleading documentation**: If a provider is disabled or unconfigured (missing API keys), it still appears in tool description strings, causing clients to see options that will fail at runtime
- **Poor user experience**: Clients have no way to present accurate, real-time provider options to end users, leading to validation errors and confusion

This affects MCP client developers who need to build interfaces that present valid provider choices, and end users who encounter error messages for providers that appeared to be available but were not configured.

## User Stories

### User Story 1: As an MCP client developer, I want to query available providers so that I can present only valid options to users

> 1.1 Given all three providers (Yahoo Finance, Alpha Vantage, Finnhub) are configured with valid API credentials, when I invoke the `list_providers` tool, then the response includes all three providers with their complete metadata (id, displayName, aliases, supportedDataTypes)
>
> 1.2 Given only Yahoo Finance and Alpha Vantage are configured (Finnhub API key is missing), when I invoke the `list_providers` tool, then the response includes only Yahoo Finance and Alpha Vantage; Finnhub is excluded
>
> 1.3 Given no providers are configured (all API keys are missing), when I invoke the `list_providers` tool, then the response returns an empty providers array: `{"providers": []}`
>
> 1.4 Given I invoke `list_providers` multiple times during a session, when provider configuration has not changed, then each invocation returns consistent results with identical provider sets

### User Story 2: As an MCP client developer, I want to see which data types each provider supports so that I can route requests intelligently

> 2.1 Given Yahoo Finance is registered, when I invoke `list_providers`, then the Yahoo Finance entry includes a `supportedDataTypes` array containing at least: `["historical_prices", "stock_info", "news", "market_news", "stock_actions", "financial_statement", "holder_info", "option_expiration_dates", "option_chain", "recommendations"]`
>
> 2.2 Given Alpha Vantage is registered, when I invoke `list_providers`, then the Alpha Vantage entry includes a `supportedDataTypes` array containing: `["historical_prices", "stock_info", "news"]`
>
> 2.3 Given Finnhub is registered, when I invoke `list_providers`, then the Finnhub entry includes a `supportedDataTypes` array containing: `["historical_prices", "stock_info", "news", "market_news"]`
>
> 2.4 Given a user requests a data type that only one provider supports (e.g., `option_chain`), when I check the `list_providers` response, then I can programmatically determine that only Yahoo Finance supports this data type

### User Story 3: As an MCP client developer, I want to see provider aliases so that I can validate natural language provider references

> 3.1 Given Yahoo Finance is registered, when I invoke `list_providers`, then the Yahoo Finance entry includes an `aliases` array containing: `["yahoo", "yfinance"]`
>
> 3.2 Given Alpha Vantage is registered, when I invoke `list_providers`, then the Alpha Vantage entry includes an `aliases` array containing: `["alphavantage", "alpha_vantage"]`
>
> 3.3 Given Finnhub is registered, when I invoke `list_providers`, then the Finnhub entry includes an `aliases` array containing: `["finnhub"]`
>
> 3.4 Given a user specifies "yfinance" in a natural language request, when I validate the provider name against the `list_providers` response, then I can confirm "yfinance" is a valid alias for the Yahoo Finance provider

### User Story 4: As an MCP client developer, I want the `list_providers` tool to be discoverable through the standard MCP tools list so that I know it exists

> 4.1 Given the MCP server is running, when I invoke the MCP protocol's `tools/list` request, then the tool list includes a `list_providers` entry
>
> 4.2 Given I inspect the `list_providers` tool definition, when I read its description, then the description clearly states: "Returns the list of stock data providers currently registered and available in the server"
>
> 4.3 Given I inspect the `list_providers` tool definition, when I check its input schema, then the schema shows the tool accepts no arguments (zero-argument tool)
>
> 4.4 Given I inspect other tools that accept a `provider` parameter (e.g., `get_stock_info`, `get_historical_prices`), when I read their parameter descriptions, then the descriptions reference the `list_providers` tool as the authoritative source for valid provider values (e.g., "Use the list_providers tool to discover available providers")

### User Story 5: As a system administrator, I want provider availability to reflect runtime configuration so that clients see accurate provider status

> 5.1 Given a provider's API key is missing from configuration, when I invoke `list_providers`, then that provider is excluded from the response
>
> 5.2 Given a provider is explicitly disabled in configuration (e.g., `Enabled: false`), when I invoke `list_providers`, then that provider is excluded from the response
>
> 5.3 Given I update the configuration to add a new provider API key and restart the server, when I invoke `list_providers` after restart, then the newly configured provider appears in the response
>
> 5.4 Given I update the configuration to remove a provider API key and restart the server, when I invoke `list_providers` after restart, then the removed provider is excluded from the response

## Requirements

### Functional Requirements

1. The system shall register a `list_providers` tool in the MCP server's `HandleToolsList()` method
2. The system shall implement a handler for `list_providers` in the `HandleToolCallAsync` method
3. The tool shall accept zero arguments (no input parameters required)
4. The tool shall return a JSON response with a `providers` array containing provider metadata objects
5. Each provider metadata object shall include: `id` (string), `displayName` (string), `aliases` (string array), and `supportedDataTypes` (string array)
6. The tool shall only include providers that are currently registered (i.e., pass `_registeredProviders.Contains` check in `ProviderSelectionValidator`)
7. The tool shall source provider data from `ProviderSelectionValidator` and/or `McpConfiguration`, not from hard-coded strings
8. The tool description shall clearly state its purpose and indicate it accepts no arguments
9. Other tools that accept a `provider` parameter shall update their parameter descriptions to reference `list_providers` for discovering valid values
10. The tool shall return an empty providers array if no providers are configured, without throwing an exception

### Non-Functional Requirements

- **Performance**: The tool must respond within 50ms under normal conditions (in-memory data assembly, no external I/O)
- **Reliability**: The tool must never throw an exception; if data is unavailable, it should return an empty providers array
- **Maintainability**: Adding a new provider to the system must automatically update the `list_providers` response without requiring code changes to the tool handler
- **Simplicity**: The response schema must be simple, flat, and easily consumed by clients (no nested complexity beyond arrays of strings)
- **Consistency**: Provider IDs and aliases in the `list_providers` response must match exactly the values accepted by other tools' `provider` parameters

## Acceptance Criteria

- [ ] **[Blocking]** `list_providers` tool is registered in `HandleToolsList()` — Evidence: Code review confirms tool registration; MCP tools/list request returns `list_providers` entry; tool description accurately describes functionality
- [ ] **[Blocking]** `list_providers` handler in `HandleToolCallAsync` returns provider data sourced from `ProviderSelectionValidator` — Evidence: Code review confirms handler implementation uses `ProviderSelectionValidator` or equivalent configuration source; no hard-coded provider strings exist in the handler
- [ ] **[Blocking]** Response includes all configured providers with complete metadata — Evidence: Integration test with all providers configured confirms response contains 3 provider entries, each with valid `id`, `displayName`, `aliases`, and `supportedDataTypes` fields
- [ ] **[Blocking]** Response excludes unconfigured providers — Evidence: Integration test with one provider missing API credentials confirms that provider is excluded from response; only configured providers appear
- [ ] **[Blocking]** Response returns empty array when no providers are configured — Evidence: Unit test with empty registered providers set confirms response is `{"providers": []}` with HTTP 200 status (not an error)
- [ ] **[Blocking]** Yahoo Finance metadata is correct — Evidence: Integration test confirms Yahoo entry includes `id: "yahoo"`, `displayName: "Yahoo Finance"`, `aliases: ["yahoo", "yfinance"]`, and supportedDataTypes includes at least 10 data types (historical_prices, stock_info, news, market_news, stock_actions, financial_statement, holder_info, option_expiration_dates, option_chain, recommendations)
- [ ] **[Blocking]** Alpha Vantage metadata is correct — Evidence: Integration test confirms Alpha Vantage entry includes `id: "alphavantage"`, `displayName: "Alpha Vantage"`, `aliases: ["alphavantage", "alpha_vantage"]`, and `supportedDataTypes: ["historical_prices", "stock_info", "news"]`
- [ ] **[Blocking]** Finnhub metadata is correct — Evidence: Integration test confirms Finnhub entry includes `id: "finnhub"`, `displayName: "Finnhub"`, `aliases: ["finnhub"]`, and `supportedDataTypes: ["historical_prices", "stock_info", "news", "market_news"]`
- [ ] **[Blocking]** Other tools' `provider` parameter descriptions reference `list_providers` — Evidence: Code review confirms at least 3 tools with `provider` parameters include text like "Use the list_providers tool to discover available providers" in their parameter descriptions
- [ ] **[Blocking]** Unit tests cover all scenarios — Evidence: Test suite includes tests for: all providers present, partial providers (one missing), empty providers (none configured), and correct metadata for each provider; test coverage report shows 95%+ coverage for the `list_providers` handler logic
- [ ] **[Non-blocking]** Response time is under 50ms — Evidence: Performance test with 100 iterations shows median response time <50ms and p99 <100ms
- [ ] **[Non-blocking]** Tool description is clear and concise — Evidence: Code review confirms description accurately summarizes tool purpose in one sentence; indicates zero arguments required

## Out of Scope

- **Provider health checks**: The tool does NOT perform real-time health checks or API availability tests; it only reports which providers are configured/registered
- **Provider rate limit status**: The tool does NOT return current rate limit consumption, remaining quota, or throttle state for providers
- **Provider feature flags**: The tool does NOT expose fine-grained feature flags or capability toggles beyond the high-level `supportedDataTypes` array
- **Dynamic provider registration**: The tool does NOT support adding or removing providers at runtime; changes require server configuration updates and restart
- **Provider performance metrics**: The tool does NOT return historical performance data, uptime statistics, or data quality scores for providers
- **Custom provider metadata**: The tool does NOT support user-defined or extensible metadata fields beyond the specified schema (id, displayName, aliases, supportedDataTypes)
- **Provider authentication status**: The tool does NOT validate or report whether API credentials are currently valid; it only checks if credentials exist in configuration
- **Recommendations**: The tool does NOT recommend which provider to use for a given request; it only reports availability

## Dependencies

- **Depends on**:
  - `ProviderSelectionValidator` class containing `_registeredProviders` and `_aliases` collections
  - `McpConfiguration` or equivalent configuration source for provider metadata
  - MCP server infrastructure (`HandleToolsList` and `HandleToolCallAsync` methods in `StockDataMcpServer.cs`)
  - Existing provider abstraction (`IStockDataProvider` implementations for Yahoo Finance, Alpha Vantage, Finnhub)

- **Blocks**:
  - Future client applications or UIs that need to present dynamic provider selection options
  - Automated validation of provider names in client-side code before sending requests
  - Documentation generation tools that need authoritative lists of available providers

- **External dependency risks**:
  - None; this tool has no external I/O dependencies and operates entirely on in-memory configuration

- **Quarantine policy**:
  - Integration tests that verify all providers are present may fail if API credentials are missing in test environments; these tests should be skipped or quarantined to environments with full provider configuration
  - Unit tests must mock `ProviderSelectionValidator` to avoid dependency on actual provider credentials

## Technical Considerations

- **Data source**: The tool handler should call a new method in `ProviderSelectionValidator` (e.g., `GetAvailableProviders()`) that assembles the response from `_registeredProviders`, `_aliases`, and provider capability metadata. Avoid duplicating logic or hard-coding provider details in the tool handler.

- **Response schema**: The returned object should be simple and serialization-friendly. Consider using an anonymous object or a plain `ProviderInfo` record with properties: `string Id`, `string DisplayName`, `string[] Aliases`, `string[] SupportedDataTypes`. Ensure JSON serialization produces clean, camelCase field names.

- **Provider capability mapping**: `supportedDataTypes` data must be maintained somewhere (configuration, constants, or provider attributes). Ensure this mapping is centralized and not duplicated across multiple locations. Consider adding a `GetSupportedDataTypes()` method to `IStockDataProvider` if capabilities vary dynamically.

- **Consistency with validation**: The `id` and `aliases` values returned by `list_providers` must match exactly the values accepted by `ProviderSelectionValidator.ValidateProviderSelection()`. Any mismatch will cause validation errors for clients using the tool output.

- **Tool description updates**: When updating other tools' `provider` parameter descriptions to reference `list_providers`, ensure the text is consistent across all tools. Consider using a shared constant or helper method to generate the description snippet.

- **Error handling**: While the tool should never throw exceptions, ensure defensive coding around configuration access. If `_registeredProviders` is null or empty, return an empty array gracefully.

- **Extensibility**: Design the `GetAvailableProviders()` method in `ProviderSelectionValidator` to be easily extensible when new providers are added. Avoid switch statements or if/else chains that enumerate providers explicitly; prefer configuration-driven or reflection-based approaches.

## Success Metrics

- **Client adoption**: 80%+ of client applications that present provider selection UI use `list_providers` to populate options within 3 months of release (measured via API call logs)
- **Error reduction**: 30%+ reduction in "invalid provider" validation errors after clients adopt `list_providers` (measured by comparing error rates before/after feature release)
- **Documentation accuracy**: Zero stale provider references in tool descriptions after feature release (measured by code audit)
- **Response time**: Median response time <20ms in production under normal load (measured via server telemetry)
- **Test coverage**: 95%+ code coverage for `list_providers` handler and new `ProviderSelectionValidator` methods (measured by code coverage tool)
