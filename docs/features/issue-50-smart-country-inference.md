# Feature: Smart Country Inference and Country Data Exposure

<!--
  Template owner: Product Manager
  Output directory: docs/features/
  Filename convention: issue-50-smart-country-inference.md
  Related Issue: #50
-->

## Document Info

- **Status**: Complete
- **Last Updated**: 2026-04-11

## Overview

The StockData.Net MCP server exposes a `country` field in all stock data responses, sourced either from live API data (Yahoo Finance) or inferred from the exchange suffix of the ticker symbol (Alpaca, Finnhub, AlphaVantage). Country values are normalized to ISO 3166-1 alpha-2 codes across all providers so that consumers receive a consistent, machine-readable format regardless of which provider serves the request.

## Problem Statement

Investors and portfolio management tools need country information for every stock in order to perform geographic diversification analysis, apply jurisdiction-specific compliance rules, and classify holdings by market region. Prior to this feature:

- Yahoo Finance returned a `summaryProfile_country` field inside its raw response, but it was undocumented, inconsistently formatted, and not surfaced as a named field.
- Alpaca, Finnhub, and AlphaVantage returned no country information at all — they simply included a hardcoded `Country: "US"` value regardless of the actual ticker.
- No provider contract existed for the `country` field, so consumers had to guess at the field name and handle format differences manually.
- Full country name strings (`"United States"`, `"United Kingdom"`) were mixed with ISO codes (`"US"`, `"GB"`), making automated processing unreliable.

This affects:

- Investors using AI assistants to analyze internationally listed stocks (e.g., Canadian TSX tickers like `RY.TO`, London LSE tickers like `HSBA.L`)
- Applications consuming the MCP tool output that need a stable, machine-readable country identifier

## User Stories

### User Story 1: As an investor, I want stock data responses to include a country code so that I can identify the listing country for portfolio diversification analysis

> 1.1 **Happy Path — US-listed ticker, any provider**
>
> Given any provider is configured and I request stock data for a plain US ticker (e.g., `AAPL`)
> When the provider returns the stock info response
> Then the response includes a `country` field with value `"US"`
>
> 1.2 **Happy Path — Non-US ticker with recognized exchange suffix**
>
> Given any non-Yahoo provider is configured and I request stock data for a ticker with a known exchange suffix (e.g., `RY.TO` for the Toronto Stock Exchange)
> When the provider returns the stock info response
> Then the response includes a `country` field with the ISO 3166-1 alpha-2 code for that exchange's country (`"CA"`)
>
> 1.3 **Happy Path — Yahoo Finance ticker with real country data**
>
> Given Yahoo Finance is configured and I request stock data for any ticker
> When Yahoo Finance returns its response
> Then the response includes a `summaryProfile_country` field whose value is an ISO 3166-1 alpha-2 country code sourced from the Yahoo Finance `summaryProfile` module
>
> 1.4 **Edge Case — Ticker with unrecognized exchange suffix**
>
> Given a non-Yahoo provider is configured and I request stock data for a ticker with a suffix not in the recognized exchange mapping (e.g., `XYZ.XX`)
> When the provider attempts country inference
> Then the `country` field is `null` (not `"International"` or any other placeholder string)
>
> 1.5 **Edge Case — Index symbol with caret prefix**
>
> Given a non-Yahoo provider is configured and I request data for an index ticker such as `^GSPC`
> When the provider returns the response
> Then the `country` field is `"US"` (caret prefix is stripped before suffix analysis, no dot suffix remains)

### User Story 2: As a developer consuming the MCP tool, I want country codes to be in a consistent ISO format across all providers so that I can process them without provider-specific logic

