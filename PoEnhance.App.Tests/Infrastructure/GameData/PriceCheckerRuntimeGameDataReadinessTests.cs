using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.GameData;

public sealed class PriceCheckerRuntimeGameDataReadinessTests
{
    private const string OrdinaryRingClipboard = """
        Item Class: Rings
        Rarity: Rare
        Ember Loop
        Gold Ring
        --------
        Item Level: 70
        --------
        +10 to Strength
        """;

    private const string HrimnorClipboard = """
        Item Class: Two Hand Maces
        Rarity: Unique
        Hrimnor's Hymn
        Sledgehammer
        --------
        Two Handed Mace
        Physical Damage: 45-67
        Critical Strike Chance: 5.00%
        Attacks per Second: 1.30
        Weapon Range: 1.3 metres
        --------
        Requirements:
        Level: 17
        Str: 62
        --------
        Item Level: 70
        --------
        { Implicit Modifier }
        45% increased Stun Duration on Enemies
        --------
        { Unique Modifier — Damage, Physical, Attack }
        150(140-200)% increased Physical Damage
        { Unique Modifier — Life, Physical, Attack }
        1% of Physical Attack Damage Leeched as Life
        { Unique Modifier — Attribute }
        +10 to Strength
        { Unique Modifier }
        15% reduced Enemy Stun Threshold
        { Unique Modifier }
        45(40-50)% increased Stun Duration on Enemies
        """;

    [Fact]
    public async Task WaitForLoadCompletionAsync_WhenAlreadyLoaded_ReturnsImmediatelyWithoutReloading()
    {
        var loadCount = 0;
        var service = CreateService(
            configuredPath: "package.json",
            loadPackageAsync: (_, _) =>
            {
                loadCount++;
                return Task.FromResult(SuccessfulLoad());
            });

        await service.LoadAsync([]);
        var before = service.Current;
        var waited = await service.WaitForLoadCompletionAsync();

        Assert.Equal(RuntimeGameDataState.Loaded, waited.State);
        Assert.Same(before, waited);
        Assert.Equal(1, loadCount);
    }

    [Fact]
    public async Task WaitForLoadCompletionAsync_WhileLoading_AwaitsExistingLoad()
    {
        var releaseLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateService(
            configuredPath: "package.json",
            loadPackageAsync: async (_, _) =>
            {
                await releaseLoad.Task;
                return SuccessfulLoad();
            });

        var loadTask = service.LoadAsync([]);
        Assert.Equal(RuntimeGameDataState.Loading, service.Current.State);

        var waitTask = service.WaitForLoadCompletionAsync();
        Assert.False(waitTask.IsCompleted);
        releaseLoad.SetResult();

        var waited = await waitTask;
        var loaded = await loadTask;
        Assert.Equal(RuntimeGameDataState.Loaded, waited.State);
        Assert.Same(loaded, waited);
        Assert.NotNull(waited.Catalog);
    }

    [Fact]
    public async Task Gate_LoadingThenLoaded_CreateDraftReceivesNonNullCatalog()
    {
        var releaseLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateService(
            configuredPath: "package.json",
            loadPackageAsync: async (_, _) =>
            {
                await releaseLoad.Task;
                return SuccessfulLoad();
            });
        var mapper = new RecordingDraftMapper();
        _ = service.LoadAsync([]);

        var preparationTask = PriceCheckerCapturedTextPreparation.PrepareAsync(
            OrdinaryRingClipboard,
            service,
            new ItemTextParser(),
            new ParsedItemGameDataDisplayService());
        Assert.False(preparationTask.IsCompleted);

        releaseLoad.SetResult();
        var preparation = await preparationTask;
        Assert.True(preparation.IsReady);
        Assert.NotNull(preparation.Catalog);

        var draft = mapper.CreateDraft(
            preparation.ParsedItem!,
            preparation.ItemBaseResolution!.Result,
            preparation.ModifierCandidateResolutions!.Results
                .Select(display => display.Result)
                .OfType<ModifierCandidateResolutionResult>()
                .ToArray(),
            preparation.Catalog);
        Assert.True(draft.IsSuccess);
        Assert.Equal(1, mapper.CallCount);
        Assert.NotNull(Assert.Single(mapper.CatalogArguments));
    }

