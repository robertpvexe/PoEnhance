using System.Diagnostics;
using System.Net;
using System.Reflection;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeFetchClientTests
{
    private const string QueryId = "search-query";

    [Fact]
    public async Task FetchAsync_ValidInputSendsExactlyOneGetToExistingFetchEndpoint()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(FetchResponse());
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(httpClient);

        var result = await client.FetchAsync(QueryId, ["id3", "id1", "id2"], CancellationToken.None);

        Assert.True(result.IsSuccess);
        var captured = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, captured.Method);
        Assert.Equal(
            "https://www.pathofexile.com/api/trade/fetch/id3,id1,id2?query=search-query",
            captured.RequestUri?.ToString());
        Assert.Contains("application/json", captured.Accept);
        Assert.Contains("PoEnhance/", captured.UserAgent);
        Assert.Null(captured.Content);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task FetchAsync_InvalidQueryIdSendsZeroRequests(string? queryId)
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(FetchResponse());
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(httpClient);

        var result = await client.FetchAsync(queryId, ["id1"], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint, Assert.Single(result.Diagnostics).Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task FetchAsync_NullOrEmptyResultIdsSendZeroRequests()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(FetchResponse());
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(httpClient);

        var nullIds = await client.FetchAsync(QueryId, null, CancellationToken.None);
        var emptyIds = await client.FetchAsync(QueryId, [], CancellationToken.None);

        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint, Assert.Single(nullIds.Diagnostics).Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint, Assert.Single(emptyIds.Diagnostics).Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task FetchAsync_BlankResultIdAndMoreThanTenIdsSendZeroRequests()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(FetchResponse());
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(httpClient);
        var tooManyIds = Enumerable.Range(1, PathOfExileTradeEndpointBuilder.MaximumFetchResultIds + 1)
            .Select(index => $"id{index}")
            .ToArray();

        var blank = await client.FetchAsync(QueryId, ["id1", " "], CancellationToken.None);
        var tooMany = await client.FetchAsync(QueryId, tooManyIds, CancellationToken.None);

        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint, Assert.Single(blank.Diagnostics).Code);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint, Assert.Single(tooMany.Diagnostics).Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task FetchAsync_DoesNotRetry429AndParsesRetryAfterWithoutWaiting()
    {
        var response = FetchResponse(
            """{"error":{"code":"RateLimit","message":"Rate limited."}}""",
            HttpStatusCode.TooManyRequests);
        response.Headers.TryAddWithoutValidation("Retry-After", "120");
        var handler = RecordingHttpMessageHandler.RespondingWith(response);
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(httpClient);
        var stopwatch = Stopwatch.StartNew();

        var result = await client.FetchAsync(QueryId, ["id1"], CancellationToken.None);

        stopwatch.Stop();
        Assert.False(result.IsSuccess);
        Assert.Equal(120, result.RateLimitSnapshot?.RetryAfterSeconds);
        Assert.Single(handler.Requests);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FetchAsync_DoesNotAddCookieOrAuthorizationHeaders()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(FetchResponse());
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(httpClient);

        await client.FetchAsync(QueryId, ["id1"], CancellationToken.None);

        var captured = Assert.Single(handler.Requests);
        Assert.False(captured.Headers.ContainsKey("Cookie"));
        Assert.False(captured.Headers.ContainsKey("Authorization"));
        Assert.DoesNotContain(captured.Headers.Keys, key =>
            key.Contains("POESESSID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FetchAsync_SuccessExposesParsedResponseAndRateLimitHeaders()
    {
        var response = FetchResponse("""
            {
              "result": [
                { "id": "result-1", "item": { "typeLine": "Titan Plate" }, "listing": { "price": { "amount": 1.5, "currency": "divine" } } },
                { "id": "result-2", "item": { "typeLine": "Gold Ring" }, "listing": {} }
              ]
            }
            """);
        AddRateLimitHeaders(response);
        var handler = RecordingHttpMessageHandler.RespondingWith(response);
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(httpClient);

        var result = await client.FetchAsync(QueryId, ["result-1", "result-2"], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, result.HttpStatusCode);
        Assert.Equal(["result-1", "result-2"], result.Response?.Result.Select(offer => offer.Id));
        Assert.Equal("Titan Plate", result.Response?.Result[0].Item.TypeLine);
        Assert.Equal(1.5m, result.Response?.Result[0].Listing.Price?.Amount);
        Assert.Equal("trade-fetch", result.RateLimitSnapshot?.Policy);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public async Task FetchAsync_PartiallyMalformedSuccessRemainsSuccessfulWithDiagnostics()
    {
        var handler = RecordingHttpMessageHandler.RespondingWith(FetchResponse("""
            {
              "result": [
                { "id": "result-1", "item": {}, "listing": {} },
                { "item": {}, "listing": {} }
              ]
            }
            """));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(httpClient);

        var result = await client.FetchAsync(QueryId, ["result-1", "bad"], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("result-1", Assert.Single(result.Response?.Result ?? []).Id);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.MalformedOffer, Assert.Single(result.Diagnostics).Code);
        Assert.Equal(1, Assert.Single(result.Diagnostics).ResultIndex);
    }

    [Fact]
    public async Task FetchAsync_NonSuccessParsesRateLimitHeadersAndProviderError()
    {
        var response = FetchResponse(
            """{"error":{"code":7,"message":"Bad fetch."}}""",
            HttpStatusCode.BadRequest);
        AddRateLimitHeaders(response);
        var handler = RecordingHttpMessageHandler.RespondingWith(response);
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(httpClient);

        var result = await client.FetchAsync(QueryId, ["id1"], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.HttpStatusCode);
        Assert.Equal("7", result.ProviderError?.Code);
        Assert.Equal("Bad fetch.", result.ProviderError?.Message);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.ProviderDeclaredError, Assert.Single(result.Diagnostics).Code);
        Assert.Equal("trade-fetch", result.RateLimitSnapshot?.Policy);
    }

    [Fact]
    public async Task FetchAsync_NonSuccessWithoutProviderErrorReturnsStatusFailure()
    {
        var response = FetchResponse("""{"not":"provider-error"}""", HttpStatusCode.InternalServerError);
        AddRateLimitHeaders(response);
        var handler = RecordingHttpMessageHandler.RespondingWith(response);
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(httpClient);

        var result = await client.FetchAsync(QueryId, ["id1"], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.InternalServerError, result.HttpStatusCode);
        Assert.Null(result.ProviderError);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus, Assert.Single(result.Diagnostics).Code);
        Assert.Equal("trade-fetch", result.RateLimitSnapshot?.Policy);
    }

    [Fact]
    public async Task FetchAsync_NetworkExceptionReturnsStructuredFailure()
    {
        var handler = RecordingHttpMessageHandler.Throwing(new HttpRequestException("network down"));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(httpClient);

        var result = await client.FetchAsync(QueryId, ["id1"], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.NetworkFailure, Assert.Single(result.Diagnostics).Code);
        Assert.Null(result.HttpStatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task FetchAsync_CallerCancellationAndTimeoutAreDistinct()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var cancelledHandler = RecordingHttpMessageHandler.Throwing(
            new OperationCanceledException(cancellation.Token));
        using var cancelledHttpClient = new HttpClient(cancelledHandler);
        var cancelledClient = new PathOfExileTradeFetchClient(cancelledHttpClient);

        var cancelled = await cancelledClient.FetchAsync(QueryId, ["id1"], cancellation.Token);

        var timeoutHandler = RecordingHttpMessageHandler.Throwing(new TaskCanceledException("timeout"));
        using var timeoutHttpClient = new HttpClient(timeoutHandler);
        var timeoutClient = new PathOfExileTradeFetchClient(timeoutHttpClient);
        var timeout = await timeoutClient.FetchAsync(QueryId, ["id1"], CancellationToken.None);

        Assert.True(cancelled.IsCancelled);
        Assert.False(cancelled.IsTimeout);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.CallerCancellation, Assert.Single(cancelled.Diagnostics).Code);
        Assert.False(timeout.IsCancelled);
        Assert.True(timeout.IsTimeout);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.Timeout, Assert.Single(timeout.Diagnostics).Code);
    }

    [Fact]
    public async Task FetchAsync_OversizedResponseIsRejected()
    {
        var oversizedBody = new string('x', 65);
        var handler = RecordingHttpMessageHandler.RespondingWith(FetchResponse(oversizedBody));
        using var httpClient = new HttpClient(handler);
        var client = new PathOfExileTradeFetchClient(
            httpClient,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeFetchResponseParser(),
            new PathOfExileTradeRateLimitParser(),
            maximumResponseBodyBytes: 64);

        var result = await client.FetchAsync(QueryId, ["id1"], CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(PathOfExileTradeHttpDiagnosticCodes.ResponseTooLarge, Assert.Single(result.Diagnostics).Code);
        Assert.DoesNotContain(oversizedBody, Assert.Single(result.Diagnostics).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FetchClient_KeepsHttpClientInjectedAndDoesNotTakeLoggerDependency()
    {
        var dependencyTypes = typeof(PathOfExileTradeFetchClient)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType))
            .Concat(typeof(PathOfExileTradeFetchClient)
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Select(field => field.FieldType))
            .ToArray();

        Assert.Contains(typeof(HttpClient), dependencyTypes);
        Assert.DoesNotContain(dependencyTypes, type =>
            type.FullName?.Contains("ILogger", StringComparison.OrdinalIgnoreCase) == true ||
            type.FullName?.Contains("Serilog", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void FetchClient_DoesNotIntroduceCoreDependencyUiInvocationOrSearchThenFetchOrchestration()
    {
        var coreAssembly = typeof(TradeSearchDraft).Assembly;
        var referencedNames = coreAssembly
            .GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name)
            .ToHashSet(StringComparer.Ordinal);
        var priceCheckerTypes = typeof(PriceCheckerWindowController).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Features.PriceChecking")
            .ToArray();

        Assert.DoesNotContain("System.Net.Http", referencedNames);
        Assert.DoesNotContain(coreAssembly.GetTypes(), type => Contains(type, "PathOfExileTrade"));
        Assert.DoesNotContain(priceCheckerTypes.SelectMany(ReferencedMemberTypes), type =>
            Contains(type, "PathOfExileTradeFetchClient"));
        Assert.DoesNotContain(typeof(PathOfExileTradeSearchClient).GetMethods(), method =>
            method.Name.Contains("Fetch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(PathOfExileTradeFetchClient).GetMethods(), method =>
            method.Name.Contains("Search", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FetchClient_DoesNotIntroduceCurrencyExchangePublicStashModifierMappingOrSchedulers()
    {
        var providerTypes = typeof(PathOfExileTradeFetchClient).Assembly
            .GetTypes()
            .Where(type => type.Namespace == "PoEnhance.App.Infrastructure.Trade.PathOfExile")
            .Where(type => !type.IsNested && !type.Name.StartsWith("<", StringComparison.Ordinal))
            .ToArray();

        Assert.DoesNotContain(providerTypes, type =>
            Contains(type, "CurrencyExchange") ||
            Contains(type, "PublicStash") ||
            Contains(type, "TradeStat") ||
            Contains(type, "Scheduler") ||
            Contains(type, "Queue") ||
            Contains(type, "Cache") ||
            Contains(type, "Wait"));
    }

    private static HttpResponseMessage FetchResponse(
        string body = """{"result":[{"id":"result-1","item":{},"listing":{}}]}""",
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(body),
        };
    }

    private static void AddRateLimitHeaders(HttpResponseMessage response)
    {
        response.Headers.TryAddWithoutValidation("X-Rate-Limit-Policy", "trade-fetch");
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
        IReadOnlyList<string> Accept,
        string UserAgent,
        string? Content)
    {
        public static async Task<CapturedHttpRequest> FromRequestAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var content = request.Content is null
                ? null
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
