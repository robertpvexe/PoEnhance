using System.Security.Cryptography;
using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class GameDataPackageWeaponPropertyAugmentationService
{
    public GameDataPackageWeaponPropertyAugmentationResult Augment(
        GameDataPackageWeaponPropertyAugmentationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var diagnostics = new List<ImportDiagnostic>();
        var inputPath = FullPath(request.InputPackagePath, nameof(request.InputPackagePath), diagnostics);
        var baseItemsPath = FullPath(request.BaseItemsPath, nameof(request.BaseItemsPath), diagnostics);
        var outputPath = FullPath(request.OutputPath, nameof(request.OutputPath), diagnostics);
        if (string.IsNullOrWhiteSpace(request.DataVersion))
        {
            diagnostics.Add(Error(nameof(request.DataVersion), "A non-empty candidate data version is required."));
        }

        if (inputPath is null || baseItemsPath is null || outputPath is null || diagnostics.Count > 0)
        {
            return Failure(diagnostics);
        }

        if (PathsEqual(inputPath, outputPath))
        {
            diagnostics.Add(Error(nameof(request.OutputPath), "Input and output package paths must differ."));
            return Failure(diagnostics);
        }

        if (!File.Exists(inputPath) || !File.Exists(baseItemsPath))
        {
            diagnostics.Add(Error(
                !File.Exists(inputPath) ? nameof(request.InputPackagePath) : nameof(request.BaseItemsPath),
                "A required augmentation input file is missing."));
            return Failure(diagnostics);
        }

        var inputBytes = File.ReadAllBytes(inputPath);
        var baseItemsBytes = File.ReadAllBytes(baseItemsPath);
        var inputSha = Sha256(inputBytes);
        var baseItemsSha = Sha256(baseItemsBytes);
        using var packageStream = new MemoryStream(inputBytes, writable: false);
        var loaded = GameDataPackageLoader.LoadFromStreamAsync(packageStream).GetAwaiter().GetResult();
        diagnostics.AddRange(loaded.Diagnostics.Select(diagnostic =>
            Error(nameof(request.InputPackagePath), $"{diagnostic.Code}: {diagnostic.Message}")));
        diagnostics.AddRange(loaded.ValidationErrors.Select(error =>
            Error(error.Path, $"{error.Code}: {error.Message}")));
        if (!loaded.IsSuccess || loaded.Package is null)
        {
            return Failure(diagnostics, inputBytes, inputSha, baseItemsBytes, baseItemsSha);
        }

        using var baseItemsStream = new MemoryStream(baseItemsBytes, writable: false);
        var imported = new RePoeBaseItemImporter().Import(baseItemsStream);
        diagnostics.AddRange(imported.Diagnostics);
        if (imported.HasErrors)
        {
            return Failure(diagnostics, inputBytes, inputSha, baseItemsBytes, baseItemsSha);
        }

        var importedById = imported.ImportedRecords
            .Where(itemBase => !string.IsNullOrWhiteSpace(itemBase.Id))
            .ToDictionary(itemBase => itemBase.Id!, StringComparer.Ordinal);
        var input = loaded.Package;
        var itemBases = input.ItemBases.Select(itemBase =>
            itemBase.Id is not null && importedById.TryGetValue(itemBase.Id, out var importedBase)
                ? itemBase with { WeaponProperties = importedBase.WeaponProperties }
                : itemBase).ToArray();
        var candidate = input with
        {
            Manifest = input.Manifest with { DataVersion = request.DataVersion.Trim() },
            ItemBases = itemBases,
        };

        var validation = GameDataPackageValidator.Validate(candidate);
        diagnostics.AddRange(validation.Errors.Select(error =>
            Error(error.Path, $"{error.Code}: {error.Message}")));
        if (!validation.IsValid)
        {
            return Result(
                false,
                candidate,
                diagnostics,
                inputBytes,
                inputSha,
                baseItemsBytes,
                baseItemsSha);
        }

        GameDataPackageAtomicWriter.Write(candidate, outputPath, out var outputSize, out var outputSha);
        return Result(
            true,
            candidate,
            diagnostics,
            inputBytes,
            inputSha,
            baseItemsBytes,
            baseItemsSha,
            outputSize,
            outputSha);
    }

    private static GameDataPackageWeaponPropertyAugmentationResult Result(
        bool success,
        GameDataPackage package,
        IReadOnlyList<ImportDiagnostic> diagnostics,
        byte[] inputBytes,
        string inputSha,
        byte[] baseItemsBytes,
        string baseItemsSha,
        long outputSize = 0,
        string? outputSha = null)
    {
        var likelyWeaponBases = package.ItemBases.Where(IsLikelyWeaponBase).ToArray();
        return new GameDataPackageWeaponPropertyAugmentationResult
        {
            IsSuccess = success,
            InputPackageSha256 = inputSha,
            InputPackageSizeBytes = inputBytes.LongLength,
            BaseItemsSha256 = baseItemsSha,
            BaseItemsSizeBytes = baseItemsBytes.LongLength,
            OutputSha256 = outputSha,
            OutputSizeBytes = outputSize,
            ItemBaseCount = package.ItemBases.Count,
            ItemBasesWithWeaponProperties = package.ItemBases.Count(itemBase => itemBase.WeaponProperties is not null),
            ItemBasesWithCompletePhysicalRange = package.ItemBases.Count(itemBase =>
                itemBase.WeaponProperties?.PhysicalDamageMinimum is not null &&
                itemBase.WeaponProperties.PhysicalDamageMaximum is not null),
            ItemBasesWithAttackTime = package.ItemBases.Count(itemBase =>
                itemBase.WeaponProperties?.AttackTimeMilliseconds is not null),
            ItemBasesWithCriticalStrikeChance = package.ItemBases.Count(itemBase =>
                itemBase.WeaponProperties?.CriticalStrikeChancePercent is not null),
            MissingCompleteWeaponPropertiesByClass = likelyWeaponBases
                .Where(itemBase =>
                    itemBase.WeaponProperties?.PhysicalDamageMinimum is null ||
                    itemBase.WeaponProperties.PhysicalDamageMaximum is null ||
                    itemBase.WeaponProperties.AttackTimeMilliseconds is null)
                .GroupBy(itemBase => itemBase.ItemClass ?? "Unknown", StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            Diagnostics = diagnostics,
            Package = package,
        };
    }

    private static bool IsLikelyWeaponBase(ItemBaseRecord itemBase) =>
        itemBase.Tags.Any(tag => string.Equals(tag, "weapon", StringComparison.OrdinalIgnoreCase)) ||
        itemBase.WeaponProperties is not null;

    private static GameDataPackageWeaponPropertyAugmentationResult Failure(
        IReadOnlyList<ImportDiagnostic> diagnostics,
        byte[]? inputBytes = null,
        string? inputSha = null,
        byte[]? baseItemsBytes = null,
        string? baseItemsSha = null) =>
        new()
        {
            Diagnostics = diagnostics,
            InputPackageSha256 = inputSha,
            InputPackageSizeBytes = inputBytes?.LongLength ?? 0,
            BaseItemsSha256 = baseItemsSha,
            BaseItemsSizeBytes = baseItemsBytes?.LongLength ?? 0,
        };

    private static string? FullPath(string path, string argument, List<ImportDiagnostic> diagnostics)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            diagnostics.Add(Error(argument, $"Path is invalid: {exception.Message}"));
            return null;
        }
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            first,
            second,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static ImportDiagnostic Error(string source, string message) =>
        new("PACKAGE_WEAPON_PROPERTY_AUGMENTATION_FAILED", ImportDiagnosticSeverity.Error, source, message);
}
