namespace PoEnhance.Core.Trade;

/// <summary>
/// Computes item-wide totals for approved Group-A logical effects (single resistance or
/// attribute) from draft modifier filters. Compact all-elemental / all-attributes parents,
/// <c>resist_all_%</c>, and any approved composite (including unsplit Implicits) contribute
/// once each; already-aggregated ordinary rows are not double-counted via their children.
/// </summary>
internal static class LogicalEffectItemTotalCalculator
{
    private const string AllResistancesIncludingChaos = "resist_all_%";
    private const string AllElementalResistances = "base_resist_all_elements_%";
    private const string AllAttributes = "additional_all_attributes";

    private static readonly HashSet<string> ElementalResistanceStatIds = new(StringComparer.Ordinal)
    {
        "base_fire_damage_resistance_%",
        "base_cold_damage_resistance_%",
        "base_lightning_damage_resistance_%",
    };

    private static readonly HashSet<string> ChaosResistanceStatIds = new(StringComparer.Ordinal)
    {
        "base_chaos_damage_resistance_%",
    };

    private static readonly HashSet<string> AttributeStatIds = new(StringComparer.Ordinal)
    {
        "additional_strength",
        "additional_dexterity",
        "additional_intelligence",
    };

    public static bool TryCompute(
        IReadOnlyList<ResolvedSearchComponent> modifierFilters,
        string logicalStatId,
        out decimal total)
    {
        ArgumentNullException.ThrowIfNull(modifierFilters);
        total = 0m;
        if (string.IsNullOrWhiteSpace(logicalStatId))
        {
            return false;
        }

        var target = logicalStatId.Trim();
        var sum = 0m;
        var found = false;
        // Deduplicate by physical affix so a projected logical Fire row and its sibling Chaos
        // row from the same Crafted composite do not double-count when totaling Fire.
        var countedPhysical = new HashSet<string>(StringComparer.Ordinal);
        foreach (var filter in modifierFilters)
        {
            if (!TryContributionScalar(filter, target, out var scalar, out var physicalKey))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(physicalKey) && !countedPhysical.Add(physicalKey))
            {
                continue;
            }

            sum += scalar;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        total = sum;
        return true;
    }

