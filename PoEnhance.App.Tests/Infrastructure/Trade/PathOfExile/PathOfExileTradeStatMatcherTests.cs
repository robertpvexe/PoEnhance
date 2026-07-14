using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeStatMatcherTests
{
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_OneExactNormalizedTemplateProducesExactWithOneNumber()
    {
        var catalog = Catalog(Entry("explicit.life", "+# to maximum Life", "explicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Prefix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.life", result.ExactCandidate?.StatId);
        Assert.Equal("+# to maximum Life", result.NormalizedItemTemplate);
        Assert.Equal([87m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_TwoNumberValuesPreserveOrder()
    {
        var catalog = Catalog(Entry("explicit.fire", "Adds # to # Fire Damage", "explicit"));

        var result = matcher.Match(Modifier("Adds 10 to 20 Fire Damage", ParsedModifierKind.Suffix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal([10m, 20m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_AdvancedRangePrefixMatchesProviderExplicitEntry()
    {
        var catalog = Catalog(Entry("explicit.dexterity", "+# to Dexterity", "explicit"));

        var result = matcher.Match(Modifier("+53(51-55) to Dexterity", ParsedModifierKind.Prefix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.dexterity", result.ExactCandidate?.StatId);
        Assert.Equal("+# to Dexterity", result.NormalizedItemTemplate);
        Assert.Equal([53m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_AdvancedRangeSuffixMatchesProviderExplicitEntry()
    {
        var catalog = Catalog(Entry("explicit.lightning_resistance", "+#% to Lightning Resistance", "explicit"));

        var result = matcher.Match(
            Modifier("+47(46-48)% to Lightning Resistance", ParsedModifierKind.Suffix),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.lightning_resistance", result.ExactCandidate?.StatId);
        Assert.Equal("+#% to Lightning Resistance", result.NormalizedItemTemplate);
        Assert.Equal([47m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_LiveShapeSyntheticCatalogMatchesAdvancedRangeSamplesExactly()
    {
        var catalog = Catalog(
            Entry("explicit.dexterity", "+# to Dexterity", "explicit"),
            Entry("explicit.cold_damage", "Adds # to # Cold Damage (Local)", "explicit"));

        var dexterity = matcher.Match(
            Modifier("+53(51-55) to Dexterity", ParsedModifierKind.Prefix),
            catalog);
        var coldDamage = matcher.Match(
            Modifier("Adds 46(41-55) to 81(81-95) Cold Damage", ParsedModifierKind.Prefix),
            catalog,
            WeaponContext(ModifierLocality.Local));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, dexterity.Status);
        Assert.Equal("explicit.dexterity", dexterity.ExactCandidate?.StatId);
        Assert.Equal([53m], dexterity.ExtractedNumericValues);
        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, coldDamage.Status);
        Assert.Equal("explicit.cold_damage", coldDamage.ExactCandidate?.StatId);
        Assert.Equal([46m, 81m], coldDamage.ExtractedNumericValues);
    }

    [Fact]
    public void Match_RangerBowColdDamageUsesObservedExplicitStatIdAndNotPseudo()
    {
        var catalog = Catalog(
            Entry("pseudo.pseudo_adds_cold_damage", "Adds # to # Cold Damage", "pseudo"),
            Entry("explicit.stat_2387423236", "Adds # to # Cold Damage", "explicit"),
            Entry("explicit.stat_1037193709", "Adds # to # Cold Damage (Local)", "explicit"),
            Entry("implicit.stat_2387423236", "Adds # to # Cold Damage", "implicit"),
            Entry("implicit.stat_1037193709", "Adds # to # Cold Damage (Local)", "implicit"),
            Entry("fractured.stat_2387423236", "Adds # to # Cold Damage", "fractured"),
            Entry("crafted.stat_2387423236", "Adds # to # Cold Damage", "crafted"),
            Entry("crafted.stat_1037193709", "Adds # to # Cold Damage (Local)", "crafted"));

        var result = matcher.Match(
            Modifier("Adds 46(41-55) to 81(81-95) Cold Damage", ParsedModifierKind.Prefix),
            catalog,
            WeaponContext(ModifierLocality.Local));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_1037193709", result.ExactCandidate?.StatId);
        Assert.Equal("explicit", result.ExactCandidate?.GroupId);
        Assert.Equal(ModifierLocality.Local, result.RequestedLocality);
        Assert.Equal(PathOfExileTradeProviderStatLocality.Local, result.ExactCandidate?.ProviderLocality);
        Assert.Contains(result.InitialCandidates, candidate => candidate.StatId == "explicit.stat_2387423236");
        Assert.Contains(result.RejectedCandidates, candidate => candidate.StatId == "explicit.stat_2387423236");
        Assert.Equal([46m, 81m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_TitanPlateArmourUsesOfficialExplicitStatId()
    {
        var catalog = Catalog(
            Entry("explicit.stat_809229260", "+# to Armour", "explicit"),
            Entry("fractured.stat_809229260", "+# to Armour", "fractured"),
            Entry("crafted.stat_809229260", "+# to Armour", "crafted"));

        var result = matcher.Match(
            Modifier("+136(121-150) to Armour", ParsedModifierKind.Prefix),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_809229260", result.ExactCandidate?.StatId);
        Assert.Equal("+# to Armour", result.NormalizedItemTemplate);
        Assert.Equal([136m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_TitanPlateLightningResistanceUsesOfficialExplicitStatId()
    {
        var catalog = Catalog(
            Entry("explicit.stat_1671376347", "+#% to Lightning Resistance", "explicit"),
            Entry("implicit.stat_1671376347", "+#% to Lightning Resistance", "implicit"),
            Entry("fractured.stat_1671376347", "+#% to Lightning Resistance", "fractured"),
            Entry("scourge.stat_1671376347", "+#% to Lightning Resistance", "scourge"),
            Entry("crafted.stat_1671376347", "+#% to Lightning Resistance", "crafted"));

        var result = matcher.Match(
            Modifier("+47(46-48)% to Lightning Resistance", ParsedModifierKind.Suffix),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_1671376347", result.ExactCandidate?.StatId);
        Assert.Equal("+#% to Lightning Resistance", result.NormalizedItemTemplate);
        Assert.Equal([47m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_FracturedHybridArmourStunRecoveryBlocksConservativelyWithoutSplitGuessing()
    {
        var catalog = Catalog(
            Entry("fractured.stat_809229260", "+# to Armour", "fractured"),
            Entry("fractured.stat_2511217560", "#% increased Stun and Block Recovery", "fractured"));

        var result = matcher.Match(
            Modifier(
                "+136(121-150) to Armour\n24(23-25)% increased Stun and Block Recovery",
                ParsedModifierKind.Prefix,
                isFractured: true),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Equal(
            "+# to Armour #% increased Stun and Block Recovery",
            result.NormalizedItemTemplate);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.NoCandidate, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_RangerBowFireDamageUsesObservedExplicitStatIdAndNotPseudo()
    {
        var catalog = Catalog(
            Entry("pseudo.pseudo_adds_fire_damage", "Adds # to # Fire Damage", "pseudo"),
            Entry("explicit.stat_321077055", "Adds # to # Fire Damage", "explicit"),
            Entry("explicit.stat_709508406", "Adds # to # Fire Damage (Local)", "explicit"),
            Entry("implicit.stat_321077055", "Adds # to # Fire Damage", "implicit"),
            Entry("implicit.stat_709508406", "Adds # to # Fire Damage (Local)", "implicit"),
            Entry("fractured.stat_321077055", "Adds # to # Fire Damage", "fractured"),
            Entry("crafted.stat_321077055", "Adds # to # Fire Damage", "crafted"),
            Entry("crafted.stat_709508406", "Adds # to # Fire Damage (Local)", "crafted"));

        var result = matcher.Match(
            Modifier("Adds 70(63-85) to 139(128-148) Fire Damage", ParsedModifierKind.Prefix),
            catalog,
            WeaponContext(ModifierLocality.Local));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_709508406", result.ExactCandidate?.StatId);
        Assert.Equal([70m, 139m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_RangerBowLightningDamageUsesOfficialLocalExplicitStatId()
    {
        var catalog = Catalog(
            Entry("pseudo.pseudo_adds_lightning_damage", "Adds # to # Lightning Damage", "pseudo"),
            Entry("explicit.stat_1334060246", "Adds # to # Lightning Damage", "explicit"),
            Entry("explicit.stat_3336890334", "Adds # to # Lightning Damage (Local)", "explicit"),
            Entry("implicit.stat_1334060246", "Adds # to # Lightning Damage", "implicit"),
            Entry("crafted.stat_3336890334", "Adds # to # Lightning Damage (Local)", "crafted"));

        var result = matcher.Match(
            Modifier("Adds 2(1-3) to 86(80-90) Lightning Damage", ParsedModifierKind.Prefix),
            catalog,
            WeaponContext(ModifierLocality.Local));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_3336890334", result.ExactCandidate?.StatId);
        Assert.Equal([2m, 86m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_WeaponAttackSpeedUsesOfficialLocalExplicitStatIdAndRejectsUnmarkedDuplicate()
    {
        var catalog = Catalog(
            Entry("explicit.stat_681332047", "#% increased Attack Speed", "explicit"),
            Entry("explicit.stat_210067635", "#% increased Attack Speed (Local)", "explicit"));

        var result = matcher.Match(
            Modifier("26(26-27)% increased Attack Speed", ParsedModifierKind.Suffix),
            catalog,
            Context(
                itemClass: "One Hand Axes",
                parsedBaseType: "Reaver Axe",
                locality: ModifierLocality.Local,
                internalStatIds: ["local_attack_speed_+%"]));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_210067635", result.ExactCandidate?.StatId);
        Assert.Equal("#% increased Attack Speed (Local)", result.ExactCandidate?.Text);
        Assert.Equal(PathOfExileTradeProviderStatLocality.Local, result.ExactCandidate?.ProviderLocality);
        Assert.Contains(result.InitialCandidates, candidate => candidate.StatId == "explicit.stat_681332047");
        Assert.Contains(result.RejectedCandidates, candidate => candidate.StatId == "explicit.stat_681332047");
        Assert.Equal([26m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_AttackSpeedProviderCandidateOrderReversalDoesNotChangeLocalWeaponResult()
    {
        var forward = Catalog(
            Entry("explicit.stat_681332047", "#% increased Attack Speed", "explicit", providerOrder: 0),
            Entry("explicit.stat_210067635", "#% increased Attack Speed (Local)", "explicit", providerOrder: 1));
        var reversed = Catalog(
            Entry("explicit.stat_210067635", "#% increased Attack Speed (Local)", "explicit", providerOrder: 0),
            Entry("explicit.stat_681332047", "#% increased Attack Speed", "explicit", providerOrder: 1));
        var modifier = Modifier("26% increased Attack Speed", ParsedModifierKind.Suffix);

        var first = matcher.Match(
            modifier,
            forward,
            Context(
                itemClass: "One Hand Axes",
                parsedBaseType: "Reaver Axe",
                locality: ModifierLocality.Local,
                internalStatIds: ["local_attack_speed_+%"]));
        var second = matcher.Match(
            modifier,
            reversed,
            Context(
                itemClass: "One Hand Axes",
                parsedBaseType: "Reaver Axe",
                locality: ModifierLocality.Local,
                internalStatIds: ["local_attack_speed_+%"]));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, first.Status);
        Assert.Equal(first.ExactCandidate?.StatId, second.ExactCandidate?.StatId);
        Assert.Equal("explicit.stat_210067635", second.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_GlobalAttackSpeedDoesNotForceLocalWeaponCandidate()
    {
        var catalog = Catalog(
            Entry("explicit.stat_681332047", "#% increased Attack Speed", "explicit"),
            Entry("explicit.stat_210067635", "#% increased Attack Speed (Local)", "explicit"));

        var result = matcher.Match(
            Modifier("8% increased Attack Speed", ParsedModifierKind.Suffix),
            catalog,
            Context(
                itemClass: "One Hand Axes",
                parsedBaseType: "Reaver Axe",
                locality: ModifierLocality.Global,
                internalStatIds: ["attack_speed_+%"]));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_681332047", result.ExactCandidate?.StatId);
        Assert.Equal(PathOfExileTradeProviderStatLocality.Unmarked, result.ExactCandidate?.ProviderLocality);
        Assert.Contains(result.RejectedCandidates, candidate => candidate.StatId == "explicit.stat_210067635");
    }

    [Fact]
    public void Match_AttackSpeedWithoutLocalityEvidenceIsAmbiguousAndDoesNotChooseFirstCandidate()
    {
        var catalog = Catalog(
            Entry("explicit.stat_681332047", "#% increased Attack Speed", "explicit"),
            Entry("explicit.stat_210067635", "#% increased Attack Speed (Local)", "explicit"));

        var result = matcher.Match(
            Modifier("26% increased Attack Speed", ParsedModifierKind.Suffix),
            catalog,
            Context(itemClass: "One Hand Axes", parsedBaseType: "Reaver Axe"));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Ambiguous, result.Status);
        Assert.Null(result.ExactCandidate);
        Assert.Equal(
            PathOfExileTradeStatMatchDiagnosticCodes.LocalityAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_ImplicitProvenanceSelectsImplicitProviderGroupInsteadOfExplicitDuplicate()
    {
        var catalog = Catalog(
            Entry("explicit.caster", "Cannot roll Caster Modifiers", "explicit"),
            Entry("implicit.caster", "Cannot roll Caster Modifiers", "implicit"),
            Entry("crafted.caster", "Cannot roll Caster Modifiers", "crafted"));

        var result = matcher.Match(
            Modifier("Cannot roll Caster Modifiers", ParsedModifierKind.Implicit),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("implicit.caster", result.ExactCandidate?.StatId);
        Assert.Equal("implicit", result.ExactCandidate?.ProviderKind);
        Assert.Contains(result.RejectedCandidates, candidate => candidate.StatId == "explicit.caster");
    }

    [Fact]
    public void Match_ImplicitProviderCandidateOrderingDoesNotAffectGroupSelection()
    {
        var forward = Catalog(
            Entry("explicit.phys", "#% additional Physical Damage Reduction", "explicit", providerOrder: 0),
            Entry("implicit.phys", "#% additional Physical Damage Reduction", "implicit", providerOrder: 1));
        var reversed = Catalog(
            Entry("implicit.phys", "#% additional Physical Damage Reduction", "implicit", providerOrder: 0),
            Entry("explicit.phys", "#% additional Physical Damage Reduction", "explicit", providerOrder: 1));
        var modifier = Modifier("3% additional Physical Damage Reduction", ParsedModifierKind.Implicit);

        var first = matcher.Match(modifier, forward);
        var second = matcher.Match(modifier, reversed);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, first.Status);
        Assert.Equal(first.ExactCandidate?.StatId, second.ExactCandidate?.StatId);
        Assert.Equal("implicit.phys", second.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_EquivalentDuplicateImplicitProviderCandidatesSelectStableCanonicalId()
    {
        var catalog = Catalog(
            Entry("implicit.suppress.one", "+#% chance to Suppress Spell Damage", "implicit"),
            Entry("implicit.suppress.two", "+#% chance to Suppress Spell Damage", "implicit"));

        var result = matcher.Match(
            Modifier("+5% chance to Suppress Spell Damage", ParsedModifierKind.Implicit),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("implicit.suppress.one", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_NonEquivalentImplicitProviderCandidatesRemainAmbiguous()
    {
        var catalog = Catalog(
            Entry("implicit.unmarked.suppress", "+#% chance to Suppress Spell Damage", "implicit"),
            Entry("implicit.local.suppress", "+#% chance to Suppress Spell Damage (Local)", "implicit"));

        var result = matcher.Match(
            Modifier("+5% chance to Suppress Spell Damage", ParsedModifierKind.Implicit),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Ambiguous, result.Status);
        Assert.Null(result.ExactCandidate);
        Assert.Equal(
            PathOfExileTradeStatMatchDiagnosticCodes.LocalityAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_ProviderCandidateOrderReversalDoesNotChangeLocalWeaponResult()
    {
        var forward = Catalog(
            Entry("explicit.stat_321077055", "Adds # to # Fire Damage", "explicit", providerOrder: 0),
            Entry("explicit.stat_709508406", "Adds # to # Fire Damage (Local)", "explicit", providerOrder: 1));
        var reversed = Catalog(
            Entry("explicit.stat_709508406", "Adds # to # Fire Damage (Local)", "explicit", providerOrder: 0),
            Entry("explicit.stat_321077055", "Adds # to # Fire Damage", "explicit", providerOrder: 1));
        var modifier = Modifier("Adds 70 to 139 Fire Damage", ParsedModifierKind.Prefix);

        var first = matcher.Match(modifier, forward, WeaponContext(ModifierLocality.Local));
        var second = matcher.Match(modifier, reversed, WeaponContext(ModifierLocality.Local));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, first.Status);
        Assert.Equal(first.ExactCandidate?.StatId, second.ExactCandidate?.StatId);
        Assert.Equal("explicit.stat_709508406", second.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_NewElementalLocalPairResolvesFromGameDataEvidenceWithoutHardCodedElementRule()
    {
        var catalog = Catalog(
            Entry("explicit.stat_void_unmarked", "Adds # to # Void Damage", "explicit"),
            Entry("explicit.stat_void_local", "Adds # to # Void Damage (Local)", "explicit"));

        var result = matcher.Match(
            Modifier("Adds 10 to 20 Void Damage", ParsedModifierKind.Prefix),
            catalog,
            WeaponContext(ModifierLocality.Local, ["local_minimum_added_void_damage", "local_maximum_added_void_damage"]));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_void_local", result.ExactCandidate?.StatId);
        Assert.Equal(["local_maximum_added_void_damage", "local_minimum_added_void_damage"], result.Trace?.InternalStatIds);
    }

    [Fact]
    public void Match_TrustedLocalRejectsOtherwiseIdenticalGlobalCandidate()
    {
        var catalog = Catalog(
            Entry("explicit.global_fire", "Adds # to # Fire Damage", "explicit"),
            Entry("explicit.local_fire", "Adds # to # Fire Damage (Local)", "explicit"));

        var result = matcher.Match(
            Modifier("Adds 10 to 20 Fire Damage", ParsedModifierKind.Prefix),
            catalog,
            Context(locality: ModifierLocality.Local));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.local_fire", result.ExactCandidate?.StatId);
        Assert.Contains(result.RejectedCandidates, candidate => candidate.StatId == "explicit.global_fire");
    }

    [Fact]
    public void Match_TrustedGlobalRejectsOtherwiseIdenticalLocalCandidate()
    {
        var catalog = Catalog(
            Entry("explicit.local_fire", "Adds # to # Fire Damage (Local)", "explicit"),
            Entry("explicit.global_fire", "Adds # to # Fire Damage", "explicit"));

        var result = matcher.Match(
            Modifier("Adds 10 to 20 Fire Damage", ParsedModifierKind.Prefix),
            catalog,
            Context(locality: ModifierLocality.Global));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.global_fire", result.ExactCandidate?.StatId);
        Assert.Contains(result.RejectedCandidates, candidate => candidate.StatId == "explicit.local_fire");
    }

    [Theory]
    [InlineData("+136 to Armour", "+# to Armour", "local_base_physical_damage_reduction_rating", "explicit.local_armour")]
    [InlineData("115% increased Armour", "#% increased Armour", "local_physical_damage_reduction_rating_+%", "explicit.local_armour_percent")]
    [InlineData("+160 to Evasion Rating", "+# to Evasion Rating", "local_base_evasion_rating", "explicit.local_evasion")]
    [InlineData("115% increased Evasion Rating", "#% increased Evasion Rating", "local_evasion_rating_+%", "explicit.local_evasion_percent")]
    [InlineData("+77 to maximum Energy Shield", "+# to maximum Energy Shield", "local_energy_shield", "explicit.local_es")]
    [InlineData("115% increased Energy Shield", "#% increased Energy Shield", "local_energy_shield_+%", "explicit.local_es_percent")]
    public void Match_LocalArmourDefenceStatsUseGameDataLocalEvidence(
        string modifierText,
        string providerText,
        string internalStatId,
        string expectedStatId)
    {
        var catalog = Catalog(
            Entry($"explicit.unmarked.{expectedStatId}", providerText, "explicit"),
            Entry(expectedStatId, $"{providerText} (Local)", "explicit"));

        var result = matcher.Match(
            Modifier(modifierText, ParsedModifierKind.Prefix),
            catalog,
            Context(locality: ModifierLocality.Local, internalStatIds: [internalStatId]));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal(expectedStatId, result.ExactCandidate?.StatId);
        Assert.Equal(PathOfExileTradeProviderStatLocality.Local, result.ExactCandidate?.ProviderLocality);
    }

    [Fact]
    public void Match_HybridLocalDefenceModifierPreservesRealSemanticCandidate()
    {
        var catalog = Catalog(
            Entry("explicit.unmarked.hybrid", "#% increased Armour and Evasion", "explicit"),
            Entry("explicit.local.hybrid", "#% increased Armour and Evasion (Local)", "explicit"));

        var result = matcher.Match(
            Modifier("84% increased Armour and Evasion", ParsedModifierKind.Prefix),
            catalog,
            Context(
                locality: ModifierLocality.Local,
                internalStatIds: ["local_armour_and_evasion_+%"]));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.local.hybrid", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_GlobalArmourModifierDoesNotSelectLocalCandidate()
    {
        var catalog = Catalog(
            Entry("explicit.global_armour", "#% increased Armour", "explicit"),
            Entry("explicit.local_armour", "#% increased Armour (Local)", "explicit"));

        var result = matcher.Match(
            Modifier("20% increased Armour", ParsedModifierKind.Prefix),
            catalog,
            Context(itemClass: "Rings", parsedBaseType: "Iron Ring", locality: ModifierLocality.Global));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.global_armour", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_ItemCategoryAloneDoesNotMarkUnrelatedModifierLocal()
    {
        var catalog = Catalog(
            Entry("explicit.global_damage", "#% increased Damage", "explicit"),
            Entry("explicit.local_damage", "#% increased Damage (Local)", "explicit"));

        var result = matcher.Match(
            Modifier("20% increased Damage", ParsedModifierKind.Prefix),
            catalog,
            Context(itemClass: "Body Armours", parsedBaseType: "Titan Plate"));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Ambiguous, result.Status);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.LocalityAmbiguous, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_UnknownLocalityWithLocalAndGlobalCandidatesIsAmbiguousAndDoesNotChooseFirst()
    {
        var catalog = Catalog(
            Entry("explicit.global_fire", "Adds # to # Fire Damage", "explicit"),
            Entry("explicit.local_fire", "Adds # to # Fire Damage (Local)", "explicit"));

        var result = matcher.Match(
            Modifier("Adds 10 to 20 Fire Damage", ParsedModifierKind.Prefix),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Ambiguous, result.Status);
        Assert.Null(result.ExactCandidate);
        Assert.Equal(
            ["explicit.global_fire", "explicit.local_fire"],
            result.Candidates.Select(candidate => candidate.StatId));
        Assert.Equal(
            PathOfExileTradeStatMatchDiagnosticCodes.LocalityAmbiguous,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_MissingExpectedLocalCandidateReturnsStableDiagnostic()
    {
        var catalog = Catalog(
            Entry("explicit.one", "+# to Armour", "explicit"),
            Entry("explicit.two", "+# to Armour", "explicit"));

        var result = matcher.Match(
            Modifier("+136 to Armour", ParsedModifierKind.Prefix),
            catalog,
            Context(locality: ModifierLocality.Local));

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Equal(
            PathOfExileTradeStatMatchDiagnosticCodes.ExpectedLocalCandidateMissing,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_MissingExpectedUnmarkedCandidateReturnsStableDiagnostic()
    {
        var catalog = Catalog(
            Entry("explicit.local.one", "#% increased Damage (Local)", "explicit"),
            Entry("explicit.local.two", "#% increased Damage (Local)", "explicit"));

        var result = matcher.Match(
            Modifier("20% increased Damage", ParsedModifierKind.Prefix),
            catalog,
            Context(locality: ModifierLocality.Global));

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Equal(
            PathOfExileTradeStatMatchDiagnosticCodes.ExpectedUnmarkedCandidateMissing,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_SpellAddedDamagePreservesObservedSpellStatId()
    {
        var catalog = Catalog(
            Entry("pseudo.pseudo_adds_fire_damage_to_spells", "Adds # to # Fire Damage to Spells", "pseudo"),
            Entry("explicit.stat_1133016593", "Adds # to # Fire Damage to Spells", "explicit"),
            Entry("implicit.stat_1133016593", "Adds # to # Fire Damage to Spells", "implicit"),
            Entry("fractured.stat_1133016593", "Adds # to # Fire Damage to Spells", "fractured"),
            Entry("crafted.stat_1133016593", "Adds # to # Fire Damage to Spells", "crafted"));

        var result = matcher.Match(
            Modifier("Adds 12(10-14) to 25(20-30) Fire Damage to Spells", ParsedModifierKind.Prefix),
            catalog,
            WeaponContext(ModifierLocality.Global));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_1133016593", result.ExactCandidate?.StatId);
        Assert.Equal(ModifierLocality.Global, result.RequestedLocality);
        Assert.Equal([12m, 25m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_AddedDamageToAttacksKeepsAttackScopeAndDoesNotMapGenericLocalWeaponDamage()
    {
        var catalog = Catalog(
            Entry("explicit.stat_709508406", "Adds # to # Fire Damage (Local)", "explicit"),
            Entry("explicit.stat_1573130764", "Adds # to # Fire Damage to Attacks", "explicit"));

        var result = matcher.Match(
            Modifier("Adds 12(10-14) to 25(20-30) Fire Damage to Attacks", ParsedModifierKind.Prefix),
            catalog,
            WeaponContext(ModifierLocality.Global));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_1573130764", result.ExactCandidate?.StatId);
        Assert.DoesNotContain(result.InitialCandidates, candidate => candidate.StatId == "explicit.stat_709508406");
    }

    [Fact]
    public void Match_MaximumLifeUsesObservedExplicitStatId()
    {
        var catalog = Catalog(
            Entry("explicit.stat_3299347043", "+# to maximum Life", "explicit"),
            Entry("implicit.stat_3299347043", "+# to maximum Life", "implicit"),
            Entry("crafted.stat_3299347043", "+# to maximum Life", "crafted"));

        var result = matcher.Match(
            Modifier("+101(100-114) to maximum Life", ParsedModifierKind.Prefix),
            catalog,
            WeaponContext(ModifierLocality.Global));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_3299347043", result.ExactCandidate?.StatId);
        Assert.Equal(ModifierLocality.Global, result.RequestedLocality);
        Assert.Equal([101m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_DexterityUsesObservedExplicitStatId()
    {
        var catalog = Catalog(
            Entry("explicit.stat_3261801346", "+# to Dexterity", "explicit"),
            Entry("implicit.stat_3261801346", "+# to Dexterity", "implicit"),
            Entry("crafted.stat_3261801346", "+# to Dexterity", "crafted"));

        var result = matcher.Match(
            Modifier("+53(51-55) to Dexterity", ParsedModifierKind.Suffix),
            catalog,
            WeaponContext(ModifierLocality.Global));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_3261801346", result.ExactCandidate?.StatId);
        Assert.Equal(ModifierLocality.Global, result.RequestedLocality);
        Assert.Equal([53m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_ResistanceUsesObservedExplicitStatIdWithoutLocalWeaponDamageInference()
    {
        var catalog = Catalog(
            Entry("explicit.stat_1671376347", "+#% to Lightning Resistance", "explicit"),
            Entry("implicit.stat_1671376347", "+#% to Lightning Resistance", "implicit"),
            Entry("crafted.stat_1671376347", "+#% to Lightning Resistance", "crafted"));

        var result = matcher.Match(
            Modifier("+47(46-48)% to Lightning Resistance", ParsedModifierKind.Suffix),
            catalog,
            WeaponContext(ModifierLocality.Global));

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.stat_1671376347", result.ExactCandidate?.StatId);
        Assert.Equal(ModifierLocality.Global, result.RequestedLocality);
    }

    [Fact]
    public void Match_PseudoOnlyCandidateIsNotUsedAsAutomaticFallbackForExplicitModifier()
    {
        var catalog = Catalog(
            Entry("pseudo.pseudo_adds_cold_damage", "Adds # to # Cold Damage", "pseudo"));

        var result = matcher.Match(
            Modifier("Adds 46(41-55) to 81(81-95) Cold Damage", ParsedModifierKind.Prefix),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Null(result.ExactCandidate);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_NoMatchingTemplateProducesNotFoundWithoutFallbackId()
    {
        var catalog = Catalog(Entry("explicit.mana", "+# to maximum Mana", "explicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Prefix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Null(result.ExactCandidate);
        Assert.Empty(result.Candidates);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.NoCandidate, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_TwoMatchingProviderEntriesProducesAmbiguousAndDoesNotChooseFirst()
    {
        var catalog = Catalog(
            Entry("explicit.life.one", "+# to maximum Life", "explicit"),
            Entry("explicit.life.two", "+# to maximum Life", "explicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Prefix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Ambiguous, result.Status);
        Assert.Null(result.ExactCandidate);
        Assert.Equal(["explicit.life.one", "explicit.life.two"], result.Candidates.Select(candidate => candidate.StatId));
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.AmbiguousCandidates, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_BlankModifierInputProducesInvalidInput()
    {
        var result = matcher.Match(Modifier("  ", ParsedModifierKind.Unknown), Catalog());

        Assert.Equal(PathOfExileTradeStatMatchStatus.InvalidInput, result.Status);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.BlankModifierText, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_NullCatalogProducesInvalidInput()
    {
        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Prefix), null);

        Assert.Equal(PathOfExileTradeStatMatchStatus.InvalidInput, result.Status);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.NullCatalog, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_UnsupportedNumericTokenProducesInvalidInput()
    {
        var result = matcher.Match(
            Modifier("Adds 1,000 Fire Damage", ParsedModifierKind.Prefix),
            Catalog(Entry("explicit.fire", "Adds # Fire Damage", "explicit")));

        Assert.Equal(PathOfExileTradeStatMatchStatus.InvalidInput, result.Status);
        Assert.Equal(
            PathOfExileTradeStatMatchDiagnosticCodes.UnsupportedNumericTokenFormat,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_ExplicitModifierDoesNotMapToClearlyImplicitEntry()
    {
        var catalog = Catalog(Entry("implicit.life", "+# to maximum Life", "implicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Prefix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_AdvancedRangePrefixDoesNotMapToClearlyImplicitEntry()
    {
        var catalog = Catalog(Entry("implicit.life", "+# to maximum Life", "implicit"));

        var result = matcher.Match(Modifier("+101(100-114) to maximum Life", ParsedModifierKind.Prefix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_ImplicitModifierDoesNotMapToClearlyExplicitEntry()
    {
        var catalog = Catalog(Entry("explicit.life", "+# to maximum Life", "explicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Implicit), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_CraftedAndFracturedModifiersDoNotMapToUnrelatedKnownCategories()
    {
        var craftedResult = matcher.Match(
            Modifier("+87 to maximum Life", ParsedModifierKind.Prefix, isCrafted: true),
            Catalog(Entry("explicit.life", "+# to maximum Life", "explicit")));
        var fracturedResult = matcher.Match(
            Modifier("+87 to maximum Life", ParsedModifierKind.Prefix, isFractured: true),
            Catalog(Entry("explicit.life", "+# to maximum Life", "explicit")));

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, craftedResult.Status);
        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, fracturedResult.Status);
    }

    [Fact]
    public void Match_UnknownProviderMetadataRemainsConservativeAndAmbiguous()
    {
        var catalog = Catalog(
            Entry("unknown.one", "+# to maximum Life", "mystery"),
            Entry("unknown.two", "+# to maximum Life", "mystery"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Implicit), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Ambiguous, result.Status);
        Assert.Equal(["unknown.one", "unknown.two"], result.Candidates.Select(candidate => candidate.StatId));
    }

    [Fact]
    public void Match_KindConstraintCanSelectOneCompatibleCandidateFromOtherwiseAmbiguousSet()
    {
        var catalog = Catalog(
            Entry("explicit.life", "+# to maximum Life", "explicit"),
            Entry("implicit.life", "+# to maximum Life", "implicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Implicit), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("implicit.life", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_RepeatedMatchingIsDeterministic()
    {
        var catalog = Catalog(
            Entry("explicit.one", "+# to maximum Life", "explicit"),
            Entry("explicit.two", "+# to maximum Life", "explicit"));
        var modifier = Modifier("+87 to maximum Life", ParsedModifierKind.Prefix);

        var first = matcher.Match(modifier, catalog);
        var second = matcher.Match(modifier, catalog);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(
            first.Candidates.Select(candidate => candidate.StatId),
            second.Candidates.Select(candidate => candidate.StatId));
    }

    private static PathOfExileTradeStatCatalog Catalog(params PathOfExileTradeStatEntry[] entries)
    {
        return new PathOfExileTradeStatCatalog(entries);
    }

    private static PathOfExileTradeStatEntry Entry(
        string id,
        string text,
        string groupId,
        int providerOrder = 0)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = providerOrder,
            GroupId = groupId,
            GroupLabel = groupId,
            Id = id,
            Text = text,
            Type = groupId,
        };
    }

    private static PathOfExileTradeStatMatchContext WeaponContext(
        ModifierLocality locality = ModifierLocality.Unknown,
        IReadOnlyList<string>? internalStatIds = null)
    {
        return Context(
            itemClass: "Bows",
            parsedBaseType: "Ranger Bow",
            locality: locality,
            internalStatIds: internalStatIds);
    }

    private static PathOfExileTradeStatMatchContext Context(
        string itemClass = "Body Armours",
        string parsedBaseType = "Titan Plate",
        ModifierLocality locality = ModifierLocality.Unknown,
        IReadOnlyList<string>? internalStatIds = null)
    {
        return new PathOfExileTradeStatMatchContext
        {
            ItemClass = itemClass,
            ParsedBaseType = parsedBaseType,
            ModifierLocality = locality,
            ResolvedModifierId = "mod.test",
            InternalStatIds = internalStatIds ?? [],
        };
    }

    private static ParsedModifier Modifier(
        string text,
        ParsedModifierKind kind,
        bool isCrafted = false,
        bool isFractured = false)
    {
        return new ParsedModifier(
            [text],
            RawMetadataLine: null,
            kind,
            Name: null,
            Tier: null,
            Rank: null,
            CategoryText: null,
            IsCrafted: isCrafted,
            IsFractured: isFractured,
            IsVeiled: false);
    }
}
