using PoEnhance.Core.Items.GameData;

namespace PoEnhance.App.Infrastructure.Trade.PathOfExile;

internal sealed class PathOfExileTradeStatCatalog
{
    private readonly Dictionary<string, PathOfExileTradeStatEntry> byId;
    private readonly Dictionary<string, PathOfExileTradeStatEntry> byProviderIdentity;
    private readonly Dictionary<string, IReadOnlyList<PathOfExileTradeStatEntry>> byNormalizedTemplate;
    private readonly Dictionary<string, IReadOnlyList<PathOfExileTradeStatCandidateGroup>> candidateGroupsByTemplate;
    private readonly Dictionary<string, IReadOnlyList<PathOfExileTradeStatCandidateGroup>>
        candidateGroupsByItemClassQualifiedTemplate;
    private readonly Dictionary<string, IReadOnlyList<PathOfExileTradeStatMatchCandidate>> candidatesByLogicalEffect;

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

        byProviderIdentity = Entries
            .GroupBy(entry => PathOfExileTradeProviderIdentity.Create(entry.Id), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);

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

        candidateGroupsByItemClassQualifiedTemplate = CandidateGroups
            .Select(group => TryCreateItemClassQualifiedKey(group, out var key)
                ? new { Key = key, Group = group }
                : null)
            .Where(entry => entry is not null)
            .GroupBy(entry => entry!.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PathOfExileTradeStatCandidateGroup>)group
                    .Select(entry => entry!.Group)
                    .ToArray(),
                StringComparer.Ordinal);

        candidatesByLogicalEffect = Entries
            .Select(PathOfExileTradeStatCandidateClassifier.ToCandidate)
            .GroupBy(
                candidate => PathOfExileTradePseudoVariantCompatibility.LogicalEffectIdentity(candidate.Text),
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<PathOfExileTradeStatMatchCandidate>)group.ToArray(),
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

    public bool TryGetByProviderIdentity(string? identity, out PathOfExileTradeStatEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(identity) &&
            byProviderIdentity.TryGetValue(identity, out var found))
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

    public IReadOnlyList<PathOfExileTradeStatCandidateGroup> FindCandidateGroupsByItemClassQualifiedTemplate(
        string? normalizedTemplate,
        string? itemClass)
    {
        var identity = CanonicalItemClassIdentityResolver.Resolve(itemClass);
        if (string.IsNullOrWhiteSpace(normalizedTemplate) ||
            !identity.IsSupported ||
            string.IsNullOrWhiteSpace(identity.CanonicalItemClass))
        {
            return [];
        }

        var key = ItemClassQualifiedKey(normalizedTemplate, identity.CanonicalItemClass);
        return candidateGroupsByItemClassQualifiedTemplate.TryGetValue(key, out var groups)
            ? groups
            : [];
    }

    public IReadOnlyList<PathOfExileTradeStatMatchCandidate> FindCandidatesByLogicalEffect(
        string? logicalEffect)
    {
        return !string.IsNullOrWhiteSpace(logicalEffect) &&
            candidatesByLogicalEffect.TryGetValue(logicalEffect, out var candidates)
            ? candidates
            : [];
    }

    public bool HasRelevantDiagnostics(
        IReadOnlyCollection<string> providerStatIds,
        string? providerKind = null)
    {
        ArgumentNullException.ThrowIfNull(providerStatIds);
        var ids = providerStatIds
            .Where(statId => !string.IsNullOrWhiteSpace(statId))
            .Select(statId => statId.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var trimmedKind = providerKind?.Trim();
        return Diagnostics.Any(diagnostic =>
            diagnostic.IsCatalogWide ||
            !string.IsNullOrWhiteSpace(diagnostic.ProviderStatId) &&
                ids.Contains(diagnostic.ProviderStatId.Trim()) ||
            !string.IsNullOrWhiteSpace(trimmedKind) &&
                !string.IsNullOrWhiteSpace(diagnostic.ProviderGroupId) &&
                string.Equals(
                    diagnostic.ProviderGroupId.Trim(),
                    trimmedKind,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryCreateItemClassQualifiedKey(
        PathOfExileTradeStatCandidateGroup group,
        out string key)
    {
        key = string.Empty;
        var template = group.Key.NormalizedTemplate;
        if (!template.EndsWith(')'))
        {
            return false;
        }

        var qualifierStart = template.LastIndexOf(" (", StringComparison.Ordinal);
        if (qualifierStart <= 0)
        {
            return false;
        }

        var qualifier = template[(qualifierStart + 2)..^1];
        var identity = CanonicalItemClassIdentityResolver.Resolve(qualifier);
        if (!identity.IsSupported || string.IsNullOrWhiteSpace(identity.CanonicalItemClass))
        {
            return false;
        }

        key = ItemClassQualifiedKey(template[..qualifierStart], identity.CanonicalItemClass);
        return true;
    }

    private static string ItemClassQualifiedKey(string template, string canonicalItemClass) =>
        $"{template}\u001f{canonicalItemClass}";
}
