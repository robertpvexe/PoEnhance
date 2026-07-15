using PoEnhance.App.Features.PriceChecking;
using System.Runtime.ExceptionServices;
using System.Windows.Controls;
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

    [Theory]
    [InlineData("Normal", "PriceCheckerTitleNormalForegroundBrush")]
    [InlineData("Magic", "PriceCheckerTitleMagicForegroundBrush")]
    [InlineData("Rare", "PriceCheckerTitleRareForegroundBrush")]
    [InlineData("Unique", "PriceCheckerTitleUniqueForegroundBrush")]
    public void TitleForeground_UsesTheParsedRarityBrush(string rarity, string expectedResourceKey)
    {
        Assert.Equal(expectedResourceKey, PriceCheckerWindow.TitleForegroundResourceKey(rarity));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unsupported")]
    public void TitleForeground_UnknownRarityFallsBackToNormal(string? rarity)
    {
        Assert.Equal(
            "PriceCheckerTitleNormalForegroundBrush",
            PriceCheckerWindow.TitleForegroundResourceKey(rarity));
    }

    [Fact]
    public void TitleForeground_ChangesWhenTheCurrentItemRarityChanges()
    {
        var firstItemBrush = PriceCheckerWindow.TitleForegroundResourceKey("Magic");
        var replacementItemBrush = PriceCheckerWindow.TitleForegroundResourceKey("Unique");

        Assert.Equal("PriceCheckerTitleMagicForegroundBrush", firstItemBrush);
        Assert.Equal("PriceCheckerTitleUniqueForegroundBrush", replacementItemBrush);
        Assert.NotEqual(firstItemBrush, replacementItemBrush);
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
        Assert.DoesNotContain("Foreground=", title);
        Assert.Contains("Color=\"White\"", ExtractElement(
            xaml,
            "<SolidColorBrush x:Key=\"PriceCheckerTitleNormalForegroundBrush\"",
            "/>"));
        Assert.Contains("Color=\"#8888FF\"", ExtractElement(
            xaml,
            "<SolidColorBrush x:Key=\"PriceCheckerTitleMagicForegroundBrush\"",
            "/>"));
        Assert.Contains("Color=\"#F3D88B\"", ExtractElement(
            xaml,
            "<SolidColorBrush x:Key=\"PriceCheckerTitleRareForegroundBrush\"",
            "/>"));
        Assert.Contains("Color=\"#AF6025\"", ExtractElement(
            xaml,
            "<SolidColorBrush x:Key=\"PriceCheckerTitleUniqueForegroundBrush\"",
            "/>"));
        Assert.Contains(
            "TitleForegroundResourceKey(draft.Rarity)",
            LoadWindowCodeBehind());
        Assert.Contains("x:Name=\"PinToggleButton\"", xaml);
        Assert.Contains("Width=\"20\"", ExtractElement(reset, "<Canvas", "</Canvas>"));
        Assert.Equal(3, reset.Split("<Path x:Name=", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("<TextBlock", reset);
        var resetLetter = ExtractElement(reset, "<Path x:Name=\"ResetLetterGeometry\"", "</Path>");
        Assert.Contains("<ScaleTransform", resetLetter);
        Assert.Contains("ScaleX=\"0.8\"", resetLetter);
        Assert.Contains("ScaleY=\"0.8\"", resetLetter);
        Assert.Contains("CenterX=\"10\"", resetLetter);
        Assert.Contains("CenterY=\"10\"", resetLetter);
        Assert.DoesNotContain("Margin=", resetLetter);
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
        Assert.Contains("Text=\"Mod Type\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding CanEditBounds}\"", modifiers);
        Assert.Contains("Text=\"{Binding MinimumText, UpdateSourceTrigger=PropertyChanged}\"", modifiers);
        Assert.Contains("Text=\"{Binding MaximumText, UpdateSourceTrigger=PropertyChanged}\"", modifiers);
        Assert.Contains("TextChanged=\"OnModifierBoundTextChanged\"", modifiers);
        Assert.Contains("ReferenceEquals(ModifierListBox.ItemsSource, state.Modifiers)", LoadWindowCodeBehind());
        Assert.Contains("IsEnabled=\"False\"", advanced);
        Assert.Contains("x:Name=\"SearchButton\"", xaml);
        Assert.Contains("x:Name=\"LoadMoreButton\"", xaml);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Disabled\"", modifiers);
    }

    [Fact]
    public void WindowXaml_UsesCompactInlineSearchStatusAndContainsNoDeveloperDiagnostics()
    {
        var xaml = LoadWindowXaml();
        var actionRow = ExtractElement(xaml, "<Grid Grid.Row=\"4\"", "</Grid>");
        var results = ExtractElement(xaml, "<Grid x:Name=\"ResultsPanel\"", "</Grid>");

        Assert.Contains("x:Name=\"SearchStatusText\"", actionRow);
        Assert.Contains("x:Name=\"SearchSummaryText\"", actionRow);
        Assert.DoesNotContain("ValidationPanel", xaml);
        Assert.DoesNotContain("ValidationTextBox", xaml);
        Assert.DoesNotContain("DebugPanel", xaml);
        Assert.DoesNotContain("DebugStateText", xaml);
        Assert.DoesNotContain("Validation", xaml);
        Assert.Contains("Grid.Row=\"5\"", results);
    }

    [Fact]
    public void WindowXaml_ModifierBoundColumnsAreWideAlignedAndFitInsideTheRow()
    {
        var xaml = LoadWindowXaml();
        var header = ExtractElement(xaml, "<Grid Grid.Row=\"2\"", "</Grid>");
        var modifiers = ExtractElement(xaml, "<ListBox x:Name=\"ModifierListBox\"", "</ListBox>");
        var row = ExtractElement(modifiers, "<Grid Margin=\"6,4,6,4\"", "</Grid>");
        var minimum = ExtractElement(modifiers, "<TextBox Grid.Column=\"3\"", "/>");
        var maximum = ExtractElement(modifiers, "<TextBox Grid.Column=\"4\"", "/>");

        Assert.Equal(2, header.Split("<ColumnDefinition Width=\"78\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, header.Split("<ColumnDefinition Width=\"92\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("Margin=\"0,10,6,4\"", header);
        Assert.Equal(2, row.Split("<ColumnDefinition Width=\"78\"", StringSplitOptions.None).Length - 1);
        Assert.Equal(1, row.Split("<ColumnDefinition Width=\"92\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("<ColumnDefinition Width=\"*\"", header);
        Assert.Contains("<ColumnDefinition Width=\"*\"", row);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", row);
        Assert.Contains("Grid.Column=\"2\"", row);
        Assert.Contains("Grid.Column=\"3\"", modifiers);
        Assert.Contains("Grid.Column=\"4\"", modifiers);
        Assert.Contains("Margin=\"3,0\"", minimum);
        Assert.Contains("Margin=\"3,0\"", maximum);
        Assert.DoesNotContain("MinWidth=\"64\"", row);
    }

    [Fact]
    public void WindowXaml_UsesCompactFilterSelectorAndReadOnlySingleOptionLabel()
    {
        var xaml = LoadWindowXaml();
        var modifiers = ExtractElement(xaml, "<ListBox x:Name=\"ModifierListBox\"", "</ListBox>");
        var style = ExtractElement(xaml, "<Style x:Key=\"ModifierFilterComboBoxStyle\"", "</Style>");

        Assert.Contains("Height", style);
        Assert.Contains("Value=\"24\"", style);
        Assert.Contains("HasMultipleFilterVariants", style);
        Assert.Contains("IsEnabled=\"{Binding CanSelectFilterVariant}\"", modifiers);
        Assert.Contains("ItemsSource=\"{Binding FilterVariants}\"", modifiers);
        Assert.Contains("SelectedItem=\"{Binding SelectedFilterVariant, Mode=OneWay}\"", modifiers);
        Assert.Contains("DropDownClosed=\"OnModifierFilterVariantDropDownClosed\"", modifiers);
        Assert.DoesNotContain("SelectionChanged=", modifiers);
        Assert.Contains("HasSingleFilterVariant", modifiers);
        Assert.Contains("Text=\"{Binding SelectedFilterVariant.Label}\"", modifiers);
        Assert.Contains("ToolTip=\"{Binding SelectedFilterVariant.Description}\"", modifiers);
        Assert.Contains("Text=\"{Binding Label}\"", modifiers);
        Assert.Contains("ToolTip=\"{Binding Description}\"", modifiers);
        Assert.DoesNotContain("Text=\"{Binding Description}\"", modifiers);
        Assert.DoesNotContain("StatId", modifiers, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProviderStat", modifiers, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WindowXaml_ModTypeSelectorUsesDarkTemplatePopupOptionsAndScrollbar()
    {
        var xaml = LoadWindowXaml();
        var selector = ExtractElement(xaml, "<Style x:Key=\"ModifierFilterComboBoxStyle\"", "</Style>");
        var option = ExtractElement(xaml, "<Style x:Key=\"ModifierFilterComboBoxItemStyle\"", "</Style>");
        var scrollbar = ExtractElement(xaml, "<Style x:Key=\"ModifierFilterScrollBarStyle\"", "</Style>");

        Assert.Contains("Background", selector);
        Assert.Contains("#191919", selector);
        Assert.Contains("BorderBrush", selector);
        Assert.Contains("CornerRadius=\"3\"", selector);
        Assert.Contains("PART_Popup", selector);
        Assert.Contains("PopupAnimation=\"Fade\"", selector);
        Assert.Contains("ModifierFilterComboBoxItemStyle", selector);
        Assert.Contains("ModifierFilterScrollBarStyle", selector);
        Assert.Contains("#303A31", option);
        Assert.Contains("#3B503D", option);
        Assert.Contains("IsHighlighted", option);
        Assert.Contains("IsSelected", option);
        Assert.Contains("#151515", scrollbar);
        Assert.Contains("#505050", scrollbar);
        Assert.DoesNotContain("White", selector, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModifierRowClickDecision_RejectsRealWpfInteractiveDescendants()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var row = new ListBoxItem();
                var surface = new Grid();
                row.Content = surface;

                var comboBox = new ComboBox();
                var comboOptionContent = new TextBlock { Text = "Implicit" };
                comboBox.Items.Add(new ComboBoxItem { Content = comboOptionContent });
                surface.Children.Add(comboBox);

                var textBox = new TextBox();
                surface.Children.Add(textBox);

                var buttonContent = new Border();
                surface.Children.Add(new Button { Content = buttonContent });

                var plainSurface = new TextBlock { Text = "Modifier text" };
                surface.Children.Add(plainSurface);

                Assert.False(PriceCheckerWindow.ShouldToggleModifierRowFrom(comboBox));
                Assert.False(PriceCheckerWindow.ShouldToggleModifierRowFrom(comboOptionContent));
                Assert.False(PriceCheckerWindow.ShouldToggleModifierRowFrom(textBox));
                Assert.False(PriceCheckerWindow.ShouldToggleModifierRowFrom(buttonContent));
                Assert.True(PriceCheckerWindow.ShouldToggleModifierRowFrom(plainSurface));
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(5)));
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    [Fact]
    public void ModifierFilterVariantState_OnlyEnablesDropdownForSelectedRowsWithMultipleOptions()
    {
        var option = new PriceCheckerModifierFilterVariantViewModel
        {
            Identity = "variant-opaque",
            Label = "Explicit",
            Description = "#% increased Attack Speed (Local)",
            SupportsValueBounds = true,
        };
        var single = new PriceCheckerModifierViewModel
        {
            SourceIndex = 0,
            Text = "20% increased Attack Speed",
            FilterVariants = [option],
            SelectedFilterVariant = option,
            IsSelected = true,
        };
        var unselectedMultiple = single with
        {
            IsSelected = false,
            FilterVariants = [option, option with { Identity = "variant-other", Label = "Crafted" }],
        };
        var selectedMultiple = unselectedMultiple with { IsSelected = true };

        Assert.True(single.HasSingleFilterVariant);
        Assert.False(single.HasMultipleFilterVariants);
        Assert.False(single.CanSelectFilterVariant);
        Assert.True(unselectedMultiple.HasMultipleFilterVariants);
        Assert.False(unselectedMultiple.CanSelectFilterVariant);
        Assert.True(selectedMultiple.CanSelectFilterVariant);
    }

    [Fact]
    public void WindowXaml_AddsTheCompactTradeButtonBesideTheDisabledAdvancedPlaceholder()
    {
        var xaml = LoadWindowXaml();
        var actionRow = ExtractElement(xaml, "<Grid Grid.Row=\"4\"", "</Grid>");
        var trade = ExtractElement(xaml, "<Button x:Name=\"TradeButton\"", "</Button>");
        var advanced = ExtractElement(actionRow, "<ToggleButton Grid.Column=\"2\"", "/>");

        Assert.Contains("<ToggleButton Grid.Column=\"2\"", actionRow);
        Assert.Contains("Grid.Column=\"3\"", trade);
        Assert.Contains("Width=\"22\"", trade);
        Assert.Contains("Height=\"22\"", trade);
        Assert.Contains("HorizontalAlignment=\"Left\"", trade);
        Assert.Contains("IsEnabled=\"False\"", trade);
        Assert.Contains("ToolTip=\"Open on Trade\"", trade);
        Assert.Contains("TitleBarButtonStyle", trade);
        Assert.Contains("<Canvas", trade);
        Assert.Contains("<Path", trade);
        Assert.DoesNotContain("TextBlock", trade);
        Assert.Contains("Margin=\"6,0,8,0\"", advanced);
    }

    [Fact]
    public void WindowXaml_StylesModifierBoundsForTheDarkTheme()
    {
        var xaml = LoadWindowXaml();
        var style = ExtractElement(xaml, "<Style x:Key=\"ModifierBoundTextBoxStyle\"", "</Style>");
        var modifiers = ExtractElement(xaml, "<ListBox x:Name=\"ModifierListBox\"", "</ListBox>");

        Assert.Contains("TextAlignment", style);
        Assert.Contains("VerticalContentAlignment", style);
        Assert.Contains("Value=\"13\"", style);
        Assert.Contains("Value=\"2,1\"", style);
        Assert.Contains("Value=\"0.75\"", style);
        Assert.Contains("Value=\"24\"", style);
        Assert.Contains("CornerRadius=\"3\"", style);
        Assert.Contains("Background", style);
        Assert.Contains("Style=\"{StaticResource ModifierBoundTextBoxStyle}\"", modifiers);
    }

    [Fact]
    public void WindowXaml_UsesAnInvisibleFourColumnOfferLayoutWithoutGridLines()
    {
        var xaml = LoadWindowXaml();
        var header = ExtractElement(xaml, "<Grid x:Name=\"OfferColumnHeader\"", "</Grid>");
        var offers = ExtractElement(xaml, "<ListBox x:Name=\"OfferListBox\"", "</ListBox>");
        var rowStyle = ExtractElement(xaml, "<Style x:Key=\"OfferRowStyle\"", "</Style>");

        Assert.Contains("Text=\"Item / Seller\"", header);
        Assert.Contains("Text=\"Listed\"", header);
        Assert.Contains("Text=\"iLvl\"", header);
        Assert.Contains("Text=\"Price\"", header);
        Assert.Equal(4, header.Split("<ColumnDefinition", StringSplitOptions.None).Length - 1);
        Assert.Equal(4, offers.Split("<ColumnDefinition", StringSplitOptions.None).Length - 1);
        Assert.Contains("Height=\"36\"", offers);
        Assert.Contains("<StackPanel VerticalAlignment=\"Center\"", offers);
        Assert.Contains("ItemName", offers);
        Assert.Contains("SellerAccountName", offers);
        Assert.Contains("FontWeight=\"SemiBold\"", offers);
        Assert.Contains("FontSize=\"10\"", offers);
        Assert.Contains("Margin=\"0\"", offers);
        Assert.Contains("ListedText", offers);
        Assert.Contains("ItemLevelText", offers);
        Assert.Contains("PriceText", offers);
        Assert.DoesNotContain("DataGrid", offers);
        Assert.DoesNotContain("Separator", offers);
        Assert.DoesNotContain("BorderBrush", offers);
        Assert.DoesNotContain("BorderBrush", rowStyle);
        Assert.Contains("FocusVisualStyle", rowStyle);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Disabled\"", offers);
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

    private static string LoadWindowCodeBehind()
    {
        return File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerWindow.xaml.cs"));
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
