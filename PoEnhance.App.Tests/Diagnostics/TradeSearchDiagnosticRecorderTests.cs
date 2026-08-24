using System.Net;
using System.Text.Json;
using PoEnhance.App.Diagnostics;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Diagnostics;

[Collection(nameof(DiagnosticEnvironmentVariableCollection))]
public sealed class TradeSearchDiagnosticRecorderTests
{
    [Fact]
    public void BuildCapture_MapsSelectedComponentToSerializedStatFilter()
    {
        var component = PresenceComponent(
            "modifier:0:0",
            "Socketed Gems are Supported by Level 10 Spell Echo",
            "explicit.stat_spell_echo_generic",
            "Socketed Gems are Supported by Level # Spell Echo") with
        {
            ObservedNumericValues = [10m],
            CanonicalNumericValues = [10m],
            FixedQueryValue = 10m,
        };
        var draft = Draft([component with { IsSelected = true }]);
        var mapped = new PathOfExileTradeSelectedModifierFilter
        {
            SourceIndex = 0,
            SourceIndexes = [0],
            StatId = "explicit.stat_spell_echo_generic",
            OriginalText = component.OriginalText,
            Minimum = 10m,
            Maximum = 10m,
        };
        var request = Request(
            name: "Reverberation Rod",
            type: "Spiraled Wand",
            new PathOfExileTradeSearchStatsGroup
            {
                Type = "and",
                Filters =
                [
                    new PathOfExileTradeSearchStatFilter
                    {
                        Id = "explicit.stat_spell_echo_generic",
                        Value = new PathOfExileTradeSearchStatValue { Min = 10m, Max = 10m },
                    },
                ],
            });

        var capture = TradeSearchDiagnosticRecorder.BuildCapture(new TradeSearchDiagnosticInput
        {
            Draft = draft,
            LeagueIdentifier = "Standard",
            MappedModifierFilters = [mapped],
            BuildResult = PathOfExileTradeQueryBuildResult.Success(
                "Standard",
                request,
                PathOfExileTradeJson.SerializeSearchRequest(request),
                "Spiraled Wand",
                ItemBaseResolutionStatus.Exact),
            SearchResult = SuccessfulSearch("abc123", total: 4, resultIds: ["r1", "r2"]),
            Sequence = 1,
        });

        var provenance = Assert.Single(capture.Provenance);
        Assert.Equal("modifier:0:0", provenance.ComponentId);
        Assert.Equal(1, provenance.ProducedFilterCount);
        var produced = Assert.Single(provenance.ProducedFilters);
        Assert.Equal("explicit.stat_spell_echo_generic", produced.StatId);
        var serialized = Assert.Single(produced.SerializedFilters);
        Assert.Equal("explicit.stat_spell_echo_generic", serialized.Id);
        Assert.Equal(10m, serialized.Minimum);
        Assert.Equal(10m, serialized.Maximum);
    }

