using System.IO;
using System.Net;
using System.Net.Http;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeFiltersClient : IPathOfExileTradeFiltersClient
{
    public const int MaximumFiltersResponseBodyBytes = 2 * 1024 * 1024;
    public const int DefaultMaximumResponseBodyBytes = MaximumFiltersResponseBodyBytes;

    private readonly HttpClient httpClient;
    private readonly PathOfExileTradeEndpointBuilder endpointBuilder;
    private readonly PathOfExileTradeFiltersResponseParser responseParser;
    private readonly PathOfExileTradeRateLimitParser rateLimitParser;
    private readonly int maximumResponseBodyBytes;

    public PathOfExileTradeFiltersClient(HttpClient httpClient)
        : this(
            httpClient,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeFiltersResponseParser(),
            new PathOfExileTradeRateLimitParser(),
            MaximumFiltersResponseBodyBytes)
    {
    }

    internal PathOfExileTradeFiltersClient(
        HttpClient httpClient,
        PathOfExileTradeEndpointBuilder endpointBuilder,
        PathOfExileTradeFiltersResponseParser responseParser,
        PathOfExileTradeRateLimitParser rateLimitParser,
        int maximumResponseBodyBytes = MaximumFiltersResponseBodyBytes)
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

    public async Task<PathOfExileTradeFiltersExecutionResult> GetFiltersAsync(
        CancellationToken cancellationToken = default)
    {
        var endpoint = endpointBuilder.BuildFiltersEndpoint();
        if (!endpoint.IsSuccess ||
            endpoint.BaseHost is null ||
            string.IsNullOrWhiteSpace(endpoint.PathAndQuery) ||
            !Uri.TryCreate(endpoint.BaseHost, endpoint.PathAndQuery, out var uri))
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint,
                "The Path of Exile Trade filters endpoint could not be built."));
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
                        $"The Trade filters response exceeded {maximumResponseBodyBytes} bytes.",
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
                        $"The Trade filters provider returned HTTP {(int)httpResponse.StatusCode}.",
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
                    "The Trade filters request was cancelled by the caller."),
                isCancelled: true);
        }
        catch (TaskCanceledException)
        {
            return Failure(
                Diagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.Timeout,
                    "The Trade filters request timed out."),
                isTimeout: true);
        }
        catch (Exception exception) when (IsNetworkException(exception))
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                "The Trade filters request failed before a provider response was available."));
        }
    }

    private PathOfExileTradeFiltersExecutionResult ParseSuccessfulResponse(
        string body,
        HttpStatusCode statusCode,
        PathOfExileTradeRateLimitParseResult rateLimitParseResult)
    {
        var parseResult = responseParser.ParseFiltersResponse(body);
        if (parseResult.IsSuccess && parseResult.Catalog is not null)
        {
            return new PathOfExileTradeFiltersExecutionResult
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
                    "The successful Trade filters response could not be parsed."),
                statusCode),
            statusCode,
            rateLimitParseResult.Snapshot,
            rateLimitParseResult.Diagnostics,
            parseResult.Diagnostics);
    }

    private static PathOfExileTradeFiltersExecutionResult Failure(
        PathOfExileTradeHttpDiagnostic diagnostic,
        HttpStatusCode? statusCode = null,
        PathOfExileTradeRateLimitSnapshot? rateLimitSnapshot = null,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? rateLimitDiagnostics = null,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? parserDiagnostics = null,
        bool isCancelled = false,
        bool isTimeout = false)
    {
        return new PathOfExileTradeFiltersExecutionResult
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
