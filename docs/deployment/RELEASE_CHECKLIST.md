# Release Checklist

## Document Info

- **Type**: Operational Runbook — Release
- **Version**: 1.0.0
- **Purpose**: Ensure each release of StockData.Net MCP Server is thoroughly validated, tested, and deployed with minimal risk
- **Architecture**: [Canonical Architecture](../architecture/stock-data-aggregation-canonical-architecture.md)
- **Security**: [Security Summary](../security/security-summary.md)
- **Related Runbooks**: [Deployment Guide](./DEPLOYMENT_GUIDE.md) · [GitHub Secrets Validation](./GITHUB_SECRETS_VALIDATION.md)
- **Status**: Approved
- **Last Updated**: 2026-03-09

---

## Release Feature: Provider Selection

**Release Version:** v1.1.0  
**Feature Status:** ✅ Released  
**Release Date:** 2026-03-09

### Overview

The Provider Selection feature enables users to explicitly choose financial data providers (Yahoo Finance, Finnhub, Alpha Vantage) through an optional `provider` parameter on all MCP tools. This provides visibility, control, and cost tracking for data sources.

### Feature Availability

**Supported in:** StockData.Net MCP Server v1.1.0+  
**MCP Tools Updated:** All 10 tools now support the optional `provider` parameter:

1. `get_historical_stock_prices`
2. `get_stock_info`
3. `get_finance_news`
4. `get_market_news`
5. `get_stock_actions`
6. `get_financial_statement`
7. `get_holder_info`
8. `get_option_expiration_dates`
9. `get_option_chain`
10. `get_recommendations`

### Breaking Changes

**None.** Provider Selection is fully backward compatible:

- ✅ The `provider` parameter is **optional** on all tools
- ✅ Existing clients work without changes
- ✅ Default provider behavior matches previous routing logic
- ✅ Response format is unchanged (metadata is additive)

### Configuration Changes

**Optional Configuration Updates:**

The feature works out-of-the-box with **no configuration changes required**. Default configuration includes:

- **Aliases**: `yahoo`, `yahoo finance`, `alphavantage`, `alpha vantage`, `finnhub`
- **Default Provider**: Yahoo Finance for all data types
- **Provider Tiers**: All providers set to `"free"` tier

**To customize configuration**, update `appsettings.json`:

```json
{
  "providerSelection": {
    "aliases": {
      "yahoo": "yahoo_finance",
      "alphavantage": "alphavantage",
      "finnhub": "finnhub"
    },
    "defaultProvider": {
      "StockInfo": "yahoo_finance",
      "HistoricalPrices": "yahoo_finance"
    }
  },
  "providers": [
    {
      "id": "yahoo_finance",
      "tier": "free"
    }
  ]
}
```

See [Configuration Reference](provider-selection-configuration.md) for full details.

### New MCP Tool Parameters

All 10 tools now accept an optional `provider` parameter:

**Parameter Schema:**

| Parameter | Type | Required | Valid Values | Description |
| --- | --- | --- | --- | --- |
| `provider` | string | No | `yahoo`, `alphavantage`, `finnhub` | Explicitly select a data provider. Aliases supported. |

**Example:**

```json
{
  "name": "get_stock_info",
  "arguments": {
    "ticker": "AAPL",
    "provider": "yahoo"
  }
}
```

### New Response Metadata Fields

All successful responses now include metadata identifying the provider and tier:

**Metadata Schema:**

```json
{
  "_meta": {
    "serviceKey": "yahoo",
    "tier": "free"
  }
}
```

| Field | Type | Description | Example Values |
| --- | --- | --- | --- |
| `serviceKey` | string | Provider that fulfilled the request | `"yahoo"`, `"finnhub"`, `"alphavantage"` |
| `tier` | string | Provider cost tier | `"free"`, `"paid"` |

### Validation Behavior at Startup

**Startup Validation:**

The MCP server validates provider selection configuration at startup:

- ✅ All alias targets must reference valid provider IDs
- ✅ All default provider values must reference enabled providers
- ✅ Provider tier values must be `"free"` or `"paid"`
- ✅ Provider names must be alphanumeric with underscores (max 50 chars)

**If validation fails**, the server logs an error and **exits** with a configuration error message.

**Example validation error:**

```
Configuration error: Alias 'yahoo' references unknown provider 'yahoo_finance_v2'.
Valid provider IDs: yahoo_finance, finnhub, alphavantage
```

### Supported Providers and Aliases

**Providers:**

