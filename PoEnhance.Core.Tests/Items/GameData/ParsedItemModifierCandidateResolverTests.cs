using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ParsedItemModifierCandidateResolverTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemModifierCandidateResolver resolver = new();

    [Fact]
    public void Resolve_PrefixNameAndGenerationType_ReturnsExactCandidate()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Prefix Modifier "hale" (Tier: 5) - Life }
+50 to maximum Life
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(ModifierGenerationType.Prefix, result.GenerationType);
        Assert.Equal("mod.prefix.hale.t5", Assert.Single(result.Candidates).Id);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierEligibilityNotEvaluated,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_ImplicitNameAndGenerationType_ReturnsExactCandidate()
    {
        var catalog = CreateCatalog(Modifier(
            "mod.implicit.gold-ring.item-rarity",
            "Gold Ring Implicit",
            ModifierGenerationType.Implicit));
        var item = ParseWithModifier("""
{ Implicit Modifier "Gold Ring Implicit" - Item Rarity }
15% increased Rarity of Items found (implicit)
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(ModifierGenerationType.Implicit, result.GenerationType);
        Assert.Equal("mod.implicit.gold-ring.item-rarity", Assert.Single(result.Candidates).Id);
    }

    [Fact]
    public void Resolve_CraftedModifierWithReliableNameAndKind_UsesUnderlyingGenerationKind()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.upgraded", "Upgraded", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Master Crafted Prefix Modifier "Upgraded" (Rank: 1) - Damage }
Adds 1 to 2 Physical Damage
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.True(result.ParsedModifier.IsCrafted);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(ModifierGenerationType.Prefix, result.GenerationType);
    }

    [Fact]
    public void Resolve_FracturedSuffixModifier_UsesUnderlyingSuffixGenerationKind()
    {
        var catalog = CreateCatalog(Modifier("mod.suffix.order", "of the Order", ModifierGenerationType.Suffix));
        var item = ParseWithModifier("""
{ Fractured Suffix Modifier "of the Order" (Tier: 4) - Caster }
12% increased Cast Speed
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.True(result.ParsedModifier.IsFractured);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(ModifierGenerationType.Suffix, result.GenerationType);
    }

    [Fact]
    public void Resolve_DuplicateNameAndGenerationType_ReturnsUnknownAmbiguousWithAllCandidatesInPackageOrder()
    {
        var catalog = CreateCatalog(
            Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix),
            Modifier("mod.prefix.hale.t6", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(
            ["mod.prefix.hale.t5", "mod.prefix.hale.t6"],
            result.Candidates.Select(candidate => candidate.Id));
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierEligibilityNotEvaluated,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_ManyNameAndKindCandidatesReduceToOneEligibleCandidate_ReturnsExact()
    {
        var catalog = CreateCatalog(
            [Base("base.gold-ring", "Gold Ring", "Ring", "item", ["default", "ring"])],
            Modifier(
                "mod.prefix.hale.ring",
                "Hale",
                ModifierGenerationType.Prefix,
                "item",
                SpawnWeight("ring", 1000)),
            Modifier(
                "mod.prefix.hale.amulet",
                "Hale",
                ModifierGenerationType.Prefix,
                "item",
                SpawnWeight("amulet", 1000)));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");
        var baseResolution = ExactBase(catalog, "base.gold-ring");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal("mod.prefix.hale.ring", Assert.Single(result.Candidates).Id);
        Assert.Equal(2, result.NameCandidateCount);
        Assert.Equal(2, result.GenerationKindCandidateCount);
        Assert.Equal(1, result.EligibilityCandidateCount);
        Assert.Equal(1, result.ExcludedCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierExactEligibleMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_ManyCandidatesReduceToSeveralEligibleCandidates_RemainsUnknown()
    {
        var catalog = CreateCatalog(
            [Base("base.gold-ring", "Gold Ring", "Ring", "item", ["default", "ring"])],
            Modifier("mod.prefix.hale.one", "Hale", ModifierGenerationType.Prefix, "item", SpawnWeight("ring", 1000)),
            Modifier("mod.prefix.hale.two", "Hale", ModifierGenerationType.Prefix, "item", SpawnWeight("default", 1000)),
            Modifier("mod.prefix.hale.amulet", "Hale", ModifierGenerationType.Prefix, "item", SpawnWeight("amulet", 1000)));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");
        var baseResolution = ExactBase(catalog, "base.gold-ring");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(["mod.prefix.hale.one", "mod.prefix.hale.two"], result.Candidates.Select(candidate => candidate.Id));
        Assert.Equal(3, result.NameCandidateCount);
        Assert.Equal(3, result.GenerationKindCandidateCount);
        Assert.Equal(2, result.EligibilityCandidateCount);
        Assert.Equal(1, result.ExcludedCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierEligibilityAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_AllCandidatesExcluded_ReturnsUnknownWithoutEligibleCandidates()
    {
        var catalog = CreateCatalog(
            [Base("base.gold-ring", "Gold Ring", "Ring", "item", ["default", "ring"])],
            Modifier("mod.prefix.hale.amulet", "Hale", ModifierGenerationType.Prefix, "item", SpawnWeight("amulet", 1000)),
            Modifier("mod.prefix.hale.flask", "Hale", ModifierGenerationType.Prefix, "flask", SpawnWeight("default", 1000)));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");
        var baseResolution = ExactBase(catalog, "base.gold-ring");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(2, result.ExcludedCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierNoEligibleCandidates,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_UnknownBasePreservesNameAndKindCandidates()
    {
        var catalog = CreateCatalog(
            Modifier("mod.prefix.hale.one", "Hale", ModifierGenerationType.Prefix),
            Modifier("mod.prefix.hale.two", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");
        var baseResolution = new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Unknown,
        };

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(["mod.prefix.hale.one", "mod.prefix.hale.two"], result.Candidates.Select(candidate => candidate.Id));
        Assert.Equal(2, result.EligibilityCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierEligibilityNotEvaluated,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_ProbableBaseCanBeEvaluated()
    {
        var catalog = CreateCatalog(
            [Base("base.gold-ring", "Gold Ring", "Ring", "item", ["default", "ring"])],
            Modifier("mod.prefix.hale.ring", "Hale", ModifierGenerationType.Prefix, "item", SpawnWeight("ring", 1000)),
            Modifier("mod.prefix.hale.amulet", "Hale", ModifierGenerationType.Prefix, "item", SpawnWeight("amulet", 1000)));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");
        var baseResolution = ProbableBase(catalog, "base.gold-ring");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal("mod.prefix.hale.ring", Assert.Single(result.Candidates).Id);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierExactEligibleMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_TraditionalInfluenceDynamicTagCanReduceCandidatesToExactMatch()
    {
        var catalog = CreateCatalog(
            [Base("base.astral-plate", "Astral Plate", "Body Armours", "item", ["body_armour", "default"])],
            Modifier(
                "mod.suffix.redemption.redeemer",
                "of Redemption",
                ModifierGenerationType.Suffix,
                "item",
                SpawnWeight("body_armour_eyrie", 1000),
                SpawnWeight("default", 0)),
            Modifier(
                "mod.suffix.redemption.ring",
                "of Redemption",
                ModifierGenerationType.Suffix,
                "item",
                SpawnWeight("ring_eyrie", 1000),
                SpawnWeight("default", 0)));
        var item = ParseWithModifier("""
{ Suffix Modifier "of Redemption" (Tier: 1) - Aura }
10% increased Effect of Non-Curse Auras from your Skills
""") with
        {
            TraditionalInfluences = ["Redeemer Item"],
        };
        var baseResolution = ExactBase(catalog, "base.astral-plate");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal("mod.suffix.redemption.redeemer", Assert.Single(result.Candidates).Id);
        Assert.Equal(1, result.EligibilityCandidateCount);
        Assert.Equal(1, result.ExcludedCandidateCount);
    }

    [Fact]
    public void Resolve_TraditionalInfluenceAffixIsExcludedFromPlainItem()
    {
        var catalog = CreateCatalog(
            [Base("base.astral-plate", "Astral Plate", "Body Armours", "item", ["body_armour", "default"])],
            Modifier(
                "mod.suffix.redemption.redeemer",
                "of Redemption",
                ModifierGenerationType.Suffix,
                "item",
                SpawnWeight("body_armour_eyrie", 1000),
                SpawnWeight("default", 0)));
        var item = ParseWithModifier("""
{ Suffix Modifier "of Redemption" (Tier: 1) - Aura }
10% increased Effect of Non-Curse Auras from your Skills
""");
        var baseResolution = ExactBase(catalog, "base.astral-plate");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(0, result.EligibilityCandidateCount);
        Assert.Equal(1, result.ExcludedCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierNoEligibleCandidates,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_EldritchInfluenceDoesNotUnlockTraditionalInfluenceAffix()
    {
        var catalog = CreateCatalog(
            [Base("base.astral-plate", "Astral Plate", "Body Armours", "item", ["body_armour", "default"])],
            Modifier(
                "mod.suffix.redemption.redeemer",
                "of Redemption",
                ModifierGenerationType.Suffix,
                "item",
                SpawnWeight("body_armour_eyrie", 1000),
                SpawnWeight("default", 0)));
        var item = ParseWithModifier("""
{ Suffix Modifier "of Redemption" (Tier: 1) - Aura }
10% increased Effect of Non-Curse Auras from your Skills
""") with
        {
            EldritchInfluences = ["Searing Exarch Item"],
        };
        var baseResolution = ExactBase(catalog, "base.astral-plate");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(1, result.ExcludedCandidateCount);
    }

    [Fact]
    public void Resolve_DoubleInfluencedItemCanMatchEitherTraditionalInfluence()
    {
        var catalog = CreateCatalog(
            [Base("base.gold-ring", "Gold Ring", "Rings", "item", ["ring", "default"])],
            Modifier(
                "mod.prefix.shaper",
                "Conqueror's",
                ModifierGenerationType.Prefix,
                "item",
                SpawnWeight("ring_shaper", 1000),
                SpawnWeight("default", 0)),
            Modifier(
                "mod.prefix.elder",
                "Conqueror's",
                ModifierGenerationType.Prefix,
                "item",
                SpawnWeight("ring_elder", 1000),
                SpawnWeight("default", 0)),
            Modifier(
                "mod.prefix.hunter",
                "Conqueror's",
                ModifierGenerationType.Prefix,
                "item",
                SpawnWeight("ring_basilisk", 1000),
                SpawnWeight("default", 0)));
        var item = ParseWithModifier("""
{ Prefix Modifier "Conqueror's" (Tier: 1) - Damage }
10% increased Damage
""") with
        {
            TraditionalInfluences = ["Shaper Item", "Elder Item"],
        };
        var baseResolution = ExactBase(catalog, "base.gold-ring");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(["mod.prefix.shaper", "mod.prefix.elder"], result.Candidates.Select(candidate => candidate.Id));
        Assert.Equal(2, result.EligibilityCandidateCount);
        Assert.Equal(1, result.ExcludedCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierEligibilityAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_AmbiguousBaseDoesNotGuessEligibility()
    {
        var catalog = CreateCatalog(
            Modifier("mod.prefix.hale.one", "Hale", ModifierGenerationType.Prefix),
            Modifier("mod.prefix.hale.two", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");
        var baseResolution = new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Unknown,
            Candidates =
            [
                Base("base.one", "Shared Base", "Ring", "item", ["ring"]),
                Base("base.two", "Shared Base", "Ring", "item", ["ring"]),
            ],
        };

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(["mod.prefix.hale.one", "mod.prefix.hale.two"], result.Candidates.Select(candidate => candidate.Id));
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierEligibilityNotEvaluated,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_NameMatchingDifferentGenerationType_ReturnsNotFound()
    {
        var catalog = CreateCatalog(Modifier("mod.suffix.hale", "Hale", ModifierGenerationType.Suffix));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierNotFound,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_SupportedKindWithoutParsedName_ReturnsNameNotAvailable()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Prefix Modifier }
+50 to maximum Life
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierNameNotAvailable,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_UnsupportedKindReportsUnsupportedWithoutFallbackMatch()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Unknown Modifier "Hale" }
+50 to maximum Life
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Null(result.GenerationType);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierKindUnsupported,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_UniqueModifierPlaceholderIsNotMatched()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix));
        var item = ParseWithModifier("""
{ Unique Modifier }
Adds 1 to 2 Physical Damage
""");

        var result = Assert.Single(resolver.Resolve(item, catalog));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Null(result.GenerationType);
        Assert.Empty(result.Candidates);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierKindUnsupported,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_NormalDescriptionModifierWithoutAdvancedMetadataIsNotGuessed()
    {
        var catalog = CreateCatalog(Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix));
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
--------
+50 to maximum Life
""");

        var results = resolver.Resolve(item, catalog);

        Assert.Empty(results);
    }

    [Fact]
    public void Resolve_DoesNotMutateParsedItemOrCatalogAndNeverReturnsProbableOrGeneric()
    {
        var catalog = CreateCatalog(
            Modifier("mod.prefix.hale.t5", "Hale", ModifierGenerationType.Prefix),
            Modifier("mod.suffix.order", "of the Order", ModifierGenerationType.Suffix));
        var originalModifiers = catalog.Modifiers.ToArray();
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
--------
{ Suffix Modifier "Missing" }
+10% to Fire Resistance
""");
        var originalRawText = item.RawText;
        var originalPrefixName = item.PrefixModifiers[0].Name;

        var results = resolver.Resolve(item, catalog);

        Assert.Same(item.PrefixModifiers[0], results[0].ParsedModifier);
        Assert.Equal(originalRawText, item.RawText);
        Assert.Equal(originalPrefixName, item.PrefixModifiers[0].Name);
        Assert.Equal(originalModifiers, catalog.Modifiers);
        Assert.DoesNotContain(results, result =>
            result.Status is ModifierCandidateResolutionStatus.Probable
                or ModifierCandidateResolutionStatus.Generic);
    }

    private ParsedItem ParseWithModifier(string modifierText)
    {
        return parser.Parse($"""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
--------
{modifierText}
""");
    }

    private static GameDataCatalog CreateCatalog(params ModifierDefinition[] modifiers)
    {
        return CreateCatalog([], modifiers);
    }

    private static GameDataCatalog CreateCatalog(
        IReadOnlyList<ItemBaseRecord> itemBases,
        params ModifierDefinition[] modifiers)
    {
        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = CreateManifest(),
            ItemBases = itemBases,
            Modifiers = modifiers,
            Stats = [Stat("test_stat")],
            StatTranslations = [],
        });
    }

    private static ModifierDefinition Modifier(
        string id,
        string name,
        ModifierGenerationType generationType)
    {
        return Modifier(id, name, generationType, "item");
    }

    private static ModifierDefinition Modifier(
        string id,
        string name,
        ModifierGenerationType generationType,
        string? domain,
        params ModifierSpawnWeight[] spawnWeights)
    {
        return new ModifierDefinition
        {
            Id = id,
            GroupId = $"group.{id}",
            Name = name,
            GenerationType = generationType,
            Domain = domain,
            SpawnWeights = spawnWeights,
            Stats =
            [
                new ModifierStat
                {
                    Index = 0,
                    StatId = "test_stat",
                    MinValue = 1m,
                    MaxValue = 2m,
                },
            ],
            Sources =
            [
                new GameDataSourceReference
                {
                    SourceId = "test",
                    ExternalId = id,
                },
            ],
        };
    }

    private static ItemBaseRecord Base(
        string id,
        string name,
        string itemClass,
        string? domain,
        IReadOnlyList<string> tags)
    {
        return new ItemBaseRecord
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
    }

    private static ItemBaseResolutionResult ExactBase(GameDataCatalog catalog, string id)
    {
        return MatchedBase(catalog, id, ItemBaseResolutionStatus.Exact);
    }

    private static ItemBaseResolutionResult ProbableBase(GameDataCatalog catalog, string id)
    {
        return MatchedBase(catalog, id, ItemBaseResolutionStatus.Probable);
    }

    private static ItemBaseResolutionResult MatchedBase(
        GameDataCatalog catalog,
        string id,
        ItemBaseResolutionStatus status)
    {
        var itemBase = Assert.Single(catalog.FindItemBasesById(id));
        return new ItemBaseResolutionResult
        {
            Status = status,
            MatchedItemBase = itemBase,
            ResolvedBaseId = itemBase.Id,
            ResolvedBaseName = itemBase.Name,
            Candidates = [itemBase],
        };
    }

    private static ModifierSpawnWeight SpawnWeight(string tag, int weight)
    {
        return new ModifierSpawnWeight
        {
            Tag = tag,
            Weight = weight,
        };
    }

    private static StatDefinition Stat(string id)
    {
        return new StatDefinition
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
    }

    private static GameDataPackageManifest CreateManifest()
    {
        return new GameDataPackageManifest
        {
            SchemaVersion = 1,
            DataVersion = "test",
            CreatedAtUtc = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
            League = "test",
            Patch = "test",
            Sources =
            [
                new GameDataPackageSource
                {
                    SourceId = "test",
                    RetrievedAtUtc = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
                    SourceVersion = "test",
                },
            ],
        };
    }
}
