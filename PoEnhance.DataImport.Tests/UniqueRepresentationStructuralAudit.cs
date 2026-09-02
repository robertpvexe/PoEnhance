using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

internal static class UniqueRepresentationStructuralAudit
{
    internal sealed record Metrics(
        int MultilineSourceObservations,
        int PreservedAsCompositions,
        int SplitAcrossBlocks,
        int FullMechanicsOnSingleChildOnly,
        IReadOnlyDictionary<int, int> OptionAxesBySelectionLimit,
        int AxesConvertedToAtomicVersions,
        int CoSelectableChoicesPreserved,
        int NewCollisionGroups,
        int StructurallyAmbiguousBlocks);

    internal static async Task<Metrics> AnalyzePackageAsync(string packagePath)
    {
        var loadResult = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        if (!loadResult.IsSuccess || loadResult.Package is null)
        {
            throw new InvalidOperationException($"Failed to load package: {packagePath}");
        }

        var catalog = GameDataCatalog.FromPackage(loadResult.Package);
        var uniqueCatalog = catalog.UniqueItems
            ?? throw new InvalidOperationException("Package has no unique catalog.");
        var items = uniqueCatalog.Items;
        var modifiers = loadResult.Package.Modifiers
            .Where(modifier => !string.IsNullOrWhiteSpace(modifier.Id))
            .ToDictionary(modifier => modifier.Id!, StringComparer.OrdinalIgnoreCase);

        var multilineSourceObservations = 0;
        var preservedAsCompositions = 0;
        var splitAcrossBlocks = 0;
        var fullMechanicsOnSingleChildOnly = 0;
        var optionAxesBySelectionLimit = new Dictionary<int, int>();
        var axesConvertedToAtomicVersions = 0;
        var coSelectableChoicesPreserved = 0;
        var structurallyAmbiguousBlocks = 0;

        foreach (var item in items)
        {
            foreach (var version in item.Versions)
            {
                foreach (var axis in version.OptionAxes)
                {
                    optionAxesBySelectionLimit[axis.SelectionLimit] =
                        optionAxesBySelectionLimit.GetValueOrDefault(axis.SelectionLimit) + 1;
                    if (axis.SelectionLimit > 1)
                    {
                        coSelectableChoicesPreserved += axis.Choices.Count;
                        var devotionLikeVersions = item.Versions.Count(candidate =>
                            candidate.Role == UniqueItemVersionRole.Current &&
                            candidate.ModifierBlocks.Count(block =>
                                block.OptionChoiceMemberships.Count > 0) == 1 &&
                            candidate.OptionAxes.Count == 0);
                        axesConvertedToAtomicVersions += devotionLikeVersions;
                    }
                }

                var multilineBlocks = version.ModifierBlocks
                    .Where(block => block.Lines.Count > 1)
                    .ToArray();
                foreach (var block in multilineBlocks)
                {
                    if (block.Composition is not null)
                    {
                        preservedAsCompositions++;
                        continue;
                    }

                    var modifierIds = block.MechanicalMapping.ModifierIds;
                    if (modifierIds.Count == 1 &&
                        modifiers.TryGetValue(modifierIds[0], out var modifier) &&
                        !string.IsNullOrWhiteSpace(modifier.SourceText) &&
                        modifier.SourceText.Contains('\n', StringComparison.Ordinal))
                    {
                        multilineSourceObservations++;
                    }
                }

                var splitGroups = version.ModifierBlocks
                    .Where(block => block.Lines.Count == 1)
                    .Select(block => block.MechanicalMapping.ModifierIds.FirstOrDefault())
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .GroupBy(id => id!, StringComparer.OrdinalIgnoreCase)
                    .Where(group => group.Count() > 1 &&
                        modifiers.TryGetValue(group.Key, out var modifier) &&
                        !string.IsNullOrWhiteSpace(modifier.SourceText) &&
                        modifier.SourceText.Contains('\n', StringComparison.Ordinal))
                    .ToArray();
                if (splitGroups.Length > 0)
                {
                    splitAcrossBlocks += splitGroups.Sum(group => group.Count());
                    fullMechanicsOnSingleChildOnly += splitGroups.Count(group =>
                        version.ModifierBlocks.Any(block =>
                            block.Lines.Count == 1 &&
                            block.MechanicalMapping.ModifierIds.Contains(group.Key) &&
                            block.MechanicalMapping.StatIds.Count > 1));
                }

                structurallyAmbiguousBlocks += version.ModifierBlocks.Count(block =>
                    block.MechanicalMapping.Status is UniqueModifierMechanicalMappingStatus.Ambiguous or
                        UniqueModifierMechanicalMappingStatus.Unsupported);
            }
        }

        var collisionSummary = UniqueBlockIdentityCollisionAuditor.CompareLegacyAndCurrent(uniqueCatalog);
        return new Metrics(
            multilineSourceObservations,
            preservedAsCompositions,
            splitAcrossBlocks,
            fullMechanicsOnSingleChildOnly,
            optionAxesBySelectionLimit,
            axesConvertedToAtomicVersions,
            coSelectableChoicesPreserved,
            collisionSummary.NewCollisionGroups,
            structurallyAmbiguousBlocks);
    }

    internal static string Serialize(Metrics metrics) =>
        JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true });
}
