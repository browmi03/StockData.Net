namespace StockData.Net.Models;

/// <summary>
/// Type of holder information to retrieve
/// </summary>
public enum HolderType
{
    /// <summary>Major holders of the stock</summary>
    MajorHolders,
    
    /// <summary>Institutional holders</summary>
    InstitutionalHolders,
    
    /// <summary>Mutual fund holders</summary>
    MutualFundHolders,
    
    /// <summary>Insider transactions</summary>
    InsiderTransactions,
    
    /// <summary>Insider purchases</summary>
    InsiderPurchases,
    
    /// <summary>Insider roster holders</summary>
    InsiderRosterHolders
}
