using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.Parsing;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeStatMatcherTests
{
    private readonly PathOfExileTradeStatMatcher matcher = new();

    [Fact]
    public void Match_OneExactNormalizedTemplateProducesExactWithOneNumber()
    {
        var catalog = Catalog(Entry("explicit.life", "+# to maximum Life", "explicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Prefix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("explicit.life", result.ExactCandidate?.StatId);
        Assert.Equal("+# to maximum Life", result.NormalizedItemTemplate);
        Assert.Equal([87m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_TwoNumberValuesPreserveOrder()
    {
        var catalog = Catalog(Entry("explicit.fire", "Adds # to # Fire Damage", "explicit"));

        var result = matcher.Match(Modifier("Adds 10 to 20 Fire Damage", ParsedModifierKind.Suffix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal([10m, 20m], result.ExtractedNumericValues);
    }

    [Fact]
    public void Match_NoMatchingTemplateProducesNotFoundWithoutFallbackId()
    {
        var catalog = Catalog(Entry("explicit.mana", "+# to maximum Mana", "explicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Prefix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Null(result.ExactCandidate);
        Assert.Empty(result.Candidates);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.NoCandidate, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_TwoMatchingProviderEntriesProducesAmbiguousAndDoesNotChooseFirst()
    {
        var catalog = Catalog(
            Entry("explicit.life.one", "+# to maximum Life", "explicit"),
            Entry("explicit.life.two", "+# to maximum Life", "explicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Prefix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Ambiguous, result.Status);
        Assert.Null(result.ExactCandidate);
        Assert.Equal(["explicit.life.one", "explicit.life.two"], result.Candidates.Select(candidate => candidate.StatId));
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.AmbiguousCandidates, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_BlankModifierInputProducesInvalidInput()
    {
        var result = matcher.Match(Modifier("  ", ParsedModifierKind.Unknown), Catalog());

        Assert.Equal(PathOfExileTradeStatMatchStatus.InvalidInput, result.Status);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.BlankModifierText, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_NullCatalogProducesInvalidInput()
    {
        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Prefix), null);

        Assert.Equal(PathOfExileTradeStatMatchStatus.InvalidInput, result.Status);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.NullCatalog, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_UnsupportedNumericTokenProducesInvalidInput()
    {
        var result = matcher.Match(
            Modifier("Adds 1,000 Fire Damage", ParsedModifierKind.Prefix),
            Catalog(Entry("explicit.fire", "Adds # Fire Damage", "explicit")));

        Assert.Equal(PathOfExileTradeStatMatchStatus.InvalidInput, result.Status);
        Assert.Equal(
            PathOfExileTradeStatMatchDiagnosticCodes.UnsupportedNumericTokenFormat,
            Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_ExplicitModifierDoesNotMapToClearlyImplicitEntry()
    {
        var catalog = Catalog(Entry("implicit.life", "+# to maximum Life", "implicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Prefix), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_ImplicitModifierDoesNotMapToClearlyExplicitEntry()
    {
        var catalog = Catalog(Entry("explicit.life", "+# to maximum Life", "explicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Implicit), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, result.Status);
        Assert.Equal(PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch, Assert.Single(result.Diagnostics).Code);
    }

    [Fact]
    public void Match_CraftedAndFracturedModifiersDoNotMapToUnrelatedKnownCategories()
    {
        var craftedResult = matcher.Match(
            Modifier("+87 to maximum Life", ParsedModifierKind.Prefix, isCrafted: true),
            Catalog(Entry("explicit.life", "+# to maximum Life", "explicit")));
        var fracturedResult = matcher.Match(
            Modifier("+87 to maximum Life", ParsedModifierKind.Prefix, isFractured: true),
            Catalog(Entry("explicit.life", "+# to maximum Life", "explicit")));

        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, craftedResult.Status);
        Assert.Equal(PathOfExileTradeStatMatchStatus.NotFound, fracturedResult.Status);
    }

    [Fact]
    public void Match_UnknownProviderMetadataRemainsConservativeAndAmbiguous()
    {
        var catalog = Catalog(
            Entry("unknown.one", "+# to maximum Life", "mystery"),
            Entry("unknown.two", "+# to maximum Life", "mystery"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Implicit), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Ambiguous, result.Status);
        Assert.Equal(["unknown.one", "unknown.two"], result.Candidates.Select(candidate => candidate.StatId));
    }

    [Fact]
    public void Match_KindConstraintCanSelectOneCompatibleCandidateFromOtherwiseAmbiguousSet()
    {
        var catalog = Catalog(
            Entry("explicit.life", "+# to maximum Life", "explicit"),
            Entry("implicit.life", "+# to maximum Life", "implicit"));

        var result = matcher.Match(Modifier("+87 to maximum Life", ParsedModifierKind.Implicit), catalog);

        Assert.Equal(PathOfExileTradeStatMatchStatus.Exact, result.Status);
        Assert.Equal("implicit.life", result.ExactCandidate?.StatId);
    }

    [Fact]
    public void Match_RepeatedMatchingIsDeterministic()
    {
        var catalog = Catalog(
            Entry("explicit.one", "+# to maximum Life", "explicit"),
            Entry("explicit.two", "+# to maximum Life", "explicit"));
        var modifier = Modifier("+87 to maximum Life", ParsedModifierKind.Prefix);

        var first = matcher.Match(modifier, catalog);
        var second = matcher.Match(modifier, catalog);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(
            first.Candidates.Select(candidate => candidate.StatId),
            second.Candidates.Select(candidate => candidate.StatId));
    }

    private static PathOfExileTradeStatCatalog Catalog(params PathOfExileTradeStatEntry[] entries)
    {
        return new PathOfExileTradeStatCatalog(entries);
    }

    private static PathOfExileTradeStatEntry Entry(
        string id,
        string text,
        string groupId)
    {
        return new PathOfExileTradeStatEntry
        {
            ProviderOrder = 0,
            GroupId = groupId,
            GroupLabel = groupId,
            Id = id,
            Text = text,
            Type = groupId,
        };
    }

    private static ParsedModifier Modifier(
        string text,
        ParsedModifierKind kind,
        bool isCrafted = false,
        bool isFractured = false)
    {
        return new ParsedModifier(
            [text],
            RawMetadataLine: null,
            kind,
            Name: null,
            Tier: null,
            Rank: null,
            CategoryText: null,
            IsCrafted: isCrafted,
            IsFractured: isFractured,
            IsVeiled: false);
    }
}
