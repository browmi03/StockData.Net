# Test Strategy: `list_providers` MCP Tool

## Document Info

- **Feature Spec**: [docs/features/list-providers-tool.md](../features/list-providers-tool.md)
- **Architecture**: [list-providers-tool-architecture.md](../architecture/list-providers-tool-architecture.md)
- **Security**: [list-providers-tool-security.md](../security/list-providers-tool-security.md)
- **Status**: In Review
- **Last Updated**: 2026-03-19

---

## Test Strategy Overview

The `list_providers` tool is a zero-argument MCP tool added to `StockDataMcpServer` that returns every registered, configured provider with its `id`, `displayName`, `aliases`, and `supportedDataTypes`. Provider data is assembled dynamically at call time via a new `GetAvailableProviders()` method on `ProviderSelectionValidator`, so the response automatically reflects runtime configuration without hard-coded strings.

Testing validates five user stories spanning 20 Given/When/Then scenarios. The strategy follows the project's established MSTest v2 + Moq pattern, using `GivenCondition_WhenAction_ThenExpectedResult` naming and mocked `ProviderSelectionValidator`/`StockDataProviderRouter` to isolate all unit tests from live credentials. Integration tests verify end-to-end behaviour against `ConfigurationLoader.GetDefaultConfiguration()` with a full provider roster.

---

## Scope

### In Scope

- Registration of `list_providers` in `HandleToolsList()`
- Tool input schema (zero arguments)
- Tool description string
- `HandleToolCallAsync` handler for `list_providers`
- Provider metadata correctness: `id`, `displayName`, `aliases`, `supportedDataTypes` for all three providers
- Partial and empty provider list scenarios
- Response JSON validity and camelCase field naming
- Reference to `list_providers` in `provider` parameter descriptions of other tools
- `GetAvailableProviders()` method on `ProviderSelectionValidator` (new method required by implementation)
- Performance of the handler (in-memory, no external I/O)

### Out of Scope

- Live API calls to Yahoo Finance, Alpha Vantage, or Finnhub
- Provider health checks or availability probing
- Dynamic provider registration at runtime
- Per-user provider preferences
- Rate limit or quota reporting
- Recommendation logic

---

## Test Levels

### Unit Tests

- **Target**: `StockDataMcpServer.HandleToolsList()`, `StockDataMcpServer.HandleToolCallAsync()` for the `list_providers` case, and `ProviderSelectionValidator.GetAvailableProviders()`
- **Coverage goal**: ≥ 95% line coverage for `list_providers` handler logic; 100% for all error/empty paths
- **Framework**: MSTest v2
- **Mocking strategy**: Moq — construct `StockDataProviderRouter` with mock `IStockDataProvider` instances whose `ProviderId` returns the desired registered provider IDs; set `ConfigurationLoader.GetDefaultConfiguration()` for alias and capability configuration

### Integration Tests

- **Target**: End-to-end tool invocation through `StockDataMcpServer.HandleRequestAsync` with real `ConfigurationLoader` configuration and a full set of provider mocks
- **Coverage goal**: All three providers present, partial provider set (one missing), empty set
- **Framework**: MSTest v2, no network calls, in-process with mocked `IStockDataProvider`
- **Environment**: Local CI pipeline; quarantine rules apply if live credentials are needed

### Performance Tests

- **Target**: `list_providers` handler latency under repeated invocation
- **Tools**: `System.Diagnostics.Stopwatch` inline within a dedicated MSTest performance test
- **Criteria**: Median < 50ms; p99 < 100ms over 100 iterations (per non-functional requirement)

---

## Given/When/Then Scenario Coverage

