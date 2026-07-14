using PoEnhance.App.Infrastructure.Trade.PathOfExile;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeStatCatalogTests
{
    [Fact]
    public void CandidateGroups_SyntheticLocalAndUnmarkedProviderPairGroupsAutomatically()
    {
        var catalog = Catalog(
            Entry("explicit.some_new_unmarked", "Some New Modifier", "explicit"),
            Entry("explicit.some_new_local", "Some New Modifier (Local)", "explicit"));

        var group = Assert.Single(catalog.FindCandidateGroupsByNormalizedTemplate("Some New Modifier"));

        Assert.Equal("explicit:Some New Modifier", group.Key.ToString());
        Assert.Equal(
            [PathOfExileTradeProviderStatLocality.Unmarked, PathOfExileTradeProviderStatLocality.Local],
            group.Candidates.Select(candidate => candidate.ProviderLocality));
    }

    [Fact]
    public void CandidateGroups_DifferentProviderKindsRemainSeparate()
    {
        var catalog = Catalog(
            Entry("explicit.life", "+# to maximum Life", "explicit"),
            Entry("implicit.life", "+# to maximum Life", "implicit"),
            Entry("pseudo.life", "+# to maximum Life", "pseudo"));

        var groups = catalog.FindCandidateGroupsByNormalizedTemplate("+# to maximum Life");

        Assert.Equal(["explicit", "implicit", "pseudo"], groups.Select(group => group.Key.ProviderKind));
    }

    [Fact]
    public void CandidateGroups_ScopedDamageTemplatesRemainSeparate()
    {
        var catalog = Catalog(
            Entry("explicit.local_fire", "Adds # to # Fire Damage (Local)", "explicit"),
            Entry("explicit.fire_attacks", "Adds # to # Fire Damage to Attacks", "explicit"),
            Entry("explicit.fire_spells", "Adds # to # Fire Damage to Spells", "explicit"));

        Assert.Single(catalog.FindCandidateGroupsByNormalizedTemplate("Adds # to # Fire Damage"));
        Assert.Single(catalog.FindCandidateGroupsByNormalizedTemplate("Adds # to # Fire Damage to Attacks"));
        Assert.Single(catalog.FindCandidateGroupsByNormalizedTemplate("Adds # to # Fire Damage to Spells"));
    }

    [Fact]
    public void CandidateGroups_ResponseOrderingDoesNotAffectDiagnosticCandidateOrdering()
    {
        var forward = Catalog(
            Entry("explicit.z", "Some New Modifier (Local)", "explicit", providerOrder: 0),
            Entry("explicit.a", "Some New Modifier", "explicit", providerOrder: 1));
        var reversed = Catalog(
            Entry("explicit.a", "Some New Modifier", "explicit", providerOrder: 0),
            Entry("explicit.z", "Some New Modifier (Local)", "explicit", providerOrder: 1));

        var forwardIds = Assert.Single(forward.FindCandidateGroupsByNormalizedTemplate("Some New Modifier"))
            .Candidates
            .Select(candidate => candidate.StatId);
        var reversedIds = Assert.Single(reversed.FindCandidateGroupsByNormalizedTemplate("Some New Modifier"))
            .Candidates
            .Select(candidate => candidate.StatId);

        Assert.Equal(forwardIds, reversedIds);
        Assert.Equal(["explicit.a", "explicit.z"], forwardIds);
    }

    private static PathOfExileTradeStatCatalog Catalog(params PathOfExileTradeStatEntry[] entries)
    {
        return new PathOfExileTradeStatCatalog(entries);
    }

    private static PathOfExileTradeStatEntry Entry(
        string id,
        string text,
        string groupId,
        int providerOrder = 0)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = providerOrder,
            GroupId = groupId,
            GroupLabel = groupId,
            Id = id,
            Text = text,
            Type = groupId,
        };
    }
}
