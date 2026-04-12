# Feature: Market-Moving Events Feed

<!--
  Template owner: Product Manager
  Output directory: docs/features/
  Filename convention: issue-26-market-moving-events.md
  Related Issue: #26
-->

## Document Info

- **Status**: Draft
- **Last Updated**: 2026-04-12

## Overview

The StockData.Net MCP server currently provides financial data and news but lacks structured access to market-moving events such as Federal Reserve rate decisions, Treasury announcements, geopolitical developments, and major regulatory actions. This feature adds a new MCP tool — `get_market_events` — that surfaces both scheduled macro-economic events and breaking high-impact announcements, sourced from already-integrated providers (Finnhub, AlphaVantage) with a provider interface extensible to future sources.

## Problem Statement

Investors and AI assistants using the StockData.Net MCP server have no way to query structured market-moving event data beyond unfiltered news articles. The existing `get_market_news` tool returns raw news without categorization, impact scoring, or planned/unplanned classification. This leaves users unable to:

- Know when Fed rate decisions, FOMC meetings, or Treasury announcements are scheduled
- Receive timely signals for breaking political or geopolitical events with direct market impact
- Filter events by category (e.g., "only central bank") or by impact level (e.g., "only high-impact")
- Understand which markets or asset classes a given event is expected to affect

**Resolved open questions (captured as explicit decisions):**

| Open Question | Decision |
|---|---|
| What data sources are available? | Finnhub `/calendar/economic` (scheduled events, free tier) + Finnhub news with category filter (breaking events); AlphaVantage news sentiment API with macro topic filter. Both providers are already integrated. |
| Planned vs. unplanned distinction | `event_type` field: `"scheduled"` (from economic calendar) or `"breaking"` (from news endpoints, filtered by impact signals). |
| Event categorization | `category` enum: `"fed"`, `"treasury"`, `"geopolitical"`, `"regulatory"`, `"central_bank"`, `"institutional"`, `"all"`. Mapped from provider-specific tags at ingestion. |
| Latency requirement | Scheduled events: next 7–30 days acceptable; breaking events: near-real-time (≤60 s lag from publication accepted). |
| New tool or extend existing? | New MCP tool `get_market_events` — not bundled with `get_market_news`. Keeps financial news and macro events semantically separate. |
| API key / rate limit / cost | Finnhub free tier includes `/calendar/economic` and news API. AlphaVantage free tier includes NEWS_SENTIMENT with topic filters. No additional API keys or cost required for MVP. |

## User Stories

### User Story 1: As an investor, I want to retrieve upcoming scheduled market events so that I can plan my trading decisions around known announcements

> 1.1 **Happy Path — Fetch next 7 days of scheduled events, no filters**
>
> Given the MCP server is running with Finnhub configured
> When I call `get_market_events` with `from_date` set to today and `to_date` set to 7 days from now
> Then I receive a list of `MarketEvent` objects where `event_type` is `"scheduled"`
> And each event includes `event_id`, `title`, `description`, `category`, `impact_level`, `event_time` (UTC ISO 8601), `source`, `source_url`, and `affected_markets`
> And events are sorted by `event_time` ascending
>
> 1.2 **Happy Path — Fetch 30-day window of scheduled events**
>
> Given the MCP server is running with Finnhub configured
> When I call `get_market_events` with `from_date` set to today and `to_date` set to 30 days from now
> Then I receive all scheduled events in that window
> And the response includes events from the Finnhub `/calendar/economic` endpoint
> And each event's `source` field is set to `"Finnhub"`
>
> 1.3 **Edge Case — No events in requested date range**
>
> Given the MCP server is running with Finnhub configured
> When I call `get_market_events` with a date range that contains no scheduled events (e.g., a public holiday weekend)
> Then I receive an empty list `[]` with no error
> And the response includes a `message` field stating "No market events found for the requested date range and filters"
>
> 1.4 **Error Scenario — Invalid date range (from_date after to_date)**
>
> Given I call `get_market_events` with `from_date` set to 2026-04-20 and `to_date` set to 2026-04-10
> Then the tool returns a structured validation error
> And the error message states "from_date must be earlier than or equal to to_date"
> And no provider is called

### User Story 2: As an investor, I want to retrieve recent breaking high-impact market events so that I am informed of unplanned announcements that could affect my portfolio

