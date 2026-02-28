# StockData.Net MCP Server

**Access comprehensive financial market data directly in your AI assistant using Model Context Protocol (MCP).**

StockData.Net is an MCP server that brings Yahoo Finance data to GitHub Copilot, Claude, VS Code, and other AI assistants. Ask questions about stock prices, financial statements, options data, and more—all from your favorite AI tool.

**Last Updated:** 2026-02-28

---

## Table of Contents

- [Key Features](#key-features)
- [Quick Start](#quick-start)
- [Installation and Setup](#installation-and-setup)
- [Add MCP Server to VS Code](#add-mcp-server-to-vs-code)
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
  - No API keys or credentials exposed
  - Works with Yahoo Finance's public data
  - Your queries remain private between you and your AI assistant

---

## Quick Start

**Get financial data in your AI assistant in just 5 minutes!**

1. **Download the latest release** from [GitHub Releases](https://github.com/your-org/FinanceMCP/releases)
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

1. Visit the [GitHub Releases page](https://github.com/your-org/FinanceMCP/releases)

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
    "yahoo-finance": {
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
- **Access:** Download from [GitHub Releases](https://github.com/your-org/FinanceMCP/releases)

### Latest Release

Download the most recent `StockData.Net.McpServer-{VERSION}-win-x64.zip` for your system.

---

## Security

### Secure by Design

- ✅ **HTTPS Only** - All communication with Yahoo Finance is encrypted
- ✅ **No Credentials** - No API keys needed; uses public market data
- ✅ **Input Validation** - All queries are validated before processing
- ✅ **Safe Error Messages** - No sensitive information in error messages

### Privacy

- Your queries are between you and your AI assistant
- No data is logged or stored by this MCP server
- Yahoo Finance data is public market data
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
- Yahoo Finance data is provided "as-is"
- The authors are not responsible for any financial or investment decisions made using this software
- Use responsibly and in compliance with Yahoo Finance's terms of service
- Past performance does not guarantee future results

---

## Getting Help

- **Setup issues?** Verify the binary path is correct and VS Code is restarted
- **Questions not answered?** Try rephrasing your query in Copilot Chat
- **Technical details?** See [docs/security/security-summary.md](docs/security/security-summary.md) for architecture info

---

**Enjoy using StockData.Net! 📈**
