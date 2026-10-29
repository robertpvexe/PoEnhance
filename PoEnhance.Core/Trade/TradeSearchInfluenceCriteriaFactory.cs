using System.Collections.Immutable;

namespace PoEnhance.Core.Trade;

public static class TradeSearchInfluenceCriteriaFactory
{
    public static ImmutableArray<TradeSearchInfluenceCriterion> Create(
        IReadOnlyList<string> traditionalInfluences,
        IReadOnlyList<string> eldritchInfluences)
    {
        ArgumentNullException.ThrowIfNull(traditionalInfluences);
        ArgumentNullException.ThrowIfNull(eldritchInfluences);

        var criteria = new List<TradeSearchInfluenceCriterion>();
        foreach (var line in traditionalInfluences)
        {
            if (TryCreate(line, TradeSearchInfluenceFamily.Traditional, out var criterion))
            {
                criteria.Add(criterion);
            }
            else
            {
                criteria.Add(CreateUnrecognized(line, TradeSearchInfluenceFamily.Traditional));
            }
        }

        foreach (var line in eldritchInfluences)
        {
            if (TryCreate(line, TradeSearchInfluenceFamily.Eldritch, out var criterion))
            {
                criteria.Add(criterion);
            }
            else
            {
                criteria.Add(CreateUnrecognized(line, TradeSearchInfluenceFamily.Eldritch));
            }
        }

        return criteria.ToImmutableArray();
    }

    public static bool TryParseObservedLine(
        string? observedLine,
        out TradeSearchInfluenceKind kind,
        out TradeSearchInfluenceFamily family,
        out string displayLabel)
    {
        kind = default;
        family = default;
        displayLabel = string.Empty;
        if (string.IsNullOrWhiteSpace(observedLine))
        {
            return false;
        }

        var trimmed = observedLine.Trim();
        if (TryMapTraditional(trimmed, out kind, out displayLabel))
        {
            family = TradeSearchInfluenceFamily.Traditional;
            return true;
        }

        if (TryMapEldritch(trimmed, out kind, out displayLabel))
        {
            family = TradeSearchInfluenceFamily.Eldritch;
            return true;
        }

        return false;
    }

    private static bool TryCreate(
        string observedLine,
        TradeSearchInfluenceFamily expectedFamily,
        out TradeSearchInfluenceCriterion criterion)
    {
        criterion = null!;
        if (!TryParseObservedLine(observedLine, out var kind, out var family, out var displayLabel) ||
            family != expectedFamily)
        {
            return false;
        }

        criterion = new TradeSearchInfluenceCriterion
        {
            Kind = kind,
            Family = family,
            ObservedLine = observedLine.Trim(),
            DisplayLabel = displayLabel,
            IsEnabled = true,
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unresolved,
        };
        return true;
    }

    private static TradeSearchInfluenceCriterion CreateUnrecognized(
        string observedLine,
        TradeSearchInfluenceFamily family)
    {
        var trimmed = observedLine.Trim();
        return new TradeSearchInfluenceCriterion
        {
            Kind = TradeSearchInfluenceKind.Unknown,
            Family = family,
            ObservedLine = trimmed,
            DisplayLabel = trimmed,
            IsEnabled = true,
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
            DiagnosticReason =
                "This influence line was observed but has no recognized Trade influence identity.",
        };
    }

    private static bool TryMapTraditional(
        string trimmed,
        out TradeSearchInfluenceKind kind,
        out string displayLabel)
    {
        if (trimmed.Equals("Shaper Item", StringComparison.OrdinalIgnoreCase))
        {
            kind = TradeSearchInfluenceKind.Shaper;
            displayLabel = "Shaper";
            return true;
        }

        if (trimmed.Equals("Elder Item", StringComparison.OrdinalIgnoreCase))
        {
            kind = TradeSearchInfluenceKind.Elder;
            displayLabel = "Elder";
            return true;
        }

        if (trimmed.Equals("Crusader Item", StringComparison.OrdinalIgnoreCase))
        {
            kind = TradeSearchInfluenceKind.Crusader;
            displayLabel = "Crusader";
            return true;
        }

        if (trimmed.Equals("Hunter Item", StringComparison.OrdinalIgnoreCase))
        {
            kind = TradeSearchInfluenceKind.Hunter;
            displayLabel = "Hunter";
            return true;
        }

        if (trimmed.Equals("Redeemer Item", StringComparison.OrdinalIgnoreCase))
        {
            kind = TradeSearchInfluenceKind.Redeemer;
            displayLabel = "Redeemer";
            return true;
        }

        if (trimmed.Equals("Warlord Item", StringComparison.OrdinalIgnoreCase))
        {
            kind = TradeSearchInfluenceKind.Warlord;
            displayLabel = "Warlord";
            return true;
        }

        kind = default;
        displayLabel = string.Empty;
        return false;
    }

    private static bool TryMapEldritch(
        string trimmed,
        out TradeSearchInfluenceKind kind,
        out string displayLabel)
    {
        if (trimmed.Equals("Searing Exarch Item", StringComparison.OrdinalIgnoreCase))
        {
            kind = TradeSearchInfluenceKind.SearingExarch;
            displayLabel = "Searing Exarch";
            return true;
        }

        if (trimmed.Equals("Eater of Worlds Item", StringComparison.OrdinalIgnoreCase))
        {
            kind = TradeSearchInfluenceKind.EaterOfWorlds;
            displayLabel = "Eater of Worlds";
            return true;
        }

        kind = default;
        displayLabel = string.Empty;
        return false;
    }
}
