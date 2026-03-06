# Feature: Smart Symbol Translation

## Overview
Implement a provider-aware symbol translation system that automatically converts market symbols (particularly indices) to the correct format expected by each financial data provider. This allows users to query using any format (bare names, Yahoo format, alternative provider formats) while ensuring the correct provider-specific format is used when making API calls.

## Problem Statement
Financial data providers use different symbol formats for the same security. For example, the VIX volatility index is represented as:
- `VIX` (canonical bare name)
- `^VIX` (Yahoo Finance format)
- `@VX` (FinViz format)

Currently, users must know and use the exact format that Yahoo Finance expects. If they use the wrong format, queries fail. This creates a poor user experience and limits the system's ability to support multiple providers in the future.

The symbol translator solves this by:
1. Accepting symbols in any recognized format
2. Automatically translating to the provider's expected format before API calls
3. Providing a centralized mapping system that supports multiple provider formats

## User Stories

### User Story 1: As a user, I want to query market indices using their bare canonical names (e.g., "VIX", "GSPC") so that I don't need to remember provider-specific prefix conventions

1.1 Given I am querying the Yahoo Finance provider, when I request data for symbol "VIX", then the system translates it to "^VIX" and successfully retrieves VIX data

1.2 Given I am querying the Yahoo Finance provider, when I request data for symbol "GSPC", then the system translates it to "^GSPC" and successfully retrieves S&P 500 data

1.3 Given I am querying the Yahoo Finance provider, when I request data for symbol "DJI", then the system translates it to "^DJI" and successfully retrieves Dow Jones data

1.4 Given I am querying the Yahoo Finance provider, when I request data for a regular stock symbol like "AAPL", then the system passes it through unchanged and successfully retrieves Apple stock data

### User Story 2: As a user, I want to use provider-specific formats if I already know them (e.g., "^VIX" for Yahoo) so that my existing queries and integrations continue to work without breaking changes

2.1 Given I am querying the Yahoo Finance provider, when I request data for symbol "^VIX", then the system recognizes it as already in Yahoo format and passes it through unchanged

2.2 Given I am querying the Yahoo Finance provider, when I request data for symbol "^GSPC", then the system passes it through unchanged and successfully retrieves S&P 500 data

2.3 Given I am querying the Yahoo Finance provider, when I request both "VIX" and "^VIX" in separate queries, then both queries return identical data

### User Story 3: As a developer, I want the symbol translator to automatically convert symbols to the correct provider format based on which provider the router selects so that translation happens transparently without routing logic changes

3.1 Given the router has selected the Yahoo Finance provider, when any symbol enters the translation layer, then it is converted to Yahoo format (if a mapping exists) before being passed to the Yahoo provider

3.2 Given the router has selected a future FinViz provider, when symbol "VIX" enters the translation layer, then it is converted to "@VX" format before being passed to the FinViz provider

3.3 Given the router has selected Yahoo Finance provider, when symbol "@VX" (FinViz format) enters the translation layer, then it is converted to "^VIX" (Yahoo format) before being passed to Yahoo

3.4 Given the translation layer receives a symbol with no configured mapping, when the target provider is Yahoo Finance, then the symbol is passed through unchanged to the provider

### User Story 4: As a user, I want clear and immediate error messages when I use an invalid or unrecognized symbol format so that I can correct my query

4.1 Given I request data for a completely invalid symbol like "!!!INVALID", when the provider returns an error, then the system returns a clear error message indicating the symbol was not found

4.2 Given I request data for a symbol that doesn't exist in any provider format, when the provider lookup fails, then the system returns an appropriate "symbol not found" error

### User Story 5: As a system administrator, I want comprehensive coverage of major market indices in the initial release so that users can query the most commonly used benchmarks without issues

5.1 Given the system is deployed, when I review the translation mappings, then all major US market indices (VIX, GSPC, DJI, IXIC, RUT, NDX, NYA) are included

5.2 Given the system is deployed, when I review the translation mappings, then major international indices (FTSE, GDAXI, N225, HSI) are included

