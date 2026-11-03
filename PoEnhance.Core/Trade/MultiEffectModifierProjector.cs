using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Trade;

/// <summary>
/// Projects approved composite single-line modifiers (for example Fire and Chaos Resistances) into
/// logical effect components before canonical aggregation, so each logical effect aggregates with
/// its ordinary single-effect siblings while every projection still points at the one physical
/// affix. Composition is decided only by RePoE stat ids through
/// <see cref="ApprovedMultiEffectProjectionCatalog"/>.
/// </summary>
public static class MultiEffectModifierProjector
{
    public static IReadOnlyList<ResolvedSearchComponent> Project(
        IReadOnlyList<ResolvedSearchComponent> components,
        GameDataCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(components);

        _ = catalog; // Reserved for future catalog-backed signature verification; the table is stat-id keyed.
        if (!components.Any(IsProjectionCandidate))
        {
            return components;
        }

        var nextComponentIndexByModifier = components
            .Where(component => component.SourceModifierIndex >= 0)
            .GroupBy(component => component.SourceModifierIndex)
            .ToDictionary(
                group => group.Key,
                group => group.Max(component => component.SourceComponentIndex) + 1);
        var result = new List<ResolvedSearchComponent>(components.Count + 2);
        foreach (var component in components)
        {
            if (!IsProjectionCandidate(component) ||
                (!ApprovedMultiEffectProjectionCatalog.TryGet(component.ResolvedStatIds, out var projection) &&
                    !ApprovedMultiEffectProjectionCatalog.TryGetByCompositeCanonicalSignature(
                        component.CanonicalSignature,
                        out projection)))
            {
                result.Add(component);
                continue;
            }

            var physicalIdentity = CreatePhysicalSourceIdentity(component);
            switch (projection.Kind)
            {
                case MultiEffectProjectionKind.SplitIntoGroups:
                    result.AddRange(Split(
                        component,
                        projection,
                        physicalIdentity,
                        nextComponentIndexByModifier));
                    break;
                case MultiEffectProjectionKind.CompactWithLogicalChildren:
                    result.Add(Compact(component, projection, physicalIdentity));
                    break;
                default:
                    result.Add(component);
                    break;
            }
        }

        return result;
    }

    internal static string CreatePhysicalSourceIdentity(ResolvedSearchComponent component) =>
        $"phys:{component.SourceModifierIndex}:{component.SourceLineIndex}:{component.ComponentId}";

    private static bool IsProjectionCandidate(ResolvedSearchComponent component)
    {
        if (!string.IsNullOrEmpty(component.PhysicalSourceIdentity) ||
            component.ResolutionStatus != ModifierCandidateResolutionStatus.Exact ||
            !component.IsSearchable ||
            component.IsBaseImplicit ||
            component.IsVeiled ||
            component.HasResolvedUniqueSourceSemantics ||
            component.ValueBoundShape != ModifierBoundShape.Scalar ||
            component.ObservedNumericValues.Count != 1 ||
            CanonicalValues(component).Count != 1)
        {
            return false;
        }

        // Explicit, Crafted, Fractured and Implicit one-line Group A composites all project so
        // Pseudo totals and independent logical selection include base implicits (e.g. Two-Stone
        // Fire+Lightning). Unique and Enchant composites keep established single-row semantics.
        // Crafted must not be blocked merely because IsCrafted/provider domain is Crafted —
        // physical provenance stays on Sources while logical rows clear Crafted for discovery.
        if (CanonicalModifierEffectAggregator.ProviderDomainFor(component) is not
            ("Explicit" or "Crafted" or "Fractured" or "Implicit"))
        {
            return false;
        }

        // Prefer a single resolved RePoE id. Empty / atypical id vectors still qualify when the
        // composite CanonicalSignature matches the approved table (Crafted ranks, Soften paths).
        if (component.ResolvedStatIds.Count == 1)
        {
            return true;
        }

        return component.ResolvedStatIds.Count == 0 &&
            ApprovedMultiEffectProjectionCatalog.TryGetByCompositeCanonicalSignature(
                component.CanonicalSignature,
                out _);
    }

