namespace StockData.Net.Models;

/// <summary>
/// Type of financial statement to retrieve
/// </summary>
public enum FinancialStatementType
{
    /// <summary>Annual income statement</summary>
    IncomeStatement,
    
    /// <summary>Quarterly income statement</summary>
    QuarterlyIncomeStatement,
    
    /// <summary>Annual balance sheet</summary>
    BalanceSheet,
    
    /// <summary>Quarterly balance sheet</summary>
    QuarterlyBalanceSheet,
    
    /// <summary>Annual cash flow statement</summary>
    CashFlow,
    
    /// <summary>Quarterly cash flow statement</summary>
    QuarterlyCashFlow
}
