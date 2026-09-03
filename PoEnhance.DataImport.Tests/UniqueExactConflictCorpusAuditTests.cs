using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class UniqueExactConflictCorpusAuditTests
{
    [Fact]
    public async Task ActivePackage_ExactConflictCorpus_HasDeterministicSubtypeProvenance()
    {
        var packagePath = Environment.GetEnvironmentVariable("POENHANCE_GAMEDATA_AUDIT_PATH")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), $"Package not found: {packagePath}");

        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess, string.Join("; ", load.Diagnostics.Select(d => d.Message)));
        var package = Assert.IsType<GameDataPackage>(load.Package);

        var conflicts = package.UniqueItems?.Items
            .SelectMany(item => item.Versions.SelectMany(version => version.ModifierBlocks
                .Where(block => string.Equals(
                    block.MechanicalMapping.DiagnosticCode,
                    "UNIQUE_MECHANICS_EXACT_CONFLICT",
                    StringComparison.Ordinal))
                .Select(block => (Item: item, Version: version, Block: block))))
            .OrderBy(entry => entry.Item.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Version.Label, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Block.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        Assert.Equal(155, conflicts.Length);
        Assert.Equal(
            73,
            conflicts.Select(entry => entry.Item.Id).Distinct(StringComparer.Ordinal).Count());

        Assert.All(conflicts, entry =>
        {
            var mapping = entry.Block.MechanicalMapping;
            Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, mapping.Status);
            Assert.Empty(mapping.StatIds);
            Assert.Null(mapping.Provenance);
            var evidence = Assert.IsType<UniqueMechanicalConflictEvidence>(mapping.ConflictEvidence);
            Assert.True(evidence.Candidates.Count >= 2);
            Assert.NotEqual(
                UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIdsWithTradeDuplicates,
                evidence.Kind);
            Assert.Equal(
                UniqueMechanicalConflictClassifier.Classify(evidence.Candidates),
                evidence.Kind);
            Assert.Equal(
                mapping.ModifierIds.OrderBy(id => id, StringComparer.Ordinal).ToArray(),
                evidence.Candidates.Select(candidate => candidate.ModifierId).ToArray());
            Assert.DoesNotContain(
                evidence.Candidates,
                candidate => candidate.ModifierId.Contains("Hrimnor", StringComparison.OrdinalIgnoreCase) ||
                    candidate.ModifierId.Contains("Asenath", StringComparison.OrdinalIgnoreCase) ||
                    candidate.ModifierId.Contains("Circle", StringComparison.OrdinalIgnoreCase));
        });

        var subtypeCounts = conflicts
            .GroupBy(entry => entry.Block.MechanicalMapping.ConflictEvidence!.Kind)
            .ToDictionary(group => group.Key, group => group.Count());
        var fingerprintPayload = string.Join(
            '\n',
            conflicts.Select(entry =>
            {
                var evidence = entry.Block.MechanicalMapping.ConflictEvidence!;
                return string.Join(
                    '\u001f',
                    entry.Block.Id,
                    evidence.Kind,
                    string.Join(',', evidence.Candidates.Select(candidate => candidate.ModifierId)),
                    string.Join(
                        '|',
                        evidence.Candidates.Select(candidate => string.Join(',', candidate.StatIds))),
                    string.Join(
                        ',',
                        evidence.Candidates
                            .SelectMany(candidate => candidate.EncodingMarkers)
                            .Distinct(StringComparer.Ordinal)
                            .OrderBy(marker => marker, StringComparer.Ordinal)));
            }));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintPayload)))
            .ToLowerInvariant();
        var secondFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintPayload)))
            .ToLowerInvariant();
        Assert.Equal(fingerprint, secondFingerprint);

        var reportPath = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-ExactConflict-SubtypeCorpus.json");
        await File.WriteAllTextAsync(
            reportPath,
            JsonSerializer.Serialize(
                new
                {
                    package.Manifest.DataVersion,
                    totalExactConflictBlocks = conflicts.Length,
                    distinctIdentities = conflicts
                        .Select(entry => entry.Item.Id)
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                    subtypeCounts = subtypeCounts
                        .OrderByDescending(entry => entry.Value)
                        .ThenBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                        .ToDictionary(entry => entry.Key.ToString(), entry => entry.Value),
                    classificationFingerprintSha256 = fingerprint,
                },
                new JsonSerializerOptions { WriteIndented = true }));

        Assert.All(
            Enum.GetValues<UniqueMechanicalConflictKind>(),
            kind =>
            {
                if (kind is UniqueMechanicalConflictKind.SameDisplayTextDifferentStatIdsWithTradeDuplicates)
                {
                    Assert.False(subtypeCounts.ContainsKey(kind));
                    return;
                }

                _ = subtypeCounts.GetValueOrDefault(kind);
            });
    }

    [Fact]
    public async Task ActivePackage_ControlItems_RetainExpectedExactConflictSubtypes()
    {
        var packagePath = Environment.GetEnvironmentVariable("POENHANCE_GAMEDATA_AUDIT_PATH")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        var package = Assert.IsType<GameDataPackage>(load.Package);

        AssertControl(
            package,
            "Hrimnor's Hymn",
            UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent,
            UniqueMechanicalConflictClassifier.MarkerPermyriad,
            UniqueMechanicalConflictClassifier.MarkerPercent);
        AssertControl(
            package,
            "Asenath's Gentle Touch",
            UniqueMechanicalConflictKind.LevelVsChanceOnHit,
            UniqueMechanicalConflictClassifier.MarkerLevel,
            UniqueMechanicalConflictClassifier.MarkerChance);
        AssertControl(
            package,
            "Circle of Fear",
            UniqueMechanicalConflictKind.InverseLegacyHandlerEncoding,
            UniqueMechanicalConflictClassifier.MarkerEfficiencyPlus,
            UniqueMechanicalConflictClassifier.MarkerEfficiencyInverse);
    }

    private static void AssertControl(
        GameDataPackage package,
        string canonicalName,
        UniqueMechanicalConflictKind expectedKind,
        string requiredMarkerA,
        string requiredMarkerB)
    {
        var item = Assert.Single(
            package.UniqueItems!.Items,
            candidate => string.Equals(
                candidate.CanonicalName,
                canonicalName,
                StringComparison.OrdinalIgnoreCase));
        var currentConflicts = item.Versions
            .Where(version => version.Role == UniqueItemVersionRole.Current)
            .SelectMany(version => version.ModifierBlocks)
            .Where(block => string.Equals(
                block.MechanicalMapping.DiagnosticCode,
                "UNIQUE_MECHANICS_EXACT_CONFLICT",
                StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(currentConflicts);
        Assert.All(currentConflicts, block =>
        {
            Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, block.MechanicalMapping.Status);
            Assert.Empty(block.MechanicalMapping.StatIds);
            var evidence = Assert.IsType<UniqueMechanicalConflictEvidence>(
                block.MechanicalMapping.ConflictEvidence);
            Assert.Equal(expectedKind, evidence.Kind);
            Assert.Contains(
                evidence.Candidates,
                candidate => candidate.EncodingMarkers.Contains(requiredMarkerA));
            Assert.Contains(
                evidence.Candidates,
                candidate => candidate.EncodingMarkers.Contains(requiredMarkerB));
        });
    }
}
