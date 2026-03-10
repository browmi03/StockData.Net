# Provider Selection User Guide

## Overview

The Provider Selection feature allows you to explicitly choose which financial data provider fulfills your requests through natural language. Instead of relying on automatic provider selection, you can say things like "get yahoo data on AAPL" or "show me finnhub prices for TSLA" to control your data source.

**Why use explicit provider selection?**

- **Data quality preferences** — Different providers may have varying accuracy or update frequency
- **Cost tracking** — Know which provider tier you're using for budget management  
- **Troubleshooting** — Isolate data discrepancies to specific providers
- **Provider-specific features** — Access data only available from certain providers

Every response includes metadata showing which provider fulfilled your request and its tier classification.

---

## Quick Start

### Basic Usage

**Explicit provider selection:**

```text
"Get yahoo data on AAPL"
"Show me alpha vantage prices for MSFT"  
"Using finnhub, what's the quote for TSLA?"
```

**Default provider (no selection):**

```text
"Get price for AAPL"
"Show me data on MSFT"
```

When you don't specify a provider, the system uses the configured default provider.

### Supported Providers

| Provider Name | Natural Language Aliases | Tier | Data Types |
|---------------|--------------------------|------|------------|
| Yahoo Finance | yahoo, yahoo finance, yf | Free | Historical prices, stock info, actions, financials, holders, options |
| Alpha Vantage | alphavantage, alpha vantage, av | Free | Historical prices, stock info |
| Finnhub | finnhub, fh | Free | Real-time quotes, news, stock info |

---

## Natural Language Patterns

The system recognizes multiple ways to express provider intent:

### Supported Phrasings

| Pattern | Example | Provider Selected |
|---------|---------|-------------------|
| `get [provider] data on [ticker]` | "get yahoo data on AAPL" | Yahoo Finance |
| `from [provider] get [ticker]` | "from alpha vantage get MSFT" | Alpha Vantage |
| `using [provider] show [ticker]` | "using finnhub show TSLA quote" | Finnhub |
| `via [provider] fetch [ticker]` | "via yahoo fetch GOOGL prices" | Yahoo Finance |
| `show me [provider] prices for [ticker]` | "show me alphavantage prices for AMZN" | Alpha Vantage |

### Case Insensitive

Provider names are case-insensitive:

```text
"Get Yahoo data" = "get yahoo data" = "GET YAHOO DATA"
```

### Spaces and Variations

The system handles common variations:

```text
"alpha vantage" = "alphavantage"
"yahoo finance" = "yahoo"
```

---

## Understanding Response Metadata

Every response includes metadata identifying the provider:

### Metadata Fields

| Field | Description | Example Values |
|-------|-------------|----------------|
| `serviceKey` | Provider identifier | `"yahoo"`, `"alphavantage"`, `"finnhub"` |
| `tier` | Service tier | `"free"`, `"premium"` |
| `rateLimitRemaining` | Remaining API quota (if available) | `450`, `null` |

### Example Response

```text
Stock data for AAPL:
- Current Price: $150.25
- Change: +2.5%
- Volume: 52,341,200

[Metadata]
Service: yahoo
Tier: free
Rate Limit Remaining: 1998
```

---

## Error Handling

### Invalid Provider

**Request:** `"Get bloomberg data on AAPL"`

**Response:**

```text
Error: Provider 'bloomberg' is not configured.
Available providers: yahoo, alphavantage, finnhub
```

**Action:** Use one of the listed available providers.

### Provider Not Available

**Request:** `"Get alphavantage data on AAPL"` (when API key is not configured)

**Response:**

```text
Error: Provider 'alphavantage' is not available. Missing required API credentials.
```

**Action:** Contact your system administrator to configure the provider.

### Provider API Failure

**Request:** `"Get yahoo data on INVALID_TICKER"`

**Response:**

```text
Error: Yahoo Finance API error: Symbol not found
```

**Action:** Verify the ticker symbol and try again. Note that when you explicitly select a provider, the system does NOT automatically try other providers on failure.

---

## Default Provider Behavior

### When You Don't Specify a Provider