| Provider | ID | Aliases | Tier |
| --- | --- | --- | --- |
| Yahoo Finance | `yahoo_finance` | `yahoo`, `yahoo finance` | Free |
| Finnhub | `finnhub` | `finnhub` | Free |
| Alpha Vantage | `alphavantage` | `alphavantage`, `alpha vantage` | Free |

**Aliases are case-insensitive:** `"Yahoo"`, `"yahoo"`, and `"YAHOO"` all resolve to Yahoo Finance.

### Testing & Validation

**Pre-Release Testing Completed:**

- ✅ Unit tests for provider validation logic (100% coverage)
- ✅ Integration tests for all 10 tools with explicit provider selection
- ✅ Error handling tests for invalid/unavailable providers
- ✅ Metadata enrichment tests for all response types
- ✅ Configuration validation tests at startup
- ✅ Backward compatibility tests (no provider parameter)

### Documentation

**Documentation Available:**

1. **[User Guide](../features/provider-selection-user-guide.md)** — End-user documentation for using provider selection
2. **[Developer Integration Guide](../architecture/provider-selection-integration-guide.md)** — API integration guide for developers
3. **[Configuration Reference](provider-selection-configuration.md)** — Admin guide for configuration
4. **[API Documentation](../architecture/provider-selection-api.md)** — Complete API reference for all tools
5. **[Feature Specification](../features/provider-selection.md)** — Complete feature requirements
6. **[Architecture Overview](../architecture/provider-selection-architecture.md)** — Technical architecture and design
7. **[Security Design](../security/provider-selection-security.md)** — Security considerations

### Migration Guide

**No migration required.** Provider Selection is fully backward compatible.

**Optional Enhancements for AI Clients:**

1. Update AI prompts to detect provider intent in user requests
2. Parse `_meta` field from responses to display provider source
3. Add user-facing provider selection options

See [Integration Guide](../architecture/provider-selection-integration-guide.md) for details.

### Known Limitations

- **Multi-provider aggregation not supported** — Each request routes to a single provider
- **No automatic provider recommendation** — Users must choose provider explicitly
- **Explicit selection bypasses failover** — No automatic fallback when explicitly selecting a provider

### Release Notes Summary

**Added:**

- ✅ Optional `provider` parameter on all 10 MCP tools
- ✅ Response metadata with `serviceKey` and `tier` fields
- ✅ Configuration schema for provider aliases and defaults
- ✅ Runtime validation for provider names
- ✅ Clear error messages for invalid/unavailable providers

**Changed:**

- ✅ All MCP tool schemas updated with `provider` parameter
- ✅ All responses now include `_meta` object

**Fixed:**

- None (new feature)

**Security:**

- ✅ Provider name input sanitization and validation
- ✅ API keys protected (never exposed in error messages)
- ✅ Rate limiting enforced regardless of provider selection mode

---

## Pre-Release Phase

### 1. Code Readiness

- [ ] All feature branches merged to `develop` branch
- [ ] Code review completed for all changes since last release
- [ ] No open blocker or critical issues in issue tracker
- [ ] All merge commits contain meaningful messages

**Validation:**
```bash
# Verify no uncommitted changes
git status

# Review commits since last tag
git log v[PREVIOUS_VERSION]..HEAD --oneline
```

### 2. Tag Format Validation

**Before pushing tags, verify the format:**

- [ ] **Stable tags** match pattern: `vMAJOR.MINOR.PATCH` (e.g., `v1.0.0`, `v1.1.2`)
- [ ] **Preview tags** match pattern: `vMAJOR.MINOR.PATCH-(alpha|beta|rc).N` (e.g., `v1.1.0-rc.1`, `v1.0.1-beta.2`)
- [ ] Semantic versioning rules followed:
  - MAJOR bump: Breaking changes to API or MCP server interface
  - MINOR bump: New features (backward compatible)
  - PATCH bump: Bug fixes (backward compatible)
  - Pre-release suffix: For testing or validation releases

**Validation Script:**
```bash
# Verify tag format before pushing
TAG="v1.0.0"

# For stable releases
if [[ ! "$TAG" =~ ^v[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
  echo "ERROR: Tag does not match stable format (vMAJOR.MINOR.PATCH)"
  exit 1
fi

# For preview releases
# if [[ ! "$TAG" =~ ^v[0-9]+\.[0-9]+\.[0-9]+-(alpha|beta|rc)\.[0-9]+$ ]]; then
#   echo "ERROR: Tag does not match preview format"
#   exit 1
# fi

echo "✓ Tag format is valid: $TAG"
```

