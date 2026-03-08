using System.Globalization;
using System.Net;
using System.Reflection;
using StockData.Net.Clients.AlphaVantage;
using StockData.Net.Configuration;
using StockData.Net.Security;

namespace StockData.Net.Tests.Clients;

[TestClass]
public class AlphaVantageClientTests
{
    private const string ApiKey = "Key123456Token";

    [TestMethod]
    public void Constructor_NonHttpsBaseAddress_ThrowsInvalidOperationException()
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(JsonResponse(HttpStatusCode.OK, "{}")))
        {
            BaseAddress = new Uri("http://api.alphavantage.test/")
        };

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => _ = new AlphaVantageClient(httpClient, new SecretValue(ApiKey)));
        StringAssert.Contains(ex.Message, "requires HTTPS");
    }

    [TestMethod]
    public async Task GetQuoteAsync_Success_ReturnsQuote()
    {
        var payload = """
        {
          "Global Quote": {
            "05. price": "120.12",
            "09. change": "1.15",
            "10. change percent": "0.97%",
            "07. latest trading day": "2026-03-06"
          }
        }
        """;
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        var quote = await client.GetQuoteAsync("MSFT");

        Assert.IsNotNull(quote);
        Assert.AreEqual(120.12d, quote.Price, 0.0001d);
        Assert.AreEqual(0.97d, quote.PercentChange, 0.0001d);
        Assert.IsGreaterThan(0L, quote.Timestamp);
    }

    [TestMethod]
    public async Task GetQuoteAsync_NotFound_ReturnsNull()
    {
        var client = CreateClient(JsonResponse(HttpStatusCode.NotFound, "{}"));

        var quote = await client.GetQuoteAsync("BAD");

        Assert.IsNull(quote);
    }

    [TestMethod]
    public async Task GetQuoteAsync_NullPayload_ReturnsNull()
    {
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, "null"));

        var quote = await client.GetQuoteAsync("AAPL");

        Assert.IsNull(quote);
    }

    [TestMethod]
    public async Task GetQuoteAsync_RateLimitedNote_ThrowsInvalidOperationException()
    {
        var payload = "{" + "\"Note\":\"Thank you for using Alpha Vantage, frequency limit reached\"}";
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetQuoteAsync("AAPL"));
        StringAssert.Contains(ex.Message, "AlphaVantage quote request failed");
        StringAssert.Contains(ex.Message, "limit");
    }

    [TestMethod]
    public async Task GetQuoteAsync_RateLimitedInformation_ThrowsInvalidOperationException()
    {
        var payload = "{" + "\"Information\":\"API call frequency limit exceeded\"}";
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetQuoteAsync("AAPL"));
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_SuccessAndDateFiltering_ReturnsOnlyInRange()
    {
        var payload = """
        {
          "Time Series (Daily)": {
            "2026-03-05": { "1. open": "10", "2. high": "12", "3. low": "9", "4. close": "11", "6. volume": "1000" },
            "2026-02-01": { "1. open": "1", "2. high": "1", "3. low": "1", "4. close": "1", "6. volume": "10" },
            "bad-date":   { "1. open": "2", "2. high": "2", "3. low": "2", "4. close": "2", "6. volume": "20" }
          }
        }
        """;
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        var data = await client.GetHistoricalPricesAsync("AAPL", new DateTime(2026, 3, 1), new DateTime(2026, 3, 10));

        Assert.HasCount(1, data);
        Assert.AreEqual(11d, data[0].Close, 0.0001d);
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
    public async Task GetNewsAsync_Success_FiltersByDateAndNullUrl()
    {
        var payload = """
        {
          "feed": [
            {
              "title": "Kept",
              "source": "Reuters",
              "url": "https://example.com/kept",
              "summary": "S",
              "time_published": "20260305T090000",
              "ticker_sentiment": [{"ticker":"AAPL"},{"ticker":"aapl"},{"ticker":"MSFT"}]
            },
            {
              "title": "DroppedNoUrl",
              "source": "Reuters",
              "url": "",
              "summary": "S",
              "time_published": "20260305T090000"
            },
            {
              "title": "DroppedOutOfRange",
              "source": "Reuters",
              "url": "https://example.com/old",
              "summary": "S",
              "time_published": "20240101T010000"
            }
          ]
        }
        """;
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        var news = await client.GetNewsAsync("AAPL", new DateTime(2026, 3, 1), new DateTime(2026, 3, 10));

        Assert.HasCount(1, news);
        Assert.AreEqual("Kept", news[0].Title);
        Assert.HasCount(2, news[0].RelatedTickers);
    }

    [TestMethod]
    public async Task GetNewsAsync_FromAfterTo_ThrowsArgumentException()
    {
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, "{}"));

        await Assert.ThrowsExactlyAsync<ArgumentException>(() => client.GetNewsAsync("AAPL", DateTime.UtcNow, DateTime.UtcNow.AddDays(-1)));
    }

    [TestMethod]
    public async Task GetNewsAsync_InvalidTimestamp_FallsBackToNowAndStillFilters()
    {
        var payload = """
        {
          "feed": [
            {
              "title": "NowFallback",
              "source": "Reuters",
              "url": "https://example.com/kept",
              "summary": "S",
              "time_published": "bad"
            }
          ]
        }
        """;
        var client = CreateClient(JsonResponse(HttpStatusCode.OK, payload));

        var news = await client.GetNewsAsync("AAPL", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));

        Assert.HasCount(1, news);
        Assert.IsGreaterThan(0L, news[0].Timestamp);
    }

    [TestMethod]
    public async Task GetQuoteAsync_HttpFailure_ThrowsSanitizedInvalidOperationException()
    {
        var client = CreateClient(_ => throw new HttpRequestException("secret ABCD1234EFGH5678 failed"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetQuoteAsync("AAPL"));
        StringAssert.Contains(ex.Message, "AlphaVantage quote request failed");
        Assert.IsFalse(ex.Message.Contains("ABCD1234EFGH5678", StringComparison.Ordinal));
        StringAssert.Contains(ex.Message, "[REDACTED]");
    }

    [TestMethod]
    public async Task GetHistoricalPricesAsync_HttpFailure_ThrowsSanitizedInvalidOperationException()
    {
        var client = CreateClient(_ => throw new HttpRequestException("secret ZXCV1234BNMM9876 failed"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetHistoricalPricesAsync("AAPL", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow));
        StringAssert.Contains(ex.Message, "AlphaVantage historical request failed");
        Assert.IsFalse(ex.Message.Contains("ZXCV1234BNMM9876", StringComparison.Ordinal));
        StringAssert.Contains(ex.Message, "[REDACTED]");
    }

    [TestMethod]
    public async Task GetNewsAsync_HttpFailure_ThrowsSanitizedInvalidOperationException()
    {
        var client = CreateClient(_ => throw new HttpRequestException("secret POIU1234LKJM9876 failed"));

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => client.GetNewsAsync("AAPL", DateTime.UtcNow.AddDays(-3), DateTime.UtcNow));
        StringAssert.Contains(ex.Message, "AlphaVantage news request failed");
        Assert.IsFalse(ex.Message.Contains("POIU1234LKJM9876", StringComparison.Ordinal));
        StringAssert.Contains(ex.Message, "[REDACTED]");
    }

    [TestMethod]
    public async Task GetQuoteAsync_RateLimiterQueueOverflow_ThrowsInvalidOperationException()
    {
        var payload = """
        {
          "Global Quote": {
            "05. price": "1",
            "09. change": "0",
            "10. change percent": "0%",
            "07. latest trading day": "2026-03-06"
          }
        }
        """;
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

    [TestMethod]
    public void ParseTradingDay_ValidAndInvalidInput_ProducesExpectedUnixSeconds()
    {
        var valid = InvokePrivateStatic<long>(nameof(ParseTradingDay_ValidAndInvalidInput_ProducesExpectedUnixSeconds), "ParseTradingDay", "2026-03-01");
        var invalid = InvokePrivateStatic<long>(nameof(ParseTradingDay_ValidAndInvalidInput_ProducesExpectedUnixSeconds), "ParseTradingDay", "bad");

        var expected = new DateTimeOffset(DateTime.SpecifyKind(DateTime.ParseExact("2026-03-01", "yyyy-MM-dd", CultureInfo.InvariantCulture), DateTimeKind.Utc)).ToUnixTimeSeconds();
        Assert.AreEqual(expected, valid);
        Assert.IsGreaterThan(0L, invalid);
    }

    [TestMethod]
    public void ParseNewsTimestamp_ValidAndInvalidInput_ProducesExpectedUnixSeconds()
    {
        var valid = InvokePrivateStatic<long>(nameof(ParseNewsTimestamp_ValidAndInvalidInput_ProducesExpectedUnixSeconds), "ParseNewsTimestamp", "20260301T103000");
        var invalid = InvokePrivateStatic<long>(nameof(ParseNewsTimestamp_ValidAndInvalidInput_ProducesExpectedUnixSeconds), "ParseNewsTimestamp", "bad");

        var expected = new DateTimeOffset(DateTime.SpecifyKind(DateTime.ParseExact("20260301T103000", "yyyyMMdd'T'HHmmss", CultureInfo.InvariantCulture), DateTimeKind.Utc)).ToUnixTimeSeconds();
        Assert.AreEqual(expected, valid);
        Assert.IsGreaterThan(0L, invalid);
    }

    [TestMethod]
    public void ParseDouble_ValidAndInvalidInput_ProducesExpectedValue()
    {
        var valid = InvokePrivateStatic<double>(nameof(ParseDouble_ValidAndInvalidInput_ProducesExpectedValue), "ParseDouble", "12.34");
        var invalid = InvokePrivateStatic<double>(nameof(ParseDouble_ValidAndInvalidInput_ProducesExpectedValue), "ParseDouble", "bad");

        Assert.AreEqual(12.34d, valid, 0.0001d);
        Assert.AreEqual(0d, invalid, 0.0001d);
    }

    [TestMethod]
    public void ParsePercent_ValidAndNullInput_ProducesExpectedValue()
    {
        var valid = InvokePrivateStatic<double>(nameof(ParsePercent_ValidAndNullInput_ProducesExpectedValue), "ParsePercent", "1.23%");
        var invalid = InvokePrivateStatic<double>(nameof(ParsePercent_ValidAndNullInput_ProducesExpectedValue), "ParsePercent", null);

        Assert.AreEqual(1.23d, valid, 0.0001d);
        Assert.AreEqual(0d, invalid, 0.0001d);
    }

    [TestMethod]
    public void ParseLong_ValidIntegerFractionalAndInvalidInput_ProducesExpectedValue()
    {
        var integer = InvokePrivateStatic<long>(nameof(ParseLong_ValidIntegerFractionalAndInvalidInput_ProducesExpectedValue), "ParseLong", "10");
        var fractional = InvokePrivateStatic<long>(nameof(ParseLong_ValidIntegerFractionalAndInvalidInput_ProducesExpectedValue), "ParseLong", "10.6");
        var invalid = InvokePrivateStatic<long>(nameof(ParseLong_ValidIntegerFractionalAndInvalidInput_ProducesExpectedValue), "ParseLong", "bad");

        Assert.AreEqual(10L, integer);
        Assert.AreEqual(11L, fractional);
        Assert.AreEqual(0L, invalid);
    }

    private static AlphaVantageClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory, RateLimitConfiguration? rateLimit = null)
    {
        var handler = new StubHttpMessageHandler(responseFactory);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.alphavantage.test/")
        };

        return new AlphaVantageClient(httpClient, new SecretValue(ApiKey), rateLimit);
    }

    private static Func<HttpRequestMessage, HttpResponseMessage> JsonResponse(HttpStatusCode statusCode, string body)
    {
        return _ => new HttpResponseMessage
        {
            StatusCode = statusCode,
            Content = new StringContent(body)
        };
    }

    private static T InvokePrivateStatic<T>(string testName, string methodName, string? input)
    {
        var method = typeof(AlphaVantageClient).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, $"Could not find method '{methodName}' for test '{testName}'.");

        var result = method!.Invoke(null, [input]);
        Assert.IsNotNull(result, $"Method '{methodName}' returned null for test '{testName}'.");
        return (T)result!;
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