> 2.1 **Happy Path — Fetch recent breaking events with no filters**
>
> Given the MCP server is running with Finnhub and AlphaVantage configured
> When I call `get_market_events` with `from_date` set to 24 hours ago and no other filters
> Then I receive a list of `MarketEvent` objects where `event_type` is `"breaking"`
> And events are sourced from the Finnhub news endpoint and/or AlphaVantage NEWS_SENTIMENT API
> And events are sorted by `event_time` descending (most recent first)
>
> 2.2 **Happy Path — Breaking events deduplicated across providers**
>
> Given the same event (e.g., a Fed statement) appears in both Finnhub and AlphaVantage responses
> When I call `get_market_events`
> Then the event appears only once in the output
> And the `source` field reflects the provider that was used (the first to return it, per provider priority order)
> And the existing deduplication logic (as used by `get_market_news`) is applied
>
> 2.3 **Edge Case — Breaking event with no sentiment score available**
>
> Given a breaking event is returned by Finnhub that has no sentiment data
> When I call `get_market_events`
> Then the event is still included in the response
> And the `sentiment` field is `null` (not omitted, not defaulted)
>
> 2.4 **Error Scenario — Provider returns 500 error for news endpoint**
>
> Given Finnhub returns a 500 server error on the news endpoint
> And AlphaVantage is configured as a fallback
> When I call `get_market_events` requesting breaking events
> Then the system falls back to AlphaVantage silently
> And I receive breaking events from AlphaVantage
> And the `source` field reflects `"AlphaVantage"` for returned events

### User Story 3: As an investor, I want to filter market events by category so that I can focus on the announcement types most relevant to my investment thesis

> 3.1 **Happy Path — Filter by "fed" category**
>
> Given the MCP server has events for multiple categories in the requested date range
> When I call `get_market_events` with `category` set to `"fed"`
> Then I receive only events categorized as `"fed"` (Fed rate decisions, FOMC minutes, Fed chair statements)
> And events from other categories (e.g., `"geopolitical"`, `"treasury"`) are excluded from the response
>
> 3.2 **Happy Path — Filter by "central_bank" category (non-Fed)**
>
> Given I call `get_market_events` with `category` set to `"central_bank"` for a date range containing ECB and BOJ announcements
> When the tool processes the request
> Then I receive events from the ECB, BOJ, and other non-Fed central banks
> And Fed events are not included (they belong to the `"fed"` category)
>
> 3.3 **Happy Path — No category filter (defaults to "all")**
>
> Given I call `get_market_events` without specifying a `category` parameter
> Then the system defaults to `category: "all"`
> And I receive events from all categories, each with its `category` field populated
>
> 3.4 **Edge Case — Category filter returns no results**
>
> Given I call `get_market_events` with `category` set to `"regulatory"` for a date range containing no SEC/CFTC announcements
> Then I receive an empty list `[]`
> And the response includes a `message` field explaining no events matched the filter
>
> 3.5 **Error Scenario — Invalid category value**
>
> Given I call `get_market_events` with `category` set to `"earnings"` (not a valid category)
> Then the tool returns a structured validation error
> And the error message lists the valid category values: `"fed"`, `"treasury"`, `"geopolitical"`, `"regulatory"`, `"central_bank"`, `"institutional"`, `"all"`

### User Story 4: As an investor, I want to filter market events by impact level so that I can prioritize the most market-significant announcements and avoid noise

> 4.1 **Happy Path — Filter by "high" impact level**
>
> Given I call `get_market_events` with `impact_level` set to `"high"`
> Then I receive only events where `impact_level` equals `"high"`
> And medium- and low-impact events are excluded
>
> 4.2 **Happy Path — Combined category and impact_level filters**
>
> Given I call `get_market_events` with `category` set to `"fed"` and `impact_level` set to `"high"`
> Then I receive only Fed events with high impact level
> And all other category/impact combinations are excluded
>
> 4.3 **Happy Path — No impact_level filter (defaults to "all")**
>
> Given I call `get_market_events` without specifying `impact_level`
> Then the system defaults to `impact_level: "all"`
> And I receive events of all impact levels, each with their `impact_level` field populated
>
> 4.4 **Edge Case — impact_level filter applied but provider does not supply impact data**
>
> Given AlphaVantage returns an event without an impact rating
> When I call `get_market_events` with `impact_level` set to `"high"`
> Then the event without impact data is excluded from the filtered results (cannot be confirmed as high-impact)
> And when called with `impact_level: "all"`, the event is included with `impact_level` set to `null`
>
> 4.5 **Error Scenario — Invalid impact_level value**
>
> Given I call `get_market_events` with `impact_level` set to `"critical"` (not a valid value)
> Then the tool returns a structured validation error
> And the error message lists valid values: `"high"`, `"medium"`, `"low"`, `"all"`

