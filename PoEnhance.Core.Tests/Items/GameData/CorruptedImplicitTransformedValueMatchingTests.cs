using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

/// <summary>
/// Package-backed coverage for corrupted special-implicits whose display scale differs from raw storage.
/// </summary>
public sealed class CorruptedImplicitTransformedValueMatchingTests
{
    private static readonly string[] TransformedCorruptedModifierIds =
    [
        "V2ColdDamageLifeLeechPermyriadCorrupted_",
        "V2FireDamageLifeLeechPermyriadCorrupted_",
        "V2LightningDamageLifeLeechPermyriadCorrupted",
        "V2IncreasedAtackCriticalStrikeCorruption",
        "V2IncreasedSpellCriticalStrikeCorruption",
    ];

    [Fact]
    public async Task Resolve_HrimnorResolveColdLeechDisplayedValue_IsExactTypedCorruptedCandidate()
    {
        var (catalog, _) = await LoadPackageAsync();
        var parsed = new ItemTextParser().Parse("""
Item Class: Helmets
Rarity: Unique
Hrimnor's Resolve
Samnite Helmet
--------
Item Level: 80
--------
{ Corruption Implicit Modifier }
0.5% of Cold Damage Leeched as Life
--------
{ Unique Modifier — Defences, Armour }
108(100-120)% increased Armour
--------
Corrupted
""");
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, catalog);
        var sources = new ParsedItemModifierCandidateResolver().Resolve(parsed, catalog, baseResolution);
        var cold = Assert.Single(
            sources,
            source => source.ParsedModifier.ValueLines.Contains("0.5% of Cold Damage Leeched as Life"));

        Assert.Equal(ParsedImplicitModifierOrigin.Corrupted, cold.ParsedModifier.ImplicitOrigin);
        Assert.Equal(ModifierGenerationType.Corrupted, cold.GenerationType);
        Assert.Equal(ModifierCandidateResolutionStatus.Exact, cold.Status);
        var candidate = Assert.Single(cold.Candidates);
        Assert.Equal("V2ColdDamageLifeLeechPermyriadCorrupted_", candidate.Id);
        Assert.Equal(
            ["base_life_leech_from_cold_damage_permyriad"],
            candidate.Stats.Select(stat => stat.StatId!).ToArray());
        Assert.DoesNotContain(
            cold.Candidates,
            match => match.Id == "ColdDamageLifeLeechPermyriadCorrupted");
    }

    [Fact]
    public async Task Resolve_AllEligibleTransformedValueCorruptedCandidates_MatchDisplayedDomain()
    {
        var (catalog, package) = await LoadPackageAsync();
        var resolver = new ParsedItemModifierCandidateResolver();
        var parser = new ItemTextParser();

        foreach (var modifierId in TransformedCorruptedModifierIds)
        {
            var modifier = Assert.Single(package.Modifiers!, candidate => candidate.Id == modifierId);
            Assert.Equal(ModifierGenerationType.Corrupted, modifier.GenerationType);
            Assert.NotEqual(ModifierSourceAvailability.Disabled, modifier.SourceAvailability);

            var stat = Assert.Single(modifier.Stats, entry => !string.IsNullOrWhiteSpace(entry.StatId));
            var translation = Assert.Single(
                catalog.FindStatTranslationsByStatIdGroup([stat.StatId!])
                    .SelectMany(entry => entry.Variants),
                variant =>
                    (variant.ValueFormats is ["#"] or ["+#"]) &&
                    variant.IndexHandlers.Any(handler =>
                        handler.Index == 0 &&
                        handler.Handlers.Count > 0));
            var handlers = Assert.Single(
                translation.IndexHandlers,
                handler => handler.Index == 0).Handlers;

            var displayedMin = Project(stat.MinValue!.Value, handlers);
            var displayedMax = Project(stat.MaxValue!.Value, handlers);
            var observed = displayedMin;
            var formatLine = Assert.Single(translation.FormatLines);
            var observedLine = formatLine.Replace(
                "{0}",
                (translation.ValueFormats[0] == "+#" ? "+" : string.Empty) +
                observed.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal);

            var clipboard = $$"""
Item Class: Helmets
Rarity: Rare
Probe Item
Probe Base
--------
Item Level: 80
--------
{ Corruption Implicit Modifier }
{{observedLine}}
--------
Corrupted
""";
            var parsed = parser.Parse(clipboard);
            var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, catalog);
            var sources = resolver.Resolve(parsed, catalog, baseResolution);
            var source = Assert.Single(sources, entry => entry.ParsedModifierKind == ParsedModifierKind.Implicit);

            Assert.True(
                source.Status == ModifierCandidateResolutionStatus.Exact,
                $"{modifierId}: status={source.Status}; diags={string.Join(" | ", source.Diagnostics.Select(diagnostic => diagnostic.Code))}; line={observedLine}");
            Assert.Contains(source.Candidates, candidate => candidate.Id == modifierId);
            Assert.Equal(
                stat.StatId,
                Assert.Single(source.Candidates.First(candidate => candidate.Id == modifierId).Stats).StatId);
            Assert.Equal(displayedMin, Project(stat.MinValue!.Value, handlers));
            Assert.Equal(displayedMax, Project(stat.MaxValue!.Value, handlers));
        }
    }

    [Fact]
    public async Task Resolve_TransformedValueCorrupted_RejectsRawDomainClipboard()
    {
        var (catalog, _) = await LoadPackageAsync();
        var parsed = new ItemTextParser().Parse("""
Item Class: Helmets
Rarity: Rare
Probe Item
Probe Base
--------
Item Level: 80
--------
{ Corruption Implicit Modifier }
50% of Cold Damage Leeched as Life
--------
Corrupted
""");
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, catalog);
        var source = Assert.Single(
            new ParsedItemModifierCandidateResolver().Resolve(parsed, catalog, baseResolution));

        Assert.Equal(ModifierCandidateResolutionStatus.Unknown, source.Status);
        Assert.DoesNotContain(
            source.Candidates,
            candidate => candidate.Id == "V2ColdDamageLifeLeechPermyriadCorrupted_");
        Assert.DoesNotContain(
            source.ExcludedCandidates ?? [],
            candidate => candidate.Id == "V2ColdDamageLifeLeechPermyriadCorrupted_");
        Assert.Contains(
            source.Diagnostics,
            diagnostic =>
                diagnostic.Code is
                    ModifierCandidateResolutionDiagnosticCodes.ModifierNotFound or
                    ModifierCandidateResolutionDiagnosticCodes.ModifierTextNoMatch);
    }

    private static decimal Project(decimal raw, IReadOnlyList<string> handlers)
    {
        var projected = raw;
        foreach (var handler in handlers)
        {
            projected = handler.Trim().ToLowerInvariant() switch
            {
                "divide_by_one_hundred" or
                "divide_by_one_hundred_2dp" or
                "divide_by_one_hundred_2dp_if_required" =>
                    decimal.Round(projected / 100m, 2, MidpointRounding.AwayFromZero),
                "old_leech_permyriad" => projected / 500m,
                "old_leech_percent" => projected / 5m,
                "divide_by_one_thousand" => projected / 1000m,
                _ => throw new InvalidOperationException($"Unexpected handler '{handler}'."),
            };
        }

        return projected;
    }

    private static async Task<(GameDataCatalog Catalog, GameDataPackage Package)> LoadPackageAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess, string.Join(" | ", load.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var package = Assert.IsType<GameDataPackage>(load.Package);
        return (GameDataCatalog.FromPackage(package), package);
    }
}
