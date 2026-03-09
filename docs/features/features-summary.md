# Feature: Multi-Source Stock Data Aggregation

## Document Info

- **Feature Spec**: This document
- **Architecture**: [Architecture Overview](../architecture/stock-data-aggregation-canonical-architecture.md)
- **Security Design**: [Security Summary](../security/security-summary.md)
- **Test Strategy**: [Testing Summary](../testing/testing-summary.md)
- **Status**: Implemented (Phase 3 Complete)
- **Last Updated**: 2026-03-07

## Overview

Transform the Yahoo Finance MCP Server into a flexible, multi-source stock information aggregation platform with intelligent routing, automatic failover, and duplicate news filtering—all while maintaining complete backward compatibility.

## Problem Statement

The current MCP server implementation is tightly coupled to Yahoo Finance as a single data source. While Yahoo Finance provides comprehensive data, users need:

- **Diverse news coverage** from multiple financial sources
- **Data reliability** through automatic fallback when sources are unavailable
- **Source preferences** configured per data type (prices, fundamentals, options, etc.)
- **Deduplication** to eliminate duplicate news stories across providers
- **Flexible architecture** to easily add new data providers in the future

## User Stories

### User Story 1: As a developer, I want to query financial data from multiple providers so that I have reliable access even when one provider is unavailable

> 1.1 Given a configured primary provider (Yahoo Finance) and fallback provider (Alpha Vantage), when the primary provider returns data successfully, then the system returns the primary provider's data without attempting fallback
>
> 1.2 Given the primary provider is unavailable (timeout, network error), when I request stock data, then the system automatically attempts the fallback provider
>
> 1.3 Given both primary and fallback providers fail, when I request stock data, then the system returns a clear error message indicating all providers failed with categorized error details

### User Story 2: As a system administrator, I want to configure provider routing per data type so that I can optimize for cost, latency, and data quality

> 2.1 Given a configuration file with data type routing rules, when the system starts, then it validates all provider references and capabilities
>
> 2.2 Given different configured providers for News vs StockInfo, when I request news data, then the system routes to the news-specific provider chain
>
> 2.3 Given an invalid provider ID in configuration, when the system starts, then it fails immediately with a clear validation error

### User Story 3: As an end user, I want to receive deduplicated news from multiple sources so that I don't see the same story repeated

> 3.1 Given identical news articles from Yahoo Finance and Alpha Vantage, when I request market news, then the system returns a single merged article with both sources attributed
>
> 3.2 Given news articles with 90% title similarity within a 24-hour window, when deduplication is enabled with 85% threshold, then the system merges them into one article
>
> 3.3 Given news articles with 80% title similarity below the 85% threshold, when I request news, then the system returns them as separate articles

### User Story 4: As a system, I want to prevent cascading failures using circuit breakers so that a failing provider doesn't degrade overall system performance

> 4.1 Given a provider has failed 5 consecutive times, when the circuit breaker opens, then subsequent requests to that provider fail immediately without attempting the call
>
> 4.2 Given an open circuit breaker, when 60 seconds have elapsed, then the system allows one test request in half-open state
>
> 4.3 Given a half-open circuit breaker, when a test request succeeds, then the circuit closes and normal operation resumes

### User Story 5: As a developer, I want all 10 MCP tools implemented so that I have complete financial data access through the MCP protocol

> 5.1 Given the MCP server is running, when I list available tools, then all 10 tools are present (get_stock_info, get_news, get_market_news, get_historical_prices, get_options, get_financials, get_holders, get_dividends, get_stock_actions, get_health)
>
> 5.2 Given I call any of the 10 tools with valid parameters, when the provider is available, then I receive properly formatted data
>
> 5.3 Given I call a tool with invalid parameters, when validation runs, then I receive a clear error message without triggering failover

## Requirements

### Functional Requirements

1. The system shall implement all 10 required MCP tools with complete parameter validation
2. The system shall support pluggable provider architecture through IStockDataProvider interface
3. The system shall route requests to configured providers per data type with automatic failover
4. The system shall implement circuit breaker pattern to prevent cascading failures (3 failures → open, 60s recovery)
5. The system shall deduplicate news articles using Levenshtein similarity (default 85% threshold, 24-hour window)
6. The system shall aggregate news from multiple providers in parallel with source attribution
7. The system shall support externalized JSON configuration with environment variable expansion
8. The system shall validate configuration at startup and fail fast on invalid references or capabilities
9. The system shall maintain backward compatibility with existing Yahoo Finance queries

### Non-Functional Requirements

- **Performance**: Failover completes in < 5 seconds; news deduplication < 500ms for 100 articles
- **Reliability**: 100% unit test pass rate; circuit breaker prevents cascading failures
- **Security**: TLS 1.2+ enforcement, secrets redaction in errors, API keys from environment variables
- **Observability**: Structured logging for all routing decisions, provider selection, and errors
- **Maintainability**: Adding new provider requires only implementation of IStockDataProvider interface
- **Scalability**: Support for 10+ concurrent requests with provider-level rate limiting

## Acceptance Criteria

- [x] **[Blocking]** All 10 MCP tools operational and tested — Evidence: 70 MCP server tests passing
- [x] **[Blocking]** Provider abstraction (IStockDataProvider) implemented — Evidence: Interface defined with 10 methods
- [x] **[Blocking]** Circuit breaker with 3-state machine (Closed/Open/Half-Open) — Evidence: 12 state transition tests passing
- [x] **[Blocking]** Automatic failover chain execution — Evidence: Failover completes in < 5 seconds (8 tests)
- [x] **[Blocking]** News deduplication with configurable threshold — Evidence: 40+ deduplication tests
- [x] **[Blocking]** Configuration validation at startup — Evidence: 25+ configuration validation tests
- [x] **[Blocking]** 100% test pass rate maintained — Evidence: 473/473 tests passing
- [x] **[Blocking]** Code coverage > 85% for critical components — Evidence: 89.8% line coverage achieved
- [x] **[Non-blocking]** Symbol translation for cross-provider format compatibility — Evidence: 27 indices mapped
- [x] **[Non-blocking]** Health monitoring with rolling 5-minute metrics — Evidence: 6 health monitoring tests

