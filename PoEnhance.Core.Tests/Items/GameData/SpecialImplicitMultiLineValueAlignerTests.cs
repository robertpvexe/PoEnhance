using System.Globalization;
using PoEnhance.Core.Items.GameData;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.GameData;

namespace PoEnhance.Core.Tests.Items.GameData;

public sealed class SpecialImplicitMultiLineValueAlignerTests
{
    private readonly ItemTextParser parser = new();
    private readonly ParsedItemModifierCandidateResolver resolver = new();

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(50)]
    public async Task TryMatch_ArbitraryCardinality_UniqueCompleteAlignment(int componentCount)
    {
        var catalog = await LoadActiveCatalogAsync();
        var components = SelectDistinctSingleStatComponents(catalog, componentCount);
        var candidate = BuildMultiStatCandidate(components);
        var lines = components
            .Select(component => RenderDisplayedLine(component.Translation, component.Observed))
            .ToArray();
        // Shuffle so alignment cannot rely on source-stat order.
        var shuffled = ShufflePreserveValues(lines, seed: componentCount * 17 + 3);
        var modifier = CreateCorruptedModifier(shuffled.Lines);

        Assert.True(SpecialImplicitMultiLineValueAligner.TryMatch(modifier, candidate, catalog));
        Assert.True(
            SpecialImplicitMultiLineValueAligner.TryAlignStatSubsets(
                modifier,
                candidate,
                catalog,
                out var subsets));
        Assert.Equal(componentCount, subsets.Count);
        for (var lineIndex = 0; lineIndex < shuffled.Lines.Length; lineIndex++)
        {
            var expectedStatId = components[shuffled.OriginalIndexes[lineIndex]].Stat.StatId!;
            Assert.Equal(
                [expectedStatId],
                subsets[lineIndex].Select(stat => stat.StatId!).ToArray());
        }
    }

    [Fact]
    public async Task TryMatch_SingleLine_ReturnsFalseSoExistingPathOwnsN1()
    {
        var catalog = await LoadActiveCatalogAsync();
        var component = Assert.Single(SelectDistinctSingleStatComponents(catalog, 1));
        var candidate = BuildMultiStatCandidate([component]);
        var modifier = CreateCorruptedModifier(
            [RenderDisplayedLine(component.Translation, component.Observed)]);

        Assert.False(SpecialImplicitMultiLineValueAligner.TryMatch(modifier, candidate, catalog));
    }

    [Fact]
    public async Task TryMatch_TwoLinesMatchSameComponent_FailsClosed()
    {
        var catalog = await LoadActiveCatalogAsync();
        var components = SelectDistinctSingleStatComponents(catalog, 2);
        var candidate = BuildMultiStatCandidate(components);
        var duplicateLine = RenderDisplayedLine(components[0].Translation, components[0].Observed);
        var modifier = CreateCorruptedModifier([duplicateLine, duplicateLine]);

        Assert.False(SpecialImplicitMultiLineValueAligner.TryMatch(modifier, candidate, catalog));
    }

    [Fact]
    public async Task TryMatch_MissingVisibleLineForOneComponent_FailsClosed()
    {
        var catalog = await LoadActiveCatalogAsync();
        var components = SelectDistinctSingleStatComponents(catalog, 2);
        var candidate = BuildMultiStatCandidate(components);
        var modifier = CreateCorruptedModifier(
        [
            RenderDisplayedLine(components[0].Translation, components[0].Observed),
            "Completely unrelated visible text with 12 number",
        ]);

        Assert.False(SpecialImplicitMultiLineValueAligner.TryMatch(modifier, candidate, catalog));
    }

    [Fact]
    public async Task TryMatch_ExtraUnrelatedVisibleLine_FailsClosedOnArity()
    {
        var catalog = await LoadActiveCatalogAsync();
        var components = SelectDistinctSingleStatComponents(catalog, 2);
        var third = SelectDistinctSingleStatComponents(catalog, 3)[2];
        var candidate = BuildMultiStatCandidate(components);
        var modifier = CreateCorruptedModifier(
        [
            RenderDisplayedLine(components[0].Translation, components[0].Observed),
            RenderDisplayedLine(components[1].Translation, components[1].Observed),
            RenderDisplayedLine(third.Translation, third.Observed),
        ]);

        Assert.False(SpecialImplicitMultiLineValueAligner.TryMatch(modifier, candidate, catalog));
    }

    [Fact]
    public async Task TryMatch_NumericBoundsFail_RejectsPairing()
    {
        var catalog = await LoadActiveCatalogAsync();
        var components = SelectDistinctSingleStatComponents(catalog, 2);
        var candidate = BuildMultiStatCandidate(components);
        var outOfRange = components[1].Stat.MaxValue!.Value + 100m;
        var modifier = CreateCorruptedModifier(
        [
            RenderDisplayedLine(components[0].Translation, components[0].Observed),
            RenderDisplayedLine(components[1].Translation, outOfRange),
        ]);

        Assert.False(SpecialImplicitMultiLineValueAligner.TryMatch(modifier, candidate, catalog));
    }

    [Fact]
    public async Task TryMatch_DuplicatedVisibleLineWithMatchingArity_FailsClosed()
    {
        var catalog = await LoadActiveCatalogAsync();
        var components = SelectDistinctSingleStatComponents(catalog, 2);
        // Candidate has two identical stat components — duplicated visible lines must not
        // invent stronger Exact provenance via multiple perfect matchings.
        var duplicated = new ModifierDefinition
        {
            Id = "SyntheticDuplicatedStatComponents",
            GenerationType = ModifierGenerationType.Corrupted,
            Stats =
            [
                components[0].Stat with { Index = 0 },
                components[0].Stat with { Index = 1 },
            ],
        };
        var line = RenderDisplayedLine(components[0].Translation, components[0].Observed);
        var modifier = CreateCorruptedModifier([line, line]);

        // Two identical components × two identical lines ⇒ multiple complete alignments.
        Assert.False(SpecialImplicitMultiLineValueAligner.TryMatch(modifier, duplicated, catalog));
    }

    [Fact]
    public async Task Resolve_WrongBaseEligibleCorrupted_RemainsExcluded()
    {
        var catalog = await LoadActiveCatalogAsync();
        var parsed = parser.Parse("""
            Item Class: Boots
            Rarity: Unique
            Kaom's Roots
            Titan Greaves
            --------
            Item Level: 70
            --------
            { Corruption Implicit Modifier }
            20% chance to cause Bleeding on Hit
            36(30-40)% increased Attack Damage against Bleeding Enemies
            --------
            Corrupted
            """);
        var baseResolution = new ParsedItemBaseResolver().Resolve(parsed, catalog);
        var results = resolver.Resolve(parsed, catalog, baseResolution);
        var bleed = Assert.Single(
            results,
            result => result.ParsedModifier.ValueLines.Any(line =>
                line.Contains("Bleeding on Hit", StringComparison.OrdinalIgnoreCase)));

        // Boots eligibility must exclude the one-handed weapon multi-stat corruption even if
        // text/value alignment would otherwise be unique.
        Assert.NotEqual(ModifierCandidateResolutionStatus.Exact, bleed.Status);
        Assert.DoesNotContain(
            bleed.Candidates,
            candidate => candidate.Id ==
                "V2ChanceToBleedOnHitAndIncreasedDamageToBleedingTargetsCorrupted_");
    }

    private static ParsedModifier CreateCorruptedModifier(IReadOnlyList<string> valueLines) =>
        new(
            valueLines,
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

    private static IReadOnlyList<SyntheticComponent> SelectDistinctSingleStatComponents(
        GameDataCatalog catalog,
        int count)
    {
        var selected = new List<SyntheticComponent>();
        var usedSignatures = new HashSet<string>(StringComparer.Ordinal);
        var matcher = new ModifierTextSignatureMatcher();
        foreach (var modifier in catalog.Modifiers
                     .Where(candidate =>
                         candidate.GenerationType == ModifierGenerationType.Corrupted &&
                         candidate.SourceAvailability != ModifierSourceAvailability.Disabled &&
                         candidate.Stats.Count(stat => !string.IsNullOrWhiteSpace(stat.StatId)) == 1)
                     .OrderBy(candidate => candidate.Id, StringComparer.Ordinal))
        {
            var stat = modifier.Stats.Single(entry => !string.IsNullOrWhiteSpace(entry.StatId));
            if (!stat.MinValue.HasValue ||
                !stat.MaxValue.HasValue ||
                selected.Any(existing =>
                    string.Equals(existing.Stat.StatId, stat.StatId, StringComparison.Ordinal)))
            {
                continue;
            }

            var translations = catalog.FindStatTranslationsByStatIdGroup([stat.StatId!]);
            var variant = translations
                .SelectMany(translation => translation.Variants)
                .FirstOrDefault(IsIdentitySinglePlaceholderVariant);
            if (variant is null)
            {
                continue;
            }

            // Reject templates with literal digits ("per 20 Dexterity") — naive displayed-number
            // scrape cannot distinguish placeholder values from fixed template numerals.
            var templateWithoutPlaceholder = variant.FormatLines[0]
                .Replace("{0}", string.Empty, StringComparison.Ordinal);
            if (templateWithoutPlaceholder.Any(char.IsDigit))
            {
                continue;
            }

            var probeCandidate = new ModifierDefinition
            {
                Id = "signature-probe",
                GenerationType = ModifierGenerationType.Corrupted,
                Stats = [stat with { Index = 0 }],
            };
            var line = RenderDisplayedLine(variant, stat.MinValue.Value);
            var text = matcher.Match(probeCandidate, catalog, [line]);
            if (text.Outcome != ModifierTextSignatureMatchOutcome.Match ||
                text.CandidateSignatures.Count == 0)
            {
                continue;
            }

            var signatureKey = string.Join('\n', text.CandidateSignatures[0].Lines);
            if (!usedSignatures.Add(signatureKey))
            {
                continue;
            }

            selected.Add(new SyntheticComponent(stat, variant, stat.MinValue.Value));
            if (selected.Count == count)
            {
                return selected;
            }
        }

        throw new InvalidOperationException(
            $"Could not find {count} distinct single-stat corrupted components with identity translations " +
            $"(found {selected.Count}).");
    }

    private static bool IsIdentitySinglePlaceholderVariant(StatTranslationVariant variant)
    {
        if (variant.FormatLines.Count != 1 ||
            variant.ValueFormats is not (["#"] or ["+#"]) ||
            variant.FormatLines[0].Count(ch => ch == '{') != 1)
        {
            return false;
        }

        var handlers = variant.IndexHandlers.Where(handler => handler.Index == 0).ToArray();
        return handlers.Length == 0 ||
            (handlers.Length == 1 && handlers[0].Handlers.Count == 0);
    }

    private static ModifierDefinition BuildMultiStatCandidate(
        IReadOnlyList<SyntheticComponent> components) =>
        new()
        {
            Id = $"SyntheticMultiStatN{components.Count}",
            GenerationType = ModifierGenerationType.Corrupted,
            Stats = components
                .Select((component, index) => component.Stat with { Index = index })
                .ToArray(),
        };

    private static string RenderDisplayedLine(StatTranslationVariant translation, decimal observed)
    {
        var prefix = translation.ValueFormats[0] == "+#" ? "+" : string.Empty;
        return translation.FormatLines[0].Replace(
            "{0}",
            prefix + observed.ToString(CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }

    private static (string[] Lines, int[] OriginalIndexes) ShufflePreserveValues(
        IReadOnlyList<string> lines,
        int seed)
    {
        var indexes = Enumerable.Range(0, lines.Count).ToArray();
        var random = new Random(seed);
        for (var index = indexes.Length - 1; index > 0; index--)
        {
            var swap = random.Next(index + 1);
            (indexes[index], indexes[swap]) = (indexes[swap], indexes[index]);
        }

        return (indexes.Select(index => lines[index]).ToArray(), indexes);
    }

    private static async Task<GameDataCatalog> LoadActiveCatalogAsync()
    {
        var packagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "artifacts", "poenhance-game-data.json"));
        Assert.True(File.Exists(packagePath), packagePath);
        var load = await GameDataPackageLoader.LoadFromFileAsync(packagePath);
        Assert.True(load.IsSuccess);
        return GameDataCatalog.FromPackage(load.Package!);
    }

    private sealed record SyntheticComponent(
        ModifierStat Stat,
        StatTranslationVariant Translation,
        decimal Observed);
}