| Spec Scenario | Description | Test Case ID(s) | Test Level | Status |
| --- | --- | --- | --- | --- |
| 4.1 | `tools/list` includes `list_providers` entry | TC-001 | Unit | Pass |
| 4.2 | `list_providers` description is "Returns the list of stock data providers..." | TC-002 | Unit | Pass |
| 4.3 | `list_providers` input schema accepts no arguments | TC-003 | Unit | Pass |
| 1.1 | All 3 providers configured → response includes all 3 with complete metadata | TC-004, TC-017 | Unit, Integration | Pass |
| 1.2 | Finnhub missing → only Yahoo and Alpha Vantage returned | TC-005, TC-018 | Unit, Integration | Pass |
| 1.3 | No providers configured → `{"providers":[]}` | TC-006 | Unit | Pass |
| 1.4 | Multiple invocations, unchanged config → identical results | TC-013 | Unit | Pass |
| 2.1 | Yahoo Finance `supportedDataTypes` includes all 10 specified types | TC-008 | Unit | Pass |
| 2.2 | Alpha Vantage `supportedDataTypes`: `["historical_prices","stock_info","news"]` | TC-009 | Unit | Pass |
| 2.3 | Finnhub `supportedDataTypes`: `["historical_prices","stock_info","news","market_news"]` | TC-010 | Unit | Pass |
| 2.4 | Only Yahoo Finance has `option_chain` in its `supportedDataTypes` | TC-011 | Unit | Pass |
| 3.1 | Yahoo Finance `aliases`: `["yahoo","yfinance"]` | TC-007 | Unit | Pass |
| 3.2 | Alpha Vantage `aliases`: `["alphavantage","alpha_vantage"]` | TC-009 | Unit | Pass |
| 3.3 | Finnhub `aliases`: `["finnhub"]` | TC-010 | Unit | Pass |
| 3.4 | `"yfinance"` is a valid alias for Yahoo Finance (cross-validation with `Validate`) | TC-012 | Unit | Pass |
| 4.4 | Other tools' `provider` descriptions reference `list_providers` | TC-014 | Unit | Pass |
| 5.1 | Missing API key → provider excluded from response | TC-005 | Unit | Pass |
| 5.2 | Provider explicitly disabled → excluded from response | TC-015 | Unit | Pass |
| 5.3 | Add API key + restart → new provider appears | TC-019 | Integration | Pass |
| 5.4 | Remove API key + restart → provider disappears | TC-019 | Integration | Pass |

**Coverage gap notice**: Scenarios 5.3 and 5.4 require server restart and configuration file mutation. They are not exercisable in unit tests. TC-019 covers both through a parametric integration test that constructs two server instances with differing provider sets and verifies each reflects its registered providers. No live credential mutation is needed.

---

## Test Cases

### TC-001: `GivenMcpServer_WhenRequestingToolsList_ThenListProvidersIsIncluded`

- **Scenario**: 4.1 — `tools/list` includes a `list_providers` entry
- **Level**: Unit (MCP server test in `StockDataMcpServerTests`)
- **Priority**: Critical
- **Input**: `McpRequest { Method = "tools/list" }` against a `StockDataMcpServer` with one mock provider
- **Expected Result**: Serialized response JSON contains `"list_providers"`
- **Pass Criteria**: `Assert.Contains("list_providers", resultJson)`

---

### TC-002: `GivenToolDefinition_WhenReadingListProvidersDescription_ThenDescriptionIsCorrect`

- **Scenario**: 4.2 — Tool description clearly states its purpose
- **Level**: Unit
- **Priority**: High
- **Input**: `tools/list` response, extract `list_providers` tool definition
- **Expected Result**: Description equals or contains `"Returns the list of stock data providers currently registered and available in the server"`
- **Pass Criteria**: `Assert.Contains("Returns the list of stock data providers", description)`

---

### TC-003: `GivenToolDefinition_WhenCheckingListProvidersInputSchema_ThenNoArgumentsRequired`

- **Scenario**: 4.3 — Tool is zero-argument
- **Level**: Unit
- **Priority**: High
- **Input**: `tools/list` response, extract `list_providers` tool definition's `inputSchema`
- **Expected Result**: Schema has `type = "object"` and either an empty `properties` object or no `required` array; no argument properties defined
- **Pass Criteria**: Schema JSON does not contain required argument names such as `ticker` or `provider`

---

### TC-004: `GivenAllThreeProvidersRegistered_WhenCallingListProviders_ThenResponseContainsThreeEntries`

