using StockData.Net.Models.Events;

namespace StockData.Net.Providers;

public interface IMarketEventsProvider
{
    string ProviderId { get; }
    string ProviderName { get; }
    Task<IReadOnlyList<MarketEvent>> GetEventsAsync(MarketEventsQuery query, CancellationToken cancellationToken = default);
}