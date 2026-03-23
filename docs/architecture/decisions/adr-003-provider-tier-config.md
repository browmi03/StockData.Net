# ADR-003: Static Code-Based Provider Capability Matrix

<!--
  Template owner: Architecture Design Agent
  Output directory: docs/architecture/decisions/
  Filename convention: adr-003-[short-title].md
-->

## Status

Accepted

## Context

Issue #32 requires each Finnhub and AlphaVantage provider to declare which MCP tools it supports on the free versus paid tier. The router and the `list_providers` tool both need to consult this information.

Two implementation locations were analysed:

1. **Configuration-driven** — The capability map lives in `appsettings.json` alongside the provider's API key and `tier` field. An administrator can update it without a code deployment.

2. **Code-based** — Each provider class exposes a method (e.g., `GetSupportedDataTypes(string tier)`) returning the set of supported data types for a given tier. The capability data is a static lookup table compiled into the provider assembly.

The question is: which location is the authoritative source of truth for provider tier capabilities, and where should it live?

### Forces

- Provider tier capabilities are determined by the underlying API contract (Finnhub's pricing page, AlphaVantage's documentation). They do not change based on user preference.
- Capabilities change only when a provider changes their API or pricing model, which always requires a code change anyway (to add/remove the actual HTTP calls).
- The existing `TierAwareNotSupportedException` already encodes tier information in code (`availableOnPaidTier: bool` parameter) — a code-based pattern is already established.
- The `ProviderSelectionValidator` has a hardcoded `ProviderMetadata` dictionary. That dictionary must already be kept consistent with the provider implementation.
- The router currently has no access to per-provider capability data at the point of failover evaluation.
- Configuration-driven capability maps can go out of sync with the actual implementation (e.g., config claims AlphaVantage supports `get_recommendations` on paid tier, but no such method exists on `IAlphaVantageClient`).

## Decision

Provider tier capabilities are declared as **static data in the provider class**, exposed through a new `IStockDataProvider.GetSupportedDataTypes(string tier)` method. Each provider returns a `HashSet<string>` of data-type keys for the given tier from a static readonly dictionary.

The `appsettings.json` `tier` field remains as the user-configurable **tier selector** — it tells the router which row of the capability matrix to read. It does not carry the capability definitions themselves.

**Canonical valid tier values:** The only accepted values are `"free"` and `"paid"` (case-insensitive). Any other value — including legacy labels such as `"premium"` or `"enterprise"` — is invalid and MUST cause server startup to fail with a descriptive configuration error message. When `tier` is absent from a provider entry, it defaults to `"free"`. There is no backward-compatibility path for legacy tier names because the `tier` field is new functionality with no prior production usage.

### What goes in code

Each provider class includes a private static capability declaration:

- `FinnhubProvider` maps `"free"` → `{StockInfo, News, MarketNews, Recommendations}` and `"paid"` → the full set including `HistoricalPrices`, `StockActions`, `FinancialStatement`, `HolderInfo`
- `AlphaVantageProvider` maps `"free"` → `{StockInfo, News, HistoricalPrices, StockActions, MarketNews}` and `"paid"` → same (AlphaVantage has no extra endpoints on paid for most of these; its paid tier offers higher rate limits, not more data types)
- `YahooFinanceProvider` maps all tier values → the full set (no tier restriction)

### What stays in configuration

```json
{
  "id": "finnhub",
  "tier": "free"
}
```

The `tier` value is the only tier-related configuration. It selects which capability set to read from the provider's static declaration. An operator changes `tier: "free"` → `tier: "paid"` when they upgrade their subscription; no code change is needed for that scenario.

## Rationale

**Capability boundaries are set by the API, not the operator.** An operator cannot make Finnhub's free tier return OHLC historical prices by editing `appsettings.json`. The capability matrix is therefore a fact about the provider implementation, not an operator override.

**Co-location with implementation prevents stale documentation.** The implementation of `FinnhubProvider.GetHistoricalPricesAsync` and its capability declaration are in the same file. A developer adding or removing an API endpoint will directly see and update the capability entry. A configuration file updated separately will inevitably diverge.

