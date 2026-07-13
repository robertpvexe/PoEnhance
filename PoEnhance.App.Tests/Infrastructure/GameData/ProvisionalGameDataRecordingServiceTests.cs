using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.GameData;

public sealed class ProvisionalGameDataRecordingServiceTests
{
    private readonly ItemTextParser parser = new();
    private readonly DateTimeOffset now = new(2026, 7, 13, 11, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordAsync_MissingItemBaseIsRecorded()
    {
        using var fixture = RecordingFixture.Create(now);
        var item = ParseItem("Unknown Ring");
        var baseResolution = UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound);

        var result = await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            baseResolution,
            [],
            "event-1");

        Assert.True(result.IsSuccess);
        var record = Assert.Single((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
        Assert.Equal(ProvisionalGameDataRecordKind.ItemBase, record.Kind);
        Assert.Equal("item-base|rings|unknown ring", record.StableKey);
        Assert.Equal("unknown ring", record.NormalizedIdentity);
        Assert.Equal("Unknown Ring", record.OriginalIdentity);
        Assert.Equal("Rings", record.ItemClass);
        Assert.Equal("Mercenaries", record.League);
        Assert.Equal("3.28.0", record.Patch);
        Assert.Equal("unknown-missing-catalog", record.Confidence);
    }

    [Fact]
    public async Task RecordAsync_KnownExactBaseIsNotRecorded()
    {
        using var fixture = RecordingFixture.Create(now);
        var item = ParseItem("Gold Ring");

        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            new ItemBaseResolutionResult
            {
                Status = ItemBaseResolutionStatus.Exact,
                MatchedItemBase = new ItemBaseRecord { Id = "base.gold-ring", Name = "Gold Ring" },
                Diagnostics =
                [
                    new ItemBaseResolutionDiagnostic(
                        ItemBaseResolutionDiagnosticCodes.BaseExactMatch,
                        "Exact."),
                ],
            },
            [],
            "event-1");

        Assert.Empty((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
    }

    [Fact]
    public async Task RecordAsync_ProbableBaseIsNotRecorded()
    {
        using var fixture = RecordingFixture.Create(now);
        var item = ParseItem("Gold Ring");

        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            new ItemBaseResolutionResult
            {
                Status = ItemBaseResolutionStatus.Probable,
                MatchedItemBase = new ItemBaseRecord { Id = "base.gold-ring", Name = "Gold Ring" },
                Diagnostics =
                [
                    new ItemBaseResolutionDiagnostic(
                        ItemBaseResolutionDiagnosticCodes.BaseProbableMagicSuffixMatch,
                        "Probable."),
                ],
            },
            [],
            "event-1");

        Assert.Empty((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
    }

    [Fact]
    public async Task RecordAsync_AmbiguousBaseIsNotRecorded()
    {
        using var fixture = RecordingFixture.Create(now);
        var item = ParseItem("Shared Base");

        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            new ItemBaseResolutionResult
            {
                Status = ItemBaseResolutionStatus.Unknown,
                Candidates =
                [
                    new ItemBaseRecord { Id = "base.one", Name = "Shared Base" },
                    new ItemBaseRecord { Id = "base.two", Name = "Shared Base" },
                ],
                Diagnostics =
                [
                    new ItemBaseResolutionDiagnostic(
                        ItemBaseResolutionDiagnosticCodes.BaseAmbiguous,
                        "Ambiguous."),
                ],
            },
            [],
            "event-1");

        Assert.Empty((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
    }

    [Fact]
    public async Task RecordAsync_ZeroCandidateAuthenticPrefixOrSuffixIsRecorded()
    {
        using var fixture = RecordingFixture.Create(now);
        var item = ParseItemWithAdvancedModifier("Prefix", "New Hale");
        var modifierResult = MissingModifier(item.PrefixModifiers[0], ParsedModifierKind.Prefix, ModifierGenerationType.Prefix);

        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            ExactBase(),
            [modifierResult],
            "event-1");

        var record = Assert.Single((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
        Assert.Equal(ProvisionalGameDataRecordKind.Modifier, record.Kind);
        Assert.Equal("modifier|prefix|new hale", record.StableKey);
        Assert.Equal("New Hale", record.OriginalIdentity);
        Assert.Equal("Prefix", record.ModifierKind);
        Assert.Equal(ModifierGenerationType.Prefix, record.ModifierGenerationType);
    }

    [Fact]
    public async Task RecordAsync_AmbiguousModifierIsNotRecorded()
    {
        using var fixture = RecordingFixture.Create(now);
        var item = ParseItemWithAdvancedModifier("Prefix", "Hale");
        var modifier = item.PrefixModifiers[0];

        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            ExactBase(),
            [
                new ModifierCandidateResolutionResult(
                    0,
                    modifier,
                    modifier.Name,
                    ParsedModifierKind.Prefix,
                    ModifierGenerationType.Prefix,
                    ModifierCandidateResolutionStatus.Unknown,
                    [
                        new ModifierDefinition { Id = "mod.one", Name = "Hale" },
                        new ModifierDefinition { Id = "mod.two", Name = "Hale" },
                    ],
                    [
                        new ModifierCandidateResolutionDiagnostic(
                            ModifierCandidateResolutionDiagnosticCodes.ModifierAmbiguous,
                            "Ambiguous."),
                    ],
                    NameCandidateCount: 2,
                    GenerationKindCandidateCount: 2),
            ],
            "event-1");

        Assert.Empty((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
    }

    [Fact]
    public async Task RecordAsync_UnsupportedUniqueModifierIsNotRecorded()
    {
        using var fixture = RecordingFixture.Create(now);
        var modifier = new ParsedModifier(["Unique text"], "{ Unique Modifier \"Special\" }", ParsedModifierKind.Unique, "Special", null, null, null, false, false, false);

        await fixture.Service.RecordAsync(
            ParseItem("Gold Ring"),
            fixture.LoadedStatus,
            ExactBase(),
            [
                new ModifierCandidateResolutionResult(
                    0,
                    modifier,
                    "Special",
                    ParsedModifierKind.Unique,
                    null,
                    ModifierCandidateResolutionStatus.Unknown,
                    [],
                    [
                        new ModifierCandidateResolutionDiagnostic(
                            ModifierCandidateResolutionDiagnosticCodes.ModifierKindUnsupported,
                            "Unsupported."),
                    ]),
            ],
            "event-1");

        Assert.Empty((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
    }

    [Fact]
    public async Task RecordAsync_MissingAdvancedModifierNameIsNotRecorded()
    {
        using var fixture = RecordingFixture.Create(now);
        var modifier = new ParsedModifier(["Text"], "{ Prefix Modifier }", ParsedModifierKind.Prefix, null, null, null, null, false, false, false);

        await fixture.Service.RecordAsync(
            ParseItem("Gold Ring"),
            fixture.LoadedStatus,
            ExactBase(),
            [
                new ModifierCandidateResolutionResult(
                    0,
                    modifier,
                    null,
                    ParsedModifierKind.Prefix,
                    ModifierGenerationType.Prefix,
                    ModifierCandidateResolutionStatus.Unknown,
                    [],
                    [
                        new ModifierCandidateResolutionDiagnostic(
                            ModifierCandidateResolutionDiagnosticCodes.ModifierNameNotAvailable,
                            "Missing name."),
                    ]),
            ],
            "event-1");

        Assert.Empty((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
    }

    [Fact]
    public async Task RecordAsync_TranslationUnknownIsNotRecordedAsMissingData()
    {
        using var fixture = RecordingFixture.Create(now);
        var item = ParseItemWithAdvancedModifier("Prefix", "Hale");
        var modifier = item.PrefixModifiers[0];

        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            ExactBase(),
            [
                new ModifierCandidateResolutionResult(
                    0,
                    modifier,
                    modifier.Name,
                    ParsedModifierKind.Prefix,
                    ModifierGenerationType.Prefix,
                    ModifierCandidateResolutionStatus.Unknown,
                    [new ModifierDefinition { Id = "mod.hale", Name = "Hale" }],
                    [
                        new ModifierCandidateResolutionDiagnostic(
                            ModifierCandidateResolutionDiagnosticCodes.ModifierTextNotEvaluated,
                            "Translation unknown."),
                    ],
                    NameCandidateCount: 1,
                    GenerationKindCandidateCount: 1,
                    EligibilityCandidateCount: 1,
                    TextSignatureCandidateCount: 1),
            ],
            "event-1");

        Assert.Empty((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
    }

    [Fact]
    public async Task RecordAsync_GameDataUnavailableDoesNotCreateProvisionalRecords()
    {
        using var fixture = RecordingFixture.Create(now);
        var item = ParseItem("Unknown Ring");

        var result = await fixture.Service.RecordAsync(
            item,
            new RuntimeGameDataStatus(),
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "event-1");

        Assert.True(result.IsSuccess);
        Assert.Empty((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
    }

    [Fact]
    public async Task RecordAsync_RepeatedSameProcessingEventDoesNotIncrementRecordAgain()
    {
        using var fixture = RecordingFixture.Create(now);
        var item = ParseItem("Unknown Ring");

        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "event-1");
        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "event-1");

        var record = Assert.Single((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
        Assert.Equal(1, record.SeenCount);
    }

    [Fact]
    public async Task RecordAsync_TwoSeparateManualParseEventsWithSameUnknownBaseIncrementExistingRecord()
    {
        var clock = new StepClock(
            new DateTimeOffset(2026, 7, 13, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 13, 11, 5, 0, TimeSpan.Zero));
        using var fixture = RecordingFixture.Create(clock.GetUtcNow);
        var item = ParseItem("Future Test Ring");

        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "manual-parse-1");
        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "manual-parse-2");

        var record = Assert.Single((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
        Assert.Equal("item-base|rings|future test ring", record.StableKey);
        Assert.Equal(2, record.SeenCount);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 11, 0, 0, TimeSpan.Zero), record.FirstSeenUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 11, 5, 0, TimeSpan.Zero), record.LastSeenUtc);
    }

    [Fact]
    public async Task RecordAsync_RepeatedUiRenderingWithSameProcessingEventDoesNotIncrement()
    {
        var clock = new StepClock(
            new DateTimeOffset(2026, 7, 13, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 13, 11, 5, 0, TimeSpan.Zero));
        using var fixture = RecordingFixture.Create(clock.GetUtcNow);
        var item = ParseItem("Future Test Ring");

        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "same-rendered-result");
        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "same-rendered-result");

        var record = Assert.Single((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
        Assert.Equal(1, record.SeenCount);
        Assert.Equal(record.FirstSeenUtc, record.LastSeenUtc);
    }

    [Fact]
    public async Task RecordAsync_TwoSeparateClipboardCaptureEventsWithSameTextIncrement()
    {
        var clock = new StepClock(
            new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 13, 12, 2, 0, TimeSpan.Zero));
        using var fixture = RecordingFixture.Create(clock.GetUtcNow);
        var item = ParseItem("Future Test Ring");

        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "clipboard-capture-1");
        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "clipboard-capture-2");

        var record = Assert.Single((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
        Assert.Equal(2, record.SeenCount);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 12, 0, 0, TimeSpan.Zero), record.FirstSeenUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 12, 2, 0, TimeSpan.Zero), record.LastSeenUtc);
    }

    [Fact]
    public async Task RecordAsync_TwoSeparateModifierEventsWithSameMissingModifierIncrementExistingRecord()
    {
        var clock = new StepClock(
            new DateTimeOffset(2026, 7, 13, 13, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 13, 13, 1, 0, TimeSpan.Zero));
        using var fixture = RecordingFixture.Create(clock.GetUtcNow);
        var item = ParseItemWithAdvancedModifier("Suffix", "of the Future");
        var modifierResult = MissingModifier(
            item.SuffixModifiers[0],
            ParsedModifierKind.Suffix,
            ModifierGenerationType.Suffix);

        await fixture.Service.RecordAsync(item, fixture.LoadedStatus, ExactBase(), [modifierResult], "manual-parse-1");
        await fixture.Service.RecordAsync(item, fixture.LoadedStatus, ExactBase(), [modifierResult], "manual-parse-2");

        var record = Assert.Single((await fixture.Store.LoadSnapshotAsync()).Snapshot.Records);
        Assert.Equal("modifier|suffix|of the future", record.StableKey);
        Assert.Equal(2, record.SeenCount);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 13, 0, 0, TimeSpan.Zero), record.FirstSeenUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 13, 1, 0, TimeSpan.Zero), record.LastSeenUtc);
    }

    [Fact]
    public async Task RecordAsync_OneProcessingEventCannotIncrementSameStableKeyTwice()
    {
        using var fixture = RecordingFixture.Create(now);
        var item = ParseItemWithAdvancedModifier("Prefix", "Future");
        var first = MissingModifier(item.PrefixModifiers[0], ParsedModifierKind.Prefix, ModifierGenerationType.Prefix);
        var duplicate = first with { ParsedModifierIndex = 1 };

        await fixture.Service.RecordAsync(
            item,
            fixture.LoadedStatus,
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [first, duplicate],
            "manual-parse-1");

        var modifierRecord = Assert.Single(
            (await fixture.Store.LoadSnapshotAsync()).Snapshot.Records,
            record => record.Kind == ProvisionalGameDataRecordKind.Modifier);
        Assert.Equal(1, modifierRecord.SeenCount);
    }

    [Fact]
    public async Task RecordAsync_PersistedAndReloadedStoreContinuesIncrementing()
    {
        var clock = new StepClock(
            new DateTimeOffset(2026, 7, 13, 14, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 13, 14, 3, 0, TimeSpan.Zero));
        using var temp = TempDirectory.Create();
        var path = Path.Combine(temp.Path, "store.json");
        var firstStore = new JsonProvisionalGameDataStore(path);
        var firstService = new ProvisionalGameDataRecordingService(firstStore, clock.GetUtcNow);
        var item = ParseItem("Future Test Ring");

        await firstService.RecordAsync(
            item,
            RecordingFixture.CreateStatus(),
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "manual-parse-1");

        var reloadedStore = new JsonProvisionalGameDataStore(path);
        var reloadedService = new ProvisionalGameDataRecordingService(reloadedStore, clock.GetUtcNow);
        await reloadedService.RecordAsync(
            item,
            RecordingFixture.CreateStatus(),
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "manual-parse-2");

        var record = Assert.Single((await reloadedStore.LoadSnapshotAsync()).Snapshot.Records);
        Assert.Equal(2, record.SeenCount);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 14, 0, 0, TimeSpan.Zero), record.FirstSeenUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 13, 14, 3, 0, TimeSpan.Zero), record.LastSeenUtc);
    }

