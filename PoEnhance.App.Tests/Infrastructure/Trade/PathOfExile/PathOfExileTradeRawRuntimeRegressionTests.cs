using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeRawRuntimeRegressionTests
{
    private static readonly Lazy<GameDataCatalog> GameData = new(LoadGameData);
    private static readonly PathOfExileTradeStatCatalog TradeCatalog = CreateTradeCatalog();
    private static readonly Lazy<PathOfExileTradeStatCatalog> OfficialTradeCatalog =
        new(LoadOfficialTradeCatalog);
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();
    private static readonly PathOfExileTradeItemPropertyResolver ItemPropertyResolver = new();
    private static readonly MethodInfo InteractionReadyMethod = typeof(PriceCheckerSearchController)
        .GetMethod("IsModifierInteractionReady", BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(nameof(PriceCheckerSearchController), "IsModifierInteractionReady");
    private static readonly MethodInfo StaticModifierLabelMethod = typeof(PriceCheckerSearchController)
        .GetMethod("StaticModifierLabel", BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(nameof(PriceCheckerSearchController), "StaticModifierLabel");
    private static readonly MethodInfo ModifierAvailabilityStatusMethod = typeof(PriceCheckerSearchController)
        .GetMethod("ModifierAvailabilityStatus", BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(nameof(PriceCheckerSearchController), "ModifierAvailabilityStatus");

    [Fact]
    public void ResolveRawCopiedItem_DragonfangMinimumFrenzy_IsSelectableWithEquivalentSourceProvenance()
    {
        var runtime = Resolve(DragonfangText);
        var parsedModifier = Assert.Single(runtime.Parsed.Modifiers);
        var sourceResolution = Assert.Single(runtime.SourceResolutions);
        var component = FindComponent(runtime.ProviderDraft, "+1 to Minimum Frenzy Charges");

        Assert.Equal(ParsedImplicitModifierOrigin.Corrupted, parsedModifier.ImplicitOrigin);
        Assert.Equal(ModifierGenerationType.Corrupted, sourceResolution.GenerationType);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, sourceResolution.Status);
        Assert.True(sourceResolution.IsEquivalentSourceSet);
        Assert.Equal(2, sourceResolution.Candidates.Count);
        Assert.Equal(2, component.Sources.Count);
        Assert.Null(component.ResolvedModifierId);
        Assert.All(component.Sources, source =>
        {
            Assert.False(string.IsNullOrWhiteSpace(source.ResolvedModifierId));
            Assert.Equal(
                SearchComponentProviderResolutionStatus.Exact,
                source.ProviderResolutionStatus);
        });
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("implicit.stat_658456881", component.ProviderStatId);
        Assert.True(component.IsSearchable);
        Assert.True(IsInteractionReady(component));

        AssertSelectionMapsExactly(runtime.ProviderDraft, component);
    }

    [Fact]
    public void ResolveRawCopiedItem_TorchoakStep_MapsBothCorruptionImplicitsWithoutUniqueMovementConfusion()
    {
        var runtime = Resolve(TorchoakStepText);
        var enduranceParsed = Assert.Single(runtime.Parsed.Modifiers, modifier =>
            modifier.ValueLines.Contains("+1 to Maximum Endurance Charges"));
        var corruptedMovementParsed = Assert.Single(runtime.Parsed.Modifiers, modifier =>
            modifier.ValueLines.Contains("9(8-10)% increased Movement Speed"));
        var uniqueMovementParsed = Assert.Single(runtime.Parsed.Modifiers, modifier =>
            modifier.ValueLines.Contains("25% increased Movement Speed"));
        var endurance = FindComponent(runtime.ProviderDraft, "+1 to Maximum Endurance Charges");
        var corruptedMovement = FindComponent(runtime.ProviderDraft, "9(8-10)% increased Movement Speed");
        var uniqueMovement = FindComponent(runtime.ProviderDraft, "25% increased Movement Speed");

        Assert.Equal(ParsedImplicitModifierOrigin.Corrupted, enduranceParsed.ImplicitOrigin);
        Assert.Equal(ParsedImplicitModifierOrigin.Corrupted, corruptedMovementParsed.ImplicitOrigin);
        Assert.Equal(ParsedModifierKind.Unique, uniqueMovementParsed.Kind);

        AssertExactSelectable(endurance, "implicit.stat_1515657623");
        AssertExactSelectable(corruptedMovement, "implicit.stat_2250533757");
        Assert.Equal(ParsedModifierKind.Unique, uniqueMovement.ParsedKind);
        Assert.NotEqual(endurance.ComponentId, uniqueMovement.ComponentId);
        Assert.NotEqual(corruptedMovement.ComponentId, uniqueMovement.ComponentId);
        Assert.False(uniqueMovement.IsSearchable);

        AssertSelectionMapsExactly(runtime.ProviderDraft, endurance);
        AssertSelectionMapsExactly(runtime.ProviderDraft, corruptedMovement);
    }

    [Fact]
    public void ResolveRawCopiedItem_EnergyFromWithinPresenceCorruptionStaysBoundlessImplicit()
    {
        var runtime = Resolve(EnergyFromWithinText, OfficialTradeCatalog.Value);
        var text = "Corrupted Blood cannot be inflicted on you";
        var parsed = Assert.Single(runtime.Parsed.Modifiers);
        var source = Assert.Single(runtime.SourceResolutions);
        var component = FindComponent(runtime.ProviderDraft, text);

        Assert.Equal(ParsedModifierKind.Implicit, parsed.Kind);
        Assert.Equal(ParsedImplicitModifierOrigin.Corrupted, parsed.ImplicitOrigin);
        Assert.Equal(ModifierGenerationType.Corrupted, source.GenerationType);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, source.Status);
        Assert.False(component.IsBaseImplicit);
        AssertExactSelectable(component, "implicit.stat_1658498488");
        Assert.Equal(ModifierBoundShape.PresenceOnly, component.ValueBoundShape);
        Assert.False(component.SupportsValueBounds);
        Assert.Empty(component.ObservedNumericValues);
        Assert.Null(component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);

        var filter = MapSingle(runtime.ProviderDraft, component, OfficialTradeCatalog.Value);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);
        AssertSingleQueryFilter(
            runtime,
            component,
            OfficialTradeCatalog.Value,
            "implicit.stat_1658498488",
            expectedMinimum: null,
            expectedMaximum: null);
    }

    [Fact]
    public void ResolveRawCopiedItem_FracturedBoneRing_ProducesTwoExactSelectableChildren()
    {
        var runtime = Resolve(BoneRingText);
        var parsedModifier = Assert.Single(runtime.Parsed.Modifiers, modifier => modifier.IsFractured);
        var sourceResolution = Assert.Single(runtime.SourceResolutions, resolution =>
            resolution.ParsedModifier.IsFractured);
        var accuracy = FindComponent(
            runtime.ProviderDraft,
            "12(12-15)% increased Global Accuracy Rating");
        var lightRadius = FindComponent(runtime.ProviderDraft, "10% increased Light Radius");

        Assert.Equal(2, parsedModifier.ValueLines.Count);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, sourceResolution.Status);
        Assert.Single(sourceResolution.Candidates);
        Assert.True(accuracy.IsFractured);
        Assert.True(lightRadius.IsFractured);
        Assert.Equal(accuracy.SourceModifierIndex, lightRadius.SourceModifierIndex);
        Assert.Equal(0, accuracy.SourceLineIndex);
        Assert.Equal(1, lightRadius.SourceLineIndex);
        AssertExactSelectable(accuracy, "fractured.stat_624954515");
        AssertExactSelectable(lightRadius, "fractured.stat_1263695895");

        AssertSelectionMapsExactly(runtime.ProviderDraft, accuracy);
        AssertSelectionMapsExactly(runtime.ProviderDraft, lightRadius);
        AssertSelectionMapsExactly(runtime.ProviderDraft, accuracy, lightRadius);
    }

    [Fact]
    public void ResolveRawCopiedItem_MarkOfTheRedCovenantReducedSpiritsUsesPositiveMagnitude()
    {
        var catalog = OfficialTradeCatalog.Value;
        var runtime = Resolve(MarkOfTheRedCovenantReducedSpiritsText, catalog);
        var component = FindComponent(
            runtime.ProviderDraft,
            "75% reduced Maximum number of Summoned Raging Spirits");

        Assert.Equal([75m], component.ObservedNumericValues);
        Assert.Equal([-75m], component.CanonicalNumericValues);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_1186934478", component.ProviderStatId);
        Assert.True(component.SupportsValueBounds);
        Assert.Equal(75m, component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.Null(component.FixedQueryValue);
        Assert.True(IsInteractionReady(component));

        var filter = MapSingle(runtime.ProviderDraft, component, catalog);
        Assert.Equal(75m, filter.Minimum);
        Assert.Null(filter.Maximum);
        AssertSingleQueryFilter(
            runtime,
            component,
            catalog,
            "explicit.stat_1186934478",
            expectedMinimum: 75m,
            expectedMaximum: null);
    }

    [Fact]
    public void ResolveRawCopiedItem_ProgenesisProjectsNegateAndFixedLiteralValues()
    {
        var runtime = Resolve(ProgenesisText);
        var charges = FindComponent(runtime.ProviderDraft, "14(20-10)% reduced Charges per use");
        var lifeLoss = FindComponent(
            runtime.ProviderDraft,
            "When Hit during effect, 25% of Life loss from Damage taken occurs over 4 seconds instead");

        AssertExactSelectable(charges, "explicit.stat_388617051");
        Assert.Equal([14m], charges.ObservedNumericValues);
        Assert.Equal([-14m], charges.CanonicalNumericValues);
        Assert.Equal(ModifierBoundDirection.Maximum, charges.DefaultBoundDirection);
        Assert.Null(charges.RequestedMinimum);
        Assert.Equal(-14m, charges.RequestedMaximum);
        var chargesFilter = MapSingle(runtime.ProviderDraft, charges);
        Assert.Null(chargesFilter.Minimum);
        Assert.Equal(-14m, chargesFilter.Maximum);

        AssertExactSelectable(lifeLoss, "explicit.stat_41860024");
        Assert.Equal([25m], lifeLoss.ObservedNumericValues);
        Assert.Equal(25m, lifeLoss.RequestedMinimum);
        Assert.Null(lifeLoss.RequestedMaximum);
        var lifeLossFilter = MapSingle(runtime.ProviderDraft, lifeLoss);
        Assert.Equal(25m, lifeLossFilter.Minimum);
        Assert.Null(lifeLossFilter.Maximum);
    }

    [Fact]
    public void ResolveRawCopiedItem_ProgenesisPresenceEnchantmentStaysEnchantAndBoundless()
    {
        var runtime = Resolve(ProgenesisText, OfficialTradeCatalog.Value);
        var parsed = Assert.Single(
            runtime.Parsed.Modifiers,
            modifier => modifier.Kind == ParsedModifierKind.Enchantment);
        var source = Assert.Single(
            runtime.SourceResolutions,
            resolution => resolution.ParsedModifierKind == ParsedModifierKind.Enchantment);
        var component = FindComponent(runtime.ProviderDraft, "Used when Charges reach full");

        Assert.Equal("Used when Charges reach full (enchant)", Assert.Single(parsed.Effects).RawText);
        Assert.Equal(ParsedUniqueModifierOrigin.Unspecified, parsed.UniqueOrigin);
        Assert.Equal(ModifierGenerationType.Enchantment, source.GenerationType);
        Assert.True(
            source.Status == ModifierCandidateResolutionStatus.Exact,
            string.Join(" | ", source.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Reason}")) +
            $"; candidates={string.Join(",", source.Candidates.Select(candidate => candidate.Id))}");
        Assert.Equal(ParsedModifierKind.Enchantment, component.ParsedKind);
        Assert.Equal(ParsedModifierKind.Enchantment, component.ResolvedSourceKind);
        Assert.Equal(ModifierGenerationType.Enchantment, component.GenerationType);
        Assert.Equal(ParsedUniqueModifierOrigin.Unspecified, component.UniqueOrigin);
        Assert.Empty(component.UniqueCatalogBlockIds);
        Assert.Empty(component.UniqueSourceObservationIds);
        Assert.Contains(component.ProviderDomainEvidence, evidence =>
            evidence.IsSourceExact &&
            string.Equals(evidence.ProviderDomain, "Enchant", StringComparison.Ordinal));
        AssertExactSelectable(component, "enchant.stat_3287581721");
        Assert.Equal(ModifierBoundShape.PresenceOnly, component.ValueBoundShape);
        Assert.False(component.SupportsValueBounds);
        Assert.Null(component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.Equal("Enchant", StaticModifierLabel(component));

        var filter = MapSingle(runtime.ProviderDraft, component, OfficialTradeCatalog.Value);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);
        AssertSingleQueryFilter(
            runtime,
            component,
            OfficialTradeCatalog.Value,
            "enchant.stat_3287581721",
            expectedMinimum: null,
            expectedMaximum: null);
    }

    [Fact]
    public void ResolveRawCopiedItem_ReplicaAlberonsPreservesVvoValuesAndPresence()
    {
        var runtime = Resolve(ReplicaAlberonsText);
        var chaosDamage = FindComponent(
            runtime.ProviderDraft,
            "Adds 1 to 82(80) Chaos Damage to Attacks per 80 Strength");
        var presence = FindComponent(runtime.ProviderDraft, "Cannot deal non-Chaos Damage");

        AssertExactSelectable(chaosDamage, "explicit.stat_117885424");
        Assert.Equal(ModifierBoundShape.ArithmeticMeanRange, chaosDamage.ValueBoundShape);
        Assert.Equal([1m, 82m], chaosDamage.ObservedNumericValues);
        Assert.Equal(41.5m, chaosDamage.RequestedMinimum);
        var chaosFilter = MapSingle(runtime.ProviderDraft, chaosDamage);
        Assert.Equal(41.5m, chaosFilter.Minimum);
        Assert.Null(chaosFilter.Maximum);

        AssertExactSelectable(presence, "explicit.stat_3180152291");
        Assert.Equal(ModifierBoundShape.PresenceOnly, presence.ValueBoundShape);
        Assert.False(presence.SupportsValueBounds);
        var presenceFilter = MapSingle(runtime.ProviderDraft, presence);
        Assert.Null(presenceFilter.Minimum);
        Assert.Null(presenceFilter.Maximum);
    }

    [Fact]
    public void ResolveRawCopiedItem_SquireSocketCountUsesExactProviderBounds()
    {
        var runtime = Resolve(SquireText);
        var sockets = FindComponent(runtime.ProviderDraft, "Has 3 Sockets");

        // Official Trade text is the fixed literal "Has 1 Socket" (no # placeholder).
        // Presence-only: do not invent Min/Max from the clipboard "3".
        AssertExactSelectable(sockets, "explicit.stat_4077843608");
        Assert.Equal([3m], sockets.ObservedNumericValues);
        Assert.Equal(ModifierBoundShape.PresenceOnly, sockets.ValueBoundShape);
        Assert.False(sockets.SupportsValueBounds);
        Assert.Null(sockets.RequestedMinimum);
        Assert.Null(sockets.RequestedMaximum);
        var filter = MapSingle(runtime.ProviderDraft, sockets);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);
    }

    [Fact]
    public void ResolveRawCopiedItem_SquireDisplayedPropertiesUseFirstClassFiltersAndCoexistWithUniqueModifier()
    {
        var runtime = Resolve(SquireText);
        var armour = FindProperty(runtime.ProviderDraft, TradeSearchItemPropertyKind.Armour);
        var evasion = FindProperty(runtime.ProviderDraft, TradeSearchItemPropertyKind.EvasionRating);
        var block = FindProperty(runtime.ProviderDraft, TradeSearchItemPropertyKind.ChanceToBlock);

        AssertDisplayedProperty(armour, 420m);
        AssertDisplayedProperty(evasion, 420m);
        AssertDisplayedProperty(block, 30m);
        Assert.Null(armour.CalculationBasisLabel);
        Assert.Null(evasion.CalculationBasisLabel);
        Assert.Null(block.CalculationBasisLabel);

        var sockets = FindComponent(runtime.ProviderDraft, "Has 3 Sockets");
        var socketsIndex = runtime.ProviderDraft.ModifierFilters
            .Select((component, index) => new { component, index })
            .Single(entry => ReferenceEquals(entry.component, sockets))
            .index;
        var armourIndex = runtime.ProviderDraft.ItemProperties.IndexOf(armour);
        var selectedDraft = runtime.ProviderDraft with
        {
            ModifierFilters = runtime.ProviderDraft.ModifierFilters
                .Select((component, index) => component with { IsSelected = index == socketsIndex })
                .ToArray(),
            ItemProperties = runtime.ProviderDraft.ItemProperties
                .Select((property, index) => property with { IsSelected = index == armourIndex })
                .ToImmutableArray(),
        };
        var modifierMapping = SelectedMapper.Map(selectedDraft, TradeCatalog);
        var propertyMapping = ItemPropertyResolver.MapSelected(selectedDraft, FilterCatalog);
        Assert.True(modifierMapping.IsSuccess);
        Assert.True(propertyMapping.IsSuccess);
        Assert.Equal("explicit.stat_4077843608", Assert.Single(modifierMapping.Filters).StatId);
        var propertyFilter = Assert.Single(propertyMapping.Filters);
        Assert.Equal("armour_filters", propertyFilter.ProviderGroupId);
        Assert.Equal("ar", propertyFilter.ProviderFilterId);

        var query = new PathOfExileTradeQueryBuilder().Build(
            selectedDraft,
            new TradeSearchDraftValidator().Validate(selectedDraft),
            "Allflame",
            modifierMapping.Filters,
            runtime.UniqueIdentity,
            FilterCatalog,
            propertyMapping.Filters);
        Assert.True(query.IsSuccess, string.Join(" | ", query.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Contains("\"armour_filters\"", query.SerializedJson, StringComparison.Ordinal);
        Assert.Contains("\"ar\"", query.SerializedJson, StringComparison.Ordinal);
        Assert.Contains("\"explicit.stat_4077843608\"", query.SerializedJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRawCopiedItem_ReplicaAlberonsDisplayedArmourAndEnergyShieldRemainSearchable()
    {
        var runtime = Resolve(ReplicaAlberonsText);

        AssertDisplayedProperty(
            FindProperty(runtime.ProviderDraft, TradeSearchItemPropertyKind.Armour),
            377m);
        AssertDisplayedProperty(
            FindProperty(runtime.ProviderDraft, TradeSearchItemPropertyKind.EnergyShield),
            22m);
    }

    [Fact]
    public void ResolveRawCopiedItem_LastResortIncreasedPhysicalDamageUsesCanonicalProviderMechanic()
    {
        var runtime = Resolve(LastResortText);
        var physical = FindComponent(runtime.ProviderDraft, "94(80-100)% increased Physical Damage");

        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, physical.ProviderResolutionStatus);
        Assert.Equal("explicit.stat_1509134228", physical.ProviderStatId);
        Assert.True(physical.IsSearchable, physical.NotSearchableReason);
        Assert.True(IsInteractionReady(physical));
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, physical.ResolutionStatus);
        Assert.Equal(ModifierStatMappingProofStatus.ProviderExact, physical.StatMappingProof);
        Assert.Null(physical.UniqueResolutionDiagnosticCode);
        Assert.True(physical.IsEquivalentSourceSet);
        Assert.Equal(["local_physical_damage_+%"], physical.ResolvedStatIds);

        var uniqueBlockId = Assert.Single(physical.UniqueCatalogBlockIds);
        var sourceObservationId = Assert.Single(physical.UniqueSourceObservationIds);
        var lastResort = Assert.Single(GameData.Value.FindUniqueItemsByExactName("Last Resort"));
        var uniqueBlock = Assert.Single(
            lastResort.Versions.SelectMany(version => version.ModifierBlocks),
            block => string.Equals(block.Id, uniqueBlockId, StringComparison.Ordinal));
        Assert.Equal([sourceObservationId], uniqueBlock.SourceObservationIds);
        Assert.Equal(
            UniqueModifierMechanicalMappingStatus.EquivalentSourceSet,
            uniqueBlock.MechanicalMapping.Status);
        Assert.Contains(
            "LocalIncreasedPhysicalDamagePercentUniqueClaw4",
            uniqueBlock.MechanicalMapping.ModifierIds);
        Assert.Equal(["local_physical_damage_+%"], uniqueBlock.MechanicalMapping.StatIds);

        var provenance = Assert.IsType<UniqueModifierMechanicalProvenance>(
            uniqueBlock.MechanicalMapping.Provenance);
        Assert.Equal(["implicit-zero-stat-composition"], provenance.ResolutionReasons);
        Assert.True(provenance.UsedComposition);
        Assert.True(provenance.CatalogValuesUsedForSelection);
        Assert.Equal("copiedInstance", provenance.ValueAuthority);
        var translation = Assert.Single(provenance.Translations);
        Assert.Equal(
            "repoe:stat-translation:ff11c209633e3e38a40088ef7c0ac25eec05f45c48f325bd6ed253b962cf691a",
            translation.TranslationId);
        Assert.Equal(
            ["local_physical_damage_+%", "local_weapon_no_physical_damage"],
            translation.StatIds);
        Assert.Equal([0], translation.ModifierStatIndices);
        Assert.Equal(["local_weapon_no_physical_damage"], translation.DefaultedStatIds);
        Assert.Equal([94m], physical.CanonicalNumericValues);
        Assert.Equal(12, physical.Sources.Count);
        var filter = MapSingle(runtime.ProviderDraft, physical);
        Assert.Equal(94m, filter.Minimum);
        Assert.Null(filter.Maximum);
        var displayedPhysical = Assert.Single(runtime.Parsed.Properties, property =>
            property.NormalizedName == "physical damage");
        var displayedRange = Assert.Single(displayedPhysical.NumericGroups);
        Assert.Equal(14m, displayedRange.MinimumValue);
        Assert.Equal(49m, displayedRange.MaximumValue);
        Assert.Equal(
            50.4m,
            FindProperty(runtime.ProviderDraft, TradeSearchItemPropertyKind.PhysicalDps).ObservedValue);
        Assert.Equal(
            50.4m,
            FindProperty(runtime.ProviderDraft, TradeSearchItemPropertyKind.TotalDps).ObservedValue);
        Assert.Equal(
            1.60m,
            FindProperty(runtime.ProviderDraft, TradeSearchItemPropertyKind.AttacksPerSecond).ObservedValue);
        Assert.Equal(
            8.39m,
            FindProperty(runtime.ProviderDraft, TradeSearchItemPropertyKind.CriticalStrikeChance).ObservedValue);
    }

    [Fact]
    public void ResolveRawCopiedItem_DragonfangReducedAttributeRequirementsUsesSignedCanonicalProjection()
    {
        var runtime = Resolve(DragonfangAttributeRequirementsText);
        var requirements = FindComponent(
            runtime.ProviderDraft,
            "Items and Gems have 5(10-5)% reduced Attribute Requirements");

        AssertExactSelectable(requirements, "explicit.stat_752930724");
        Assert.Equal([5m], requirements.ObservedNumericValues);
        Assert.Equal([-5m], requirements.CanonicalNumericValues);
        Assert.Null(requirements.RequestedMinimum);
        Assert.Equal(-5m, requirements.RequestedMaximum);
        var filter = MapSingle(runtime.ProviderDraft, requirements);
        Assert.Null(filter.Minimum);
        Assert.Equal(-5m, filter.Maximum);
    }

    [Fact]
    public void ResolveRawCopiedItem_FoulbornMagebloodPresenceHasNoFakeBounds()
    {
        var runtime = Resolve(FoulbornMagebloodText);
        var presence = FindComponent(runtime.ProviderDraft, "Magic Utility Flasks cannot be Used");

        AssertExactSelectable(presence, "explicit.stat_3986704288");
        Assert.Equal(ModifierBoundShape.PresenceOnly, presence.ValueBoundShape);
        Assert.False(presence.SupportsValueBounds);
        var filter = MapSingle(runtime.ProviderDraft, presence);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);
    }

    [Theory]
    [InlineData(nameof(WindscreamText))]
    [InlineData(nameof(DoedresDamningText))]
    public void ResolveRawCopiedItem_MonsterModifierCurseOnResolvedUnique_RecoversPresenceOnlyWithoutRewritingMetadata(
        string fixtureName)
    {
        var runtime = Resolve(RawText(fixtureName), OfficialTradeCatalog.Value);
        var parsedModifier = Assert.Single(
            runtime.Parsed.Modifiers,
            modifier => modifier.ValueLines.Contains(AdditionalCurseText));

        // The parser must keep telling the truth about what the client emitted.
        Assert.Contains("Monster Modifier", parsedModifier.RawMetadataLine!, StringComparison.Ordinal);
        Assert.Equal(ParsedModifierKind.Unknown, parsedModifier.Kind);
        Assert.Equal(ParsedUniqueModifierOrigin.Unspecified, parsedModifier.UniqueOrigin);

        var component = FindComponent(runtime.ProviderDraft, AdditionalCurseText);
        AssertExactSelectable(component, AdditionalCurseProviderStatId);
        Assert.Equal(ModifierBoundShape.PresenceOnly, component.ValueBoundShape);
        Assert.False(component.SupportsValueBounds);
        Assert.Single(component.UniqueCatalogBlockIds);
        Assert.NotEmpty(component.UniqueSourceObservationIds);
        Assert.Null(component.UniqueResolutionDiagnosticCode);
        Assert.True(component.UsesIdentityBoundUniqueRecovery);
        Assert.Equal(ParsedUniqueModifierOrigin.Unspecified, component.UniqueOrigin);
        Assert.Equal(ParsedModifierKind.Unique, component.ResolvedSourceKind);
        Assert.Equal(ParsedUniqueModifierOrigin.Ordinary, component.ResolvedSourceUniqueOrigin);
        Assert.True(component.HasExactUniqueSourceProvenance);
        Assert.Equal("Unique", StaticModifierLabel(component));
        Assert.Equal([AdditionalCurseProviderStatId], component.ProviderCandidateStatIds);
        Assert.NotEqual(
            PathOfExileTradeStatMatchDiagnosticCodes.AmbiguousCandidates,
            component.ProviderDiagnosticCode);

        AssertOfficialAdditionalCurseDomains();
        var filter = MapSingle(runtime.ProviderDraft, component, OfficialTradeCatalog.Value);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);
        AssertSingleBoundlessQueryFilter(runtime, component, OfficialTradeCatalog.Value);

        if (fixtureName == nameof(WindscreamText))
        {
            var armour = FindComponent(runtime.ProviderDraft, "59(50-80)% increased Armour");
            Assert.Equal(ModifierCandidateResolutionStatus.Exact, armour.ResolutionStatus);
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, armour.ProviderResolutionStatus);
            Assert.StartsWith("explicit.", armour.ProviderStatId, StringComparison.Ordinal);
            Assert.True(armour.IsSearchable, armour.NotSearchableReason);
            Assert.True(IsInteractionReady(armour));
            Assert.Equal(ModifierBoundShape.Scalar, armour.ValueBoundShape);
            Assert.True(armour.SupportsValueBounds);
            Assert.Equal(59m, armour.RequestedMinimum);
            var armourFilter = MapSingle(runtime.ProviderDraft, armour, OfficialTradeCatalog.Value);
            Assert.Equal(59m, armourFilter.Minimum);
        }
    }

    [Fact]
    public void ResolveRawCopiedItem_UniqueModifierCurseControl_StaysOnOrdinaryUniquePathWithoutRecovery()
    {
        var runtime = Resolve(CospriWillText, OfficialTradeCatalog.Value);
        var parsedModifier = Assert.Single(
            runtime.Parsed.Modifiers,
            modifier => modifier.ValueLines.Contains(AdditionalCurseText));

        Assert.Contains("Unique Modifier", parsedModifier.RawMetadataLine!, StringComparison.Ordinal);
        Assert.Equal(ParsedModifierKind.Unique, parsedModifier.Kind);

        var component = FindComponent(runtime.ProviderDraft, AdditionalCurseText);
        AssertExactSelectable(component, AdditionalCurseProviderStatId);
        Assert.Equal(ModifierBoundShape.PresenceOnly, component.ValueBoundShape);
        Assert.False(component.SupportsValueBounds);
        Assert.Null(component.UniqueResolutionDiagnosticCode);
        Assert.False(component.UsesIdentityBoundUniqueRecovery);
        Assert.Equal(ParsedModifierKind.Unique, component.ResolvedSourceKind);
        Assert.Equal(ParsedUniqueModifierOrigin.Ordinary, component.ResolvedSourceUniqueOrigin);
        Assert.True(component.HasExactUniqueSourceProvenance);
        Assert.Equal("Unique", StaticModifierLabel(component));

        var filter = MapSingle(runtime.ProviderDraft, component, OfficialTradeCatalog.Value);
        Assert.Null(filter.Minimum);
        Assert.Null(filter.Maximum);
        AssertSingleBoundlessQueryFilter(runtime, component, OfficialTradeCatalog.Value);
    }

    [Fact]
    public void ResolveRawCopiedItem_EbersUnification_VoidGazeUsesEditableMinimumBounds()
    {
        var parsed = new ItemTextParser().Parse(EbersUnificationText);
        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            gameDataCatalog: GameData.Value).Draft);
        var component = FindComponent(
            draft,
            "Trigger Level 10 Void Gaze when you use a Skill");

        Assert.Equal(NumericQueryRole.SkillGemLevelThreshold, component.NumericQueryRole);
        Assert.True(component.SupportsValueBounds);
        Assert.Equal(10m, component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.Null(component.FixedQueryValue);
        Assert.True(component.IsSearchable, component.NotSearchableReason);
    }

    [Fact]
    public void ResolveRawCopiedItem_ReverberationRod_GemLevelSupportsUseEditableMinimumBounds()
    {
        var catalog = OfficialTradeCatalog.Value;
        var runtime = Resolve(ReverberationRodText, catalog);
        var gemLevelSupports = new[]
        {
            FindComponent(runtime.ProviderDraft, "Socketed Gems are Supported by Level 10 Spell Echo"),
            FindComponent(runtime.ProviderDraft, "Socketed Gems are Supported by Level 10 Controlled Destruction"),
            FindComponent(runtime.ProviderDraft, "Socketed Gems are Supported by Level 10 Arcane Surge"),
        };

        foreach (var component in gemLevelSupports)
        {
            Assert.Equal(
                SearchComponentProviderResolutionStatus.ExactEquivalentSet,
                component.ProviderResolutionStatus);
            Assert.Null(component.ProviderStatId);
            Assert.True(component.ProviderStatAlternativeIds.Count > 1);
            Assert.All(component.ProviderStatAlternativeIds, statId =>
            {
                Assert.True(catalog.TryGetById(statId, out var entry));
                Assert.Contains('#', entry.Text);
            });
            Assert.True(component.IsSearchable, component.NotSearchableReason);
            Assert.True(IsInteractionReady(component));
            Assert.True(component.SupportsValueBounds);
            Assert.Equal(ModifierBoundShape.Scalar, component.ValueBoundShape);
            Assert.Equal(10m, component.RequestedMinimum);
            Assert.Null(component.RequestedMaximum);
            Assert.Equal([10m], component.ObservedNumericValues);
            Assert.Equal([10m], component.CanonicalNumericValues);
            Assert.Null(component.FixedQueryValue);
            Assert.Equal(NumericQueryRole.SkillGemLevelThreshold, component.NumericQueryRole);
            Assert.Equal(ModifierBoundDirection.Minimum, component.DefaultBoundDirection);

            var selectedDraft = runtime.ProviderDraft with
            {
                ModifierFilters = runtime.ProviderDraft.ModifierFilters
                    .Select(candidate => candidate with
                    {
                        IsSelected = candidate.ComponentId == component.ComponentId,
                    })
                    .ToArray(),
            };
            var mapping = SelectedMapper.Map(selectedDraft, catalog);
            Assert.True(
                mapping.IsSuccess,
                string.Join(" | ", mapping.Diagnostics.Select(diagnostic => diagnostic.Message)));
            var mapped = Assert.Single(mapping.Filters);
            Assert.Equal(component.ProviderStatAlternativeIds.Count, mapped.Alternatives.Count);
            Assert.All(mapped.Alternatives, alternative =>
            {
                Assert.Equal(10m, alternative.Minimum);
                Assert.Null(alternative.Maximum);
            });

            var query = new PathOfExileTradeQueryBuilder().Build(
                selectedDraft,
                new TradeSearchDraftValidator().Validate(selectedDraft),
                "Allflame",
                mapping.Filters,
                runtime.UniqueIdentity,
                FilterCatalog);
            Assert.True(
                query.IsSuccess,
                string.Join(" | ", query.Diagnostics.Select(diagnostic => diagnostic.Message)));
            using var document = JsonDocument.Parse(query.SerializedJson!);
            var serialized = Assert.Single(document.RootElement
                .GetProperty("query")
                .GetProperty("stats")
                .EnumerateArray())
                .GetProperty("filters")
                .EnumerateArray()
                .ToArray();
            Assert.Equal(component.ProviderStatAlternativeIds.Count, serialized.Length);
            Assert.All(serialized, filter =>
            {
                var value = filter.GetProperty("value");
                Assert.Equal(10m, value.GetProperty("min").GetDecimal());
                Assert.False(value.TryGetProperty("max", out _));
            });

            var editedMapping = SelectedMapper.Map(
                selectedDraft with
                {
                    ModifierFilters =
                    [
                        component with
                        {
                            IsSelected = true,
                            RequestedMinimum = 11m,
                            RequestedMaximum = null,
                        },
                    ],
                },
                catalog);
            Assert.True(editedMapping.IsSuccess);
            var editedFilter = Assert.Single(editedMapping.Filters);
            Assert.Equal(11m, editedFilter.Minimum);
            Assert.Null(editedFilter.Maximum);
        }

        var gemLevels = FindComponent(runtime.ProviderDraft, "+2 to Level of Socketed Gems");
        var intelligence = FindComponent(runtime.ProviderDraft, "+21(10-30) to Intelligence");
        Assert.True(gemLevels.SupportsValueBounds);
        Assert.Equal(2m, gemLevels.RequestedMinimum);
        Assert.Null(gemLevels.FixedQueryValue);
        Assert.True(intelligence.SupportsValueBounds);
        Assert.Equal(21m, intelligence.RequestedMinimum);
        Assert.Null(intelligence.FixedQueryValue);
    }

    [Fact]
    public void ResolveRawCopiedItem_WurmsMoltPhysicalLeechRowsRemainSupported()
    {
        var catalog = OfficialTradeCatalog.Value;
        var runtime = Resolve("""
            Item Class: Belts
            Rarity: Unique
            Wurm's Molt
            Leather Belt
            --------
            Item Level: 86
            --------
            { Unique Modifier — Mana, Physical, Attack }
            2% of Physical Attack Damage Leeched as Mana
            { Unique Modifier — Life, Physical, Attack }
            2% of Physical Attack Damage Leeched as Life
            { Unique Modifier — Attribute }
            +22(20-30) to Strength
            { Unique Modifier — Attribute }
            +20(20-30) to Intelligence
            { Unique Modifier — Elemental, Cold, Resistance }
            +28(20-30)% to Cold Resistance
            { Unique Modifier — Life }
            611(500-1000)% increased total Recovery per second from Life Leech
            { Unique Modifier — Mana }
            965(500-1000)% increased total Recovery per second from Mana Leech
            """, catalog);
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["2% of Physical Attack Damage Leeched as Mana"] = "explicit.stat_3237948413",
            ["2% of Physical Attack Damage Leeched as Life"] = "explicit.stat_3593843976",
        };

        foreach (var (text, providerStatId) in expected)
        {
            var component = FindComponent(runtime.ProviderDraft, text);
            Assert.True(
                component.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact,
                $"{text}: source={component.ResolutionStatus}; sourceCode={component.UniqueResolutionDiagnosticCode}; " +
                $"provider={component.ProviderResolutionStatus}; providerCode={component.ProviderDiagnosticCode}; " +
                $"providerMessage={component.ProviderDiagnosticMessage}; searchable={component.IsSearchable}; " +
                $"reason={component.NotSearchableReason}");
            Assert.Equal(providerStatId, component.ProviderStatId);
            Assert.True(component.IsSearchable, component.NotSearchableReason);
            Assert.True(IsInteractionReady(component));
            Assert.True(component.HasExactUniqueSourceProvenance);
            Assert.Single(component.UniqueCatalogBlockIds);
            Assert.NotEmpty(component.UniqueSourceObservationIds);
            Assert.Equal(2m, component.RequestedMinimum);
            Assert.Null(component.RequestedMaximum);
            AssertSingleQueryFilter(
                runtime,
                component,
                catalog,
                providerStatId,
                expectedMinimum: 2m,
                expectedMaximum: null);
        }
    }

    [Fact]
    public void ResolveRawCopiedItems_CompositionManualMatrixHasExpectedStaticOutcomes()
    {
        var catalog = OfficialTradeCatalog.Value;
        var separatedCases = new[]
        {
            (Resolve(AsenathsMarkCompositionText, catalog), "+39(30-50) to maximum Energy Shield"),
            (Resolve(HrimnorsResolveCompositionText, catalog), "108(100-120)% increased Armour"),
            (Resolve(MarkOfTheRedCovenantCompositionText, catalog), "+45(30-50) to maximum Energy Shield"),
        };
        foreach (var (runtime, text) in separatedCases)
        {
            var component = FindComponent(runtime.ProviderDraft, text);
            Assert.Equal("Unique", StaticModifierLabel(component));
            Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", component.UniqueResolutionDiagnosticCode);
            Assert.Equal("Ambiguous", ModifierAvailabilityStatus(component));
            Assert.False(component.HasExactUniqueSourceProvenance);
            Assert.False(component.IsSearchable);
            Assert.False(IsInteractionReady(component));
        }

        var redCovenant = Resolve(MarkOfTheRedCovenantCompositionText, catalog);
        var healthyControl = FindComponent(
            redCovenant.ProviderDraft,
            "Summoned Raging Spirits' Melee Strikes deal Fire-only Splash" +
            Environment.NewLine + "Damage to Surrounding Targets");
        Assert.Equal("Unique", StaticModifierLabel(healthyControl));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact,
            healthyControl.ProviderResolutionStatus);
        Assert.False(healthyControl.SupportsValueBounds);
        Assert.True(IsInteractionReady(healthyControl));
        var healthyFilter = MapSingle(redCovenant.ProviderDraft, healthyControl, catalog);
        Assert.Equal("explicit.stat_221328679", healthyFilter.StatId);
        Assert.Null(healthyFilter.Minimum);
        Assert.Null(healthyFilter.Maximum);

        var bones = Resolve(BonesOfUllrCompositionText, catalog);
        foreach (var (text, minimum) in new[]
        {
            ("+1 to Level of all Raise Zombie Gems", 1m),
            ("+1 to Level of all Raise Spectre Gems", 1m),
        })
        {
            var component = FindComponent(bones.ProviderDraft, text);
            Assert.Equal("Unique", StaticModifierLabel(component));
            Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
            Assert.Equal(SearchComponentProviderResolutionStatus.ExactEquivalentSet,
                component.ProviderResolutionStatus);
            Assert.True(component.HasExactUniqueSourceProvenance);
            Assert.Equal(minimum, component.RequestedMinimum);
            Assert.Null(component.RequestedMaximum);
            Assert.True(IsInteractionReady(component));
            var filter = MapSingle(bones.ProviderDraft, component, catalog);
            Assert.Contains(filter.StatId, component.ProviderStatAlternativeIds);
            Assert.Equal(minimum, filter.Minimum);
            Assert.Null(filter.Maximum);
        }

        var life = FindComponent(bones.ProviderDraft, "+20 to maximum Life");
        var mana = FindComponent(bones.ProviderDraft, "+20 to maximum Mana");
        Assert.NotEqual(life.SourceModifierIndex, mana.SourceModifierIndex);
        Assert.All(new[] { life, mana }, component =>
        {
            Assert.Equal("Unique", StaticModifierLabel(component));
            Assert.Equal(20m, component.RequestedMinimum);
            Assert.Null(component.RequestedMaximum);
            Assert.True(IsInteractionReady(component));
            var filter = MapSingle(bones.ProviderDraft, component, catalog);
            Assert.Equal(20m, filter.Minimum);
            Assert.Null(filter.Maximum);
        });

        var battle = Resolve(BattleWithinCompositionText, catalog);
        var battleComponent = Assert.Single(battle.ProviderDraft.ModifierFilters);
        Assert.Equal("Unique", StaticModifierLabel(battleComponent));
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, battleComponent.ResolutionStatus);
        Assert.True(battleComponent.HasExactUniqueSourceProvenance);
        Assert.Equal(SearchComponentProviderResolutionStatus.Ambiguous,
            battleComponent.ProviderResolutionStatus);
        Assert.Equal("Ambiguous", ModifierAvailabilityStatus(battleComponent));
        Assert.False(battleComponent.IsSearchable);
        Assert.False(IsInteractionReady(battleComponent));
    }

    [Fact]
    public void ResolveRawCopiedItems_OptionAxisManualMatrixHasExpectedStaticOutcomes()
    {
        var catalog = OfficialTradeCatalog.Value;
        var anguish = Resolve("""
            Item Class: Rings
            Rarity: Unique
            Circle of Anguish
            Ruby Ring
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +1% to maximum Fire Resistance while affected by Herald of Ash
            { Unique Modifier }
            +55(50-60)% to Fire Resistance while affected by Herald of Ash
            """, catalog);
        AssertExpectedSupported(
            anguish,
            "+1% to maximum Fire Resistance while affected by Herald of Ash",
            1m,
            catalog);
        AssertExpectedSupported(
            anguish,
            "+55(50-60)% to Fire Resistance while affected by Herald of Ash",
            55m,
            catalog);

        var fear = Resolve("""
            Item Class: Rings
            Rarity: Unique
            Circle of Fear
            Sapphire Ring
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Herald of Ice has 36(30-40)% increased Mana Reservation Efficiency
            { Unique Modifier }
            +1% to maximum Cold Resistance while affected by Herald of Ice
            """, catalog);
        var reservation = FindComponent(
            fear.ProviderDraft,
            "Herald of Ice has 36(30-40)% increased Mana Reservation Efficiency");
        Assert.Equal("Unique", StaticModifierLabel(reservation));
        Assert.Equal("Ambiguous", ModifierAvailabilityStatus(reservation));
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", reservation.UniqueResolutionDiagnosticCode);
        Assert.False(reservation.IsSearchable);
        Assert.False(IsInteractionReady(reservation));
        Assert.True(reservation.SupportsValueBounds);
        Assert.Equal(36m, reservation.RequestedMinimum);
        Assert.Null(reservation.RequestedMaximum);
        AssertExpectedSupported(
            fear,
            "+1% to maximum Cold Resistance while affected by Herald of Ice",
            1m,
            catalog);

        var split = Resolve("""
            Item Class: Jewels
            Rarity: Unique
            Split Personality
            Crimson Jewel
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +5 to maximum Energy Shield
            { Unique Modifier }
            +5 to Intelligence
            """, catalog);
        AssertExpectedSupported(split, "+5 to maximum Energy Shield", 5m, catalog);
        AssertExpectedSupported(split, "+5 to Intelligence", 5m, catalog);

        var coralito = Resolve("""
            Item Class: Utility Flasks
            Rarity: Unique
            Coralito's Signature
            Diamond Flask
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +25(20-30)% to Damage over Time Multiplier for Poison from Critical Strikes during Effect
            """, catalog);
        AssertExpectedSupported(
            coralito,
            "+25(20-30)% to Damage over Time Multiplier for Poison from Critical Strikes during Effect",
            25m,
            catalog);
    }

    [Theory]
    [InlineData(
        nameof(LethalPrideText),
        "Commanded leadership over <number> warriors under Rakiata",
        "explicit.pseudo_timeless_jewel_rakiata",
        "Rakiata(Akoya-Rakiata)",
        14245)]
    [InlineData(
        nameof(BrutalRestraintText),
        "Denoted service of <number> dekhara in the akhara of Asenath",
        "explicit.pseudo_timeless_jewel_asenath",
        "Asenath(Asenath-Nasima)",
        2844)]
    [InlineData(
        nameof(GloriousVanityText),
        "Bathed in the blood of <number> sacrificed in the name of Ahuana",
        "explicit.pseudo_timeless_jewel_ahuana",
        "Ahuana(Ahuana-Xibaqua)",
        1073)]
    [InlineData(
        nameof(MilitantFaithText),
        "Carved to glorify <number> new faithful converted by High Templar Avarius",
        "explicit.pseudo_timeless_jewel_avarius",
        "Avarius(Avarius-Maxarius)",
        2549)]
    public void ResolveRawCopiedItem_TimelessJewelUsesExactSourceLineAndFixedSeedQuery(
        string fixtureName,
        string providerSignature,
        string providerStatId,
        string optionAnnotatedText,
        decimal seed)
    {
        var catalog = OfficialTradeCatalog.Value;
        var runtime = Resolve(TimelessRawText(fixtureName), catalog);
        var component = Assert.Single(runtime.ProviderDraft.ModifierFilters);

        Assert.Equal(-1, component.SourceLineIndex);
        Assert.Equal(3, component.OriginalText.Split('\n').Length);
        Assert.Contains(optionAnnotatedText, component.OriginalText, StringComparison.Ordinal);
        Assert.DoesNotContain(optionAnnotatedText, component.PresentationText, StringComparison.Ordinal);
        Assert.Contains(providerSignature, component.ProviderSearchSignatures);
        Assert.True(component.HasExactUniqueSourceProvenance);
        Assert.Single(component.UniqueCatalogBlockIds);
        Assert.NotEmpty(component.UniqueSourceObservationIds);
        var providerMatch = new PathOfExileTradeStatMatcher().Match(component, catalog);
        Assert.True(
            component.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact,
            $"status={component.ProviderResolutionStatus}; code={component.ProviderDiagnosticCode}; " +
            $"message={component.ProviderDiagnosticMessage}; alternatives=" +
            string.Join(',', component.ProviderStatAlternativeIds) + "; candidates=" +
            string.Join(" | ", providerMatch.Candidates.Select(candidate =>
                $"{candidate.StatId}:{candidate.Text}")));
        Assert.Equal(ModifierStatMappingProofStatus.ProviderExact, component.StatMappingProof);
        Assert.Equal(providerStatId, component.ProviderStatId);
        Assert.Null(component.FixedQueryValue);
        Assert.Equal(ModifierBoundShape.Scalar, component.ValueBoundShape);
        Assert.True(component.SupportsValueBounds);
        Assert.Equal(seed, component.RequestedMinimum);
        Assert.Equal(seed, component.RequestedMaximum);
        Assert.True(component.IsSearchable, component.NotSearchableReason);
        Assert.True(IsInteractionReady(component));
        Assert.Equal("Unique", StaticModifierLabel(component));
        AssertSingleQueryFilter(runtime, component, catalog, providerStatId, seed, seed);
    }

    [Theory]
    [InlineData(nameof(ReplicaBatedBreathText), "-20(-25--15) to Intelligence")]
    [InlineData(nameof(AugyreText), "205(180-220)% increased Physical Damage")]
    public void ResolveRawCopiedItem_UnrelatedVersionMismatchRemainsBlocked(
        string fixtureName,
        string mismatchedLine)
    {
        var runtime = Resolve(VersionMismatchRawText(fixtureName), OfficialTradeCatalog.Value);
        var component = FindComponent(runtime.ProviderDraft, mismatchedLine);

        Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", component.UniqueResolutionDiagnosticCode);
        Assert.False(component.HasExactUniqueSourceProvenance);
        Assert.Empty(component.ProviderSearchSignatures);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.Unsupported,
            component.ProviderResolutionStatus);
        Assert.Equal(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
            component.ProviderDiagnosticCode);
        Assert.False(component.IsSearchable);
        Assert.Empty(component.FilterVariants);
        Assert.Null(component.FixedQueryValue);
    }

    [Fact]
    public void ResolveRawCopiedItem_ReverberationRod_CorruptedPowerChargeStaysSearchableImplicit()
    {
        var runtime = Resolve(ReverberationRodText, OfficialTradeCatalog.Value);
        var text = "7(5-7)% chance to gain a Power Charge on Critical Strike";
        var parsed = Assert.Single(runtime.Parsed.Modifiers, modifier => modifier.ValueLines.Contains(text));
        var source = Assert.Single(runtime.SourceResolutions, resolution =>
            resolution.ParsedModifier.ValueLines.Contains(text));
        var component = FindComponent(runtime.ProviderDraft, text);

        Assert.Equal(ParsedModifierKind.Implicit, parsed.Kind);
        Assert.Equal(ParsedImplicitModifierOrigin.Corrupted, parsed.ImplicitOrigin);
        Assert.Equal(ParsedUniqueModifierOrigin.Unspecified, parsed.UniqueOrigin);
        Assert.Equal(ModifierGenerationType.Corrupted, source.GenerationType);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, source.Status);
        Assert.Equal(ParsedModifierKind.Implicit, component.ResolvedSourceKind);
        Assert.Equal(ParsedImplicitModifierOrigin.Corrupted, component.ImplicitOrigin);
        Assert.False(component.IsBaseImplicit);
        Assert.Empty(component.UniqueCatalogBlockIds);
        Assert.Contains(component.ProviderDomainEvidence, evidence =>
            evidence.IsSourceExact &&
            string.Equals(evidence.ProviderDomain, "Implicit", StringComparison.Ordinal));
        AssertExactSelectable(component, "implicit.stat_3814876985");
        Assert.Equal(ModifierBoundShape.Scalar, component.ValueBoundShape);
        Assert.True(component.SupportsValueBounds);
        Assert.Equal([7m], component.ObservedNumericValues);
        Assert.Equal(7m, component.RequestedMinimum);
        Assert.Equal("Implicit", StaticModifierLabel(component));
        Assert.All(component.FilterVariants, variant =>
            Assert.Equal("implicit", variant.ProviderKind));

        var filter = MapSingle(runtime.ProviderDraft, component, OfficialTradeCatalog.Value);
        Assert.Equal(7m, filter.Minimum);
        Assert.Null(filter.Maximum);
        AssertSingleQueryFilter(
            runtime,
            component,
            OfficialTradeCatalog.Value,
            "implicit.stat_3814876985",
            expectedMinimum: 7m,
            expectedMaximum: null);
    }

    [Fact]
    public void ResolveRawCopiedItem_SpiraledWandBaseImplicitHasSameSemanticsOnUniqueAndRareItems()
    {
        var uniqueRuntime = Resolve(ReverberationRodText, OfficialTradeCatalog.Value);
        var rareRuntime = Resolve(RareSpiraledWandText, OfficialTradeCatalog.Value);
        var text = "Adds 2(1-2) to 10(9-11) Lightning Damage to Spells and Attacks";
        var unique = FindComponent(uniqueRuntime.ProviderDraft, text);
        var rare = FindComponent(rareRuntime.ProviderDraft, text);

        foreach (var component in new[] { unique, rare })
        {
            Assert.Equal(ParsedModifierKind.Implicit, component.ParsedKind);
            Assert.Equal(ParsedModifierKind.Implicit, component.ResolvedSourceKind);
            Assert.Equal(ParsedImplicitModifierOrigin.Unspecified, component.ImplicitOrigin);
            Assert.Equal(ParsedUniqueModifierOrigin.Unspecified, component.UniqueOrigin);
            Assert.Equal(ModifierGenerationType.Implicit, component.GenerationType);
            Assert.True(component.IsBaseImplicit);
            Assert.Empty(component.UniqueCatalogBlockIds);
            Assert.Empty(component.UniqueSourceObservationIds);
            Assert.NotNull(component.BaseImplicitProvenance);
            Assert.Contains(component.ProviderDomainEvidence, evidence =>
                evidence.IsSourceExact &&
                !string.IsNullOrWhiteSpace(evidence.ItemBaseId) &&
                string.Equals(evidence.ProviderDomain, "Implicit", StringComparison.Ordinal));
            AssertExactSelectable(component, "implicit.stat_2885144362");
            Assert.Equal(ModifierBoundShape.ArithmeticMeanRange, component.ValueBoundShape);
            Assert.Equal([2m, 10m], component.ObservedNumericValues);
            Assert.Equal(6m, component.RequestedMinimum);
            Assert.Equal("Implicit", StaticModifierLabel(component));
            Assert.All(component.FilterVariants, variant =>
                Assert.Equal("implicit", variant.ProviderKind));
        }

        Assert.Equal(unique.ResolvedModifierId, rare.ResolvedModifierId);
        Assert.Equal(unique.ResolvedStatIds, rare.ResolvedStatIds);
        Assert.Equal(unique.ProviderStatId, rare.ProviderStatId);
        Assert.Equal(unique.RequestedMinimum, rare.RequestedMinimum);

        var uniqueFilter = MapSingle(uniqueRuntime.ProviderDraft, unique, OfficialTradeCatalog.Value);
        var rareFilter = MapSingle(rareRuntime.ProviderDraft, rare, OfficialTradeCatalog.Value);
        Assert.Equal(6m, uniqueFilter.Minimum);
        Assert.Equal(6m, rareFilter.Minimum);
        AssertSingleQueryFilter(
            uniqueRuntime,
            unique,
            OfficialTradeCatalog.Value,
            "implicit.stat_2885144362",
            expectedMinimum: 6m,
            expectedMaximum: null);
    }

    [Fact]
    public void UiSemanticLabel_DoesNotPromoteRawUnknownFromUnprovenRecoveredFields()
    {
        var component = new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            ParsedKind = ParsedModifierKind.Unknown,
            UniqueOrigin = ParsedUniqueModifierOrigin.Unspecified,
            UsesIdentityBoundUniqueRecovery = true,
            RecoveredSourceKind = ParsedModifierKind.Unique,
            RecoveredSourceUniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Unknown,
        };

        Assert.Equal(ParsedModifierKind.Unknown, component.ResolvedSourceKind);
        Assert.Equal("modifier", StaticModifierLabel(component));
    }

    private const string AdditionalCurseText = "You can apply an additional Curse";
    private const string AdditionalCurseProviderStatId = "explicit.stat_30642521";

    private static string RawText(string fixtureName) => fixtureName switch
    {
        nameof(WindscreamText) => WindscreamText,
        nameof(DoedresDamningText) => DoedresDamningText,
        nameof(CospriWillText) => CospriWillText,
        _ => throw new ArgumentOutOfRangeException(nameof(fixtureName), fixtureName, "Unknown fixture."),
    };

    private static string TimelessRawText(string fixtureName) => fixtureName switch
    {
        nameof(LethalPrideText) => LethalPrideText,
        nameof(BrutalRestraintText) => BrutalRestraintText,
        nameof(GloriousVanityText) => GloriousVanityText,
        nameof(MilitantFaithText) => MilitantFaithText,
        _ => throw new ArgumentOutOfRangeException(nameof(fixtureName), fixtureName, "Unknown fixture."),
    };

    private static string VersionMismatchRawText(string fixtureName) => fixtureName switch
    {
        nameof(ReplicaBatedBreathText) => ReplicaBatedBreathText,
        nameof(AugyreText) => AugyreText,
        _ => throw new ArgumentOutOfRangeException(nameof(fixtureName), fixtureName, "Unknown fixture."),
    };

    private static RuntimeResult Resolve(
        string rawText,
        PathOfExileTradeStatCatalog? tradeCatalog = null)
    {
        var parsed = new ItemTextParser().Parse(rawText);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, GameData.Value);
        var sourceResolutions = new ParsedItemModifierCandidateResolver().Resolve(
            parsed,
            GameData.Value,
            baseResolution);
        var draftResult = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            baseResolution,
            sourceResolutions,
            GameData.Value);
        var draft = Assert.IsType<TradeSearchDraft>(draftResult.Draft);
        var uniqueIdentity = new PathOfExileTradeItemIdentityMapper()
            .Map(draft, TradeItemCatalog)
            .Identity;
        var propertyDraft = ItemPropertyResolver.Resolve(draft, FilterCatalog);
        var providerDraft = CreatePriceCheckService().ResolveProviderComponents(
            propertyDraft,
            tradeCatalog ?? TradeCatalog,
            uniqueIdentity,
            FilterCatalog);
        return new RuntimeResult(parsed, baseResolution, sourceResolutions, providerDraft, uniqueIdentity);
    }

    private static void AssertExactSelectable(ResolvedSearchComponent component, string providerStatId)
    {
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal(providerStatId, component.ProviderStatId);
        Assert.True(
            component.IsSearchable,
            $"{component.NotSearchableReason} | {component.UniqueResolutionDiagnosticCode}");
        Assert.True(
            IsInteractionReady(component),
            $"blocks={component.UniqueCatalogBlockIds.Count}; sources={component.UniqueSourceObservationIds.Count}; " +
            $"proof={component.StatMappingProof}; diagnostic={component.UniqueResolutionDiagnosticCode}; " +
            $"bounds={component.SupportsValueBounds}/{component.CanonicalNumericValues.Count}");
    }

    private static void AssertSelectionMapsExactly(
        TradeSearchDraft draft,
        params ResolvedSearchComponent[] selectedComponents)
    {
        var selectedIds = selectedComponents
            .Select(component => component.ComponentId)
            .ToHashSet(StringComparer.Ordinal);
        var selectedDraft = draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select(component => component with
                {
                    IsSelected = selectedIds.Contains(component.ComponentId),
                })
                .ToArray(),
        };

        var mapping = SelectedMapper.Map(selectedDraft, TradeCatalog);

        Assert.True(mapping.IsSuccess, string.Join(" | ", mapping.Diagnostics.Select(diagnostic => diagnostic.Message)));
        Assert.Equal(selectedComponents.Length, mapping.Filters.Count);
        Assert.Equal(
            selectedComponents.Select(component => component.ProviderStatId).OrderBy(id => id, StringComparer.Ordinal),
            mapping.Filters.Select(filter => filter.StatId).OrderBy(id => id, StringComparer.Ordinal));
    }

    private static PathOfExileTradeSelectedModifierFilter MapSingle(
        TradeSearchDraft draft,
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog? catalog = null)
    {
        var selectedDraft = draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select(candidate => candidate with
                {
                    IsSelected = string.Equals(
                        candidate.ComponentId,
                        component.ComponentId,
                        StringComparison.Ordinal),
                })
                .ToArray(),
        };
        var mapping = SelectedMapper.Map(selectedDraft, catalog ?? TradeCatalog);
        Assert.True(mapping.IsSuccess, string.Join(" | ", mapping.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.Single(mapping.Filters);
    }

    private static ResolvedSearchComponent FindComponent(TradeSearchDraft draft, string originalText)
    {
        return Assert.Single(draft.ModifierFilters, component =>
            string.Equals(component.OriginalText, originalText, StringComparison.Ordinal));
    }

    private static TradeSearchItemProperty FindProperty(
        TradeSearchDraft draft,
        TradeSearchItemPropertyKind kind) =>
        Assert.Single(draft.ItemProperties, property => property.Kind == kind);

    private static void AssertDisplayedProperty(TradeSearchItemProperty property, decimal expected)
    {
        Assert.Equal(expected, property.ObservedValue);
        Assert.Equal(expected, property.RequestedMinimum);
        Assert.Equal(TradeSearchItemPropertyProviderResolutionStatus.Exact, property.ProviderResolutionStatus);
        Assert.True(property.IsSearchable, property.NotSearchableReason);
    }

    private static bool IsInteractionReady(ResolvedSearchComponent component)
    {
        return (bool)(InteractionReadyMethod.Invoke(null, [component]) ?? false);
    }

    private static string StaticModifierLabel(ResolvedSearchComponent component)
    {
        return (string)(StaticModifierLabelMethod.Invoke(null, [component]) ?? string.Empty);
    }

    private static string ModifierAvailabilityStatus(ResolvedSearchComponent component)
    {
        return (string)(ModifierAvailabilityStatusMethod.Invoke(null, [component]) ?? string.Empty);
    }

    private static void AssertExpectedSupported(
        RuntimeResult runtime,
        string text,
        decimal expectedMinimum,
        PathOfExileTradeStatCatalog catalog)
    {
        var component = FindComponent(runtime.ProviderDraft, text);
        Assert.Equal("Unique", StaticModifierLabel(component));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.True(component.IsSearchable, component.NotSearchableReason);
        Assert.True(IsInteractionReady(component));
        Assert.True(component.SupportsValueBounds);
        Assert.Equal(expectedMinimum, component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.NotEmpty(component.UniqueCatalogBlockIds);
        Assert.NotEmpty(component.UniqueSourceObservationIds);
        var filter = MapSingle(runtime.ProviderDraft, component, catalog);
        Assert.Equal(expectedMinimum, filter.Minimum);
        Assert.Null(filter.Maximum);
    }

    private static void AssertOfficialAdditionalCurseDomains()
    {
        var catalog = OfficialTradeCatalog.Value;
        Assert.True(catalog.Entries.Count > 17_900);
        var template = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
            "You can apply # additional Curses");
        var providerKinds = catalog.FindByNormalizedTemplate(template)
            .Select(PathOfExileTradeStatCandidateClassifier.ToCandidate)
            .Select(PathOfExileTradeStatCandidateClassifier.GetProviderKind)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("crafted", providerKinds);
        Assert.Contains("enchant", providerKinds);
        Assert.Contains("explicit", providerKinds);
        Assert.Contains("fractured", providerKinds);
        Assert.Contains("implicit", providerKinds);
        Assert.Contains("scourge", providerKinds);
    }

    private static void AssertSingleBoundlessQueryFilter(
        RuntimeResult runtime,
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog)
    {
        var selectedDraft = runtime.ProviderDraft with
        {
            ModifierFilters = runtime.ProviderDraft.ModifierFilters
                .Select(candidate => candidate with
                {
                    IsSelected = string.Equals(
                        candidate.ComponentId,
                        component.ComponentId,
                        StringComparison.Ordinal),
                })
                .ToArray(),
        };
        var mapping = SelectedMapper.Map(selectedDraft, catalog);
        Assert.True(mapping.IsSuccess, string.Join(" | ", mapping.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var query = new PathOfExileTradeQueryBuilder().Build(
            selectedDraft,
            new TradeSearchDraftValidator().Validate(selectedDraft),
            "Standard",
            mapping.Filters,
            runtime.UniqueIdentity,
            FilterCatalog);
        Assert.True(query.IsSuccess, string.Join(" | ", query.Diagnostics.Select(diagnostic => diagnostic.Message)));
        using var document = JsonDocument.Parse(query.SerializedJson!);
        var serializedFilter = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.Equal(AdditionalCurseProviderStatId, serializedFilter.GetProperty("id").GetString());
        Assert.False(serializedFilter.TryGetProperty("value", out _));
    }

    private static void AssertSingleQueryFilter(
        RuntimeResult runtime,
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        string expectedStatId,
        decimal? expectedMinimum,
        decimal? expectedMaximum)
    {
        var selectedDraft = runtime.ProviderDraft with
        {
            ModifierFilters = runtime.ProviderDraft.ModifierFilters
                .Select(candidate => candidate with
                {
                    IsSelected = string.Equals(
                        candidate.ComponentId,
                        component.ComponentId,
                        StringComparison.Ordinal),
                })
                .ToArray(),
        };
        var mapping = SelectedMapper.Map(selectedDraft, catalog);
        Assert.True(mapping.IsSuccess, string.Join(" | ", mapping.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var query = new PathOfExileTradeQueryBuilder().Build(
            selectedDraft,
            new TradeSearchDraftValidator().Validate(selectedDraft),
            "Standard",
            mapping.Filters,
            runtime.UniqueIdentity,
            FilterCatalog);
        Assert.True(query.IsSuccess, string.Join(" | ", query.Diagnostics.Select(diagnostic => diagnostic.Message)));
        using var document = JsonDocument.Parse(query.SerializedJson!);
        var serializedFilter = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.Equal(expectedStatId, serializedFilter.GetProperty("id").GetString());
        if (!expectedMinimum.HasValue && !expectedMaximum.HasValue)
        {
            Assert.False(serializedFilter.TryGetProperty("value", out _));
            return;
        }

        var value = serializedFilter.GetProperty("value");
        if (expectedMinimum.HasValue)
        {
            Assert.Equal(expectedMinimum.Value, value.GetProperty("min").GetDecimal());
        }
        else
        {
            Assert.False(value.TryGetProperty("min", out _));
        }

        if (expectedMaximum.HasValue)
        {
            Assert.Equal(expectedMaximum.Value, value.GetProperty("max").GetDecimal());
        }
        else
        {
            Assert.False(value.TryGetProperty("max", out _));
        }
    }

    private static PathOfExileTradePriceCheckService CreatePriceCheckService()
    {
        return new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatProvider(TradeCatalog),
            new StaticItemProvider(TradeItemCatalog),
            SelectedMapper,
            new PathOfExileTradeItemIdentityMapper(),
            new NoSearchClient(),
            new NoFetchClient());
    }

    private static PathOfExileTradeStatCatalog CreateTradeCatalog()
    {
        return new PathOfExileTradeStatCatalog(
        [
            Entry(0, "implicit.stat_658456881", "+# to Minimum Frenzy Charges", "implicit"),
            Entry(1, "implicit.stat_1515657623", "+# to Maximum Endurance Charges", "implicit"),
            Entry(2, "implicit.stat_2250533757", "#% increased Movement Speed", "implicit"),
            Entry(3, "fractured.stat_624954515", "#% increased Global Accuracy Rating", "fractured"),
            Entry(4, "fractured.stat_1263695895", "#% increased Light Radius", "fractured"),
            Entry(5, "explicit.stat_388617051", "#% increased Charges per use", "explicit"),
            Entry(6, "explicit.stat_1256719186", "#% increased Duration", "explicit"),
            Entry(7, "explicit.stat_41860024", "When Hit during effect, #% of Life loss from Damage taken occurs over 4 seconds instead", "explicit"),
            Entry(8, "explicit.stat_117885424", "Adds # to # Chaos Damage to Attacks per 80 Strength", "explicit"),
            Entry(9, "explicit.stat_3180152291", "Cannot deal non-Chaos Damage", "explicit"),
            Entry(10, "explicit.stat_4077843608", "Has 1 Socket", "explicit"),
            Entry(11, "explicit.stat_3986704288", "Magic Utility Flasks cannot be Used", "explicit"),
            Entry(12, "explicit.stat_1509134228", "#% increased Physical Damage", "explicit"),
            Entry(13, "explicit.stat_752930724", "Items and Gems have #% increased Attribute Requirements", "explicit"),
            Entry(14, "explicit.stat_30642521", "You can apply # additional Curses", "explicit"),
        ]);
    }

    private static PathOfExileTradeStatEntry Entry(int order, string id, string text, string type)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = order,
            GroupId = type,
            GroupLabel = type,
            Id = id,
            Text = text,
            Type = type,
        };
    }

    private static PathOfExileTradeItemCatalog CreateTradeItemCatalog()
    {
        return new PathOfExileTradeItemCatalog(
        [
            UniqueItem(0, "Replica Dragonfang's Flight", "Onyx Amulet", "accessory"),
            UniqueItem(1, "Torchoak Step", "Antique Greaves", "armour"),
            UniqueItem(2, "Progenesis", "Amethyst Flask", "flask"),
            UniqueItem(3, "Replica Alberon's Warpath", "Soldier Boots", "armour"),
            UniqueItem(4, "The Squire", "Elegant Round Shield", "armour"),
            UniqueItem(5, "Mageblood", "Heavy Belt", "accessory"),
            UniqueItem(6, "Last Resort", "Nailed Fist", "weapon"),
            UniqueItem(7, "Windscream", "Reinforced Greaves", "armour"),
            UniqueItem(8, "Doedre's Damning", "Paua Ring", "accessory"),
            UniqueItem(9, "Cospri's Will", "Assassin's Garb", "armour"),
            UniqueItem(10, "Reverberation Rod", "Spiraled Wand", "weapon"),
            UniqueItem(11, "Energy From Within", "Cobalt Jewel", "jewel"),
            UniqueItem(12, "Lethal Pride", "Timeless Jewel", "jewel"),
            UniqueItem(13, "Brutal Restraint", "Timeless Jewel", "jewel"),
            UniqueItem(14, "Glorious Vanity", "Timeless Jewel", "jewel"),
            UniqueItem(15, "Militant Faith", "Timeless Jewel", "jewel"),
            UniqueItem(16, "Replica Bated Breath", "Chain Belt", "accessory"),
            UniqueItem(17, "Augyre", "Void Sceptre", "weapon"),
            UniqueItem(18, "Wurm's Molt", "Leather Belt", "accessory"),
            UniqueItem(19, "Circle of Anguish", "Ruby Ring", "accessory"),
            UniqueItem(20, "Circle of Fear", "Sapphire Ring", "accessory"),
            UniqueItem(21, "Split Personality", "Crimson Jewel", "jewel"),
            UniqueItem(22, "Coralito's Signature", "Diamond Flask", "flask"),
            UniqueItem(23, "Asenath's Mark", "Iron Circlet", "armour"),
            UniqueItem(24, "Hrimnor's Resolve", "Samnite Helmet", "armour"),
            UniqueItem(25, "Mark of the Red Covenant", "Tribal Circlet", "armour"),
            UniqueItem(26, "Bones of Ullr", "Silk Slippers", "armour"),
            UniqueItem(27, "The Battle Within", "Oakbranch Tincture", "tincture"),
        ]);
    }

    private static PathOfExileTradeItemEntry UniqueItem(
        int order,
        string name,
        string type,
        string groupId)
    {
        return new PathOfExileTradeItemEntry
        {
            ProviderOrder = order,
            GroupId = groupId,
            GroupLabel = groupId,
            Name = name,
            Type = type,
            IsUnique = true,
        };
    }

    private static GameDataCatalog LoadGameData()
    {
        var result = GameDataPackageLoader
            .LoadFromFileAsync(FindRepoFile("artifacts", "poenhance-game-data.json"))
            .GetAwaiter()
            .GetResult();
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(result.Package));
    }

    private static PathOfExileTradeStatCatalog LoadOfficialTradeCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        var result = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        Assert.True(result.IsSuccess, string.Join(" | ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.IsType<PathOfExileTradeStatCatalog>(result.Catalog);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(relativeParts)}");
    }

    private sealed record RuntimeResult(
        ParsedItem Parsed,
        ItemBaseResolutionResult BaseResolution,
        IReadOnlyList<ModifierCandidateResolutionResult> SourceResolutions,
        TradeSearchDraft ProviderDraft,
        PathOfExileTradeItemIdentity? UniqueIdentity);

    private sealed class StaticStatProvider(PathOfExileTradeStatCatalog catalog) :
        IPathOfExileTradeStatCatalogProvider
    {
        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
    }

    private sealed class StaticItemProvider(PathOfExileTradeItemCatalog catalog) :
        IPathOfExileTradeItemCatalogProvider
    {
        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(
            PathOfExileTradeItemCatalogProviderResult.Success(catalog));
    }

    private sealed class NoSearchClient : IPathOfExileTradeSearchClient
    {
        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradeSearchExecutionResult());
    }

    private sealed class NoFetchClient : IPathOfExileTradeFetchClient
    {
        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradeFetchExecutionResult());
    }

    private const string AsenathsMarkCompositionText = """
Item Class: Helmets
Rarity: Unique
Asenath's Mark
Iron Circlet
--------
Item Level: 80
--------
{ Unique Modifier — Defences, Energy Shield }
+39(30-50) to maximum Energy Shield
""";

    private const string HrimnorsResolveCompositionText = """
Item Class: Helmets
Rarity: Unique
Hrimnor's Resolve
Samnite Helmet
--------
Item Level: 80
--------
{ Unique Modifier — Defences, Armour }
108(100-120)% increased Armour
""";

    private const string MarkOfTheRedCovenantCompositionText = """
Item Class: Helmets
Rarity: Unique
Mark of the Red Covenant
Tribal Circlet
--------
Item Level: 80
--------
{ Unique Modifier — Defences, Energy Shield }
+45(30-50) to maximum Energy Shield
{ Unique Modifier — Elemental, Fire, Minion }
Summoned Raging Spirits' Melee Strikes deal Fire-only Splash
Damage to Surrounding Targets
""";

    private const string MarkOfTheRedCovenantReducedSpiritsText = """
Item Class: Helmets
Rarity: Unique
Mark of the Red Covenant
Tribal Circlet
--------
Item Level: 80
--------
+45(30-50) to maximum Energy Shield
11(10-15)% increased Stun and Block Recovery
Summoned Raging Spirits deal 200(175-250)% increased Damage
75% reduced Maximum number of Summoned Raging Spirits
Summoned Raging Spirits' Melee Strikes deal Fire-only Splash
Damage to Surrounding Targets
Summoned Raging Spirits' Hits always Ignite
""";

    private const string BonesOfUllrCompositionText = """
Item Class: Boots
Rarity: Unique
Bones of Ullr
Silk Slippers
--------
Item Level: 80
--------
{ Unique Modifier — Life }
+20 to maximum Life
{ Unique Modifier — Mana }
+20 to maximum Mana
{ Unique Modifier — Minion, Gem }
+1 to Level of all Raise Zombie Gems
+1 to Level of all Raise Spectre Gems
""";

    private const string BattleWithinCompositionText = """
Item Class: Tinctures
Rarity: Unique
The Battle Within
Oakbranch Tincture
--------
Item Level: 80
--------
{ Unique Modifier — Attack }
Does not inflict Mana Burn over time
Inflicts Mana Burn on you when you Hit an Enemy with a Melee Weapon
""";

    private const string DragonfangText = """
Item Class: Amulets
Rarity: Unique
Replica Dragonfang's Flight
Onyx Amulet
--------
Item Level: 80
--------
{ Corruption Implicit Modifier }
+1 to Minimum Frenzy Charges
--------
"Did we make this? Why do we have no record of it?
We were warned that there would be consequences..."
- Administrator Qotra
--------
Corrupted
""";

    private const string TorchoakStepText = """
Item Class: Boots
Rarity: Unique
Torchoak Step
Antique Greaves
--------
Armour: 273 (augmented)
--------
Sockets: W-W
--------
Item Level: 83
--------
{ Corruption Implicit Modifier }
+1 to Maximum Endurance Charges
{ Corruption Implicit Modifier — Speed }
9(8-10)% increased Movement Speed
--------
{ Unique Modifier }
25% increased Movement Speed
--------
Corrupted
""";

    private const string BoneRingText = """
Item Class: Rings
Rarity: Rare
Ghoul Circle
Bone Ring
--------
Item Level: 85
--------
{ Fractured Suffix Modifier "of Light" (Tier: 2) — Attack }
12(12-15)% increased Global Accuracy Rating
10% increased Light Radius
--------
Fractured Item
""";

    private const string EnergyFromWithinText = """
Item Class: Jewels
Rarity: Unique
Energy From Within
Cobalt Jewel
--------
Item Level: 82
--------
{ Corruption Implicit Modifier }
Corrupted Blood cannot be inflicted on you
--------
Corrupted
""";

    private const string LethalPrideText = """
Item Class: Jewels
Rarity: Unique
Lethal Pride
Timeless Jewel
--------
Item Level: 86
--------
{ Unique Modifier }
Commanded leadership over 14245(10000-18000) warriors under Rakiata(Akoya-Rakiata)
Passives in radius are Conquered by the Karui
Historic
""";

    private const string BrutalRestraintText = """
Item Class: Jewels
Rarity: Unique
Brutal Restraint
Timeless Jewel
--------
Item Level: 86
--------
{ Unique Modifier }
Denoted service of 2844(500-8000) dekhara in the akhara of Asenath(Asenath-Nasima)
Passives in radius are Conquered by the Maraketh
Historic
""";

    private const string GloriousVanityText = """
Item Class: Jewels
Rarity: Unique
Glorious Vanity
Timeless Jewel
--------
Item Level: 86
--------
{ Unique Modifier }
Bathed in the blood of 1073(100-8000) sacrificed in the name of Ahuana(Ahuana-Xibaqua)
Passives in radius are Conquered by the Vaal
Historic
""";

    private const string MilitantFaithText = """
Item Class: Jewels
Rarity: Unique
Militant Faith
Timeless Jewel
--------
Item Level: 86
--------
{ Unique Modifier }
Carved to glorify 2549(2000-10000) new faithful converted by High Templar Avarius(Avarius-Maxarius)
Passives in radius are Conquered by the Templars
Historic
""";

    private const string ReplicaBatedBreathText = """
Item Class: Belts
Rarity: Unique
Replica Bated Breath
Chain Belt
--------
Item Level: 85
--------
{ Implicit Modifier — Defences, Energy Shield }
+20(9-20) to maximum Energy Shield
--------
{ Unique Modifier — Attribute }
-20(-25--15) to Intelligence
{ Unique Modifier — Damage }
10% increased Damage
{ Unique Modifier }
50% increased Fishing Pool Consumption
{ Unique Modifier }
20% increased Fishing Range
{ Unique Modifier — Drop }
26(20-30)% increased Rarity of Fish Caught
""";

    private const string AugyreText = """
Item Class: Sceptres
Rarity: Unique
Augyre
Void Sceptre
--------
Item Level: 85
--------
{ Implicit Modifier — Damage, Elemental }
40% increased Elemental Damage
--------
{ Unique Modifier — Damage, Physical, Attack }
205(180-220)% increased Physical Damage
{ Unique Modifier — Attack, Speed }
10(10-15)% increased Attack Speed
{ Unique Modifier — Attack, Critical }
85(80-100)% increased Critical Strike Chance
{ Unique Modifier — Damage, Physical, Elemental, Lightning }
50% of Physical Damage Converted to Lightning Damage
{ Unique Modifier — Damage, Elemental, Critical }
Every 16 seconds you gain Elemental Overload for 8 seconds
{ Unique Modifier — Attack, Critical }
You have Resolute Technique while you do not have Elemental Overload
{ Unique Modifier — Damage, Physical }
100% increased Physical Damage while you have Resolute Technique
""";

    private const string ProgenesisText = """
Item Class: Utility Flasks
Rarity: Unique
Progenesis
Amethyst Flask
--------
Quality: +20% (augmented)
Lasts 10,50 (augmented) Seconds
Consumes 30 (augmented) of 65 Charges on use
Currently has 65 Charges
Intangibility: 8%
+35% to Chaos Resistance
--------
Requirements:
Level: 60
--------
Item Level: 84
--------
Used when Charges reach full (enchant)
--------
{ Unique Modifier }
14(20-10)% reduced Charges per use
{ Unique Modifier }
34(-35-35)% increased Duration
{ Unique Modifier }
When Hit during effect, 25% of Life loss from Damage taken occurs over 4 seconds instead
--------
They were bred in a cosmic ocean of raw creation.
Feasting and drinking of the milk of the mother,
they fought to the death for every last drop.
--------
Right click to drink. Can only hold charges while in belt. Refills as you kill monsters.
--------
Foil Unique (Celestial Quartz)
""";

    private const string ReplicaAlberonsText = """
Item Class: Boots
Rarity: Unique
Replica Alberon's Warpath
Soldier Boots
--------
Quality: +20% (augmented)
Armour: 377 (augmented)
Energy Shield: 22 (augmented)
--------
Requirements:
Level: 70
Str: 155
Int: 47
--------
Sockets: R-W-W-R
--------
Item Level: 84
--------
{ Unique Modifier — Attribute }
19(15-18)% increased Strength
{ Unique Modifier — Defences, Armour }
+226(180-220) to Armour
{ Unique Modifier — Chaos, Resistance }
+13(13-19)% to Chaos Resistance
{ Unique Modifier — Speed }
20(25)% increased Movement Speed
{ Unique Modifier — Damage, Chaos }
Cannot deal non-Chaos Damage
{ Unique Modifier — Damage, Chaos, Attack }
Adds 1 to 82(80) Chaos Damage to Attacks per 80 Strength
--------
"Starving test subject became completely incapable of exerting force.
However, after being fed, he began to poison everything he touched..."
--------
Corrupted
""";

    private const string SquireText = """
Item Class: Shields
Rarity: Unique
The Squire
Elegant Round Shield
--------
Quality: +20% (augmented)
Chance to Block: 30% (augmented)
Armour: 420 (augmented)
Evasion Rating: 420 (augmented)
--------
Requirements:
Level: 76
Str: 111
Dex: 120
--------
Sockets: R-G-G
--------
Item Level: 84
--------
{ Implicit Modifier }
120% increased Block Recovery
--------
{ Unique Modifier }
Has 3 Sockets
{ Unique Modifier — Gem }
+8(5-8)% to Quality of Socketed Support Gems
{ Unique Modifier }
Socketed Support Gems can also Support Skills from your Main Hand
{ Unique Modifier — Defences, Armour, Evasion }
107(100-150)% increased Armour and Evasion
{ Unique Modifier }
+5(3-5)% Chance to Block
--------
Judge not the weak, for
they empower the strong.
""";

    private const string FoulbornMagebloodText = """
Item Class: Belts
Rarity: Unique
Foulborn Mageblood
Heavy Belt
--------
Quality (Attribute Modifiers): +20% (augmented)
--------
Requirements:
Level: 44
--------
Item Level: 80
--------
{ Implicit Modifier — Attribute  — 20% Increased }
+35(25-35) to Strength
--------
{ Unique Modifier — Attribute  — 20% Increased }
+49(30-50) to Dexterity
{ Unique Modifier — Elemental, Fire, Resistance }
+25(15-25)% to Fire Resistance
{ Unique Modifier — Elemental, Cold, Resistance }
+15(15-25)% to Cold Resistance
{ Unique Modifier }
Magic Utility Flasks cannot be Used
{ Unique Modifier }
Magic Utility Flask Effects cannot be removed
{ Foulborn Unique Modifier }
Rightmost 4(2-4) Magic Utility Flasks constantly apply their Flask Effects to you
--------
Rivers of power course through your veins.
""";

    // Verbatim Ctrl+D bodies from the authoritative E6 live capture. Windscream and Doedre's
    // Damning emit "Monster Modifier" for the additional-curse row; Cospri's Will emits
    // "Unique Modifier" for the same semantic stat and acts as the control.
    private const string WindscreamText = """
Item Class: Boots
Rarity: Unique
Windscream
Reinforced Greaves
--------
Armour: 173 (augmented)
--------
Requirements:
Level: 33
Str: 61
--------
Sockets: W-W-W
--------
Item Level: 85
--------
{ Unique Modifier — Defences, Armour }
59(50-80)% increased Armour
{ Unique Modifier — Elemental, Resistance }
+11(10-15)% to all Elemental Resistances
{ Monster Modifier — Caster, Curse }
You can apply an additional Curse
{ Unique Modifier — Speed }
20% increased Movement Speed
{ Unique Modifier — Caster, Curse }
50% increased Area of Effect of Hex Skills
--------
The mocking wind, a shielding spell,
The haunting screams, a maddening hell.
""";

    private const string DoedresDamningText = """
Item Class: Rings
Rarity: Unique
Doedre's Damning
Paua Ring
--------
Item Level: 85
--------
{ Implicit Modifier — Mana }
+28(20-30) to maximum Mana
--------
{ Unique Modifier — Attribute }
+11(5-20) to Intelligence
{ Unique Modifier — Elemental, Resistance }
+18(5-20)% to all Elemental Resistances
{ Unique Modifier — Mana }
Gain 17(5-20) Mana per Enemy Killed
{ Monster Modifier — Caster, Curse }
You can apply an additional Curse
--------
Where her mouth should have been
there was only a whirling, black void.
""";

    private const string CospriWillText = """
Item Class: Body Armours
Rarity: Unique
Cospri's Will
Assassin's Garb
--------
Evasion Rating: 2045 (augmented)
--------
Requirements:
Level: 68
Dex: 183 (unmet)
--------
Sockets: W W-W
--------
Item Level: 83
--------
{ Implicit Modifier — Speed }
3% increased Movement Speed
--------
{ Unique Modifier — Defences, Evasion }
168(150-200)% increased Evasion Rating
{ Unique Modifier — Caster, Curse }
You can apply an additional Curse
{ Unique Modifier — Chaos, Resistance }
+43(31-53)% to Chaos Resistance
{ Unique Modifier }
Your Hexes can affect Hexproof Enemies — Unscalable Value
{ Unique Modifier — Chaos, Ailment }
Always Poison on Hit against Cursed Enemies
(Poison deals Chaos Damage over time, based on the base Physical and Chaos Damage of the Skill. Multiple instances of Poison stack)
--------
Curse their vile Council,
They cast me aside as if I am some bastard child.
If they only knew the power I possess.
""";

    private const string LastResortText = """
Item Class: Claws
Rarity: Unique
Last Resort
Nailed Fist
--------
Claw
Quality: +20% (augmented)
Physical Damage: 14-49 (augmented)
Critical Strike Chance: 8.39% (augmented)
Attacks per Second: 1.60
Weapon Range: 1.1 metres
--------
Item Level: 80
--------
{ Unique Modifier â€” Damage, Physical, Attack }
94(80-100)% increased Physical Damage
{ Unique Modifier â€” Damage, Physical, Attack }
Adds 2 to 10 Physical Damage
--------
Desperate times demand desperate measures.
""";

    private const string DragonfangAttributeRequirementsText = """
Item Class: Amulets
Rarity: Unique
Replica Dragonfang's Flight
Onyx Amulet
--------
Item Level: 80
--------
{ Unique Modifier }
Items and Gems have 5(10-5)% reduced Attribute Requirements
(Attributes are Strength, Dexterity, and Intelligence)
--------
"Did we make this? Why do we have no record of it?
We were warned that there would be consequences..."
- Administrator Qotra
""";

    private const string EbersUnificationText = """
Item Class: Helmets
Rarity: Unique
Eber's Unification
Hubris Circlet
--------
Item Level: 84
--------
{ Unique Modifier }
Trigger Level 10 Void Gaze when you use a Skill — Unscalable Value
--------
Corrupted
""";

    private const string ReverberationRodText = """
Item Class: Wands
Rarity: Unique
Reverberation Rod
Spiraled Wand
--------
Wand
--------
Item Level: 85
--------
{ Implicit Modifier }
Adds 2(1-2) to 10(9-11) Lightning Damage to Spells and Attacks
{ Corruption Implicit Modifier }
7(5-7)% chance to gain a Power Charge on Critical Strike
--------
{ Unique Modifier }
+2 to Level of Socketed Gems
{ Unique Modifier }
Socketed Gems are Supported by Level 10 Spell Echo — Unscalable Value
{ Unique Modifier }
Socketed Gems are Supported by Level 10 Controlled Destruction — Unscalable Value
{ Unique Modifier }
Socketed Gems are Supported by Level 10 Arcane Surge — Unscalable Value
{ Unique Modifier }
+21(10-30) to Intelligence
--------
Corrupted
""";

    private const string RareSpiraledWandText = """
Item Class: Wands
Rarity: Rare
Witness Spiral
Spiraled Wand
--------
Wand
--------
Item Level: 85
--------
{ Implicit Modifier }
Adds 2(1-2) to 10(9-11) Lightning Damage to Spells and Attacks
""";
}
