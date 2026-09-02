using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class UniqueBlockIdentityImporterTests
{
    [Fact]
    public void ExtractSourceValueDomainKey_DistinguishesDistinctRangesWithSameNormalizedSignature()
    {
        var low = PoBUniqueCatalogImporter.ExtractSourceValueDomainKey(["+(8-10)% to all Elemental Resistances"]);
        var high = PoBUniqueCatalogImporter.ExtractSourceValueDomainKey(["+(20-25)% to all Elemental Resistances"]);

        Assert.NotEqual(low, high);
        Assert.Equal(
            PoBUniqueCatalogImporter.NormalizeSignature("+(8-10)% to all Elemental Resistances"),
            PoBUniqueCatalogImporter.NormalizeSignature("+(20-25)% to all Elemental Resistances"));
    }

    [Fact]
    public void ComputeLegacyFixedBlockStableId_CollidesForDistinctRangesWithSameSignature()
    {
        var identityId = "unique:test";
        const string versionLabel = "Current";
        var low = PoBUniqueCatalogImporter.ComputeLegacyFixedBlockStableId(
            identityId,
            versionLabel,
            UniqueModifierBlockKind.Unique,
            ["+(8-10)% to all Elemental Resistances"]);
        var high = PoBUniqueCatalogImporter.ComputeLegacyFixedBlockStableId(
            identityId,
            versionLabel,
            UniqueModifierBlockKind.Unique,
            ["+(20-25)% to all Elemental Resistances"]);

        Assert.Equal(low, high);
    }

    [Fact]
    public void Import_SameSignatureDifferentRanges_ProducesDistinctBlocksAndProvenance()
    {
        var result = ImportSingle(
            """
                Test Resistance Split
                Test Amulet
                Implicits: 0
                +(8-10)% to all Elemental Resistances
                +(20-25)% to all Elemental Resistances
                """,
            modifiers:
            [
                Modifier("implicit.all-res", "base_resist_all_elements_%", 8, 10),
                Modifier("unique.all-res", "base_resist_all_elements_%", 20, 25),
            ],
            translations:
            [
                Translation("all-res", "base_resist_all_elements_%", "{0}% to all Elemental Resistances", "+#"),
            ]);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.Equal(2, blocks.Count);
        Assert.Equal(2, blocks.Select(block => block.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(blocks, block =>
            block.Lines[0].Contains("(8-10)", StringComparison.Ordinal) &&
            block.MechanicalMapping.ModifierIds.Contains("implicit.all-res"));
        Assert.Contains(blocks, block =>
            block.Lines[0].Contains("(20-25)", StringComparison.Ordinal) &&
            block.MechanicalMapping.ModifierIds.Contains("unique.all-res"));
    }

    [Fact]
    public void Import_EquivalentSameRangeObservations_MergeProvenanceWithoutDuplicateBlocks()
    {
        const string raw = """
            Test Resistance Merge
            Test Amulet
            Implicits: 0
            +(20-25)% to all Elemental Resistances
            """;
        const string equivalentRaw = """
            Test Resistance Merge
            Test Amulet
            Source: Equivalent observation copy
            Implicits: 0
            +(20-25)% to all Elemental Resistances
            """;
        var path = WriteCatalog(
            (raw, null),
            (equivalentRaw, null));
        try
        {
            var result = ImportFromPath(
                path,
                [Modifier("unique.all-res", "base_resist_all_elements_%", 20, 25)],
                [Translation("all-res", "base_resist_all_elements_%", "{0}% to all Elemental Resistances", "+#")]);

            var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
                .ModifierBlocks);
            Assert.Equal(2, block.SourceObservationIds.Count);
            Assert.Equal(["unique.all-res"], block.MechanicalMapping.ModifierIds);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Import_SameSignatureDifferentSourceLocality_RemainDistinctBlocks()
    {
        const string line = "10% increased Attack Speed";
        var result = ImportSingle(
            $"""
                Test Blade
                Stiletto
                Implicits: 0
                {line}
                +5 to Strength
                {line}
                """,
            modifiers:
            [
                Modifier("unique.attack-speed.global", "attack_speed_+%", 10, 10),
                Modifier("unique.attack-speed.local", "local_attack_speed_+%", 10, 10),
            ],
            translations:
            [
                Translation("attack-speed-global", "attack_speed_+%", "{0}% increased Attack Speed", "#"),
                Translation("attack-speed-local", "local_attack_speed_+%", "{0}% increased Attack Speed", "#"),
            ],
            stats:
            [
                new StatDefinition { Id = "attack_speed_+%", IsLocal = false },
                new StatDefinition { Id = "local_attack_speed_+%", IsLocal = true },
            ],
            semanticFingerprints:
            [
                Fingerprint("unique", 0, line, "Stiletto", UniqueModifierSemanticLocality.Global),
                Fingerprint("unique", 2, line, "Stiletto", UniqueModifierSemanticLocality.Local),
            ]);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.Equal(2, blocks.Count);
        Assert.Equal(2, blocks.Select(block => block.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Import_RotmotherSourceData_RetainsDistinctAllResistanceBlocksWithExactUniqueProvenance()
    {
        const string raw = """
            Rotmother's Mutiny
            Pearlescent Amulet
            Variant: Fire
            Variant: Cold
            Variant: Lightning
            {tags:resistance}+(8-10)% to all Elemental Resistances
            {tags:resistance}+(20-25)% to all Elemental Resistances
            {tags:resistance}+(1-3)% to all maximum Resistances
            (25-75)% increased Light Radius
            {variant:2}+1% to Chaos Resistance per 1% Cold Resistance
            {variant:1}+1% to Chaos Resistance per 1% Fire Resistance
            {variant:3}+1% to Chaos Resistance per 1% Lightning Resistance
            """;
        var result = ImportSingle(
            raw,
            modifiers:
            [
                Modifier("AllResistancesImplicitAmulet1", "base_resist_all_elements_%", 8, 10),
                Modifier("AllResistancesUniqueAmulet87", "base_resist_all_elements_%", 20, 25),
                Modifier("IncreasedMaximumResistsUniqueAmulet87", "additional_maximum_all_resistances_%", 1, 3),
                Modifier("LightRadiusUniqueAmulet87", "light_radius_+%", 25, 75),
                Modifier("UniqueAmuletChaosResistancePerColdResistance", "chaos_damage_resistance_%_per_1%_cold_resistance", 1, 1),
                Modifier("UniqueAmuletChaosResistancePerFireResistance", "chaos_damage_resistance_%_per_1%_fire_resistance", 1, 1),
                Modifier("UniqueAmuletChaosResistancePerLightningResistance", "chaos_damage_resistance_%_per_1%_lightning_resistance", 1, 1),
            ],
            translations:
            [
                Translation("all-res", "base_resist_all_elements_%", "{0}% to all Elemental Resistances", "+#"),
                Translation("max-res", "additional_maximum_all_resistances_%", "{0}% to all maximum Resistances", "+#"),
                Translation("light-radius", "light_radius_+%", "{0}% increased Light Radius", "#"),
                Translation("chaos-per-cold", "chaos_damage_resistance_%_per_1%_cold_resistance", "{0}% to Chaos Resistance per {1}% Cold Resistance", "#", "#"),
                Translation("chaos-per-fire", "chaos_damage_resistance_%_per_1%_fire_resistance", "{0}% to Chaos Resistance per {1}% Fire Resistance", "#", "#"),
                Translation("chaos-per-lightning", "chaos_damage_resistance_%_per_1%_lightning_resistance", "{0}% to Chaos Resistance per {1}% Lightning Resistance", "#", "#"),
            ],
            baseItems:
            [
                new ItemBaseRecord
                {
                    Id = "Metadata/Items/Amulets/Amulet13",
                    Name = "Pearlescent Amulet",
                    Domain = "item",
                },
            ],
            semanticFingerprints:
            [
                Fingerprint("unique", 0, "+(8-10)% to all Elemental Resistances", "Pearlescent Amulet", UniqueModifierSemanticLocality.Global),
                Fingerprint("unique", 1, "+(20-25)% to all Elemental Resistances", "Pearlescent Amulet", UniqueModifierSemanticLocality.Global),
            ]);

        var version = Assert.Single(
            result.Catalog!.Items,
            item => item.CanonicalName == "Rotmother's Mutiny").Versions
            .First(version => version.Label == "Cold");
        var resistanceBlocks = version.ModifierBlocks
            .Where(block => block.Lines.Any(line => line.Contains("Elemental Resistances", StringComparison.Ordinal)))
            .ToArray();
        Assert.Equal(2, resistanceBlocks.Length);
        Assert.Equal(2, resistanceBlocks.Select(block => block.Id).Distinct(StringComparer.Ordinal).Count());

        var lowBlock = Assert.Single(resistanceBlocks, block => block.Lines[0].Contains("(8-10)", StringComparison.Ordinal));
        var highBlock = Assert.Single(resistanceBlocks, block => block.Lines[0].Contains("(20-25)", StringComparison.Ordinal));
        Assert.Contains("AllResistancesUniqueAmulet87", highBlock.MechanicalMapping.ModifierIds);
        Assert.DoesNotContain(
            highBlock.MechanicalMapping.ModifierIds,
            id => id.Equals("AllResistancesImplicitAmulet1", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("AllResistancesImplicitAmulet1", lowBlock.MechanicalMapping.ModifierIds);
        Assert.DoesNotContain(
            lowBlock.MechanicalMapping.ModifierIds,
            id => id.Equals("AllResistancesUniqueAmulet87", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CollisionAuditor_ClassifiesDistinctNumericDomainsWithinLegacyIdentityGroups()
    {
        var samples = new[]
        {
            Sample("Item A", "Current", ["+(8-10)% to all Elemental Resistances"], ["obs-1"]),
            Sample("Item A", "Current", ["+(20-25)% to all Elemental Resistances"], ["obs-1"]),
        };
        var legacyKey = PoBUniqueCatalogImporter.ComputeLegacyFixedBlockStableId(
            "unique:item-a",
            "Current",
            UniqueModifierBlockKind.Unique,
            samples[0].Lines);
        Assert.Equal(legacyKey, PoBUniqueCatalogImporter.ComputeLegacyFixedBlockStableId(
            "unique:item-a",
            "Current",
            UniqueModifierBlockKind.Unique,
            samples[1].Lines));
        Assert.Equal(
            UniqueBlockIdentityCollisionAuditor.CollisionClass.DistinctNumericValueDomain,
            UniqueBlockIdentityCollisionAuditor.ClassifyBlocks(samples));
    }

    private static UniqueBlockIdentityCollisionAuditor.CollisionBlockSample Sample(
        string item,
        string version,
        IReadOnlyList<string> lines,
        IReadOnlyList<string> observationIds) => new(
        item,
        version,
        "block-id",
        UniqueModifierBlockKind.Unique,
        lines,
        lines.Select(PoBUniqueCatalogImporter.NormalizeSignature).ToArray(),
        UniqueModifierSemanticLocality.Unknown,
        observationIds);

    private static object Fingerprint(
        string kind,
        int lineIndex,
        string line,
        string baseType,
        UniqueModifierSemanticLocality locality) => new
    {
        kind,
        lineIndex,
        line,
        baseType,
        locality = locality.ToString().ToLowerInvariant(),
        evidenceMethod = "pob-item-context-v1",
    };

    private static string WriteCatalog(params (string Raw, object[]? SemanticFingerprints)[] rawEntries)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-uniques-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new
        {
            entries = rawEntries.Select(entry => new
            {
                uniqueType = "amulet",
                sourcePath = "Data/Uniques/amulet.lua",
                generated = false,
                raw = entry.Raw,
                semanticFingerprints = entry.SemanticFingerprints,
            }),
        }));
        return path;
    }

    private static PoBUniqueCatalogImportResult ImportFromPath(
        string path,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemBaseRecord>? baseItems = null,
        IReadOnlyList<ItemPropertySemanticDescriptor>? itemPropertySemantics = null,
        IReadOnlyList<StatDefinition>? stats = null) =>
        new PoBUniqueCatalogImporter().Import(
            path,
            "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
            "v2.67.2",
            "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
            modifiers,
            translations,
            baseItems,
            itemPropertySemantics,
            stats);

    private static PoBUniqueCatalogImportResult ImportSingle(
        string raw,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemBaseRecord>? baseItems = null,
        IReadOnlyList<ItemPropertySemanticDescriptor>? itemPropertySemantics = null,
        IReadOnlyList<StatDefinition>? stats = null,
        object[]? semanticFingerprints = null)
    {
        var path = WriteCatalog((raw, semanticFingerprints));
        try
        {
            return ImportFromPath(path, modifiers, translations, baseItems, itemPropertySemantics, stats);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ModifierDefinition Modifier(string id, string statId, decimal min, decimal max) => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "item",
        Stats = [new ModifierStat { Index = 0, StatId = statId, MinValue = min, MaxValue = max }],
    };

    private static StatTranslationDefinition Translation(
        string id,
        string statId,
        string format,
        params string[] valueFormats) => new()
    {
        Id = id,
        StatIds = [statId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = valueFormats.Select((_, index) => new StatTranslationCondition { Index = index }).ToArray(),
                FormatLines = [format],
                ValueFormats = valueFormats,
                IndexHandlers = valueFormats.Select((_, index) => new StatTranslationIndexHandler { Index = index }).ToArray(),
            },
        ],
    };
}
