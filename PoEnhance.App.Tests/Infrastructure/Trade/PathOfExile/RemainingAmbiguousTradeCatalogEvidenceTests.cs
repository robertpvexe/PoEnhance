using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// U.2.4 final Ambiguous investigation — Trade catalog evidence only (frozen fixture).
/// Live api/trade/data/stats (2026-10-25) matched these pierce/spectre labels; this test
/// locks the fixture so Exact cannot be invented from missing exact-text Trade identity.
/// Not a Search/Fetch verification.
/// </summary>
public sealed class RemainingAmbiguousTradeCatalogEvidenceTests
{
    private const string OfficialStatsFixtureRelative =
        "PoEnhance.App.Tests/TestData/Trade/official-stats-2026-08-19.json";

    [Fact]
    public void FrozenOfficialCatalog_PoacherPierce_HasNumericHashForm_NotExactAnAdditionalLabel()
    {
        var catalog = LoadOfficialTradeCatalog();

        Assert.True(catalog.TryGetById("explicit.stat_2067062068", out var hashForm));
        Assert.Equal("Projectiles Pierce # additional Targets", hashForm.Text);

        Assert.True(catalog.TryGetById("explicit.stat_97250660", out var phasing));
        Assert.Equal(
            "Projectiles Pierce an additional Target while you have Phasing",
            phasing.Text);

        Assert.True(catalog.TryGetById("explicit.stat_3423006863", out var arrows));
        Assert.Equal("Arrows Pierce an additional Target", arrows.Text);

        // Exact Unique display text is absent as a typed Trade label.
        Assert.DoesNotContain(
            catalog.Entries,
            entry => string.Equals(
                entry.Text,
                "Projectiles Pierce an additional Target",
                StringComparison.Ordinal));
    }

    [Fact]
    public void FrozenOfficialCatalog_SpectreMaximum_HasExplicitHashForm()
    {
        // Bitterbind in-game Explicit/Prefix searchability uses this filter family; it does not
        // prove Unique Fixed Exact among MaximumMinionCount* / TalismanEnchant* candidates.
        var catalog = LoadOfficialTradeCatalog();
        Assert.True(catalog.TryGetById("explicit.stat_125218179", out var spectre));
        Assert.Equal("+# to maximum number of Spectres", spectre.Text);
    }

    private static PathOfExileTradeStatCatalog LoadOfficialTradeCatalog()
    {
        var path = FindRepoFile(
            "PoEnhance.App.Tests",
            "TestData",
            "Trade",
            "official-stats-2026-08-19.json");
        Assert.EndsWith(
            OfficialStatsFixtureRelative.Replace('/', Path.DirectorySeparatorChar),
            path,
            StringComparison.OrdinalIgnoreCase);
        var parsed = new PathOfExileTradeStatsResponseParser()
            .ParseStatsResponse(File.ReadAllText(path));
        Assert.True(parsed.IsSuccess, string.Join(" | ", parsed.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<PathOfExileTradeStatCatalog>(parsed.Catalog);
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
}
