using System.Text.Json;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.PathOfExile;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeFracturedApproximationProductionTests
{
    private const string League = "Mirage";

    [Fact]
    public async Task SkullTrack_NumericEnchantmentUsesEnchantCollisionCandidateAndObservedBound()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat(
                "explicit.stat_4291115328",
                "#% of Damage Leeched as Life if you've Killed Recently",
                "explicit",
                0),
            Stat(
                "enchant.stat_4291115328",
                "#% of Damage Leeched as Life if you've Killed Recently",
                "enchant",
                1),
        ]);
        var searchClient = new RecordingSearchClient();
        var service = CreateService(catalog, FracturedStateFilterCatalog(), searchClient);
        var sourceDraft = ParseProductionDraft(SkullTrackText);
        var prepared = await service.PrepareEffectiveDraftAsync(sourceDraft);
        var enchantment = Assert.Single(prepared.ModifierFilters, component =>
            component.ParsedKind == ParsedModifierKind.Enchantment);

        Assert.Equal(
            "0.6% of Damage Leeched as Life if you've Killed Recently",
            enchantment.OriginalText);
        Assert.Equal(ParsedModifierKind.Enchantment, enchantment.ResolvedSourceKind);
        Assert.Equal(ModifierGenerationType.Enchantment, enchantment.GenerationType);
        Assert.Equal(ParsedUniqueModifierOrigin.Unspecified, enchantment.UniqueOrigin);
        Assert.True(
            enchantment.ResolutionStatus == ModifierCandidateResolutionStatus.Exact,
            $"{enchantment.NotSearchableReason}; {enchantment.ProviderDiagnosticCode}: " +
            enchantment.ProviderDiagnosticMessage);
        Assert.Contains(enchantment.ProviderDomainEvidence, evidence =>
            evidence.IsSourceExact &&
            string.Equals(evidence.ProviderDomain, "Enchant", StringComparison.Ordinal));
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, enchantment.ProviderResolutionStatus);
        Assert.Equal("enchant.stat_4291115328", enchantment.ProviderStatId);
        Assert.Equal(ModifierBoundShape.Scalar, enchantment.ValueBoundShape);
        Assert.True(enchantment.SupportsValueBounds);
        Assert.Equal([0.6m], enchantment.ObservedNumericValues);
        Assert.Equal(0.6m, enchantment.RequestedMinimum);
        Assert.Null(enchantment.RequestedMaximum);
        Assert.All(enchantment.FilterVariants, variant =>
            Assert.Equal("enchant", variant.ProviderKind));
        Assert.DoesNotContain(prepared.ModifierFilters, component =>
            component.OriginalText.StartsWith("(Leeched Life", StringComparison.Ordinal) ||
            component.OriginalText.StartsWith("(Recently", StringComparison.Ordinal));

        var selected = SelectOnly(prepared, enchantment.ComponentId);
        var result = await service.CheckAsync(
            selected,
            new TradeSearchDraftValidator().Validate(selected),
            League);

        Assert.True(
            result.IsSuccess,
            string.Join(" | ", result.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}")));
        using var document = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(
            Assert.Single(searchClient.Requests)));
        var filter = Assert.Single(document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.Equal("enchant.stat_4291115328", filter.GetProperty("id").GetString());
        Assert.Equal(0.6m, filter.GetProperty("value").GetProperty("min").GetDecimal());
        Assert.False(filter.GetProperty("value").TryGetProperty("max", out _));
    }

    [Fact]
    public async Task SkullTrack_FullCopiedItemUsesEquivalentFracturedOrExplicitSetInFinalJson()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("fractured.stat_3680664274", "+#% chance to Suppress Spell Damage", "fractured", 0),
            Stat("fractured.stat_492027537", "+#% chance to Suppress Spell Damage", "fractured", 1),
            Stat("explicit.stat_3680664274", "+#% chance to Suppress Spell Damage", "explicit", 2),
            Stat("explicit.stat_492027537", "+#% chance to Suppress Spell Damage", "explicit", 3),
        ]);
        var searchClient = new RecordingSearchClient();
        var service = CreateService(catalog, FracturedStateFilterCatalog(), searchClient);
        var sourceDraft = ParseProductionDraft(SkullTrackText);
        var prepared = await service.PrepareEffectiveDraftAsync(sourceDraft);
        var suppress = Assert.Single(prepared.ModifierFilters, component =>
            component.IsFractured &&
            component.OriginalText.Contains("Suppress Spell Damage", StringComparison.Ordinal));

        Assert.True(suppress.IsFractured);
        Assert.Equal("fractured", suppress.RequestedFilterVariantKind);
        Assert.Equal(SearchComponentProviderResolutionStatus.ExactEquivalentSet, suppress.ProviderResolutionStatus);
        Assert.Equal(2, suppress.ProviderStatAlternativeIds.Count);
        Assert.Equal(12m, suppress.RequestedMinimum);
        Assert.Contains(suppress.FilterVariants, option =>
            option.ProviderKind == "explicit" && option.ProviderAlternativeCount == 2);

        var selectedFractured = SelectOnly(prepared, suppress.ComponentId);
        var fracturedResult = await service.CheckAsync(
            selectedFractured,
            new TradeSearchDraftValidator().Validate(selectedFractured),
            League);
        Assert.True(
            fracturedResult.IsSuccess,
            string.Join(" | ", fracturedResult.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}")));
        using (var document = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(
                   searchClient.Requests[^1])))
        {
            var query = document.RootElement.GetProperty("query");
            var group = Assert.Single(query.GetProperty("stats").EnumerateArray());
            Assert.Equal("count", group.GetProperty("type").GetString());
            Assert.Equal(1m, group.GetProperty("value").GetProperty("min").GetDecimal());
            Assert.Equal(2, group.GetProperty("filters").GetArrayLength());
            Assert.All(group.GetProperty("filters").EnumerateArray(), filter =>
                Assert.Equal(12m, filter.GetProperty("value").GetProperty("min").GetDecimal()));
            Assert.False(query
                .GetProperty("filters")
                .GetProperty("misc_filters")
                .GetProperty("filters")
                .TryGetProperty("fractured_item", out _));
        }

        var explicitOption = Assert.Single(suppress.FilterVariants, option =>
            option.ProviderKind == "explicit");
        var selectedExplicit = selectedFractured with
        {
            ModifierFilters = selectedFractured.ModifierFilters
                .Select(component => component.ComponentId == suppress.ComponentId
                    ? component with
                    {
                        RequestedFilterVariantIdentity = explicitOption.Identity,
                        RequestedFilterVariantKind = explicitOption.ProviderKind,
                    }
                    : component)
                .ToArray(),
        };
        var explicitResult = await service.CheckAsync(
            selectedExplicit,
            new TradeSearchDraftValidator().Validate(selectedExplicit),
            League);
        Assert.True(explicitResult.IsSuccess);
        using var explicitDocument = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(
            searchClient.Requests[^1]));
        var explicitQuery = explicitDocument.RootElement.GetProperty("query");
        var explicitGroup = Assert.Single(explicitQuery.GetProperty("stats").EnumerateArray());
        Assert.Equal("count", explicitGroup.GetProperty("type").GetString());
        Assert.All(explicitGroup.GetProperty("filters").EnumerateArray(), filter =>
            Assert.StartsWith("explicit.", filter.GetProperty("id").GetString(), StringComparison.Ordinal));
        Assert.False(explicitQuery
            .GetProperty("filters")
            .GetProperty("misc_filters")
            .GetProperty("filters")
            .TryGetProperty("fractured_item", out _));
        var explicitEffective = Assert.IsType<TradeSearchDraft>(explicitResult.EffectiveDraft);
        var explicitComponent = Assert.Single(explicitEffective.ModifierFilters, component =>
            component.ComponentId == suppress.ComponentId);
        Assert.True(explicitComponent.IsFractured);
        Assert.Equal("explicit", explicitComponent.RequestedFilterVariantKind);
        Assert.Equal(SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            explicitComponent.ProviderResolutionStatus);
    }

    [Fact]
    public async Task MiracleTouch_FullCopiedItemKeepsDualFracturedSourcesIndependentAndResolvesOrdinarySuppress()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat("fractured.stat_3299347043", "+# to maximum Life", "fractured", 0),
            Stat("explicit.stat_3299347043", "+# to maximum Life", "explicit", 1),
            Stat("fractured.stat_1510714129", "Attacks have #% chance to Maim on Hit", "fractured", 2),
            Stat("explicit.stat_1510714129", "Attacks have #% chance to Maim on Hit", "explicit", 3),
            Stat("explicit.stat_3680664274", "+#% chance to Suppress Spell Damage", "explicit", 4),
            Stat("explicit.stat_492027537", "+#% chance to Suppress Spell Damage", "explicit", 5),
            Stat("explicit.stat_210067635", "#% increased Attack Speed (Local)", "explicit", 6),
            Stat("implicit.stat_210067635", "#% increased Attack Speed (Local)", "implicit", 7),
        ]);
        var searchClient = new RecordingSearchClient();
        var service = CreateService(catalog, FracturedStateFilterCatalog(), searchClient);
        var sourceDraft = ParseProductionDraft(MiracleTouchText);
        var prepared = await service.PrepareEffectiveDraftAsync(sourceDraft);
        var life = Assert.Single(prepared.ModifierFilters, component =>
            component.IsFractured &&
            component.OriginalText.Contains("maximum Life", StringComparison.Ordinal));
        var maim = Assert.Single(prepared.ModifierFilters, component =>
            component.IsFractured &&
            component.OriginalText.Contains("Maim on Hit", StringComparison.Ordinal));
        var suppress = Assert.Single(prepared.ModifierFilters, component =>
            !component.IsFractured &&
            component.OriginalText.Contains("Suppress Spell Damage", StringComparison.Ordinal));

        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, life.ProviderResolutionStatus);
        Assert.Equal(76m, life.RequestedMinimum);
        Assert.True(
            maim.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact,
            $"{maim.ProviderResolutionStatus}: {maim.ProviderDiagnosticMessage}; " +
            $"reason={maim.NotSearchableReason}; signature={maim.CanonicalSignature}; " +
            $"requested={maim.RequestedFilterVariantKind}/{maim.RequestedFilterVariantIdentity}; " +
            $"variants={string.Join(", ", maim.FilterVariants.Select(option =>
                $"{option.ProviderKind}:{option.Identity}"))}");
        Assert.Equal(21m, maim.RequestedMinimum);
        Assert.Equal(SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            suppress.ProviderResolutionStatus);
        Assert.Equal(2, suppress.ProviderStatAlternativeIds.Count);
        Assert.DoesNotContain(prepared.ModifierFilters, component =>
            component.OriginalText.Contains("Maimed enemies", StringComparison.Ordinal));
        Assert.Contains(prepared.ModifierFilters, component =>
            !component.IsFractured &&
            component.OriginalText.Contains("increased Attack Speed", StringComparison.Ordinal));
        Assert.Contains(prepared.ModifierFilters, component => component.IsCrafted &&
            component.OriginalText.Contains("Converted to Cold", StringComparison.Ordinal));
        Assert.Equal(2, prepared.ModifierFilters.Count(component =>
            component.ImplicitOrigin is ParsedImplicitModifierOrigin.SearingExarch
                or ParsedImplicitModifierOrigin.EaterOfWorlds));

        var selected = SelectOnly(prepared, life.ComponentId, maim.ComponentId);
        var result = await service.CheckAsync(
            selected,
            new TradeSearchDraftValidator().Validate(selected),
            League);
        Assert.True(result.IsSuccess);
        using var document = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(
            Assert.Single(searchClient.Requests)));
        var query = document.RootElement.GetProperty("query");
        var groups = query.GetProperty("stats").EnumerateArray().ToArray();
        Assert.Equal(2, groups.Length);
        Assert.All(groups, group =>
        {
            Assert.Equal("and", group.GetProperty("type").GetString());
            Assert.Single(group.GetProperty("filters").EnumerateArray());
        });
        Assert.Equal(
            [76m, 21m],
            groups.Select(group => group.GetProperty("filters")[0]
                .GetProperty("value")
                .GetProperty("min")
                .GetDecimal()));
        Assert.False(query
            .GetProperty("filters")
            .GetProperty("misc_filters")
            .GetProperty("filters")
            .TryGetProperty("fractured_item", out _));

        var explicitLife = Assert.Single(life.FilterVariants, option =>
            option.ProviderKind == "explicit");
        var changed = service.ResolveProviderComponents(
            prepared with
            {
                ModifierFilters = prepared.ModifierFilters
                    .Select(component => component.ComponentId == life.ComponentId
                        ? component with
                        {
                            RequestedFilterVariantIdentity = explicitLife.Identity,
                            RequestedFilterVariantKind = explicitLife.ProviderKind,
                        }
                        : component)
                    .ToArray(),
            },
            catalog,
            filterCatalog: FracturedStateFilterCatalog());
        var changedLife = Assert.Single(changed.ModifierFilters, component =>
            component.ComponentId == life.ComponentId);
        var unchangedMaim = Assert.Single(changed.ModifierFilters, component =>
            component.ComponentId == maim.ComponentId);
        Assert.Equal("explicit", changedLife.RequestedFilterVariantKind);
        Assert.Equal(76m, changedLife.RequestedMinimum);
        Assert.Equal(maim.RequestedMinimum, unchangedMaim.RequestedMinimum);
        Assert.Equal("fractured", unchangedMaim.RequestedFilterVariantKind);
    }

    [Fact]
    public async Task Thirsty_FullCopiedItemResolvesGlobalSourceBoundsAndFinalJson()
    {
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Stat(
                "fractured.stat_3237948413",
                "#% of Physical Attack Damage Leeched as Mana",
                "fractured",
                0),
            Stat(
                "fractured.stat_669069897",
                "#% of Physical Attack Damage Leeched as Mana (Local)",
                "fractured",
                1),
            Stat(
                "explicit.stat_3237948413",
                "#% of Physical Attack Damage Leeched as Mana",
                "explicit",
                2),
            Stat(
                "explicit.stat_669069897",
                "#% of Physical Attack Damage Leeched as Mana (Local)",
                "explicit",
                3),
        ]);
        var searchClient = new RecordingSearchClient();
        var service = CreateService(catalog, FracturedStateFilterCatalog(), searchClient);
        var sourceDraft = ParseProductionDraft(ThirstyText);
        var source = Assert.Single(sourceDraft.ModifierFilters);

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, source.ResolutionStatus);
        Assert.Equal("ManaLeechPermyriad1", source.ResolvedModifierId);
        Assert.Equal(
            ["mana_leech_from_physical_attack_damage_permyriad"],
            source.ResolvedStatIds);
        Assert.Equal(ModifierLocality.Global, source.Locality);
        Assert.Equal(0.34m, source.RequestedMinimum);
        Assert.Null(source.RequestedMaximum);

        var prepared = await service.PrepareEffectiveDraftAsync(sourceDraft);
        var manaLeech = Assert.Single(prepared.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, manaLeech.ProviderResolutionStatus);
        Assert.Equal("fractured.stat_3237948413", manaLeech.ProviderStatId);
        Assert.Equal(0.34m, manaLeech.RequestedMinimum);
        Assert.DoesNotContain(
            manaLeech.ProviderCandidateStatIds,
            id => id == "fractured.stat_669069897");
        Assert.Contains(manaLeech.FilterVariants, option =>
            option.ProviderKind == "fractured");
        var explicitOption = Assert.Single(manaLeech.FilterVariants, option =>
            option.ProviderKind == "explicit");

        var selected = SelectOnly(prepared, manaLeech.ComponentId);
        var result = await service.CheckAsync(
            selected,
            new TradeSearchDraftValidator().Validate(selected),
            League);
        Assert.True(result.IsSuccess);
        using (var document = JsonDocument.Parse(PathOfExileTradeJson.SerializeSearchRequest(
                   Assert.Single(searchClient.Requests))))
        {
            var query = document.RootElement.GetProperty("query");
            var group = Assert.Single(query.GetProperty("stats").EnumerateArray());
            var filter = Assert.Single(group.GetProperty("filters").EnumerateArray());
            Assert.Equal("fractured.stat_3237948413", filter.GetProperty("id").GetString());
            Assert.Equal(0.34m, filter.GetProperty("value").GetProperty("min").GetDecimal());
            Assert.DoesNotContain("669069897", document.RootElement.GetRawText(), StringComparison.Ordinal);
        }

        var manuallyExplicit = service.ResolveProviderComponents(
            selected with
            {
                ModifierFilters = selected.ModifierFilters
                    .Select(component => component.ComponentId == manaLeech.ComponentId
                        ? component with
                        {
                            RequestedFilterVariantIdentity = explicitOption.Identity,
                            RequestedFilterVariantKind = explicitOption.ProviderKind,
                        }
                        : component)
                    .ToArray(),
            },
            catalog,
            filterCatalog: FracturedStateFilterCatalog());
        var explicitManaLeech = Assert.Single(manuallyExplicit.ModifierFilters);
        Assert.Equal("ManaLeechPermyriad1", explicitManaLeech.ResolvedModifierId);
        Assert.True(explicitManaLeech.IsFractured);
        Assert.Equal("explicit.stat_3237948413", explicitManaLeech.ProviderStatId);
        Assert.Equal(0.34m, explicitManaLeech.RequestedMinimum);
    }

    [Fact]
    public async Task CheckAsync_ApproximateFracturedModifierForcesExactBaseStateAndExplicitBoundsInFinalJson()
    {
        var searchClient = new RecordingSearchClient();
        var service = CreateService(
            new PathOfExileTradeStatCatalog(
            [
                Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
            ]),
            FracturedStateFilterCatalog(),
            searchClient);
        var prepared = await service.PrepareEffectiveDraftAsync(Draft(FracturedLifeComponent()));
        var selected = prepared with
        {
            ModifierFilters =
            [
                Assert.Single(prepared.ModifierFilters) with { IsSelected = true },
            ],
        };

        var result = await service.CheckAsync(
            selected,
            new TradeSearchDraftValidator().Validate(selected),
            League);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(searchClient.Requests);
        var json = PathOfExileTradeJson.SerializeSearchRequest(request);
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.Equal("Titan Plate", query.GetProperty("type").GetString());
        Assert.False(query.TryGetProperty("name", out _));
        var typeFilters = query.GetProperty("filters").GetProperty("type_filters").GetProperty("filters");
        Assert.False(typeFilters.TryGetProperty("category", out _));
        Assert.Equal("rare", typeFilters.GetProperty("rarity").GetProperty("option").GetString());
        Assert.Equal("true", query
            .GetProperty("filters")
            .GetProperty("misc_filters")
            .GetProperty("filters")
            .GetProperty("fractured_item")
            .GetProperty("option")
            .GetString());
        var stat = Assert.Single(query
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray());
        Assert.Equal("explicit.stat_life", stat.GetProperty("id").GetString());
        Assert.Equal(84m, stat.GetProperty("value").GetProperty("min").GetDecimal());
        Assert.Equal(90m, stat.GetProperty("value").GetProperty("max").GetDecimal());
        Assert.DoesNotContain("fractured.stat", json, StringComparison.Ordinal);

        var effective = Assert.IsType<TradeSearchDraft>(result.EffectiveDraft);
        var component = Assert.Single(effective.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Approximate, component.ProviderResolutionStatus);
        Assert.True(component.IsFractured);
        Assert.Equal("explicit.stat_life", component.ProviderStatId);
        Assert.Equal(BaseSearchMode.ExactBase, effective.Base.ActiveCriterion?.Mode);
        Assert.Equal(TradeTriState.Yes, effective.ItemStateCriteria.Fractured);
    }

    [Fact]
    public async Task CheckAsync_ExactFracturedStatWinsWithoutApproximationStateOrWarning()
    {
        var searchClient = new RecordingSearchClient();
        var service = CreateService(
            new PathOfExileTradeStatCatalog(
            [
                Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
                Stat("fractured.stat_life", "+# to maximum Life", "fractured"),
            ]),
            FracturedStateFilterCatalog(),
            searchClient);
        var prepared = await service.PrepareEffectiveDraftAsync(
            Draft(FracturedLifeComponent(), exactBaseActive: true));
        var selected = prepared with
        {
            ModifierFilters =
            [
                Assert.Single(prepared.ModifierFilters) with { IsSelected = true },
            ],
        };

        var result = await service.CheckAsync(
            selected,
            new TradeSearchDraftValidator().Validate(selected),
            League);

        Assert.True(result.IsSuccess);
        var json = PathOfExileTradeJson.SerializeSearchRequest(Assert.Single(searchClient.Requests));
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        var stat = Assert.Single(query.GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal("fractured.stat_life", stat.GetProperty("id").GetString());
        Assert.Equal(84m, stat.GetProperty("value").GetProperty("min").GetDecimal());
        Assert.Equal(90m, stat.GetProperty("value").GetProperty("max").GetDecimal());
        Assert.False(query.GetProperty("filters").TryGetProperty("misc_filters", out _));

        var effective = Assert.IsType<TradeSearchDraft>(result.EffectiveDraft);
        var component = Assert.Single(effective.ModifierFilters);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderDiagnosticMessage);
        Assert.Equal(TradeTriState.Any, effective.ItemStateCriteria.Fractured);
    }

    [Fact]
    public async Task CheckAsync_ManualExplicitRequestKeepsFracturedSourceWithoutApproximationConstraints()
    {
        var searchClient = new RecordingSearchClient();
        var service = CreateService(
            new PathOfExileTradeStatCatalog(
            [
                Stat("explicit.stat_life", "+# to maximum Life", "explicit"),
                Stat("fractured.stat_life", "+# to maximum Life", "fractured"),
            ]),
            FracturedStateFilterCatalog(),
            searchClient);
        var prepared = await service.PrepareEffectiveDraftAsync(Draft(FracturedLifeComponent()));
        var preparedComponent = Assert.Single(prepared.ModifierFilters);
        var explicitOption = Assert.Single(preparedComponent.FilterVariants, option =>
            option.ProviderKind == "explicit");
        var selected = prepared with
        {
            ModifierFilters =
            [
                preparedComponent with
                {
                    IsSelected = true,
                    RequestedFilterVariantIdentity = explicitOption.Identity,
                    RequestedFilterVariantKind = explicitOption.ProviderKind,
                },
            ],
        };

        var result = await service.CheckAsync(
            selected,
            new TradeSearchDraftValidator().Validate(selected),
            League);

        Assert.True(result.IsSuccess);
        var json = PathOfExileTradeJson.SerializeSearchRequest(Assert.Single(searchClient.Requests));
        using var document = JsonDocument.Parse(json);
        var query = document.RootElement.GetProperty("query");
        Assert.False(query.TryGetProperty("type", out _));
        var typeFilters = query.GetProperty("filters").GetProperty("type_filters").GetProperty("filters");
        Assert.Equal(
            "armour.body",
            typeFilters.GetProperty("category").GetProperty("option").GetString());
        Assert.False(query.GetProperty("filters").TryGetProperty("misc_filters", out _));
        var stat = Assert.Single(query.GetProperty("stats")[0].GetProperty("filters").EnumerateArray());
        Assert.Equal("explicit.stat_life", stat.GetProperty("id").GetString());
        Assert.Equal(84m, stat.GetProperty("value").GetProperty("min").GetDecimal());
        Assert.Equal(90m, stat.GetProperty("value").GetProperty("max").GetDecimal());

        var effective = Assert.IsType<TradeSearchDraft>(result.EffectiveDraft);
        var component = Assert.Single(effective.ModifierFilters);
        Assert.True(component.IsFractured);
        Assert.Equal("explicit", component.RequestedFilterVariantKind);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Null(component.ProviderDiagnosticMessage);
        Assert.Equal(BaseSearchMode.Category, effective.Base.ActiveCriterion?.Mode);
        Assert.Equal(TradeTriState.Any, effective.ItemStateCriteria.Fractured);
    }

    private static TradeSearchDraft SelectOnly(
        TradeSearchDraft draft,
        params string[] componentIds)
    {
        var selected = componentIds.ToHashSet(StringComparer.Ordinal);
        return draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select(component => component with
                {
                    IsSelected = selected.Contains(component.ComponentId),
                })
                .ToArray(),
        };
    }

    private static TradeSearchDraft ParseProductionDraft(string copiedText)
    {
        var packageResult = GameDataPackageLoader.LoadFromFileAsync(
                FindRepoFile("artifacts", "poenhance-game-data.json"))
            .GetAwaiter()
            .GetResult();
        Assert.True(packageResult.IsSuccess && packageResult.Package is not null);
        var gameData = GameDataCatalog.FromPackage(packageResult.Package!);
        var parsed = new ItemTextParser().Parse(copiedText);
        var displayService = new ParsedItemGameDataDisplayService();
        var baseResolution = Assert.IsType<ItemBaseResolutionResult>(
            displayService.ResolveItemBase(parsed, gameData).Result);
        var modifierResolutions = displayService
            .ResolveModifierCandidates(parsed, gameData, baseResolution)
            .Results
            .Select(result => result.Result)
            .OfType<ModifierCandidateResolutionResult>()
            .ToArray();
        var unresolvedFractured = modifierResolutions
            .Where(result => result.ParsedModifier.IsFractured &&
                result.Status != ModifierCandidateResolutionStatus.Exact)
            .ToArray();
        Assert.True(
            unresolvedFractured.Length == 0,
            $"baseDomain={baseResolution.MatchedItemBase?.Domain}; " +
            string.Join(" | ", unresolvedFractured.Select(result =>
                $"{result.ParsedModifier.Name}: " +
                $"{string.Join(", ", result.Diagnostics.Select(diagnostic =>
                    $"{diagnostic.Code}={diagnostic.Reason}"))}; " +
                $"values={string.Join(" || ", result.ParsedModifier.ValueLines)}; " +
                $"counts={result.NameCandidateCount}/{result.GenerationKindCandidateCount}/" +
                $"{result.EligibilityCandidateCount}/{result.TextSignatureCandidateCount}; " +
                $"text={string.Join(",", (result.TextSignatureMatches ?? [])
                    .GroupBy(match => $"{match.Outcome}:{match.ReasonCode}")
                    .Select(group => $"{group.Key}={group.Count()}"))}; " +
                $"candidates={string.Join(",", result.Candidates.Select(candidate => candidate.Id))}; " +
                $"excluded={string.Join(",", (result.ExcludedCandidates ?? []).Select(candidate =>
                    $"{candidate.Id}[{candidate.Domain}]"))}")));
        var draftResult = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            baseResolution,
            modifierResolutions,
            gameData);
        Assert.True(
            draftResult.IsSuccess,
            string.Join(" | ", draftResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
        return Assert.IsType<TradeSearchDraft>(draftResult.Draft);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeParts]);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repo file: {Path.Combine(relativeParts)}");
    }

    private static PathOfExileTradePriceCheckService CreateService(
        PathOfExileTradeStatCatalog statCatalog,
        PathOfExileTradeFilterCatalog filterCatalog,
        RecordingSearchClient searchClient)
    {
        return new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatCatalogProvider(statCatalog),
            new ThrowingItemCatalogProvider(),
            new PathOfExileTradeSelectedModifierMapper(),
            new ThrowingItemIdentityMapper(),
            searchClient,
            new ThrowingFetchClient(),
            new StaticFilterCatalogProvider(filterCatalog));
    }

    private static TradeSearchDraft Draft(
        ResolvedSearchComponent component,
        bool exactBaseActive = false)
    {
        var category = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.Category,
            Category = "Body Armour",
        };
        var exactBase = new BaseSearchCriterion
        {
            Mode = BaseSearchMode.ExactBase,
            Category = "Body Armour",
            ExactBaseName = "Titan Plate",
        };
        return new TradeSearchDraft
        {
            ItemClass = "Body Armours",
            CanonicalItemClass = "Body Armour",
            Rarity = "Rare",
            DisplayName = "Armoured Shell",
            ParsedBaseType = "Titan Plate",
            Base = new TradeSearchBaseDraft
            {
                Status = ItemBaseResolutionStatus.Exact,
                ResolvedBaseId = "base.titan-plate",
                ResolvedBaseName = "Titan Plate",
                Category = "Body Armour",
                Observed = new ObservedBaseIdentity
                {
                    Status = ItemBaseResolutionStatus.Exact,
                    ExactBaseId = "base.titan-plate",
                    ExactBaseName = "Titan Plate",
                    Category = "Body Armour",
                },
                AvailableCriteria = new AvailableBaseSearchCriteria
                {
                    Category = category,
                    ExactBase = exactBase,
                },
                ActiveCriterion = exactBaseActive ? exactBase : category,
            },
            ModifierFilters = [component],
        };
    }

    private static ResolvedSearchComponent FracturedLifeComponent()
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            SourceComponentIndex = 0,
            OriginalText = "+84 to maximum Life",
            CanonicalSignature = "+<number> to maximum Life",
            ParsedKind = ParsedModifierKind.Suffix,
            Locality = ModifierLocality.Global,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "mod.fractured.life",
            ResolvedStatIds = ["base_maximum_life"],
            IsSearchable = true,
            IsFractured = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [84m],
            CanonicalNumericValues = [84m],
            ValueBoundTranslationHandlers = [[]],
            ValueBoundTranslationIdentity = "identity",
            RequestedMinimum = 84m,
            RequestedMaximum = 90m,
            ProviderDomainEvidence =
            [
                new SearchComponentProviderDomainEvidence
                {
                    ProviderDomain = "Fractured",
                    ModifierId = "mod.fractured.life",
                    GenerationType = ModifierGenerationType.Suffix,
                    Locality = ModifierLocality.Global,
                    IsSourceExact = true,
                    ItemBaseId = "base.titan-plate",
                    ItemClass = "Body Armour",
                    ApplicabilityReason = "Exact Fractured source fixture.",
                },
                new SearchComponentProviderDomainEvidence
                {
                    ProviderDomain = "Explicit",
                    ModifierId = "mod.explicit.life",
                    GenerationType = ModifierGenerationType.Suffix,
                    Locality = ModifierLocality.Global,
                    IsProjectedDomain = true,
                    ItemBaseId = "base.titan-plate",
                    ItemClass = "Body Armour",
                    ApplicabilityReason = "Compatible ordinary provider fixture.",
                },
            ],
        };
    }

    private static PathOfExileTradeStatEntry Stat(
        string id,
        string text,
        string type,
        int providerOrder = 0)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = providerOrder,
            GroupId = type,
            GroupLabel = type,
            Id = id,
            Text = text,
            Type = type,
        };
    }

    private static PathOfExileTradeFilterCatalog FracturedStateFilterCatalog()
    {
        return new PathOfExileTradeFilterCatalog(
            [
                new PathOfExileTradeFilterOption
                {
                    ProviderOrder = 0,
                    GroupId = "type_filters",
                    FilterId = "category",
                    Id = "armour.body",
                    Text = "Body Armour",
                },
                new PathOfExileTradeFilterOption
                {
                    ProviderOrder = 1,
                    GroupId = "type_filters",
                    FilterId = "category",
                    Id = "armour.boots",
                    Text = "Boots",
                },
                new PathOfExileTradeFilterOption
                {
                    ProviderOrder = 2,
                    GroupId = "type_filters",
                    FilterId = "category",
                    Id = "armour.gloves",
                    Text = "Gloves",
                },
            ],
            optionFilterDefinitions:
            [
                new PathOfExileTradeOptionFilterDefinition
                {
                    GroupProviderOrder = 0,
                    ProviderOrder = 0,
                    GroupId = "misc_filters",
                    GroupTitle = "Miscellaneous",
                    FilterId = "fractured_item",
                    Text = "Fractured Item",
                    Options =
                    [
                        new PathOfExileTradeOptionDefinition { Id = null, Text = "Any" },
                        new PathOfExileTradeOptionDefinition { Id = "true", Text = "Yes" },
                        new PathOfExileTradeOptionDefinition { Id = "false", Text = "No" },
                    ],
                },
                BooleanStateDefinition(1, "mirrored", "Mirrored"),
                BooleanStateDefinition(2, "corrupted", "Corrupted"),
                BooleanStateDefinition(3, "identified", "Identified"),
            ]);
    }

    private static PathOfExileTradeOptionFilterDefinition BooleanStateDefinition(
        int providerOrder,
        string filterId,
        string text)
    {
        return new PathOfExileTradeOptionFilterDefinition
        {
            GroupProviderOrder = 0,
            ProviderOrder = providerOrder,
            GroupId = "misc_filters",
            GroupTitle = "Miscellaneous",
            FilterId = filterId,
            Text = text,
            Options =
            [
                new PathOfExileTradeOptionDefinition { Id = null, Text = "Any" },
                new PathOfExileTradeOptionDefinition { Id = "true", Text = "Yes" },
                new PathOfExileTradeOptionDefinition { Id = "false", Text = "No" },
            ],
        };
    }

    private const string SkullTrackText = """
Item Class: Boots
Rarity: Rare
Skull Track
Ambush Boots
--------
Quality: +20% (augmented)
Evasion Rating: 103 (augmented)
Energy Shield: 22 (augmented)
--------
Requirements:
Level: 70
Str: 155
Dex: 155
Int: 73
--------
Sockets: R-R-B-G
--------
Item Level: 85
--------
0.6% of Damage Leeched as Life if you've Killed Recently (enchant)
(Leeched Life is recovered over time. Multiple Leeches can occur simultaneously, up to a maximum rate) (enchant)
(Recently refers to the past 4 seconds) (enchant)
--------
{ Eater of Worlds Implicit Modifier (Lesser) - Damage, Elemental, Fire, Ailment }
Ignites you inflict deal Damage 5% faster
(They will deal the same total damage over a shorter duration)
{ Searing Exarch Implicit Modifier (Lesser) }
Drops Scorched Ground while moving, lasting 2 seconds
(Enemies on your Scorched Ground are Scorched. They have -10% to Elemental Resistances)
--------
{ Prefix Modifier "Rotund" (Tier: 3) - Life }
+69(85-99) to maximum Life
{ Prefix Modifier "Gazelle's" (Tier: 3) - Speed }
25% increased Movement Speed
{ Prefix Modifier "Azure" (Tier: 10) - Mana }
+28(25-29) to maximum Mana
{ Fractured Suffix Modifier "of Abjuration" (Tier: 2) }
+12(11-12)% chance to Suppress Spell Damage
(40% of Damage from Suppressed Hits and Ailments they inflict is prevented)
{ Suffix Modifier "of the Essence" - Elemental, Lightning, Ailment }
56(56-60)% chance to Avoid being Shocked
{ Suffix Modifier "of the Flatworm" (Tier: 6) - Life }
Regenerate 12.5(8.1-16) Life per second
Searing Exarch Item
Eater of Worlds Item
--------
Fractured Item
""";

    private const string MiracleTouchText = """
Item Class: Gloves
Rarity: Rare
Miracle Touch
Slink Gloves
--------
Quality: +20% (augmented)
Evasion Rating: 326 (augmented)
--------
Requirements:
Level: 70
Dex: 95
--------
Sockets: G-R-R-G
--------
Item Level: 85
--------
{ Eater of Worlds Implicit Modifier (Lesser) - Damage, Physical, Elemental, Cold }
10% of Physical Damage Converted to Cold Damage
{ Searing Exarch Implicit Modifier (Lesser) - Attack, Speed }
8% increased Attack Speed
--------
{ Fractured Prefix Modifier "Virile" (Tier: 2) - Life }
+76(100-114) to maximum Life
{ Prefix Modifier "Pirate's" (Tier: 1) - Drop }
15(13-18)% increased Rarity of Items found
{ Master Crafted Prefix Modifier "Upgraded" - Damage, Physical, Elemental, Cold }
23(20-25)% of Physical Damage Converted to Cold Damage
{ Fractured Suffix Modifier "of Haunting" - Attack }
Attacks have 21(15-25)% chance to Maim on Hit
(Maimed enemies have 30% reduced Movement Speed)
{ Suffix Modifier "of the Essence" - Attack, Speed }
18(17-18)% increased Attack Speed
{ Suffix Modifier "of Abjuration" (Tier: 2) }
+12(11-12)% chance to Suppress Spell Damage
(40% of Damage from Suppressed Hits and Ailments they inflict is prevented)
Searing Exarch Item
Eater of Worlds Item
--------
Fractured Item
""";

    private const string ThirstyText = """
Item Class: Gloves
Rarity: Magic
Thirsty Stealth Gloves
--------
Evasion Rating: 264
--------
Requirements:
Level: 62
Dex: 97
--------
Sockets: G 
--------
Item Level: 83
--------
{ Fractured Prefix Modifier "Thirsty" — Mana, Physical, Attack }
0.34(0.2-0.4)% of Physical Attack Damage Leeched as Mana
(Leeched Mana is recovered over time. Multiple Leeches can occur simultaneously, up to a maximum rate)
--------
Fractured Item
""";

    private sealed class RecordingSearchClient : IPathOfExileTradeSearchClient
    {
        public List<PathOfExileTradeSearchRequest> Requests { get; } = [];

        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(Assert.IsType<PathOfExileTradeSearchRequest>(request));
            Assert.Equal(League, leagueIdentifier);
            return Task.FromResult(new PathOfExileTradeSearchExecutionResult
            {
                IsSuccess = true,
                Response = new PathOfExileTradeSearchResponse
                {
                    Id = "query-fractured",
                    Result = [],
                    Total = 0,
                },
            });
        }
    }
}
