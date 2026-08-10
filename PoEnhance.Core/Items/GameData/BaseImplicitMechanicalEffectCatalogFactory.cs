using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

internal static class BaseImplicitMechanicalEffectCatalogFactory
{
    public static GameDataCatalog Create(BaseImplicitMechanicalEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        ArgumentNullException.ThrowIfNull(effect.Modifier);

        var sourceIds = effect.Modifier.Sources
            .Concat(effect.Stats.SelectMany(stat => stat.Sources))
            .Concat(effect.StatTranslations.SelectMany(translation => translation.Sources))
            .Select(source => source.SourceId?.Trim())
            .Where(sourceId => !string.IsNullOrWhiteSpace(sourceId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "base-implicit-mechanical-effect",
                CreatedAtUtc = DateTimeOffset.UnixEpoch,
                Sources = sourceIds.Select(sourceId => new GameDataPackageSource
                {
                    SourceId = sourceId,
                    RetrievedAtUtc = DateTimeOffset.UnixEpoch,
                }).ToArray(),
            },
            Modifiers = [effect.Modifier],
            Stats = effect.Stats,
            StatTranslations = effect.StatTranslations,
        });
    }
}
