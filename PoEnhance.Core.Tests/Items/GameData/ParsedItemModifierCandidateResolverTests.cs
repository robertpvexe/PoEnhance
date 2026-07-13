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
        Assert.Equal(1, result.TextSignatureCandidateCount);
        Assert.Equal(1, result.ExcludedCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextNotEvaluated,
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
        Assert.Equal(2, result.TextSignatureCandidateCount);
        Assert.Equal(1, result.ExcludedCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextNotEvaluated,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_DefinitiveTextMismatchExcludesCandidateAndCanLeaveExactMatch()
    {
        var catalog = CreateCatalogWithTranslations(
            [Base("base.gold-ring", "Gold Ring", "Ring", "item", ["default", "ring"])],
            [
                Translation(["life_stat"], Variant(["{0} to maximum Life"], ["+#"])),
                Translation(["damage_stat"], Variant(["{0}% increased Damage"], ["#"])),
            ],
            ModifierWithStat(
                "mod.prefix.hale.life",
                "Hale",
                ModifierGenerationType.Prefix,
                "item",
                "life_stat",
                SpawnWeight("ring", 1000)),
            ModifierWithStat(
                "mod.prefix.hale.damage",
                "Hale",
                ModifierGenerationType.Prefix,
                "item",
                "damage_stat",
                SpawnWeight("ring", 1000)));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");
        var baseResolution = ExactBase(catalog, "base.gold-ring");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal("mod.prefix.hale.life", Assert.Single(result.Candidates).Id);
        Assert.Equal(2, result.EligibilityCandidateCount);
        Assert.Equal(1, result.TextSignatureCandidateCount);
        Assert.Equal(1, result.ExcludedByTextCandidateCount);
        Assert.Equal(1, result.ExcludedCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextExactMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_DisplayedTierDoesNotAffectTextSignatureMatch()
    {
        var catalog = CreateCatalogWithTranslations(
            [Base("base.gold-ring", "Gold Ring", "Ring", "item", ["default", "ring"])],
            [Translation(["life_stat"], Variant(["{0} to maximum Life"], ["+#"]))],
            ModifierWithStat(
                "mod.prefix.hale.life",
                "Hale",
                ModifierGenerationType.Prefix,
                "item",
                "life_stat",
                SpawnWeight("ring", 1000)));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 999) - Life }
+1 to maximum Life
""");
        var baseResolution = ExactBase(catalog, "base.gold-ring");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal("mod.prefix.hale.life", Assert.Single(result.Candidates).Id);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextExactMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_UnknownTextCandidateIsRetainedWithMatchingCandidate()
    {
        var catalog = CreateCatalogWithTranslations(
            [Base("base.gold-ring", "Gold Ring", "Ring", "item", ["default", "ring"])],
            [Translation(["life_stat"], Variant(["{0} to maximum Life"], ["+#"]))],
            ModifierWithStat(
                "mod.prefix.hale.life",
                "Hale",
                ModifierGenerationType.Prefix,
                "item",
                "life_stat",
                SpawnWeight("ring", 1000)),
            ModifierWithStat(
                "mod.prefix.hale.unknown",
                "Hale",
                ModifierGenerationType.Prefix,
                "item",
                "unknown_stat",
                SpawnWeight("ring", 1000)));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 9) - Life }
+999 to maximum Life
""");
        var baseResolution = ExactBase(catalog, "base.gold-ring");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(["mod.prefix.hale.life", "mod.prefix.hale.unknown"], result.Candidates.Select(candidate => candidate.Id));
        Assert.Equal(2, result.TextSignatureCandidateCount);
        Assert.Equal(0, result.ExcludedByTextCandidateCount);
        Assert.Contains(result.TextSignatureMatches ?? [], match =>
            match.Outcome == ModifierTextSignatureMatchOutcome.Unknown);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_AllTextMismatchesReturnUnknownWithNoFinalCandidates()
    {
        var catalog = CreateCatalogWithTranslations(
            [Base("base.gold-ring", "Gold Ring", "Ring", "item", ["default", "ring"])],
            [
                Translation(["damage_stat"], Variant(["{0}% increased Damage"], ["#"])),
                Translation(["accuracy_stat"], Variant(["{0}% increased Global Accuracy Rating"], ["#"])),
            ],
            ModifierWithStat(
                "mod.prefix.hale.damage",
                "Hale",
                ModifierGenerationType.Prefix,
                "item",
                "damage_stat",
                SpawnWeight("ring", 1000)),
            ModifierWithStat(
                "mod.prefix.hale.accuracy",
                "Hale",
                ModifierGenerationType.Prefix,
                "item",
                "accuracy_stat",
                SpawnWeight("ring", 1000)));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 5) - Life }
+50 to maximum Life
""");
        var baseResolution = ExactBase(catalog, "base.gold-ring");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Empty(result.Candidates);
        Assert.Equal(0, result.TextSignatureCandidateCount);
        Assert.Equal(2, result.ExcludedByTextCandidateCount);
        Assert.Equal(2, result.ExcludedCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextNoMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_MultipleTextMatchesRemainUnknownInPackageOrder()
    {
        var catalog = CreateCatalogWithTranslations(
            [Base("base.gold-ring", "Gold Ring", "Ring", "item", ["default", "ring"])],
            [Translation(["life_stat"], Variant(["{0} to maximum Life"], ["+#"]))],
            ModifierWithStat(
                "mod.prefix.hale.one",
                "Hale",
                ModifierGenerationType.Prefix,
                "item",
                "life_stat",
                SpawnWeight("ring", 1000)),
            ModifierWithStat(
                "mod.prefix.hale.two",
                "Hale",
                ModifierGenerationType.Prefix,
                "item",
                "life_stat",
                SpawnWeight("ring", 1000)));
        var item = ParseWithModifier("""
{ Prefix Modifier "Hale" (Tier: 1) - Life }
+1 to maximum Life
""");
        var baseResolution = ExactBase(catalog, "base.gold-ring");

        var result = Assert.Single(resolver.Resolve(item, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(["mod.prefix.hale.one", "mod.prefix.hale.two"], result.Candidates.Select(candidate => candidate.Id));
        Assert.Equal(2, result.TextSignatureCandidateCount);
        Assert.Equal(0, result.ExcludedByTextCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Resolve_RealAdvancedBowAnnotations_ReachExactTextCandidates()
    {
        var catalog = CreateCatalogWithTranslations(
            [Base("base.ranger-bow", "Ranger Bow", "Bows", "item", ["default", "bow"])],
            [
                Translation(["cold_min", "cold_max"], Variant(["Adds {0} to {1} Cold Damage"], ["#", "#"])),
                Translation(["fire_min", "fire_max"], Variant(["Adds {0} to {1} Fire Damage"], ["#", "#"])),
                Translation(["lightning_min", "lightning_max"], Variant(["Adds {0} to {1} Lightning Damage"], ["#", "#"])),
                Translation(["dexterity"], Variant(["{0} to Dexterity"], ["+#"])),
            ],
            ModifierWithStats(
                "mod.freezing",
                "Freezing",
                ModifierGenerationType.Prefix,
                "item",
                [StatRef("cold_min", 41, 55), StatRef("cold_max", 81, 95)],
                SpawnWeight("bow", 1000)),
            ModifierWithStats(
                "mod.scorching",
                "Scorching",
                ModifierGenerationType.Prefix,
                "item",
                [StatRef("fire_min", 63, 85), StatRef("fire_max", 128, 148)],
                SpawnWeight("bow", 1000)),
            ModifierWithStats(
                "mod.sparking",
                "Sparking",
                ModifierGenerationType.Prefix,
                "item",
                [StatRef("lightning_min", 8, 10), StatRef("lightning_max", 148, 173)],
                SpawnWeight("bow", 1000)),
            ModifierWithStat(
                "mod.wind",
                "of the Wind",
                ModifierGenerationType.Suffix,
                "item",
                "dexterity",
                SpawnWeight("bow", 1000)));
        var item = parser.Parse("""
Item Class: Bows
Rarity: Rare
Audit Branch
Ranger Bow
--------
Item Level: 84
--------
{ Prefix Modifier "Freezing" (Tier: 1) - Elemental, Cold, Attack }
Adds 46(41-55) to 81(81-95) Cold Damage
{ Prefix Modifier "Scorching" (Tier: 1) - Elemental, Fire, Attack }
Adds 70(63-85) to 139(128-148) Fire Damage
{ Prefix Modifier "Sparking" (Tier: 1) - Elemental, Lightning, Attack }
Adds 9(8-10) to 155(148-173) Lightning Damage
{ Suffix Modifier "of the Wind" (Tier: 1) - Attribute }
+53(51-55) to Dexterity
""");

        var results = resolver.Resolve(item, catalog, ExactBase(catalog, "base.ranger-bow"));

        AssertExactTextResult(results, "Freezing", "mod.freezing");
        AssertExactTextResult(results, "Scorching", "mod.scorching");
        AssertExactTextResult(results, "Sparking", "mod.sparking");
        AssertExactTextResult(results, "of the Wind", "mod.wind");
    }

    [Fact]
    public void Resolve_RealAdvancedRedeemerAccuracyAnnotations_DoNotDropTextCandidatesToZero()
    {
        var catalog = CreateCatalogWithTranslations(
            [Base("base.gold-ring", "Gold Ring", "Rings", "item", ["default", "ring"])],
            [
                Translation(["accuracy"], Variant(["{0}% increased Global Accuracy Rating"], ["#"])),
                Translation(["trap_damage"], Variant(["{0}% increased Trap Damage"], ["#"])),
                Translation(["attack_speed"], Variant(["{0}% increased Attack Speed"], ["#"])),
                Translation(["life"], Variant(["{0} to maximum Life"], ["+#"])),
                Translation(["lightning_resistance"], Variant(["{0}% to Lightning Resistance"], ["+#"])),
                Translation(["stun_recovery"], Variant(["{0}% increased Stun and Block Recovery"], ["#"])),
            ],
            ModifierWithStat("mod.accuracy.one", "of Redemption", ModifierGenerationType.Suffix, "item", "accuracy", SpawnWeight("ring_eyrie", 1000)),
            ModifierWithStat("mod.accuracy.two", "of Redemption", ModifierGenerationType.Suffix, "item", "accuracy", SpawnWeight("ring_eyrie", 1000)),
            ModifierWithStat("mod.trap", "of Redemption", ModifierGenerationType.Suffix, "item", "trap_damage", SpawnWeight("ring_eyrie", 1000)),
            ModifierWithStat("mod.speed", "of Redemption", ModifierGenerationType.Suffix, "item", "attack_speed", SpawnWeight("ring_eyrie", 1000)),
            ModifierWithStat("mod.life", "of Redemption", ModifierGenerationType.Suffix, "item", "life", SpawnWeight("ring_eyrie", 1000)),
            ModifierWithStat("mod.resist", "of Redemption", ModifierGenerationType.Suffix, "item", "lightning_resistance", SpawnWeight("ring_eyrie", 1000)),
            ModifierWithStat("mod.stun", "of Redemption", ModifierGenerationType.Suffix, "item", "stun_recovery", SpawnWeight("ring_eyrie", 1000)));
        var item = parser.Parse("""
Item Class: Rings
Rarity: Rare
Audit Loop
Gold Ring
--------
Redeemer Item
--------
Item Level: 84
--------
{ Suffix Modifier "of Redemption" (Tier: 1) - Attack }
20(16-20)% increased Global Accuracy Rating
""");

        var result = Assert.Single(resolver.Resolve(item, catalog, ExactBase(catalog, "base.gold-ring")));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, result.Status);
        Assert.Equal(7, result.EligibilityCandidateCount);
        Assert.Equal(2, result.TextSignatureCandidateCount);
        Assert.Equal(5, result.ExcludedByTextCandidateCount);
        Assert.Equal(["mod.accuracy.one", "mod.accuracy.two"], result.Candidates.Select(candidate => candidate.Id));
    }

    [Fact]
    public void Resolve_RealAdvancedCobaltJewelAnnotations_ReachExactTextCandidates()
    {
        var catalog = CreateCatalogWithTranslations(
            [Base("base.cobalt-jewel", "Cobalt Jewel", "Jewels", "misc", ["default", "not_str"])],
            [
                Translation(["trap_damage"], Variant(["{0}% increased Trap Damage"], ["#"])),
                Translation(["attack_speed"], Variant(["{0}% increased Attack Speed"], ["#"])),
            ],
            ModifierWithStat("mod.trapping", "Trapping", ModifierGenerationType.Prefix, "misc", "trap_damage", SpawnWeight("not_str", 1000)),
            ModifierWithStat("mod.berserking", "of Berserking", ModifierGenerationType.Suffix, "misc", "attack_speed", SpawnWeight("default", 1000)));
        var item = parser.Parse("""
Item Class: Jewels
Rarity: Rare
Audit Spark
Cobalt Jewel
--------
Item Level: 84
--------
{ Prefix Modifier "Trapping" (Tier: 1) - Trap, Damage }
15(14-16)% increased Trap Damage
{ Suffix Modifier "of Berserking" (Tier: 1) - Attack, Speed }
5(3-5)% increased Attack Speed
""");

        var results = resolver.Resolve(item, catalog, ExactBase(catalog, "base.cobalt-jewel"));

        AssertExactTextResult(results, "Trapping", "mod.trapping");
        AssertExactTextResult(results, "of Berserking", "mod.berserking");
    }

    [Fact]
    public void Resolve_RealAdvancedTitanPlateAnnotations_DoNotFalseNoMatch()
    {
        var catalog = CreateCatalogWithTranslations(
            [Base("base.titan-plate", "Titan Plate", "Body Armours", "item", ["default", "armour", "body_armour", "str_armour"])],
            [
                Translation(["life"], Variant(["{0} to maximum Life"], ["+#"])),
                Translation(["lightning_resistance"], Variant(["{0}% to Lightning Resistance"], ["+#"])),
                Translation(["stun_recovery"], Variant(["{0}% increased Stun and Block Recovery"], ["#"])),
                Translation(["life_regen"], Variant(["Regenerate {0} Life per second"], ["#"])),
                Translation(["armour"], Variant(["{0} to Armour"], ["+#"])),
            ],
            ModifierWithStat("mod.virile", "Virile", ModifierGenerationType.Prefix, "item", "life", SpawnWeight("default", 1000)),
            ModifierWithStat("mod.ephij", "of Ephij", ModifierGenerationType.Suffix, "item", "lightning_resistance", SpawnWeight("armour", 1000)),
            ModifierWithStat("mod.adamantite", "of Adamantite Skin", ModifierGenerationType.Suffix, "item", "stun_recovery", SpawnWeight("armour", 1000)),
            ModifierWithStat("mod.hydra", "of the Hydra", ModifierGenerationType.Suffix, "item", "life_regen", SpawnWeight("default", 1000)),
            ModifierWithStat("mod.unmoving", "Unmoving", ModifierGenerationType.Prefix, "item", "armour", SpawnWeight("str_armour", 1000)));
        var item = parser.Parse("""
Item Class: Body Armours
Rarity: Rare
Audit Shell
Titan Plate
--------
Item Level: 84
--------
{ Prefix Modifier "Virile" (Tier: 1) - Life }
+101(100-114) to maximum Life
{ Suffix Modifier "of Ephij" (Tier: 1) - Elemental, Lightning, Resistance }
+47(46-48)% to Lightning Resistance
{ Suffix Modifier "of Adamantite Skin" (Tier: 1) - Defences }
25(23-25)% increased Stun and Block Recovery
{ Suffix Modifier "of the Hydra" (Tier: 1) - Life }
Regenerate 29.2(24.1-32) Life per second
{ Fractured Prefix Modifier "Unmoving" (Tier: 1) - Defences, Armour }
+350(301-400) to Armour
""");

        var results = resolver.Resolve(item, catalog, ExactBase(catalog, "base.titan-plate"));

        AssertExactTextResult(results, "Virile", "mod.virile");
        AssertExactTextResult(results, "of Ephij", "mod.ephij");
        AssertExactTextResult(results, "of Adamantite Skin", "mod.adamantite");
        AssertExactTextResult(results, "of the Hydra", "mod.hydra");
        AssertExactTextResult(results, "Unmoving", "mod.unmoving");
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
        Assert.Equal(1, result.TextSignatureCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextNotEvaluated,
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
        Assert.Equal(2, result.TextSignatureCandidateCount);
        Assert.Equal(1, result.ExcludedCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextNotEvaluated,
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

    private static GameDataCatalog CreateCatalogWithTranslations(
        IReadOnlyList<ItemBaseRecord> itemBases,
        IReadOnlyList<StatTranslationDefinition> translations,
        params ModifierDefinition[] modifiers)
    {
        var statIds = modifiers
            .SelectMany(modifier => modifier.Stats)
            .Select(stat => stat.StatId)
            .Where(statId => !string.IsNullOrWhiteSpace(statId))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return GameDataCatalog.FromPackage(new GameDataPackage
        {
            Manifest = CreateManifest(),
            ItemBases = itemBases,
            Modifiers = modifiers,
            Stats = statIds.Select(Stat).ToArray(),
            StatTranslations = translations,
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

    private static ModifierDefinition ModifierWithStat(
        string id,
        string name,
        ModifierGenerationType generationType,
        string? domain,
        string statId,
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
                    StatId = statId,
                    MinValue = 1m,
                    MaxValue = 100m,
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

    private static ModifierDefinition ModifierWithStats(
        string id,
        string name,
        ModifierGenerationType generationType,
        string? domain,
        IReadOnlyList<ModifierStat> stats,
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
            Stats = stats
                .Select((stat, index) => stat with { Index = index })
                .ToArray(),
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

    private static ModifierStat StatRef(string statId, decimal min, decimal max)
    {
        return new ModifierStat
        {
            StatId = statId,
            MinValue = min,
            MaxValue = max,
        };
    }

    private static void AssertExactTextResult(
        IReadOnlyList<ModifierCandidateResolutionResult> results,
        string modifierName,
        string expectedId)
    {
        var result = Assert.Single(results, result => result.ParsedModifierName == modifierName);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(expectedId, Assert.Single(result.Candidates).Id);
        Assert.Equal(1, result.TextSignatureCandidateCount);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextExactMatch,
            Assert.Single(result.Diagnostics).Code);
    }

    private static StatTranslationDefinition Translation(
        IReadOnlyList<string> statIds,
        params StatTranslationVariant[] variants)
    {
        return new StatTranslationDefinition
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
    }

    private static StatTranslationVariant Variant(
        IReadOnlyList<string> lines,
        IReadOnlyList<string> formats)
    {
        return new StatTranslationVariant
        {
            Conditions = formats
                .Select((_, index) => new StatTranslationCondition
                {
                    Index = index,
                })
                .ToArray(),
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
