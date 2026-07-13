using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeHttpClientSupport
{
    public const int DefaultMaximumResponseBodyBytes = 1024 * 1024;

    private static readonly ProductInfoHeaderValue UserAgent = CreateUserAgent();

    public static void AddJsonHeaders(HttpRequestMessage request)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.Add(UserAgent);
    }

    public static async Task<PathOfExileTradeBoundedBodyReadResult> ReadBoundedBodyAsync(
        HttpContent? content,
        int maximumResponseBodyBytes,
        CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return PathOfExileTradeBoundedBodyReadResult.Success(string.Empty);
        }

        if (content.Headers.ContentLength is > 0 &&
            content.Headers.ContentLength > maximumResponseBodyBytes)
        {
            return PathOfExileTradeBoundedBodyReadResult.TooLarge();
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
                return PathOfExileTradeBoundedBodyReadResult.TooLarge();
            }

            memoryStream.Write(buffer, 0, bytesRead);
        }

        return PathOfExileTradeBoundedBodyReadResult.Success(
            Encoding.UTF8.GetString(memoryStream.ToArray()));
    }

    public static IEnumerable<KeyValuePair<string, IEnumerable<string>>> ResponseHeaders(
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

    private static ProductInfoHeaderValue CreateUserAgent()
    {
        var assemblyName = Assembly.GetExecutingAssembly().GetName();
        var version = assemblyName.Version?.ToString(fieldCount: 3) ?? "0.1.0";
        return new ProductInfoHeaderValue("PoEnhance", version);
    }
}
