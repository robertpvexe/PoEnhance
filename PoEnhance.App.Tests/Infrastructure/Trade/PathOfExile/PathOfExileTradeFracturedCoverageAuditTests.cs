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
    private const int ReviewedFracturedFixtureItemCount = 17;

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

        var corpusCount = await AuditReviewedFracturedFixtureAsync(
            statResult.Catalog!,
            filterCatalog,
            gameDataCatalog);
        Assert.Equal(17, corpusCount);
    }

    [Fact]
    public async Task RuneKeep_MixedAesAggregate_RetainsFracturedSourceWithoutParentFracturedFlag()
    {
        var (statCatalog, filterCatalog, gameDataCatalog) = await LoadFrozenAuditCatalogsAsync();
        var runeKeep = await LoadCorpusItemAsync("Rune Keep");
        var draft = CreateResolvedDraft(
            runeKeep,
            statCatalog,
            filterCatalog,
            gameDataCatalog);

        Assert.Null(DescribeRuneKeepAesContractFailure(draft));
        var aes = Assert.Single(
            draft.ModifierFilters,
            component => component.OriginalText.Contains(
                "increased Armour and Energy Shield",
                StringComparison.Ordinal));
        Assert.Equal("132% increased Armour and Energy Shield", aes.OriginalText);
        Assert.False(aes.IsFractured);
        Assert.Equal(
            [true, false],
            aes.Sources
                .Where(source => source.OriginalText.Contains(
                    "increased Armour and Energy Shield",
                    StringComparison.Ordinal))
                .Select(source => source.IsFractured));
        Assert.Equal(
            [91m, 41m],
            aes.Sources
                .Where(source => source.OriginalText.Contains(
                    "increased Armour and Energy Shield",
                    StringComparison.Ordinal))
                .Select(source => Assert.Single(source.CanonicalNumericValues)));
        Assert.Contains(
            aes.Contributors,
            contributor => contributor.Source.IsFractured &&
                contributor.RequestedMinimum == 91m);
        Assert.Contains(
            aes.Contributors,
            contributor => !contributor.Source.IsFractured &&
                contributor.DisplayText.Contains(
                    "increased Armour and Energy Shield",
                    StringComparison.Ordinal) &&
                contributor.RequestedMinimum == 41m);
        // Official frozen stats expose Fractured/Explicit/Crafted AES Local only — no Pseudo —
        // so the parent cannot compose an aggregate Mod Type. Child-only exact sources must
        // still filter when the parent checkbox is unchecked.
        Assert.DoesNotContain(
            aes.FilterVariants,
            option => option.SupportsContributorComposition);
        Assert.False(SearchComponentContributorActivation.SupportsComposition(aes));
        Assert.True(SearchComponentContributorActivation.AllowsContributorFiltering(aes));

        AssertAesChildOnlyFilters(
            draft,
            aes,
            statCatalog,
            filterCatalog,
            selectFractured: true,
            selectExplicit: false,
            expectedStatIds: ["fractured.stat_3321629045"],
            expectedMins: [91m]);
        AssertAesChildOnlyFilters(
            draft,
            aes,
            statCatalog,
            filterCatalog,
            selectFractured: false,
            selectExplicit: true,
            expectedStatIds: ["explicit.stat_3321629045"],
            expectedMins: [41m]);
        AssertAesChildOnlyFilters(
            draft,
            aes,
            statCatalog,
            filterCatalog,
            selectFractured: true,
            selectExplicit: true,
            expectedStatIds: ["fractured.stat_3321629045", "explicit.stat_3321629045"],
            expectedMins: [91m, 41m]);
        AssertAesChildOnlyFilters(
            draft,
            aes,
            statCatalog,
            filterCatalog,
            selectFractured: false,
            selectExplicit: false,
            expectedStatIds: [],
            expectedMins: []);
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
        var corpusCount = await AuditReviewedFracturedFixtureAsync(
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

    private static async Task<int> AuditReviewedFracturedFixtureAsync(
        PathOfExileTradeStatCatalog statCatalog,
        PathOfExileTradeFilterCatalog filterCatalog,
        GameDataCatalog gameDataCatalog)
    {
        var fixturePath = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Items",
            "fractured-trade-coverage-reference-corpus.txt");
        var fixtureItems = Regex.Split(
                await File.ReadAllTextAsync(fixturePath),
                @"(?m)(?=^Item Class:)")
            .Where(block => block.TrimStart().StartsWith("Item Class:", StringComparison.Ordinal))
            .ToArray();
        Assert.Equal(ReviewedFracturedFixtureItemCount, fixtureItems.Length);
        var nonFracturedItems = fixtureItems
            .Select((block, index) => new { Block = block, Index = index })
            .Where(item => !item.Block.Contains("Fractured Item", StringComparison.Ordinal))
            .ToArray();
        Assert.True(
            nonFracturedItems.Length == 0,
            $"Reviewed Fractured fixture contains non-Fractured blocks at one-based indexes: " +
            string.Join(", ", nonFracturedItems.Select(item => item.Index + 1)));

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
        foreach (var (copiedText, fixtureIndex) in fixtureItems.Select((text, index) => (text, index)))
        {
            var parsed = new ItemTextParser().Parse(copiedText);
            var label = $"{parsed.DisplayName ?? parsed.BaseType} / {parsed.BaseType}";
            if (parsed.InputFormat != ParsedItemInputFormat.Advanced)
            {
                failures.Add(
                    $"Fixture item {fixtureIndex + 1} ({label}) did not parse as Advanced Item Description text.");
                continue;
            }

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

            // Physical Fractured provenance lives on Sources/Contributors. Mixed-domain aggregates
            // intentionally keep parent.IsFractured=false; requiring the parent flag reintroduced
            // Fractured+aggregate Trade routing (e.g. Rune Keep 132% / Life +78).
            var fracturedSources = effective.ModifierFilters
                .SelectMany(component => component.Sources)
                .Where(source => source.IsFractured)
                .ToArray();
            if (fracturedSources.Length == 0)
            {
                failures.Add($"{label}: no Fractured source provenance was retained.");
                continue;
            }

            if (IsRuneKeepSaintlyChainmail(label))
            {
                var runeKeepFailure = DescribeRuneKeepAesContractFailure(effective);
                if (runeKeepFailure is not null)
                {
                    failures.Add(runeKeepFailure);
                    continue;
                }
            }

            effective = EnsureSearchableFracturedParents(effective, service, statCatalog, filterCatalog);
            var selection = TrySelectFracturedSearchTargets(effective, label);
            if (!selection.IsSuccess)
            {
                failures.Add(selection.Failure!);
                continue;
            }

            effective = selection.Draft!;
            var lockedParents = effective.ModifierFilters
                .Where(component => component.IsFractured && component.IsSelected && !IsUsable(component))
                .ToArray();
            if (lockedParents.Length > 0)
            {
                failures.AddRange(lockedParents.Select(component =>
                    $"{label}: {component.OriginalText}: " +
                    (component.ProviderDiagnosticMessage ?? component.NotSearchableReason)));
                continue;
            }

            var lockedContributors = effective.ModifierFilters
                .SelectMany(component => component.Contributors
                    .Where(contributor => contributor.IsSelected && contributor.Source.IsFractured)
                    .Select(contributor => (component, contributor)))
                .Select(entry =>
                {
                    var hasExactIdentity =
                        entry.contributor.ProviderResolutionStatus ==
                            SearchComponentProviderResolutionStatus.Exact &&
                        !string.IsNullOrWhiteSpace(entry.contributor.ProviderIdentity) &&
                        statCatalog.TryGetByProviderIdentity(
                            entry.contributor.ProviderIdentity,
                            out var providerEntry) &&
                        string.Equals(
                            providerEntry.Type,
                            "fractured",
                            StringComparison.OrdinalIgnoreCase);
                    return (entry.contributor, hasExactIdentity, Identity: entry.contributor.ProviderIdentity);
                })
                .Where(entry => !entry.hasExactIdentity)
                .ToArray();
            if (lockedContributors.Length > 0)
            {
                failures.AddRange(lockedContributors.Select(entry =>
                    $"{label}: Fractured contributor '{entry.contributor.DisplayText}' is not Exact fractured: " +
                    $"status={entry.contributor.ProviderResolutionStatus}, " +
                    $"identity={entry.Identity ?? "<none>"}"));
                continue;
            }

            var requestsBefore = searchClient.Requests.Count;
            var result = await service.CheckAsync(
                effective,
                new TradeSearchDraftValidator().Validate(effective),
                "Mirage");
            if (!result.IsSuccess)
            {
                failures.Add($"{label}: final query failed: " +
                    string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Message)));
                continue;
            }

            if (IsRuneKeepSaintlyChainmail(label))
            {
                var queryFailure = DescribeRuneKeepChildOnlyQueryFailure(
                    searchClient.Requests.Skip(requestsBefore).ToArray());
                if (queryFailure is not null)
                {
                    failures.Add(queryFailure);
                }
            }
        }

        Console.WriteLine(
            $"Reviewed Fractured fixture: {searchClient.Requests.Count}/{fixtureItems.Length} full items reached final query JSON.");
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
        Assert.Equal(fixtureItems.Length, searchClient.Requests.Count);
        return fixtureItems.Length;
    }

    private static bool IsUsable(ResolvedSearchComponent component) =>
        component.ProviderResolutionStatus is
            SearchComponentProviderResolutionStatus.Exact or
            SearchComponentProviderResolutionStatus.ExactEquivalentSet or
            SearchComponentProviderResolutionStatus.Approximate;

    private static bool IsRuneKeepSaintlyChainmail(string label) =>
        label.StartsWith("Rune Keep /", StringComparison.Ordinal);

    private static string? DescribeRuneKeepAesContractFailure(TradeSearchDraft draft)
    {
        var aes = draft.ModifierFilters.FirstOrDefault(component =>
            component.OriginalText.Contains(
                "increased Armour and Energy Shield",
                StringComparison.Ordinal));
        if (aes is null)
        {
            return "Rune Keep / Saintly Chainmail: missing Armour and Energy Shield aggregate parent.";
        }

        if (!string.Equals(
                aes.OriginalText,
                "132% increased Armour and Energy Shield",
                StringComparison.Ordinal))
        {
            return "Rune Keep / Saintly Chainmail: expected aggregate parent " +
                $"'132% increased Armour and Energy Shield', got '{aes.OriginalText}'.";
        }

        if (aes.IsFractured)
        {
            return "Rune Keep / Saintly Chainmail: mixed AES aggregate parent must not inherit IsFractured.";
        }

        if (aes.Sources.Count < 2 ||
            !aes.Sources.Any(source => source.IsFractured) ||
            !aes.Sources.Any(source => !source.IsFractured))
        {
            return "Rune Keep / Saintly Chainmail: expected mixed Fractured+Explicit AES Sources.";
        }

        var fractured = aes.Sources.SingleOrDefault(source => source.IsFractured);
        var explicitSource = aes.Sources.SingleOrDefault(source =>
            !source.IsFractured &&
            source.OriginalText.Contains(
                "increased Armour and Energy Shield",
                StringComparison.Ordinal));
        if (fractured is null ||
            fractured.CanonicalNumericValues is not [91m] ||
            explicitSource is null ||
            explicitSource.CanonicalNumericValues is not [41m])
        {
            return "Rune Keep / Saintly Chainmail: expected Fractured +91 and Explicit +41 AES Sources; " +
                $"got fractured=[{string.Join(',', fractured?.CanonicalNumericValues ?? [])}] " +
                $"explicit=[{string.Join(',', explicitSource?.CanonicalNumericValues ?? [])}].";
        }

        return null;
    }

    private static void AssertAesChildOnlyFilters(
        TradeSearchDraft draft,
        ResolvedSearchComponent aes,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeFilterCatalog filterCatalog,
        bool selectFractured,
        bool selectExplicit,
        IReadOnlyList<string> expectedStatIds,
        IReadOnlyList<decimal> expectedMins)
    {
        var selected = draft with
        {
            ModifierFilters = draft.ModifierFilters
                .Select(component =>
                {
                    if (component.ComponentId != aes.ComponentId)
                    {
                        return component with
                        {
                            IsSelected = false,
                            Contributors = component.Contributors
                                .Select(contributor => contributor with { IsSelected = false })
                                .ToArray(),
                        };
                    }

                    return component with
                    {
                        IsSelected = false,
                        Contributors = component.Contributors
                            .Select(contributor =>
                            {
                                var isAes = contributor.DisplayText.Contains(
                                    "increased Armour and Energy Shield",
                                    StringComparison.Ordinal);
                                var isSelected = isAes &&
                                    ((selectFractured && contributor.Source.IsFractured) ||
                                        (selectExplicit && !contributor.Source.IsFractured));
                                return contributor with { IsSelected = isSelected };
                            })
                            .ToArray(),
                    };
                })
                .ToArray(),
        };
        var selectedAes = Assert.Single(
            selected.ModifierFilters,
            component => component.ComponentId == aes.ComponentId);
        Assert.False(selectedAes.IsSelected);
        Assert.Equal(
            selectFractured || selectExplicit,
            SearchComponentContributorActivation.IsFilteringActive(selectedAes));

        var mapping = new PathOfExileTradeSelectedModifierMapper().Map(selected, catalog);
        Assert.True(
            mapping.IsSuccess,
            string.Join(" | ", mapping.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}")));
        Assert.Equal(expectedStatIds, mapping.Filters.Select(filter => filter.StatId));
        Assert.Equal(expectedMins, mapping.Filters.Select(filter => filter.Minimum ?? 0m));
        Assert.DoesNotContain(
            mapping.Filters,
            filter => filter.Minimum == 132m);

        if (expectedStatIds.Count == 0)
        {
            return;
        }

        var validation = new TradeSearchDraftValidator().Validate(selected);
        var build = new PathOfExileTradeQueryBuilder().Build(
            selected,
            validation,
            "Mirage",
            mapping.Filters,
            providerItemIdentity: null,
            providerFilterCatalog: filterCatalog);
        Assert.True(
            build.IsSuccess,
            string.Join(" | ", build.Diagnostics.Select(diagnostic =>
                $"{diagnostic.Code}: {diagnostic.Message}")));
        using var document = JsonDocument.Parse(build.SerializedJson!);
        var filters = document.RootElement
            .GetProperty("query")
            .GetProperty("stats")[0]
            .GetProperty("filters")
            .EnumerateArray()
            .ToArray();
        Assert.Equal(expectedStatIds, filters.Select(filter => filter.GetProperty("id").GetString()));
        Assert.Equal(
            expectedMins,
            filters.Select(filter => filter.GetProperty("value").GetProperty("min").GetDecimal()));
        Assert.All(filters, filter => Assert.False(filter.GetProperty("value").TryGetProperty("max", out _)));
    }

    private static string? DescribeRuneKeepChildOnlyQueryFailure(
        IReadOnlyList<PathOfExileTradeSearchRequest> requests)
    {
        if (requests.Count != 1)
        {
            return $"Rune Keep / Saintly Chainmail: expected one child-only Trade request, got {requests.Count}.";
        }

        using var document = JsonDocument.Parse(
            PathOfExileTradeJson.SerializeSearchRequest(requests[0]));
        var filters = document.RootElement
            .GetProperty("query")
            .GetProperty("stats")
            .EnumerateArray()
            .SelectMany(group => group.GetProperty("filters").EnumerateArray())
            .ToArray();
        var fracturedFilters = filters
            .Where(filter => (filter.GetProperty("id").GetString() ?? string.Empty)
                .StartsWith("fractured.", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (fracturedFilters.Length != 1 || filters.Length != 1)
        {
            return "Rune Keep / Saintly Chainmail: child-only query must emit exactly one Fractured filter; " +
                $"got ids=[{string.Join(", ", filters.Select(filter => filter.GetProperty("id").GetString()))}].";
        }

        var fractured = fracturedFilters[0];
        var id = fractured.GetProperty("id").GetString();
        if (!string.Equals(id, "fractured.stat_3321629045", StringComparison.Ordinal))
        {
            return "Rune Keep / Saintly Chainmail: expected fractured.stat_3321629045 for AES, " +
                $"got '{id}'.";
        }

        if (!fractured.TryGetProperty("value", out var value) ||
            !value.TryGetProperty("min", out var min) ||
            min.GetDecimal() != 91m ||
            value.TryGetProperty("max", out _))
        {
            var rendered = fractured.TryGetProperty("value", out var renderedValue)
                ? renderedValue.ToString()
                : "<missing>";
            return "Rune Keep / Saintly Chainmail: expected Fractured AES min=91 with no max; " +
                $"got value={rendered}.";
        }

        return null;
    }

    private static TradeSearchDraft EnsureSearchableFracturedParents(
        TradeSearchDraft draft,
        PathOfExileTradePriceCheckService service,
        PathOfExileTradeStatCatalog statCatalog,
        PathOfExileTradeFilterCatalog filterCatalog)
    {
        var effective = draft;
        foreach (var component in effective.ModifierFilters.Where(candidate =>
                     candidate.IsFractured && !IsUsable(candidate)))
        {
            var alternative = component.FilterVariants.FirstOrDefault();
            if (alternative is null)
            {
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

        return effective;
    }

    private static FracturedSelectionResult TrySelectFracturedSearchTargets(
        TradeSearchDraft draft,
        string label)
    {
        var modifiers = draft.ModifierFilters.ToArray();
        for (var index = 0; index < modifiers.Length; index++)
        {
            var component = modifiers[index];
            var fracturedSources = component.Sources.Where(source => source.IsFractured).ToArray();
            if (fracturedSources.Length == 0)
            {
                modifiers[index] = component with
                {
                    IsSelected = false,
                    Contributors = component.Contributors
                        .Select(contributor => contributor with { IsSelected = false })
                        .ToArray(),
                };
                continue;
            }

            if (component.IsFractured)
            {
                // Unanimous Fractured parent remains the faithful Trade row.
                modifiers[index] = component with
                {
                    IsSelected = true,
                    Contributors = component.Contributors
                        .Select(contributor => contributor with { IsSelected = false })
                        .ToArray(),
                };
                continue;
            }

            // Mixed-domain aggregate: parent stays unchecked; only Fractured contributors search.
            // Child-only exact source filters do not require a composition-capable parent Mod Type.
            modifiers[index] = component with
            {
                IsSelected = false,
                Contributors = component.Contributors
                    .Select(contributor => contributor with
                    {
                        IsSelected = contributor.Source.IsFractured,
                    })
                    .ToArray(),
            };

            if (!SearchComponentContributorActivation.AllowsContributorFiltering(modifiers[index]) ||
                !SearchComponentContributorActivation.IsFilteringActive(modifiers[index]))
            {
                return FracturedSelectionResult.Failed(
                    $"{label}: mixed-domain aggregate '{component.OriginalText}' selected Fractured " +
                    "contributor(s) with parent unchecked, but independent contributor filtering " +
                    "did not activate.");
            }
        }

        if (!modifiers.Any(component =>
                component.IsSelected ||
                SearchComponentContributorActivation.IsFilteringActive(component)))
        {
            return FracturedSelectionResult.Failed(
                $"{label}: no Fractured parent or Fractured contributor search target was activated.");
        }

        return FracturedSelectionResult.Succeeded(draft with { ModifierFilters = modifiers });
    }

    private readonly record struct FracturedSelectionResult(
        bool IsSuccess,
        TradeSearchDraft? Draft,
        string? Failure)
    {
        public static FracturedSelectionResult Succeeded(TradeSearchDraft draft) =>
            new(true, draft, null);

        public static FracturedSelectionResult Failed(string failure) =>
            new(false, null, failure);
    }

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

    private static async Task<(
        PathOfExileTradeStatCatalog StatCatalog,
        PathOfExileTradeFilterCatalog FilterCatalog,
        GameDataCatalog GameDataCatalog)> LoadFrozenAuditCatalogsAsync()
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
        return (
            statResult.Catalog!,
            FrozenFracturedCorpusFilterCatalog(),
            GameDataCatalog.FromPackage(packageResult.Package!));
    }

    private static async Task<string> LoadCorpusItemAsync(string displayName)
    {
        var fixturePath = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Items",
            "fractured-trade-coverage-reference-corpus.txt");
        var item = Regex.Split(
                await File.ReadAllTextAsync(fixturePath),
                @"(?m)(?=^Item Class:)")
            .FirstOrDefault(block =>
                block.TrimStart().StartsWith("Item Class:", StringComparison.Ordinal) &&
                block.Contains(displayName, StringComparison.Ordinal));
        Assert.False(string.IsNullOrWhiteSpace(item), $"Corpus item '{displayName}' was not found.");
        return item!;
    }

    private static TradeSearchDraft CreateResolvedDraft(
        string copiedText,
        PathOfExileTradeStatCatalog statCatalog,
        PathOfExileTradeFilterCatalog filterCatalog,
        GameDataCatalog gameDataCatalog)
    {
        var parsed = new ItemTextParser().Parse(copiedText);
        Assert.Equal(ParsedItemInputFormat.Advanced, parsed.InputFormat);
        var displayService = new ParsedItemGameDataDisplayService();
        var baseResolution = Assert.IsType<ItemBaseResolutionResult>(
            displayService.ResolveItemBase(parsed, gameDataCatalog).Result);
        var modifierResolutions = displayService
            .ResolveModifierCandidates(parsed, gameDataCatalog, baseResolution)
            .Results
            .Select(result => result.Result)
            .OfType<ModifierCandidateResolutionResult>()
            .ToArray();
        var draftResult = new TradeSearchDraftMapper().CreateDraft(
            parsed,
            baseResolution,
            modifierResolutions,
            gameDataCatalog);
        Assert.True(draftResult.IsSuccess && draftResult.Draft is not null);
        var service = new PathOfExileTradePriceCheckService(
            new PathOfExileTradeQueryBuilder(),
            new PathOfExileTradeStatMatcher(),
            new StaticStatCatalogProvider(statCatalog),
            new ThrowingItemCatalogProvider(),
            new PathOfExileTradeSelectedModifierMapper(),
            new ThrowingItemIdentityMapper(),
            new CorpusRecordingSearchClient(),
            new ThrowingFetchClient(),
            new StaticFilterCatalogProvider(filterCatalog));
        return service.ResolveProviderComponents(
            draftResult.Draft!,
            statCatalog,
            filterCatalog: filterCatalog);
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
