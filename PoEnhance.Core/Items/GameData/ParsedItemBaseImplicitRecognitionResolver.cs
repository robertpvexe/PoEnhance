using System.Globalization;
using System.Text.RegularExpressions;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Items.GameData;

/// <summary>
/// Recognizes exact current or historical base-implicit observations. This service
/// deliberately stops before provider eligibility or provider-stat mapping.
/// </summary>
public sealed partial class ParsedItemBaseImplicitRecognitionResolver
{
    private readonly ModifierTextSignatureMatcher textMatcher = new();

    public BaseImplicitRecognitionResult Resolve(
        ParsedModifier modifier,
        ItemBaseRecord canonicalBase,
        GameDataCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(modifier);
        ArgumentNullException.ThrowIfNull(canonicalBase);
        ArgumentNullException.ThrowIfNull(catalog);

        if (modifier.Kind != ParsedModifierKind.Implicit ||
            modifier.ImplicitOrigin != ParsedImplicitModifierOrigin.Unspecified)
        {
            return BaseImplicitRecognitionResult.Unknown(
                "base-implicit-origin-ineligible",
                "Only an ordinary, unspecified implicit may use versioned base-implicit evidence.");
        }

        var history = catalog.BaseImplicitHistory;
        if (history is null)
        {
            return BaseImplicitRecognitionResult.Unknown(
                "base-implicit-history-unavailable",
                "This package does not carry versioned base-implicit evidence.");
        }

        var baseId = canonicalBase.Id?.Trim();
        if (string.IsNullOrWhiteSpace(baseId))
        {
            return BaseImplicitRecognitionResult.Unknown(
                "base-implicit-canonical-base-unavailable",
                "The resolved base has no canonical metadata id.");
        }

        var sources = history.SourceSnapshots
            .Where(source => !string.IsNullOrWhiteSpace(source.Id))
            .ToDictionary(source => source.Id!.Trim(), StringComparer.OrdinalIgnoreCase);
        var effects = history.MechanicalEffects
            .Where(effect => effect.IsResolved &&
                effect.Modifier is not null &&
                !string.IsNullOrWhiteSpace(effect.Id))
            .ToDictionary(effect => effect.Id!.Trim(), StringComparer.OrdinalIgnoreCase);
        var observations = history.Observations
            .Where(observation => string.Equals(
                observation.CanonicalBaseId?.Trim(),
                baseId,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var currentMatches = Match(
            modifier,
            observations,
            sources,
            effects,
            BaseImplicitSnapshotRole.CurrentCandidate);
        if (currentMatches.Count > 0)
        {
            return Collapse(currentMatches, BaseImplicitRecognitionStatus.CurrentExact);
        }

        var historicalMatches = Match(
            modifier,
            observations,
            sources,
            effects,
            BaseImplicitSnapshotRole.HistoricalObserved);
        return historicalMatches.Count == 0
            ? BaseImplicitRecognitionResult.Unknown(
                "base-implicit-no-exact-observation",
                "No exact current or historical mechanical observation matched this implicit for the canonical base.")
            : Collapse(historicalMatches, BaseImplicitRecognitionStatus.HistoricalExact);
    }

    private IReadOnlyList<BaseImplicitRecognitionMatch> Match(
        ParsedModifier parsed,
        IReadOnlyList<BaseImplicitObservation> observations,
        IReadOnlyDictionary<string, BaseImplicitSourceSnapshot> sources,
        IReadOnlyDictionary<string, BaseImplicitMechanicalEffect> effects,
        BaseImplicitSnapshotRole role)
    {
        var matches = new List<BaseImplicitRecognitionMatch>();
        foreach (var observation in observations)
        {
            if (!sources.TryGetValue(observation.SourceSnapshotId ?? string.Empty, out var source) ||
                source.Role != role)
            {
                continue;
            }

            foreach (var effectId in observation.MechanicalEffectIds.Where(value => value is not null))
            {
                if (!effects.TryGetValue(effectId!, out var effect) ||
                    !MechanicallyMatches(parsed, effect))
                {
                    continue;
                }

                matches.Add(new BaseImplicitRecognitionMatch(observation, effect, source));
            }
        }

        return matches;
    }

    private bool MechanicallyMatches(ParsedModifier parsed, BaseImplicitMechanicalEffect effect)
    {
        GameDataCatalog effectCatalog;
        try
        {
            effectCatalog = BaseImplicitMechanicalEffectCatalogFactory.Create(effect);
        }
        catch (ArgumentException)
        {
            return false;
        }

        var textMatch = textMatcher.Match(effect.Modifier!, effectCatalog, parsed.ValueLines);
        if (textMatch.Outcome != ModifierTextSignatureMatchOutcome.Match)
        {
            return false;
        }

        var values = NumericValuePattern().Matches(string.Join("\n", parsed.ValueLines));
        var stats = effect.Modifier!.Stats.OrderBy(stat => stat.Index).ToArray();
        if (values.Count != stats.Length)
        {
            return false;
        }

        for (var index = 0; index < stats.Length; index++)
        {
            var stat = stats[index];
            if (!stat.MinValue.HasValue || !stat.MaxValue.HasValue ||
                !TryDecimal(values[index].Groups["value"].Value, out var observed))
            {
                return false;
            }

            if (values[index].Groups["minimum"].Success)
            {
                if (!TryDecimal(values[index].Groups["minimum"].Value, out var minimum) ||
                    !TryDecimal(values[index].Groups["maximum"].Value, out var maximum) ||
                    !RangesEqual(stat.MinValue.Value, stat.MaxValue.Value, minimum, maximum))
                {
                    return false;
                }
            }

            if (!Contains(stat.MinValue.Value, stat.MaxValue.Value, observed))
            {
                return false;
            }
        }

        return true;
    }

    private static BaseImplicitRecognitionResult Collapse(
        IReadOnlyList<BaseImplicitRecognitionMatch> matches,
        BaseImplicitRecognitionStatus exactStatus)
    {
        var distinctMechanics = matches
            .Select(match => match.Effect.MechanicalSignature)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        return distinctMechanics == 1
            ? new BaseImplicitRecognitionResult(
                exactStatus,
                matches,
                exactStatus == BaseImplicitRecognitionStatus.CurrentExact
                    ? "base-implicit-current-exact"
                    : "base-implicit-historical-exact",
                exactStatus == BaseImplicitRecognitionStatus.CurrentExact
                    ? "The parsed implicit exactly matches current candidate source evidence for this canonical base."
                    : "The parsed implicit exactly matches historical observed source evidence for this canonical base.")
            : new BaseImplicitRecognitionResult(
                BaseImplicitRecognitionStatus.Ambiguous,
                matches,
                "base-implicit-history-ambiguous",
                "Multiple matching observations imply different structured mechanics; resolution failed closed.");
    }

    private static bool TryDecimal(string value, out decimal parsed) =>
        decimal.TryParse(
            value,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out parsed);

    private static bool RangesEqual(decimal sourceMin, decimal sourceMax, decimal observedMin, decimal observedMax) =>
        (sourceMin == observedMin && sourceMax == observedMax) ||
        (sourceMin == -observedMax && sourceMax == -observedMin);

    private static bool Contains(decimal minimum, decimal maximum, decimal value) =>
        (value >= minimum && value <= maximum) ||
        (-value >= minimum && -value <= maximum);

    [GeneratedRegex(@"(?<![A-Za-z<])(?<value>[+-]?\d+(?:\.\d+)?)(?:\(\s*(?<minimum>[+-]?\d+(?:\.\d+)?)\s*-\s*(?<maximum>[+-]?\d+(?:\.\d+)?)\s*\))?", RegexOptions.CultureInvariant)]
    private static partial Regex NumericValuePattern();
}