    /// <summary>
    /// Official Trade <c>pseudo.pseudo_total_elemental_resistance</c>: Fire total + Cold total +
    /// Lightning total across the item. Chaos is excluded. Physical affixes are counted once
    /// inside each per-element total.
    /// </summary>
    public static bool TryComputeTotalElementalResistance(
        IReadOnlyList<ResolvedSearchComponent> modifierFilters,
        out decimal total)
    {
        ArgumentNullException.ThrowIfNull(modifierFilters);
        total = 0m;
        var found = false;
        var sum = 0m;
        foreach (var elementalStatId in ElementalResistanceStatIds)
        {
            if (!TryCompute(modifierFilters, elementalStatId, out var elementalTotal))
            {
                continue;
            }

            sum += elementalTotal;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        total = sum;
        return true;
    }

    public static bool IsElementalResistanceLogicalStat(string? logicalStatId) =>
        !string.IsNullOrWhiteSpace(logicalStatId) &&
        ElementalResistanceStatIds.Contains(logicalStatId.Trim());

    public static bool IsAttributeLogicalStat(string? logicalStatId) =>
        !string.IsNullOrWhiteSpace(logicalStatId) &&
        AttributeStatIds.Contains(logicalStatId.Trim());

    /// <summary>
    /// Rows that may mirror draft-level Pseudo Total Elemental Resistance: Fire/Cold/Lightning
    /// logical rows (including projected splits and aggregates) and compact all-Elemental
    /// parents (so the shared criterion stays discoverable when only all-Elemental is present).
    /// Chaos never hosts the mirror.
    /// </summary>
    public static bool IsElementalResistanceMirrorHost(ResolvedSearchComponent component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (component.ResolvedStatIds.Count != 1)
        {
            return false;
        }

        var statId = component.ResolvedStatIds[0];
        return IsElementalResistanceLogicalStat(statId) ||
            string.Equals(statId, AllElementalResistances, StringComparison.Ordinal);
    }

    private static bool TryContributionScalar(
        ResolvedSearchComponent filter,
        string logicalStatId,
        out decimal scalar,
        out string physicalKey)
    {
        scalar = 0m;
        physicalKey = PhysicalKey(filter);

        if (MatchesExactLogicalRow(filter, logicalStatId) &&
            TryScalar(filter, out scalar))
        {
            return true;
        }

        if (ContributesFromComposite(filter, logicalStatId) &&
            TryScalar(filter, out scalar))
        {
            return true;
        }

        return false;
    }

    private static string PhysicalKey(ResolvedSearchComponent filter)
    {
        var fromSources = filter.Sources
            .Select(source => source.PhysicalSourceIdentity)
            .FirstOrDefault(identity => !string.IsNullOrEmpty(identity));
        if (!string.IsNullOrEmpty(fromSources))
        {
            // Strip logical suffix so Fire and Chaos children of one craft share one key.
            var logicIndex = fromSources.IndexOf(":logic:", StringComparison.Ordinal);
            return logicIndex >= 0 ? fromSources[..logicIndex] : fromSources;
        }

        if (!string.IsNullOrEmpty(filter.PhysicalSourceIdentity))
        {
            var logicIndex = filter.PhysicalSourceIdentity.IndexOf(":logic:", StringComparison.Ordinal);
            return logicIndex >= 0
                ? filter.PhysicalSourceIdentity[..logicIndex]
                : filter.PhysicalSourceIdentity;
        }

        // Unprojected implicits / ordinary rows: one component = one physical contribution.
        return $"comp:{filter.ComponentId}";
    }

    private static bool MatchesExactLogicalRow(
        ResolvedSearchComponent filter,
        string logicalStatId)
    {
        return filter.ResolvedStatIds.Count == 1 &&
            string.Equals(filter.ResolvedStatIds[0], logicalStatId, StringComparison.Ordinal);
    }

    private static bool ContributesFromComposite(
        ResolvedSearchComponent filter,
        string logicalStatId)
    {
        if (filter.ResolvedStatIds.Count != 1)
        {
            return false;
        }

        var compositeId = filter.ResolvedStatIds[0];
        if (string.Equals(compositeId, AllResistancesIncludingChaos, StringComparison.Ordinal))
        {
            return ElementalResistanceStatIds.Contains(logicalStatId) ||
                ChaosResistanceStatIds.Contains(logicalStatId);
        }

        if (string.Equals(compositeId, AllElementalResistances, StringComparison.Ordinal))
        {
            return ElementalResistanceStatIds.Contains(logicalStatId);
        }

        if (string.Equals(compositeId, AllAttributes, StringComparison.Ordinal))
        {
            return AttributeStatIds.Contains(logicalStatId);
        }

        // Compact parents already expose LogicalContributions; also expand any approved
        // unsplit composite (notably Implicit Fire+Lightning) via the Group-A catalog so
        // Pseudo totals include implicits without projecting them into ordinary rows.
        if (filter.LogicalContributions.Any(contribution => string.Equals(
                contribution.LogicalStatId,
                logicalStatId,
                StringComparison.Ordinal)))
        {
            return true;
        }

        return ApprovedMultiEffectProjectionCatalog.TryGet(filter.ResolvedStatIds, out var projection) &&
            projection.Kind != MultiEffectProjectionKind.KeepOne &&
            projection.Targets.Any(target => string.Equals(
                target.StatId,
                logicalStatId,
                StringComparison.Ordinal));
    }

    private static bool TryScalar(ResolvedSearchComponent filter, out decimal scalar)
    {
        if (filter.CanonicalNumericValues.Count == 1)
        {
            scalar = filter.CanonicalNumericValues[0];
            return true;
        }

        if (filter.ObservedNumericValues.Count == 1)
        {
            scalar = filter.ObservedNumericValues[0];
            return true;
        }

        scalar = 0m;
        return false;
    }
}