    [Fact]
    public async Task Gate_FailedLoad_DoesNotInvokeCreateDraft()
    {
        var service = CreateService(
            configuredPath: "package.json",
            loadPackageAsync: (_, _) => Task.FromResult(new GameDataPackageLoadResult
            {
                SourcePath = "package.json",
                Diagnostics =
                [
                    new GameDataPackageLoadDiagnostic(
                        GameDataPackageLoadDiagnosticCodes.JsonInvalid,
                        "invalid"),
                ],
            }));
        await service.LoadAsync([]);
        var mapper = new RecordingDraftMapper();

        var preparation = await PriceCheckerCapturedTextPreparation.PrepareAsync(
            OrdinaryRingClipboard,
            service,
            new ItemTextParser(),
            new ParsedItemGameDataDisplayService());

        Assert.False(preparation.IsReady);
        Assert.Equal(RuntimeGameDataState.Failed, preparation.Readiness.Status.State);
        Assert.Equal(0, mapper.CallCount);
        Assert.Contains("failed to load", preparation.UserFacingStatus, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Gate_LoadedFastPath_DoesNotDelayAndPassesCatalog()
    {
        var service = CreateService(
            configuredPath: "package.json",
            loadPackageAsync: (_, _) => Task.FromResult(SuccessfulLoad()));
        await service.LoadAsync([]);
        var mapper = new RecordingDraftMapper();

        var preparation = await PriceCheckerCapturedTextPreparation.PrepareAsync(
            OrdinaryRingClipboard,
            service,
            new ItemTextParser(),
            new ParsedItemGameDataDisplayService());

        Assert.True(preparation.IsReady);
        var draft = mapper.CreateDraft(
            preparation.ParsedItem!,
            preparation.ItemBaseResolution!.Result,
            [],
            preparation.Catalog);
        Assert.True(draft.IsSuccess);
        Assert.Same(service.Current.Catalog, Assert.Single(mapper.CatalogArguments));
    }

    [Fact]
    public async Task OneUserCapture_WhileLoading_ProducesOneDraftWithCatalog_WithoutReReadingText()
    {
        var releaseLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = CreateService(
            configuredPath: "package.json",
            loadPackageAsync: async (_, _) =>
            {
                await releaseLoad.Task;
                return SuccessfulLoad();
            });
        var mapper = new RecordingDraftMapper();
        var capturedTextReads = 0;
        _ = service.LoadAsync([]);

        // Simulate the production order: clipboard text is captured once, then preparation waits.
        capturedTextReads++;
        var capturedText = OrdinaryRingClipboard;
        var preparationTask = PriceCheckerCapturedTextPreparation.PrepareAsync(
            capturedText,
            service,
            new ItemTextParser(),
            new ParsedItemGameDataDisplayService());
        Assert.Equal(1, capturedTextReads);
        Assert.False(preparationTask.IsCompleted);
        Assert.Equal(0, mapper.CallCount);

        releaseLoad.SetResult();
        var preparation = await preparationTask;
        Assert.True(preparation.IsReady);
        mapper.CreateDraft(
            preparation.ParsedItem!,
            preparation.ItemBaseResolution!.Result,
            [],
            preparation.Catalog);

        Assert.Equal(1, capturedTextReads);
        Assert.Equal(1, mapper.CallCount);
        Assert.NotNull(Assert.Single(mapper.CatalogArguments));
    }

    [Fact]
    public async Task Hrimnor_ProductionPreparationPath_PreservesLeechExactProvenance()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        if (!File.Exists(packagePath))
        {
            return;
        }

        var loadCount = 0;
        var releaseLoad = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new RuntimeGameDataService(
            new FixedPathResolver(packagePath),
            async (path, cancellationToken) =>
            {
                loadCount++;
                await releaseLoad.Task.WaitAsync(cancellationToken);
                return await GameDataPackageLoader.LoadFromFileAsync(path, cancellationToken);
            },
            GameDataCatalog.FromPackage);

        _ = service.LoadAsync([]);
        var preparationTask = PriceCheckerCapturedTextPreparation.PrepareAsync(
            HrimnorClipboard,
            service,
            new ItemTextParser(),
            new ParsedItemGameDataDisplayService());
        Assert.False(preparationTask.IsCompleted);
        releaseLoad.SetResult();

        var preparation = await preparationTask;
        Assert.True(preparation.IsReady);
        Assert.Equal(1, loadCount);

        var draft = new TradeSearchDraftMapper().CreateDraft(
            preparation.ParsedItem!,
            preparation.ItemBaseResolution!.Result,
            preparation.ModifierCandidateResolutions!.Results
                .Select(display => display.Result)
                .OfType<ModifierCandidateResolutionResult>()
                .ToArray(),
            preparation.Catalog);
        Assert.True(draft.IsSuccess);
        var leech = Assert.Single(
            draft.Draft!.ModifierFilters,
            component => component.RawCopiedText.Contains("Leeched as Life", StringComparison.OrdinalIgnoreCase));
        Assert.True(leech.HasExactUniqueSourceProvenance);
        Assert.True(leech.IsEquivalentSourceSet);
        Assert.Contains(
            "local_life_leech_from_physical_damage_permyriad",
            leech.ResolvedStatIds);
        Assert.Equal(
            UniqueHistoricalEncodingAggregationCodes.HistoricalEncodingConflictDidNotOverrideCurrentProof,
            leech.UniqueAggregationDiagnosticCode);
    }

    private static RuntimeGameDataService CreateService(
        string? configuredPath,
        Func<string, CancellationToken, Task<GameDataPackageLoadResult>> loadPackageAsync)
    {
        return new RuntimeGameDataService(
            new StubPathResolver(configuredPath),
            loadPackageAsync,
            GameDataCatalog.FromPackage);
    }

    private static GameDataPackageLoadResult SuccessfulLoad()
    {
        return new GameDataPackageLoadResult
        {
            Package = CreatePackage(),
            SourcePath = "package.json",
        };
    }

    private static GameDataPackage CreatePackage()
    {
        return new GameDataPackage
        {
            Manifest = new GameDataPackageManifest
            {
                SchemaVersion = 1,
                DataVersion = "test-data",
                CreatedAtUtc = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
                Sources =
                [
                    new GameDataPackageSource
                    {
                        SourceId = "test",
                        SourceVersion = "test-source",
                        RetrievedAtUtc = new DateTimeOffset(2026, 7, 12, 0, 0, 0, TimeSpan.Zero),
                    },
                ],
            },
            ItemBases =
            [
                new ItemBaseRecord
                {
                    Id = "item-base.gold-ring",
                    Name = "Gold Ring",
                    ItemClass = "Rings",
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "test",
                            ExternalId = "item-base.gold-ring",
                        },
                    ],
                },
            ],
            Modifiers = [],
            Stats = [],
            StatTranslations = [],
        };
    }

    private sealed class RecordingDraftMapper : ITradeSearchDraftMapper
    {
        public int CallCount { get; private set; }

        public List<GameDataCatalog?> CatalogArguments { get; } = [];

        public TradeSearchDraftResult CreateDraft(
            ParsedItem parsedItem,
            ItemBaseResolutionResult? itemBaseResolution,
            IReadOnlyList<ModifierCandidateResolutionResult> modifierResolutions,
            GameDataCatalog? gameDataCatalog)
        {
            CallCount++;
            CatalogArguments.Add(gameDataCatalog);
            return new TradeSearchDraftMapper().CreateDraft(
                parsedItem,
                itemBaseResolution,
                modifierResolutions,
                gameDataCatalog);
        }
    }

    private sealed class StubPathResolver : GameDataPackagePathResolver
    {
        private readonly string? configuredPath;

        public StubPathResolver(string? configuredPath)
        {
            this.configuredPath = configuredPath;
        }

        public override GameDataPackagePathResolution Resolve(IReadOnlyList<string> commandLineArgs)
        {
            return configuredPath is null
                ? new GameDataPackagePathResolution(null, GameDataPackagePathSource.None)
                : new GameDataPackagePathResolution(configuredPath, GameDataPackagePathSource.CommandLine);
        }
    }

    private sealed class FixedPathResolver : GameDataPackagePathResolver
    {
        private readonly string path;

        public FixedPathResolver(string path)
        {
            this.path = path;
        }

        public override GameDataPackagePathResolution Resolve(IReadOnlyList<string> commandLineArgs)
        {
            return new GameDataPackagePathResolution(path, GameDataPackagePathSource.CommandLine);
        }
    }
}
