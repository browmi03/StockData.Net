using System.Net;
using StockData.Net.Clients.Alpaca;
using StockData.Net.Security;

namespace StockData.Net.Tests.Clients;

[TestClass]
public class AlpacaClientTests
{
    private const string ApiKeyId = "TestKey123456";
    private const string SecretKey = "TestSecret123456";

    [TestMethod]
    public void Constructor_NonHttpsBaseAddress_ThrowsInvalidOperationException()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("http://data.alpaca.markets/v2/")
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => _ = new AlpacaClient(httpClient, new SecretValue(ApiKeyId), new SecretValue(SecretKey)));
        StringAssert.Contains(ex.Message, "requires HTTPS");
    }

    [TestMethod]
    public void Constructor_UnauthorizedHost_ThrowsInvalidOperationException()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https://evil.example.com/v2/")
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() =>
            _ = new AlpacaClient(httpClient, new SecretValue("test-key-id"), new SecretValue("test-secret")));

        StringAssert.Contains(ex.Message, "not in the allowed list");
    }

    [TestMethod]
    public void Constructor_AllowedHost_DoesNotThrow()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https://data.alpaca.markets/v2/")
        };

        var client = new AlpacaClient(httpClient, new SecretValue("test-key-id"), new SecretValue("test-secret"));

        Assert.IsNotNull(client);
    }

    [TestMethod]
    public void Constructor_NullArguments_ThrowArgumentNullException()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)))
        {
            BaseAddress = new Uri("https://data.alpaca.markets/v2/")
        };

        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new AlpacaClient(null!, new SecretValue(ApiKeyId), new SecretValue(SecretKey)));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new AlpacaClient(httpClient, null!, new SecretValue(SecretKey)));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new AlpacaClient(httpClient, new SecretValue(ApiKeyId), null!));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("ABCDEFGHIJK")]
    [DataRow("AAPL:")]
    public async Task SymbolValidation_RejectsInvalidSymbols(string? symbol)
    {
        var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.GetLatestQuoteAsync(symbol!));
    }

    [TestMethod]
    public async Task GetHistoricalBarsAsync_Success_ReturnsMappedBars()
    {
        var payload = """
        {
          "bars": [
            {
              "t": "2026-03-20T00:00:00Z",
              "o": 101.5,
              "h": 103.2,
              "l": 100.8,
              "c": 102.7,
              "v": 152300,
              "n": 480,
              "vw": 102.1
            }
          ]
        }
        """;

        var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, payload));

        var bars = await client.GetHistoricalBarsAsync("AAPL", DateTime.UtcNow.AddDays(-5), DateTime.UtcNow);

        Assert.HasCount(1, bars);
        Assert.AreEqual(101.5d, bars[0].Open, 0.0001d);
        Assert.AreEqual(480L, bars[0].TradeCount);
    }

    [TestMethod]
    public async Task GetLatestQuoteAsync_Success_ReturnsMappedQuote()
    {
        var payload = """
        {
          "quote": {
            "ap": 197.15,
            "as": 12,
            "bp": 197.10,
            "bs": 10,
            "t": "2026-03-22T15:30:00Z"
          }
        }
        """;

        var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, payload));

        var quote = await client.GetLatestQuoteAsync("AAPL");

        Assert.IsNotNull(quote);
        Assert.AreEqual(197.15d, quote.AskPrice, 0.0001d);
        Assert.AreEqual(197.10d, quote.BidPrice, 0.0001d);
    }

    [TestMethod]
    public async Task GetHealthStatusAsync_Success_ReturnsTrue()
    {
        var payload = """
        {
          "quote": {
            "ap": 197.15,
            "as": 12,
            "bp": 197.10,
            "bs": 10,
            "t": "2026-03-22T15:30:00Z"
          }
        }
        """;

        var client = CreateClient(_ => JsonResponse(HttpStatusCode.OK, payload));

        var result = await client.GetHealthStatusAsync();

        Assert.IsTrue(result);
    }

    [TestMethod]
    public async Task GetHealthStatusAsync_Exception_ReturnsFalse()
    {
        var client = CreateClient(_ => throw new HttpRequestException("network failure"));

        var result = await client.GetHealthStatusAsync();

        Assert.IsFalse(result);
    }

    [TestMethod]
    public async Task Http404_ReturnsNullOrEmpty()
    {
        var client = CreateClient(_ => JsonResponse(HttpStatusCode.NotFound, "{}"));

        var quote = await client.GetLatestQuoteAsync("AAPL");
        var bars = await client.GetHistoricalBarsAsync("AAPL", DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

        Assert.IsNull(quote);
        Assert.IsEmpty(bars);
    }

    [TestMethod]
    public async Task Http429_ThrowsSanitizedMessage()
    {
        var client = CreateClient(_ =>
        {
            var response = JsonResponse(HttpStatusCode.TooManyRequests, "rate limit token ABCD1234EFGH5678");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30));
            return response;
        });

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetLatestQuoteAsync("AAPL"));
        StringAssert.Contains(ex.Message, "Alpaca quote request failed");
        StringAssert.Contains(ex.Message, "[REDACTED]");
        Assert.IsFalse(ex.Message.Contains("ABCD1234EFGH5678", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Http500_ThrowsSanitizedMessage()
    {
        var client = CreateClient(_ => JsonResponse(HttpStatusCode.InternalServerError, "server exploded with key ZXCV1234BNMM9876"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetHistoricalBarsAsync("AAPL", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow));
        StringAssert.Contains(ex.Message, "Alpaca historical request failed");
        StringAssert.Contains(ex.Message, "[REDACTED]");
        Assert.IsFalse(ex.Message.Contains("ZXCV1234BNMM9876", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SendGetAsync_SetsAuthHeadersPerRequest()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(HttpStatusCode.OK, "{\"quote\":{\"ap\":197.15,\"as\":12,\"bp\":197.10,\"bs\":10,\"t\":\"2026-03-22T15:30:00Z\"}}"));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://data.alpaca.markets/v2/")
        };
        var client = new AlpacaClient(httpClient, new SecretValue(ApiKeyId), new SecretValue(SecretKey));

        _ = await client.GetLatestQuoteAsync("AAPL");

        Assert.IsNotNull(handler.LastRequest);
        Assert.IsTrue(handler.LastRequest.Headers.TryGetValues("APCA-API-KEY-ID", out var keyIdValues));
        Assert.IsTrue(handler.LastRequest.Headers.TryGetValues("APCA-API-SECRET-KEY", out var secretValues));
        Assert.AreEqual(ApiKeyId, keyIdValues.Single());
        Assert.AreEqual(SecretKey, secretValues.Single());
    }

    private static AlpacaClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var handler = new StubHttpMessageHandler(responseFactory);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://data.alpaca.markets/v2/")
        };

        return new AlpacaClient(httpClient, new SecretValue(ApiKeyId), new SecretValue(SecretKey));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
    {
        return new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(body)
        };
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            var response = responder(request);
            return Task.FromResult(response);
        }
    }
}
