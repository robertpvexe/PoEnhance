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
        out LevelVsChanceExportOwnerCollapseDiagnostic? diagnostic) =>
        TryResolveCurrentSingularExportOwnerCollapse(
            canonicalUniqueName,
            versionLabel,
            versionRole,
            sourceVariantIndex,
            blockLines,
            candidates,
            ownershipIndex,
            UniqueMechanicalConflictKind.LevelVsChanceOnHit,
            CurrentLevelVsChanceExportOwnerCollapseReason,
            CompletenessComplete: "level-vs-chance-export-owner-complete",
            out survivors,
            out filterReason,
            out diagnostic,
            static (name, label, completeness, variant, owners, removed, surviving) =>
                new LevelVsChanceExportOwnerCollapseDiagnostic(
                    name, label, completeness, variant, owners, removed, surviving));

    private sealed record LevelVsChanceExportOwnerCollapseDiagnostic(
        string CanonicalUniqueName,
        string VersionLabel,
        string Completeness,
        string? VariantDecision,
        IReadOnlyList<string> OwnerModifierIds,
        IReadOnlyList<string> RemovedModifierIds,
        IReadOnlyList<string> SurvivingModifierIds);
}
