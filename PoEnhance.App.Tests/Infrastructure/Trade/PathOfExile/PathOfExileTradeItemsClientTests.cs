using System.Diagnostics;
using System.Net;
using System.Text;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeItemsClientTests
{
    [Fact]
    public async Task GetItemsAsync_SendsExactlyOneGetToItemsEndpointWithExpectedHeaders()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(ItemsResponse());
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeItemsClient(httpClient);

        var result = await client.GetItemsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var captured = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, captured.Method);
        Assert.Equal(
            "https://www.pathofexile.com/api/trade/data/items",
            captured.RequestUri?.ToString());
        Assert.Contains("application/json", captured.Accept);
        Assert.Contains("PoEnhance/", captured.UserAgent);
        Assert.Equal(string.Empty, captured.Content);
        Assert.False(captured.Headers.ContainsKey("Cookie"));
        Assert.False(captured.Headers.ContainsKey("Authorization"));
    }

    [Fact]
    public async Task GetItemsAsync_SuccessParsesCatalogAndRateLimitHeaders()
    {
        var response = ItemsResponse();
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Policy", "trade-data");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Rules", "Ip");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip", "30:60:0");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip-State", "2:60:0");
        var handler = RecordingHttpMessageHandler.RespondingWith(response);
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeItemsClient(httpClient);

        var result = await client.GetItemsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, result.HttpStatusCode);
        Assert.Equal("trade-data", result.RateLimitSnapshot?.Policy);
        Assert.Equal("Moonbender's Wing", Assert.Single(result.Catalog!.Entries).Name);
    }

    [Fact]
    public async Task GetItemsAsync_ResponseSizeBoundIsEnforced()
    {
        var oversizedBody = new string('x', 65);
        var handler = RecordingHttpMessageHandler.RespondingWith(ItemsResponse(oversizedBody));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeItemsClient(
            httpClient,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeItemsResponseParser(),
            new PathOfExileTradeRateLimitParser(),
            maximumResponseBodyBytes: 64);

        var result = await client.GetItemsAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.ResponseTooLarge, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task GetItemsAsync_MalformedSuccessJsonReturnsStructuredMalformedResponse()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(ItemsResponse("""{"notResult":true}"""));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeItemsClient(httpClient);

        var result = await client.GetItemsAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.MalformedResponse, Assert.Single(result.Diagnostics).Code);
        Assert.Contains(
            result.ParserDiagnostics,
            diagnostic => diagnostic.Code == PathOfExileTradeItemsDiagnosticCodes.MissingResultCollection);
    }

    [Fact]
    public async Task GetItemsAsync_DoesNotRetryOrWaitForRateLimit()
    {
        var response = ItemsResponse("""{}""", HttpStatusCode.TooManyRequests);
        response.Headers.TryAddWithoutValidation("Retry-After", "120");
        var handler = RecordingHttpMessageHandler.RespondingWith(response);
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeItemsClient(httpClient);
        var stopwatch = Stopwatch.StartNew();

        var result = await client.GetItemsAsync(CancellationToken.None);

        stopwatch.Stop();
        Assert.False(result.IsSuccess);
        Assert.Equal(120, result.RateLimitSnapshot?.RetryAfterSeconds);
        Assert.Single(handler.Requests);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    private static HttpResponseMessage ItemsResponse(
        string body = """{"result":[{"id":"weapon","label":"Weapons","entries":[{"name":"Moonbender's Wing","type":"Tomahawk","flags":{"unique":true}}]}]}""",
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> responders;

        private RecordingHttpMessageHandler(
            IEnumerable<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> responders)
        {
            this.responders = new Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>>(
                responders);
        }

        public List<CapturedHttpRequest> Requests { get; } = [];

        public static RecordingHttpMessageHandler RespondingWith(params HttpResponseMessage[] responses)
        {
            return new RecordingHttpMessageHandler(responses.Select(response =>
                new Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>(
                    (_, _) => Task.FromResult(response))));
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(await CapturedHttpRequest.FromRequestAsync(request, cancellationToken));

            if (responders.Count == 0)
            {
                throw new InvalidOperationException("No fake HTTP response was configured.");
            }

            return await responders.Dequeue()(request, cancellationToken);
        }
    }

    private sealed record CapturedHttpRequest(
        HttpMethod Method,
        Uri? RequestUri,
        IReadOnlyDictionary<string, string[]> Headers,
        IReadOnlyList<string> Accept,
        string UserAgent,
        string Content)
    {
        public static async Task<CapturedHttpRequest> FromRequestAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var contentHeaders = request.Content?.Headers
                .Select(header => header) ?? [];
            var headers = request.Headers
                .Concat(contentHeaders)
                .ToDictionary(
                    header => header.Key,
                    header => header.Value.ToArray(),
                    StringComparer.OrdinalIgnoreCase);

            return new CapturedHttpRequest(
                request.Method,
                request.RequestUri,
                headers,
                request.Headers.Accept.Select(value => value.MediaType ?? string.Empty).ToArray(),
                string.Join(" ", request.Headers.UserAgent.Select(value => value.ToString())),
                content);
        }
    }
}
