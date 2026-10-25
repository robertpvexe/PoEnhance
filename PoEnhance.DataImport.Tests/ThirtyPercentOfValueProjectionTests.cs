using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

/// <summary>
/// Generic RePoE translation handler <c>30%_of_value</c> (e.g. legacy assumed-block spell block).
/// </summary>
public sealed class ThirtyPercentOfValueProjectionTests
{
    [Fact]
    public void Import_ThirtyPercentOfValue_ProjectsExactDisplay()
    {
        // Authentic Remnant-of-Empires shaped encoding: raw 100 with 30%_of_value → "30% Chance…"
        var result = Import(
            """
                Synthetic Assumed Spell Block
                Goathide Buckler
                Variant: Current
                Implicits: 0
                30% Chance to Block Spell Damage
                """,
            modifiers:
            [
                new ModifierDefinition
                {
                    Id = "SpellBlockUniqueDescentShieldStr1",
                    GroupId = "SpellBlockUniqueDescentShieldStr1",
                    Name = "SpellBlockUniqueDescentShieldStr1",
                    GenerationType = ModifierGenerationType.Implicit,
                    SourceGenerationType = "unique",
                    Domain = "item",
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "old_do_not_use_spell_block_%_from_assumed_block_value",
                            MinValue = 100,
                            MaxValue = 100,
                        },
                    ],
                },
            ],
            translations:
            [
                new StatTranslationDefinition
                {
                    Id = "assumed_block",
                    StatIds = ["old_do_not_use_spell_block_%_from_assumed_block_value"],
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions = [new StatTranslationCondition { Index = 0 }],
                            FormatLines = ["{0}% Chance to Block Spell Damage"],
                            ValueFormats = ["#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler
                                {
                                    Index = 0,
                                    Handlers = ["30%_of_value"],
                                },
                            ],
                        },
                    ],
                },
            ]);

        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(["SpellBlockUniqueDescentShieldStr1"], block.MechanicalMapping.ModifierIds);
        Assert.Equal(
            ["old_do_not_use_spell_block_%_from_assumed_block_value"],
            block.MechanicalMapping.StatIds);
    }

    private static PoBUniqueCatalogImportResult Import(
        string raw,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-30pct-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new[]
                {
                    new
                    {
                        uniqueType = "armour",
                        sourcePath = "Data/Uniques/test.lua",
                        generated = false,
                        raw,
                    },
                },
            }));
            return new PoBUniqueCatalogImporter().Import(
                path,
                "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                "v2.67.2",
                "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                modifiers,
                translations);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
