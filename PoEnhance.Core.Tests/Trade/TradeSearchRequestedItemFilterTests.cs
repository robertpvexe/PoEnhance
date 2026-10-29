using System.Collections.Immutable;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.Core.Tests.Trade;

public sealed class TradeSearchRequestedItemFilterTests
{
    private readonly ItemTextParser parser = new();
    private readonly TradeSearchDraftMapper mapper = new();

    [Fact]
    public void CreateDraft_ProjectsRequestedHeaderFiltersInCanonicalOrderWithEditableSocketCount()
    {
        var draft = CreateDraft("""
            Item Class: One Hand Axes
            Rarity: Rare
            Header Test
            Reaver Axe
            --------
            Sockets: G-R-R G-B
            --------
            Item Level: 85
            """);

        Assert.Equal(
            [
                TradeSearchRequestedItemFilterKind.ItemLevel,
                TradeSearchRequestedItemFilterKind.Quality,
                TradeSearchRequestedItemFilterKind.Sockets,
                TradeSearchRequestedItemFilterKind.Links,
                TradeSearchRequestedItemFilterKind.MemoryStrands,
                TradeSearchRequestedItemFilterKind.Intangibility,
            ],
            draft.RequestedItemFilters.Select(filter => filter.Kind));
        Assert.Equal(
            [85, 0, 5, 3, null, null],
            draft.RequestedItemFilters.Select(filter => filter.ObservedValue));
        Assert.Equal(
            ["85", "0", "5", "3", "", ""],
            draft.RequestedItemFilters.Select(filter => filter.CurrentText));
        var memory = Filter(draft, TradeSearchRequestedItemFilterKind.MemoryStrands);
        Assert.True(memory.IsMinimumOnly);
        Assert.False(memory.IsMaximumOnly);
        Assert.Equal(string.Empty, memory.MaximumText);
        Assert.Null(memory.RequestedMaximum);
        Assert.False(memory.IsActive);
        var intangibility = Filter(draft, TradeSearchRequestedItemFilterKind.Intangibility);
        Assert.True(intangibility.IsMaximumOnly);
        Assert.False(intangibility.IsMinimumOnly);
        Assert.Equal(string.Empty, intangibility.CurrentText);
        Assert.Null(intangibility.RequestedMinimum);
        Assert.Equal(string.Empty, intangibility.MaximumText);
        Assert.Null(intangibility.RequestedMaximum);
        Assert.False(intangibility.IsActive);
        Assert.False(Filter(draft, TradeSearchRequestedItemFilterKind.ItemLevel).IsActive);
        Assert.False(Filter(draft, TradeSearchRequestedItemFilterKind.Quality).IsActive);
        Assert.False(Filter(draft, TradeSearchRequestedItemFilterKind.Links).IsActive);
        Assert.False(Filter(draft, TradeSearchRequestedItemFilterKind.Sockets).IsActive);
        Assert.Equal("G-R-R G-B", draft.SocketText);
        AssertSocketColoursBlank(Filter(draft, TradeSearchRequestedItemFilterKind.Sockets));
        AssertSocketColoursBlank(Filter(draft, TradeSearchRequestedItemFilterKind.Links));
    }

    [Fact]
    public void ParseMinimumOnly_AcceptsZeroAndPositiveMinAndIgnoresStaleMaximum()
    {
        var empty = Filter(
            CreateDraft(ItemWithProperty("Quality: +20% (augmented)")),
            TradeSearchRequestedItemFilterKind.MemoryStrands);
        var zeroMin = TradeSearchDraftMapper.ParseRequestedItemFilterBoundText(
            empty,
            TradeSearchRequestedItemFilterBound.Minimum,
            "0",
            isActive: true);
        Assert.Equal(0, zeroMin.RequestedMinimum);
        Assert.Null(zeroMin.RequestedMaximum);
        Assert.Equal(string.Empty, zeroMin.MaximumText);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Valid, zeroMin.LocalValidationStatus);

        var positiveMin = TradeSearchDraftMapper.ParseRequestedItemFilterBoundText(
            empty,
            TradeSearchRequestedItemFilterBound.Minimum,
            "40",
            isActive: true);
        Assert.Equal(40, positiveMin.RequestedMinimum);
        Assert.Null(positiveMin.RequestedMaximum);

