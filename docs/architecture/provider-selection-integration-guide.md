# Provider Selection Integration Guide

## Purpose

This guide explains how provider selection is integrated in the current MCP server implementation.

## Related Docs

- [Feature Guide](../features/provider-selection.md)
- [Architecture](provider-selection-architecture.md)
- [Security](../security/provider-selection-security.md)
- [Testing](../testing/provider-selection-test-strategy.md)

## End-to-End Flow

1. MCP client calls a tool with normal arguments and an optional `provider` string.
2. `StockDataMcpServer.HandleToolCallAsync` reads `provider` with `GetOptionalString(arguments, "provider", string.Empty)`.
3. `ProviderSelectionValidator.Validate` validates and resolves aliases to provider IDs.
4. If no explicit provider was selected, server resolves a per-data-type default via `ResolveDefaultProviderForDataType(MapToolToDataType(toolName))`.
5. Router executes either:
   - explicit path: `ExecuteWithExplicitProviderAsync`
   - default path: failover/aggregation path based on routing config
6. Server returns tool result content plus `_meta.serviceKey` and `_meta.tier`.

## Optional `provider` Parameter

- The `provider` parameter is optional for all provider-aware tools.
- If omitted (or empty), behavior is treated as default selection mode.
- If present, it is validated against aliases and registered providers.

## Default Provider Resolution

`ProviderSelectionValidator.ResolveDefaultProviderForDataType(dataType)` resolves in this order:

1. `providerSelection.defaultProvider[dataType]`
2. `routing.dataTypeRouting[dataType].primaryProviderId`
3. hardcoded fallback: `"yahoo_finance"`

```csharp
public string? ResolveDefaultProviderForDataType(string dataType)
{
    if (_configuration.ProviderSelection.DefaultProvider.TryGetValue(dataType, out var explicitDefault)
        && !string.IsNullOrWhiteSpace(explicitDefault))
        return explicitDefault;
    if (_configuration.Routing.DataTypeRouting.TryGetValue(dataType, out var routing)
        && !string.IsNullOrWhiteSpace(routing.PrimaryProviderId))
        return routing.PrimaryProviderId;
    return "yahoo_finance";
}
```

## Explicit Provider Behavior

When `provider` is explicitly set and valid:

- Router uses `ExecuteWithExplicitProviderAsync`.
- No failover chain is attempted.
- Circuit breaker and health-check gating in failover path are bypassed.
- Provider errors are returned directly (no fallback provider retry).

## Response Metadata

Successful tool responses include provider attribution metadata:

```json
{
  "content": [
    {
      "type": "text",
      "text": "...provider payload..."
    }
  ],
  "_meta": {
    "serviceKey": "yahoo",
    "tier": "free"
  }
}
```

- `_meta.serviceKey` is derived from provider ID using `GetServiceKeyForProviderId`.
- `_meta.tier` is derived from provider config using `GetTierForProviderId`.

## Invalid Provider Behavior

If `provider` is invalid, `ProviderSelectionValidator.Validate` fails before router execution.

- Error message examples:
  - `Provider 'bloomberg' is not available. Supported providers: alphavantage, finnhub, yahoo`
  - `Provider 'x' is not currently available.`
- MCP response is an error object (current implementation uses error code `-32603`).
- For invalid-provider validation errors, provider-specific metadata is not attached.

```json
{
  "error": {
    "code": -32603,
    "message": "Provider 'bloomberg' is not available. Supported providers: alphavantage, finnhub, yahoo"
  }
}
```
