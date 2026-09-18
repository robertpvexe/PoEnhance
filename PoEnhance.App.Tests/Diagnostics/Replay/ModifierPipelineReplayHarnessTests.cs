using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Diagnostics.Replay;

public sealed class ModifierPipelineReplayHarnessTests
{
    [Fact]
    public async Task Gate_ShaMismatch_RefusesReplay()
    {
        var identity = await LoadRepoGameDataIdentityAsync();
        var capture = MinimalReplayReadyCapture(
            identity.DataVersion,
            "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
            "Item Class: Jewels\nRarity: Unique\nLethal Pride\nTimeless Jewel\n");
        var decision = ModifierPipelineReplayGameDataGate.Evaluate(capture, identity);
        Assert.False(decision.CanReplay);
        Assert.Equal(ModifierPipelineReplayDivergenceClass.GameDataMismatch, decision.DivergenceClass);
    }

    [Fact]
    public async Task Gate_VersionMismatch_RefusesReplay()
    {
        var identity = await LoadRepoGameDataIdentityAsync();
        var capture = MinimalReplayReadyCapture(
            "not-the-real-version",
            identity.Sha256,
            "Item Class: Jewels\nRarity: Unique\nLethal Pride\nTimeless Jewel\n");
        var decision = ModifierPipelineReplayGameDataGate.Evaluate(capture, identity);
        Assert.False(decision.CanReplay);
        Assert.Equal(ModifierPipelineReplayDivergenceClass.GameDataMismatch, decision.DivergenceClass);
    }

