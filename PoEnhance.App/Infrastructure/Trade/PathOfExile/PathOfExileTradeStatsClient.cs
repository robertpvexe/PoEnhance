using System.IO;
using System.Net;
using System.Net.Http;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeStatsClient : IPathOfExileTradeStatsClient
{
    public const int MaximumStatsResponseBodyBytes = 8 * 1024 * 1024;
    public const int DefaultMaximumResponseBodyBytes = MaximumStatsResponseBodyBytes;

    private readonly HttpClient httpClient;
    private readonly PathOfExileTradeEndpointBuilder endpointBuilder;
    private readonly PathOfExileTradeStatsResponseParser responseParser;
    private readonly PathOfExileTradeRateLimitParser rateLimitParser;
    private readonly int maximumResponseBodyBytes;

    public PathOfExileTradeStatsClient(HttpClient httpClient)
        : this(
            httpClient,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeStatsResponseParser(),
            new PathOfExileTradeRateLimitParser(),
            MaximumStatsResponseBodyBytes)
    {
    }

    internal PathOfExileTradeStatsClient(
        HttpClient httpClient,
        PathOfExileTradeEndpointBuilder endpointBuilder,
        PathOfExileTradeStatsResponseParser responseParser,
        PathOfExileTradeRateLimitParser rateLimitParser,
        int maximumResponseBodyBytes = MaximumStatsResponseBodyBytes)
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

    public async Task<PathOfExileTradeStatsExecutionResult> GetStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var endpoint = endpointBuilder.BuildStatsEndpoint();
        if (!endpoint.IsSuccess ||
            endpoint.BaseHost is null ||
            string.IsNullOrWhiteSpace(endpoint.PathAndQuery) ||
            !Uri.TryCreate(endpoint.BaseHost, endpoint.PathAndQuery, out var uri))
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint,
                "The Path of Exile Trade stats endpoint could not be built."));
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
                        $"The Trade stats response exceeded {maximumResponseBodyBytes} bytes.",
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
                        $"The Trade stats provider returned HTTP {(int)httpResponse.StatusCode}.",
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
                    "The Trade stats request was cancelled by the caller."),
                isCancelled: true);
        }
        catch (TaskCanceledException)
        {
            return Failure(
                Diagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.Timeout,
                    "The Trade stats request timed out."),
                isTimeout: true);
        }
        catch (Exception exception) when (IsNetworkException(exception))
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                "The Trade stats request failed before a provider response was available."));
        }
    }

    private PathOfExileTradeStatsExecutionResult ParseSuccessfulResponse(
        string body,
        HttpStatusCode statusCode,
        PathOfExileTradeRateLimitParseResult rateLimitParseResult)
    {
        var parseResult = responseParser.ParseStatsResponse(body);
        if (parseResult.IsSuccess && parseResult.Catalog is not null)
        {
            return new PathOfExileTradeStatsExecutionResult
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
                    "The successful Trade stats response could not be parsed."),
                statusCode),
            statusCode,
            rateLimitParseResult.Snapshot,
            rateLimitParseResult.Diagnostics,
            parseResult.Diagnostics);
    }

    private static PathOfExileTradeStatsExecutionResult Failure(
        PathOfExileTradeHttpDiagnostic diagnostic,
        HttpStatusCode? statusCode = null,
        PathOfExileTradeRateLimitSnapshot? rateLimitSnapshot = null,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? rateLimitDiagnostics = null,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? parserDiagnostics = null,
        bool isCancelled = false,
        bool isTimeout = false)
    {
        return new PathOfExileTradeStatsExecutionResult
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
