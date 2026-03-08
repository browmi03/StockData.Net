using System.Net;
using StockData.Net.Clients.Polygon;
using StockData.Net.Configuration;
using StockData.Net.Security;

namespace StockData.Net.Tests.Clients;

[TestClass]
public class PolygonClientTests
{
    private const string ApiKey = "Key123456Token";

    [TestMethod]
    public void Constructor_NonHttpsBaseAddress_ThrowsInvalidOperationException()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(JsonResponse(HttpStatusCode.OK, "{}")))
        {
            BaseAddress = new Uri("http://api.polygon.test/")
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => _ = new PolygonClient(httpClient, new SecretValue(ApiKey)));
        StringAssert.Contains(ex.Message, "requires HTTPS");
    }

    [TestMethod]
    public async Task GetQuoteAsync_Success_ReturnsQuote()
    {
        var payload = """
        {"status":"OK","results":{"p":320.25,"t":1700000000123}}
        """;
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        var quote = await client.GetQuoteAsync("MSFT");

        Assert.IsNotNull(quote);
        Assert.AreEqual(320.25d, quote.Price, 0.0001d);
        Assert.AreEqual(1_700_000_000L, quote.Timestamp);
    }

    [TestMethod]
    public async Task GetQuoteAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(JsonResponse(HttpStatusCode.NotFound, "{}"));

        var quote = await client.GetQuoteAsync("BAD");

        Assert.IsNull(quote);
    }

    [TestMethod]
    public async Task GetQuoteAsync_NonOkPayloadStatus_ReturnsNull()
    {
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, "{" + "\"status\":\"ERROR\"}"));

        var quote = await client.GetQuoteAsync("AAPL");

        Assert.IsNull(quote);
    }

    [TestMethod]
    public async Task GetQuoteAsync_HttpFailure_ThrowsSanitizedInvalidOperationException()
    {
        var client = CreateClient(_ => throw new HttpRequestException("secret ABCD1234EFGH5678 failed"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetQuoteAsync("AAPL"));
        StringAssert.Contains(ex.Message, "Polygon quote request failed");
        Assert.IsFalse(ex.Message.Contains("ABCD1234EFGH5678", StringComparison.Ordinal));
        StringAssert.Contains(ex.Message, "[REDACTED]");
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_Success_ReturnsMappedBars()
    {
        var payload = """
        {
          "status":"OK",
          "results":[
            {"t":1700000000123,"o":10.0,"h":11.0,"l":9.0,"c":10.5,"v":1000.4},
            {"t":1700086400,"o":11.0,"h":12.0,"l":10.0,"c":11.5,"v":2000.6}
          ]
        }
        """;
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        var data = await client.GetHistoricalPricesAsync("AAPL", new DateTime(2023, 1, 1), new DateTime(2024, 1, 1));

        Assert.HasCount(2, data);
        Assert.AreEqual(1_700_000_000L, data[0].Timestamp);
        Assert.AreEqual(2001L, data[1].Volume);
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_FromAfterTo_ThrowsArgumentException()
    {
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.GetHistoricalPricesAsync("AAPL", DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)));
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_NotFound_ReturnsEmptyList()
    {
        var client = CreateClient(JsonResponse(HttpStatusCode.NotFound, "{}"));

        var data = await client.GetHistoricalPricesAsync("AAPL", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow);

        Assert.IsEmpty(data);
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_NonPositiveTimestamp_NormalizesToCurrentTime()
    {
        var payload = """
        {"status":"OK","results":[{"t":0,"o":1,"h":1,"l":1,"c":1,"v":1}]}
        """;
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        var before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var data = await client.GetHistoricalPricesAsync("AAPL", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);
        var after = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        Assert.HasCount(1, data);
        Assert.IsTrue(data[0].Timestamp >= before && data[0].Timestamp <= after);
    }

    [TestMethod]
    public async Task GetNewsAsync_Success_ParsesAndMapsFields()
    {
        var payload = """
        {
          "status":"OK",
          "results":[
            {
              "id":"n1",
              "title":"Headline",
              "description":"Summary",
              "article_url":"https://example.com/news",
              "published_utc":"2026-02-20T11:30:00Z",
              "tickers":["AAPL","QQQ"],
              "publisher":{"name":"Reuters"}
            }
          ]
        }
        """;
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        var news = await client.GetNewsAsync("AAPL", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        Assert.HasCount(1, news);
        Assert.AreEqual("Headline", news[0].Title);
        Assert.AreEqual("Reuters", news[0].Publisher);
        Assert.HasCount(2, news[0].Tickers);
    }

    [TestMethod]
    public async Task GetNewsAsync_FromAfterTo_ThrowsArgumentException()
    {
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.GetNewsAsync("AAPL", DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)));
    }

    [TestMethod]
    public async Task GetNewsAsync_NullUrlEntries_AreFilteredOut()
    {
        var payload = """
        {
          "status":"OK",
          "results":[
            {"id":"n1","title":"A","article_url":null,"published_utc":"2026-02-20T11:30:00Z","publisher":{"name":"R"}},
            {"id":"n2","title":"B","article_url":"https://example.com/b","published_utc":"2026-02-20T12:30:00Z","publisher":{"name":"R"}}
          ]
        }
        """;
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        var news = await client.GetNewsAsync("AAPL", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        Assert.HasCount(1, news);
        Assert.AreEqual("n2", news[0].Id);
    }

    [TestMethod]
    public async Task GetNewsAsync_InvalidPublishedDate_FallsBackToCurrentUtc()
    {
        var payload = """
        {
          "status":"OK",
          "results":[
            {"id":"n1","title":"A","article_url":"https://example.com/a","published_utc":"bad-date","publisher":{"name":"R"}}
          ]
        }
        """;
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        var before = DateTimeOffset.UtcNow;
        var news = await client.GetNewsAsync("AAPL", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var after = DateTimeOffset.UtcNow;

        Assert.HasCount(1, news);
        Assert.IsTrue(news[0].PublishedUtc >= before.AddSeconds(-1) && news[0].PublishedUtc <= after.AddSeconds(1));
    }

    [TestMethod]
    public async Task GetQuoteAsync_WhitespaceSymbol_ThrowsArgumentException()
    {
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.GetQuoteAsync("   "));
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_ErrorPath_ThrowsSanitizedInvalidOperationException()
    {
        var client = CreateClient(_ => throw new HttpRequestException("token ZXCV1234BNMM9876 unreachable"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetHistoricalPricesAsync("AAPL", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow));

        StringAssert.Contains(ex.Message, "Polygon historical request failed");
        Assert.IsFalse(ex.Message.Contains("ZXCV1234BNMM9876", StringComparison.Ordinal));
        StringAssert.Contains(ex.Message, "[REDACTED]");
    }

    [TestMethod]
    public async Task GetNewsAsync_ErrorPath_ThrowsSanitizedInvalidOperationException()
    {
        var client = CreateClient(_ => throw new HttpRequestException("token QWER1234TYUI5678 unreachable"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetNewsAsync("AAPL", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow));

        StringAssert.Contains(ex.Message, "Polygon news request failed");
        Assert.IsFalse(ex.Message.Contains("QWER1234TYUI5678", StringComparison.Ordinal));
        StringAssert.Contains(ex.Message, "[REDACTED]");
    }

    [TestMethod]
    public async Task GetQuoteAsync_RateLimiterQueueOverflow_ThrowsInvalidOperationException()
    {
        var payload = "{" + "\"status\":\"OK\",\"results\":{\"p\":1.0,\"t\":1700000000000}}";
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload), new RateLimitConfiguration { RequestsPerMinute = 1 });

        using var cts = new CancellationTokenSource(250);
        var tasks = Enumerable.Range(0, 140)
            .Select(_ => client.GetQuoteAsync("AAPL", cts.Token))
            .ToArray();

        await Task.Delay(100);

        var hasRateLimitFailure = tasks.Any(t =>
            t.IsFaulted &&
            t.Exception is not null &&
            t.Exception.Flatten().InnerExceptions.Any(e => e is InvalidOperationException ioe && ioe.Message.Contains("rate limit exceeded", StringComparison.OrdinalIgnoreCase)));

        cts.Cancel();
        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // Intentionally ignored: this test only asserts overflow produced rate-limit failures.
        }

        Assert.IsTrue(hasRateLimitFailure, "Expected at least one queue-overflow rate limit exception.");
    }

    private static PolygonClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory, RateLimitConfiguration? rateLimit = null)
    {
        var handler = new StubHttpMessageHandler(responseFactory);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.polygon.test/")
        };

        return new PolygonClient(httpClient, new SecretValue(ApiKey), rateLimit);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> JsonResponse(HttpStatusCode statusCode, string body)
    {
        return _ => new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(body)
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = responder(request);
            return Task.FromResult(response);
        }
    }
}