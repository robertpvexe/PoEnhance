using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PoEnhance.App.Infrastructure.GameData;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeFracturedCoverageAuditTests
{
    [Fact]
    public async Task FrozenOfficialStats_ConfirmedCurrentCoverageAndAppendedCorpusHaveNoLockedModifier()
    {
        var statResult = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(
            await File.ReadAllTextAsync(FindRepoFile(
                "artifacts",
                "audits",
                "official-trade-stats-2026-07-24.json")));
        var packageResult = await GameDataPackageLoader.LoadFromFileAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        Assert.True(
            statResult.IsSuccess && statResult.Catalog is not null,
            string.Join(", ", statResult.Diagnostics.Select(diagnostic => diagnostic.Code)));
        Assert.True(packageResult.IsSuccess && packageResult.Package is not null);

        var filterCatalog = FrozenFracturedCorpusFilterCatalog();
        var gameDataCatalog = GameDataCatalog.FromPackage(packageResult.Package!);
        var report = PathOfExileTradeFracturedCoverageAuditor.Audit(
            statResult.Catalog!,
            filterCatalog,
            gameDataCatalog,
            packageResult.Package!.Manifest.DataVersion);
        WriteCoverageReport(report);

        Assert.True(report.TotalPackagedModifierRecordsConsidered > 0);
        Assert.True(report.ConfirmedCurrentFracturedRecords > 0);
        Assert.True(report.CanonicalFamilies > 0);
        Assert.Equal(0, report.CompletelyLockedKnown);
        Assert.True(report.BeforeCompletelyLockedKnown > report.CompletelyLockedKnown);

        var excludedSignatures = report.ExcludedRecords
            .Where(record => string.Equals(
                record.Reason,
                "UnsupportedNonMvpItemClass",
                StringComparison.Ordinal))
            .Select(record => record.CanonicalSignature)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Subset(
            excludedSignatures,
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Ezomyte Shell Hook",
                "Vaal Soul Hook",
                "Eternal Iron Hook",
                "Siren Worm Bait",
                "Totemic Wood Lure",
            });
        Assert.All(
            report.ExcludedRecords.Where(record =>
                excludedSignatures.Contains(record.CanonicalSignature)),
            record => Assert.Equal("FishingRod", record.ItemClass));

        var productionAuditSource = await File.ReadAllTextAsync(FindRepoFile(
            "PoEnhance.App",
            "Infrastructure",
            "Trade",
            "PathOfExile",
            "PathOfExileTradeFracturedCoverageAudit.cs"));
        Assert.False(
            productionAuditSource.Contains("Fishing", StringComparison.OrdinalIgnoreCase),
            "Coverage scope must be derived from the reviewed canonical item-class contract.");

        var corpusCount = await AuditAppendedFracturedCorpusAsync(
            statResult.Catalog!,
            filterCatalog,
            gameDataCatalog);
        Assert.Equal(17, corpusCount);
    }

    [Fact]
    public async Task OfficialCatalog_OptInConfirmedCurrentFracturedCoverageHasNoCompletelyLockedKnownModifier()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("POENHANCE_RUN_LIVE_FRACTURED_COVERAGE"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "PoEnhance-development-fractured-coverage-audit");
        var statResult = await new PathOfExileTradeStatCatalogProvider(
                new PathOfExileTradeStatsClient(httpClient))
            .GetCatalogAsync();
        var filterResult = await new PathOfExileTradeFilterCatalogProvider(
                new PathOfExileTradeFiltersClient(httpClient))
            .GetCatalogAsync();
        var packageResult = await GameDataPackageLoader.LoadFromFileAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        Assert.True(
            statResult.IsSuccess && statResult.Catalog is not null,
            string.Join(", ", statResult.Diagnostics.Select(diagnostic => diagnostic.Code)));
        Assert.True(
            filterResult.IsSuccess && filterResult.Catalog is not null,
            string.Join(", ", filterResult.Diagnostics.Select(diagnostic => diagnostic.Code)));
        Assert.True(packageResult.IsSuccess && packageResult.Package is not null);

        var gameDataCatalog = GameDataCatalog.FromPackage(packageResult.Package!);
        var report = PathOfExileTradeFracturedCoverageAuditor.Audit(
            statResult.Catalog!,
            filterResult.Catalog!,
            gameDataCatalog,
            packageResult.Package!.Manifest.DataVersion);
        var reportPath = Path.Combine(
            FindRepoDirectory(),
            "artifacts",
            "audits",
            "fractured-modifier-coverage-current.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        }));
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            report.PackagedDataVersion,
            report.TotalPackagedModifierRecordsConsidered,
            report.ConfirmedCurrentFracturedRecords,
            report.CanonicalFamilies,
            report.ExactSingle,
            report.ExactEquivalentSet,
            report.GuardedApproximate,
            report.SafeAlternativeOnly,
            report.UnknownOrNew,
            report.CompletelyLockedKnown,
            report.BeforeExactSingle,
            report.BeforeExactEquivalentSet,
            report.BeforeGuardedApproximate,
            report.BeforeSafeAlternativeOnly,
            report.BeforeUnknownOrNew,
            report.BeforeCompletelyLockedKnown,
            report.ExcludedHistoricalOrNonFracturable,
            report.AmbiguousOrUnresolvedByReason,
            report.ProviderAbsentCurrentBlockers,
            ReportPath = reportPath,
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        }));

        Assert.True(report.ConfirmedCurrentFracturedRecords > 0);
        Assert.True(report.CanonicalFamilies > 0);
        var corpusCount = await AuditAppendedFracturedCorpusAsync(
            statResult.Catalog!,
            filterResult.Catalog!,
            gameDataCatalog);
        Assert.Equal(17, corpusCount);
        Assert.Equal(0, report.CompletelyLockedKnown);
    }

    private static void WriteCoverageReport(PathOfExileTradeFracturedCoverageReport report)
    {
        var reportPath = Path.Combine(
            FindRepoDirectory(),
            "artifacts",
            "audits",
            "fractured-modifier-coverage-current.json");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        }));
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            report.PackagedDataVersion,
            report.TotalPackagedModifierRecordsConsidered,
            report.ConfirmedCurrentFracturedRecords,
            report.CanonicalFamilies,
            report.ExactSingle,
            report.ExactEquivalentSet,
            report.GuardedApproximate,
            report.SafeAlternativeOnly,
            report.UnknownOrNew,
            report.CompletelyLockedKnown,
            report.BeforeExactSingle,
            report.BeforeExactEquivalentSet,
            report.BeforeGuardedApproximate,
            report.BeforeSafeAlternativeOnly,
            report.BeforeUnknownOrNew,
            report.BeforeCompletelyLockedKnown,
            report.ExcludedHistoricalOrNonFracturable,
            report.AmbiguousOrUnresolvedByReason,
            ReportPath = reportPath,
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() },
        }));
    }

    private static async Task<int> AuditAppendedFracturedCorpusAsync(
        PathOfExileTradeStatCatalog statCatalog,
        PathOfExileTradeFilterCatalog filterCatalog,
        GameDataCatalog gameDataCatalog)
    {
        var corpusPath = Path.Combine(
            Directory.GetParent(FindRepoDirectory())!.FullName,
            "zfortests_v2.txt");
        Assert.True(File.Exists(corpusPath), $"Fractured corpus was not found at '{corpusPath}'.");
        var blocks = Regex.Split(
                await File.ReadAllTextAsync(corpusPath),
                @"(?m)(?=^Item Class:)")
            .Where(block => block.TrimStart().StartsWith("Item Class:", StringComparison.Ordinal))
            .ToArray();
        var appendedStart = Array.FindLastIndex(
            blocks,
            block => !block.Contains("Fractured Item", StringComparison.Ordinal)) + 1;
        var appended = blocks
            .Skip(appendedStart)
            .Where(block => block.Contains("Fractured Item", StringComparison.Ordinal))
            .ToArray();
        var searchClient = new CorpusRecordingSearchClient();
        var service = new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatCatalogProvider(statCatalog),
            new ThrowingItemCatalogProvider(),
            new PathOfExileTradeSelectedModifierMapper(),
            new ThrowingItemIdentityMapper(),
            searchClient,
            new ThrowingFetchClient(),
            new StaticFilterCatalogProvider(filterCatalog));
        var displayService = new ParsedItemGameDataDisplayService();
        var failures = new List<string>();
        foreach (var copiedText in appended)
        {
            var parsed = new ItemTextParser().Parse(copiedText);
            var label = $"{parsed.DisplayName ?? parsed.BaseType} / {parsed.BaseType}";
            var baseResolution = Assert.IsType<ItemBaseResolutionResult>(
                displayService.ResolveItemBase(parsed, gameDataCatalog).Result);
            var modifierResolutions = displayService
                .ResolveModifierCandidates(parsed, gameDataCatalog, baseResolution)
                .Results
                .Select(result => result.Result)
                .OfType<ModifierCandidateResolutionResult>()
                .ToArray();
            var unresolvedSources = modifierResolutions
                .Where(result => result.ParsedModifier.IsFractured &&
                    result.Status != ModifierCandidateResolutionStatus.Exact)
                .ToArray();
            if (unresolvedSources.Length > 0)
            {
                failures.AddRange(unresolvedSources.Select(resolution =>
                    $"{label}: source '{resolution.ParsedModifier.Name}' unresolved: " +
                    string.Join(", ", resolution.Diagnostics.Select(diagnostic =>
                        $"{diagnostic.Code}={diagnostic.Reason}")) +
                    $"; candidates={string.Join(",", resolution.Candidates.Select(candidate => candidate.Id))}"));
                continue;
            }

            var draftResult = new TradeSearchDraftMapper().CreateDraft(
                parsed,
                baseResolution,
                modifierResolutions,
                gameDataCatalog);
            if (!draftResult.IsSuccess || draftResult.Draft is null)
            {
                failures.Add($"{label}: draft mapping failed: " +
                    string.Join(", ", draftResult.Diagnostics.Select(diagnostic => diagnostic.Message)));
                continue;
            }

            var effective = service.ResolveProviderComponents(
                draftResult.Draft,
                statCatalog,
                filterCatalog: filterCatalog);
            var fractured = effective.ModifierFilters.Where(component => component.IsFractured).ToArray();
            if (fractured.Length == 0)
            {
                failures.Add($"{label}: no Fractured source component was retained.");
                continue;
            }

            foreach (var component in fractured.Where(component => !IsUsable(component)))
            {
                var alternative = component.FilterVariants.FirstOrDefault();
                if (alternative is null)
                {
                    failures.Add($"{label}: {component.OriginalText}: " +
                        (component.ProviderDiagnosticMessage ?? component.NotSearchableReason));
                    continue;
                }

                effective = service.ResolveProviderComponents(
                    effective with
                    {
                        ModifierFilters = effective.ModifierFilters
                            .Select(current => current.ComponentId == component.ComponentId
                                ? current with
                                {
                                    RequestedFilterVariantIdentity = alternative.Identity,
                                    RequestedFilterVariantKind = alternative.ProviderKind,
                                }
                                : current)
                            .ToArray(),
                    },
                    statCatalog,
                    filterCatalog: filterCatalog);
            }

            fractured = effective.ModifierFilters.Where(component => component.IsFractured).ToArray();
            var locked = fractured.Where(component => !IsUsable(component)).ToArray();
            if (locked.Length > 0)
            {
                failures.AddRange(locked.Select(component =>
                    $"{label}: {component.OriginalText}: " +
                    (component.ProviderDiagnosticMessage ?? component.NotSearchableReason)));
                continue;
            }

            var selectedIds = fractured.Select(component => component.ComponentId)
                .ToHashSet(StringComparer.Ordinal);
            var selected = effective with
            {
                ModifierFilters = effective.ModifierFilters
                    .Select(component => component with
                    {
                        IsSelected = selectedIds.Contains(component.ComponentId),
                    })
                    .ToArray(),
            };
            var result = await service.CheckAsync(
                selected,
                new TradeSearchDraftValidator().Validate(selected),
                "Mirage");
            if (!result.IsSuccess)
            {
                failures.Add($"{label}: final query failed: " +
                    string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
            }
        }

        Console.WriteLine(
            $"Appended Fractured corpus: {searchClient.Requests.Count}/{appended.Length} full items reached final query JSON.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.Equal(appended.Length, searchClient.Requests.Count);
        return appended.Length;
    }

    private static bool IsUsable(ResolvedSearchComponent component) =>
        component.ProviderResolutionStatus is
            SearchComponentProviderResolutionStatus.Exact or
            SearchComponentProviderResolutionStatus.ExactEquivalentSet or
            SearchComponentProviderResolutionStatus.Approximate;

    private static PathOfExileTradeFilterCatalog FrozenFracturedCorpusFilterCatalog()
    {
        var categories = new (string Id, string Text)[]
        {
            ("armour.shield", "Shield"),
            ("armour.boots", "Boots"),
            ("accessory.ring", "Ring"),
            ("armour.gloves", "Gloves"),
            ("jewel.base", "Jewel"),
            ("weapon.wand", "Wand"),
            ("armour.helmet", "Helmet"),
            ("armour.chest", "Body Armour"),
            ("accessory.belt", "Belt"),
            ("accessory.amulet", "Amulet"),
        };
        return new PathOfExileTradeFilterCatalog(
            categories.Select((category, index) => new PathOfExileTradeFilterOption
            {
                ProviderOrder = index,
                GroupId = "type_filters",
                FilterId = "category",
                Id = category.Id,
                Text = category.Text,
            }),
            optionFilterDefinitions:
            [
                BooleanStateDefinition(0, "fractured_item", "Fractured Item"),
                BooleanStateDefinition(1, "mirrored", "Mirrored"),
                BooleanStateDefinition(2, "corrupted", "Corrupted"),
                BooleanStateDefinition(3, "identified", "Identified"),
            ]);
    }

    private static PathOfExileTradeOptionFilterDefinition BooleanStateDefinition(
        int providerOrder,
        string filterId,
        string text) =>
        new()
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

    private static string FindRepoDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the PoEnhance repository root.");
    }

    private sealed class CorpusRecordingSearchClient : IPathOfExileTradeSearchClient
    {
        public List<PathOfExileTradeSearchRequest> Requests { get; } = [];

        public Task<PathOfExileTradeSearchExecutionResult> SearchAsync(
            PathOfExileTradeSearchRequest? request,
            string? leagueIdentifier,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(Assert.IsType<PathOfExileTradeSearchRequest>(request));
            return Task.FromResult(new PathOfExileTradeSearchExecutionResult
            {
                IsSuccess = true,
                Response = new PathOfExileTradeSearchResponse
                {
                    Id = $"corpus-{Requests.Count}",
                    Result = [],
                    Total = 0,
                },
            });
        }
    }
}
