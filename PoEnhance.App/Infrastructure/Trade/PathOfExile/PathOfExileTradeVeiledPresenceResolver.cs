namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal static class PathOfExileTradeVeiledPresenceResolver
{
    public static bool TryResolve(
        PathOfExileTradeStatCatalog catalog,
        out PathOfExileTradeStatMatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var matches = catalog.Entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.GroupLabel))
            .Select(PathOfExileTradeStatCandidateClassifier.ToCandidate)
            .Where(entry => string.Equals(
                PathOfExileTradeStatCandidateClassifier.GetProviderKind(entry),
                "veiled",
                StringComparison.Ordinal))
            .Where(entry => string.Equals(
                entry.LookupTemplate,
                PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(entry.GroupLabel),
                StringComparison.Ordinal))
            .ToArray();

        if (matches.Length == 1)
        {
            candidate = matches[0];
            return true;
        }

        candidate = null!;
        return false;
    }
}