### User Story 5: As an AI assistant user, I want event results to be deduplicated across providers so that I receive clean, non-redundant information without manual filtering

> 5.1 **Happy Path — Same event from two providers, one result returned**
>
> Given both Finnhub and AlphaVantage return data about the same Fed rate decision announcement
> When I call `get_market_events`
> Then the event appears exactly once in the response
> And the deduplication is applied using the same logic as used by `get_market_news` (title similarity + event_time proximity within ±1 hour)
>
> 5.2 **Happy Path — Similar but distinct events not merged**
>
> Given Finnhub returns an FOMC rate decision event and AlphaVantage returns an FOMC meeting minutes event (same date, different event)
> When I call `get_market_events`
> Then both events are returned as separate entries
> And they are not merged despite sharing a date and category
>
> 5.3 **Edge Case — Single provider configured, no deduplication needed**
>
> Given only Finnhub is configured with no other providers
> When I call `get_market_events`
> Then events are returned without deduplication processing overhead
> And all events from Finnhub are returned as-is
>
> 5.4 **Edge Case — All providers return duplicate of same event**
>
> Given three providers all return the same breaking geopolitical event
> When I call `get_market_events`
> Then the event appears exactly once in the response
> And performance is not degraded (deduplication completes in <200ms regardless of provider count)

### User Story 6: As a developer integrating future event sources, I want the market events provider interface to be extensible so that new data sources can be added without modifying the core tool logic

> 6.1 **Provider interface contract — Finnhub implements it**
>
> Given the `IMarketEventsProvider` interface is defined
> When the Finnhub provider class implements `IMarketEventsProvider`
> Then it correctly maps Finnhub economic calendar data to `MarketEvent` objects using the defined `event_type`, `category`, and `impact_level` fields
> And the mapping is verified by unit tests
>
> 6.2 **Provider interface contract — AlphaVantage implements it**
>
> Given the `IMarketEventsProvider` interface is defined
> When the AlphaVantage provider class implements `IMarketEventsProvider`
> Then it correctly maps AlphaVantage NEWS_SENTIMENT results (filtered by macro topics) to `MarketEvent` objects
> And events sourced from AlphaVantage have `event_type: "breaking"`
> And the mapping is verified by unit tests
>
> 6.3 **New provider can be added without changing tool core**
>
> Given a hypothetical third provider (e.g., Polygon.io) implements `IMarketEventsProvider`
> When it is registered in the dependency injection container and appsettings.json
> Then `get_market_events` automatically includes it as a source without any modification to the tool handler
>
> 6.4 **Provider failure does not block other providers**
>
> Given Finnhub is configured as the primary events provider and fails with a network timeout
> And AlphaVantage is the secondary provider
> When I call `get_market_events`
> Then the system applies circuit breaker / resilience logic (as used by existing tools)
> And AlphaVantage results are returned without exposing the Finnhub failure to the user

## Data Model

### `MarketEvent` Response Object

Each event returned by `get_market_events` conforms to the following model:

| Field | Type | Nullable | Description |
|---|---|---|---|
| `event_id` | `string` | No | Unique identifier for the event (provider-scoped, e.g., `"finnhub-eco-12345"`) |
| `title` | `string` | No | Short descriptive title of the event (e.g., "FOMC Rate Decision") |
| `description` | `string` | Yes | Longer explanation of the event; may be `null` for calendar stubs |
| `event_type` | `string` | No | One of: `"scheduled"` (economic calendar) or `"breaking"` (news-derived) |
| `category` | `string` | No | One of: `"fed"`, `"treasury"`, `"geopolitical"`, `"regulatory"`, `"central_bank"`, `"institutional"` |
| `impact_level` | `string` | Yes | One of: `"high"`, `"medium"`, `"low"`. `null` if not rated by source. |
| `event_time` | `string` | No | UTC timestamp in ISO 8601 format (e.g., `"2026-04-15T18:00:00Z"`) |
| `source` | `string` | No | Provider that returned this event (e.g., `"Finnhub"`, `"AlphaVantage"`) |
| `source_url` | `string` | Yes | Direct URL to the source article or announcement; `null` for calendar entries without links |
| `affected_markets` | `string[]` | No | List of affected asset classes or regions (e.g., `["USD", "US Equities", "Treasuries"]`). Empty array `[]` if unknown. |
| `sentiment` | `string` | Yes | One of: `"positive"`, `"negative"`, `"neutral"`. `null` if no sentiment data available. |

