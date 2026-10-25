using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed partial class PoBUniqueCatalogImporter
{
    /// <summary>
    /// Shared Current-role ExactConflict collapse when Export owns exactly one typed ModifierId
    /// among candidates with a usable StatId vector. Kind-specific callers supply the allowed
    /// conflict kind and provenance reason; residual text and cross-unique ownership stay
    /// fail-closed.
    /// </summary>
    private static bool TryResolveCurrentSingularExportOwnerCollapse<TDiagnostic>(
        string canonicalUniqueName,
        string versionLabel,
        UniqueItemVersionRole versionRole,
        int? sourceVariantIndex,
        IReadOnlyList<string> blockLines,
        IReadOnlyList<MechanicalCandidate> candidates,
        PoBExportUniqueOwnershipIndex? ownershipIndex,
        UniqueMechanicalConflictKind requiredKind,
        string successReason,
        string CompletenessComplete,
        out IReadOnlyList<MechanicalCandidate> survivors,
        out string? filterReason,
        out TDiagnostic? diagnostic,
        Func<string, string, string, string?, IReadOnlyList<string>, IReadOnlyList<string>, IReadOnlyList<string>, TDiagnostic> createDiagnostic)
        where TDiagnostic : class
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
        if (conflictEvidence.Kind != requiredKind)
        {
            diagnostic = createDiagnostic(
                canonicalUniqueName,
                versionLabel,
                "conflict-kind-mismatch",
                null,
                [],
                [],
                candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        if (!ownershipIndex.TryGetEntry(canonicalUniqueName, out var entry))
        {
            diagnostic = createDiagnostic(
                canonicalUniqueName,
                versionLabel,
                "export-entry-missing",
                null,
                [],
                [],
                candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        if (!PoBExportUniqueOwnershipParser.TryResolveVariantScope(
                entry,
                versionLabel,
                sourceVariantIndex,
                out var variantIndices,
                out var variantDecision))
        {
            diagnostic = createDiagnostic(
                canonicalUniqueName,
                versionLabel,
                "variant-unresolved",
                variantDecision,
                [],
                [],
                candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var residualTexts = PoBExportUniqueOwnershipParser.GetResidualTextsForScope(entry, variantIndices);
        var blockSignatures = blockLines
            .Select(NormalizeSignature)
            .Where(signature => signature.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
        if (residualTexts.Any(text => blockSignatures.Contains(NormalizeSignature(text))))
        {
            diagnostic = createDiagnostic(
                canonicalUniqueName,
                versionLabel,
                "residual-text-export-line",
                variantDecision,
                [],
                [],
                candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var ownerModifierIds = PoBExportUniqueOwnershipParser.GetTypedOwnerModifierIds(entry, variantIndices);
        if (ownerModifierIds.Count == 0)
        {
            diagnostic = createDiagnostic(
                canonicalUniqueName,
                versionLabel,
                "no-typed-owners-in-scope",
                variantDecision,
                [],
                [],
                candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var owned = candidates
            .Where(candidate => ownerModifierIds.Contains(candidate.ModifierId))
            .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
            .ToArray();
        if (owned.Length == 0)
        {
            diagnostic = createDiagnostic(
                canonicalUniqueName,
                versionLabel,
                "no-owned-candidate",
                variantDecision,
                ownerModifierIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                [],
                candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        if (owned.Length > 1)
        {
            diagnostic = createDiagnostic(
                canonicalUniqueName,
                versionLabel,
                "multiple-owned-candidates",
                variantDecision,
                owned.Select(candidate => candidate.ModifierId).ToArray(),
                [],
                candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var ownedCandidate = owned[0];
        if (ownedCandidate.StatIds.Count == 0 ||
            ownedCandidate.StatIds.All(string.IsNullOrWhiteSpace))
        {
            diagnostic = createDiagnostic(
                canonicalUniqueName,
                versionLabel,
                "owned-statids-empty",
                variantDecision,
                [ownedCandidate.ModifierId],
                [],
                candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        // Reject when the sole Export-owned ModId is also typed-owned by a different Unique —
        // ownership ambiguity must remain fail-closed.
        if (ownershipIndex.IsOwnedByOtherUnique(ownedCandidate.ModifierId, canonicalUniqueName))
        {
            diagnostic = createDiagnostic(
                canonicalUniqueName,
                versionLabel,
                "owned-modifier-cross-unique-ambiguous",
                variantDecision,
                [ownedCandidate.ModifierId],
                [],
                candidates.Select(candidate => candidate.ModifierId).ToArray());
            return false;
        }

        var nonOwned = candidates
            .Where(candidate => !ownerModifierIds.Contains(candidate.ModifierId))
            .DistinctBy(candidate => candidate.ModifierId, StringComparer.OrdinalIgnoreCase)
            .OrderBy(candidate => candidate.ModifierId, StringComparer.Ordinal)
            .ToArray();

        survivors = owned;
        filterReason = successReason;
        diagnostic = createDiagnostic(
            canonicalUniqueName,
            versionLabel,
            CompletenessComplete,
            variantDecision,
            [ownedCandidate.ModifierId],
            nonOwned
                .Select(candidate => candidate.ModifierId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray(),
            [ownedCandidate.ModifierId]);
        return true;
    }
}
