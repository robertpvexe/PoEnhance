namespace PoEnhance.DataImport;

public sealed partial class PoBUniqueCatalogImporter
{
    public const string PassageOwnedStatIdsSupersetCollapseReason =
        "passage-owned-statids-superset-collapse";

    /// <summary>
    /// Canonical PoB <c>ModItemExclusive</c> / RePoE translation FormatLine display-chrome token
    /// for jewel Passage presentation. Proven as a format line, not a ModifierId, StatId, Export
    /// ownership token, or radius selector. Kept centralized so production code does not scatter
    /// raw Passage string checks.
    /// </summary>
    internal const string PassageDisplayChromeToken = "Passage";

    /// <summary>
    /// SameDisplay ExactConflict collapse when Export owns exactly one candidate whose StatIds
    /// properly supersede every non-owned competitor under shared Passage display-chrome evidence.
    /// Does not synthesize Composition; keeps the owned multi-StatId vector as Exact.
    /// </summary>
    private static bool TryApplyPassageOwnedStatIdsSupersetCollapse(
        string canonicalUniqueName,
        string versionLabel,
        int? sourceVariantIndex,
        IReadOnlyList<string> blockLines,
        IReadOnlyList<MechanicalCandidate> candidates,
        PoBExportUniqueOwnershipIndex? ownershipIndex,
        out IReadOnlyList<MechanicalCandidate> survivors,
        out string? filterReason,
        out PassageOwnedStatIdsSupersetDiagnostic? diagnostic)
    {
        survivors = candidates;
        filterReason = null;
        diagnostic = null;
        if (ownershipIndex is null || candidates.Count < 2)
        {
            return false;
        }

        if (!TryGetSharedPassageDisplayChromeEvidence(
                blockLines,
                candidates,
                out var sharedPassageTranslationIds))
        {
            diagnostic = new PassageOwnedStatIdsSupersetDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "passage-evidence-incomplete",
                VariantDecision: null,
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray(),
                SharedPassageTranslationIds: []);
            return false;
        }

        if (!ownershipIndex.TryGetEntry(canonicalUniqueName, out var entry))
        {
            return false;
        }

