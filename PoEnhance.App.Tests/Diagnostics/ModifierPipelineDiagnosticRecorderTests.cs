using PoEnhance.App.Diagnostics;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Diagnostics;

[Collection(nameof(DiagnosticEnvironmentVariableCollection))]
public sealed class ModifierPipelineDiagnosticRecorderTests
{
    [Fact]
    public void CompleteCapture_WritesStructuredArtifactForResolvedModifier()
    {
        var outputDirectory = Path.Combine(Path.GetTempPath(), $"PoEnhanceModifierPipelineDiag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var previous = Environment.GetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName, outputDirectory);
        try
        {
            var parsed = new ItemTextParser().Parse(WindscreamText);
            var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, LoadGameData());
            var sourceResolutions = new ParsedItemModifierCandidateResolver().Resolve(
                parsed,
                LoadGameData(),
                baseResolution);
            var draftResult = new TradeSearchDraftMapper().CreateDraft(
                parsed,
                baseResolution,
                sourceResolutions,
                LoadGameData());
            var draft = Assert.IsType<TradeSearchDraft>(draftResult.Draft);
            ModifierPipelineDiagnosticRecorder.TryBeginCapture(
                parsed,
                baseResolution,
                sourceResolutions,
                draft);

            var catalog = new PathOfExileTradeStatCatalog(
            [
                Stat("explicit.stat_30642521", "You can apply # additional Curses", "explicit"),
            ]);
            var service = new PathOfExileTradePriceCheckService(
                new PathOfExileTradeQueryBuilder(),
                new PathOfExileTradeStatMatcher(),
                new StaticStatProvider(catalog),
                new StaticItemProvider(new PathOfExileTradeItemCatalog([])),
                new PathOfExileTradeSelectedModifierMapper(),
                new PathOfExileTradeItemIdentityMapper(),
                new NoSearchClient(),
                new NoFetchClient());
            var resolved = service.ResolveProviderComponents(
                draft,
                catalog,
                new PathOfExileTradeItemIdentity
                {
                    CanonicalName = "Windscream",
                    CanonicalType = "Reinforced Greaves",
                });
            var curse = Assert.Single(
                resolved.ModifierFilters,
                component => component.OriginalText.Contains("additional Curse", StringComparison.Ordinal));
            var selected = resolved with
            {
                ModifierFilters = resolved.ModifierFilters
                    .Select(component => component with
                    {
                        IsSelected = string.Equals(
                            component.ComponentId,
                            curse.ComponentId,
                            StringComparison.Ordinal),
                    })
                    .ToArray(),
            };

            ModifierPipelineDiagnosticRecorder.TryCompleteCapture(
                selected,
                TradeSearchValidationResult.FromDiagnostics([]));

            var artifactPath = Directory.GetFiles(outputDirectory, "*.json").Single();
            var json = File.ReadAllText(artifactPath);
            Assert.Contains("\"diagnosticVersion\": \"E6b-generic-live-1\"", json, StringComparison.Ordinal);
            Assert.Contains("You can apply an additional Curse", json, StringComparison.Ordinal);
            Assert.Contains("\"providerPasses\"", json, StringComparison.Ordinal);
            Assert.Contains("\"consumer\"", json, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName, previous);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void IsEnabled_IsFalseWhenEnvironmentVariableMissing()
    {
        var previous = Environment.GetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName, null);
        try
        {
            Assert.False(ModifierPipelineDiagnosticRecorder.IsEnabled);
        }
        finally
        {
            Environment.SetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName, previous);
        }
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

    private static PathOfExileTradeStatEntry Stat(string id, string text, string type)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = 0,
            GroupId = type,
            GroupLabel = type,
            Id = id,
            Text = text,
            Type = type,
        };
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

    private sealed class StaticStatProvider(PathOfExileTradeStatCatalog catalog) : IPathOfExileTradeStatCatalogProvider
    {
        public bool TryGetCachedCatalog(out PathOfExileTradeStatCatalog cachedCatalog)
        {
            cachedCatalog = catalog;
            return true;
        }

        public Task<PathOfExileTradeStatCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeStatCatalogProviderResult.Success(catalog));
    }

    private sealed class StaticItemProvider(PathOfExileTradeItemCatalog catalog) : IPathOfExileTradeItemCatalogProvider
    {
        public bool TryGetCachedCatalog(out PathOfExileTradeItemCatalog cachedCatalog)
        {
            cachedCatalog = catalog;
            return true;
        }

        public Task<PathOfExileTradeItemCatalogProviderResult> GetCatalogAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(PathOfExileTradeItemCatalogProviderResult.Success(catalog));
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
}
