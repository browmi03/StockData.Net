
# Deployment Guide — StockData.Net MCP Server (Windows)

## Document Info

- **Type**: Operational Runbook — Deployment
- **Version**: 1.0.0
- **Architecture**: [Canonical Architecture](../architecture/stock-data-aggregation-canonical-architecture.md)
- **Security**: [Security Summary](../security/security-summary.md)
- **Related Runbooks**: [Release Checklist](./RELEASE_CHECKLIST.md) · [GitHub Secrets Validation](./GITHUB_SECRETS_VALIDATION.md)
- **Status**: Approved
- **Last Updated**: 2026-03-09

---

## Overview

The StockData.Net MCP Server is a locally-run Windows executable for accessing Yahoo Finance data in GitHub Copilot and VS Code.

This guide provides step-by-step instructions for deploying the binary and configuring it with VS Code.

**Note:** This is a locally-run MCP server for Windows. For support, open a GitHub Issue in the repository.

---

## Prerequisites

- **Windows 10 or later** (64-bit)
- **VS Code 1.90+** with Copilot extension enabled
- **GitHub Copilot subscription** (for using the MCP server with Copilot)
- **[Optional] .NET 8.0+** if running from source instead of binary

---

## Quick Start

1. Download `StockData.Net.McpServer-{VERSION}-win-x64.zip` from [Releases](https://github.com/browmi03/StockData.Net/releases)
2. Extract to `C:\Tools\StockData.Net\`
3. Configure VS Code `settings.json` (see below)
4. Restart VS Code and test in Copilot Chat

---

## Deployment

### Step 1: Extract the Binary

1. **Download** `StockData.Net.McpServer-1.0.0-win-x64.zip` from releases
2. **Extract** to a stable directory. Recommended:

  ```text
   C:\Tools\StockData.Net\

   ```
3. **Verify** extraction:

   ```powershell
   dir C:\Tools\StockData.Net\

   ```
   You should see `StockData.Net.McpServer.exe` in the directory.

### Step 2: Test the Binary

Open PowerShell and test that the binary runs:

```powershell
C:\Tools\StockData.Net\StockData.Net.McpServer.exe
```

You should see output indicating the MCP server is starting. Press `Ctrl+C` to stop it.

**Expected Output:**

```text
Listening for MCP requests on stdio...

```

### Step 2.5: Configure API Keys (Local Option A)

For local deployments in `C:\Tools\StockData.Net\`, store provider API keys directly in:

`C:\Tools\StockData.Net\appsettings.json`

Example:

```json
{
  "providers": {
    "finnhub": {
      "enabled": true,
      "apiKey": "your_finnhub_key"
    },
    "alphavantage": {
      "enabled": true,
      "apiKey": "your_alphavantage_key"
    }
  }
}
```

Security notes:

- Keep this `appsettings.json` local and private.
- Do not store the deployment folder in cloud-synced paths (OneDrive, Google Drive, Dropbox).
- If you use paid-tier API keys, lock file permissions to your user account:

```powershell
icacls "C:\Tools\StockData.Net\appsettings.json" /inheritance:r /grant:r "%USERNAME%:(R)"
```

- Rotate paid API keys immediately if you suspect exposure.

### Step 3: Configure VS Code

1. **Open VS Code**
2. **Command Palette:** `Ctrl+Shift+P` → Search for **"Settings (JSON)"** → Select **"Preferences: Open User Settings (JSON)"**
3. **Add the MCP server configuration:**

```json
{
  "github.copilot.chat.mcp.servers": {
    "StockData": {
      "command": "C:\\Tools\\StockData.Net\\StockData.Net.McpServer.exe",
      "args": []
    }
  }
}
```

**Important Notes:**
- Use **forward slashes** (`/`) or **double backslashes** (`\\`) in the path

- The `args` array should be empty `[]`
- Save the settings file (`Ctrl+S`)

### Step 4: Restart VS Code

1. **Close VS Code completely**
2. **Reopen VS Code**
3. **Open the GitHub Copilot Chat panel** (if not visible, use: `Ctrl+Shift+I`)
4. **Test the connection:**
   - Type a message like: `Get the stock price for Apple`
   - The MCP server should respond with financial data

## Troubleshooting

### Error: "Cannot find executable"

- Verify the path in settings.json exists: `dir C:\Tools\StockData.Net\`
- Use absolute path, not relative path
- Restart VS Code after changes

### Error: "MCP server failed to start"

- Test binary manually: `C:\Tools\StockData.Net\StockData.Net.McpServer.exe`

- Check Windows Defender/antivirus hasn't blocked the executable
- Verify .NET 8.0+ is installed (even though binary is self-contained)

**No response from MCP server:**
- Check VS Code output panel for errors (`View` → `Output`)
- Verify no firewall is blocking stdio communication
- Check that Copilot extension is installed and enabled

---

## Source-Based Deployment (Alternative)

If you prefer **not to use a compiled binary**, you can run the MCP server directly from source:

### Development Prerequisites

- **Git** installed
- **.NET 8.0+** installed (check: `dotnet --version`)

### Setup

1. **Clone the repository:**
   ```bash
   git clone https://github.com/browmi03/StockData.Net.git
   cd StockData.Net
   ```

2. **Build the solution:**
   ```bash
   dotnet build -c Release
   ```

3. **Configure VS Code `settings.json`:**

```json
{
  "github.copilot.chat.mcp.servers": {
    "StockData": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "C:/path/to/FinanceMCP/StockData.Net/StockData.Net.McpServer/StockData.Net.McpServer.csproj",
        "--configuration",
        "Release"
      ]
    }
  }
}
```

**Notes:**
- Use **absolute paths** to the `.csproj` file
- Requires .NET 8.0 to be installed
- Slightly slower startup than pre-compiled binary
- Useful for development or custom modifications

---

## Updating the MCP Server

### Binary Update

1. **Download new release ZIP**
2. **Back up old binary:**
   ```powershell
   Rename-Item -Path "C:\Tools\StockData.Net\StockData.Net.McpServer.exe" -NewName "StockData.Net.McpServer.exe.backup"
   ```

3. **Extract new binary** to same location
4. **Restart VS Code** to load new version
5. **Verify version** by checking GitHub Release notes

### Source Update

1. **Pull latest changes:**
   ```bash
   cd FinanceMCP
   git pull origin main
   ```

2. **Rebuild:**
   ```bash
   dotnet build -c Release
   ```

3. **Restart VS Code**

---

## Verification Checklist

After completing deployment, verify:

- [ ] **Binary extracted** to `C:\Tools\StockData.Net\`
- [ ] **Binary executable** (tested: `StockData.Net.McpServer.exe` runs without errors)
- [ ] **VS Code settings.json updated** with correct path
- [ ] **VS Code restarted** completely
- [ ] **Copilot Chat works** with a test query like "Get Apple stock price"
- [ ] **Financial data retrieved** (response contains stock symbol and price)
- [ ] **No errors in VS Code output panel** (`View` → `Output` → "Extension Host")

---

## Notes

- **Internet Required:** MCP server requires internet access to Yahoo Finance API
- **Windows Defender:** Binary is self-signed; add to Windows Defender exclusions if blocked
- **Firewall:** Ensure HTTPS outbound traffic to Yahoo Finance is allowed
- **Updates:** Subscribe to GitHub releases to stay current
- **Support:** For issues, open a GitHub Issue in the repository

---

## Secret Management in CI/CD

CI/CD pipelines (GitHub Actions, Azure DevOps, etc.) must inject API keys as **native pipeline secrets mapped to environment variables** — never via a `.env` file.

### Required Environment Variables

| Variable | Description |
| --- | --- |
| `FINNHUB_API_KEY` | Finnhub API key for real-time stock data |
| `ALPHAVANTAGE_API_KEY` | Alpha Vantage API key for historical data |
| `YAHOO_FINANCE_API_KEY` | Yahoo Finance key (leave empty — no key required) |

### GitHub Actions

Define secrets in **Settings → Secrets and variables → Actions**, then map them in your workflow:

```yaml
env:
  FINNHUB_API_KEY: ${{ secrets.FINNHUB_API_KEY }}
  ALPHAVANTAGE_API_KEY: ${{ secrets.ALPHAVANTAGE_API_KEY }}
```

### Azure DevOps / Azure Key Vault

Link Key Vault secrets as pipeline variables and expose them as environment variables in the pipeline definition. Refer to your pipeline platform's documentation for the exact syntax.

### Important Rules

- **Do NOT deploy a `.env` file** — the `.env` file is for local developer use only. Pipelines must never package or copy a `.env` file into a build artifact or deployment target.
- **Pipeline env vars always win** — `DotNetEnv` is configured with `Env.TraversePath()` and does **not** override existing environment variables. Any key already set in the OS environment or pipeline takes precedence over values in a `.env` file, so injected CI/CD secrets are always authoritative.
- **Use secret scanning** — enable GitHub's secret scanning and push protection to prevent accidental credential commits.

---

## Related Documents

- [Release Checklist](./RELEASE_CHECKLIST.md) — Release validation procedures
- [GitHub Secrets Validation](./GITHUB_SECRETS_VALIDATION.md) — CI/CD secret management
- [Features Summary](../features/features-summary.md) — Feature specifications
- [README: Quick Start](../../README.md#quick-start)
- [README: Add MCP Server to VS Code](../../README.md#add-mcp-server-to-vs-code)
- [GitHub Releases](https://github.com/browmi03/StockData.Net/releases)
