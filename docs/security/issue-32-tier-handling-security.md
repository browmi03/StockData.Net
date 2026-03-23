# Security Design: Provider Free/Paid Tier Handling

<!--
  Template owner: Security Architect
  Output directory: docs/security/
  Filename convention: issue-32-tier-handling-security.md
  Related Issue: #32
-->

## Document Info

- **Feature Spec**: [docs/features/issue-32-provider-free-paid-tier-handling.md](../features/issue-32-provider-free-paid-tier-handling.md)
- **Architecture**: [docs/architecture/stock-data-aggregation-canonical-architecture.md](../architecture/stock-data-aggregation-canonical-architecture.md)
- **Baseline Security**: [docs/security/security-summary.md](security-summary.md)
- **Prior Art**: [docs/security/provider-selection-security.md](provider-selection-security.md)
- **Status**: Draft
- **Last Updated**: 2026-03-20

---

## Security Overview

Issue #32 adds three mutually reinforcing concerns to the security surface: a new configuration dimension (`tier`), richer user-facing error messages that include per-provider failure reasons and upgrade URLs, and null-reference bug fixes across two provider implementations. Each concern is low-risk individually; the combination of all three touching the error-message pipeline at the same time elevates the overall risk to **MEDIUM**.

**Risk Assessment**: **MEDIUM**

**Deployment Model**: Single-user local process (stdio-based JSON-RPC, MCP server on localhost).

**Key findings**:

| # | Finding | Severity | Blocking |
| --- | --- | --- | --- |
| SEC-32-1 | Per-provider error messages may expose unsanitized exception text to users | **HIGH** | **Yes** |
| SEC-32-2 | `AlphaVantageProvider` uses bare `NotSupportedException` instead of `TierAwareNotSupportedException` | **MEDIUM** | **Yes** |
| SEC-32-3 | `tier` config value is not validated against the `"free"` / `"paid"` allow-list at startup | **LOW** | No |
| SEC-32-4 | Upgrade URLs must be hardcoded constants, not constructed from provider data or config | **LOW** | No |
| SEC-32-5 | Systematic tier-skip fallback can multiply external API calls per request | **LOW-MEDIUM** | No |
| SEC-32-6 | `baseUrl` per provider is not allowlisted at config load time | **LOW** | No |
| SEC-32-7 | `SensitiveDataSanitizer` regex may miss non-alphanumeric API key patterns | **LOW** | No |

---

## Threat Model

### Assets

| Asset | Classification | Owner |
| --- | --- | --- |
| Provider API keys (Finnhub, Alpha Vantage) | Confidential | DevOps/Security Team |
| Tier configuration (`"free"` / `"paid"` per provider) | Internal | DevOps Team |
| Per-provider failure reasons in error messages | Internal | System Runtime |
| Upgrade URLs embedded in error messages | Public | Product |
| Provider capability matrix (tier-filtered) | Internal | System Runtime |
| `list_providers` tier-annotated output | Internal | System Runtime |

### Threat Actors

| Actor | Capability | Motivation |
| --- | --- | --- |
| Malicious MCP Client | Sends crafted tool-call requests | Elicit verbose error messages containing internal details; exhaust rate limits |
| Local System Attacker | Reads/modifies config files and environment | Extract API keys; manipulate `tier` to change routing behaviour |
| Malicious External API | Returns crafted response payloads | Inject content into per-provider failure messages shown to users |

### Attack Surface

| Surface | Exposure | Threats |
| --- | --- | --- |
| `tier` field in `appsettings.json` | Local (file system) | Configuration tampering |
| Per-provider failure reason formatting | Internal â†’ response | Information disclosure, content injection |
| Upgrade URL inclusion in error messages | User-visible response | URL injection if constructed dynamically |
| `baseUrl` override in `appsettings.json` | Local (file system) | SSRF via config manipulation |
| Null-fix deserialized API response fields | Internal | Injection from malicious API payloads |
| `list_providers` tier-annotated output | Local (stdio) | Tier/topology disclosure |

### STRIDE Analysis

