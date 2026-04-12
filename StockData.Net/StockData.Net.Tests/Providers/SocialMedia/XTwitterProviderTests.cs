using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using StockData.Net.Providers;
using StockData.Net.Providers.SocialMedia;

namespace StockData.Net.Tests.Providers.SocialMedia;

[TestClass]
public class XTwitterProviderTests
{
    [TestMethod]
    public async Task GivenValidResponse_WhenGettingPosts_ThenMapsAllSocialPostFields()
    {
        var payload = """
        {
          "data": [
            {
              "id": "123",
              "text": "Breaking $AAPL move",
              "created_at": "2026-04-12T14:30:00Z",
              "author_id": "456",
              "public_metrics": { "retweet_count": 5 }
            }
          ],
          "includes": {
            "users": [
              { "id": "456", "username": "Reuters" }
            ]
          }
        }
        """;

        var provider = CreateProvider(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });

        var result = await provider.GetPostsAsync(new SocialFeedRequest
        {
            Handles = new[] { "Reuters" },
            Query = "$AAPL",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        });

        Assert.HasCount(1, result.Posts);
        var post = result.Posts[0];
        Assert.AreEqual("123", post.PostId);
        Assert.AreEqual("Reuters", post.AuthorHandle);
        Assert.AreEqual("Breaking $AAPL move", post.Content);
        Assert.AreEqual(DateTimeOffset.Parse("2026-04-12T14:30:00Z"), post.PostedAt);
        Assert.AreEqual("https://x.com/Reuters/status/123", post.Url);
        Assert.AreEqual("X", post.SourcePlatform);
        Assert.AreEqual(5, post.RetweetCount);
        CollectionAssert.AreEqual(new[] { "$AAPL" }, post.MatchedKeywords.ToArray());
        Assert.IsEmpty(result.Errors);
    }

    [TestMethod]
    public async Task GivenEmptyData_WhenGettingPosts_ThenReturnsEmptyList()
    {
        var payload = """{ "data": [], "includes": { "users": [] } }""";
        var provider = CreateProvider(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });

        var result = await provider.GetPostsAsync(new SocialFeedRequest
        {
            Query = "inflation",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        });

        Assert.IsEmpty(result.Posts);
    }

        [TestMethod]
        public async Task GivenMultipleHandlesWithPosts_WhenGettingPosts_ThenMergesAndSortsByPostedAtDescending()
        {
                var firstPayload = """
                {
                    "data": [
                        {
                            "id": "201",
                            "text": "older post",
                            "created_at": "2026-04-12T08:00:00Z",
                            "author_id": "u1",
                            "public_metrics": { "retweet_count": 1 }
                        }
                    ],
                    "includes": { "users": [ { "id": "u1", "username": "Reuters" } ] }
                }
                """;

                var secondPayload = """
                {
                    "data": [
                        {
                            "id": "202",
                            "text": "newer post",
                            "created_at": "2026-04-12T09:00:00Z",
                            "author_id": "u2",
                            "public_metrics": { "retweet_count": 2 }
                        }
                    ],
                    "includes": { "users": [ { "id": "u2", "username": "Bloomberg" } ] }
                }
                """;

                var provider = CreateProvider(
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                                Content = new StringContent(firstPayload, Encoding.UTF8, "application/json")
                        },
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                                Content = new StringContent(secondPayload, Encoding.UTF8, "application/json")
                        });

                var result = await provider.GetPostsAsync(new SocialFeedRequest
                {
                        Handles = new[] { "Reuters", "Bloomberg" },
                        MaxResults = 10,
                        LookbackHours = 24,
                        Tier = ProviderTier.Free
                });

                Assert.HasCount(2, result.Posts);
                Assert.AreEqual("202", result.Posts[0].PostId);
                Assert.AreEqual("201", result.Posts[1].PostId);
        }

        [TestMethod]
        public async Task GivenKeywordQuery_WhenGettingPosts_ThenSetsMatchedKeywordsOnlyForMatchingPosts()
        {
                var payload = """
                {
                    "data": [
                        {
                            "id": "301",
                            "text": "Inflation trend is cooling",
                            "created_at": "2026-04-12T12:00:00Z",
                            "author_id": "u1",
                            "public_metrics": { "retweet_count": 2 }
                        },
                        {
                            "id": "302",
                            "text": "Labor market update",
                            "created_at": "2026-04-12T11:00:00Z",
                            "author_id": "u1",
                            "public_metrics": { "retweet_count": 1 }
                        }
                    ],
                    "includes": { "users": [ { "id": "u1", "username": "Reuters" } ] }
                }
                """;

                var provider = CreateProvider(new HttpResponseMessage(HttpStatusCode.OK)
                {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });

                var result = await provider.GetPostsAsync(new SocialFeedRequest
                {
                        Query = "inflation",
                        MaxResults = 10,
                        LookbackHours = 24,
                        Tier = ProviderTier.Free
                });

                Assert.HasCount(2, result.Posts);
                CollectionAssert.AreEqual(new[] { "inflation" }, result.Posts.First(post => post.PostId == "301").MatchedKeywords.ToArray());
                Assert.IsEmpty(result.Posts.First(post => post.PostId == "302").MatchedKeywords);
        }

        [TestMethod]
        public async Task GivenHandlesAndQuery_WhenGettingPosts_ThenBuildsAndQueryForSearch()
        {
                var payload = """{ "data": [], "includes": { "users": [] } }""";
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };

                var provider = CreateProvider(response, out var handler);

                await provider.GetPostsAsync(new SocialFeedRequest
                {
                        Handles = new[] { "Reuters" },
                        Query = "inflation",
                        MaxResults = 10,
                        LookbackHours = 24,
                        Tier = ProviderTier.Free
                });

                Assert.HasCount(1, handler.Requests);
                var requestUri = handler.Requests[0].RequestUri;
                Assert.IsNotNull(requestUri);
                var decoded = WebUtility.UrlDecode(requestUri!.Query);
                StringAssert.Contains(decoded, "query=(from:Reuters) inflation");
        }

        [TestMethod]
        public async Task GivenKeywordQueryWithNoMatches_WhenGettingPosts_ThenReturnsEmptyList()
        {
                var payload = """{ "data": [], "includes": { "users": [] } }""";
                var provider = CreateProvider(new HttpResponseMessage(HttpStatusCode.OK)
                {
                        Content = new StringContent(payload, Encoding.UTF8, "application/json")
                });

                var result = await provider.GetPostsAsync(new SocialFeedRequest
                {
                        Query = "unmatched-term",
                        MaxResults = 10,
                        LookbackHours = 24,
                        Tier = ProviderTier.Free
                });

                Assert.IsEmpty(result.Posts);
        }

    [TestMethod]
    public async Task Given429Response_WhenGettingPosts_ThenThrowsXRateLimitException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.Add("x-rate-limit-reset", DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString());

        var provider = CreateProvider(response);

        await Assert.ThrowsExactlyAsync<XRateLimitException>(() => provider.GetPostsAsync(new SocialFeedRequest
        {
            Query = "fed",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        }));
    }

    [TestMethod]
    public async Task GivenUnauthorizedResponse_WhenGettingPosts_ThenThrowsInvalidOperationException()
    {
        var provider = CreateProvider(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.GetPostsAsync(new SocialFeedRequest
        {
            Query = "fed",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        }));
    }

    [TestMethod]
    public async Task GivenUnauthorizedResponseContainingToken_WhenGettingPosts_ThenErrorDoesNotExposeToken()
    {
        const string token = "token-secret-123";
        var provider = CreateProvider(new HttpResponseMessage(HttpStatusCode.Unauthorized), token);

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.GetPostsAsync(new SocialFeedRequest
        {
            Query = "fed",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        }));

        Assert.IsFalse(ex.Message.Contains(token, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GivenServerError_WhenGettingPosts_ThenThrowsSocialMediaServiceUnavailableException()
    {
        var provider = CreateProvider(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await Assert.ThrowsExactlyAsync<SocialMediaServiceUnavailableException>(() => provider.GetPostsAsync(new SocialFeedRequest
        {
            Query = "fed",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        }));
    }

    [TestMethod]
    public async Task GivenMissingToken_WhenGettingPosts_ThenThrowsInvalidOperationException()
    {
        var provider = CreateProvider(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
        }, token: null);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => provider.GetPostsAsync(new SocialFeedRequest
        {
            Query = "fed",
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        }));
    }

    [TestMethod]
    public async Task GivenMixedHandleResponses_WhenGettingPosts_ThenIsolatesPerHandleErrors()
    {
        var successPayload = """
        {
          "data": [
            {
              "id": "101",
              "text": "macro update",
              "created_at": "2026-04-12T10:00:00Z",
              "author_id": "u1",
              "public_metrics": { "retweet_count": 1 }
            }
          ],
          "includes": { "users": [ { "id": "u1", "username": "goodhandle" } ] }
        }
        """;

        var provider = CreateProvider(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(successPayload, Encoding.UTF8, "application/json")
            },
            new HttpResponseMessage(HttpStatusCode.BadRequest));

        var result = await provider.GetPostsAsync(new SocialFeedRequest
        {
            Handles = new[] { "goodhandle", "badhandle" },
            MaxResults = 10,
            LookbackHours = 24,
            Tier = ProviderTier.Free
        });

        Assert.HasCount(1, result.Posts);
        Assert.HasCount(1, result.Errors);
        Assert.AreEqual("badhandle", result.Errors[0].Handle);
    }

    private static XTwitterProvider CreateProvider(HttpResponseMessage response, string? token = "token123")
    {
        return CreateProvider(new[] { response }, token);
    }

    private static XTwitterProvider CreateProvider(HttpResponseMessage response, out QueueHttpMessageHandler handler, string? token = "token123")
    {
        return CreateProvider(new[] { response }, out handler, token);
    }

    private static XTwitterProvider CreateProvider(HttpResponseMessage first, HttpResponseMessage second, string? token = "token123")
    {
        return CreateProvider(new[] { first, second }, token);
    }

    private static XTwitterProvider CreateProvider(IEnumerable<HttpResponseMessage> responses, string? token = "token123")
    {
        return CreateProvider(responses, out _, token);
    }

    private static XTwitterProvider CreateProvider(IEnumerable<HttpResponseMessage> responses, out QueueHttpMessageHandler handler, string? token = "token123")
    {
        handler = new QueueHttpMessageHandler(responses);
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.twitter.com/")
        };

        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("xtwitter").Returns(httpClient);

        var values = new Dictionary<string, string?>
        {
            ["X_BEARER_TOKEN"] = token,
            ["XTwitter:rateLimitWindowSeconds"] = "900",
            ["XTwitter:maxResults"] = "10",
            ["XTwitter:maxLookbackHours"] = "168"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new XTwitterProvider(factory, configuration);
    }

    private sealed class QueueHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;
        public List<HttpRequestMessage> Requests { get; } = new();

        public QueueHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
        {
            _responses = new Queue<HttpResponseMessage>(responses);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (_responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"data\":[]}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(_responses.Dequeue());
        }
    }
}
