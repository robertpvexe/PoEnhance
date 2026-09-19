using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeExactOwnerChancePercentSiblingTests
{
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_ExactChanceMechanicWithUniqueCompatibleSibling_SelectsParametricChanceForm()
    {
        const string presence = "Curse Enemies with Vulnerability on Hit";
        const string chance = "#% chance to Curse Enemies with Vulnerability on Hit";
        var catalog = Catalog(
            Entry("explicit.presence", presence, "explicit", 0),
            Entry("explicit.chance", chance, "explicit", 1));

        var result = matcher.Match(
            ExactChancePresenceComponent(
                presence,
                "curse_on_hit_level_10_vulnerability_%",
                fallback: 100m),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.chance", result.ExactCandidate?.StatId);
        Assert.Equal(chance, result.ExactCandidate?.Text);
        Assert.Contains(result.InitialCandidates, candidate => candidate.StatId == "explicit.presence");
        Assert.DoesNotContain(result.Candidates, candidate => candidate.StatId == "explicit.presence");
    }

    [Fact]
    public void ProjectBounds_ExactChanceSibling_ProjectsAuthoritativeOwnerFallbackScalar()
    {
        const string presence = "Curse Enemies with Vulnerability on Hit";
        const string chance = "#% chance to Curse Enemies with Vulnerability on Hit";
        var projection = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            ExactChancePresenceComponent(
                presence,
                "curse_on_hit_level_10_vulnerability_%",
                fallback: 100m),
            Candidate(chance, "explicit.chance"));

        Assert.True(projection.IsFaithful);
        Assert.Equal(ModifierBoundShape.Scalar, projection.ValueBoundShape);
        Assert.Equal(100m, projection.Minimum);
        Assert.Equal(100m, projection.Maximum);
        Assert.Equal("ExactOwnerChancePercentSiblingFallback", projection.ProjectionKind);
    }

    [Fact]
    public void Match_PresenceMappingWithoutCompatibleSibling_RemainsUnchanged()
    {
        const string presence = "Curse Enemies with Temporal Chains on Hit";
        var catalog = Catalog(
            Entry("explicit.presence.a", presence, "explicit", 0),
            Entry("explicit.presence.b", presence, "explicit", 1));

        var result = matcher.Match(
            ExactChancePresenceComponent(
                presence,
                "curse_on_hit_%_temporal_chains",
                fallback: 100m),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.ExactEquivalentSet, result.Status);
        Assert.Equal(2, result.Candidates.Count);
        Assert.All(
            result.Candidates,
            candidate => Assert.Equal(presence, candidate.Text));
    }

    [Fact]
    public void Match_EquivalentPresenceAliases_PreservedWhenNoChanceSibling()
    {
        const string presence = "Curse Enemies with Flammability on Hit";
        var catalog = Catalog(
            Entry("explicit.flam.a", presence, "explicit", 0),
            Entry("explicit.flam.b", presence, "explicit", 1));

        var result = matcher.Match(
            ExactChancePresenceComponent(
                presence,
                "curse_on_hit_%_flammability",
                fallback: 100m),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.ExactEquivalentSet, result.Status);
        Assert.Equal(
            ["explicit.flam.a", "explicit.flam.b"],
            result.Candidates.Select(candidate => candidate.StatId).OrderBy(id => id).ToArray());
    }

    [Fact]
    public void Match_WithoutExactSourceProvenance_DoesNotRemapToChanceSibling()
    {
        const string presence = "Curse Enemies with Vulnerability on Hit";
        const string chance = "#% chance to Curse Enemies with Vulnerability on Hit";
        var catalog = Catalog(
            Entry("explicit.presence", presence, "explicit", 0),
            Entry("explicit.chance", chance, "explicit", 1));
        var component = ExactChancePresenceComponent(
            presence,
            "curse_on_hit_level_10_vulnerability_%",
            fallback: 100m) with
        {
            UniqueCatalogBlockIds = [],
            UniqueSourceObservationIds = [],
        };

        var result = matcher.Match(component, catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.presence", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_NonChanceInternalMechanic_DoesNotRemapToChanceSibling()
    {
        const string presence = "Curse Enemies with Vulnerability on Hit";
        const string chance = "#% chance to Curse Enemies with Vulnerability on Hit";
        var catalog = Catalog(
            Entry("explicit.presence", presence, "explicit", 0),
            Entry("explicit.chance", chance, "explicit", 1));

        var result = matcher.Match(
            ExactChancePresenceComponent(
                presence,
                "local_physical_damage_+%",
                fallback: 100m),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.presence", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_MissingAuthoritativeNumericFallback_DoesNotRemapToChanceSibling()
    {
        const string presence = "Curse Enemies with Vulnerability on Hit";
        const string chance = "#% chance to Curse Enemies with Vulnerability on Hit";
        var catalog = Catalog(
            Entry("explicit.presence", presence, "explicit", 0),
            Entry("explicit.chance", chance, "explicit", 1));
        var component = ExactChancePresenceComponent(
            presence,
            "curse_on_hit_level_10_vulnerability_%",
            fallback: 100m) with
        {
            ProviderFallbackNumericValues = [],
        };

        var result = matcher.Match(component, catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.presence", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void ProjectBounds_MissingAuthoritativeNumericFallback_DoesNotInventScalar()
    {
        const string presence = "Curse Enemies with Vulnerability on Hit";
        const string chance = "#% chance to Curse Enemies with Vulnerability on Hit";
        var component = ExactChancePresenceComponent(
            presence,
            "curse_on_hit_level_10_vulnerability_%",
            fallback: 100m) with
        {
            ProviderFallbackNumericValues = [],
        };

        var projection = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            component,
            Candidate(chance, "explicit.chance"));

        Assert.True(projection.IsFaithful);
        Assert.Null(projection.Minimum);
        Assert.Null(projection.Maximum);
        Assert.False(
            string.Equals(
                "ExactOwnerChancePercentSiblingFallback",
                projection.ProjectionKind,
                StringComparison.Ordinal));
    }

    [Fact]
    public void Match_MultipleNonEquivalentChanceSiblings_FailsClosedToPresence()
    {
        const string presence = "Curse Enemies with Vulnerability on Hit";
        const string chance = "#% chance to Curse Enemies with Vulnerability on Hit";
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry("explicit.presence", presence, "explicit", 0),
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 1,
                GroupId = "explicit",
                GroupLabel = "explicit",
                Id = "explicit.chance.a",
                Text = chance,
                Type = "explicit",
            },
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 2,
                GroupId = "explicit",
                GroupLabel = "explicit-alt",
                Id = "explicit.chance.b",
                Text = chance,
                Type = "explicit",
            },
        ]);

        var result = matcher.Match(
            ExactChancePresenceComponent(
                presence,
                "curse_on_hit_level_10_vulnerability_%",
                fallback: 100m),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.presence", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_OptionedChanceSiblings_FailsClosedToPresence()
    {
        const string presence = "Curse Enemies with Vulnerability on Hit";
        const string chance = "#% chance to Curse Enemies with Vulnerability on Hit";
        var catalog = new PathOfExileTradeStatCatalog(
        [
            Entry("explicit.presence", presence, "explicit", 0),
            new PathOfExileTradeStatEntry
            {
                ProviderOrder = 1,
                GroupId = "explicit",
                GroupLabel = "explicit",
                Id = "explicit.chance.optioned",
                Text = chance,
                Type = "explicit",
                OptionMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["1"] = "option-a",
                },
            },
        ]);

        var result = matcher.Match(
            ExactChancePresenceComponent(
                presence,
                "curse_on_hit_level_10_vulnerability_%",
                fallback: 100m),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.presence", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_WrongProviderKind_DoesNotCrossMapChanceSibling()
    {
        const string presence = "Curse Enemies with Vulnerability on Hit";
        const string chance = "#% chance to Curse Enemies with Vulnerability on Hit";
        var catalog = Catalog(
            Entry("explicit.presence", presence, "explicit", 0),
            Entry("implicit.chance", chance, "implicit", 1));

        var result = matcher.Match(
            ExactChancePresenceComponent(
                presence,
                "curse_on_hit_level_10_vulnerability_%",
                fallback: 100m),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.presence", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_UnrelatedPresenceChanceFamily_DoesNotRemapWithoutChanceMechanic()
    {
        const string presence = "Ignite Enemies";
        const string chance = "#% chance to Ignite Enemies";
        var catalog = Catalog(
            Entry("explicit.presence", presence, "explicit", 0),
            Entry("explicit.chance", chance, "explicit", 1));

        var result = matcher.Match(
            ExactChancePresenceComponent(
                presence,
                "base_fire_damage_taken",
                fallback: 20m),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.presence", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_OfficialCatalog_UulSelectsChanceSibling_AsenathAndDreadarcStayPresence()
    {
        var catalog = LoadOfficialTradeCatalog();

        var uul = matcher.Match(
            ExactChancePresenceComponent(
                "Curse Enemies with Vulnerability on Hit",
                "curse_on_hit_level_10_vulnerability_%",
                fallback: 100m),
            catalog);
        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, uul.Status);
        Assert.Equal("explicit.stat_2213584313", uul.ExactCandidate?.StatId);
        Assert.Equal(
            "#% chance to Curse Enemies with Vulnerability on Hit",
            uul.ExactCandidate?.Text);

        var asenath = matcher.Match(
            ExactChancePresenceComponent(
                "Curse Enemies with Temporal Chains on Hit",
                "curse_on_hit_%_temporal_chains",
                fallback: 100m),
            catalog);
        Assert.Equal(PathOfExileTradeStatMatchStatus.ExactEquivalentSet, asenath.Status);
        Assert.All(
            asenath.Candidates,
            candidate => Assert.Equal(
                "Curse Enemies with Temporal Chains on Hit",
                candidate.Text));
        Assert.DoesNotContain(
            asenath.Candidates,
            candidate => candidate.Text.Contains("#% chance", StringComparison.Ordinal));

        var dreadarc = matcher.Match(
            ExactChancePresenceComponent(
                "Curse Enemies with Flammability on Hit",
                "curse_on_hit_%_flammability",
                fallback: 100m),
            catalog);
        Assert.Equal(PathOfExileTradeStatMatchStatus.ExactEquivalentSet, dreadarc.Status);
        Assert.All(
            dreadarc.Candidates,
            candidate => Assert.Equal(
                "Curse Enemies with Flammability on Hit",
                candidate.Text));
    }

    private static ResolvedSearchComponent ExactChancePresenceComponent(
        string presenceText,
        string resolvedStatId,
        decimal fallback) =>
        new()
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            OriginalText = presenceText,
            CanonicalSignature = presenceText,
            ProviderSearchSignatures = [presenceText],
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "mod.exact.unique",
            ResolvedStatIds = [resolvedStatId],
            UniqueCatalogBlockIds = ["unique-block:test"],
            UniqueSourceObservationIds = ["pob-observation:test"],
            IsSearchable = true,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            SupportsValueBounds = false,
            ProviderFallbackNumericValues = [fallback],
            NumericQueryRole = NumericQueryRole.PresenceOnly,
        };

    private static PathOfExileTradeStatMatchCandidate Candidate(string text, string id) =>
        new()
        {
            ProviderOrder = 0,
            GroupId = "explicit",
            GroupLabel = "explicit",
            Type = "explicit",
            StatId = id,
            Text = text,
            NormalizedTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeTemplate(text),
            LookupTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(text),
            ProviderKind = "explicit",
        };

    private static PathOfExileTradeStatCatalog Catalog(params PathOfExileTradeStatEntry[] entries) =>
        new(entries);

    private static PathOfExileTradeStatEntry Entry(
        string id,
        string text,
        string groupId,
        int providerOrder = 0) =>
        new()
        {
            ProviderOrder = providerOrder,
            GroupId = groupId,
            GroupLabel = groupId,
            Id = id,
            Text = text,
            Type = groupId,
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
