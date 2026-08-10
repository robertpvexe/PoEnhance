using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeT3ProviderBlockerTests
{
    public static TheoryData<string, string, ParsedModifierKind, bool, string, int>
        Cases => new()
        {
            {
                "{0}% of Hit Damage from you and your Minions cannot be Reflected",
                "You and your Minions prevent +<number>% of Reflected Damage",
                ParsedModifierKind.Suffix,
                false,
                nameof(PathOfExileTradeStatMatchStatus.Exact),
                1
            },
            {
                "{0}% of Elemental Hit Damage from you and your Minions cannot be Reflected",
                "You and your Minions prevent +<number>% of Reflected Elemental Damage",
                ParsedModifierKind.Prefix,
                false,
                nameof(PathOfExileTradeStatMatchStatus.Exact),
                1
            },
            {
                "{0}% of Physical Hit Damage from you and your Minions cannot be Reflected",
                "You and your Minions prevent +<number>% of Reflected Physical Damage",
                ParsedModifierKind.Prefix,
                false,
                nameof(PathOfExileTradeStatMatchStatus.Exact),
                1
            },
            {
                "{0}% of Damage from your Hits cannot be Reflected",
                "Prevent +<number>% of Reflected Damage",
                ParsedModifierKind.Implicit,
                false,
                nameof(PathOfExileTradeStatMatchStatus.ExactEquivalentSet),
                2
            },
            {
                "{0}% of Damage from your Hits cannot be Reflected during Effect",
                "Prevent +<number>% of Reflected Damage during Effect",
                ParsedModifierKind.Suffix,
                true,
                nameof(PathOfExileTradeStatMatchStatus.Exact),
                1
            },
            {
                "{0}% of Hit Damage from your Minions cannot be Reflected",
                "Minions prevent +<number>% of Reflected Damage they would take",
                ParsedModifierKind.Implicit,
                false,
                nameof(PathOfExileTradeStatMatchStatus.Exact),
                1
            },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Match_HistoricalRenderingUsesCurrentCanonicalProviderIdentity(
        string historicalRendering,
        string currentCanonicalSignature,
        ParsedModifierKind kind,
        bool isCrafted,
        string expectedStatus,
        int expectedCandidateCount)
    {
        var providerText = currentCanonicalSignature.Replace("<number>", "#", StringComparison.Ordinal);
        var providerKind = isCrafted
            ? "crafted"
            : kind == ParsedModifierKind.Implicit
                ? "implicit"
                : "explicit";
        var entries = Enumerable.Range(0, expectedCandidateCount)
            .Select(index => Entry($"{providerKind}.fixture_{index}", providerText, providerKind, index))
            .ToArray();
        var component = new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            SourceModifierIndex = 0,
            SourceLineIndex = 0,
            SourceComponentIndex = 0,
            OriginalText = historicalRendering.Replace("{0}", "10", StringComparison.Ordinal),
            CanonicalSignature = currentCanonicalSignature,
            ProviderCanonicalSignature = currentCanonicalSignature,
            ParsedKind = kind,
            IsCrafted = isCrafted,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            ResolvedModifierId = "repoe.current.modifier",
            ResolvedStatIds = ["repoe.current.stat"],
            StatMappingProof = ModifierStatMappingProofStatus.WholeVector,
            IsSearchable = true,
        };

        var match = new PathOfExileTradeStatMatcher().Match(
            component,
            new PathOfExileTradeStatCatalog(entries));

        Assert.Equal(expectedStatus, match.Status.ToString());
        Assert.Equal(expectedCandidateCount, ExactCandidates(match).Count);
        Assert.All(ExactCandidates(match), candidate => Assert.Equal(providerKind, candidate.ProviderKind));
    }

    private static IReadOnlyList<PathOfExileTradeStatMatchCandidate> ExactCandidates(
        PathOfExileTradeStatMatchResult match) => match.Status switch
        {
            PathOfExileTradeStatMatchStatus.Exact when match.ExactCandidate is not null =>
                [match.ExactCandidate],
            PathOfExileTradeStatMatchStatus.ExactEquivalentSet => match.ExactEquivalentCandidates,
            _ => [],
        };

    private static PathOfExileTradeStatEntry Entry(
        string id,
        string text,
        string type,
        int order) => new()
        {
            ProviderOrder = order,
            GroupId = type,
            GroupLabel = type,
            Id = id,
            Text = text,
            Type = type,
        };
}
