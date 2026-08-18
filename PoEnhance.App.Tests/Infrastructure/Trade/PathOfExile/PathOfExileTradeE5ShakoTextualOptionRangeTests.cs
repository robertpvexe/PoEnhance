using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed partial class PathOfExileTradeE5ShakoTextualOptionRangeTests
{
    [Fact]
    public async Task E5_ExistingGeneratedOptionControls_RemainPresenceOnlyProviderOptions()
    {
        var candidatePath = Environment.GetEnvironmentVariable("POENHANCE_UNIQUE_CANDIDATE");
        var providerFixturePath = Environment.GetEnvironmentVariable("POENHANCE_TRADE_STATS_FIXTURE");
        if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath) ||
            string.IsNullOrWhiteSpace(providerFixturePath) || !File.Exists(providerFixturePath))
        {
            return;
        }

        var load = await GameDataPackageLoader.LoadFromFileAsync(candidatePath);
        var gameData = GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(load.Package));
        var statCatalog = Assert.IsType<PathOfExileTradeStatCatalog>(
            new PathOfExileTradeStatsResponseParser()
                .ParseStatsResponse(File.ReadAllText(providerFixturePath)).Catalog);

        var forbiddenDraft = ResolveProviderControl(
            gameData,
            statCatalog,
            ForbiddenFlameControl,
            "Forbidden Flame",
            "Crimson Jewel",
            out _);
        AssertProviderOption(
            Assert.Single(forbiddenDraft.ModifierFilters),
            "Allocates Unnatural Strength");

        var impossibleDraft = ResolveProviderControl(
            gameData,
            statCatalog,
            ImpossibleEscapeControl,
            "Impossible Escape",
            "Viridian Jewel",
            out var impossibleService);
        AssertUnsupportedOption(
            Assert.Single(impossibleDraft.ModifierFilters),
            "Passive Skills in Radius of Chaos Inoculation");

        var identityOnlyDraft = impossibleDraft with
        {
            ModifierFilters = impossibleDraft.ModifierFilters
                .Select(component => component with { IsSelected = false })
                .ToArray(),
        };
        var identityOnlyResult = await impossibleService.CheckAsync(
            identityOnlyDraft,
            new TradeSearchDraftValidator().Validate(identityOnlyDraft),
            "Mirage");
        Assert.True(
            identityOnlyResult.IsSuccess,
            string.Join(" | ", identityOnlyResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
    }

    [Fact]
    public async Task TrueControllerDoubleProviderPassAndQuery_PreserveGeneratedLevelsAndEditableMinimums()
    {
        var candidatePath = Environment.GetEnvironmentVariable("POENHANCE_UNIQUE_CANDIDATE");
        if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
        {
            return;
        }

        var load = await GameDataPackageLoader.LoadFromFileAsync(candidatePath);
        var gameData = GameDataCatalog.FromPackage(Assert.IsType<GameDataPackage>(load.Package));
        var parsed = new ItemTextParser().Parse(TrueAdvancedCopyTextualOptionRange);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, gameData);
        var sourceResolutions = new ParsedItemModifierCandidateResolver().Resolve(
            parsed,
            gameData,
            baseResolution);
        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            baseResolution,
            sourceResolutions,
            gameData).Draft);

        var providerEntries = draft.ModifierFilters.Select((component, index) =>
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = index,
                GroupId = "explicit",
                GroupLabel = "Explicit",
                Id = $"explicit.generated-option-{index}",
                Text = ObservedLevelPattern().Replace(
                    component.PresentationText ?? component.OriginalText,
                    "Level #"),
                Type = "explicit",
            }).ToArray();
        var providerFixturePath = Environment.GetEnvironmentVariable("POENHANCE_TRADE_STATS_FIXTURE");
        var statCatalog = !string.IsNullOrWhiteSpace(providerFixturePath) &&
            File.Exists(providerFixturePath)
                ? Assert.IsType<PathOfExileTradeStatCatalog>(
                    new PathOfExileTradeStatsResponseParser()
                        .ParseStatsResponse(File.ReadAllText(providerFixturePath)).Catalog)
                : new PathOfExileTradeStatCatalog(providerEntries);
        var itemCatalog = new PathOfExileTradeItemCatalog(
        [
            new PathOfExileTradeItemEntry
            {
                ProviderOrder = 0,
                GroupId = "armour",
                GroupLabel = "Armour",
                Name = parsed.DisplayName,
                Type = parsed.BaseType!,
                IsUnique = true,
            },
        ]);
        var searchClient = new CapturingSearchClient();
        var service = new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatProvider(statCatalog),
            new StaticItemProvider(itemCatalog),
            new PathOfExileTradeSelectedModifierMapper(),
            new PathOfExileTradeItemIdentityMapper(),
            searchClient,
            new EmptyFetchClient(),
            new ShakoFilterProvider(PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog()));
        var controller = new PriceCheckerSearchController(
            service,
            ApplicationLeagueSetting.CreateTransient("Mirage"),
            new global::PoEnhance.App.Tests.TestTradeLeagueResolver());

        var prepared = await controller.PrepareDraftAsync(draft);
        AssertGeneratedState(prepared.ModifierFilters[0], 10m);
        AssertGeneratedState(prepared.ModifierFilters[1], 26m);

        var secondPass = service.ResolveEffectiveDraft(prepared);
        AssertGeneratedState(secondPass.ModifierFilters[0], 10m);
        AssertGeneratedState(secondPass.ModifierFilters[1], 26m);
        Assert.Equal(
            prepared.ModifierFilters.Select(ComponentState),
            secondPass.ModifierFilters.Select(ComponentState));

        controller.UpdateCurrentDraft(
            prepared,
            new TradeSearchDraftValidator().Validate(prepared));
        Assert.Collection(
            controller.CurrentViewState.Modifiers,
            first => AssertUiState(first, "10", selected: false),
            second => AssertUiState(second, "26", selected: false));

        controller.UpdateModifierSelection(0, true);
        controller.UpdateModifierSelection(1, true);
        Assert.All(controller.CurrentViewState.Modifiers, row =>
        {
            Assert.True(row.IsSelected);
            Assert.True(row.IsInteractionEnabled, row.AvailabilityReason);
            Assert.True(row.CanEditBounds);
        });

        controller.CurrentViewState.Modifiers[0].MinimumText = "9";
        controller.UpdateModifierBounds(0, "9", string.Empty);
        Assert.Equal("9", controller.CurrentViewState.Modifiers[0].MinimumText);
        Assert.Equal(9m, CurrentDraft(controller).ModifierFilters[0].RequestedMinimum);
        controller.CurrentViewState.Modifiers[0].MinimumText = "10";
        controller.UpdateModifierBounds(0, "10", string.Empty);
        Assert.Equal("10", controller.CurrentViewState.Modifiers[0].MinimumText);
        Assert.Equal(10m, CurrentDraft(controller).ModifierFilters[0].RequestedMinimum);

        var selectedDraft = CurrentDraft(controller);
        var directResult = await service.CheckAsync(
            selectedDraft,
            new TradeSearchDraftValidator().Validate(selectedDraft),
            "Mirage");
        Assert.True(
            directResult.IsSuccess,
            string.Join(" | ", directResult.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Stage}/{diagnostic.Code}/{diagnostic.SourceCode}: {diagnostic.Message}")));
        searchClient.Clear();

        await controller.SearchAsync();

        Assert.True(
            searchClient.Request is not null,
            $"{controller.CurrentViewState.Status}: {controller.CurrentViewState.Message}");
        var request = Assert.IsType<PathOfExileTradeSearchRequest>(searchClient.Request);
        var supportFilters = request.Query.Stats
            .SelectMany(group => group.Filters)
            .Where(filter => filter.Value?.Min is 10m or 26m)
            .ToArray();
        Assert.Contains(supportFilters, filter => filter.Value?.Min == 10m);
        Assert.Contains(supportFilters, filter => filter.Value?.Min == 26m);
        Assert.All(supportFilters, filter =>
        {
            var component = Assert.Single(secondPass.ModifierFilters, candidate =>
                candidate.RequestedMinimum == filter.Value?.Min);
            var compatibleProviderIds = component.ProviderStatAlternativeIds
                .Append(component.ProviderStatId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .ToArray();
            Assert.Contains(filter.Id, compatibleProviderIds);
            AssertQueryFilter(filter, filter.Id, component.RequestedMinimum!.Value);
        });
    }

    private static void AssertGeneratedState(ResolvedSearchComponent component, decimal expectedMinimum)
    {
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, component.UniqueSourceSemantics);
        Assert.Single(component.UniqueCandidatePoolMembershipIds);
        Assert.Equal(["Greater Multiple Projectiles-Hallow"],
            component.UniqueTextualOptionRangeAnnotations);
        Assert.Equal([expectedMinimum], component.ObservedNumericValues);
        Assert.Equal([expectedMinimum], component.CanonicalNumericValues);
        Assert.Equal(expectedMinimum, component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.True(component.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.Scalar, component.ValueBoundShape);
        Assert.Contains(
            component.ProviderResolutionStatus,
            new[]
            {
                SearchComponentProviderResolutionStatus.Exact,
                SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            });
        Assert.True(
            !string.IsNullOrWhiteSpace(component.ProviderStatId) ||
            component.ProviderStatAlternativeIds.Count > 0);
    }

    private static TradeSearchDraft ResolveProviderControl(
        GameDataCatalog gameData,
        PathOfExileTradeStatCatalog statCatalog,
        string rawText,
        string displayName,
        string baseType,
        out PathOfExileTradePriceCheckService service)
    {
        var parsed = new ItemTextParser().Parse(rawText);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, gameData);
        var sourceResolutions = new ParsedItemModifierCandidateResolver().Resolve(
            parsed,
            gameData,
            baseResolution);
        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            baseResolution,
            sourceResolutions,
            gameData).Draft);
        service = new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatProvider(statCatalog),
            new StaticItemProvider(new PathOfExileTradeItemCatalog(
            [
                new PathOfExileTradeItemEntry
                {
                    ProviderOrder = 0,
                    GroupId = "jewel",
                    GroupLabel = "Jewel",
                    Name = displayName,
                    Type = baseType,
                    IsUnique = true,
                },
            ])),
            new PathOfExileTradeSelectedModifierMapper(),
            new PathOfExileTradeItemIdentityMapper(),
            new CapturingSearchClient(),
            new EmptyFetchClient(),
            new ShakoFilterProvider(PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog()));

        return service.ResolveEffectiveDraft(draft);
    }

    private static void AssertProviderOption(
        ResolvedSearchComponent component,
        string expectedTextPrefix)
    {
        Assert.StartsWith(expectedTextPrefix, component.OriginalText, StringComparison.Ordinal);
        Assert.True(
            component.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact,
            $"{component.ProviderResolutionStatus}: {component.ProviderDiagnosticCode} / " +
            $"{component.ProviderDiagnosticMessage}; unique={component.UniqueResolutionDiagnosticCode} / " +
            component.NotSearchableReason);
        Assert.False(string.IsNullOrWhiteSpace(component.ProviderStatId));
        Assert.Contains('|', component.ProviderStatId);
        Assert.Equal(ModifierBoundShape.PresenceOnly, component.ValueBoundShape);
        Assert.False(component.SupportsValueBounds);
        Assert.Null(component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.Empty(component.UniqueTextualOptionRangeAnnotations);
    }

    private static void AssertUnsupportedOption(
        ResolvedSearchComponent component,
        string expectedTextPrefix)
    {
        Assert.StartsWith(expectedTextPrefix, component.OriginalText, StringComparison.Ordinal);
        Assert.Equal(
            SearchComponentProviderResolutionStatus.Unsupported,
            component.ProviderResolutionStatus);
        Assert.False(component.IsSearchable);
        Assert.Equal(ModifierBoundShape.PresenceOnly, component.ValueBoundShape);
        Assert.False(component.SupportsValueBounds);
        Assert.Null(component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.Empty(component.UniqueTextualOptionRangeAnnotations);
    }

    private static void AssertUiState(
        PriceCheckerModifierViewModel row,
        string expectedMinimum,
        bool selected)
    {
        Assert.Equal(expectedMinimum, row.MinimumText);
        Assert.Equal(string.Empty, row.MaximumText);
        Assert.True(row.SupportsValueBounds);
        Assert.True(row.IsInteractionEnabled);
        Assert.Equal(selected, row.IsSelected);
        Assert.Equal(selected, row.CanEditBounds);
    }

    private static void AssertQueryFilter(
        PathOfExileTradeSearchStatFilter filter,
        string expectedId,
        decimal expectedMinimum)
    {
        Assert.Equal(expectedId, filter.Id);
        Assert.Equal(expectedMinimum, filter.Value?.Min);
        Assert.Null(filter.Value?.Max);
    }

    private static object ComponentState(ResolvedSearchComponent component) => new
    {
        component.UniqueSourceSemantics,
        Membership = string.Join('\u001f', component.UniqueCandidatePoolMembershipIds),
        component.ProviderStatId,
        component.ProviderResolutionStatus,
        Observed = string.Join('\u001f', component.ObservedNumericValues),
        Canonical = string.Join('\u001f', component.CanonicalNumericValues),
        component.RequestedMinimum,
        component.RequestedMaximum,
        component.SupportsValueBounds,
        component.ValueBoundShape,
    };

    private static TradeSearchDraft CurrentDraft(PriceCheckerSearchController controller) =>
        Assert.IsType<TradeSearchDraft>(typeof(PriceCheckerSearchController)
            .GetField("currentDraft", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(controller));

    private sealed class StaticStatProvider(PathOfExileTradeStatCatalog catalog) :
        IPathOfExileTradeStatCatalogProvider
    {
        public bool TryGetCachedCatalog(out PathOfExileTradeStatCatalog cached)
        {
            cached = catalog;
            return true;
        }

        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
    }

    private sealed class StaticItemProvider(PathOfExileTradeItemCatalog catalog) :
        IPathOfExileTradeItemCatalogProvider
    {
        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeItemCatalogProviderResult.Success(catalog));
    }

    private sealed class ShakoFilterProvider(PathOfExileTradeFilterCatalog catalog) :
        IPathOfExileTradeFilterCatalogProvider
    {
        public bool TryGetCachedCatalog(out PathOfExileTradeFilterCatalog cached)
        {
            cached = catalog;
            return true;
        }

        public Task<PathOfExileTradeFilterCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeFilterCatalogProviderResult.Success(catalog));
    }

    private sealed class CapturingSearchClient : IPathOfExileTradeSearchClient
    {
        public PathOfExileTradeSearchRequest? Request { get; private set; }

        public void Clear() => Request = null;

        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(new PathOfExileTradeSearchExecutionResult
            {
                IsSuccess = true,
                HttpStatusCode = HttpStatusCode.OK,
                Response = new PathOfExileTradeSearchResponse
                {
                    Id = "test-query",
                    Result = [],
                    Total = 0,
                },
            });
        }
    }

    private sealed class EmptyFetchClient : IPathOfExileTradeFetchClient
    {
        public Task<PathOfExileTradeFetchExecutionResult> FetchAsync(
            string? queryId,
            IReadOnlyList<string?>? resultIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradeFetchExecutionResult
            {
                IsSuccess = true,
                HttpStatusCode = HttpStatusCode.OK,
                Response = new PathOfExileTradeFetchResponse { Result = [] },
            });
    }

    private const string TrueAdvancedCopyTextualOptionRange = """
Item Class: Helmets
Rarity: Unique
Forbidden Shako
Great Crown
--------
Item Level: 86
--------
{ Unique Modifier — Gem }
Socketed Gems are Supported by Level 10(1-10) Endurance Charge on Melee Stun(Greater Multiple Projectiles-Hallow) — Unscalable Value
{ Unique Modifier — Gem }
Socketed Gems are Supported by Level 26(25-35) Inspiration(Greater Multiple Projectiles-Hallow) — Unscalable Value
""";

    private const string ForbiddenFlameControl = """
Item Class: Jewels
Rarity: Unique
Forbidden Flame
Crimson Jewel
--------
Limited to: 1
--------
Item Level: 86
--------
{ Unique Modifier }
Allocates Unnatural Strength if you have the matching modifier on Forbidden Flesh
""";

    private const string ImpossibleEscapeControl = """
Item Class: Jewels
Rarity: Unique
Impossible Escape
Viridian Jewel
--------
Limited to: 1
Radius: Small
--------
Item Level: 86
--------
{ Unique Modifier }
Passive Skills in Radius of Chaos Inoculation can be Allocated without being connected to your tree
""";

    [GeneratedRegex(@"Level\s+[+-]?\d+(?:\(\s*[+-]?\d+\s*-\s*[+-]?\d+\s*\))?", RegexOptions.CultureInvariant)]
    private static partial Regex ObservedLevelPattern();
}