> 2.1 **Happy Path — ISO alpha-2 code returned by inferred providers**
>
> Given Alpaca, Finnhub, or AlphaVantage is configured
> When I request stock info for a ticker with a known suffix (e.g., `HSBA.L`)
> Then the `country` field value is exactly `"GB"` — two uppercase letters, ISO 3166-1 alpha-2
> And the value is never a full country name such as `"United Kingdom"`
>
> 2.2 **Happy Path — Same ISO code for same ticker across providers**
>
> Given both Alpaca and Finnhub are configured as fallback providers
> When I request stock info for `RY.TO` and both providers respond
> Then both return `country: "CA"`
> And the value is identical between providers
>
> 2.3 **Error Case — Invalid ticker input (lone caret)**
>
> Given any provider is configured
> When I request stock data using the ticker `"^"` (caret only, no symbol body)
> Then the system returns an `ArgumentException` / validation error
> And no country inference is attempted

### User Story 3: As an investor using a non-US exchange ticker, I want the system to correctly identify the country from the ticker format so that I do not have to manually specify the country

> 3.1 **Happy Path — Canadian TSX ticker**
>
> Given Alpaca, Finnhub, or AlphaVantage is configured
> When I request stock info for `RY.TO`
> Then `country` is `"CA"`
>
> 3.2 **Happy Path — London Stock Exchange ticker**
>
> Given Alpaca, Finnhub, or AlphaVantage is configured
> When I request stock info for `HSBA.L`
> Then `country` is `"GB"`
>
> 3.3 **Happy Path — Japanese ticker (Tokyo Stock Exchange)**
>
> Given Alpaca, Finnhub, or AlphaVantage is configured
> When I request stock info for `7203.T`
> Then `country` is `"JP"`
>
> 3.4 **Happy Path — Indian ticker (NSE)**
>
> Given Alpaca, Finnhub, or AlphaVantage is configured
> When I request stock info for `INFY.NS`
> Then `country` is `"IN"`
>
> 3.5 **Happy Path — Chinese ticker (Shanghai)**
>
> Given Alpaca, Finnhub, or AlphaVantage is configured
> When I request stock info for `600519.SS`
> Then `country` is `"CN"`

## Requirements

### Functional Requirements

1. All non-Yahoo provider responses for `get_stock_info`, `get_historical_stock_prices`, and `get_finance_news` shall include a `country` field.
2. The `country` field value shall be an ISO 3166-1 alpha-2 code (two uppercase letters) or `null` if the exchange cannot be determined.
3. The system shall recognize at least 22 exchange suffixes covering: Canada (`.TO`, `.V`, `.CN`), United Kingdom (`.L`, `.IL`), Australia (`.AX`), New Zealand (`.NZ`), Germany (`.DE`, `.F`), France (`.PA`), Netherlands (`.AS`), Switzerland (`.SW`), Brazil (`.BR`, `.SA`), Hong Kong (`.HK`), Japan (`.T`), South Korea (`.KS`, `.KQ`), China (`.SS`, `.SZ`), India (`.BO`, `.NS`).
4. Tickers with no exchange dot-suffix shall default to `"US"`.
5. Tickers with an unrecognized exchange suffix shall return `null` for `country`.
6. A lone `^` symbol (caret with no following characters) shall be rejected with a validation error before inference is attempted.
7. Yahoo Finance shall continue to source country from its `summaryProfile` API module; the inferred-country logic shall not be applied to Yahoo Finance responses.
8. Country inference logic shall exist in a single shared location — not duplicated across individual provider implementations.
9. Suffix matching shall be case-insensitive (`.to` and `.TO` are equivalent).

### Non-Functional Requirements

- **Performance**: Country inference is a pure in-memory string operation with O(1) dictionary lookup — no measurable latency impact on any provider response.
- **Security**: Ticker input is validated before inference; the country field value is always drawn from a closed, static dictionary — no user-controlled strings are reflected into the country output.
- **Maintainability**: Adding a new exchange suffix requires a single dictionary entry in one file (`TickerCountryInferrer`); no provider files need to be modified.
- **Consistency**: The `country` field must serialize as `null` (not `"null"`, `"Unknown"`, or `"International"`) when the exchange cannot be determined.