| Component | S | T | R | I | D | E |
| --- | --- | --- | --- | --- | --- | --- |
| Tier config loader | N/A | Config tampering â†’ startup validation mitigates | N/A | tier value disclosed in responses â†’ acceptable per design | Invalid value causes misconfiguration | N/A (local) |
| Per-provider error formatter | N/A | N/A | N/A | Raw exception text â†’ must sanitize before user exposure **(SEC-32-1)** | N/A | N/A (local) |
| Upgrade URL builder | N/A | URL injection if built from provider data â†’ hardcode constants **(SEC-32-4)** | N/A | Reveals which providers have paid tiers â†’ acceptable per design | N/A | N/A (local) |
| Fallback chain (tier-skip mode) | N/A | N/A | N/A | N/A | Rate-limit exhaustion via fan-out **(SEC-32-5)** | N/A (local) |
| Null-fix deserialization | N/A | N/A | N/A | Crafted API payloads surfaced in messages â†’ sanitizer covers | N/A | N/A (local) |

---

## Detailed Threat Scenarios

### SEC-32-1 (HIGH â€” BLOCKING): Per-Provider Error Message Exposes Raw Exception Text

**Description**: The new feature (User Story 3) returns per-provider failure reasons when all providers fail. The `ProviderFailoverException.ProviderErrors` dictionary holds caught exception objects. The message of those exceptions may contain:

- The raw API key value if an external API returns a response like `"Invalid API key: sk-abc123def456"`
- Internal stack-trace fragments if a poorly-matched catch block re-wraps without sanitization
- HTTP URL fragments that include query-string API key parameters (e.g., Finnhub appends `token=<key>` to URLs in some HTTP client errors)

**Current mitigations (partial)**: Both `FinnhubProvider.ExecuteSafelyAsync` and `AlphaVantageProvider.ExecuteSafelyAsync` already call `SensitiveDataSanitizer.Sanitize(ex.Message)` before rethrowing â€” so the **re-thrown** exception message is sanitized. The router's fallover loop also calls `SensitiveDataSanitizer.Sanitize(ex.Message)` before logging. However:

- The `providerErrors` dictionary stores the **original exception** object, not the sanitized string. Any new code that formats these exceptions into user-facing messages must re-sanitize `ex.Message` rather than accessing the inner exception chain.
- The `Exception.ToString()` output (stack trace + inner exceptions) must **never** appear in user-facing messages.

**Required mitigation**:

1. The new per-provider error message formatter must call `SensitiveDataSanitizer.Sanitize(exception.Message)` on each provider's exception **at the point of message construction**, not relying on prior sanitization in the throwing code path.
2. Use only `exception.Message` (not `exception.ToString()`, `exception.InnerException`, or `exception.StackTrace`) when composing user-facing text.
3. Map internal `ProviderErrorType` enum values to investor-friendly strings before including them in responses (e.g., `RateLimitExceeded` â†’ `"rate limit exceeded"`, `AuthenticationError` â†’ `"authentication failed â€” check your API key"`).
4. Add a unit test asserting that a simulated API response containing a mock API key string (`"token=ABCD1234"`) never appears verbatim in the formatted user-facing error message.

**Implementation guidance**:

```csharp
// CORRECT â€” sanitize at the point of message construction
var userMessage = SensitiveDataSanitizer.Sanitize(providerException.Message);

// WRONG â€” don't use ToString() (includes stack trace and inner exceptions)
var userMessage = providerException.ToString();

// WRONG â€” don't use InnerException chain without sanitization
var userMessage = providerException.InnerException?.Message;
```

---

### SEC-32-2 (MEDIUM â€” BLOCKING): `AlphaVantageProvider` Uses `NotSupportedException` Instead of `TierAwareNotSupportedException`

**Description**: `AlphaVantageProvider.cs` throws bare `NotSupportedException` for unsupported methods (`GetMarketNewsAsync`, `GetStockActionsAsync`, `GetFinancialStatementAsync`, `GetHolderInfoAsync`, `GetOptionExpirationDatesAsync`, `GetOptionChainAsync`, `GetRecommendationsAsync`). The `NotSupportedException` messages include the raw C# method name (e.g., `"Provider 'alphavantage' does not support GetMarketNewsAsync"`).

