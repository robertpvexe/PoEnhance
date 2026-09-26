using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

/// <summary>
/// TRADE.4c Track A — Exact Unique numeric-count translation branch discovers Trade article form.
/// </summary>
public sealed class PathOfExileTradeNumericCountArticleGrammarTests
{
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_NumericCountBranch_WithArticleSibling_ResolvesExactPresence()
    {
        var catalog = Catalog(
            Entry(
                "explicit.writhing",
                "An Enemy Writhing Worms escape the Flask when used\nWrithing Worms are destroyed when Hit",
                "explicit"));
        var result = matcher.Match(
            ExactUnique(
                "2 Enemy Writhing Worms escape the Flask when used\nWrithing Worms are destroyed when Hit",
                "<number> Enemy Writhing Worms escape the Flask when used\nWrithing Worms are destroyed when Hit",
                ["local_number_of_bloodworms_to_spawn_on_flask_use"],
                [2m],
                Translation(
                    "local_number_of_bloodworms_to_spawn_on_flask_use",
                    ArticleVariant(
                        "An Enemy Writhing Worms escape the Flask when used",
                        "Writhing Worms are destroyed when Hit"),
                    NumericVariant(
                        "{0} Enemy Writhing Worms escape the Flask when used",
                        "Writhing Worms are destroyed when Hit"))),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.writhing", result.ExactCandidate?.StatId);
        Assert.Equal(0, PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(
            result.ExactCandidate!.Text));

        var component = ExactUnique(
            "2 Enemy Writhing Worms escape the Flask when used\nWrithing Worms are destroyed when Hit",
            "<number> Enemy Writhing Worms escape the Flask when used\nWrithing Worms are destroyed when Hit",
            ["local_number_of_bloodworms_to_spawn_on_flask_use"],
            [2m],
            Translation(
                "local_number_of_bloodworms_to_spawn_on_flask_use",
                ArticleVariant(
                    "An Enemy Writhing Worms escape the Flask when used",
                    "Writhing Worms are destroyed when Hit"),
                NumericVariant(
                    "{0} Enemy Writhing Worms escape the Flask when used",
                    "Writhing Worms are destroyed when Hit")));
        var bounds = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            component,
            result.ExactCandidate);
        Assert.Equal(ModifierBoundShape.PresenceOnly, bounds.ValueBoundShape);
        Assert.Null(bounds.Minimum);
        Assert.Null(bounds.Maximum);
        Assert.Equal("NumericCountArticlePresence", bounds.ProjectionKind);
    }

