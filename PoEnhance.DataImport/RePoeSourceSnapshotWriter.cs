using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PoEnhance.DataImport;

internal static class RePoeSourceSnapshotWriter
{
    public const string ManifestFileName = "source-snapshot-manifest.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string Write(
        string outputDirectory,
        string repositoryUri,
        string branch,
        string commitSha,
        string packageDataVersion,
        DateTimeOffset buildTimestampUtc,
        IReadOnlyList<RePoeSourceSnapshotInput> inputs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(branch);
        ArgumentException.ThrowIfNullOrWhiteSpace(commitSha);
        ArgumentException.ThrowIfNullOrWhiteSpace(packageDataVersion);
        ArgumentNullException.ThrowIfNull(inputs);

        var destinationDirectory = Path.GetFullPath(outputDirectory);
        var parentDirectory = Path.GetDirectoryName(
            destinationDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            throw new IOException("Source snapshot output directory must have a parent directory.");
        }

        if (Directory.Exists(destinationDirectory) &&
            Directory.EnumerateFileSystemEntries(destinationDirectory).Any())
        {
            throw new IOException("Source snapshot output directory already exists and is not empty.");
        }

        Directory.CreateDirectory(parentDirectory);
        var destinationName = Path.GetFileName(
            destinationDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var stagingDirectory = Path.Combine(
            parentDirectory,
            $".{destinationName}.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(stagingDirectory);
            var retainedFiles = new List<RePoeSourceSnapshotFile>(inputs.Count);

            foreach (var input in inputs)
            {
                var originalPath = Path.GetFullPath(input.OriginalPath);
                var retainedPath = Path.Combine(stagingDirectory, input.PackageInputLabel);
                File.Copy(originalPath, retainedPath, overwrite: false);

                var sizeBytes = new FileInfo(retainedPath).Length;
                var sha256 = ComputeSha256(retainedPath);
                if (sizeBytes != input.ExpectedSizeBytes ||
                    !string.Equals(sha256, input.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException(
                        $"Retained source input '{input.PackageInputLabel}' does not match its package input fingerprint.");
                }

                retainedFiles.Add(new RePoeSourceSnapshotFile
                {
                    LogicalInputRole = input.LogicalInputRole,
                    SnapshotRole = input.SnapshotRole,
                    RepositoryUri = input.RepositoryUri,
                    Branch = input.Branch,
                    CommitSha = input.CommitSha,
                    SourceDataVersion = input.SourceDataVersion,
                    OriginalResolvedPath = originalPath,
                    RetainedFileName = input.PackageInputLabel,
                    RetainedRelativePath = input.PackageInputLabel,
                    SizeBytes = sizeBytes,
                    Sha256 = sha256,
                });
            }

            var manifest = new RePoeSourceSnapshotManifest
            {
                RepositoryUri = repositoryUri,
                Branch = branch,
                CommitSha = commitSha,
                PackageDataVersion = packageDataVersion,
                BuildTimestampUtc = buildTimestampUtc,
                Files = retainedFiles,
            };
            WriteManifest(stagingDirectory, manifest);

            if (Directory.Exists(destinationDirectory))
            {
                Directory.Delete(destinationDirectory, recursive: false);
            }

            Directory.Move(stagingDirectory, destinationDirectory);
            return Path.Combine(destinationDirectory, ManifestFileName);
        }
        catch
        {
            TryDeleteDirectory(stagingDirectory);
            throw;
        }
    }

    private static void WriteManifest(
        string stagingDirectory,
        RePoeSourceSnapshotManifest manifest)
    {
        var manifestPath = Path.Combine(stagingDirectory, ManifestFileName);
        var json = JsonSerializer.Serialize(manifest, SerializerOptions);
        using var stream = new FileStream(
            manifestPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 16 * 1024,
            FileOptions.SequentialScan);
        var bytes = Encoding.UTF8.GetBytes(json);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static void TryDeleteDirectory(string directoryPath)
    {
        try
        {
            if (Directory.Exists(directoryPath))
            {
                Directory.Delete(directoryPath, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup only. The snapshot write already failed.
        }
    }
}
