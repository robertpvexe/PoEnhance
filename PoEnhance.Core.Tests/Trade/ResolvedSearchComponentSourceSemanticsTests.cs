using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.Core.Tests.Trade;

public sealed class ResolvedSearchComponentSourceSemanticsTests
{
    [Fact]
    public void ExactIdentityBoundRecoveryExposesResolvedUniqueSemanticsWithoutChangingRawTruth()
    {
        var component = ExactRecoveredUnique();

        Assert.Equal(ParsedModifierKind.Unknown, component.ParsedKind);
        Assert.Equal(ParsedUniqueModifierOrigin.Unspecified, component.UniqueOrigin);
        Assert.Equal(ParsedModifierKind.Unique, component.ResolvedSourceKind);
        Assert.Equal(ParsedUniqueModifierOrigin.Ordinary, component.ResolvedSourceUniqueOrigin);
        Assert.True(component.HasResolvedUniqueSourceSemantics);
        Assert.True(component.HasExactUniqueSourceProvenance);
        Assert.Equal("Unique", CanonicalModifierEffectAggregator.ProviderDomainFor(component));

        var source = CanonicalModifierEffectAggregator.CreateSourceProvenance(component);
        Assert.Equal(ParsedModifierKind.Unknown, source.ParsedKind);
        Assert.Equal(ParsedUniqueModifierOrigin.Unspecified, source.UniqueOrigin);
        Assert.Equal(ParsedModifierKind.Unique, source.ResolvedSourceKind);
        Assert.Equal(ParsedUniqueModifierOrigin.Ordinary, source.ResolvedSourceUniqueOrigin);
    }

    [Fact]
    public void RecoverySemanticsRemainFailClosedWithoutEveryExactProofGate()
    {
        var exact = ExactRecoveredUnique();
        var cases = new[]
        {
            exact with { UsesIdentityBoundUniqueRecovery = false },
            exact with { ResolutionStatus = ModifierCandidateResolutionStatus.Unknown },
            exact with { UniqueCatalogBlockIds = [] },
            exact with { UniqueSourceObservationIds = [] },
            exact with { UniqueResolutionDiagnosticCode = "UNIQUE_BLOCK_INDEPENDENT_DIMENSIONS" },
            exact with { ResolvedStatIds = [] },
            exact with { RecoveredSourceUniqueOrigin = null },
        };

        Assert.All(cases, component =>
        {
            Assert.Equal(ParsedModifierKind.Unknown, component.ResolvedSourceKind);
            Assert.Equal(ParsedUniqueModifierOrigin.Unspecified, component.ResolvedSourceUniqueOrigin);
            Assert.False(component.HasResolvedUniqueSourceSemantics);
            Assert.False(component.HasExactUniqueSourceProvenance);
            Assert.Equal("Unknown", CanonicalModifierEffectAggregator.ProviderDomainFor(component));
        });
    }

    [Theory]
    [InlineData(ParsedModifierKind.Unique, ParsedUniqueModifierOrigin.Ordinary, true)]
    [InlineData(ParsedModifierKind.Unique, ParsedUniqueModifierOrigin.Foulborn, true)]
    [InlineData(ParsedModifierKind.Implicit, ParsedUniqueModifierOrigin.Unspecified, false)]
    [InlineData(ParsedModifierKind.Prefix, ParsedUniqueModifierOrigin.Unspecified, false)]
    [InlineData(ParsedModifierKind.Suffix, ParsedUniqueModifierOrigin.Unspecified, false)]
    public void NormalRecognizedKindsKeepTheirRawSemantics(
        ParsedModifierKind kind,
        ParsedUniqueModifierOrigin origin,
        bool isUnique)
    {
        var component = new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            ParsedKind = kind,
            UniqueOrigin = origin,
            RecoveredSourceKind = ParsedModifierKind.Unique,
            RecoveredSourceUniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            UsesIdentityBoundUniqueRecovery = true,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            UniqueCatalogBlockIds = ["block:test"],
            UniqueSourceObservationIds = ["observation:test"],
            ResolvedStatIds = ["stat:test"],
        };

        Assert.Equal(kind, component.ResolvedSourceKind);
        Assert.Equal(origin, component.ResolvedSourceUniqueOrigin);
        Assert.Equal(isUnique, component.HasResolvedUniqueSourceSemantics);
    }

    private static ResolvedSearchComponent ExactRecoveredUnique()
    {
        return new ResolvedSearchComponent
        {
            ComponentId = "modifier:0:0",
            ParsedKind = ParsedModifierKind.Unknown,
            UniqueOrigin = ParsedUniqueModifierOrigin.Unspecified,
            UsesIdentityBoundUniqueRecovery = true,
            RecoveredSourceKind = ParsedModifierKind.Unique,
            RecoveredSourceUniqueOrigin = ParsedUniqueModifierOrigin.Ordinary,
            ResolutionStatus = ModifierCandidateResolutionStatus.Exact,
            UniqueCatalogBlockIds = ["block:test"],
            UniqueSourceObservationIds = ["observation:test"],
            ResolvedStatIds = ["stat:test"],
            IsSearchable = true,
        };
    }
}