- **Scenario**: 1.1
- **Level**: Unit
- **Priority**: Critical
- **Setup**: Construct server with three mock `IStockDataProvider` instances returning `ProviderId` of `"yahoo_finance"`, `"alphavantage"`, and `"finnhub"` respectively; use default configuration
- **Input**: `tools/call` request with `name = "list_providers"` and empty `arguments`
- **Expected Result**: Response JSON contains `"yahoo"`, `"alphavantage"`, and `"finnhub"` within a `providers` array; count = 3
- **Pass Criteria**:
  - `Assert.IsNull(response.Error)`
  - `Assert.Contains("\"yahoo\"", resultJson)` (the `id` field)
  - `Assert.Contains("\"alphavantage\"", resultJson)`
  - `Assert.Contains("\"finnhub\"", resultJson)`
  - Deserialize and confirm `providers.Length == 3`

---

### TC-005: `GivenFinnhubNotRegistered_WhenCallingListProviders_ThenFinnhubIsExcluded`

- **Scenario**: 1.2, 5.1 — Partial provider configuration
- **Level**: Unit
- **Priority**: Critical
- **Setup**: Server with only `yahoo_finance` and `alphavantage` mock providers; `finnhub` not in registered set
- **Input**: `tools/call` with `name = "list_providers"`, empty arguments
- **Expected Result**: Response contains exactly 2 provider entries; no `"finnhub"` id in response
- **Pass Criteria**:
  - `Assert.IsNull(response.Error)`
  - `Assert.DoesNotContain("\"finnhub\"", resultJson)` (checking `id` field specifically)
  - Deserialize and confirm `providers.Length == 2`

---

### TC-006: `GivenNoProvidersRegistered_WhenCallingListProviders_ThenReturnsEmptyProvidersArray`

- **Scenario**: 1.3 — Empty provider set
- **Level**: Unit
- **Priority**: Critical
- **Setup**: Server constructed with no `IStockDataProvider` instances (empty enumerable)
- **Input**: `tools/call` with `name = "list_providers"`, empty arguments
- **Expected Result**: Response JSON is `{"providers":[]}` (or equivalent); no error; no exception thrown
- **Pass Criteria**:
  - `Assert.IsNull(response.Error)`
  - Deserialize and confirm `providers.Length == 0`

---

### TC-007: `GivenYahooFinanceRegistered_WhenCallingListProviders_ThenYahooAliasesAreCorrect`

- **Scenario**: 3.1 — Yahoo Finance aliases
- **Level**: Unit
- **Priority**: Critical
- **Setup**: Server with only `yahoo_finance` provider mock; default configuration
- **Input**: `tools/call` with `name = "list_providers"`, empty arguments
- **Expected Result**: Yahoo Finance entry has `aliases` array containing both `"yahoo"` and `"yfinance"`
- **Pass Criteria**:
  - Deserialize provider entry with `id == "yahoo"`
  - `CollectionAssert.Contains(yahooEntry.Aliases, "yahoo")`
  - `CollectionAssert.Contains(yahooEntry.Aliases, "yfinance")`

---

### TC-008: `GivenYahooFinanceRegistered_WhenCallingListProviders_ThenSupportedDataTypesIncludesAll10Types`

- **Scenario**: 2.1 — Yahoo Finance `supportedDataTypes`
- **Level**: Unit
- **Priority**: Critical
- **Setup**: Server with `yahoo_finance` provider mock; default configuration
- **Input**: `tools/call` with `name = "list_providers"`, empty arguments
- **Expected Result**: Yahoo Finance entry `supportedDataTypes` contains all of: `historical_prices`, `stock_info`, `news`, `market_news`, `stock_actions`, `financial_statement`, `holder_info`, `option_expiration_dates`, `option_chain`, `recommendations`
- **Pass Criteria**: For each of the 10 data type strings, `CollectionAssert.Contains(yahooEntry.SupportedDataTypes, dataType)`

---

### TC-009: `GivenAlphaVantageRegistered_WhenCallingListProviders_ThenMetadataIsCorrect`

