using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed partial class PoBUniqueCatalogImporter
{
    public const string CurrentSameDisplayExportOwnerCollapseReason =
        "current-role-same-display-export-owner-collapse";

    /// <summary>
    /// Current-role <see cref="UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIds"/>
    /// ExactConflict collapse when Export owns exactly one typed ModifierId among candidates and
    /// that owner carries a usable StatId vector. Same-item orphan / Updated / Divergent / New
    /// forks remain diagnostic evidence only and do not block Exact when ownership is singular.
    /// Residual text-only Export lines and cross-unique ownership ambiguity stay fail-closed.
    /// Passage display-chrome blocks stay under the Passage-owned StatIds-superset rule and are
    /// not collapsed here. Does not apply to Historical versions or other conflict kinds.
    /// </summary>
    private static bool TryResolveCurrentSameDisplayExportOwnerCollapse(
        string canonicalUniqueName,
        string versionLabel,
        UniqueItemVersionRole versionRole,
        int? sourceVariantIndex,
        IReadOnlyList<string> blockLines,
        IReadOnlyList<MechanicalCandidate> candidates,
        PoBExportUniqueOwnershipIndex? ownershipIndex,
        out IReadOnlyList<MechanicalCandidate> survivors,
        out string? filterReason,
        out SameDisplayExportOwnerCollapseDiagnostic? diagnostic)
    {
        survivors = candidates;
        filterReason = null;
        diagnostic = null;

        // Passage chrome has its own stricter ExactConflict path; do not bypass it.
        if (blockLines.Any(IsPassageDisplayChromeLine) ||
            candidates.Any(candidate =>
                candidate.ProvenanceTranslations.Any(evidence =>
                    evidence.FormatLines.Any(IsPassageDisplayChromeLine))))
        {
            diagnostic = new SameDisplayExportOwnerCollapseDiagnostic(
                canonicalUniqueName,
                versionLabel,
                Completeness: "passage-chrome-deferred",
                VariantDecision: null,
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        return TryResolveCurrentSingularExportOwnerCollapse(
            canonicalUniqueName,
            versionLabel,
            versionRole,
            sourceVariantIndex,
            blockLines,
            candidates,
            ownershipIndex,
            UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIds,
            CurrentSameDisplayExportOwnerCollapseReason,
            CompletenessComplete: "same-display-export-owner-complete",
            out survivors,
            out filterReason,
            out diagnostic,
            static (name, label, completeness, variant, owners, removed, surviving) =>
                new SameDisplayExportOwnerCollapseDiagnostic(
                    name, label, completeness, variant, owners, removed, surviving));
    }

    private sealed record SameDisplayExportOwnerCollapseDiagnostic(
        string CanonicalUniqueName,
        string VersionLabel,
        string Completeness,
        string? VariantDecision,
        IReadOnlyList<string> OwnerModifierIds,
        IReadOnlyList<string> RemovedModifierIds,
        IReadOnlyList<string> SurvivingModifierIds);
}
