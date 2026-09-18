using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// A.5.6 — AID <c>— Unscalable Value</c> must not reject Fixed Unique block compatibility when
/// cleaned mechanical SemanticText already exactly matches the Fixed catalog block.
/// </summary>
public sealed class FixedUniqueAidUnscalableCompatibilityTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedUniqueItemResolver resolver = new();

    [Fact]
    public void Resolve_FixedExactMechanics_WithAidUnscalableSibling_IsExact()
    {
        var parsed = parser.Parse(Clipboard(
            "Selected variant(Alpha-Omega)",
            "Historic — Unscalable Value"));
        var historic = Assert.Single(parsed.Modifiers.SelectMany(m => m.Effects), e => e.Text == "Historic");
        Assert.True(historic.HasUnscalableValue);
        Assert.EndsWith(" — Unscalable Value", historic.RawText, StringComparison.Ordinal);
        Assert.Equal("Historic", historic.Text);

        var resolution = resolver.Resolve(parsed, CreateSingleVariantCatalog());

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, resolution.Status);
        Assert.Null(resolution.DiagnosticCode);
        var block = Assert.Single(resolution.ModifierBlocks);
        Assert.True(block.IsResolved);
        Assert.Null(block.DiagnosticCode);
        Assert.Equal(["modifier:variant"], block.ModifierIds);
        Assert.Equal(["stat:variant"], block.StatIds);
        Assert.Equal(
            ["Selected variant", "Historic"],
            block.PresentationLines);
        Assert.Equal(["Alpha-Omega"], block.TextualOptionRangeAnnotations);

        // Metadata remains on the parsed component after Exact resolution.
        Assert.True(historic.HasUnscalableValue);
        Assert.EndsWith(" — Unscalable Value", historic.RawText, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_FixedExactMechanics_WithoutAidUnscalable_IsSameExact()
    {
        var with = resolver.Resolve(
            parser.Parse(Clipboard("Selected variant(Alpha-Omega)", "Historic — Unscalable Value")),
            CreateSingleVariantCatalog());
        var without = resolver.Resolve(
            parser.Parse(Clipboard("Selected variant(Alpha-Omega)", "Historic")),
            CreateSingleVariantCatalog());

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, with.Status);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, without.Status);
        Assert.Equal(
            Assert.Single(with.ModifierBlocks).ModifierIds,
            Assert.Single(without.ModifierBlocks).ModifierIds);
        Assert.Equal(
            Assert.Single(with.ModifierBlocks).StatIds,
            Assert.Single(without.ModifierBlocks).StatIds);
        Assert.Equal(
            Assert.Single(with.ModifierBlocks).PresentationLines,
            Assert.Single(without.ModifierBlocks).PresentationLines);
    }

    [Fact]
    public void CreateDraft_FixedExactWithAidUnscalable_PreservesUnscalableAndSourceProvenance()
    {
        var parsed = parser.Parse(Clipboard(
            "Selected variant(Alpha-Omega)",
            "Historic — Unscalable Value"));
        var catalog = CreateSingleVariantCatalog();

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);

        var row = Assert.Single(draft.ModifierFilters);
        Assert.True(row.IsSearchable);
        Assert.True(row.HasExactUniqueSourceProvenance);
        Assert.Null(row.UniqueResolutionDiagnosticCode);
        Assert.Contains("Historic", row.OriginalText, StringComparison.Ordinal);
        Assert.Contains(
            parsed.Modifiers.SelectMany(m => m.Effects),
            e => e.HasUnscalableValue &&
                 e.RawText.EndsWith(" — Unscalable Value", StringComparison.Ordinal));
        Assert.Contains("Historic", row.PresentationText ?? string.Empty, StringComparison.Ordinal);
        Assert.NotEmpty(row.ResolvedStatIds);
    }

    [Fact]
    public void Resolve_AidUnscalable_ButMechanicalLineDiffers_FailsClosed()
    {
        var parsed = parser.Parse(Clipboard(
            "Selected variant(Alpha-Omega)",
            "Not Historic — Unscalable Value"));
        var resolution = resolver.Resolve(parsed, CreateSingleVariantCatalog());

        Assert.Empty(resolution.CompatibleVersions);
        var block = Assert.Single(resolution.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_AidUnscalable_ButObservedRollIncompatible_FailsClosed()
    {
        // Catalog roll domain (1-10) vs copied annotation (50-60) is mechanically incompatible.
        var parsed = parser.Parse(Clipboard(
            "Rolls 55(50-60) damage(Alpha-Omega)",
            "Historic — Unscalable Value"));
        var catalog = CreateCatalog(
            Version(
                "Current",
                UniqueItemVersionRole.Current,
                FixedBlock(
                    "roll",
                    ["Rolls (1-10) damage", "Historic"],
                    ["Rolls <number> damage", "Historic"],
                    ["modifier:roll"],
                    ["stat:roll"])));

        var resolution = resolver.Resolve(parsed, catalog);

        Assert.Empty(resolution.CompatibleVersions);
        var block = Assert.Single(resolution.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_AidUnscalable_TwoMechanicallyCompatibleCandidates_FailsClosed()
    {
        const string semantic = "Selected variant";
        var parsed = parser.Parse(Clipboard(
            $"{semantic}(Alpha-Omega)",
            "Historic — Unscalable Value"));
        var catalog = CreateCatalog(
            Version(
                "First",
                UniqueItemVersionRole.Current,
                FixedBlock(
                    "first",
                    [semantic, "Historic"],
                    [semantic, "Historic"],
                    ["modifier:first"],
                    ["stat:first"])),
            Version(
                "Second",
                UniqueItemVersionRole.Current,
                FixedBlock(
                    "second",
                    [semantic, "Historic"],
                    [semantic, "Historic"],
                    ["modifier:second"],
                    ["stat:second"])));

        var resolution = resolver.Resolve(parsed, catalog);

        Assert.Equal(2, resolution.CompatibleVersions.Count);
        var block = Assert.Single(resolution.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_INDEPENDENT_DIMENSIONS", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_AidUnscalable_OnGeneratedCandidate_DoesNotBecomeFixedExactBypass()
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
        var version = Version(
            "Generated",
            UniqueItemVersionRole.Current,
            GeneratedBlock(
                "first",
                "Selected modifier",
                "Selected modifier",
                "first_stat",
                "pool:first"),
            GeneratedBlock(
                "second",
                "Selected modifier",
                "Selected modifier",
                "second_stat",
                "pool:second")) with
        {
            GeneratedCandidateSelectionLimit = 2,
        };

        var result = resolver.Resolve(
            parsed,
            CreateCatalog("Test Crown", "Great Crown", version));

        var block = Assert.Single(result.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_GENERATED_TEXTUAL_OPTION_RANGE_AMBIGUOUS", block.DiagnosticCode);
    }

    [Fact]
    public void Resolve_NoAidUnscalable_BaselineExactUnchanged()
    {
        var parsed = parser.Parse(Clipboard("Selected variant(Alpha-Omega)", "Historic"));
        var resolution = resolver.Resolve(parsed, CreateSingleVariantCatalog());

        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, resolution.Status);
        Assert.True(Assert.Single(resolution.ModifierBlocks).IsResolved);
        Assert.DoesNotContain(
            parsed.Modifiers.SelectMany(m => m.Effects),
            e => e.HasUnscalableValue);
    }

    [Fact]
    public void Resolve_AidUnscalable_HistoricalOnlyMechanicallyIncompatible_FailsClosed()
    {
        var parsed = parser.Parse(Clipboard(
            "Selected variant(Alpha-Omega)",
            "Historic — Unscalable Value"));
        var catalog = CreateCatalog(
            Version(
                "HistoricalOnly",
                UniqueItemVersionRole.Historical,
                FixedBlock(
                    "hist",
                    ["Completely different line", "Historic"],
                    ["Completely different line", "Historic"],
                    ["modifier:hist"],
                    ["stat:hist"])));

        var resolution = resolver.Resolve(parsed, catalog);

        Assert.Empty(resolution.CompatibleVersions);
        var block = Assert.Single(resolution.ModifierBlocks);
        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", block.DiagnosticCode);
    }

    [Fact]
    public async Task Resolve_LethalPrideReplayShape_WithUnscalable_IsExactSearchable()
    {
        var catalog = await LoadActiveCatalogAsync();
        var parsed = parser.Parse("""
            Item Class: Jewels
            Rarity: Unique
            Lethal Pride
            Timeless Jewel
            --------
            Item Level: 84
            --------
            { Unique Modifier }
            Commanded leadership over 14245(10000-18000) warriors under Rakiata(Akoya-Rakiata)
            Passives in radius are Conquered by the Karui
            (Conquered Passive Skills cannot be modified by other Jewels)
            Historic — Unscalable Value
            """);

        Assert.Contains(
            parsed.Modifiers.SelectMany(m => m.Effects),
            e => e.HasUnscalableValue && e.Text == "Historic");

        var resolution = resolver.Resolve(parsed, catalog);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, resolution.Status);
        Assert.Null(resolution.DiagnosticCode);
        var seed = Assert.Single(resolution.ModifierBlocks);
        Assert.True(seed.IsResolved);
        Assert.Null(seed.DiagnosticCode);
        Assert.NotEmpty(seed.ModifierIds);
        Assert.NotEmpty(seed.StatIds);

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        var row = Assert.Single(draft.ModifierFilters);
        Assert.True(row.IsSearchable);
        Assert.True(row.HasExactUniqueSourceProvenance);
        Assert.Null(row.UniqueResolutionDiagnosticCode);
        Assert.Contains(
            parsed.Modifiers.SelectMany(m => m.Effects),
            e => e.HasUnscalableValue);
    }

    private static GameDataCatalog CreateSingleVariantCatalog() => CreateCatalog(
        "Test Jewel",
        "Crimson Jewel",
        Version(
            "Alpha",
            UniqueItemVersionRole.Current,
            FixedBlock(
                "variant",
                ["Selected variant", "Historic"],
                ["Selected variant", "Historic"],
                ["modifier:variant"],
                ["stat:variant"])));

    private static string Clipboard(string firstLine, string secondLine) => $$"""
        Item Class: Jewels
        Rarity: Unique
        Test Jewel
        Crimson Jewel
        --------
        Item Level: 80
        --------
        { Unique Modifier }
        {{firstLine}}
        {{secondLine}}
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

    private static GameDataCatalog CreateCatalog(params UniqueItemVersionObservation[] versions) =>
        CreateCatalog("Test Jewel", "Crimson Jewel", versions);

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
                        Versions = versions.Select(version => version with
                        {
                            BaseType = baseType,
                        }).ToArray(),
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

    private static UniqueModifierBlock FixedBlock(
        string id,
        IReadOnlyList<string> lines,
        IReadOnlyList<string> signatures,
        IReadOnlyList<string> modifierIds,
        IReadOnlyList<string> statIds) => new()
    {
        Id = $"block:{id}",
        Kind = UniqueModifierBlockKind.Unique,
        Lines = lines,
        CanonicalSignatures = signatures,
        SourceSemantics = UniqueModifierSourceSemantics.Fixed,
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Exact,
            ModifierIds = modifierIds,
            StatIds = statIds,
        },
        SourceObservationIds = ["pob-observation:test"],
    };

    private static UniqueModifierBlock GeneratedBlock(
        string id,
        string line,
        string signature,
        string statId,
        string poolMembershipId) => FixedBlock(
            id,
            [line],
            [signature],
            [$"modifier:{id}"],
            [statId]) with
    {
        SourceSemantics = UniqueModifierSourceSemantics.GeneratedCandidate,
        CandidatePoolMembershipIds = [poolMembershipId],
        SourceObservationIds = ["generated-observation:test"],
    };
}
