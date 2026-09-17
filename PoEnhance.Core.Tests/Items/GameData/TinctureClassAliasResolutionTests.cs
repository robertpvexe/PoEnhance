using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class TinctureClassAliasResolutionTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver modifierResolver = new();
    private readonly ParsedUniqueItemResolver uniqueResolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    private const string BattleWithinClipboard = """
        Item Class: Tinctures
        Rarity: Unique
        The Battle Within
        Oakbranch Tincture
        --------
        Item Level: 84
        --------
        { Implicit Modifier }
        Gain 3 Rage on Melee Weapon Hit
        --------
        { Unique Modifier — Attack }
        Does not inflict Mana Burn over time
        Inflicts Mana Burn on you when you Hit an Enemy with a Melee Weapon
        { Unique Modifier }
        Melee Weapon Attacks have Culling Strike
        { Unique Modifier }
        4(1-5)% increased Rarity of Items found per Mana Burn, up to a maximum of 100%
        """;

    [Fact]
    public async Task BattleWithin_ClassAliasRemovesMismatch_ButOakbranchRemainsAmbiguousWithoutTranslationProof()
    {
        var catalog = await LoadCatalogAsync();
        var oakCandidates = catalog.FindItemBasesByExactName("Oakbranch Tincture").ToArray();
        Assert.Equal(2, oakCandidates.Length);

        var candidateWithRage = Assert.Single(
            oakCandidates,
            candidate => candidate.Id == "Metadata/Items/Tinctures/Tincture9");
        var candidateWithoutImplicit = Assert.Single(
            oakCandidates,
            candidate => candidate.Id == "Metadata/Items/Tinctures/TinctureCullingStrike");
        Assert.Equal(["TinctureRageOnHitImplicit1"], candidateWithRage.ImplicitModifierIds);
        Assert.Empty(candidateWithoutImplicit.ImplicitModifierIds);

        var parsed = parser.Parse(BattleWithinClipboard);
        Assert.Equal("Tinctures", parsed.ItemClass);
        Assert.True(ItemBaseClassCompatibility.AreCompatible(parsed.ItemClass, "Tincture"));

        var afterClassFilter = oakCandidates
            .Where(candidate =>
                ItemBaseClassCompatibility.AreCompatible(parsed.ItemClass, candidate.ItemClass))
            .ToArray();
        Assert.Equal(2, afterClassFilter.Length);

        var matcher = new ModifierTextSignatureMatcher();
        var rageModifier = Assert.Single(
            catalog.FindModifiersById("TinctureRageOnHitImplicit1"));
        var translationMatch = matcher.Match(
            rageModifier,
            catalog,
            parsed.ImplicitModifiers.Single().ValueLines);
        Assert.Equal(ModifierTextSignatureMatchOutcome.Unknown, translationMatch.Outcome);
        Assert.Equal("MODIFIER_TEXT_TRANSLATION_MISSING", translationMatch.ReasonCode);

        var baseResolution = baseResolver.Resolve(parsed, catalog);
        Assert.Equal(ItemBaseResolutionStatus.Unknown, baseResolution.Status);
        Assert.Null(baseResolution.MatchedItemBase);
        Assert.Equal(2, baseResolution.Candidates.Count);
        Assert.Equal(
            ItemBaseResolutionDiagnosticCodes.BaseAmbiguous,
            baseResolution.Diagnostics.Single().Code);
        Assert.DoesNotContain(
            baseResolution.Diagnostics,
            diagnostic => diagnostic.Code == ItemBaseResolutionDiagnosticCodes.BaseItemClassMismatch);

        var draft = CreateDraft(parsed, catalog, baseResolution);

        var rage = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Gain 3 Rage", StringComparison.Ordinal));
        Assert.Equal(ParsedModifierKind.Implicit, rage.ParsedKind);
        Assert.False(rage.IsBaseImplicit);
        Assert.Equal(
            ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
            rage.UniqueCatalogImplicitConsumptionReason);
        Assert.Equal(3m, rage.RequestedMinimum);
        Assert.True(rage.IsSearchable, rage.NotSearchableReason);

        var mana = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Mana Burn over time", StringComparison.Ordinal));
        Assert.Equal(ParsedModifierKind.Unique, mana.ResolvedSourceKind);
        Assert.True(mana.HasExactUniqueSourceProvenance);

        var culling = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Culling Strike", StringComparison.Ordinal));
        Assert.Equal(ParsedModifierKind.Unique, culling.ResolvedSourceKind);
        Assert.True(culling.HasExactUniqueSourceProvenance);

        var rarity = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Rarity of Items found", StringComparison.Ordinal));
        Assert.Equal(ParsedModifierKind.Unique, rarity.ResolvedSourceKind);
        Assert.Equal(4m, rarity.RequestedMinimum);
        Assert.True(rarity.HasExactUniqueSourceProvenance);
    }

    [Fact]
    public async Task SingleBaseUniqueTinctures_ResolveExactAfterClassAlias()
    {
        var catalog = await LoadCatalogAsync();
        var controls = new (string Name, string Base, string ExpectedBaseId, string Clipboard)[]
        {
            ("Grasping Nightshade", "Sporebloom Tincture", "Metadata/Items/Tinctures/Tincture10", """
                Item Class: Tinctures
                Rarity: Unique
                Grasping Nightshade
                Sporebloom Tincture
                --------
                Item Level: 84
                --------
                { Implicit Modifier }
                25% chance to Blind Enemies on Hit with Melee Weapons
                30% increased Effect of Blind from Melee Weapons
                """),
            ("Sap of the Seasons", "Prismatic Tincture", "Metadata/Items/Tinctures/Tincture1", """
                Item Class: Tinctures
                Rarity: Unique
                Sap of the Seasons
                Prismatic Tincture
                --------
                Item Level: 84
                --------
                { Implicit Modifier }
                85% increased Elemental Damage with Melee Weapons
                """),
        };

        foreach (var control in controls)
        {
            var parsed = parser.Parse(control.Clipboard);
            Assert.True(ItemBaseClassCompatibility.AreCompatible(parsed.ItemClass, "Tincture"));
            var baseResolution = baseResolver.Resolve(parsed, catalog);
            Assert.Equal(ItemBaseResolutionStatus.Exact, baseResolution.Status);
            Assert.Equal(control.Base, baseResolution.MatchedItemBase?.Name);
            Assert.Equal(control.ExpectedBaseId, baseResolution.MatchedItemBase?.Id);
            Assert.Equal(
                ItemBaseResolutionDiagnosticCodes.BaseExactMatch,
                baseResolution.Diagnostics.Single().Code);
        }
    }

    [Fact]
    public async Task DualBaseUniqueTinctures_RemainAmbiguousWhenImplicitTranslationProofIsMissing()
    {
        var catalog = await LoadCatalogAsync();
        var controls = new (string Name, string Base, string Clipboard)[]
        {
            ("Mightblood Ire", "Ironwood Tincture", """
                Item Class: Tinctures
                Rarity: Unique
                Mightblood Ire
                Ironwood Tincture
                --------
                Item Level: 84
                --------
                { Implicit Modifier }
                40% reduced Enemy Stun Threshold with Melee Weapons
                20% increased Stun Duration with Melee Weapons
                """),
            ("Wildfire Phloem", "Ashbark Tincture", """
                Item Class: Tinctures
                Rarity: Unique
                Wildfire Phloem
                Ashbark Tincture
                --------
                Item Level: 84
                --------
                { Implicit Modifier }
                25% chance to Ignite with Melee Weapons
                75% increased Damage with Ignite from Melee Weapons
                """),
        };

        foreach (var control in controls)
        {
            var parsed = parser.Parse(control.Clipboard);
            Assert.True(ItemBaseClassCompatibility.AreCompatible(parsed.ItemClass, "Tincture"));
            var candidates = catalog.FindItemBasesByExactName(control.Base).ToArray();
            Assert.Equal(2, candidates.Length);
            var baseResolution = baseResolver.Resolve(parsed, catalog);
            Assert.Equal(ItemBaseResolutionStatus.Unknown, baseResolution.Status);
            Assert.Equal(
                ItemBaseResolutionDiagnosticCodes.BaseAmbiguous,
                baseResolution.Diagnostics.Single().Code);
            Assert.DoesNotContain(
                baseResolution.Diagnostics,
                diagnostic => diagnostic.Code == ItemBaseResolutionDiagnosticCodes.BaseItemClassMismatch);
        }
    }

    private TradeSearchDraft CreateDraft(
        ParsedItem parsed,
        GameDataCatalog catalog,
        ItemBaseResolutionResult baseResolution)
    {
        var unique = uniqueResolver.Resolve(parsed, catalog, baseResolution);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);
        var result = mapper.CreateDraft(
            parsed,
            baseResolution,
            modifierResolver.Resolve(parsed, catalog, baseResolution),
            catalog);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static async Task<GameDataCatalog> LoadCatalogAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess, string.Join(", ", load.Diagnostics.Select(d => d.Code)));
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(load.Package));
    }
}
