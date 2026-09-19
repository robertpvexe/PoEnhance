using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Diagnostics.Replay;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class LevelVsChanceCurseOnHitExportOwnerReplayTests
{
    [Theory]
    [InlineData(
        "Asenath-s-Gentle-Touch.clipboard.txt",
        "Asenath's Gentle Touch",
        "Temporal Chains",
        "TemporalChainsOnHitUniqueGlovesInt3",
        "curse_on_hit_%_temporal_chains")]
    [InlineData(
        "Dreadarc.clipboard.txt",
        "Dreadarc",
        "Flammability",
        "FlammabilityOnHitUniqueOneHandAxe7",
        "curse_on_hit_%_flammability")]
    [InlineData(
        "Uul-Netol-s-Kiss.clipboard.txt",
        "Uul-Netol's Kiss",
        "Vulnerability",
        "CurseLevel10VulnerabilityOnHitUnique__1",
        "curse_on_hit_level_10_vulnerability_%")]
    public async Task ReplayReadyClipboard_LevelVsChanceCurseOnHit_IsExactUniqueSearchable(
        string fixtureFile,
        string expectedName,
        string curseNeedle,
        string expectedModifierId,
        string expectedStatId)
    {
        var raw = await File.ReadAllTextAsync(FindFixture(fixtureFile));
        Assert.Contains(expectedName, raw, StringComparison.Ordinal);
        Assert.Contains(curseNeedle, raw, StringComparison.OrdinalIgnoreCase);

        var identity = await ModifierPipelineReplayGameDataGate.LoadIdentityAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        var tradeCatalog = LoadOfficialTradeCatalog();
        var tradeItems = new PathOfExileTradeItemCatalog(
        [
            TradeUnique(0, "Asenath's Gentle Touch", "Silk Gloves", "armour"),
            TradeUnique(1, "Dreadarc", "Cleaver", "weapon"),
            TradeUnique(2, "Uul-Netol's Kiss", "Vaal Axe", "weapon"),
        ]);
        var execution = ModifierPipelineReplayProductionPath.Execute(
            raw,
            identity.Catalog,
            tradeCatalog,
            tradeItems,
            PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());

        Assert.Equal(expectedName, execution.Parsed.Name);
        var unique = Assert.IsType<UniqueItemResolutionResult>(execution.UniqueResolution);
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unique.Status);

        var curse = Assert.Single(
            execution.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains(curseNeedle, StringComparison.OrdinalIgnoreCase) &&
                component.OriginalText.Contains("Curse Enemies", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, curse.ResolutionStatus);
        Assert.Equal(expectedModifierId, curse.ResolvedModifierId);
        Assert.Equal([expectedStatId], curse.ResolvedStatIds);
        Assert.True(curse.HasExactUniqueSourceProvenance);
        Assert.True(
            curse.ProviderResolutionStatus is
                SearchComponentProviderResolutionStatus.Exact or
                SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            $"Unexpected provider status {curse.ProviderResolutionStatus} / {curse.ProviderDiagnosticCode}");
        Assert.True(curse.IsSearchable, curse.NotSearchableReason);
        Assert.False(
            string.Equals(
                curse.ProviderDiagnosticCode,
                "POE_TRADE_SELECTED_MODIFIER_MISSING_UNIQUE_PROVENANCE",
                StringComparison.Ordinal));

        if (string.Equals(expectedStatId, "curse_on_hit_level_10_vulnerability_%", StringComparison.Ordinal))
        {
            Assert.Equal(SearchComponentProviderResolutionStatus.Exact, curse.ProviderResolutionStatus);
            Assert.Equal("explicit.stat_2213584313", curse.ProviderStatId);
            Assert.Equal(
                "#% chance to Curse Enemies with Vulnerability on Hit",
                curse.ProviderStatText);
            Assert.Equal([100m], curse.ProviderFallbackNumericValues);
            var projection = PathOfExileTradeModifierBoundProjector.ProjectBounds(
                curse,
                new PathOfExileTradeStatMatchCandidate
                {
                    ProviderOrder = 0,
                    GroupId = "explicit",
                    GroupLabel = "explicit",
                    Type = "explicit",
                    StatId = curse.ProviderStatId!,
                    Text = curse.ProviderStatText!,
                    NormalizedTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeTemplate(
                        curse.ProviderStatText),
                    LookupTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                        curse.ProviderStatText),
                    ProviderKind = "explicit",
                });
            Assert.Equal(100m, projection.Minimum);
            Assert.Equal(100m, projection.Maximum);
        }
        else
        {
            Assert.DoesNotContain(
                curse.ProviderStatText ?? string.Empty,
                "#% chance",
                StringComparison.Ordinal);
            if (curse.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact)
            {
                Assert.False(string.IsNullOrWhiteSpace(curse.ProviderStatId));
            }
            else
            {
                Assert.NotEmpty(curse.ProviderStatAlternativeIds);
            }
        }
    }

    private static PathOfExileTradeItemEntry TradeUnique(
        int order,
        string name,
        string type,
        string groupId) =>
        new()
        {
            ProviderOrder = order,
            GroupId = groupId,
            GroupLabel = groupId,
            Name = name,
            Type = type,
            IsUnique = true,
        };

    private static PathOfExileTradeStatCatalog LoadOfficialTradeCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        var parsed = new PathOfExileTradeStatsResponseParser().ParseStatsResponse(File.ReadAllText(path));
        Assert.True(parsed.IsSuccess);
        return Assert.IsType<PathOfExileTradeStatCatalog>(parsed.Catalog);
    }

    private static string FindFixture(string fileName) =>
        FindRepoFile("PoEnhance.App.Tests", "TestData", "Unique", "CurseOnHit", fileName);

    private static string FindRepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, parts));
    }
}