If you don't mention a provider in your request, the system uses the default provider configured by your administrator. This default may differ by data type:

| Data Type | Typical Default |
|-----------|-----------------|
| Historical Prices | Yahoo Finance |
| Stock Information | Yahoo Finance |
| Real-time Quotes | Finnhub |
| News Aggregation | All enabled providers |

**Example with default provider:**

```text
Request: "Get price for AAPL"
Response includes: serviceKey: "yahoo" (or whatever default is configured)
```

### Default vs. Explicit Selection

| Aspect | Default Provider | Explicit Provider |
|--------|------------------|-------------------|
| Fallback behavior | May try alternative providers if default fails | No fallback — returns error immediately |
| Circuit breaker | Active | Bypassed |
| Rate limiting | Applies | Applies |
| Metadata | Shows actual provider used | Shows your selected provider |

---

## Best Practices

### When to Use Explicit Provider Selection

✅ **Use explicit selection when:**

- You need data from a specific source for consistency
- You're troubleshooting data discrepancies  
- You want to manage costs by controlling which tier you use
- A specific provider has unique data you need

❌ **Use default provider when:**

- You want the fastest response (automatic failover enabled)
- You don't care about the data source
- You're making exploratory queries

### Cost Management

Monitor the `tier` metadata to track which service levels you're using:

- `tier: "free"` — No cost implications
- `tier: "premium"` — May count against paid API quotas

### Rate Limiting

All providers have rate limits. The `rateLimitRemaining` field (when available) shows how many requests you have left:

- Watch for low values (< 100) to avoid hitting limits
- Consider spacing out requests if you're doing bulk operations
- Explicit provider selection does NOT bypass rate limits

---

## Troubleshooting

### Problem: Provider Not Recognized

**Symptom:** `"Provider 'xyz' is not configured"`

**Solutions:**

1. Check spelling of provider name
2. Use one of the supported aliases (see [Supported Providers](#supported-providers))
3. Verify the provider is configured in your system

### Problem: Data Differs Between Providers

**Symptom:** Different results from different providers for the same ticker

**Explanation:** Provider data sources may differ in:

- Update frequency (real-time vs. delayed)
- Data completeness
- Price adjustment methodology

**Solution:** Use explicit provider selection to ensure consistency within a single analysis session.

### Problem: Request Timeout

**Symptom:** No response or timeout error

**Solutions:**

1. Check if the provider is experiencing an outage
2. Try a different provider explicitly
3. Verify your network connection

### Problem: Rate Limit Exceeded

**Symptom:** `"Rate limit exceeded"` error

**Solutions:**

1. Wait for the rate limit window to reset (typically hourly or daily)
2. Use a different provider that has remaining quota
3. Contact your administrator about higher rate limits

---

## Advanced Usage

### Combining Provider Selection with Other Parameters

You can combine explicit provider selection with all other request parameters:

```text
"Get yahoo historical prices for AAPL from 2023-01-01 to 2023-12-31"
"Using finnhub, show me real-time quotes for TSLA and NVDA"
"From alpha vantage get quarterly financials for MSFT"
```

### Provider-Specific Features

Some providers offer unique data:

- **Yahoo Finance:** Options chains, holder information, stock actions (splits, dividends)
- **Alpha Vantage:** Technical indicators (when supported)
- **Finnhub:** Real-time news sentiment, earnings surprises

Reference the provider's documentation for their specific capabilities.

---

## Related Documentation

- **Feature Specification:** [provider-selection.md](provider-selection.md)
- **Configuration Guide:** [../deployment/provider-selection-configuration.md](../deployment/provider-selection-configuration.md)
- **Architecture:** [../architecture/provider-selection-architecture.md](../architecture/provider-selection-architecture.md)
- **API Reference:** [../architecture/provider-selection-api.md](../architecture/provider-selection-api.md)
- **Security:** [../security/provider-selection-security.md](../security/provider-selection-security.md)

---

## Support

**For configuration issues:** Contact your system administrator  
**For provider availability:** Check [deployment configuration](../deployment/provider-selection-configuration.md)  
**For API integration:** See [integration guide](../architecture/provider-selection-integration-guide.md)