### 3. Pre-Release Testing

- [ ] **Unit tests pass:** `dotnet test StockData.Net.sln --configuration Release`
- [ ] **Integration tests pass:** No integration test failures
- [ ] **Code quality:** No critical compiler warnings
- [ ] **Security review:** Code has been scanned for security issues
- [ ] **Functional testing:** MCP server core functionality verified:
  - [ ] MCP server starts without errors
  - [ ] Can retrieve stock historical data
  - [ ] Can retrieve financial statements
  - [ ] Can retrieve holder information
  - [ ] Can retrieve options data
  - [ ] Proper error handling for invalid symbols/periods

**Run Tests:**
```bash
cd FinanceMCP
dotnet test StockData.Net.sln --configuration Release --verbosity normal
```

### 4. Build Verification

- [ ] Clean build succeeds: `dotnet clean && dotnet build -c Release`
- [ ] Release configuration only (no Debug artifacts)
- [ ] No unintended temporary files in publish directory
- [ ] Version numbers in `.csproj` files match release tag

**Update Version in Project Files:**
```xml
<!-- StockData.Net.csproj -->
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>

<!-- StockData.Net.McpServer.csproj -->
<Version>1.0.0</Version>
<AssemblyVersion>1.0.0.0</AssemblyVersion>
<FileVersion>1.0.0.0</FileVersion>
```

### 5. Documentation Review

- [ ] README.md reflects current features and setup
- [ ] Deployment and Versioning section is accurate
- [ ] Add MCP Server to VS Code section includes all platforms (Windows, Linux, macOS)
- [ ] Usage examples are current and functional
- [ ] Architecture documentation is complete
- [ ] Security guidelines are documented

---

## Release Phase

### 6. Git Tag Creation and Push

- [ ] On appropriate branch:
  - Stable releases: From `main` branch
  - Preview releases: From `develop` branch
- [ ] Create annotated tag: `git tag -a v1.0.0 -m "Release v1.0.0"`
- [ ] Verify tag: `git tag -l -n3` (shows tag annotation)
- [ ] Push tag: `git push origin v1.0.0`

**Confirm tag is on correct commit:**

```bash
git log v1.0.0 -1 --oneline
```

### 7. Manual Validation Before Tagging (Developer)

**Before pushing the tag, validate the MCP server locally:**

- [ ] Extract freshly-built binary to `C:\Tools\StockData-Test\`

  ```powershell
  # From publish output
  Copy-Item -Path "publish\StockData.Net.McpServer-win-x64\StockData.Net.McpServer.exe" -Destination "C:\Tools\StockData-Test\"
  ```

- [ ] Test binary starts without errors:

  ```powershell
  C:\Tools\StockData-Test\StockData.Net.McpServer.exe
  # Should output: "Listening for MCP requests on stdio..."
  # Press Ctrl+C to stop
  ```

- [ ] Edit VS Code `settings.json` with test path:

  ```json
  "command": "C:/Tools/StockData-Test/StockData.Net.McpServer.exe"
  ```

- [ ] Reload VS Code: `Ctrl+Shift+P` → "Developer: Reload Window"
- [ ] Open Copilot Chat (`Ctrl+Shift+I`)
- [ ] Test query: `Get Apple (AAPL) stock price`
- [ ] Confirm response is correct, no errors in Output panel
- [ ] Revert `settings.json` to production path: `C:/Tools/StockData.Net/`

---

## Workflow Phase

### 8. GitHub Actions Release Workflow

- [ ] Workflow triggered automatically (watch `.github/workflows/release.yml`)
- [ ] Build step: ✓ Passed
- [ ] Test step: ✓ Passed
- [ ] Publish step: ✓ Completed (win-x64)
- [ ] Artifact upload: ✓ ZIP uploaded
- [ ] GitHub Release created with:
  - [ ] Correct version in title: "StockData.Net MCP Server v1.0.0"
  - [ ] Correct pre-release flag (stable = false, preview = true)
  - [ ] Single win-x64 ZIP attached
  - [ ] Release notes include version, channel, and setup instructions

**Monitor Workflow:**

- View at: `.github/workflows/release.yml` in GitHub Actions tab
- Check logs if workflow fails
- Verify release artifacts are downloadable

### 9. Release Notes & Announcement

For **Stable Releases (v1.x.x):**
- [ ] Highlight breaking changes (if any)
- [ ] List new features with examples
- [ ] Link to updated documentation
- [ ] Mention bug fixes
- [ ] Include deployment instructions

For **Preview Releases (v1.x.x-rc.N):**
- [ ] Note pre-release status
- [ ] List testing focus areas
- [ ] Encourage feedback
- [ ] Mention known limitations

**Example Release Note Format:**
```markdown
## StockData.Net MCP Server v1.0.0

