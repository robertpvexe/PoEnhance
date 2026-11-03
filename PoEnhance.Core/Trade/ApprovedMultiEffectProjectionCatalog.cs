namespace PoEnhance.Core.Trade;

/// <summary>
/// How an approved composite RePoE stat is projected into logical effect groups.
/// </summary>
internal enum MultiEffectProjectionKind
{
    /// <summary>
    /// The one composite component is replaced by one logical component per target. Every
    /// logical component keeps the same physical source identity and composite provenance.
    /// </summary>
    SplitIntoGroups,

    /// <summary>
    /// One compact parent is kept. Logical children are attached as display/selection views of the
    /// same physical source; they are never folded into the matching ordinary aggregates.
    /// </summary>
    CompactWithLogicalChildren,

    /// <summary>
    /// Explicit no-op: the composite is one ordinary row and is never split or expanded.
    /// </summary>
    KeepOne,
}

internal sealed record MultiEffectLogicalTarget(string StatId, string CanonicalSignature);

internal sealed record ApprovedMultiEffectProjection(
    string CompositeStatId,
    MultiEffectProjectionKind Kind,
    IReadOnlyList<MultiEffectLogicalTarget> Targets);

/// <summary>
/// Data-driven table of approved multi-effect modifier projections, keyed only by RePoE stat id.
/// No item or modifier names are consulted. Logical signatures use the canonical
/// <c>&lt;number&gt;</c> placeholder form produced by the modifier text signature normalizer, so a
/// projected logical effect aggregates with the same ordinary single-effect modifier.
/// </summary>
internal static class ApprovedMultiEffectProjectionCatalog
{
    private static readonly MultiEffectLogicalTarget Fire = new(
        "base_fire_damage_resistance_%",
        "+<number>% to Fire Resistance");

    private static readonly MultiEffectLogicalTarget Cold = new(
        "base_cold_damage_resistance_%",
        "+<number>% to Cold Resistance");

    private static readonly MultiEffectLogicalTarget Lightning = new(
        "base_lightning_damage_resistance_%",
        "+<number>% to Lightning Resistance");

    private static readonly MultiEffectLogicalTarget Chaos = new(
        "base_chaos_damage_resistance_%",
        "+<number>% to Chaos Resistance");

    private static readonly MultiEffectLogicalTarget Strength = new(
        "additional_strength",
        "+<number> to Strength");

    private static readonly MultiEffectLogicalTarget Dexterity = new(
        "additional_dexterity",
        "+<number> to Dexterity");

    private static readonly MultiEffectLogicalTarget Intelligence = new(
        "additional_intelligence",
        "+<number> to Intelligence");

    private static readonly IReadOnlyDictionary<string, ApprovedMultiEffectProjection> Projections =
        new[]
        {
            Split("fire_and_chaos_damage_resistance_%", Fire, Chaos),
            Split("cold_and_chaos_damage_resistance_%", Cold, Chaos),
            Split("lightning_and_chaos_damage_resistance_%", Lightning, Chaos),
            Split("fire_and_cold_damage_resistance_%", Fire, Cold),
            Split("fire_and_lightning_damage_resistance_%", Fire, Lightning),
            Split("cold_and_lightning_damage_resistance_%", Cold, Lightning),
            Split("additional_strength_and_dexterity", Strength, Dexterity),
            Split("additional_strength_and_intelligence", Strength, Intelligence),
            Split("additional_dexterity_and_intelligence", Dexterity, Intelligence),
            Compact("base_resist_all_elements_%", Fire, Cold, Lightning),
            Compact("additional_all_attributes", Strength, Dexterity, Intelligence),
            // All Resistances including Chaos stays one unsplit ordinary row. Listed explicitly so
            // the decision is documented data rather than an omission.
            new ApprovedMultiEffectProjection("resist_all_%", MultiEffectProjectionKind.KeepOne, []),
        }.ToDictionary(projection => projection.CompositeStatId, StringComparer.Ordinal);

