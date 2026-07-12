using System.Collections.ObjectModel;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed class ParsedItemModifierCandidateResolver
{
    private readonly ParsedItemBaseResolver baseResolver = new();
    private readonly ModifierEligibilityEvaluator eligibilityEvaluator = new();

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

        return eligibleCandidates.Count switch
        {
            1 => MatchedEligible(
                index,
                modifier,
                generationType,
                eligibleCandidates[0],
                nameCandidates.Count,
                kindCandidates.Count,
                excludedCandidates),
            0 => Unknown(
                index,
                modifier,
                generationType,
                candidates: [],
                ModifierCandidateResolutionDiagnosticCodes.ModifierNoEligibleCandidates,
                "All name and generation-kind candidates were excluded by item-base eligibility.",
                nameCandidates.Count,
                kindCandidates.Count,
                eligibilityCandidateCount: 0,
                excludedCandidates),
            _ => Unknown(
                index,
                modifier,
                generationType,
                eligibleCandidates,
                ModifierCandidateResolutionDiagnosticCodes.ModifierEligibilityAmbiguous,
                "Multiple candidates remained after item-base eligibility filtering.",
                nameCandidates.Count,
                kindCandidates.Count,
                eligibleCandidates.Count,
                excludedCandidates),
        };
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
            EligibilityCandidateCount: 1);
    }

    private static ModifierCandidateResolutionResult MatchedEligible(
        int index,
        ParsedModifier modifier,
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
            excludedCandidates);
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
        IReadOnlyList<ModifierDefinition>? excludedCandidates = null)
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
            excludedCandidates);
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
