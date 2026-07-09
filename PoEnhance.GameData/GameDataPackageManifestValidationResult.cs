namespace PoEnhance.GameData;

public sealed record GameDataPackageManifestValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}