This is a blocking issue because:

1. The fallback router classifies exceptions by type. Without a consistent `TierAwareNotSupportedException`, the router cannot distinguish "tier limitation â†’ skip silently" from "unexpected failure â†’ record and continue". This means AlphaVantage NotSupported cases may be treated as health-degrading failures, incorrectly advancing the circuit breaker.
2. The raw C# method name (`GetMarketNewsAsync`) exposed in an error message visible to users violates the non-functional requirement for investor-friendly language.

**Required mitigation**: Replace all bare `NotSupportedException` and `Task.FromException<string>(new NotSupportedException(...))` calls in `AlphaVantageProvider.cs` with `TierAwareNotSupportedException`, specifying the correct `availableOnPaidTier` flag for each method:

| Method | `availableOnPaidTier` |
| --- | --- |
| `GetMarketNewsAsync` | N/A — available on free tier; implement method (Category A bug fix) |
| `GetStockActionsAsync` | N/A — available on free tier; implement method (Category A bug fix) |
| `GetFinancialStatementAsync` | `false` |
| `GetHolderInfoAsync` | `false` |
| `GetOptionExpirationDatesAsync` | `false` |
| `GetOptionChainAsync` | `false` |
| `GetRecommendationsAsync` | `false` |

The fallback router must then classify `TierAwareNotSupportedException` as a **non-health-degrading** skip, not as a `ServiceError` (do not advance circuit breaker state on tier limitation misses).

---

### SEC-32-3 (LOW): `tier` Config Value Not Validated Against Allow-List

**Description**: `ProviderConfiguration.Tier` is a free-form `string` defaulting to `"free"`. Configuration loading does not reject unknown tier values (e.g., `"premium"`, `"enterprise"`, `""`). Unknown values will silently be treated the same as `"free"` by string comparison in routing logic, introducing a latent misconfiguration surface.

**Required mitigation**: In `ConfigurationLoader.ValidateConfiguration` (or a new `ValidateProviderTier` method), add:

```csharp
private static readonly HashSet<string> ValidTierValues =
    new(StringComparer.OrdinalIgnoreCase) { "free", "paid" };

// During validation loop over providers:
if (!ValidTierValues.Contains(provider.Tier))
{
    throw new InvalidOperationException(
        $"Provider '{provider.Id}' has invalid tier value '{provider.Tier}'. " +
        "Valid values are: free, paid.");
}
```

This converts silent misconfiguration into a fail-fast startup error, consistent with existing configuration validation patterns.

---

### SEC-32-4 (LOW): Upgrade URLs Must Be Hardcoded Constants

**Description**: The feature spec (User Story 3.3) requires messages like *"Consider upgrading at <https://finnhub.io/pricing>"*. If the upgrade URL is sourced from provider configuration, an API response, or a database value rather than compile-time constants, a local attacker who controls configuration could replace the URL with an internal endpoint, a phishing URL, or an SSRF target.

**Required mitigation**: Define upgrade URLs as compile-time constants in a dedicated mapping class, not from configuration or external data:

```csharp
internal static class ProviderUpgradeUrls
{
    public const string Finnhub = "https://finnhub.io/pricing";
    public const string AlphaVantage = "https://www.alphavantage.co/premium/";
    // No entry for Yahoo Finance (no paid tier)
}
```

Never construct upgrade URLs by concatenating values from `appsettings.json`, provider API responses, or the `TierAwareNotSupportedException` message field.

---

### SEC-32-5 (LOW-MEDIUM): Systematic Tier-Skip Fallback Multiplies External API Calls

**Description**: When a request reaches an endpoint unavailable on a provider's free tier, the router silently skips that provider and tries the next. With three configured providers and a request that skips providers 1 and 2, up to three outbound API calls are made per single MCP request. A sustained stream of requests for paid-tier-only endpoints (e.g., `get_stock_actions`) would systematically exhaust the rate limits of all providers sequentially:

