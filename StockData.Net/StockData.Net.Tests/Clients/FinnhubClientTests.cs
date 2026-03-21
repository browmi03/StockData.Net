using System.Net;
using StockData.Net.Clients.Finnhub;
using StockData.Net.Security;

namespace StockData.Net.Tests.Clients;

[TestClass]
public class FinnhubClientTests
{
    private const string ApiKey = "Key123456Token";

    [TestMethod]
    public async Task GetMarketNewsAsync_UsesNewsEndpointWithCategory()
    {
        string? requestedUri = null;
        var client = CreateClient(request =>
        {
            requestedUri = request.RequestUri?.ToString();
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]")
            };
        });

        var result = await client.GetMarketNewsAsync("general");

        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(requestedUri));
        StringAssert.Contains(requestedUri!, "news?category=general");
        StringAssert.Contains(requestedUri!, "token=");
    }

    [TestMethod]
    public async Task GetRecommendationTrendsAsync_UsesRecommendationEndpoint()
    {
        string? requestedUri = null;
        var client = CreateClient(request =>
        {
            requestedUri = request.RequestUri?.ToString();
            return new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("[]")
            };
        });

        var result = await client.GetRecommendationTrendsAsync("AAPL");

        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrWhiteSpace(requestedUri));
        StringAssert.Contains(requestedUri!, "stock/recommendation?symbol=AAPL");
        StringAssert.Contains(requestedUri!, "token=");
    }

    private static FinnhubClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var handler = new StubHttpMessageHandler(responseFactory);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://finnhub.io/api/v1/")
        };

        return new FinnhubClient(httpClient, new SecretValue(ApiKey));
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