    [Fact]
    public async Task RecordAsync_StoreFailureDoesNotThrowIntoItemProcessing()
    {
        using var temp = TempDirectory.Create();
        var directoryAsFilePath = Path.Combine(temp.Path, "store-directory");
        Directory.CreateDirectory(directoryAsFilePath);
        var store = new JsonProvisionalGameDataStore(directoryAsFilePath);
        var service = new ProvisionalGameDataRecordingService(store, () => now);
        var item = ParseItem("Unknown Ring");

        var result = await service.RecordAsync(
            item,
            RecordingFixture.CreateStatus(),
            UnknownBase(ItemBaseResolutionDiagnosticCodes.BaseNotFound),
            [],
            "event-1");

        Assert.False(result.IsSuccess);
        Assert.Contains("failed", result.Diagnostic, StringComparison.OrdinalIgnoreCase);
    }

    private ParsedItem ParseItem(string baseType)
    {
        return parser.Parse($$"""
Item Class: Rings
Rarity: Rare
Dire Loop
{{baseType}}
--------
Item Level: 80
""");
    }

    private ParsedItem ParseItemWithAdvancedModifier(string kind, string modifierName)
    {
        return parser.Parse($$"""
Item Class: Rings
Rarity: Rare
Dire Loop
Gold Ring
--------
Item Level: 80
--------
{ {{kind}} Modifier "{{modifierName}}" (Tier: 5) - Test }
+50 to maximum Life
""");
    }