### `get_market_events` Tool Parameters

| Parameter | Type | Required | Default | Valid Values |
|---|---|---|---|---|
| `category` | `string` | No | `"all"` | `"fed"`, `"treasury"`, `"geopolitical"`, `"regulatory"`, `"central_bank"`, `"institutional"`, `"all"` |
| `event_type` | `string` | No | `"all"` | `"scheduled"`, `"breaking"`, `"all"` |
| `from_date` | `string` | No | Current UTC date | ISO 8601 date string (e.g., `"2026-04-12"`) |
| `to_date` | `string` | No | 7 days from `from_date` | ISO 8601 date string |
| `impact_level` | `string` | No | `"all"` | `"high"`, `"medium"`, `"low"`, `"all"` |

## Requirements

### Functional Requirements

1. The system shall expose a new MCP tool named `get_market_events` separate from `get_market_news`
2. The tool shall accept optional parameters: `category`, `from_date`, `to_date`, `impact_level`
3. When `from_date` is omitted, it shall default to the current UTC date; when `to_date` is omitted, it shall default to 7 days after `from_date`
4. The tool shall query Finnhub `/calendar/economic` for scheduled events and Finnhub news with category filter for breaking events
5. The tool shall query AlphaVantage NEWS_SENTIMENT API with macro-relevant topic filters for breaking events
6. All provider results shall be merged and deduplicated before returning, using the same deduplication logic applied by `get_market_news`
7. Each returned event shall conform to the `MarketEvent` data model defined in this specification
8. Events shall be classified as `event_type: "scheduled"` when sourced from an economic calendar endpoint, and `"breaking"` when sourced from a news endpoint
9. The tool shall apply `category` and `impact_level` filters client-side after fetching from providers, when providers do not support server-side filtering
10. The tool shall sort scheduled events ascending by `event_time` and breaking events descending by `event_time`
11. The system shall define an `IMarketEventsProvider` interface that both Finnhub and AlphaVantage implement, enabling future provider additions without modifying the tool handler
12. When `from_date` is after `to_date`, the tool shall return a validation error without querying providers
13. When an invalid `category` or `impact_level` value is supplied, the tool shall return a validation error listing valid values
14. When all providers fail, the tool shall return an investor-friendly error message listing each provider and the reason for failure

### Non-Functional Requirements

- **Latency — Scheduled Events**: Response for economic calendar queries (next 7–30 days) must complete in ≤3 seconds under normal network conditions
- **Latency — Breaking Events**: Breaking events must be available within ≤60 seconds of publication on the source provider; the tool itself must respond in ≤3 seconds
- **Rate Limits**: The system must respect Finnhub free-tier rate limits (60 calls/minute) and AlphaVantage free-tier limits (5 calls/minute, 500/day) using the existing rate-limit-aware resilience layer
- **Deduplication Performance**: Deduplication across provider results must complete in ≤200ms regardless of the number of configured providers
- **Provider Resilience**: The tool must apply the existing circuit breaker pattern; a failing provider must not block results from a healthy provider
- **Extensibility**: Adding a new `IMarketEventsProvider` implementation must not require changes to the `get_market_events` tool handler
- **Data Integrity**: All `event_time` values must be stored and returned in UTC; local time from provider responses must be normalized to UTC at ingestion
- **Security**: Provider API keys must be read from environment configuration only; no key shall appear in logs or tool responses

## Acceptance Criteria

### Category A: Tool Registration and Parameter Validation

- [ ] **[Blocking]** AC-1: `get_market_events` is registered as a new MCP tool and appears in the tools list
  - **Evidence**: Calling `list_providers` or MCP tool discovery returns `get_market_events` in the tool list; tool is callable from Claude Desktop or equivalent MCP client

- [ ] **[Blocking]** AC-2: `category` parameter validates against the defined enum; invalid values return a descriptive error
  - **Evidence**: Calling `get_market_events` with `category: "earnings"` returns a validation error listing valid values; no provider is queried

- [ ] **[Blocking]** AC-3: `impact_level` parameter validates against the defined enum; invalid values return a descriptive error
  - **Evidence**: Calling `get_market_events` with `impact_level: "critical"` returns a validation error listing valid values

- [ ] **[Blocking]** AC-4: When `from_date` is after `to_date`, tool returns validation error without querying providers
  - **Evidence**: Unit test confirms provider `GetEventsAsync` is never called when date range is invalid; error message is investor-friendly