5.3 Given the system is deployed, when I review the translation mappings, then sector-specific indices (SOX, XOI, HUI) are included

### User Story 6: As a developer, I want the symbol mapping system to be easily extensible so that adding new providers or new symbol mappings requires minimal code changes

6.1 Given I need to add a new provider (e.g., Google Finance), when I add provider-specific formats to the symbol mapping dictionary, then the translator automatically handles conversions to that provider

6.2 Given I need to add a new index symbol, when I add a new entry to the symbol mapping dictionary with provider-specific formats, then the translator immediately recognizes and handles that symbol

6.3 Given the mapping dictionary is implemented in C# code, when I add or modify mappings, then no configuration file changes or database updates are required

## Requirements

### Functional Requirements

1. The system shall accept market symbols in any recognized format (canonical, Yahoo format, or alternative provider formats)

2. The system shall maintain a comprehensive mapping of symbol formats for each supported provider, stored as C# constants/dictionary

3. The system shall translate input symbols to the correct format for the target provider before making API calls

4. The system shall support the following translation scenarios:
   - Canonical name → Provider-specific format
   - Provider-specific format → Same provider format (pass-through)
   - Alternative provider format → Target provider format

5. The system shall include mappings for at minimum these index categories:
   - **US Market Indices**: VIX, GSPC (S&P 500), DJI (Dow Jones), IXIC (NASDAQ Composite), RUT (Russell 2000), NDX (NASDAQ-100), NYA (NYSE Composite), OEX (S&P 100), MID (S&P 400)
   - **International Indices**: FTSE (UK), GDAXI (DAX Germany), N225 (Nikkei 225), HSI (Hang Seng), SSEC (Shanghai Composite), AXJO (ASX Australia), KS11 (KOSPI Korea), BSESN (BSE Sensex India)
   - **Sector/Commodity Indices**: SOX (PHLX Semiconductor), XOI (AMEX Oil), HUI (Gold Bugs), XAU (Philadelphia Gold/Silver)
   - **Volatility Indices**: VIX (CBOE Volatility), VXN (NASDAQ-100 Volatility), RVX (Russell 2000 Volatility)
   - **Bond Indices**: TNX (10-Year Treasury Yield), TYX (30-Year Treasury Yield), FVX (5-Year Treasury Yield), IRX (13-Week Treasury Bill)

6. The system shall pass through unrecognized symbols unchanged to the provider (allow provider to handle validation)

7. The system shall maintain backward compatibility with existing queries that use Yahoo-specific format (e.g., "^VIX")

8. The system shall provide a structure that supports multiple provider formats simultaneously (Yahoo, FinViz, and future providers)

9. The system shall NOT change routing logic—translation happens after the router selects the provider

### Non-Functional Requirements

- **Performance**: Symbol translation shall add no more than 1ms overhead per query (dictionary lookup)
- **Maintainability**: Adding a new symbol mapping shall require only a single new entry in the mapping dictionary
- **Extensibility**: Adding a new provider shall require only adding that provider's format key to existing symbol mappings
- **Memory**: The symbol mapping dictionary shall consume less than 100KB of memory
- **Reliability**: Translation logic shall have 100% test coverage with unit tests for all supported symbols
- **Backward Compatibility**: All existing queries using Yahoo format (^SYMBOL) shall continue to work without modification

## Acceptance Criteria

### Critical (Blocking)

- [ ] **AC1**: Symbol translator accepts canonical index names (VIX, GSPC, DJI, etc.) and correctly translates them to Yahoo Finance format (^VIX, ^GSPC, ^DJI)
  - **Evidence**: Unit tests demonstrating successful translation and integration tests showing successful data retrieval
  - **Pass Condition**: All canonical names in the requirements list translate correctly

- [ ] **AC2**: Symbol translator accepts Yahoo Finance format symbols (^VIX, ^GSPC) and passes them through unchanged
  - **Evidence**: Unit tests showing pass-through behavior
  - **Pass Condition**: No double-translation occurs (^VIX does not become ^^VIX)

