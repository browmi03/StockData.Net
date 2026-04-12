# Test Strategy: Issue #51 Social Media Data Sources (X/Twitter Provider)

## Document Info

- **Feature Spec**: [docs/features/issue-51-social-media-x-provider.md](../features/issue-51-social-media-x-provider.md)
- **Architecture**: [docs/architecture/provider-selection-architecture.md](../architecture/provider-selection-architecture.md)
- **Related Security Context**: [docs/security/security-summary.md](../security/security-summary.md)
- **Status**: Draft
- **Last Updated**: 2026-04-12

---

## Test Strategy Overview

Issue #51 introduces a new social media capability through the `get_social_feed` MCP tool and an `XTwitterProvider` implementation under a new `SocialMedia` provider category. This strategy enforces a unit-first pyramid, full Given/When/Then traceability, explicit security/rate-limit testing, and regression coverage for existing MCP tools.

Primary quality goals:

- 100% mapping of feature-spec GWT scenarios (1.1 through 5.4) to test cases
- 100% coverage of blocking validation/security/routing paths
- No live X API calls in CI for deterministic and secure pipelines
- No credential leakage in logs, errors, or tool responses

---

## Scope

### In Scope

- `get_social_feed` tool registration, parameter validation, provider dispatch, and response contract
- `XTwitterProvider` behavior with mocked `IXTwitterClient`/`HttpClient`
- `SocialPost` model mapping and sort order guarantees
- Provider selection routing for `SocialMedia` category
- Rate limit, authentication, and missing-credential handling
- Regression protection for `get_stock_data`, `get_market_events`, and `list_providers`

### Out of Scope

- Social sentiment scoring or NLP enrichment
- Real-time streaming/websocket delivery
- New providers beyond X in this issue
- Production load testing against live X API in CI

---

## Test Levels

### Unit Tests

- **Coverage goal**: >=90% line coverage for new social feed domain logic
- **Framework**: xUnit
- **Mocking strategy**: NSubstitute for provider/client boundaries (`IXTwitterClient`, provider selector, logger), plus fake `HttpMessageHandler` where HTTP-level assertions are needed
- **Primary focus**:
  - `XTwitterProvider` happy path and error behavior
  - Input validation (`handles`, `query`, `max_results`, `lookback_hours`)
  - `SocialPost` mapping and sorting
  - Credential-loading and error sanitization

### Integration Tests (Mocked External)

- **Coverage goal**: 100% for `get_social_feed` handler contract and provider routing path
- **Framework**: xUnit
- **Environment**: local + CI using only test doubles (record/playback fixtures or in-memory mock HTTP server)
- **Primary focus**:
  - End-to-end MCP tool invocation for `get_social_feed`
  - Provider selection routing to `XTwitterProvider` for `SocialMedia` category
  - Cached response behavior in repeated identical requests

### Integration Tests (Live External, Optional)

- **Coverage goal**: smoke-only confidence against live X API
- **Framework**: xUnit
- **Execution policy**: tagged `[IntegrationTest]` + `[RequiresCredentials]`; excluded from standard CI; run manually or in credentialed nightly pipeline only

### Security Tests

- **Coverage goal**: 100% for secret handling and hostile-input validation paths
- **Framework**: xUnit
- **Primary focus**:
  - Missing `X_BEARER_TOKEN` handling
  - Token redaction from errors/logs/response
  - Injection-character input rejection

### Regression Tests

- **Coverage goal**: 100% pass for impacted existing tools
- **Framework**: existing project test framework with xUnit migration-compatible assertions where applicable
- **Primary focus**:
  - `get_stock_data` unaffected by social provider category addition
  - `get_market_events` unaffected by routing/category extension
  - `list_providers` still reports financial providers and now includes social providers with correct category labels

---

## Given/When/Then Scenario Coverage (Traceability Matrix)

