using PoEnhance.Core.Items.Derived;
using PoEnhance.Core.Items.GameData;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

internal static class DerivedWeaponModifierEffectProjector
{
    public static IReadOnlyList<DerivedWeaponModifierEffect> Project(
        IReadOnlyList<ResolvedSearchComponent> components)
    {
        return components.SelectMany(ProjectComponent).ToArray();
    }

    public static IReadOnlyList<DerivedWeaponModifierEffect> ProjectSourcesIndependently(
        IReadOnlyList<ResolvedSearchComponent> components)
    {
        return components
            .SelectMany(component => component.Sources.Count > 0
                ? component.Sources.Select(FromSource)
                : [FromComponent(component)])
            .ToArray();
    }

    private static IEnumerable<DerivedWeaponModifierEffect> ProjectComponent(
        ResolvedSearchComponent component)
    {
        if (component.Sources.Count == 0)
        {
            return [FromComponent(component)];
        }

        if (!component.IsEquivalentSourceSet)
        {
            return component.Sources.Select(FromSource);
        }

        var canonical = FromComponent(component);
        if (component.Sources
            .Select(FromSource)
            .All(source => HasSameCanonicalMechanics(canonical, source)))
        {
            return [canonical];
        }

        return
        [
            canonical with
            {
                CanonicalizationUnsupportedReason =
                    $"Equivalent source alternatives for modifier component '{component.ComponentId}' " +
                    "do not prove one identical canonical derived-property mechanic and value.",
            },
        ];
    }

    private static DerivedWeaponModifierEffect FromComponent(ResolvedSearchComponent component) =>
        new()
        {
            ComponentId = component.ComponentId,
            SourceModifierIndex = component.SourceModifierIndex,
            ResolvedModifierId = component.ResolvedModifierId,
            IsExactlyResolved = component.ResolutionStatus == ModifierCandidateResolutionStatus.Exact,
            IsLocal = component.Locality == ModifierLocality.Local,
            HasProvenStatAssociation = component.StatMappingProof is
                ModifierStatMappingProofStatus.ProvenExact or
                ModifierStatMappingProofStatus.WholeVector,
            UsesPositionalFallback = component.StatMappingProof ==
                ModifierStatMappingProofStatus.PositionalFallback,
            ResolvedStatIds = component.ResolvedStatIds,
            CanonicalNumericValues = CanonicalValues(
                component.CanonicalNumericValues,
                component.ObservedNumericValues),
            ReviewedItemPropertySemantic = component.ReviewedItemPropertySemantic,
        };

    private static DerivedWeaponModifierEffect FromSource(SearchComponentSourceProvenance source) =>
        new()
        {
            ComponentId = source.ComponentId,
            SourceModifierIndex = source.SourceModifierIndex,
            ResolvedModifierId = source.ResolvedModifierId,
            IsExactlyResolved = !string.IsNullOrWhiteSpace(source.ResolvedModifierId),
            IsLocal = source.Locality == ModifierLocality.Local,
            HasProvenStatAssociation = source.StatMappingProof is
                ModifierStatMappingProofStatus.ProvenExact or
                ModifierStatMappingProofStatus.WholeVector,
            UsesPositionalFallback = source.StatMappingProof ==
                ModifierStatMappingProofStatus.PositionalFallback,
            ResolvedStatIds = source.ResolvedStatIds,
            CanonicalNumericValues = CanonicalValues(
                source.CanonicalNumericValues,
                source.ObservedNumericValues),
            ReviewedItemPropertySemantic = source.ReviewedItemPropertySemantic,
        };

    private static IReadOnlyList<decimal> CanonicalValues(
        IReadOnlyList<decimal> canonical,
        IReadOnlyList<decimal> observed) => canonical.Count > 0 ? canonical : observed;

    private static bool HasSameCanonicalMechanics(
        DerivedWeaponModifierEffect canonical,
        DerivedWeaponModifierEffect alternative)
    {
        return canonical.IsExactlyResolved == alternative.IsExactlyResolved &&
            canonical.IsLocal == alternative.IsLocal &&
            canonical.HasProvenStatAssociation == alternative.HasProvenStatAssociation &&
            canonical.UsesPositionalFallback == alternative.UsesPositionalFallback &&
            canonical.ResolvedStatIds.SequenceEqual(alternative.ResolvedStatIds, StringComparer.Ordinal) &&
            canonical.CanonicalNumericValues.SequenceEqual(alternative.CanonicalNumericValues) &&
            HasSameReviewedMechanics(
                canonical.ReviewedItemPropertySemantic,
                alternative.ReviewedItemPropertySemantic);
    }

    private static bool HasSameReviewedMechanics(
        ItemPropertySemanticDescriptor? canonical,
        ItemPropertySemanticDescriptor? alternative)
    {
        if (canonical is null || alternative is null)
        {
            return canonical is null && alternative is null;
        }

        return canonical.Applicability == alternative.Applicability &&
            canonical.OrderedStatIds.SequenceEqual(alternative.OrderedStatIds, StringComparer.Ordinal) &&
            canonical.Contributions.Count == alternative.Contributions.Count &&
            canonical.Contributions.Zip(alternative.Contributions).All(pair =>
                pair.First.Operation == pair.Second.Operation &&
                pair.First.Targets.SequenceEqual(pair.Second.Targets));
    }
}
