# Provider Selection API

## Scope

This document describes the provider-selection API contract implemented by `StockDataMcpServer` and `StockDataProviderRouter`.

## Related Docs

- [Feature Guide](../features/provider-selection.md)
- [Architecture](provider-selection-architecture.md)
- [Security](../security/provider-selection-security.md)
- [Testing](../testing/provider-selection-test-strategy.md)

## Common Request Contract

All provider-aware MCP tools accept an optional `provider` argument.

```json
{
  "name": "get_stock_info",
  "arguments": {
    "ticker": "AAPL",
    "provider": "yahoo"
  }
}
```

- `provider` omitted or empty: default-provider path.
- `provider` present: explicit-provider path (after validation).

## Tools with Optional `provider`

Current implementation includes `provider` on:

- `get_historical_stock_prices`
- `get_stock_info`
- `get_finance_news`
- `get_market_news`
- `get_stock_actions`
- `get_financial_statement`
- `get_holder_info`
- `get_option_expiration_dates`
- `get_option_chain`
- `get_recommendations`

## Provider Validation and Resolution

Validation entrypoint: `ProviderSelectionValidator.Validate(string? provider)`

- Allowed characters and length are checked.
- Alias map is used to resolve user-facing names to provider IDs.
- Registered provider availability is enforced.

Default resolution entrypoint: `ResolveDefaultProviderForDataType(string dataType)`

Resolution order:

1. `providerSelection.defaultProvider[dataType]`
2. `routing.dataTypeRouting[dataType].primaryProviderId`
3. `"yahoo_finance"`

## Explicit Provider Semantics

When `provider` is explicitly provided and valid:

- Router executes explicit path (`ExecuteWithExplicitProviderAsync`).
- No fallback chain is attempted.
- Circuit breaker checks in failover path are bypassed.
- Failure returns immediately from selected provider path.

## Successful Response Contract

Tool call responses include normal `content` and provider metadata in `_meta`:

```json
{
  "content": [
    {
      "type": "text",
      "text": "..."
    }
  ],
  "_meta": {
    "serviceKey": "yahoo",
    "tier": "free"
  }
}
```

- `_meta.serviceKey`: alias-like provider key returned by `GetServiceKeyForProviderId`.
- `_meta.tier`: provider tier returned by `GetTierForProviderId`.

## Error Behavior

### Invalid provider input

If validation fails, tool execution stops before router calls.

```json
{
  "error": {
    "code": -32603,
    "message": "Provider 'bloomberg' is not available. Supported providers: alphavantage, finnhub, yahoo"
  }
}
```

### Explicit provider runtime failure

When an explicit provider is selected and fails during execution:

- no fallback provider is attempted
- the error is returned for that selected provider path
- server may include provider metadata in error `data` via provider-aware exception handling

```json
{
  "error": {
    "code": -32603,
    "message": "...sanitized provider error...",
    "data": {
      "serviceKey": "yahoo",
      "tier": "free"
    }
  }
}
```
