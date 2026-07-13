using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeSearchClient : IPathOfExileTradeSearchClient
{
    public const int DefaultMaximumResponseBodyBytes = 1024 * 1024;

    private static readonly ProductInfoHeaderValue UserAgent = CreateUserAgent();

    private readonly HttpClient httpClient;
    private readonly PathOfExileTradeEndpointBuilder endpointBuilder;
    private readonly PathOfExileTradeResponseParser responseParser;
    private readonly PathOfExileTradeRateLimitParser rateLimitParser;
    private readonly int maximumResponseBodyBytes;

    public PathOfExileTradeSearchClient(HttpClient httpClient)
        : this(
            httpClient,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeResponseParser(),
            new PathOfExileTradeRateLimitParser(),
            DefaultMaximumResponseBodyBytes)
    {
    }

    internal PathOfExileTradeSearchClient(
        HttpClient httpClient,
        PathOfExileTradeEndpointBuilder endpointBuilder,
        PathOfExileTradeResponseParser responseParser,
        PathOfExileTradeRateLimitParser rateLimitParser,
        int maximumResponseBodyBytes = DefaultMaximumResponseBodyBytes)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(endpointBuilder);
        ArgumentNullException.ThrowIfNull(responseParser);
        ArgumentNullException.ThrowIfNull(rateLimitParser);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseBodyBytes);

        this.httpClient = httpClient;
        this.endpointBuilder = endpointBuilder;
        this.responseParser = responseParser;
        this.rateLimitParser = rateLimitParser;
        this.maximumResponseBodyBytes = maximumResponseBodyBytes;
    }

    public async Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
        PathOfExileTradeSearchRequest? request,
        string? leagueIdentifier,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.NullRequest,
                "A Path of Exile Trade search request is required."));
        }

        var endpoint = endpointBuilder.BuildSearchEndpoint(leagueIdentifier);
        if (!endpoint.IsSuccess ||
            endpoint.BaseHost is null ||
            string.IsNullOrWhiteSpace(endpoint.PathAndQuery) ||
            !Uri.TryCreate(endpoint.BaseHost, endpoint.PathAndQuery, out var uri))
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint,
                EndpointFailureMessage(endpoint)));
        }

        string serializedJson;
        try
        {
            serializedJson = PathOfExileTradeJson.SerializeSearchRequest(request);
        }
        catch (Exception exception) when (IsSerializationException(exception))
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.SerializationFailed,
                "The Path of Exile Trade search request could not be serialized."));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, uri);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Headers.UserAgent.Add(UserAgent);
        httpRequest.Content = new StringContent(
            serializedJson,
            Encoding.UTF8,
            "application/json");

        try
        {
            using var httpResponse = await httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            var rateLimitParseResult = rateLimitParser.Parse(ResponseHeaders(httpResponse));
            var bodyReadResult = await ReadBoundedBodyAsync(
                    httpResponse.Content,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!bodyReadResult.IsSuccess)
            {
                return Failure(
                    Diagnostic(
                        PathOfExileTradeHttpDiagnosticCodes.ResponseTooLarge,
                        $"The Trade search response exceeded {maximumResponseBodyBytes} bytes.",
                        httpResponse.StatusCode),
                    httpResponse.StatusCode,
                    rateLimitParseResult.Snapshot,
                    rateLimitParseResult.Diagnostics);
            }

            return httpResponse.IsSuccessStatusCode
                ? ParseSuccessfulResponse(
                    bodyReadResult.Content,
                    httpResponse.StatusCode,
                    rateLimitParseResult)
                : ParseNonSuccessResponse(
                    bodyReadResult.Content,
                    httpResponse.StatusCode,
                    rateLimitParseResult);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                Diagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
                    "The Trade search request was cancelled by the caller."),
                isCancelled: true);
        }
        catch (TaskCanceledException)
        {
            return Failure(
                Diagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.Timeout,
                    "The Trade search request timed out."),
                isTimeout: true);
        }
        catch (Exception exception) when (IsNetworkException(exception))
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                "The Trade search request failed before a provider response was available."));
        }
    }

    private PathOfExileTradeSearchExecutionResult ParseSuccessfulResponse(
        string body,
        HttpStatusCode statusCode,
        PathOfExileTradeRateLimitParseResult rateLimitParseResult)
    {
        var parseResult = responseParser.ParseSearchResponse(body);
        if (parseResult.IsSuccess && parseResult.Response is not null)
        {
            return new PathOfExileTradeSearchExecutionResult
            {
                IsSuccess = true,
                HttpStatusCode = statusCode,
                Response = parseResult.Response,
                RateLimitSnapshot = rateLimitParseResult.Snapshot,
                RateLimitDiagnostics = rateLimitParseResult.Diagnostics,
            };
        }

        if (parseResult.ProviderError is not null)
        {
            return Failure(
                ProviderDiagnostic(parseResult.ProviderError, statusCode),
                statusCode,
                rateLimitParseResult.Snapshot,
                rateLimitParseResult.Diagnostics,
                parseResult.ProviderError);
        }

        return Failure(
            Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.MalformedResponse,
                FirstMessageOrDefault(
                    parseResult.Diagnostics,
                    "The successful Trade search response could not be parsed."),
                statusCode),
            statusCode,
            rateLimitParseResult.Snapshot,
            rateLimitParseResult.Diagnostics);
    }

    private PathOfExileTradeSearchExecutionResult ParseNonSuccessResponse(
        string body,
        HttpStatusCode statusCode,
        PathOfExileTradeRateLimitParseResult rateLimitParseResult)
    {
        var parseResult = responseParser.ParseSearchResponse(body);
        if (parseResult.ProviderError is not null)
        {
            return Failure(
                ProviderDiagnostic(parseResult.ProviderError, statusCode),
                statusCode,
                rateLimitParseResult.Snapshot,
                rateLimitParseResult.Diagnostics,
                parseResult.ProviderError);
        }

        if (parseResult.Diagnostics.Any(diagnostic =>
                diagnostic.Code == PathOfExileTradeResponseDiagnosticCodes.MalformedProviderError))
        {
            return Failure(
                Diagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.MalformedResponse,
                    FirstMessageOrDefault(
                        parseResult.Diagnostics,
                        "The Trade provider error response could not be parsed."),
                    statusCode),
                statusCode,
                rateLimitParseResult.Snapshot,
                rateLimitParseResult.Diagnostics);
        }

        return Failure(
            Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus,
                $"The Trade search provider returned HTTP {(int)statusCode}.",
                statusCode),
            statusCode,
            rateLimitParseResult.Snapshot,
            rateLimitParseResult.Diagnostics);
    }

    private async Task<BoundedBodyReadResult> ReadBoundedBodyAsync(
        HttpContent? content,
        CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return BoundedBodyReadResult.Success(string.Empty);
        }

        if (content.Headers.ContentLength is > 0 &&
            content.Headers.ContentLength > maximumResponseBodyBytes)
        {
            return BoundedBodyReadResult.TooLarge();
        }

        await using var stream = await content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var memoryStream = new MemoryStream();
        var buffer = new byte[8192];

        while (true)
        {
            var remainingBytes = maximumResponseBodyBytes - memoryStream.Length;
            var readLength = (int)Math.Min(buffer.Length, remainingBytes + 1);
            var bytesRead = await stream
                .ReadAsync(buffer.AsMemory(0, readLength), cancellationToken)
                .ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            if (memoryStream.Length + bytesRead > maximumResponseBodyBytes)
            {
                return BoundedBodyReadResult.TooLarge();
            }

            memoryStream.Write(buffer, 0, bytesRead);
        }

        return BoundedBodyReadResult.Success(Encoding.UTF8.GetString(memoryStream.ToArray()));
    }

    private static IEnumerable<KeyValuePair<string, IEnumerable<string>>> ResponseHeaders(
        HttpResponseMessage response)
    {
        foreach (var header in response.Headers)
        {
            yield return header;
        }

        if (response.Content is null)
        {
            yield break;
        }

        foreach (var header in response.Content.Headers)
        {
            yield return header;
        }
    }

    private static PathOfExileTradeSearchExecutionResult Failure(
        PathOfExileTradeHttpDiagnostic diagnostic,
        HttpStatusCode? statusCode = null,
        PathOfExileTradeRateLimitSnapshot? rateLimitSnapshot = null,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? rateLimitDiagnostics = null,
        PathOfExileTradeProviderError? providerError = null,
        bool isCancelled = false,
        bool isTimeout = false)
    {
        return new PathOfExileTradeSearchExecutionResult
        {
            IsSuccess = false,
            HttpStatusCode = statusCode,
            ProviderError = providerError,
            RateLimitSnapshot = rateLimitSnapshot,
            RateLimitDiagnostics = rateLimitDiagnostics ?? [],
            Diagnostics = [diagnostic],
            IsCancelled = isCancelled,
            IsTimeout = isTimeout,
        };
    }

    private static PathOfExileTradeHttpDiagnostic Diagnostic(
        string code,
        string message,
        HttpStatusCode? statusCode = null,
        string? providerCode = null)
    {
        return new PathOfExileTradeHttpDiagnostic(code, message, statusCode, providerCode);
    }

    private static PathOfExileTradeHttpDiagnostic ProviderDiagnostic(
        PathOfExileTradeProviderError providerError,
        HttpStatusCode statusCode)
    {
        return Diagnostic(
            PathOfExileTradeHttpDiagnosticCodes.ProviderDeclaredError,
            providerError.Message,
            statusCode,
            providerError.Code);
    }

    private static string EndpointFailureMessage(PathOfExileTradeEndpointBuildResult endpoint)
    {
        var message = endpoint.Diagnostics
            .Select(diagnostic => diagnostic.Message)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return message ?? "The Path of Exile Trade search endpoint could not be built.";
    }

    private static string FirstMessageOrDefault(
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> diagnostics,
        string fallback)
    {
        return diagnostics
            .Select(diagnostic => diagnostic.Message)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? fallback;
    }

    private static bool IsSerializationException(Exception exception)
    {
        return exception is NotSupportedException or InvalidOperationException or ArgumentException;
    }

    private static bool IsNetworkException(Exception exception)
    {
        return exception is HttpRequestException or IOException or InvalidOperationException;
    }

    private static ProductInfoHeaderValue CreateUserAgent()
    {
        var assemblyName = Assembly.GetExecutingAssembly().GetName();
        var version = assemblyName.Version?.ToString(fieldCount: 3) ?? "0.1.0";
        return new ProductInfoHeaderValue("PoEnhance", version);
    }

    private sealed record BoundedBodyReadResult(bool IsSuccess, string Content)
    {
        public static BoundedBodyReadResult Success(string content)
        {
            return new BoundedBodyReadResult(true, content);
        }

        public static BoundedBodyReadResult TooLarge()
        {
            return new BoundedBodyReadResult(false, string.Empty);
        }
    }
}
