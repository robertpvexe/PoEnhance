using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed partial class PoBUniqueCatalogImporter
{
    public const string CurrentLevelVsChanceExportOwnerCollapseReason =
        "current-role-level-vs-chance-export-owner-collapse";

    /// <summary>
    /// Current-role <see cref="UniqueMechanicalConflictKind.LevelVsChanceOnHit"/> ExactConflict
    /// collapse when Export owns exactly one typed ModifierId among the mechanically relevant
    /// candidates and that owner carries a usable StatId vector. Orphan / Synthesis / Mutated
    /// competitors remain diagnostic evidence only and do not block Exact when ownership is
    /// singular and unambiguous. Does not apply to other conflict kinds or Historical versions.
    /// </summary>
    private static bool TryResolveCurrentLevelVsChanceExportOwnerCollapse(
        string canonicalUniqueName,
        string versionLabel,
        UniqueItemVersionRole versionRole,
        int? sourceVariantIndex,
        IReadOnlyList<string> blockLines,
        IReadOnlyList<MechanicalCandidate> candidates,
        PoBExportUniqueOwnershipIndex? ownershipIndex,
        out IReadOnlyList<MechanicalCandidate> survivors,
        out string? filterReason,
        out LevelVsChanceExportOwnerCollapseDiagnostic? diagnostic)
    {
        survivors = candidates;
        filterReason = null;
        diagnostic = null;

        if (versionRole != UniqueItemVersionRole.Current ||
            ownershipIndex is null ||
            candidates.Count < 2)
        {
            return false;
        }

        var conflictEvidence = BuildExactConflictEvidence(candidates);
        if (conflictEvidence.Kind != UniqueMechanicalConflictKind.LevelVsChanceOnHit)
        {
            diagnostic = new LevelVsChanceExportOwnerCollapseDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "conflict-kind-mismatch",
                VariantDecision: null,
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        if (!ownershipIndex.TryGetEntry(canonicalUniqueName, out var entry))
        {
            diagnostic = new LevelVsChanceExportOwnerCollapseDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "export-entry-missing",
                VariantDecision: null,
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        if (!PoBExportUniqueOwnershipParser.TryResolveVariantScope(
                entry,
                versionLabel,
                sourceVariantIndex,
                out var variantIndices,
                out var variantDecision))
        {
            diagnostic = new LevelVsChanceExportOwnerCollapseDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "variant-unresolved",
                VariantDecision: variantDecision,
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var residualTexts = PoBExportUniqueOwnershipParser.GetResidualTextsForScope(entry, variantIndices);
        var blockSignatures = blockLines
            .Select(NormalizeSignature)
            .Where(signature => signature.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        if (residualTexts.Any(text => blockSignatures.Contains(NormalizeSignature(text))))
        {
            diagnostic = new LevelVsChanceExportOwnerCollapseDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "residual-text-export-line",
                VariantDecision: variantDecision,
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var ownerModifierIds = PoBExportUniqueOwnershipParser.GetTypedOwnerModifierIds(entry, variantIndices);
        if (ownerModifierIds.Count == 0)
        {
            diagnostic = new LevelVsChanceExportOwnerCollapseDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "no-typed-owners-in-scope",
                VariantDecision: variantDecision,
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var owned = candidates
            .Where(candidate => ownerModifierIds.Contains(candidate.ModifierId))
            .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
            .ToArray();
        if (owned.Length == 0)
        {
            diagnostic = new LevelVsChanceExportOwnerCollapseDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "no-owned-candidate",
                VariantDecision: variantDecision,
                OwnerModifierIds: ownerModifierIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        if (owned.Length > 1)
        {
            diagnostic = new LevelVsChanceExportOwnerCollapseDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "multiple-owned-candidates",
                VariantDecision: variantDecision,
                OwnerModifierIds: owned.Select(candidate => candidate.ModifierId).ToArray(),
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var ownedCandidate = owned[0];
        if (ownedCandidate.StatIds.Count == 0 ||
            ownedCandidate.StatIds.All(string.IsNullOrWhiteSpace))
        {
            diagnostic = new LevelVsChanceExportOwnerCollapseDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "owned-statids-empty",
                VariantDecision: variantDecision,
                OwnerModifierIds: [ownedCandidate.ModifierId],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        // Reject when the sole Export-owned ModId is also typed-owned by a different Unique —
        // ownership ambiguity must remain fail-closed.
        if (ownershipIndex.IsOwnedByOtherUnique(ownedCandidate.ModifierId, canonicalUniqueName))
        {
            diagnostic = new LevelVsChanceExportOwnerCollapseDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "owned-modifier-cross-unique-ambiguous",
                VariantDecision: variantDecision,
                OwnerModifierIds: [ownedCandidate.ModifierId],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var nonOwned = candidates
            .Where(candidate => !ownerModifierIds.Contains(candidate.ModifierId))
            .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
            .ToArray();

        survivors = owned;
        filterReason = CurrentLevelVsChanceExportOwnerCollapseReason;
        diagnostic = new LevelVsChanceExportOwnerCollapseDiagnostic(
            canonicalUniqueName,
            versionLabel,
            Completeness: "level-vs-chance-export-owner-complete",
            VariantDecision: variantDecision,
            OwnerModifierIds: [ownedCandidate.ModifierId],
            RemovedModifierIds: nonOwned
                .Select(candidate => candidate.ModifierId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray(),
            SurvivingModifierIds: [ownedCandidate.ModifierId]);
        return true;
    }

    private sealed record LevelVsChanceExportOwnerCollapseDiagnostic(
        string CanonicalUniqueName,
        string VersionLabel,
        string Completeness,
        string? VariantDecision,
        IReadOnlyList<string> OwnerModifierIds,
        IReadOnlyList<string> RemovedModifierIds,
        IReadOnlyList<string> SurvivingModifierIds);
}
