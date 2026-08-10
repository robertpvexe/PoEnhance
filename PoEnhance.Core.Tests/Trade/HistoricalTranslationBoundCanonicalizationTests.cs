using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Trade;

public sealed class HistoricalTranslationBoundCanonicalizationTests
{
    [Fact]
    public void MechanicallyEquivalentHistoricalRendering_UsesCurrentCanonicalIdentityAndProviderSignature()
    {
        var current = Translation("{0}% increased Damage");
        var historical = Translation("Damage increased by {0}%");
        var modifier = new ModifierDefinition
        {
            Id = "modifier",
            GroupId = "group",
            Name = "Test",
            GenerationType = ModifierGenerationType.Prefix,
            Domain = "item",
            Stats = [new ModifierStat { Index = 0, StatId = "damage", MinValue = 1, MaxValue = 100 }],
            Sources = [new GameDataSourceReference { SourceId = "test" }],
        };
        var catalog = Catalog(modifier, current);
        var mechanicalIdentity = StatTranslationStructuralSemantics.MechanicalSignature(current);
        var evidence = new StatTranslationRecognitionEvidence
        {
            Role = StatTranslationRecognitionRole.HistoricalExact,
            CanonicalMechanicalSignature = mechanicalIdentity,
            CanonicalSignature = ModifierTextSignature.Create(["<number>% increased Damage"]),
            RecognizedTranslation = historical,
            CanonicalTranslation = current,
        };

        var result = ModifierBoundDefaults.Create(
            modifier,
            modifier.Stats,
            ["Damage increased by 20%"],
            catalog,
            evidence);

        Assert.True(result.IsSupported);
        Assert.Equal(20m, result.ObservedCanonicalValue);
        Assert.Equal(mechanicalIdentity, result.TranslationIdentity);
        Assert.Equal("<number>% increased Damage", result.ProviderCanonicalSignature);
    }

    private static GameDataCatalog Catalog(
        ModifierDefinition modifier,
        StatTranslationDefinition translation) => GameDataCatalog.FromPackage(new GameDataPackage
    {
        Manifest = new GameDataPackageManifest
        {
            SchemaVersion = 1,
            DataVersion = "test",
            CreatedAtUtc = DateTimeOffset.Parse("2026-08-10T00:00:00Z"),
            League = "test",
            Patch = "test",
            Sources = [new GameDataPackageSource { SourceId = "test" }],
        },
        ItemBases = [],
        Modifiers = [modifier],
        Stats = [new StatDefinition { Id = "damage", Sources = [new GameDataSourceReference { SourceId = "test" }] }],
        StatTranslations = [translation with { Sources = [new GameDataSourceReference { SourceId = "test" }] }],
    });

    private static StatTranslationDefinition Translation(string line) => new()
    {
        Id = "translation",
        StatIds = ["damage"],
        Language = "English",
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                ValueFormats = ["#"],
                IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
                FormatLines = [line],
            },
        ],
    };
}
