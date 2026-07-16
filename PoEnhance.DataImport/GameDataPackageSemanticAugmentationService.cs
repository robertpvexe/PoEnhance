using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class GameDataPackageSemanticAugmentationService
{
    private const string OperationId = "augment-package-semantics";
    private const string InputPackageLabel = "input-package";

    private readonly ReviewedItemPropertySemanticImporter _semanticImporter;
    private readonly Func<GameDataPackage, GameDataValidationResult> _finalPackageValidator;

    public GameDataPackageSemanticAugmentationService()
        : this(new ReviewedItemPropertySemanticImporter(), GameDataPackageValidator.Validate)
    {
    }

    internal GameDataPackageSemanticAugmentationService(
        ReviewedItemPropertySemanticImporter semanticImporter,
        Func<GameDataPackage, GameDataValidationResult> finalPackageValidator)
    {
        _semanticImporter = semanticImporter;
        _finalPackageValidator = finalPackageValidator;
    }

    public GameDataPackageSemanticAugmentationResult Augment(
        GameDataPackageSemanticAugmentationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var diagnostics = new List<ImportDiagnostic>();
        ValidateRequiredArguments(request, diagnostics);
        if (HasErrors(diagnostics))
        {
            return Failure(GameDataPackageSemanticAugmentationExitCode.InvalidArguments, diagnostics);
        }

        if (!TryGetFullPath(request.InputPackagePath!, "--input-package", diagnostics, out var inputPackagePath) ||
            !TryGetFullPath(request.OutputPath!, "--output", diagnostics, out var outputPath))
        {
            return Failure(GameDataPackageSemanticAugmentationExitCode.InvalidArguments, diagnostics);
        }

        if (PathsAreEqual(inputPackagePath, outputPath))
        {
            diagnostics.Add(Diagnostic(
                GameDataPackageSemanticAugmentationDiagnosticCodes.ArgumentInvalid,
                "--output",
                "Input package and output paths must resolve to different files."));
            return Failure(GameDataPackageSemanticAugmentationExitCode.InvalidArguments, diagnostics);
        }

        if (!File.Exists(inputPackagePath))
        {
            diagnostics.Add(Diagnostic(
                GameDataPackageSemanticAugmentationDiagnosticCodes.InputFileMissing,
                "--input-package",
                "Required input package file is missing."));
        }

        if (!File.Exists(request.ItemPropertySemanticsPath!))
        {
            diagnostics.Add(Diagnostic(
                GameDataPackageSemanticAugmentationDiagnosticCodes.InputFileMissing,
                "--item-property-semantics",
                "Required reviewed item-property semantic input file is missing."));
        }

        if (HasErrors(diagnostics))
        {
            return Failure(GameDataPackageSemanticAugmentationExitCode.MissingInputFile, diagnostics);
        }

        byte[] inputPackageBytes;
        try
        {
            inputPackageBytes = File.ReadAllBytes(inputPackagePath);
        }
        catch (Exception exception) when (IsFileReadFailure(exception))
        {
            diagnostics.Add(Diagnostic(
                GameDataPackageSemanticAugmentationDiagnosticCodes.InputPackageReadFailed,
                "--input-package",
                $"Input package could not be read: {exception.Message}"));
            return Failure(GameDataPackageSemanticAugmentationExitCode.InputPackageInvalid, diagnostics);
        }

        var inputPackageSha256 = GameDataPackageHash.ComputeSha256(inputPackageBytes);
        using var inputPackageStream = new MemoryStream(inputPackageBytes, writable: false);
        var inputLoad = GameDataPackageLoader
            .LoadFromStreamAsync(inputPackageStream)
            .GetAwaiter()
            .GetResult();
        AddInputLoadDiagnostics(inputLoad, diagnostics);
        if (!inputLoad.IsSuccess || inputLoad.Package is null)
        {
            return Failure(
                GameDataPackageSemanticAugmentationExitCode.InputPackageInvalid,
                diagnostics,
                inputPackagePath,
                inputPackageBytes.LongLength,
                inputPackageSha256);
        }

        byte[] semanticInputBytes;
        try
        {
            semanticInputBytes = File.ReadAllBytes(request.ItemPropertySemanticsPath!);
        }
        catch (Exception exception) when (IsFileReadFailure(exception))
        {
            diagnostics.Add(Diagnostic(
                GameDataPackageSemanticAugmentationDiagnosticCodes.SemanticInputReadFailed,
                "--item-property-semantics",
                $"Reviewed item-property semantic input could not be read: {exception.Message}"));
            return Failure(
                GameDataPackageSemanticAugmentationExitCode.SemanticImportFailure,
                diagnostics,
                inputPackagePath,
                inputPackageBytes.LongLength,
                inputPackageSha256);
        }

        var inputPackage = inputLoad.Package;
        using var semanticInputStream = new MemoryStream(semanticInputBytes, writable: false);
        var semanticImport = _semanticImporter.Import(semanticInputStream, inputPackage.Stats);
        diagnostics.AddRange(semanticImport.Diagnostics);
        if (semanticImport.HasErrors)
        {
            return Failure(
                GameDataPackageSemanticAugmentationExitCode.SemanticImportFailure,
                diagnostics,
                inputPackagePath,
                inputPackageBytes.LongLength,
                inputPackageSha256);
        }

        var semanticProvenance = ReviewedItemPropertySemanticProvenanceFactory.Create(
            request.ItemPropertySemanticsPath!,
            semanticInputBytes);
        var augmentationProvenance = new GameDataPackageItemPropertySemanticAugmentation
        {
            OperationId = OperationId,
            InputPackageLabel = InputPackageLabel,
            InputPackageDisplayPath = Path.GetFileName(inputPackagePath),
            InputPackageSizeBytes = inputPackageBytes.LongLength,
            InputPackageSha256 = inputPackageSha256,
            InputPackageDataVersion = inputPackage.Manifest.DataVersion,
        };
        var augmentedPackage = inputPackage with
        {
            Manifest = inputPackage.Manifest with
            {
                DataVersion = request.DataVersion!.Trim(),
                ReviewedItemPropertySemantics = semanticProvenance,
                ItemPropertySemanticAugmentation = augmentationProvenance,
            },
            ItemPropertySemantics = semanticImport.ImportedRecords.ToArray(),
        };

        var finalValidation = _finalPackageValidator(augmentedPackage);
        foreach (var error in finalValidation.Errors)
        {
            diagnostics.Add(Diagnostic(
                GameDataPackageSemanticAugmentationDiagnosticCodes.FinalPackageValidationFailed,
                error.Path,
                $"{error.Code}: {error.Message}"));
        }

        if (!finalValidation.IsValid)
        {
            return Failure(
                GameDataPackageSemanticAugmentationExitCode.FinalPackageValidationFailure,
                diagnostics,
                inputPackagePath,
                inputPackageBytes.LongLength,
                inputPackageSha256,
                counts: CountRecords(augmentedPackage));
        }

        try
        {
            GameDataPackageAtomicWriter.Write(augmentedPackage, outputPath, out var outputSize, out var outputSha256);
            return new GameDataPackageSemanticAugmentationResult
            {
                ExitCode = GameDataPackageSemanticAugmentationExitCode.Success,
                Diagnostics = diagnostics,
                FinalCounts = CountRecords(augmentedPackage),
                InputPackagePath = inputPackagePath,
                InputPackageSizeBytes = inputPackageBytes.LongLength,
                InputPackageSha256 = inputPackageSha256,
                OutputPath = outputPath,
                OutputFileSizeBytes = outputSize,
                Sha256 = outputSha256,
                Package = augmentedPackage,
            };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            diagnostics.Add(Diagnostic(
                GameDataPackageSemanticAugmentationDiagnosticCodes.OutputWriteFailed,
                "--output",
                $"Failed to write the augmented package output: {exception.Message}"));
            return Failure(
                GameDataPackageSemanticAugmentationExitCode.OutputWriteFailure,
                diagnostics,
                inputPackagePath,
                inputPackageBytes.LongLength,
                inputPackageSha256,
                CountRecords(augmentedPackage),
                outputPath);
        }
    }

    private static void ValidateRequiredArguments(
        GameDataPackageSemanticAugmentationRequest request,
        List<ImportDiagnostic> diagnostics)
    {
        AddRequiredArgumentDiagnostic(request.InputPackagePath, "--input-package", diagnostics);
        AddRequiredArgumentDiagnostic(request.ItemPropertySemanticsPath, "--item-property-semantics", diagnostics);
        AddRequiredArgumentDiagnostic(request.OutputPath, "--output", diagnostics);
        AddRequiredArgumentDiagnostic(request.DataVersion, "--data-version", diagnostics);
    }

    private static void AddRequiredArgumentDiagnostic(
        string? value,
        string argumentName,
        List<ImportDiagnostic> diagnostics)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            diagnostics.Add(Diagnostic(
                GameDataPackageSemanticAugmentationDiagnosticCodes.ArgumentInvalid,
                argumentName,
                $"Required argument is missing: {argumentName}."));
        }
    }

    private static bool TryGetFullPath(
        string path,
        string argumentName,
        List<ImportDiagnostic> diagnostics,
        out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            diagnostics.Add(Diagnostic(
                GameDataPackageSemanticAugmentationDiagnosticCodes.ArgumentInvalid,
                argumentName,
                $"Path is invalid: {exception.Message}"));
            fullPath = string.Empty;
            return false;
        }
    }

    private static bool PathsAreEqual(string first, string second)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return string.Equals(first, second, comparison);
    }

    private static void AddInputLoadDiagnostics(
        GameDataPackageLoadResult loadResult,
        List<ImportDiagnostic> diagnostics)
    {
        diagnostics.AddRange(loadResult.Diagnostics.Select(diagnostic => Diagnostic(
            diagnostic.Code,
            "--input-package",
            diagnostic.Message)));
        diagnostics.AddRange(loadResult.ValidationErrors.Select(error => Diagnostic(
            GameDataPackageSemanticAugmentationDiagnosticCodes.InputPackageValidationFailed,
            error.Path,
            $"{error.Code}: {error.Message}")));
    }

    private static GameDataPackageBuildRecordCounts CountRecords(GameDataPackage package)
    {
        return new GameDataPackageBuildRecordCounts
        {
            ItemBases = package.ItemBases.Count,
            Modifiers = package.Modifiers.Count,
            Stats = package.Stats.Count,
            StatTranslations = package.StatTranslations.Count,
            ItemPropertySemantics = package.ItemPropertySemantics.Count,
        };
    }

    private static bool IsFileReadFailure(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
    }

    private static bool HasErrors(IEnumerable<ImportDiagnostic> diagnostics)
    {
        return diagnostics.Any(diagnostic => diagnostic.Severity == ImportDiagnosticSeverity.Error);
    }

    private static GameDataPackageSemanticAugmentationResult Failure(
        GameDataPackageSemanticAugmentationExitCode exitCode,
        IReadOnlyList<ImportDiagnostic> diagnostics,
        string? inputPackagePath = null,
        long? inputPackageSizeBytes = null,
        string? inputPackageSha256 = null,
        GameDataPackageBuildRecordCounts? counts = null,
        string? outputPath = null)
    {
        return new GameDataPackageSemanticAugmentationResult
        {
            ExitCode = exitCode,
            Diagnostics = diagnostics,
            FinalCounts = counts ?? new GameDataPackageBuildRecordCounts(),
            InputPackagePath = inputPackagePath,
            InputPackageSizeBytes = inputPackageSizeBytes,
            InputPackageSha256 = inputPackageSha256,
            OutputPath = outputPath,
        };
    }

    private static ImportDiagnostic Diagnostic(string code, string? sourceRecordId, string message)
    {
        return new ImportDiagnostic(code, ImportDiagnosticSeverity.Error, sourceRecordId, message);
    }
}
