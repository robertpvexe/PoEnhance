using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

/// <summary>
/// A.5.48 — MULTI_LAYER Pure Talent composite translation packaging without invented ModIds.
/// </summary>
public sealed class ComponentCompositeTranslationPackagingTests
{
    private const string Header =
        "While your Passive Skill Tree connects to a class' starting location, you gain:";
    private const string MarauderLine = "Marauder: Melee Skills have 25% increased Area of Effect";
    private const string DuelistLine = "Duelist: 1% of Attack Damage Leeched as Life";
    private const string RangerLine = "Ranger: 7% increased Movement Speed";
    private const string ShadowLine = "Shadow: +0.5% to Critical Strike Chance";
    private const string WitchLine = "Witch: 0.5% of Mana Regenerated per second";
    private const string TemplarLine = "Templar: Damage Penetrates 5% Elemental Resistances";
    private const string ScionLine = "Scion: +25 to All Attributes";

    private static readonly string[] PureTalentPayloadLines =
    [
        Header,
        MarauderLine,
        DuelistLine,
        RangerLine,
        ShadowLine,
        WitchLine,
        TemplarLine,
        ScionLine,
    ];

    private static readonly string[] ComponentModIds =
    [
        "comp.marauder",
        "comp.duelist",
        "comp.ranger",
        "comp.shadow",
        "comp.witch",
        "comp.templar",
        "comp.scion",
    ];

    private static readonly string[] ComponentStatIds =
    [
        "stat_marauder",
        "stat_duelist",
        "stat_ranger",
        "stat_shadow",
        "stat_witch",
        "stat_templar",
        "stat_scion",
    ];

