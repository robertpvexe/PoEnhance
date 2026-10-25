using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed partial class PoBUniqueCatalogImporter
{
    public const string ExportOwnedRangeOverrideExactReason =
        "export-owned-range-override-exact";

    public const string ExportOwnedCurrentRePoEBaseReconcileExactReason =
        "export-owned-current-repoe-base-reconcile-exact";

    /// <summary>
    /// Exact recovery when PoB Export typed ownership carries trailing <c>ModId[min,max]…</c>
    /// instance roll overrides whose re-rendered ExactText uniquely matches the Data display
    /// block. For <see cref="UniqueItemVersionRole.Current"/>, overrides that disagree with the
    /// RePoE base roll are refused (stale Export/Data vs current game) — use
    /// <see cref="TryReconcileCurrentStaleExportOwnedRangeAgainstRePoEBase"/> instead.
    /// Historical roles may keep genuine legacy overrides that differ from the current RePoE base.
    /// </summary>
    private static bool TryResolveExportOwnedRangeOverrideExact(
        string canonicalUniqueName,
        string versionLabel,
        UniqueItemVersionRole versionRole,
        int? sourceVariantIndex,
        UniqueModifierBlockKind blockKind,
        IReadOnlyList<string> blockLines,
        string baseType,
        bool hasGeneratedOptionEvidence,
        MechanicalIndex mechanicalIndex,
        PoBExportUniqueOwnershipIndex? ownershipIndex,
        out IReadOnlyList<MechanicalCandidate> survivors,
        out string? filterReason,
        out ExportOwnedRangeOverrideExactDiagnostic? diagnostic)
    {
        survivors = [];
        filterReason = null;
        diagnostic = null;
        if (ownershipIndex is null ||
            blockLines.Count == 0 ||
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
            diagnostic = new ExportOwnedRangeOverrideExactDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: "variant-unresolved",
                OverrideOwnerModifierIds: [],
                MatchedModifierIds: [],
                RejectedModifierIds: [],
                SurvivingModifierId: null);
            return false;
        }

        var overrideOwners = PoBExportUniqueOwnershipParser.GetTypedOwnerLinesWithRangeOverrides(
            entry,
            variantIndices);
        if (overrideOwners.Count == 0)
        {
            diagnostic = new ExportOwnedRangeOverrideExactDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: "no-typed-range-override-owners",
                OverrideOwnerModifierIds: [],
                MatchedModifierIds: [],
                RejectedModifierIds: [],
                SurvivingModifierId: null);
            return false;
        }

        var matched = new List<(string ModifierId, MechanicalCandidate Candidate)>();
        var rejected = new List<string>();
        foreach (var owner in overrideOwners)
        {
            if (versionRole == UniqueItemVersionRole.Current &&
                !mechanicalIndex.ValueRangeOverridesMatchModifierBase(
                    owner.ModifierId!,
                    owner.ValueRangeOverrides))
            {
                rejected.Add($"{owner.ModifierId}:current-override-disagrees-with-repoe-base");
                continue;
            }

            if (!mechanicalIndex.TryCreateOwnedModifierCandidateWithRangeOverride(
                    owner.ModifierId!,
                    owner.ValueRangeOverrides,
                    blockKind,
                    blockLines,
                    baseType,
                    hasGeneratedOptionEvidence,
                    versionRole,
                    out var candidate,
                    out var rejectReason))
            {
                rejected.Add($"{owner.ModifierId}:{rejectReason ?? "rejected"}");
                continue;
            }

            matched.Add((owner.ModifierId!, candidate));
        }

        var overrideOwnerIds = overrideOwners
            .Select(owner => owner.ModifierId!)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (matched.Count == 0)
        {
            diagnostic = new ExportOwnedRangeOverrideExactDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: "no-override-render-match",
                OverrideOwnerModifierIds: overrideOwnerIds,
                MatchedModifierIds: [],
                RejectedModifierIds: rejected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                SurvivingModifierId: null);
            return false;
        }

        var distinctMatchedIds = matched
            .Select(pair => pair.ModifierId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (distinctMatchedIds.Length != 1)
        {
            diagnostic = new ExportOwnedRangeOverrideExactDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: "ambiguous-override-render-match",
                OverrideOwnerModifierIds: overrideOwnerIds,
                MatchedModifierIds: distinctMatchedIds,
                RejectedModifierIds: rejected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                SurvivingModifierId: null);
            return false;
        }

        survivors = [matched[0].Candidate];
        filterReason = ExportOwnedRangeOverrideExactReason;
        diagnostic = new ExportOwnedRangeOverrideExactDiagnostic(
            canonicalUniqueName,
            versionLabel,
            versionRole,
            blockKind,
            entry.RelativePath,
            variantDecision,
            Completeness: "complete",
            OverrideOwnerModifierIds: overrideOwnerIds,
            MatchedModifierIds: distinctMatchedIds,
            RejectedModifierIds: rejected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            SurvivingModifierId: distinctMatchedIds[0]);
        return true;
    }

    /// <summary>
    /// Current-role correction when Export <c>ModId[min,max]</c> and Data display agree on a
    /// legacy roll that disagrees with the pinned RePoE base for that owned ModId. Rewrites the
    /// packaged Fixed lines to the RePoE ExactText so live current clipboards (base roll) match,
    /// while preserving the Export-owned ModifierId / StatIds. Historical override Exact stays on
    /// <see cref="TryResolveExportOwnedRangeOverrideExact"/>.
    /// </summary>
    private static bool TryReconcileCurrentStaleExportOwnedRangeAgainstRePoEBase(
        string canonicalUniqueName,
        string versionLabel,
        UniqueItemVersionRole versionRole,
        int? sourceVariantIndex,
        UniqueModifierBlockKind blockKind,
        IReadOnlyList<string> blockLines,
        string baseType,
        bool hasGeneratedOptionEvidence,
        MechanicalIndex mechanicalIndex,
        PoBExportUniqueOwnershipIndex? ownershipIndex,
        out IReadOnlyList<string> reconciledLines,
        out IReadOnlyList<MechanicalCandidate> survivors,
        out string? filterReason,
        out ExportOwnedRangeOverrideExactDiagnostic? diagnostic)
    {
        reconciledLines = blockLines;
        survivors = [];
        filterReason = null;
        diagnostic = null;
        if (versionRole != UniqueItemVersionRole.Current ||
            ownershipIndex is null ||
            blockLines.Count == 0 ||
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
            diagnostic = new ExportOwnedRangeOverrideExactDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: "variant-unresolved",
                OverrideOwnerModifierIds: [],
                MatchedModifierIds: [],
                RejectedModifierIds: [],
                SurvivingModifierId: null);
            return false;
        }

        var overrideOwners = PoBExportUniqueOwnershipParser.GetTypedOwnerLinesWithRangeOverrides(
            entry,
            variantIndices);
        if (overrideOwners.Count == 0)
        {
            return false;
        }

        var matched = new List<(string ModifierId, MechanicalCandidate Candidate, string ExactText)>();
        var rejected = new List<string>();
        var blockKey = UnorderedMultilineKey(
            string.Join("\n", blockLines.Select(NormalizeExactEvidence)));
        foreach (var owner in overrideOwners)
        {
            if (mechanicalIndex.ValueRangeOverridesMatchModifierBase(
                    owner.ModifierId!,
                    owner.ValueRangeOverrides))
            {
                rejected.Add($"{owner.ModifierId}:override-matches-repoe-base");
                continue;
            }

            if (!mechanicalIndex.TryRenderOwnedModifierWithRangeOverrideExactText(
                    owner.ModifierId!,
                    owner.ValueRangeOverrides,
                    out var overrideExactText,
                    out var overrideReject))
            {
                rejected.Add($"{owner.ModifierId}:override-render:{overrideReject}");
                continue;
            }

            if (!string.Equals(
                    UnorderedMultilineKey(overrideExactText),
                    blockKey,
                    StringComparison.Ordinal))
            {
                rejected.Add($"{owner.ModifierId}:data-does-not-match-override");
                continue;
            }

            if (!mechanicalIndex.TryCreateOwnedModifierCandidateFromBaseExactRender(
                    owner.ModifierId!,
                    blockKind,
                    baseType,
                    hasGeneratedOptionEvidence,
                    versionRole,
                    out var candidate,
                    out var baseExactText,
                    out var candidateReject))
            {
                rejected.Add($"{owner.ModifierId}:base-candidate:{candidateReject}");
                continue;
            }

            matched.Add((owner.ModifierId!, candidate, baseExactText));
        }

        var overrideOwnerIds = overrideOwners
            .Select(owner => owner.ModifierId!)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (matched.Count == 0)
        {
            diagnostic = new ExportOwnedRangeOverrideExactDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: "no-current-repoe-reconcile",
                OverrideOwnerModifierIds: overrideOwnerIds,
                MatchedModifierIds: [],
                RejectedModifierIds: rejected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                SurvivingModifierId: null);
            return false;
        }

        var distinctMatchedIds = matched
            .Select(pair => pair.ModifierId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (distinctMatchedIds.Length != 1)
        {
            diagnostic = new ExportOwnedRangeOverrideExactDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: "ambiguous-current-repoe-reconcile",
                OverrideOwnerModifierIds: overrideOwnerIds,
                MatchedModifierIds: distinctMatchedIds,
                RejectedModifierIds: rejected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                SurvivingModifierId: null);
            return false;
        }

        var survivor = matched[0];
        reconciledLines = SplitRenderedExactTextLines(survivor.ExactText);
        survivors = [survivor.Candidate];
        filterReason = ExportOwnedCurrentRePoEBaseReconcileExactReason;
        diagnostic = new ExportOwnedRangeOverrideExactDiagnostic(
            canonicalUniqueName,
            versionLabel,
            versionRole,
            blockKind,
            entry.RelativePath,
            variantDecision,
            Completeness: "complete",
            OverrideOwnerModifierIds: overrideOwnerIds,
            MatchedModifierIds: distinctMatchedIds,
            RejectedModifierIds: rejected.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            SurvivingModifierId: distinctMatchedIds[0]);
        return true;
    }

    private static IReadOnlyList<string> SplitRenderedExactTextLines(string exactText) =>
        exactText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private sealed record ExportOwnedRangeOverrideExactDiagnostic(
        string CanonicalUniqueName,
        string VersionLabel,
        UniqueItemVersionRole VersionRole,
        UniqueModifierBlockKind BlockKind,
        string RelativePath,
        string VariantDecision,
        string Completeness,
        IReadOnlyList<string> OverrideOwnerModifierIds,
        IReadOnlyList<string> MatchedModifierIds,
        IReadOnlyList<string> RejectedModifierIds,
        string? SurvivingModifierId);
}