- AlphaVantage (5 req/min on free tier) is particularly vulnerable.
- A batch of 5 `get_stock_actions` requests could saturate AlphaVantage's per-minute quota for any subsequent real request.

**Existing mitigations**: Per-provider sliding-window rate limiters (Finnhub: 60/min, AlphaVantage: 5/min) and circuit breakers are already implemented and apply regardless of selection mode.

**Additional mitigations for tier-skip path**:

1. **Do not deduct from rate-limit quota for tier-skip decisions**: If a provider is skipped because its configured `tier = "free"` and the method is `availableOnPaidTier`, no HTTP call is made and no rate-limit token should be consumed. Confirm the skip happens *before* any circuit-breaker or rate-limiter check.
2. **`TierAwareNotSupportedException` must not advance circuit breaker**: Classify this exception as a non-failure skip (see SEC-32-2). A tier limitation is predictable and static â€” it should not penalize the provider's health score.
3. **Document this behaviour**: The rate-limit and circuit-breaker integration for tier-aware skips should be explicitly described in the architecture document.

---

### SEC-32-6 (LOW): `baseUrl` Not Validated Against Provider Domain Allowlist

**Description**: `appsettings.json` allows overriding `baseUrl` per provider (e.g., `"baseUrl": "https://www.alphavantage.co"`). No startup validation confirms that the configured URL is a known provider domain. A local attacker who modifies `appsettings.json` could set `baseUrl` to an internal network address (`http://169.254.169.254/latest/meta-data/`), redirecting API calls to AWS metadata or internal services.

**Context**: The deployment model is local process; the local filesystem is within the local attacker's threat boundary. However, applying defence-in-depth is worth the minimal implementation cost.

**Required mitigation**: In `ConfigurationLoader.ValidateConfiguration`, add an allowlist check for `baseUrl` values:

```csharp
private static readonly Dictionary<string, string> ProviderBaseUrlAllowlist =
    new(StringComparer.OrdinalIgnoreCase)
    {
        ["finnhub"] = "https://finnhub.io",
        ["alphavantage"] = "https://www.alphavantage.co"
    };

// During provider validation, warn (not hard-fail) if baseUrl does not
// start with the expected prefix:
if (ProviderBaseUrlAllowlist.TryGetValue(provider.Id, out var expectedPrefix)
    && !provider.Settings.TryGetValue("baseUrl", out var url)
    || !url.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
{
    _logger?.LogWarning(
        "Provider '{ProviderId}' baseUrl '{Url}' does not match expected prefix '{Expected}'. " +
        "Verify this is intentional.", provider.Id, url, expectedPrefix);
}
```

A warning-level log (not a hard failure) is appropriate because legitimate dev/test scenarios may redirect to a local mock server.

---

### SEC-32-7 (LOW): `SensitiveDataSanitizer` Regex May Miss Non-Alphanumeric Key Patterns

**Description**: The current `SensitiveTokenPattern` regex matches only alphanumeric tokens (`[A-Za-z0-9]+`) of 8+ characters containing both letters and digits. Some API key formats include hyphens, underscores, or dots (e.g., `"abc-123-def-456"`, `"pk_live_XXXX"`). Such keys would not be matched and would pass through sanitization into log entries and error messages.

**Context**: Finnhub and Alpha Vantage free-tier keys are typically all-alphanumeric (matching the current pattern). This risk is latent; it would only materialize if a provider returns their own key format in an error message.

**Required mitigation**: Extend the regex or add a secondary pattern covering dash/underscore-separated segments:

```csharp
// Existing pattern (keep)
[GeneratedRegex(@"\b(?=[A-Za-z0-9]{8,}\b)(?=[A-Za-z0-9]*[A-Za-z])(?=[A-Za-z0-9]*\d)[A-Za-z0-9]+\b")]
private static partial Regex SensitiveTokenPattern();

// Add supplementary pattern for key formats with separators:
[GeneratedRegex(@"\b[A-Za-z0-9]{4,}(?:[-_][A-Za-z0-9]{4,}){2,}\b")]
private static partial Regex SeparatedTokenPattern();
```

