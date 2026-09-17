using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class UniqueCatalogImplicitBlockConsumptionTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver modifierResolver = new();
    private readonly ParsedUniqueItemResolver uniqueResolver = new();
    private readonly TradeSearchDraftMapper mapper = new();

    private const string BattleWithinFullClipboard = """
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
    public async Task BattleWithin_RageImplicit_ConsumesExactUniqueCatalogImplicitBlock()
    {
        var catalog = await LoadCatalogAsync();
        var draft = CreateDraft(BattleWithinFullClipboard, catalog);
        var rage = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Gain 3 Rage", StringComparison.Ordinal));

        Assert.Equal(ParsedModifierKind.Implicit, rage.ParsedKind);
        Assert.Equal(ParsedModifierKind.Implicit, rage.ResolvedSourceKind);
        Assert.False(rage.IsBaseImplicit);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, rage.ResolutionStatus);
        Assert.Equal("TinctureRageOnHitImplicit1", rage.ResolvedModifierId);
        Assert.Equal(["gain_x_rage_on_hit_with_tinctured_weapons"], rage.ResolvedStatIds);
        Assert.Equal(3m, rage.RequestedMinimum);
        Assert.Null(rage.RequestedMaximum);
        Assert.Equal(
            ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
            rage.UniqueCatalogImplicitConsumptionReason);
        Assert.True(rage.IsSearchable, rage.NotSearchableReason);
        Assert.Null(rage.UniqueResolutionDiagnosticCode);
        Assert.DoesNotContain(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Gain 3 Rage", StringComparison.Ordinal) &&
                      filter.ResolvedSourceKind == ParsedModifierKind.Unique);
    }

    [Fact]
    public async Task BattleWithin_CullingRarityManaBurn_RemainUniqueAndUnchanged()
    {
        var catalog = await LoadCatalogAsync();
        var draft = CreateDraft(BattleWithinFullClipboard, catalog);

        var mana = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Mana Burn over time", StringComparison.Ordinal));
        Assert.Equal(ParsedModifierKind.Unique, mana.ResolvedSourceKind);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, mana.ResolutionStatus);
        Assert.True(mana.HasExactUniqueSourceProvenance);
        Assert.Equal(2, mana.ResolvedStatIds.Count);

        var culling = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Culling Strike", StringComparison.Ordinal));
        Assert.Equal(ParsedModifierKind.Unique, culling.ResolvedSourceKind);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, culling.ResolutionStatus);
        Assert.True(culling.HasExactUniqueSourceProvenance);

        var rarity = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("Rarity of Items found", StringComparison.Ordinal));
        Assert.Equal(ParsedModifierKind.Unique, rarity.ResolvedSourceKind);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, rarity.ResolutionStatus);
        Assert.Equal(4m, rarity.RequestedMinimum);
        Assert.Null(rarity.RequestedMaximum);
        Assert.True(rarity.HasExactUniqueSourceProvenance);
    }

    [Fact]
    public async Task GraspingNightshade_BlindImplicit_ConsumesExactUniqueCatalogImplicitBlock()
    {
        var catalog = await LoadCatalogAsync();
        var control = RequireExactCurrentImplicitControl(catalog, "Grasping Nightshade");
        Assert.Equal("TinctureChanceToBlindImplicit1", control.ModifierId);
        Assert.Equal(2, control.DisplayLines.Count);

        var draft = CreateDraft(
            BuildClipboard(
                control,
                """
                25% chance to Blind Enemies on Hit with Melee Weapons
                30% increased Effect of Blind from Melee Weapons
                """),
            catalog);
        var components = draft.ModifierFilters
            .Where(filter =>
                filter.RawCopiedText.Contains("Blind", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        Assert.Equal(2, components.Length);
        // Exact composition proves independent line↔StatId ownership. Native base ownership is not
        // upgraded without recognition snapshots required by the provider base-implicit gate.
        Assert.All(components, component =>
        {
            Assert.Equal(ParsedModifierKind.Implicit, component.ParsedKind);
            Assert.Equal(ParsedModifierKind.Implicit, component.ResolvedSourceKind);
            Assert.False(component.IsBaseImplicit);
            Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
            Assert.Equal(ModifierStatMappingProofStatus.ProvenExact, component.StatMappingProof);
            Assert.Equal(
                ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
                component.UniqueCatalogImplicitConsumptionReason);
            Assert.Equal("TinctureChanceToBlindImplicit1", component.ResolvedModifierId);
            Assert.Single(component.ResolvedStatIds);
            Assert.True(component.IsSearchable, component.NotSearchableReason);
        });
        Assert.Contains(
            components,
            component => component.ResolvedStatIds.Contains(
                "chance_to_blind_on_hit_%_with_tinctured_weapons"));
        Assert.Contains(
            components,
            component => component.ResolvedStatIds.Contains("blind_effect_+%_with_tinctured_weapons"));
    }

    [Fact]
    public async Task Andvarius_RarityImplicit_PrefersProvenNativeBaseImplicitOverCatalogDuplicate()
    {
        var catalog = await LoadCatalogAsync();
        var control = RequireExactCurrentImplicitControl(catalog, "Andvarius");
        Assert.Equal("ItemFoundRarityIncreaseImplicitRing1", control.ModifierId);

        var draft = CreateDraft(
            BuildClipboard(control, "12% increased Rarity of Items found"),
            catalog);
        var component = Assert.Single(draft.ModifierFilters);

        Assert.Equal(ParsedModifierKind.Implicit, component.ParsedKind);
        Assert.Equal(ParsedModifierKind.Implicit, component.ResolvedSourceKind);
        Assert.True(component.IsBaseImplicit);
        Assert.NotNull(component.BaseImplicitProvenance);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Equal(control.ModifierId, component.ResolvedModifierId);
        Assert.Equal(control.StatIds, component.ResolvedStatIds);
        Assert.Equal(12m, component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.Null(component.UniqueCatalogImplicitConsumptionReason);
        Assert.Empty(component.UniqueCatalogBlockIds);
        Assert.True(component.IsSearchable, component.NotSearchableReason);
    }

    [Fact]
    public async Task AbberathHorn_CurrentExactImplicit_PrefersProvenNativeBaseImplicit()
    {
        var catalog = await LoadCatalogAsync();
        var control = RequireExactCurrentImplicitControl(catalog, "Abberath's Horn");
        var draft = CreateDraft(
            BuildClipboard(control, "Adds 2 to 3 Fire Damage to Spells and Attacks"),
            catalog);
        var component = Assert.Single(draft.ModifierFilters);

        Assert.Equal(ParsedModifierKind.Implicit, component.ResolvedSourceKind);
        Assert.True(component.IsBaseImplicit);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Equal(control.ModifierId, component.ResolvedModifierId);
        Assert.Equal(control.StatIds, component.ResolvedStatIds);
        Assert.Null(component.UniqueResolutionDiagnosticCode);
        Assert.Null(component.UniqueCatalogImplicitConsumptionReason);
    }

    [Fact]
    public async Task ParsedUniqueLine_DoesNotMatchCatalogImplicitBlock()
    {
        var catalog = await LoadCatalogAsync();
        var control = RequireExactCurrentImplicitControl(catalog, "The Battle Within");
        var clipboard = $$"""
            Item Class: Tinctures
            Rarity: Unique
            The Battle Within
            Oakbranch Tincture
            --------
            Item Level: 84
            --------
            { Unique Modifier }
            {{control.DisplayLine}}
            """;
        var draft = CreateDraft(clipboard, catalog);
        var component = Assert.Single(draft.ModifierFilters);
        Assert.Equal(ParsedModifierKind.Unique, component.ParsedKind);
        Assert.NotEqual(
            ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
            component.UniqueCatalogImplicitConsumptionReason);
        Assert.False(component.HasExactUniqueSourceProvenance);
    }

    [Fact]
    public async Task ParsedImplicitLine_DoesNotMatchOrdinaryUniqueBlock()
    {
        var catalog = await LoadCatalogAsync();
        var clipboard = """
            Item Class: Tinctures
            Rarity: Unique
            The Battle Within
            Oakbranch Tincture
            --------
            Item Level: 84
            --------
            { Implicit Modifier }
            Melee Weapon Attacks have Culling Strike
            """;
        var draft = CreateDraft(clipboard, catalog);
        var component = Assert.Single(draft.ModifierFilters);
        Assert.Equal(ParsedModifierKind.Implicit, component.ParsedKind);
        Assert.Null(component.UniqueCatalogImplicitConsumptionReason);
        Assert.NotEqual("TinctureCullingStrikeUnique__1", component.ResolvedModifierId);
        Assert.False(component.HasExactUniqueSourceProvenance);
    }

    [Fact]
    public async Task HistoricalOnlyImplicitText_DoesNotOverrideCurrent()
    {
        var catalog = await LoadCatalogAsync();
        var control = RequireExactCurrentImplicitControl(catalog, "Abberath's Horn");
        var clipboard = BuildClipboard(control, "(9-12)% increased Spell Damage");
        var draft = CreateDraft(clipboard, catalog);
        var component = Assert.Single(draft.ModifierFilters);

        Assert.Equal(ParsedModifierKind.Implicit, component.ParsedKind);
        Assert.NotEqual(control.ModifierId, component.ResolvedModifierId);
        Assert.NotEqual(
            ParsedUniqueItemResolver.UniqueCatalogImplicitBlockConsumptionReason,
            component.UniqueCatalogImplicitConsumptionReason);
        Assert.NotEqual(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
    }

    [Fact]
    public async Task NativeBaseAndUniqueCatalogImplicit_DisagreeMechanically_FailClosed()
    {
        var catalog = await LoadCatalogAsync();
        var collision = FindMechanicallyDifferentNativeCatalogCollision(catalog);
        Assert.False(
            collision is null,
            "Need one Current Unique where catalog Implicit and native base implicit both match the copied line with different mechanics.");

        var draft = CreateDraft(
            BuildClipboard(
                new ExactImplicitControl(
                    collision!.Value.Name,
                    collision.Value.BaseType,
                    InferItemClass(collision.Value.BaseType),
                    collision.Value.CatalogLines,
                    collision.Value.CatalogModifierId,
                    collision.Value.CatalogStatIds),
                string.Join(Environment.NewLine, collision.Value.CatalogLines)),
            catalog);
        var component = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains(
                collision.Value.CatalogLines[0].Split('(')[0].Trim(),
                StringComparison.OrdinalIgnoreCase) ||
                filter.RawCopiedText.Contains(
                    collision.Value.CatalogLines[0],
                    StringComparison.OrdinalIgnoreCase));

        Assert.Equal(ParsedModifierKind.Implicit, component.ParsedKind);
        Assert.False(component.IsBaseImplicit);
        Assert.NotEqual(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Equal("UNIQUE_CATALOG_IMPLICIT_BASE_IMPLICIT_CONFLICT", component.UniqueResolutionDiagnosticCode);
        Assert.Empty(component.ResolvedStatIds);
    }

    [Fact]
    public async Task UnsupportedCatalogImplicit_RemainsUnsupported()
    {
        var catalog = await LoadCatalogAsync();
        var unsupported = FindCurrentImplicitBlocks(catalog)
            .FirstOrDefault(entry =>
                entry.Block.MechanicalMapping.Status ==
                    UniqueModifierMechanicalMappingStatus.Unsupported &&
                entry.Block.Lines.Count == 1);
        Assert.False(unsupported.Equals(default), "Need one Current Unsupported Implicit block.");

        var draft = CreateDraft(
            BuildClipboard(
                new ExactImplicitControl(
                    unsupported.Name,
                    unsupported.BaseType,
                    InferItemClass(unsupported.BaseType),
                    unsupported.Block.Lines.ToArray(),
                    unsupported.Block.MechanicalMapping.ModifierIds.FirstOrDefault() ?? "none",
                    unsupported.Block.MechanicalMapping.StatIds.ToArray()),
                string.Join(Environment.NewLine, unsupported.Block.Lines)),
            catalog);
        var component = Assert.Single(draft.ModifierFilters);
        Assert.Equal(ParsedModifierKind.Implicit, component.ParsedKind);
        Assert.NotEqual(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Empty(component.ResolvedStatIds);
    }

    [Fact]
    public async Task AmbiguousCatalogImplicit_RemainsAmbiguousOrUnresolved()
    {
        var catalog = await LoadCatalogAsync();
        var ambiguous = FindCurrentImplicitBlocks(catalog)
            .FirstOrDefault(entry =>
                entry.Block.MechanicalMapping.Status ==
                    UniqueModifierMechanicalMappingStatus.Ambiguous &&
                entry.Block.Lines.Count == 1);
        if (ambiguous.Equals(default))
        {
            // Package may have zero Ambiguous Current Implicit blocks; corpus audit still covers count.
            return;
        }

        var draft = CreateDraft(
            BuildClipboard(
                new ExactImplicitControl(
                    ambiguous.Name,
                    ambiguous.BaseType,
                    InferItemClass(ambiguous.BaseType),
                    ambiguous.Block.Lines.ToArray(),
                    ambiguous.Block.MechanicalMapping.ModifierIds.FirstOrDefault() ?? "none",
                    ambiguous.Block.MechanicalMapping.StatIds.ToArray()),
                string.Join(Environment.NewLine, ambiguous.Block.Lines)),
            catalog);
        var component = Assert.Single(draft.ModifierFilters);
        Assert.Equal(ParsedModifierKind.Implicit, component.ParsedKind);
        Assert.NotEqual(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Empty(component.ResolvedStatIds);
    }

    [Fact]
    public async Task CurrentUniqueImplicitCorpus_IsAudited()
    {
        var catalog = await LoadCatalogAsync();
        var currentImplicit = FindCurrentImplicitBlocks(catalog).ToArray();

        var exact = currentImplicit.Count(entry =>
            entry.Block.MechanicalMapping.Status == UniqueModifierMechanicalMappingStatus.Exact);
        var equivalent = currentImplicit.Count(entry =>
            entry.Block.MechanicalMapping.Status ==
                UniqueModifierMechanicalMappingStatus.EquivalentSourceSet);
        var unsupported = currentImplicit.Count(entry =>
            entry.Block.MechanicalMapping.Status == UniqueModifierMechanicalMappingStatus.Unsupported);
        var ambiguous = currentImplicit.Count(entry =>
            entry.Block.MechanicalMapping.Status == UniqueModifierMechanicalMappingStatus.Ambiguous);

        Assert.True(currentImplicit.Length > 0);
        Assert.True(exact > 0);
        Assert.Equal(
            currentImplicit.Length,
            exact + equivalent + unsupported + ambiguous);
        Assert.Contains(
            currentImplicit,
            entry => string.Equals(entry.Name, "The Battle Within", StringComparison.Ordinal) &&
                     entry.Block.MechanicalMapping.ModifierIds.Contains("TinctureRageOnHitImplicit1"));
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

    private static (
        string Name,
        string BaseType,
        IReadOnlyList<string> CatalogLines,
        string CatalogModifierId,
        IReadOnlyList<string> CatalogStatIds)? FindMechanicallyDifferentNativeCatalogCollision(
        GameDataCatalog catalog)
    {
        foreach (var entry in FindCurrentImplicitBlocks(catalog))
        {
            if (entry.Block.Lines.Count == 0 ||
                entry.Block.MechanicalMapping.Status is not (
                    UniqueModifierMechanicalMappingStatus.Exact or
                    UniqueModifierMechanicalMappingStatus.EquivalentSourceSet) ||
                entry.Block.MechanicalMapping.StatIds.Count == 0)
            {
                continue;
            }

            var baseItem = catalog.FindItemBasesByExactName(entry.BaseType).FirstOrDefault()
                ?? catalog.ItemBases.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, entry.BaseType, StringComparison.OrdinalIgnoreCase));
            if (baseItem?.ImplicitModifierIds.Count is not > 0)
            {
                continue;
            }

            var catalogStatKey = string.Join(
                '\u001f',
                entry.Block.MechanicalMapping.StatIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
            foreach (var implicitId in baseItem.ImplicitModifierIds)
            {
                var native = catalog.FindModifiersById(implicitId).FirstOrDefault();
                if (native is null || string.IsNullOrWhiteSpace(native.Id))
                {
                    continue;
                }

                var nativeStatKey = string.Join(
                    '\u001f',
                    native.Stats
                        .Select(stat => stat.StatId)
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Select(id => id!)
                        .OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
                if (nativeStatKey.Length == 0)
                {
                    continue;
                }

                var sameStats = string.Equals(
                    nativeStatKey,
                    catalogStatKey,
                    StringComparison.OrdinalIgnoreCase);
                var sharesModifier = entry.Block.MechanicalMapping.ModifierIds.Contains(
                    native.Id,
                    StringComparer.OrdinalIgnoreCase);
                if (sameStats && sharesModifier)
                {
                    continue;
                }

                // Both candidates must be able to match the same copied catalog lines.
                if (!NativeBaseImplicitMatchesCatalogLines(native, entry.Block.Lines, catalog))
                {
                    continue;
                }

                return (
                    entry.Name,
                    entry.BaseType,
                    entry.Block.Lines.ToArray(),
                    entry.Block.MechanicalMapping.ModifierIds.FirstOrDefault() ?? native.Id,
                    entry.Block.MechanicalMapping.StatIds.ToArray());
            }
        }

        return null;
    }

    private static bool NativeBaseImplicitMatchesCatalogLines(
        ModifierDefinition native,
        IReadOnlyList<string> catalogLines,
        GameDataCatalog catalog)
    {
        var matcher = new ModifierTextSignatureMatcher();
        var match = matcher.Match(native, catalog, catalogLines);
        return match.Outcome == ModifierTextSignatureMatchOutcome.Match;
    }

    private static ExactImplicitControl RequireExactCurrentImplicitControl(
        GameDataCatalog catalog,
        string name)
    {
        var entry = FindCurrentImplicitBlocks(catalog)
            .Where(candidate =>
                string.Equals(candidate.Name, name, StringComparison.Ordinal) &&
                candidate.Block.MechanicalMapping.Status ==
                    UniqueModifierMechanicalMappingStatus.Exact &&
                candidate.Block.MechanicalMapping.ModifierIds.Count == 1 &&
                candidate.Block.MechanicalMapping.StatIds.Count > 0 &&
                candidate.Block.Lines.Count > 0)
            .Select(candidate => new ExactImplicitControl(
                candidate.Name,
                candidate.BaseType,
                InferItemClass(candidate.BaseType),
                candidate.Block.Lines.ToArray(),
                candidate.Block.MechanicalMapping.ModifierIds[0],
                candidate.Block.MechanicalMapping.StatIds.ToArray()))
            .SingleOrDefault();
        Assert.False(entry is null, $"Missing Exact Current Implicit control for {name}.");
        return entry!;
    }

    private static IEnumerable<(
        string Name,
        string BaseType,
        UniqueModifierBlock Block)> FindCurrentImplicitBlocks(GameDataCatalog catalog)
    {
        foreach (var identity in catalog.UniqueItems?.Items ?? [])
        {
            foreach (var version in identity.Versions.Where(version =>
                         version.Role == UniqueItemVersionRole.Current))
            {
                foreach (var block in version.ModifierBlocks.Where(block =>
                             block.Kind == UniqueModifierBlockKind.Implicit))
                {
                    yield return (
                        identity.CanonicalName ?? identity.Id ?? "<unnamed>",
                        version.BaseType ?? identity.BaseTypeEvidence.FirstOrDefault() ?? "Unknown Base",
                        block);
                }
            }
        }
    }

    private static string InferItemClass(string? baseType) => baseType switch
    {
        not null when baseType.Contains("Tincture", StringComparison.OrdinalIgnoreCase) => "Tinctures",
        not null when baseType.Contains("Ring", StringComparison.OrdinalIgnoreCase) => "Rings",
        not null when baseType.Contains("Amulet", StringComparison.OrdinalIgnoreCase) => "Amulets",
        not null when baseType.Contains("Belt", StringComparison.OrdinalIgnoreCase) => "Belts",
        not null when baseType.Contains("Jewel", StringComparison.OrdinalIgnoreCase) => "Jewels",
        not null when baseType.Contains("Wand", StringComparison.OrdinalIgnoreCase) ||
                      baseType.Contains("Horn", StringComparison.OrdinalIgnoreCase) => "Wands",
        not null when baseType.Contains("Shield", StringComparison.OrdinalIgnoreCase) => "Shields",
        not null when baseType.Contains("Claw", StringComparison.OrdinalIgnoreCase) ||
                      baseType.Contains("Ripper", StringComparison.OrdinalIgnoreCase) => "Claws",
        _ => "Rings",
    };

    private static string BuildClipboard(ExactImplicitControl control, string? displayLines = null) => $$"""
        Item Class: {{control.ItemClass}}
        Rarity: Unique
        {{control.Name}}
        {{control.BaseType}}
        --------
        Item Level: 84
        --------
        { Implicit Modifier }
        {{displayLines ?? string.Join(Environment.NewLine, control.DisplayLines)}}
        """;

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

    private sealed record ExactImplicitControl(
        string Name,
        string BaseType,
        string ItemClass,
        IReadOnlyList<string> DisplayLines,
        string ModifierId,
        IReadOnlyList<string> StatIds)
    {
        public string DisplayLine => DisplayLines[0];
    }
}