## Acceptance Criteria

- [ ] **[Blocking]** All 22 recognized exchange suffixes return their correct ISO alpha-2 code — Evidence: `TickerCountryInferrerTests` data-driven test passes for all 22 `[DataRow]` cases.
- [ ] **[Blocking]** Plain US tickers (no suffix) return `"US"` — Evidence: unit test `InferIsoCountryCode_PlainUsTicker_ReturnsUS` passes.
- [ ] **[Blocking]** Unknown exchange suffix returns `null` — Evidence: unit test `InferIsoCountryCode_UnknownSuffix_ReturnsNull` passes.
- [ ] **[Blocking]** Lone `^` input returns `null` from the inferrer and is rejected by each provider's validation — Evidence: unit test `InferIsoCountryCode_LoneCaret_ReturnsNull` passes; each provider throws `ArgumentException` for `"^"`.
- [ ] **[Blocking]** `InferCountryFromSymbol()` does not exist in `AlpacaProvider`, `FinnhubProvider`, or `AlphaVantageProvider` — Evidence: workspace-wide search for `InferCountryFromSymbol` returns zero matches.
- [ ] **[Blocking]** No full country name strings (`"United States"`, `"Canada"`, `"United Kingdom"`, `"International"`) appear in any provider file — Evidence: grep search returns zero matches.
- [ ] **[Blocking]** Provider unit tests assert the `country` JSON field in stock info responses — Evidence: `AlpacaProviderTests`, `FinnhubProviderTests`, `AlphaVantageProviderTests` each contain at least one assertion on the `country` field.
- [ ] **[Blocking]** Solution builds with 0 errors — Evidence: `dotnet build` exits with code 0.
- [ ] **[Blocking]** All unit tests pass — Evidence: `dotnet test` reports 0 failures.
- [ ] **[Non-blocking]** Suffix matching is case-insensitive — Evidence: unit test `InferIsoCountryCode_CasingInsensitive_ReturnsCA` passes.
- [ ] **[Non-blocking]** Yahoo Finance `summaryProfile_country` field is present in integration test responses for 3+ stocks from 3 different countries — Evidence: dedicated integration test assertions on `summaryProfile_country` field.

## Out of Scope

- Creating strongly-typed response models for country data (deferred to a future phase)
- Supporting country data from providers that do not embed country in the ticker format (e.g., via a separate API call to an exchange reference database)
- Country name localisation or translation (the `country` field is always an ISO code, not a display name)
- Mapping country codes to geographic regions or continents
- Handling tickers that list on multiple exchanges simultaneously
- Adding exchange suffixes beyond the 22 documented in FR3 (new suffixes are a future enhancement)

## Dependencies

- **Depends on**: Ticker validation logic per provider (`ValidateTicker` or equivalent) — the lone `^` guard is added at that layer
- **Depends on**: Yahoo Finance `summaryProfile` module being included in the `quoteSummary` API request (already in scope from prior work)
- **Blocks**: Any feature that consumes the `country` field and expects ISO codes (e.g., geographic filtering, compliance screening)

## Technical Considerations

- Inference is exchange-suffix-based, not company-domicile-based: a US company listed on the Toronto Stock Exchange under a `.TO` suffix will return `"CA"`, not `"US"`. This is intentional — the field represents the listing country, not the incorporation country.
- The shared inferrer class (`TickerCountryInferrer`) is `internal` to the provider layer. If the inference logic ever needs to be consumed by higher layers (e.g., the MCP server for tool description metadata), it would need to be promoted to `internal` at the solution level or exposed via a interface.
- Yahoo Finance country values come from a live API and may differ from inferred values for dual-listed stocks; no reconciliation between the two sources is performed.
- The dot-suffix extraction uses the last `.` in the symbol, which correctly handles numeric tickers like `600519.SS`.
