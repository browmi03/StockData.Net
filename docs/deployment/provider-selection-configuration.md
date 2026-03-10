# Provider Selection Configuration Guide

## Overview

This guide explains how to configure the Provider Selection feature for system administrators and DevOps engineers. Provider selection allows users to explicitly choose financial data providers through natural language requests.

**Target audience:** System administrators, DevOps engineers, deployment managers.

---

## Configuration Files

### Primary Configuration File

**Location:** `StockData.Net.McpServer/appsettings.json`

### Environment Variables

Provider API credentials are stored in environment variables:

- `FINNHUB_API_KEY` — Finnhub API key
- `ALPHAVANTAGE_API_KEY` — Alpha Vantage API key
- `YAHOO_FINANCE_API_KEY` — Yahoo Finance API key (optional for most operations)

---

## Configuration Schema

### Complete Configuration Structure

```json
{
  "providers": [
    {
      "id": "yahoo_finance",
      "type": "YahooFinanceProvider",
      "enabled": true,
      "priority": 1,
      "tier": "free",
      "configuration": {
        "baseUrl": "https://query2.finance.yahoo.com"
      }
    },
    {
      "id": "alphavantage",
      "type": "AlphaVantageProvider",
      "enabled": true,
      "priority": 2,
      "tier": "free",
      "configuration": {
        "apiKey": "${ALPHAVANTAGE_API_KEY}",
        "baseUrl": "https://www.alphavantage.co"
      }
    },
    {
      "id": "finnhub",
      "type": "FinnhubProvider",
      "enabled": true,
      "priority": 3,
      "tier": "free",
      "configuration": {
        "apiKey": "${FINNHUB_API_KEY}",
        "baseUrl": "https://finnhub.io"
      }
    }
  ],
  
  "providerSelection": {
    "aliases": {
      "yahoo": "yahoo_finance",
      "yahoo finance": "yahoo_finance",
      "yf": "yahoo_finance",
      "alphavantage": "alphavantage",
      "alpha vantage": "alphavantage",
      "av": "alphavantage",
      "finnhub": "finnhub",
      "fh": "finnhub"
    },
    "defaultProvider": {
      "HistoricalPrices": "yahoo_finance",
      "StockInfo": "yahoo_finance",
      "News": null,
      "MarketNews": null,
      "StockActions": "yahoo_finance",
      "FinancialStatement": "yahoo_finance",
      "HolderInfo": "yahoo_finance",
      "OptionExpirationDates": "yahoo_finance",
      "OptionChain": "yahoo_finance",
      "Recommendations": "yahoo_finance"
    }
  },
  
  "routing": {
    "defaultFailoverBehavior": "tryAll",
    "dataTypeRouting": [
      {
        "dataType": "HistoricalPrices",
        "primaryProviderId": "yahoo_finance",
        "fallbackProviderIds": ["alphavantage"]
      }
    ]
  }
}
```

---

## Configuration Sections

### Provider Configuration

Each provider in the `providers[]` array requires:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | Yes | Unique provider identifier (e.g., "yahoo_finance") |
| `type` | string | Yes | Provider adapter class name |
| `enabled` | boolean | Yes | Whether provider is available for use |
| `priority` | integer | Yes | Routing priority (lower = higher priority) |
| `tier` | string | Yes | Service tier classification ("free", "premium") |
| `configuration` | object | Yes | Provider-specific settings (API keys, URLs) |

#### Example Provider Entry

```json
{
  "id": "yahoo_finance",
  "type": "YahooFinanceProvider",
  "enabled": true,
  "priority": 1,
  "tier": "free",
  "configuration": {
    "baseURL": "https://query2.finance.yahoo.com",
    "timeout": 10
  }
}
```

### Provider Selection Configuration

The `providerSelection` section configures natural language mapping and defaults:

#### Aliases

Maps natural language provider names to provider IDs:

| Field | Type | Description |
|-------|------|-------------|
| `aliases` | object | Key-value pairs: user input → provider ID |

**Alias Rules:**

