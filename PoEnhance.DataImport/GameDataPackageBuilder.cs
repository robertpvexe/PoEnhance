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
            itemPropertySemantics: [],
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
            itemPropertySemantics: [],
            requireCompleteStatReferences: true);
    }

    public GameDataPackageCreationResult CreatePackage(
        GameDataPackageManifest manifest,
        IEnumerable<ItemBaseRecord> itemBases,
        IEnumerable<ModifierDefinition> modifiers,
        IEnumerable<StatDefinition> stats,
        IEnumerable<StatTranslationDefinition> statTranslations,
        IEnumerable<ItemPropertySemanticDescriptor> itemPropertySemantics)
    {
        return CreatePackage(
            manifest,
            itemBases,
            modifiers,
            stats,
            statTranslations,
            itemPropertySemantics,
            requireCompleteStatReferences: true);
    }

    private static GameDataPackageCreationResult CreatePackage(
        GameDataPackageManifest manifest,
        IEnumerable<ItemBaseRecord> itemBases,
        IEnumerable<ModifierDefinition> modifiers,
        IEnumerable<StatDefinition> stats,
        IEnumerable<StatTranslationDefinition> statTranslations,
        IEnumerable<ItemPropertySemanticDescriptor> itemPropertySemantics,
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

        var modifierRecords = modifiers.ToArray();
        var modifierIds = modifierRecords
            .Where(modifier => modifier is not null)
            .Select(modifier => modifier.Id?.Trim())
            .Where(modifierId => !string.IsNullOrWhiteSpace(modifierId))
            .Select(modifierId => modifierId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var itemBaseRecords = requireCompleteStatReferences
            ? FilterItemBaseImplicitReferences(itemBases, modifierIds, diagnostics)
            : itemBases.ToArray();

        var package = new GameDataPackage
        {
            Manifest = manifest,
            ItemBases = itemBaseRecords,
            Modifiers = modifierRecords,
            Stats = stats.ToArray(),
            StatTranslations = statTranslations.ToArray(),
            ItemPropertySemantics = itemPropertySemantics.ToArray(),
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

    private static ItemBaseRecord[] FilterItemBaseImplicitReferences(
        IEnumerable<ItemBaseRecord> itemBases,
        ISet<string> modifierIds,
        List<ImportDiagnostic> diagnostics)
    {
        return itemBases
            .Select(itemBase => FilterItemBaseImplicitReferences(itemBase, modifierIds, diagnostics))
            .ToArray();
    }

    private static ItemBaseRecord FilterItemBaseImplicitReferences(
        ItemBaseRecord itemBase,
        ISet<string> modifierIds,
        List<ImportDiagnostic> diagnostics)
    {
        if (itemBase.ImplicitModifierIds.Count == 0)
        {
            return itemBase;
        }

        var retained = new List<string>(itemBase.ImplicitModifierIds.Count);
        foreach (var implicitModifierIdValue in itemBase.ImplicitModifierIds)
        {
            var implicitModifierId = implicitModifierIdValue?.Trim();
            if (string.IsNullOrWhiteSpace(implicitModifierId))
            {
                continue;
            }

            if (modifierIds.Contains(implicitModifierId))
            {
                retained.Add(implicitModifierId);
                continue;
            }

            diagnostics.Add(Diagnostic(
                RePoeImportDiagnosticCodes.PackageBaseImplicitModifierReferenceMissing,
                ImportDiagnosticSeverity.Warning,
                $"Item base '{itemBase.Id}' references implicit modifier id '{implicitModifierId}', but the assembled package does not contain that modifier; the runtime package will not retain this implicit reference."));
        }

        return retained.Count == itemBase.ImplicitModifierIds.Count
            ? itemBase
            : itemBase with { ImplicitModifierIds = retained };
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