- [ ] **AC3**: All major US market indices listed in requirements are included in the symbol mapping
  - **Evidence**: Code review of mapping dictionary showing all required indices
  - **Pass Condition**: VIX, GSPC, DJI, IXIC, RUT, NDX, NYA, OEX, MID are all present with Yahoo mappings

- [ ] **AC4**: Symbol translator correctly handles cross-provider format translation (e.g., @VX → ^VIX for Yahoo provider)
  - **Evidence**: Unit tests demonstrating conversion from alternative provider formats
  - **Pass Condition**: At least 3 example cross-provider translations work correctly

- [ ] **AC5**: Unrecognized symbols (regular stocks, ETFs) are passed through unchanged to the provider
  - **Evidence**: Unit tests showing AAPL, MSFT, SPY pass through without translation
  - **Pass Condition**: No exceptions thrown, symbols remain unchanged

- [ ] **AC6**: Translation layer integrates with router and occurs after provider selection
  - **Evidence**: Integration tests showing router selects provider, then translation happens
  - **Pass Condition**: Translation only occurs for the selected provider's format

- [ ] **AC7**: Backward compatibility maintained—existing queries using Yahoo format continue to work
  - **Evidence**: Integration tests using ^VIX, ^GSPC successfully retrieve data
  - **Pass Condition**: Zero breaking changes to existing API consumers

### Important (Non-Blocking)

- [ ] **AC8**: Symbol mapping includes international indices (FTSE, GDAXI, N225, HSI, SSEC, AXJO, KS11, BSESN)
  - **Evidence**: Code review of mapping dictionary
  - **Pass Condition**: All international indices from requirements are present
  - **Waiver Allowed**: Can be added in a follow-up PR if initial scope is too large

- [ ] **AC9**: Symbol mapping includes sector/commodity indices (SOX, XOI, HUI, XAU)
  - **Evidence**: Code review of mapping dictionary
  - **Pass Condition**: All sector/commodity indices from requirements are present
  - **Waiver Allowed**: Can be added in a follow-up PR if initial scope is too large

- [ ] **AC10**: Symbol mapping includes bond indices (TNX, TYX, FVX, IRX)
  - **Evidence**: Code review of mapping dictionary
  - **Pass Condition**: All bond indices from requirements are present
  - **Waiver Allowed**: Can be added in a follow-up PR if initial scope is too large

- [ ] **AC11**: Documentation includes examples of all supported translation scenarios
  - **Evidence**: README or inline code documentation with examples
  - **Pass Condition**: Canonical→Yahoo, Yahoo→Yahoo, AltProvider→Yahoo examples documented

- [ ] **AC12**: Translation performance overhead is under 1ms per query
  - **Evidence**: Performance benchmark tests
  - **Pass Condition**: Average translation time < 1ms over 10,000 iterations

### Design Requirements (Architectural)

- [ ] **AC13**: Symbol mapping is stored as C# dictionary/constants (not config file)
  - **Evidence**: Code review showing in-memory dictionary implementation
  - **Pass Condition**: No JSON/XML config files, no database lookups

- [ ] **AC14**: Mapping structure supports multiple provider formats per symbol
  - **Evidence**: Code review showing structure like `Dictionary<string, Dictionary<ProviderType, string>>`
  - **Pass Condition**: Can represent Yahoo, FinViz, and future provider formats simultaneously

- [ ] **AC15**: Design supports adding new providers without modifying existing translation logic
  - **Evidence**: Architecture review, code structure allows adding new provider enum values
  - **Pass Condition**: Adding "GoogleFinance" provider requires only updating mapping dictionary, not core translation logic

## Out of Scope

The following items are explicitly NOT included in this feature:

