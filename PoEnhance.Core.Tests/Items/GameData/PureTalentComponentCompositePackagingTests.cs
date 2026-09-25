using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// A.5.48 — Pure Talent component-composite Exact packaging (no invented ModId).
/// </summary>
public sealed class PureTalentComponentCompositePackagingTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver modifierResolver = new();
    private readonly ParsedUniqueItemResolver uniqueResolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    private const string ExpectedDataVersion =
        "3.29.1.2.10-unique-component-composite-translation";

    private static readonly string[] ExpectedComponentModIds =
    [
        "StarterPassiveTreeJewelUnique__1_",
        "StarterPassiveTreeJewelUnique__2",
        "StarterPassiveTreeJewelUnique__3",
        "StarterPassiveTreeJewelUnique__4",
        "StarterPassiveTreeJewelUnique__5",
        "StarterPassiveTreeJewelUnique__6",
        "StarterPassiveTreeJewelUnique__7",
    ];

    private static readonly string[] ExpectedStatIds =
    [
        "local_unique_jewel_melee_skills_area_of_effect_+%_with_passive_tree_connected_to_marauder_start",
        "local_unique_jewel_life_leech_from_attack_damage_permyriad_with_passive_tree_connected_to_duelist_start",
        "local_unique_jewel_movement_speed_+%_with_passive_tree_connected_to_ranger_start",
        "local_unique_jewel_additional_critical_strike_chance_permyriad_with_passive_tree_connected_to_shadow_start",
        "local_unique_jewel_mana_regeneration_rate_per_minute_%_with_passive_tree_connected_to_witch_start",
        "local_unique_jewel_elemental_penetration_%_with_passive_tree_connected_to_templar_start",
        "local_unique_jewel_additional_all_attributes_with_passive_tree_connected_to_scion_start",
    ];

    private const string PureTalentClipboard = """
        Item Class: Jewels
        Rarity: Unique
        Pure Talent
        Viridian Jewel
        --------
        Item Level: 84
        --------
        { Unique Modifier }
        While your Passive Skill Tree connects to a class' starting location, you gain:
        Marauder: Melee Skills have 25% increased Area of Effect
        Duelist: 1% of Attack Damage Leeched as Life
        Ranger: 7% increased Movement Speed
        Shadow: +0.5% to Critical Strike Chance
        Witch: 0.5% of Mana Regenerated per second
        Templar: Damage Penetrates 5% Elemental Resistances
        Scion: +25 to All Attributes
        """;

    [Fact]
    public async Task PackagedPureTalent_Current_IsExactComponentComposite()
    {
        var package = await LoadPackageAsync();
        var item = Assert.Single(
            package.UniqueItems!.Items,
            candidate => string.Equals(candidate.CanonicalName, "Pure Talent", StringComparison.Ordinal));
        var version = Assert.Single(
            item.Versions,
            candidate => candidate.Role == UniqueItemVersionRole.Current);

        var composite = Assert.Single(version.ModifierBlocks);
        Assert.Equal(8, composite.Lines.Count);
        Assert.Equal(
            "While your Passive Skill Tree connects to a class' starting location, you gain:",
            composite.Lines[0]);
        Assert.Equal(UniqueModifierMechanicalMappingStatus.Exact, composite.MechanicalMapping.Status);
        Assert.Null(composite.MechanicalMapping.DiagnosticCode);
        Assert.Null(composite.Composition);
        Assert.Equal(ExpectedComponentModIds.OrderBy(id => id, StringComparer.Ordinal),
            composite.MechanicalMapping.ModifierIds);
        Assert.Equal(ExpectedStatIds, composite.MechanicalMapping.StatIds);
        Assert.Contains(
            "source-component-composite-translation",
            composite.MechanicalMapping.Provenance!.ResolutionReasons);
        Assert.DoesNotContain(
            composite.MechanicalMapping.ModifierIds,
            id => id.Contains('|', StringComparison.Ordinal) ||
                id.Contains("composite", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(composite.SourceObservationIds);
    }

    [Fact]
    public async Task PackagedReplicaPureTalent_RemainsUnsupportedWithoutUnsafeSynonyms()
    {
        var package = await LoadPackageAsync();
        var item = Assert.Single(
            package.UniqueItems!.Items,
            candidate => string.Equals(
                candidate.CanonicalName,
                "Replica Pure Talent",
                StringComparison.Ordinal));

        Assert.All(item.Versions, version =>
        {
            Assert.DoesNotContain(
                version.ModifierBlocks,
                block => block.MechanicalMapping.Status == UniqueModifierMechanicalMappingStatus.Exact &&
                    block.MechanicalMapping.Provenance?.ResolutionReasons.Contains(
                        "source-component-composite-translation") == true);
            Assert.Contains(
                version.ModifierBlocks,
                block => block.MechanicalMapping.Status ==
                    UniqueModifierMechanicalMappingStatus.Unsupported);
        });
    }

    [Fact]
    public async Task PackagedMightAndInfluence_RemainsMutuallyExclusiveVersionsWithZeroAxes()
    {
        var package = await LoadPackageAsync();
        var item = Assert.Single(
            package.UniqueItems!.Items,
            candidate => string.Equals(
                candidate.CanonicalName,
                "Might and Influence",
                StringComparison.Ordinal));

        Assert.True(item.Versions.Count >= 2);
        Assert.All(item.Versions, version => Assert.Empty(version.OptionAxes));
        Assert.Contains(item.Versions, version => version.Role == UniqueItemVersionRole.Current);
        Assert.Contains(item.Versions, version => version.Role == UniqueItemVersionRole.Historical);
    }

    [Fact]
    public async Task PureTalentClipboard_ConsumesExactCompositeProvenance()
    {
        var catalog = await LoadCatalogAsync();
        var draft = CreateDraft(PureTalentClipboard, catalog);
        var filter = Assert.Single(draft.ModifierFilters);
        Assert.Equal(ParsedModifierKind.Unique, filter.ResolvedSourceKind);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, filter.ResolutionStatus);
        Assert.Null(filter.ResolvedModifierId);
        Assert.Equal(ExpectedStatIds, filter.ResolvedStatIds);
        Assert.True(filter.HasExactUniqueSourceProvenance);
        Assert.Null(filter.UniqueResolutionDiagnosticCode);
        Assert.True(filter.IsSearchable, filter.NotSearchableReason);
        Assert.Contains('\n', filter.RawCopiedText);
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

    private static async Task<GameDataPackage> LoadPackageAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess, string.Join(", ", load.Diagnostics.Select(d => d.Code)));
        Assert.Equal(4, load.Package!.Manifest.SchemaVersion);
        Assert.Equal(ExpectedDataVersion, load.Package.Manifest.DataVersion);
        return load.Package;
    }

    private static async Task<GameDataCatalog> LoadCatalogAsync()
    {
        var package = await LoadPackageAsync();
        return GameDataCatalog.FromPackage(await LoadPackageAsync());
    }
}
