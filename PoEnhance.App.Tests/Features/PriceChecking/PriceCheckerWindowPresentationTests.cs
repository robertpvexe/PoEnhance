using PoEnhance.App.Features.PriceChecking;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerWindowPresentationTests
{
    [Theory]
    [InlineData(BaseSearchMode.Category, "One-Handed Axe", null, null, "Item Category: —")]
    [InlineData(BaseSearchMode.Category, "Wand", null, "Wand", "Item Category: Wand")]
    [InlineData(BaseSearchMode.Category, "One Hand Axes", null, "One-Handed Axe", "Item Category: One-Handed Axe")]
    [InlineData(BaseSearchMode.Category, "Belt", null, "Belt", "Item Category: Belt")]
    [InlineData(BaseSearchMode.ExactBase, null, "Stygian Vise", "One-Handed Axe", "Exact Base: Stygian Vise")]
    public void ActiveBaseCriterion_UsesTheEffectiveCategoryOrExactBase(
        BaseSearchMode mode,
        string? category,
        string? exactBaseName,
        string? providerCategoryDisplayLabel,
        string expected)
    {
        var criterion = new BaseSearchCriterion
        {
            Mode = mode,
            Category = category,
            ExactBaseName = exactBaseName,
        };

        Assert.Equal(expected, PriceCheckerWindow.FormatActiveCriterion(
            criterion,
            providerCategoryDisplayLabel));
    }

    [Theory]
    [InlineData("Armageddon Thirst", "Reaver Axe", "Armageddon Thirst Reaver Axe")]
    [InlineData("Corruption Bond", "Stygian Vise", "Corruption Bond Stygian Vise")]
    public void Title_CombinesItemNameAndResolvedBaseName(
        string itemName,
        string baseName,
        string expected)
    {
        Assert.Equal(expected, PriceCheckerWindow.FormatTitle(itemName, baseName));
    }

    [Fact]
    public void ItemPresentation_UsesAlreadyParsedSocketPropertyAndOnlyShowsLinksWhenPresent()
    {
        var parsedItem = new ItemTextParser().Parse("""
Item Class: One Hand Axes
Rarity: Rare
Armageddon Thirst
Reaver Axe
--------
Sockets: B B-R
--------
Item Level: 82
""");

        var presentation = PriceCheckerItemPresentation.FromParsedItem(parsedItem);

        Assert.Equal("B B-R", presentation.SocketText);
        Assert.Equal("2", presentation.LinkText);
    }

    [Fact]
    public void ItemPresentation_DoesNotCreateSocketOrLinkMetadataWhenNoSocketPropertyExists()
    {
        var parsedItem = new ItemTextParser().Parse("""
Item Class: Rings
Rarity: Rare
Armageddon Thirst
Coral Ring
--------
Item Level: 82
""");

        var presentation = PriceCheckerItemPresentation.FromParsedItem(parsedItem);

        Assert.Null(presentation.SocketText);
        Assert.Null(presentation.LinkText);
    }

    [Fact]
    public void ModifierSelectedCount_UsesVisibleComponentCounts()
    {
        Assert.Equal("2 of 3 stats selected", PriceCheckerWindow.FormatModifierCount(2, 3));
    }

    [Fact]
    public void WindowXaml_PreservesAcceptedTitleBarAndCriterionPresentation()
    {
        var xaml = LoadWindowXaml();
        var title = ExtractElement(xaml, "<TextBlock x:Name=\"TitleDisplayNameText\"", "/>");
        var reset = ExtractElement(xaml, "<Button x:Name=\"ResetPositionButton\"", "</Button>");
        var close = ExtractElement(xaml, "<Button x:Name=\"CloseButton\"", "</Button>");
        var criterionStyle = ExtractElement(xaml, "<Style x:Key=\"BaseCriterionButtonStyle\"", "</Style>");

        Assert.Contains("Grid.Column=\"1\"", title);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", title);
        Assert.Contains("x:Name=\"PinToggleButton\"", xaml);
        Assert.Contains("Width=\"20\"", ExtractElement(reset, "<Canvas", "</Canvas>"));
        Assert.Equal(3, reset.Split("<Path", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("<TextBlock", reset);
        Assert.Contains("Width=\"20\"", ExtractElement(close, "<Canvas", "</Canvas>"));
        Assert.Contains("<Path", close);
        Assert.DoesNotContain("<TextBlock", close);
        Assert.Contains("Value=\"ExactBase\"", criterionStyle);
        Assert.Contains("ExactBaseActiveBorderBrush", criterionStyle);
        Assert.DoesNotContain("LeagueTextBox", xaml);
        Assert.DoesNotContain("x:Name=\"BaseTypeText\"", xaml);
    }

    [Fact]
    public void WindowXaml_PreservesModifierSelectionActionsAndInactivePlaceholders()
    {
        var xaml = LoadWindowXaml();
        var modifiers = ExtractElement(xaml, "<ListBox x:Name=\"ModifierListBox\"", "</ListBox>");
        var advanced = ExtractElement(xaml, "<ToggleButton Grid.Column=\"2\"", "/>");

        Assert.Contains("Click=\"OnModifierSelectionClick\"", modifiers);
        Assert.Contains("OnModifierRowPreviewMouseLeftButtonDown", xaml);
        Assert.DoesNotContain("ModifierTextButtonStyle", xaml);
        Assert.Contains("Text=\"Min\"", xaml);
        Assert.Contains("Text=\"Max\"", xaml);
        Assert.Contains("IsEnabled=\"False\"", advanced);
        Assert.Contains("x:Name=\"SearchButton\"", xaml);
        Assert.Contains("x:Name=\"LoadMoreButton\"", xaml);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Disabled\"", modifiers);
    }

    private static string LoadWindowXaml()
    {
        return File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerWindow.xaml"));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private static string ExtractElement(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find {startMarker}.");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end >= 0, $"Could not find {endMarker}.");
        return source[start..(end + endMarker.Length)];
    }
}