- Keys are case-insensitive (automatically normalized)
- All values must reference a valid provider ID in `providers[]`
- Multiple aliases can map to the same provider

**Example:**

```json
"aliases": {
  "yahoo": "yahoo_finance",
  "yahoo finance": "yahoo_finance",
  "yf": "yahoo_finance"
}
```

#### Default Provider

Specifies which provider to use when none is explicitly selected:

| Field | Type | Description |
|-------|------|-------------|
| `defaultProvider` | object | Data type → provider ID mapping |

**Supported Data Types:**

- `HistoricalPrices`
- `StockInfo`
- `News`
- `MarketNews`
- `StockActions`
- `FinancialStatement`
- `HolderInfo`
- `OptionExpirationDates`
- `OptionChain`
- `Recommendations`

**Default Behavior:**

- `null` value: use existing routing configuration (failover chains active)
- Provider ID value: route exclusively to that provider (no failover)

**Example:**

```json
"defaultProvider": {
  "HistoricalPrices": "yahoo_finance",
  "StockInfo": "yahoo_finance",
  "News": null
}
```

---

## Configuration Scenarios

### Scenario 1: Enable All Providers

All providers available, Yahoo Finance as default:

```json
{
  "providers": [
    { "id": "yahoo_finance", "enabled": true, "priority": 1, "tier": "free" },
    { "id": "alphavantage", "enabled": true, "priority": 2, "tier": "free" },
    { "id": "finnhub", "enabled": true, "priority": 3, "tier": "free" }
  ],
  "providerSelection": {
    "aliases": {
      "yahoo": "yahoo_finance",
      "alphavantage": "alphavantage",
      "finnhub": "finnhub"
    },
    "defaultProvider": {
      "HistoricalPrices": "yahoo_finance",
      "StockInfo": "yahoo_finance"
    }
  }
}
```

### Scenario 2: Disable a Provider

Disable Alpha Vantage universally:

```json
{
  "providers": [
    { "id": "yahoo_finance", "enabled": true },
    { "id": "alphavantage", "enabled": false },
    { "id": "finnhub", "enabled": true }
  ]
}
```

**Result:** Users cannot explicitly select Alpha Vantage. Validator returns error: `"Provider 'alphavantage' is disabled in configuration."`

### Scenario 3: Premium Provider with Rate Limits

Configure a premium-tier provider:

```json
{
  "providers": [
    {
      "id": "premium_provider",
      "type": "PremiumProviderAdapter",
      "enabled": true,
      "priority": 1,
      "tier": "premium",
      "configuration": {
        "apiKey": "${PREMIUM_PROVIDER_API_KEY}",
        "rateLimit": {
          "requestsPerHour": 5000,
          "requestsPerDay": 50000
        }
      }
    }
  ],
  "providerSelection": {
    "aliases": {
      "premium": "premium_provider"
    },
    "defaultProvider": {
      "HistoricalPrices": "premium_provider"
    }
  }
}
```

### Scenario 4: Provider with Missing Credentials

Yahoo Finance enabled but Alpha Vantage missing API key:

```json
{
  "providers": [
    { "id": "yahoo_finance", "enabled": true },
    {
      "id": "alphavantage",
      "enabled": true,
      "configuration": {
        "apiKey": "${ALPHAVANTAGE_API_KEY}"
      }
    }
  ]
}
```

**Environment:** `ALPHAVANTAGE_API_KEY` is not set

**Result:** Validator returns error: `"Provider 'alphavantage' is not available. Missing required API credentials."`

---

## Environment Variables

### Setting Environment Variables

#### Windows (PowerShell)

```powershell
$env:FINNHUB_API_KEY = "your_finnhub_key_here"
$env:ALPHAVANTAGE_API_KEY = "your_alphavantage_key_here"
```

#### Linux / macOS (Bash)

```bash
export FINNHUB_API_KEY="your_finnhub_key_here"
export ALPHAVANTAGE_API_KEY="your_alphavantage_key_here"
```

#### Docker / Container Environments

Pass as environment variables in `docker-compose.yml`:

