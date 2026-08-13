using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeSelectedModifierMapper : IPathOfExileTradeSelectedModifierMapper
{
    public PathOfExileTradeSelectedModifierMappingResult Map(
        TradeSearchDraft? draft,
        PathOfExileTradeStatCatalog? catalog = null)
    {
        var selectedModifiers = (draft?.ModifierFilters ?? [])
            .Select((modifier, index) => new IndexedModifier(index, modifier))
            .Where(indexed => indexed.Modifier.IsSelected)
            .ToArray();

        if (selectedModifiers.Length == 0)
        {
            return PathOfExileTradeSelectedModifierMappingResult.Success([]);
        }

        var filters = new List<PathOfExileTradeSelectedModifierFilter>();
        var contributorFilters = new List<PathOfExileTradeSelectedModifierFilter>();
        var diagnostics = new List<PathOfExileTradeSelectedModifierMappingDiagnostic>();
        foreach (var selectedModifier in selectedModifiers)
        {
            AddActiveContributorFilters(
                selectedModifier.Index,
                selectedModifier.Modifier,
                catalog,
                contributorFilters,
                diagnostics);

            if (TryCreateResolvedProviderFilter(
                    selectedModifier.Index,
                    selectedModifier.Modifier,
                    catalog,
                    out var resolvedFilter,
                    out var resolvedDiagnostic))
            {
                if (resolvedFilter is not null)
                {
                    filters.Add(resolvedFilter);
                }

                continue;
            }

            if (selectedModifier.Modifier.ProviderResolutionStatus is
                    SearchComponentProviderResolutionStatus.Exact or
                    SearchComponentProviderResolutionStatus.ExactEquivalentSet or
                    SearchComponentProviderResolutionStatus.Approximate &&
                !CanSerializeProviderResolvedComponent(selectedModifier.Modifier))
            {
                diagnostics.Add(new PathOfExileTradeSelectedModifierMappingDiagnostic(
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
                    MessageFor(
                        PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
                        selectedModifier.Modifier.OriginalText),
                    selectedModifier.Index));
                continue;
            }

            if (selectedModifier.Modifier.ProviderResolutionStatus !=
                SearchComponentProviderResolutionStatus.NotResolved)
            {
                diagnostics.Add(resolvedDiagnostic ??
                    ToProviderResolutionDiagnostic(selectedModifier.Index, selectedModifier.Modifier));
                continue;
            }

            if (!CanSerializeSelectedComponent(selectedModifier.Modifier))
            {
                diagnostics.Add(new PathOfExileTradeSelectedModifierMappingDiagnostic(
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
                    MessageFor(
                        PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
                        selectedModifier.Modifier.OriginalText),
                    selectedModifier.Index));
                continue;
            }

            diagnostics.Add(resolvedDiagnostic ??
                ToProviderResolutionDiagnostic(selectedModifier.Index, selectedModifier.Modifier));
        }

        filters.AddRange(contributorFilters);
        var collapsedFilters = CollapseSharedPresenceFilters(filters, diagnostics);
        var result = diagnostics.Count == 0
            ? PathOfExileTradeSelectedModifierMappingResult.Success(collapsedFilters)
            : PathOfExileTradeSelectedModifierMappingResult.Failure(diagnostics);
        return result;
    }

    private static void AddActiveContributorFilters(
        int parentIndex,
        ResolvedSearchComponent parent,
        PathOfExileTradeStatCatalog? catalog,
        List<PathOfExileTradeSelectedModifierFilter> filters,
        List<PathOfExileTradeSelectedModifierMappingDiagnostic> diagnostics)
    {
        if (!SearchComponentContributorActivation.IsFilteringActive(parent))
        {
            return;
        }

        var selected = parent.Contributors
            .Where(contributor => contributor.IsSelected)
            .ToArray();
        var parentProviderIdentity = string.IsNullOrWhiteSpace(parent.ProviderStatId)
            ? null
            : PathOfExileTradeProviderIdentity.Create(parent.ProviderStatId);
        var resolved = new List<ResolvedContributor>(selected.Length);
        foreach (var contributor in selected)
        {
            PathOfExileTradeProviderLocalityDecision? localityDecision = null;
            PathOfExileTradeStatEntry? providerEntry = null;
            if (contributor.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact &&
                !string.IsNullOrWhiteSpace(contributor.ProviderIdentity) &&
                catalog is not null &&
                catalog.TryGetByProviderIdentity(contributor.ProviderIdentity, out var retainedEntry))
            {
                providerEntry = retainedEntry;
                localityDecision = PathOfExileTradeProviderLocalityCompatibility
                    .EvaluateRetainedSourceIdentity(
                        contributor.Source,
                        PathOfExileTradeStatCandidateClassifier.ToCandidate(retainedEntry));
            }

            if (contributor.ProviderResolutionStatus != SearchComponentProviderResolutionStatus.Exact ||
                string.IsNullOrWhiteSpace(contributor.ProviderIdentity) ||
                providerEntry is null ||
                localityDecision?.IsCompatible != true)
            {
                var localityAmbiguous = localityDecision?.Status ==
                    PathOfExileTradeProviderLocalityDecisionStatus.Ambiguous;
                diagnostics.Add(new PathOfExileTradeSelectedModifierMappingDiagnostic(
                    contributor.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Ambiguous ||
                        localityAmbiguous
                        ? PathOfExileTradeSelectedModifierMappingDiagnosticCodes.ContributorSourceIdentityAmbiguous
                        : PathOfExileTradeSelectedModifierMappingDiagnosticCodes.ContributorSourceIdentityUnavailable,
                    localityDecision?.Reason ?? contributor.ProviderDiagnosticMessage ??
                        $"Selected contributor has no exact retained source provider identity: {SafeModifierText(contributor.Source.OriginalText)}",
                    parentIndex,
                    localityDecision?.ReasonCode ?? contributor.ProviderDiagnosticCode));
                continue;
            }

            resolved.Add(new ResolvedContributor(contributor, providerEntry));
        }

        foreach (var providerGroup in resolved.GroupBy(
                     contributor => contributor.Contributor.ProviderIdentity!,
                     StringComparer.Ordinal))
        {
            var compatibilityGroups = providerGroup
                .GroupBy(contributor => ContributorCompatibilityKey.Create(parent, contributor.Contributor))
                .ToArray();
            if (compatibilityGroups.Length != 1)
            {
                diagnostics.Add(new PathOfExileTradeSelectedModifierMappingDiagnostic(
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.ContributorBoundsIncompatible,
                    "Selected contributors sharing one retained provider identity have incompatible source semantics and cannot be combined faithfully.",
                    parentIndex));
                continue;
            }

            var compatible = compatibilityGroups[0]
                .Select(contributor => contributor.Contributor)
                .ToArray();
            if (parent.ContributorProjection != SearchComponentContributorProjection.Additive ||
                compatible.Any(contributor => !contributor.RequestedMinimum.HasValue))
            {
                diagnostics.Add(new PathOfExileTradeSelectedModifierMappingDiagnostic(
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.ContributorBoundsIncompatible,
                    "Selected contributors sharing one retained provider identity do not have complete additive Min values.",
                    parentIndex));
                continue;
            }

            var maximumCount = compatible.Count(contributor => contributor.RequestedMaximum.HasValue);
            if (maximumCount != 0 && maximumCount != compatible.Length)
            {
                diagnostics.Add(new PathOfExileTradeSelectedModifierMappingDiagnostic(
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.ContributorBoundsIncompatible,
                    "Selected contributors sharing one retained provider identity have only partially specified Max values.",
                    parentIndex));
                continue;
            }

            if (string.Equals(
                    providerGroup.Key,
                    parentProviderIdentity,
                    StringComparison.Ordinal))
            {
                continue;
            }

            filters.Add(new PathOfExileTradeSelectedModifierFilter
            {
                SourceIndex = parentIndex,
                SourceIndexes = [parentIndex],
                StatId = providerGroup.First().ProviderEntry.Id,
                OriginalText = string.Join(" + ", compatible.Select(contributor => contributor.Source.OriginalText)),
                NormalizedItemTemplate = ToProviderTemplate(compatible[0].Source.CanonicalSignature),
                ExtractedNumericValues = [],
                Minimum = compatible.Sum(contributor => contributor.RequestedMinimum!.Value),
                Maximum = maximumCount == compatible.Length
                    ? compatible.Sum(contributor => contributor.RequestedMaximum!.Value)
                    : null,
            });
        }
    }

    private static bool TryCreateResolvedProviderFilter(
        int sourceIndex,
        ResolvedSearchComponent modifier,
        PathOfExileTradeStatCatalog? catalog,
        out PathOfExileTradeSelectedModifierFilter? filter,
        out PathOfExileTradeSelectedModifierMappingDiagnostic? diagnostic)
    {
        filter = null;
        diagnostic = null;

        if (modifier.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.BaseGuaranteed)
        {
            return true;
        }

        if (modifier.ProviderResolutionStatus is
                SearchComponentProviderResolutionStatus.Exact or
                SearchComponentProviderResolutionStatus.ExactEquivalentSet or
                SearchComponentProviderResolutionStatus.Approximate &&
            CanSerializeProviderResolvedComponent(modifier))
        {
            var providerStatIds = SelectedProviderStatIds(modifier);
            if (providerStatIds.Count == 0)
            {
                diagnostic = ToProviderResolutionDiagnostic(sourceIndex, modifier);
                return false;
            }

            if (!TryVerifySpecialProviderKind(sourceIndex, modifier, catalog, out diagnostic))
            {
                return false;
            }

            if (!TryVerifyReviewedLocalProviderScope(
                    sourceIndex,
                    modifier,
                    catalog,
                    out diagnostic))
            {
                return false;
            }

            var alternatives = new List<PathOfExileTradeSelectedModifierFilterAlternative>(
                providerStatIds.Count);
            foreach (var providerStatId in providerStatIds)
            {
                var minimum = modifier.SupportsValueBounds ? modifier.RequestedMinimum : null;
                var maximum = modifier.SupportsValueBounds ? modifier.RequestedMaximum : null;
                if (catalog is not null &&
                    catalog.TryGetById(providerStatId, out var entry))
                {
                    var projected = PathOfExileTradeModifierBoundProjector.ProjectBounds(
                        modifier,
                        PathOfExileTradeStatCandidateClassifier.ToCandidate(entry));
                    if (!projected.IsFaithful)
                    {
                        diagnostic = new PathOfExileTradeSelectedModifierMappingDiagnostic(
                            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.IncompatibleBounds,
                            "The provider alternative has no faithful projection from the selected displayed-value bounds.",
                            sourceIndex);
                        return false;
                    }

                    minimum = projected.Minimum;
                    maximum = projected.Maximum;
                }
                else if (providerStatIds.Count > 1)
                {
                    diagnostic = new PathOfExileTradeSelectedModifierMappingDiagnostic(
                        PathOfExileTradeSelectedModifierMappingDiagnosticCodes.CatalogRequired,
                        "Equivalent provider alternatives require the current Trade stat catalog.",
                        sourceIndex);
                    return false;
                }

                alternatives.Add(new PathOfExileTradeSelectedModifierFilterAlternative
                {
                    StatId = providerStatId,
                    Minimum = minimum,
                    Maximum = maximum,
                });
            }

            filter = new PathOfExileTradeSelectedModifierFilter
            {
                SourceIndex = sourceIndex,
                SourceIndexes = [sourceIndex],
                StatId = providerStatIds[0],
                OriginalText = modifier.OriginalText,
                NormalizedItemTemplate = ToProviderTemplate(modifier.CanonicalSignature),
                ExtractedNumericValues = [],
                Minimum = alternatives[0].Minimum,
                Maximum = alternatives[0].Maximum,
                Alternatives = alternatives.Count > 1 ? alternatives : [],
            };
            return true;
        }

        diagnostic = ToProviderResolutionDiagnostic(sourceIndex, modifier);
        return false;
    }

    private static bool TryVerifySpecialProviderKind(
        int sourceIndex,
        ResolvedSearchComponent modifier,
        PathOfExileTradeStatCatalog? catalog,
        out PathOfExileTradeSelectedModifierMappingDiagnostic? diagnostic)
    {
        diagnostic = null;
        var isCorruptedImplicit = modifier.ParsedKind == ParsedModifierKind.Implicit &&
            modifier.ImplicitOrigin == ParsedImplicitModifierOrigin.Corrupted;
        if (!modifier.IsFractured && !modifier.IsVeiled && !isCorruptedImplicit)
        {
            return true;
        }

        var selectedVariant = modifier.FilterVariants.FirstOrDefault(variant => string.Equals(
            variant.Identity,
            modifier.SelectedFilterVariantIdentity,
            StringComparison.Ordinal));
        var providerStatIds = SelectedProviderStatIds(modifier);
        if (catalog is not null &&
            selectedVariant is not null &&
            providerStatIds.Count > 0)
        {
            var entries = providerStatIds
                .Select(statId => catalog.TryGetById(statId, out var entry) ? entry : null)
                .ToArray();
            if (entries.Any(entry => entry is null))
            {
                goto Failed;
            }

            var candidates = entries
                .Select(entry => PathOfExileTradeStatCandidateClassifier.ToCandidate(entry!))
                .ToArray();
            var providerKinds = candidates
                .Select(PathOfExileTradeStatCandidateClassifier.GetProviderKind)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var identityMatches = string.Equals(
                selectedVariant.Identity,
                PathOfExileTradeModifierVariantResolver.IdentityFor(candidates),
                StringComparison.Ordinal);
            var kindMatches = providerKinds.Length == 1 &&
                string.Equals(
                    selectedVariant.ProviderKind,
                    providerKinds[0],
                    StringComparison.OrdinalIgnoreCase);
            var expectedProviderKind = modifier.IsVeiled
                ? "veiled"
                : isCorruptedImplicit
                    ? "implicit"
                : modifier.ProviderResolutionStatus ==
                    SearchComponentProviderResolutionStatus.Approximate
                    ? "explicit"
                    : string.IsNullOrWhiteSpace(modifier.RequestedFilterVariantKind)
                        ? "fractured"
                        : modifier.RequestedFilterVariantKind.Trim();
            var specialKindMatches = string.Equals(
                providerKinds.SingleOrDefault(),
                expectedProviderKind,
                StringComparison.OrdinalIgnoreCase);
            if (identityMatches && kindMatches && specialKindMatches)
            {
                return true;
            }
        }

    Failed:
        diagnostic = new PathOfExileTradeSelectedModifierMappingDiagnostic(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.KindMismatch,
            $"Selected {(modifier.IsFractured
                ? "Fractured-source"
                : modifier.IsVeiled
                    ? "Veiled"
                    : "Corrupted implicit")} modifier requires one catalog-backed provider identity compatible with its structured resolution quality.",
            sourceIndex);
        return false;
    }

    private static bool TryVerifyReviewedLocalProviderScope(
        int sourceIndex,
        ResolvedSearchComponent modifier,
        PathOfExileTradeStatCatalog? catalog,
        out PathOfExileTradeSelectedModifierMappingDiagnostic? diagnostic)
    {
        diagnostic = null;
        if (modifier.ReviewedItemPropertySemantic?.Applicability !=
            ItemPropertyApplicability.UnconditionalDisplayedLocal)
        {
            return true;
        }

        if (catalog is null)
        {
            diagnostic = new PathOfExileTradeSelectedModifierMappingDiagnostic(
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.CatalogRequired,
                "The current Trade stat catalog is required to verify a selected local displayed-item modifier.",
                sourceIndex);
            return false;
        }

        var providerStatIds = SelectedProviderStatIds(modifier);
        var entries = providerStatIds
            .Select(statId => catalog.TryGetById(statId, out var entry) ? entry : null)
            .ToArray();
        if (entries.Length == 0 || entries.Any(entry => entry is null))
        {
            diagnostic = new PathOfExileTradeSelectedModifierMappingDiagnostic(
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable,
                MessageFor(
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable,
                    modifier.OriginalText),
                sourceIndex);
            return false;
        }

        var localityDecisions = entries
            .Select(entry =>
            {
                var candidate = PathOfExileTradeStatCandidateClassifier.ToCandidate(entry!);
                return PathOfExileTradeProviderLocalityCompatibility.EvaluateVariant(
                    modifier,
                    candidate,
                    candidate);
            })
            .ToArray();
        if (localityDecisions.All(locality => locality.IsCompatible))
        {
            return true;
        }

        var locality = localityDecisions.First(decision => !decision.IsCompatible);
        diagnostic = new PathOfExileTradeSelectedModifierMappingDiagnostic(
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.UnsafeLocalDisplayedProviderScope,
            locality.Reason,
            sourceIndex,
            locality.ReasonCode);
        return false;
    }

    private static IReadOnlyList<PathOfExileTradeSelectedModifierFilter> CollapseSharedPresenceFilters(
        IReadOnlyList<PathOfExileTradeSelectedModifierFilter> filters,
        List<PathOfExileTradeSelectedModifierMappingDiagnostic> diagnostics)
    {
        var equivalentSets = filters
            .Where(filter => filter.Alternatives.Count > 1)
            .ToArray();
        var collapsedSingles = filters
            .Where(filter => filter.Alternatives.Count <= 1)
            .GroupBy(filter => filter.StatId, StringComparer.Ordinal)
            .Select(group =>
            {
                var first = group.First();
                if (group.Any(filter => filter.Minimum != first.Minimum || filter.Maximum != first.Maximum))
                {
                    diagnostics.Add(new PathOfExileTradeSelectedModifierMappingDiagnostic(
                        PathOfExileTradeSelectedModifierMappingDiagnosticCodes.IncompatibleBounds,
                        "Selected modifiers resolve to one Trade stat with incompatible value bounds.",
                        first.SourceIndex));
                }
                return first with
                {
                    SourceIndexes = group
                        .SelectMany(SourceIndexes)
                        .Distinct()
                        .OrderBy(index => index)
                        .ToArray(),
                };
            })
            .ToArray();
        return [.. collapsedSingles, .. equivalentSets];
    }

    private static IEnumerable<int> SourceIndexes(PathOfExileTradeSelectedModifierFilter filter)
    {
        return filter.SourceIndexes.Count > 0
            ? filter.SourceIndexes
            : [filter.SourceIndex];
    }

    private static bool CanSerializeSelectedComponent(ResolvedSearchComponent modifier)
    {
        var hasExactProviderOwnedUniqueProvenance =
            modifier.ParsedKind == ParsedModifierKind.Unique &&
            modifier.UniqueOrigin is ParsedUniqueModifierOrigin.Ordinary or ParsedUniqueModifierOrigin.Foulborn &&
            modifier.StatMappingProof == ModifierStatMappingProofStatus.ProviderExact &&
            modifier.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact &&
            !string.IsNullOrWhiteSpace(modifier.ProviderStatId);
        var hasExactProviderOwnedVeiledPresence = modifier.IsVeiled &&
            modifier.StatMappingProof == ModifierStatMappingProofStatus.ProviderExact &&
            modifier.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact &&
            modifier.IsSearchable &&
            !string.IsNullOrWhiteSpace(modifier.ProviderStatId);
        var hasExactProviderOwnedAdvancedExplicit =
            modifier.ParsedKind is ParsedModifierKind.Prefix or ParsedModifierKind.Suffix &&
            modifier.SourceModifierIndex >= 0 &&
            modifier.SourceLineIndex >= 0 &&
            !modifier.IsFractured &&
            !modifier.IsVeiled &&
            modifier.StatMappingProof == ModifierStatMappingProofStatus.ProviderExact &&
            modifier.ProviderResolutionStatus is SearchComponentProviderResolutionStatus.Exact or
                SearchComponentProviderResolutionStatus.ExactEquivalentSet &&
            modifier.IsSearchable &&
            (!string.IsNullOrWhiteSpace(modifier.ProviderStatId) ||
                modifier.ProviderStatAlternativeIds.Count > 0);
        var hasExactGameDataProvenance = modifier.IsSearchable &&
            modifier.ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
            (!string.IsNullOrWhiteSpace(modifier.ResolvedModifierId) ||
                modifier.ParsedKind == ParsedModifierKind.Unique &&
                (modifier.UniqueCatalogBlockIds.Count > 0 ||
                    modifier.UniqueFoulbornRelationshipIds.Count > 0) &&
                modifier.UniqueSourceObservationIds.Count > 0 &&
                string.IsNullOrWhiteSpace(modifier.UniqueResolutionDiagnosticCode) ||
                modifier.Sources.Count > 0 && modifier.Sources.All(source =>
                    !string.IsNullOrWhiteSpace(source.ResolvedModifierId) &&
                    source.ResolvedStatIds.Count > 0)) &&
            modifier.ResolvedStatIds.Count > 0;
        return hasExactGameDataProvenance || hasExactProviderOwnedUniqueProvenance ||
            hasExactProviderOwnedVeiledPresence ||
            hasExactProviderOwnedAdvancedExplicit ||
            modifier.ParsedKind == ParsedModifierKind.Implicit &&
            modifier.ImplicitOrigin == ParsedImplicitModifierOrigin.Unspecified;
    }

    private static bool CanSerializeProviderResolvedComponent(ResolvedSearchComponent modifier)
    {
        return CanSerializeSelectedComponent(modifier);
    }

    private static IReadOnlyList<string> SelectedProviderStatIds(ResolvedSearchComponent modifier)
    {
        var alternatives = modifier.ProviderStatAlternativeIds
            .Where(statId => !string.IsNullOrWhiteSpace(statId))
            .Select(statId => statId.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (alternatives.Length > 0)
        {
            return alternatives;
        }

        return string.IsNullOrWhiteSpace(modifier.ProviderStatId)
            ? []
            : [modifier.ProviderStatId.Trim()];
    }

    private static PathOfExileTradeSelectedModifierMappingDiagnostic ToProviderResolutionDiagnostic(
        int sourceIndex,
        ResolvedSearchComponent modifier)
    {
        var code = modifier.ProviderResolutionStatus switch
        {
            SearchComponentProviderResolutionStatus.Ambiguous
                when modifier.ProviderDiagnosticCode ==
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageAmbiguous =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageAmbiguous,
            SearchComponentProviderResolutionStatus.Ambiguous =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous,
            SearchComponentProviderResolutionStatus.NotFound
                when modifier.ProviderDiagnosticCode ==
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable,
            SearchComponentProviderResolutionStatus.Unsupported
                when modifier.ProviderDiagnosticCode ==
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable,
            SearchComponentProviderResolutionStatus.Unsupported
                when modifier.ProviderDiagnosticCode ==
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageUnavailable =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageUnavailable,
            SearchComponentProviderResolutionStatus.NotFound
                when modifier.ProviderDiagnosticCode == PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.KindMismatch,
            SearchComponentProviderResolutionStatus.NotFound =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.NotFound,
            SearchComponentProviderResolutionStatus.Unsupported
                when modifier.ProviderDiagnosticCode ==
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance =>
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance,
            _ => PathOfExileTradeSelectedModifierMappingDiagnosticCodes.InvalidInput,
        };

        return new PathOfExileTradeSelectedModifierMappingDiagnostic(
            code,
            MessageFor(code, modifier.OriginalText),
            sourceIndex,
            modifier.ProviderDiagnosticCode);
    }

    private static string MessageFor(string code, string modifierText)
    {
        var safeModifierText = SafeModifierText(modifierText);
        return code switch
        {
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.Ambiguous =>
                $"Selected modifier matches multiple Trade filters: {safeModifierText}",
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.KindMismatch =>
                $"Selected modifier kind does not match Trade filters: {safeModifierText}",
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.NotFound =>
                $"Selected modifier is not available in Trade search: {safeModifierText}",
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.MissingGameDataProvenance =>
                $"Selected modifier has no exact GameData Trade provenance: {safeModifierText}",
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable =>
                $"Selected modifier type is unavailable in the Trade stat catalog: {safeModifierText}",
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageUnavailable =>
                $"Selected aggregate has no Trade filter covering every contributor: {safeModifierText}",
            PathOfExileTradeSelectedModifierMappingDiagnosticCodes.AggregateCoverageAmbiguous =>
                $"Selected aggregate has multiple Trade filters covering every contributor: {safeModifierText}",
            _ => $"Selected modifier cannot be mapped to Trade search: {safeModifierText}",
        };
    }

    private static string SafeModifierText(string? modifierText)
    {
        var safe = new string(
            (modifierText ?? string.Empty)
            .ReplaceLineEndings(" ")
            .Where(character => !char.IsControl(character))
            .ToArray());
        safe = string.Join(' ', safe.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safe))
        {
            return "<blank>";
        }

        const int maximumLength = 80;
        return safe.Length <= maximumLength
            ? safe
            : $"{safe[..maximumLength]}...";
    }

    private static string ToProviderTemplate(string canonicalSignature)
    {
        return canonicalSignature
            .ReplaceLineEndings(" ")
            .Replace("+<number>", "+#", StringComparison.Ordinal)
            .Replace("-<number>", "-#", StringComparison.Ordinal)
            .Replace("<number>", "#", StringComparison.Ordinal);
    }

    private sealed record IndexedModifier(
        int Index,
        ResolvedSearchComponent Modifier);

    private sealed record ResolvedContributor(
        SearchComponentContributor Contributor,
        PathOfExileTradeStatEntry ProviderEntry);

    private sealed record ContributorCompatibilityKey(
        string ProviderDomain,
        PoEnhance.Core.Items.GameData.ModifierLocality Locality,
        string CanonicalSignature,
        ModifierBoundShape ValueShape,
        int Arity,
        string TranslationIdentity,
        string TranslationHandlers,
        SearchComponentContributorProjection Projection)
    {
        public static ContributorCompatibilityKey Create(
            ResolvedSearchComponent parent,
            SearchComponentContributor contributor)
        {
            var source = contributor.Source;
            return new ContributorCompatibilityKey(
                source.ProviderDomain.Trim().ToLowerInvariant(),
                source.Locality,
                source.CanonicalSignature.Trim(),
                contributor.ValueBoundShape,
                source.CanonicalNumericValues.Count,
                source.TranslationIdentity?.Trim() ?? string.Empty,
                string.Join("|", source.TranslationHandlers.Select(handlers =>
                    string.Join(">", handlers))),
                parent.ContributorProjection);
        }
    }
}