        if (!PoBExportUniqueOwnershipParser.TryResolveVariantScope(
                entry,
                versionLabel,
                sourceVariantIndex,
                out var variantIndices,
                out var variantDecision))
        {
            diagnostic = new PassageOwnedStatIdsSupersetDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "variant-unresolved",
                VariantDecision: variantDecision,
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray(),
                SharedPassageTranslationIds: sharedPassageTranslationIds);
            return false;
        }

        var residualTexts = PoBExportUniqueOwnershipParser.GetResidualTextsForScope(entry, variantIndices);
        var blockSignatures = blockLines
            .Select(NormalizeSignature)
            .Where(signature => signature.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        if (residualTexts.Any(text => blockSignatures.Contains(NormalizeSignature(text))))
        {
            diagnostic = new PassageOwnedStatIdsSupersetDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "residual-text-export-line",
                VariantDecision: variantDecision,
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray(),
                SharedPassageTranslationIds: sharedPassageTranslationIds);
            return false;
        }

        var ownerModifierIds = PoBExportUniqueOwnershipParser.GetTypedOwnerModifierIds(entry, variantIndices);
        if (ownerModifierIds.Count == 0)
        {
            diagnostic = new PassageOwnedStatIdsSupersetDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "no-typed-owners-in-scope",
                VariantDecision: variantDecision,
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray(),
                SharedPassageTranslationIds: sharedPassageTranslationIds);
            return false;
        }

        var owned = candidates
            .Where(candidate => ownerModifierIds.Contains(candidate.ModifierId))
            .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
            .ToArray();
        if (owned.Length != 1)
        {
            diagnostic = new PassageOwnedStatIdsSupersetDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: owned.Length == 0 ? "no-owned-candidate" : "multiple-owned-candidates",
                VariantDecision: variantDecision,
                OwnerModifierIds: ownerModifierIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray(),
                SharedPassageTranslationIds: sharedPassageTranslationIds);
            return false;
        }

        var ownedCandidate = owned[0];
        if (ownedCandidate.StatIds.Count == 0)
        {
            diagnostic = new PassageOwnedStatIdsSupersetDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "owned-statids-empty",
                VariantDecision: variantDecision,
                OwnerModifierIds: [ownedCandidate.ModifierId],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray(),
                SharedPassageTranslationIds: sharedPassageTranslationIds);
            return false;
        }

        var nonOwned = candidates
            .Where(candidate => !ownerModifierIds.Contains(candidate.ModifierId))
            .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
            .ToArray();
        if (nonOwned.Length == 0)
        {
            diagnostic = new PassageOwnedStatIdsSupersetDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "no-nonowned-competitors",
                VariantDecision: variantDecision,
                OwnerModifierIds: [ownedCandidate.ModifierId],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray(),
                SharedPassageTranslationIds: sharedPassageTranslationIds);
            return false;
        }

        if (!nonOwned.All(candidate =>
                candidate.StatIds.Count > 0 &&
                IsProperStatIdsSubset(candidate.StatIds, ownedCandidate.StatIds)))
        {
            diagnostic = new PassageOwnedStatIdsSupersetDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "nonowned-not-proper-subset",
                VariantDecision: variantDecision,
                OwnerModifierIds: [ownedCandidate.ModifierId],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray(),
                SharedPassageTranslationIds: sharedPassageTranslationIds);
            return false;
        }

        var localities = candidates
            .Select(candidate => candidate.CandidateSemanticFingerprint.Locality)
            .Distinct()
            .ToArray();
        if (localities.Length != 1)
        {
            diagnostic = new PassageOwnedStatIdsSupersetDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "locality-incompatible",
                VariantDecision: variantDecision,
                OwnerModifierIds: [ownedCandidate.ModifierId],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray(),
                SharedPassageTranslationIds: sharedPassageTranslationIds);
            return false;
        }

        survivors = owned;
        filterReason = PassageOwnedStatIdsSupersetCollapseReason;
        diagnostic = new PassageOwnedStatIdsSupersetDiagnostic(
            canonicalUniqueName,
            versionLabel,
            Completeness: "passage-owned-superset-complete",
            VariantDecision: variantDecision,
            OwnerModifierIds: [ownedCandidate.ModifierId],
            RemovedModifierIds: nonOwned
                .Select(candidate => candidate.ModifierId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray(),
            SurvivingModifierIds: [ownedCandidate.ModifierId],
            SharedPassageTranslationIds: sharedPassageTranslationIds);
        return true;
    }

    private static bool TryGetSharedPassageDisplayChromeEvidence(
        IReadOnlyList<string> blockLines,
        IReadOnlyList<MechanicalCandidate> candidates,
        out IReadOnlyList<string> sharedPassageTranslationIds) =>
        HasPassageDisplayChromeEvidence(
            blockLines,
            candidates
                .Select(candidate => candidate.ProvenanceTranslations
                    .Select(evidence => new PassageTranslationFormatEvidence(
                        evidence.TranslationId,
                        evidence.FormatLines))
                    .ToArray())
                .ToArray(),
            out sharedPassageTranslationIds);

    /// <summary>
    /// Passage context is proven when the evaluated block and every candidate's matched
    /// translation FormatLines expose the canonical Passage display-chrome token, and all
    /// candidates share at least one Passage-bearing TranslationId.
    /// </summary>
    internal static bool HasPassageDisplayChromeEvidence(
        IReadOnlyList<string> blockLines,
        IReadOnlyList<IReadOnlyList<PassageTranslationFormatEvidence>> candidateTranslationEvidence,
        out IReadOnlyList<string> sharedPassageTranslationIds)
    {
        sharedPassageTranslationIds = [];
        if (candidateTranslationEvidence.Count == 0 ||
            !blockLines.Any(IsPassageDisplayChromeLine))
        {
            return false;
        }

        HashSet<string>? intersection = null;
        foreach (var evidenceSet in candidateTranslationEvidence)
        {
            var passageTranslationIds = evidenceSet
                .Where(evidence =>
                    !string.IsNullOrWhiteSpace(evidence.TranslationId) &&
                    evidence.FormatLines.Any(IsPassageDisplayChromeLine))
                .Select(evidence => evidence.TranslationId!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (passageTranslationIds.Count == 0)
            {
                return false;
            }

            if (intersection is null)
            {
                intersection = passageTranslationIds;
            }
            else
            {
                intersection.IntersectWith(passageTranslationIds);
                if (intersection.Count == 0)
                {
                    return false;
                }
            }
        }

        sharedPassageTranslationIds = intersection!
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        return sharedPassageTranslationIds.Count > 0;
    }

    internal static bool IsPassageDisplayChromeLine(string? line) =>
        !string.IsNullOrWhiteSpace(line) &&
        string.Equals(line.Trim(), PassageDisplayChromeToken, StringComparison.Ordinal);

    internal static bool IsProperStatIdsSubset(
        IReadOnlyList<string> subset,
        IReadOnlyList<string> superset)
    {
        if (subset.Count == 0 ||
            superset.Count == 0 ||
            subset.Count >= superset.Count)
        {
            return false;
        }

        var subsetSet = subset
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var supersetSet = superset
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (subsetSet.Count == 0 ||
            subsetSet.Count != subset.Count ||
            supersetSet.Count != superset.Count)
        {
            return false;
        }

        return subsetSet.IsProperSubsetOf(supersetSet);
    }

    internal readonly record struct PassageTranslationFormatEvidence(
        string? TranslationId,
        IReadOnlyList<string> FormatLines);

    private sealed record PassageOwnedStatIdsSupersetDiagnostic(
        string CanonicalUniqueName,
        string VersionLabel,
        string Completeness,
        string? VariantDecision,
        IReadOnlyList<string> OwnerModifierIds,
        IReadOnlyList<string> RemovedModifierIds,
        IReadOnlyList<string> SurvivingModifierIds,
        IReadOnlyList<string> SharedPassageTranslationIds);
}
