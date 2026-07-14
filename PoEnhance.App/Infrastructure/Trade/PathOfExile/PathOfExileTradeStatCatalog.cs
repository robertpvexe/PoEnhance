namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeStatCatalog
{
    private readonly Dictionary<string, PathOfExileTradeStatEntry> byId;
    private readonly Dictionary<string, IReadOnlyList<PathOfExileTradeStatEntry>> byNormalizedTemplate;
    private readonly Dictionary<string, IReadOnlyList<PathOfExileTradeStatCandidateGroup>> candidateGroupsByTemplate;

    public PathOfExileTradeStatCatalog(
        IEnumerable<PathOfExileTradeStatEntry> entries,
        IReadOnlyList<PathOfExileTradeQueryDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(entries);

        Entries = entries
            .OrderBy(entry => entry.ProviderOrder)
            .ToArray();
        Diagnostics = diagnostics ?? [];

        byId = new Dictionary<string, PathOfExileTradeStatEntry>(StringComparer.Ordinal);
        foreach (var entry in Entries)
        {
            if (!byId.ContainsKey(entry.Id))
            {
                byId.Add(entry.Id, entry);
            }
        }

        byNormalizedTemplate = Entries
            .GroupBy(
                entry => PathOfExileTradeStatTemplateNormalizer.NormalizeLookupTemplate(entry.Text),
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PathOfExileTradeStatEntry>)group.ToArray(),
                StringComparer.Ordinal);

        CandidateGroups = Entries
            .Select(PathOfExileTradeStatCandidateClassifier.ToCandidate)
            .GroupBy(
                candidate => new PathOfExileTradeStatCandidateGroupKey
                {
                    NormalizedTemplate = candidate.LookupTemplate,
                    ProviderKind = candidate.ProviderKind,
                })
            .Select(group => new PathOfExileTradeStatCandidateGroup
            {
                Key = group.Key,
                Candidates = group
                    .OrderBy(candidate => candidate.ProviderLocality)
                    .ThenBy(candidate => candidate.StatId, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.Text, StringComparer.Ordinal)
                    .ThenBy(candidate => candidate.ProviderOrder)
                    .ToArray(),
            })
            .OrderBy(group => group.Key.NormalizedTemplate, StringComparer.Ordinal)
            .ThenBy(group => group.Key.ProviderKind, StringComparer.Ordinal)
            .ToArray();

        candidateGroupsByTemplate = CandidateGroups
            .GroupBy(group => group.Key.NormalizedTemplate, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PathOfExileTradeStatCandidateGroup>)group.ToArray(),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<PathOfExileTradeStatEntry> Entries { get; }

    public IReadOnlyList<PathOfExileTradeStatCandidateGroup> CandidateGroups { get; }

    public IReadOnlyList<PathOfExileTradeQueryDiagnostic> Diagnostics { get; }

    public bool TryGetById(string? statId, out PathOfExileTradeStatEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(statId) &&
            byId.TryGetValue(statId, out var found))
        {
            entry = found;
            return true;
        }

        entry = null!;
        return false;
    }

    public IReadOnlyList<PathOfExileTradeStatEntry> FindByNormalizedTemplate(
        string? normalizedTemplate)
    {
        return !string.IsNullOrWhiteSpace(normalizedTemplate) &&
            byNormalizedTemplate.TryGetValue(normalizedTemplate, out var entries)
            ? entries
            : [];
    }

    public IReadOnlyList<PathOfExileTradeStatCandidateGroup> FindCandidateGroupsByNormalizedTemplate(
        string? normalizedTemplate)
    {
        return !string.IsNullOrWhiteSpace(normalizedTemplate) &&
            candidateGroupsByTemplate.TryGetValue(normalizedTemplate, out var groups)
            ? groups
            : [];
    }
}
