using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using Serilog;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeStatMatcher : IPathOfExileTradeStatMatcher
{
    private const string RejectedByProviderKind = "ProviderKindMismatch";
    private const string RejectedByExpectedLocality = "ExpectedLocalityMismatch";

    public PathOfExileTradeStatMatchResult Match(
        ParsedModifier? modifier,
        PathOfExileTradeStatCatalog? catalog,
        PathOfExileTradeStatMatchContext? context = null)
    {
        if (modifier is null)
        {
            return InvalidInput(
                PathOfExileTradeStatMatchDiagnosticCodes.BlankModifierText,
                "Modifier text is required.");
        }

        var modifierText = modifier.Text;
        if (string.IsNullOrWhiteSpace(modifierText))
        {
            return InvalidInput(
                PathOfExileTradeStatMatchDiagnosticCodes.BlankModifierText,
                "Modifier text is required.");
        }

        var normalization = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText(modifierText);
        var source = StatMatchSource.FromParsedModifier(modifier);
        return Match(source, normalization, catalog, context);
    }

    public PathOfExileTradeStatMatchResult Match(
        ResolvedSearchComponent? component,
        PathOfExileTradeStatCatalog? catalog,
        PathOfExileTradeStatMatchContext? context = null)
    {
        if (component is null)
        {
            return InvalidInput(
                PathOfExileTradeStatMatchDiagnosticCodes.BlankModifierText,
                "A resolved search component is required.");
        }

        var providerSignature = string.IsNullOrWhiteSpace(component.ProviderCanonicalSignature)
            ? component.CanonicalSignature
            : component.ProviderCanonicalSignature;
        if (string.IsNullOrWhiteSpace(providerSignature))
        {
            return InvalidInput(
                PathOfExileTradeStatMatchDiagnosticCodes.BlankModifierText,
                "A resolved search component needs a canonical signature.");
        }

        var normalization = new PathOfExileTradeStatModifierNormalization
        {
            NormalizedTemplate = ToProviderTemplate(providerSignature),
            ExtractedNumericValues = [],
        };
        var source = StatMatchSource.FromResolvedComponent(component);
        return Match(source, normalization, catalog, context);
    }

    private static PathOfExileTradeStatMatchResult Match(
        StatMatchSource source,
        PathOfExileTradeStatModifierNormalization normalization,
        PathOfExileTradeStatCatalog? catalog,
        PathOfExileTradeStatMatchContext? context)
    {
        if (catalog is null)
        {
            return InvalidInput(
                PathOfExileTradeStatMatchDiagnosticCodes.NullCatalog,
                "A Trade stats catalog is required.");
        }

        if (normalization.Diagnostic is not null)
        {
            return new PathOfExileTradeStatMatchResult
            {
                Status = PathOfExileTradeStatMatchStatus.InvalidInput,
                NormalizedItemTemplate = normalization.NormalizedTemplate,
                Diagnostics = [normalization.Diagnostic],
                Trace = CreateTrace(
                    normalization.NormalizedTemplate,
                    context,
                    providerCandidateGroupKey: null,
                    compatibleProviderCandidates: [],
                    rejections: [],
                    selectedProviderStatId: null,
                    finalDiagnosticCode: normalization.Diagnostic.Code),
            };
        }

        var (lookupTemplate, discoveredGroups) = DiscoverCandidateGroups(
            source,
            normalization,
            catalog,
            context);
        var groups = discoveredGroups;
        if (groups.Length == 0 && source.Component is not null)
        {
            groups = PathOfExileTradeModifierBoundProjector
                .ProjectedLookupTemplates(source.Component)
                .SelectMany(catalog.FindCandidateGroupsByNormalizedTemplate)
                .Select(group => group with
                {
                    Candidates = group.Candidates
                        .Where(candidate =>
                            PathOfExileTradeModifierBoundProjector.CanProjectSemanticBridge(
                                source.Component,
                                candidate))
                        .ToArray(),
                })
                .Where(group => group.Candidates.Count > 0)
                .DistinctBy(group => group.Key)
                .ToArray();
        }
        var initialCandidates = groups
            .SelectMany(group => group.Candidates)
            .ToArray();
        var expectedLocality = context?.ModifierLocality ?? ModifierLocality.Unknown;

        Log.Debug(
            "Path of Exile Trade stat match candidate groups. NormalizedTemplate={NormalizedTemplate}; LookupTemplate={LookupTemplate}; GroupCount={GroupCount}; CandidateCount={CandidateCount}; ParsedKind={ParsedKind}; IsCrafted={IsCrafted}; IsFractured={IsFractured}; IsVeiled={IsVeiled}; ExpectedLocality={ExpectedLocality}",
            normalization.NormalizedTemplate,
            lookupTemplate,
            groups.Length,
            initialCandidates.Length,
            source.Kind,
            source.IsCrafted,
            source.IsFractured,
            source.IsVeiled,
            expectedLocality);

        if (groups.Length == 0)
        {
            return Failure(
                PathOfExileTradeStatMatchStatus.NotFound,
                normalization,
                expectedLocality,
                initialCandidates,
                candidates: [],
                rejections: [],
                PathOfExileTradeStatMatchDiagnosticCodes.NoCandidate,
                "No Trade stat template matched the modifier text.",
                context,
                providerCandidateGroupKey: null);
        }

        var compatibleGroups = ApplyKindConstraints(source, groups, out var mismatchWasCertain);
        var compatibleCandidates = compatibleGroups
            .SelectMany(group => group.Candidates)
            .ToArray();
        var kindRejections = Rejections(
            initialCandidates,
            compatibleCandidates,
            RejectedByProviderKind);

        if (compatibleGroups.Length == 0)
        {
            return Failure(
                PathOfExileTradeStatMatchStatus.NotFound,
                normalization,
                expectedLocality,
                initialCandidates,
                candidates: [],
                kindRejections,
                mismatchWasCertain
                    ? PathOfExileTradeStatMatchDiagnosticCodes.ModifierKindMismatch
                    : PathOfExileTradeStatMatchDiagnosticCodes.NoCandidate,
                "Trade stat template candidates were incompatible with the parsed modifier kind.",
                context,
                providerCandidateGroupKey: null);
        }

        if (compatibleGroups.Length > 1)
        {
            return Failure(
                PathOfExileTradeStatMatchStatus.Ambiguous,
                normalization,
                expectedLocality,
                initialCandidates,
                compatibleCandidates,
                kindRejections,
                PathOfExileTradeStatMatchDiagnosticCodes.AmbiguousCandidates,
                "Multiple Trade stat candidate groups matched the modifier text and kind.",
                context,
                providerCandidateGroupKey: null);
        }

        var group = compatibleGroups[0];
        var candidatesAfterKind = group.Candidates;
        if (expectedLocality is ModifierLocality.Local or ModifierLocality.Global)
        {
            var hasExactGameDataProvenance =
                (context?.HasExactGameDataSourceProof == true ||
                    !string.IsNullOrWhiteSpace(context?.ResolvedModifierId)) &&
                context.InternalStatIds.Count > 0 &&
                context.InternalStatIds.All(statId => !string.IsNullOrWhiteSpace(statId));
            var localityEvaluations = candidatesAfterKind
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Decision = PathOfExileTradeProviderLocalityCompatibility.EvaluateExactGameDataMatch(
                        expectedLocality,
                        hasExactGameDataProvenance,
                        candidate),
                })
                .ToArray();
            var localityCandidates = localityEvaluations
                .Where(evaluation => evaluation.Decision.IsCompatible)
                .Select(evaluation => evaluation.Candidate)
                .ToArray();
            var expectedMarker = expectedLocality == ModifierLocality.Local
                ? PathOfExileTradeProviderStatLocality.Local
                : PathOfExileTradeProviderStatLocality.Global;
            var explicitlyMarkedCandidates = localityCandidates
                .Where(candidate => candidate.ProviderLocality == expectedMarker)
                .ToArray();
            if (explicitlyMarkedCandidates.Length > 0)
            {
                localityCandidates = explicitlyMarkedCandidates;
            }

            var localityRejections = kindRejections
                .Concat(Rejections(
                    candidatesAfterKind,
                    localityCandidates,
                    RejectedByExpectedLocality))
                .ToArray();

            if (localityCandidates.Length == 0)
            {
                var code = expectedLocality == ModifierLocality.Local
                    ? PathOfExileTradeStatMatchDiagnosticCodes.ExpectedLocalCandidateMissing
                    : PathOfExileTradeStatMatchDiagnosticCodes.ExpectedUnmarkedCandidateMissing;
                return Failure(
                    PathOfExileTradeStatMatchStatus.NotFound,
                    normalization,
                    expectedLocality,
                    initialCandidates,
                    candidates: [],
                    localityRejections,
                    code,
                    expectedLocality == ModifierLocality.Local
                        ? "Expected a local Trade stat template candidate, but none remained after filtering."
                        : "Expected an unmarked Trade stat template candidate, but none remained after filtering.",
                    context,
                    group.Key.ToString());
            }

            return ResolveRemainingCandidates(
                normalization,
                expectedLocality,
                initialCandidates,
                localityCandidates,
                localityRejections,
                context,
                group.Key.ToString(),
                source.CanProveEquivalentSet,
                source.Component);
        }

        return ResolveRemainingCandidates(
            normalization,
            expectedLocality,
            initialCandidates,
            candidatesAfterKind,
            kindRejections,
            context,
            group.Key.ToString(),
            source.CanProveEquivalentSet,
            source.Component);
    }

    private static (string LookupTemplate, PathOfExileTradeStatCandidateGroup[] Groups)
        DiscoverCandidateGroups(
            StatMatchSource source,
            PathOfExileTradeStatModifierNormalization normalization,
            PathOfExileTradeStatCatalog catalog,
            PathOfExileTradeStatMatchContext? context)
    {
        var templates = new List<string> { normalization.NormalizedTemplate };
        if (HasExactUniqueEvidence(source.Component))
        {
            templates.AddRange(source.Component!.ProviderSearchSignatures);
            templates.Add(source.Component.CanonicalSignature);
            if (!string.IsNullOrWhiteSpace(source.Component.OriginalText))
            {
                templates.Add(PathOfExileTradeStatTemplateNormalizer
                    .NormalizeModifierText(source.Component.OriginalText)
                    .NormalizedTemplate);
            }
        }

        var lookups = templates
            .Where(template => !string.IsNullOrWhiteSpace(template))
            .Select(template => PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                ToProviderTemplate(template)))
            .Where(template => !string.IsNullOrWhiteSpace(template))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var lookup in lookups)
        {
            var direct = catalog.FindCandidateGroupsByNormalizedTemplate(lookup).ToArray();
            if (direct.Length > 0)
            {
                return (lookup, direct);
            }

            var qualified = FindItemClassQualifiedGroups(catalog, lookup, context?.ItemClass);
            if (qualified.Length > 0)
            {
                return (lookup, qualified);
            }
        }

        foreach (var lookup in lookups)
        {
            var signedValueLookup = NegativeEvaluatedValueProviderLookup(
                source,
                normalization,
                lookup);
            if (signedValueLookup is null)
            {
                continue;
            }

            var direct = catalog.FindCandidateGroupsByNormalizedTemplate(signedValueLookup).ToArray();
            if (direct.Length > 0)
            {
                return (signedValueLookup, direct);
            }

            var qualified = FindItemClassQualifiedGroups(
                catalog,
                signedValueLookup,
                context?.ItemClass);
            if (qualified.Length > 0)
            {
                return (signedValueLookup, qualified);
            }
        }

        var fallbackLookup = lookups.FirstOrDefault() ?? string.Empty;
        return (fallbackLookup, []);
    }

    private static string? NegativeEvaluatedValueProviderLookup(
        StatMatchSource source,
        PathOfExileTradeStatModifierNormalization normalization,
        string lookupTemplate)
    {
        if (PathOfExileTradeStatTemplateNormalizer.CountNumericPlaceholders(lookupTemplate) != 1 ||
            !lookupTemplate.Contains("-#", StringComparison.Ordinal))
        {
            return null;
        }

        var values = normalization.ExtractedNumericValues.Count > 0
            ? normalization.ExtractedNumericValues
            : source.Component?.ObservedNumericValues.Count > 0
                ? source.Component.ObservedNumericValues
                : source.Component?.CanonicalNumericValues ?? [];
        if (values.Count == 0 && !string.IsNullOrWhiteSpace(source.Component?.OriginalText))
        {
            var original = PathOfExileTradeStatTemplateNormalizer.NormalizeModifierText(
                source.Component.OriginalText);
            if (original.Diagnostic is null && string.Equals(
                    PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
                        original.NormalizedTemplate),
                    lookupTemplate,
                    StringComparison.Ordinal))
            {
                values = original.ExtractedNumericValues;
            }
        }
        return values.Count == 1 && values[0] < 0m
            ? lookupTemplate.Replace("-#", "+#", StringComparison.Ordinal)
            : null;
    }

    private static PathOfExileTradeStatCandidateGroup[] FindItemClassQualifiedGroups(
        PathOfExileTradeStatCatalog catalog,
        string lookupTemplate,
        string? itemClass)
    {
        return catalog.FindCandidateGroupsByItemClassQualifiedTemplate(lookupTemplate, itemClass).ToArray();
    }

    private static bool HasExactUniqueEvidence(ResolvedSearchComponent? component) =>
        component?.HasExactUniqueSourceProvenance == true;

    private static PathOfExileTradeStatMatchResult ResolveRemainingCandidates(
        PathOfExileTradeStatModifierNormalization normalization,
        ModifierLocality expectedLocality,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> initialCandidates,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates,
        IReadOnlyList<PathOfExileTradeStatCandidateRejection> rejections,
        PathOfExileTradeStatMatchContext? context,
        string providerCandidateGroupKey,
        bool canProveEquivalentSet,
        ResolvedSearchComponent? component)
    {
        if (candidates.Count > 1 &&
            HasExactUniqueEvidence(component) &&
            component!.FixedQueryValue.HasValue)
        {
            var parametricCandidates = candidates
                .Where(candidate =>
                    PathOfExileTradeModifierBoundProjector.CanApplyFixedQueryValue(
                        component,
                        candidate))
                .ToArray();
            if (parametricCandidates.Length > 0 &&
                parametricCandidates.Length < candidates.Count)
            {
                candidates = parametricCandidates;
            }
        }

        if (candidates.Count == 1)
        {
            return Exact(
                normalization,
                expectedLocality,
                initialCandidates,
                candidates,
                rejections,
                context,
                providerCandidateGroupKey,
                candidates[0]);
        }

        if (canProveEquivalentSet && AreEquivalentProviderCandidates(candidates))
        {
            return ExactEquivalentSet(
                normalization,
                expectedLocality,
                initialCandidates,
                candidates,
                rejections,
                context,
                providerCandidateGroupKey);
        }

        var diagnosticCode = expectedLocality == ModifierLocality.Unknown &&
            candidates.Select(candidate => candidate.ProviderLocality).Distinct().Count() > 1
            ? PathOfExileTradeStatMatchDiagnosticCodes.LocalityAmbiguous
            : PathOfExileTradeStatMatchDiagnosticCodes.AmbiguousCandidates;
        return Failure(
            PathOfExileTradeStatMatchStatus.Ambiguous,
            normalization,
            expectedLocality,
            initialCandidates,
            candidates,
            rejections,
            diagnosticCode,
            diagnosticCode == PathOfExileTradeStatMatchDiagnosticCodes.LocalityAmbiguous
                ? "Could not determine whether the modifier requires a local or unmarked Trade stat candidate."
                : "Multiple Trade stat templates matched the modifier text.",
            context,
            providerCandidateGroupKey);
    }

    private static PathOfExileTradeStatMatchResult Exact(
        PathOfExileTradeStatModifierNormalization normalization,
        ModifierLocality expectedLocality,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> initialCandidates,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates,
        IReadOnlyList<PathOfExileTradeStatCandidateRejection> rejections,
        PathOfExileTradeStatMatchContext? context,
        string providerCandidateGroupKey,
        PathOfExileTradeStatMatchCandidate selected)
    {
        Log.Debug(
            "Path of Exile Trade stat selected. StatId={StatId}; GroupKey={GroupKey}; GroupId={GroupId}; Type={Type}; ProviderKind={ProviderKind}; ProviderLocality={ProviderLocality}; ExpectedLocality={ExpectedLocality}; CandidateCount={CandidateCount}; NormalizedTemplate={NormalizedTemplate}",
            selected.StatId,
            providerCandidateGroupKey,
            selected.GroupId,
            selected.Type,
            selected.ProviderKind,
            selected.ProviderLocality,
            expectedLocality,
            initialCandidates.Count,
            normalization.NormalizedTemplate);
        return new PathOfExileTradeStatMatchResult
        {
            Status = PathOfExileTradeStatMatchStatus.Exact,
            NormalizedItemTemplate = normalization.NormalizedTemplate,
            ExtractedNumericValues = normalization.ExtractedNumericValues,
            RequestedLocality = expectedLocality,
            ExactCandidate = selected,
            InitialCandidates = initialCandidates,
            Candidates = candidates,
            RejectedCandidates = rejections.Select(rejection => rejection.Candidate).ToArray(),
            Trace = CreateTrace(
                normalization.NormalizedTemplate,
                context,
                providerCandidateGroupKey,
                candidates,
                rejections,
                selected.StatId,
                finalDiagnosticCode: null),
        };
    }

    private static PathOfExileTradeStatMatchResult ExactEquivalentSet(
        PathOfExileTradeStatModifierNormalization normalization,
        ModifierLocality expectedLocality,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> initialCandidates,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates,
        IReadOnlyList<PathOfExileTradeStatCandidateRejection> rejections,
        PathOfExileTradeStatMatchContext? context,
        string providerCandidateGroupKey)
    {
        var ordered = candidates
            .OrderBy(candidate => candidate.ProviderOrder)
            .ThenBy(candidate => candidate.StatId, StringComparer.Ordinal)
            .ToArray();
        Log.Debug(
            "Path of Exile Trade equivalent stat set selected. GroupKey={GroupKey}; ProviderKind={ProviderKind}; CandidateCount={CandidateCount}; NormalizedTemplate={NormalizedTemplate}",
            providerCandidateGroupKey,
            ordered[0].ProviderKind,
            ordered.Length,
            normalization.NormalizedTemplate);
        return new PathOfExileTradeStatMatchResult
        {
            Status = PathOfExileTradeStatMatchStatus.ExactEquivalentSet,
            NormalizedItemTemplate = normalization.NormalizedTemplate,
            ExtractedNumericValues = normalization.ExtractedNumericValues,
            RequestedLocality = expectedLocality,
            ExactEquivalentCandidates = ordered,
            InitialCandidates = initialCandidates,
            Candidates = ordered,
            RejectedCandidates = rejections.Select(rejection => rejection.Candidate).ToArray(),
            Trace = CreateTrace(
                normalization.NormalizedTemplate,
                context,
                providerCandidateGroupKey,
                ordered,
                rejections,
                selectedProviderStatId: null,
                finalDiagnosticCode: null),
        };
    }

    internal static bool AreEquivalentProviderCandidates(
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates)
    {
        if (candidates.Count <= 1)
        {
            return false;
        }

        var first = candidates[0];
        if (string.Equals(
                first.ProviderKind,
                PathOfExileTradeStatCandidateClassifier.UnknownProviderKind,
                StringComparison.Ordinal))
        {
            return false;
        }

        return candidates.All(candidate =>
            string.Equals(candidate.NormalizedTemplate, first.NormalizedTemplate, StringComparison.Ordinal) &&
            string.Equals(candidate.LookupTemplate, first.LookupTemplate, StringComparison.Ordinal) &&
            string.Equals(candidate.GroupId, first.GroupId, StringComparison.Ordinal) &&
            string.Equals(candidate.GroupLabel, first.GroupLabel, StringComparison.Ordinal) &&
            string.Equals(candidate.Type, first.Type, StringComparison.Ordinal) &&
            string.Equals(candidate.ProviderKind, first.ProviderKind, StringComparison.Ordinal) &&
            candidate.ProviderLocality == first.ProviderLocality &&
            string.Equals(candidate.Text, first.Text, StringComparison.Ordinal) &&
            candidate.OptionMetadata.Count == first.OptionMetadata.Count &&
            candidate.OptionMetadata.All(option =>
                first.OptionMetadata.TryGetValue(option.Key, out var firstValue) &&
                string.Equals(option.Value, firstValue, StringComparison.Ordinal)));
    }

    private static PathOfExileTradeStatCandidateGroup[] ApplyKindConstraints(
        StatMatchSource source,
        IReadOnlyList<PathOfExileTradeStatCandidateGroup> groups,
        out bool mismatchWasCertain)
    {
        mismatchWasCertain = false;
        if (source.Kind == ParsedModifierKind.Unique &&
            source.UniqueOrigin != ParsedUniqueModifierOrigin.Ordinary)
        {
            var evidenceKinds = EvidenceBackedProviderKinds(source.Component);
            if (evidenceKinds.Count > 0)
            {
                var evidenced = groups
                    .Where(group => evidenceKinds.Contains(group.Key.ProviderKind))
                    .ToArray();
                if (evidenced.Length > 0)
                {
                    return evidenced;
                }
            }

            // A copied Unique source block is not intrinsically a provider "explicit" stat.
            // Prefer that adapter family when it exists, but fall back to the complete
            // provider evidence instead of rejecting other provider kinds up front.
            var explicitGroups = groups
                .Where(group => string.Equals(group.Key.ProviderKind, "explicit", StringComparison.Ordinal))
                .ToArray();
            if (explicitGroups.Length > 0)
            {
                return explicitGroups;
            }

            return groups
                .Where(group => group.Key.ProviderKind is not (
                    "pseudo" or PathOfExileTradeStatCandidateClassifier.UnknownProviderKind))
                .ToArray();
        }

        var requiredKind = RequiredKind(source);
        if (requiredKind is null)
        {
            return groups.ToArray();
        }

        var knownGroups = groups
            .Where(group => group.Key.ProviderKind != PathOfExileTradeStatCandidateClassifier.UnknownProviderKind)
            .ToArray();
        if (knownGroups.Length == 0)
        {
            return groups.ToArray();
        }

        var compatible = groups
            .Where(group => string.Equals(group.Key.ProviderKind, requiredKind, StringComparison.Ordinal))
            .ToArray();
        mismatchWasCertain = compatible.Length == 0;
        return compatible;
    }

    private static IReadOnlySet<string> EvidenceBackedProviderKinds(ResolvedSearchComponent? component)
    {
        if (component is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var supported = new HashSet<string>(
            ["crafted", "enchant", "explicit", "fractured", "implicit", "scourge", "veiled"],
            StringComparer.Ordinal);
        var evidence = component.ProviderDomainEvidence
            .Select(entry => new
            {
                Kind = entry.ProviderDomain.Trim().ToLowerInvariant(),
                entry.EvidenceStrength,
            })
            .Where(entry => supported.Contains(entry.Kind))
            .ToArray();
        if (evidence.Length == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var strongest = evidence.Max(entry => entry.EvidenceStrength);
        return evidence
            .Where(entry => entry.EvidenceStrength == strongest)
            .Select(entry => entry.Kind)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string? RequiredKind(StatMatchSource source)
    {
        if (source.IsCrafted)
        {
            return "crafted";
        }

        if (source.IsFractured)
        {
            return "fractured";
        }

        if (source.IsVeiled)
        {
            return "veiled";
        }

        return source.Kind switch
        {
            ParsedModifierKind.Implicit => "implicit",
            ParsedModifierKind.Prefix or ParsedModifierKind.Suffix => "explicit",
            ParsedModifierKind.Unique when source.UniqueOrigin == ParsedUniqueModifierOrigin.Ordinary => "explicit",
            _ => null,
        };
    }

    private static IReadOnlyList<PathOfExileTradeStatCandidateRejection> Rejections(
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> retained,
        string reason)
    {
        var retainedIds = retained
            .Select(candidate => candidate.StatId)
            .ToHashSet(StringComparer.Ordinal);
        return candidates
            .Where(candidate => !retainedIds.Contains(candidate.StatId))
            .Select(candidate => new PathOfExileTradeStatCandidateRejection
            {
                Candidate = candidate,
                Reason = reason,
            })
            .ToArray();
    }

    private static PathOfExileTradeStatMatchResult Failure(
        PathOfExileTradeStatMatchStatus status,
        PathOfExileTradeStatModifierNormalization normalization,
        ModifierLocality expectedLocality,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> initialCandidates,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates,
        IReadOnlyList<PathOfExileTradeStatCandidateRejection> rejections,
        string diagnosticCode,
        string diagnosticMessage,
        PathOfExileTradeStatMatchContext? context,
        string? providerCandidateGroupKey)
    {
        return new PathOfExileTradeStatMatchResult
        {
            Status = status,
            NormalizedItemTemplate = normalization.NormalizedTemplate,
            ExtractedNumericValues = normalization.ExtractedNumericValues,
            RequestedLocality = expectedLocality,
            InitialCandidates = initialCandidates,
            Candidates = candidates,
            RejectedCandidates = rejections.Select(rejection => rejection.Candidate).ToArray(),
            Diagnostics =
            [
                new PathOfExileTradeStatMatchDiagnostic(
                    diagnosticCode,
                    diagnosticMessage),
            ],
            Trace = CreateTrace(
                normalization.NormalizedTemplate,
                context,
                providerCandidateGroupKey,
                candidates,
                rejections,
                selectedProviderStatId: null,
                finalDiagnosticCode: diagnosticCode),
        };
    }

    private static PathOfExileTradeStatResolutionTrace CreateTrace(
        string copiedNormalizedTemplate,
        PathOfExileTradeStatMatchContext? context,
        string? providerCandidateGroupKey,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> compatibleProviderCandidates,
        IReadOnlyList<PathOfExileTradeStatCandidateRejection> rejections,
        string? selectedProviderStatId,
        string? finalDiagnosticCode)
    {
        return new PathOfExileTradeStatResolutionTrace
        {
            CopiedNormalizedTemplate = copiedNormalizedTemplate,
            ResolvedModifierId = TrimToNull(context?.ResolvedModifierId),
            InternalStatIds = context?.InternalStatIds
                .Select(TrimToNull)
                .Where(statId => statId is not null)
                .Select(statId => statId!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(statId => statId, StringComparer.Ordinal)
                .ToArray() ?? [],
            ExpectedLocality = context?.ModifierLocality ?? ModifierLocality.Unknown,
            ProviderCandidateGroupKey = providerCandidateGroupKey,
            CompatibleProviderCandidates = compatibleProviderCandidates,
            Rejections = rejections,
            SelectedProviderStatId = selectedProviderStatId,
            FinalDiagnosticCode = finalDiagnosticCode,
        };
    }

    private static PathOfExileTradeStatMatchResult InvalidInput(
        string code,
        string message)
    {
        return new PathOfExileTradeStatMatchResult
        {
            Status = PathOfExileTradeStatMatchStatus.InvalidInput,
            Diagnostics = [new PathOfExileTradeStatMatchDiagnostic(code, message)],
            Trace = new PathOfExileTradeStatResolutionTrace
            {
                FinalDiagnosticCode = code,
            },
        };
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string ToProviderTemplate(string canonicalSignature)
    {
        return canonicalSignature
            .ReplaceLineEndings(" ")
            .Replace("+<number>", "+#", StringComparison.Ordinal)
            .Replace("-<number>", "-#", StringComparison.Ordinal)
            .Replace("<number>", "#", StringComparison.Ordinal);
    }

    private sealed record StatMatchSource
    {
        public required ParsedModifierKind Kind { get; init; }

        public ParsedUniqueModifierOrigin UniqueOrigin { get; init; }

        public bool IsCrafted { get; init; }

        public bool IsFractured { get; init; }

        public bool IsVeiled { get; init; }

        public bool CanProveEquivalentSet { get; init; }

        public ResolvedSearchComponent? Component { get; init; }

        public static StatMatchSource FromParsedModifier(ParsedModifier modifier)
        {
            return new StatMatchSource
            {
                Kind = modifier.Kind,
                UniqueOrigin = modifier.UniqueOrigin,
                IsCrafted = modifier.IsCrafted,
                IsFractured = modifier.IsFractured,
                IsVeiled = modifier.IsVeiled,
                CanProveEquivalentSet = false,
                Component = null,
            };
        }

        public static StatMatchSource FromResolvedComponent(ResolvedSearchComponent component)
        {
            return new StatMatchSource
            {
                Kind = component.ResolvedSourceKind,
                UniqueOrigin = component.ResolvedSourceUniqueOrigin,
                IsCrafted = component.IsCrafted,
                IsFractured = component.IsFractured,
                IsVeiled = component.IsVeiled,
                CanProveEquivalentSet =
                    !string.IsNullOrWhiteSpace(component.CanonicalSignature) &&
                    (component.ResolutionStatus == ModifierCandidateResolutionStatus.Exact &&
                        !string.IsNullOrWhiteSpace(component.ResolvedModifierId) &&
                        component.ResolvedStatIds.Count > 0 ||
                    HasExactUniqueEvidence(component) ||
                    component.Sources.Count > 0 &&
                        component.Sources.All(source =>
                            !string.IsNullOrWhiteSpace(source.ResolvedModifierId) &&
                            source.ResolvedStatIds.Count > 0)),
                Component = component,
            };
        }
    }
}
