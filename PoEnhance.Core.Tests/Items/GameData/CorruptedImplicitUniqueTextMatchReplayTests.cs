using System.Text.Json;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class CorruptedImplicitUniqueTextMatchReplayTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemModifierCandidateResolver resolver = new();

    [Theory]
    [InlineData(
        "20260919-194512-110-Uul-Netol's Kiss.json",
        "Fortify",
        "V2SupportedByFortifyCorrupted_")]
    [InlineData(
        "20260919-194449-087-Dreamfeather.json",
        "Resolute Technique",
        "V2ResoluteTechniqueCorrupted")]
    [InlineData(
        "20260919-194348-356-Kaom's Roots.json",
        "Shocked Ground",
        "V2UnaffectedByShockedGroundCorrupted")]
    public async Task Resolve_A522CorruptedPresenceOrFixedText_BecomesExact(
        string fileName,
        string lineContains,
        string expectedModId)
    {
        var catalog = await LoadActiveCatalogAsync();
        var raw = await ReadCaptureClipboardAsync(fileName);
        var parsed = parser.Parse(raw);

        var result = FindCorruptedLine(resolver.Resolve(parsed, catalog), lineContains);

        Assert.Equal(ParsedImplicitModifierOrigin.Corrupted, result.ParsedModifier.ImplicitOrigin);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(expectedModId, Assert.Single(result.Candidates).Id);
        Assert.Equal(
            ModifierCandidateResolutionDiagnosticCodes.ModifierTextExactMatch,
            Assert.Single(result.Diagnostics).Code);
        Assert.NotNull(result.TextSignatureMatches);
        Assert.Contains(
            result.TextSignatureMatches,
            match => match.Outcome == ModifierTextSignatureMatchOutcome.Match);
    }

    [Theory]
    [InlineData(
        "20260919-194244-619-Kaom's Roots.json",
        "Socketed Curse",
        "V2SocketedCurseGemCorrupted")]
    [InlineData(
        "20260919-194458-274-Soul Taker.json",
        "Adds",
        "V2LocalAddedPhysicalDamage1hCorrupted2")]
    public async Task Resolve_A522WorkingCorruptedControls_RemainExact(
        string fileName,
        string lineContains,
        string expectedModId)
    {
        var catalog = await LoadActiveCatalogAsync();
        var raw = await ReadCaptureClipboardAsync(fileName);
        var parsed = parser.Parse(raw);

        var result = FindCorruptedLine(resolver.Resolve(parsed, catalog), lineContains);

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal(expectedModId, Assert.Single(result.Candidates).Id);
    }

    [Fact]
    public async Task Resolve_A522WeaponRangeTransformedUnits_BecomesExact()
    {
        var catalog = await LoadActiveCatalogAsync();
        var raw = await ReadCaptureClipboardAsync("20260919-194326-390-Innsbury Edge.json");
        var parsed = parser.Parse(raw);

        var result = FindCorruptedLine(resolver.Resolve(parsed, catalog), "Weapon Range");

        Assert.Equal(ModifierCandidateResolutionStatus.Exact, result.Status);
        Assert.Equal("V2LocalMeleeWeaponRangeCorrupted", Assert.Single(result.Candidates).Id);
        Assert.Contains(
            result.Candidates[0].Stats,
            stat => string.Equals(stat.StatId, "local_weapon_range_+", StringComparison.Ordinal));
        Assert.Equal(2m, result.Candidates[0].Stats.Single(stat =>
            string.Equals(stat.StatId, "local_weapon_range_+", StringComparison.Ordinal)).MinValue);
        Assert.Equal(4m, result.Candidates[0].Stats.Single(stat =>
            string.Equals(stat.StatId, "local_weapon_range_+", StringComparison.Ordinal)).MaxValue);
    }

    [Fact]
    public async Task Resolve_A522SoulBleedMultiline_RemainsNonExactUntilA527()
    {
        var catalog = await LoadActiveCatalogAsync();
        var raw = await ReadCaptureClipboardAsync("20260919-194458-274-Soul Taker.json");
        var parsed = parser.Parse(raw);

        var result = FindCorruptedLine(resolver.Resolve(parsed, catalog), "Bleeding on Hit");

        Assert.NotEqual(ModifierCandidateResolutionStatus.Exact, result.Status);
    }

    private static ModifierCandidateResolutionResult FindCorruptedLine(
        IReadOnlyList<ModifierCandidateResolutionResult> results,
        string lineContains)
    {
        return Assert.Single(
            results,
            candidate =>
                candidate.ParsedModifier.ImplicitOrigin == ParsedImplicitModifierOrigin.Corrupted &&
                candidate.ParsedModifier.ValueLines.Any(line =>
                    line.Contains(lineContains, StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task<string> ReadCaptureClipboardAsync(string fileName)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "PoEnhance-A.5.22-Corrupted-Implicit-Provenance",
            fileName);
        Assert.True(File.Exists(path), $"Missing ReplayReady capture: {path}");
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream);
        var raw = document.RootElement
            .GetProperty("replayContext")
            .GetProperty("rawClipboardText")
            .GetString();
        Assert.False(string.IsNullOrWhiteSpace(raw));
        return raw!;
    }

    private static async Task<GameDataCatalog> LoadActiveCatalogAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        Assert.Equal(4, load.Package!.Manifest.SchemaVersion);
        Assert.Equal("3.29.1.2.6-passive-skill-identity", load.Package.Manifest.DataVersion);
        return GameDataCatalog.FromPackage(load.Package);
    }
}
