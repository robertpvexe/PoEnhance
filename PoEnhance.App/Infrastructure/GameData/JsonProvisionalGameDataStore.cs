using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using PoEnhance.GameData;
using Serilog;

namespace PoEnhance.App.Infrastructure.GameData;

internal sealed class JsonProvisionalGameDataStore : IProvisionalGameDataStore
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly string filePath;
    private ProvisionalGameDataStoreStatus status;

    public JsonProvisionalGameDataStore(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        this.filePath = Path.GetFullPath(filePath);
        status = new ProvisionalGameDataStoreStatus
        {
            FilePath = this.filePath,
        };
    }

    public ProvisionalGameDataStoreStatus Status => status;

    public async Task<ProvisionalGameDataStoreResult> LoadSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await LoadSnapshotUnsafeAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            writeGate.Release();
        }
    }

    public async Task<ProvisionalGameDataStoreResult> UpsertAsync(
        IReadOnlyList<ProvisionalGameDataRecord> records,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(records);

        if (records.Count == 0)
        {
            return await LoadSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }

        await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var loadResult = await LoadSnapshotUnsafeAsync(cancellationToken).ConfigureAwait(false);
            if (!loadResult.IsSuccess)
            {
                return loadResult;
            }

            var merged = loadResult.Snapshot.Records
                .Where(record => !string.IsNullOrWhiteSpace(record.StableKey))
                .ToDictionary(record => record.StableKey, StringComparer.Ordinal);

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record.StableKey))
                {
                    continue;
                }

                if (merged.TryGetValue(record.StableKey, out var existing))
                {
                    merged[record.StableKey] = existing with
                    {
                        LastSeenUtc = record.LastSeenUtc,
                        SeenCount = existing.SeenCount + 1,
                        DiscoveryContext = record.DiscoveryContext,
                        League = record.League ?? existing.League,
                        Patch = record.Patch ?? existing.Patch,
                    };
                }
                else
                {
                    merged[record.StableKey] = record;
                }
            }

            var snapshot = CreateSortedSnapshot(merged.Values);
            var writeResult = await WriteSnapshotUnsafeAsync(snapshot, cancellationToken).ConfigureAwait(false);
            if (!writeResult.IsSuccess)
            {
                return writeResult;
            }

            return SetStatus(ProvisionalGameDataStoreResult.Success(
                snapshot,
                $"Provisional store updated. Records: {snapshot.Records.Count}."));
        }
        finally
        {
            writeGate.Release();
        }
    }

    private async Task<ProvisionalGameDataStoreResult> LoadSnapshotUnsafeAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return SetStatus(ProvisionalGameDataStoreResult.Success(
                new ProvisionalGameDataSnapshot(),
                "Provisional store is empty."));
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            Log.Warning(exception, "Provisional game-data store could not be read from {FilePath}", filePath);
            return SetStatus(ProvisionalGameDataStoreResult.Failure(
                new ProvisionalGameDataSnapshot(),
                "Provisional store read failed."));
        }

        ProvisionalGameDataSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<ProvisionalGameDataSnapshot>(json, SerializerOptions);
        }
        catch (JsonException exception)
        {
            Log.Warning(exception, "Provisional game-data store JSON is malformed at {FilePath}", filePath);
            return SetStatus(ProvisionalGameDataStoreResult.Failure(
                new ProvisionalGameDataSnapshot(),
                "Provisional store JSON is malformed; existing file was not overwritten."));
        }

        if (snapshot is null || snapshot.SchemaVersion != ProvisionalGameDataSnapshot.CurrentSchemaVersion)
        {
            return SetStatus(ProvisionalGameDataStoreResult.Failure(
                new ProvisionalGameDataSnapshot(),
                "Provisional store schema is unsupported; existing file was not overwritten."));
        }

        return SetStatus(ProvisionalGameDataStoreResult.Success(
            CreateSortedSnapshot(snapshot.Records),
            $"Provisional store loaded. Records: {snapshot.Records.Count}."));
    }

    private async Task<ProvisionalGameDataStoreResult> WriteSnapshotUnsafeAsync(
        ProvisionalGameDataSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        var tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);

            return ProvisionalGameDataStoreResult.Success(snapshot, "Provisional store written.");
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            Log.Warning(exception, "Provisional game-data store could not be written to {FilePath}", filePath);
            TryDeleteTempFile(tempPath);
            return SetStatus(ProvisionalGameDataStoreResult.Failure(
                snapshot,
                "Provisional store write failed."));
        }
    }

    private ProvisionalGameDataStoreResult SetStatus(ProvisionalGameDataStoreResult result)
    {
        status = new ProvisionalGameDataStoreStatus
        {
            FilePath = filePath,
            RecordCount = result.Snapshot.Records.Count,
            LastDiagnostic = result.Diagnostic,
        };

        return result;
    }

    private static ProvisionalGameDataSnapshot CreateSortedSnapshot(
        IEnumerable<ProvisionalGameDataRecord> records)
    {
        return new ProvisionalGameDataSnapshot
        {
            Records = records
                .OrderBy(record => record.StableKey, StringComparer.Ordinal)
                .ToArray(),
        };
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        options.Converters.Add(new JsonStringEnumConverter<ProvisionalGameDataRecordKind>(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));
        options.Converters.Add(new JsonStringEnumConverter<ModifierGenerationType>(
            JsonNamingPolicy.CamelCase,
            allowIntegerValues: false));

        return options;
    }

    private static void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static bool IsFileFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
    }
}
