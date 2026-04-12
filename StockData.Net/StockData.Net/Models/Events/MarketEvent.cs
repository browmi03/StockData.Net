namespace StockData.Net.Models.Events;

public sealed class MarketEvent
{
    public string EventId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string? ImpactLevel { get; init; }
    public DateTimeOffset EventTime { get; init; }
    public string Source { get; init; } = string.Empty;
    public string? SourceUrl { get; init; }
    public IReadOnlyList<string> AffectedMarkets { get; init; } = [];
    public string? Sentiment { get; init; }
}