- [ ] **[Non-blocking]** AC-5: When no parameters are supplied, tool defaults to `category: "all"`, `impact_level: "all"`, and a 7-day window from today
  - **Evidence**: Calling `get_market_events` with no arguments returns events for today through today+7 days with no category/impact filter applied

### Category B: Data Retrieval — Scheduled Events

- [ ] **[Blocking]** AC-6: Finnhub economic calendar events are returned as `event_type: "scheduled"`
  - **Evidence**: Integration test calling `get_market_events` with Finnhub configured returns at least one event with `event_type: "scheduled"` and `source: "Finnhub"` for a date range known to contain FOMC or economic calendar entries

- [ ] **[Blocking]** AC-7: Each scheduled event includes all non-nullable `MarketEvent` fields
  - **Evidence**: Integration test verifies `event_id`, `title`, `event_type`, `category`, `event_time`, `source`, and `affected_markets` are non-null/non-empty for all returned scheduled events

- [ ] **[Blocking]** AC-8: `event_time` is returned in UTC ISO 8601 format
  - **Evidence**: Unit test for Finnhub provider mapping confirms `event_time` is normalized to UTC and conforms to ISO 8601 (e.g., ends in `Z`)

### Category C: Data Retrieval — Breaking Events

- [ ] **[Blocking]** AC-9: AlphaVantage NEWS_SENTIMENT results with macro topics are returned as `event_type: "breaking"`
  - **Evidence**: Integration test calling `get_market_events` with AlphaVantage configured returns at least one event with `event_type: "breaking"` and `source: "AlphaVantage"` for a recent date range

- [ ] **[Blocking]** AC-10: When `sentiment` data is available from AlphaVantage, it is mapped to `"positive"`, `"negative"`, or `"neutral"`
  - **Evidence**: Unit test for AlphaVantage provider mapping confirms sentiment score ranges are correctly mapped to the three-value enum

- [ ] **[Non-blocking]** AC-11: When no sentiment data is available for an event, `sentiment` field is `null` (not omitted)
  - **Evidence**: Unit test mocks a Finnhub event with no sentiment field; confirms serialized response contains `"sentiment": null`

### Category D: Filtering

- [ ] **[Blocking]** AC-12: `category: "fed"` filter returns only Fed-categorized events; no other categories included
  - **Evidence**: Unit test with mock data containing mixed-category events confirms only `"fed"` events are in the response when filter is applied

- [ ] **[Blocking]** AC-13: `impact_level: "high"` filter excludes events with `impact_level: "medium"`, `"low"`, or `null`
  - **Evidence**: Unit test with mock data containing all impact levels confirms only `"high"` events are returned when filter is applied

- [ ] **[Blocking]** AC-14: Combined `category` and `impact_level` filters are applied conjunctively (AND logic)
  - **Evidence**: Unit test confirms that only events matching both `category: "fed"` AND `impact_level: "high"` are returned; events matching only one filter are excluded

### Category E: Deduplication

- [ ] **[Blocking]** AC-15: When the same event is returned by both Finnhub and AlphaVantage, only one copy appears in the response
  - **Evidence**: Unit test with mocked provider responses containing the same event (matching title and `event_time` within ±1 hour) confirms exactly one result in the output

- [ ] **[Blocking]** AC-16: Similar but distinct events (same date, different title) are not merged by deduplication
  - **Evidence**: Unit test confirms two events with matching `event_time` but different titles are both present in the response

- [ ] **[Non-blocking]** AC-17: Deduplication completes in ≤200ms for up to 100 combined events across providers
  - **Evidence**: Performance test (or benchmark unit test) confirms deduplication of 100 mock events completes within the time budget

### Category F: Provider Resilience and Fallback

- [ ] **[Blocking]** AC-18: When Finnhub fails, AlphaVantage results are returned without exposing the Finnhub error to the user
  - **Evidence**: Integration test with Finnhub mocked to return a 500 error; response contains AlphaVantage events with `source: "AlphaVantage"` and no Finnhub error text in the user-facing output

- [ ] **[Blocking]** AC-19: When all providers fail, a structured, investor-friendly error message is returned listing each provider and reason
  - **Evidence**: Unit test mocking all providers to fail returns an error message referencing each provider with a plain-language reason (no HTTP status codes or stack traces)

- [ ] **[Non-blocking]** AC-20: Existing circuit breaker configuration is applied to the events provider calls
  - **Evidence**: Code review confirms `get_market_events` providers use the same resilience pipeline as other tools (Polly or equivalent); no new direct `HttpClient` calls without resilience wrapping

