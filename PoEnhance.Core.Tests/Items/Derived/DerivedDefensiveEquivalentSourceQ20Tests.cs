using PoEnhance.Core.Items.Derived;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.Derived;

public sealed class DerivedDefensiveEquivalentSourceQ20Tests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ParsedItemModifierCandidateResolver modifierResolver = new();
    private readonly TradeSearchDraftMapper mapper = new();
    private readonly DerivedWeaponPropertyCalculator calculator = new();

    private const string AbyssusClipboard = """
        Item Class: Helmets
        Rarity: Unique
        Abyssus
        Ezomyte Burgonet
        --------
        Quality: +0% (augmented)
        Armour: 772 (augmented)
        --------
        Requirements:
        Level: 60
        Str: 138
        --------
        Item Level: 84
        --------
        { Unique Modifier — Defences, Armour }
        120(100-120)% increased Armour
        { Unique Modifier — Attribute }
        +22(20-25) to all Attributes
        { Unique Modifier }
        45(40-50)% increased Physical Damage taken
        { Unique Modifier — Critical }
        +110(100-125)% to Melee Critical Strike Multiplier
        { Unique Modifier — Damage, Physical, Attack }
        Adds 40 to 60 Physical Damage to Attacks
        --------
        Corrupted
        """;

    private const string HrimnorResolveClipboard = """
        Item Class: Helmets
        Rarity: Unique
        Hrimnor's Resolve
        Samnite Helmet
        --------
        Quality: +0% (augmented)
        Armour: 659 (augmented)
        --------
        Requirements:
        Level: 55
        Str: 114
        --------
        Item Level: 80
        --------
        { Unique Modifier — Elemental, Cold, Resistance }
        +30% to Cold Resistance
        { Unique Modifier }
        100% chance to Avoid being Chilled or Frozen if you have used a Fire Skill Recently
        { Unique Modifier — Defences, Armour }
        108(100-120)% increased Armour
        10% increased Stun and Block Recovery
        { Unique Modifier — Damage, Elemental, Fire }
        50(40-60)% increased Fire Damage
        --------
        Corrupted
        """;

    private const string AsenathMarkClipboard = """
        Item Class: Helmets
        Rarity: Unique
        Asenath's Mark
        Iron Circlet
        --------
        Quality: +0% (augmented)
        Energy Shield: 54 (augmented)
        --------
        Requirements:
        Level: 8
        Int: 23
        --------
        Item Level: 80
        --------
        { Unique Modifier — Defences, Energy Shield }
        +39(30-50) to maximum Energy Shield
        12(10-15)% increased Stun and Block Recovery
        { Unique Modifier }
        30% increased Attack Speed when on Full Life
        { Unique Modifier — Critical }
        +(30-50)% to Critical Strike Multiplier with Attack Skills when on Full Life
        { Unique Modifier }
        25% chance to gain a Power Charge when you Stun with Melee Weapons
        """;

    [Fact]
    public async Task Abyssus_EquivalentSourceArmour_NormalizesToQ20Once()
    {
        var draft = await CreatePackageDraftAsync(AbyssusClipboard);
        var armourFilter = Assert.Single(
            draft.ModifierFilters,
            filter => filter.RawCopiedText.Contains("% increased Armour", StringComparison.Ordinal));
        Assert.True(armourFilter.IsEquivalentSourceSet);
        Assert.Equal(2, armourFilter.Sources.Count);

        var projected = DerivedWeaponModifierEffectProjector.Project(draft.ModifierFilters);
        var armourEffects = projected.Where(effect =>
            effect.ReviewedItemPropertySemantic?.Contributions.Any(contribution =>
                contribution.Targets.Contains(ItemPropertyTarget.Armour)) == true).ToArray();
        Assert.Single(armourEffects);
        Assert.Equal([120m], armourEffects[0].CanonicalNumericValues);
        Assert.Null(armourEffects[0].CanonicalizationUnsupportedReason);

        var armour = Assert.Single(draft.ItemProperties, property => property.Kind == TradeSearchItemPropertyKind.Armour);
        Assert.Equal(927m, armour.ObservedValue);
        Assert.Equal("Q20", armour.CalculationBasisLabel);
        Assert.Null(armour.DerivationUnsupportedReason);
    }

    [Fact]
    public async Task Abyssus_IndependentProjectionWouldDoubleCount_ButCreateDraftUsesCollapse()
    {
        var draft = await CreatePackageDraftAsync(AbyssusClipboard);
        var independent = DerivedWeaponModifierEffectProjector.ProjectSourcesIndependently(draft.ModifierFilters);
        var independentDefensive = calculator.CalculateDefensiveQ20(
            parser.Parse(AbyssusClipboard),
            Assert.Single(
                (await LoadCatalogAsync()).ItemBases,
                itemBase => itemBase.Id == "Metadata/Items/Armours/Helmets/HelmetStr9"),
            independent);
        var independentArmour = Assert.Single(
            independentDefensive.Properties,
            property => property.Target == ItemPropertyTarget.Armour);
        Assert.Equal(240m, independentArmour.LocalIncreasedPercent);
        Assert.False(independentArmour.IsQ20);
        Assert.Equal(772m, independentArmour.Value);

        var armour = Assert.Single(draft.ItemProperties, property => property.Kind == TradeSearchItemPropertyKind.Armour);
        Assert.Equal(927m, armour.ObservedValue);
        Assert.Equal("Q20", armour.CalculationBasisLabel);
    }

    [Fact]
    public async Task HrimnorResolve_SingleSourceArmour_RemainsQ20()
    {
        var draft = await CreatePackageDraftAsync(HrimnorResolveClipboard);
        var armour = Assert.Single(draft.ItemProperties, property => property.Kind == TradeSearchItemPropertyKind.Armour);
        Assert.Equal(791m, armour.ObservedValue);
        Assert.Equal("Q20", armour.CalculationBasisLabel);
        Assert.Null(armour.DerivationUnsupportedReason);
    }

    [Fact]
    public async Task AsenathMark_SingleSourceEnergyShield_RemainsQ20()
    {
        var draft = await CreatePackageDraftAsync(AsenathMarkClipboard);
        var energyShield = Assert.Single(
            draft.ItemProperties,
            property => property.Kind == TradeSearchItemPropertyKind.EnergyShield);
        Assert.Equal(65m, energyShield.ObservedValue);
        Assert.Equal("Q20", energyShield.CalculationBasisLabel);
        Assert.Null(energyShield.DerivationUnsupportedReason);
    }

    [Fact]
    public void SlinkBoots_EvasionQ20_StillNormalizesFromCalculatorContract()
    {
        var item = parser.Parse("""
            Item Class: Boots
            Rarity: Rare
            Cataclysm League
            Slink Boots
            --------
            Quality: +10% (augmented)
            Evasion Rating: 445 (augmented)
            --------
            Item Level: 84
            """);
        var result = calculator.CalculateDefensiveQ20(
            item,
            Base(evasion: (246, 283)),
            [
                Effect(ItemPropertyTarget.Evasion, ItemPropertyOperation.Added, 42),
                Effect(ItemPropertyTarget.Evasion, ItemPropertyOperation.IncreasedPercent, 34),
            ]);

        var evasion = Assert.Single(result.Properties);
        Assert.Equal(ItemPropertyTarget.Evasion, evasion.Target);
        Assert.Equal(486m, evasion.Value);
        Assert.Equal(260, evasion.ReconstructedBaseValue);
        Assert.True(evasion.IsQ20);
    }

    [Fact]
    public void EquivalentSource_AgreeingAlternatives_ContributeOnceToArmourQ20()
    {
        var semantic = ArmourIncreasedSemantic();
        var component = EquivalentComponent(
            semantic,
            ["local_physical_damage_reduction_rating_+%"],
            120m,
            Source("alt-a", ["local_physical_damage_reduction_rating_+%"], 120m, semantic),
            Source("alt-b", ["local_physical_damage_reduction_rating_+%"], 120m, semantic));

        var effect = Assert.Single(DerivedWeaponModifierEffectProjector.Project([component]));
        Assert.Null(effect.CanonicalizationUnsupportedReason);
        Assert.Equal([120m], effect.CanonicalNumericValues);

        var result = calculator.CalculateDefensiveQ20(
            parser.Parse(ArmourItem(772, quality: 0)),
            Base(armour: (346, 381)),
            [effect]);
        var armour = Assert.Single(result.Properties);
        Assert.Equal(120m, armour.LocalIncreasedPercent);
        Assert.Equal(351, armour.ReconstructedBaseValue);
        Assert.Equal(927m, armour.Value);
        Assert.True(armour.IsQ20);
    }

    [Fact]
    public void EquivalentSource_DifferingStatIds_FailClosedForArmourQ20()
    {
        var semantic = ArmourIncreasedSemantic();
        var component = EquivalentComponent(
            semantic,
            ["local_physical_damage_reduction_rating_+%"],
            120m,
            Source("alt-a", ["local_physical_damage_reduction_rating_+%"], 120m, semantic),
            Source("alt-b", ["local_evasion_rating_+%"], 120m, semantic));

        var effect = Assert.Single(DerivedWeaponModifierEffectProjector.Project([component]));
        Assert.NotNull(effect.CanonicalizationUnsupportedReason);

        var result = calculator.CalculateDefensiveQ20(
            parser.Parse(ArmourItem(772, quality: 0)),
            Base(armour: (346, 381)),
            [effect]);
        var armour = Assert.Single(result.Properties);
        Assert.False(armour.IsQ20);
        Assert.Equal(772m, armour.Value);
        Assert.Equal(effect.CanonicalizationUnsupportedReason, armour.UnsupportedReason);
    }

    [Fact]
    public void EquivalentSource_ConflictingValues_FailClosedForArmourQ20()
    {
        var semantic = ArmourIncreasedSemantic();
        var component = EquivalentComponent(
            semantic,
            ["local_physical_damage_reduction_rating_+%"],
            120m,
            Source("alt-a", ["local_physical_damage_reduction_rating_+%"], 120m, semantic),
            Source("alt-b", ["local_physical_damage_reduction_rating_+%"], 110m, semantic));

        var effect = Assert.Single(DerivedWeaponModifierEffectProjector.Project([component]));
        Assert.NotNull(effect.CanonicalizationUnsupportedReason);

        var result = calculator.CalculateDefensiveQ20(
            parser.Parse(ArmourItem(772, quality: 0)),
            Base(armour: (346, 381)),
            [effect]);
        var armour = Assert.Single(result.Properties);
        Assert.False(armour.IsQ20);
        Assert.Equal(772m, armour.Value);
        Assert.Equal(effect.CanonicalizationUnsupportedReason, armour.UnsupportedReason);
    }

    [Fact]
    public void MultipleIndependentLocalModifiers_StillSumForArmourQ20()
    {
        var result = calculator.CalculateDefensiveQ20(
            parser.Parse(ArmourItem(772, quality: 0)),
            Base(armour: (346, 381)),
            [
                Effect(ItemPropertyTarget.Armour, ItemPropertyOperation.IncreasedPercent, 60),
                Effect(ItemPropertyTarget.Armour, ItemPropertyOperation.IncreasedPercent, 60),
            ]);

        var armour = Assert.Single(result.Properties);
        Assert.Equal(120m, armour.LocalIncreasedPercent);
        Assert.Equal(351, armour.ReconstructedBaseValue);
        Assert.Equal(927m, armour.Value);
        Assert.True(armour.IsQ20);
        Assert.Equal(2, armour.ModifierContributions.Count);
    }

    [Fact]
    public void NonLocalArmourModifier_DoesNotContributeToQ20Reconstruction()
    {
        var result = calculator.CalculateDefensiveQ20(
            parser.Parse(ArmourItem(351, quality: 0)),
            Base(armour: (346, 381)),
            [
                Effect(ItemPropertyTarget.Armour, ItemPropertyOperation.IncreasedPercent, 120) with
                {
                    IsLocal = false,
                    ResolvedStatIds = ["physical_damage_reduction_rating_+%"],
                },
            ]);

        var armour = Assert.Single(result.Properties);
        Assert.False(armour.IsQ20);
        Assert.Equal(351m, armour.Value);
        Assert.NotNull(armour.UnsupportedReason);
        Assert.Equal(0m, armour.LocalIncreasedPercent);
    }

    [Fact]
    public async Task CurrentUniqueCorpus_EquivalentSourceLocalDefenceBlocks_AreCatalogued()
    {
        var catalog = await LoadCatalogAsync();
        var defenceStatFragments = new[]
        {
            "local_physical_damage_reduction_rating_+%",
            "local_base_physical_damage_reduction_rating",
            "local_evasion_rating_+%",
            "local_base_evasion_rating",
            "local_energy_shield_+%",
            "local_energy_shield",
            "local_armour_and_evasion_+%",
            "local_armour_and_energy_shield_+%",
            "local_evasion_and_energy_shield_+%",
            "local_armour_and_evasion_and_energy_shield_+%",
            "local_evasion_rating_and_energy_shield",
        };

        var matches = new List<(string Name, string Role, string Line, string Family, int AltCount, bool SafeCollapse)>();
        foreach (var identity in catalog.UniqueItems?.Items ?? [])
        {
            foreach (var version in identity.Versions.Where(version => version.Role == UniqueItemVersionRole.Current))
            {
                foreach (var block in version.ModifierBlocks)
                {
                    var mapping = block.MechanicalMapping;
                    if (mapping.Status != UniqueModifierMechanicalMappingStatus.EquivalentSourceSet)
                    {
                        continue;
                    }

                    var statIds = mapping.StatIds;
                    if (!statIds.Any(statId =>
                            defenceStatFragments.Any(fragment =>
                                statId.Contains(fragment, StringComparison.OrdinalIgnoreCase))))
                    {
                        continue;
                    }

                    var family = ClassifyDefenceFamily(statIds);
                    var altCount = mapping.ModifierIds.Count;
                    var safeCollapse = altCount >= 2 && AreEquivalentSourcesMechanicallyCompatible(
                        catalog,
                        mapping.ModifierIds);
                    matches.Add((
                        identity.CanonicalName ?? identity.Id ?? "<unnamed>",
                        version.Role.ToString(),
                        string.Join(" / ", block.Lines),
                        family,
                        altCount,
                        safeCollapse));
                }
            }
        }

        Assert.Contains(matches, match =>
            string.Equals(match.Name, "Abyssus", StringComparison.Ordinal) &&
            match.Family == "Armour" &&
            match.SafeCollapse);
        Assert.True(matches.Count >= 1, "Expected at least Abyssus-shaped equivalent defence blocks.");
        Assert.Equal(0, matches.Count(match => match.Family == "Other"));
        Assert.True(matches.Count(match => match.SafeCollapse) >= 200);
        Assert.True(matches.Count(match => !match.SafeCollapse) >= 1);
    }

    private async Task<TradeSearchDraft> CreatePackageDraftAsync(string clipboard)
    {
        var catalog = await LoadCatalogAsync();
        var parsed = parser.Parse(clipboard);
        var baseResolution = baseResolver.Resolve(parsed, catalog);
        var modifierResolutions = modifierResolver.Resolve(parsed, catalog, baseResolution);
        var result = mapper.CreateDraft(parsed, baseResolution, modifierResolutions, catalog);
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static async Task<GameDataCatalog> LoadCatalogAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess, string.Join(", ", load.Diagnostics.Select(diagnostic => diagnostic.Code)));
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(load.Package));
    }

    private static bool AreEquivalentSourcesMechanicallyCompatible(
        GameDataCatalog catalog,
        IReadOnlyList<string> modifierIds)
    {
        var modifiers = modifierIds
            .Select(id => catalog.Modifiers.FirstOrDefault(modifier =>
                string.Equals(modifier.Id, id, StringComparison.Ordinal)))
            .Where(modifier => modifier is not null)
            .Cast<ModifierDefinition>()
            .ToArray();
        if (modifiers.Length != modifierIds.Count || modifiers.Length < 2)
        {
            return false;
        }

        var first = NormalizeModifierMechanics(modifiers[0]);
        return modifiers.Skip(1).All(modifier => NormalizeModifierMechanics(modifier) == first);
    }

    private static string NormalizeModifierMechanics(ModifierDefinition modifier)
    {
        var stats = string.Join("|", modifier.Stats
            .OrderBy(stat => stat.Index)
            .Select(stat => $"{stat.StatId}:{stat.MinValue}:{stat.MaxValue}"));
        return stats;
    }

    private static string ClassifyDefenceFamily(IReadOnlyList<string> statIds)
    {
        var joined = string.Join(" ", statIds);
        var armour = joined.Contains("physical_damage_reduction_rating", StringComparison.OrdinalIgnoreCase) ||
                     joined.Contains("armour", StringComparison.OrdinalIgnoreCase);
        var evasion = joined.Contains("evasion", StringComparison.OrdinalIgnoreCase);
        var energyShield = joined.Contains("energy_shield", StringComparison.OrdinalIgnoreCase);
        var count = (armour ? 1 : 0) + (evasion ? 1 : 0) + (energyShield ? 1 : 0);
        if (count > 1)
        {
            return "Hybrid";
        }

        if (armour)
        {
            return "Armour";
        }

        if (evasion)
        {
            return "Evasion";
        }

        if (energyShield)
        {
            return "EnergyShield";
        }

        return "Other";
    }

    private static ItemPropertySemanticDescriptor ArmourIncreasedSemantic() => new()
    {
        Id = "item.armour.increased-percent.local",
        OrderedStatIds = ["local_physical_damage_reduction_rating_+%"],
        Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
        Contributions =
        [
            new ItemPropertyContribution
            {
                Operation = ItemPropertyOperation.IncreasedPercent,
                Targets = [ItemPropertyTarget.Armour],
            },
        ],
    };

    private static ResolvedSearchComponent EquivalentComponent(
        ItemPropertySemanticDescriptor semantic,
        IReadOnlyList<string> resolvedStatIds,
        decimal value,
        params SearchComponentSourceProvenance[] sources) => new()
    {
        ComponentId = "modifier:0:0",
        ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
        Locality = ModifierLocality.Local,
        StatMappingProof = ModifierStatMappingProofStatus.WholeVector,
        ResolvedStatIds = resolvedStatIds,
        CanonicalNumericValues = [value],
        ReviewedItemPropertySemantic = semantic,
        IsEquivalentSourceSet = true,
        Sources = sources,
    };

    private static SearchComponentSourceProvenance Source(
        string id,
        IReadOnlyList<string> statIds,
        decimal value,
        ItemPropertySemanticDescriptor semantic) => new()
    {
        ComponentId = "modifier:0:0",
        ResolvedModifierId = id,
        Locality = ModifierLocality.Local,
        StatMappingProof = ModifierStatMappingProofStatus.WholeVector,
        ResolvedStatIds = statIds,
        CanonicalNumericValues = [value],
        ReviewedItemPropertySemantic = semantic,
    };

    private static DerivedWeaponModifierEffect Effect(
        ItemPropertyTarget target,
        ItemPropertyOperation operation,
        decimal value) => new()
    {
        ComponentId = Guid.NewGuid().ToString("N"),
        SourceModifierIndex = 0,
        ResolvedModifierId = "test.mod",
        IsExactlyResolved = true,
        IsLocal = true,
        HasProvenStatAssociation = true,
        ResolvedStatIds = ["local_test"],
        CanonicalNumericValues = [value],
        ReviewedItemPropertySemantic = new ItemPropertySemanticDescriptor
        {
            Id = "test.semantic",
            Applicability = ItemPropertyApplicability.UnconditionalDisplayedLocal,
            Contributions = [new ItemPropertyContribution { Targets = [target], Operation = operation }],
        },
    };

    private static ItemBaseRecord Base(
        (int Min, int Max)? armour = null,
        (int Min, int Max)? evasion = null,
        (int Min, int Max)? energyShield = null) => new()
    {
        Id = "base",
        DefenceProperties = new ItemBaseDefenceProperties
        {
            ArmourMinimum = armour?.Min,
            ArmourMaximum = armour?.Max,
            EvasionRatingMinimum = evasion?.Min,
            EvasionRatingMaximum = evasion?.Max,
            EnergyShieldMinimum = energyShield?.Min,
            EnergyShieldMaximum = energyShield?.Max,
            Sources = [new GameDataSourceReference { SourceId = "repoe", ExternalId = "base" }],
        },
    };

    private static string ArmourItem(decimal armour, int quality) => $$"""
        Item Class: Helmets
        Rarity: Unique
        Test Helmet
        Ezomyte Burgonet
        --------
        Quality: +{{quality}}%
        Armour: {{armour}} (augmented)
        --------
        Item Level: 84
        """;
}