- **Non-Yahoo provider implementations**: Only Yahoo Finance format translations are required; FinViz and other providers are design considerations only
- **Real-time symbol validation**: The translator does not verify if symbols exist or are valid; that is the provider's responsibility
- **Symbol search/suggestion**: No autocomplete or "did you mean?" functionality
- **User-defined symbol mappings**: Users cannot add custom symbol translations
- **Configuration UI**: No admin interface for managing symbol mappings
- **Symbol aliases beyond provider formats**: No support for user-created nicknames (e.g., "SPX" as alias for "GSPC")
- **Historical symbol mappings**: No support for symbols that have changed over time (e.g., company name changes)
- **Sector/exchange routing**: Translation does not route based on symbol type or exchange
- **Database storage**: All mappings are in-memory C# code, no persistence layer
- **Dynamic symbol loading**: Mappings are compile-time, not loaded from external sources

## Dependencies

### Internal Dependencies
- **Stock Data Router**: The symbol translator receives the selected provider from the router; translation happens after routing decision
- **Yahoo Finance Provider**: The translated symbols are passed to the Yahoo provider implementation
- **MCP Server Interface**: Symbol translation must occur before MCP tools call underlying providers

### External Dependencies
- None (all functionality is internal)

### Blocks
- This feature blocks future multi-provider support (Google Finance, FinViz, etc.) as it provides the translation infrastructure

## Technical Considerations

### Data Structure Design
- Use a two-level dictionary: `Dictionary<string, Dictionary<ProviderType, string>>`
  - First level: Canonical symbol name (key)
  - Second level: Provider → Provider-specific format
  - Example: `["VIX"]["Yahoo"] = "^VIX"`, `["VIX"]["FinViz"] = "@VX"`
- This structure allows O(1) lookup performance and easy extensibility

### Provider Enumeration
- Define a `ProviderType` enum (e.g., `Yahoo`, `FinViz`, `GoogleFinance`)
- Use enum as dictionary key for type safety and compile-time checking

### Translation Algorithm
1. Identify input symbol format (canonical, Yahoo, or alternative)
2. Look up canonical name in mapping dictionary
3. Retrieve the target provider's format from the nested dictionary
4. Return translated symbol

### Reverse Mapping Support
- Some inputs may already be in provider-specific format (e.g., "^VIX")
- Need reverse lookup to find canonical name, then forward lookup to target provider
- Consider creating a secondary reverse-lookup dictionary for performance

### Regular Stock Symbol Handling
- Symbols not in the mapping dictionary are passed through unchanged
- This handles regular stocks (AAPL, MSFT), ETFs (SPY, QQQ), and other securities

### Error Handling Philosophy
- Translator is permissive: unknown symbols pass through unchanged
- Provider handles validation and returns appropriate errors
- Only truly malformed input (null, empty string) should throw exceptions in the translator

### Memory and Performance
- Entire mapping dictionary loaded at startup (lazy initialization acceptable)
- Dictionary size estimated at ~100 symbols × 3 providers × 10 bytes = ~3KB
- Lookup performance: O(1) for both forward and reverse translation

### Testing Strategy
- Unit tests for each symbol in the mapping (canonical → Yahoo)
- Unit tests for pass-through scenarios (Yahoo → Yahoo)
- Unit tests for cross-provider scenarios (FinViz → Yahoo)
- Integration tests with actual router and provider
- Performance benchmarks to ensure < 1ms overhead

## Implementation Phases

### Phase 1: MVP - Core Translation for Yahoo Finance
**Goal**: Support canonical and Yahoo format symbols for major US indices

**Deliverables**:
- Symbol mapping dictionary with US market indices (VIX, GSPC, DJI, IXIC, RUT, NDX, NYA, OEX, MID)
- Translation logic (canonical → Yahoo, Yahoo pass-through)
- Integration with existing Yahoo Finance provider
- Unit tests for core translation scenarios
- Integration tests with router

**Success Criteria**:
- AC1, AC2, AC3, AC5, AC6, AC7 pass
- All existing Yahoo queries continue to work

### Phase 2: Extended Symbol Coverage
**Goal**: Add international, sector, and bond indices

**Deliverables**:
- Add international indices to mapping (FTSE, GDAXI, N225, HSI, SSEC, AXJO, KS11, BSESN)
- Add sector/commodity indices (SOX, XOI, HUI, XAU)
- Add bond indices (TNX, TYX, FVX, IRX)
- Add volatility indices (VXN, RVX)
- Unit tests for all new symbols

