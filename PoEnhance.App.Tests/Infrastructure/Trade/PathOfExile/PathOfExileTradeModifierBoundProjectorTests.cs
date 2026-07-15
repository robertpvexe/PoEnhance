using PoEnhance.App.Infrastructure.Trade.PathOfExile;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Infrastructure.Trade.PathOfExile;

public sealed class PathOfExileTradeModifierBoundProjectorTests
{
    [Fact]
    public void Project_ProvenTwoValueTradeDamageStatEnablesArithmeticMeanBounds()
    {
        var result = PathOfExileTradeModifierBoundProjector.Project(
            DamageRangeComponent([14m, 25m]),
            Candidate("Adds # to # Test Damage (Local)"));

        Assert.True(result.SupportsValueBounds);
        Assert.Equal(19.5m, result.RequestedMinimum);
        Assert.Null(result.RequestedMaximum);
        Assert.Null(result.ValueBoundsUnsupportedReason);
    }

    [Theory]
    [InlineData("Adds # Test Damage")]
    [InlineData("Test Damage is present")]
    [InlineData("Adds # to # to # Test Damage")]
    public void Project_ProviderArityThatDoesNotConfirmRangeRemainsUnsupported(string providerText)
    {
        var result = PathOfExileTradeModifierBoundProjector.Project(
            DamageRangeComponent([14m, 25m]),
            Candidate(providerText));

        Assert.False(result.SupportsValueBounds);
        Assert.Null(result.RequestedMinimum);
        Assert.Null(result.RequestedMaximum);
        Assert.Contains("does not expose the same two-value range", result.ValueBoundsUnsupportedReason);
    }

    [Fact]
    public void Project_ProviderPresenceStatIsExplicitlyClassifiedWithoutNumericBounds()
    {
        var result = PathOfExileTradeModifierBoundProjector.Project(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:0:0",
                ValueBoundShape = ModifierBoundShape.Unsupported,
                SupportsValueBounds = false,
            },
            Candidate("Test effect is present"));

        Assert.False(result.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.PresenceOnly, result.ValueBoundShape);
        Assert.Contains("presence-only", result.ValueBoundsUnsupportedReason);
    }

    private static ResolvedSearchComponent DamageRangeComponent(IReadOnlyList<decimal> values)
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            ValueBoundShape = ModifierBoundShape.ArithmeticMeanRange,
            ObservedNumericValues = values,
            SupportsValueBounds = false,
            ValueBoundsUnsupportedReason = "Provider confirmation required.",
        };
    }

    private static PathOfExileTradeStatMatchCandidate Candidate(string text)
    {
        return PathOfExileTradeStatCandidateClassifier.ToCandidate(new PathOfExileTradeStatEntry
        {
            ProviderOrder = 0,
            GroupId = "explicit",
            GroupLabel = "Explicit",
            Id = "explicit.stat_test",
            Text = text,
            Type = "explicit",
        });
    }
}
