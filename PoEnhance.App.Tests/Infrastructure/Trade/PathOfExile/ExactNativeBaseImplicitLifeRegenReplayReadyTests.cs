using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.App.Tests.Diagnostics.Replay;
using PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class ExactNativeBaseImplicitLifeRegenReplayReadyTests
{
    [Theory]
    [InlineData("Tainted-Pact.clipboard.txt", "Tainted Pact")]
    [InlineData("Tavukai.clipboard.txt", "Tavukai")]
    public async Task ReplayReadyClipboard_ExactNativeLifeRegenImplicit_IsProviderExactSearchable(
        string fixtureFile,
        string expectedName)
    {
        var raw = await File.ReadAllTextAsync(FindFixture(fixtureFile));
        Assert.Contains(expectedName, raw, StringComparison.Ordinal);

        var identity = await ModifierPipelineReplayGameDataGate.LoadIdentityAsync(
            FindRepoFile("artifacts", "poenhance-game-data.json"));
        var tradeCatalog = LoadOfficialTradeCatalog();
        var execution = ModifierPipelineReplayProductionPath.Execute(
            raw,
            identity.Catalog,
            tradeCatalog,
            new PathOfExileTradeItemCatalog([]),
            PathOfExileTradeItemPropertyTestFixtures.OfficialCatalog());

        Assert.Equal(expectedName, execution.Parsed.Name);
        var life = Assert.Single(
            execution.ProviderDraft.ModifierFilters,
            component => component.OriginalText.Contains("Life per second", StringComparison.Ordinal) &&
                component.OriginalText.Contains("Regenerate", StringComparison.Ordinal));

        Assert.Equal(ParsedModifierKind.Implicit, life.ParsedKind);
        Assert.True(life.IsBaseImplicit);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, life.ResolutionStatus);
        Assert.Equal("LifeRegenerationImplicitAmulet1", life.ResolvedModifierId);
        Assert.Equal(["base_life_regeneration_rate_per_minute"], life.ResolvedStatIds);
        var provenance = Assert.IsType<SearchComponentBaseImplicitProvenance>(life.BaseImplicitProvenance);
        Assert.Equal(BaseImplicitRecognitionStatus.CurrentExact, provenance.RecognitionStatus);
        Assert.Single(provenance.MechanicalSignatures);
        Assert.Contains(
            provenance.SourceSnapshots,
            snapshot => snapshot.Role == BaseImplicitSnapshotRole.CurrentCandidate);
        Assert.Equal(SearchComponentProviderResolutionStatus.Exact, life.ProviderResolutionStatus);
        Assert.True(life.IsSearchable);
        Assert.NotNull(life.ProviderStatId);
        Assert.StartsWith("implicit.", life.ProviderStatId, StringComparison.Ordinal);
        Assert.Equal("Regenerate # Life per second", life.ProviderStatText);
        Assert.True(life.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.Scalar, life.ValueBoundShape);

        var match = new PathOfExileTradeStatMatcher().Match(
            life with
            {
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.NotResolved,
                ProviderDiagnosticCode = null,
                ProviderStatId = null,
                ProviderStatAlternativeIds = [],
                ProviderCandidateStatIds = [],
            },
            tradeCatalog,
            new PathOfExileTradeStatMatchContext
            {
                ItemClass = execution.ProviderDraft.ItemClass,
                ParsedBaseType = execution.ProviderDraft.ParsedBaseType,
                ModifierLocality = life.Locality,
                ResolvedModifierId = life.ResolvedModifierId,
                HasExactGameDataSourceProof = true,
                ResolvedModifierName = life.ResolvedModifierName,
                InternalStatIds = life.ResolvedStatIds,
                InternalStatLocalities = life.ResolvedStatLocalities,
            });
        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, match.Status);
        Assert.NotNull(match.ExactCandidate);
        Assert.Equal("implicit", match.ExactCandidate.Type, ignoreCase: true);
        Assert.Equal("Regenerate # Life per second", match.ExactCandidate.Text);
        Assert.All(
            match.Candidates,
            candidate => Assert.Equal("implicit", candidate.Type, ignoreCase: true));
    }

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
        FindRepoFile("PoEnhance.App.Tests", "TestData", "Unique", "A59BaseImplicitLifeRegen", fileName);

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

        throw new FileNotFoundException(string.Join("/", relativeParts));
    }
}
