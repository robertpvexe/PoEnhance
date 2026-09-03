using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PoEnhance.GameData;

namespace PoEnhance.DataImport.Tests;

public sealed class UniqueExactConflictCorpusAuditTests
{
    private const string PreviousExactConflictCountBaseline = "155";
    private const string PreviousSubclassPermyriadBaseline = "93";

    [Fact]
    public async Task ActivePackage_ExactConflictCorpus_ReflectsCurrentEncodingResolution()
    {
        var package = await LoadActivePackageAsync();

        var exactConflicts = EnumerateExactConflicts(package).ToArray();
        var resolvedByCurrentEncoding = package.UniqueItems!.Items
            .SelectMany(item => item.Versions.SelectMany(version => version.ModifierBlocks
                .Where(block =>
                    block.MechanicalMapping.Provenance?.ResolutionReasons.Contains(
                        "current-role-deprecated-encoding-filter",
                        StringComparer.Ordinal) == true)
                .Select(block => (Item: item, Version: version, Block: block))))
            .ToArray();

        Assert.True(
            exactConflicts.Length < int.Parse(PreviousExactConflictCountBaseline),
            $"Expected ExactConflict count to drop below prior baseline {PreviousExactConflictCountBaseline}; observed {exactConflicts.Length}.");
        Assert.All(exactConflicts, entry =>
        {
            var mapping = entry.Block.MechanicalMapping;
            Assert.Equal(UniqueModifierMechanicalMappingStatus.Ambiguous, mapping.Status);
            Assert.Empty(mapping.StatIds);
            Assert.Null(mapping.Provenance);
            var evidence = Assert.IsType<UniqueMechanicalConflictEvidence>(mapping.ConflictEvidence);
            Assert.True(evidence.Candidates.Count >= 2);
            Assert.Equal(
                UniqueMechanicalConflictClassifier.Classify(evidence.Candidates),
                evidence.Kind);
        });

        Assert.NotEmpty(resolvedByCurrentEncoding);
        Assert.All(resolvedByCurrentEncoding, entry =>
        {
            Assert.Equal(UniqueItemVersionRole.Current, entry.Version.Role);
            Assert.True(entry.Block.MechanicalMapping.Status is
                UniqueModifierMechanicalMappingStatus.Exact or
                UniqueModifierMechanicalMappingStatus.EquivalentSourceSet);
            Assert.Null(entry.Block.MechanicalMapping.ConflictEvidence);
            Assert.NotEmpty(entry.Block.MechanicalMapping.StatIds);
            Assert.DoesNotContain(
                entry.Block.MechanicalMapping.StatIds,
                statId => UniqueMechanicalConflictClassifier.BuildEncodingMarkers(
                    "x",
                    [statId],
                    []).Contains(UniqueMechanicalConflictClassifier.MarkerDeprecatedName));
        });
        Assert.DoesNotContain(
            resolvedByCurrentEncoding,
            entry => entry.Version.Role == UniqueItemVersionRole.Historical);

        var remainingPermyriad = exactConflicts.Count(entry =>
            entry.Block.MechanicalMapping.ConflictEvidence!.Kind ==
            UniqueMechanicalConflictKind.CurrentVsDeprecatedEncodingPermyriadPercent);
        Assert.True(
            remainingPermyriad < int.Parse(PreviousSubclassPermyriadBaseline),
            $"Expected remaining permyriad subclass count below {PreviousSubclassPermyriadBaseline}; observed {remainingPermyriad}.");

        var subtypeCounts = exactConflicts
            .GroupBy(entry => entry.Block.MechanicalMapping.ConflictEvidence!.Kind)
            .ToDictionary(group => group.Key, group => group.Count());
        var fingerprintPayload = string.Join(
            '\n',
            exactConflicts.Select(entry =>
            {
                var evidence = entry.Block.MechanicalMapping.ConflictEvidence!;
                return string.Join(
                    '\u001f',
                    entry.Block.Id,
                    evidence.Kind,
                    string.Join(',', evidence.Candidates.Select(candidate => candidate.ModifierId)),
                    string.Join(
                        '|',
                        evidence.Candidates.Select(candidate => string.Join(',', candidate.StatIds))));
            }));
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintPayload)))
            .ToLowerInvariant();
        Assert.Equal(
            fingerprint,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintPayload)))
                .ToLowerInvariant());

        var reportPath = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-ExactConflict-CurrentEncodingCorpus.json");
        await File.WriteAllTextAsync(
            reportPath,
            JsonSerializer.Serialize(
                new
                {
                    package.Manifest.DataVersion,
                    priorExactConflictBlocks = int.Parse(PreviousExactConflictCountBaseline),
                    totalExactConflictBlocks = exactConflicts.Length,
                    distinctIdentities = exactConflicts
                        .Select(entry => entry.Item.Id)
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                    resolvedByCurrentEncodingFilter = resolvedByCurrentEncoding.Length,
                    resolvedHistoricalByCurrentEncodingFilter = resolvedByCurrentEncoding
                        .Count(entry => entry.Version.Role == UniqueItemVersionRole.Historical),
                    resolvedExact = resolvedByCurrentEncoding.Count(entry =>
                        entry.Block.MechanicalMapping.Status ==
                        UniqueModifierMechanicalMappingStatus.Exact),
                    resolvedEquivalentSourceSet = resolvedByCurrentEncoding.Count(entry =>
                        entry.Block.MechanicalMapping.Status ==
                        UniqueModifierMechanicalMappingStatus.EquivalentSourceSet),
                    remainingPermyriadSubclass = remainingPermyriad,
                    subtypeCounts = subtypeCounts
                        .OrderByDescending(entry => entry.Value)
                        .ThenBy(entry => entry.Key.ToString(), StringComparer.Ordinal)
                        .ToDictionary(entry => entry.Key.ToString(), entry => entry.Value),
                    classificationFingerprintSha256 = fingerprint,
                },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public async Task ActivePackage_ControlItems_MatchCurrentEncodingResolutionContract()
    {
        var package = await LoadActivePackageAsync();

        AssertResolvedCurrentEncodingControl(
            package,
            "Hrimnor's Hymn",
            "local_life_leech_from_physical_damage_permyriad");
        AssertExactConflictControl(
            package,
            "Asenath's Gentle Touch",
            UniqueMechanicalConflictKind.LevelVsChanceOnHit,
            UniqueMechanicalConflictClassifier.MarkerLevel,
            UniqueMechanicalConflictClassifier.MarkerChance);
        AssertExactConflictControl(
            package,
            "Circle of Fear",
            UniqueMechanicalConflictKind.InverseLegacyHandlerEncoding,
            UniqueMechanicalConflictClassifier.MarkerEfficiencyPlus,
            UniqueMechanicalConflictClassifier.MarkerEfficiencyInverse);
    }

    private static async Task<GameDataPackage> LoadActivePackageAsync()
    {
        var packagePath = Environment.GetEnvironmentVariable("POENHANCE_GAMEDATA_AUDIT_PATH")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), $"Package not found: {packagePath}");
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess, string.Join("; ", load.Diagnostics.Select(d => d.Message)));
        return Assert.IsType<GameDataPackage>(load.Package);
    }

    private static IEnumerable<(UniqueItemIdentity Item, UniqueItemVersionObservation Version, UniqueModifierBlock Block)>
        EnumerateExactConflicts(GameDataPackage package) =>
        package.UniqueItems!.Items.SelectMany(item => item.Versions.SelectMany(version =>
            version.ModifierBlocks
                .Where(block => string.Equals(
                    block.MechanicalMapping.DiagnosticCode,
                    "UNIQUE_MECHANICS_EXACT_CONFLICT",
                    StringComparison.Ordinal))
                .Select(block => (item, version, block))));

    private static void AssertResolvedCurrentEncodingControl(
        GameDataPackage package,
        string canonicalName,
        string expectedStatId)
    {
        var item = Assert.Single(
            package.UniqueItems!.Items,
            candidate => string.Equals(
                candidate.CanonicalName,
                canonicalName,
                StringComparison.OrdinalIgnoreCase));
        var current = Assert.Single(
            item.Versions,
            version => version.Role == UniqueItemVersionRole.Current);
        var resolved = current.ModifierBlocks
            .Where(block =>
                block.MechanicalMapping.Provenance?.ResolutionReasons.Contains(
                    "current-role-deprecated-encoding-filter",
                    StringComparer.Ordinal) == true)
            .ToArray();
        Assert.NotEmpty(resolved);
        Assert.All(resolved, block =>
        {
            Assert.True(block.MechanicalMapping.Status is
                UniqueModifierMechanicalMappingStatus.Exact or
                UniqueModifierMechanicalMappingStatus.EquivalentSourceSet);
            Assert.Null(block.MechanicalMapping.ConflictEvidence);
            Assert.Contains(expectedStatId, block.MechanicalMapping.StatIds);
            Assert.DoesNotContain(
                block.MechanicalMapping.StatIds,
                statId => UniqueMechanicalConflictClassifier.BuildEncodingMarkers(
                        "x",
                        [statId],
                        [])
                    .Contains(UniqueMechanicalConflictClassifier.MarkerDeprecatedName));
        });

        Assert.All(
            item.Versions.Where(version => version.Role == UniqueItemVersionRole.Historical),
            version => Assert.DoesNotContain(
                version.ModifierBlocks,
                block => block.MechanicalMapping.Provenance?.ResolutionReasons.Contains(
                    "current-role-deprecated-encoding-filter",
                    StringComparer.Ordinal) == true));
    }

    private static void AssertExactConflictControl(
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
