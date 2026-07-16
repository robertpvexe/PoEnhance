namespace PoEnhance.GameData;

public sealed record GameDataPackageManifest
{
    public int SchemaVersion { get; init; }

    public string? DataVersion { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public string? League { get; init; }

    public string? Patch { get; init; }

    public GameDataPackageReviewedItemPropertySemanticInput? ReviewedItemPropertySemantics { get; init; }

    public GameDataPackageItemPropertySemanticAugmentation? ItemPropertySemanticAugmentation { get; init; }

    public IReadOnlyList<GameDataPackageSource> Sources { get; init; } = [];
}