**Type safety.** A code-based declaration is checked at compile time. If a data-type key is mistyped, a test will fail. A configuration value is not checked until runtime.

**Simplicity.** The capability lookup becomes a single dictionary access. No config schema change, no deserialisation, no validation of user-supplied capability flags.

**Consistency with existing pattern.** `TierAwareNotSupportedException` already carries `availableOnPaidTier: bool` as a coded fact. `ProviderSelectionValidator.ProviderMetadata` is already a code-level static dictionary. This decision aligns with the established pattern rather than introducing a second, inconsistent mechanism.

## Alternatives Considered

### Configuration-Driven Capability Map

Capability declared in `appsettings.json` under each provider:

```json
{
  "id": "finnhub",
  "tier": "free",
  "capabilities": {
    "free":  ["StockInfo", "News", "MarketNews", "Recommendations"],
    "paid":  ["StockInfo", "News", "MarketNews", "Recommendations", "HistoricalPrices", "StockActions"]
  }
}
```

- **Pros**: No code deployment to update a capability when a provider changes its API. Visible to operators without reading source code.
- **Cons**: Can diverge from actual implementation silently (config says supported, but `IFinnhubClient` has no such method → runtime error). Requires schema validation. Operators may accidentally misconfigure it. Adds config complexity with no practical benefit — an API capability change always requires code changes anyway (to add or remove the HTTP call).
- **Why rejected**: The supposed benefit (no code deploy for capability changes) is illusory because capability changes always accompany code changes. The cons (silent divergence, misconfiguration risk) outweigh it.

### Separate Capability Registry Class

A standalone `ProviderCapabilityRegistry` class outside the provider assemblies, listing all providers' capabilities in one place.

- **Pros**: Single file summarises all providers' capabilities in a scannable format.
- **Cons**: Separates capability data from implementation; a developer adding a new endpoint in `FinnhubClient` must remember to also update the registry. Increases the blast radius of a "forgot to update" mistake.
- **Why rejected**: The co-location benefit of putting the capability data in the provider class itself is stronger. A single-file summary also becomes a merge conflict surface point.

### Extend `TierAwareNotSupportedException` as the Sole Capability Signal

Keep throwing `TierAwareNotSupportedException` from provider methods and fix `ClassifyError` to treat it as failover-worthy rather than terminal.

- **Pros**: Minimal new machinery; no new interface method.
- **Cons**: No pre-flight filtering — the router still calls the provider and triggers the circuit breaker for a known-unavailable endpoint. `list_providers` cannot report tier-accurate capabilities without a separate mechanism. Each call produces an unnecessary HTTP attempt (or at minimum a stack walk through the provider layer).
- **Why rejected**: Accepted as a necessary **companion fix** (ClassifyError must be corrected regardless), but insufficient on its own. Pre-flight filtering requires a positive capability declaration, not just a runtime exception.

## Consequences

### Positive

- Pre-flight filtering prevents unnecessary HTTP calls, rate-limiter token consumption, and circuit-breaker state mutations for unsupported endpoints.
- `list_providers` can accurately report tier-filtered capabilities without calling provider methods.
- Capability data is co-located with and consistent with the implementation.
- No new configuration schema or validation requirements.

### Negative

- Two files must be updated when a provider adds an endpoint: the implementation and the capability declaration (within the same class file — minor discipline cost).
- Operators cannot inspect capabilities in `appsettings.json`; they must read source code or `list_providers` output to understand what each tier supports.
- **Legacy tier labels (`premium`, `enterprise`) are not accepted.** Any migration from a system using those terms must explicitly update the configuration to use `"paid"`. There is no backward-compatibility mapping because the `tier` field is new functionality with no prior production usage.

## Related

- **Feature Spec**: [docs/features/issue-32-provider-free-paid-tier-handling.md](../../features/issue-32-provider-free-paid-tier-handling.md)
- **Architecture Overview**: [docs/architecture/issue-32-tier-handling-architecture.md](../issue-32-tier-handling-architecture.md)
- **Supersedes**: None
- **Related ADRs**: [ADR-001](adr-001-consolidate-architecture-documentation.md), [ADR-002](adr-002-deployment-docs-directory.md)