Apply both patterns in `Sanitize()`. Add unit tests for typical key formats returned by Finnhub and Alpha Vantage error responses.

---

## Authentication

No changes to authentication mechanisms. Provider API keys continue to authenticate via:

- **Finnhub**: `token` query parameter on every request
- **AlphaVantage**: `apikey` query parameter on every request
- **Yahoo Finance**: Cookie/crumb session (no API key required)

The `tier` configuration is a local access-control hint only; it does not change how the server authenticates with providers.

**New concern**: The `tier` value must not appear in outbound HTTP headers or query parameters sent to provider APIs. It is a local routing hint only and must remain internal to the application.

---

## Authorization

No new authorization roles are introduced. `tier` configuration is operator-controlled (set in `appsettings.json` at deployment time); there is no runtime mechanism for an MCP client to change the tier of a provider.

**Clarification on "tier tamper" risk**: An MCP client that sends crafted requests cannot alter tier configuration â€” the tier is read from the local configuration file at startup and is immutable during process lifetime. A local system attacker who modifies `appsettings.json` and restarts the server could change `tier`, but this results in API-level rejections from the provider (not privilege escalation), because the API provider's subscription tier is enforced server-side.

---

## Data Security

### Per-Provider Error Messages

Error messages returned to MCP clients when all providers fail will contain:

| Field | Classification | Handling |
| --- | --- | --- |
| Provider name (e.g., "Finnhub") | Internal | Acceptable to disclose per design |
| Tier label (e.g., "free tier") | Internal | Acceptable to disclose per design |
| Failure reason (investor-friendly) | Internal | Must map from internal `ProviderErrorType` â€” never raw exception message |
| Upgrade URL (e.g., `https://finnhub.io/pricing`) | Public | Hardcoded constant â€” never dynamic |
| API key values | Confidential | **Must not appear** â€” enforced by `SensitiveDataSanitizer` |
| Internal method names or class names | Internal | **Must not appear** â€” use friendly endpoint names |
| HTTP status codes (e.g., "429") | Internal | Translate to friendly language (e.g., "rate limit exceeded") |

### `list_providers` Output

The tier-annotated `list_providers` output discloses which providers have paid-tier capabilities. This is consistent with the prior decision in [provider-selection-security.md](provider-selection-security.md) (information disclosure of tier is acceptable for single-user local deployment).

---

## Secret Management

No new secret storage requirements. The `tier` field is not a secret.

**Existing controls remain in effect**:

- API keys loaded from environment variable substitution (`${VAR_NAME}`) at startup
- `SecretValue` wrapper prevents exposure in debugger views, serialization, and `ToString()` calls
- `SensitiveDataSanitizer` applied to all error messages before log output and user-visible responses
- Validation rejects `appsettings.json` files that contain literal key values (pattern-matched)

**New control required** (see SEC-32-1): The new per-provider error message builder must explicitly invoke `SensitiveDataSanitizer.Sanitize()` on each exception message it incorporates. This must not be assumed from prior layers.

---

## Input Validation

### Tier Configuration Value

The `tier` field accepts only `"free"` or `"paid"` (case-insensitive). Enforce at config load time (see SEC-32-3).

### Provider Error Reason Text

When provider failure reasons are formatted:

- Source must be internal `ProviderErrorType` enum values mapped to fixed strings
- Exception messages must pass through `SensitiveDataSanitizer.Sanitize()`
- No user-supplied text is interpolated into the error reason strings
- Upgrade URLs are compile-time constants (see SEC-32-4)

### Ticker Symbol Validation

No change. Existing `ValidateTicker()` in both Finnhub and AlphaVantage providers enforces alphanumeric pattern before any API call.

---

## Audit and Logging

**New logging requirements for Issue #32**:

1. When a provider is skipped due to tier limitation, log at `Debug` level:

   ```text
   Provider '{ProviderId}' skipped for {DataType}: tier limitation (method not available on {Tier} tier)
   ```

   This must **not** be logged at `Warning` level (tier skips are expected, not failures) and must **not** advance circuit-breaker state.

