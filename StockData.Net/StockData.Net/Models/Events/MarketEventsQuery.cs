namespace StockData.Net.Models.Events;

public sealed class MarketEventsQuery
{
    public EventCategory Category { get; init; } = EventCategory.All;
    public EventType EventType { get; init; } = EventType.All;
    public DateOnly FromDate { get; init; }
    public DateOnly ToDate { get; init; }
    public ImpactLevel ImpactLevel { get; init; } = ImpactLevel.All;
}