        var withStaleMax = empty with
        {
            MaximumText = "100",
            RequestedMaximum = 100,
        };
        var cleared = TradeSearchDraftMapper.ParseRequestedItemFilterBoundText(
            withStaleMax,
            TradeSearchRequestedItemFilterBound.Minimum,
            "10",
            isActive: true);
        Assert.Equal(10, cleared.RequestedMinimum);
        Assert.Null(cleared.RequestedMaximum);
        Assert.Equal(string.Empty, cleared.MaximumText);

        var ignoredMaxWrite = TradeSearchDraftMapper.ParseRequestedItemFilterBoundText(
            cleared,
            TradeSearchRequestedItemFilterBound.Maximum,
            "80",
            isActive: true);
        Assert.Equal(10, ignoredMaxWrite.RequestedMinimum);
        Assert.Null(ignoredMaxWrite.RequestedMaximum);
        Assert.Equal(string.Empty, ignoredMaxWrite.MaximumText);
        Assert.True(ignoredMaxWrite.HasRequestedConstraint);
    }

    [Fact]
    public void ParseMaximumOnly_AcceptsZeroAndPositiveMaxAndIgnoresStaleMinimum()
    {
        var empty = Filter(
            CreateDraft(ItemWithProperty("Quality: +20% (augmented)")),
            TradeSearchRequestedItemFilterKind.Intangibility);
        var zeroMax = TradeSearchDraftMapper.ParseRequestedItemFilterBoundText(
            empty,
            TradeSearchRequestedItemFilterBound.Maximum,
            "0",
            isActive: true);
        Assert.Equal(0, zeroMax.RequestedMaximum);
        Assert.Null(zeroMax.RequestedMinimum);
        Assert.Equal(string.Empty, zeroMax.CurrentText);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Valid, zeroMax.LocalValidationStatus);

        var positiveMax = TradeSearchDraftMapper.ParseRequestedItemFilterBoundText(
            empty,
            TradeSearchRequestedItemFilterBound.Maximum,
            "21",
            isActive: true);
        Assert.Equal(21, positiveMax.RequestedMaximum);
        Assert.Null(positiveMax.RequestedMinimum);

        var withStaleMin = empty with
        {
            CurrentText = "8",
            RequestedMinimum = 8,
        };
        var cleared = TradeSearchDraftMapper.ParseRequestedItemFilterBoundText(
            withStaleMin,
            TradeSearchRequestedItemFilterBound.Maximum,
            "21",
            isActive: true);
        Assert.Equal(21, cleared.RequestedMaximum);
        Assert.Null(cleared.RequestedMinimum);
        Assert.Equal(string.Empty, cleared.CurrentText);

        var redirectedMinWrite = TradeSearchDraftMapper.ParseRequestedItemFilterBoundText(
            cleared,
            TradeSearchRequestedItemFilterBound.Minimum,
            "15",
            isActive: true);
        Assert.Equal(15, redirectedMinWrite.RequestedMaximum);
        Assert.Null(redirectedMinWrite.RequestedMinimum);
        Assert.Equal(string.Empty, redirectedMinWrite.CurrentText);
        Assert.True(redirectedMinWrite.HasRequestedConstraint);
    }

    [Theory]
    [InlineData("Normal", 59, false)]
    [InlineData("Normal", 60, true)]
    [InlineData("Magic", 59, false)]
    [InlineData("Magic", 60, true)]
    [InlineData("Rare", 49, false)]
    [InlineData("Rare", 89, false)]
    [InlineData("Rare", 90, true)]
    [InlineData("Unique", 100, false)]
    public void CreateDraft_MemoryStrandsAutoActivationFollowsRarityThresholds(
        string rarity,
        int strands,
        bool expectedActive)
    {
        var draft = CreateDraft(MemoryStrandsItem(rarity, strands));

        var filter = Filter(draft, TradeSearchRequestedItemFilterKind.MemoryStrands);
        Assert.Equal(strands, filter.ObservedValue);
        Assert.Equal(strands.ToString(System.Globalization.CultureInfo.InvariantCulture), filter.CurrentText);
        Assert.Equal(strands, filter.RequestedMinimum);
        Assert.Null(filter.RequestedMaximum);
        Assert.Equal(expectedActive, filter.IsActive);
        Assert.True(filter.IsMinimumOnly);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Valid, filter.LocalValidationStatus);
    }

    [Theory]
    [InlineData("Corrupted")]
    [InlineData("Mirrored")]
    [InlineData("Corrupted\nMirrored")]
    public void CreateDraft_MemoryStrandsNeverAutoActivatesWhenCorruptedOrMirrored(string states)
    {
        var draft = CreateDraft($$"""
            Item Class: Gloves
            Rarity: Rare
            Morbid Vise
            Martyr Gloves
            --------
            Quality: +20% (augmented)
            Armour: 100
            --------
            Requires Level: 40
            --------
            Item Level: 84
            --------
            Memory Strands: 100
            --------
            {{states}}
            """);

        var filter = Filter(draft, TradeSearchRequestedItemFilterKind.MemoryStrands);
        Assert.Equal(100, filter.ObservedValue);
        Assert.Equal("100", filter.CurrentText);
        Assert.Equal(100, filter.RequestedMinimum);
        Assert.False(filter.IsActive);
    }

    [Fact]
    public void CreateDraft_IntangibilityHeaderSeedsInactiveMaximum()
    {
        var draft = CreateDraft("""
            Item Class: Amulets
            Rarity: Rare
            Grim Pendant
            Amber Amulet
            --------
            Requires Level: 7
            --------
            Item Level: 84
            --------
            Intangibility: 21%
            """);

        var filter = Filter(draft, TradeSearchRequestedItemFilterKind.Intangibility);
        Assert.Equal(21, filter.ObservedValue);
        Assert.Equal("21", filter.MaximumText);
        Assert.Equal(21, filter.RequestedMaximum);
        Assert.Null(filter.RequestedMinimum);
        Assert.Equal(string.Empty, filter.CurrentText);
        Assert.False(filter.IsActive);
        Assert.True(filter.IsMaximumOnly);
        Assert.True(filter.HasRequestedConstraint);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Valid, filter.LocalValidationStatus);
        Assert.DoesNotContain(
            draft.RequestedItemFilters,
            candidate => candidate.Kind == TradeSearchRequestedItemFilterKind.BasePercentile);
    }

    [Fact]
    public void ParseMaximumOnly_TogglingActivationPreservesMaximumValue()
    {
        var seeded = Filter(
            CreateDraft("""
                Item Class: Amulets
                Rarity: Rare
                Grim Pendant
                Amber Amulet
                --------
                Requires Level: 7
                --------
                Item Level: 84
                --------
                Intangibility: 10%
                """),
            TradeSearchRequestedItemFilterKind.Intangibility);
        Assert.False(seeded.IsActive);
        Assert.Equal(10, seeded.RequestedMaximum);

        var activated = TradeSearchDraftMapper.ParseRequestedItemFilterMaximumText(
            seeded,
            seeded.MaximumText,
            isActive: true);
        Assert.True(activated.IsActive);
        Assert.Equal("10", activated.MaximumText);
        Assert.Equal(10, activated.RequestedMaximum);

        var deactivated = TradeSearchDraftMapper.ParseRequestedItemFilterMaximumText(
            activated,
            activated.MaximumText,
            isActive: false);
        Assert.False(deactivated.IsActive);
        Assert.Equal("10", deactivated.MaximumText);
        Assert.Equal(10, deactivated.RequestedMaximum);
        Assert.Null(deactivated.RequestedMinimum);
    }

    private static string MemoryStrandsItem(string rarity, int strands) => $$"""
        Item Class: Gloves
        Rarity: {{rarity}}
        Morbid Vise
        Martyr Gloves
        --------
        Quality: +20% (augmented)
        Armour: 100
        --------
        Requires Level: 40
        --------
        Item Level: 84
        --------
        Memory Strands: {{strands}}
        """;

    [Fact]
    public void CreateDraft_MissingSocketDataDoesNotFabricateSocketCountOrColours()
    {
        var draft = CreateDraft(ItemWithProperty("Quality: +20% (augmented)"));

        Assert.DoesNotContain(draft.RequestedItemFilters,
            filter => filter.Kind == TradeSearchRequestedItemFilterKind.Sockets);
        Assert.DoesNotContain(draft.RequestedItemFilters,
            filter => filter.Kind == TradeSearchRequestedItemFilterKind.Links);
        Assert.Null(draft.SocketText);
    }

    [Theory]
    [InlineData("Normal", "R-R-R-R G-G", true, false)]
    [InlineData("Normal", "R-R-R-R-R-G", true, true)]
    [InlineData("Normal", "R-R-R-R", false, false)]
    [InlineData("Magic", "R-R-R-R-R-G", false, false)]
    [InlineData("Rare", "R-R-R-R-R-G", false, false)]
    [InlineData("Unique", "R-R-R-R-R-G", false, false)]
    public void CreateDraft_SocketAndLinkDefaultActivationDependsOnNormalSixOnly(
        string rarity,
        string sockets,
        bool expectedSocketsActive,
        bool expectedLinksActive)
    {
        var draft = CreateDraft($$"""
            Item Class: Body Armours
            Rarity: {{rarity}}
            Header Test
            Plate Vest
            --------
            Sockets: {{sockets}}
            --------
            Item Level: 85
            """);

        var socketsFilter = Filter(draft, TradeSearchRequestedItemFilterKind.Sockets);
        var linksFilter = Filter(draft, TradeSearchRequestedItemFilterKind.Links);
        Assert.Equal(expectedSocketsActive, socketsFilter.IsActive);
        Assert.Equal(expectedLinksActive, linksFilter.IsActive);
        Assert.Equal(
            sockets.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Sum(group => group.Split('-').Length),
            socketsFilter.ObservedValue);
        Assert.Equal(
            sockets.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Max(group => group.Split('-').Length),
            linksFilter.ObservedValue);
        AssertSocketColoursBlank(socketsFilter);
        AssertSocketColoursBlank(linksFilter);
    }

    [Fact]
    public void CreateDraft_DisjointLinkedGroupsPrefillLinksFromLargestSingleGroup()
    {
        var draft = CreateDraft("""
            Item Class: Body Armours
            Rarity: Rare
            Header Test
            Plate Vest
            --------
            Sockets: R-R-R-G B-B
            --------
            Item Level: 85
            """);

        Assert.Equal(6, Filter(draft, TradeSearchRequestedItemFilterKind.Sockets).ObservedValue);
        Assert.Equal(4, Filter(draft, TradeSearchRequestedItemFilterKind.Links).ObservedValue);
        Assert.Equal("4", Filter(draft, TradeSearchRequestedItemFilterKind.Links).CurrentText);
    }

    [Fact]
    public void ParseRequestedItemFilterColourText_DoesNotAutoActivateOrPopulateSiblingFields()
    {
        var source = Filter(CreateDraft("""
            Item Class: One Hand Axes
            Rarity: Rare
            Header Test
            Reaver Axe
            --------
            Sockets: W-W-W-W-W-W
            --------
            Item Level: 85
            """), TradeSearchRequestedItemFilterKind.Sockets);

        var edited = TradeSearchDraftMapper.ParseRequestedItemFilterColourText(
            source,
            TradeSearchRequestedItemFilterColour.White,
            "3");

        Assert.False(edited.IsActive);
        Assert.Equal("3", edited.WhiteText);
        Assert.Equal(3, edited.RequestedWhite);
        Assert.Equal(string.Empty, edited.RedText);
        Assert.Null(edited.RequestedRed);
        Assert.Equal(source.CurrentText, edited.CurrentText);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Valid, edited.LocalValidationStatus);
    }

    [Theory]
    [InlineData("Quality: +10% (augmented)", 10, false)]
    [InlineData("Quality: +20% (augmented)", 20, false)]
    [InlineData("Quality: +21% (augmented)", 21, true)]
    [InlineData("Quality: +28% (augmented)", 28, true)]
    public void CreateDraft_ValidQualityRetainsExactObservedInteger(
        string qualityLine,
        int expected,
        bool expectedActive)
    {
        var draft = CreateDraft(ItemWithProperty(qualityLine));

        var quality = Filter(draft, TradeSearchRequestedItemFilterKind.Quality);
        Assert.Equal(expected, quality.ObservedValue);
        Assert.Equal(expected.ToString(System.Globalization.CultureInfo.InvariantCulture), quality.CurrentText);
        Assert.Equal(expected, quality.RequestedMinimum);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Valid, quality.LocalValidationStatus);
        Assert.Equal(expectedActive, quality.IsActive);
    }

    [Theory]
    [InlineData("Normal", true)]
    [InlineData("Magic", false)]
    [InlineData("Rare", false)]
    public void CreateDraft_ItemLevelDefaultActivationDependsOnCopiedRarityWhenQualityIsAtMostTwenty(
        string rarity,
        bool expectedActive)
    {
        var draft = CreateDraft(ItemWithRarity(rarity, "Quality: +20% (augmented)"));

        var itemLevel = Filter(draft, TradeSearchRequestedItemFilterKind.ItemLevel);
        Assert.Equal(85, itemLevel.ObservedValue);
        Assert.Equal("85", itemLevel.CurrentText);
        Assert.Equal(85, itemLevel.RequestedMinimum);
        Assert.Equal(expectedActive, itemLevel.IsActive);
        Assert.False(Filter(draft, TradeSearchRequestedItemFilterKind.Quality).IsActive);
    }

    [Theory]
    [InlineData("Normal", 21)]
    [InlineData("Magic", 28)]
    [InlineData("Rare", 28)]
    public void CreateDraft_OverqualityActivatesItemLevelForOrdinaryItems(
        string rarity,
        int quality)
    {
        var draft = CreateDraft(ItemWithRarity(rarity, $"Quality: +{quality}% (augmented)"));

        var itemLevel = Filter(draft, TradeSearchRequestedItemFilterKind.ItemLevel);
        var qualityFilter = Filter(draft, TradeSearchRequestedItemFilterKind.Quality);
        Assert.True(itemLevel.IsActive);
        Assert.Equal(85, itemLevel.RequestedMinimum);
        Assert.True(qualityFilter.IsActive);
        Assert.Equal(quality, qualityFilter.RequestedMinimum);
    }

    [Fact]
    public void CreateDraft_QualityAboveTwentyDefaultsOnForOrdinaryItemsButNotUnique()
    {
        var rare = CreateDraft(ItemWithRarity("Rare", "Quality: +28% (augmented)"));
        var unique = CreateDraft(ItemWithRarity("Unique", "Quality: +28% (augmented)"));

        var rareQuality = Filter(rare, TradeSearchRequestedItemFilterKind.Quality);
        Assert.True(rareQuality.IsActive);
        Assert.Equal(28, rareQuality.RequestedMinimum);
        Assert.True(Filter(rare, TradeSearchRequestedItemFilterKind.ItemLevel).IsActive);
        Assert.Equal(85, Filter(rare, TradeSearchRequestedItemFilterKind.ItemLevel).RequestedMinimum);

        var uniqueQuality = Filter(unique, TradeSearchRequestedItemFilterKind.Quality);
        Assert.False(uniqueQuality.IsActive);
        Assert.Equal(28, uniqueQuality.ObservedValue);
        Assert.Equal("28", uniqueQuality.CurrentText);
        Assert.False(Filter(unique, TradeSearchRequestedItemFilterKind.ItemLevel).IsActive);
    }

    [Fact]
    public void CreateDraft_OverqualityWithoutItemLevelDoesNotFabricateIlvl()
    {
        var draft = CreateDraft("""
            Item Class: One Hand Axes
            Rarity: Rare
            Header Test
            Reaver Axe
            --------
            Quality: +28% (augmented)
            """);

        var itemLevel = Filter(draft, TradeSearchRequestedItemFilterKind.ItemLevel);
        Assert.Null(itemLevel.ObservedValue);
        Assert.Null(itemLevel.RequestedMinimum);
        Assert.False(itemLevel.IsActive);
        Assert.True(Filter(draft, TradeSearchRequestedItemFilterKind.Quality).IsActive);
    }

    [Fact]
    public void CreateDraft_UnknownItemLevelDoesNotFabricateActiveCriterion()
    {
        var draft = CreateDraft("""
            Item Class: One Hand Axes
            Rarity: Normal
            Header Test
            Reaver Axe
            --------
            Quality: +20% (augmented)
            """);

        var itemLevel = Filter(draft, TradeSearchRequestedItemFilterKind.ItemLevel);
        Assert.Null(itemLevel.ObservedValue);
        Assert.Null(itemLevel.RequestedMinimum);
        Assert.False(itemLevel.IsActive);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Empty, itemLevel.LocalValidationStatus);
    }

    [Fact]
    public void ParseRequestedText_EditingMinDoesNotToggleActivation()
    {
        var source = Filter(CreateDraft(ItemWithProperty("Quality: +20% (augmented)")),
            TradeSearchRequestedItemFilterKind.Quality);

        var editedWhileOff = TradeSearchDraftMapper.ParseRequestedItemFilterText(source, "28");
        var activated = TradeSearchDraftMapper.ParseRequestedItemFilterText(
            editedWhileOff,
            editedWhileOff.CurrentText,
            isActive: true);
        var editedWhileOn = TradeSearchDraftMapper.ParseRequestedItemFilterText(activated, "15");

        Assert.False(editedWhileOff.IsActive);
        Assert.Equal(28, editedWhileOff.RequestedMinimum);
        Assert.Equal(20, editedWhileOff.ObservedValue);
        Assert.True(editedWhileOn.IsActive);
        Assert.Equal(15, editedWhileOn.RequestedMinimum);
        Assert.Equal(20, editedWhileOn.ObservedValue);
    }

    [Fact]
    public void CreateDraft_MalformedQualityIsNotSilentlyReplacedWithZero()
    {
        var draft = CreateDraft(ItemWithProperty("Quality: unusually fine"));

        var quality = Filter(draft, TradeSearchRequestedItemFilterKind.Quality);
        Assert.Null(quality.ObservedValue);
        Assert.NotEqual("0", quality.CurrentText);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Invalid, quality.LocalValidationStatus);
        Assert.Contains("malformed", quality.DiagnosticReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseRequestedText_SeparatesObservedRequestedAndActiveState()
    {
        var source = Filter(CreateDraft(ItemWithProperty("Quality: +20% (augmented)")),
            TradeSearchRequestedItemFilterKind.Quality);

        var activeEqual = TradeSearchDraftMapper.ParseRequestedItemFilterText(source, "20", isActive: true);
        var edited = TradeSearchDraftMapper.ParseRequestedItemFilterText(activeEqual, "28");
        var inactive = TradeSearchDraftMapper.ParseRequestedItemFilterText(edited, edited.CurrentText, isActive: false);

        Assert.True(activeEqual.IsActive);
        Assert.Equal(20, activeEqual.ObservedValue);
        Assert.Equal(20, activeEqual.RequestedMinimum);
        Assert.Equal(20, edited.ObservedValue);
        Assert.Equal(28, edited.RequestedMinimum);
        Assert.Equal("28", inactive.CurrentText);
        Assert.Equal(28, inactive.RequestedMinimum);
        Assert.False(inactive.IsActive);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("１２")]
    public void ParseRequestedText_InvalidInputRetainsTextAndHasNoRequestedMinimum(string text)
    {
        var source = Filter(CreateDraft(ItemWithProperty("Quality: +20% (augmented)")),
            TradeSearchRequestedItemFilterKind.Quality);

        var result = TradeSearchDraftMapper.ParseRequestedItemFilterText(source, text, isActive: true);

        Assert.Equal(text, result.CurrentText);
        Assert.Null(result.RequestedMinimum);
        Assert.True(result.IsActive);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Invalid, result.LocalValidationStatus);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseRequestedText_EmptyInputIsAnOptionalActiveConstraint(string text)
    {
        var source = Filter(CreateDraft(ItemWithProperty("Quality: +20% (augmented)")),
            TradeSearchRequestedItemFilterKind.Quality);

        var result = TradeSearchDraftMapper.ParseRequestedItemFilterText(source, text, isActive: true);

        Assert.True(result.IsActive);
        Assert.Equal(text, result.CurrentText);
        Assert.Null(result.RequestedMinimum);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Empty, result.LocalValidationStatus);
        Assert.Null(result.DiagnosticReason);
    }

    [Fact]
    public void Validate_ActiveEmptyRequestedFilterDoesNotBlockSearch()
    {
        var draft = CreateDraft(ItemWithProperty("Quality: +20% (augmented)"));
        var quality = TradeSearchDraftMapper.ParseRequestedItemFilterText(
            Filter(draft, TradeSearchRequestedItemFilterKind.Quality),
            "",
            isActive: true);

        var result = new TradeSearchDraftValidator().Validate(Replace(draft, quality));

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Code ==
            TradeSearchValidationDiagnosticCodes.RequestedItemFilterInvalid);
    }

    [Fact]
    public void Validate_InactiveUnsupportedFilterDoesNotBlockButSelectedUnsupportedAndInvalidDo()
    {
        var draft = CreateDraft(ItemWithProperty("Quality: +20% (augmented)"));
        var quality = Filter(draft, TradeSearchRequestedItemFilterKind.Quality) with
        {
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Unsupported,
            DiagnosticReason = "unsupported test mapping",
        };
        var inactive = Replace(draft, quality);
        var activeUnsupported = Replace(draft, quality with { IsActive = true });
        var activeInvalid = Replace(draft, TradeSearchDraftMapper.ParseRequestedItemFilterText(
            quality,
            "not-a-number",
            isActive: true));

        Assert.DoesNotContain(
            new TradeSearchDraftValidator().Validate(inactive).Diagnostics,
            diagnostic => diagnostic.Severity == TradeSearchValidationSeverity.Error);
        Assert.Contains(
            new TradeSearchDraftValidator().Validate(activeUnsupported).Diagnostics,
            diagnostic => diagnostic.Code ==
                TradeSearchValidationDiagnosticCodes.RequestedItemFilterUnsupported);
        Assert.Contains(
            new TradeSearchDraftValidator().Validate(activeInvalid).Diagnostics,
            diagnostic => diagnostic.Code ==
                TradeSearchValidationDiagnosticCodes.RequestedItemFilterInvalid);
    }

    private TradeSearchDraft CreateDraft(string text)
    {
        var result = mapper.CreateDraft(parser.Parse(text));
        Assert.True(result.IsSuccess);
        return Assert.IsType<TradeSearchDraft>(result.Draft);
    }

    private static TradeSearchRequestedItemFilter Filter(
        TradeSearchDraft draft,
        TradeSearchRequestedItemFilterKind kind) =>
        Assert.Single(draft.RequestedItemFilters, filter => filter.Kind == kind);

    private static TradeSearchDraft Replace(
        TradeSearchDraft draft,
        TradeSearchRequestedItemFilter replacement) =>
        draft with
        {
            RequestedItemFilters = draft.RequestedItemFilters
                .Select(filter => filter.Kind == replacement.Kind ? replacement : filter)
                .ToImmutableArray(),
        };

    private static string ItemWithProperty(string propertyLine) =>
        ItemWithRarity("Rare", propertyLine);

    private static string ItemWithRarity(string rarity, string propertyLine) => $$"""
        Item Class: One Hand Axes
        Rarity: {{rarity}}
        Header Test
        Reaver Axe
        --------
        {{propertyLine}}
        --------
        Item Level: 85
        """;

    private static void AssertSocketColoursBlank(TradeSearchRequestedItemFilter filter)
    {
        Assert.Equal(string.Empty, filter.RedText);
        Assert.Equal(string.Empty, filter.GreenText);
        Assert.Equal(string.Empty, filter.BlueText);
        Assert.Equal(string.Empty, filter.WhiteText);
        Assert.Null(filter.RequestedRed);
        Assert.Null(filter.RequestedGreen);
        Assert.Null(filter.RequestedBlue);
        Assert.Null(filter.RequestedWhite);
    }
}
