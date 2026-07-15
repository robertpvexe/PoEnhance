namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeStatCandidateClassifier
{
    public const string UnknownProviderKind = "unknown";

    public static PathOfExileTradeStatMatchCandidate ToCandidate(PathOfExileTradeStatEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new PathOfExileTradeStatMatchCandidate
        {
            ProviderOrder = entry.ProviderOrder,
            StatId = entry.Id,
            Text = entry.Text,
            NormalizedTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeTemplate(entry.Text),
            LookupTemplate = PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(entry.Text),
            GroupId = entry.GroupId,
            GroupLabel = entry.GroupLabel,
            Type = entry.Type,
            ProviderKind = GetProviderKind(entry.GroupId, entry.GroupLabel, entry.Type),
            ProviderLocality = PathOfExileTradeStatTemplateNormalizer.HasProviderLocalAnnotation(entry.Text)
                ? PathOfExileTradeProviderStatLocality.Local
                : PathOfExileTradeStatTemplateNormalizer.HasProviderGlobalAnnotation(entry.Text)
                    ? PathOfExileTradeProviderStatLocality.Global
                    : PathOfExileTradeProviderStatLocality.Unmarked,
            OptionMetadata = entry.OptionMetadata,
        };
    }

    public static string GetProviderKind(PathOfExileTradeStatMatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return string.IsNullOrWhiteSpace(candidate.ProviderKind)
            ? UnknownProviderKind
            : candidate.ProviderKind;
    }

    private static string GetProviderKind(
        string? groupId,
        string? groupLabel,
        string? type)
    {
        var metadata = string.Join(
            " ",
            groupId,
            groupLabel,
            type);

        if (Contains(metadata, "crafted"))
        {
            return "crafted";
        }

        if (Contains(metadata, "fractured"))
        {
            return "fractured";
        }

        if (Contains(metadata, "veiled"))
        {
            return "veiled";
        }

        if (Contains(metadata, "pseudo"))
        {
            return "pseudo";
        }

        if (Contains(metadata, "scourge"))
        {
            return "scourge";
        }

        if (Contains(metadata, "implicit"))
        {
            return "implicit";
        }

        if (Contains(metadata, "enchant"))
        {
            return "enchant";
        }

        if (Contains(metadata, "explicit"))
        {
            return "explicit";
        }

        return UnknownProviderKind;
    }

    private static bool Contains(string value, string expected)
    {
        return value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