| Spec Scenario | Description | Test Case ID(s) | Test Level | Status |
| --- | --- | --- | --- | --- |
| 1.1 | Single-handle social feed returns sorted `SocialPost` fields | TC-U-001, TC-I-001 | Unit, Integration | Planned |
| 1.2 | Multi-handle request merges posts and preserves `author_handle` | TC-U-002 | Unit | Planned |
| 1.3 | No posts in lookback returns `[]` + informative message | TC-U-003 | Unit | Planned |
| 1.4 | Non-existent handle returns per-handle structured error isolation | TC-U-004 | Unit | Planned |
| 1.5 | Missing credentials returns structured safe error with no leak | TC-S-001 | Security | Planned |
| 2.1 | Query by ticker (`$AAPL`) returns only matching posts | TC-U-005 | Unit | Planned |
| 2.2 | Query by keyword returns matching posts and `matched_keywords` | TC-U-006 | Unit | Planned |
| 2.3 | Combined handle + query applies AND logic | TC-U-007 | Unit | Planned |
| 2.4 | Query with no matches returns `[]` + no-match message | TC-U-008 | Unit | Planned |
| 2.5 | Blank query is rejected before X API call | TC-U-009 | Unit | Planned |
| 3.1 | Request within quota succeeds with normal response | TC-U-010 | Unit | Planned |
| 3.2 | 429 rate-limit returns investor-friendly structured error | TC-U-011, TC-S-003 | Unit, Security | Planned |
| 3.3 | Repeated identical request returns cached response with `cached_at` | TC-U-012, TC-I-002 | Unit, Integration | Planned |
| 3.4 | Upstream 5xx returns sanitized user-facing error + server diagnostics | TC-U-013 | Unit | Planned |
| 4.1 | `ISocialMediaProvider` contract implemented and mapping verified | TC-U-014 | Unit | Planned |
| 4.2 | Additional provider can be DI-registered without tool handler changes | TC-I-003 | Integration | Planned |
| 4.3 | Provider unrecoverable failure returns structured provider failure message | TC-U-015 | Unit | Planned |
| 4.4 | Social requests route only to social providers; stock tool remains financial-only | TC-I-004, TC-R-001 | Integration, Regression | Planned |
| 5.1 | Startup loads token from environment without logging it | TC-U-016, TC-S-004 | Unit, Security | Planned |
| 5.2 | 401 auth failures never expose token in user/server output | TC-U-017, TC-S-002 | Unit, Security | Planned |
| 5.3 | Repository secret scan finds no hardcoded X credentials | TC-S-005 | Security | Planned |
| 5.4 | Invalid token returns authentication-failed guidance without echo | TC-U-018 | Unit | Planned |

Traceability result target: **22/22 GWT scenarios covered (100%)**.

---

## Additional Acceptance and Validation Coverage

These tests are required by Issue #51 acceptance criteria and key requirements, even where not explicitly listed as standalone GWT scenarios.

| Requirement Focus | Test Case ID(s) | Level |
| --- | --- | --- |
| Invalid handle containing spaces is rejected | TC-U-019 | Unit |
| Invalid handle with disallowed special characters is rejected | TC-U-020 | Unit |
| Invalid handle length (>15 chars) is rejected | TC-U-021 | Unit |
| Empty `handles` + empty/absent `query` rejected, no X API call | TC-U-022 | Unit |
| `max_results` out of range is rejected | TC-U-023 | Unit |
| `SocialPost` mapping contract from X API response object is correct | TC-U-024 | Unit |
| Provider selection routes `SocialMedia` category to `XTwitterProvider` | TC-I-005 | Integration |
| `list_providers` includes category labels and no regressions | TC-R-003 | Regression |

---

## Test Case Catalog

### Unit Test Cases