- **Scenario**: 2.2, 3.2 — Alpha Vantage metadata (aliases and supportedDataTypes)
- **Level**: Unit
- **Priority**: Critical
- **Setup**: Server with `alphavantage` provider mock; default configuration
- **Input**: `tools/call` with `name = "list_providers"`, empty arguments
- **Expected Result**:
  - Entry `id == "alphavantage"`, `displayName == "Alpha Vantage"`
  - `aliases` contains `"alphavantage"` and `"alpha_vantage"`
  - `supportedDataTypes` equals `["historical_prices", "stock_info", "news"]` (exact set, no extras required but all three present)
- **Pass Criteria**:
  - `CollectionAssert.Contains(avEntry.Aliases, "alphavantage")`
  - `CollectionAssert.Contains(avEntry.Aliases, "alpha_vantage")`
  - `CollectionAssert.Contains(avEntry.SupportedDataTypes, "historical_prices")`
  - `CollectionAssert.Contains(avEntry.SupportedDataTypes, "stock_info")`
  - `CollectionAssert.Contains(avEntry.SupportedDataTypes, "news")`

---

### TC-010: `GivenFinnhubRegistered_WhenCallingListProviders_ThenMetadataIsCorrect`

- **Scenario**: 2.3, 3.3 — Finnhub metadata
- **Level**: Unit
- **Priority**: Critical
- **Setup**: Server with `finnhub` provider mock; default configuration
- **Input**: `tools/call` with `name = "list_providers"`, empty arguments
- **Expected Result**:
  - Entry `id == "finnhub"`, `displayName == "Finnhub"`
  - `aliases` contains `"finnhub"` (and only canonical aliases, no extras from other providers)
  - `supportedDataTypes` contains: `historical_prices`, `stock_info`, `news`, `market_news`
- **Pass Criteria**:
  - `CollectionAssert.Contains(finnhubEntry.Aliases, "finnhub")`
  - `CollectionAssert.DoesNotContain(finnhubEntry.Aliases, "yahoo")`
  - All four data types present in `SupportedDataTypes`

---

### TC-011: `GivenAllProvidersRegistered_WhenComparingSupportedDataTypes_ThenOnlyYahooHasOptionChain`

- **Scenario**: 2.4 — Data type only in one provider supports capability routing
- **Level**: Unit
- **Priority**: Medium
- **Setup**: Server with all three provider mocks; default configuration
- **Input**: `tools/call` with `name = "list_providers"`, empty arguments; deserialize all provider entries
- **Expected Result**: `option_chain` appears in Yahoo Finance `supportedDataTypes`; does not appear in Alpha Vantage or Finnhub `supportedDataTypes`
- **Pass Criteria**:
  - `CollectionAssert.Contains(yahooEntry.SupportedDataTypes, "option_chain")`
  - `CollectionAssert.DoesNotContain(avEntry.SupportedDataTypes, "option_chain")`
  - `CollectionAssert.DoesNotContain(finnhubEntry.SupportedDataTypes, "option_chain")`

---

### TC-012: `GivenYahooFinanceRegistered_WhenValidatingYfinanceAlias_ThenAliasMatchesYahooProvider`

- **Scenario**: 3.4 — Cross-validation between `list_providers` output and `ProviderSelectionValidator.Validate()`
- **Level**: Unit
- **Priority**: Medium
- **Setup**: `ProviderSelectionValidator` constructed with `["yahoo_finance"]` as registered providers and default configuration
- **Input**: Call `validator.Validate("yfinance")`
- **Expected Result**: Result `IsValid == true`, `ResolvedProviderId == "yahoo_finance"`
- **Pass Criteria**:
  - `Assert.IsTrue(result.IsValid)`
  - `Assert.AreEqual("yahoo_finance", result.ResolvedProviderId)` — confirming the alias exposed by `list_providers` works as input to `Validate()`
- **Note**: This test does not call `list_providers` directly; it validates the contract that what `list_providers` exposes as an alias is actually accepted by the validator.

---

### TC-013: `GivenStableConfiguration_WhenCallingListProvidersMultipleTimes_ThenResultsAreIdentical`

