using System.IO;
using System.Net;
using System.Net.Http;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeItemsClient : IPathOfExileTradeItemsClient
{
    public const int MaximumItemsResponseBodyBytes = 8 * 1024 * 1024;
    public const int DefaultMaximumResponseBodyBytes = MaximumItemsResponseBodyBytes;

    private readonly HttpClient httpClient;
    private readonly PathOfExileTradeEndpointBuilder endpointBuilder;
    private readonly PathOfExileTradeItemsResponseParser responseParser;
    private readonly PathOfExileTradeRateLimitParser rateLimitParser;
    private readonly int maximumResponseBodyBytes;

    public PathOfExileTradeItemsClient(HttpClient httpClient)
        : this(
            httpClient,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeItemsResponseParser(),
            new PathOfExileTradeRateLimitParser(),
            MaximumItemsResponseBodyBytes)
    {
    }

    internal PathOfExileTradeItemsClient(
        HttpClient httpClient,
        PathOfExileTradeEndpointBuilder endpointBuilder,
        PathOfExileTradeItemsResponseParser responseParser,
        PathOfExileTradeRateLimitParser rateLimitParser,
        int maximumResponseBodyBytes = MaximumItemsResponseBodyBytes)
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

    public async Task<PathOfExileTradeItemsExecutionResult> GetItemsAsync(
        CancellationToken cancellationToken = default)
    {
        var endpoint = endpointBuilder.BuildItemsEndpoint();
        if (!endpoint.IsSuccess ||
            endpoint.BaseHost is null ||
            string.IsNullOrWhiteSpace(endpoint.PathAndQuery) ||
            !Uri.TryCreate(endpoint.BaseHost, endpoint.PathAndQuery, out var uri))
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint,
                "The Path of Exile Trade items endpoint could not be built."));
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, uri);
        PathOfExileTradeHttpClientSupport.AddJsonHeaders(httpRequest);

        try
        {
            using var httpResponse = await httpClient
                .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            var rateLimitParseResult = rateLimitParser.Parse(
                PathOfExileTradeHttpClientSupport.ResponseHeaders(httpResponse));
            var bodyReadResult = await PathOfExileTradeHttpClientSupport.ReadBoundedBodyAsync(
                    httpResponse.Content,
                    maximumResponseBodyBytes,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!bodyReadResult.IsSuccess)
            {
                return Failure(
                    Diagnostic(
                        PathOfExileTradeHttpDiagnosticCodes.ResponseTooLarge,
                        $"The Trade items response exceeded {maximumResponseBodyBytes} bytes.",
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
                : Failure(
                    Diagnostic(
                        PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus,
                        $"The Trade items provider returned HTTP {(int)httpResponse.StatusCode}.",
                        httpResponse.StatusCode),
                    httpResponse.StatusCode,
                    rateLimitParseResult.Snapshot,
                    rateLimitParseResult.Diagnostics);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                Diagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
                    "The Trade items request was cancelled by the caller."),
                isCancelled: true);
        }
        catch (TaskCanceledException)
        {
            return Failure(
                Diagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.Timeout,
                    "The Trade items request timed out."),
                isTimeout: true);
        }
        catch (Exception exception) when (IsNetworkException(exception))
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                "The Trade items request failed before a provider response was available."));
        }
    }

    private PathOfExileTradeItemsExecutionResult ParseSuccessfulResponse(
        string body,
        HttpStatusCode statusCode,
        PathOfExileTradeRateLimitParseResult rateLimitParseResult)
    {
        var parseResult = responseParser.ParseItemsResponse(body);
        if (parseResult.IsSuccess && parseResult.Catalog is not null)
        {
            return new PathOfExileTradeItemsExecutionResult
            {
                IsSuccess = true,
                HttpStatusCode = statusCode,
                Catalog = parseResult.Catalog,
                Diagnostics = ToHttpDiagnostics(parseResult.Diagnostics, statusCode),
                ParserDiagnostics = parseResult.Diagnostics,
                RateLimitSnapshot = rateLimitParseResult.Snapshot,
                RateLimitDiagnostics = rateLimitParseResult.Diagnostics,
            };
        }

        return Failure(
            Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.MalformedResponse,
                FirstMessageOrDefault(
                    parseResult.Diagnostics,
                    "The successful Trade items response could not be parsed."),
                statusCode),
            statusCode,
            rateLimitParseResult.Snapshot,
            rateLimitParseResult.Diagnostics,
            parseResult.Diagnostics);
    }

    private static PathOfExileTradeItemsExecutionResult Failure(
        PathOfExileTradeHttpDiagnostic diagnostic,
        HttpStatusCode? statusCode = null,
        PathOfExileTradeRateLimitSnapshot? rateLimitSnapshot = null,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? rateLimitDiagnostics = null,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? parserDiagnostics = null,
        bool isCancelled = false,
        bool isTimeout = false)
    {
        return new PathOfExileTradeItemsExecutionResult
        {
            IsSuccess = false,
            HttpStatusCode = statusCode,
            RateLimitSnapshot = rateLimitSnapshot,
            RateLimitDiagnostics = rateLimitDiagnostics ?? [],
            ParserDiagnostics = parserDiagnostics ?? [],
            Diagnostics = [diagnostic],
            IsCancelled = isCancelled,
            IsTimeout = isTimeout,
        };
    }

    private static IReadOnlyList<PathOfExileTradeHttpDiagnostic> ToHttpDiagnostics(
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> diagnostics,
        HttpStatusCode statusCode)
    {
        return diagnostics
            .Select(diagnostic => new PathOfExileTradeHttpDiagnostic(
                diagnostic.Code,
                diagnostic.Message,
                statusCode))
            .ToArray();
    }

    private static PathOfExileTradeHttpDiagnostic Diagnostic(
        string code,
        string message,
        HttpStatusCode? statusCode = null)
    {
        return new PathOfExileTradeHttpDiagnostic(code, message, statusCode);
    }

    private static string FirstMessageOrDefault(
        IReadOnlyList<PathOfExileTradeQueryDiagnostic> diagnostics,
        string fallback)
    {
        return diagnostics
            .Select(diagnostic => diagnostic.Message)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? fallback;
    }

    private static bool IsNetworkException(Exception exception)
    {
        return exception is HttpRequestException or IOException or InvalidOperationException;
    }
}
