using System.Text.RegularExpressions;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed partial class UniqueCandidateRuntimeTests
{
    [Fact]
    public async Task Candidate_RepresentativeRawUniquePathsResolveAndFailClosed()
    {
        var candidatePath = Environment.GetEnvironmentVariable("POENHANCE_UNIQUE_CANDIDATE");
        if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
        {
            return;
        }

        var load = await GameDataPackageLoader.LoadFromFileAsync(candidatePath);
        var package = Assert.IsType<GameDataPackage>(load.Package);
        var catalog = GameDataCatalog.FromPackage(package);
        var uniqueCatalog = Assert.IsType<UniqueItemCatalog>(package.UniqueItems);

        var dragonfang = Resolve(catalog, """
            Item Class: Amulets
            Rarity: Unique
            Replica Dragonfang's Flight
            Onyx Amulet
            --------
            Item Level: 80
            """);
        Assert.Equal(UniqueItemKind.Replica, dragonfang.Identity?.Kind);
        Assert.Contains(dragonfang.CompatibleVersions, version => version.Role == UniqueItemVersionRole.Current);
        Assert.Contains(dragonfang.CompatibleVersions, version => version.Role == UniqueItemVersionRole.Historical);
        Assert.False(dragonfang.IsLegacy);

        var dragonfangGenerated = Resolve(catalog, """
            Item Class: Amulets
            Rarity: Unique
            Replica Dragonfang's Flight
            Onyx Amulet
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +3 to Level of all Absolution Gems
            """);
        var dragonfangBlock = Assert.Single(dragonfangGenerated.ModifierBlocks);
        Assert.True(dragonfangBlock.IsResolved, dragonfangBlock.Diagnostic);
        Assert.Equal(
            ["random_skill_gem_level_+_level", "random_skill_gem_level_+_index"],
            dragonfangBlock.StatIds);
        Assert.All(dragonfangBlock.SourceObservationIds, observationId =>
            Assert.True(Assert.Single(uniqueCatalog.SourceObservations, source =>
                source.Id == observationId).IsGenerated));
        var dragonfangDraft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            ParseRaw("""
                Item Class: Amulets
                Rarity: Unique
                Replica Dragonfang's Flight
                Onyx Amulet
                --------
                Item Level: 80
                --------
                { Unique Modifier }
                +3 to Level of all Absolution Gems
                """),
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        Assert.True(Assert.Single(dragonfangDraft.ModifierFilters).IsSearchable);

        var foulbornRaw = """
            Item Class: Wands
            Rarity: Unique
            Foulborn Midnight Bargain
            Calling Wand
            --------
            Item Level: 83
            --------
            { Unique Modifier — Minion }
            +1 to maximum number of Raised Zombies
            +1 to maximum number of Spectres
            +1 to maximum number of Skeletons
            { Foulborn Unique Modifier — Life, Defences, Energy Shield, Minion }
            Lose 0.5% Life and Energy Shield per Second per Minion
            """;
        var foulborn = Resolve(catalog, foulbornRaw);
        Assert.True(foulborn.IsFoulborn);
        Assert.Equal("Midnight Bargain", foulborn.Identity?.CanonicalName);
        Assert.Collection(
            foulborn.ModifierBlocks,
            ordinary =>
            {
                Assert.False(ordinary.IsResolved);
                Assert.Equal("UNIQUE_BLOCK_VERSION_MISMATCH", ordinary.DiagnosticCode);
            },
            replacement => Assert.Equal(
                "FOULBORN_REPLACEMENT_MECHANICS_UNAVAILABLE",
                replacement.DiagnosticCode));
        var foulbornDraft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            ParseRaw(foulbornRaw),
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        Assert.Equal(TradeTriState.Yes, foulbornDraft.ItemVariantCriteria.Foulborn);
        Assert.Collection(
            foulbornDraft.ModifierFilters,
            ordinary => Assert.False(ordinary.IsSearchable),
            replacement => Assert.False(replacement.IsSearchable));

        var currentScalar = FindCase(uniqueCatalog, version => version.Role == UniqueItemVersionRole.Current,
            block => IsResolved(block) && block.Lines.Count == 1 && SourceRangePattern().IsMatch(block.Lines[0]));
        AssertResolvedBlock(catalog, currentScalar, expectLegacy: false);

        var presence = FindCase(uniqueCatalog, version => version.Role == UniqueItemVersionRole.Current,
            block => IsResolved(block) && block.Lines.Count == 1 && !block.Lines[0].Any(char.IsAsciiDigit));
        var presenceParsed = Parse(presence);
        var presenceDraft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            presenceParsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        var presenceRow = Assert.Single(presenceDraft.ModifierFilters);
        Assert.False(presenceRow.SupportsValueBounds);
        Assert.Null(presenceRow.RequestedMinimum);
        Assert.Null(presenceRow.RequestedMaximum);

        var multiLine = FindCase(uniqueCatalog, version => version.Role == UniqueItemVersionRole.Current,
            block => IsResolved(block) && block.Lines.Count > 1);
        var multiParsed = Parse(multiLine);
        var multiDraft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            multiParsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        Assert.Single(multiDraft.ModifierFilters);
        Assert.Contains(Environment.NewLine, multiDraft.ModifierFilters[0].OriginalText, StringComparison.Ordinal);

        var historical = FindHistoricalOnlyCase(uniqueCatalog);
        AssertResolvedBlock(catalog, historical, expectLegacy: true);

        var dataDerivedFoulborn = currentScalar with { Foulborn = true };
        var foulbornResolution = Resolve(catalog, Raw(dataDerivedFoulborn));
        Assert.True(foulbornResolution.IsFoulborn);
        Assert.Equal(currentScalar.Item.CanonicalName, foulbornResolution.Identity?.CanonicalName);

        var unsupported = FindCase(uniqueCatalog, _ => true,
            block => block.Kind == UniqueModifierBlockKind.Unique &&
                block.MechanicalMapping.Status is UniqueModifierMechanicalMappingStatus.Unsupported or
                    UniqueModifierMechanicalMappingStatus.Ambiguous);
        var unsupportedResolution = Resolve(catalog, Raw(unsupported));
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, unsupportedResolution.Status);
        Assert.False(Assert.Single(unsupportedResolution.ModifierBlocks).IsResolved);
        Assert.False(string.IsNullOrWhiteSpace(unsupportedResolution.ModifierBlocks[0].DiagnosticCode));
    }

    private static void AssertResolvedBlock(GameDataCatalog catalog, CandidateCase candidate, bool expectLegacy)
    {
        var resolution = Resolve(catalog, Raw(candidate));
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, resolution.Status);
        Assert.Equal(expectLegacy, resolution.IsLegacy);
        Assert.True(Assert.Single(resolution.ModifierBlocks).IsResolved);
    }

    private static UniqueItemResolutionResult Resolve(GameDataCatalog catalog, string raw)
    {
        var parsed = new ItemTextParser().Parse(raw);
        return new ParsedUniqueItemResolver().Resolve(parsed, catalog);
    }

    private static ParsedItem Parse(CandidateCase candidate) => new ItemTextParser().Parse(Raw(candidate));

    private static ParsedItem ParseRaw(string raw) => new ItemTextParser().Parse(raw);

    private static CandidateCase FindCase(
        UniqueItemCatalog catalog,
        Func<UniqueItemVersionObservation, bool> versionPredicate,
        Func<UniqueModifierBlock, bool> blockPredicate)
    {
        foreach (var item in catalog.Items)
        foreach (var version in item.Versions.Where(versionPredicate))
        foreach (var block in version.ModifierBlocks.Where(blockPredicate))
        {
            return new CandidateCase(item, version, block, Foulborn: false);
        }
        throw new Xunit.Sdk.XunitException("The candidate catalog lacks a required representative coverage case.");
    }

    private static CandidateCase FindHistoricalOnlyCase(UniqueItemCatalog catalog)
    {
        foreach (var item in catalog.Items)
        {
            var currentSignatures = item.Versions
                .Where(version => version.Role == UniqueItemVersionRole.Current)
                .SelectMany(version => version.ModifierBlocks)
                .Select(block => string.Join('\u001f', block.CanonicalSignatures))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var version in item.Versions.Where(version => version.Role == UniqueItemVersionRole.Historical))
            foreach (var block in version.ModifierBlocks.Where(block => IsResolved(block) &&
                block.Kind == UniqueModifierBlockKind.Unique &&
                !currentSignatures.Contains(string.Join('\u001f', block.CanonicalSignatures))))
            {
                return new CandidateCase(item, version, block, Foulborn: false);
            }
        }
        throw new Xunit.Sdk.XunitException("The candidate catalog lacks a historical-only resolved block.");
    }

    private static bool IsResolved(UniqueModifierBlock block) =>
        block.Kind == UniqueModifierBlockKind.Unique &&
        block.MechanicalMapping.Status is UniqueModifierMechanicalMappingStatus.Exact or
            UniqueModifierMechanicalMappingStatus.EquivalentSourceSet;

    private static string Raw(CandidateCase candidate)
    {
        var name = candidate.Foulborn
            ? $"Foulborn {candidate.Item.CanonicalName}"
            : candidate.Item.CanonicalName;
        var lines = candidate.Block.Lines.Select(MaterializeObservedRange);
        return string.Join(Environment.NewLine,
        [
            "Item Class: Test Items",
            "Rarity: Unique",
            name!,
            candidate.Version.BaseType!,
            "--------",
            "Item Level: 80",
            "--------",
            "{ Unique Modifier }",
            ..lines,
        ]);
    }

    private static string MaterializeObservedRange(string line) => SourceRangePattern().Replace(
        line,
        match => $"{match.Groups["sign"].Value}{match.Groups["minimum"].Value}" +
            $"({match.Groups["minimum"].Value}-{match.Groups["maximum"].Value})");

    [GeneratedRegex(@"(?<sign>[+-]?)\(\s*(?<minimum>[+-]?\d+(?:[\.,]\d+)?)\s*-\s*(?<maximum>[+-]?\d+(?:[\.,]\d+)?)\s*\)")]
    private static partial Regex SourceRangePattern();

    private sealed record CandidateCase(
        UniqueItemIdentity Item,
        UniqueItemVersionObservation Version,
        UniqueModifierBlock Block,
        bool Foulborn);
}
