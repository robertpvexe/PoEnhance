using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

internal static class UniqueBlockIdentityCollisionAuditor
{
    internal enum CollisionClass
    {
        LegitimateEquivalentObservation,
        DistinctNumericValueDomain,
        DistinctSourceStructure,
        DistinctOptionOrCompositionStructure,
        UnknownPotentiallyUnsafe,
    }

    internal sealed record CollisionGroup(
        string LegacyIdentityKey,
        string NewIdentityKey,
        CollisionClass Classification,
        IReadOnlyList<CollisionBlockSample> Blocks);

    internal sealed record CollisionBlockSample(
        string ItemName,
        string VersionLabel,
        string BlockId,
        UniqueModifierBlockKind Kind,
        IReadOnlyList<string> Lines,
        IReadOnlyList<string> CanonicalSignatures,
        UniqueModifierSemanticLocality Locality,
        IReadOnlyList<string> SourceObservationIds);

    internal sealed record AuditSummary(
        int TotalBlocks,
        int LegacyCollisionGroups,
        int NewCollisionGroups,
        IReadOnlyDictionary<CollisionClass, int> LegacyClassificationCounts,
        IReadOnlyDictionary<CollisionClass, int> NewClassificationCounts,
        IReadOnlyList<CollisionGroup> LegacyGroups,
        IReadOnlyList<CollisionGroup> NewGroups);

    internal static AuditSummary Audit(UniqueItemCatalog catalog, bool useLegacyIdentity) =>
        Audit(catalog.Items, useLegacyIdentity);

    internal static AuditSummary Audit(
        IReadOnlyList<UniqueItemIdentity> items,
        bool useLegacyIdentity)
    {
        var samples = new List<(string Key, CollisionBlockSample Sample)>();
        foreach (var item in items)
        {
            foreach (var version in item.Versions)
            {
                foreach (var block in version.ModifierBlocks)
                {
                    var key = useLegacyIdentity
                        ? BuildLegacyKey(item.Id, version.Label, block)
                        : block.Id;
                    samples.Add((key, new CollisionBlockSample(
                        item.CanonicalName,
                        version.Label,
                        block.Id,
                        block.Kind,
                        block.Lines,
                        block.CanonicalSignatures,
                        block.SourceSemanticFingerprint.Locality,
                        block.SourceObservationIds)));
                }
            }
        }

        var groups = samples
            .GroupBy(sample => sample.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => BuildGroup(
                group.Key,
                useLegacyIdentity ? group.Key : group.First().Sample.BlockId,
                group.Select(entry => entry.Sample).ToArray()))
            .OrderByDescending(group => group.Blocks.Count)
            .ThenBy(group => group.LegacyIdentityKey, StringComparer.Ordinal)
            .ToArray();

        return new AuditSummary(
            samples.Count,
            useLegacyIdentity ? groups.Length : 0,
            useLegacyIdentity ? 0 : groups.Length,
            CountByClass(groups, useLegacyIdentity),
            CountByClass(groups, useLegacyIdentity),
            useLegacyIdentity ? groups : [],
            useLegacyIdentity ? [] : groups);
    }

    internal static AuditSummary CompareLegacyAndCurrent(UniqueItemCatalog catalog)
    {
        var legacy = Audit(catalog, useLegacyIdentity: true);
        var current = Audit(catalog, useLegacyIdentity: false);
        return legacy with
        {
            NewCollisionGroups = current.NewCollisionGroups,
            NewClassificationCounts = current.NewClassificationCounts,
            NewGroups = current.NewGroups,
        };
    }

    private static string BuildLegacyKey(
        string identityId,
        string versionLabel,
        UniqueModifierBlock block) =>
        PoBUniqueCatalogImporter.ComputeLegacyFixedBlockStableId(
            identityId,
            versionLabel,
            block.Kind,
            block.Lines);

    private static CollisionGroup BuildGroup(
        string legacyIdentityKey,
        string newIdentityKey,
        IReadOnlyList<CollisionBlockSample> blocks)
    {
        var classification = Classify(blocks);
        return new CollisionGroup(
            legacyIdentityKey,
            newIdentityKey,
            classification,
            blocks);
    }

    internal static CollisionClass ClassifyBlocks(IReadOnlyList<CollisionBlockSample> blocks) =>
        Classify(blocks);

    private static CollisionClass Classify(IReadOnlyList<CollisionBlockSample> blocks)
    {
        if (blocks.Select(block => string.Join('\n', block.Lines)).Distinct(StringComparer.Ordinal).Count() == 1 &&
            blocks.Select(block => string.Join('\u001f', block.SourceObservationIds)).Distinct(StringComparer.Ordinal).Count() > 1)
        {
            return CollisionClass.LegitimateEquivalentObservation;
        }

        if (blocks.Select(block => PoBUniqueCatalogImporter.ExtractSourceValueDomainKey(block.Lines))
                .Distinct(StringComparer.Ordinal).Count() > 1)
        {
            return CollisionClass.DistinctNumericValueDomain;
        }

        if (blocks.Select(block => block.Locality).Distinct().Count() > 1)
        {
            return CollisionClass.DistinctSourceStructure;
        }

        if (blocks.Select(block => string.Join('\n', block.Lines)).Distinct(StringComparer.Ordinal).Count() > 1)
        {
            return CollisionClass.DistinctOptionOrCompositionStructure;
        }

        return CollisionClass.UnknownPotentiallyUnsafe;
    }

    private static IReadOnlyDictionary<CollisionClass, int> CountByClass(
        IReadOnlyList<CollisionGroup> groups,
        bool _) => groups
        .GroupBy(group => group.Classification)
        .ToDictionary(group => group.Key, group => group.Count());
}