**Success Criteria**:
- AC8, AC9, AC10 pass
- All major index categories covered

### Phase 3: Multi-Provider Infrastructure
**Goal**: Prepare for future provider additions (design only, no implementation)

**Deliverables**:
- Add FinViz format mappings to dictionary (for future use)
- Implement cross-provider translation logic (e.g., @VX → ^VIX)
- Update documentation with multi-provider examples
- Performance benchmarks

**Success Criteria**:
- AC4, AC12, AC14, AC15 pass
- System ready for Google Finance or FinViz provider addition

## Success Metrics

### Functional Success
- **Translation Accuracy**: 100% of mapped symbols translate correctly (verified by unit tests)
- **Backward Compatibility**: 0 breaking changes to existing Yahoo Finance queries
- **Symbol Coverage**: At least 30 major indices mapped in Phase 1, 50+ by end of Phase 2

### Performance Success
- **Translation Overhead**: < 1ms per symbol translation (measured via benchmarks)
- **Memory Footprint**: < 100KB for complete symbol mapping dictionary
- **Lookup Speed**: O(1) constant-time dictionary lookups

### User Experience Success
- **Format Flexibility**: Users can query with canonical names (VIX) or provider formats (^VIX) interchangeably
- **Error Clarity**: Invalid symbols produce clear, actionable error messages from provider

### Technical Success
- **Test Coverage**: 100% code coverage for translation logic
- **Extensibility**: Adding a new provider requires < 10 lines of code per symbol
- **Maintainability**: Adding a new symbol requires exactly 1 dictionary entry

## Work Tracking

### GitHub Issues

**Epic Issue**: Symbol Translation System
- Label: `enhancement`, `symbol-translation`

**Phase 1 Issues**:
1. **Create Symbol Mapping Dictionary** (`enhancement`, `p1-mvp`)
   - Define ProviderType enum
   - Create two-level dictionary structure
   - Add US market indices (VIX, GSPC, DJI, IXIC, RUT, NDX, NYA, OEX, MID)

2. **Implement Translation Logic** (`enhancement`, `p1-mvp`)
   - Canonical → Provider format translation
   - Provider format pass-through detection
   - Unknown symbol pass-through

3. **Integrate with Router** (`enhancement`, `p1-mvp`)
   - Inject translator between router and provider
   - Pass selected provider type to translator

4. **Unit Tests - Core Translation** (`testing`, `p1-mvp`)
   - Test each mapped symbol (canonical → Yahoo)
   - Test pass-through scenarios
   - Test unknown symbol handling

5. **Integration Tests** (`testing`, `p1-mvp`)
   - End-to-end tests with router + translator + Yahoo provider
   - Backward compatibility tests

**Phase 2 Issues**:
6. **Extend Symbol Coverage - International** (`enhancement`, `p2-extended`)
   - Add FTSE, GDAXI, N225, HSI, SSEC, AXJO, KS11, BSESN

7. **Extend Symbol Coverage - Sector/Bond** (`enhancement`, `p2-extended`)
   - Add SOX, XOI, HUI, XAU, TNX, TYX, FVX, IRX

8. **Unit Tests - Extended Coverage** (`testing`, `p2-extended`)
   - Test all newly added symbols

**Phase 3 Issues**:
9. **Multi-Provider Infrastructure** (`enhancement`, `p3-future`)
   - Add FinViz format mappings (for future use)
   - Implement cross-provider translation

10. **Performance Benchmarks** (`testing`, `p3-future`)
    - Measure translation overhead
    - Verify < 1ms requirement

11. **Documentation** (`documentation`)
    - Update README with translation examples
    - Document supported symbols and formats

### Milestone
**Milestone**: Symbol Translation v1.0
- Target: Q2 2026
- Required Deliverables:
  - All Phase 1 items (MVP)
  - AC1-AC7 passing (critical acceptance criteria)
  - Integration tests passing
  - Documentation updated