Stable release with production-ready MCP server implementation.

### New Features
- [Feature description]

### Bug Fixes
- [Bug fix description]

### Deployment
Download your platform package and follow the Deployment Guide in docs/deployment/.
```

---

## Post-Release Phase

### 9. Rollback Procedures

**If Critical Issues Are Discovered:**

1. **Do NOT delete the release tag** (GitHub history preservation)
2. **Create a hotfix branch:** `git checkout -b hotfix/issue-description v1.0.0`
3. **Apply fix and commit**
4. **Create new patch tag:** `git tag v1.0.1` (increment PATCH version)
5. **Merge hotfix to `main` and `develop`**
6. **Push new tag:** `git push origin v1.0.1`

**End-user Rollback:**
```bash
# If deployed, revert to previous stable version
git checkout v[PREVIOUS_VERSION]
dotnet build -c Release
# Redeploy to your environment
```

### 10. Post-Release Verification

- [ ] **GitHub Release:** Accessible and downloadable from Releases page
- [ ] **Artifact availability:** win-x64 ZIP present and complete
- [ ] **Version visibility:** Version number displayed correctly in release
- [ ] **Stable branch updated:** Ensure `main` reflects stable state
- [ ] **Develop branch updated:** Forward-merge stable to develop if needed

**Verify Release Integrity:**
```powershell
# Download and extract a release ZIP
cd Downloads
Expand-Archive StockData.Net.McpServer-1.0.0-win-x64.zip
# Verify binary exists
Get-Item .\StockData.Net.McpServer-win-x64\StockData.Net.McpServer.exe
```

### 11. Deployment Tracking

- [ ] Document deployment locations (internal/external systems)
- [ ] Update version in any deployment manifests or documentation
- [ ] Notify relevant teams (if applicable)
- [ ] Log deployment date/time and deployed version

**Deployment Manifest Example:**
```markdown
## Deployment Record

- Version: v1.0.0
- Release Date: 2026-02-28
- Deployed To: [Environment]
- Deployed By: [Team/Person]
- Deployment Date: [Date]
- Status: [Active/Testing/Staged]
```

### 12. Continuous Monitoring (First 24-48 hours)

- [ ] Monitor for error reports from end-users
- [ ] Check GitHub Issues for "bug" or "regression" labels
- [ ] Verify MCP server stability in production environments
- [ ] Response plan ready if critical issues emerge

---

## Version Maintenance

### Next Development Cycle

After a release:

1. **Update `develop` branch:**
   ```bash
   git checkout develop
   git pull
   git merge main  # For stable releases
   ```

2. **Bump version for next cycle** (if planned):
   ```xml
   <!-- Update to next MINOR version -->
   <Version>1.1.0-dev.0</Version>
   ```

3. **Update CHANGELOG** (if maintaining one):
   - Document what's next
   - Link to milestone/project board

---

## Troubleshooting

### Tag Already Exists
```bash
# Delete local tag
git tag -d v1.0.0

# Delete remote tag (caution: permanent)
git push origin --delete v1.0.0

# Re-create and push
git tag -a v1.0.0 -m "Release v1.0.0"
git push origin v1.0.0
```

### Workflow Failed, Need to Retry
- Check workflow logs in GitHub Actions
- Fix the issue (code, config, or environment)
- Delete the failed tag and re-push

### Release Created but Artifacts Missing
- Check "Publish binaries" step in workflow
- Verify RID targets (win-x64, linux-x64, osx-x64)
- Check disk space in GitHub Actions runner
- Re-run failed job or delete tag and retry

---

## Sign-Off

**Release Manager:** ________________________  
**Date:** ________________________  
**Version:** ________________________  
**Status:** ☐ Passed | ☐ Failed (Reason: ___________________)

---

## Related Documents

- [Deployment Guide](./DEPLOYMENT_GUIDE.md) — Platform-specific deployment instructions
- [GitHub Secrets Validation](./GITHUB_SECRETS_VALIDATION.md) — CI/CD secret management
- [Features Summary](../features/features-summary.md) — Feature specifications
- [README: Deployment and Versioning](../../README.md#deployment-and-versioning)
- [GitHub Actions Release Workflow](../../.github/workflows/release.yml)
