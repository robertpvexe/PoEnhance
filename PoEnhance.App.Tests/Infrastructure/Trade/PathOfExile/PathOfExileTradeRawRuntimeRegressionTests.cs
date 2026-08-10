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
    private static readonly PathOfExileTradeSelectedModifierMapper SelectedMapper = new();
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
        var providerDraft = CreatePriceCheckService().ResolveProviderComponents(draft, TradeCatalog);
        return new RuntimeResult(parsed, baseResolution, sourceResolutions, providerDraft);
    }

    private static void AssertExactSelectable(ResolvedSearchComponent component, string providerStatId)
    {
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, component.ResolutionStatus);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, component.ProviderResolutionStatus);
        Assert.Equal(providerStatId, component.ProviderStatId);
        Assert.True(component.IsSearchable);
        Assert.True(IsInteractionReady(component));
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

    private static ResolvedSearchComponent FindComponent(TradeSearchDraft draft, string originalText)
    {
        return Assert.Single(draft.ModifierFilters, component =>
            string.Equals(component.OriginalText, originalText, StringComparison.Ordinal));
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
            new EmptyItemProvider(),
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
        TradeSearchDraft ProviderDraft);

    private sealed class StaticStatProvider(PathOfExileTradeStatCatalog catalog) :
        IPathOfExileTradeStatCatalogProvider
    {
        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
    }

    private sealed class EmptyItemProvider : IPathOfExileTradeItemCatalogProvider
    {
        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PathOfExileTradeItemCatalogProviderResult());
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
}