    private static ItemBaseResolutionResult UnknownBase(string code)
    {
        return new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Unknown,
            Diagnostics = [new ItemBaseResolutionDiagnostic(code, "Test.")],
        };
    }

    private static ItemBaseResolutionResult ExactBase()
    {
        return new ItemBaseResolutionResult
        {
            Status = ItemBaseResolutionStatus.Exact,
            MatchedItemBase = new ItemBaseRecord { Id = "base.gold-ring", Name = "Gold Ring" },
            Diagnostics =
            [
                new ItemBaseResolutionDiagnostic(
                    ItemBaseResolutionDiagnosticCodes.BaseExactMatch,
                    "Exact."),
            ],
        };
    }

    private static ModifierCandidateResolutionResult MissingModifier(
        ParsedModifier modifier,
        ParsedModifierKind kind,
        ModifierGenerationType generationType)
    {
        return new ModifierCandidateResolutionResult(
            0,
            modifier,
            modifier.Name,
            kind,
            generationType,
            ModifierCandidateResolutionStatus.Unknown,
            [],
            [
                new ModifierCandidateResolutionDiagnostic(
                    ModifierCandidateResolutionDiagnosticCodes.ModifierNotFound,
                    "Not found."),
            ],
            NameCandidateCount: 0,
            GenerationKindCandidateCount: 0);
    }

    private sealed class RecordingFixture : IDisposable
    {
        private readonly TempDirectory tempDirectory;

        private RecordingFixture(
            TempDirectory tempDirectory,
            JsonProvisionalGameDataStore store,
            ProvisionalGameDataRecordingService service,
            RuntimeGameDataStatus loadedStatus)
        {
            this.tempDirectory = tempDirectory;
            Store = store;
            Service = service;
            LoadedStatus = loadedStatus;
        }

        public JsonProvisionalGameDataStore Store { get; }

        public ProvisionalGameDataRecordingService Service { get; }

        public RuntimeGameDataStatus LoadedStatus { get; }

        public static RecordingFixture Create(DateTimeOffset now)
        {
            return Create(() => now);
        }

        public static RecordingFixture Create(Func<DateTimeOffset> getUtcNow)
        {
            var temp = TempDirectory.Create();
            var store = new JsonProvisionalGameDataStore(Path.Combine(temp.Path, "store.json"));
            return new RecordingFixture(
                temp,
                store,
                new ProvisionalGameDataRecordingService(store, getUtcNow),
                CreateStatus());
        }

        public static RuntimeGameDataStatus CreateStatus()
        {
            var package = new GameDataPackage
            {
                Manifest = new GameDataPackageManifest
                {
                    SchemaVersion = 1,
                    DataVersion = "test-data",
                    CreatedAtUtc = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
                    League = "Mercenaries",
                    Patch = "3.28.0",
                    Sources =
                    [
                        new GameDataPackageSource
                        {
                            SourceId = "test",
                            SourceVersion = "test-source",
                            RetrievedAtUtc = new DateTimeOffset(2026, 7, 13, 0, 0, 0, TimeSpan.Zero),
                        },
                    ],
                },
            };

            return new RuntimeGameDataStatus
            {
                State = RuntimeGameDataState.Loaded,
                Package = package,
                Catalog = GameDataCatalog.FromPackage(package),
            };
        }

        public void Dispose()
        {
            tempDirectory.Dispose();
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"poenhance-provisional-recording-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class StepClock
    {
        private readonly DateTimeOffset[] timestamps;
        private int index;

        public StepClock(params DateTimeOffset[] timestamps)
        {
            this.timestamps = timestamps;
        }

        public DateTimeOffset GetUtcNow()
        {
            var currentIndex = Math.Min(index, timestamps.Length - 1);
            index++;
            return timestamps[currentIndex];
        }
    }
}
