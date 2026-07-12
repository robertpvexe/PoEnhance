using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed class GameDataPackageBuilder
{
    public GameDataPackageCreationResult CreatePackage(
        GameDataPackageManifest manifest,
        IEnumerable<ItemBaseRecord> itemBases)
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
            Modifiers = [],
        };

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
