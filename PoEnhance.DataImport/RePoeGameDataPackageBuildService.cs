using System.Security.Cryptography;
using System.Text;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class RePoeGameDataPackageBuildService
{
    private const int CurrentSchemaVersion = 1;
    private const string RePoeSourceUri = "https://github.com/brather1ng/RePoE";

    private readonly RePoeBaseItemImporter _baseItemImporter = new();
    private readonly RePoeModifierImporter _modifierImporter = new();
    private readonly RePoeStatsImporter _statsImporter = new();
    private readonly RePoeStatTranslationsImporter _translationImporter = new();
    private readonly GameDataPackageBuilder _packageBuilder = new();

    public GameDataPackageBuildResult Build(GameDataPackageBuildRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = new List<ImportDiagnostic>();
        ValidateRequiredArguments(request, diagnostics);
        if (HasErrors(diagnostics))
        {
            return Failure(GameDataPackageBuildExitCode.InvalidArguments, diagnostics);
        }

        var inputFiles = BuildInputFileList(request);
        foreach (var inputFile in inputFiles)
        {
            if (!File.Exists(inputFile.Path))
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.BuildInputFileMissing,
                    ImportDiagnosticSeverity.Error,
                    inputFile.Label,
                    $"Required local RePoE input file is missing: {inputFile.Label}."));
            }
        }

        if (HasErrors(diagnostics))
        {
            return Failure(GameDataPackageBuildExitCode.MissingInputFile, diagnostics);
        }

        var createdAtUtc = NormalizeCreatedAtUtc(request.CreatedAtUtc ?? DateTimeOffset.UtcNow);
        var manifest = CreateManifest(request, createdAtUtc);

        var baseItems = _baseItemImporter.Import(request.BaseItemsPath!);
        var modifiers = _modifierImporter.Import(request.ModsPath!);
        var stats = _statsImporter.Import(request.StatsPath!);
        var translations = _translationImporter.Import(request.TranslationsPath!, stats.ImportedRecords);

        diagnostics.AddRange(baseItems.Diagnostics);
        diagnostics.AddRange(modifiers.Diagnostics);
        diagnostics.AddRange(stats.Diagnostics);
        diagnostics.AddRange(translations.Diagnostics);

        var summaries = new[]
        {
            Summary("ItemBases", baseItems),
            Summary("Modifiers", modifiers),
            Summary("Stats", stats),
            Summary("StatTranslations", translations),
        };

        if (HasErrors(diagnostics))
        {
            return Failure(
                GameDataPackageBuildExitCode.SourceImportFailure,
                diagnostics,
                summaries);
        }

        var packageCreation = _packageBuilder.CreatePackage(
            manifest,
            baseItems.ImportedRecords,
            modifiers.ImportedRecords,
            stats.ImportedRecords,
            translations.ImportedRecords);
        diagnostics.AddRange(packageCreation.Diagnostics);

        if (packageCreation.Package is null || HasErrors(diagnostics))
        {
            return Failure(
                GameDataPackageBuildExitCode.PackageValidationFailure,
                diagnostics,
                summaries,
                CountRecords(packageCreation.Package));
        }

        var package = packageCreation.Package;
        var counts = CountRecords(package);
        var outputPath = Path.GetFullPath(request.OutputPath!);

        try
        {
            WritePackageAtomically(package, outputPath, out var fileSize, out var sha256);
            return new GameDataPackageBuildResult
            {
                ExitCode = GameDataPackageBuildExitCode.Success,
                Diagnostics = diagnostics,
                SourceSummaries = summaries,
                FinalCounts = counts,
                OutputPath = outputPath,
                OutputFileSizeBytes = fileSize,
                Sha256 = sha256,
                Package = package,
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildOutputWriteFailed,
                ImportDiagnosticSeverity.Error,
                null,
                $"Failed to write the game-data package output: {exception.Message}"));

            return Failure(
                GameDataPackageBuildExitCode.OutputWriteFailure,
                diagnostics,
                summaries,
                counts,
                outputPath);
        }
    }

    private static void ValidateRequiredArguments(
        GameDataPackageBuildRequest request,
        List<ImportDiagnostic> diagnostics)
    {
        AddRequiredArgumentDiagnostic(request.BaseItemsPath, "--base-items", diagnostics);
        AddRequiredArgumentDiagnostic(request.ModsPath, "--mods", diagnostics);
        AddRequiredArgumentDiagnostic(request.StatsPath, "--stats", diagnostics);
        AddRequiredArgumentDiagnostic(request.TranslationsPath, "--translations", diagnostics);
        AddRequiredArgumentDiagnostic(request.OutputPath, "--output", diagnostics);
        AddRequiredArgumentDiagnostic(request.DataVersion, "--data-version", diagnostics);

        if (!string.IsNullOrWhiteSpace(request.OutputPath) && IsInsidePoEnhanceAppDirectory(request.OutputPath))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                ImportDiagnosticSeverity.Error,
                "--output",
                "Output path must not be inside a PoEnhance.App directory."));
        }
    }

    private static void AddRequiredArgumentDiagnostic(
        string? value,
        string argumentName,
        List<ImportDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                ImportDiagnosticSeverity.Error,
                argumentName,
                $"Required argument is missing: {argumentName}."));
        }
    }

    private static IReadOnlyList<(string Label, string Path)> BuildInputFileList(GameDataPackageBuildRequest request)
    {
        return
        [
            ("base_items.json", request.BaseItemsPath!),
            ("mods.json", request.ModsPath!),
            ("stats.json", request.StatsPath!),
            ("stat_translations.json", request.TranslationsPath!),
        ];
    }

    private static GameDataPackageManifest CreateManifest(
        GameDataPackageBuildRequest request,
        DateTimeOffset createdAtUtc)
    {
        return new GameDataPackageManifest
        {
            SchemaVersion = CurrentSchemaVersion,
            DataVersion = request.DataVersion!.Trim(),
            CreatedAtUtc = createdAtUtc,
            League = TrimToNull(request.League),
            Patch = TrimToNull(request.Patch),
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = RePoeBaseItemImporter.SourceId,
                    RetrievedAtUtc = createdAtUtc,
                    SourceVersion = TrimToNull(request.SourceVersion),
                    SourceUri = RePoeSourceUri,
                },
            ],
        };
    }

    private static DateTimeOffset NormalizeCreatedAtUtc(DateTimeOffset value)
    {
        return value.Offset == TimeSpan.Zero
            ? value
            : value.ToUniversalTime();
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static GameDataPackageBuildSourceSummary Summary<TRecord>(
        string sourceName,
        ImportResult<TRecord> result)
    {
        return new GameDataPackageBuildSourceSummary
        {
            SourceName = sourceName,
            SourceRecordsRead = result.SourceRecordsRead,
            RecordsImported = result.RecordsImported,
            RecordsSkipped = result.RecordsSkipped,
        };
    }

    private static GameDataPackageBuildRecordCounts CountRecords(GameDataPackage? package)
    {
        return new GameDataPackageBuildRecordCounts
        {
            ItemBases = package?.ItemBases?.Count ?? 0,
            Modifiers = package?.Modifiers?.Count ?? 0,
            Stats = package?.Stats?.Count ?? 0,
            StatTranslations = package?.StatTranslations?.Count ?? 0,
        };
    }

    private static void WritePackageAtomically(
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
            // Best effort cleanup only. The build already failed.
        }
    }

    private static bool IsInsidePoEnhanceAppDirectory(string outputPath)
    {
        var fullPath = Path.GetFullPath(outputPath);
        return fullPath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => string.Equals(part, "PoEnhance.App", StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasErrors(IEnumerable<ImportDiagnostic> diagnostics)
    {
        return diagnostics.Any(diagnostic => diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    private static GameDataPackageBuildResult Failure(
        GameDataPackageBuildExitCode exitCode,
        IReadOnlyList<ImportDiagnostic> diagnostics,
        IReadOnlyList<GameDataPackageBuildSourceSummary>? summaries = null,
        GameDataPackageBuildRecordCounts? counts = null,
        string? outputPath = null)
    {
        return new GameDataPackageBuildResult
        {
            ExitCode = exitCode,
            Diagnostics = diagnostics,
            SourceSummaries = summaries ?? [],
            FinalCounts = counts ?? new GameDataPackageBuildRecordCounts(),
            OutputPath = outputPath,
        };
    }

    private static ImportDiagnostic Diagnostic(
        string code,
        ImportDiagnosticSeverity severity,
        string? sourceRecordId,
        string message)
    {
        return new ImportDiagnostic(code, severity, sourceRecordId, message);
    }
}
