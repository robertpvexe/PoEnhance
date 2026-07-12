namespace PoEnhance.App.Infrastructure.GameData;

internal sealed record GameDataPackagePathResolution(
    string? Path,
    GameDataPackagePathSource Source)
{
    public bool IsConfigured => Source != GameDataPackagePathSource.None
        && !string.IsNullOrWhiteSpace(Path);
}
