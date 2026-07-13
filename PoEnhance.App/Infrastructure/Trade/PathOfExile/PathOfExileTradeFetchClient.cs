using System.IO;
using System.Net;
using System.Net.Http;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeFetchClient : IPathOfExileTradeFetchClient
{
    public const int DefaultMaximumResponseBodyBytes = 1024 * 1024;

    private readonly HttpClient httpClient;
    private readonly PathOfExileTradeEndpointBuilder endpointBuilder;
    private readonly PathOfExileTradeFetchResponseParser responseParser;
    private readonly PathOfExileTradeRateLimitParser rateLimitParser;
    private readonly int maximumResponseBodyBytes;

    public PathOfExileTradeFetchClient(HttpClient httpClient)
        : this(
            httpClient,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeFetchResponseParser(),
            new PathOfExileTradeRateLimitParser(),
            PathOfExileTradeHttpClientSupport.DefaultMaximumResponseBodyBytes)
    {
    }

    internal PathOfExileTradeFetchClient(
        HttpClient httpClient,
        PathOfExileTradeEndpointBuilder endpointBuilder,
        PathOfExileTradeFetchResponseParser responseParser,
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

    public async Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
        string? queryId,
        IReadOnlyList<string?>? resultIds,
        CancellationToken cancellationToken = default)
    {
        var endpoint = endpointBuilder.BuildFetchEndpoint(queryId, resultIds);
        if (!endpoint.IsSuccess ||
            endpoint.BaseHost is null ||
            string.IsNullOrWhiteSpace(endpoint.PathAndQuery) ||
            !Uri.TryCreate(endpoint.BaseHost, endpoint.PathAndQuery, out var uri))
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint,
                EndpointFailureMessage(endpoint)));
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
                        $"The Trade fetch response exceeded {maximumResponseBodyBytes} bytes.",
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
                    "The Trade fetch request was cancelled by the caller."),
                isCancelled: true);
        }
        catch (TaskCanceledException)
        {
            return Failure(
                Diagnostic(
                    PathOfExileTradeHttpDiagnosticCodes.Timeout,
                    "The Trade fetch request timed out."),
                isTimeout: true);
        }
        catch (Exception exception) when (IsNetworkException(exception))
        {
            return Failure(Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                "The Trade fetch request failed before a provider response was available."));
        }
    }

    private PathOfExileTradeFetchExecutionResult ParseSuccessfulResponse(
        string body,
        HttpStatusCode statusCode,
        PathOfExileTradeRateLimitParseResult rateLimitParseResult)
    {
        var parseResult = responseParser.ParseFetchResponse(body);
        if (parseResult.IsSuccess && parseResult.Response is not null)
        {
            return new PathOfExileTradeFetchExecutionResult
            {
                IsSuccess = true,
                HttpStatusCode = statusCode,
                Response = parseResult.Response,
                RateLimitSnapshot = rateLimitParseResult.Snapshot,
                RateLimitDiagnostics = rateLimitParseResult.Diagnostics,
                Diagnostics = parseResult.Diagnostics,
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
            FirstDiagnosticOrDefault(
                parseResult.Diagnostics,
                statusCode,
                "The successful Trade fetch response could not be parsed."),
            statusCode,
            rateLimitParseResult.Snapshot,
            rateLimitParseResult.Diagnostics);
    }

    private PathOfExileTradeFetchExecutionResult ParseNonSuccessResponse(
        string body,
        HttpStatusCode statusCode,
        PathOfExileTradeRateLimitParseResult rateLimitParseResult)
    {
        var parseResult = responseParser.ParseFetchResponse(body);
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
                diagnostic.Code == PathOfExileTradeHttpDiagnosticCodes.MalformedProviderError))
        {
            return Failure(
                FirstDiagnosticOrDefault(
                    parseResult.Diagnostics,
                    statusCode,
                    "The Trade provider error response could not be parsed."),
                statusCode,
                rateLimitParseResult.Snapshot,
                rateLimitParseResult.Diagnostics);
        }

        return Failure(
            Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus,
                $"The Trade fetch provider returned HTTP {(int)statusCode}.",
                statusCode),
            statusCode,
            rateLimitParseResult.Snapshot,
            rateLimitParseResult.Diagnostics);
    }

    private static PathOfExileTradeFetchExecutionResult Failure(
        PathOfExileTradeHttpDiagnostic diagnostic,
        HttpStatusCode? statusCode = null,
        PathOfExileTradeRateLimitSnapshot? rateLimitSnapshot = null,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? rateLimitDiagnostics = null,
        PathOfExileTradeProviderError? providerError = null,
        bool isCancelled = false,
        bool isTimeout = false)
    {
        return new PathOfExileTradeFetchExecutionResult
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

    private static PathOfExileTradeHttpDiagnostic FirstDiagnosticOrDefault(
        IReadOnlyList<PathOfExileTradeHttpDiagnostic> diagnostics,
        HttpStatusCode statusCode,
        string fallback)
    {
        var diagnostic = diagnostics.FirstOrDefault();
        return diagnostic is null
            ? Diagnostic(
                PathOfExileTradeHttpDiagnosticCodes.MalformedResponse,
                fallback,
                statusCode)
            : diagnostic with { HttpStatusCode = statusCode };
    }

    private static string EndpointFailureMessage(PathOfExileTradeEndpointBuildResult endpoint)
    {
        var message = endpoint.Diagnostics
            .Select(diagnostic => diagnostic.Message)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return message ?? "The Path of Exile Trade fetch endpoint could not be built.";
    }

    private static bool IsNetworkException(Exception exception)
    {
        return exception is HttpRequestException or IOException or InvalidOperationException;
    }
}
