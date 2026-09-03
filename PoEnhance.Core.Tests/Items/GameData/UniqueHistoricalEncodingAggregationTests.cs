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
                    conflict.Kind == UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent &&
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
                    conflict.Kind != UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent)),
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
        IReadOnlyList<string> markers) => new()
    {
        ModifierId = modifierId,
        StatIds = statIds,
        EncodingMarkers = markers,
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
            if (conflictEvidence.Kind !=
                    UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent ||
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

            return !conflictEvidence.Candidates
                .Where(candidate =>
                    !UniqueMechanicalConflictClassifier.HasDeprecatedLegacyEncodingEvidence(candidate))
                .Select(candidate => string.Join('\u001f', candidate.StatIds))
                .Where(vector => vector.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Any(vector => !string.Equals(vector, currentVector, StringComparison.OrdinalIgnoreCase));
        }
    }
}
