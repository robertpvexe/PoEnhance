using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

/// <summary>
/// Core consumption of axes produced by DataImport for pure-Current non-generated Alt pools.
/// </summary>
public sealed class UniqueOptionAxisCoSelectableConsumptionTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedUniqueItemResolver resolver = new();

    [Fact]
    public void Resolve_ImportedPureCurrentAltPool_AcceptsThreeChoicesAndRejectsFour()
    {
        var import = ImportAmbitionPool();
        var imported = Assert.Single(import.Catalog!.Items);
        var importedVersion = Assert.Single(imported.Versions);
        var importedAxis = Assert.Single(importedVersion.OptionAxes);
        Assert.Equal(3, importedAxis.SelectionLimit);
        Assert.Equal(5, importedAxis.Choices.Count);

        var choiceBlocks = importedVersion.ModifierBlocks
            .Where(block => block.OptionChoiceMemberships.Count > 0)
            .OrderBy(block => block.Lines[0], StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(5, choiceBlocks.Length);

        var observationId = importedVersion.SourceObservationIds[0];
        var catalogBlocks = choiceBlocks.Select((block, index) =>
        {
            var membership = Assert.Single(block.OptionChoiceMemberships);
            return block with
            {
                Id = $"block:choice-{index}",
                MechanicalMapping = new UniqueModifierMechanicalMapping
                {
                    Status = UniqueModifierMechanicalMappingStatus.Exact,
                    ModifierIds = [$"modifier:choice-{index}"],
                    StatIds = block.MechanicalMapping.StatIds,
                },
                SourceObservationIds = [observationId],
                SourceSemanticFingerprint = new UniqueModifierSemanticFingerprint
                {
                    Locality = UniqueModifierSemanticLocality.Global,
                    EvidenceMethods = ["pob-item-context-v1"],
                },
                OptionChoiceMemberships =
                [
                    new UniqueModifierOptionChoiceMembership
                    {
                        OptionAxisId = importedAxis.Id,
                        OptionChoiceId = membership.OptionChoiceId,
                        SourceObservationIds = [observationId],
                    },
                ],
            };
        }).ToArray();

        var version = new UniqueItemVersionObservation
        {
            Id = "version:observed",
            Label = importedVersion.Label,
            Role = UniqueItemVersionRole.Current,
            BaseType = "Prismatic Ring",
            ModifierBlocks = catalogBlocks,
            OptionAxes =
            [
                new UniqueItemOptionAxis
                {
                    Id = importedAxis.Id,
                    SelectionLimit = importedAxis.SelectionLimit,
                    Choices = importedAxis.Choices,
                    SourceObservationIds = importedAxis.SourceObservationIds,
                },
            ],
            SourceObservationIds = importedVersion.SourceObservationIds,
        };

        var catalog = CreateCatalog("Test Ambition Pool", "Prismatic Ring", version, catalogBlocks);

        var valid = resolver.Resolve(parser.Parse("""
            Item Class: Rings
            Rarity: Unique
            Test Ambition Pool
            Prismatic Ring
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            50% increased Alpha Damage
            { Unique Modifier }
            35% increased Beta Efficiency
            { Unique Modifier }
            50% increased Gamma Effect
            """), catalog);

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, valid.Status);
        Assert.Null(valid.DiagnosticCode);
        Assert.Contains(version.Label, valid.CompatibleVersions.Select(v => v.Label));
        Assert.Equal(3, valid.ModifierBlocks.Count(block => block.IsResolved));
        Assert.DoesNotContain(
            valid.ModifierBlocks,
            block => string.Equals(
                block.DiagnosticCode,
                "UNIQUE_OPTION_SELECTION_LIMIT_EXCEEDED",
                StringComparison.Ordinal));

        var invalid = resolver.Resolve(parser.Parse("""
            Item Class: Rings
            Rarity: Unique
            Test Ambition Pool
            Prismatic Ring
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            50% increased Alpha Damage
            { Unique Modifier }
            35% increased Beta Efficiency
            { Unique Modifier }
            50% increased Gamma Effect
            { Unique Modifier }
            50% increased Delta Damage
            """), catalog);

        Assert.Empty(invalid.CompatibleVersions);
        Assert.Contains(
            invalid.ModifierBlocks,
            block => string.Equals(
                block.DiagnosticCode,
                "UNIQUE_OPTION_SELECTION_LIMIT_EXCEEDED",
                StringComparison.Ordinal));
        Assert.All(
            invalid.ModifierBlocks.Where(block =>
                block.CatalogBlocks.Any(catalogBlock => catalogBlock.OptionChoiceMemberships.Count > 0)),
            block =>
            {
                Assert.False(block.IsResolved);
                Assert.Equal("UNIQUE_OPTION_SELECTION_LIMIT_EXCEEDED", block.DiagnosticCode);
            });
    }

    private static GameDataCatalog CreateCatalog(
        string name,
        string baseType,
        UniqueItemVersionObservation version,
        IReadOnlyList<UniqueModifierBlock> blocks)
    {
        var observation = version.SourceObservationIds[0];
        var modifiers = blocks.Select(block => new ModifierDefinition
        {
            Id = block.MechanicalMapping.ModifierIds[0],
            GroupId = block.MechanicalMapping.ModifierIds[0],
            Name = block.MechanicalMapping.ModifierIds[0],
            GenerationType = ModifierGenerationType.Implicit,
            SourceGenerationType = "unique",
            Domain = "item",
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = block.MechanicalMapping.StatIds[0],
                    MinValue = 1,
                    MaxValue = 1,
                },
            ],
        }).ToArray();
        var statIds = blocks
            .SelectMany(block => block.MechanicalMapping.StatIds)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 2,
                DataVersion = "test",
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "path-of-building",
                        RetrievedAtUtc = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
                    },
                ],
            },
            Modifiers = modifiers,
            Stats = statIds.Select(statId => new StatDefinition { Id = statId }).ToArray(),
            UniqueItems = new UniqueItemCatalog
            {
                SourceObservations =
                [
                    new UniqueCatalogSourceObservation
                    {
                        Id = observation,
                        ManifestSourceId = "path-of-building",
                        RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                        Tag = "v2.67.2",
                        CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                        SourcePath = "Data/Uniques/ring.lua",
                        ObservedKind = UniqueItemKind.Ordinary,
                        RawEntrySha256 = new string('a', 64),
                    },
                ],
                Items =
                [
                    new UniqueItemIdentity
                    {
                        Id = "unique:test-ambition-pool",
                        CanonicalName = name,
                        CanonicalIdentityKey = name,
                        Kind = UniqueItemKind.Ordinary,
                        BaseTypeEvidence = [baseType],
                        Versions = [version],
                        SourceObservationIds = [observation],
                    },
                ],
            },
        });
    }


    private static PoBUniqueCatalogImportResult ImportAmbitionPool()
    {
        var path = Path.Combine(Path.GetTempPath(), $"poenhance-pob-uniques-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
                {
                  "entries": [
                    {
                      "uniqueType": "ring",
                      "sourcePath": "Data/Uniques/ring.lua",
                      "generated": false,
                      "raw": "Test Ambition Pool\nPrismatic Ring\nHas Alt Variant: true\nHas Alt Variant Two: true\nSelected Variant: 1\nSelected Alt Variant: 2\nSelected Alt Variant Two: 3\nVariant: Choice Alpha\nVariant: Choice Beta\nVariant: Choice Gamma\nVariant: Choice Delta\nVariant: Choice Epsilon\nImplicits: 0\n{variant:1}(40-60)% increased Alpha Damage\n{variant:2}(30-40)% increased Beta Efficiency\n{variant:3}(40-60)% increased Gamma Effect\n{variant:4}(40-60)% increased Delta Damage\n{variant:5}(40-60)% increased Epsilon Effect"
                    }
                  ]
                }
                """);
            return new PoBUniqueCatalogImporter().Import(
                path,
                "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                "v2.67.2",
                "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                [
                    Modifier("unique.alpha", "alpha_damage", 40, 60),
                    Modifier("unique.beta", "beta_efficiency", 30, 40),
                    Modifier("unique.gamma", "gamma_effect", 40, 60),
                    Modifier("unique.delta", "delta_damage", 40, 60),
                    Modifier("unique.epsilon", "epsilon_effect", 40, 60),
                ],
                [
                    Translation("alpha", "alpha_damage", "{0}% increased Alpha Damage"),
                    Translation("beta", "beta_efficiency", "{0}% increased Beta Efficiency"),
                    Translation("gamma", "gamma_effect", "{0}% increased Gamma Effect"),
                    Translation("delta", "delta_damage", "{0}% increased Delta Damage"),
                    Translation("epsilon", "epsilon_effect", "{0}% increased Epsilon Effect"),
                ]);
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

    private static StatTranslationDefinition Translation(string id, string statId, string format) => new()
    {
        Id = id,
        StatIds = [statId],
        Variants =
        [
            new StatTranslationVariant
            {
                Conditions = [new StatTranslationCondition { Index = 0 }],
                FormatLines = [format],
                ValueFormats = ["#"],
                IndexHandlers = [new StatTranslationIndexHandler { Index = 0 }],
            },
        ],
    };
}