    [Fact]
    public void Import_CompleteClassHeadingComposite_PackagesExactComponentProvenance()
    {
        var result = ImportPureTalent(PureTalentPayloadLines);
        var version = Assert.Single(Assert.Single(result.Catalog!.Items).Versions);
        var block = Assert.Single(version.ModifierBlocks);

        Assert.Equal(8, block.Lines.Count);
        Assert.Equal(Header, block.Lines[0]);
        Assert.DoesNotContain(
            block.Lines,
            line => line.Contains("While your Passive Skill Tree connects to the Marauder",
                StringComparison.Ordinal));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Null(block.MechanicalMapping.DiagnosticCode);
        Assert.Null(block.Composition);
        Assert.Equal(ComponentModIds.OrderBy(id => id, StringComparer.Ordinal),
            block.MechanicalMapping.ModifierIds);
        Assert.Equal(ComponentStatIds, block.MechanicalMapping.StatIds);
        Assert.DoesNotContain(
            block.MechanicalMapping.ModifierIds,
            id => id.Contains('+') || id.Contains('|') || id.Contains("composite", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(
            PoBUniqueCatalogImporter.ComponentCompositeTranslationReason,
            block.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.Contains(
            block.SourceObservationIds,
            id => !string.IsNullOrWhiteSpace(id));
        Assert.Contains(
            block.MechanicalMapping.Provenance.Translations,
            evidence => evidence.StatIds.SequenceEqual(ComponentStatIds, StringComparer.OrdinalIgnoreCase) &&
                evidence.FormatLines.Count == 8);
    }

    [Fact]
    public void Import_MissingOneOfSeven_RemainsUnsupported()
    {
        var lines = PureTalentPayloadLines.Where(line => line != ScionLine).ToArray();
        AssertUnsupported(ImportPureTalent(lines));
    }

    [Fact]
    public void Import_OnlyOneOfSeven_RemainsUnsupported()
    {
        AssertUnsupported(ImportPureTalent([Header, MarauderLine]));
    }

    [Fact]
    public void Import_OnlySixOfSeven_RemainsUnsupported()
    {
        var lines = PureTalentPayloadLines.Where(line => line != WitchLine).ToArray();
        AssertUnsupported(ImportPureTalent(lines));
    }

    [Fact]
    public void Import_ExtraUnrelatedMechanic_NotSilentlyAbsorbed()
    {
        var lines = PureTalentPayloadLines.Concat(["+10 to maximum Life"]).ToArray();
        var result = ImportPureTalent(
            lines,
            extraModifiers:
            [
                Mod("extra.life", "maximum_life", 10, 10),
            ],
            extraTranslations:
            [
                SingleStatTranslation("life", "maximum_life", "{0} to maximum Life", "+#"),
            ]);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.Equal(2, blocks.Count);
        var composite = Assert.Single(
            blocks,
            block => block.Lines.Count == 8 &&
                block.MechanicalMapping.Status == UniqueModifierMechanicalMappingStatus.Exact);
        Assert.Equal(ComponentStatIds, composite.MechanicalMapping.StatIds);
        Assert.Contains(
            PoBUniqueCatalogImporter.ComponentCompositeTranslationReason,
            composite.MechanicalMapping.Provenance!.ResolutionReasons);
        var extra = Assert.Single(
            blocks,
            block => block.Lines.Count == 1 &&
                block.Lines[0].Contains("maximum Life", StringComparison.Ordinal));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, extra.MechanicalMapping.Status);
        Assert.Equal(["maximum_life"], extra.MechanicalMapping.StatIds);
        Assert.DoesNotContain(
            ComponentStatIds,
            statId => extra.MechanicalMapping.StatIds.Contains(statId, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Import_DisagreeingCompleteFingerprint_FailsClosed()
    {
        var result = ImportPureTalent(
            PureTalentPayloadLines,
            extraModifiers:
            [
                Mod("alt.marauder", "stat_marauder_alt", 25, 25),
                Mod("alt.duelist", "stat_duelist_alt", 100, 100),
                Mod("alt.ranger", "stat_ranger_alt", 7, 7),
                Mod("alt.shadow", "stat_shadow_alt", 50, 50),
                Mod("alt.witch", "stat_witch_alt", 30, 30),
                Mod("alt.templar", "stat_templar_alt", 5, 5),
                Mod("alt.scion", "stat_scion_alt", 25, 25),
            ],
            extraTranslations:
            [
                CompositeTranslation(
                    "alt-composite",
                    [
                        "stat_marauder_alt",
                        "stat_duelist_alt",
                        "stat_ranger_alt",
                        "stat_shadow_alt",
                        "stat_witch_alt",
                        "stat_templar_alt",
                        "stat_scion_alt",
                    ]),
            ]);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.DoesNotContain(
            blocks,
            block => block.MechanicalMapping.Status == UniqueModifierMechanicalMappingStatus.Exact);
        Assert.DoesNotContain(
            blocks,
            block => block.MechanicalMapping.Provenance?.ResolutionReasons.Contains(
                PoBUniqueCatalogImporter.ComponentCompositeTranslationReason) == true);
        Assert.Contains(
            blocks,
            block => block.MechanicalMapping.Status == UniqueModifierMechanicalMappingStatus.Unsupported);
    }

    [Fact]
    public void Import_SameHeadingDifferentPayload_RemainsUnsupported()
    {
        AssertUnsupported(ImportPureTalent(
        [
            Header,
            "Marauder: Melee Skills have 99% increased Area of Effect",
            DuelistLine,
            RangerLine,
            ShadowLine,
            WitchLine,
            TemplarLine,
            ScionLine,
        ]));
    }

    [Fact]
    public void Import_UnrelatedContiguousLines_DoNotBecomeComposite()
    {
        var result = ImportSingle(
            """
                Contiguous Probe
                Viridian Jewel
                Implicits: 0
                +10 to maximum Life
                +20 to maximum Mana
                """,
            modifiers:
            [
                Mod("life", "maximum_life", 10, 10),
                Mod("mana", "maximum_mana", 20, 20),
            ],
            translations:
            [
                SingleStatTranslation("life", "maximum_life", "{0} to maximum Life", "+#"),
                SingleStatTranslation("mana", "maximum_mana", "{0} to maximum Mana", "+#"),
            ]);

        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.Equal(2, blocks.Count);
        Assert.All(
            blocks,
            block => Assert.Equal(1, block.Lines.Count));
        Assert.DoesNotContain(
            blocks,
            block => block.MechanicalMapping.Provenance?.ResolutionReasons.Contains(
                PoBUniqueCatalogImporter.ComponentCompositeTranslationReason) == true);
    }

    [Fact]
    public void Import_ReorderedCompositeLines_SucceedWhenTranslationSemanticsAreOrderIrrelevant()
    {
        string[] reordered =
        [
            ScionLine,
            Header,
            TemplarLine,
            WitchLine,
            ShadowLine,
            RangerLine,
            DuelistLine,
            MarauderLine,
        ];
        var result = ImportPureTalent(reordered);
        var block = Assert.Single(Assert.Single(Assert.Single(result.Catalog!.Items).Versions)
            .ModifierBlocks);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, block.MechanicalMapping.Status);
        Assert.Equal(ComponentStatIds, block.MechanicalMapping.StatIds);
        Assert.Equal(8, block.Lines.Count);
    }

    [Fact]
    public void Import_ReplicaStyleWordingMismatch_RemainsUnsupported()
    {
        // Metres / +2 style Duelist mismatch must not be forced Exact via synonym stripping.
        AssertUnsupported(ImportPureTalent(
        [
            Header,
            MarauderLine,
            "Duelist: +2 to Melee Strike Range",
            RangerLine,
            ShadowLine,
            WitchLine,
            TemplarLine,
            ScionLine,
        ]));
    }

    [Fact]
    public void Import_HeaderAlone_IsNotAnEighthMechanic()
    {
        AssertUnsupported(ImportPureTalent([Header]));
    }

    private static void AssertUnsupported(PoBUniqueCatalogImportResult result)
    {
        var blocks = Assert.Single(Assert.Single(result.Catalog!.Items).Versions).ModifierBlocks;
        Assert.DoesNotContain(
            blocks,
            block => block.MechanicalMapping.Status == UniqueModifierMechanicalMappingStatus.Exact &&
                block.MechanicalMapping.Provenance?.ResolutionReasons.Contains(
                    PoBUniqueCatalogImporter.ComponentCompositeTranslationReason) == true);
        Assert.Contains(
            blocks,
            block => block.MechanicalMapping.Status == UniqueModifierMechanicalMappingStatus.Unsupported);
    }

    private static PoBUniqueCatalogImportResult ImportPureTalent(
        IReadOnlyList<string> effectLines,
        IReadOnlyList<ModifierDefinition>? extraModifiers = null,
        IReadOnlyList<StatTranslationDefinition>? extraTranslations = null)
    {
        var raw = string.Join(
            "\n",
            new[] { "Pure Talent Probe", "Viridian Jewel", "Implicits: 0" }.Concat(effectLines));
        return ImportSingle(
            raw,
            modifiers: PureTalentComponents().Concat(extraModifiers ?? []).ToArray(),
            translations: new[] { PureTalentCompositeTranslation() }
                .Concat(extraTranslations ?? [])
                .ToArray(),
            baseItems: [new ItemBaseRecord { Name = "Viridian Jewel", Domain = "misc" }]);
    }

    private static ModifierDefinition[] PureTalentComponents() =>
    [
        Mod(ComponentModIds[0], ComponentStatIds[0], 25, 25) with { Domain = "misc" },
        Mod(ComponentModIds[1], ComponentStatIds[1], 100, 100) with { Domain = "misc" },
        Mod(ComponentModIds[2], ComponentStatIds[2], 7, 7) with { Domain = "misc" },
        Mod(ComponentModIds[3], ComponentStatIds[3], 50, 50) with { Domain = "misc" },
        Mod(ComponentModIds[4], ComponentStatIds[4], 30, 30) with { Domain = "misc" },
        Mod(ComponentModIds[5], ComponentStatIds[5], 5, 5) with { Domain = "misc" },
        Mod(ComponentModIds[6], ComponentStatIds[6], 25, 25) with { Domain = "misc" },
    ];

    private static StatTranslationDefinition PureTalentCompositeTranslation() =>
        CompositeTranslation("class-heading-composite", ComponentStatIds);

    private static StatTranslationDefinition CompositeTranslation(
        string id,
        IReadOnlyList<string> statIds) => new()
    {
        Id = id,
        StatIds = statIds.ToArray(),
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = Enumerable.Range(0, 7)
                    .Select(index => new StatTranslationCondition { Index = index })
                    .ToArray(),
                ValueFormats = ["#", "#", "#", "+#", "#", "#", "+#"],
                IndexHandlers =
                [
                    new StatTranslationIndexHandler { Index = 0 },
                    new StatTranslationIndexHandler
                    {
                        Index = 1,
                        Handlers = ["divide_by_one_hundred"],
                    },
                    new StatTranslationIndexHandler { Index = 2 },
                    new StatTranslationIndexHandler
                    {
                        Index = 3,
                        Handlers = ["divide_by_one_hundred"],
                    },
                    new StatTranslationIndexHandler
                    {
                        Index = 4,
                        Handlers = ["per_minute_to_per_second"],
                    },
                    new StatTranslationIndexHandler { Index = 5 },
                    new StatTranslationIndexHandler { Index = 6 },
                ],
                FormatLines =
                [
                    Header,
                    "Marauder: Melee Skills have {0}% increased Area of Effect",
                    "Duelist: {1}% of Attack Damage Leeched as Life",
                    "Ranger: {2}% increased Movement Speed",
                    "Shadow: {3}% to Critical Strike Chance",
                    "Witch: {4}% of Mana Regenerated per second",
                    "Templar: Damage Penetrates {5}% Elemental Resistances",
                    "Scion: {6} to All Attributes",
                ],
            },
        ],
    };

    private static StatTranslationDefinition SingleStatTranslation(
        string id,
        string statId,
        string format,
        string valueFormat) => new()
    {
        Id = id,
        StatIds = [statId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                FormatLines = [format],
                ValueFormats = [valueFormat],
                IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
            },
        ],
    };

    private static ModifierDefinition Mod(
        string id,
        string statId,
        decimal min,
        decimal max) => new()
    {
        Id = id,
        GroupId = id,
        Name = id,
        GenerationType = ModifierGenerationType.Implicit,
        SourceGenerationType = "unique",
        Domain = "misc",
        Stats = [new ModifierStat { Index = 0, StatId = statId, MinValue = min, MaxValue = max }],
    };

    private static PoBUniqueCatalogImportResult ImportSingle(
        string raw,
        IReadOnlyList<ModifierDefinition> modifiers,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ItemBaseRecord>? baseItems = null)
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-composite-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new
            {
                entries = new object[]
                {
                    new
                    {
                        uniqueType = "jewel",
                        sourcePath = "Data/Uniques/jewel.lua",
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
                translations,
                baseItems ?? [new ItemBaseRecord { Name = "Viridian Jewel", Domain = "misc" }],
                stats: ComponentStatIds.Concat(["maximum_life", "maximum_mana"])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(statId => new StatDefinition { Id = statId, IsLocal = false })
                    .ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