- **Scenario**: 1.4 — Idempotency
- **Level**: Unit
- **Priority**: Medium
- **Setup**: Server with all three provider mocks; default configuration
- **Input**: Two sequential `tools/call list_providers` requests
- **Expected Result**: Both responses serialize to identical JSON strings
- **Pass Criteria**: `Assert.AreEqual(firstResultJson, secondResultJson)`

---

### TC-014: `GivenToolsWithProviderParameter_WhenReadingDescriptions_ThenListProvidersIsReferenced`

- **Scenario**: 4.4 — Other tools reference `list_providers` in `provider` parameter descriptions
- **Level**: Unit
- **Priority**: High
- **Setup**: Request `tools/list` from a default server instance
- **Input**: Parse each tool definition that has a `provider` property in its schema
- **Expected Result**: The `provider` property description for every tool that has one contains the text `"list_providers"`
- **Pass Criteria**: For each tool with a `provider` parameter, `Assert.Contains("list_providers", providerParamDescription)` — minimum 3 tools must satisfy this (per acceptance criteria wording)
- **Tools to validate**: `get_historical_stock_prices`, `get_stock_info`, `get_finance_news`, `get_market_news`, `get_stock_actions`, `get_financial_statement`, `get_holder_info`, `get_option_expiration_dates`, `get_option_chain`, `get_recommendations`

---

### TC-015: `GivenProviderExplicitlyDisabled_WhenCallingListProviders_ThenProviderIsExcluded`

