using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class UniqueHistoricalEncodingAggregationTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedUniqueItemResolver resolver = new();

    private const string LeechLine = "1% of Physical Attack Damage Leeched as Life";
    private const string CurrentPermyriadStat = "local_life_leech_from_physical_damage_permyriad";
    private const string DeprecatedPercentStat = "old_local_life_leech_from_physical_damage_percent";
    private const string OtherModernStat = "local_mana_leech_from_physical_damage_permyriad";
    private const string CurrentSourceMechanicsStat =
        "local_unique_flask_life_leech_from_chaos_damage_permyriad_while_healing";
    private const string DeprecatedSourceMechanicsStat =
        "old_do_not_use_local_unique_flask_life_leech_from_chaos_damage_permyriad_while_healing";
    private const string OtherModernSourceMechanicsStat =
        "base_life_leech_from_elemental_damage_permyriad";

    [Fact]
    public void Resolve_CurrentExact_PlusCompatibleHistoricalPermyriadConflict_PreservesCurrentVector()
    {
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.Exact,
                ["modifier:current"],
                [CurrentPermyriadStat]),
            HistoricalConflictBlock(
                "historical-leech",
                CurrentPermyriadStat,
                DeprecatedPercentStat));

        var block = ResolveLeech(catalog);

        Assert.True(block.IsResolved, block.Diagnostic);
        Assert.Null(block.DiagnosticCode);
        Assert.Equal([CurrentPermyriadStat], block.StatIds);
        Assert.Equal(["modifier:current"], block.ModifierIds);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            block.AggregationDiagnosticCode);
        Assert.NotNull(block.NonBlockingHistoricalConflictEvidence);
        Assert.Equal(
            UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent,
            block.NonBlockingHistoricalConflictEvidence!.Kind);
        Assert.DoesNotContain(
            block.ModifierIds,
            id => id.Contains("deprecated", StringComparison.OrdinalIgnoreCase));
        Assert.All(
            block.CatalogBlocks,
            catalogBlock => Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact,
                catalogBlock.MechanicalMapping.Status));
    }

    [Fact]
    public void Resolve_CurrentEquivalentSourceSet_PlusCompatibleHistoricalConflict_PreservesEquivalentProvenance()
    {
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
                ["modifier:current-a", "modifier:current-b"],
                [CurrentPermyriadStat]),
            HistoricalConflictBlock(
                "historical-leech",
                CurrentPermyriadStat,
                DeprecatedPercentStat));

        var block = ResolveLeech(catalog);

        Assert.True(block.IsResolved, block.Diagnostic);
        Assert.True(block.IsEquivalentSourceSet);
        Assert.Equal([CurrentPermyriadStat], block.StatIds);
        Assert.Equal(["modifier:current-a", "modifier:current-b"], block.ModifierIds);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            block.AggregationDiagnosticCode);
        Assert.DoesNotContain(block.ModifierIds, id => id.Contains("old", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_MultipleCompatibleHistoricalEncodingConflicts_RemainNonBlocking()
    {
        var catalog = CreateCatalog(
            "Test Hymn",
            "Sledgehammer",
            UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                CurrentResolvedBlock(
                    "current-leech",
                    UniqueModifierMechanicalMappingStatus.Exact,
                    ["modifier:current"],
                    [CurrentPermyriadStat])),
            Version("Pre 2.6.0", UniqueItemVersionRole.Historical,
                HistoricalConflictBlock(
                    "historical-leech-a",
                    CurrentPermyriadStat,
                    DeprecatedPercentStat)),
            Version("Pre 2.0.0", UniqueItemVersionRole.Historical,
                HistoricalConflictBlock(
                    "historical-leech-b",
                    CurrentPermyriadStat,
                    DeprecatedPercentStat,
                    deprecatedModifierId: "modifier:deprecated-b")));

        var block = ResolveLeech(catalog);

        Assert.True(block.IsResolved, block.Diagnostic);
        Assert.Equal([CurrentPermyriadStat], block.StatIds);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            block.AggregationDiagnosticCode);
        Assert.NotNull(block.NonBlockingHistoricalConflictEvidence);
    }

    [Fact]
    public void Resolve_CurrentPlusHistoricalLevelVsChance_RemainsFailClosed()
    {
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.Exact,
                ["modifier:current"],
                [CurrentPermyriadStat]),
            HistoricalConflictBlockWithKind(
                "historical-leech",
                UniqueMechanicalConflictKind.LevelVsChanceOnHit,
                Candidate("modifier:level", ["grant_level_x"], ["level"]),
                Candidate("modifier:chance", ["chance_to_gain"], ["chance"])));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
        Assert.Empty(block.StatIds);
    }

    [Fact]
    public void Resolve_CurrentPlusHistoricalInverseLegacy_RemainsFailClosed()
    {
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.Exact,
                ["modifier:current"],
                [CurrentPermyriadStat]),
            HistoricalConflictBlockWithKind(
                "historical-leech",
                UniqueMechanicalConflictKind.InverseLegacyHandlerEncoding,
                Candidate("modifier:plus", ["mana_reservation_efficiency_+%"], ["reservation", "efficiency-plus"]),
                Candidate(
                    "modifier:inverse",
                    ["base_mana_reservation_efficiency_-100%_final"],
                    ["reservation", "efficiency-inverse", "handler-negate", "handler-legacy"])));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
    }

    [Fact]
    public void Resolve_CurrentPlusHistoricalSameDisplayText_RemainsFailClosed()
    {
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.Exact,
                ["modifier:current"],
                [CurrentPermyriadStat]),
            HistoricalConflictBlockWithKind(
                "historical-leech",
                UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIds,
                Candidate("modifier:a", ["stat_a"], []),
                Candidate("modifier:b", ["stat_b"], [])));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
    }

    [Fact]
    public void Resolve_CurrentExact_PlusCompatibleHistoricalSourceMechanicsConflict_PreservesCurrentVector()
    {
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.Exact,
                ["modifier:current"],
                [CurrentSourceMechanicsStat]),
            HistoricalSourceMechanicsConflictBlock(
                "historical-leech",
                CurrentSourceMechanicsStat,
                DeprecatedSourceMechanicsStat));

        var block = ResolveLeech(catalog);

        Assert.True(block.IsResolved, block.Diagnostic);
        Assert.Null(block.DiagnosticCode);
        Assert.Equal([CurrentSourceMechanicsStat], block.StatIds);
        Assert.Equal(["modifier:current"], block.ModifierIds);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            block.AggregationDiagnosticCode);
        Assert.NotNull(block.NonBlockingHistoricalConflictEvidence);
        Assert.Equal(
            UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics,
            block.NonBlockingHistoricalConflictEvidence!.Kind);
        Assert.DoesNotContain(
            block.ModifierIds,
            id => id.Contains("deprecated", StringComparison.OrdinalIgnoreCase));
        Assert.All(
            block.CatalogBlocks,
            catalogBlock => Assert.Equal(
                UniqueModifierMechanicalMappingStatus.Exact,
                catalogBlock.MechanicalMapping.Status));
    }

    [Fact]
    public void Resolve_CurrentEquivalentSourceSet_PlusCompatibleHistoricalSourceMechanics_PreservesEquivalentProvenance()
    {
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
                ["modifier:current-a", "modifier:current-b"],
                [CurrentSourceMechanicsStat]),
            HistoricalSourceMechanicsConflictBlock(
                "historical-leech",
                CurrentSourceMechanicsStat,
                DeprecatedSourceMechanicsStat));

        var block = ResolveLeech(catalog);

        Assert.True(block.IsResolved, block.Diagnostic);
        Assert.True(block.IsEquivalentSourceSet);
        Assert.Equal([CurrentSourceMechanicsStat], block.StatIds);
        Assert.Equal(["modifier:current-a", "modifier:current-b"], block.ModifierIds);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            block.AggregationDiagnosticCode);
        Assert.DoesNotContain(
            block.ModifierIds,
            id => id.Contains("old_do_not_use", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_HistoricalSourceMechanicsMissingCurrentVector_RemainsFailClosed()
    {
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.Exact,
                ["modifier:current"],
                [CurrentSourceMechanicsStat]),
            HistoricalSourceMechanicsConflictBlock(
                "historical-leech",
                OtherModernSourceMechanicsStat,
                DeprecatedSourceMechanicsStat));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
        Assert.Empty(block.StatIds);
    }

    [Fact]
    public void Resolve_HistoricalSourceMechanicsWithExtraModernVector_RemainsFailClosed()
    {
        var conflict = new UniqueMechanicalConflictEvidence
        {
            Kind = UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics,
            Candidates =
            [
                Candidate(
                    "modifier:current",
                    [CurrentSourceMechanicsStat],
                    ["permyriad"],
                    UniqueModifierSemanticLocality.Global),
                Candidate(
                    "modifier:other-modern",
                    [OtherModernSourceMechanicsStat],
                    ["permyriad"],
                    UniqueModifierSemanticLocality.Global),
                Candidate(
                    "modifier:deprecated",
                    [DeprecatedSourceMechanicsStat],
                    ["permyriad", "deprecated-name", "handler-legacy"],
                    UniqueModifierSemanticLocality.Global),
            ],
        };
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.Exact,
                ["modifier:current"],
                [CurrentSourceMechanicsStat]),
            HistoricalConflictBlockWithEvidence("historical-leech", conflict));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
    }

    [Fact]
    public void Resolve_HistoricalSourceMechanicsIncompatibleLocality_RemainsFailClosed()
    {
        var conflict = new UniqueMechanicalConflictEvidence
        {
            Kind = UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics,
            Candidates =
            [
                Candidate(
                    "modifier:current-local",
                    [CurrentSourceMechanicsStat],
                    ["permyriad"],
                    UniqueModifierSemanticLocality.Local),
                Candidate(
                    "modifier:current-global",
                    [CurrentSourceMechanicsStat],
                    ["permyriad"],
                    UniqueModifierSemanticLocality.Global),
                Candidate(
                    "modifier:deprecated",
                    [DeprecatedSourceMechanicsStat],
                    ["permyriad", "deprecated-name", "handler-legacy"],
                    UniqueModifierSemanticLocality.Local),
            ],
        };
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.Exact,
                ["modifier:current"],
                [CurrentSourceMechanicsStat]),
            HistoricalConflictBlockWithEvidence("historical-leech", conflict));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
    }

    [Fact]
    public void Resolve_HistoricalSourceMechanicsSignatureMismatch_RemainsFailClosed()
    {
        var catalog = CreateCatalog(
            "Test Hymn",
            "Sledgehammer",
            UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                CurrentResolvedBlock(
                    "current-other",
                    UniqueModifierMechanicalMappingStatus.Exact,
                    ["modifier:other"],
                    ["local_physical_damage_+%"],
                    line: "50% increased Physical Damage",
                    signature: "50% increased Physical Damage")),
            Version("Pre 2.6.0", UniqueItemVersionRole.Historical,
                HistoricalSourceMechanicsConflictBlock(
                    "historical-leech",
                    CurrentSourceMechanicsStat,
                    DeprecatedSourceMechanicsStat)));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
        Assert.All(
            Assert.IsType<UniqueItemResolutionResult>(
                resolver.Resolve(ParseLeechItem(), catalog)).CompatibleVersions,
            version => Assert.Equal(UniqueItemVersionRole.Historical, version.Role));
    }

    [Fact]
    public void Resolve_HistoricalOnlySourceMechanicsConflict_RemainsFailClosed()
    {
        var catalog = CreateCatalog(
            "Test Hymn",
            "Sledgehammer",
            UniqueItemKind.Ordinary,
            Version("Pre 2.6.0", UniqueItemVersionRole.Historical,
                HistoricalSourceMechanicsConflictBlock(
                    "historical-leech",
                    CurrentSourceMechanicsStat,
                    DeprecatedSourceMechanicsStat)));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
        Assert.NotNull(block.ConflictEvidence);
        Assert.Empty(block.StatIds);
    }

    [Fact]
    public void Resolve_TwoConflictingCurrentResolvedBlocks_WithSourceMechanicsHistorical_RemainsFailClosed()
    {
        var catalog = CreateCatalog(
            "Test Hymn",
            "Sledgehammer",
            UniqueItemKind.Ordinary,
            Version("Current A", UniqueItemVersionRole.Current,
                CurrentResolvedBlock(
                    "current-leech-a",
                    UniqueModifierMechanicalMappingStatus.Exact,
                    ["modifier:current-a"],
                    [CurrentSourceMechanicsStat])),
            Version("Current B", UniqueItemVersionRole.Current,
                CurrentResolvedBlock(
                    "current-leech-b",
                    UniqueModifierMechanicalMappingStatus.Exact,
                    ["modifier:current-b"],
                    [OtherModernSourceMechanicsStat])),
            Version("Pre 2.6.0", UniqueItemVersionRole.Historical,
                HistoricalSourceMechanicsConflictBlock(
                    "historical-leech",
                    CurrentSourceMechanicsStat,
                    DeprecatedSourceMechanicsStat)));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Null(block.AggregationDiagnosticCode);
        Assert.Empty(block.StatIds);
    }

    [Fact]
    public void Resolve_HistoricalConflictMissingCurrentVector_RemainsFailClosed()
    {
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.Exact,
                ["modifier:current"],
                [CurrentPermyriadStat]),
            HistoricalConflictBlock(
                "historical-leech",
                OtherModernStat,
                DeprecatedPercentStat));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
        Assert.Empty(block.StatIds);
    }

    [Fact]
    public void Resolve_HistoricalConflictWithExtraNonDeprecatedModernVector_RemainsFailClosed()
    {
        var conflict = new UniqueMechanicalConflictEvidence
        {
            Kind = UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent,
            Candidates =
            [
                Candidate("modifier:current", [CurrentPermyriadStat], ["permyriad"]),
                Candidate("modifier:other-modern", [OtherModernStat], ["permyriad"]),
                Candidate(
                    "modifier:deprecated",
                    [DeprecatedPercentStat],
                    ["percent", "deprecated-name", "handler-legacy"]),
            ],
        };
        var catalog = CreateLeechCatalog(
            CurrentResolvedBlock(
                "current-leech",
                UniqueModifierMechanicalMappingStatus.Exact,
                ["modifier:current"],
                [CurrentPermyriadStat]),
            HistoricalConflictBlockWithEvidence("historical-leech", conflict));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
    }

    [Fact]
    public void Resolve_TwoConflictingCurrentResolvedBlocks_RemainsFailClosed()
    {
        var catalog = CreateCatalog(
            "Test Hymn",
            "Sledgehammer",
            UniqueItemKind.Ordinary,
            Version("Current A", UniqueItemVersionRole.Current,
                CurrentResolvedBlock(
                    "current-leech-a",
                    UniqueModifierMechanicalMappingStatus.Exact,
                    ["modifier:current-a"],
                    [CurrentPermyriadStat])),
            Version("Current B", UniqueItemVersionRole.Current,
                CurrentResolvedBlock(
                    "current-leech-b",
                    UniqueModifierMechanicalMappingStatus.Exact,
                    ["modifier:current-b"],
                    [OtherModernStat])),
            Version("Pre 2.6.0", UniqueItemVersionRole.Historical,
                HistoricalConflictBlock(
                    "historical-leech",
                    CurrentPermyriadStat,
                    DeprecatedPercentStat)));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Null(block.AggregationDiagnosticCode);
        Assert.Empty(block.StatIds);
    }

    [Fact]
    public void Resolve_HistoricalOnlyEncodingConflict_RemainsFailClosed()
    {
        var catalog = CreateCatalog(
            "Test Hymn",
            "Sledgehammer",
            UniqueItemKind.Ordinary,
            Version("Pre 2.6.0", UniqueItemVersionRole.Historical,
                HistoricalConflictBlock(
                    "historical-leech",
                    CurrentPermyriadStat,
                    DeprecatedPercentStat)));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
        Assert.True(block.ConflictEvidence is not null);
        Assert.Empty(block.StatIds);
    }

    [Fact]
    public void Resolve_HistoricalPinnedWhenCurrentLacksLine_DoesNotBypassHistoricalConflict()
    {
        var catalog = CreateCatalog(
            "Test Hymn",
            "Sledgehammer",
            UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                CurrentResolvedBlock(
                    "current-other",
                    UniqueModifierMechanicalMappingStatus.Exact,
                    ["modifier:other"],
                    ["local_physical_damage_+%"],
                    line: "50% increased Physical Damage",
                    signature: "50% increased Physical Damage")),
            Version("Pre 2.6.0", UniqueItemVersionRole.Historical,
                HistoricalConflictBlock(
                    "historical-leech",
                    CurrentPermyriadStat,
                    DeprecatedPercentStat)));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
        Assert.All(
            Assert.IsType<UniqueItemResolutionResult>(
                resolver.Resolve(ParseLeechItem(), catalog)).CompatibleVersions,
            version => Assert.Equal(UniqueItemVersionRole.Historical, version.Role));
    }

    [Fact]
    public void Resolve_CurrentExact_PlusHistoricalBroadMechanicsConflict_PreservesCurrentVector()
    {
        var catalog = CreateCatalog(
            "Test Hymn",
            "Sledgehammer",
            UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current,
                CurrentResolvedBlock(
                    "current-leech",
                    UniqueModifierMechanicalMappingStatus.Exact,
                    ["modifier:current"],
                    [CurrentPermyriadStat])),
            Version("Pre 2.6.0", UniqueItemVersionRole.Historical,
                new UniqueModifierBlock
                {
                    Id = "block:historical-broad",
                    Kind = UniqueModifierBlockKind.Unique,
                    Lines = [LeechLine],
                    CanonicalSignatures = [LeechLine],
                    MechanicalMapping = new UniqueModifierMechanicalMapping
                    {
                        Status = UniqueModifierMechanicalMappingStatus.Ambiguous,
                        ModifierIds =
                        [
                            "modifier:historical-a",
                            "modifier:historical-b",
                            "modifier:current",
                        ],
                        StatIds = [],
                        DiagnosticCode = "UNIQUE_MECHANICS_CONFLICT",
                        Diagnostic = "The PoB Unique line matched conflicting RePoE mechanical stat vectors.",
                    },
                    SourceObservationIds = ["pob-observation:historical-broad"],
                }));

        var block = ResolveLeech(catalog);

        Assert.True(block.IsResolved, block.Diagnostic);
        Assert.Null(block.DiagnosticCode);
        Assert.Equal([CurrentPermyriadStat], block.StatIds);
        Assert.Equal(["modifier:current"], block.ModifierIds);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalMechanicsConflictDidNotOverrideCurrentProof,
            block.AggregationDiagnosticCode);
        Assert.Null(block.NonBlockingHistoricalConflictEvidence);
        Assert.DoesNotContain(block.ModifierIds, id => id.Contains("historical", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Resolve_HistoricalOnlyBroadMechanicsConflict_RemainsFailClosed()
    {
        var catalog = CreateCatalog(
            "Test Hymn",
            "Sledgehammer",
            UniqueItemKind.Ordinary,
            Version("Pre 2.6.0", UniqueItemVersionRole.Historical,
                new UniqueModifierBlock
                {
                    Id = "block:historical-broad",
                    Kind = UniqueModifierBlockKind.Unique,
                    Lines = [LeechLine],
                    CanonicalSignatures = [LeechLine],
                    MechanicalMapping = new UniqueModifierMechanicalMapping
                    {
                        Status = UniqueModifierMechanicalMappingStatus.Ambiguous,
                        ModifierIds = ["modifier:historical-a", "modifier:historical-b"],
                        StatIds = [],
                        DiagnosticCode = "UNIQUE_MECHANICS_CONFLICT",
                        Diagnostic = "The PoB Unique line matched conflicting RePoE mechanical stat vectors.",
                    },
                    SourceObservationIds = ["pob-observation:historical-broad"],
                }));

        var block = ResolveLeech(catalog);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_CONFLICT", block.DiagnosticCode);
        Assert.Null(block.AggregationDiagnosticCode);
        Assert.Empty(block.StatIds);
    }

    [Fact]
    public void Resolve_CurrentAmbiguousMechanicsConflict_NarrowsByUniqueGenerationAndDomain()
    {
        var line = "Freezes you inflict spread to other Enemies within 1.5 metres";
        var catalog = GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 2,
                DataVersion = "test",
                CreatedAtUtc = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero),
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "path-of-building",
                        RetrievedAtUtc = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero),
                    },
                ],
            },
            ItemBases =
            [
                new ItemBaseRecord { Id = "Metadata/Items/Amulets/Amulet2", Name = "Jade Amulet", ItemClass = "Amulets", Domain = "item" },
            ],
            Modifiers =
            [
                new ModifierDefinition
                {
                    Id = "FreezeProliferationUnique__1",
                    GroupId = "freeze-unique",
                    Name = "FreezeProliferationUnique__1",
                    GenerationType = ModifierGenerationType.Implicit,
                    SourceGenerationType = "unique",
                    Domain = "item",
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "amulet_freeze_proliferation_radius",
                            MinValue = 15,
                            MaxValue = 15,
                        },
                    ],
                },
                new ModifierDefinition
                {
                    Id = "FreezeProliferationEldritchImplicit1",
                    GroupId = "freeze-eldritch",
                    Name = "FreezeProliferationEldritchImplicit1",
                    GenerationType = ModifierGenerationType.Implicit,
                    SourceGenerationType = "searing_exarch_implicit",
                    Domain = "item",
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "gloves_freeze_proliferation_radius",
                            MinValue = 12,
                            MaxValue = 12,
                        },
                    ],
                },
                new ModifierDefinition
                {
                    Id = "SanctumSpecialFreezeProliferation",
                    GroupId = "freeze-sanctum",
                    Name = "SanctumSpecialFreezeProliferation",
                    GenerationType = ModifierGenerationType.Prefix,
                    SourceGenerationType = "prefix",
                    Domain = "sanctum_relic",
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "gloves_freeze_proliferation_radius",
                            MinValue = 16,
                            MaxValue = 16,
                        },
                    ],
                },
            ],
            Stats =
            [
                new StatDefinition { Id = "amulet_freeze_proliferation_radius" },
                new StatDefinition { Id = "gloves_freeze_proliferation_radius" },
            ],
            UniqueItems = new UniqueItemCatalog
            {
                SourceObservations =
                [
                    new UniqueCatalogSourceObservation
                    {
                        Id = "pob-observation:halcyon",
                        ManifestSourceId = "path-of-building",
                        RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                        Tag = "v2.67.2",
                        CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                        SourcePath = "Data/Uniques/test.lua",
                        ObservedKind = UniqueItemKind.Ordinary,
                        RawEntrySha256 = new string('b', 64),
                    },
                ],
                Items =
                [
                    new UniqueItemIdentity
                    {
                        Id = "unique:the-halcyon",
                        CanonicalName = "The Halcyon",
                        Kind = UniqueItemKind.Ordinary,
                        BaseTypeEvidence = ["Jade Amulet"],
                        SourceObservationIds = ["pob-observation:halcyon"],
                        Versions =
                        [
                            new UniqueItemVersionObservation
                            {
                                Id = "version:current",
                                Label = "Current",
                                Role = UniqueItemVersionRole.Current,
                                BaseType = "Jade Amulet",
                                SourceObservationIds = ["pob-observation:halcyon"],
                                ModifierBlocks =
                                [
                                    new UniqueModifierBlock
                                    {
                                        Id = "block:freeze",
                                        Kind = UniqueModifierBlockKind.Unique,
                                        Lines = [line],
                                        CanonicalSignatures = [line],
                                        MechanicalMapping = new UniqueModifierMechanicalMapping
                                        {
                                            Status = UniqueModifierMechanicalMappingStatus.Ambiguous,
                                            ModifierIds =
                                            [
                                                "FreezeProliferationEldritchImplicit1",
                                                "FreezeProliferationUnique__1",
                                                "SanctumSpecialFreezeProliferation",
                                            ],
                                            StatIds = [],
                                            DiagnosticCode = "UNIQUE_MECHANICS_CONFLICT",
                                            Diagnostic =
                                                "The PoB Unique line matched conflicting RePoE mechanical stat vectors.",
                                        },
                                        SourceObservationIds = ["pob-observation:halcyon"],
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        });

        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            The Halcyon
            Jade Amulet
            --------
            Requires Level 64
            --------
            Item Level: 70
            --------
            { Unique Modifier }
            Freezes you inflict spread to other Enemies within 1.5 metres
            """);
        var unique = resolver.Resolve(parsed, catalog);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);
        Assert.Contains(parsed.Modifiers, modifier => modifier.Kind == ParsedModifierKind.Unique);
        var block = Assert.Single(unique.ModifierBlocks);

        Assert.True(block.IsResolved, block.Diagnostic);
        Assert.Null(block.DiagnosticCode);
        Assert.Equal(["FreezeProliferationUnique__1"], block.ModifierIds);
        Assert.Equal(["amulet_freeze_proliferation_radius"], block.StatIds);
    }

    [Fact]
    public void Resolve_TwoDisagreeingUniqueGenerationCandidates_RemainFailClosed()
    {
        var line = "10% chance to gain Onslaught for 10 seconds on Kill";
        var catalog = GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 2,
                DataVersion = "test",
                CreatedAtUtc = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero),
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "path-of-building",
                        RetrievedAtUtc = new DateTimeOffset(2026, 9, 25, 0, 0, 0, TimeSpan.Zero),
                    },
                ],
            },
            ItemBases =
            [
                new ItemBaseRecord { Id = "Metadata/Items/Amulets/Amulet4", Name = "Agate Amulet", ItemClass = "Amulets", Domain = "item" },
            ],
            Modifiers =
            [
                new ModifierDefinition
                {
                    Id = "unique.onslaught.a",
                    GroupId = "onslaught-a",
                    GenerationType = ModifierGenerationType.Implicit,
                    SourceGenerationType = "unique",
                    Domain = "item",
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "chance_to_gain_onslaught_on_kill_for_10_seconds_%",
                            MinValue = 10,
                            MaxValue = 10,
                        },
                    ],
                },
                new ModifierDefinition
                {
                    Id = "unique.onslaught.b",
                    GroupId = "onslaught-b",
                    GenerationType = ModifierGenerationType.Implicit,
                    SourceGenerationType = "unique",
                    Domain = "item",
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "chance_to_gain_onslaught_on_kill_for_4_seconds_%",
                            MinValue = 10,
                            MaxValue = 10,
                        },
                    ],
                },
            ],
            Stats =
            [
                new StatDefinition { Id = "chance_to_gain_onslaught_on_kill_for_10_seconds_%" },
                new StatDefinition { Id = "chance_to_gain_onslaught_on_kill_for_4_seconds_%" },
            ],
            UniqueItems = new UniqueItemCatalog
            {
                SourceObservations =
                [
                    new UniqueCatalogSourceObservation
                    {
                        Id = "pob-observation:onslaught",
                        ManifestSourceId = "path-of-building",
                        RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
                        Tag = "v2.67.2",
                        CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
                        SourcePath = "Data/Uniques/test.lua",
                        ObservedKind = UniqueItemKind.Ordinary,
                        RawEntrySha256 = new string('c', 64),
                    },
                ],
                Items =
                [
                    new UniqueItemIdentity
                    {
                        Id = "unique:extractor",
                        CanonicalName = "Extractor Mentis",
                        Kind = UniqueItemKind.Ordinary,
                        BaseTypeEvidence = ["Agate Amulet"],
                        SourceObservationIds = ["pob-observation:onslaught"],
                        Versions =
                        [
                            new UniqueItemVersionObservation
                            {
                                Id = "version:current",
                                Label = "Current",
                                Role = UniqueItemVersionRole.Current,
                                BaseType = "Agate Amulet",
                                SourceObservationIds = ["pob-observation:onslaught"],
                                ModifierBlocks =
                                [
                                    new UniqueModifierBlock
                                    {
                                        Id = "block:onslaught",
                                        Kind = UniqueModifierBlockKind.Unique,
                                        Lines = [line],
                                        CanonicalSignatures = [line],
                                        MechanicalMapping = new UniqueModifierMechanicalMapping
                                        {
                                            Status = UniqueModifierMechanicalMappingStatus.Ambiguous,
                                            ModifierIds = ["unique.onslaught.a", "unique.onslaught.b"],
                                            StatIds = [],
                                            DiagnosticCode = "UNIQUE_MECHANICS_CONFLICT",
                                        },
                                        SourceObservationIds = ["pob-observation:onslaught"],
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
        });

        var parsed = parser.Parse("""
            Item Class: Amulets
            Rarity: Unique
            Extractor Mentis
            Agate Amulet
            --------
            Requires Level 16
            --------
            Item Level: 70
            --------
            { Unique Modifier }
            10% chance to gain Onslaught for 10 seconds on Kill
            """);
        var unique = resolver.Resolve(parsed, catalog);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);
        Assert.Contains(parsed.Modifiers, modifier => modifier.Kind == ParsedModifierKind.Unique);
        var block = Assert.Single(unique.ModifierBlocks);

        Assert.False(block.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_CONFLICT", block.DiagnosticCode);
        Assert.Empty(block.StatIds);
    }

    [Fact]
    public async Task ActivePackage_HrimnorLeech_PreservesCurrentPermyriadAcrossHistoricalConflict()
    {
        var package = await LoadActivePackageAsync();
        var catalog = GameDataCatalog.FromPackage(package);
        var parsed = parser.Parse("""
            Item Class: Two Hand Maces
            Rarity: Unique
            Hrimnor's Hymn
            Sledgehammer
            --------
            Item Level: 70
            --------
            { Unique Modifier }
            1% of Physical Attack Damage Leeched as Life
            """);
        var unique = resolver.Resolve(parsed, catalog);
        var leech = Assert.Single(
            unique.ModifierBlocks,
            block => block.CatalogBlocks.Count > 0 ||
                block.StatIds.Contains(CurrentPermyriadStat) ||
                block.DiagnosticCode == "UNIQUE_MECHANICS_EXACT_CONFLICT" ||
                block.AggregationDiagnosticCode is not null);

        Assert.True(leech.IsResolved, leech.Diagnostic);
        Assert.Null(leech.DiagnosticCode);
        Assert.Equal([CurrentPermyriadStat], leech.StatIds);
        Assert.True(leech.IsEquivalentSourceSet);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            leech.AggregationDiagnosticCode);
        Assert.NotNull(leech.NonBlockingHistoricalConflictEvidence);
        Assert.Equal(
            UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent,
            leech.NonBlockingHistoricalConflictEvidence!.Kind);
        Assert.DoesNotContain(
            leech.ModifierIds,
            id => id.Contains("LifeLeechUnique", StringComparison.Ordinal) &&
                !id.Contains("Permyriad", StringComparison.Ordinal));

        var draft = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog);
        var filter = Assert.Single(
            Assert.IsType<TradeSearchDraft>(draft.Draft).ModifierFilters,
            component => component.RawCopiedText.Contains("Leeched as Life", StringComparison.Ordinal));
        Assert.Equal([CurrentPermyriadStat], filter.ResolvedStatIds);
        Assert.Null(filter.UniqueResolutionDiagnosticCode);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            filter.UniqueAggregationDiagnosticCode);
        Assert.True(filter.HasExactUniqueSourceProvenance);
    }

    [Fact]
    public async Task ActivePackage_CircleOfFearReservation_CurrentResolvesWithoutChangingHistoricalConflict()
    {
        var package = await LoadActivePackageAsync();
        var item = Assert.Single(
            package.UniqueItems!.Items,
            candidate => string.Equals(
                candidate.CanonicalName,
                "Circle of Fear",
                StringComparison.OrdinalIgnoreCase));
        var current = Assert.Single(
            item.Versions,
            version => version.Role == UniqueItemVersionRole.Current);
        var currentReservation = Assert.Single(
            current.ModifierBlocks,
            block => block.Lines.Any(line =>
                line.Contains("increased Mana Reservation Efficiency", StringComparison.Ordinal)));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, currentReservation.MechanicalMapping.Status);
        Assert.Null(currentReservation.MechanicalMapping.ConflictEvidence);
        Assert.Equal(
            ["herald_of_ice_mana_reservation_efficiency_+%"],
            currentReservation.MechanicalMapping.StatIds);
        Assert.Contains(
            "current-role-inverse-legacy-encoding-filter",
            currentReservation.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.DoesNotContain(
            currentReservation.MechanicalMapping.StatIds,
            statId => UniqueMechanicalConflictClassifier.BuildEncodingMarkers("x", [statId], [])
                .Contains(UniqueMechanicalConflictClassifier.MarkerEfficiencyInverse));

        Assert.All(
            item.Versions.Where(version => version.Role == UniqueItemVersionRole.Historical),
            version =>
            {
                Assert.DoesNotContain(
                    version.ModifierBlocks,
                    block => block.MechanicalMapping.Provenance?.ResolutionReasons.Contains(
                        "current-role-inverse-legacy-encoding-filter",
                        StringComparer.Ordinal) == true);
                var historicalReservation = Assert.Single(
                    version.ModifierBlocks,
                    block => block.Lines.Any(line =>
                        line.Contains("Mana Reservation Efficiency", StringComparison.Ordinal)));
                Assert.Equal(
                    UniqueModifierMechanicalMappingStatus.Ambiguous,
                    historicalReservation.MechanicalMapping.Status);
                Assert.NotEqual(
                    UniqueModifierMechanicalMappingStatus.Exact,
                    historicalReservation.MechanicalMapping.Status);
            });

        var catalog = GameDataCatalog.FromPackage(package);
        var parsed = parser.Parse("""
            Item Class: Rings
            Rarity: Unique
            Circle of Fear
            Sapphire Ring
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Herald of Ice has 36(30-40)% increased Mana Reservation Efficiency
            { Unique Modifier }
            +1% to maximum Cold Resistance while affected by Herald of Ice
            """);
        var unique = resolver.Resolve(parsed, catalog);
        Assert.Single(unique.CompatibleVersions);
        Assert.Equal(UniqueItemVersionRole.Current, unique.CompatibleVersions[0].Role);
        var reservation = unique.ModifierBlocks.Single(block => block.ParsedModifierIndex == 0);
        Assert.True(reservation.IsResolved, reservation.Diagnostic);
        Assert.Null(reservation.DiagnosticCode);
        Assert.Equal(
            ["herald_of_ice_mana_reservation_efficiency_+%"],
            reservation.StatIds);
        Assert.Null(reservation.AggregationDiagnosticCode);
        var maximumCold = unique.ModifierBlocks.Single(block => block.ParsedModifierIndex == 1);
        Assert.True(maximumCold.IsResolved, maximumCold.Diagnostic);
    }

    [Fact]
    public async Task ActivePackage_AtziriChaosLeech_PreservesCurrentSourceMechanicsAcrossHistoricalConflict()
    {
        var package = await LoadActivePackageAsync();
        var item = Assert.Single(
            package.UniqueItems!.Items,
            candidate => string.Equals(
                candidate.CanonicalName,
                "Atziri's Promise",
                StringComparison.OrdinalIgnoreCase));
        var current = Assert.Single(
            item.Versions,
            version => version.Role == UniqueItemVersionRole.Current);
        var currentLeech = Assert.Single(
            current.ModifierBlocks,
            block => block.Lines.Any(line =>
                line.Contains("Chaos Damage Leeched as Life", StringComparison.Ordinal)));
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, currentLeech.MechanicalMapping.Status);
        Assert.Equal(
            ["local_unique_flask_life_leech_from_chaos_damage_permyriad_while_healing"],
            currentLeech.MechanicalMapping.StatIds);
        Assert.Contains(
            "current-role-deprecated-source-mechanics-filter",
            currentLeech.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.All(
            item.Versions.Where(version => version.Role == UniqueItemVersionRole.Historical),
            version =>
            {
                var historicalLeech = Assert.Single(
                    version.ModifierBlocks,
                    block => block.Lines.Any(line =>
                        line.Contains("Chaos Damage Leeched as Life", StringComparison.Ordinal)));
                Assert.Equal(
                    UniqueModifierMechanicalMappingStatus.Ambiguous,
                    historicalLeech.MechanicalMapping.Status);
                Assert.Equal(
                    "UNIQUE_MECHANICS_EXACT_CONFLICT",
                    historicalLeech.MechanicalMapping.DiagnosticCode);
                Assert.Equal(
                    UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics,
                    historicalLeech.MechanicalMapping.ConflictEvidence!.Kind);
            });

        var catalog = GameDataCatalog.FromPackage(package);
        var parsed = parser.Parse("""
            Item Class: Life Flasks
            Rarity: Unique
            Atziri's Promise
            Amethyst Flask
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Gain 15% of Physical Damage as Extra Chaos Damage during effect
            { Unique Modifier }
            2% of Chaos Damage Leeched as Life during Effect
            { Unique Modifier }
            Gain 12% of Elemental Damage as Extra Chaos Damage during effect
            """);
        var unique = resolver.Resolve(parsed, catalog);
        var leech = Assert.Single(
            unique.ModifierBlocks,
            block => block.StatIds.Contains(
                "local_unique_flask_life_leech_from_chaos_damage_permyriad_while_healing") ||
                block.AggregationDiagnosticCode is not null ||
                (block.DiagnosticCode == "UNIQUE_MECHANICS_EXACT_CONFLICT" &&
                    block.ConflictEvidence?.Kind ==
                        UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics));

        Assert.True(leech.IsResolved, leech.Diagnostic);
        Assert.Null(leech.DiagnosticCode);
        Assert.Equal(
            ["local_unique_flask_life_leech_from_chaos_damage_permyriad_while_healing"],
            leech.StatIds);
        Assert.Equal(
            ["ChaosDamageLifeLeechPermyriadWhileUsingFlaskUniqueFlask5New"],
            leech.ModifierIds);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            leech.AggregationDiagnosticCode);
        Assert.NotNull(leech.NonBlockingHistoricalConflictEvidence);
        Assert.Equal(
            UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics,
            leech.NonBlockingHistoricalConflictEvidence!.Kind);
        Assert.DoesNotContain(
            leech.ModifierIds,
            id => id.Contains("old_do_not_use", StringComparison.OrdinalIgnoreCase) ||
                id.Equals(
                    "ChaosDamageLifeLeechPerMyriadWhileUsingFlaskUniqueFlask5",
                    StringComparison.OrdinalIgnoreCase));

        var draft = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog);
        var filter = Assert.Single(
            Assert.IsType<TradeSearchDraft>(draft.Draft).ModifierFilters,
            component => component.RawCopiedText.Contains(
                "Chaos Damage Leeched as Life",
                StringComparison.Ordinal));
        Assert.Equal(
            ["local_unique_flask_life_leech_from_chaos_damage_permyriad_while_healing"],
            filter.ResolvedStatIds);
        Assert.Null(filter.UniqueResolutionDiagnosticCode);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            filter.UniqueAggregationDiagnosticCode);
        Assert.True(filter.HasExactUniqueSourceProvenance);
    }

    [Fact]
    public async Task ActivePackage_DoryaniElementalLeech_PreservesCurrentEquivalentSourceSetAcrossHistoricalConflict()
    {
        var package = await LoadActivePackageAsync();
        var item = Assert.Single(
            package.UniqueItems!.Items,
            candidate => string.Equals(
                candidate.CanonicalName,
                "Doryani's Catalyst",
                StringComparison.OrdinalIgnoreCase));
        var current = Assert.Single(
            item.Versions,
            version => version.Role == UniqueItemVersionRole.Current);
        var currentLeech = Assert.Single(
            current.ModifierBlocks,
            block => block.Lines.Any(line =>
                line.Contains("Elemental Damage Leeched as Life", StringComparison.Ordinal)));
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            currentLeech.MechanicalMapping.Status);
        Assert.Equal(
            ["base_life_leech_from_elemental_damage_permyriad"],
            currentLeech.MechanicalMapping.StatIds);
        Assert.Contains(
            "current-role-deprecated-source-mechanics-filter",
            currentLeech.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.All(
            item.Versions.Where(version => version.Role == UniqueItemVersionRole.Historical),
            version =>
            {
                var historicalLeech = Assert.Single(
                    version.ModifierBlocks,
                    block => block.Lines.Any(line =>
                        line.Contains("Elemental Damage Leeched as Life", StringComparison.Ordinal)));
                Assert.Equal(
                    UniqueModifierMechanicalMappingStatus.Ambiguous,
                    historicalLeech.MechanicalMapping.Status);
                Assert.Equal(
                    UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics,
                    historicalLeech.MechanicalMapping.ConflictEvidence!.Kind);
            });

        var catalog = GameDataCatalog.FromPackage(package);
        var parsed = parser.Parse("""
            Item Class: Sceptres
            Rarity: Unique
            Doryani's Catalyst
            Vaal Sceptre
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            0.2% of Elemental Damage Leeched as Life
            """);
        var unique = resolver.Resolve(parsed, catalog);
        var leech = Assert.Single(
            unique.ModifierBlocks,
            block => block.StatIds.Contains("base_life_leech_from_elemental_damage_permyriad") ||
                block.AggregationDiagnosticCode is not null);

        Assert.True(leech.IsResolved, leech.Diagnostic);
        Assert.Null(leech.DiagnosticCode);
        Assert.Equal(["base_life_leech_from_elemental_damage_permyriad"], leech.StatIds);
        Assert.True(leech.IsEquivalentSourceSet);
        Assert.Equal(
            [
                "ElementalDamageLeechedAsLifePermyriadUniqueSceptre7_",
                "SynthesisImplicitElementalLeechMinor1",
            ],
            leech.ModifierIds);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            leech.AggregationDiagnosticCode);
        Assert.Equal(
            UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics,
            leech.NonBlockingHistoricalConflictEvidence!.Kind);
        Assert.DoesNotContain(
            leech.ModifierIds,
            id => id.Equals(
                "ElementalDamageLeechedAsLifeUniqueSceptre7",
                StringComparison.OrdinalIgnoreCase));

        var draft = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog);
        var filter = Assert.Single(
            Assert.IsType<TradeSearchDraft>(draft.Draft).ModifierFilters,
            component => component.RawCopiedText.Contains(
                "Elemental Damage Leeched as Life",
                StringComparison.Ordinal));
        Assert.Equal(["base_life_leech_from_elemental_damage_permyriad"], filter.ResolvedStatIds);
        Assert.True(filter.HasExactUniqueSourceProvenance);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            filter.UniqueAggregationDiagnosticCode);
    }

    [Fact]
    public async Task ActivePackage_TheHarvestLeech_RemainsExactWithoutHistoricalAggregation()
    {
        var package = await LoadActivePackageAsync();
        var catalog = GameDataCatalog.FromPackage(package);
        var parsed = parser.Parse("""
            Item Class: Two Hand Axes
            Rarity: Unique
            The Harvest
            Jasper Chopper
            --------
            Item Level: 70
            --------
            { Unique Modifier }
            1.2% of Damage Leeched as Life on Critical Strike
            """);
        var unique = resolver.Resolve(parsed, catalog);
        Assert.Equal(UniqueItemVersionRole.Current, Assert.Single(unique.CompatibleVersions).Role);
        var leech = Assert.Single(unique.ModifierBlocks);
        Assert.True(leech.IsResolved, leech.Diagnostic);
        Assert.Equal(["life_leech_permyriad_on_crit"], leech.StatIds);
        Assert.Null(leech.AggregationDiagnosticCode);
        Assert.Null(leech.NonBlockingHistoricalConflictEvidence);

        var draft = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog);
        var filter = Assert.Single(Assert.IsType<TradeSearchDraft>(draft.Draft).ModifierFilters);
        Assert.True(filter.HasExactUniqueSourceProvenance);
        Assert.Equal(["life_leech_permyriad_on_crit"], filter.ResolvedStatIds);
        Assert.Null(filter.UniqueAggregationDiagnosticCode);
    }

    [Fact]
    public async Task ActivePackage_MultiVersionAggregationCorpus_ReportsCompatibleHistoricalEncodingGroups()
    {
        var package = await LoadActivePackageAsync();
        var groups = EnumerateCurrentPlusHistoricalExactConflictGroups(package).ToArray();
        var eligible = groups
            .Where(group => group.HistoricalConflicts.All(conflict =>
                ParsedUniqueItemResolverCompatibilityProbe.IsCompatible(
                    conflict,
                    group.CurrentVector)))
            .ToArray();
        var blocking = groups.Except(eligible).ToArray();
        var subtypeBreakdown = groups
            .SelectMany(group => group.HistoricalConflicts.Select(conflict => conflict.Kind))
            .GroupBy(kind => kind)
            .ToDictionary(group => group.Key.ToString(), group => group.Count());
        var collisions = new
        {
            currentVectorAbsent = groups.Count(group =>
                group.HistoricalConflicts.Any(conflict =>
                    (conflict.Kind == UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent ||
                        conflict.Kind == UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics) &&
                    !conflict.Candidates.Any(candidate =>
                        string.Join('\u001f', candidate.StatIds)
                            .Equals(group.CurrentVector, StringComparison.OrdinalIgnoreCase)))),
            multipleNonDeprecatedVectors = groups.Count(group =>
                group.HistoricalConflicts.Any(conflict =>
                {
                    var modern = conflict.Candidates
                        .Where(candidate =>
                            !UniqueMechanicalConflictClassifier.HasDeprecatedLegacyEncodingEvidence(candidate))
                        .Select(candidate => string.Join('\u001f', candidate.StatIds))
                        .Where(vector => vector.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    return modern.Length > 1;
                })),
            multipleCurrentVectors = groups.Count(group => group.CurrentVectorCount > 1),
            otherSubtypes = groups.Count(group =>
                group.HistoricalConflicts.Any(conflict =>
                    conflict.Kind != UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent &&
                    conflict.Kind != UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics)),
        };

        var reportPath = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-UniqueHistoricalEncodingAggregationCorpus.json");
        await File.WriteAllTextAsync(
            reportPath,
            JsonSerializer.Serialize(
                new
                {
                    package.Manifest.DataVersion,
                    runtimeEligibleCurrentPlusHistoricalExactConflictGroups = groups.Length,
                    eligibleForCompatibilityRule = eligible.Length,
                    nonBlockingExpected = eligible.Length,
                    stillBlocking = blocking.Length,
                    historicalSubtypeBreakdown = subtypeBreakdown,
                    collisions,
                    classificationFingerprintSha256 = Convert.ToHexString(
                        SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(
                            '\n',
                            groups.Select(group =>
                                $"{group.ItemName}\u001f{group.Line}\u001f{group.CurrentVector}\u001f{string.Join('|', group.HistoricalConflicts.Select(c => c.Kind))}")))))
                        .ToLowerInvariant(),
                },
                new JsonSerializerOptions { WriteIndented = true }));

        Assert.True(groups.Length >= eligible.Length);
        Assert.True(File.Exists(reportPath));
    }

    private UniqueModifierBlockResolution ResolveLeech(GameDataCatalog catalog)
    {
        var result = resolver.Resolve(ParseLeechItem(), catalog);
        return Assert.Single(result.ModifierBlocks);
    }

    private ParsedItem ParseLeechItem() => parser.Parse("""
        Item Class: Two Hand Maces
        Rarity: Unique
        Test Hymn
        Sledgehammer
        --------
        Item Level: 70
        --------
        { Unique Modifier }
        1% of Physical Attack Damage Leeched as Life
        """);

    private static GameDataCatalog CreateLeechCatalog(
        UniqueModifierBlock current,
        UniqueModifierBlock historical) =>
        CreateCatalog(
            "Test Hymn",
            "Sledgehammer",
            UniqueItemKind.Ordinary,
            Version("Current", UniqueItemVersionRole.Current, current),
            Version("Pre 2.6.0", UniqueItemVersionRole.Historical, historical));

    private static UniqueModifierBlock CurrentResolvedBlock(
        string id,
        UniqueModifierMechanicalMappingStatus status,
        IReadOnlyList<string> modifierIds,
        IReadOnlyList<string> statIds,
        string? line = null,
        string? signature = null) => new()
    {
        Id = $"block:{id}",
        Kind = UniqueModifierBlockKind.Unique,
        Lines = [line ?? LeechLine],
        CanonicalSignatures = [signature ?? LeechLine],
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = status,
            ModifierIds = modifierIds,
            StatIds = statIds,
        },
        SourceObservationIds = [$"pob-observation:{id}"],
    };

    private static UniqueModifierBlock HistoricalConflictBlock(
        string id,
        string currentStatId,
        string deprecatedStatId,
        string deprecatedModifierId = "modifier:deprecated") =>
        HistoricalConflictBlockWithEvidence(
            id,
            new UniqueMechanicalConflictEvidence
            {
                Kind = UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent,
                Candidates =
                [
                    Candidate("modifier:historical-current", [currentStatId], ["permyriad"]),
                    Candidate(
                        deprecatedModifierId,
                        [deprecatedStatId],
                        ["percent", "deprecated-name", "handler-legacy"]),
                ],
            });

    private static UniqueModifierBlock HistoricalSourceMechanicsConflictBlock(
        string id,
        string currentStatId,
        string deprecatedStatId,
        string deprecatedModifierId = "modifier:deprecated-source") =>
        HistoricalConflictBlockWithEvidence(
            id,
            new UniqueMechanicalConflictEvidence
            {
                Kind = UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics,
                Candidates =
                [
                    Candidate(
                        "modifier:historical-current",
                        [currentStatId],
                        ["permyriad"],
                        UniqueModifierSemanticLocality.Local),
                    Candidate(
                        deprecatedModifierId,
                        [deprecatedStatId],
                        ["permyriad", "deprecated-name", "handler-legacy"],
                        UniqueModifierSemanticLocality.Local),
                ],
            });

    private static UniqueModifierBlock HistoricalConflictBlockWithKind(
        string id,
        UniqueMechanicalConflictKind kind,
        params UniqueMechanicalConflictCandidate[] candidates)
    {
        var evidence = new UniqueMechanicalConflictEvidence
        {
            Kind = kind,
            Candidates = candidates,
        };
        return HistoricalConflictBlockWithEvidence(id, evidence);
    }

    private static UniqueModifierBlock HistoricalConflictBlockWithEvidence(
        string id,
        UniqueMechanicalConflictEvidence evidence) => new()
    {
        Id = $"block:{id}",
        Kind = UniqueModifierBlockKind.Unique,
        Lines = [LeechLine],
        CanonicalSignatures = [LeechLine],
        MechanicalMapping = new UniqueModifierMechanicalMapping
        {
            Status = UniqueModifierMechanicalMappingStatus.Ambiguous,
            ModifierIds = evidence.Candidates.Select(candidate => candidate.ModifierId).ToArray(),
            StatIds = [],
            ConflictEvidence = evidence,
            DiagnosticCode = "UNIQUE_MECHANICS_EXACT_CONFLICT",
            Diagnostic = $"ExactConflict: {evidence.Kind}",
        },
        SourceObservationIds = [$"pob-observation:{id}"],
    };

    private static UniqueMechanicalConflictCandidate Candidate(
        string modifierId,
        IReadOnlyList<string> statIds,
        IReadOnlyList<string> markers,
        UniqueModifierSemanticLocality locality = UniqueModifierSemanticLocality.Unknown) => new()
    {
        ModifierId = modifierId,
        StatIds = statIds,
        EncodingMarkers = markers,
        Locality = locality,
        SourceAvailability = ModifierSourceAvailability.Unknown,
    };

    private static UniqueItemVersionObservation Version(
        string label,
        UniqueItemVersionRole role,
        params UniqueModifierBlock[] blocks) => new()
    {
        Id = $"version:{label}",
        Label = label,
        Role = role,
        BaseType = "Sledgehammer",
        ModifierBlocks = blocks,
        SourceObservationIds = blocks.SelectMany(block => block.SourceObservationIds).Distinct().ToArray(),
    };

    private static GameDataCatalog CreateCatalog(
        string name,
        string baseType,
        UniqueItemKind kind,
        params UniqueItemVersionObservation[] versions)
    {
        var observationIds = versions
            .SelectMany(version => version.SourceObservationIds)
            .Concat(versions.SelectMany(version => version.ModifierBlocks)
                .SelectMany(block => block.SourceObservationIds))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var observations = observationIds.Select(id => new UniqueCatalogSourceObservation
        {
            Id = id,
            ManifestSourceId = "path-of-building",
            RepositoryUri = "https://github.com/PathOfBuildingCommunity/PathOfBuilding",
            Tag = "v2.67.2",
            CommitSha = "b32759ab0f31a1c8499a0d420cb0f0633d4fe478",
            SourcePath = "Data/Uniques/test.lua",
            ObservedKind = kind,
            RawEntrySha256 = new string('a', 64),
        }).ToArray();
        var mappings = versions.SelectMany(version => version.ModifierBlocks)
            .Select(block => block.MechanicalMapping)
            .ToArray();
        var conflictCandidates = mappings
            .SelectMany(mapping => mapping.ConflictEvidence?.Candidates ?? [])
            .ToArray();
        var statIds = mappings.SelectMany(mapping => mapping.StatIds)
            .Concat(conflictCandidates.SelectMany(candidate => candidate.StatIds))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var modifiers = mappings.SelectMany(mapping => mapping.ModifierIds)
            .Concat(conflictCandidates.Select(candidate => candidate.ModifierId))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(modifierId => new ModifierDefinition
            {
                Id = modifierId,
                GroupId = $"group:{modifierId}",
                Name = modifierId,
                GenerationType = ModifierGenerationType.Prefix,
                Domain = "item",
                Stats =
                [
                    new ModifierStat
                    {
                        Index = 0,
                        StatId = statIds.FirstOrDefault() ?? "placeholder_stat",
                        MinValue = 1,
                        MaxValue = 1,
                    },
                ],
            })
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
                SourceObservations = observations,
                Items =
                [
                    new UniqueItemIdentity
                    {
                        Id = "unique:test-hymn",
                        CanonicalName = name,
                        Kind = kind,
                        BaseTypeEvidence = [baseType],
                        Versions = versions.Select(version => version with { BaseType = baseType }).ToArray(),
                        SourceObservationIds = observationIds,
                    },
                ],
            },
        });
    }

    private static async Task<GameDataPackage> LoadActivePackageAsync()
    {
        var packagePath = Environment.GetEnvironmentVariable("POENHANCE_GAMEDATA_AUDIT_PATH")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), $"Package not found: {packagePath}");
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess, string.Join("; ", load.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<GameDataPackage>(load.Package);
    }

    private static IEnumerable<AggregationCorpusGroup> EnumerateCurrentPlusHistoricalExactConflictGroups(
        GameDataPackage package)
    {
        foreach (var item in package.UniqueItems!.Items)
        {
            var currentVersions = item.Versions
                .Where(version => version.Role == UniqueItemVersionRole.Current)
                .ToArray();
            var historicalVersions = item.Versions
                .Where(version => version.Role == UniqueItemVersionRole.Historical)
                .ToArray();
            if (currentVersions.Length == 0 || historicalVersions.Length == 0)
            {
                continue;
            }

            foreach (var currentVersion in currentVersions)
            foreach (var currentBlock in currentVersion.ModifierBlocks.Where(block =>
                (block.MechanicalMapping.Status is UniqueModifierMechanicalMappingStatus.Exact or
                    UniqueModifierMechanicalMappingStatus.EquivalentSourceSet) &&
                block.MechanicalMapping.StatIds.Count > 0))
            {
                var signature = string.Join('\u001f', currentBlock.CanonicalSignatures);
                var historicalConflicts = historicalVersions
                    .SelectMany(version => version.ModifierBlocks)
                    .Where(block =>
                        string.Join('\u001f', block.CanonicalSignatures)
                            .Equals(signature, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(
                            block.MechanicalMapping.DiagnosticCode,
                            "UNIQUE_MECHANICS_EXACT_CONFLICT",
                            StringComparison.Ordinal) &&
                        block.MechanicalMapping.ConflictEvidence is not null)
                    .Select(block => block.MechanicalMapping.ConflictEvidence!)
                    .ToArray();
                if (historicalConflicts.Length == 0)
                {
                    continue;
                }

                yield return new AggregationCorpusGroup(
                    item.CanonicalName ?? item.Id ?? "unknown",
                    string.Join('\n', currentBlock.Lines),
                    string.Join('\u001f', currentBlock.MechanicalMapping.StatIds),
                    1,
                    historicalConflicts);
            }
        }
    }

    private sealed record AggregationCorpusGroup(
        string ItemName,
        string Line,
        string CurrentVector,
        int CurrentVectorCount,
        IReadOnlyList<UniqueMechanicalConflictEvidence> HistoricalConflicts);

    /// <summary>
    /// Test-local mirror of the production compatibility predicate for corpus counting only.
    /// </summary>
    private static class ParsedUniqueItemResolverCompatibilityProbe
    {
        public static bool IsCompatible(
            UniqueMechanicalConflictEvidence conflictEvidence,
            string currentVector)
        {
            if ((conflictEvidence.Kind !=
                        UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent &&
                    conflictEvidence.Kind !=
                        UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics) ||
                conflictEvidence.Candidates.Count < 2)
            {
                return false;
            }

            var candidateVectors = conflictEvidence.Candidates
                .Select(candidate => string.Join('\u001f', candidate.StatIds))
                .Where(vector => vector.Length > 0)
                .ToArray();
            if (!candidateVectors.Contains(currentVector, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            var nonDeprecatedCandidates = conflictEvidence.Candidates
                .Where(candidate =>
                    !UniqueMechanicalConflictClassifier.HasDeprecatedLegacyEncodingEvidence(candidate))
                .ToArray();
            if (nonDeprecatedCandidates
                .Select(candidate => string.Join('\u001f', candidate.StatIds))
                .Where(vector => vector.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Any(vector => !string.Equals(vector, currentVector, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var nonDeprecatedLocalities = nonDeprecatedCandidates
                .Select(candidate => candidate.Locality)
                .Where(locality => locality is UniqueModifierSemanticLocality.Local or
                    UniqueModifierSemanticLocality.Global)
                .Distinct()
                .ToArray();
            return nonDeprecatedLocalities.Length <= 1;
        }
    }
}
