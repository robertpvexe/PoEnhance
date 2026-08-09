using System.IO;
using System.Net;
using System.Net.Http;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeLeaguesClient : IPathOfExileTradeLeaguesClient
{
    public const int MaximumLeaguesResponseBodyBytes = 1024 * 1024;
    public static readonly TimeSpan FallbackFreshnessLifetime = TimeSpan.FromMinutes(1);

    private readonly HttpClient httpClient;
    private readonly PathOfExileTradeEndpointBuilder endpointBuilder;
    private readonly PathOfExileTradeLeaguesResponseParser responseParser;
    private readonly TimeProvider timeProvider;
    private readonly int maximumResponseBodyBytes;

    public PathOfExileTradeLeaguesClient(HttpClient httpClient)
        : this(
            httpClient,
            new PathOfExileTradeEndpointBuilder(),
            new PathOfExileTradeLeaguesResponseParser(),
            TimeProvider.System,
            MaximumLeaguesResponseBodyBytes)
    {
    }

    internal PathOfExileTradeLeaguesClient(
        HttpClient httpClient,
        PathOfExileTradeEndpointBuilder endpointBuilder,
        PathOfExileTradeLeaguesResponseParser responseParser,
        TimeProvider timeProvider,
        int maximumResponseBodyBytes = MaximumLeaguesResponseBodyBytes)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.endpointBuilder = endpointBuilder ?? throw new ArgumentNullException(nameof(endpointBuilder));
        this.responseParser = responseParser ?? throw new ArgumentNullException(nameof(responseParser));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseBodyBytes);
        this.maximumResponseBodyBytes = maximumResponseBodyBytes;
    }

    public async Task<PathOfExileTradeLeaguesExecutionResult> GetLeaguesAsync(
        CancellationToken cancellationToken = default)
    {
        var endpoint = endpointBuilder.BuildLeaguesEndpoint();
        if (!endpoint.IsSuccess || endpoint.BaseHost is null ||
            !Uri.TryCreate(endpoint.BaseHost, endpoint.PathAndQuery, out var uri))
        {
            return Failure(PathOfExileTradeHttpDiagnosticCodes.InvalidEndpoint,
                "The Path of Exile Trade leagues endpoint could not be built.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        PathOfExileTradeHttpClientSupport.AddJsonHeaders(request);
        try
        {
            using var response = await httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken)
                .ConfigureAwait(false);
            var retrievedAt = timeProvider.GetUtcNow();
            var body = await PathOfExileTradeHttpClientSupport.ReadBoundedBodyAsync(
                    response.Content,
                    maximumResponseBodyBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!body.IsSuccess)
            {
                return Failure(
                    PathOfExileTradeHttpDiagnosticCodes.ResponseTooLarge,
                    $"The Trade leagues response exceeded {maximumResponseBodyBytes} bytes.",
                    response.StatusCode);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Failure(
                    PathOfExileTradeHttpDiagnosticCodes.NonSuccessStatus,
                    $"The Trade leagues provider returned HTTP {(int)response.StatusCode}.",
                    response.StatusCode);
            }

            var parsed = responseParser.ParseLeaguesResponse(body.Content);
            if (!parsed.IsSuccess || parsed.Entries is null)
            {
                return new PathOfExileTradeLeaguesExecutionResult
                {
                    HttpStatusCode = response.StatusCode,
                    ParserDiagnostics = parsed.Diagnostics,
                    Diagnostics =
                    [
                        new PathOfExileTradeHttpDiagnostic(
                            PathOfExileTradeHttpDiagnosticCodes.MalformedResponse,
                            parsed.Diagnostics.FirstOrDefault()?.Message ??
                                "The successful Trade leagues response could not be parsed.",
                            response.StatusCode),
                    ],
                };
            }

            var lifetime = CacheLifetime(response);
            return new PathOfExileTradeLeaguesExecutionResult
            {
                IsSuccess = true,
                HttpStatusCode = response.StatusCode,
                Catalog = new PathOfExileTradeLeagueCatalog(
                    parsed.Entries,
                    retrievedAt,
                    retrievedAt + lifetime,
                    parsed.Diagnostics),
                ParserDiagnostics = parsed.Diagnostics,
                Diagnostics = parsed.Diagnostics.Select(diagnostic =>
                    new PathOfExileTradeHttpDiagnostic(
                        diagnostic.Code,
                        diagnostic.Message,
                        response.StatusCode)).ToArray(),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Failure(
                PathOfExileTradeHttpDiagnosticCodes.CallerCancellation,
                "The Trade leagues request was cancelled by the caller.",
                isCancelled: true);
        }
        catch (TaskCanceledException)
        {
            return Failure(
                PathOfExileTradeHttpDiagnosticCodes.Timeout,
                "The Trade leagues request timed out.",
                isTimeout: true);
        }
        catch (Exception exception) when (
            exception is HttpRequestException or IOException or InvalidOperationException)
        {
            return Failure(
                PathOfExileTradeHttpDiagnosticCodes.NetworkFailure,
                "The Trade leagues request failed before a provider response was available.");
        }
    }

    private static TimeSpan CacheLifetime(HttpResponseMessage response)
    {
        var maxAge = response.Headers.CacheControl?.MaxAge;
        if (maxAge is null || maxAge < TimeSpan.Zero)
        {
            return FallbackFreshnessLifetime;
        }

        return maxAge.Value;
    }

    private static PathOfExileTradeLeaguesExecutionResult Failure(
        string code,
        string message,
        HttpStatusCode? statusCode = null,
        bool isCancelled = false,
        bool isTimeout = false) => new()
    {
        HttpStatusCode = statusCode,
        Diagnostics = [new PathOfExileTradeHttpDiagnostic(code, message, statusCode)],
        IsCancelled = isCancelled,
        IsTimeout = isTimeout,
    };
}
