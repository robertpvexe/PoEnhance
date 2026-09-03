namespace PoEnhance.GameData;

/// <summary>
/// Deterministic ExactConflict subtype classifier based only on compact candidate provenance.
/// More specific structural classes win over broad classes. Never resolves a winner.
/// </summary>
public static class UniqueMechanicalConflictClassifier
{
    public const string MarkerDeprecatedName = "deprecated-name";
    public const string MarkerPermyriad = "permyriad";
    public const string MarkerPercent = "percent";
    public const string MarkerLevel = "level";
    public const string MarkerChance = "chance";
    public const string MarkerReservation = "reservation";
    public const string MarkerEfficiencyPlus = "efficiency-plus";
    public const string MarkerEfficiencyInverse = "efficiency-inverse";
    public const string MarkerHandlerNegate = "handler-negate";
    public const string MarkerHandlerLegacy = "handler-legacy";
    public const string MarkerHandlerDouble = "handler-double";

    public static UniqueMechanicalConflictKind Classify(
        IReadOnlyList<UniqueMechanicalConflictCandidate> candidates)
    {
        if (candidates.Count < 2)
        {
            return UniqueMechanicalConflictKind.Unclassified;
        }

        var deprecatedVsCurrent = candidates.Any(HasDeprecatedEvidence) &&
            candidates.Any(candidate => !HasDeprecatedEvidence(candidate));
        var permyriadVsPercent = candidates.Any(HasMarker(MarkerPermyriad)) &&
            candidates.Any(candidate =>
                HasMarker(MarkerPercent)(candidate) &&
                !HasMarker(MarkerPermyriad)(candidate));
        var levelVsChance = candidates.Any(HasMarker(MarkerLevel)) &&
            candidates.Any(HasMarker(MarkerChance));
        var inverseEncoding = HasInverseLegacyEncoding(candidates);
        var distinctStatVectors = candidates
            .Select(candidate => string.Join('\u001f', candidate.StatIds))
            .Where(key => key.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        // Precedence: more specific structural classes before broad same-text fallbacks.
        if (deprecatedVsCurrent && permyriadVsPercent)
        {
            return UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent;
        }

        if (levelVsChance)
        {
            return UniqueMechanicalConflictKind.LevelVsChanceOnHit;
        }

        if (inverseEncoding)
        {
            return UniqueMechanicalConflictKind.InverseLegacyHandlerEncoding;
        }

        if (deprecatedVsCurrent)
        {
            return UniqueMechanicalConflictKind.CurrentVsDeprecatedSourceMechanics;
        }

        if (distinctStatVectors > 1)
        {
            // Trade-duplicate enrichment is provider-owned; importer classification stays source-owned.
            return UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIds;
        }

        return UniqueMechanicalConflictKind.Unclassified;
    }

    public static IReadOnlyList<string> BuildEncodingMarkers(
        string modifierId,
        IReadOnlyList<string> statIds,
        IReadOnlyList<string> handlers)
    {
        var markers = new SortedSet<string>(StringComparer.Ordinal);
        if (LooksDeprecated(modifierId) || statIds.Any(LooksDeprecated))
        {
            markers.Add(MarkerDeprecatedName);
        }

        foreach (var statId in statIds)
        {
            if (LooksPermyriad(statId))
            {
                markers.Add(MarkerPermyriad);
            }

            if (LooksPercent(statId) && !LooksPermyriad(statId))
            {
                markers.Add(MarkerPercent);
            }

            if (LooksLevel(statId))
            {
                markers.Add(MarkerLevel);
            }

            if (LooksChance(statId))
            {
                markers.Add(MarkerChance);
            }

            if (LooksReservation(statId))
            {
                markers.Add(MarkerReservation);
            }

            if (LooksEfficiencyPlus(statId))
            {
                markers.Add(MarkerEfficiencyPlus);
            }

            if (LooksEfficiencyInverse(statId))
            {
                markers.Add(MarkerEfficiencyInverse);
            }
        }

        foreach (var handler in handlers)
        {
            if (handler.Contains("negate", StringComparison.OrdinalIgnoreCase))
            {
                markers.Add(MarkerHandlerNegate);
            }

            if (handler.Contains("old_", StringComparison.OrdinalIgnoreCase) ||
                handler.Contains("legacy", StringComparison.OrdinalIgnoreCase))
            {
                markers.Add(MarkerHandlerLegacy);
            }

            if (handler.Contains("double", StringComparison.OrdinalIgnoreCase))
            {
                markers.Add(MarkerHandlerDouble);
            }
        }

        return markers.ToArray();
    }

    private static bool HasInverseLegacyEncoding(
        IReadOnlyList<UniqueMechanicalConflictCandidate> candidates)
    {
        var reservationSignConflict = candidates.Any(HasMarker(MarkerReservation)) &&
            candidates
                .Select(SignPattern)
                .Where(pattern => pattern is not null)
                .Distinct(StringComparer.Ordinal)
                .Count() > 1;
        if (reservationSignConflict)
        {
            return true;
        }

        var anyNegate = candidates.Any(HasMarker(MarkerHandlerNegate));
        var anyNonNegateHandler = candidates.Any(candidate =>
            candidate.Handlers.Count > 0 &&
            !HasMarker(MarkerHandlerNegate)(candidate));
        return anyNegate && anyNonNegateHandler;
    }

    private static string? SignPattern(UniqueMechanicalConflictCandidate candidate)
    {
        if (HasMarker(MarkerHandlerNegate)(candidate))
        {
            return "negated";
        }

        if (HasMarker(MarkerEfficiencyInverse)(candidate))
        {
            return "efficiency-inverse";
        }

        if (HasMarker(MarkerEfficiencyPlus)(candidate))
        {
            return "efficiency-plus";
        }

        return null;
    }

    private static bool HasDeprecatedEvidence(UniqueMechanicalConflictCandidate candidate) =>
        HasMarker(MarkerDeprecatedName)(candidate) ||
        HasMarker(MarkerHandlerLegacy)(candidate);

    private static Func<UniqueMechanicalConflictCandidate, bool> HasMarker(string marker) =>
        candidate => candidate.EncodingMarkers.Contains(marker, StringComparer.Ordinal);

    private static bool LooksDeprecated(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        (id.Contains("old_do_not_use", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("do_not_use", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("deprecated", StringComparison.OrdinalIgnoreCase) ||
            id.StartsWith("old_", StringComparison.OrdinalIgnoreCase));

    private static bool LooksPermyriad(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.Contains("permyriad", StringComparison.OrdinalIgnoreCase);

    private static bool LooksPercent(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        (id.Contains("percent", StringComparison.OrdinalIgnoreCase) ||
            id.Contains('%'));

    private static bool LooksChance(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        (id.Contains("chance", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("_%_", StringComparison.Ordinal) ||
            id.Contains("_%", StringComparison.Ordinal));

    private static bool LooksLevel(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.Contains("level", StringComparison.OrdinalIgnoreCase);

    private static bool LooksReservation(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.Contains("reservation", StringComparison.OrdinalIgnoreCase);

    private static bool LooksEfficiencyPlus(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        id.Contains("efficiency", StringComparison.OrdinalIgnoreCase) &&
        id.Contains("+%", StringComparison.Ordinal) &&
        !LooksEfficiencyInverse(id);

    private static bool LooksEfficiencyInverse(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        (id.Contains("inefficiency", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("efficiency_-", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("-2%_per_1", StringComparison.OrdinalIgnoreCase));
}
