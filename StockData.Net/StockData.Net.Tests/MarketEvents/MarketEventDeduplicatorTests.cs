using StockData.Net.Deduplication;
using StockData.Net.Models.Events;

namespace StockData.Net.Tests.MarketEvents;

[TestClass]
public class MarketEventDeduplicatorTests
{
    [TestMethod]
    public void GivenEquivalentEvents_WhenDeduplicating_ThenKeepsSingleFirstEvent()
    {
        var deduplicator = new MarketEventDeduplicator();
        var now = DateTimeOffset.UtcNow;

        var input = new List<MarketEvent>
        {
            new() { EventId = "p1", Title = "Fed rate decision", EventType = "breaking", Category = "fed", EventTime = now, Source = "P1" },
            new() { EventId = "p2", Title = "FED rate decision", EventType = "breaking", Category = "fed", EventTime = now.AddMinutes(30), Source = "P2" }
        };

        var output = deduplicator.Deduplicate(input);

        Assert.HasCount(1, output);
        Assert.AreEqual("p1", output[0].EventId);
    }

    [TestMethod]
    public void GivenDistinctTitles_WhenDeduplicating_ThenKeepsBothEvents()
    {
        var deduplicator = new MarketEventDeduplicator();
        var now = DateTimeOffset.UtcNow;

        var input = new List<MarketEvent>
        {
            new() { EventId = "1", Title = "FOMC Rate Decision", EventType = "scheduled", Category = "fed", EventTime = now, Source = "P1" },
            new() { EventId = "2", Title = "FOMC Meeting Minutes", EventType = "scheduled", Category = "fed", EventTime = now, Source = "P2" }
        };

        var output = deduplicator.Deduplicate(input);

        Assert.HasCount(2, output);
    }

    [TestMethod]
    public void GivenSingleProviderEvents_WhenDeduplicating_ThenReturnsAsIs()
    {
        var deduplicator = new MarketEventDeduplicator();
        var input = new List<MarketEvent>
        {
            new() { EventId = "1", Title = "Treasury auction", EventType = "scheduled", Category = "treasury", EventTime = DateTimeOffset.UtcNow, Source = "P1" }
        };

        var output = deduplicator.Deduplicate(input);

        Assert.HasCount(1, output);
        Assert.AreEqual("1", output[0].EventId);
    }
}
