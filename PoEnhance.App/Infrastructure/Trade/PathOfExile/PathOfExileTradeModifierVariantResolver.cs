using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeModifierVariantResolver
{
    private const string UnsupportedBoundsMessage =
        "This Trade filter has incompatible numeric semantics; retained Min/Max text is not sent.";
    internal const string FracturedApproximationMessage =
        "Exact Fractured stat is unavailable. Searching for this stat on a Fractured item with the same base.";
    internal const string FracturedRequestIdentity = "requested-provider-kind:fractured";

    public static ResolvedSearchComponent Apply(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchCandidate sourceExactCandidate)
    {
        return Apply(component, catalog, [sourceExactCandidate], includePseudo: true);
    }

    public static ResolvedSearchComponent Apply(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> sourceExactCandidates)
    {
        return Apply(component, catalog, sourceExactCandidates, includePseudo: true);
    }

    public static ResolvedSearchComponent ApplyProviderOwnedUniqueExact(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate exactCandidate)
    {
        return ApplyProviderOwnedUniqueExact(component, [exactCandidate]);
    }

    public static ResolvedSearchComponent ApplyProviderOwnedUniqueExact(
        ResolvedSearchComponent component,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> exactCandidates)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(exactCandidates);
        if (exactCandidates.Count == 0)
        {
            throw new ArgumentException(
                "At least one exact provider-owned Unique candidate is required.",
                nameof(exactCandidates));
        }

        var exactCandidate = exactCandidates[0];
        var option = CreateOption(component, exactCandidate, exactCandidates);
        var resolved = component with
        {
            FilterVariants = [option],
            SelectedFilterVariantIdentity = option.Identity,
            ProviderResolutionStatus = exactCandidates.Count == 1
                ? SearchComponentProviderResolutionStatus.Exact
                : SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            ProviderStatId = exactCandidates.Count == 1 ? exactCandidate.StatId : null,
            ProviderStatText = exactCandidate.Text,
            ProviderStatAlternativeIds = exactCandidates
                .Select(candidate => candidate.StatId)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            ProviderCandidateStatIds = exactCandidates
                .Select(candidate => candidate.StatId)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            ProviderDiagnosticCode = null,
            ProviderDiagnosticMessage = null,
            Contributors = [],
        };
        return ApplyBounds(resolved, option, exactCandidates);
    }

    public static ResolvedSearchComponent ApplyProviderOwnedPresenceExact(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate exactCandidate)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(exactCandidate);

        var providerKind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(exactCandidate);
        var option = new SearchFilterVariant
        {
            Identity = IdentityFor(exactCandidate.StatId),
            Label = ConciseLabel(exactCandidate, providerKind),
            Description = exactCandidate.Text,
            ProviderKind = providerKind,
            Mode = SearchFilterVariantMode.Standalone,
            SupportsContributorComposition = false,
            SupportsValueBounds = false,
            ValueBoundsUnsupportedReason =
                "This Trade filter represents presence only and has no numeric Min/Max.",
        };
        var providerIdentity = PathOfExileTradeProviderIdentity.Create(exactCandidate.StatId);
        return component with
        {
            StatMappingProof = ModifierStatMappingProofStatus.ProviderExact,
            IsSearchable = true,
            NotSearchableReason = null,
            FilterVariants = [option],
            SelectedFilterVariantIdentity = option.Identity,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderStatId = exactCandidate.StatId,
            ProviderStatText = exactCandidate.Text,
            ProviderStatAlternativeIds = [exactCandidate.StatId],
            ProviderCandidateStatIds = [exactCandidate.StatId],
            ProviderDiagnosticCode = null,
            ProviderDiagnosticMessage = null,
            SupportsValueBounds = false,
            ValueBoundsUnsupportedReason = option.ValueBoundsUnsupportedReason,
            RequestedMinimum = null,
            RequestedMaximum = null,
            ValueBoundShape = ModifierBoundShape.PresenceOnly,
            Sources = component.Sources.Select(source => source with
            {
                StatMappingProof = ModifierStatMappingProofStatus.ProviderExact,
                ProviderIdentity = providerIdentity,
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            }).ToArray(),
            Contributors = [],
        };
    }

    public static ResolvedSearchComponent ApplyFracturedExact(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchCandidate exactCandidate)
    {
        return ApplyFracturedExact(component, catalog, [exactCandidate]);
    }

    public static ResolvedSearchComponent ApplyFracturedExact(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> exactCandidates)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(exactCandidates);
        if (exactCandidates.Count == 0)
        {
            throw new ArgumentException("At least one exact Fractured provider candidate is required.", nameof(exactCandidates));
        }

        var exactIdentity = IdentityFor(exactCandidates);
        var applied = Apply(
            component with { SelectedFilterVariantIdentity = exactIdentity },
            catalog,
            exactCandidates,
            includePseudo: true);
        var exactOption = applied.FilterVariants.SingleOrDefault(option =>
            string.Equals(option.Identity, exactIdentity, StringComparison.Ordinal) &&
            string.Equals(option.ProviderKind, "fractured", StringComparison.OrdinalIgnoreCase));
        if (applied.ProviderResolutionStatus is not (
                SearchComponentProviderResolutionStatus.Exact or
                SearchComponentProviderResolutionStatus.ExactEquivalentSet) ||
            exactOption is null ||
            component.CanonicalNumericValues.Count > 0 &&
            (!component.SupportsValueBounds ||
                !applied.SupportsValueBounds ||
                applied.RequestedMinimum != component.RequestedMinimum ||
                applied.RequestedMaximum != component.RequestedMaximum))
        {
            return applied with
            {
                IsSearchable = false,
                NotSearchableReason =
                    "The exact Fractured provider stat does not preserve the source numeric projection.",
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Unsupported,
                ProviderStatId = null,
                ProviderStatText = null,
                ProviderStatAlternativeIds = [],
                ProviderDiagnosticCode =
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.FracturedApproximationUnavailable,
                ProviderDiagnosticMessage =
                    "The exact Fractured provider stat could not be retained with fracture-specific, numerically faithful semantics.",
            };
        }

        return applied with
        {
            SelectedFilterVariantIdentity = exactOption.Identity,
            RequestedFilterVariantIdentity = exactOption.Identity,
            RequestedFilterVariantKind = "fractured",
            ProviderDiagnosticCode = null,
            ProviderDiagnosticMessage = null,
        };
    }

    public static ResolvedSearchComponent ApplyFracturedApproximate(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchCandidate explicitCandidate)
    {
        return ApplyFracturedApproximate(component, catalog, [explicitCandidate]);
    }

    public static ResolvedSearchComponent ApplyFracturedApproximate(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> explicitCandidates)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(explicitCandidates);
        if (explicitCandidates.Count == 0)
        {
            throw new ArgumentException("At least one explicit approximation candidate is required.", nameof(explicitCandidates));
        }

        var explicitIdentity = IdentityFor(explicitCandidates);
        var applied = Apply(
            component with { SelectedFilterVariantIdentity = explicitIdentity },
            catalog,
            explicitCandidates,
            includePseudo: true);
        var option = applied.FilterVariants.SingleOrDefault(candidate =>
            string.Equals(candidate.Identity, explicitIdentity, StringComparison.Ordinal) &&
            string.Equals(candidate.ProviderKind, "explicit", StringComparison.OrdinalIgnoreCase));
        if (applied.ProviderResolutionStatus is not (
                SearchComponentProviderResolutionStatus.Exact or
                SearchComponentProviderResolutionStatus.ExactEquivalentSet) ||
            option is null)
        {
            return applied with
            {
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Unsupported,
                ProviderStatId = null,
                ProviderStatText = null,
                ProviderStatAlternativeIds = [],
                ProviderDiagnosticCode =
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.FracturedApproximationUnavailable,
                ProviderDiagnosticMessage =
                    "The guarded Fractured request could not retain its compatible explicit provider representation.",
            };
        }

        var fracturedRequestOption = new SearchFilterVariant
        {
            Identity = FracturedRequestIdentity,
            Label = "Fractured",
            Description = FracturedApproximationMessage,
            ProviderKind = "fractured",
            Mode = SearchFilterVariantMode.Standalone,
            SupportsContributorComposition = false,
            SupportsValueBounds = option.SupportsValueBounds,
            ValueBoundsUnsupportedReason = option.ValueBoundsUnsupportedReason,
        };
        var resolved = applied with
        {
            FilterVariants = applied.FilterVariants
                .Where(candidate => !string.Equals(
                    candidate.Identity,
                    FracturedRequestIdentity,
                    StringComparison.Ordinal))
                .Append(fracturedRequestOption)
                .ToArray(),
            SelectedFilterVariantIdentity = option.Identity,
            RequestedFilterVariantIdentity = FracturedRequestIdentity,
            RequestedFilterVariantKind = "fractured",
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Approximate,
            ProviderStatId = explicitCandidates.Count == 1 ? explicitCandidates[0].StatId : null,
            ProviderStatText = explicitCandidates[0].Text,
            ProviderStatAlternativeIds = explicitCandidates
                .Select(candidate => candidate.StatId)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            ProviderCandidateStatIds = explicitCandidates
                .Select(candidate => candidate.StatId)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            ProviderDiagnosticCode =
                PathOfExileTradeSelectedModifierMappingDiagnosticCodes.FracturedApproximation,
            ProviderDiagnosticMessage = FracturedApproximationMessage,
            Contributors = [],
        };
        return ApplyBounds(resolved, option, explicitCandidates);
    }

    public static ResolvedSearchComponent ApplyFracturedRequestedVariant(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchCandidate sourceExactCandidate,
        string requestedIdentity,
        string requestedKind)
    {
        return ApplyFracturedRequestedVariant(
            component,
            catalog,
            [sourceExactCandidate],
            requestedIdentity,
            requestedKind);
    }

    public static ResolvedSearchComponent ApplyFracturedRequestedVariant(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> sourceExactCandidates,
        string requestedIdentity,
        string requestedKind)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sourceExactCandidates);
        if (sourceExactCandidates.Count == 0)
        {
            throw new ArgumentException("At least one source provider candidate is required.", nameof(sourceExactCandidates));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedIdentity);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedKind);

        var applied = Apply(
            component with { SelectedFilterVariantIdentity = requestedIdentity },
            catalog,
            sourceExactCandidates,
            includePseudo: true);
        var selected = applied.FilterVariants.FirstOrDefault(option => string.Equals(
            option.Identity,
            applied.SelectedFilterVariantIdentity,
            StringComparison.Ordinal));
        if (applied.ProviderResolutionStatus is
                SearchComponentProviderResolutionStatus.Exact or
                SearchComponentProviderResolutionStatus.ExactEquivalentSet &&
            (selected is null ||
                !string.Equals(
                    selected.ProviderKind,
                    requestedKind,
                    StringComparison.OrdinalIgnoreCase)))
        {
            applied = applied with
            {
                ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Unsupported,
                ProviderStatId = null,
                ProviderStatText = null,
                ProviderStatAlternativeIds = [],
                ProviderDiagnosticCode =
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.KindMismatch,
                ProviderDiagnosticMessage =
                    "The requested Trade Mod Type is not compatible with the resolved provider identity.",
            };
        }

        return applied with
        {
            RequestedFilterVariantIdentity = requestedIdentity,
            RequestedFilterVariantKind = requestedKind,
        };
    }

    private static ResolvedSearchComponent Apply(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> sourceExactCandidates,
        bool includePseudo)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sourceExactCandidates);
        if (sourceExactCandidates.Count == 0)
        {
            throw new ArgumentException("At least one source provider candidate is required.", nameof(sourceExactCandidates));
        }
        var sourceExactCandidate = sourceExactCandidates[0];

        var discovery = PathOfExileTradeModifierVariantDiscovery.Discover(
            component,
            catalog,
            sourceExactCandidate);
        var requiresExactSourceIdentity = component.HasResolvedUniqueSourceSemantics ||
            component.IsVeiled;
        var sourceCandidateIds = sourceExactCandidates
            .Select(candidate => candidate.StatId)
            .ToHashSet(StringComparer.Ordinal);
        var discoveredCandidates = discovery.Candidates
            .Where(candidate => includePseudo || !string.Equals(
                PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate),
                "pseudo",
                StringComparison.Ordinal))
            .Where(candidate => !requiresExactSourceIdentity ||
                sourceCandidateIds.Contains(candidate.StatId))
            .ToArray();

        var contributors = ResolveContributors(component, component.Sources);
        var candidates = discoveredCandidates;

        var groups = candidates
            .GroupBy(
                candidate => PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate),
                StringComparer.Ordinal)
            .Select(group => new ProviderVariantGroup(
                group.Key,
                group.OrderBy(candidate => candidate.ProviderOrder)
                    .ThenBy(candidate => candidate.StatId, StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(group => group.Candidates.Min(candidate => candidate.ProviderOrder))
            .ToArray();
        var options = groups
            .Select(group => CreateOption(component, sourceExactCandidate, group.Candidates))
            .ToArray();
        var requestedIdentity = component.SelectedFilterVariantIdentity?.Trim();
        if (groups.Length == 0)
        {
            var localityDiagnostic = discovery.Diagnostics.FirstOrDefault(diagnostic =>
                diagnostic.Code ==
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantLocalityAmbiguous);
            return component with
            {
                FilterVariants = [],
                SelectedFilterVariantIdentity = requestedIdentity,
                ProviderResolutionStatus = localityDiagnostic is null
                    ? SearchComponentProviderResolutionStatus.Unsupported
                    : SearchComponentProviderResolutionStatus.Ambiguous,
                ProviderStatId = null,
                ProviderStatText = null,
                ProviderStatAlternativeIds = [],
                ProviderDiagnosticCode = localityDiagnostic?.Code ??
                    PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable,
                ProviderDiagnosticMessage = DiagnosticMessage(null, discovery.Diagnostics),
                Sources = component.Sources,
                Contributors = contributors,
            };
        }

        var selectedIndex = string.IsNullOrWhiteSpace(requestedIdentity)
            ? -1
            : Array.FindIndex(options, option => string.Equals(
                option.Identity,
                requestedIdentity,
                StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(requestedIdentity) && selectedIndex < 0)
        {
            var requestedTrace = discovery.Trace.FirstOrDefault(trace => string.Equals(
                IdentityFor(trace.ProviderStatId),
                requestedIdentity,
                StringComparison.Ordinal));
            var localityAmbiguous = requestedTrace?.RejectionReason ==
                $"{PathOfExileTradeModifierVariantDiscovery.SemanticMismatch}:" +
                    PathOfExileTradeProviderLocalityCompatibility.AmbiguousLocalityEvidence;
            return component with
            {
                FilterVariants = options,
                SelectedFilterVariantIdentity = requestedIdentity,
                ProviderResolutionStatus = localityAmbiguous
                    ? SearchComponentProviderResolutionStatus.Ambiguous
                    : SearchComponentProviderResolutionStatus.NotFound,
                ProviderStatId = null,
                ProviderStatText = null,
                ProviderStatAlternativeIds = [],
                ProviderDiagnosticCode = localityAmbiguous
                    ? PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantLocalityAmbiguous
                    : PathOfExileTradeSelectedModifierMappingDiagnosticCodes.VariantUnavailable,
                ProviderDiagnosticMessage = DiagnosticMessage(null, discovery.Diagnostics),
                Sources = component.Sources,
                Contributors = contributors,
            };
        }

        if (selectedIndex < 0)
        {
            selectedIndex = DefaultCandidateIndex(component, groups, sourceExactCandidates);
        }

        selectedIndex = Math.Max(0, selectedIndex);
        var selectedCandidates = groups[selectedIndex].Candidates;
        var selectedCandidate = selectedCandidates[0];
        var selectedOption = options[selectedIndex];
        var resolved = component with
        {
            FilterVariants = options,
            SelectedFilterVariantIdentity = selectedOption.Identity,
            ProviderResolutionStatus = selectedCandidates.Count == 1
                ? SearchComponentProviderResolutionStatus.Exact
                : SearchComponentProviderResolutionStatus.ExactEquivalentSet,
            ProviderStatId = selectedCandidates.Count == 1 ? selectedCandidate.StatId : null,
            ProviderStatText = selectedCandidate.Text,
            ProviderStatAlternativeIds = selectedCandidates
                .Select(candidate => candidate.StatId)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            ProviderCandidateStatIds = selectedCandidates
                .Select(candidate => candidate.StatId)
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            ProviderDiagnosticCode = discovery.Diagnostics.FirstOrDefault()?.Code,
            ProviderDiagnosticMessage = DiagnosticMessage(null, discovery.Diagnostics),
            Sources = component.Sources,
            Contributors = contributors,
        };

        return ApplyBounds(resolved, selectedOption, selectedCandidates);
    }

    internal static PathOfExileTradeModifierVariantDiscoveryResult DiscoverForAudit(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchCandidate sourceExactCandidate)
    {
        return PathOfExileTradeModifierVariantDiscovery.Discover(
            component,
            catalog,
            sourceExactCandidate);
    }

    private static string? DiagnosticMessage(
        string? primary,
        IReadOnlyList<PathOfExileTradeModifierVariantDiscoveryDiagnostic> diagnostics)
    {
        var messages = new[] { primary }
            .Concat(diagnostics.Select(diagnostic => diagnostic.Message))
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return messages.Length == 0 ? null : string.Join(" ", messages);
    }

    private static int DefaultCandidateIndex(
        ResolvedSearchComponent component,
        IReadOnlyList<ProviderVariantGroup> groups,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> sourceExactCandidates)
    {
        var sourceDomains = component.Sources
            .Select(source => source.ProviderDomain?.Trim())
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceDomains.Length > 1)
        {
            var aggregateIndex = Array.FindIndex(groups.ToArray(), group => string.Equals(
                group.ProviderKind,
                "pseudo",
                StringComparison.Ordinal));
            if (aggregateIndex >= 0)
            {
                return aggregateIndex;
            }
        }

        var sourceIds = sourceExactCandidates
            .Select(candidate => candidate.StatId)
            .ToHashSet(StringComparer.Ordinal);
        var exactIndex = Array.FindIndex(groups.ToArray(), group =>
            group.Candidates.Any(candidate => sourceIds.Contains(candidate.StatId)));
        if (exactIndex >= 0)
        {
            return exactIndex;
        }

        var sourceKinds = sourceExactCandidates
            .Select(PathOfExileTradeStatCandidateClassifier.GetProviderKind)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        return Array.FindIndex(groups.ToArray(), group => sourceKinds.Contains(group.ProviderKind));
    }

    private static IReadOnlyList<SearchComponentContributor> ResolveContributors(
        ResolvedSearchComponent parent,
        IReadOnlyList<SearchComponentSourceProvenance> sources)
    {
        if (sources.Count <= 1)
        {
            return [];
        }

        var previousById = parent.Contributors.ToDictionary(
            contributor => contributor.ContributorId,
            StringComparer.Ordinal);
        return sources.Select((source, index) =>
        {
            var contributorId = ContributorId(source, index);
            previousById.TryGetValue(contributorId, out var previous);
            var scalar = source.CanonicalNumericValues.Count == 1
                ? source.CanonicalNumericValues[0]
                : (decimal?)null;
            var isExact = source.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact &&
                !string.IsNullOrWhiteSpace(source.ProviderIdentity);
            var isAmbiguous = source.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Ambiguous;

            return new SearchComponentContributor
            {
                ContributorId = contributorId,
                Source = source,
                DisplayText = previous?.DisplayText ??
                    CanonicalModifierEffectAggregator.RenderAggregateText(
                        source.CanonicalSignature,
                        source.CanonicalNumericValues),
                IsSelected = previous?.IsSelected == true,
                RequestedMinimum = previous?.RequestedMinimum ??
                    (parent.DefaultBoundDirection == ModifierBoundDirection.Minimum ? scalar : null),
                RequestedMaximum = previous?.RequestedMaximum ??
                    (parent.DefaultBoundDirection == ModifierBoundDirection.Maximum ? scalar : null),
                SupportsValueBounds = parent.SupportsValueBounds && scalar.HasValue,
                ValueBoundsUnsupportedReason = parent.SupportsValueBounds && scalar.HasValue
                    ? null
                    : parent.ValueBoundsUnsupportedReason,
                ValueBoundShape = scalar.HasValue ? ModifierBoundShape.Scalar : ModifierBoundShape.Unsupported,
                DefaultBoundDirection = parent.DefaultBoundDirection,
                ProviderResolutionStatus = source.ProviderResolutionStatus,
                ProviderIdentity = source.ProviderIdentity,
                ProviderDiagnosticCode = isExact
                    ? null
                    : isAmbiguous
                        ? PathOfExileTradeSelectedModifierMappingDiagnosticCodes.ContributorSourceIdentityAmbiguous
                        : PathOfExileTradeSelectedModifierMappingDiagnosticCodes.ContributorSourceIdentityUnavailable,
                ProviderDiagnosticMessage = isExact
                    ? null
                    : isAmbiguous
                        ? $"Contributor '{source.OriginalText}' has ambiguous retained source provider provenance."
                        : $"Contributor '{source.OriginalText}' has no exact retained source provider identity.",
            };
        }).ToArray();
    }

    private static string ContributorId(SearchComponentSourceProvenance source, int index)
    {
        return $"{source.ComponentId}:{source.SourceModifierIndex}:{source.SourceComponentIndex}:{index}";
    }

    internal static string IdentityFor(string providerStatId)
    {
        return PathOfExileTradeProviderIdentity.Create(providerStatId);
    }

    internal static string IdentityFor(
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (candidates.Count == 0)
        {
            throw new ArgumentException("At least one provider candidate is required.", nameof(candidates));
        }

        var ids = candidates
            .Select(candidate => candidate.StatId.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(statId => statId, StringComparer.Ordinal)
            .ToArray();
        return ids.Length == 1
            ? IdentityFor(ids[0])
            : PathOfExileTradeProviderIdentity.Create($"equivalent-set:{string.Join('\u001f', ids)}");
    }

    private static SearchFilterVariant CreateOption(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate source,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        return CreateOption(component, source, [candidate]);
    }

    private static SearchFilterVariant CreateOption(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate source,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            throw new ArgumentException("At least one provider candidate is required.", nameof(candidates));
        }
        var candidate = candidates[0];
        var kind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate);
        var supportsBounds = candidates.All(current =>
            PathOfExileTradeModifierBoundProjector.Project(component, current).SupportsValueBounds &&
            (
                HasCompatibleNumericSemantics(source, current) ||
                PathOfExileTradeModifierBoundProjector.CanProjectSemanticBridge(
                    component,
                    current)));
        var contributorShapes = candidates
            .Select(current => SupportsContributorComposition(component, current))
            .Distinct()
            .ToArray();
        var supportsContributorComposition = contributorShapes.Length == 1 && contributorShapes[0];
        var label = ConciseLabel(candidate, kind);
        return new SearchFilterVariant
        {
            Identity = IdentityFor(candidates),
            Label = label,
            Description = candidates.Count == 1
                ? candidate.Text
                : $"{candidate.Text} ({candidates.Count} equivalent Trade alternatives)",
            ProviderKind = kind,
            ProviderAlternativeCount = candidates.Count,
            Mode = supportsContributorComposition
                ? SearchFilterVariantMode.Aggregate
                : SearchFilterVariantMode.Standalone,
            SupportsContributorComposition = supportsContributorComposition,
            SupportsValueBounds = supportsBounds,
            ValueBoundsUnsupportedReason = supportsBounds ? null : UnsupportedBoundsMessage,
        };
    }

    private static bool SupportsContributorComposition(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        if (component.Sources.Count <= 1 ||
            component.ContributorProjection != SearchComponentContributorProjection.Additive ||
            component.Sources.Any(source =>
                source.ValueBoundShape != ModifierBoundShape.Scalar ||
                source.CanonicalNumericValues.Count != 1))
        {
            return false;
        }

        var kind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate);
        if (string.Equals(kind, "pseudo", StringComparison.Ordinal))
        {
            return true;
        }

        var candidateIdentity = PathOfExileTradeProviderIdentity.Create(candidate.StatId);
        return component.Sources.All(source =>
            source.ProviderResolutionStatus == SearchComponentProviderResolutionStatus.Exact &&
            string.Equals(source.ProviderIdentity, candidateIdentity, StringComparison.Ordinal));
    }

    private static ResolvedSearchComponent ApplyBounds(
        ResolvedSearchComponent component,
        SearchFilterVariant option,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        return ApplyBounds(component, option, [candidate]);
    }

    private static ResolvedSearchComponent ApplyBounds(
        ResolvedSearchComponent component,
        SearchFilterVariant option,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates)
    {
        var presenceProjections = candidates
            .Select(candidate => PathOfExileTradeModifierBoundProjector.Project(component, candidate))
            .ToArray();
        var isFaithfulPresence = component.ValueBoundShape == ModifierBoundShape.PresenceOnly ||
            presenceProjections.All(projection =>
                projection.ValueBoundShape == ModifierBoundShape.PresenceOnly);
        if (!option.SupportsValueBounds)
        {
            return component with
            {
                IsSearchable = component.IsSearchable,
                NotSearchableReason = component.IsSearchable ? null : component.NotSearchableReason,
                SupportsValueBounds = false,
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason = option.ValueBoundsUnsupportedReason ??
                    UnsupportedBoundsMessage,
            };
        }

        if (isFaithfulPresence)
        {
            return component with
            {
                IsSearchable = component.IsSearchable,
                NotSearchableReason = component.NotSearchableReason,
                SupportsValueBounds = false,
                ValueBoundShape = ModifierBoundShape.PresenceOnly,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason = presenceProjections.FirstOrDefault()?.ValueBoundsUnsupportedReason ??
                    component.ValueBoundsUnsupportedReason,
            };
        }

        var restored = component with
        {
            SupportsValueBounds = true,
            ValueBoundsUnsupportedReason = null,
        };
        var displayProjections = candidates
            .Select(candidate => PathOfExileTradeModifierBoundProjector.Project(restored, candidate))
            .ToArray();
        var displayProjection = displayProjections[0];
        if (displayProjections.Any(projected =>
                projected.SupportsValueBounds != displayProjection.SupportsValueBounds ||
                projected.ValueBoundShape != displayProjection.ValueBoundShape ||
                projected.RequestedMinimum != displayProjection.RequestedMinimum ||
                projected.RequestedMaximum != displayProjection.RequestedMaximum))
        {
            return component with
            {
                SupportsValueBounds = false,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason =
                    "Equivalent provider alternatives do not share one faithful displayed-value projection.",
            };
        }

        var projections = candidates
            .Select(candidate => PathOfExileTradeModifierBoundProjector.ProjectBounds(displayProjection, candidate))
            .ToArray();
        if (projections.Any(projection => !projection.IsFaithful))
        {
            return component with
            {
                SupportsValueBounds = false,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason =
                    "Equivalent provider alternatives do not share one faithful displayed-value projection.",
            };
        }
        return displayProjection;
    }

    private static bool HasCompatibleNumericSemantics(
        PathOfExileTradeStatMatchCandidate source,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        return PathOfExileTradePseudoVariantCompatibility.HasCompatibleNumericSemantics(
            source,
            candidate);
    }

    private static string ConciseLabel(PathOfExileTradeStatMatchCandidate candidate, string kind)
    {
        var metadataLabel = string.IsNullOrWhiteSpace(candidate.GroupLabel)
            ? candidate.Type
            : candidate.GroupLabel;
        var value = string.Equals(kind, PathOfExileTradeStatCandidateClassifier.UnknownProviderKind, StringComparison.Ordinal)
            ? metadataLabel
            : kind;
        value = string.IsNullOrWhiteSpace(value) ? "Filter" : value.Trim();
        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }

    private sealed record ProviderVariantGroup(
        string ProviderKind,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> Candidates);

}