    [Fact]
    public void BuildCapture_StoresFinalRequestPayloadAndResponseSummary()
    {
        var component = ScalarComponent("modifier:1:0", "+2 to Level of Socketed Gems", "explicit.stat_gem_level", 2m);
        var draft = Draft([component with { IsSelected = true }]);
        var mapped = new PathOfExileTradeSelectedModifierFilter
        {
            SourceIndex = 0,
            SourceIndexes = [0],
            StatId = "explicit.stat_gem_level",
            OriginalText = component.OriginalText,
            Minimum = 2m,
            Maximum = null,
        };
        var request = Request(
            name: "Reverberation Rod",
            type: "Spiraled Wand",
            new PathOfExileTradeSearchStatsGroup
            {
                Filters =
                [
                    new PathOfExileTradeSearchStatFilter
                    {
                        Id = "explicit.stat_gem_level",
                        Value = new PathOfExileTradeSearchStatValue { Min = 2m },
                    },
                ],
            });
        var serialized = PathOfExileTradeJson.SerializeSearchRequest(request);

        var capture = TradeSearchDiagnosticRecorder.BuildCapture(new TradeSearchDiagnosticInput
        {
            Draft = draft,
            LeagueIdentifier = "Standard",
            MappedModifierFilters = [mapped],
            BuildResult = PathOfExileTradeQueryBuildResult.Success(
                "Standard",
                request,
                serialized,
                "Spiraled Wand",
                ItemBaseResolutionStatus.Exact),
            SearchResult = SuccessfulSearch("qid-9", total: 0, resultIds: []),
            Sequence = 2,
        });

        Assert.Equal(serialized, capture.FinalRequestJson);
        Assert.NotNull(capture.FinalRequest);
        Assert.Equal("/api/trade/search/Standard", capture.Context.EndpointPath);
        Assert.Equal("Reverberation Rod", capture.Context.ItemName);
        Assert.True(capture.Response.HttpSuccess);
        Assert.Equal("qid-9", capture.Response.SearchId);
        Assert.Equal(0, capture.Response.Total);
        Assert.Equal(0, capture.Response.ResultIdCount);
        Assert.Contains("explicit.stat_gem_level", capture.FinalRequestJson, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildCapture_SelectedRowWithZeroFilters_IsVisibleInProvenance()
    {
        var component = PresenceComponent(
            "modifier:2:0",
            "Base guaranteed placeholder",
            providerStatId: null,
            providerStatText: null) with
        {
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.BaseGuaranteed,
            IsSelected = true,
            IsSearchable = true,
        };
        var draft = Draft([component]);

        var capture = TradeSearchDiagnosticRecorder.BuildCapture(new TradeSearchDiagnosticInput
        {
            Draft = draft,
            LeagueIdentifier = "Standard",
            MappedModifierFilters = [],
            Sequence = 3,
            Stage = "ModifierMapping",
        });

        var provenance = Assert.Single(capture.Provenance);
        Assert.Equal("modifier:2:0", provenance.ComponentId);
        Assert.Equal(0, provenance.ProducedFilterCount);
        Assert.Empty(provenance.ProducedFilters);
        Assert.Contains("no Trade stat filter", provenance.ZeroFilterReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCapture_FixedQueryRow_SeparatesBlankEditableBoundsFromSerializedEquality()
    {
        var component = PresenceComponent(
            "modifier:3:0",
            "Socketed Gems are Supported by Level 10 Controlled Destruction",
            "explicit.stat_controlled_destruction_generic",
            "Socketed Gems are Supported by Level # Controlled Destruction") with
        {
            IsSelected = true,
            RequestedMinimum = null,
            RequestedMaximum = null,
            SupportsValueBounds = false,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = [10m],
            CanonicalNumericValues = [10m],
            FixedQueryValue = 10m,
        };
        var draft = Draft([component]);
        var mapped = new PathOfExileTradeSelectedModifierFilter
        {
            SourceIndex = 0,
            SourceIndexes = [0],
            StatId = "explicit.stat_controlled_destruction_generic",
            OriginalText = component.OriginalText,
            Minimum = 10m,
            Maximum = 10m,
        };
        var request = Request(
            name: "Reverberation Rod",
            type: "Spiraled Wand",
            new PathOfExileTradeSearchStatsGroup
            {
                Filters =
                [
                    new PathOfExileTradeSearchStatFilter
                    {
                        Id = "explicit.stat_controlled_destruction_generic",
                        Value = new PathOfExileTradeSearchStatValue { Min = 10m, Max = 10m },
                    },
                ],
            });

        var capture = TradeSearchDiagnosticRecorder.BuildCapture(new TradeSearchDiagnosticInput
        {
            Draft = draft,
            LeagueIdentifier = "Standard",
            MappedModifierFilters = [mapped],
            BuildResult = PathOfExileTradeQueryBuildResult.Success(
                "Standard",
                request,
                PathOfExileTradeJson.SerializeSearchRequest(request),
                "Spiraled Wand",
                ItemBaseResolutionStatus.Exact),
            SearchResult = SuccessfulSearch("qid-presence", total: 1, resultIds: ["r1"]),
            Sequence = 4,
        });

        var selected = Assert.Single(capture.SelectedInputs.Modifiers);
        Assert.False(selected.IsPresenceOnly);
        Assert.False(selected.SupportsEditableBounds);
        Assert.Null(selected.SelectedMinimum);
        Assert.Null(selected.SelectedMaximum);
        Assert.Equal(10m, selected.FixedQueryValue);

        var produced = Assert.Single(Assert.Single(capture.Provenance).ProducedFilters);
        Assert.Equal(10m, produced.Minimum);
        Assert.Equal(10m, produced.Maximum);
        var serialized = Assert.Single(produced.SerializedFilters);
        Assert.Equal(10m, serialized.Minimum);
        Assert.Equal(10m, serialized.Maximum);

        var group = Assert.Single(capture.FinalTradeFilters!.StatsGroups);
        var filter = Assert.Single(group.Filters);
        Assert.Equal(10m, filter.Minimum);
        Assert.Equal(10m, filter.Maximum);
        Assert.Contains("\"min\":10", capture.FinalRequestJson!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"max\":10", capture.FinalRequestJson!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCapture_WritesArtifactAndCompanionRequestPayload()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"PoEnhanceTradeSearchDiag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var previousDedicated = Environment.GetEnvironmentVariable(
            TradeSearchDiagnosticRecorder.EnvironmentVariableName);
        var previousPipeline = Environment.GetEnvironmentVariable(
            ModifierPipelineDiagnosticRecorder.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(TradeSearchDiagnosticRecorder.EnvironmentVariableName, outputDirectory);
        Environment.SetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName, null);
        try
        {
            var component = PresenceComponent(
                "modifier:0:0",
                "Socketed Gems are Supported by Level # Arcane Surge",
                "explicit.stat_arcane_surge",
                "Socketed Gems are Supported by Level # Arcane Surge") with
            {
                IsSelected = true,
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.ExactEquivalentSet,
                ProviderStatAlternativeIds = ["explicit.stat_arcane_surge_a", "explicit.stat_arcane_surge_b"],
            };
            var draft = Draft([component]);
            var mapped = new PathOfExileTradeSelectedModifierFilter
            {
                SourceIndex = 0,
                SourceIndexes = [0],
                StatId = "explicit.stat_arcane_surge_a",
                OriginalText = component.OriginalText,
                Alternatives =
                [
                    new PathOfExileTradeSelectedModifierFilterAlternative
                    {
                        StatId = "explicit.stat_arcane_surge_a",
                    },
                    new PathOfExileTradeSelectedModifierFilterAlternative
                    {
                        StatId = "explicit.stat_arcane_surge_b",
                    },
                ],
            };
            var request = Request(
                name: "Reverberation Rod",
                type: "Spiraled Wand",
                new PathOfExileTradeSearchStatsGroup
                {
                    Type = "count",
                    Value = new PathOfExileTradeSearchStatValue { Min = 1m },
                    Filters =
                    [
                        new PathOfExileTradeSearchStatFilter { Id = "explicit.stat_arcane_surge_a" },
                        new PathOfExileTradeSearchStatFilter { Id = "explicit.stat_arcane_surge_b" },
                    ],
                });

            TradeSearchDiagnosticRecorder.TryCapture(new TradeSearchDiagnosticInput
            {
                Draft = draft,
                LeagueIdentifier = "Standard",
                MappedModifierFilters = [mapped],
                BuildResult = PathOfExileTradeQueryBuildResult.Success(
                    "Standard",
                    request,
                    PathOfExileTradeJson.SerializeSearchRequest(request),
                    "Spiraled Wand",
                    ItemBaseResolutionStatus.Exact),
                SearchResult = new PathOfExileTradeSearchExecutionResult
                {
                    IsSuccess = false,
                    HttpStatusCode = HttpStatusCode.BadRequest,
                    ProviderError = new PathOfExileTradeProviderError
                    {
                        Code = "invalid_query",
                        Message = "Query rejected.",
                    },
                },
            });

            var artifacts = Directory.GetFiles(outputDirectory, "*-search-*.json")
                .Where(path => !path.EndsWith("-request.json", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var artifactPath = Assert.Single(artifacts);
            var requestPayloadPath = Assert.Single(Directory.GetFiles(outputDirectory, "*-request.json"));
            var json = File.ReadAllText(artifactPath);
            Assert.Contains("\"diagnosticVersion\": \"E6b-trade-search-1\"", json, StringComparison.Ordinal);
            Assert.Contains("provenance", json, StringComparison.Ordinal);
            Assert.Contains("finalRequestJson", json, StringComparison.Ordinal);
            Assert.Contains("invalid_query", json, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(requestPayloadPath)));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                TradeSearchDiagnosticRecorder.EnvironmentVariableName,
                previousDedicated);
            Environment.SetEnvironmentVariable(
                ModifierPipelineDiagnosticRecorder.EnvironmentVariableName,
                previousPipeline);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void TryCapture_UsesModifierPipelineDirectoryWhenDedicatedDirectoryIsMissing()
    {
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            $"PoEnhanceTradeSearchFallbackDiag-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outputDirectory);
        var previousDedicated = Environment.GetEnvironmentVariable(
            TradeSearchDiagnosticRecorder.EnvironmentVariableName);
        var previousPipeline = Environment.GetEnvironmentVariable(
            ModifierPipelineDiagnosticRecorder.EnvironmentVariableName);
        Environment.SetEnvironmentVariable(TradeSearchDiagnosticRecorder.EnvironmentVariableName, null);
        Environment.SetEnvironmentVariable(ModifierPipelineDiagnosticRecorder.EnvironmentVariableName, outputDirectory);
        try
        {
            TradeSearchDiagnosticRecorder.TryCapture(new TradeSearchDiagnosticInput
            {
                Stage = "FallbackOutputDirectory",
            });

            var artifactPath = Assert.Single(Directory.GetFiles(outputDirectory, "*-search-*.json"));
            var json = File.ReadAllText(artifactPath);
            Assert.Contains("\"diagnosticVersion\": \"E6b-trade-search-1\"", json, StringComparison.Ordinal);
            Assert.Contains("FallbackOutputDirectory", json, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                TradeSearchDiagnosticRecorder.EnvironmentVariableName,
                previousDedicated);
            Environment.SetEnvironmentVariable(
                ModifierPipelineDiagnosticRecorder.EnvironmentVariableName,
                previousPipeline);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }
    }

    private static TradeSearchDraft Draft(IReadOnlyList<ResolvedSearchComponent> modifiers) =>
        new()
        {
            DisplayName = "Reverberation Rod",
            ParsedBaseType = "Spiraled Wand",
            ItemClass = "Wands",
            Rarity = "Unique",
            ModifierFilters = modifiers.ToArray(),
        };

    private static ResolvedSearchComponent PresenceComponent(
        string componentId,
        string originalText,
        string? providerStatId,
        string? providerStatText) =>
        new()
        {
            ComponentId = componentId,
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            SourceComponentIndex = 0,
            OriginalText = originalText,
            CanonicalSignature = originalText,
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            UniqueCatalogBlockIds = ["unique-block:test"],
            UniqueSourceObservationIds = ["observation:test"],
            ResolvedStatIds = ["stat:test"],
            IsSearchable = true,
            SupportsValueBounds = false,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderStatId = providerStatId,
            ProviderStatText = providerStatText,
            ProviderStatAlternativeIds = providerStatId is null ? [] : [providerStatId],
        };

    private static ResolvedSearchComponent ScalarComponent(
        string componentId,
        string originalText,
        string providerStatId,
        decimal minimum) =>
        PresenceComponent(componentId, originalText, providerStatId, originalText) with
        {
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            RequestedMinimum = minimum,
            CanonicalNumericValues = [minimum],
            ObservedNumericValues = [minimum],
        };

    private static PathOfExileTradeSearchRequest Request(
        string name,
        string type,
        params PathOfExileTradeSearchStatsGroup[] groups) =>
        new()
        {
            Query = new PathOfExileTradeSearchQuery
            {
                Status = new PathOfExileTradeSearchStatus { Option = "securable" },
                Name = name,
                Type = type,
                Stats = groups,
            },
            Sort = new PathOfExileTradeSearchSort(),
        };

    private static PathOfExileTradeSearchExecutionResult SuccessfulSearch(
        string id,
        int total,
        IReadOnlyList<string> resultIds) =>
        new()
        {
            IsSuccess = true,
            HttpStatusCode = HttpStatusCode.OK,
            Response = new PathOfExileTradeSearchResponse
            {
                Id = id,
                Total = total,
                Result = resultIds,
            },
        };
}
