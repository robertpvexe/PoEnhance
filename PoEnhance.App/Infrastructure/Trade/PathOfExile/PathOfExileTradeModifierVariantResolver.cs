using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeModifierVariantResolver
{
    private const string UnsupportedBoundsMessage =
        "This Trade filter has incompatible numeric semantics; retained Min/Max text is not sent.";

    public static ResolvedSearchComponent Apply(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchCandidate sourceExactCandidate)
    {
        return Apply(component, catalog, sourceExactCandidate, includePseudo: true);
    }

    public static ResolvedSearchComponent ApplyProviderOwnedUniqueExact(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate exactCandidate)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(exactCandidate);

        var option = CreateOption(component, exactCandidate, exactCandidate);
        var resolved = component with
        {
            FilterVariants = [option],
            SelectedFilterVariantIdentity = option.Identity,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderStatId = exactCandidate.StatId,
            ProviderStatText = exactCandidate.Text,
            ProviderDiagnosticCode = null,
            ProviderDiagnosticMessage = null,
            Contributors = [],
        };
        return ApplyBounds(resolved, option, exactCandidate);
    }

    private static ResolvedSearchComponent Apply(
        ResolvedSearchComponent component,
        PathOfExileTradeStatCatalog catalog,
        PathOfExileTradeStatMatchCandidate sourceExactCandidate,
        bool includePseudo)
    {
        ArgumentNullException.ThrowIfNull(component);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sourceExactCandidate);

        var discovery = PathOfExileTradeModifierVariantDiscovery.Discover(
            component,
            catalog,
            sourceExactCandidate);
        var discoveredCandidates = discovery.Candidates
            .Where(candidate => includePseudo || !string.Equals(
                PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate),
                "pseudo",
                StringComparison.Ordinal))
            .Where(candidate => component.ParsedKind != ParsedModifierKind.Unique ||
                string.Equals(candidate.StatId, sourceExactCandidate.StatId, StringComparison.Ordinal))
            .ToArray();

        var contributors = ResolveContributors(component, component.Sources);
        var candidates = discoveredCandidates;

        var options = candidates
            .Select(candidate => CreateOption(component, sourceExactCandidate, candidate))
            .ToArray();
        var requestedIdentity = component.SelectedFilterVariantIdentity?.Trim();
        if (candidates.Length == 0)
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
            selectedIndex = DefaultCandidateIndex(component, candidates, sourceExactCandidate);
        }

        selectedIndex = Math.Max(0, selectedIndex);
        var selectedCandidate = candidates[selectedIndex];
        var selectedOption = options[selectedIndex];
        var resolved = component with
        {
            FilterVariants = options,
            SelectedFilterVariantIdentity = selectedOption.Identity,
            ProviderResolutionStatus = SearchComponentProviderResolutionStatus.Exact,
            ProviderStatId = selectedCandidate.StatId,
            ProviderStatText = selectedCandidate.Text,
            ProviderDiagnosticCode = discovery.Diagnostics.FirstOrDefault()?.Code,
            ProviderDiagnosticMessage = DiagnosticMessage(null, discovery.Diagnostics),
            Sources = component.Sources,
            Contributors = contributors,
        };

        return ApplyBounds(resolved, selectedOption, selectedCandidate);
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
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates,
        PathOfExileTradeStatMatchCandidate sourceExactCandidate)
    {
        var sourceDomains = component.Sources
            .Select(source => source.ProviderDomain?.Trim())
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (sourceDomains.Length > 1)
        {
            var aggregateIndex = Array.FindIndex(candidates.ToArray(), candidate => string.Equals(
                PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate),
                "pseudo",
                StringComparison.Ordinal));
            if (aggregateIndex >= 0)
            {
                return aggregateIndex;
            }
        }

        return Array.FindIndex(candidates.ToArray(), candidate => string.Equals(
            candidate.StatId,
            sourceExactCandidate.StatId,
            StringComparison.Ordinal));
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

    private static SearchFilterVariant CreateOption(
        ResolvedSearchComponent component,
        PathOfExileTradeStatMatchCandidate source,
        PathOfExileTradeStatMatchCandidate candidate)
    {
        var kind = PathOfExileTradeStatCandidateClassifier.GetProviderKind(candidate);
        var supportsBounds = HasCompatibleNumericSemantics(source, candidate);
        var supportsContributorComposition = SupportsContributorComposition(component, candidate);
        var label = ConciseLabel(candidate, kind);
        return new SearchFilterVariant
        {
            Identity = IdentityFor(candidate.StatId),
            Label = label,
            Description = candidate.Text,
            ProviderKind = kind,
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
        if (!option.SupportsValueBounds || component.ValueBoundShape is ModifierBoundShape.PresenceOnly or ModifierBoundShape.Unsupported)
        {
            return component with
            {
                SupportsValueBounds = false,
                RequestedMinimum = null,
                RequestedMaximum = null,
                ValueBoundsUnsupportedReason = option.ValueBoundsUnsupportedReason ?? component.ValueBoundsUnsupportedReason,
            };
        }

        var restored = component with
        {
            SupportsValueBounds = true,
            ValueBoundsUnsupportedReason = null,
        };
        return PathOfExileTradeModifierBoundProjector.Project(restored, candidate);
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

}
