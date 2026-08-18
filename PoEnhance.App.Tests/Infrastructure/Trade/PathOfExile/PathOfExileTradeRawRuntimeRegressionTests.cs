using System.Collections.Immutable;
using System.Reflection;
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
    private static readonly PathOfExileTradeItemCatalog TradeItemCatalog = CreateTradeItemCatalog();
    private static readonly PathOfExileTradeFilterCatalog FilterCatalog =
        PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog();
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();
    private static readonly PathOfExileTradeItemPropertyResolver ItemPropertyResolver = new();
    private static readonly MethodInfo InteractionReadyMethod = typeof(PriceCheckerSearchController)
        .GetMethod("IsModifierInteractionReady", BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new MissingMethodException(nameof(PriceCheckerSearchController), "IsModifierInteractionReady");

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

        AssertExactSelectable(sockets, "explicit.stat_4077843608");
        Assert.Equal([3m], sockets.ObservedNumericValues);
        Assert.Equal(3m, sockets.RequestedMinimum);
        Assert.Equal(3m, sockets.RequestedMaximum);
        var filter = MapSingle(runtime.ProviderDraft, sockets);
        Assert.Equal(3m, filter.Minimum);
        Assert.Equal(3m, filter.Maximum);
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

    private static RuntimeResult Resolve(string rawText)
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
            TradeCatalog,
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
        ResolvedSearchComponent component)
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
        var mapping = SelectedMapper.Map(selectedDraft, TradeCatalog);
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
}
