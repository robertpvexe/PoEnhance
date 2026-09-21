using System.Globalization;
using System.Text.Json;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class A527MultiStatCorpusScanTests
{
    [Fact]
    public async Task Scan_CurrentMultiStatCorrupted_WritesTempReport()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        var package = load.Package!;
        var catalog = GameDataCatalog.FromPackage(package);

        var currentCorrupted = package.Modifiers!
            .Where(modifier =>
                modifier.GenerationType == ModifierGenerationType.Corrupted &&
                modifier.SourceAvailability != ModifierSourceAvailability.Disabled)
            .ToArray();
        var multiStat = currentCorrupted
            .Where(modifier =>
                modifier.Stats.Count(stat => !string.IsNullOrWhiteSpace(stat.StatId)) >= 2)
            .ToArray();

        var splitFamilies = new List<object>();
        var uniqueAligned = 0;
        var zeroAlignment = 0;
        var multiAlignmentAmbiguous = 0;
        var unexpectedExactProbe = new List<string>();

        foreach (var modifier in multiStat.OrderBy(entry => entry.Id, StringComparer.Ordinal))
        {
            var stats = modifier.Stats
                .Where(stat => !string.IsNullOrWhiteSpace(stat.StatId))
                .OrderBy(stat => stat.Index)
                .ToArray();
            var fullGroupCount = catalog.FindStatTranslationsByStatIdGroup(
                stats.Select(stat => stat.StatId!).ToArray()).Count;
            var perStatLines = new List<string>();
            var canRender = true;
            foreach (var stat in stats)
            {
                var variant = catalog.FindStatTranslationsByStatIdGroup([stat.StatId!])
                    .SelectMany(translation => translation.Variants)
                    .FirstOrDefault(entry =>
                        entry.FormatLines.Count == 1 &&
                        entry.ValueFormats is ["#"] or ["+#"]);
                if (variant is null || !stat.MinValue.HasValue)
                {
                    canRender = false;
                    break;
                }

                var prefix = variant.ValueFormats[0] == "+#" ? "+" : string.Empty;
                var observed = stat.MinValue.Value;
                // Prefer advanced-range form when the source has a real roll band.
                if (stat.MaxValue.HasValue && stat.MaxValue.Value != stat.MinValue.Value)
                {
                    perStatLines.Add(
                        variant.FormatLines[0].Replace(
                            "{0}",
                            prefix +
                            observed.ToString(CultureInfo.InvariantCulture) +
                            "(" +
                            stat.MinValue.Value.ToString(CultureInfo.InvariantCulture) +
                            "-" +
                            stat.MaxValue.Value.ToString(CultureInfo.InvariantCulture) +
                            ")",
                            StringComparison.Ordinal));
                }
                else
                {
                    perStatLines.Add(
                        variant.FormatLines[0].Replace(
                            "{0}",
                            prefix + observed.ToString(CultureInfo.InvariantCulture),
                            StringComparison.Ordinal));
                }
            }

            var isSplitRender = fullGroupCount == 0 &&
                stats.All(stat =>
                    catalog.FindStatTranslationsByStatIdGroup([stat.StatId!]).Count > 0);
            if (isSplitRender)
            {
                splitFamilies.Add(new
                {
                    modifier.Id,
                    statCount = stats.Length,
                    fullGroupTranslationCount = fullGroupCount,
                });
            }

            if (!canRender || !isSplitRender)
            {
                continue;
            }

            var parsed = new ParsedModifier(
                perStatLines,
                RawMetadataLine: null,
                ParsedModifierKind.Implicit,
                Name: null,
                Tier: null,
                Rank: null,
                CategoryText: null,
                IsCrafted: false,
                IsFractured: false,
                IsVeiled: false)
            {
                ImplicitOrigin = ParsedImplicitModifierOrigin.Corrupted,
            };

            var outcome = SpecialImplicitMultiLineValueAligner.Classify(parsed, modifier, catalog);
            switch (outcome)
            {
                case SpecialImplicitMultiLineAlignmentOutcome.UniqueComplete:
                    uniqueAligned++;
                    if (!string.Equals(
                            modifier.Id,
                            "V2ChanceToBleedOnHitAndIncreasedDamageToBleedingTargetsCorrupted_",
                            StringComparison.Ordinal))
                    {
                        unexpectedExactProbe.Add(modifier.Id!);
                    }

                    break;
                case SpecialImplicitMultiLineAlignmentOutcome.Ambiguous:
                    multiAlignmentAmbiguous++;
                    break;
                default:
                    zeroAlignment++;
                    break;
            }
        }

        var report = new
        {
            totalCurrentEligibleCorrupted = currentCorrupted.Length,
            totalMultiStat = multiStat.Length,
            splitRenderFamilyCount = splitFamilies.Count,
            uniquelyAlignedCount = uniqueAligned,
            zeroAlignmentUnknownCount = zeroAlignment,
            multiAlignmentAmbiguousCount = multiAlignmentAmbiguous,
            unexpectedExactProbeModIds = unexpectedExactProbe,
            note =
                "Synthetic AID lines from per-stat translations; unique alignment uses SpecialImplicitMultiLineValueAligner.TryMatch. " +
                "unexpectedExactProbe lists non-Soul-Taker split-render families that uniquely align under the same generic algorithm (expected when N==M evidence is complete).",
            sampleSplitFamilies = splitFamilies.Take(10).ToArray(),
        };

        var dir = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-A.5.27-MultiStat-MultiLine-Alignment");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "multistat-corpus-after.json");
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

        Assert.Equal(56, multiStat.Length);
        Assert.Equal(5, splitFamilies.Count);
        Assert.True(File.Exists(path));
    }
}