- **TC-U-001**: `GivenSingleValidHandle_WhenFetchingSocialFeed_ThenReturnsMappedPostsSortedByPostedAtDesc`
- **TC-U-002**: `GivenMultipleHandles_WhenFetchingSocialFeed_ThenReturnsMergedChronologicalFeedWithAuthorHandles`
- **TC-U-003**: `GivenNoPostsInWindow_WhenFetchingSocialFeed_ThenReturnsEmptyListWithNoPostsMessage`
- **TC-U-004**: `GivenOneInvalidHandleAmongValidHandles_WhenFetchingSocialFeed_ThenReturnsPerHandleErrorWithoutFailingValidHandles`
- **TC-U-005**: `GivenTickerQuery_WhenFetchingSocialFeed_ThenReturnsOnlyCashtagMatches`
- **TC-U-006**: `GivenKeywordQuery_WhenFetchingSocialFeed_ThenReturnsOnlyKeywordMatchesWithMatchedKeywords`
- **TC-U-007**: `GivenHandlesAndQuery_WhenFetchingSocialFeed_ThenAppliesConjunctiveFiltering`
- **TC-U-008**: `GivenQueryWithNoMatches_WhenFetchingSocialFeed_ThenReturnsEmptyListWithNoMatchesMessage`
- **TC-U-009**: `GivenBlankQuery_WhenValidatingRequest_ThenReturnsValidationErrorAndSkipsApiCall`
- **TC-U-010**: `GivenRateLimitBudgetAvailable_WhenFetchingSocialFeed_ThenReturnsNormalResponse`
- **TC-U-011**: `GivenXApiReturns429_WhenFetchingSocialFeed_ThenReturnsStructuredRateLimitMessage`
- **TC-U-012**: `GivenCachedIdenticalRequest_WhenFetchingSocialFeedAgain_ThenReturnsCachedDataWithCachedAt`
- **TC-U-013**: `GivenXApiReturns5xx_WhenFetchingSocialFeed_ThenReturnsSanitizedProviderError`
- **TC-U-014**: `GivenXProvider_WhenMappingApiObjects_ThenImplementsISocialMediaProviderAndProducesSocialPost`
- **TC-U-015**: `GivenProviderFailure_WhenFetchingSocialFeed_ThenReturnsStructuredProviderFailure`
- **TC-U-016**: `GivenBearerTokenInEnvironment_WhenInitializingProvider_ThenLoadsTokenWithoutLogging`
- **TC-U-017**: `GivenXApiReturns401_WhenFetchingSocialFeed_ThenReturnsAuthFailureWithoutTokenLeak`
- **TC-U-018**: `GivenMalformedOrExpiredToken_WhenFetchingSocialFeed_ThenReturnsCredentialGuidanceWithoutEcho`
- **TC-U-019**: `GivenHandleContainsWhitespace_WhenValidatingHandles_ThenReturnsValidationError`
- **TC-U-020**: `GivenHandleContainsDisallowedCharacters_WhenValidatingHandles_ThenReturnsValidationError`
- **TC-U-021**: `GivenHandleLengthGreaterThan15_WhenValidatingHandles_ThenReturnsValidationError`
- **TC-U-022**: `GivenNoHandlesAndNoQuery_WhenValidatingRequest_ThenReturnsValidationErrorAndSkipsApiCall`
- **TC-U-023**: `GivenMaxResultsOutsideRange_WhenValidatingRequest_ThenReturnsValidationError`
- **TC-U-024**: `GivenRawXApiResponse_WhenMappingToSocialPost_ThenAllRequiredFieldsAndUtcFormatAreCorrect`

### Integration Test Cases (Mocked X API)

- **TC-I-001**: `GivenMockXApiServer_WhenCallingGetSocialFeed_ThenMcpReturnsExpectedSocialPostPayload`
- **TC-I-002**: `GivenMockedCacheAndRepeatedRequest_WhenCallingGetSocialFeedTwice_ThenSecondResponseIsCached`
- **TC-I-003**: `GivenAdditionalISocialMediaProviderRegistered_WhenCallingGetSocialFeed_ThenHandlerRequiresNoCoreChange`
- **TC-I-004**: `GivenFinancialAndSocialProvidersConfigured_WhenCallingGetSocialFeed_ThenOnlySocialProvidersAreInvoked`
- **TC-I-005**: `GivenProviderCategorySocialMedia_WhenSelectingProvider_ThenRoutesToXTwitterProvider`

### Security Test Cases

- **TC-S-001**: `GivenXBearerTokenMissing_WhenCallingGetSocialFeed_ThenReturnsGracefulCredentialsNotConfiguredError`
- **TC-S-002**: `Given401FromXApi_WhenHandlingError_ThenTokenNeverAppearsInLogsOrResponses`
- **TC-S-003**: `Given429FromXApi_WhenHandlingRateLimit_ThenNoHttpInternalsOrCredentialFragmentsLeak`
- **TC-S-004**: `GivenProviderStartup_WhenLoadingToken_ThenEnvironmentOnlySourceIsUsedAndNeverDumped`
- **TC-S-005**: `GivenRepositorySecretScan_WhenRunningSecurityChecks_ThenNoHardcodedXSecretsFound`
- **TC-S-006**: `GivenInjectionCharactersInHandleOrQuery_WhenValidatingInput_ThenRequestIsRejectedCleanly`

