using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.GameData;

public sealed class RuntimeGameDataServiceTests
{
    [Fact]
    public async Task LoadAsync_SuccessfulLoadProducesLoadedAndExposesCatalog()
    {
        var package = CreatePackage();
        var service = CreateService(
            configuredPath: "package.json",
            loadPackageAsync: (_, _) => Task.FromResult(new GameDataPackageLoadResult
            {
                Package = package,
                SourcePath = "package.json",
            }));

        var result = await service.LoadAsync([]);

        Assert.Equal(RuntimeGameDataState.Loaded, result.State);
        Assert.Same(result, service.Current);
        Assert.NotNull(result.Catalog);
        Assert.Equal("test-data", result.DataVersion);
        Assert.Equal("test-source", result.SourceVersion);
        Assert.Equal(1, result.ItemBaseCount);
        Assert.Equal(1, result.ModifierCount);
        Assert.Equal(1, result.StatCount);
        Assert.Equal(1, result.StatTranslationCount);
        Assert.Equal("item-base.gold-ring", Assert.Single(result.Catalog!.FindItemBasesByNormalizedName("gold ring")).Id);
    }

    [Fact]
    public async Task LoadAsync_MalformedPackageProducesFailed()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"poenhance-malformed-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempPath, "not json");
        try
        {
            var service = CreateService(
                configuredPath: tempPath,
                loadPackageAsync: GameDataPackageLoader.LoadFromFileAsync);

            var result = await service.LoadAsync([]);

            Assert.Equal(RuntimeGameDataState.Failed, result.State);
            Assert.Null(result.Catalog);
            Assert.Equal(
                GameDataPackageLoadDiagnosticCodes.JsonInvalid,
                Assert.Single(result.Diagnostics).Code);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public async Task LoadAsync_NotConfiguredDoesNotInvokeLoader()
    {
        var loadCount = 0;
        var service = CreateService(
            configuredPath: null,
            loadPackageAsync: (_, _) =>
            {
                loadCount++;
                return Task.FromResult(new GameDataPackageLoadResult());
            });

        var result = await service.LoadAsync([]);

        Assert.Equal(RuntimeGameDataState.NotConfigured, result.State);
        Assert.Null(result.Catalog);
        Assert.Equal(0, loadCount);
        Assert.Contains("beside PoEnhance.exe", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("development artifacts", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_LoadsOnlyOnce()
    {
        var loadCount = 0;
        var service = CreateService(
            configuredPath: "package.json",
            loadPackageAsync: (_, _) =>
            {
                loadCount++;
                return Task.FromResult(new GameDataPackageLoadResult
                {
                    Package = CreatePackage(),
                    SourcePath = "package.json",
                });
            });

        var first = await service.LoadAsync([]);
        var second = await service.LoadAsync([]);

        Assert.Equal(RuntimeGameDataState.Loaded, first.State);
        Assert.Same(first, second);
        Assert.Equal(1, loadCount);
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
            Modifiers =
            [
                new ModifierDefinition
                {
                    Id = "mod.gold-ring-implicit",
                    GroupId = "mod-group.gold-ring-implicit",
                    GenerationType = ModifierGenerationType.Implicit,
                    Stats =
                    [
                        new ModifierStat
                        {
                            Index = 0,
                            StatId = "base_item_found_rarity_+%",
                            MinValue = 6m,
                            MaxValue = 15m,
                        },
                    ],
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "test",
                            ExternalId = "mod.gold-ring-implicit",
                        },
                    ],
                },
            ],
            Stats =
            [
                new StatDefinition
                {
                    Id = "base_item_found_rarity_+%",
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "test",
                            ExternalId = "base_item_found_rarity_+%",
                        },
                    ],
                },
            ],
            StatTranslations =
            [
                new StatTranslationDefinition
                {
                    Id = "translation.item-rarity",
                    StatIds = ["base_item_found_rarity_+%"],
                    Language = "English",
                    Variants =
                    [
                        new StatTranslationVariant
                        {
                            Conditions =
                            [
                                new StatTranslationCondition
                                {
                                    Index = 0,
                                    MinValue = 1m,
                                },
                            ],
                            ValueFormats = ["#"],
                            IndexHandlers =
                            [
                                new StatTranslationIndexHandler
                                {
                                    Index = 0,
                                    Handlers = [],
                                },
                            ],
                            FormatLines = ["{0}% increased Rarity of Items found"],
                        },
                    ],
                    Sources =
                    [
                        new GameDataSourceReference
                        {
                            SourceId = "test",
                            ExternalId = "translation.item-rarity",
                        },
                    ],
                },
            ],
        };
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
}
