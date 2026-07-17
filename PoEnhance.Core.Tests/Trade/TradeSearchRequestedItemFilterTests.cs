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
                TradeSearchRequestedItemFilterKind.Links,
                TradeSearchRequestedItemFilterKind.Sockets,
            ],
            draft.RequestedItemFilters.Select(filter => filter.Kind));
        Assert.Equal([85, 0, 3, 5], draft.RequestedItemFilters.Select(filter => filter.ObservedValue));
        Assert.Equal(["85", "0", "3", "5"], draft.RequestedItemFilters.Select(filter => filter.CurrentText));
        Assert.All(draft.RequestedItemFilters, filter => Assert.False(filter.IsActive));
        Assert.Equal("G-R-R G-B", draft.SocketText);
    }

    [Fact]
    public void CreateDraft_MissingSocketDataDoesNotFabricateSocketCountOrColours()
    {
        var draft = CreateDraft(ItemWithProperty("Quality: +20% (augmented)"));

        Assert.DoesNotContain(draft.RequestedItemFilters,
            filter => filter.Kind == TradeSearchRequestedItemFilterKind.Sockets);
        Assert.Null(draft.SocketText);
    }

    [Theory]
    [InlineData("Quality: +10% (augmented)", 10)]
    [InlineData("Quality: +20% (augmented)", 20)]
    [InlineData("Quality: +28% (augmented)", 28)]
    public void CreateDraft_ValidQualityRetainsExactObservedInteger(string qualityLine, int expected)
    {
        var draft = CreateDraft(ItemWithProperty(qualityLine));

        var quality = Filter(draft, TradeSearchRequestedItemFilterKind.Quality);
        Assert.Equal(expected, quality.ObservedValue);
        Assert.Equal(expected.ToString(System.Globalization.CultureInfo.InvariantCulture), quality.CurrentText);
        Assert.Equal(TradeSearchRequestedItemFilterValidationStatus.Valid, quality.LocalValidationStatus);
        Assert.False(quality.IsActive);
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

    private static string ItemWithProperty(string propertyLine) => $$"""
        Item Class: One Hand Axes
        Rarity: Rare
        Header Test
        Reaver Axe
        --------
        {{propertyLine}}
        --------
        Item Level: 85
        """;
}
