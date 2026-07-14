using System.Diagnostics;
using System.Net;
using System.Text;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeStatsClientTests
{
    [Fact]
    public async Task GetStatsAsync_SendsExactlyOneGetToStatsEndpointWithExpectedHeaders()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(StatsResponse());
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeStatsClient(httpClient);

        var result = await client.GetStatsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        var captured = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, captured.Method);
        Assert.Equal(
            "https://www.pathofexile.com/api/trade/data/stats",
            captured.RequestUri?.ToString());
        Assert.Contains("application/json", captured.Accept);
        Assert.Contains("PoEnhance/", captured.UserAgent);
        Assert.Equal(string.Empty, captured.Content);
    }

    [Fact]
    public async Task GetStatsAsync_UsesInjectedHttpClientAndDoesNotAddCookieOrAuthorizationHeaders()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(StatsResponse());
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeStatsClient(httpClient);

        await client.GetStatsAsync(CancellationToken.None);

        var captured = Assert.Single(handler.Requests);
        Assert.False(captured.Headers.ContainsKey("Cookie"));
        Assert.False(captured.Headers.ContainsKey("Authorization"));
        Assert.DoesNotContain(captured.Headers.Keys, key =>
            key.Contains("POESESSID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetStatsAsync_SuccessParsesCatalogAndRateLimitHeaders()
    {
        var response = StatsResponse();
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Policy", "trade-data");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Rules", "Ip");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip", "30:60:0");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip-State", "2:60:0");
        var handler = RecordingHttpMessageHandler.RespondingWith(response);
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeStatsClient(httpClient);

        var result = await client.GetStatsAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, result.HttpStatusCode);
        Assert.Equal("trade-data", result.RateLimitSnapshot?.Policy);
        Assert.Equal("explicit.stat", Assert.Single(result.Catalog!.Entries).Id);
    }

    [Fact]
    public async Task GetStatsAsync_MalformedSuccessJsonReturnsStructuredMalformedResponse()
    {
        const string BodyMarker = "full-body-marker";
        var handler = RecordingHttpMessageHandler.RespondingWith(StatsResponse(
            $$"""{"{{BodyMarker}}":true}"""));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeStatsClient(httpClient);

        var result = await client.GetStatsAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.MalformedResponse, diagnostic.Code);
        Assert.DoesNotContain(BodyMarker, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStatsAsync_NonSuccessIsStructured()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(
            StatsResponse("""{"error":"nope"}""", HttpStatusCode.BadGateway));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeStatsClient(httpClient);

        var result = await client.GetStatsAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadGateway, result.HttpStatusCode);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task GetStatsAsync_CallerCancellationAndTimeoutAreStructured()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var cancelledHandler = RecordingHttpMessageHandler.Throwing(new OperationCanceledException(cancellation.Token));
        using var cancelledHttpClient = new HttpClient(cancelledHandler);
        var cancelledClient = new PathOfExileTradeStatsClient(cancelledHttpClient);

        var cancelled = await cancelledClient.GetStatsAsync(cancellation.Token);

        var timeoutHandler = RecordingHttpMessageHandler.Throwing(new TaskCanceledException("timeout"));
        using var timeoutHttpClient = new HttpClient(timeoutHandler);
        var timeoutClient = new PathOfExileTradeStatsClient(timeoutHttpClient);

        var timeout = await timeoutClient.GetStatsAsync(CancellationToken.None);

        Assert.True(cancelled.IsCancelled);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.CallerCancellation, Assert.Single(cancelled.Diagnostics).Code);
        Assert.True(timeout.IsTimeout);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.Timeout, Assert.Single(timeout.Diagnostics).Code);
    }

    [Fact]
    public async Task GetStatsAsync_NetworkFailureIsStructured()
    {
        var handler = RecordingHttpMessageHandler.Throwing(new HttpRequestException("network down"));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeStatsClient(httpClient);

        var result = await client.GetStatsAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NetworkFailure, Assert.Single(result.Diagnostics).Code);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetStatsAsync_ResponseSizeBoundIsEnforced()
    {
        var oversizedBody = new string('x', 65);
        var handler = RecordingHttpMessageHandler.RespondingWith(StatsResponse(oversizedBody));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeStatsClient(
            httpClient,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeStatsResponseParser(),
            new PathOfExileTradeRateLimitParser(),
            maximumResponseBodyBytes: 64);

        var result = await client.GetStatsAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.ResponseTooLarge, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task GetStatsAsync_DoesNotRetryOrWaitForRateLimit()
    {
        var response = StatsResponse("""{}""", HttpStatusCode.TooManyRequests);
        response.Headers.TryAddWithoutValidation("Retry-After", "120");
        var handler = RecordingHttpMessageHandler.RespondingWith(response);
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeStatsClient(httpClient);
        var stopwatch = Stopwatch.StartNew();

        var result = await client.GetStatsAsync(CancellationToken.None);

        stopwatch.Stop();
        Assert.False(result.IsSuccess);
        Assert.Equal(120, result.RateLimitSnapshot?.RetryAfterSeconds);
        Assert.Single(handler.Requests);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    private static HttpResponseMessage StatsResponse(
        string body = """{"result":[{"id":"explicit","label":"Explicit","entries":[{"id":"explicit.stat","text":"+# to maximum Life","type":"explicit"}]}]}""",
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

        public static RecordingHttpMessageHandler Throwing(Exception exception)
        {
            return new RecordingHttpMessageHandler(
            [
                (_, _) => Task.FromException<HttpResponseMessage>(exception),
            ]);
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