```yaml
services:
  stockdata-mcp:
    environment:
      - FINNHUB_API_KEY=${FINNHUB_API_KEY}
      - ALPHAVANTAGE_API_KEY=${ALPHAVANTAGE_API_KEY}
```

Or in Kubernetes `Deployment`:

```yaml
env:
  - name: FINNHUB_API_KEY
    valueFrom:
      secretKeyRef:
        name: provider-credentials
        key: finnhub-api-key
```

### Obtaining API Keys

| Provider | Free Tier | Registration URL |
|----------|-----------|------------------|
| Yahoo Finance | Yes (no key required for basic usage) | N/A |
| Alpha Vantage | Yes (25 requests/day) | https://www.alphavantage.co/support/#api-key |
| Finnhub | Yes (60 requests/minute) | https://finnhub.io/register |

---

## Validation and Troubleshooting

### Validation at Startup

The system validates configuration when the MCP server starts:

| Validation Check | Error if Invalid |
|------------------|------------------|
| All alias targets exist in `providers[]` | `ConfigurationException: Alias 'xyz' references unknown provider 'abc'` |
| All enabled providers have `tier` | `ConfigurationException: Provider 'xyz' missing required 'tier' field` |
| All `defaultProvider` entries reference enabled providers | `ConfigurationException: Default provider 'xyz' is not enabled` |

**Check logs on startup:**

```
[INFO] Configuration validated successfully
[INFO] Provider selection enabled: yahoo, alphavantage, finnhub
[INFO] Default providers: HistoricalPrices=yahoo_finance, StockInfo=yahoo_finance
```

### Common Configuration Errors

#### Error: Alias References Unknown Provider

```
ConfigurationException: Alias 'yahoo' references unknown provider 'yahoo_finance'
```

**Cause:** `providers[]` array doesn't contain a provider with `id: "yahoo_finance"`  
**Solution:** Add the provider to `providers[]` or update the alias value

#### Error: Missing Tier Field

```
ConfigurationException: Provider 'alphavantage' missing required 'tier' field
```

**Cause:** Provider entry doesn't have a `tier` field  
**Solution:** Add `"tier": "free"` (or "premium") to the provider configuration

#### Error: Default Provider Not Enabled

```
ConfigurationException: Default provider 'finnhub' for data type 'StockInfo' is not enabled
```

**Cause:** `defaultProvider` references a disabled provider  
**Solution:** Enable the provider or change the default to an enabled provider

#### Error: Invalid JSON

```
System.Text.Json.JsonException: ',' expected at line 45
```

**Cause:** Syntax error in `appsettings.json` (missing comma, bracket, quote)  
**Solution:** Use a JSON validator to find and fix the syntax error

---

## Production Deployment Checklist

- [ ] All required environment variables are set (API keys)
- [ ] `appsettings.json` syntax is valid (run through JSON validator)
- [ ] All enabled providers have valid API keys
- [ ] `tier` field is set for all providers
- [ ] `aliases` are configured for user-friendly names
- [ ] `defaultProvider` mappings reference enabled providers
- [ ] Configuration validation passes at startup (check logs)
- [ ] Test explicit provider selection with each provider
- [ ] Test default provider behavior (omit provider parameter)
- [ ] Test invalid provider error handling
- [ ] Rate limits are appropriate for expected usage
- [ ] Logging is configured to capture provider selection decisions
- [ ] Monitoring alerts are set up for provider failures

---

## Related Documentation

- **User Guide:** [../features/provider-selection-user-guide.md](../features/provider-selection-user-guide.md)
- **Feature Specification:** [../features/provider-selection.md](../features/provider-selection.md)
- **Architecture:** [../architecture/provider-selection-architecture.md](../architecture/provider-selection-architecture.md)
- **Integration Guide:** [../architecture/provider-selection-integration-guide.md](../architecture/provider-selection-integration-guide.md)
- **Security Design:** [../security/provider-selection-security.md](../security/provider-selection-security.md)

---

## Support

**For deployment issues:** Check [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)  
**For configuration validation:** Review startup logs and error messages  
**For API key issues:** Reference provider documentation linked above
