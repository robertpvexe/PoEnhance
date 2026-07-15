using System.Security.Cryptography;
using System.Text;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeProviderIdentity
{
    public static string Create(string providerStatId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerStatId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(providerStatId.Trim()));
        return $"variant-{Convert.ToHexString(hash.AsSpan(0, 10)).ToLowerInvariant()}";
    }
}