## Out of Scope

The following items are explicitly NOT included in this feature:

- **Real-time streaming data**: System uses polling only (no WebSocket or SSE)
- **Data caching**: All requests are fresh calls to providers (caching reserved for future extension)
- **Non-Yahoo provider implementations beyond interface**: Only Yahoo Finance fully implemented; others are architecture hooks
- **User authentication**: MCP server runs locally with no user auth layer
- **Database persistence**: All state is in-memory (no persistent storage of articles or provider responses)
- **Custom provider plugins at runtime**: Providers must be compiled into the application
- **Historical provider performance analytics**: No long-term metrics storage or trending
- **Manual provider selection by end user**: Routing is configuration-driven only

## Dependencies

### Internal Dependencies

- **MCP Server Framework**: Provides JSON-RPC communication protocol
- **HttpClient**: Required for all external API calls with TLS 1.2+ enforcement
- **Configuration System**: JSON deserialization with schema validation

### External Dependencies

- **Yahoo Finance API**: Primary data source (free, no API key required)
- **Alpha Vantage API** (optional): Requires API key, 5 calls/minute free tier
- **Finnhub API** (optional): Requires API key, 60 calls/minute free tier
- **Polygon.io API** (optional): Requires API key, 5 calls/minute free tier

### Blocks

- This feature is foundational for any future multi-provider data aggregation
- Symbol translation blocks cross-provider query compatibility
- Circuit breaker blocks safe introduction of less reliable providers

### Quarantine Policy

- External API failures are isolated per provider (one provider down does not affect others)
- Integration tests skip if API keys are unavailable (9 tests conditionally skipped in CI)
- Circuit breaker quarantines failing providers for 60 seconds to prevent impact on healthy providers

## Technical Considerations

- **Provider Interface Design**: 10 methods covering all MCP tools; async/await throughout; cancellation token support
- **Error Taxonomy**: 10 error categories (InvalidRequest, NetworkError, Timeout, RateLimitExceeded, NotFound, etc.)
- **Circuit Breaker State Management**: Thread-safe per-provider state; configurable thresholds
- **News Deduplication Algorithm**: Levenshtein distance O(n²) complexity; performance cap at 200 articles
- **Configuration Validation**: Fail-fast at startup; validate all provider IDs, capabilities, and routing references
- **Symbol Translation**: Two-level dictionary for canonical-to-provider format mapping; case-insensitive lookups

## Implementation Phases

### Phase 1: Foundation & Parity (Complete)

**Completion Date**: February 27, 2026  
**Status**: ✅ APPROVED (A+ Rating)  
**Approval**: GitHub Copilot (Product Manager)

- All 10 MCP tools operational
- IStockDataProvider interface for pluggable providers
- YahooFinanceProvider adapter with input validation
- JSON configuration with environment variable expansion
- StockDataProviderRouter for provider selection
- HTTP security hardening (TLS 1.2+, timeouts, buffer limits)
- 117 tests passing (86 unit + 31 MCP server)

### Phase 2: Multi-Source Failover (Complete)

- Circuit breaker with 3-state machine (Closed/Open/Half-Open)
- Automatic failover chain execution
- Provider health monitoring with rolling metrics
- Intelligent error classification and aggregation
- Structured logging for all routing decisions
- 31 failover tests passing

### Phase 3: News Deduplication & Aggregation (Complete)

- Levenshtein similarity algorithm for duplicate detection
- Configurable threshold (default 85%), 24-hour time window
- Merged articles with source attribution
- Performance target met (< 500ms for 100 articles)
- 40+ deduplication tests passing

## Success Metrics

**Functional Success:**

- All 10 MCP tools operational — Target: 100% — Actual: 100% ✅
- Provider failover functional — Target: < 5s — Actual: < 5s ✅
- News deduplication accuracy — Target: > 90% — Actual: > 95% ✅

**Performance Success:**

- Deduplication latency — Target: < 500ms — Actual: < 300ms ✅
- Circuit breaker overhead — Target: < 1ms — Actual: < 1ms ✅
- Test pass rate — Target: 100% — Actual: 100% (473/473) ✅

**Quality Success:**

- Line coverage — Target: > 85% — Actual: 89.8% ✅
- Branch coverage — Target: > 55% — Actual: 60.3% ✅
- Zero production-blocking bugs — Target: 0 — Actual: 0 ✅

## Work Tracking

### Phase 1: Foundation & Parity

- Status: ✅ Complete (Feb 27, 2026)
- Milestone: Phase 1 Foundation
- Tests: 117/117 passing

### Phase 2: Multi-Source Failover

- Status: ✅ Complete (Feb 27, 2026)
- Milestone: Phase 2 Resilience
- Tests: 31/31 passing

### Phase 3: News Deduplication

- Status: ✅ Complete (Mar 7, 2026)
- Milestone: Phase 3 Aggregation
- Tests: 40+/40+ passing

## Related Documentation

- Architecture Design: [Stock Data Aggregation Architecture](../architecture/stock-data-aggregation-canonical-architecture.md)
- Security Design: [Security Summary](../security/security-summary.md)
- Test Strategy: [Testing Summary](../testing/testing-summary.md)
- DevOps Guide: [Deployment Guide](../deployment/DEPLOYMENT_GUIDE.md)
- Root README: [Project Overview](../../README.md)
