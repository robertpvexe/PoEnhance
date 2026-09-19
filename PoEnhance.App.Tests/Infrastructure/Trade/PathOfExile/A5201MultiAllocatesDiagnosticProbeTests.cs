using System.Text.Json;
using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class A5201MultiAllocatesDiagnosticProbeTests
{
    private const string League = "Standard";
    private readonly PathOfExileTradeSelectedModifierMapper mapper = new();
    private readonly PathOfExileTradeQueryBuilder builder = new();

    [Fact]
    public void Probe_N1_And_N2_MultiAllocates_MapperAndQuery()
    {
        var catalog = LoadOfficial();
        var n1 = mapper.Map(Draft([Anoint("Heartseeker", 65502, 0)]), catalog);
        Assert.True(n1.IsSuccess, string.Join(";", n1.Diagnostics.Select(d => d.Code)));
        Assert.Single(n1.Filters);
        Assert.Equal("65502", n1.Filters[0].Option);

        var n2 = mapper.Map(Draft([
            Anoint("Heartseeker", 65502, 0),
            Anoint("Lethality", 41119, 1),
        ]), catalog);

        Assert.True(n2.IsSuccess, string.Join(";", n2.Diagnostics.Select(d => d.Code)));
        Assert.Equal(2, n2.Filters.Count);
        Assert.Equal(
            ["65502", "41119"],
            n2.Filters.Select(filter => filter.Option).ToArray());
        Assert.All(n2.Filters, filter => Assert.Equal(n2.Filters[0].StatId, filter.StatId));

        WriteJson("probe-mapper-n1-n2.json", new
        {
            n1Success = n1.IsSuccess,
            n1Filters = n1.Filters.Select(Describe),
            n2Success = n2.IsSuccess,
            n2Diagnostics = n2.Diagnostics.Select(d => new { d.Code, d.Message, d.SourceIndex }),
            n2Filters = n2.Filters.Select(Describe),
            earliestLossyLayerWas =
                "PathOfExileTradeSelectedModifierMapper.CollapseSharedPresenceFilters",
            fix =
                "Collapse key is now StatId+Option(+bounds); differing options remain distinct filters",
        });
    }

    [Fact]
    public void Probe_QueryBuilder_AcceptsMultipleSameStatDifferentOptions_IfMapperPreservedThem()
    {
        var draft = Draft([
            Anoint("Heartseeker", 65502, 0),
            Anoint("Lethality", 41119, 1),
        ]);
        var filters = new[]
        {
            Filter(0, "enchant.stat_2954116742", "65502", "Allocates Heartseeker"),
            Filter(1, "enchant.stat_2954116742", "41119", "Allocates Lethality"),
        };
        var built = builder.Build(
            draft,
            TradeSearchValidationResult.FromDiagnostics([]),
            League,
            filters,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Cowl of the Ceraunophile",
                CanonicalType = "Solaris Circlet",
                Foulborn = TradeTriState.No,
            });

        Assert.True(built.IsSuccess, string.Join(";", built.Diagnostics.Select(d => $"{d.Code}:{d.Message}")));
        Assert.NotNull(built.SerializedJson);
        File.WriteAllText(
            Path.Combine(ReportDir(), "probe-querybuilder-bypass.json"),
            built.SerializedJson!);

        using var doc = JsonDocument.Parse(built.SerializedJson!);
        var stats = doc.RootElement.GetProperty("query").GetProperty("stats");
        Assert.Equal(2, stats.GetArrayLength());
        Assert.Equal("65502", stats[0].GetProperty("filters")[0].GetProperty("value").GetProperty("option").GetString());
        Assert.Equal("41119", stats[1].GetProperty("filters")[0].GetProperty("value").GetProperty("option").GetString());
        Assert.Equal(
            "enchant.stat_2954116742",
            stats[0].GetProperty("filters")[0].GetProperty("id").GetString());
        Assert.Equal(
            "enchant.stat_2954116742",
            stats[1].GetProperty("filters")[0].GetProperty("id").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(100)]
    public void Probe_Cardinality_QueryBuilderPreservesN_WhenFiltersNotCollapsed(int n)
    {
        var hashes = Enumerable.Range(0, n).Select(i => 10000 + i).ToArray();
        var components = hashes
            .Select((hash, index) => Anoint($"Passive{hash}", hash, index))
            .ToArray();
        var filters = hashes
            .Select((hash, index) => Filter(
                index,
                "enchant.stat_2954116742",
                hash.ToString(System.Globalization.CultureInfo.InvariantCulture),
                $"Allocates Passive{hash}"))
            .ToArray();
        var draft = Draft(components);
        var built = builder.Build(
            draft,
            TradeSearchValidationResult.FromDiagnostics([]),
            League,
            filters,
            new PathOfExileTradeItemIdentity
            {
                CanonicalName = "Synthetic Multi Anoint",
                CanonicalType = "Solaris Circlet",
                Foulborn = TradeTriState.No,
            });

        Assert.True(built.IsSuccess, string.Join(";", built.Diagnostics.Select(d => d.Code)));
        using var doc = JsonDocument.Parse(built.SerializedJson!);
        var stats = doc.RootElement.GetProperty("query").GetProperty("stats");
        if (n == 0)
        {
            Assert.Equal(1, stats.GetArrayLength());
            Assert.Empty(stats[0].GetProperty("filters").EnumerateArray());
            return;
        }

        Assert.Equal(n, stats.GetArrayLength());
        for (var i = 0; i < n; i++)
        {
            Assert.Equal(
                hashes[i].ToString(System.Globalization.CultureInfo.InvariantCulture),
                stats[i].GetProperty("filters")[0].GetProperty("value").GetProperty("option").GetString());
        }
    }

    private static object Describe(PathOfExileTradeSelectedModifierFilter filter) => new
    {
        filter.StatId,
        filter.Option,
        filter.SourceIndex,
        SourceIndexes = filter.SourceIndexes,
        filter.Minimum,
        filter.Maximum,
    };

    private static PathOfExileTradeSelectedModifierFilter Filter(
        int sourceIndex,
        string statId,
        string option,
        string text) => new()
    {
        SourceIndex = sourceIndex,
        SourceIndexes = [sourceIndex],
        StatId = statId,
        OriginalText = text,
        Option = option,
    };

    private static PathOfExileTradeStatCatalog LoadOfficial()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Trade", "official-stats-2026-08-19.json");
        return new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path)).Catalog!;
    }

    private static ResolvedSearchComponent Anoint(string name, int hash, int index) => new()
    {
        ComponentId = $"anoint:{hash}",
        SourceModifierIndex = index,
        SourceLineIndex = 0,
        SourceComponentIndex = index,
        OriginalText = $"Allocates {name}",
        CanonicalSignature = "Allocates <passive>",
        ParsedKind = ParsedModifierKind.Enchantment,
        GenerationType = ModifierGenerationType.Enchantment,
        ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
        ResolvedStatIds = ["mod_granted_passive_hash"],
        AnointPassiveIdentity = new PassiveSkillIdentity
        {
            CanonicalName = name,
            PassiveHash = hash,
            Sources = [new GameDataSourceReference { SourceId = "repoe", ExternalId = hash.ToString() }],
        },
        IsSearchable = true,
        IsSelected = true,
    };

    private static TradeSearchDraft Draft(IReadOnlyList<ResolvedSearchComponent> modifiers) => new()
    {
        ItemClass = "Helmets",
        Rarity = "Unique",
        DisplayName = "Cowl of the Ceraunophile",
        ParsedBaseType = "Solaris Circlet",
        Base = new TradeSearchBaseDraft
        {
            Status = ItemBaseResolutionStatus.Exact,
            ResolvedBaseId = "Metadata/Items/Armours/Helmets/HelmetInt7",
            ResolvedBaseName = "Solaris Circlet",
        },
        ModifierFilters = modifiers,
    };

    private static string ReportDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "PoEnhance-A.5.20.1-Multi-Allocates-Diagnostic");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteJson(string name, object value) =>
        File.WriteAllText(
            Path.Combine(ReportDir(), name),
            JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
}
