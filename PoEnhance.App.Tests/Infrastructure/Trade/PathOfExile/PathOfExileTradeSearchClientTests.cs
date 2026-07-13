using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeSearchClientTests
{
    private const string League = "Mercenaries";

    [Fact]
    public async Task SearchAsync_SendsOnePostToSearchEndpointWithExpectedHeadersAndJsonBody()
    {
        var request = SearchRequest();
        var expectedJson = PathOfExileTradeJson.SerializeSearchRequest(request);
        var handler = RecordingHttpMessageHandler.RespondingWith(SearchResponse());
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var result = await client.SearchAsync(request, League, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var captured = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal(
            "https://www.pathofexile.com/api/trade/search/Mercenaries",
            captured.RequestUri?.ToString());
        Assert.Equal("application/json", captured.ContentType);
        Assert.Contains("application/json", captured.Accept);
        Assert.Contains("PoEnhance/", captured.UserAgent);
        Assert.Equal(expectedJson, captured.Content);
    }

    [Fact]
    public async Task SearchAsync_SuccessParsesSearchResponseAndPreservesResultOrder()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(SearchResponse(
            """{"id":"search-1","result":["first","second","third"],"total":3,"inexact":false}"""));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var result = await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, result.HttpStatusCode);
        Assert.Equal("search-1", result.Response?.Id);
        Assert.Equal(["first", "second", "third"], result.Response?.Result);
        Assert.Equal(3, result.Response?.Total);
        Assert.False(result.Response?.Inexact);
        Assert.Null(result.ProviderError);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task SearchAsync_SuccessAcceptsZeroResultSearch()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(SearchResponse(
            """{"id":"search-empty","result":[],"total":0}"""));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var result = await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Response?.Result ?? ["unexpected"]);
        Assert.Equal(0, result.Response?.Total);
    }

    [Fact]
    public async Task SearchAsync_ParsesRateLimitHeadersOnSuccess()
    {
        var response = SearchResponse();
        AddRateLimitHeaders(response);
        var handler = RecordingHttpMessageHandler.RespondingWith(response);
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var result = await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("trade-search", result.RateLimitSnapshot?.Policy);
        var rule = Assert.Single(result.RateLimitSnapshot?.Rules ?? []);
        Assert.Equal("Ip", rule.RuleName);
        Assert.Equal(30, rule.MaximumRequestCount);
        Assert.Equal(60, rule.IntervalSeconds);
        Assert.Equal(2, rule.CurrentRequestCount);
        Assert.Equal(0, rule.CurrentTimeoutSeconds);
    }

    [Fact]
    public async Task SearchAsync_NonSuccessParsesRateLimitHeadersAndProviderError()
    {
        var response = SearchResponse(
            """{"error":{"code":3,"message":"Invalid query shape."}}""",
            HttpStatusCode.BadRequest);
        AddRateLimitHeaders(response);
        var handler = RecordingHttpMessageHandler.RespondingWith(response);
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var result = await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.HttpStatusCode);
        Assert.Equal("3", result.ProviderError?.Code);
        Assert.Equal("Invalid query shape.", result.ProviderError?.Message);
        Assert.Equal("Invalid query shape.", Assert.Single(result.Diagnostics).Message);
        Assert.Equal(
            PathOfExileTradeHttpDiagnosticCodes.ProviderDeclaredError,
            Assert.Single(result.Diagnostics).Code);
        Assert.Equal("trade-search", result.RateLimitSnapshot?.Policy);
    }

    [Fact]
    public async Task SearchAsync_NonSuccessWithoutProviderErrorReturnsStatusDiagnostic()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(SearchResponse(
            """{"not":"a provider error"}""",
            HttpStatusCode.InternalServerError));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var result = await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.InternalServerError, result.HttpStatusCode);
        Assert.Null(result.ProviderError);
        Assert.Equal(
            PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task SearchAsync_MalformedProviderErrorReturnsMalformedResponseDiagnostic()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(SearchResponse(
            """{"error":{"message":"missing code"}}""",
            HttpStatusCode.BadRequest));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var result = await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.HttpStatusCode);
        Assert.Null(result.ProviderError);
        Assert.Equal(
            PathOfExileTradeHttpDiagnosticCodes.MalformedResponse,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task SearchAsync_MalformedSuccessJsonFailsWithoutEchoingBody()
    {
        const string BodyMarker = "full-body-marker";
        var handler = RecordingHttpMessageHandler.RespondingWith(SearchResponse(
            $$"""{"{{BodyMarker}}":true}"""));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var result = await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var diagnostic = Assert.Single(result.Diagnostics);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.MalformedResponse, diagnostic.Code);
        Assert.DoesNotContain(BodyMarker, diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_RejectsOversizedResponseBodyWithoutParsingFullBody()
    {
        var oversizedBody = new string('x', 65);
        var handler = RecordingHttpMessageHandler.RespondingWith(SearchResponse(oversizedBody));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(
            httpClient,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeResponseParser(),
            new PathOfExileTradeRateLimitParser(),
            maximumResponseBodyBytes: 64);

        var result = await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(
            PathOfExileTradeHttpDiagnosticCodes.ResponseTooLarge,
            Assert.Single(result.Diagnostics).Code);
        Assert.DoesNotContain(oversizedBody, Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_NetworkExceptionReturnsNetworkDiagnostic()
    {
        var handler = RecordingHttpMessageHandler.Throwing(new HttpRequestException("network down"));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var result = await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NetworkFailure, Assert.Single(result.Diagnostics).Code);
        Assert.Null(result.HttpStatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_CallerCancellationIsDistinctFromTimeout()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var handler = RecordingHttpMessageHandler.Throwing(new OperationCanceledException(cancellation.Token));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var result = await client.SearchAsync(SearchRequest(), League, cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsCancelled);
        Assert.False(result.IsTimeout);
        Assert.Equal(
            PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task SearchAsync_TimeoutIsDistinctFromCallerCancellation()
    {
        var handler = RecordingHttpMessageHandler.Throwing(new TaskCanceledException("timeout"));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var result = await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsCancelled);
        Assert.True(result.IsTimeout);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.Timeout, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public async Task SearchAsync_NullRequestInvalidLeagueAndSerializationFailureSendNoRequest()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(SearchResponse());
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        var nullRequest = await client.SearchAsync(null, League, CancellationToken.None);
        var invalidLeague = await client.SearchAsync(SearchRequest(), " ", CancellationToken.None);
        var serializationFailure = await client.SearchAsync(
            SearchRequest(filterValue: typeof(string)),
            League,
            CancellationToken.None);

        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NullRequest, Assert.Single(nullRequest.Diagnostics).Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint, Assert.Single(invalidLeague.Diagnostics).Code);
        Assert.Equal(
            PathOfExileTradeHttpDiagnosticCodes.SerializationFailed,
            Assert.Single(serializationFailure.Diagnostics).Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SearchAsync_DoesNotRetry429AndDoesNotWaitForRetryAfter()
    {
        var response = SearchResponse(
            """{"error":{"code":"RateLimit","message":"You are rate limited."}}""",
            HttpStatusCode.TooManyRequests);
        response.Headers.TryAddWithoutValidation("Retry-After", "120");
        var handler = RecordingHttpMessageHandler.RespondingWith(response);
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);
        var stopwatch = Stopwatch.StartNew();

        var result = await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        stopwatch.Stop();
        Assert.False(result.IsSuccess);
        Assert.Equal(120, result.RateLimitSnapshot?.RetryAfterSeconds);
        Assert.Single(handler.Requests);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SearchAsync_UsesInjectedHttpClientAndDoesNotAddCookieOrAuthorizationHeaders()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(SearchResponse());
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeSearchClient(httpClient);

        await client.SearchAsync(SearchRequest(), League, CancellationToken.None);

        var captured = Assert.Single(handler.Requests);
        Assert.False(captured.Headers.ContainsKey("Cookie"));
        Assert.False(captured.Headers.ContainsKey("Authorization"));
        Assert.DoesNotContain(captured.Headers.Keys, key =>
            key.Contains("POESESSID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SearchClient_DoesNotTakeLoggerDependency()
    {
        var dependencyTypes = typeof(PathOfExileTradeSearchClient)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(typeof(PathOfExileTradeSearchClient)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType));

        Assert.DoesNotContain(dependencyTypes, type =>
            type.FullName?.Contains("ILogger", StringComparison.OrdinalIgnoreCase) == true ||
            type.FullName?.Contains("Serilog", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void CoreAssembly_KeepsHttpAndProviderTradeTypesOut()
    {
        var coreAssembly = typeof(TradeSearchDraft).Assembly;
        var referencedNames = coreAssembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("System.Net.Http", referencedNames);
        Assert.DoesNotContain(coreAssembly.GetTypes(), type =>
            type.FullName?.Contains("PathOfExileTrade", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void SearchClient_DoesNotInvokeFetchCurrencyExchangePublicStashOrUi()
    {
        var providerTypes = typeof(PathOfExileTradeSearchClient).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Infrastructure.Trade.PathOfExile")
            .ToArray();
        var priceCheckerTypes = typeof(PriceCheckerWindowController).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Features.PriceChecking")
            .ToArray();

        Assert.DoesNotContain(providerTypes, type =>
            Contains(type, "CurrencyExchange") ||
            Contains(type, "PublicStash"));
        Assert.DoesNotContain(typeof(PathOfExileTradeSearchClient).GetMethods(), method =>
            method.Name.Contains("Fetch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(priceCheckerTypes.SelectMany(ReferencedMemberTypes), type =>
            Contains(type, "PathOfExileTradeSearchClient"));
    }

    private static PathOfExileTradeSearchRequest SearchRequest(object? filterValue = null)
    {
        var filters = filterValue is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object> { ["bad"] = filterValue };

        return new PathOfExileTradeSearchRequest
        {
            Query = new PathOfExileTradeSearchQuery
            {
                Status = new PathOfExileTradeSearchStatus
                {
                    Option = "securable",
                },
                Type = "Titan Plate",
                Filters = filters,
            },
            Sort = new PathOfExileTradeSearchSort(),
        };
    }

    private static HttpResponseMessage SearchResponse(
        string body = """{"id":"search-1","result":["result-1"],"total":1}""",
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private static void AddRateLimitHeaders(HttpResponseMessage response)
    {
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Policy", "trade-search");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Rules", "Ip");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip", "30:60:0");
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Ip-State", "2:60:0");
    }

    private static IEnumerable<Type> ReferencedMemberTypes(Type type)
    {
        const BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        return type.GetConstructors(flags).SelectMany(constructor =>
                constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(type.GetFields(flags).Select(field => field.FieldType))
            .Concat(type.GetProperties(flags).Select(property => property.PropertyType))
            .Concat(type.GetMethods(flags).Select(method => method.ReturnType))
            .Concat(type.GetMethods(flags).SelectMany(method =>
                method.GetParameters().Select(parameter => parameter.ParameterType)));
    }

    private static bool Contains(Type type, string value)
    {
        return type.FullName?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;
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
        string? ContentType,
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
                request.Content?.Headers.ContentType?.MediaType,
                request.Headers.Accept.Select(value => value.MediaType ?? string.Empty).ToArray(),
                string.Join(" ", request.Headers.UserAgent.Select(value => value.ToString())),
                content);
        }
    }
}
