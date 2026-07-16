using System.Security.Cryptography;

namespace PoEnhance.DataImport;

internal static class GameDataPackageHash
{
    public static string ComputeSha256(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
