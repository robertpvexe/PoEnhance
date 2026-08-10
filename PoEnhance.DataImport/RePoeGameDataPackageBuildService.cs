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
    private readonly RePoeItemClassImporter _itemClassImporter = new();
    private readonly RePoeTagImporter _tagImporter = new();
    private readonly RePoeModsByBaseImporter _modsByBaseImporter = new();
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
        var historicalInputFiles = HasHistoricalInputs(request)
            ? BuildHistoricalInputFileList(request)
            : [];
        if (historicalInputFiles.Count > 0)
        {
            ValidateSourceGuard(
                new GameDataPackageBuildRequest
                {
                    SourceRootPath = request.HistoricalSourceRootPath,
                    SourceDataRootPath = request.HistoricalSourceDataRootPath,
                    SourceUri = request.HistoricalSourceUri,
                    SourceBranch = request.HistoricalSourceBranch,
                    SourceVersion = request.HistoricalSourceVersion,
                },
                historicalInputFiles,
                diagnostics);
        }
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

        foreach (var inputFile in historicalInputFiles)
        {
            if (!File.Exists(inputFile.Path))
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.BuildInputFileMissing,
                    ImportDiagnosticSeverity.Error,
                    inputFile.Label,
                    $"Required historical RePoE input file is missing: {inputFile.Label}."));
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
        ImportResult<ItemBaseRecord>? historicalBaseItems = null;
        ImportResult<ModifierDefinition>? historicalModifiers = null;
        ImportResult<StatDefinition>? historicalStats = null;
        ImportResult<StatTranslationDefinition>? historicalTranslations = null;
        if (historicalInputFiles.Count > 0)
        {
            historicalBaseItems = _baseItemImporter.Import(request.HistoricalBaseItemsPath!);
            historicalModifiers = _modifierImporter.Import(request.HistoricalModsPath!);
            historicalStats = _statsImporter.Import(request.HistoricalStatsPath!);
            historicalTranslations = _translationImporter.Import(
                request.HistoricalTranslationsPath!,
                historicalStats.ImportedRecords);
            diagnostics.AddRange(historicalBaseItems.Diagnostics);
            diagnostics.AddRange(historicalModifiers.Diagnostics);
            diagnostics.AddRange(historicalStats.Diagnostics);
            diagnostics.AddRange(historicalTranslations.Diagnostics);
        }
        var itemClasses = _itemClassImporter.Import(request.ItemClassesPath!);
        var tags = _tagImporter.Import(request.TagsPath!);
        var modsByBase = _modsByBaseImporter.Import(
            request.ModsByBasePath!,
            request.BaseItemsPath!,
            request.ModsPath!,
            baseItems.ImportedRecords,
            modifiers.ImportedRecords);
        var semanticInputBytes = File.ReadAllBytes(request.ItemPropertySemanticsPath!);
        using var semanticInputStream = new MemoryStream(semanticInputBytes, writable: false);
        var itemPropertySemantics = _itemPropertySemanticImporter.Import(
            semanticInputStream,
            stats.ImportedRecords);

        diagnostics.AddRange(baseItems.Diagnostics);
        diagnostics.AddRange(modifiers.Diagnostics);
        diagnostics.AddRange(stats.Diagnostics);
        diagnostics.AddRange(translations.Diagnostics);
        diagnostics.AddRange(itemClasses.Diagnostics);
        diagnostics.AddRange(tags.Diagnostics);
        diagnostics.AddRange(modsByBase.Diagnostics);
        diagnostics.AddRange(itemPropertySemantics.Diagnostics);

        var summaries = new[]
        {
            Summary("ItemBases", baseItems),
            Summary("Modifiers", modifiers),
            Summary("Stats", stats),
            Summary("StatTranslations", translations),
            Summary("ItemClasses", itemClasses),
            Summary("Tags", tags),
            new GameDataPackageBuildSourceSummary
            {
                SourceName = "BaseModifierEvidence",
                SourceRecordsRead = modsByBase.Audit.SourceBaseEntriesRead,
                RecordsImported = modsByBase.Audit.BaseEntriesImported,
                RecordsSkipped = modsByBase.Audit.BaseEntriesSkipped,
            },
            Summary("ItemPropertySemantics", itemPropertySemantics),
        };

        if (HasErrors(diagnostics))
        {
            return Failure(
                GameDataPackageBuildExitCode.SourceImportFailure,
                diagnostics,
                summaries,
                baseModifierEvidenceAudit: modsByBase.Audit);
        }

        var createdAtUtc = NormalizeCreatedAtUtc(request.CreatedAtUtc ?? DateTimeOffset.UtcNow);
        var reviewedSemanticInput = ReviewedItemPropertySemanticProvenanceFactory.Create(
            request.ItemPropertySemanticsPath!,
            semanticInputBytes);
        var manifest = CreateManifest(request, createdAtUtc, inputFiles, reviewedSemanticInput);

        BaseImplicitHistoryCatalog? baseImplicitHistory = null;
        StatTranslationHistoryCatalog? statTranslationHistory = null;
        if (historicalBaseItems is not null &&
            historicalModifiers is not null &&
            historicalStats is not null &&
            historicalTranslations is not null)
        {
            baseImplicitHistory = BaseImplicitHistoryBuilder.Build(
                request.SourceUri!,
                request.SourceVersion!,
                request.DataVersion!,
                baseItems.ImportedRecords,
                modifiers.ImportedRecords,
                stats.ImportedRecords,
                translations.ImportedRecords,
                request.HistoricalSourceUri!,
                request.HistoricalSourceVersion!,
                request.HistoricalDataVersion!,
                historicalBaseItems.ImportedRecords,
                historicalModifiers.ImportedRecords,
                historicalStats.ImportedRecords,
                historicalTranslations.ImportedRecords);

            statTranslationHistory = StatTranslationHistoryBuilder.Build(
                request.SourceUri!,
                request.SourceVersion!,
                request.DataVersion!,
                modifiers.ImportedRecords,
                stats.ImportedRecords,
                translations.ImportedRecords,
                request.HistoricalSourceUri!,
                request.HistoricalSourceVersion!,
                request.HistoricalDataVersion!,
                historicalModifiers.ImportedRecords,
                historicalStats.ImportedRecords,
                historicalTranslations.ImportedRecords);
        }

        var packageCreation = _packageBuilder.CreatePackage(
            manifest,
            baseItems.ImportedRecords,
            modifiers.ImportedRecords,
            stats.ImportedRecords,
            translations.ImportedRecords,
            itemPropertySemantics.ImportedRecords,
            itemClasses.ImportedRecords,
            tags.ImportedRecords,
            modsByBase.Evidence!,
            baseImplicitHistory,
            statTranslationHistory);
        diagnostics.AddRange(packageCreation.Diagnostics);

        if (packageCreation.Package is null || HasErrors(diagnostics))
        {
            return Failure(
                GameDataPackageBuildExitCode.PackageValidationFailure,
                diagnostics,
                summaries,
                CountRecords(packageCreation.Package),
                baseModifierEvidenceAudit: modsByBase.Audit);
        }

        var package = packageCreation.Package;
        var counts = CountRecords(package);
        var outputPath = Path.GetFullPath(request.OutputPath!);
        var sourceSnapshotDirectory = NormalizePathOrNull(request.SourceSnapshotDirectory);
        string? sourceSnapshotManifestPath = null;

        if (sourceSnapshotDirectory is not null)
        {
            try
            {
                var repoeSource = package.Manifest.Sources.Single(source =>
                    string.Equals(
                        source.SourceId,
                        RePoeBaseItemImporter.SourceId,
                        StringComparison.OrdinalIgnoreCase));
                sourceSnapshotManifestPath = RePoeSourceSnapshotWriter.Write(
                    sourceSnapshotDirectory,
                    request.SourceUri!,
                    request.SourceBranch!,
                    request.SourceVersion!,
                    request.DataVersion!,
                    createdAtUtc,
                    CreateSourceSnapshotInputs(
                        inputFiles,
                        repoeSource.InputFiles,
                        "current",
                        request.SourceUri!,
                        request.SourceBranch!,
                        request.SourceVersion!,
                        request.DataVersion!)
                    .Concat(historicalInputFiles.Count == 0
                        ? []
                        : CreateSourceSnapshotInputs(
                            historicalInputFiles,
                            package.Manifest.Sources.Single(source => string.Equals(
                                source.SourceId,
                                BaseImplicitHistoryBuilder.HistoricalManifestSourceId,
                                StringComparison.OrdinalIgnoreCase)).InputFiles,
                            "historical-base-implicit",
                            request.HistoricalSourceUri!,
                            request.HistoricalSourceBranch!,
                            request.HistoricalSourceVersion!,
                            request.HistoricalDataVersion!))
                    .ToArray());
            }
            catch (Exception exception) when (exception is
                IOException or
                UnauthorizedAccessException or
                ArgumentException or
                NotSupportedException)
            {
                diagnostics.Add(Diagnostic(
                    RePoeImportDiagnosticCodes.BuildSourceSnapshotWriteFailed,
                    ImportDiagnosticSeverity.Error,
                    "--source-snapshot-dir",
                    $"Failed to retain the RePoE source snapshot: {exception.Message}"));

                return Failure(
                    GameDataPackageBuildExitCode.OutputWriteFailure,
                    diagnostics,
                    summaries,
                    counts,
                    outputPath,
                    sourceSnapshotDirectory,
                    modsByBase.Audit);
            }
        }

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
                SourceSnapshotDirectory = sourceSnapshotDirectory,
                SourceSnapshotManifestPath = sourceSnapshotManifestPath,
                Package = package,
                BaseModifierEvidenceAudit = modsByBase.Audit,
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
                outputPath,
                sourceSnapshotDirectory,
                modsByBase.Audit);
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
        AddRequiredArgumentDiagnostic(request.ItemClassesPath, "--item-classes", diagnostics);
        AddRequiredArgumentDiagnostic(request.TagsPath, "--tags", diagnostics);
        AddRequiredArgumentDiagnostic(request.ModsByBasePath, "--mods-by-base", diagnostics);
        AddRequiredArgumentDiagnostic(request.ItemPropertySemanticsPath, "--item-property-semantics", diagnostics);
        AddRequiredArgumentDiagnostic(request.OutputPath, "--output", diagnostics);
        AddRequiredArgumentDiagnostic(request.SourceRootPath, "--source-root", diagnostics);
        AddRequiredArgumentDiagnostic(request.SourceDataRootPath, "--source-data-root", diagnostics);
        AddRequiredArgumentDiagnostic(request.SourceUri, "--source-uri", diagnostics);
        AddRequiredArgumentDiagnostic(request.SourceBranch, "--source-branch", diagnostics);
        AddRequiredArgumentDiagnostic(request.SourceVersion, "--source-version", diagnostics);
        AddRequiredArgumentDiagnostic(request.DataVersion, "--data-version", diagnostics);

        var historicalValues = new (string? Value, string Name)[]
        {
            (request.HistoricalBaseItemsPath, "--historical-base-items"),
            (request.HistoricalModsPath, "--historical-mods"),
            (request.HistoricalStatsPath, "--historical-stats"),
            (request.HistoricalTranslationsPath, "--historical-translations"),
            (request.HistoricalSourceRootPath, "--historical-source-root"),
            (request.HistoricalSourceDataRootPath, "--historical-source-data-root"),
            (request.HistoricalSourceUri, "--historical-source-uri"),
            (request.HistoricalSourceBranch, "--historical-source-branch"),
            (request.HistoricalSourceVersion, "--historical-source-version"),
            (request.HistoricalDataVersion, "--historical-data-version"),
        };
        if (historicalValues.Any(value => !string.IsNullOrWhiteSpace(value.Value)))
        {
            foreach (var value in historicalValues)
            {
                AddRequiredArgumentDiagnostic(value.Value, value.Name, diagnostics);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.OutputPath) && IsInsidePoEnhanceAppDirectory(request.OutputPath))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                ImportDiagnosticSeverity.Error,
                "--output",
                "Output path must not be inside a PoEnhance.App directory."));
        }

        ValidateSourceSnapshotArgument(request, diagnostics);
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

    private static IReadOnlyList<(string LogicalRole, string Label, string Path)> BuildInputFileList(
        GameDataPackageBuildRequest request)
    {
        return
        [
            ("baseItems", "base_items.json", request.BaseItemsPath!),
            ("modifiers", "mods.json", request.ModsPath!),
            ("stats", "stats.json", request.StatsPath!),
            ("statTranslations", "stat_translations.json", request.TranslationsPath!),
            ("itemClasses", "item_classes.json", request.ItemClassesPath!),
            ("tags", "tags.json", request.TagsPath!),
            ("baseModifierEvidence", "mods_by_base.json", request.ModsByBasePath!),
        ];
    }

    private static bool HasHistoricalInputs(GameDataPackageBuildRequest request) =>
        !string.IsNullOrWhiteSpace(request.HistoricalBaseItemsPath);

    private static IReadOnlyList<(string LogicalRole, string Label, string Path)> BuildHistoricalInputFileList(
        GameDataPackageBuildRequest request)
    {
        return
        [
            ("baseItems", "historical-base_items.json", request.HistoricalBaseItemsPath!),
            ("modifiers", "historical-mods.json", request.HistoricalModsPath!),
            ("stats", "historical-stats.json", request.HistoricalStatsPath!),
            ("statTranslations", "historical-stat_translations.json", request.HistoricalTranslationsPath!),
        ];
    }

    private static GameDataPackageManifest CreateManifest(
        GameDataPackageBuildRequest request,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<(string LogicalRole, string Label, string Path)> inputFiles,
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
                    DataVersion = TrimToNull(request.DataVersion),
                    SourceUri = TrimToNull(request.SourceUri) ?? RePoeSourceUri,
                    SourceBranch = TrimToNull(request.SourceBranch),
                    SourceRoot = NormalizePathOrNull(request.SourceRootPath),
                    SourceDataRoot = NormalizePathOrNull(request.SourceDataRootPath),
                    InputFiles = CreateInputFingerprints(request.SourceDataRootPath!, inputFiles),
                },
                ..(HasHistoricalInputs(request)
                    ? new[]
                    {
                        new GameDataPackageSource
                        {
                            SourceId = BaseImplicitHistoryBuilder.HistoricalManifestSourceId,
                            RetrievedAtUtc = createdAtUtc,
                            SourceVersion = TrimToNull(request.HistoricalSourceVersion),
                            DataVersion = TrimToNull(request.HistoricalDataVersion),
                            SourceUri = TrimToNull(request.HistoricalSourceUri),
                            SourceBranch = TrimToNull(request.HistoricalSourceBranch),
                            SourceRoot = NormalizePathOrNull(request.HistoricalSourceRootPath),
                            SourceDataRoot = NormalizePathOrNull(request.HistoricalSourceDataRootPath),
                            InputFiles = CreateInputFingerprints(
                                request.HistoricalSourceDataRootPath!,
                                BuildHistoricalInputFileList(request)),
                        },
                    }
                    : []),
            ],
        };
    }

    private static void ValidateSourceGuard(
        GameDataPackageBuildRequest request,
        IReadOnlyList<(string LogicalRole, string Label, string Path)> inputFiles,
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
        IReadOnlyList<(string LogicalRole, string Label, string Path)> inputFiles)
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

    private static IReadOnlyList<RePoeSourceSnapshotInput> CreateSourceSnapshotInputs(
        IReadOnlyList<(string LogicalRole, string Label, string Path)> inputFiles,
        IReadOnlyList<GameDataPackageInputFileFingerprint> fingerprints,
        string snapshotRole,
        string repositoryUri,
        string branch,
        string commitSha,
        string sourceDataVersion)
    {
        return inputFiles
            .Select(inputFile =>
            {
                var fingerprint = fingerprints.Single(candidate =>
                    string.Equals(candidate.Label, inputFile.Label, StringComparison.Ordinal));
                return new RePoeSourceSnapshotInput
                {
                    LogicalInputRole = inputFile.LogicalRole,
                    PackageInputLabel = inputFile.Label,
                    OriginalPath = inputFile.Path,
                    ExpectedSizeBytes = fingerprint.SizeBytes,
                    ExpectedSha256 = fingerprint.Sha256!,
                    SnapshotRole = snapshotRole,
                    RepositoryUri = repositoryUri,
                    Branch = branch,
                    CommitSha = commitSha,
                    SourceDataVersion = sourceDataVersion,
                };
            })
            .ToArray();
    }

    private static void ValidateSourceSnapshotArgument(
        GameDataPackageBuildRequest request,
        List<ImportDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(request.SourceSnapshotDirectory))
        {
            return;
        }

        try
        {
            var snapshotDirectory = Path.GetFullPath(request.SourceSnapshotDirectory);
            if (!string.IsNullOrWhiteSpace(request.SourceRootPath))
            {
                var sourceRoot = Path.GetFullPath(request.SourceRootPath);
                if (PathsEqual(snapshotDirectory, sourceRoot) ||
                    IsUnderDirectory(snapshotDirectory, sourceRoot))
                {
                    diagnostics.Add(Diagnostic(
                        RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                        ImportDiagnosticSeverity.Error,
                        "--source-snapshot-dir",
                        "Source snapshot output must be outside the RePoE checkout."));
                }
            }

            if (!string.IsNullOrWhiteSpace(request.HistoricalSourceRootPath))
            {
                var historicalSourceRoot = Path.GetFullPath(request.HistoricalSourceRootPath);
                if (PathsEqual(snapshotDirectory, historicalSourceRoot) ||
                    IsUnderDirectory(snapshotDirectory, historicalSourceRoot))
                {
                    diagnostics.Add(Diagnostic(
                        RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                        ImportDiagnosticSeverity.Error,
                        "--source-snapshot-dir",
                        "Source snapshot output must be outside the historical RePoE checkout."));
                }
            }

            if (!string.IsNullOrWhiteSpace(request.OutputPath))
            {
                var packageOutputPath = Path.GetFullPath(request.OutputPath);
                if (IsUnderDirectory(packageOutputPath, snapshotDirectory))
                {
                    diagnostics.Add(Diagnostic(
                        RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                        ImportDiagnosticSeverity.Error,
                        "--source-snapshot-dir",
                        "The runtime package output must not be placed inside the source snapshot directory."));
                }
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.BuildArgumentInvalid,
                ImportDiagnosticSeverity.Error,
                "--source-snapshot-dir",
                $"Source snapshot output path is invalid: {exception.Message}"));
        }
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

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(
            first.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            second.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);
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
            ItemClasses = package?.ItemClasses?.Count ?? 0,
            Tags = package?.Tags?.Count ?? 0,
            BaseModifierEvidenceGroups = package?.BaseModifierEvidence?.Groups.Count ?? 0,
            BaseModifierRelationships = package?.BaseModifierEvidence?.RelationshipsRepresented ?? 0,
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
        string? outputPath = null,
        string? sourceSnapshotDirectory = null,
        RePoeModsByBaseImportAudit? baseModifierEvidenceAudit = null)
    {
        return new GameDataPackageBuildResult
        {
            ExitCode = exitCode,
            Diagnostics = diagnostics,
            SourceSummaries = summaries ?? [],
            FinalCounts = counts ?? new GameDataPackageBuildRecordCounts(),
            OutputPath = outputPath,
            SourceSnapshotDirectory = sourceSnapshotDirectory,
            BaseModifierEvidenceAudit = baseModifierEvidenceAudit,
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
