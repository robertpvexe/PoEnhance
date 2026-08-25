using System.Text.RegularExpressions;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed partial class UniqueCandidateRuntimeTests
{
    [Fact]
    public async Task Candidate_TrueAdvancedCopyTextualOptionRange_PreservesGeneratedIdentityAndCopiedLevels()
    {
        var candidatePath = Environment.GetEnvironmentVariable("POENHANCE_UNIQUE_CANDIDATE");
        if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
        {
            return;
        }

        var load = await GameDataPackageLoader.LoadFromFileAsync(candidatePath);
        var package = Assert.IsType<GameDataPackage>(load.Package);
        var catalog = GameDataCatalog.FromPackage(package);
        var parsed = ParseRaw(TrueAdvancedCopyTextualOptionRange);

        Assert.Collection(
            parsed.UniqueModifiers,
            endurance =>
            {
                var effect = Assert.Single(endurance.Effects);
                Assert.True(effect.HasUnscalableValue);
                Assert.EndsWith(" — Unscalable Value", effect.RawText, StringComparison.Ordinal);
                Assert.Equal(
                    "Socketed Gems are Supported by Level 10(1-10) Endurance Charge on Melee Stun",
                    effect.SemanticText);
                Assert.Equal("Greater Multiple Projectiles-Hallow", effect.TextualOptionRange?.Text);
                Assert.Equal([10m], ModifierBoundDefaults.ExtractObservedValues(effect.Text));
            },
            inspiration =>
            {
                var effect = Assert.Single(inspiration.Effects);
                Assert.True(effect.HasUnscalableValue);
                Assert.EndsWith(" — Unscalable Value", effect.RawText, StringComparison.Ordinal);
                Assert.Equal(
                    "Socketed Gems are Supported by Level 26(25-35) Inspiration",
                    effect.SemanticText);
                Assert.Equal("Greater Multiple Projectiles-Hallow", effect.TextualOptionRange?.Text);
                Assert.Equal([26m], ModifierBoundDefaults.ExtractObservedValues(effect.Text));
            });

        var resolution = Resolve(catalog, TrueAdvancedCopyTextualOptionRange);
        Assert.Collection(
            resolution.ModifierBlocks,
            endurance =>
            {
                Assert.True(endurance.IsResolved, endurance.Diagnostic);
                Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, endurance.SourceSemantics);
                Assert.Single(endurance.CandidatePoolMembershipIds);
                Assert.Equal(
                    "Socketed Gems are Supported by Level (1-10) Endurance Charge on Melee Stun",
                    Assert.Single(endurance.CatalogBlocks).Lines[0]);
            },
            inspiration =>
            {
                Assert.True(inspiration.IsResolved, inspiration.Diagnostic);
                Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, inspiration.SourceSemantics);
                Assert.Single(inspiration.CandidatePoolMembershipIds);
                Assert.Equal(
                    "Socketed Gems are Supported by Level (25-35) Inspiration",
                    Assert.Single(inspiration.CatalogBlocks).Lines[0]);
            });

        var draft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            parsed,
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        Assert.Collection(
            draft.ModifierFilters,
            endurance => AssertGeneratedNumericLevel(endurance, 10m),
            inspiration => AssertGeneratedNumericLevel(inspiration, 26m));
    }

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
        var dragonfangComponent = Assert.Single(dragonfangDraft.ModifierFilters);
        Assert.True(dragonfangComponent.IsSearchable);
        Assert.Equal(3m, dragonfangComponent.RequestedMinimum);
        Assert.Null(dragonfangComponent.RequestedMaximum);
        Assert.Equal(ModifierBoundShape.Scalar, dragonfangComponent.ValueBoundShape);

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
                Assert.True(ordinary.IsResolved, ordinary.Diagnostic);
                Assert.True(ordinary.IsEquivalentSourceSet);
                Assert.Equal(3, ordinary.StatIds.Count);
                Assert.NotEmpty(ordinary.SourceObservationIds);
                Assert.Null(ordinary.DiagnosticCode);
            },
            replacement =>
            {
                Assert.False(replacement.IsResolved);
                Assert.Equal("FOULBORN_REPLACEMENT_TEXT_MISMATCH", replacement.DiagnosticCode);
            });
        var foulbornDraft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            ParseRaw(foulbornRaw),
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        Assert.Equal(TradeTriState.Yes, foulbornDraft.ItemVariantCriteria.Foulborn);
        Assert.Collection(
            foulbornDraft.ModifierFilters,
            ordinary =>
            {
                Assert.True(ordinary.IsSearchable);
                Assert.Contains(Environment.NewLine, ordinary.OriginalText, StringComparison.Ordinal);
            },
            replacement =>
            {
                Assert.False(replacement.IsSearchable);
                Assert.Equal(
                    "FOULBORN_REPLACEMENT_TEXT_MISMATCH",
                    replacement.UniqueResolutionDiagnosticCode);
            });

        var hungryLoopRaw = """
            Item Class: Rings
            Rarity: Unique
            The Hungry Loop
            Unset Ring
            --------
            Item Level: 80
            --------
            { Implicit Modifier }
            Has 1 Socket — Unscalable Value
            --------
            { Unique Modifier }
            Consumes Socketed Uncorrupted Support Gems when they reach Maximum Level
            Can Consume 4 Uncorrupted Support Gems
            Has not Consumed any Gems
            """;
        var hungryLoop = Resolve(catalog, hungryLoopRaw);
        Assert.Equal("The Hungry Loop", hungryLoop.Identity?.CanonicalName);
        Assert.Contains(hungryLoop.CompatibleVersions, version => version.BaseType == "Unset Ring");
        Assert.All(hungryLoop.ModifierBlocks, block => Assert.True(block.IsResolved, block.Diagnostic));
        var consumptionBlock = Assert.Single(hungryLoop.ModifierBlocks, block =>
            block.StatIds.Contains("local_unique_hungry_loop_number_of_gems_to_consume"));
        Assert.False(consumptionBlock.IsEquivalentSourceSet);
        Assert.Equal(["ConsumesSupportGemsUnique"], consumptionBlock.ModifierIds);
        Assert.NotEmpty(consumptionBlock.SourceObservationIds);
        var hungryLoopDraft = Assert.IsType<TradeSearchDraft>(new TradeSearchDraftMapper().CreateDraft(
            ParseRaw(hungryLoopRaw),
            modifierResolutions: [],
            gameDataCatalog: catalog).Draft);
        var consumptionRow = Assert.Single(hungryLoopDraft.ModifierFilters, component =>
            component.ResolvedStatIds.Contains("local_unique_hungry_loop_number_of_gems_to_consume"));
        Assert.True(consumptionRow.IsSearchable);
        Assert.Contains(Environment.NewLine, consumptionRow.OriginalText, StringComparison.Ordinal);

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

    [Fact]
    public async Task Candidate_OrthogonalOptionRowsCoexistWhileTrueAtomicVersionsRemainExclusive()
    {
        var candidatePath = Environment.GetEnvironmentVariable("POENHANCE_UNIQUE_CANDIDATE");
        if (string.IsNullOrWhiteSpace(candidatePath) || !File.Exists(candidatePath))
        {
            return;
        }

        var load = await GameDataPackageLoader.LoadFromFileAsync(candidatePath);
        var package = Assert.IsType<GameDataPackage>(load.Package);
        var catalog = GameDataCatalog.FromPackage(package);

        var anguish = Resolve(catalog, """
            Item Class: Rings
            Rarity: Unique
            Circle of Anguish
            Ruby Ring
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +1% to maximum Fire Resistance while affected by Herald of Ash
            { Unique Modifier }
            +55(50-60)% to Fire Resistance while affected by Herald of Ash
            """);
        Assert.NotEmpty(anguish.CompatibleVersions);
        Assert.Equal(2, anguish.ModifierBlocks.Count);
        Assert.All(anguish.ModifierBlocks, block =>
        {
            Assert.True(block.IsResolved, $"{block.DiagnosticCode}: {block.Diagnostic}");
            Assert.NotEmpty(block.CatalogBlocks);
            Assert.All(block.CatalogBlocks, catalogBlock => Assert.True(
                catalogBlock.MechanicalMapping.Status is
                    UniqueModifierMechanicalMappingStatus.Exact or
                    UniqueModifierMechanicalMappingStatus.EquivalentSourceSet));
            Assert.Single(block.OptionChoiceMemberships);
            Assert.NotEmpty(block.ModifierIds);
            Assert.NotEmpty(block.StatIds);
            Assert.NotEmpty(block.SourceObservationIds);
        });

        var fear = Resolve(catalog, """
            Item Class: Rings
            Rarity: Unique
            Circle of Fear
            Sapphire Ring
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            Herald of Ice has 36(30-40)% increased Mana Reservation Efficiency
            { Unique Modifier }
            +1% to maximum Cold Resistance while affected by Herald of Ice
            """);
        Assert.Single(fear.CompatibleVersions);
        var reservation = fear.ModifierBlocks.Single(block => block.ParsedModifierIndex == 0);
        Assert.False(reservation.IsResolved);
        Assert.Equal("UNIQUE_MECHANICS_EXACT_CONFLICT", reservation.DiagnosticCode);
        Assert.Single(reservation.OptionChoiceMemberships);
        var maximumCold = fear.ModifierBlocks.Single(block => block.ParsedModifierIndex == 1);
        Assert.True(maximumCold.IsResolved, maximumCold.Diagnostic);
        Assert.Single(maximumCold.OptionChoiceMemberships);

        var split = Resolve(catalog, """
            Item Class: Jewels
            Rarity: Unique
            Split Personality
            Crimson Jewel
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            +5 to maximum Energy Shield
            { Unique Modifier }
            +5 to Intelligence
            """);
        Assert.Single(split.CompatibleVersions);
        Assert.Equal(2, split.ModifierBlocks.Count);
        Assert.All(split.ModifierBlocks, block => Assert.True(
            block.IsResolved,
            $"{block.DiagnosticCode}: {block.Diagnostic}"));
        var splitMemberships = split.ModifierBlocks
            .SelectMany(block => block.OptionChoiceMemberships)
            .ToArray();
        Assert.Equal(2, splitMemberships.Length);
        Assert.Single(splitMemberships.Select(membership => membership.OptionAxisId).Distinct());
        Assert.Equal(2, splitMemberships.Select(membership => membership.OptionChoiceId).Distinct().Count());

        var coralito = Resolve(catalog, """
            Item Class: Utility Flasks
            Rarity: Unique
            Coralito's Signature
            Diamond Flask
            --------
            Item Level: 80
            --------
            { Unique Modifier }
            60(50-75)% increased Duration of Poisons you inflict during Effect
            { Unique Modifier }
            +25(20-30)% to Damage over Time Multiplier for Poison from Critical Strikes during Effect
            """);
        Assert.Empty(coralito.CompatibleVersions);
        Assert.Equal("UNIQUE_VERSION_NOT_FOUND", coralito.DiagnosticCode);
        Assert.All(coralito.Identity!.Versions, version => Assert.Empty(version.OptionAxes));
    }

    private static void AssertResolvedBlock(GameDataCatalog catalog, CandidateCase candidate, bool expectLegacy)
    {
        var resolution = Resolve(catalog, Raw(candidate));
        Assert.Equal(UniqueItemResolutionStatus.ExactIdentity, resolution.Status);
        Assert.Equal(expectLegacy, resolution.IsLegacy);
        Assert.True(Assert.Single(resolution.ModifierBlocks).IsResolved);
    }

    private static void AssertGeneratedNumericLevel(ResolvedSearchComponent component, decimal expectedLevel)
    {
        Assert.Equal(UniqueModifierSourceSemantics.GeneratedCandidate, component.UniqueSourceSemantics);
        Assert.Single(component.UniqueCandidatePoolMembershipIds);
        Assert.Equal(["Greater Multiple Projectiles-Hallow"], component.UniqueTextualOptionRangeAnnotations);
        Assert.EndsWith(" — Unscalable Value", component.RawCopiedText, StringComparison.Ordinal);
        Assert.NotEmpty(component.Sources);
        Assert.All(component.Sources, source => Assert.EndsWith(
            " — Unscalable Value",
            source.RawCopiedText,
            StringComparison.Ordinal));
        Assert.Equal([expectedLevel], component.ObservedNumericValues);
        Assert.Equal([expectedLevel], component.CanonicalNumericValues);
        Assert.Equal(expectedLevel, component.RequestedMinimum);
        Assert.Null(component.RequestedMaximum);
        Assert.Equal(ModifierBoundShape.Scalar, component.ValueBoundShape);
        Assert.True(component.SupportsValueBounds);
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

    private const string TrueAdvancedCopyTextualOptionRange = """
Item Class: Helmets
Rarity: Unique
Forbidden Shako
Great Crown
--------
Item Level: 86
--------
{ Unique Modifier — Gem }
Socketed Gems are Supported by Level 10(1-10) Endurance Charge on Melee Stun(Greater Multiple Projectiles-Hallow) — Unscalable Value
{ Unique Modifier — Gem }
Socketed Gems are Supported by Level 26(25-35) Inspiration(Greater Multiple Projectiles-Hallow) — Unscalable Value
""";

    [GeneratedRegex(@"(?<sign>[+-]?)\(\s*(?<minimum>[+-]?\d+(?:[\.,]\d+)?)\s*-\s*(?<maximum>[+-]?\d+(?:[\.,]\d+)?)\s*\)")]
    private static partial Regex SourceRangePattern();

    private sealed record CandidateCase(
        UniqueItemIdentity Item,
        UniqueItemVersionObservation Version,
        UniqueModifierBlock Block,
        bool Foulborn);
}
