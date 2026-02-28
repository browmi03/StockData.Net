# Release Checklist

**Purpose:** Ensure each release of StockData.Net MCP Server is thoroughly validated, tested, and deployed with minimal risk.

**Version:** 1.0.0 (2026-02-28)

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

## Related Documentation

- [Deployment Guide](./DEPLOYMENT_GUIDE.md) - Platform-specific deployment instructions
- [README: Deployment and Versioning](../../README.md#deployment-and-versioning)
- [GitHub Actions Release Workflow](.github/workflows/release.yml)