### Category G: Extensibility

- [ ] **[Blocking]** AC-21: `IMarketEventsProvider` interface is defined and implemented by both Finnhub and AlphaVantage providers
  - **Evidence**: Code review confirms the interface exists with at minimum `GetEventsAsync(MarketEventsQuery query)` method; both providers implement it

- [ ] **[Non-blocking]** AC-22: A new provider can be registered in DI and appsettings.json without modifying the `get_market_events` tool handler
  - **Evidence**: Code review or architecture review confirms the tool handler iterates over `IEnumerable<IMarketEventsProvider>` from DI

## Out of Scope

- **Real-time streaming / WebSocket delivery**: Events are retrieved on-demand per tool call; push-based or streaming event delivery is not included in this feature
- **Earnings calendar events**: Scheduled earnings announcements are served by `get_stock_info` and are out of scope for this feed; this tool focuses on macro/governmental events only
- **Social media signals**: Twitter/X, Reddit, or other social sentiment feeds are not included; only structured provider APIs are in scope
- **Event de-duplication using ML/NLP**: Deduplication uses existing title-similarity and time-proximity logic; no machine learning models are introduced
- **Historical event archive beyond 30 days**: The tool is not designed as a historical events database; providers impose their own lookback limits which the tool respects without extension
- **Notification/webhook delivery of events**: The MCP server is request/response only; no proactive push of events to clients is in scope
- **User-configurable alert thresholds**: No per-user impact threshold storage or alert rules in this version
- **New provider integrations**: No new providers (e.g., Polygon.io news, Bloomberg) are added in this issue; the interface is designed to support them in a future issue

## Dependencies

### Depends On

- **Issue #17 (MCP Error Handling)**: Investor-friendly error messages and per-provider failure reporting rely on the error handling architecture from Issue #17
- **Existing Finnhub provider**: Must expose the `/calendar/economic` endpoint in addition to existing endpoints; Finnhub provider class will be extended
- **Existing AlphaVantage provider**: Must expose NEWS_SENTIMENT with macro topic filtering; AlphaVantage provider class will be extended
- **Existing deduplication module**: The `Deduplication/` module currently used by news tools must be reusable for `MarketEvent` entities; confirm interface genericity or extend as needed

### Blocks

- **Future Issue: Event-Driven Alerts** — A future alerting feature would depend on the `get_market_events` data model and `IMarketEventsProvider` interface established here

### Related Issues

- **Issue #17** (MCP Error Handling): Error handling patterns apply directly to provider fallback in this feature
- **Issue #32** (Provider Tier Handling): Free-tier rate limits and tier-awareness apply to both Finnhub and AlphaVantage event endpoints

## Technical Considerations

1. **Finnhub Economic Calendar**: The `/calendar/economic` endpoint returns events keyed by date with country codes and impact ratings. The provider mapping layer must translate Finnhub impact ratings (`1`=low, `2`=medium, `3`=high) to the `MarketEvent.impact_level` enum, and must infer `category` from the event `event` field text (e.g., events containing "Fed" or "FOMC" → `"fed"`; "ECB", "BOJ" → `"central_bank"`).

2. **AlphaVantage NEWS_SENTIMENT Topics**: AlphaVantage supports `topics` query parameter values including `economy_macro`, `economy_monetary`, `economy_fiscal`, and `government_and_politics`. These map to `"fed"`, `"central_bank"`, `"treasury"`, and `"regulatory"` categories respectively. Multiple topics can be requested in a single call.

3. **Deduplication Key Design**: Events should be deduplicated on a composite key of normalized title (lowercase, punctuation-stripped) and `event_time` bucket (±1 hour). The existing deduplication module in `Deduplication/` should be evaluated for reuse before creating a new implementation.

4. **UTC Normalization**: Finnhub economic calendar events include a `date` field (date-only) and optional `time` field. When time is missing, events should default to `event_time: "<date>T00:00:00Z"` and this should be visible to consumers. The description should note the time is approximate.

5. **Provider Priority for Events**: Provider priority order should follow the same `appsettings.json` configuration as other tools. Events from higher-priority providers take precedence during deduplication.

6. **Rate Limit Awareness**: AlphaVantage free tier allows 5 calls/minute. `get_market_events` may query multiple AlphaVantage topic filters in one logical call; batching or prioritizing the most relevant topic per call should be considered by the architecture team to avoid exhausting the rate limit.
