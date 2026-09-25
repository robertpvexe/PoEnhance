using System.Text.Json;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class CorruptedImplicitFixedSecondaryTranslationFidelityTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver resolver = new();
    private readonly ModifierTextSignatureMatcher matcher = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Theory]
    [InlineData("13(10-15)% chance to gain Onslaught for 4 seconds on Kill")]
    [InlineData("12(10-15)% chance to gain Onslaught for 4 seconds on Kill")]
    [InlineData("10(10-15)% chance to gain Onslaught for 4 seconds on Kill")]
    public async Task Resolve_OnslaughtCorruptedWithFixedSecondaryTranslation_IsExact(string line)
    {
        var catalog = await LoadActiveCatalogAsync();
        var raw = MjolnerClipboard(line);
        var parsed = parser.Parse(raw);
        var baseResolution = baseResolver.Resolve(parsed, catalog);
        var result = Assert.Single(
            resolver.Resolve(parsed, catalog, baseResolution),
            resolution => resolution.ParsedModifier.ValueLines.Any(value =>
                value.Contains("Onslaught", StringComparison.OrdinalIgnoreCase)));

        Assert.Equal(ParsedImplicitModifierOrigin.Corrupted, result.ParsedModifier.ImplicitOrigin);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("V2ChanceToGainOnslaughtOnKillCorrupted_", candidate.Id);
        Assert.Equal(
            ["chance_to_gain_onslaught_on_kill_%"],
            candidate.Stats
                .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
                .OrderBy(stat => stat.Index)
                .Select(stat => stat.StatId!)
                .ToArray());
        Assert.Contains(
            result.TextSignatureMatches ?? [],
            match => match.Outcome == ModifierTextSignatureMatchOutcome.Match);
        Assert.DoesNotContain(
            result.TextSignatureMatches ?? [],
            match => match.Outcome == ModifierTextSignatureMatchOutcome.Match &&
                match.CandidateSignatures.Any(signature =>
                    signature.Lines.Any(signatureLine =>
                        signatureLine.Contains("Physical Damage", StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public async Task Resolve_RealMjolnerCapture_OnslaughtIsExactWithProviderProvenance()
    {
        var catalog = await LoadActiveCatalogAsync();
        var raw = await ReadCaptureClipboardAsync(
            Path.Combine(
                Path.GetTempPath(),
                "PoEnhance-A.5.32-Corrupted-Onslaught-Capture"),
            fileNameContains: "103002");
        var parsed = parser.Parse(raw);
        var baseResolution = baseResolver.Resolve(parsed, catalog);
        var resolutions = resolver.Resolve(parsed, catalog, baseResolution);
        var onslaught = Assert.Single(
            resolutions,
            resolution => resolution.ParsedModifier.ValueLines.Any(line =>
                line.Contains("Onslaught", StringComparison.OrdinalIgnoreCase)));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, onslaught.Status);
        Assert.Equal(
            "V2ChanceToGainOnslaughtOnKillCorrupted_",
            Assert.Single(onslaught.Candidates).Id);

        var draftResult = mapper.CreateDraft(parsed, baseResolution, resolutions, catalog);
        Assert.True(draftResult.IsSuccess, string.Join("; ", draftResult.Diagnostics.Select(d => d.Message)));
        var draft = Assert.IsType<TradeSearchDraft>(draftResult.Draft);
        var component = Assert.Single(
            draft.ModifierFilters,
            filter => filter.ResolvedModifierId == "V2ChanceToGainOnslaughtOnKillCorrupted_");
        Assert.True(component.IsSearchable, component.NotSearchableReason);
        Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, component.StatMappingProof);
        Assert.Equal(["chance_to_gain_onslaught_on_kill_%"], component.ResolvedStatIds.ToArray());
        Assert.Equal(13m, component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.DoesNotContain(
            draft.ModifierFilters,
            filter => filter.ResolvedModifierId == "V2LocalIncreasedPhysicalDamageCorrupted1");
        Assert.Equal(
            1,
            draft.ModifierFilters.Count(filter =>
                filter.ResolvedModifierId == "V2ChanceToGainOnslaughtOnKillCorrupted_"));
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.DoesNotContain(
            "physical",
            string.Join(' ', component.ResolvedStatIds),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Resolve_EnduranceSibling_RemainsExactViaSingleStatTranslation()
    {
        var catalog = await LoadActiveCatalogAsync();
        var raw = MjolnerClipboard(
            "6(5-7)% chance to gain an Endurance Charge when you Stun an Enemy");
        var parsed = parser.Parse(raw);
        var baseResolution = baseResolver.Resolve(parsed, catalog);
        var result = Assert.Single(
            resolver.Resolve(parsed, catalog, baseResolution),
            resolution => resolution.ParsedModifier.ImplicitOrigin == ParsedImplicitModifierOrigin.Corrupted);

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(
            "V2ChanceToGainEnduranceChargeOnStunCorrupted_",
            Assert.Single(result.Candidates).Id);
    }

    [Fact]
    public void Match_ContainingFixedSecondary_MatchesParsedSignature()
    {
        var modifier = Modifier(Stat("chance_stat", 10, 15));
        var catalog = CreateCatalog(
            Translation(
                ["chance_stat", "duration_stat"],
                Variant(
                    ["{0}% chance to gain Effect for 4 seconds on Kill"],
                    ["#", "ignore"],
                    Condition(0, 1, null),
                    Condition(1, 0, 0))));

        var result = matcher.Match(
            modifier,
            catalog,
            ["13(10-15)% chance to gain Effect for 4 seconds on Kill"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
        Assert.Equal(
            ["<number>% chance to gain Effect for <number> seconds on Kill"],
            Assert.Single(result.CandidateSignatures).Lines);
    }

    [Fact]
    public void Match_SameBoundUnrelatedContainingTranslation_IsNoMatch()
    {
        var modifier = Modifier(Stat("local_phys", 10, 15));
        var catalog = CreateCatalog(
            Translation(
                ["local_phys", "no_phys"],
                Variant(
                    ["{0}% increased Physical Damage"],
                    ["#", "ignore"],
                    Condition(0, 1, null),
                    Condition(1, 0, 0))));

        var result = matcher.Match(
            modifier,
            catalog,
            ["13(10-15)% chance to gain Onslaught for 4 seconds on Kill"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.NoMatch, result.Outcome);
    }

    [Fact]
    public void Match_MissingTranslationWithoutContainingGroup_RemainsUnknown()
    {
        var modifier = Modifier(Stat("orphan_stat", 10, 15));
        var catalog = CreateCatalog();

        var result = matcher.Match(modifier, catalog, ["10% increased Damage"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Unknown, result.Outcome);
        Assert.Equal(ModifierTextSignatureMatchReasonCodes.TranslationMissing, result.ReasonCode);
    }

    [Fact]
    public void Match_ContainingGroupWithUnresolvedRangedSecondary_RemainsUnknown()
    {
        var modifier = Modifier(Stat("chance_stat", 10, 15));
        var catalog = CreateCatalog(
            Translation(
                ["chance_stat", "duration_stat"],
                Variant(
                    ["{0}% chance to gain Effect for {1} seconds on Kill"],
                    ["#", "#"],
                    Condition(0, 1, null),
                    Condition(1, 1, null))));

        var result = matcher.Match(
            modifier,
            catalog,
            ["13(10-15)% chance to gain Effect for 4 seconds on Kill"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Unknown, result.Outcome);
        Assert.Equal(ModifierTextSignatureMatchReasonCodes.TranslationMissing, result.ReasonCode);
    }

    [Fact]
    public void Match_MultipleContainingGroupsBothMatch_RemainAmbiguous()
    {
        var modifier = Modifier(Stat("chance_stat", 10, 15));
        var catalog = CreateCatalog(
            Translation(
                ["chance_stat", "duration_a"],
                Variant(
                    ["{0}% chance to gain Effect for 4 seconds on Kill"],
                    ["#", "ignore"],
                    Condition(0, 1, null),
                    Condition(1, 0, 0))),
            Translation(
                ["chance_stat", "duration_b"],
                Variant(
                    ["{0}% chance to gain Effect for 4 seconds on Kill"],
                    ["#", "ignore"],
                    Condition(0, 1, null),
                    Condition(1, 0, 0))));

        var result = matcher.Match(
            modifier,
            catalog,
            ["13(10-15)% chance to gain Effect for 4 seconds on Kill"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Unknown, result.Outcome);
        Assert.Equal(
            ModifierTextSignatureMatchReasonCodes.ContainingTranslationAmbiguous,
            result.ReasonCode);
    }

    [Fact]
    public void Match_ExactStatSetPreferredOverContainingGroup()
    {
        var modifier = Modifier(Stat("life_stat", 10, 15));
        var catalog = CreateCatalog(
            Translation(["life_stat"], Variant(["{0} to maximum Life"], ["+#"])),
            Translation(
                ["life_stat", "hidden_stat"],
                Variant(
                    ["{0} to maximum Life"],
                    ["+#", "ignore"],
                    Condition(0, null, null),
                    Condition(1, 0, 0))));

        var result = matcher.Match(modifier, catalog, ["+12(10-15) to maximum Life"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
        Assert.Equal(ModifierTextSignatureMatchReasonCodes.Match, result.ReasonCode);
    }

    [Fact]
    public void Match_TwoFixedSecondaries_CanMatch()
    {
        var modifier = Modifier(Stat("primary_stat", 5, 5));
        var catalog = CreateCatalog(
            Translation(
                ["primary_stat", "fixed_a", "fixed_b"],
                Variant(
                    ["Grants Effect for 4 seconds and 2 charges"],
                    ["ignore", "ignore", "ignore"],
                    Condition(0, 5, 5),
                    Condition(1, 0, 0),
                    Condition(2, 0, 0))));

        var result = matcher.Match(
            modifier,
            catalog,
            ["Grants Effect for 4 seconds and 2 charges"]);

        Assert.Equal(ModifierTextSignatureMatchOutcome.Match, result.Outcome);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(100)]
    public void Resolve_ContainingMatchAmongNSameBoundUnknownPeers_IsDeterministicExact(int peerCount)
    {
        // Keep peer translations exact/single-stat so they NoMatch the Onslaught-like line quickly.
        // N=100 proves no arbitrary candidate-cap without item-specific fast paths.
        var itemBase = Base("base.gavel", "Gavel", "One Hand Maces", "item", ["default", "mace"]);
        var onslaught = CorruptedModifier(
            "mod.onslaught",
            "chance_stat",
            10,
            15,
            SpawnWeight("mace", 1000),
            SpawnWeight("default", 0));
        var peers = Enumerable.Range(0, peerCount - 1)
            .Select(index => CorruptedModifier(
                $"mod.peer.{index}",
                $"peer_stat_{index}",
                10,
                15,
                SpawnWeight("mace", 1000),
                SpawnWeight("default", 0)))
            .ToArray();
        var translations = new List<StatTranslationDefinition>
        {
            Translation(
                ["chance_stat", "duration_stat"],
                Variant(
                    ["{0}% chance to gain Effect for 4 seconds on Kill"],
                    ["#", "ignore"],
                    Condition(0, 1, null),
                    Condition(1, 0, 0))),
        };
        translations.AddRange(peers.Select(peer =>
            Translation(
                [peer.Stats[0].StatId!],
                Variant(["{0}% increased Unrelated Damage"], ["#"]))));

        var catalog = CreateCatalogWithTranslations(
            [itemBase],
            translations.ToArray(),
            [onslaught, .. peers]);
        var item = parser.Parse("""
Item Class: One Hand Maces
Rarity: Unique
Test
Gavel
--------
Item Level: 85
--------
{ Corruption Implicit Modifier }
13(10-15)% chance to gain Effect for 4 seconds on Kill
--------
Corrupted
""");

        var result = Assert.Single(
            resolver.Resolve(item, catalog, ExactBase(catalog, "base.gavel")),
            resolution => resolution.ParsedModifier.ImplicitOrigin == ParsedImplicitModifierOrigin.Corrupted);

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal("mod.onslaught", Assert.Single(result.Candidates).Id);
        Assert.True(peerCount is 1 or 2 or 6 or 100);
    }

    [Fact]
    public void Resolve_AdvancedRangeAloneWithTranslationMissing_DoesNotManufactureExact()
    {
        var itemBase = Base("base.boots", "Vaal Greaves", "Boots", "item", ["default", "boots", "armour"]);
        var missingTranslation = CorruptedModifier(
            "mod.missing",
            "missing_stat",
            10,
            15,
            SpawnWeight("boots", 1000),
            SpawnWeight("default", 0));
        var catalog = CreateCatalogWithTranslations(
            [itemBase],
            [],
            [missingTranslation]);
        var item = parser.Parse("""
Item Class: Boots
Rarity: Rare
Test
Vaal Greaves
--------
Item Level: 70
--------
{ Corruption Implicit Modifier }
15(10-15)% chance for Melee Hits to Fortify
--------
Corrupted
""");

        var result = Assert.Single(
            resolver.Resolve(item, catalog, ExactBase(catalog, "base.boots")),
            resolution => resolution.ParsedModifier.ImplicitOrigin == ParsedImplicitModifierOrigin.Corrupted);

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal("mod.missing", Assert.Single(result.Candidates).Id);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextNotEvaluated,
            Assert.Single(result.Diagnostics).Code);
        Assert.DoesNotContain(
            result.TextSignatureMatches ?? [],
            match => match.Outcome == ModifierTextSignatureMatchOutcome.Match);
    }

    [Fact]
    public void Resolve_TwoGenuineSemanticMatches_RemainAmbiguous()
    {
        var itemBase = Base("base.gavel", "Gavel", "One Hand Maces", "item", ["default", "mace"]);
        var first = CorruptedModifier(
            "mod.first",
            "shared_stat",
            10,
            15,
            SpawnWeight("mace", 1000),
            SpawnWeight("default", 0));
        var second = CorruptedModifier(
            "mod.second",
            "shared_stat",
            10,
            15,
            SpawnWeight("mace", 1000),
            SpawnWeight("default", 0));
        var catalog = CreateCatalogWithTranslations(
            [itemBase],
            [Translation(["shared_stat"], Variant(["{0}% increased Damage"], ["#"]))],
            [first, second]);
        var item = parser.Parse("""
Item Class: One Hand Maces
Rarity: Unique
Test
Gavel
--------
Item Level: 85
--------
{ Corruption Implicit Modifier }
13(10-15)% increased Damage
--------
Corrupted
""");

        var result = Assert.Single(
            resolver.Resolve(item, catalog, ExactBase(catalog, "base.gavel")),
            resolution => resolution.ParsedModifier.ImplicitOrigin == ParsedImplicitModifierOrigin.Corrupted);

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    private static string MjolnerClipboard(string corruptedLine) =>
        "Item Class: One Hand Maces\n" +
        "Rarity: Unique\n" +
        "Mjölner\n" +
        "Gavel\n" +
        "--------\n" +
        "Item Level: 85\n" +
        "--------\n" +
        "{ Corruption Implicit Modifier }\n" +
        corruptedLine + "\n" +
        "--------\n" +
        "Corrupted\n";

    private static async Task<string> ReadCaptureClipboardAsync(string directory, string fileNameContains)
    {
        var path = Directory.EnumerateFiles(directory, "*.json")
            .First(file =>
                Path.GetFileName(file).Contains(fileNameContains, StringComparison.Ordinal) &&
                !Path.GetFileName(file).Contains("search", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(file).Contains("request", StringComparison.OrdinalIgnoreCase));
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);
        var raw = document.RootElement
            .GetProperty("replayContext")
            .GetProperty("rawClipboardText")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(raw));
        return raw!;
    }

    private static async Task<GameDataCatalog> LoadActiveCatalogAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        Assert.Equal(4, load.Package!.Manifest.SchemaVersion);
        Assert.Equal("3.29.1.2.10-unique-component-composite-translation", load.Package.Manifest.DataVersion);
        return GameDataCatalog.FromPackage(load.Package);
    }

    private static GameDataCatalog CreateCatalog(params StatTranslationDefinition[] translations)
    {
        var statIds = translations
            .SelectMany(translation => translation.StatIds)
            .Distinct(StringComparer.Ordinal)
            .DefaultIfEmpty("missing_stat")
            .ToArray();
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = CreateManifest(),
            ItemBases = [],
            Modifiers = [Modifier(statIds.Select(statId => Stat(statId, 1, 1)).ToArray())],
            Stats = statIds.Select(StatDefinition).ToArray(),
            StatTranslations = translations,
        });
    }

    private static GameDataCatalog CreateCatalogWithTranslations(
        IReadOnlyList<ItemBaseRecord> itemBases,
        IReadOnlyList<StatTranslationDefinition> translations,
        IReadOnlyList<ModifierDefinition> modifiers)
    {
        var statIds = modifiers
            .SelectMany(modifier => modifier.Stats)
            .Select(stat => stat.StatId)
            .Concat(translations.SelectMany(translation => translation.StatIds))
            .Where(statId => !string.IsNullOrWhiteSpace(statId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = CreateManifest(),
            ItemBases = itemBases,
            Modifiers = modifiers,
            Stats = statIds.Select(StatDefinition).ToArray(),
            StatTranslations = translations,
        });
    }

    private static ItemBaseResolutionResult ExactBase(GameDataCatalog catalog, string id) =>
        new()
        {
            Status = ItemBaseResolutionStatus.Exact,
            MatchedItemBase = catalog.FindItemBasesById(id).Single(),
        };

    private static ItemBaseRecord Base(
        string id,
        string name,
        string itemClass,
        string domain,
        IReadOnlyList<string> tags) =>
        new()
        {
            Id = id,
            Name = name,
            ItemClass = itemClass,
            Domain = domain,
            Tags = tags,
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "test",
                    ExternalId = id,
                },
            ],
        };

    private static ModifierDefinition CorruptedModifier(
        string id,
        string statId,
        decimal min,
        decimal max,
        params ModifierSpawnWeight[] spawnWeights) =>
        new()
        {
            Id = id,
            GroupId = id,
            Name = id,
            GenerationType = ModifierGenerationType.Corrupted,
            SourceGenerationType = "corrupted",
            SourceAvailability = ModifierSourceAvailability.PotentiallyEligible,
            Domain = "item",
            Stats = [Stat(statId, min, max)],
            SpawnWeights = spawnWeights,
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "test",
                    ExternalId = id,
                },
            ],
        };

    private static ModifierDefinition Modifier(params ModifierStat[] stats) =>
        new()
        {
            Id = "mod.test",
            GroupId = "group.test",
            Name = "Test",
            GenerationType = ModifierGenerationType.Prefix,
            Domain = "item",
            Stats = stats.Select((stat, index) => stat with { Index = index }).ToArray(),
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "test",
                    ExternalId = "mod.test",
                },
            ],
        };

    private static ModifierStat Stat(string statId, decimal min, decimal max) =>
        new()
        {
            Index = 0,
            StatId = statId,
            MinValue = min,
            MaxValue = max,
        };

    private static ModifierSpawnWeight SpawnWeight(string tag, int weight) =>
        new()
        {
            Tag = tag,
            Weight = weight,
        };

    private static StatTranslationDefinition Translation(
        IReadOnlyList<string> statIds,
        params StatTranslationVariant[] variants) =>
        new()
        {
            Id = "translation." + string.Join(".", statIds),
            StatIds = statIds,
            Language = "English",
            Variants = variants,
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "test",
                    ExternalId = "translation." + string.Join(".", statIds),
                },
            ],
        };

    private static StatTranslationVariant Variant(
        IReadOnlyList<string> lines,
        IReadOnlyList<string> formats,
        params StatTranslationCondition[] conditions)
    {
        conditions = conditions.Length == 0
            ? formats.Select((_, index) => Condition(index, null, null)).ToArray()
            : conditions;
        return new StatTranslationVariant
        {
            Conditions = conditions,
            ValueFormats = formats,
            IndexHandlers = formats
                .Select((_, index) => new StatTranslationIndexHandler
                {
                    Index = index,
                    Handlers = [],
                })
                .ToArray(),
            FormatLines = lines,
        };
    }

    private static StatTranslationCondition Condition(
        int index,
        decimal? min,
        decimal? max,
        bool isNegated = false) =>
        new()
        {
            Index = index,
            MinValue = min,
            MaxValue = max,
            IsNegated = isNegated,
        };

    private static StatDefinition StatDefinition(string id) =>
        new()
        {
            Id = id,
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "test",
                    ExternalId = id,
                },
            ],
        };

    private static GameDataPackageManifest CreateManifest() =>
        new()
        {
            SchemaVersion = 1,
            DataVersion = "test",
            CreatedAtUtc = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "test",
                    RetrievedAtUtc = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
                    SourceVersion = "test",
                },
            ],
        };
}
