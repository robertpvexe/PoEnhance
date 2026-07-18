using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

public sealed partial class ParsedItemModifierCandidateResolver
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
            var specialImplicitResult = TryResolveSpecialImplicit(
                index,
                modifier,
                catalog,
                eligibilityContext,
                generationType);
            if (specialImplicitResult is not null)
            {
                return specialImplicitResult;
            }

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
            if (TryResolveStructurally(
                    index,
                    modifier,
                    catalog,
                    eligibilityContext,
                    generationType,
                    nameCandidates.Count,
                    out var structuralResult))
            {
                return structuralResult;
            }

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

        if (modifier.IsCrafted)
        {
            var craftedCandidates = ToReadOnly(kindCandidates.Where(candidate =>
                string.Equals(Normalize(candidate.Domain), "crafted", StringComparison.OrdinalIgnoreCase)));
            if (craftedCandidates.Count == 0)
            {
                return Unknown(
                    index,
                    modifier,
                    generationType,
                    candidates: [],
                    ModifierCandidateResolutionDiagnosticCodes.ModifierNotFound,
                    "No crafted-domain catalog modifier matched the parsed modifier name and generation type.",
                    nameCandidates.Count,
                    kindCandidates.Count,
                    eligibilityCandidateCount: 0);
            }

            if (craftedCandidates.Count == 1)
            {
                return MatchedWithoutEligibility(
                    index,
                    modifier,
                    catalog,
                    generationType,
                    craftedCandidates[0],
                    nameCandidates.Count,
                    kindCandidates.Count);
            }

            // Crafted modifiers belong to the catalog's crafted domain rather than the
            // item's domain. Their copied provenance and stat/range text provide the
            // appropriate evidence; ordinary item spawn-weight eligibility does not.
            return ResolveTextSignatures(
                index,
                modifier,
                catalog,
                generationType,
                nameCandidates.Count,
                kindCandidates.Count,
                craftedCandidates,
                eligibilityExcludedCandidates: []);
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
            var structurallyCompatibleCandidates = ToReadOnly(evaluations
                .Where(evaluation => IsStructurallyCompatibleDespiteSpawnWeight(evaluation.Candidate, eligibilityContext))
                .Select(evaluation => evaluation.Candidate));
            if (structurallyCompatibleCandidates.Count > 0)
            {
                var structuralResult = ResolveTextSignatures(
                    index,
                    modifier,
                    catalog,
                    generationType,
                    nameCandidates.Count,
                    kindCandidates.Count,
                    structurallyCompatibleCandidates,
                    excludedCandidates);
                if (structuralResult.Status == ModifierCandidateResolutionStatus.Exact)
                {
                    return structuralResult;
                }
            }

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

    private ModifierCandidateResolutionResult? TryResolveSpecialImplicit(
        int index,
        ParsedModifier modifier,
        GameDataCatalog catalog,
        ItemModifierEligibilityContext? eligibilityContext,
        ModifierGenerationType generationType)
    {
        if (generationType != ModifierGenerationType.Implicit)
        {
            return null;
        }

        var originCandidates = modifier.ImplicitOrigin switch
        {
            ParsedImplicitModifierOrigin.SearingExarch => catalog.Modifiers
                .Where(candidate => IsEldritchSource(candidate, ParsedImplicitModifierOrigin.SearingExarch)),
            ParsedImplicitModifierOrigin.EaterOfWorlds => catalog.Modifiers
                .Where(candidate => IsEldritchSource(candidate, ParsedImplicitModifierOrigin.EaterOfWorlds)),
            ParsedImplicitModifierOrigin.Synthesis => catalog.Modifiers.Where(IsSynthesisImplicitSource),
            _ => null,
        };
        if (originCandidates is null)
        {
            return null;
        }

        var originAndTierCandidates = originCandidates
            .Where(candidate => modifier.EldritchTier is null ||
                MatchesEldritchTier(candidate, modifier.EldritchTier.Value))
            .ToArray();
        var candidates = originAndTierCandidates
            .Where(candidate => SpecialImplicitValuesMatchCandidate(modifier, candidate))
            .ToArray();
        if (candidates.Length == 0)
        {
            return Unknown(
                index,
                modifier,
                generationType,
                candidates: [],
                ModifierCandidateResolutionDiagnosticCodes.ModifierNotFound,
                "No GameData modifier matched the parsed implicit source origin, tier, and copied roll values.");
        }

        if (modifier.ImplicitOrigin == ParsedImplicitModifierOrigin.Synthesis || eligibilityContext is null)
        {
            return ResolveTextSignatures(
                index,
                modifier,
                catalog,
                generationType,
                nameCandidateCount: 0,
                generationKindCandidateCount: candidates.Length,
                candidates,
                eligibilityExcludedCandidates: []);
        }

        var evaluations = candidates
            .Select(candidate => new
            {
                Candidate = candidate,
                Result = eligibilityEvaluator.Evaluate(candidate, eligibilityContext),
            })
            .ToArray();
        var retainedCandidates = evaluations
            .Where(evaluation => evaluation.Result.Outcome != ModifierEligibilityOutcome.Ineligible)
            .Select(evaluation => evaluation.Candidate)
            .ToArray();
        var excludedCandidates = evaluations
            .Where(evaluation => evaluation.Result.Outcome == ModifierEligibilityOutcome.Ineligible)
            .Select(evaluation => evaluation.Candidate)
            .ToArray();
        if (retainedCandidates.Length == 0)
        {
            return Unknown(
                index,
                modifier,
                generationType,
                candidates: [],
                ModifierCandidateResolutionDiagnosticCodes.ModifierNoEligibleCandidates,
                "All implicit source-origin candidates were excluded by item-base eligibility.",
                nameCandidateCount: 0,
                generationKindCandidateCount: candidates.Length,
                eligibilityCandidateCount: 0,
                excludedCandidates);
        }

        return ResolveTextSignatures(
            index,
            modifier,
            catalog,
            generationType,
            nameCandidateCount: 0,
            generationKindCandidateCount: candidates.Length,
            retainedCandidates,
            excludedCandidates);
    }

    private static bool SpecialImplicitValuesMatchCandidate(
        ParsedModifier modifier,
        ModifierDefinition candidate)
    {
        var advancedRanges = ExtractAdvancedStatRanges(modifier.ValueLines);
        if (advancedRanges.Count > 0)
        {
            return CandidateRangesMatch(candidate, advancedRanges);
        }

        var observedValues = ExtractDisplayedStatValues(modifier.ValueLines);
        var stats = candidate.Stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
            .OrderBy(stat => stat.Index)
            .ToArray();
        if (observedValues.Count == 0 || stats.Length != observedValues.Count)
        {
            return false;
        }

        for (var index = 0; index < stats.Length; index++)
        {
            var minimum = stats[index].MinValue;
            var maximum = stats[index].MaxValue;
            if (!minimum.HasValue ||
                !maximum.HasValue ||
                observedValues[index] < minimum.Value ||
                observedValues[index] > maximum.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<decimal> ExtractDisplayedStatValues(IReadOnlyList<string> valueLines)
    {
        var values = new List<decimal>();
        foreach (var line in valueLines)
        {
            foreach (Match match in DisplayedStatValuePattern().Matches(line))
            {
                if (!decimal.TryParse(
                        match.Groups["value"].Value,
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var value))
                {
                    return [];
                }

                values.Add(value);
            }
        }

        return values;
    }

    private static bool IsEldritchSource(
        ModifierDefinition candidate,
        ParsedImplicitModifierOrigin origin)
    {
        var sourceGeneration = NormalizeSourceGeneration(candidate.SourceGenerationType);
        return origin switch
        {
            ParsedImplicitModifierOrigin.SearingExarch => sourceGeneration is
                "exarch_implicit" or "searing_exarch_implicit",
            ParsedImplicitModifierOrigin.EaterOfWorlds => sourceGeneration is
                "eater_implicit" or "eater_of_worlds_implicit",
            _ => false,
        };
    }

    private static bool IsSynthesisImplicitSource(ModifierDefinition candidate)
    {
        if (candidate.GenerationType != ModifierGenerationType.Implicit)
        {
            return false;
        }

        return IsSynthesisImplicitIdentity(candidate.Id) ||
            candidate.Sources.Any(source => IsSynthesisImplicitIdentity(source.ExternalId));
    }

    private static bool IsSynthesisImplicitIdentity(string? value)
    {
        return value?.Trim().StartsWith("SynthesisImplicit", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool MatchesEldritchTier(
        ModifierDefinition candidate,
        ParsedEldritchImplicitTier tier)
    {
        var excludedTier = 7 - (int)tier;
        var tierTag = $"no_tier_{excludedTier}_eldritch_implicit";
        return candidate.SpawnWeights.Any(spawnWeight =>
            spawnWeight.Weight == 0 &&
            string.Equals(
                NormalizeSourceGeneration(spawnWeight.Tag),
                tierTag,
                StringComparison.Ordinal));
    }

    private static string NormalizeSourceGeneration(string? value)
    {
        return (value?.Trim() ?? string.Empty)
            .Replace('-', '_')
            .Replace(' ', '_')
            .ToLowerInvariant();
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

        if (TrySelectOneByAdvancedRange(
                modifier,
                finalCandidates,
                out var rangeSelectedCandidate,
                out var rangeExcludedCandidates))
        {
            return MatchedByStructuralEvidence(
                index,
                modifier,
                catalog,
                generationType,
                rangeSelectedCandidate,
                nameCandidateCount,
                generationKindCandidateCount,
                eligibleCandidates.Count,
                allExcludedCandidates.Concat(rangeExcludedCandidates).ToArray(),
                finalCandidates.Count,
                textExcludedCandidates.Length,
                textResults,
                "Exactly one candidate remained after Advanced Item Description stat-range matching.");
        }

        if (TrySelectOneByDisplayedTier(
                modifier,
                finalCandidates,
                out var tierSelectedCandidate,
                out var tierExcludedCandidates))
        {
            return MatchedByStructuralEvidence(
                index,
                modifier,
                catalog,
                generationType,
                tierSelectedCandidate,
                nameCandidateCount,
                generationKindCandidateCount,
                eligibleCandidates.Count,
                allExcludedCandidates.Concat(tierExcludedCandidates).ToArray(),
                finalCandidates.Count,
                textExcludedCandidates.Length,
                textResults,
                "Exactly one candidate remained after displayed tier disambiguation.");
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

    private bool TryResolveStructurally(
        int index,
        ParsedModifier modifier,
        GameDataCatalog catalog,
        ItemModifierEligibilityContext? eligibilityContext,
        ModifierGenerationType generationType,
        int nameCandidateCount,
        out ModifierCandidateResolutionResult result)
    {
        result = default!;
        if (eligibilityContext is null)
        {
            return false;
        }

        var kindCandidates = ToReadOnly(catalog.FindModifiersByGenerationType(generationType));
        if (kindCandidates.Count == 0)
        {
            return false;
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
            return false;
        }

        var eligibleCandidates = ToReadOnly(evaluations
            .Where(evaluation => evaluation.Result.Outcome == ModifierEligibilityOutcome.Eligible)
            .Select(evaluation => evaluation.Candidate));
        if (eligibleCandidates.Count == 0)
        {
            return false;
        }

        var excludedCandidates = ToReadOnly(evaluations
            .Where(evaluation => evaluation.Result.Outcome == ModifierEligibilityOutcome.Ineligible)
            .Select(evaluation => evaluation.Candidate));

        result = ResolveTextSignatures(
            index,
            modifier,
            catalog,
            generationType,
            nameCandidateCount,
            kindCandidates.Count,
            eligibleCandidates,
            excludedCandidates);
        return result.Status == ModifierCandidateResolutionStatus.Exact;
    }

    private static ModifierCandidateResolutionResult MatchedByStructuralEvidence(
        int index,
        ParsedModifier modifier,
        GameDataCatalog catalog,
        ModifierGenerationType generationType,
        ModifierDefinition candidate,
        int nameCandidateCount,
        int generationKindCandidateCount,
        int eligibilityCandidateCount,
        IReadOnlyList<ModifierDefinition> excludedCandidates,
        int textSignatureCandidateCount,
        int excludedByTextCandidateCount,
        IReadOnlyList<ModifierTextSignatureMatchResult> textResults,
        string reason)
    {
        return new ModifierCandidateResolutionResult(
            index,
            modifier,
            modifier.Name,
            modifier.Kind,
            generationType,
            ModifierCandidateResolutionStatus.Exact,
            ToReadOnly([candidate]),
            Diagnostics(ModifierCandidateResolutionDiagnosticCodes.ModifierTextExactMatch, reason),
            nameCandidateCount,
            generationKindCandidateCount,
            eligibilityCandidateCount,
            excludedCandidates.Count,
            excludedCandidates,
            textSignatureCandidateCount,
            excludedByTextCandidateCount,
            textResults,
            DetermineLocality(candidate, catalog));
    }

    private static bool TrySelectOneByAdvancedRange(
        ParsedModifier modifier,
        IReadOnlyList<ModifierDefinition> candidates,
        out ModifierDefinition selectedCandidate,
        out IReadOnlyList<ModifierDefinition> excludedCandidates)
    {
        selectedCandidate = default!;
        excludedCandidates = [];
        var ranges = ExtractAdvancedStatRanges(modifier.ValueLines);
        if (ranges.Count == 0)
        {
            return false;
        }

        var retained = candidates
            .Where(candidate => CandidateRangesMatch(candidate, ranges))
            .ToArray();
        if (retained.Length != 1)
        {
            return false;
        }

        var selected = retained[0];
        selectedCandidate = selected;
        excludedCandidates = candidates
            .Where(candidate => !ReferenceEquals(candidate, selected))
            .ToArray();
        return true;
    }

    private static bool TrySelectOneByDisplayedTier(
        ParsedModifier modifier,
        IReadOnlyList<ModifierDefinition> candidates,
        out ModifierDefinition selectedCandidate,
        out IReadOnlyList<ModifierDefinition> excludedCandidates)
    {
        selectedCandidate = default!;
        excludedCandidates = [];
        if (!modifier.Tier.HasValue)
        {
            return false;
        }

        var retained = candidates
            .Where(candidate => candidate.Tier == modifier.Tier.Value)
            .ToArray();
        if (retained.Length != 1)
        {
            return false;
        }

        var selected = retained[0];
        selectedCandidate = selected;
        excludedCandidates = candidates
            .Where(candidate => !ReferenceEquals(candidate, selected))
            .ToArray();
        return true;
    }

    private static bool CandidateRangesMatch(
        ModifierDefinition candidate,
        IReadOnlyList<AdvancedStatRange> ranges)
    {
        var stats = candidate.Stats
            .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
            .OrderBy(stat => stat.Index)
            .ToArray();
        if (stats.Length != ranges.Count)
        {
            return false;
        }

        for (var index = 0; index < stats.Length; index++)
        {
            var minimum = stats[index].MinValue;
            var maximum = stats[index].MaxValue;
            if (!minimum.HasValue ||
                !maximum.HasValue ||
                minimum.Value != ranges[index].Minimum ||
                maximum.Value != ranges[index].Maximum)
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<AdvancedStatRange> ExtractAdvancedStatRanges(
        IReadOnlyList<string> valueLines)
    {
        var ranges = new List<AdvancedStatRange>();
        foreach (var line in valueLines)
        {
            foreach (Match match in AdvancedRangePattern().Matches(line))
            {
                if (!decimal.TryParse(
                        match.Groups["minimum"].Value,
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var minimum) ||
                    !decimal.TryParse(
                        match.Groups["maximum"].Value,
                        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture,
                        out var maximum))
                {
                    return [];
                }

                ranges.Add(new AdvancedStatRange(minimum, maximum));
            }
        }

        return ranges;
    }

    private static bool IsStructurallyCompatibleDespiteSpawnWeight(
        ModifierDefinition candidate,
        ItemModifierEligibilityContext context)
    {
        var modifierDomain = Normalize(candidate.Domain);
        var itemBaseDomain = Normalize(context.ItemBase.Domain);
        return modifierDomain is not null &&
            itemBaseDomain is not null &&
            string.Equals(modifierDomain, itemBaseDomain, StringComparison.OrdinalIgnoreCase) &&
            HasOnlyZeroDefaultSpawnWeights(candidate);
    }

    private static bool HasOnlyZeroDefaultSpawnWeights(ModifierDefinition candidate)
    {
        return candidate.SpawnWeights.Count > 0 &&
            candidate.SpawnWeights.All(spawnWeight =>
                string.Equals(
                    Normalize(spawnWeight.Tag),
                    "default",
                    StringComparison.OrdinalIgnoreCase) &&
                spawnWeight.Weight == 0);
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

    private static string? Normalize(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
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

    private sealed record AdvancedStatRange(decimal Minimum, decimal Maximum);

    [GeneratedRegex(@"[+-]?\d+(?:\.\d+)?\((?<minimum>[+-]?\d+(?:\.\d+)?)-(?<maximum>[+-]?\d+(?:\.\d+)?)\)", RegexOptions.CultureInvariant)]
    private static partial Regex AdvancedRangePattern();

    [GeneratedRegex(@"(?<![\w])(?<value>[+-]?\d+(?:\.\d+)?)(?:\([+-]?\d+(?:\.\d+)?-[+-]?\d+(?:\.\d+)?\))?", RegexOptions.CultureInvariant)]
    private static partial Regex DisplayedStatValuePattern();
}
