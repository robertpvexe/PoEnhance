using PoEnhance.GameData;

namespace PoEnhance.DataImport;

public sealed partial class PoBUniqueCatalogImporter
{
    public const string ExportOwnerModTextMapPositiveExactReason =
        "export-owner-modtextmap-positive-exact";

    /// <summary>
    /// Fallback Exact recovery when normal MechanicalIndex resolution found no candidates.
    /// Requires convergence of Export typed ownership, ModTextMap key→ModifierId relation,
    /// and a non-disabled RePoE ModifierId with StatIds. Never uses ModTextMap alone.
    /// </summary>
    private static bool TryApplyExportOwnerModTextMapPositiveExact(
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
        PoBModTextMapIndex? modTextMap,
        out IReadOnlyList<MechanicalCandidate> survivors,
        out string? filterReason,
        out ExportOwnerModTextMapBridgeDiagnostic? diagnostic)
    {
        survivors = [];
        filterReason = null;
        diagnostic = null;
        if (ownershipIndex is null ||
            modTextMap is null ||
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
            diagnostic = new ExportOwnerModTextMapBridgeDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: "variant-unresolved",
                OwnerModifierIds: [],
                ModTextMapModifierIds: [],
                IntersectionModifierIds: [],
                RejectedModifierIds: [],
                SurvivingModifierId: null);
            return false;
        }

        var ownerModifierIds = PoBExportUniqueOwnershipParser.GetTypedOwnerModifierIds(
            entry,
            variantIndices);
        if (ownerModifierIds.Count == 0)
        {
            diagnostic = new ExportOwnerModTextMapBridgeDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: "no-typed-owners-in-scope",
                OwnerModifierIds: [],
                ModTextMapModifierIds: [],
                IntersectionModifierIds: [],
                RejectedModifierIds: [],
                SurvivingModifierId: null);
            return false;
        }

        HashSet<string>? modTextIntersection = null;
        var unionModTextIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in blockLines)
        {
            if (!modTextMap.TryGetModifierIds(line, out var lineModifierIds))
            {
                diagnostic = new ExportOwnerModTextMapBridgeDiagnostic(
                    canonicalUniqueName,
                    versionLabel,
                    versionRole,
                    blockKind,
                    entry.RelativePath,
                    variantDecision,
                    Completeness: "modtextmap-key-missing",
                    OwnerModifierIds: OrderedIds(ownerModifierIds),
                    ModTextMapModifierIds: OrderedIds(unionModTextIds),
                    IntersectionModifierIds: [],
                    RejectedModifierIds: [],
                    SurvivingModifierId: null);
                return false;
            }

            foreach (var id in lineModifierIds)
            {
                unionModTextIds.Add(id);
            }

            var lineSet = lineModifierIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            modTextIntersection = modTextIntersection is null
                ? lineSet
                : modTextIntersection.Intersect(lineSet, StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (modTextIntersection.Count == 0)
            {
                diagnostic = new ExportOwnerModTextMapBridgeDiagnostic(
                    canonicalUniqueName,
                    versionLabel,
                    versionRole,
                    blockKind,
                    entry.RelativePath,
                    variantDecision,
                    Completeness: "modtextmap-line-intersection-empty",
                    OwnerModifierIds: OrderedIds(ownerModifierIds),
                    ModTextMapModifierIds: OrderedIds(unionModTextIds),
                    IntersectionModifierIds: [],
                    RejectedModifierIds: [],
                    SurvivingModifierId: null);
                return false;
            }
        }

        if (modTextIntersection is null || modTextIntersection.Count == 0)
        {
            return false;
        }

        var intersection = ownerModifierIds
            .Where(modTextIntersection.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (intersection.Length == 0)
        {
            diagnostic = new ExportOwnerModTextMapBridgeDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: "ownership-modtextmap-disjoint",
                OwnerModifierIds: OrderedIds(ownerModifierIds),
                ModTextMapModifierIds: OrderedIds(modTextIntersection),
                IntersectionModifierIds: [],
                RejectedModifierIds: [],
                SurvivingModifierId: null);
            return false;
        }

        if (intersection.Length > 1)
        {
            diagnostic = new ExportOwnerModTextMapBridgeDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: "ambiguous-intersection",
                OwnerModifierIds: OrderedIds(ownerModifierIds),
                ModTextMapModifierIds: OrderedIds(modTextIntersection),
                IntersectionModifierIds: intersection,
                RejectedModifierIds: [],
                SurvivingModifierId: null);
            return false;
        }

        var survivorId = intersection[0];
        if (!mechanicalIndex.TryCreateOwnedModifierCandidate(
                survivorId,
                blockKind,
                blockLines,
                baseType,
                hasGeneratedOptionEvidence,
                versionRole,
                out var candidate,
                out var rejectReason))
        {
            diagnostic = new ExportOwnerModTextMapBridgeDiagnostic(
                canonicalUniqueName,
                versionLabel,
                versionRole,
                blockKind,
                entry.RelativePath,
                variantDecision,
                Completeness: rejectReason ?? "repoe-candidate-rejected",
                OwnerModifierIds: OrderedIds(ownerModifierIds),
                ModTextMapModifierIds: OrderedIds(modTextIntersection),
                IntersectionModifierIds: intersection,
                RejectedModifierIds: [survivorId],
                SurvivingModifierId: null);
            return false;
        }

        survivors = [candidate];
        filterReason = ExportOwnerModTextMapPositiveExactReason;
        diagnostic = new ExportOwnerModTextMapBridgeDiagnostic(
            canonicalUniqueName,
            versionLabel,
            versionRole,
            blockKind,
            entry.RelativePath,
            variantDecision,
            Completeness: "typed-positive-exact",
            OwnerModifierIds: OrderedIds(ownerModifierIds),
            ModTextMapModifierIds: OrderedIds(modTextIntersection),
            IntersectionModifierIds: intersection,
            RejectedModifierIds: OrderedIds(
                modTextIntersection.Where(id =>
                    !string.Equals(id, survivorId, StringComparison.OrdinalIgnoreCase))),
            SurvivingModifierId: survivorId);
        return true;
    }

    private static IReadOnlyList<string> OrderedIds(IEnumerable<string> ids) =>
        ids.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

    private sealed record ExportOwnerModTextMapBridgeDiagnostic(
        string CanonicalUniqueName,
        string VersionLabel,
        UniqueItemVersionRole VersionRole,
        UniqueModifierBlockKind BlockKind,
        string ExportRelativePath,
        string VariantDecision,
        string Completeness,
        IReadOnlyList<string> OwnerModifierIds,
        IReadOnlyList<string> ModTextMapModifierIds,
        IReadOnlyList<string> IntersectionModifierIds,
        IReadOnlyList<string> RejectedModifierIds,
        string? SurvivingModifierId);
}
