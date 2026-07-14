using System.Collections.ObjectModel;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed class ParsedItemModifierCandidateResolver
{
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ModifierEligibilityEvaluator eligibilityEvaluator = new();
    private readonly ModifierTextSignatureMatcher textSignatureMatcher = new();

    public IReadOnlyList<ModifierCandidateResolutionResult> Resolve(
        ParsedItem parsedItem,
        GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);
        ArgumentNullException.ThrowIfNull(catalog);

        return Resolve(parsedItem, catalog, baseResolver.Resolve(parsedItem, catalog));
    }

    public IReadOnlyList<ModifierCandidateResolutionResult> Resolve(
        ParsedItem parsedItem,
        GameDataCatalog catalog,
        ItemBaseResolutionResult baseResolution)
    {
        ArgumentNullException.ThrowIfNull(parsedItem);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(baseResolution);

        var eligibilityContext = TryGetEligibilityBase(baseResolution, out var itemBase)
            ? ItemModifierEligibilityContext.Create(itemBase, parsedItem.TraditionalInfluences)
            : null;
        var results = new List<ModifierCandidateResolutionResult>();
        for (var index = 0; index < parsedItem.Modifiers.Count; index++)
        {
            var modifier = parsedItem.Modifiers[index];
            if (!HasCandidateDiscoverySignal(modifier))
            {
                continue;
            }

            results.Add(ResolveModifier(index, modifier, catalog, eligibilityContext));
        }

        return ToReadOnly(results);
    }

    private ModifierCandidateResolutionResult ResolveModifier(
        int index,
        ParsedModifier modifier,
        GameDataCatalog catalog,
        ItemModifierEligibilityContext? eligibilityContext)
    {
        if (!TryMapGenerationType(modifier.Kind, out var generationType))
        {
            return Unknown(
                index,
                modifier,
                generationType: null,
                candidates: [],
                ModifierCandidateResolutionDiagnosticCodes.ModifierKindUnsupported,
                "The parsed modifier kind is not supported by first-stage candidate discovery.");
        }

        if (string.IsNullOrWhiteSpace(modifier.Name))
        {
            return Unknown(
                index,
                modifier,
                generationType,
                candidates: [],
                ModifierCandidateResolutionDiagnosticCodes.ModifierNameNotAvailable,
                "The parsed modifier does not expose an authentic Advanced Item Description modifier name.");
        }

        var nameCandidates = catalog.FindModifiersByNormalizedName(modifier.Name);
        var kindCandidates = ToReadOnly(
            nameCandidates.Where(candidate => candidate.GenerationType == generationType));
        if (kindCandidates.Count == 0)
        {
            return Unknown(
                index,
                modifier,
                generationType,
                candidates: [],
                ModifierCandidateResolutionDiagnosticCodes.ModifierNotFound,
                "No catalog modifier matched the parsed modifier name and generation type.",
                nameCandidates.Count,
                generationKindCandidateCount: 0,
                eligibilityCandidateCount: 0);
        }

        if (eligibilityContext is null)
        {
            return kindCandidates.Count == 1
                ? MatchedWithoutEligibility(
                    index,
                    modifier,
                    catalog,
                    generationType,
                    kindCandidates[0],
                    nameCandidates.Count,
                    kindCandidates.Count)
                : Unknown(
                    index,
                    modifier,
                    generationType,
                    kindCandidates,
                    ModifierCandidateResolutionDiagnosticCodes.ModifierEligibilityNotEvaluated,
                    "Modifier eligibility was not evaluated because the parsed item base was not resolved to one catalog record.",
                    nameCandidates.Count,
                    kindCandidates.Count,
                    kindCandidates.Count);
        }

        var evaluations = kindCandidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Result = eligibilityEvaluator.Evaluate(candidate, eligibilityContext),
            })
            .ToArray();
        if (evaluations.Any(evaluation => evaluation.Result.Outcome == ModifierEligibilityOutcome.Unknown))
        {
            return kindCandidates.Count == 1
                ? MatchedWithoutEligibility(
                    index,
                    modifier,
                    catalog,
                    generationType,
                    kindCandidates[0],
                    nameCandidates.Count,
                    kindCandidates.Count)
                : Unknown(
                    index,
                    modifier,
                    generationType,
                    kindCandidates,
                    ModifierCandidateResolutionDiagnosticCodes.ModifierEligibilityNotEvaluated,
                    "Modifier eligibility could not be evaluated from the available provider-neutral data.",
                    nameCandidates.Count,
                    kindCandidates.Count,
                    kindCandidates.Count);
        }

        var eligibleCandidates = ToReadOnly(evaluations
            .Where(evaluation => evaluation.Result.Outcome == ModifierEligibilityOutcome.Eligible)
            .Select(evaluation => evaluation.Candidate));
        var excludedCandidates = ToReadOnly(evaluations
            .Where(evaluation => evaluation.Result.Outcome == ModifierEligibilityOutcome.Ineligible)
            .Select(evaluation => evaluation.Candidate));

        if (eligibleCandidates.Count == 0)
        {
            return Unknown(
                index,
                modifier,
                generationType,
                candidates: [],
                ModifierCandidateResolutionDiagnosticCodes.ModifierNoEligibleCandidates,
                "All name and generation-kind candidates were excluded by item-base eligibility.",
                nameCandidates.Count,
                kindCandidates.Count,
                eligibilityCandidateCount: 0,
                excludedCandidates);
        }

        return ResolveTextSignatures(
            index,
            modifier,
            catalog,
            generationType,
            nameCandidates.Count,
            kindCandidates.Count,
            eligibleCandidates,
            excludedCandidates);
    }

    private ModifierCandidateResolutionResult ResolveTextSignatures(
        int index,
        ParsedModifier modifier,
        GameDataCatalog catalog,
        ModifierGenerationType generationType,
        int nameCandidateCount,
        int generationKindCandidateCount,
        IReadOnlyList<ModifierDefinition> eligibleCandidates,
        IReadOnlyList<ModifierDefinition> eligibilityExcludedCandidates)
    {
        var textEvaluations = eligibleCandidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Result = textSignatureMatcher.Match(candidate, catalog, modifier.ValueLines),
            })
            .ToArray();
        var retainedEvaluations = textEvaluations
            .Where(evaluation => evaluation.Result.Outcome != ModifierTextSignatureMatchOutcome.NoMatch)
            .ToArray();
        var textExcludedCandidates = textEvaluations
            .Where(evaluation => evaluation.Result.Outcome == ModifierTextSignatureMatchOutcome.NoMatch)
            .Select(evaluation => evaluation.Candidate)
            .ToArray();
        var finalCandidates = ToReadOnly(retainedEvaluations.Select(evaluation => evaluation.Candidate));
        var allExcludedCandidates = ToReadOnly(eligibilityExcludedCandidates.Concat(textExcludedCandidates));
        var textResults = ToReadOnly(textEvaluations.Select(evaluation => evaluation.Result));

        if (finalCandidates.Count == 0)
        {
            return Unknown(
                index,
                modifier,
                generationType,
                finalCandidates,
                ModifierCandidateResolutionDiagnosticCodes.ModifierTextNoMatch,
                "All eligible candidates were excluded by stat-text signature matching.",
                nameCandidateCount,
                generationKindCandidateCount,
                eligibleCandidates.Count,
                allExcludedCandidates,
                textSignatureCandidateCount: 0,
                excludedByTextCandidateCount: textExcludedCandidates.Length,
                textResults);
        }

        if (finalCandidates.Count == 1)
        {
            var retainedTextResult = retainedEvaluations[0].Result;
            var diagnosticCode = retainedTextResult.Outcome == ModifierTextSignatureMatchOutcome.Match
                ? ModifierCandidateResolutionDiagnosticCodes.ModifierTextExactMatch
                : ModifierCandidateResolutionDiagnosticCodes.ModifierTextNotEvaluated;
            var reason = retainedTextResult.Outcome == ModifierTextSignatureMatchOutcome.Match
                ? "Exactly one candidate remained after stat-text signature matching."
                : "Exactly one candidate remained, but stat-text signature matching could not verify it.";

            return new ModifierCandidateResolutionResult(
                index,
                modifier,
                modifier.Name,
                modifier.Kind,
                generationType,
                ModifierCandidateResolutionStatus.Exact,
                finalCandidates,
                Diagnostics(diagnosticCode, reason),
                nameCandidateCount,
                generationKindCandidateCount,
                eligibleCandidates.Count,
                allExcludedCandidates.Count,
                allExcludedCandidates,
                TextSignatureCandidateCount: finalCandidates.Count,
                ExcludedByTextCandidateCount: textExcludedCandidates.Length,
                TextSignatureMatches: textResults,
                Locality: DetermineLocality(retainedEvaluations[0].Candidate, catalog));
        }

        var allRetainedUnknown = retainedEvaluations.All(evaluation =>
            evaluation.Result.Outcome == ModifierTextSignatureMatchOutcome.Unknown);
        return Unknown(
            index,
            modifier,
            generationType,
            finalCandidates,
            allRetainedUnknown && textExcludedCandidates.Length == 0
                ? ModifierCandidateResolutionDiagnosticCodes.ModifierTextNotEvaluated
                : ModifierCandidateResolutionDiagnosticCodes.ModifierTextAmbiguous,
            allRetainedUnknown && textExcludedCandidates.Length == 0
                ? "Stat-text signature matching could not be evaluated for the retained candidates."
                : "Multiple candidates remained after stat-text signature matching.",
            nameCandidateCount,
            generationKindCandidateCount,
            eligibleCandidates.Count,
            allExcludedCandidates,
            textSignatureCandidateCount: finalCandidates.Count,
            excludedByTextCandidateCount: textExcludedCandidates.Length,
            textResults);
    }

    private static bool HasCandidateDiscoverySignal(ParsedModifier modifier)
    {
        return modifier.RawMetadataLine is not null
            || modifier.IsCrafted
            || modifier.IsFractured
            || modifier.IsVeiled;
    }

    private static bool TryMapGenerationType(
        ParsedModifierKind kind,
        out ModifierGenerationType generationType)
    {
        generationType = kind switch
        {
            ParsedModifierKind.Prefix => ModifierGenerationType.Prefix,
            ParsedModifierKind.Suffix => ModifierGenerationType.Suffix,
            ParsedModifierKind.Implicit => ModifierGenerationType.Implicit,
            _ => ModifierGenerationType.Unknown,
        };

        return generationType != ModifierGenerationType.Unknown;
    }

    private static bool TryGetEligibilityBase(
        ItemBaseResolutionResult baseResolution,
        out ItemBaseRecord itemBase)
    {
        itemBase = default!;
        if (baseResolution.MatchedItemBase is null ||
            baseResolution.Status is not (ItemBaseResolutionStatus.Exact or ItemBaseResolutionStatus.Probable))
        {
            return false;
        }

        itemBase = baseResolution.MatchedItemBase;
        return true;
    }

    private static ModifierCandidateResolutionResult MatchedWithoutEligibility(
        int index,
        ParsedModifier modifier,
        GameDataCatalog catalog,
        ModifierGenerationType generationType,
        ModifierDefinition candidate,
        int nameCandidateCount,
        int generationKindCandidateCount)
    {
        return new ModifierCandidateResolutionResult(
            index,
            modifier,
            modifier.Name,
            modifier.Kind,
            generationType,
            ModifierCandidateResolutionStatus.Exact,
            ToReadOnly([candidate]),
            Diagnostics(
                ModifierCandidateResolutionDiagnosticCodes.ModifierEligibilityNotEvaluated,
                "The parsed modifier name and generation type matched one catalog modifier, but item-base eligibility was not evaluated."),
            nameCandidateCount,
            generationKindCandidateCount,
            EligibilityCandidateCount: 1,
            Locality: DetermineLocality(candidate, catalog));
    }

    private static ModifierCandidateResolutionResult MatchedEligible(
        int index,
        ParsedModifier modifier,
        GameDataCatalog catalog,
        ModifierGenerationType generationType,
        ModifierDefinition candidate,
        int nameCandidateCount,
        int generationKindCandidateCount,
        IReadOnlyList<ModifierDefinition> excludedCandidates)
    {
        return new ModifierCandidateResolutionResult(
            index,
            modifier,
            modifier.Name,
            modifier.Kind,
            generationType,
            ModifierCandidateResolutionStatus.Exact,
            ToReadOnly([candidate]),
            Diagnostics(
                ModifierCandidateResolutionDiagnosticCodes.ModifierExactEligibleMatch,
                "Exactly one candidate remained after item-base eligibility filtering."),
            nameCandidateCount,
            generationKindCandidateCount,
            EligibilityCandidateCount: 1,
            excludedCandidates.Count,
            excludedCandidates,
            Locality: DetermineLocality(candidate, catalog));
    }

    private static ModifierCandidateResolutionResult Unknown(
        int index,
        ParsedModifier modifier,
        ModifierGenerationType? generationType,
        IReadOnlyList<ModifierDefinition> candidates,
        string diagnosticCode,
        string reason,
        int nameCandidateCount = 0,
        int generationKindCandidateCount = 0,
        int eligibilityCandidateCount = 0,
        IReadOnlyList<ModifierDefinition>? excludedCandidates = null,
        int textSignatureCandidateCount = 0,
        int excludedByTextCandidateCount = 0,
        IReadOnlyList<ModifierTextSignatureMatchResult>? textSignatureMatches = null)
    {
        excludedCandidates ??= [];
        return new ModifierCandidateResolutionResult(
            index,
            modifier,
            modifier.Name,
            modifier.Kind,
            generationType,
            ModifierCandidateResolutionStatus.Unknown,
            ToReadOnly(candidates),
            Diagnostics(diagnosticCode, reason),
            nameCandidateCount,
            generationKindCandidateCount,
            eligibilityCandidateCount,
            excludedCandidates.Count,
            excludedCandidates,
            textSignatureCandidateCount,
            excludedByTextCandidateCount,
            textSignatureMatches);
    }

    private static ModifierLocality DetermineLocality(
        ModifierDefinition candidate,
        GameDataCatalog catalog)
    {
        var statIds = candidate.Stats
            .Select(stat => stat.StatId?.Trim())
            .Where(statId => !string.IsNullOrWhiteSpace(statId))
            .ToArray();
        if (statIds.Length == 0)
        {
            return ModifierLocality.Unknown;
        }

        var localCount = 0;
        var globalCount = 0;
        foreach (var statId in statIds)
        {
            var stats = catalog.FindStatsById(statId);
            if (stats.Count != 1)
            {
                return ModifierLocality.Unknown;
            }

            if (stats[0].IsLocal)
            {
                localCount++;
            }
            else
            {
                globalCount++;
            }
        }

        return (localCount, globalCount) switch
        {
            (> 0, 0) => ModifierLocality.Local,
            (0, > 0) => ModifierLocality.Global,
            _ => ModifierLocality.Unknown,
        };
    }

    private static IReadOnlyList<ModifierCandidateResolutionDiagnostic> Diagnostics(string code, string reason)
    {
        return ToReadOnly([new ModifierCandidateResolutionDiagnostic(code, reason)]);
    }

    private static IReadOnlyList<T> ToReadOnly<T>(IEnumerable<T> values)
    {
        return new ReadOnlyCollection<T>(values.ToArray());
    }
}
