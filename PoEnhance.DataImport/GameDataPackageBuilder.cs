using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class GameDataPackageBuilder
{
    public GameDataPackageCreationResult CreatePackage(
        GameDataPackageManifest manifest,
        IEnumerable<ItemBaseRecord> itemBases)
    {
        return CreatePackage(
            manifest,
            itemBases,
            modifiers: [],
            stats: [],
            statTranslations: [],
            requireCompleteStatReferences: false);
    }

    public GameDataPackageCreationResult CreatePackage(
        GameDataPackageManifest manifest,
        IEnumerable<ItemBaseRecord> itemBases,
        IEnumerable<ModifierDefinition> modifiers,
        IEnumerable<StatDefinition> stats,
        IEnumerable<StatTranslationDefinition> statTranslations)
    {
        return CreatePackage(
            manifest,
            itemBases,
            modifiers,
            stats,
            statTranslations,
            requireCompleteStatReferences: true);
    }

    private static GameDataPackageCreationResult CreatePackage(
        GameDataPackageManifest manifest,
        IEnumerable<ItemBaseRecord> itemBases,
        IEnumerable<ModifierDefinition> modifiers,
        IEnumerable<StatDefinition> stats,
        IEnumerable<StatTranslationDefinition> statTranslations,
        bool requireCompleteStatReferences)
    {
        var diagnostics = new List<ImportDiagnostic>();

        var manifestValidation = GameDataPackageManifestValidator.Validate(manifest);
        foreach (var error in manifestValidation.Errors)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.PackageManifestInvalid,
                ImportDiagnosticSeverity.Error,
                $"{error.Code} at {error.Path}: {error.Message}"));
        }

        if (!ManifestDeclaresRePoeSource(manifest))
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.PackageRePoeSourceMissing,
                ImportDiagnosticSeverity.Error,
                "The manifest must declare SourceId 'repoe' before imported RePoE item bases can be packaged."));
        }

        if (diagnostics.Any(diagnostic => diagnostic.Severity == ImportDiagnosticSeverity.Error))
        {
            return new GameDataPackageCreationResult
            {
                Diagnostics = diagnostics,
            };
        }

        var package = new GameDataPackage
        {
            Manifest = manifest,
            ItemBases = itemBases.ToArray(),
            Modifiers = modifiers.ToArray(),
            Stats = stats.ToArray(),
            StatTranslations = statTranslations.ToArray(),
        };

        if (requireCompleteStatReferences)
        {
            AddCompleteStatReferenceDiagnostics(package, diagnostics);
        }

        var packageValidation = GameDataPackageValidator.Validate(package);
        foreach (var error in packageValidation.Errors)
        {
            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.PackageValidationFailed,
                ImportDiagnosticSeverity.Error,
                $"{error.Code} at {error.Path}: {error.Message}"));
        }

        return new GameDataPackageCreationResult
        {
            Package = diagnostics.Any(diagnostic => diagnostic.Severity == ImportDiagnosticSeverity.Error)
                ? null
                : package,
            Diagnostics = diagnostics,
        };
    }

    private static void AddCompleteStatReferenceDiagnostics(
        GameDataPackage package,
        List<ImportDiagnostic> diagnostics)
    {
        var statIds = package.Stats
            .Where(stat => stat is not null)
            .Where(stat => !string.IsNullOrWhiteSpace(stat.Id))
            .Select(stat => stat.Id!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var stat in package.Stats)
        {
            if (stat is null)
            {
                continue;
            }

            AddMissingAliasDiagnostic(stat.Id, stat.MainHandAliasId, "main-hand", statIds, diagnostics);
            AddMissingAliasDiagnostic(stat.Id, stat.OffHandAliasId, "off-hand", statIds, diagnostics);
        }

        foreach (var modifier in package.Modifiers)
        {
            if (modifier?.Stats is null)
            {
                continue;
            }

            foreach (var stat in modifier.Stats)
            {
                if (stat is null)
                {
                    continue;
                }

                var statId = stat.StatId?.Trim();
                if (!string.IsNullOrWhiteSpace(statId) && !statIds.Contains(statId))
                {
                    diagnostics.Add(Diagnostic(
                        RePoeImportDiagnosticCodes.PackageModifierStatReferenceMissing,
                        ImportDiagnosticSeverity.Error,
                        $"Modifier '{modifier.Id}' references stat id '{statId}', but the assembled package does not contain that stat."));
                }
            }
        }

        foreach (var translation in package.StatTranslations)
        {
            if (translation?.StatIds is null)
            {
                continue;
            }

            foreach (var statIdValue in translation.StatIds)
            {
                var statId = statIdValue?.Trim();
                if (!string.IsNullOrWhiteSpace(statId) && !statIds.Contains(statId))
                {
                    diagnostics.Add(Diagnostic(
                        RePoeImportDiagnosticCodes.PackageTranslationStatReferenceMissing,
                        ImportDiagnosticSeverity.Error,
                        $"Stat translation '{translation.Id}' references stat id '{statId}', but the assembled package does not contain that stat."));
                }
            }
        }
    }

    private static void AddMissingAliasDiagnostic(
        string? statId,
        string? aliasId,
        string aliasKind,
        ISet<string> statIds,
        List<ImportDiagnostic> diagnostics)
    {
        var normalizedAliasId = aliasId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAliasId) || statIds.Contains(normalizedAliasId))
        {
            return;
        }

        diagnostics.Add(Diagnostic(
            RePoeImportDiagnosticCodes.PackageStatAliasReferenceMissing,
            ImportDiagnosticSeverity.Error,
            $"Stat '{statId}' references {aliasKind} alias stat id '{normalizedAliasId}', but the assembled package does not contain that stat."));
    }

    private static bool ManifestDeclaresRePoeSource(GameDataPackageManifest manifest)
    {
        return manifest.Sources.Any(source =>
            string.Equals(source.SourceId?.Trim(), RePoeBaseItemImporter.SourceId, StringComparison.OrdinalIgnoreCase));
    }

    private static ImportDiagnostic Diagnostic(
        string code,
        ImportDiagnosticSeverity severity,
        string message)
    {
        return new ImportDiagnostic(code, severity, SourceRecordId: null, message);
    }
}
