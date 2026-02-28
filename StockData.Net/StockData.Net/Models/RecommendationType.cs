namespace StockData.Net.Models;

/// <summary>
/// Type of recommendation data to retrieve
/// </summary>
public enum RecommendationType
{
    /// <summary>Analyst recommendations</summary>
    Recommendations,
    
    /// <summary>Analyst upgrades and downgrades</summary>
    UpgradesDowngrades
}