    private static IReadOnlyList<decimal> CanonicalValues(ResolvedSearchComponent component) =>
        component.CanonicalNumericValues.Count > 0
            ? component.CanonicalNumericValues
            : component.ObservedNumericValues;

    private static IEnumerable<ResolvedSearchComponent> Split(
        ResolvedSearchComponent component,
        ApprovedMultiEffectProjection projection,
        string physicalIdentity,
        Dictionary<int, int> nextComponentIndexByModifier)
    {
        // Physical provenance keeps the composite text/stat vector for display and counting.
        // Logical components intentionally clear ProviderCanonicalSignature so Trade discovery
        // matches the logical effect (e.g. Fire Resistance → Pseudo total Fire), not the
        // combined exact affix. Selection is independent per logical criterion (03.11A).
        var physicalSources = (component.Sources.Count > 0
                ? component.Sources
                : [CanonicalModifierEffectAggregator.CreateSourceProvenance(component)])
            .Select(source => source with { PhysicalSourceIdentity = physicalIdentity })
            .ToArray();
        var canonicalValues = CanonicalValues(component).ToArray();

        for (var targetIndex = 0; targetIndex < projection.Targets.Count; targetIndex++)
        {
            var target = projection.Targets[targetIndex];
            var componentIndex = component.SourceComponentIndex;
            if (targetIndex > 0)
            {
                componentIndex = component.SourceModifierIndex >= 0
                    ? nextComponentIndexByModifier[component.SourceModifierIndex]++
                    : component.SourceComponentIndex + targetIndex;
            }

            var text = CanonicalModifierEffectAggregator.RenderAggregateText(
                target.CanonicalSignature,
                component.ObservedNumericValues);
            var logicalIdentity = $"{physicalIdentity}:logic:{target.StatId}";
            yield return component with
            {
                ComponentId = targetIndex == 0
                    ? component.ComponentId
                    : $"{component.ComponentId}:split:{target.StatId}",
                SourceComponentIndex = componentIndex,
                OriginalText = text,
                PresentationText = text,
                CanonicalSignature = target.CanonicalSignature,
                ResolvedStatIds = [target.StatId],
                CanonicalNumericValues = canonicalValues,
                // Discover variants for the logical effect, not the composite exact affix.
                ProviderCanonicalSignature = null,
                ProviderSearchSignatures = [],
                FilterVariants = [],
                SelectedFilterVariantIdentity = null,
                RequestedFilterVariantIdentity = null,
                RequestedFilterVariantKind = null,
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.NotResolved,
                ProviderStatId = null,
                ProviderStatText = null,
                ProviderStatAlternativeIds = [],
                ProviderCandidateStatIds = [],
                ProviderDiagnosticCode = null,
                ProviderDiagnosticMessage = null,
                Sources = physicalSources,
                PhysicalSourceIdentity = logicalIdentity,
                // Logical criteria search catalog variants for the effect, not the composite's
                // Crafted/Fractured/Implicit provider kind (Sources keep physical provenance).
                IsCrafted = false,
                IsFractured = false,
                IsVeiled = false,
                ParsedKind = ParsedModifierKind.Suffix,
                GenerationType = ModifierGenerationType.Suffix,
                // Global resistances/attributes are never reviewed local item-property sources.
                ReviewedItemPropertySemantic = null,
            };
        }
    }

    private static ResolvedSearchComponent Compact(
        ResolvedSearchComponent component,
        ApprovedMultiEffectProjection projection,
        string physicalIdentity)
    {
        var canonicalValues = CanonicalValues(component).ToArray();
        return component with
        {
            PhysicalSourceIdentity = physicalIdentity,
            LogicalContributions = projection.Targets
                .Select(target => new SearchComponentLogicalContribution
                {
                    ContributionId = $"{physicalIdentity}:{target.StatId}",
                    LogicalStatId = target.StatId,
                    DisplayText = CanonicalModifierEffectAggregator.RenderAggregateText(
                        target.CanonicalSignature,
                        component.ObservedNumericValues),
                    CanonicalSignature = target.CanonicalSignature,
                    // Shared physical provenance, distinct logical criterion identity.
                    PhysicalSourceIdentity = $"{physicalIdentity}:logic:{target.StatId}",
                    CanonicalNumericValues = canonicalValues,
                })
                .ToArray(),
        };
    }
}
