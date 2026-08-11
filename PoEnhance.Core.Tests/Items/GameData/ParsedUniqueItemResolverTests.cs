using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;
using PoEnhance.Core.Trade;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ParsedUniqueItemResolverTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedUniqueItemResolver resolver = new();

    [Fact]
    public void Resolve_HistoricalMultiLineBlock_RetainsOneSourceBlockAndMarksLegacy()
    {
        var parsed = parser.Parse("""
            Item Class: Wands
            Rarity: Unique
            Foulborn Test Calling
            Calling Wand
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +1 to maximum number of Raised Zombies
            +1 to maximum number of Spectres
            """);
        var catalog = CreateCatalog("Test Calling", "Calling Wand", UniqueItemKind.Ordinary,
            Version("Pre 3.29.0", UniqueItemVersionRole.Historical,
                MultiBlock()));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, result.Status);
        Assert.True(result.IsFoulborn);
        Assert.True(result.IsLegacy);
        var sourceBlock = Assert.Single(result.ModifierBlocks);
        Assert.True(sourceBlock.IsResolved);
        Assert.Single(sourceBlock.CatalogBlocks);
        Assert.Equal(["spectre_stat", "zombie_stat"], sourceBlock.StatIds.Order().ToArray());
        Assert.Equal(2, sourceBlock.SourceObservationIds.Count);
        Assert.True(sourceBlock.IsEquivalentSourceSet);
    }

    [Fact]
    public void Resolve_AuthenticFoulbornRaw_ResolvesOrdinaryBlockAndFailsReplacementClosed()
    {
        var parsed = parser.Parse("""
            Item Class: Wands
            Rarity: Unique
            Foulborn Midnight Bargain
            Calling Wand
            --------
            Item Level: 83
            --------
            { Unique Modifier — Minion }
            +1 to maximum number of Raised Zombies
            +1 to maximum number of Spectres
            +1 to maximum number of Skeletons
            { Foulborn Unique Modifier — Life, Defences, Energy Shield, Minion }
            Lose 0.5% Life and Energy Shield per Second per Minion
            """);
        var catalog = CreateCatalog("Midnight Bargain", "Calling Wand", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current, FoulbornOrdinaryBlock()));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, result.Status);
        Assert.True(result.IsFoulborn);
        Assert.Equal("Midnight Bargain", result.Identity?.CanonicalName);
        Assert.Collection(
            result.ModifierBlocks,
            ordinary =>
            {
                Assert.True(ordinary.IsResolved);
                Assert.Single(ordinary.CatalogBlocks);
                Assert.Equal(3, ordinary.StatIds.Count);
            },
            replacement =>
            {
                Assert.False(replacement.IsResolved);
                Assert.Empty(replacement.CatalogBlocks);
                Assert.Equal(
                    "FOULBORN_REPLACEMENT_MECHANICS_UNAVAILABLE",
                    replacement.DiagnosticCode);
            });

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        Assert.Equal(TradeTriState.Yes, draft.ItemVariantCriteria.Foulborn);
        Assert.Collection(
            draft.ModifierFilters,
            ordinary =>
            {
                Assert.True(ordinary.IsSearchable);
                Assert.Contains(Environment.NewLine, ordinary.OriginalText, StringComparison.Ordinal);
            },
            replacement =>
            {
                Assert.False(replacement.IsSearchable);
                Assert.False(replacement.IsSelected);
                Assert.Equal(
                    "FOULBORN_REPLACEMENT_MECHANICS_UNAVAILABLE",
                    replacement.UniqueResolutionDiagnosticCode);
            });
    }

    [Fact]
    public void Resolve_ReplicaDoesNotFallBackToOrdinaryIdentity()
    {
        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            Replica Test Flight
            Onyx Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Cannot be Stunned
            """);
        var catalog = CreateCatalog("Test Flight", "Onyx Amulet", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                Block("stun", "Cannot be Stunned", "stun")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.Unsupported, result.Status);
        Assert.Equal("UNIQUE_IDENTITY_NOT_FOUND", result.DiagnosticCode);
    }

    [Fact]
    public void CreateDraft_MultiLineUniqueSourceBlock_RemainsOneProvenanceBackedRow()
    {
        var parsed = parser.Parse("""
            Item Class: Wands
            Rarity: Unique
            Foulborn Test Calling
            Calling Wand
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +1 to maximum number of Raised Zombies
            +1 to maximum number of Spectres
            """);
        var catalog = CreateCatalog("Test Calling", "Calling Wand", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                MultiBlock()));

        var result = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog);

        var draft = Assert.IsType<TradeSearchDraft>(result.Draft);
        Assert.Equal(TradeTriState.Yes, draft.ItemVariantCriteria.Foulborn);
        Assert.Equal("Test Calling", draft.UniqueItemResolution?.Identity?.CanonicalName);
        var row = Assert.Single(draft.ModifierFilters);
        Assert.Contains(Environment.NewLine, row.OriginalText, StringComparison.Ordinal);
        Assert.Single(row.UniqueCatalogBlockIds);
        Assert.Equal(2, row.UniqueSourceObservationIds.Count);
        Assert.True(row.IsEquivalentSourceSet);
        Assert.True(row.IsSearchable);
        Assert.False(row.SupportsValueBounds);
    }

    [Fact]
    public void CreateDraft_ResolvedUniqueBlock_PreservesStatVectorPerStatLocalityAndSourceProvenance()
    {
        var parsed = parser.Parse("""
            Item Class: Shields
            Rarity: Unique
            Test Shield
            Test Round Shield
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +100 to Armour
            """);
        var catalog = CreateCatalog("Test Shield", "Test Round Shield", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                Block("local-armour", "+<number> to Armour", "local_armour_stat")));

        var result = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog);

        var row = Assert.Single(Assert.IsType<TradeSearchDraft>(result.Draft).ModifierFilters);
        Assert.Equal(["local_armour_stat"], row.ResolvedStatIds);
        Assert.Equal([ModifierLocality.Local], row.ResolvedStatLocalities);
        Assert.Equal(ModifierLocality.Local, row.Locality);
        Assert.Contains("+<number> to Armour", row.ProviderSearchSignatures);
        Assert.Single(row.UniqueCatalogBlockIds);
        Assert.Single(row.UniqueSourceObservationIds);
        var source = Assert.Single(row.Sources);
        Assert.Equal(["local_armour_stat"], source.ResolvedStatIds);
        Assert.Equal([ModifierLocality.Local], source.ResolvedStatLocalities);
    }

    [Fact]
    public void Resolve_CompatibleVersionsWithConflictingMechanics_FailsBlockClosed()
    {
        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            Test Calling
            Calling Wand
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Cannot be Stunned
            """);
        var catalog = CreateCatalog("Test Calling", "Calling Wand", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                Block("current-stun", "Cannot be Stunned", "current_stun_stat")),
            Version("Pre 3.29.0", UniqueItemVersionRole.Historical,
                Block("historical-stun", "Cannot be Stunned", "historical_stun_stat")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, result.Status);
        Assert.Equal(2, result.CompatibleVersions.Count);
        var block = Assert.Single(result.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Empty(block.StatIds);
        Assert.Equal("UNIQUE_BLOCK_INDEPENDENT_DIMENSIONS", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_EvaluatedFixedAndRangeAnnotations_SelectsOneCoherentVersion()
    {
        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            Test Flight
            Onyx Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            20(25)% increased Movement Speed
            { Unique Modifier }
            +19(13-19)% to Chaos Resistance
            """);
        var catalog = CreateCatalog("Test Flight", "Onyx Amulet", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                EvidenceBlock(
                    "current-speed",
                    "25% increased Movement Speed",
                    "<number>% increased Movement Speed",
                    "speed_stat"),
                EvidenceBlock(
                    "current-chaos",
                    "+(13-19)% to Chaos Resistance",
                    "+(<number>-<number>)% to Chaos Resistance",
                    "chaos_stat")),
            Version("Historical", UniqueItemVersionRole.Historical,
                EvidenceBlock(
                    "historical-speed",
                    "20% increased Movement Speed",
                    "<number>% increased Movement Speed",
                    "historical_speed_stat"),
                EvidenceBlock(
                    "historical-chaos",
                    "+(9-12)% to Chaos Resistance",
                    "+(<number>-<number>)% to Chaos Resistance",
                    "historical_chaos_stat")));

        var result = resolver.Resolve(parsed, catalog);

        var version = Assert.Single(result.CompatibleVersions);
        Assert.Equal("Current", version.Label);
        Assert.Equal(2, result.ModifierBlocks.Count);
        Assert.All(result.ModifierBlocks, block => Assert.True(block.IsResolved));
        Assert.Equal(
            ["chaos_stat", "speed_stat"],
            result.ModifierBlocks.SelectMany(block => block.StatIds).Order().ToArray());
    }

    [Fact]
    public void Resolve_SignedCanonicalRange_AllowsOnlyTheBackedPolarityInversion()
    {
        var parsed = parser.Parse("""
            Item Class: Belts
            Rarity: Unique
            Test Distillation
            Heavy Belt
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            34(-35-35)% increased Duration of Elemental Ailments on you
            """);
        var catalog = CreateCatalog("Test Distillation", "Heavy Belt", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                EvidenceBlock(
                    "ailment-duration",
                    "(-35-35)% reduced Duration of Elemental Ailments on you",
                    "<number>% reduced Duration of Elemental Ailments on you",
                    "ailment_duration_stat")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Single(result.CompatibleVersions);
        var block = Assert.Single(result.ModifierBlocks);
        Assert.True(block.IsResolved);
        Assert.Equal(["ailment_duration_stat"], block.StatIds);
    }

    [Fact]
    public void Resolve_UnsignedPolarityDifference_DoesNotBecomeACompatibleVersion()
    {
        var parsed = parser.Parse("""
            Item Class: Belts
            Rarity: Unique
            Test Distillation
            Heavy Belt
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            34% increased Duration of Elemental Ailments on you
            """);
        var catalog = CreateCatalog("Test Distillation", "Heavy Belt", UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                EvidenceBlock(
                    "ailment-duration",
                    "35% reduced Duration of Elemental Ailments on you",
                    "<number>% reduced Duration of Elemental Ailments on you",
                    "ailment_duration_stat")));

        var result = resolver.Resolve(parsed, catalog);

        Assert.Empty(result.CompatibleVersions);
        var block = Assert.Single(result.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void CreateDraft_GeneratedAttachedAnnotation_UsesCleanPresentationAndPreservesRawEvidence()
    {
        const string rawLine = "Pride(Fireball-Mana-Infused Staff) has no Reservation";
        var parsed = parser.Parse($$"""
            Item Class: Amulets
            Rarity: Unique
            Test Dragonflight
            Onyx Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            {{rawLine}}
            """);
        var catalog = CreateCatalog("Test Dragonflight", "Onyx Amulet", UniqueItemKind.Ordinary,
            Version("Generated", UniqueItemVersionRole.Current,
                EvidenceBlock(
                    "generated-pride",
                    "Pride has no Reservation",
                    "Pride has no Reservation",
                    "pride_stat",
                    "generated-observation:test")),
            Version("Non-generated", UniqueItemVersionRole.Historical,
                EvidenceBlock(
                    "non-generated-pride",
                    "Pride has no Reservation",
                    "Pride has no Reservation",
                    "other_pride_stat")));

        var resolution = resolver.Resolve(parsed, catalog);

        var version = Assert.Single(resolution.CompatibleVersions);
        Assert.Equal("Generated", version.Label);
        var block = Assert.Single(resolution.ModifierBlocks);
        Assert.Equal(["Pride has no Reservation"], block.PresentationLines);

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        var row = Assert.Single(draft.ModifierFilters);
        Assert.Equal(rawLine, row.OriginalText);
        Assert.Equal("Pride has no Reservation", row.PresentationText);
        Assert.Equal(rawLine, Assert.Single(row.Sources).OriginalText);
    }

    private static GameDataCatalog CreateCatalog(
        string name,
        string baseType,
        UniqueItemKind kind,
        params UniqueItemVersionObservation[] versions)
    {
        const string observation = "pob-observation:test";
        var mappings = versions.SelectMany(version => version.ModifierBlocks)
            .Select(block => block.MechanicalMapping)
            .ToArray();
        var statIds = mappings.SelectMany(mapping => mapping.StatIds)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var modifiers = mappings.SelectMany(mapping => mapping.ModifierIds.Select(modifierId =>
                new ModifierDefinition
                {
                    Id = modifierId,
                    GroupId = $"group:{modifierId}",
                    Name = modifierId,
                    GenerationType = ModifierGenerationType.Prefix,
                    Domain = "item",
                    Stats = mapping.StatIds.Select((statId, index) => new ModifierStat
                    {
                        Index = index,
                        StatId = statId,
                        MinValue = 1,
                        MaxValue = 1,
                    }).ToArray(),
                }))
            .DistinctBy(modifier => modifier.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 2,
                DataVersion = "test",
                CreatedAtUtc = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
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
            Stats = statIds.Select(statId => new StatDefinition
            {
                Id = statId,
                IsLocal = statId.StartsWith("local_", StringComparison.OrdinalIgnoreCase),
            }).ToArray(),
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
                        SourcePath = "Data/Uniques/test.lua",
                        ObservedKind = kind,
                        RawEntrySha256 = new string('a', 64),
                    },
                    new UniqueCatalogSourceObservation
                    {
                        Id = "pob-observation:test-two",
                        ManifestSourceId = "path-of-building",
                        RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                        Tag = "v2.67.2",
                        CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                        SourcePath = "Data/Uniques/test.lua",
                        ObservedKind = kind,
                        RawEntrySha256 = new string('b', 64),
                    },
                    new UniqueCatalogSourceObservation
                    {
                        Id = "generated-observation:test",
                        ManifestSourceId = "path-of-building",
                        RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                        Tag = "v2.67.2",
                        CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                        SourcePath = "Data/Uniques/generated.lua",
                        IsGenerated = true,
                        ObservedKind = kind,
                        RawEntrySha256 = new string('c', 64),
                    },
                ],
                Items =
                [
                    new UniqueItemIdentity
                    {
                        Id = "unique:test",
                        CanonicalName = name,
                        Kind = kind,
                        BaseTypeEvidence = [baseType],
                        Versions = versions.Select(version => version with { BaseType = baseType }).ToArray(),
                        SourceObservationIds = [observation],
                    },
                ],
            },
        });
    }

    private static UniqueItemVersionObservation Version(
        string label,
        UniqueItemVersionRole role,
        params UniqueModifierBlock[] blocks) => new()
    {
        Id = $"version:{label}",
        Label = label,
        Role = role,
        BaseType = "Calling Wand",
        ModifierBlocks = blocks,
        SourceObservationIds = ["pob-observation:test"],
    };

    private static UniqueModifierBlock Block(string id, string signature, string statId) => new()
    {
        Id = $"block:{id}",
        Kind = UniqueModifierBlockKind.Unique,
        Lines = [signature],
        CanonicalSignatures = [signature],
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Exact,
            ModifierIds = [$"modifier:{id}"],
            StatIds = [statId],
        },
        SourceObservationIds = id == "spectres"
            ? ["pob-observation:test-two"]
            : ["pob-observation:test"],
    };

    private static UniqueModifierBlock EvidenceBlock(
        string id,
        string line,
        string canonicalSignature,
        string statId,
        string sourceObservationId = "pob-observation:test") => new()
    {
        Id = $"block:{id}",
        Kind = UniqueModifierBlockKind.Unique,
        Lines = [line],
        CanonicalSignatures = [canonicalSignature],
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Exact,
            ModifierIds = [$"modifier:{id}"],
            StatIds = [statId],
        },
        SourceObservationIds = [sourceObservationId],
    };

    private static UniqueModifierBlock MultiBlock() => new()
    {
        Id = "block:minions",
        Kind = UniqueModifierBlockKind.Unique,
        Lines =
        [
            "+1 to maximum number of Raised Zombies",
            "+1 to maximum number of Spectres",
        ],
        CanonicalSignatures =
        [
            "+<number> to maximum number of Raised Zombies",
            "+<number> to maximum number of Spectres",
        ],
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Exact,
            ModifierIds = ["modifier:minions"],
            StatIds = ["zombie_stat", "spectre_stat"],
        },
        SourceObservationIds = ["pob-observation:test", "pob-observation:test-two"],
    };

    private static UniqueModifierBlock FoulbornOrdinaryBlock() => new()
    {
        Id = "block:midnight-bargain-minions",
        Kind = UniqueModifierBlockKind.Unique,
        Lines =
        [
            "+1 to maximum number of Raised Zombies",
            "+1 to maximum number of Spectres",
            "+1 to maximum number of Skeletons",
        ],
        CanonicalSignatures =
        [
            "+<number> to maximum number of Raised Zombies",
            "+<number> to maximum number of Spectres",
            "+<number> to maximum number of Skeletons",
        ],
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Exact,
            ModifierIds = ["modifier:midnight-bargain-minions"],
            StatIds = ["zombie_stat", "spectre_stat", "skeleton_stat"],
        },
        SourceObservationIds = ["pob-observation:test"],
    };
}
