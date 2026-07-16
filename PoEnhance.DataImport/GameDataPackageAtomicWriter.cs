using System.Security.Cryptography;
using System.Text;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

internal static class GameDataPackageAtomicWriter
{
    public static void Write(
        GameDataPackage package,
        string outputPath,
        out long fileSize,
        out string sha256)
    {
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            outputDirectory = Directory.GetCurrentDirectory();
            outputPath = Path.Combine(outputDirectory, outputPath);
        }

        Directory.CreateDirectory(outputDirectory);

        var tempPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var json = GameDataPackageJson.Serialize(package);
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 64 * 1024,
                       FileOptions.SequentialScan))
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(outputPath))
            {
                File.Replace(tempPath, outputPath, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, outputPath);
            }

            fileSize = new FileInfo(outputPath).Length;
            sha256 = ComputeSha256(outputPath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void TryDelete(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch
        {
            // Best effort cleanup only. The write already failed.
        }
    }
}
