namespace PoEnhance.DataImport;

public sealed partial class PoBUniqueCatalogImporter
{
    public const string ExportUniqueOwnershipFilterReason = "export-unique-ownership-filter";

    /// <summary>
    /// Applies typed PoB Export Uniques ownership for SameDisplay ExactConflicts when the
    /// block has proven typed Export correspondence and at least one candidate is owned by a
    /// different Unique (cross-item pollution). Same-item orphan forks and residual text-only
    /// Export lines remain fail-closed. Encoding-class ExactConflicts are not handled here.
    /// </summary>
    private static bool TryApplyExportUniqueOwnershipFilter(
        string canonicalUniqueName,
        string versionLabel,
        int? sourceVariantIndex,
        IReadOnlyList<string> blockLines,
        IReadOnlyList<MechanicalCandidate> candidates,
        PoBExportUniqueOwnershipIndex? ownershipIndex,
        out IReadOnlyList<MechanicalCandidate> survivors,
        out string? filterReason,
        out ExportOwnershipFilterDiagnostic? diagnostic)
    {
        survivors = candidates;
        filterReason = null;
        diagnostic = null;
        if (ownershipIndex is null ||
            candidates.Count < 2 ||
            !ownershipIndex.TryGetEntry(canonicalUniqueName, out var entry))
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
            diagnostic = new ExportOwnershipFilterDiagnostic(
                canonicalUniqueName,
                versionLabel,
                entry.RelativePath,
                variantDecision,
                Completeness: "variant-unresolved",
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
            diagnostic = new ExportOwnershipFilterDiagnostic(
                canonicalUniqueName,
                versionLabel,
                entry.RelativePath,
                variantDecision,
                Completeness: "residual-text-export-line",
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var ownerModifierIds = PoBExportUniqueOwnershipParser.GetTypedOwnerModifierIds(entry, variantIndices);
        if (ownerModifierIds.Count == 0)
        {
            diagnostic = new ExportOwnershipFilterDiagnostic(
                canonicalUniqueName,
                versionLabel,
                entry.RelativePath,
                variantDecision,
                Completeness: "no-typed-owners-in-scope",
                OwnerModifierIds: [],
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var owned = candidates
            .Where(candidate => ownerModifierIds.Contains(candidate.ModifierId))
            .ToArray();
        if (owned.Length == 0)
        {
            // Evaluated block has no typed Export ModifierId among candidates — incomplete.
            diagnostic = new ExportOwnershipFilterDiagnostic(
                canonicalUniqueName,
                versionLabel,
                entry.RelativePath,
                variantDecision,
                Completeness: "no-owned-candidate",
                OwnerModifierIds: ownerModifierIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var foreign = candidates
            .Where(candidate =>
                !ownerModifierIds.Contains(candidate.ModifierId) &&
                ownershipIndex.IsOwnedByOtherUnique(candidate.ModifierId, canonicalUniqueName))
            .ToArray();
        if (foreign.Length == 0)
        {
            // Remaining extras are Export orphans / same-item forks — do not owner-filter.
            diagnostic = new ExportOwnershipFilterDiagnostic(
                canonicalUniqueName,
                versionLabel,
                entry.RelativePath,
                variantDecision,
                Completeness: "same-item-or-orphan-only",
                OwnerModifierIds: ownerModifierIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                RemovedModifierIds: [],
                SurvivingModifierIds: candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        survivors = owned;
        filterReason = ExportUniqueOwnershipFilterReason;
        diagnostic = new ExportOwnershipFilterDiagnostic(
            canonicalUniqueName,
            versionLabel,
            entry.RelativePath,
            variantDecision,
            Completeness: "typed-cross-item-complete",
            OwnerModifierIds: ownerModifierIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
            RemovedModifierIds: candidates
                .Where(candidate => !ownerModifierIds.Contains(candidate.ModifierId))
                .Select(candidate => candidate.ModifierId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray(),
            SurvivingModifierIds: owned
                .Select(candidate => candidate.ModifierId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray());
        return true;
    }

    private sealed record ExportOwnershipFilterDiagnostic(
        string CanonicalUniqueName,
        string VersionLabel,
        string ExportRelativePath,
        string VariantDecision,
        string Completeness,
        IReadOnlyList<string> OwnerModifierIds,
        IReadOnlyList<string> RemovedModifierIds,
        IReadOnlyList<string> SurvivingModifierIds);
}