    [Fact]
    public void Match_SyntheticArticleSibling_ResolvesExact()
    {
        var catalog = Catalog(
            Entry("explicit.synthetic", "An Example Token escapes when used", "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "3 Example Token escapes when used",
                "<number> Example Token escapes when used",
                ["synthetic_count_article_stat"],
                [3m],
                Translation(
                    "synthetic_count_article_stat",
                    ArticleVariant("An Example Token escapes when used"),
                    NumericVariant("{0} Example Token escapes when used"))),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.synthetic", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_WithoutArticleSiblingEvidence_RemainsNotFound()
    {
        var catalog = Catalog(
            Entry("explicit.article", "An Example Token escapes when used", "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "3 Example Token escapes when used",
                "<number> Example Token escapes when used",
                ["synthetic_count_only_stat"],
                [3m],
                Translation(
                    "synthetic_count_only_stat",
                    NumericVariant("{0} Example Token escapes when used"))),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Match_WrongTranslationFamily_Rejected()
    {
        var catalog = Catalog(
            Entry("explicit.article", "An Example Token escapes when used", "explicit"));

        var result = matcher.Match(
            ExactUnique(
                "3 Example Token escapes when used",
                "<number> Example Token escapes when used",
                ["other_family_stat"],
                [3m],
                Translation(
                    "other_family_stat",
                    ArticleVariant("An Other Thing happens"),
                    NumericVariant("{0} Other Thing happens"))),
            catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void ProjectBounds_ArticleCandidateWithArity_DoesNotForcePresenceOnly()
    {
        // Arity>0 article-looking text is outside Track A FilterArity-0 class.
        var candidate = Candidate("#% chance to An Example Token escapes when used", "explicit.chance");
        var projection = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            ExactUnique(
                "3 Example Token escapes when used",
                "<number> Example Token escapes when used",
                ["synthetic_count_article_stat"],
                [3m]),
            candidate);
        Assert.NotEqual("NumericCountArticlePresence", projection.ProjectionKind);
    }

    [Fact]
    public void Match_MultipleArticleCandidates_RemainAmbiguous()
    {
        var catalog = Catalog(
            Entry("explicit.a", "An Example Token escapes when used", "explicit", 0),
            Entry("explicit.b", "An Example Token escapes when used", "explicit", 1));

        var result = matcher.Match(
            ExactUnique(
                "3 Example Token escapes when used",
                "<number> Example Token escapes when used",
                ["synthetic_count_article_stat"],
                [3m],
                Translation(
                    "synthetic_count_article_stat",
                    ArticleVariant("An Example Token escapes when used"),
                    NumericVariant("{0} Example Token escapes when used"))),
            catalog);

        Assert.True(
            result.Status is PathOfExileTradeStatMatchStatus.Ambiguous or
                PathOfExileTradeStatMatchStatus.ExactEquivalentSet);
        Assert.True(result.Candidates.Count >= 2 || result.ExactEquivalentCandidates.Count >= 2);
    }

    private static StatTranslationRecognitionEvidence Translation(
        string statId,
        params StatTranslationVariant[] variants) =>
        new()
        {
            Role = StatTranslationRecognitionRole.CurrentExact,
            CanonicalTranslation = new StatTranslationDefinition
            {
                Id = "test-translation:" + statId,
                StatIds = [statId],
                Language = "English",
                Variants = variants,
            },
            RecognizedTranslation = new StatTranslationDefinition
            {
                Id = "test-translation:" + statId,
                StatIds = [statId],
                Language = "English",
                Variants = variants,
            },
        };

    private static StatTranslationVariant ArticleVariant(params string[] lines) =>
        new()
        {
            Conditions = [new StatTranslationCondition { Index = 0, MinValue = 1, MaxValue = 1 }],
            ValueFormats = ["ignore"],
            FormatLines = lines,
        };

    private static StatTranslationVariant NumericVariant(params string[] lines) =>
        new()
        {
            Conditions = [new StatTranslationCondition { Index = 0, MinValue = 2, MaxValue = null }],
            ValueFormats = ["#"],
            FormatLines = lines,
        };

    private static ResolvedSearchComponent ExactUnique(
        string original,
        string signature,
        IReadOnlyList<string> statIds,
        IReadOnlyList<decimal>? observed = null,
        StatTranslationRecognitionEvidence? translation = null) =>
        new()
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            OriginalText = original,
            CanonicalSignature = signature,
            ProviderCanonicalSignature = signature,
            ProviderSearchSignatures = [signature, .. original.Split('\n')],
            ParsedKind = ParsedModifierKind.Unique,
            UniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "unique-mod:article-grammar-test",
            ResolvedStatIds = statIds.ToArray(),
            UniqueCatalogBlockIds = ["unique-block:test"],
            UniqueSourceObservationIds = ["pob-observation:test"],
            IsSearchable = true,
            SupportsValueBounds = true,
            ValueBoundShape = ModifierBoundShape.Scalar,
            ObservedNumericValues = observed?.ToArray() ?? [],
            ProviderFallbackNumericValues = observed?.ToArray() ?? [],
            RequestedMinimum = observed is { Count: > 0 } ? observed[0] : null,
            RequestedMaximum = observed is { Count: > 0 } ? observed[0] : null,
            TranslationRecognition = translation,
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
}