### Regression Test Cases

- **TC-R-001**: `GivenSocialMediaCategoryAdded_WhenCallingGetStockData_ThenOnlyFinancialProvidersAreUsed`
- **TC-R-002**: `GivenSocialMediaCategoryAdded_WhenCallingGetMarketEvents_ThenBehaviorRemainsUnchanged`
- **TC-R-003**: `GivenSocialMediaCategoryAdded_WhenCallingListProviders_ThenFinancialAndSocialProvidersRenderWithCorrectCategories`

---

## Test Data Strategy

### Mock X API Payloads

- Fixture JSON files (no live calls):
  - `x_posts_single_handle_success.json`
  - `x_posts_multi_handle_success.json`
  - `x_posts_empty_window.json`
  - `x_error_401.json`
  - `x_error_429_with_reset.json`
  - `x_error_503.json`
- Response fixtures include both valid and hostile/untrusted content samples to validate mapping and sanitization behavior.

### Data Creation and Isolation

- Deterministic fixtures checked into test assets; no random test data in CI-critical suites
- Each test constructs fresh request objects and substitutes; no shared mutable state
- Cache tests use isolated in-memory cache instances with deterministic clocks

### Cleanup

- No persistent writes; test artifacts are in-memory only
- Mock HTTP servers are disposed per test fixture

---

## CI/CD Integration and Test Gate Policy

### Pipeline Stages

1. **PR Required Gate**
   - Unit tests (`TC-U-*`)
   - Security tests (`TC-S-*`, excluding optional live secret scan tooling if unavailable in runner image)
   - Mocked integration tests (`TC-I-*` using fake server/fixtures)
   - Regression tests (`TC-R-*`)

2. **Credentialed Optional Stage**
   - Live X API tests tagged `[IntegrationTest]` + `[RequiresCredentials]`
   - Runs only when explicit opt-in and valid credentials are provided

### Gate Rules

- 100% pass required for all required-gate suites
- 100% GWT traceability required (22/22 scenarios mapped)
- No live X API calls permitted in default CI runs
- Any test invoking live network without live-test tags fails pipeline policy
- Security failures (token leak, missing-env crash, unsanitized error) are release blockers

### Live Call Policy

- CI default: `X_LIVE_TESTS=false` and no `X_BEARER_TOKEN` injected into standard test jobs
- Live tests run only in dedicated, explicitly credentialed workflow
- Live test failures do not block PR unless release manager promotes them to blocking for a release cut

---

## Coverage Targets

| Metric | Target |
| --- | --- |
| Unit line coverage (new social-feed logic) | >= 90% |
| Unit branch coverage (validation + error paths) | >= 80% |
| GWT scenario coverage (1.1-5.4) | 100% |
| Blocking validation/security/routing path coverage | 100% |
| Regression suite pass rate (`get_stock_data`, `get_market_events`, `list_providers`) | 100% |

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
| --- | --- | --- |
| X API contract drift (payload schema changes) | Mapping regressions | Keep fixture snapshots versioned and add contract tests around required fields and parsing fallbacks |
| Rate-limit behavior variability | Flaky tests | Use mocked 429 fixtures in CI and isolate live checks to optional stage |
| Secret leakage via exception messages | Security incident | Dedicated redaction tests (`TC-S-002`, `TC-S-003`) and log sink assertions |
| Provider-category routing regression | Existing tool breakage | Mandatory regression tests (`TC-R-001` to `TC-R-003`) on every PR |

---

## Related Documents

- Feature Specification: [docs/features/issue-51-social-media-x-provider.md](../features/issue-51-social-media-x-provider.md)
- Existing Test Strategy Baselines:
  - [docs/testing/testing-summary.md](./testing-summary.md)
  - [docs/testing/issue-32-tier-handling-test-strategy.md](./issue-32-tier-handling-test-strategy.md)
  - [docs/testing/issue-26-market-moving-events-test-strategy.md](./issue-26-market-moving-events-test-strategy.md)
- Template: [docs/templates/test-strategy.md](../templates/test-strategy.md)