    public static bool TryGet(
        IReadOnlyList<string> resolvedStatIds,
        out ApprovedMultiEffectProjection projection)
    {
        projection = null!;
        if (resolvedStatIds.Count != 1 ||
            string.IsNullOrWhiteSpace(resolvedStatIds[0]))
        {
            return false;
        }

        return Projections.TryGetValue(resolvedStatIds[0].Trim(), out projection!);
    }

    /// <summary>
    /// Fallback when a composite was Exact-matched by text/signature but the resolved RePoE id
    /// vector did not land on the approved table (for example a Crafted rank with atypical ids).
    /// Matches the composite's own canonical signature only — never item names or free text.
    /// </summary>
    public static bool TryGetByCompositeCanonicalSignature(
        string? canonicalSignature,
        out ApprovedMultiEffectProjection projection)
    {
        projection = null!;
        if (string.IsNullOrWhiteSpace(canonicalSignature))
        {
            return false;
        }

        var trimmed = canonicalSignature.Trim();
        foreach (var candidate in Projections.Values)
        {
            if (candidate.Kind == MultiEffectProjectionKind.KeepOne ||
                candidate.Targets.Count == 0)
            {
                continue;
            }

            // Composite signatures are the only keys we accept here; logical target signatures
            // must not expand ordinary single-effect rows.
            var compositeSignature = CompositeCanonicalSignature(candidate.CompositeStatId);
            if (string.Equals(compositeSignature, trimmed, StringComparison.Ordinal))
            {
                projection = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool IsApprovedCompositeStat(IReadOnlyList<string> resolvedStatIds) =>
        TryGet(resolvedStatIds, out var projection) &&
        projection.Kind != MultiEffectProjectionKind.KeepOne;

    private static string CompositeCanonicalSignature(string compositeStatId) =>
        compositeStatId switch
        {
            "fire_and_chaos_damage_resistance_%" => "+<number>% to Fire and Chaos Resistances",
            "cold_and_chaos_damage_resistance_%" => "+<number>% to Cold and Chaos Resistances",
            "lightning_and_chaos_damage_resistance_%" => "+<number>% to Lightning and Chaos Resistances",
            "fire_and_cold_damage_resistance_%" => "+<number>% to Fire and Cold Resistances",
            "fire_and_lightning_damage_resistance_%" => "+<number>% to Fire and Lightning Resistances",
            "cold_and_lightning_damage_resistance_%" => "+<number>% to Cold and Lightning Resistances",
            "additional_strength_and_dexterity" => "+<number> to Strength and Dexterity",
            "additional_strength_and_intelligence" => "+<number> to Strength and Intelligence",
            "additional_dexterity_and_intelligence" => "+<number> to Dexterity and Intelligence",
            "base_resist_all_elements_%" => "+<number>% to all Elemental Resistances",
            "additional_all_attributes" => "+<number> to all Attributes",
            _ => string.Empty,
        };

    public static bool TryGetLogicalTarget(
        string logicalStatId,
        out MultiEffectLogicalTarget target)
    {
        target = null!;
        if (string.IsNullOrWhiteSpace(logicalStatId))
        {
            return false;
        }

        var match = Projections.Values
            .SelectMany(projection => projection.Targets)
            .FirstOrDefault(candidate => string.Equals(
                candidate.StatId,
                logicalStatId.Trim(),
                StringComparison.Ordinal));
        if (match is null)
        {
            return false;
        }

        target = match;
        return true;
    }

    private static ApprovedMultiEffectProjection Split(
        string compositeStatId,
        params MultiEffectLogicalTarget[] targets) =>
        new(compositeStatId, MultiEffectProjectionKind.SplitIntoGroups, targets);

    private static ApprovedMultiEffectProjection Compact(
        string compositeStatId,
        params MultiEffectLogicalTarget[] targets) =>
        new(compositeStatId, MultiEffectProjectionKind.CompactWithLogicalChildren, targets);
}