- **Scenario**: 5.2 — Disabled provider excluded
- **Level**: Unit
- **Priority**: High
- **Setup**: Server constructed without the disabled provider in registered set (i.e., the provider's registration is driven by whether credentials exist; simulate "disabled" by not including it in the `IStockDataProvider[]` array)
- **Input**: `tools/call list_providers` with empty arguments
- **Expected Result**: The disabled provider's `id` does not appear in the response
- **Pass Criteria**: `Assert.DoesNotContain(disabledProviderId, resultJson)` for the `id` field of each entry
- **Note**: The implementation sources the provider list directly from the registered `IStockDataProvider` set. "Disabled" is modelled as "not registered", which is validated by TC-005. TC-015 adds an explicit test with a `Enabled: false` configuration path if that config key is implemented; otherwise it is identical in behaviour to TC-005 and can be merged. Flag this for implementation clarification.

---

### TC-016: `GivenListProvidersResponse_WhenDeserializingJson_ThenSchemaIsValid`

- **Scenario**: Acceptance criterion #10 — Response is valid JSON with expected structure
- **Level**: Unit
- **Priority**: Critical
- **Setup**: Server with all three provider mocks; default configuration
- **Input**: `tools/call list_providers`, extract the `text` content from the MCP result wrapper
- **Expected Result**: Content parses as valid JSON; top-level has `providers` array; each element has `id` (string), `displayName` (string), `aliases` (string array), `supportedDataTypes` (string array); all field names are camelCase
- **Pass Criteria**:
  - `JsonDocument.Parse(textContent)` does not throw
  - Root element has property `providers`
  - Each provider element has properties `id`, `displayName`, `aliases`, `supportedDataTypes`
  - `"Id"` (PascalCase) does not appear in the JSON string (enforces camelCase serialization)

---

### TC-017 (Integration): `GivenFullConfiguration_WhenCallingListProviders_ThenAllThreeProvidersArePresent`

- **Scenario**: 1.1 — Integration-level verification with real configuration loader
- **Level**: Integration
- **Priority**: Critical
- **Setup**: `new StockDataMcpServer(router, ConfigurationLoader().GetDefaultConfiguration())` with all three mock providers; do not require live credentials
- **Input**: `tools/call list_providers` with empty arguments
- **Expected Result**: Three provider entries in response
- **Pass Criteria**: Deserialize and confirm `providers.Count == 3`, correct `id` values present
- **Quarantine rule**: If the test environment has no API keys configured, this test confirms mock providers are always registered regardless of credentials.

---

### TC-018 (Integration): `GivenPartialConfiguration_WhenCallingListProviders_ThenOnlyRegisteredProvidersAppear`

- **Scenario**: 1.2 — Integration with partial provider set
- **Level**: Integration
- **Priority**: High
- **Setup**: `ConfigurationLoader().GetDefaultConfiguration()` with only `yahoo_finance` and `alphavantage` mock providers; Finnhub mock omitted
- **Input**: `tools/call list_providers` with empty arguments
- **Expected Result**: Response contains exactly `yahoo` and `alphavantage`; Finnhub absent
- **Pass Criteria**: `providers.Count == 2`; no `"finnhub"` id in deserialised array

---

### TC-019 (Integration): `GivenDifferentServerInstances_WhenProviderSetDiffers_ThenEachReflectsItsOwnRegisteredProviders`

- **Scenario**: 5.3, 5.4 — Configuration-change-and-restart simulation
- **Level**: Integration
- **Priority**: Medium
- **Setup**: Construct two independent `StockDataMcpServer` instances — `serverA` with all three providers, `serverB` with only Yahoo Finance
- **Input**: Call `list_providers` on both instances
- **Expected Result**: `serverA` returns 3 providers; `serverB` returns 1
- **Pass Criteria**:
  - `serverAProviders.Count == 3`
  - `serverBProviders.Count == 1`
- **Note**: This is the closest approximation of a "restart with new config" scenario achievable in-process. Actual configuration-file mutation after server restart is out of scope for automated tests.

---

### TC-020: `GivenListProviders_WhenCalledWithNoArguments_ThenNoErrorIsReturned`

- **Scenario**: Non-functional — handler must never throw; returns gracefully for zero-argument call
- **Level**: Unit
- **Priority**: Critical
- **Setup**: Server with all three provider mocks
- **Input**: `tools/call` with `name = "list_providers"` and `arguments = {}`
- **Expected Result**: `response.Error == null`; valid result present
- **Pass Criteria**: `Assert.IsNull(response.Error)` ; `Assert.IsNotNull(response.Result)`

---

### TC-021 (Performance): `GivenAllProviders_WhenCallingListProviders100Times_ThenMedianLatencyIsUnder50ms`

- **Scenario**: Non-functional requirement — response < 50ms
- **Level**: Performance
- **Priority**: Low (non-blocking)
- **Setup**: Server with all three provider mocks; run 10 warm-up iterations before measurement
- **Input**: 100 sequential `tools/call list_providers` invocations
- **Expected Result**: Median elapsed time per call < 50ms; p99 < 100ms
- **Pass Criteria**: `medianMs < 50`; `p99Ms < 100`

---

## Test Data

### Mock Provider Setup Pattern

```csharp
private static Mock<IStockDataProvider> CreateProviderMock(string providerId)
{
    var mock = new Mock<IStockDataProvider>();
    mock.Setup(p => p.ProviderId).Returns(providerId);
    mock.Setup(p => p.ProviderName).Returns(providerId); // name resolved from config in practice
    return mock;
}
```

### Registered Provider Combinations

| Scenario | Registered Array |
| --- | --- |
| All three | `["yahoo_finance", "alphavantage", "finnhub"]` |
| Yahoo + Alpha Vantage only | `["yahoo_finance", "alphavantage"]` |
| Yahoo only | `["yahoo_finance"]` |
| Empty | `[]` |

### Configuration Source

All tests use `new ConfigurationLoader().GetDefaultConfiguration()` as the base configuration. This ensures alias mappings (`yahoo` → `yahoo_finance`, `yfinance` → `yahoo_finance`, `alphavantage` → `alphavantage`, `alpha_vantage` → `alphavantage`, `finnhub` → `finnhub`) match the live configuration.

**No tests should hard-code alias strings that diverge from the configuration file.** Alias values asserted in TC-007, TC-009, and TC-010 must be validated against what `ConfigurationLoader` returns.

### Test Isolation

- Each test constructs its own `StockDataMcpServer` instance (or reuses the shared `_server` from `TestInitialize` where provider set is irrelevant)
- No shared mutable state between test cases
- No file system or network I/O in unit tests
- Integration tests (TC-017 through TC-019) may share a single `TestInitialize`-scoped server if provider configuration is identical

---

## New Test File / Class Placement

| Test Class | File | Covers |
| --- | --- | --- |
| `StockDataMcpServerTests` (existing) | `StockData.Net.McpServer.Tests/McpServerTests.cs` | TC-001 through TC-003, TC-013, TC-014, TC-016, TC-020, TC-021 |
| `ListProvidersToolTests` (new) | `StockData.Net.McpServer.Tests/ListProvidersToolTests.cs` | TC-004 through TC-012, TC-015 |
| `ListProvidersIntegrationTests` (new) | `StockData.Net.McpServer.Tests/ListProvidersIntegrationTests.cs` | TC-017 through TC-019 |

Rationale for a dedicated class: the existing `McpServerTests.cs` already has 20+ test methods; the metadata-heavy provider entry assertions (TC-007 through TC-012) are verbose enough that grouping them in their own class improves readability without changing the test framework setup.

---

## Implementation Prerequisite

`ProviderSelectionValidator` currently has no `GetAvailableProviders()` method. The tool handler cannot be implemented — and TC-004 through TC-012 cannot be written — until this method (or an equivalent data assembly path) exists. The method signature expected by the tests:

```csharp
public IReadOnlyList<ProviderInfo> GetAvailableProviders();
```

Where `ProviderInfo` is a new record:

```csharp
public sealed record ProviderInfo(
    string Id,
    string DisplayName,
    string[] Aliases,
    string[] SupportedDataTypes);
```

The test strategy assumes the implementation populates `SupportedDataTypes` from a static or configuration-driven capability map keyed by provider ID. Tests that assert specific `supportedDataTypes` values (TC-008, TC-009, TC-010, TC-011) are blocked until that map is defined and stable.

---

## CI/CD Integration

| Test Stage | Tests Run | Gate Policy |
| --- | --- | --- |
| PR build | All unit tests (TC-001 – TC-016, TC-020) | All must pass; PR blocked on failure |
| PR build | Performance test TC-021 | Non-blocking; result logged as informational |
| Merge to main | All unit + integration tests | All must pass |
| Nightly | Full suite including integration | Failures generate alerts; do not block PRs |

**Flaky test policy**: Any test that fails intermittently more than once in 10 runs must be quarantined within one business day. The `[Ignore]` attribute with a tracking comment (`// Quarantined: see issue #N`) is the quarantine mechanism used project-wide.

---

## Coverage Targets

| Metric | Target |
| --- | --- |
| Line coverage — `list_providers` handler in `StockDataMcpServer` | ≥ 95% |
| Line coverage — `GetAvailableProviders()` in `ProviderSelectionValidator` | 100% |
| GWT scenario coverage (spec scenarios mapped to test cases) | 100% (20/20) |
| Critical-priority test cases passing | 100% |
| Non-functional (performance) | Non-blocking; tracked as informational metric |

---

## Gaps and Untestable Scenarios

| Scenario | Gap Type | Disposition |
| --- | --- | --- |
| 5.3 — Add provider after live restart | Requires server restart + config mutation | Approximated by TC-019 (two independent server instances); live restart scenario is out of scope for automated tests |
| 5.4 — Remove provider after live restart | Requires server restart + config mutation | Same as 5.3 |
| 5.2 — `Enabled: false` config key | Implementation undefined | TC-015 flags this as pending; if `Enabled` is not a supported config key, TC-015 merges with TC-005 and scenario 5.2 is satisfied by the "not registered" model |
| TC-019 server-restart fidelity | In-process simulation only | Acceptable for unit/integration coverage; any production config rollout is covered by deployment checklist |

---

## Related Documents

- Feature Specification: [docs/features/list-providers-tool.md](../features/list-providers-tool.md)
- Provider Selection Test Strategy: [docs/testing/provider-selection-test-strategy.md](provider-selection-test-strategy.md)
- Testing Standards: [docs/coding-standards/testing.md](../coding-standards/testing.md)
- Architecture: [docs/architecture/stock-data-aggregation-canonical-architecture.md](../architecture/stock-data-aggregation-canonical-architecture.md)
