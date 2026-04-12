# Feature: Social Media Data Sources — X/Twitter Provider

<!--
  Template owner: Product Manager
  Output directory: docs/features/
  Filename convention: issue-51-social-media-x-provider.md
  Related Issue: #51
-->

## Document Info

- **Status**: Draft
- **Last Updated**: 2026-04-12

## Overview

StockData.Net currently aggregates financial data and market events from structured financial data providers. This feature introduces a new **social media provider category**, starting with X (Twitter), enabling users to monitor financial commentary, breaking sentiment signals, and market-relevant posts from curated account handles and keyword/ticker searches. The new `get_social_feed` MCP tool exposes this capability alongside existing tools while establishing an extensible provider pattern for future social media sources.

## Problem Statement

AI assistants and investors using StockData.Net have access to structured financial data and macro-economic events, but no access to real-time social commentary that increasingly drives short-term market sentiment. Notable central bank communications, analyst commentary, and breaking macro signals routinely appear on X/Twitter before they surface in structured news endpoints.

The existing `get_market_events` tool (issue #26) explicitly scopes out social media signals. This leaves a gap:

- Users cannot monitor financial accounts on X (e.g., central bank feeds, prominent analysts) via the MCP server
- AI assistants cannot correlate unstructured social signals with financial data returned by other tools
- The provider architecture has no category for social media sources — only financial data providers exist today

**Open questions (to be resolved before implementation begins — see [Open Questions](#open-questions)):**

| Open Question | Impact |
| --- | --- |
| Which X API tier is required for meaningful use? | Determines whether free-tier MVP is viable or if paid access is a prerequisite |
| How is the monitored accounts list configured? | Affects whether handles are a per-call parameter, a config file, or environment-defined allow-list |
| What lookback window should be supported? | X API free tier restricts recent posts only; window definition affects usefulness |
| What is the rate limit mitigation strategy? | Free tier is very restrictive; caching, queuing, or graceful degradation approach must be decided |
| Is at least one handle OR one keyword required, or can both be omitted? | Affects default behaviour and potential for overly broad unfiltered queries |

## User Stories

### User Story 1: As a financial analyst, I want to retrieve recent posts from monitored X/Twitter account handles so that I can track market commentary from trusted financial sources

> 1.1 **Happy Path — Retrieve posts from a single handle**
>
> Given the MCP server is running with X API credentials configured as environment variables
> And the X API is reachable
> When I call `get_social_feed` with `handles: ["federalreserve"]` and no other filters
> Then I receive a list of `SocialPost` objects from that handle
> And each post includes `post_id`, `author_handle`, `content`, `posted_at` (UTC ISO 8601), `url`, and `source: "X"`
> And posts are sorted by `posted_at` descending (most recent first)
>
> 1.2 **Happy Path — Retrieve posts from multiple handles**
>
> Given the MCP server is running with X API credentials configured
> When I call `get_social_feed` with `handles: ["federalreserve", "ecb"]`
> Then I receive posts from both handles merged into a single list
> And posts are sorted by `posted_at` descending across both accounts
> And the `author_handle` field on each post identifies the originating account
>
> 1.3 **Edge Case — Handle has no posts within the lookback window**
>
> Given the MCP server is running with X API credentials configured
> And the requested handle has posted nothing in the lookback period
> When I call `get_social_feed` with that handle
> Then I receive an empty list `[]` with no error
> And the response includes a `message` field stating "No posts found for the requested handles and filters within the lookback window"
>
> 1.4 **Edge Case — Handle does not exist on X**
>
> Given I call `get_social_feed` with `handles: ["nonexistent_handle_xyz_12345"]`
> Then the tool returns a structured error
> And the error message states that the handle was not found on X
> And no other handles in the same request are affected (per-handle error isolation)
>
> 1.5 **Error Scenario — X API credentials are missing**
>
> Given X API credentials are not set in the environment
> When I call `get_social_feed`
> Then the tool returns a structured, investor-friendly error message
> And the error message states that X API credentials are not configured
> And no credential values or partial secret data appear in the error message or logs

### User Story 2: As an investor, I want to filter X/Twitter posts by ticker symbol or keyword so that I can focus on social commentary relevant to specific securities or themes

> 2.1 **Happy Path — Filter by ticker symbol**
>
> Given the MCP server is running with X API credentials configured
> When I call `get_social_feed` with `query: "$AAPL"`
> Then I receive posts containing the ticker cashtag `$AAPL`
> And posts not mentioning `$AAPL` are excluded from the response
> And the `matched_keywords` field on each post includes `"$AAPL"`
>
> 2.2 **Happy Path — Filter by keyword**
>
> Given the MCP server is running with X API credentials configured
> When I call `get_social_feed` with `query: "interest rate"`
> Then I receive posts containing the phrase "interest rate"
> And the `matched_keywords` field on each post includes `"interest rate"`
>
> 2.3 **Happy Path — Combined handle and keyword filter**
>
> Given I call `get_social_feed` with `handles: ["federalreserve"]` and `query: "inflation"`
> Then I receive only posts from the `federalreserve` handle that also contain the keyword "inflation"
> And posts from that handle not mentioning "inflation" are excluded
>
> 2.4 **Edge Case — No posts match the keyword filter**
>
> Given I call `get_social_feed` with `query: "quantitative_easing_xyz_unlikely_phrase"`
> Then I receive an empty list `[]` with no error
> And the response includes a `message` field explaining no posts matched the query
>
> 2.5 **Error Scenario — Query string is empty after trimming whitespace**
>
> Given I call `get_social_feed` with `query: "   "` (whitespace only)
> Then the tool returns a structured validation error
> And the error message states that `query` must not be blank if provided
> And the X API is not called

### User Story 3: As a developer, I want X API rate limits to be handled transparently so that tool calls degrade gracefully when free-tier limits are reached

> 3.1 **Happy Path — Request completes within rate limit budget**
>
> Given the X API rate limit has not been exhausted for the current window
> When I call `get_social_feed`
> Then I receive a normal response with posts
> And no rate limit information is surfaced to the user
>
> 3.2 **Edge Case — X API rate limit is reached mid-request window**
>
> Given the X API free tier rate limit has been exhausted for the current 15-minute window
> When I call `get_social_feed`
> Then the tool returns a structured, investor-friendly error message
> And the message states that the X API rate limit has been reached and suggests when it will reset (if the API provides reset time)
> And no stack trace or HTTP status code is exposed in the user-facing message
>
> 3.3 **Edge Case — Cached response is served within the rate limit window**
>
> Given a previous identical `get_social_feed` request was made within the same rate limit window
> And the response was cached
> When the same request is made again
> Then the cached response is returned without calling the X API
> And the response clearly indicates it may reflect data from the earlier call (via `cached_at` timestamp or equivalent)
>
> 3.4 **Error Scenario — X API returns an unexpected error (5xx)**
>
> Given the X API returns a 503 Service Unavailable response
> When I call `get_social_feed`
> Then the tool returns a structured, investor-friendly error message
> And the message does not expose internal HTTP details
> And the error is logged server-side for diagnostic purposes

### User Story 4: As a developer building on StockData.Net, I want the social media provider architecture to be extensible so that new platforms (such as Reddit) can be added in future iterations without modifying core tool logic

> 4.1 **Provider interface contract — X provider implements it**
>
> Given the `ISocialMediaProvider` interface is defined
> When the X provider class implements `ISocialMediaProvider`
> Then it correctly maps X API v2 tweet objects to `SocialPost` objects
> And the mapping is verified by unit tests
>
> 4.2 **New provider can be added without changing tool core**
>
> Given a hypothetical second provider (e.g., Reddit) implements `ISocialMediaProvider`
> When it is registered in the dependency injection container
> Then `get_social_feed` can route to it via the provider selection mechanism without any modification to the tool handler
>
> 4.3 **Provider failure does not block the tool response**
>
> Given the X provider is configured and experiences an unrecoverable error
> And no other social media provider is configured
> When I call `get_social_feed`
> Then the tool returns a structured error message describing the provider failure
> And the existing resilience infrastructure (circuit breaker / retry) is applied to X API calls
>
> 4.4 **Provider selection routes social media requests to social media providers only**
>
> Given both a financial data provider (Yahoo Finance) and the X social media provider are configured
> When I call `get_social_feed`
> Then only the X provider (and other registered `ISocialMediaProvider` implementations) are invoked
> And financial data providers are not called
> And calling `get_stock_data` continues to route only to financial data providers (no regression)

### User Story 5: As a platform administrator, I want X API credentials to be stored exclusively in environment variables so that no secrets are ever embedded in source code, configuration files, or tool responses

> 5.1 **Happy Path — Credentials loaded from environment at startup**
>
> Given the environment variable `X_BEARER_TOKEN` is set to a valid Bearer Token value
> When the MCP server starts
> Then the X provider is initialized successfully using that token
> And the token value does not appear in any log file, startup output, or configuration dump
>
> 5.2 **Security — Credential never appears in API response or error**
>
> Given the X API returns an authentication failure (401)
> When I call `get_social_feed`
> Then the error message returned to the user does not contain any portion of the Bearer Token
> And the server logs the error without including the credential value
>
> 5.3 **Audit — Source code contains no hardcoded secrets**
>
> Given the repository is scanned with a secrets-detection tool (e.g., `truffleHog` or `git-secrets`)
> Then no X API keys, Bearer Tokens, or OAuth secrets are found in any committed file
> And the `.gitignore` and secret scanner configuration exclude `.env` and secrets files
>
> 5.4 **Error Scenario — Environment variable is set but contains an invalid token**
>
> Given `X_BEARER_TOKEN` is set to a malformed or expired value
> When I call `get_social_feed`
> Then the tool returns a structured error stating X API authentication failed
> And the message instructs the administrator to verify the configured credentials
> And the invalid token value is not echoed back in the error

## Data Model

### `SocialPost` Response Object

Each post returned by `get_social_feed` conforms to the following model:

| Field | Type | Nullable | Description |
| --- | --- | --- | --- |
| `post_id` | `string` | No | Unique identifier for the post as assigned by the source platform (e.g., X tweet ID) |
| `source` | `string` | No | Social media platform that provided this post (e.g., `"X"`) |
| `author_handle` | `string` | No | Account handle of the post author, without the `@` prefix (e.g., `"federalreserve"`) |
| `content` | `string` | No | Full text content of the post |
| `posted_at` | `string` | No | UTC timestamp in ISO 8601 format (e.g., `"2026-04-12T14:30:00Z"`) |
| `url` | `string` | No | Direct URL to the post on the source platform |
| `matched_keywords` | `string[]` | No | List of query keywords or cashtags matched in this post. Empty array `[]` if no keyword filter was applied. |
| `cached_at` | `string` | Yes | UTC timestamp indicating when this result was cached, if served from cache. `null` if fetched live. |

### `get_social_feed` Tool Parameters

| Parameter | Type | Required | Default | Valid Values / Constraints |
| --- | --- | --- | --- | --- |
| `handles` | `string[]` | No | `[]` (empty) | Array of X account handles (without `@`). At least one of `handles` or `query` must be provided. |
| `query` | `string` | No | `null` | Keyword or cashtag filter (e.g., `"$AAPL"`, `"interest rate"`). Non-blank string if provided. At least one of `handles` or `query` must be provided. |
| `max_results` | `integer` | No | `10` | 1–100. Subject to X API tier limits. |
| `lookback_hours` | `integer` | No | `24` | 1–168 (7 days). Actual available history depends on X API tier. |
| `provider` | `string` | No | `null` (auto-select) | Optional override to select a specific social media provider (e.g., `"X"`). When omitted, the configured default social media provider is used. |

**Constraint**: At least one of `handles` or `query` must be non-empty. Calling `get_social_feed` with neither returns a validation error.

## Requirements

### Functional Requirements

1. The system shall expose a new MCP tool named `get_social_feed` alongside the existing `get_stock_data`, `get_market_events`, and `list_providers` tools.
2. The tool shall accept optional parameters: `handles`, `query`, `max_results`, `lookback_hours`, and `provider`.
3. At least one of `handles` or `query` must be provided; the tool shall return a validation error if both are absent or blank.
4. When `handles` is provided, the tool shall retrieve recent posts from each specified account handle within the configured lookback window.
5. When `query` is provided, the tool shall filter results to posts matching the specified keyword or cashtag.
6. When both `handles` and `query` are provided, they shall be applied conjunctively: only posts from the specified handles that also match the keyword/query are returned.
7. Results shall be sorted by `posted_at` descending (most recent first).
8. Each returned post shall conform to the `SocialPost` data model defined in this specification.
9. The X (Twitter) provider shall authenticate using OAuth 2.0 Bearer Token read exclusively from the `X_BEARER_TOKEN` environment variable.
10. The system shall define an `ISocialMediaProvider` interface that the X provider implements, enabling future provider additions without modifying the `get_social_feed` tool handler.
11. The provider selection architecture shall introduce a `SocialMedia` provider category, separate from the existing `FinancialData` category.
12. The `list_providers` tool shall be updated to display registered social media providers alongside financial data providers, with their category clearly indicated.
13. When a specified handle does not exist on X, the tool shall return a per-handle error while continuing to return results for valid handles in the same request.
14. When the X API rate limit is exhausted, the tool shall return a structured investor-friendly error message; it shall not expose HTTP status codes or internal error details.
15. When neither `handles` nor `query` is provided, the tool shall return a validation error without calling the X API.
16. When `query` is a blank or whitespace-only string, the tool shall return a validation error without calling the X API.
17. The tool shall apply the existing resilience infrastructure (circuit breaker, retry) to all X API calls.
18. The system shall cache responses within the X API rate limit window to reduce API call consumption; cached responses shall include a `cached_at` timestamp in the response.
19. The `get_stock_data` and `get_market_events` tools shall continue to operate without regression after provider category changes.

### Non-Functional Requirements

- **Latency**: Responses for `get_social_feed` must complete within ≤5 seconds under normal network conditions when serving live data from the X API.
- **Rate Limit Respect**: The system must not exceed X API rate limits for the configured tier. Any in-process caching or queuing strategy must prevent burst requests from triggering API bans.
- **Security — Secrets**: The `X_BEARER_TOKEN` must never appear in log output, API responses, error messages, or source code. Credentials must be loaded at startup from environment variables only.
- **Security — Output Sanitization**: Post content returned from X must be treated as untrusted user-supplied data. It must not be interpreted as executable content or injected into downstream processing unsanitized.
- **Extensibility**: Adding a new `ISocialMediaProvider` implementation must not require changes to the `get_social_feed` tool handler or the provider selection core.
- **Backwards Compatibility**: All existing MCP tools (`get_stock_data`, `get_market_events`, `list_providers`) must continue to function identically after this feature is integrated.
- **Tier Transparency**: When the configured X API tier limits the lookback window or `max_results` below what was requested, the response should include a note indicating the effective limit applied.

## Acceptance Criteria

### Category A: Tool Registration and Parameter Validation

- [ ] **[Blocking]** AC-1: `get_social_feed` is registered as a new MCP tool and is discoverable
  - **Evidence**: Tool discovery (e.g., via `list_providers` or MCP client enumeration) returns `get_social_feed` in the tool list; tool is callable from a connected MCP client

- [ ] **[Blocking]** AC-2: Calling `get_social_feed` with neither `handles` nor `query` returns a validation error without calling the X API
  - **Evidence**: Unit test confirms the X API client is never invoked when both parameters are absent; error message is investor-friendly

- [ ] **[Blocking]** AC-3: Calling `get_social_feed` with a blank `query` (whitespace only) returns a validation error without calling the X API
  - **Evidence**: Unit test confirms validation fires before provider dispatch; error message states `query` must not be blank if provided

- [ ] **[Blocking]** AC-4: `max_results` must be between 1 and 100; values outside this range return a validation error
  - **Evidence**: Unit tests for `max_results: 0` and `max_results: 101` each return structured validation errors without calling the X API

- [ ] **[Non-blocking]** AC-5: `handles` entries are sanitized — leading `@` symbols are stripped before dispatch to the X API
  - **Evidence**: Unit test confirms `handles: ["@federalreserve"]` is normalized to `"federalreserve"` before the API call

### Category B: X/Twitter Post Retrieval

- [ ] **[Blocking]** AC-6: Posts retrieved from the X API are returned as `SocialPost` objects with all non-nullable fields populated
  - **Evidence**: Integration test calling `get_social_feed` with a known active handle returns at least one post with `post_id`, `author_handle`, `content`, `posted_at`, `url`, and `source: "X"` all non-null

- [ ] **[Blocking]** AC-7: `posted_at` is returned in UTC ISO 8601 format
  - **Evidence**: Unit test for X provider mapping confirms `posted_at` is normalized to UTC and ends in `Z`

- [ ] **[Blocking]** AC-8: Posts are sorted by `posted_at` descending (most recent first)
  - **Evidence**: Unit test with mock multi-post X API response confirms output ordering

- [ ] **[Blocking]** AC-9: When a handle does not exist on X, a per-handle error is returned without terminating results for other valid handles in the same request
  - **Evidence**: Unit test with two handles — one valid, one nonexistent — confirms one result set and one per-handle error in the response

- [ ] **[Non-blocking]** AC-10: When `handles` returns no posts within the lookback window, an empty list `[]` and informative `message` are returned
  - **Evidence**: Unit test with a mocked X API response of zero results for the lookback period returns `[]` with the correct message

### Category C: Keyword and Ticker Filtering

- [ ] **[Blocking]** AC-11: When `query` is provided, only posts matching the keyword or cashtag are returned
  - **Evidence**: Integration or unit test confirms posts not containing the query term are excluded from the response

- [ ] **[Blocking]** AC-12: When both `handles` and `query` are provided, results satisfy both conditions (AND logic)
  - **Evidence**: Unit test with mocked posts — some from the handle but not matching the keyword, some matching the keyword but from other handles — confirms only the intersection is returned

- [ ] **[Non-blocking]** AC-13: Each returned `SocialPost` includes a `matched_keywords` array listing the query terms that matched
  - **Evidence**: Unit test confirms `matched_keywords` is populated when a `query` is supplied, and is `[]` when no query filter is applied

### Category D: Authentication and Security

- [ ] **[Blocking]** AC-14: The X provider reads the Bearer Token exclusively from the `X_BEARER_TOKEN` environment variable; no token value appears in any source file
  - **Evidence**: Secrets scanner (e.g., `truffleHog`) finds no hardcoded tokens; code review confirms environment variable lookup at startup

- [ ] **[Blocking]** AC-15: When `X_BEARER_TOKEN` is not set, `get_social_feed` returns a structured error stating credentials are not configured; no partial credential data appears in the message
  - **Evidence**: Unit test with empty environment returns the correct error; log output is reviewed to confirm no token leakage

- [ ] **[Blocking]** AC-16: When the X API returns a 401 Unauthorized response, the error returned to the user does not contain the Bearer Token value or any portion thereof
  - **Evidence**: Unit test mocking a 401 response confirms the error message has no credential content; server log is checked for token-free output

- [ ] **[Blocking]** AC-17: Post content returned from X is treated as untrusted data and is not processed or interpreted before inclusion in the `SocialPost.content` field
  - **Evidence**: Code review confirms no eval, template rendering, or HTML processing is applied to post content before serialization

### Category E: Rate Limit Handling

- [ ] **[Blocking]** AC-18: When the X API returns a 429 Too Many Requests response, the tool returns a structured investor-friendly error message indicating the rate limit has been reached
  - **Evidence**: Unit test mocking a 429 response confirms the error message is plain-language with no HTTP status code; reset time is included if available from the API response headers

- [ ] **[Non-blocking]** AC-19: Cached responses are served within the X API rate limit window to reduce API consumption
  - **Evidence**: Integration test (or unit test with mock cache) confirms a second identical request within the cache window returns `cached_at` in the response without calling the X API

- [ ] **[Non-blocking]** AC-20: When the requested `lookback_hours` exceeds the X API tier's historical limit, the response includes a note indicating the effective window applied
  - **Evidence**: Unit test with `lookback_hours: 168` and a free-tier mock confirms a `message` or `warning` field indicates the applied lookback was reduced

### Category F: Provider Architecture and Extensibility

- [ ] **[Blocking]** AC-21: `ISocialMediaProvider` interface is defined and implemented by the X provider
  - **Evidence**: Code review confirms the interface exists with at minimum a `GetPostsAsync(SocialFeedQuery query)` method; the X provider implements it

- [ ] **[Blocking]** AC-22: The provider selection architecture recognizes a `SocialMedia` provider category distinct from `FinancialData`
  - **Evidence**: Code review confirms provider category enum or discrimination is updated; `list_providers` output groups and labels providers by category

- [ ] **[Non-blocking]** AC-23: A new `ISocialMediaProvider` implementation can be registered in DI without modifying the `get_social_feed` tool handler
  - **Evidence**: Code review confirms the tool handler iterates `IEnumerable<ISocialMediaProvider>` from dependency injection

### Category G: Regression — Existing Tools

- [ ] **[Blocking]** AC-24: All existing unit and integration tests pass without modification after this feature is integrated
  - **Evidence**: CI build runs full test suite; zero new failures attributable to provider category or selection changes

- [ ] **[Blocking]** AC-25: `get_stock_data` continues to route only to financial data providers after the `SocialMedia` category is added
  - **Evidence**: Unit test confirms `get_stock_data` invokes no `ISocialMediaProvider` implementations

- [ ] **[Blocking]** AC-26: `list_providers` returns existing financial providers alongside the new X social media provider, each with correct category labels
  - **Evidence**: Integration test confirms `list_providers` output includes both Yahoo Finance (category: `FinancialData`) and X (category: `SocialMedia`), with no duplicate or missing entries

## Out of Scope

- **Facebook**: Facebook is explicitly excluded from this and all future social media provider additions per product decision. No Facebook API integration shall be designed or implemented.
- **Mastodon**: Out of scope for this issue; may be considered in a future social media provider iteration.
- **Reddit**: Out of scope for this issue; the `ISocialMediaProvider` interface is designed to support it, but no Reddit implementation is delivered here.
- **LinkedIn**: Out of scope for this issue and not currently planned.
- **Sentiment scoring of social posts**: Raw post content is returned as-is; no NLP sentiment analysis is applied in this feature. Sentiment signals from social data may be explored in a separate enhancement.
- **Real-time streaming / WebSockets**: `get_social_feed` is a request/response tool only. Push-based delivery of new posts is not in scope.
- **Post engagement metrics**: Likes, reposts, view counts, and other engagement data are not included in the `SocialPost` model in this iteration.
- **Proactive alerting on social signals**: No threshold-based alert or notification system is included; users must invoke the tool explicitly.
- **X account authentication (user-delegated OAuth 1.0a / OAuth 2.0 PKCE)**: Only app-level Bearer Token authentication (OAuth 2.0 Client Credentials) is supported. Reading protected or private accounts is out of scope.
- **Posting or interacting with X**: The provider is read-only. No write operations (posting, liking, replying) are permitted or implemented.

## Open Questions

The following questions must be resolved before implementation begins. The architecture team should address them in the issue #51 architecture document.

| # | Question | Why It Matters | Owner |
| --- | --- | --- | --- |
| OQ-1 | Which X API tier (Free, Basic, Pro) is required for meaningful production use? | Free tier limits to ~1 post/month per user timeline and ~500k tweets/month read. Basic tier ($100/month) provides ~10k posts/month. This directly determines whether an MVP is viable without paid access. | Product Owner / Architecture |
| OQ-2 | How is the monitored accounts list configured — per-call parameter, `appsettings.json` allow-list, or environment variable? | An environment-defined allow-list prevents arbitrary account scraping but limits flexibility. A per-call parameter is flexible but requires caller validation. | Architecture / Security |
| OQ-3 | What is the supported lookback window, and how should requests exceeding the API tier's limit be handled? | X free tier restricts to very recent posts. If a user requests 7 days but the tier only supports 24 hours, the system must degrade gracefully and communicate the constraint clearly. | Architecture |
| OQ-4 | What is the rate limit mitigation strategy — in-process cache, a Redis cache, a request queue, or simple error exposure? | The chosen approach affects infrastructure requirements. In-process caching is simpler but lost on restart. Redis requires infrastructure. The strategy must align with issue #32 tier handling patterns. | Architecture / DevOps |
| OQ-5 | Is an allow-list of permitted handles required, or can any public X handle be queried? | An unrestricted handle parameter could be misused to scrape arbitrary accounts at scale, creating legal and rate-limit exposure. An allow-list in configuration limits this risk. | Security / Product Owner |

## Dependencies

### Depends On

- **Provider Selection Architecture** ([docs/architecture/provider-selection-architecture.md](../architecture/provider-selection-architecture.md)): The `SocialMedia` provider category must be added to the provider type taxonomy. The `ProviderSelectionService` must be updated to route `get_social_feed` requests to `ISocialMediaProvider` implementations only.
- **Issue #17 (MCP Error Handling)** ([docs/features/issue-17-mcp-error-handling.md](issue-17-mcp-error-handling.md)): Investor-friendly error formatting and per-provider failure reporting patterns apply directly to X API error handling (401, 429, 5xx).
- **Issue #32 (Provider Free/Paid Tier Handling)** ([docs/features/issue-32-provider-free-paid-tier-handling.md](issue-32-provider-free-paid-tier-handling.md)): Rate limit awareness, tier-based capability gating, and graceful degradation patterns established in issue #32 must be applied to the X API's tiered access model.
- **Issue #26 (Market-Moving Events)** ([docs/features/issue-26-market-moving-events.md](issue-26-market-moving-events.md)): Confirms explicit scope boundary — `get_market_events` excludes social signals, and `get_social_feed` does not overlap with macro event categories. Both tools coexist without ambiguity.

### Blocks

- **Future Issue: Reddit Social Media Provider** — The `ISocialMediaProvider` interface and `SocialMedia` category established here are prerequisites for any future Reddit or other social media provider integration.
- **Future Issue: Social Sentiment Aggregation** — Any feature that aggregates or scores sentiment across social posts depended on the `SocialPost` data model and provider interface defined here.

### Related Issues

- **Issue #17** (MCP Error Handling): Error handling patterns required for production-grade X API error exposure
- **Issue #26** (Market-Moving Events): Confirms social signals are explicitly excluded from `get_market_events`, establishing the boundary this feature fills
- **Issue #32** (Provider Tier Handling): Rate-limit and tier handling patterns used directly by the X API integration

## Technical Considerations

1. **X API v2 Authentication**: The X API v2 uses OAuth 2.0 Bearer Token for app-only read access to public posts. The token must be passed as an `Authorization: Bearer <token>` HTTP header. The `HttpClient` used by the X provider must be configured with this header at the factory level — not injected per-request — to avoid token exposure in request logs.

2. **Provider Category Taxonomy**: The existing `ProviderSelectionService` and related configuration must be extended to support a `SocialMedia` provider category. The architecture team must define whether this is an enum extension, a new registration attribute, or a separate DI registration group. Category isolation must be validated to prevent cross-category routing.

3. **ISocialMediaProvider Interface Design**: The interface signature should accept a `SocialFeedQuery` parameter object (handles, query, max_results, lookback_hours) and return `IReadOnlyList<SocialPost>`. Using a query object rather than positional parameters makes the interface stable when new optional fields are introduced for future providers.

4. **X API Rate Limit Headers**: The X API v2 returns `x-rate-limit-limit`, `x-rate-limit-remaining`, and `x-rate-limit-reset` headers. The provider should read and expose these to the resilience layer so the circuit breaker or cache layer can make informed decisions rather than relying on 429 responses.

5. **Handle Allow-List Security**: If a configurable allow-list of permitted handles is adopted (see OQ-5), the allow-list must be validated at startup so that misconfiguration (empty allow-list) fails fast rather than silently permitting all queries. The allow-list must not be logged in its entirety to avoid enumeration of monitored accounts.

6. **Lookback and Tier Constraints**: The X API free tier does not support timeline lookback beyond very recent posts and has severely restricted read quotas. The architecture team must specify whether the tool advertises the configured tier's constraints at startup (e.g., in `list_providers` metadata) or only surfaces them reactively when requests exceed tier limits.

7. **Content as Untrusted Data**: Post content from X must be treated as unvalidated external data. It must not be interpolated into log messages using format strings (avoid log injection), and must not be passed through any template engine or HTML renderer without escaping.
