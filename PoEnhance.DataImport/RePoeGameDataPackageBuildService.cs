using System.Security.Cryptography;
using System.Diagnostics;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class RePoeGameDataPackageBuildService
{
    private const int CurrentSchemaVersion = 1;
    private const string RePoeSourceUri = "https://github.com/repoe-fork/repoe";

    private readonly RePoeBaseItemImporter _baseItemImporter = new();
    private readonly RePoeModifierImporter _modifierImporter = new();
    private readonly RePoeStatsImporter _statsImporter = new();
    private readonly RePoeStatTranslationsImporter _translationImporter = new();
    private readonly ReviewedItemPropertySemanticImporter _itemPropertySemanticImporter = new();
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
        ValidateSourceGuard(request, inputFiles, diagnostics);
        if (HasErrors(diagnostics))
        {
            return Failure(GameDataPackageBuildExitCode.InvalidArguments, diagnostics);
        }

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

        if (!File.Exists(request.ItemPropertySemanticsPath!))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildInputFileMissing,
                ImportDiagnosticSeverity.Error,
                "--item-property-semantics",
                "Required reviewed item-property semantic input file is missing."));
        }

        if (HasErrors(diagnostics))
        {
            return Failure(GameDataPackageBuildExitCode.MissingInputFile, diagnostics);
        }

        var baseItems = _baseItemImporter.Import(request.BaseItemsPath!);
        var modifiers = _modifierImporter.Import(request.ModsPath!);
        var stats = _statsImporter.Import(request.StatsPath!);
        var translations = _translationImporter.Import(request.TranslationsPath!, stats.ImportedRecords);
        var semanticInputBytes = File.ReadAllBytes(request.ItemPropertySemanticsPath!);
        using var semanticInputStream = new MemoryStream(semanticInputBytes, writable: false);
        var itemPropertySemantics = _itemPropertySemanticImporter.Import(
            semanticInputStream,
            stats.ImportedRecords);

        diagnostics.AddRange(baseItems.Diagnostics);
        diagnostics.AddRange(modifiers.Diagnostics);
        diagnostics.AddRange(stats.Diagnostics);
        diagnostics.AddRange(translations.Diagnostics);
        diagnostics.AddRange(itemPropertySemantics.Diagnostics);

        var summaries = new[]
        {
            Summary("ItemBases", baseItems),
            Summary("Modifiers", modifiers),
            Summary("Stats", stats),
            Summary("StatTranslations", translations),
            Summary("ItemPropertySemantics", itemPropertySemantics),
        };

        if (HasErrors(diagnostics))
        {
            return Failure(
                GameDataPackageBuildExitCode.SourceImportFailure,
                diagnostics,
                summaries);
        }

        var createdAtUtc = NormalizeCreatedAtUtc(request.CreatedAtUtc ?? DateTimeOffset.UtcNow);
        var reviewedSemanticInput = ReviewedItemPropertySemanticProvenanceFactory.Create(
            request.ItemPropertySemanticsPath!,
            semanticInputBytes);
        var manifest = CreateManifest(request, createdAtUtc, inputFiles, reviewedSemanticInput);

        var packageCreation = _packageBuilder.CreatePackage(
            manifest,
            baseItems.ImportedRecords,
            modifiers.ImportedRecords,
            stats.ImportedRecords,
            translations.ImportedRecords,
            itemPropertySemantics.ImportedRecords);
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
            GameDataPackageAtomicWriter.Write(package, outputPath, out var fileSize, out var sha256);
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
        AddRequiredArgumentDiagnostic(request.ItemPropertySemanticsPath, "--item-property-semantics", diagnostics);
        AddRequiredArgumentDiagnostic(request.OutputPath, "--output", diagnostics);
        AddRequiredArgumentDiagnostic(request.SourceRootPath, "--source-root", diagnostics);
        AddRequiredArgumentDiagnostic(request.SourceDataRootPath, "--source-data-root", diagnostics);
        AddRequiredArgumentDiagnostic(request.SourceUri, "--source-uri", diagnostics);
        AddRequiredArgumentDiagnostic(request.SourceBranch, "--source-branch", diagnostics);
        AddRequiredArgumentDiagnostic(request.SourceVersion, "--source-version", diagnostics);
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
        DateTimeOffset createdAtUtc,
        IReadOnlyList<(string Label, string Path)> inputFiles,
        GameDataPackageReviewedItemPropertySemanticInput reviewedSemanticInput)
    {
        return new GameDataPackageManifest
        {
            SchemaVersion = CurrentSchemaVersion,
            DataVersion = request.DataVersion!.Trim(),
            CreatedAtUtc = createdAtUtc,
            League = TrimToNull(request.League),
            Patch = TrimToNull(request.Patch),
            ReviewedItemPropertySemantics = reviewedSemanticInput,
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = RePoeBaseItemImporter.SourceId,
                    RetrievedAtUtc = createdAtUtc,
                    SourceVersion = TrimToNull(request.SourceVersion),
                    SourceUri = TrimToNull(request.SourceUri) ?? RePoeSourceUri,
                    SourceBranch = TrimToNull(request.SourceBranch),
                    SourceRoot = NormalizePathOrNull(request.SourceRootPath),
                    SourceDataRoot = NormalizePathOrNull(request.SourceDataRootPath),
                    InputFiles = CreateInputFingerprints(request.SourceDataRootPath!, inputFiles),
                },
            ],
        };
    }

    private static void ValidateSourceGuard(
        GameDataPackageBuildRequest request,
        IReadOnlyList<(string Label, string Path)> inputFiles,
        List<ImportDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(request.SourceRootPath) ||
            string.IsNullOrWhiteSpace(request.SourceDataRootPath) ||
            string.IsNullOrWhiteSpace(request.SourceUri) ||
            string.IsNullOrWhiteSpace(request.SourceBranch) ||
            string.IsNullOrWhiteSpace(request.SourceVersion))
        {
            return;
        }

        var sourceRoot = Path.GetFullPath(request.SourceRootPath);
        var sourceDataRoot = Path.GetFullPath(request.SourceDataRootPath);
        if (!Directory.Exists(sourceRoot))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                ImportDiagnosticSeverity.Error,
                "--source-root",
                "Source root directory does not exist."));
            return;
        }

        if (!Directory.Exists(sourceDataRoot))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                ImportDiagnosticSeverity.Error,
                "--source-data-root",
                "Source data root directory does not exist."));
        }

        var remote = RunGit(sourceRoot, "remote get-url origin", diagnostics, "--source-uri");
        if (remote is not null &&
            !string.Equals(
                NormalizeRepositoryUri(remote),
                NormalizeRepositoryUri(request.SourceUri),
                StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                ImportDiagnosticSeverity.Error,
                "--source-uri",
                $"Source repository mismatch. Expected '{request.SourceUri}', actual '{remote}'."));
        }

        var branch = RunGit(sourceRoot, "branch --show-current", diagnostics, "--source-branch");
        if (branch is not null &&
            !string.Equals(branch.Trim(), request.SourceBranch.Trim(), StringComparison.Ordinal))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                ImportDiagnosticSeverity.Error,
                "--source-branch",
                $"Source branch mismatch. Expected '{request.SourceBranch}', actual '{branch}'."));
        }

        var head = RunGit(sourceRoot, "rev-parse HEAD", diagnostics, "--source-version");
        if (head is not null &&
            !string.Equals(head.Trim(), request.SourceVersion.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                ImportDiagnosticSeverity.Error,
                "--source-version",
                $"Source SHA mismatch. Expected '{request.SourceVersion}', actual '{head}'."));
        }

        foreach (var inputFile in inputFiles)
        {
            var fullPath = Path.GetFullPath(inputFile.Path);
            if (!IsUnderDirectory(fullPath, sourceDataRoot))
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                    ImportDiagnosticSeverity.Error,
                    inputFile.Label,
                    $"Input file '{inputFile.Label}' is outside the declared source data root."));
            }
        }
    }

    private static IReadOnlyList<GameDataPackageInputFileFingerprint> CreateInputFingerprints(
        string sourceDataRootPath,
        IReadOnlyList<(string Label, string Path)> inputFiles)
    {
        var sourceDataRoot = Path.GetFullPath(sourceDataRootPath);
        return inputFiles
            .Where(inputFile => File.Exists(inputFile.Path))
            .Select(inputFile =>
            {
                var fullPath = Path.GetFullPath(inputFile.Path);
                return new GameDataPackageInputFileFingerprint
                {
                    Label = inputFile.Label,
                    RelativePath = Path.GetRelativePath(sourceDataRoot, fullPath),
                    SizeBytes = new FileInfo(fullPath).Length,
                    Sha256 = ComputeSha256(fullPath),
                };
            })
            .ToArray();
    }

    private static string? RunGit(
        string sourceRoot,
        string arguments,
        List<ImportDiagnostic> diagnostics,
        string sourceRecordId)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"-C \"{sourceRoot}\" {arguments}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process is null)
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                    ImportDiagnosticSeverity.Error,
                    sourceRecordId,
                    "Could not start git to validate source provenance."));
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            var error = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                    ImportDiagnosticSeverity.Error,
                    sourceRecordId,
                    $"Git source provenance check failed: {error}"));
                return null;
            }

            return output;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                ImportDiagnosticSeverity.Error,
                sourceRecordId,
                $"Git source provenance check failed: {exception.Message}"));
            return null;
        }
    }

    private static bool IsUnderDirectory(string fullPath, string directory)
    {
        var normalizedDirectory = directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        return fullPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRepositoryUri(string value)
    {
        var normalized = value.Trim().Replace('\\', '/').TrimEnd('/');
        if (normalized.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^4];
        }

        return normalized;
    }

    private static string? NormalizePathOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : Path.GetFullPath(value);
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
            ItemPropertySemantics = package?.ItemPropertySemantics?.Count ?? 0,
        };
    }

    private static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
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
