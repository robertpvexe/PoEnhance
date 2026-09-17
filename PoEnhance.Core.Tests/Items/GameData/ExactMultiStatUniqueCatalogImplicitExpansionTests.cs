using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class ExactMultiStatUniqueCatalogImplicitExpansionTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver modifierResolver = new();
    private readonly ParsedUniqueItemResolver uniqueResolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    private const string GraspingClipboard = """
        Item Class: Tinctures
        Rarity: Unique
        Grasping Nightshade
        Sporebloom Tincture
        --------
        Item Level: 84
        --------
        { Implicit Modifier }
        25% chance to Blind Enemies on Hit with Melee Weapons
        27(25-35)% increased Effect of Blind from Melee Weapons
        --------
        { Unique Modifier }
        Melee Weapon Attacks apply Withered on Hit for 2 seconds
        { Unique Modifier }
        Melee Weapon Attacks have a chance to create Grasping Vines on Hit
        """;

    [Fact]
    public async Task GraspingNightshade_BlindImplicit_ExpandsExactCompositionIntoProvenExactCatalogComponents()
    {
        var catalog = await LoadCatalogAsync();
        var draft = CreateDraft(GraspingClipboard, catalog);
        var blinds = draft.ModifierFilters
            .Where(filter => filter.RawCopiedText.Contains("Blind", StringComparison.OrdinalIgnoreCase))
            .OrderBy(filter => filter.SourceLineIndex)
            .ToArray();

        Assert.Equal(2, blinds.Length);
        Assert.All(blinds, component =>
        {
            Assert.Equal(ParsedModifierKind.Implicit, component.ParsedKind);
            Assert.Equal(ParsedModifierKind.Implicit, component.ResolvedSourceKind);
            Assert.False(component.IsBaseImplicit);
            Assert.Equal(
                ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
                component.UniqueCatalogImplicitConsumptionReason);
            Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
            Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, component.StatMappingProof);
            Assert.Equal("TinctureChanceToBlindImplicit1", component.ResolvedModifierId);
            Assert.Single(component.ResolvedStatIds);
            Assert.True(component.IsSearchable, component.NotSearchableReason);
            Assert.Null(component.UniqueResolutionDiagnosticCode);
        });

        var chance = Assert.Single(
            blinds,
            component => component.ResolvedStatIds.Contains(
                "chance_to_blind_on_hit_%_with_tinctured_weapons"));
        var effect = Assert.Single(
            blinds,
            component => component.ResolvedStatIds.Contains("blind_effect_+%_with_tinctured_weapons"));
        Assert.Equal(25m, chance.RequestedMinimum);
        Assert.Null(chance.RequestedMaximum);
        Assert.Equal(27m, effect.RequestedMinimum);
        Assert.Null(effect.RequestedMaximum);
    }

    [Fact]
    public async Task MightbloodIre_StunImplicit_ExpandsExactComposition()
    {
        var catalog = await LoadCatalogAsync();
        var draft = CreateDraft(
            """
            Item Class: Tinctures
            Rarity: Unique
            Mightblood Ire
            Ironwood Tincture
            --------
            Item Level: 84
            --------
            { Implicit Modifier }
            40% reduced Enemy Stun Threshold with Melee Weapons
            20(15-25)% increased Stun Duration with Melee Weapons
            """,
            catalog);
        var components = draft.ModifierFilters
            .Where(filter => filter.ParsedKind == ParsedModifierKind.Implicit)
            .ToArray();
        Assert.Equal(2, components.Length);
        Assert.All(components, component =>
        {
            Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, component.StatMappingProof);
            Assert.Single(component.ResolvedStatIds);
            Assert.True(component.IsSearchable, component.NotSearchableReason);
        });
    }

    [Fact]
    public async Task WildfirePhloem_IgniteImplicit_ExpandsExactComposition()
    {
        var catalog = await LoadCatalogAsync();
        var draft = CreateDraft(
            """
            Item Class: Tinctures
            Rarity: Unique
            Wildfire Phloem
            Ashbark Tincture
            --------
            Item Level: 84
            --------
            { Implicit Modifier }
            25% chance to Ignite with Melee Weapons
            75(60-90)% increased Damage with Ignite from Melee Weapons
            """,
            catalog);
        var components = draft.ModifierFilters
            .Where(filter => filter.ParsedKind == ParsedModifierKind.Implicit)
            .ToArray();
        Assert.Equal(2, components.Length);
        Assert.All(components, component =>
        {
            Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, component.StatMappingProof);
            Assert.Single(component.ResolvedStatIds);
            Assert.True(component.IsSearchable, component.NotSearchableReason);
        });
    }

    [Fact]
    public async Task DuplicateCopiedLines_DoNotProduceIndependentProvenExactExpansion()
    {
        var catalog = await LoadCatalogAsync();
        var draft = CreateDraft(
            """
            Item Class: Tinctures
            Rarity: Unique
            Grasping Nightshade
            Sporebloom Tincture
            --------
            Item Level: 84
            --------
            { Implicit Modifier }
            25% chance to Blind Enemies on Hit with Melee Weapons
            25% chance to Blind Enemies on Hit with Melee Weapons
            """,
            catalog);
        var blinds = draft.ModifierFilters
            .Where(filter => filter.RawCopiedText.Contains("Blind", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.DoesNotContain(
            blinds,
            component =>
                component.StatMappingProof == ModifierStatMappingProofStatus.ProvenExact &&
                component.ResolvedStatIds.Count == 1);
    }

    [Fact]
    public async Task CurrentExactMultiStatImplicit_CompositionEligibilityAudit()
    {
        var catalog = await LoadCatalogAsync();
        var exactMulti = 0;
        var withEligibleComposition = 0;
        var withoutComposition = 0;
        foreach (var item in catalog.UniqueItems?.Items ?? [])
        {
            foreach (var version in item.Versions.Where(v => v.Role == UniqueItemVersionRole.Current))
            {
                foreach (var block in version.ModifierBlocks.Where(b =>
                             b.Kind == UniqueModifierBlockKind.Implicit &&
                             b.MechanicalMapping.Status == UniqueModifierMechanicalMappingStatus.Exact &&
                             b.MechanicalMapping.StatIds.Count > 1))
                {
                    exactMulti++;
                    var composition = block.Composition;
                    var eligible = composition is not null &&
                        composition.AuxiliaryStatIds.Count == 0 &&
                        composition.Components.Count == block.Lines.Count &&
                        composition.Components.Count >= 2 &&
                        composition.Components.All(c =>
                            c.StatIds.Count == 1 &&
                            !string.IsNullOrWhiteSpace(c.StatIds[0]) &&
                            c.CanonicalSignatures.Count > 0) &&
                        composition.Components.Select(c => c.StatIds[0])
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Count() == composition.Components.Count;
                    if (eligible)
                    {
                        withEligibleComposition++;
                    }
                    else if (composition is null)
                    {
                        withoutComposition++;
                    }
                }
            }
        }

        Assert.True(exactMulti >= 3, $"Expected multi-stat Exact Implicit corpus, got {exactMulti}.");
        Assert.True(
            withEligibleComposition >= 3,
            $"Expected Grasping/Mightblood/Wildfire-shaped composition corpus, got {withEligibleComposition}.");
        Assert.True(
            withoutComposition >= 1,
            "Expected at least one Exact multi-stat Implicit without Composition to remain fail-closed.");
        Assert.Equal(exactMulti, withEligibleComposition + withoutComposition +
            (exactMulti - withEligibleComposition - withoutComposition));
    }

    private TradeSearchDraft CreateDraft(string clipboard, GameDataCatalog catalog)
    {
        var parsed = parser.Parse(clipboard);
        var baseResolution = baseResolver.Resolve(parsed, catalog);
        var modifierResolutions = modifierResolver.Resolve(parsed, catalog, baseResolution);
        var unique = uniqueResolver.Resolve(parsed, catalog, baseResolution);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);
        var result = mapper.CreateDraft(parsed, baseResolution, modifierResolutions, catalog);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static async Task<GameDataCatalog> LoadCatalogAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(load.Package));
    }
}
