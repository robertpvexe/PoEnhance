using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// A.1.9.2 — Fixed Unique + Advanced Item Description textual option-range matching.
/// </summary>
public sealed class FixedTextualOptionRangeUniqueMatchingTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedUniqueItemResolver resolver = new();

    [Fact]
    public async Task Resolve_ThreadOfHope_SmallRingAid_RetainsExactSmallVersion()
    {
        var catalog = await LoadActiveCatalogAsync();
        var parsed = parser.Parse(ThreadClipboard(
            "Only affects Passives in Small Ring(Small Ring-Very Large Ring)"));

        var ringEffect = Assert.Single(
            parsed.UniqueModifiers.SelectMany(modifier => modifier.Effects),
            effect => effect.SemanticText.Contains("Small Ring", StringComparison.Ordinal));
        Assert.Equal("Only affects Passives in Small Ring", ringEffect.SemanticText);
        Assert.Equal("Small Ring-Very Large Ring", ringEffect.TextualOptionRange?.Text);

        var resolution = resolver.Resolve(parsed, catalog);

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, resolution.Status);
        Assert.Null(resolution.DiagnosticCode);
        var version = Assert.Single(resolution.CompatibleVersions);
        Assert.Equal("Small Ring", version.Label);

        var ring = Assert.Single(
            resolution.ModifierBlocks,
            block => parsed.Modifiers[block.ParsedModifierIndex].ValueLines
                .Any(line => line.Contains("Small Ring", StringComparison.Ordinal)));
        Assert.True(ring.IsResolved);
        Assert.Null(ring.DiagnosticCode);
        Assert.Equal(["JewelRingRadiusValuesUnique__1"], ring.ModifierIds);
        Assert.Equal(["local_jewel_variable_ring_radius_value"], ring.StatIds);
        Assert.Equal(["Only affects Passives in Small Ring"], ring.PresentationLines);
        Assert.Equal(["Small Ring-Very Large Ring"], ring.TextualOptionRangeAnnotations);

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        var row = Assert.Single(
            draft.ModifierFilters,
            component => component.OriginalText?.Contains("Small Ring", StringComparison.Ordinal) == true);
        Assert.True(row.HasExactUniqueSourceProvenance);
        Assert.Null(row.UniqueResolutionDiagnosticCode);
        Assert.Equal(
            "Only affects Passives in Small Ring(Small Ring-Very Large Ring)",
            row.OriginalText);
        Assert.Equal("Only affects Passives in Small Ring", row.PresentationText);
    }

    [Fact]
    public async Task Resolve_ThreadOfHope_LargeRingAid_RetainsExactLargeVersion()
    {
        var catalog = await LoadActiveCatalogAsync();
        var parsed = parser.Parse(ThreadClipboard(
            "Only affects Passives in Large Ring(Small Ring-Very Large Ring)"));

        var resolution = resolver.Resolve(parsed, catalog);

        var version = Assert.Single(resolution.CompatibleVersions);
        Assert.Equal("Large Ring", version.Label);
        var ring = Assert.Single(
            resolution.ModifierBlocks,
            block => parsed.Modifiers[block.ParsedModifierIndex].ValueLines
                .Any(line => line.Contains("Large Ring", StringComparison.Ordinal)));
        Assert.True(ring.IsResolved);
        Assert.Null(ring.DiagnosticCode);
        Assert.Equal(["JewelRingRadiusValuesUnique__1"], ring.ModifierIds);
        Assert.Equal(["Only affects Passives in Large Ring"], ring.PresentationLines);
    }

    [Fact]
    public async Task Resolve_ThreadOfHope_MassivePlain_RemainsExact()
    {
        var catalog = await LoadActiveCatalogAsync();
        var parsed = parser.Parse(ThreadClipboard("Only affects Passives in Massive Ring"));

        var resolution = resolver.Resolve(parsed, catalog);

        var version = Assert.Single(resolution.CompatibleVersions);
        Assert.Equal("Massive Ring (Uber)", version.Label);
        var ring = Assert.Single(
            resolution.ModifierBlocks,
            block => parsed.Modifiers[block.ParsedModifierIndex].ValueLines
                .Any(line => line.Contains("Massive Ring", StringComparison.Ordinal)));
        Assert.True(ring.IsResolved);
        Assert.Equal(["JewelRingRadiusValuesUnique__2"], ring.ModifierIds);
        Assert.Empty(ring.TextualOptionRangeAnnotations);
    }

    [Fact]
    public async Task Resolve_IntuitiveLeap_Passage_RemainsExact()
    {
        var catalog = await LoadActiveCatalogAsync();
        var parsed = parser.Parse("""
            Item Class: Jewels
            Rarity: Unique
            Intuitive Leap
            Viridian Jewel
            --------
            Limited to: 1
            Radius: Small
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Passive Skills in Radius can be Allocated without being connected to your tree
            Passage
            """);

        var resolution = resolver.Resolve(parsed, catalog);

        Assert.NotEmpty(resolution.CompatibleVersions);
        var passage = Assert.Single(
            resolution.ModifierBlocks,
            block => parsed.Modifiers[block.ParsedModifierIndex].ValueLines
                .Any(line => line.Contains("Allocated", StringComparison.Ordinal)));
        Assert.True(passage.IsResolved);
        Assert.Contains(
            "JewelUniqueAllocateDisconnectedPassives",
            passage.ModifierIds);
    }

    [Fact]
    public void Resolve_FixedWithoutTextualOptionRange_UsesDirectPath()
    {
        const string line = "Only affects Passives in Small Ring";
        var parsed = parser.Parse($$"""
            Item Class: Jewels
            Rarity: Unique
            Test Jewel
            Crimson Jewel
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            {{line}}
            """);
        var catalog = CreateCatalog(
            "Test Jewel",
            "Crimson Jewel",
            Version("Small Ring", UniqueItemVersionRole.Current,
                EvidenceBlock("small", line, line, "ring_stat")));

        var resolution = resolver.Resolve(parsed, catalog);

        var version = Assert.Single(resolution.CompatibleVersions);
        Assert.Equal("Small Ring", version.Label);
        var block = Assert.Single(resolution.ModifierBlocks);
        Assert.True(block.IsResolved);
        Assert.Empty(block.PresentationLines);
        Assert.Empty(block.TextualOptionRangeAnnotations);
    }

    [Fact]
    public void Resolve_FixedTextualOptionRange_SemanticMismatch_FailsClosed()
    {
        var parsed = parser.Parse("""
            Item Class: Jewels
            Rarity: Unique
            Test Jewel
            Crimson Jewel
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Only affects Passives in Small Ring(Small Ring-Very Large Ring)
            """);
        var catalog = CreateCatalog(
            "Test Jewel",
            "Crimson Jewel",
            Version("Large Ring", UniqueItemVersionRole.Current,
                EvidenceBlock(
                    "large",
                    "Only affects Passives in Large Ring",
                    "Only affects Passives in Large Ring",
                    "ring_stat")));

        var resolution = resolver.Resolve(parsed, catalog);

        Assert.Empty(resolution.CompatibleVersions);
        var block = Assert.Single(resolution.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_FixedTextualOptionRange_MultipleVersionsSameSemantic_FailClosed()
    {
        const string semantic = "Only affects Passives in Small Ring";
        var parsed = parser.Parse($$"""
            Item Class: Jewels
            Rarity: Unique
            Test Jewel
            Crimson Jewel
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            {{semantic}}(Small Ring-Very Large Ring)
            """);
        var catalog = CreateCatalog(
            "Test Jewel",
            "Crimson Jewel",
            Version("First", UniqueItemVersionRole.Current,
                EvidenceBlock("first", semantic, semantic, "ring_stat_a")),
            Version("Second", UniqueItemVersionRole.Current,
                EvidenceBlock("second", semantic, semantic, "ring_stat_b")));

        var resolution = resolver.Resolve(parsed, catalog);

        // Both Current versions contain the copied block, so compatibility succeeds, but
        // independently mapped StatIds remain fail-closed.
        Assert.Equal(2, resolution.CompatibleVersions.Count);
        var block = Assert.Single(resolution.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_INDEPENDENT_DIMENSIONS", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_GeneratedTextualOptionRange_UnchangedAmbiguousCollision()
    {
        const string rawLine = "Selected modifier(Alpha-Omega) — Unscalable Value";
        var parsed = parser.Parse($$"""
            Item Class: Helmets
            Rarity: Unique
            Test Crown
            Great Crown
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            {{rawLine}}
            """);
        var version = Version("Generated", UniqueItemVersionRole.Current,
            GeneratedEvidenceBlock(
                "first-semantic-candidate",
                "Selected modifier",
                "Selected modifier",
                "first_stat",
                "pool:first"),
            GeneratedEvidenceBlock(
                "second-semantic-candidate",
                "Selected modifier",
                "Selected modifier",
                "second_stat",
                "pool:second")) with
        {
            GeneratedCandidateSelectionLimit = 2,
        };
        var catalog = CreateCatalog("Test Crown", "Great Crown", version);

        var result = resolver.Resolve(parsed, catalog);

        var block = Assert.Single(result.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_GENERATED_TEXTUAL_OPTION_RANGE_AMBIGUOUS", block.DiagnosticCode);
    }

    private static string ThreadClipboard(string ringLine) => $$"""
        Item Class: Jewels
        Rarity: Unique
        Thread of Hope
        Crimson Jewel
        --------
        Limited to: 1
        Radius: Variable
        --------
        Item Level: 86
        --------
        { Unique Modifier }
        {{ringLine}}
        { Unique Modifier }
        Passive Skills in Radius can be Allocated without being connected to your tree
        Passage
        { Unique Modifier }
        -(20-10)% to all Elemental Resistances
        --------
        Corrupted
        """;

    private static async Task<GameDataCatalog> LoadActiveCatalogAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(load.Package));
    }

    private static GameDataCatalog CreateCatalog(
        string name,
        string baseType,
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
                IsLocal = true,
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
                        ObservedKind = UniqueItemKind.Ordinary,
                        RawEntrySha256 = new string('a', 64),
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
                        ObservedKind = UniqueItemKind.Ordinary,
                        RawEntrySha256 = new string('c', 64),
                    },
                ],
                Items =
                [
                    new UniqueItemIdentity
                    {
                        Id = "unique:test",
                        CanonicalName = name,
                        Kind = UniqueItemKind.Ordinary,
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
        ModifierBlocks = blocks,
        SourceObservationIds = ["pob-observation:test"],
    };

    private static UniqueModifierBlock EvidenceBlock(
        string id,
        string line,
        string canonicalSignature,
        string statId) => new()
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
        SourceObservationIds = ["pob-observation:test"],
    };

    private static UniqueModifierBlock GeneratedEvidenceBlock(
        string id,
        string line,
        string canonicalSignature,
        string statId,
        string poolMembershipId) => EvidenceBlock(id, line, canonicalSignature, statId) with
    {
        SourceSemantics = UniqueModifierSourceSemantics.GeneratedCandidate,
        CandidatePoolMembershipIds = [poolMembershipId],
        SourceObservationIds = ["generated-observation:test"],
    };
}
