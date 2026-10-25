using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// U.2.4H — AID Prefix/Suffix Chosen / of the Order rows on Exact Unique identities recover
/// packaged GeneratedCandidate Exact blocks (with numeric component bounds).
/// </summary>
public sealed class GeneratedOptionIdentityBoundRecoveryTests
{
    private static readonly Lazy<GameDataCatalog> Catalog = new(LoadCatalog);

    [Fact]
    public void GenuineReplicaParadoxica_ChosenPhysBlind_RecoversGeneratedCandidateWithEditableBounds()
    {
        var raw = File.ReadAllText(FindReplay(
            "Replica-Paradoxica-Chosen-PhysBlind.clipboard.txt"));
        var parsed = new ItemTextParser().Parse(raw);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, Catalog.Value);
        var unique = new ParsedUniqueItemResolver().Resolve(parsed, Catalog.Value, baseResolution);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);

        var physBlind = Assert.Single(
            unique.ModifierBlocks,
            block => block.IsIdentityBoundRecovery &&
                block.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate &&
                block.ModifierIds.Contains(
                    "JunMasterVeiledLocalIncreasedPhysicalDamageAndBlindChanceCrafted_"));
        Assert.True(physBlind.IsResolved);
        Assert.Equal(
            ["local_physical_damage_+%", "local_chance_to_blind_on_hit_%"],
            physBlind.StatIds.ToArray());

        var resolutions = new ParsedItemModifierCandidateResolver()
            .Resolve(parsed, Catalog.Value, baseResolution);
        var draft = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            baseResolution,
            resolutions,
            Catalog.Value);
        Assert.True(draft.IsSuccess);

        var phys = Assert.Single(
            draft.Draft!.ModifierFilters,
            component => component.OriginalText.Contains(
                "increased Physical Damage",
                StringComparison.OrdinalIgnoreCase));
        var blind = Assert.Single(
            draft.Draft.ModifierFilters,
            component => component.OriginalText.Contains(
                "chance to Blind Enemies on hit",
                StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            draft.Draft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Being Blinded",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal([134m], phys.ObservedNumericValues);
        Assert.Equal([23m], blind.ObservedNumericValues);
        Assert.True(phys.SupportsValueBounds);
        Assert.True(blind.SupportsValueBounds);
        Assert.Equal(134m, phys.RequestedMinimum);
        Assert.Equal(23m, blind.RequestedMinimum);
        Assert.Null(phys.RequestedMaximum);
        Assert.Null(blind.RequestedMaximum);

        // Single-line GeneratedCandidate siblings on the same genuine capture must retain
        // Unique-resolved StatIds (AID Exact must not clear them on the non-expanded path).
        Assert.Contains(
            unique.ModifierBlocks,
            block => block.IsIdentityBoundRecovery &&
                block.IsResolved &&
                block.ModifierIds.Contains("JunMasterVeiledChaosPenetrationWithAttacks__") &&
                block.StatIds.SequenceEqual(["local_chaos_penetration_%"]));
        Assert.Contains(
            unique.ModifierBlocks,
            block => block.IsIdentityBoundRecovery &&
                block.IsResolved &&
                block.ModifierIds.Contains(
                    "JunMasterVeiledCriticalStrikeMultiplierIfRareOrUniqueEnemyNearby1h") &&
                block.StatIds.SequenceEqual(
                    ["critical_strike_multiplier_+_if_rare_or_unique_enemy_nearby"]));
        Assert.Contains(
            unique.ModifierBlocks,
            block => block.IsIdentityBoundRecovery &&
                block.IsResolved &&
                block.ModifierIds.Contains("JunMasterVeiledDoubleDamageChance1h") &&
                block.StatIds.SequenceEqual(["chance_to_deal_double_damage_%"]));

        var penetrate = Assert.Single(
            draft.Draft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Penetrate",
                StringComparison.OrdinalIgnoreCase) &&
                component.OriginalText.Contains("Chaos Resistance", StringComparison.OrdinalIgnoreCase));
        var critNearby = Assert.Single(
            draft.Draft.ModifierFilters,
            component => component.OriginalText.Contains(
                "Critical Strike Multiplier while a Rare or Unique Enemy is Nearby",
                StringComparison.OrdinalIgnoreCase));
        var doubleDamage = Assert.Single(
            draft.Draft.ModifierFilters,
            component => component.OriginalText.Contains(
                "chance to deal Double Damage",
                StringComparison.OrdinalIgnoreCase) &&
                !component.OriginalText.Contains("Focused", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(["local_chaos_penetration_%"], penetrate.ResolvedStatIds.ToArray());
        Assert.Equal(
            ["critical_strike_multiplier_+_if_rare_or_unique_enemy_nearby"],
            critNearby.ResolvedStatIds.ToArray());
        Assert.Equal(["chance_to_deal_double_damage_%"], doubleDamage.ResolvedStatIds.ToArray());
        Assert.Equal(14m, penetrate.RequestedMinimum);
        Assert.Equal(40m, critNearby.RequestedMinimum);
        Assert.Equal(6m, doubleDamage.RequestedMinimum);
        Assert.True(penetrate.SupportsValueBounds);
        Assert.True(critNearby.SupportsValueBounds);
        Assert.True(doubleDamage.SupportsValueBounds);
    }

    public static TheoryData<string, string, string, string, string> GeneratedOptionFamilyCases() =>
        new()
        {
            {
                "Paradoxica",
                "Vaal Rapier",
                """
                { Prefix Modifier "Chosen" (Tier: 1) — Damage, Physical, Attack }
                125(120-139)% increased Physical Damage
                22(21-25)% chance to cause Bleeding on Hit
                """,
                "JunMasterVeiledLocalIncreasedPhysicalDamageAndBleedChanceCrafted_",
                "125,22"
            },
            {
                "Replica Paradoxica",
                "Vaal Rapier",
                """
                { Prefix Modifier "Chosen" (Tier: 1) — Damage, Physical, Attack }
                128(120-139)% increased Physical Damage
                23(21-25)% chance to cause Bleeding on Hit
                """,
                "JunMasterVeiledLocalIncreasedPhysicalDamageAndBleedChanceCrafted_",
                "128,23"
            },
            {
                "Cane of Kulemak",
                "Serpentine Staff",
                """
                { Prefix Modifier "Chosen" (Tier: 1) — Damage, Physical, Attack }
                130(120-139)% increased Physical Damage
                24(21-25)% chance to cause Bleeding on Hit
                """,
                "JunMasterVeiledLocalIncreasedPhysicalDamageAndBleedChanceCrafted_",
                "130,24"
            },
            {
                "The Queen's Hunger",
                "Vaal Regalia",
                """
                { Prefix Modifier "Chosen" (Tier: 1) — Defences }
                9(9-10)% Chance to Block Spell Damage
                """,
                "JunMasterVeiledSpellBlockPercent____",
                "9"
            },
            {
                "That Which Was Taken",
                "Crimson Jewel",
                """
                { Prefix Modifier "Chosen" (Tier: 1) }
                10(8-12)% increased Mana Reservation Efficiency of Skills
                """,
                "AnimalCharmManaReservationEfficiency2",
                "10"
            },
            {
                "That Which Was Taken",
                "Crimson Jewel",
                """
                { Prefix Modifier "Chosen" (Tier: 1) }
                +12(10-15)% chance to Suppress Spell Damage
                """,
                "AnimalCharmSpellSuppressionChance2",
                "12"
            },
            {
                "That Which Was Taken",
                "Crimson Jewel",
                """
                { Prefix Modifier "Chosen" (Tier: 1) }
                Recover 2% of Life on Kill
                Recover 2% of Energy Shield on Kill
                Recover 2% of Mana on Kill
                """,
                "AnimalCharmLifeManaESOnKill2",
                "2,2,2"
            },
        };

    [Theory]
    [MemberData(nameof(GeneratedOptionFamilyCases))]
    public void GeneratedOptionFamilies_AidPrefixSuffix_RecoverExactNumericBounds(
        string uniqueName,
        string baseType,
        string advancedBlock,
        string expectedModifierId,
        string expectedObservedCsv)
    {
        var expectedObserved = expectedObservedCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(decimal.Parse)
            .ToArray();

        var clipboard = $"""
            Item Class: Tests
            Rarity: Unique
            {uniqueName}
            {baseType}
            --------
            Item Level: 85
            --------
            {advancedBlock}
            --------
            """;
        var parsed = new ItemTextParser().Parse(clipboard);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, Catalog.Value);
        var unique = new ParsedUniqueItemResolver().Resolve(parsed, Catalog.Value, baseResolution);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);

        var recovered = Assert.Single(
            unique.ModifierBlocks,
            block => block.IsIdentityBoundRecovery &&
                block.IsResolved &&
                block.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate &&
                block.ModifierIds.Contains(expectedModifierId));
        Assert.Equal(expectedModifierId, Assert.Single(recovered.ModifierIds));

        var resolutions = new ParsedItemModifierCandidateResolver()
            .Resolve(parsed, Catalog.Value, baseResolution);
        var draft = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            baseResolution,
            resolutions,
            Catalog.Value);
        Assert.True(draft.IsSuccess);

        var numericComponents = draft.Draft!.ModifierFilters
            .Where(component =>
                component.UniqueSourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate &&
                string.Equals(
                    component.ResolvedModifierId,
                    expectedModifierId,
                    StringComparison.OrdinalIgnoreCase) &&
                component.SupportsValueBounds)
            .OrderBy(component => component.SourceComponentIndex)
            .ToArray();
        Assert.Equal(expectedObserved.Length, numericComponents.Length);
        for (var index = 0; index < expectedObserved.Length; index++)
        {
            Assert.Equal([expectedObserved[index]], numericComponents[index].ObservedNumericValues);
            Assert.Equal(expectedObserved[index], numericComponents[index].RequestedMinimum);
            Assert.Null(numericComponents[index].RequestedMaximum);
        }
    }

    [Fact]
    public void FixedUniquePrefixText_DoesNotRecoverAsGeneratedOption()
    {
        // Crafted/prefix-shaped AID must not claim Fixed Unique catalog Exact via GeneratedOption path.
        var clipboard = """
            Item Class: Gloves
            Rarity: Unique
            Oskarm
            Nubuck Gloves
            --------
            Item Level: 80
            --------
            { Prefix Modifier "Chosen" (Tier: 1) }
            +(20-30) to Dexterity
            --------
            """;
        var parsed = new ItemTextParser().Parse(clipboard);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, Catalog.Value);
        var unique = new ParsedUniqueItemResolver().Resolve(parsed, Catalog.Value, baseResolution);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);
        Assert.DoesNotContain(
            unique.ModifierBlocks,
            block => block.IsIdentityBoundRecovery &&
                block.SourceSemantics == UniqueModifierSourceSemantics.GeneratedCandidate);
    }

    private static GameDataCatalog LoadCatalog()
    {
        var path = FindRepoFile("artifacts", "poenhance-game-data.json");
        var result = GameDataPackageLoader.LoadFromFileAsync(path).GetAwaiter().GetResult();
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(result.Package));
    }

    private static string FindReplay(string fileName) =>
        FindRepoFile(
            "PoEnhance.Core.Tests",
            "TestData",
            "Unique",
            "ReplayReadyCaptures",
            fileName);

    private static string FindRepoFile(params string[] relativeSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(
                new[] { directory.FullName }.Concat(relativeSegments).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException(string.Join('/', relativeSegments));
    }
}
