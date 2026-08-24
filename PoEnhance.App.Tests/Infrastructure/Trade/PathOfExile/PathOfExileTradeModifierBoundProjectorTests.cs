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

    [Fact]
    public void Project_FixedLiteralProviderTextIsPresenceOnly()
    {
        var result = PathOfExileTradeModifierBoundProjector.Project(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:0:0",
                ValueBoundShape = ModifierBoundShape.Scalar,
                SupportsValueBounds = true,
                ObservedNumericValues = [3m],
                CanonicalNumericValues = [3m],
                ProviderSearchSignatures = ["Has 1 Socket", "Has <number> Sockets"],
                RequestedMinimum = 3m,
            },
            Candidate("Has 1 Socket"));

        Assert.False(result.SupportsValueBounds);
        Assert.Equal(ModifierBoundShape.PresenceOnly, result.ValueBoundShape);
        Assert.Null(result.RequestedMinimum);
        Assert.Null(result.RequestedMaximum);
    }

    [Fact]
    public void ProjectBounds_CanonicalNegatedScalarIsNotNegatedTwice()
    {
        var result = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:0:0",
                CanonicalSignature = "<number>% reduced Charges per use",
                ProviderCanonicalSignature = "<number>% reduced Charges per use",
                ValueBoundShape = ModifierBoundShape.Scalar,
                SupportsValueBounds = true,
                ObservedNumericValues = [14m],
                CanonicalNumericValues = [-14m],
                ValueBoundTranslationHandlers = [["negate"]],
                DefaultBoundDirection = ModifierBoundDirection.Maximum,
                RequestedMaximum = -14m,
            },
            Candidate("#% increased Charges per use"));

        Assert.True(result.IsFaithful);
        Assert.Null(result.Minimum);
        Assert.Equal(-14m, result.Maximum);
        Assert.Equal("CanonicalNegatedScalar", result.ProjectionKind);
    }

    [Fact]
    public void ProjectBounds_NonEditableFixedQueryValueUsesExactParametricConstraint()
    {
        var result = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:0:0",
                CanonicalSignature =
                    "Socketed Gems are Supported by Level <number> Test Support",
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                SupportsValueBounds = false,
                ObservedNumericValues = [10m],
                CanonicalNumericValues = [10m],
                FixedQueryValue = 10m,
            },
            Candidate("Socketed Gems are Supported by Level # Test Support"));

        Assert.True(result.IsFaithful);
        Assert.Equal(ModifierBoundShape.Scalar, result.ValueBoundShape);
        Assert.Equal(10m, result.Minimum);
        Assert.Equal(10m, result.Maximum);
        Assert.Equal("FixedNumericQueryConstraint", result.ProjectionKind);
    }

    [Fact]
    public void ProjectBounds_FixedPresenceLookupBridgeDoesNotInventNumericBounds()
    {
        var result = PathOfExileTradeModifierBoundProjector.ProjectBounds(
            new ResolvedSearchComponent
            {
                ComponentId = "modifier:0:0",
                CanonicalSignature = "You can apply an additional Curse",
                ProviderCanonicalSignature = "You can apply an additional Curse",
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                SupportsValueBounds = false,
                ProviderFallbackNumericValues = [1m],
            },
            Candidate("You can apply # additional Curses"));

        Assert.True(result.IsFaithful);
        Assert.Equal(ModifierBoundShape.PresenceOnly, result.ValueBoundShape);
        Assert.Null(result.Minimum);
        Assert.Null(result.Maximum);
        Assert.Equal("FixedPresenceIdentity", result.ProjectionKind);
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
