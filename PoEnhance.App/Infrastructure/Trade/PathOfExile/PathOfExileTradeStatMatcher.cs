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

        if (string.IsNullOrWhiteSpace(component.CanonicalSignature))
        {
            return InvalidInput(
                PathOfExileTradeStatMatchDiagnosticCodes.BlankModifierText,
                "A resolved search component needs a canonical signature.");
        }

        var normalization = new PathOfExileTradeStatModifierNormalization
        {
            NormalizedTemplate = ToProviderTemplate(component.CanonicalSignature),
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

        var lookupTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(
            normalization.NormalizedTemplate);
        var groups = catalog
            .FindCandidateGroupsByNormalizedTemplate(lookupTemplate)
            .ToArray();
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
            var expectedProviderLocality = expectedLocality == ModifierLocality.Local
                ? PathOfExileTradeProviderStatLocality.Local
                : PathOfExileTradeProviderStatLocality.Unmarked;
            var localityCandidates = candidatesAfterKind
                .Where(candidate => candidate.ProviderLocality == expectedProviderLocality)
                .ToArray();
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
                source.Kind == ParsedModifierKind.Implicit);
        }

        return ResolveRemainingCandidates(
            normalization,
            expectedLocality,
            initialCandidates,
            candidatesAfterKind,
            kindRejections,
            context,
            group.Key.ToString(),
            source.Kind == ParsedModifierKind.Implicit);
    }

    private static PathOfExileTradeStatMatchResult ResolveRemainingCandidates(
        PathOfExileTradeStatModifierNormalization normalization,
        ModifierLocality expectedLocality,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> initialCandidates,
        IReadOnlyList<PathOfExileTradeStatMatchCandidate> candidates,
        IReadOnlyList<PathOfExileTradeStatCandidateRejection> rejections,
        PathOfExileTradeStatMatchContext? context,
        string providerCandidateGroupKey,
        bool allowEquivalentCandidateCollapse)
    {
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

        if (allowEquivalentCandidateCollapse &&
            AreEquivalentProviderCandidates(candidates))
        {
            var selected = candidates
                .OrderBy(candidate => candidate.StatId, StringComparer.Ordinal)
                .First();
            return Exact(
                normalization,
                expectedLocality,
                initialCandidates,
                candidates,
                rejections,
                context,
                providerCandidateGroupKey,
                selected);
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

    private static bool AreEquivalentProviderCandidates(
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
            string.Equals(candidate.Text, first.Text, StringComparison.Ordinal));
    }

    private static PathOfExileTradeStatCandidateGroup[] ApplyKindConstraints(
        StatMatchSource source,
        IReadOnlyList<PathOfExileTradeStatCandidateGroup> groups,
        out bool mismatchWasCertain)
    {
        mismatchWasCertain = false;
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

        public bool IsCrafted { get; init; }

        public bool IsFractured { get; init; }

        public bool IsVeiled { get; init; }

        public static StatMatchSource FromParsedModifier(ParsedModifier modifier)
        {
            return new StatMatchSource
            {
                Kind = modifier.Kind,
                IsCrafted = modifier.IsCrafted,
                IsFractured = modifier.IsFractured,
                IsVeiled = modifier.IsVeiled,
            };
        }

        public static StatMatchSource FromResolvedComponent(ResolvedSearchComponent component)
        {
            return new StatMatchSource
            {
                Kind = component.ParsedKind,
                IsCrafted = component.IsCrafted,
                IsFractured = component.IsFractured,
                IsVeiled = component.IsVeiled,
            };
        }
    }
}