    [Fact]
    public void Gate_AuditOnlyMissingReplayContext_RefusesSafely()
    {
        var capture = new ModifierPipelineReplayCaptureDocument
        {
            Item = new ModifierPipelineReplayCaptureItem { DisplayName = "Legacy" },
        };
        Assert.False(ModifierPipelineReplayGameDataGate.IsReplayReady(capture, out var reason));
        Assert.Contains("replayContext", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Gate_UnknownSchema_RefusesSafely()
    {
        var capture = new ModifierPipelineReplayCaptureDocument
        {
            ReplayContext = new ModifierPipelineReplayCaptureReplayContext
            {
                CaptureSchemaVersion = "A.9.9-replay-future",
                RawClipboardText = "raw",
                GameDataVersion = "v",
                GameDataSha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            },
            Item = new ModifierPipelineReplayCaptureItem { DisplayName = "X" },
        };
        Assert.False(ModifierPipelineReplayGameDataGate.IsReplayReady(capture, out _));
    }

    [Fact]
    public void Normalizer_IgnoresTimestampsAndUsesStableRowKeys()
    {
        var left = ModifierPipelineReplayNormalizer.FromCapture(MinimalReplayReadyCapture(
            "v",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            "raw",
            includeModifier: true));
        var right = ModifierPipelineReplayNormalizer.FromCapture(MinimalReplayReadyCapture(
            "v",
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            "raw",
            includeModifier: true));
        Assert.Equal(left.Modifiers.Single().RowKey, right.Modifiers.Single().RowKey);
        Assert.DoesNotContain("CapturedAtUtc", left.DisplayName, StringComparison.Ordinal);
    }

    [Fact]
    public void Comparer_MissingCaptureField_IsCaptureFieldUnavailable()
    {
        var captured = new ModifierPipelineNormalizedItem
        {
            ItemClass = "Jewels",
            Rarity = "Unique",
            DisplayName = "X",
            BaseType = "Timeless Jewel",
            Modifiers =
            [
                new ModifierPipelineNormalizedModifier
                {
                    RowKey = "0|-1|text",
                    SourceModifierIndex = 0,
                    SourceLineIndex = -1,
                    OriginalText = "text",
                    CoreStatus = "Unknown",
                    UniqueBlockDiagnosticCode = "UNIQUE_BLOCK_VERSION_MISMATCH",
                },
            ],
        };
        var replayed = new ModifierPipelineNormalizedItem
        {
            ItemClass = "Jewels",
            Rarity = "Unique",
            DisplayName = "X",
            BaseType = "Timeless Jewel",
            Modifiers =
            [
                new ModifierPipelineNormalizedModifier
                {
                    RowKey = "0|-1|text",
                    SourceModifierIndex = 0,
                    SourceLineIndex = -1,
                    OriginalText = "text",
                    CoreStatus = "Unknown",
                    UniqueBlockDiagnosticCode = "UNIQUE_BLOCK_VERSION_MISMATCH",
                    SourceDiagnosticCode = "only-on-replay",
                },
            ],
            DraftFilters =
            [
                new ModifierPipelineNormalizedDraftFilter
                {
                    ComponentId = "modifier:0:0",
                    IsSearchable = false,
                },
            ],
        };

        var comparison = ModifierPipelineReplayComparer.Compare(captured, replayed);
        Assert.Equal(ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable, comparison.Classification);
        Assert.Contains(
            comparison.Deltas,
            delta => delta.Classification == ModifierPipelineReplayDivergenceClass.CaptureFieldUnavailable);
    }

    [Fact]
    public void Comparer_CoreSemanticDifference_IsInputEquivalentOutputDivergence()
    {
        var captured = BaseNormalized("Unknown", "UNIQUE_BLOCK_VERSION_MISMATCH");
        var replayed = BaseNormalized("Exact", null);
        var comparison = ModifierPipelineReplayComparer.Compare(captured, replayed);
        Assert.Equal(
            ModifierPipelineReplayDivergenceClass.InputEquivalentOutputDivergence,
            comparison.Classification);
    }

    [Fact]
    public void Comparer_ProviderOnlyDifference_IsProviderContextUnverified()
    {
        var captured = BaseNormalized("Exact", null, providerStatus: "Exact", providerId: "explicit.a");
        var replayed = BaseNormalized("Exact", null, providerStatus: "Unsupported", providerId: null);
        var comparison = ModifierPipelineReplayComparer.Compare(captured, replayed);
        Assert.Equal(
            ModifierPipelineReplayDivergenceClass.ProviderContextUnverified,
            comparison.Classification);
    }

    [Fact]
    public async Task Runner_DuplicateCapturesAreRetained()
    {
        var identity = await LoadRepoGameDataIdentityAsync();
        var directory = CreateTempCorpus(
            ("a.json", Serialize(MinimalReplayReadyCapture(identity.DataVersion, identity.Sha256, WindscreamRaw, includeModifier: true, name: "Windscream"))),
            ("b.json", Serialize(MinimalReplayReadyCapture(identity.DataVersion, identity.Sha256, WindscreamRaw, includeModifier: true, name: "Windscream"))));
        var output = Path.Combine(Path.GetTempPath(), $"PoEnhanceReplayOut-{Guid.NewGuid():N}");
        try
        {
            var report = await ModifierPipelineReplayRunner.ReplayDirectoryAsync(
                directory,
                identity.PackagePath,
                output,
                tradeStatCatalog: LoadPinnedOfficialTradeCatalog(),
                tradeItemCatalog: new PathOfExileTradeItemCatalog([]),
                filterCatalog: PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());
            Assert.Equal(2, report.Items.Count);
            Assert.Equal(2, report.Summary.ReplayReady);
        }
        finally
        {
            Directory.Delete(directory, true);
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    [Fact]
    public async Task Runner_DoesNotHardcodeItemNames_AndPassesRawUnchanged()
    {
        var identity = await LoadRepoGameDataIdentityAsync();
        var raw = WindscreamRaw + "\n#marker-not-a-hardcode";
        var capture = MinimalReplayReadyCapture(identity.DataVersion, identity.Sha256, raw, includeModifier: true, name: "Windscream");
        var directory = CreateTempCorpus(("w.json", Serialize(capture)));
        var output = Path.Combine(Path.GetTempPath(), $"PoEnhanceReplayOut-{Guid.NewGuid():N}");
        try
        {
            var report = await ModifierPipelineReplayRunner.ReplayDirectoryAsync(
                directory,
                identity.PackagePath,
                output,
                tradeStatCatalog: LoadPinnedOfficialTradeCatalog(),
                tradeItemCatalog: new PathOfExileTradeItemCatalog([]),
                filterCatalog: PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());
            var item = Assert.Single(report.Items);
            Assert.NotEqual(ModifierPipelineReplayDivergenceClass.ReplayError, item.Classification);
            Assert.Equal(
                ModifierPipelineReplayNormalizer.HashRawClipboard(raw),
                item.RawClipboardSha256);
            Assert.DoesNotContain("Lethal Pride", Serialize(capture), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    [Fact]
    public async Task Runner_ReplayException_IsReplayError()
    {
        var identity = await LoadRepoGameDataIdentityAsync();
        var capturePath = Path.Combine(Path.GetTempPath(), $"replay-err-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            capturePath,
            Serialize(MinimalReplayReadyCapture(identity.DataVersion, identity.Sha256, "not-an-item")));
        try
        {
            // Sabotage package path identity object by using a catalog that will fail CreateDraft uniqueness path
            // while keeping SHA/version gate green.
            var result = ModifierPipelineReplayRunner.ReplayFile(
                capturePath,
                identity with { Catalog = null! },
                LoadPinnedOfficialTradeCatalog(),
                new PathOfExileTradeItemCatalog([]),
                PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());
            Assert.Equal(ModifierPipelineReplayDivergenceClass.ReplayError, result.Classification);
        }
        finally
        {
            File.Delete(capturePath);
        }
    }

    private static ModifierPipelineNormalizedItem BaseNormalized(
        string coreStatus,
        string? blockDiagnostic,
        string? providerStatus = "Unsupported",
        string? providerId = null) =>
        new()
        {
            ItemClass = "Jewels",
            Rarity = "Unique",
            DisplayName = "X",
            BaseType = "Timeless Jewel",
            UniqueResolutionStatus = "ExactIdentity",
            UniqueResolutionDiagnosticCode = "UNIQUE_VERSION_NOT_FOUND",
            Modifiers =
            [
                new ModifierPipelineNormalizedModifier
                {
                    RowKey = "0|-1|seed",
                    SourceModifierIndex = 0,
                    SourceLineIndex = -1,
                    OriginalText = "seed",
                    CoreStatus = coreStatus,
                    UniqueBlockDiagnosticCode = blockDiagnostic,
                    ProviderStatus = providerStatus,
                    ProviderStatIds = string.IsNullOrWhiteSpace(providerId) ? [] : [providerId],
                    IsSearchable = false,
                },
            ],
        };

    private static ModifierPipelineReplayCaptureDocument MinimalReplayReadyCapture(
        string version,
        string sha,
        string raw,
        bool includeModifier = false,
        string name = "Fixture")
    {
        return new ModifierPipelineReplayCaptureDocument
        {
            DiagnosticVersion = "E6b-generic-live-1",
            CapturedAtUtc = DateTimeOffset.UtcNow,
            ReplayContext = new ModifierPipelineReplayCaptureReplayContext
            {
                CaptureSchemaVersion = ModifierPipelineReplaySchemas.KnownReplaySchemaVersion,
                RawClipboardText = raw,
                InputKind = "PathOfExileClipboard",
                GameDataVersion = version,
                GameDataSha256 = sha,
                GameDataPathSource = "CommandLine",
            },
            Item = new ModifierPipelineReplayCaptureItem
            {
                ItemClass = "Boots",
                Rarity = "Unique",
                DisplayName = name,
                ParsedBaseType = "Reinforced Greaves",
                BaseResolutionStatus = "Exact",
                ResolvedBaseName = "Reinforced Greaves",
            },
            UniqueIdentity = new ModifierPipelineReplayCaptureUniqueIdentity
            {
                CanonicalName = name,
                CanonicalType = "Reinforced Greaves",
            },
            UniqueMechanicalResolution = new ModifierPipelineReplayCaptureUniqueMechanicalResolution
            {
                Status = "Exact",
                DiagnosticCode = null,
                IdentityCanonicalName = name,
                ModifierBlocks = [],
            },
            Modifiers = includeModifier
                ?
                [
                    new ModifierPipelineReplayCaptureModifier
                    {
                        ComponentId = "modifier:0:0",
                        SourceModifierIndex = 0,
                        SourceLineIndex = 0,
                        Raw = new ModifierPipelineReplayCaptureRaw
                        {
                            ParsedKind = "Unique",
                            OriginalText = "You can apply an additional Curse",
                            ValueLines = ["You can apply an additional Curse"],
                        },
                        SourceResolution = new ModifierPipelineReplayCaptureSourceResolution
                        {
                            Status = "Exact",
                            ResolvedModifierId = "UniqueAdditionalCurse",
                            ResolvedStatIds = ["base_additional_curse"],
                        },
                        ResolvedSemantics = new ModifierPipelineReplayCaptureSemantics
                        {
                            ParsedKind = "Unique",
                            ResolvedSourceKind = "Unique",
                            HasExactUniqueSourceProvenance = true,
                        },
                        ProviderResolution = new ModifierPipelineReplayCaptureProviderOutcome
                        {
                            ProviderResolutionStatus = "Exact",
                            ProviderStatId = "explicit.stat_30642521",
                        },
                        Consumer = new ModifierPipelineReplayCaptureConsumer
                        {
                            IsSearchable = true,
                            AvailabilityStatus = "Supported",
                        },
                    },
                ]
                : [],
        };
    }

    private static string Serialize(ModifierPipelineReplayCaptureDocument capture) =>
        JsonSerializer.Serialize(capture, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        });

    private static string CreateTempCorpus(params (string Name, string Json)[] files)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"PoEnhanceReplayCorpus-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        foreach (var (name, json) in files)
        {
            File.WriteAllText(Path.Combine(directory, name), json);
        }

        return directory;
    }

    private static async Task<ModifierPipelineReplayGameDataIdentity> LoadRepoGameDataIdentityAsync()
    {
        var path = FindRepoFile("artifacts", "poenhance-game-data.json");
        return await ModifierPipelineReplayGameDataGate.LoadIdentityAsync(path);
    }

    private static PathOfExileTradeStatCatalog LoadPinnedOfficialTradeCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        var result = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        Assert.True(result.IsSuccess);
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

    private const string WindscreamRaw = """
Item Class: Boots
Rarity: Unique
Windscream
Reinforced Greaves
--------
Quality: +20% (augmented)
Armour: 109 (augmented)
--------
Requirements:
Level: 33
Str: 60
--------
Sockets: R-R 
--------
Item Level: 55
--------
{ Unique Modifier }
You can apply an additional Curse
""";
}
