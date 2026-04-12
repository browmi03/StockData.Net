# Security Design: Issue #51 Social Media X Provider

## Document Info

- **Feature Spec**: [Issue #51 Social Media X Provider](../features/issue-51-social-media-x-provider.md)
- **Architecture Context**: [Provider Selection Architecture](../architecture/provider-selection-architecture.md)
- **Baseline Security**: [Security Summary](security-summary.md)
- **Related Security Context**: [Issue #32 Tier Handling Security](issue-32-tier-handling-security.md)
- **Status**: Draft
- **Last Updated**: 2026-04-12

## Security Overview

Issue #51 introduces a new external content ingestion path from X/Twitter through the `get_social_feed` tool. The security posture is driven by six primary concerns:

1. Bearer token secrecy and lifecycle control.
2. Untrusted input handling for `handles`, `query`, and range parameters.
3. Untrusted output handling for social post content and upstream error text.
4. Rate-limit abuse resilience to protect provider account health.
5. Strict transport security on all outbound calls.
6. Cross-category isolation so social provider behavior does not regress financial tools.

**Overall Risk Rating**: **MEDIUM**  
Rationale: local MCP deployment reduces external exposure, but dependency on external social content and strict API quotas create meaningful integrity, availability, and disclosure risk.

## Threat Model

### Scope

- In scope component: `get_social_feed` MCP tool and X provider integration path.
- In scope data flow: request parameters -> provider selection (`SocialMedia`) -> outbound X API call -> mapping to `SocialPost` -> MCP response.
- Out of scope: write operations to X, private account access, user-delegated OAuth flows.

### Assets

| Asset | Classification | Owner |
| --- | --- | --- |
| `X_BEARER_TOKEN` | Confidential | DevOps/Security |
| X API response payloads (post content, metadata) | Internal (untrusted external content) | Runtime |
| `get_social_feed` request parameters (`handles`, `query`) | Internal | Runtime |
| Provider routing metadata (category/type) | Internal | Platform |
| Tool responses sent to MCP clients | Internal | Runtime |
| Security logs and error telemetry | Internal | Platform |

### Threat Actors

| Actor | Capability | Motivation |
| --- | --- | --- |
| Malicious MCP client | Sends crafted parameters at high frequency | Trigger abuse, exfiltrate details, force rate-limit exhaustion |
| Malicious or compromised upstream source | Returns crafted content/errors | Inject content and influence downstream systems |
| Local attacker with config/env access | Reads or modifies local process config | Steal token or weaken controls |

### Attack Surface

| Surface | Exposure | Threats |
| --- | --- | --- |
| `get_social_feed` parameters | Local MCP | Input injection, validation bypass, abuse traffic |
| Outbound HTTPS to X API | External | MITM downgrade attempts, availability failures |
| Error translation path | Local response/logs | Secret leakage, internal detail leakage |
| Response content (`SocialPost.content`) | Local downstream consumers | Content injection/log injection propagation |
| Cache/throttle layer | Local runtime | Quota exhaustion, stale-response confusion |

### STRIDE Analysis for `get_social_feed`

| STRIDE | Threat | Primary Mitigation | Residual Risk |
| --- | --- | --- | --- |
| Spoofing | Caller attempts to impersonate trusted upstream or provider identity | Use fixed HTTPS X endpoints only, validate host/scheme, no caller-controlled base URL | Low |
| Tampering | Crafted `handles`/`query` alter outbound request semantics | Strict allow-list validation and canonicalization; reject invalid characters and blank queries | Medium-Low |
| Repudiation | Caller denies abusive usage or repeated quota-draining calls | Structured audit events for request hash, validation outcomes, and rate-limit decisions | Medium |
| Information Disclosure | Bearer token leaks via logs/errors/response payloads | Env-only secret load, redaction on all error paths, never include auth headers or token fragments in logs | Low |
| Denial of Service | Repeated calls exhaust X API limits or local resources | Per-tool throttling, bounded parameters, cache within window, circuit breaker/retry alignment | Medium |
| Elevation of Privilege | Cross-category routing allows social calls to influence financial provider path | Enforce `SocialMedia` provider category isolation and deny-by-default routing | Low |

## Authentication

- **Mechanism**: OAuth 2.0 Bearer Token (app-level access).
- **Credential Source**: environment variable `X_BEARER_TOKEN` only.
- **Credential Handling**:
  - Token is loaded at startup or provider initialization from process environment.
  - Token is attached to outbound requests via `Authorization: Bearer <token>` header.
  - Token value is never included in tool output, logs, exceptions, telemetry labels, or diagnostics.
- **Failure Behavior**:
  - Missing token: structured configuration error without secret details.
  - Invalid/expired token: structured authentication failure without echoing token content.

## Authorization

- **Model**: Capability and provider-category based routing.
- **Enforcement Point**: tool handler and provider selection layer.
- **Default Policy**: deny-by-default for unsupported provider categories and invalid inputs.
- `get_social_feed` may invoke only `ISocialMediaProvider` implementations.
- Existing `get_stock_data` and `get_market_events` flows remain restricted to financial/event providers.

## Data Security

- **Encryption in Transit**: TLS only (HTTPS endpoints). Outbound HTTP downgrade is not permitted.
- **Encryption at Rest**: no persistent storage required for social post content in current scope.
- **Data Classification**:
  - Social content is externally sourced and untrusted.
  - Handles and queries are internal user input and must be sanitized.
- **Output Handling**:
  - `SocialPost.content` is returned as plain untrusted text; no rendering/evaluation assumptions.
  - Log records must sanitize control characters and avoid direct interpolation of untrusted content.

## Secret Management

- **Primary Secret**: `X_BEARER_TOKEN`.
- **Storage Policy**: environment variable only; no hardcoded token and no checked-in secret files.
- **Operational Integration**:
  - Local/dev: environment variable injection at process start.
  - CI/CD and hosted runtime: inject from managed secret store (for example, GitHub Actions secret and/or cloud vault) into environment at runtime, never into source-controlled config.
- **Rotation Policy**:
  - Rotate on suspected exposure, provider revocation, or scheduled key hygiene window.
  - Rotation requires process restart or token reload path that does not log current/previous token values.
- **Verification**:
  - Secret scanning in CI to detect hardcoded bearer tokens.
  - Negative tests to ensure token substrings never appear in logs or tool responses.

## Input Validation

### Validation Strategy

Allow-list and schema-first validation before provider dispatch. If validation fails, do not call X API.

### Handle Rules

- Accept handle characters: `A-Z`, `a-z`, `0-9`, `_`.
- Normalize by trimming whitespace and removing a single leading `@` if present.
- Length constraint: 1-15 characters after normalization.
- Reject handles containing URL control characters, path separators, spaces, query delimiters, or percent-encoding sequences.
- Deduplicate handles case-insensitively before outbound calls.

### Query Rules

- Optional but must be non-blank when supplied.
- Trim whitespace; reject if empty after trim.
- Enforce maximum length (for example, 256 characters) to reduce abuse surface.
- Reject unsafe control characters and malformed Unicode sequences.

### Numeric Parameter Rules

- `max_results`: 1-100.
- `lookback_hours`: 1-168, then capped to effective provider-tier window with explicit user-facing notice.
- At least one of `handles` or `query` must be valid and non-empty.

### Allow-List Consideration (Open Question OQ-5)

Assessment: unrestricted handle queries increase abuse and legal/compliance exposure and may create SSRF-adjacent pressure if future logic ever dereferences profile URLs or external links.

Recommendation:

1. Prefer a configurable handle allow-list for production deployments with high trust requirements.
2. If unrestricted mode is allowed, gate it with stricter throttling, request quotas, and audit logging.
3. Do not accept caller-provided base URLs or platform host overrides under any mode.

## API and Network Security

- Outbound calls are restricted to X API HTTPS endpoints.
- Http client configuration must reject insecure redirect/downgrade behavior.
- Timeouts, retries, and circuit breaker policies must align with existing resilience controls without exposing raw upstream details.
- No dynamic endpoint composition from user input.

## Rate Limit Abuse Prevention

- Implement per-tool request throttling to bound call bursts.
- Use response caching keyed by normalized request parameters within the X limit window.
- Serve cache hits without calling X API and include `cached_at` for transparency.
- Respect provider reset metadata when available; communicate investor-friendly reset guidance.
- Ensure rate-limit and cache logic cannot be bypassed by parameter padding or case variants.

## Security Requirements (Cross-Referenced to Acceptance Criteria)

1. `X_BEARER_TOKEN` must be loaded only from environment variable and never hardcoded. (AC-14, AC-15)
2. No token value or substring may appear in logs, exceptions, tool output, or telemetry fields. (AC-14, AC-16)
3. Missing or invalid token must return structured, investor-friendly authentication errors only. (AC-15, AC-16)
4. `handles` must be normalized and validated against an allow-list character set and length bounds. (AC-5, AC-2, AC-3)
5. `query` must be non-blank if provided; blank or whitespace-only input is rejected before outbound calls. (AC-3, AC-16)
6. `max_results` and `lookback_hours` must be range-validated and constrained before provider dispatch. (AC-4, AC-20)
7. At least one of `handles` or `query` must be present; otherwise validation error with no provider call. (AC-2, AC-15)
8. All outbound provider requests must use HTTPS and must not permit HTTP downgrade or user-controlled host selection. (AC-14, AC-17)
9. `SocialPost.content` must be treated as untrusted external data and never interpreted/executed by the server. (AC-17)
10. Structured error handling must avoid exposing raw HTTP status internals or stack traces to clients. (AC-18)
11. 429 handling must produce investor-friendly rate-limit messaging, optionally with reset-time hints. (AC-18)
12. Cache/throttle controls must reduce repeated identical API calls in-window and expose `cached_at` metadata. (AC-19)
13. `get_social_feed` routing must remain isolated to social providers; no cross-category invocation. (AC-21, AC-22, AC-25)
14. `list_providers` output must label social vs financial categories accurately without leaking secret values. (AC-22, AC-26)
15. Security logging must preserve forensic value while redacting credentials and sanitizing untrusted content. (AC-16, AC-17, AC-18)

## Vulnerability Analysis (OWASP Top 10 2021)

| OWASP Category | Relevance | Risk | Controls |
| --- | --- | --- | --- |
| A01 Broken Access Control | Moderate | Low | Provider-category isolation and deny-by-default tool routing |
| A02 Cryptographic Failures | High | Medium-Low | HTTPS-only outbound traffic; no secret persistence; secure secret injection |
| A03 Injection | High | Medium | Strict input validation for handles/query; no user-controlled URL composition |
| A04 Insecure Design | High | Medium | Explicit threat model, abuse controls, and fail-safe validation gates |
| A05 Security Misconfiguration | High | Medium | Env-only token policy, endpoint restrictions, startup config checks |
| A06 Vulnerable and Outdated Components | Moderate | Medium | Maintain patched HTTP/client dependencies and monitor advisories |
| A07 Identification and Authentication Failures | High | Medium-Low | Bearer token management, auth failure sanitization, no token logging |
| A08 Software and Data Integrity Failures | Moderate | Medium | Treat X responses as untrusted; sanitize before logs and downstream handling |
| A09 Security Logging and Monitoring Failures | High | Medium | Structured security events for validation rejects, auth failures, rate-limit events |
| A10 SSRF | Moderate | Medium-Low | Fixed provider host allow-list and prohibition on caller-supplied endpoints |

## Dependency and Content Injection Risk

X API responses are external and untrusted. Even when syntactically valid, content may contain manipulation attempts, malicious links, control characters, or misleading text. Primary downstream risk is not server-side code execution, but unsafe propagation into clients, logs, or follow-on agents.

Mitigation direction:

- Preserve content as plain text and avoid implicit rendering semantics.
- Sanitize content before logging.
- Validate and normalize URLs in response fields to approved schemes only.
- Document trust boundary so MCP consumers perform context-appropriate escaping and safety filtering.

## Audit and Logging

- Log security-relevant events: validation rejection, missing secret, auth failure, 429 events, cache hit/miss for throttling controls.
- Never log authorization headers, raw bearer token, or full upstream error payloads.
- Use structured fields with safe summaries instead of raw content where possible.
- Retention follows project runtime policy; if persisted in future, apply minimum-retention and least-privilege access.

## Security Test Cases

| ID | Scenario Type | Test Scenario | Expected Result | AC Mapping |
| --- | --- | --- | --- | --- |
| SEC-51-01 | Happy Path | Valid token, one valid handle | Sorted `SocialPost` results from X, no secret leaks | AC-6, AC-7, AC-8, AC-14 |
| SEC-51-02 | Happy Path | Valid token, repeated identical request | Second response served from cache with `cached_at`, reduced outbound calls | AC-19 |
| SEC-51-03 | Boundary | Handle length exactly 1 and 15 | Accepted and processed | AC-5 |
| SEC-51-04 | Boundary | Handle length 0 or 16 | Validation error, no X API call | AC-2, AC-5 |
| SEC-51-05 | Boundary | `max_results` = 1 and 100 | Accepted | AC-4 |
| SEC-51-06 | Boundary | `max_results` = 0 or 101 | Validation error, no X API call | AC-4 |
| SEC-51-07 | Boundary | `lookback_hours` above effective tier window | Request constrained and response includes effective-limit note | AC-20 |
| SEC-51-08 | Attack | Handle contains `/`, `?`, `%`, whitespace, or control chars | Rejected by validation | AC-2, AC-5 |
| SEC-51-09 | Attack | Query is whitespace-only | Validation error, no outbound call | AC-3 |
| SEC-51-10 | Attack | Missing `X_BEARER_TOKEN` | Structured config error; no token-like data in response/logs | AC-15 |
| SEC-51-11 | Attack | Upstream 401 includes token-like fragments in message body | Returned/logged message redacted; token not exposed | AC-16 |
| SEC-51-12 | Attack | Upstream 429 rate limit | Investor-friendly rate-limit error; no raw internals | AC-18 |
| SEC-51-13 | Attack | Burst requests attempt quota exhaustion | Throttle triggers and/or cache serves; no uncontrolled upstream spike | AC-18, AC-19 |
| SEC-51-14 | Attack | Crafted post content with script-like payload | Content treated as inert text; no execution or template interpolation | AC-17 |
| SEC-51-15 | Regression Security | `get_stock_data` invoked after social provider registration | No social provider invocation | AC-25 |
| SEC-51-16 | Regression Security | `list_providers` includes X and financial providers | Correct category labels, no secret fields | AC-22, AC-26 |

## Compliance Notes

- **PII Scope**: Feature does not intentionally collect new first-party PII; it returns public social content and metadata from X as provided by upstream API.
- **Data Minimization**: Return only fields required by `SocialPost` model.
- **Retention**:
  - In-memory cache retention is limited to rate-limit mitigation window.
  - No long-term persistence is required for this feature scope.
  - If persistence is introduced later, a separate compliance review is required before rollout.
- **Deletion**: Cached entries expire automatically by TTL; process restart clears in-memory cache.
- **Regulatory Posture**: No direct HIPAA/PCI processing in current scope. GDPR considerations remain limited to handling of externally sourced public content and operational logs.

## Design Decisions and Rationale

1. **Env-only token sourcing** reduces accidental secret disclosure risk versus config-file fallback.
2. **Strict input allow-lists** are preferred over blacklist filtering for handle/query controls.
3. **Cache plus throttle** is required to defend provider quota and preserve availability under repeated calls.
4. **Fixed HTTPS endpoints** and no caller-controlled hosts reduce SSRF and downgrade risk.
5. **Untrusted content boundary documentation** is required to prevent downstream unsafe rendering assumptions.
6. **Handle allow-list mode** is recommended for production environments with strict governance needs.

## Related Documents

- [Feature Spec: Issue #51 Social Media X Provider](../features/issue-51-social-media-x-provider.md)
- [Security Summary](security-summary.md)
- [Issue #32 Tier Handling Security](issue-32-tier-handling-security.md)
- [Provider Selection Architecture](../architecture/provider-selection-architecture.md)
- [Issue #32 Test Strategy](../testing/issue-32-tier-handling-test-strategy.md)