2. When an all-providers-failed error message is formatted for user output, log the sanitized message at `Information` level alongside the full (sanitized) `ProviderFailoverException` at `Error` level. The user-facing message and the internal log message may differ; only the internal log needs the full per-provider failure chain.

3. Do not log the upgrade URL separately — it adds noise without security value.

**Existing logging controls remain in effect**: structured logging, sanitized messages, no raw exception `ToString()` in user-visible output.

---

## Compliance

No regulatory compliance requirements (GDPR, HIPAA, PCI-DSS) are introduced by this feature. Financial data returned is public market data. No PII is processed.

---

## Security Test Cases

The following test cases must pass before the feature is considered ready for merge:

| ID | Test | Type | Required |
| --- | --- | --- | --- |
| ST-32-1 | Formatted user error message containing a simulated Finnhub error response with an embedded API key (`token=ABCD1234EF`) redacts the key to `[REDACTED]` | Unit | **Blocking** |
| ST-32-2 | Per-provider error message does not contain any Java method name, C# class name, or stack trace fragment | Unit | **Blocking** |
| ST-32-3 | Setting `tier: "premium"` in appsettings.json causes startup to fail with a descriptive error | Unit | Non-blocking |
| ST-32-4 | Upgrade URL in error message for Finnhub tier limitation exactly equals `https://finnhub.io/pricing` regardless of appsettings.json content | Unit | **Blocking** |
| ST-32-5 | `AlphaVantageProvider.GetMarketNewsAsync` throws `TierAwareNotSupportedException` (not `NotSupportedException`) | Unit | **Blocking** |
| ST-32-6 | A `TierAwareNotSupportedException` from `AlphaVantageProvider` does not advance the circuit breaker failure counter in the router | Unit | **Blocking** |
| ST-32-7 | Rate-limit token is not consumed when a provider is skipped due to tier limitation (no HTTP call made) | Unit | Non-blocking |
| ST-32-8 | `list_providers` output for Finnhub configured as `tier: "free"` does not list `get_historical_stock_prices` | Integration | Non-blocking |
| ST-32-9 | `list_providers` output for Finnhub configured as `tier: "paid"` lists `get_historical_stock_prices` | Integration | Non-blocking |
| ST-32-10 | AlphaVantage error with dash-separated key format (e.g., `"key-abcd-1234-efgh"`) is redacted when included in a log or error message | Unit | Non-blocking |

---

## Blocking Items Summary

The following items must be resolved **before development begins** on the tier-routing and error-messaging portions of this feature:

### BLK-1: Establish the per-provider error message sanitization contract (SEC-32-1)

**Owner**: Developer implementing the user-facing error message formatter  
**Action**: Define and document the exact sanitization steps applied to each provider's exception before message construction. Add ST-32-1 and ST-32-2 as gating tests.

### BLK-2: Migrate `AlphaVantageProvider` to `TierAwareNotSupportedException` (SEC-32-2)

**Owner**: Developer implementing provider bug fixes  
**Action**: Replace all bare `NotSupportedException` instances in `AlphaVantageProvider.cs` with `TierAwareNotSupportedException`. Update the router's error classification (`ClassifyError`) to treat `TierAwareNotSupportedException` as a non-health-degrading skip. Add ST-32-5 and ST-32-6 as gating tests.

### BLK-3: Define upgrade URL constants before error message implementation (SEC-32-4)

**Owner**: Developer implementing error message formatting  
**Action**: Create `ProviderUpgradeUrls` static class (or similar) with compile-time string constants for each provider's upgrade URL. Verify via ST-32-4.

---

## Related Documents

- Feature Specification: [docs/features/issue-32-provider-free-paid-tier-handling.md](../features/issue-32-provider-free-paid-tier-handling.md)
- Baseline Security Overview: [docs/security/security-summary.md](security-summary.md)
- Provider Selection Security: [docs/security/provider-selection-security.md](provider-selection-security.md)
- Coding Standards (Security): [docs/coding-standards/security-performance.md](../coding-standards/security-performance.md)
- Architecture: [docs/architecture/stock-data-aggregation-canonical-architecture.md](../architecture/stock-data-aggregation-canonical-architecture.md)
