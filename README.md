# StockData.Net MCP Server

**Access comprehensive financial market data directly in your AI assistant using Model Context Protocol (MCP).**

StockData.Net is an MCP server that brings comprehensive financial market data to GitHub Copilot, Claude, VS Code, and other AI assistants. Ask questions about stock prices, financial statements, options data, and more—all from your favorite AI tool.

**Last Updated:** 2026-02-28

---

## Table of Contents

- [Key Features](#key-features)
- [Quick Start](#quick-start)
- [Installation and Setup](#installation-and-setup)
- [Add MCP Server to VS Code](#add-mcp-server-to-vs-code)
- [Configuring Market Data Providers](#configuring-market-data-providers)
- [MCP Server Capabilities](#mcp-server-capabilities)
- [Deployment and Versioning](#deployment-and-versioning)
- [Security](#security)
- [Contributing](#contributing)
- [License](#license)
- [Disclaimer](#disclaimer)

---

## Key Features

- **📊 Rich Financial Data Access**
  - Stock price history (daily, hourly, or intraday)
  - Real-time stock quotes and metrics
  - Financial statements (income, balance sheet, cash flow)
  - Options chains and expiration dates
  - Institutional and insider shareholding information
  - Analyst recommendations and upgrades/downgrades
  - Stock actions (dividends and splits)
  - Market news and articles

- **🤖 AI-First Design**
  - Works seamlessly with GitHub Copilot, Claude Desktop, and VS Code
  - Ask natural questions: "What's Apple's stock price?" or "Show me Tesla's quarterly earnings"
  - No programming knowledge required—just conversational queries
  - Uses Model Context Protocol (MCP) for secure, standard communication

- **⚡ Ready to Use**
  - Download the pre-built Windows binary
  - Simple 5-minute setup in VS Code
  - No compilation or development environment needed
  - Start querying financial data immediately

- **🔒 Secure & Private**
  - All communication encrypted with HTTPS
  - Provider API keys are supported and must be provisioned securely
  - Works with StockData.Net's financial market data
  - Your queries remain private between you and your AI assistant

---

## Quick Start

**Get financial data in your AI assistant in just 5 minutes!**

1. **Download the latest release** from [GitHub Releases](https://github.com/browmi03/StockData.Net/releases)
   - Look for `StockData.Net.McpServer-{VERSION}-win-x64.zip`

2. **Extract the files** to a folder like `C:\Tools\StockData.Net\`

3. **Open VS Code settings**
   - Press `Ctrl+,` to open Settings
   - Search for "MCP" to find the Copilot MCP settings

4. **Add the MCP server configuration** (see instructions in next section)

5. **Restart VS Code** and start asking questions in Copilot Chat!

### Example Queries You Can Make

Once configured, ask your AI assistant:

- "What's the current stock price for Apple?"
- "Show me Microsoft's quarterly earnings for the last year"
- "What are analyst recommendations for Tesla?"
- "Get the options chain for SPY"
- "Which companies are the biggest holders of Google stock?"
- "Show me the latest financial news"

---

## Installation and Setup

### For Windows Users (Recommended)

**Prerequisites:**
- Windows 10 or later
- VS Code with GitHub Copilot or Claude extension
- Internet connection

**Steps:**

1. Visit the [GitHub Releases page](https://github.com/browmi03/StockData.Net/releases)

2. Download the latest `StockData.Net.McpServer-{VERSION}-win-x64.zip` file

3. Extract the ZIP file to a permanent location, such as:
   ```
   C:\Tools\StockData.Net\
   ```

4. Note the full path to the extracted `.exe` file (e.g., `C:\Tools\StockData.Net\StockData.Net.McpServer.exe`)

5. Proceed to the "Add MCP Server to VS Code" section below

---

## Add MCP Server to VS Code

**Simple configuration to enable financial data in Copilot Chat:**

1. Open VS Code and access the Settings editor (`Ctrl+,`)

2. Search for "MCP" to find copilot chat settings

3. Find `github.copilot.chat.mcp.servers` and edit the JSON:

```json
{
  "github.copilot.chat.mcp.servers": {
    "StockData": {
      "type": "stdio",
      "command": "C:/Tools/StockData.Net/StockData.Net.McpServer.exe",
      "args": []
    }
  }
}
```

4. Replace `C:/Tools/StockData.Net/` with your actual installation path

5. Restart VS Code

6. Try asking in Copilot Chat: "What's the stock price for AAPL?"

---

## Configuring Market Data Providers

StockData.Net MCP Server uses a **multi-provider architecture** with intelligent failover and routing. The server supports three market data providers with different strengths and capabilities:

- **Yahoo Finance** - Free, no API key required, used as default fallback
- **Finnhub** - Real-time stock data with generous free tier
- **Alpha Vantage** - Historical time series and forex data

Each provider can be independently enabled/disabled and configured with its own API key and rate limits. The server automatically routes requests to the best available provider based on data type and implements automatic failover if a provider fails.

---

### Provider Details and Registration

#### Yahoo Finance (Built-in Provider)

**Description:** Built-in provider with no API key required. Provides stock prices, historical data, and basic fundamentals.

**Free Tier:** No explicit limits, but rate limiting may apply at the provider level.

**Configuration:** Enabled by default, no API key needed.

**Best used for:** General stock quotes, historical prices, basic financial data, and as a reliable fallback provider.

---

#### Finnhub

**Description:** Real-time stock market data including quotes, news, fundamentals, and economic data. Excellent for real-time price data and market news.

**Free Tier:** 60 API calls per minute

**Registration:**

1. Visit [https://finnhub.io/](https://finnhub.io/)
2. Click "Get free API key" and create an account
3. Copy your API key from the dashboard

**Configuration:**

Edit `C:\Tools\StockData.Net\appsettings.json` and add your API key using one of these three methods:

**Option 1 - providerCredentials section (Recommended):**

This is the cleanest approach, separating credentials from provider configuration:

```json
{
  "providerCredentials": {
    "finnhub": {
      "apiKey": "your_actual_finnhub_api_key_here"
    }
  }
}
```

**Option 2 - Inline in provider settings:**

Add the API key directly in the provider's `settings` block within the `providers` array:

```json
{
  "providers": [
    {
      "id": "finnhub",
      "type": "FinnhubProvider",
      "enabled": true,
      "priority": 2,
      "tier": "free",
      "settings": {
        "apiKey": "your_actual_finnhub_api_key_here",
        "baseUrl": "https://finnhub.io/api/v1"
      },
      "rateLimit": {
        "requestsPerMinute": 60
      }
    }
  ]
}
```

**Option 3 - Environment variable:**

Use an environment variable placeholder in settings, then set the `FINNHUB_API_KEY` environment variable on your system:

```json
{
  "providers": [
    {
      "id": "finnhub",
      "settings": {
        "apiKey": "${FINNHUB_API_KEY}"
      }
    }
  ]
}
```

**Rate Limit Configuration:** The default 60 requests/minute matches the free tier. Adjust `requestsPerMinute` if you have a paid plan.

---

#### Alpha Vantage

**Description:** Historical time series data, technical indicators, and forex data. Good for historical analysis and international markets.

**Free Tier:** 5 API calls per minute (500 per day)

**Registration:**

1. Visit [https://www.alphavantage.co/](https://www.alphavantage.co/)
2. Click "Get Your Free API Key Today"
3. Fill out the form and submit
4. Check your email for the API key

**Configuration:**

Edit `C:\Tools\StockData.Net\appsettings.json` and add your API key using one of these three methods:

**Option 1 - providerCredentials section (Recommended):**

This is the cleanest approach, separating credentials from provider configuration:

```json
{
  "providerCredentials": {
    "alphavantage": {
      "apiKey": "your_actual_alphavantage_api_key_here"
    }
  }
}
```

**Option 2 - Inline in provider settings:**

Add the API key directly in the provider's `settings` block within the `providers` array:

```json
{
  "providers": [
    {
      "id": "alphavantage",
      "type": "AlphaVantageProvider",
      "enabled": true,
      "priority": 3,
      "tier": "free",
      "settings": {
        "apiKey": "your_actual_alphavantage_api_key_here",
        "baseUrl": "https://www.alphavantage.co"
      },
      "rateLimit": {
        "requestsPerMinute": 5
      }
    }
  ]
}
```

**Option 3 - Environment variable:**

Use an environment variable placeholder in settings, then set the `ALPHAVANTAGE_API_KEY` environment variable on your system:

```json
{
  "providers": [
    {
      "id": "alphavantage",
      "settings": {
        "apiKey": "${ALPHAVANTAGE_API_KEY}"
      }
    }
  ]
}
```

**Rate Limit Configuration:** The default 5 requests/minute matches the free tier. Note that Alpha Vantage also has a daily limit of 500 requests.

---

### Enable or Disable Providers

Each provider in `appsettings.json` has an `enabled` flag:

```json
{
  "id": "finnhub",
  "enabled": true,
  "priority": 2
}
```

- Set `enabled: true` to include a provider in the routing chain
- Set `enabled: false` to remove it from operation (API key not required if disabled)
- `priority` determines the order (lower values = higher priority)

**Important:** If all providers are disabled, the server will refuse to start with an error message.

---

### Provider Routing and Failover

The server intelligently routes requests based on data type and implements automatic failover:

1. **Request arrives** - Server determines data type (e.g., StockPrice, HistoricalData)
2. **Primary provider selected** - Based on priority and capabilities
3. **Request attempted** - Server calls the primary provider
4. **On failure** - Server automatically tries fallback providers in priority order
5. **First success wins** - The first successful response is returned
6. **All failed** - Returns a provider failover error

Provider order is controlled by `priority` values and `routing.dataTypeRouting.*.fallbackProviderIds` in `appsettings.json`.

---

### Default Rate Limits Summary

| Provider | Default Limit | Free Tier | Notes |
| --- | --- | --- | --- |
| Yahoo Finance | Not rate-limited | N/A | Provider-side limits may apply |
| Finnhub | 60 requests/min | 60 requests/min | Configurable via `rateLimit.requestsPerMinute` |
| Alpha Vantage | 5 requests/min | 5 req/min (500/day) | Has daily limit in addition to per-minute limit |

---

### Security Best Practices

**IMPORTANT:** Follow these security practices when working with API keys:

✅ **DO:**

- Set API keys in local `C:\Tools\StockData.Net\appsettings.json`
- Keep placeholder values like `"<injected-from-secrets>"` in committed config files
- Restrict local file permissions on deployed config files when using paid API keys
- Rotate paid API keys immediately if you suspect exposure
- Store API keys securely and never share them publicly

❌ **DON'T:**

- Never commit real API keys to source control
- Never include API keys in screenshots or documentation
- Never share API keys via email or chat
- Never commit real API keys in repository `appsettings.json` files

**Fail-Fast Validation:** If an enabled provider has a missing or placeholder API key, the server will refuse to start and display a clear error message.

---

### Quick Setup

**For Local Deployment (C:\Tools\StockData.Net\):**

Edit `C:\Tools\StockData.Net\appsettings.json` with your API keys using the **providerCredentials** section (recommended):

```json
{
  "providerCredentials": {
    "finnhub": {
      "apiKey": "your_finnhub_key"
    },
    "alphavantage": {
      "apiKey": "your_alphavantage_key"
    }
  }
}
```

**Note:** Replace `"your_*_key"` with your actual API keys. See the provider-specific sections above for alternative configuration methods (inline settings or environment variables). For paid tiers, lock down file permissions on `appsettings.json` and avoid storing the file in cloud-synced folders.

---

## MCP Server Capabilities

Once you've configured the MCP server, here are the types of queries you can ask your AI assistant:

### Stock Historical Data & Prices
- "What was Apple's stock price on January 1st?"
- "Show me TSLA's price history for the last 6 months"
- "Get hourly prices for Microsoft over the last week"
- "What's the average closing price of Google stock this month?"

### Real-Time Stock Information
- "What's the current stock price for Netflix?"
- "Show me Amazon's 52-week high and low"
- "Get the P/E ratio for Meta Platforms"
- "What's Tesla's market capitalization?"

### Financial Statements
- "Show me Apple's quarterly income statement"
- "What was Microsoft's annual cash flow?"
- "Get Google's balance sheet for Q3 2024"
- "Show me quarterly earnings trends for Amazon"

### Options Data
- "What are the available expiration dates for SPY options?"
- "Get the call options chain for AAPL"
- "Show me put options for QQQ"
- "What's the implied volatility for Tesla options?"

### Institutional & Insider Holdings
- "Who are the largest institutional shareholders of Microsoft?"
- "Show me insider transactions for Tesla"
- "Which mutual funds hold Apple stock?"
- "Get insider purchase history for Google"

### Analyst Recommendations
- "What do analysts recommend for Meta stock?"
- "Show me recent upgrades and downgrades"
- "What's the price target for Microsoft?"
- "Get analyst sentiment for Amazon"

### Market News & Events
- "What's the latest news about Apple?"
- "Show me market headlines for today"
- "Are there any earnings announcements coming up?"
- "Get recent stock splits or dividend announcements"

---

## Deployment and Versioning

The project uses GitHub Actions for automated release management.

### Release Workflow

- **Trigger:** Push a Git tag starting with `v` (e.g., `v1.0.0`)
- **Output:** Automated release with compiled Windows binary
- **Access:** Download from [GitHub Releases](https://github.com/browmi03/StockData.Net/releases)

### Latest Release

Download the most recent `StockData.Net.McpServer-{VERSION}-win-x64.zip` for your system.

---

## Security

### Secure by Design

- ✅ **HTTPS Only** - All communication with StockData.Net is encrypted
- ✅ **Credential Safety** - API keys for optional providers are supported and validated at startup
- ✅ **Input Validation** - All queries are validated before processing
- ✅ **Safe Error Messages** - No sensitive information in error messages

### Privacy

- Your queries are between you and your AI assistant
- No data is logged or stored by this MCP server
- StockData.Net data is public market data
- All communication flows through secure HTTPS connections

For more details, see [docs/security/security-summary.md](docs/security/security-summary.md).

---

## Contributing

We welcome contributions! If you'd like to improve the MCP server or documentation:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/improvement`)
3. Make your changes
4. Run tests: `dotnet test`
5. Commit and push to your branch
6. Open a Pull Request

---

## License

See the LICENSE file in the repository for details.

---

## Disclaimer

**This project is for educational and research purposes.**

- Always verify important financial information from official sources
- StockData.Net data is provided "as-is"
- The authors are not responsible for any financial or investment decisions made using this software
- Use responsibly and in compliance with financial data providers' terms of service
- Past performance does not guarantee future results

---

## Getting Help

- **Setup issues?** Verify the binary path is correct and VS Code is restarted
- **Questions not answered?** Try rephrasing your query in Copilot Chat
- **Technical details?** See [docs/security/security-summary.md](docs/security/security-summary.md) for architecture info

---

**Enjoy using StockData.Net! 📈**
