using PoEnhance.App.Features.PriceChecking;
using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using PoEnhance.Core.Items.Parsing;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerWindowPresentationTests
{
    [Theory]
    [InlineData(BaseSearchMode.Category, "One-Handed Axe", null, null, "Category: —")]
    [InlineData(BaseSearchMode.Category, "Wand", null, "Wand", "Category: Wand")]
    [InlineData(BaseSearchMode.Category, "One Hand Axes", null, "One-Handed Axe", "Category: One-Handed Axe")]
    [InlineData(BaseSearchMode.Category, "Belt", null, "Belt", "Category: Belt")]
    [InlineData(BaseSearchMode.ExactBase, null, "Stygian Vise", "One-Handed Axe", "Base: Stygian Vise")]
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
    [InlineData("Any", "PriceCheckerTitleAnyForegroundBrush")]
    public void TitleForeground_UsesTheParsedRarityBrush(string rarity, string expectedResourceKey)
    {
        Assert.Equal(expectedResourceKey, PriceCheckerWindow.TitleForegroundResourceKey(rarity));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unsupported")]
    public void TitleForeground_UnknownRarityFallsBackToNeutral(string? rarity)
    {
        Assert.Equal(
            "PriceCheckerTitleAnyForegroundBrush",
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

    [Theory]
    [InlineData(TradeTriState.No, TradeTriState.Yes)]
    [InlineData(TradeTriState.Yes, TradeTriState.Any)]
    [InlineData(TradeTriState.Any, TradeTriState.No)]
    public void ItemStateCycleUsesNoYesAnyOrder(TradeTriState current, TradeTriState expected)
    {
        Assert.Equal(expected, PriceCheckerWindow.CycleItemState(current));
    }

    [Fact]
    public void WindowXaml_PresentsRarityAndThreeStableClickableItemStateControlsInOneHeaderRow()
    {
        var xaml = LoadWindowXaml();
        var categoryBar = ExtractElement(
            xaml,
            "<Border Grid.Row=\"0\"",
            "<StackPanel x:Name=\"HeaderRequestedFiltersPanel\"");
        var stateStyle = ExtractElement(xaml, "<Style x:Key=\"ItemStateButtonStyle\"", "</Style>");
        var baseCriterion = ExtractElement(categoryBar, "<Button x:Name=\"BaseCriterionButton\"", "</Button>");
        var baseCriterionText = ExtractElement(baseCriterion, "<TextBlock x:Name=\"BaseCriterionText\"", "/>" );
        var mirrored = ExtractElement(categoryBar, "<Button x:Name=\"MirroredStateButton\"", "/>" );
        var corrupted = ExtractElement(categoryBar, "<Button x:Name=\"CorruptedStateButton\"", "/>" );
        var identified = ExtractElement(categoryBar, "<Button x:Name=\"IdentifiedStateButton\"", "/>" );
        var rarity = ExtractElement(categoryBar, "<ComboBox x:Name=\"RarityComboBox\"", "</ComboBox>");

        Assert.True(categoryBar.IndexOf("BaseCriterionButton", StringComparison.Ordinal) <
            categoryBar.IndexOf("RarityComboBox", StringComparison.Ordinal));
        Assert.True(categoryBar.IndexOf("RarityComboBox", StringComparison.Ordinal) <
            categoryBar.IndexOf("MirroredStateButton", StringComparison.Ordinal));
        Assert.True(categoryBar.IndexOf("MirroredStateButton", StringComparison.Ordinal) <
            categoryBar.IndexOf("CorruptedStateButton", StringComparison.Ordinal));
        Assert.True(categoryBar.IndexOf("CorruptedStateButton", StringComparison.Ordinal) <
            categoryBar.IndexOf("IdentifiedStateButton", StringComparison.Ordinal));
        Assert.Contains("Grid.Column=\"4\"", identified);
        Assert.DoesNotContain("WrapPanel", categoryBar);
        Assert.Contains("MinWidth=\"72\"", baseCriterion);
        Assert.Contains("HorizontalAlignment=\"Stretch\"", baseCriterion);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", baseCriterionText);
        Assert.Contains("TextWrapping=\"NoWrap\"", baseCriterionText);
        Assert.Contains("ComboBoxItem Content=\"Any\"", rarity);
        Assert.Contains("ComboBoxItem Content=\"Normal\"", rarity);
        Assert.Contains("ComboBoxItem Content=\"Magic\"", rarity);
        Assert.Contains("ComboBoxItem Content=\"Rare\"", rarity);
        Assert.Contains("BasedOn=\"{StaticResource PriceCheckerButtonStyle}\"", stateStyle);
        Assert.Contains("Click=\"OnItemStateButtonClick\"", mirrored);
        Assert.Contains("Click=\"OnItemStateButtonClick\"", corrupted);
        Assert.Contains("Click=\"OnItemStateButtonClick\"", identified);
        Assert.Contains("Content=\"Mirrored: No\"", mirrored);
        Assert.Contains("Content=\"Corrupted: No\"", corrupted);
        Assert.Contains("Content=\"Identified: Yes\"", identified);
        Assert.Contains("ToolTip=\"Click to cycle: No → Yes → Any\"", mirrored);
        Assert.Contains("Width=\"78\"", mirrored);
        Assert.Contains("Width=\"82\"", corrupted);
        Assert.Contains("Width=\"82\"", identified);
        Assert.DoesNotContain("Path", categoryBar);
    }

    [Fact]
    public void ItemStateControls_RuntimeStayVisibleStableAndClickableAtMinimumWidth()
    {
        RunOnSta(() =>
        {
            var draft = new TradeSearchDraft
            {
                ItemClass = "Rings",
                Rarity = "Rare",
                DisplayName = "State Band",
                ParsedBaseType = "Iron Ring",
                ItemStateCriteria = new TradeItemStateCriteria
                {
                    Mirrored = TradeTriState.No,
                    Corrupted = TradeTriState.No,
                    Identified = TradeTriState.Yes,
                },
            };
            var validation = TradeSearchValidationResult.FromDiagnostics([]);
            var window = new PriceCheckerWindow
            {
                Width = PriceCheckerPlacementCalculator.UserPanelMinimumWidth,
                Height = 720,
            };
            PriceCheckerItemStateChangedEventArgs? changed = null;
            window.ItemStateChanged += (_, e) => changed = e;
            window.UpdateContent(new PriceCheckerWindowState(draft, validation)
            {
                Presentation = new PriceCheckerItemPresentation { IsRarityEditable = true },
            });
            window.Show();
            window.UpdateLayout();

            var mirrored = Assert.IsType<Button>(window.FindName("MirroredStateButton"));
            var corrupted = Assert.IsType<Button>(window.FindName("CorruptedStateButton"));
            var identified = Assert.IsType<Button>(window.FindName("IdentifiedStateButton"));
            var buttons = new[] { mirrored, corrupted, identified };
            var widths = buttons.Select(button => button.ActualWidth).ToArray();
            var tops = buttons.Select(button => button.TranslatePoint(new Point(0, 0), window).Y).ToArray();
            Assert.All(buttons, button => Assert.Equal(Visibility.Visible, button.Visibility));
            Assert.Equal([78d, 82d, 82d], widths);
            Assert.All(tops, top => Assert.Equal(tops[0], top, 3));
            Assert.All(buttons, button => Assert.True(
                button.TranslatePoint(new Point(button.ActualWidth, 0), window).X <= window.ActualWidth));

            mirrored.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, mirrored));
            Assert.NotNull(changed);
            Assert.Equal(TradeItemStateKind.Mirrored, changed!.Kind);
            Assert.Equal(TradeTriState.Yes, changed.State);

            window.UpdateContent(new PriceCheckerWindowState(
                draft with
                {
                    ItemStateCriteria = new TradeItemStateCriteria
                    {
                        Mirrored = TradeTriState.Any,
                        Corrupted = TradeTriState.Any,
                        Identified = TradeTriState.Any,
                    },
                },
                validation)
            {
                Presentation = new PriceCheckerItemPresentation { IsRarityEditable = true },
            });
            window.UpdateLayout();
            Assert.Equal(widths, buttons.Select(button => button.ActualWidth));
            Assert.Equal(["Mirrored: Any", "Corrupted: Any", "Identified: Any"],
                buttons.Select(button => button.Content?.ToString()));
            Assert.Equal(Visibility.Collapsed,
                Assert.IsType<ToggleButton>(window.FindName("AdvancedToggle")).Visibility);
            Assert.Equal(Visibility.Visible,
                Assert.IsType<Button>(window.FindName("TradeButton")).Visibility);
            window.Close();
        });
    }

    [Fact]
    public void RarityControl_RuntimeEditsOrdinaryItemsAndKeepsUniqueItemsStatic()
    {
        RunOnSta(() =>
        {
            var validation = TradeSearchValidationResult.FromDiagnostics([]);
            var ordinaryDraft = new TradeSearchDraft
            {
                Rarity = "Rare",
                DisplayName = "State Band",
                ParsedBaseType = "Iron Ring",
            };
            var window = new PriceCheckerWindow
            {
                Width = PriceCheckerPlacementCalculator.UserPanelMinimumWidth,
                Height = 720,
            };
            PriceCheckerRarityChangedEventArgs? changed = null;
            window.RarityChanged += (_, e) => changed = e;
            window.UpdateContent(new PriceCheckerWindowState(ordinaryDraft, validation)
            {
                Presentation = new PriceCheckerItemPresentation { IsRarityEditable = true },
            });
            window.Show();
            window.UpdateLayout();

            var selector = Assert.IsType<ComboBox>(window.FindName("RarityComboBox"));
            var staticField = Assert.IsType<Border>(window.FindName("RarityStaticBorder"));
            Assert.Equal(Visibility.Visible, selector.Visibility);
            Assert.Equal(Visibility.Collapsed, staticField.Visibility);
            Assert.Equal("Rare", Assert.IsType<ComboBoxItem>(selector.SelectedItem).Content);

            selector.SelectedIndex = 0;

            Assert.Equal("Any", changed?.Rarity);
            window.UpdateContent(new PriceCheckerWindowState(
                ordinaryDraft with { Rarity = "Any" },
                validation)
            {
                Presentation = new PriceCheckerItemPresentation { IsRarityEditable = true },
            });
            var title = Assert.IsType<TextBlock>(window.FindName("TitleDisplayNameText"));
            Assert.Same(window.FindResource("PriceCheckerTitleAnyForegroundBrush"), title.Foreground);

            changed = null;
            window.UpdateContent(new PriceCheckerWindowState(
                ordinaryDraft with
                {
                    Rarity = "Unique",
                    DisplayName = "Foulborn Moonbender's Wing",
                    ParsedBaseType = "Tomahawk",
                },
                validation));
            window.UpdateLayout();

            Assert.Equal(Visibility.Collapsed, selector.Visibility);
            Assert.Equal(Visibility.Visible, staticField.Visibility);
            Assert.Equal("Unique", Assert.IsType<TextBlock>(window.FindName("RarityStaticText")).Text);
            selector.SelectedIndex = 2;
            Assert.Null(changed);
            Assert.Same(window.FindResource("PriceCheckerTitleUniqueForegroundBrush"), title.Foreground);
            window.Close();
        });
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
        Assert.True(presentation.IsRarityEditable);
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
        Assert.True(presentation.IsRarityEditable);
    }

    [Fact]
    public void ItemPresentation_KeepsUniqueAndFoulbornUniqueRarityStatic()
    {
        var parsedItem = new ItemTextParser().Parse("""
Item Class: One Hand Axes
Rarity: Unique
Foulborn Moonbender's Wing
Tomahawk
--------
Item Level: 82
""");

        Assert.False(PriceCheckerItemPresentation.FromParsedItem(parsedItem).IsRarityEditable);
    }

    [Fact]
    public void StatsSelectedCount_UsesUnifiedCanonicalEntryCounts()
    {
        Assert.Equal("Stats 2 of 8 selected", PriceCheckerWindow.FormatStatsCount(2, 8));
    }

    [Fact]
    public void WindowXaml_PreservesAcceptedTitleBarAndCriterionPresentation()
    {
        var xaml = LoadWindowXaml();
        var title = ExtractElement(xaml, "<TextBlock x:Name=\"TitleDisplayNameText\"", "/>");
        var reset = ExtractElement(xaml, "<Button x:Name=\"ResetItemButton\"", "</Button>");
        var close = ExtractElement(xaml, "<Button x:Name=\"CloseButton\"", "</Button>");
        var criterionStyle = ExtractElement(xaml, "<Style x:Key=\"BaseCriterionButtonStyle\"", "</Style>");

        Assert.Contains("Grid.Column=\"1\"", title);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", title);
        Assert.DoesNotContain("Foreground=", title);
        Assert.Contains("Color=\"White\"", ExtractElement(
            xaml,
            "<SolidColorBrush x:Key=\"PriceCheckerTitleNormalForegroundBrush\"",
            "/>"));
        Assert.Contains("Color=\"#EEEEEE\"", ExtractElement(
            xaml,
            "<SolidColorBrush x:Key=\"PriceCheckerTitleAnyForegroundBrush\"",
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
        Assert.Contains("ToolTip=\"Reset item\"", reset);
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
        var modifiers = ExtractElement(xaml, "<ListBox x:Name=\"StatsListBox\"", "</ListBox>");
        var advanced = ExtractElement(xaml, "<ToggleButton Grid.Column=\"2\"", "/>");

        Assert.Contains("Click=\"OnModifierSelectionClick\"", modifiers);
        Assert.Contains("OnStatsRowPreviewMouseLeftButtonDown", xaml);
        Assert.DoesNotContain("ModifierTextButtonStyle", xaml);
        Assert.Contains("Text=\"Min\"", xaml);
        Assert.Contains("Text=\"Max\"", xaml);
        Assert.Contains("Text=\"Mod Type\"", xaml);
        Assert.Contains("IsEnabled=\"{Binding CanEditBounds}\"", modifiers);
        Assert.Contains("Text=\"{Binding MinimumText, UpdateSourceTrigger=PropertyChanged}\"", modifiers);
        Assert.Contains("Text=\"{Binding MaximumText, UpdateSourceTrigger=PropertyChanged}\"", modifiers);
        Assert.Contains("TextChanged=\"OnModifierBoundTextChanged\"", modifiers);
        Assert.Contains("!textBox.IsKeyboardFocusWithin", LoadWindowCodeBehind());
        Assert.Contains("ToolTip=\"{Binding SourceBreakdown}\"", modifiers);
        Assert.Contains("statsRowsAreUnchanged", LoadWindowCodeBehind());
        Assert.Contains("ReferenceEquals(CurrentSearchState.ItemProperties, state.ItemProperties)", LoadWindowCodeBehind());
        Assert.Contains("ReferenceEquals(CurrentSearchState.Modifiers, state.Modifiers)", LoadWindowCodeBehind());
        Assert.Contains("IsEnabled=\"False\"", advanced);
        Assert.Contains("x:Name=\"SearchButton\"", xaml);
        Assert.Contains("x:Name=\"LoadMoreButton\"", xaml);
        Assert.Contains("ScrollViewer.VerticalScrollBarVisibility=\"Disabled\"", modifiers);
    }

    [Fact]
    public void WindowXaml_UsesExplicitSharedColumnsForEachStatsHierarchyLevel()
    {
        var xaml = LoadWindowXaml();
        var modifiers = ExtractElement(xaml, "<ListBox x:Name=\"StatsListBox\"", "</ListBox>");
        var property = ExtractElement(modifiers, "<Grid x:Name=\"StatsTopLevelPropertyRow\"", "</Grid>");
        var modifier = ExtractElement(modifiers, "<Grid x:Name=\"StatsTopLevelModifierRow\"", "</Grid>");
        var propertyChild = ExtractElement(modifiers, "<Grid x:Name=\"StatsPropertyChildRow\"", "</Grid>");
        var propertyContributor = ExtractElement(
            modifiers,
            "<Grid x:Name=\"StatsPropertyContributorRow\"",
            "</Grid>");
        var standaloneContributor = ExtractElement(
            modifiers,
            "<Grid x:Name=\"StatsStandaloneContributorRow\"",
            "</Grid>");

        Assert.Contains("Grid.IsSharedSizeScope=\"True\"", xaml);
        AssertTopLevelHierarchyColumns(property, "HasChildren");
        AssertTopLevelHierarchyColumns(modifier, "ShowsExpansionControl");

        AssertColumnGroups(
            propertyChild,
            "StatsExpansionSlotColumn",
            "StatsChildIndentColumn",
            "StatsCheckboxColumn",
            "StatsModTypeColumn",
            "StatsMinimumColumn",
            "StatsMaximumColumn");
        Assert.Contains("<CheckBox Grid.Column=\"2\"", propertyChild);
        Assert.DoesNotContain("<Button", propertyChild);
        Assert.DoesNotContain("OnModifierExpansionClick", propertyChild);

        AssertColumnGroups(
            propertyContributor,
            "StatsExpansionSlotColumn",
            "StatsChildIndentColumn",
            "StatsContributorIndentColumn",
            "StatsCheckboxColumn",
            "StatsModTypeColumn",
            "StatsMinimumColumn",
            "StatsMaximumColumn");
        Assert.Contains("<CheckBox Grid.Column=\"3\"", propertyContributor);
        Assert.Contains("<TextBox Grid.Column=\"6\"", propertyContributor);
        Assert.Contains("<TextBox Grid.Column=\"7\"", propertyContributor);

        AssertColumnGroups(
            standaloneContributor,
            "StatsExpansionSlotColumn",
            "StatsContributorIndentColumn",
            "StatsCheckboxColumn",
            "StatsModTypeColumn",
            "StatsMinimumColumn",
            "StatsMaximumColumn");
        Assert.Contains("<CheckBox Grid.Column=\"2\"", standaloneContributor);
        Assert.Contains("Grid.ColumnSpan=\"4\"", standaloneContributor);
        Assert.DoesNotContain("OnModifierExpansionClick", standaloneContributor);
    }

    [Fact]
    public void WindowXaml_UsesCompactInlineSearchStatusAndContainsNoDeveloperDiagnostics()
    {
        var xaml = LoadWindowXaml();
        var actionRow = ExtractElement(xaml, "<Grid Grid.Row=\"6\"", "</Grid>");
        var results = ExtractElement(xaml, "<Grid x:Name=\"ResultsPanel\"", "</Grid>");

        Assert.Contains("x:Name=\"SearchStatusText\"", actionRow);
        Assert.Contains("x:Name=\"SearchSummaryText\"", actionRow);
        Assert.DoesNotContain("ValidationPanel", xaml);
        Assert.DoesNotContain("ValidationTextBox", xaml);
        Assert.DoesNotContain("DebugPanel", xaml);
        Assert.DoesNotContain("DebugStateText", xaml);
        Assert.DoesNotContain("Validation", xaml);
        Assert.Contains("Grid.Row=\"7\"", results);
    }

    [Fact]
    public void WindowXaml_ModifierBoundColumnsAreWideAlignedAndFitInsideTheRow()
    {
        var xaml = LoadWindowXaml();
        var header = ExtractElement(xaml, "<Grid x:Name=\"StatsHeader\"", "</Grid>");
        var modifiers = ExtractElement(xaml, "<ListBox x:Name=\"StatsListBox\"", "</ListBox>");
        var row = ExtractElement(modifiers, "<Grid x:Name=\"StatsTopLevelPropertyRow\"", "</Grid>");
        var minimum = ExtractElement(modifiers, "<TextBox Grid.Column=\"4\"", "/>");
        var maximum = ExtractElement(modifiers, "<TextBox Grid.Column=\"5\"", "/>");

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
    public void WindowXaml_PresentsOneUnifiedStatsSectionWithPropertiesBeforeUngroupedModifiers()
    {
        var xaml = LoadWindowXaml();
        var propertiesHeader = ExtractElement(xaml, "<Grid x:Name=\"StatsHeader\"", "</Grid>");
        var properties = ExtractElement(xaml, "<ListBox x:Name=\"StatsListBox\"", "</ListBox>");
        var propertyRow = ExtractElement(properties, "<Grid x:Name=\"StatsTopLevelPropertyRow\"", "</Grid>");
        var groupedRow = ExtractElement(properties, "<Grid x:Name=\"StatsPropertyChildRow\"", "</Grid>");
        var groupedContributors = ExtractElement(
            properties,
            "<Grid x:Name=\"StatsPropertyContributorRow\"",
            "</Grid>");

        Assert.Contains("x:Name=\"StatsCountText\"", propertiesHeader);
        Assert.Contains("FormatStatsCount(state.SelectedStatsCount, state.StatsCount)", LoadWindowCodeBehind());
        Assert.DoesNotContain("Text=\"Item Properties\"", xaml);
        Assert.DoesNotContain("Text=\"Modifiers\"", xaml);
        Assert.DoesNotContain("ItemPropertyListBox", xaml);
        Assert.DoesNotContain("ModifierListBox", xaml);
        Assert.Contains("Grid.Row=\"2\"", propertiesHeader);
        Assert.Contains("Grid.Row=\"3\"", properties);
        Assert.Equal(2, propertiesHeader.Split("<ColumnDefinition Width=\"78\"", StringSplitOptions.None).Length - 1);
        Assert.Contains("<ColumnDefinition Width=\"92\"", propertiesHeader);
        Assert.Contains("Click=\"OnItemPropertySelectionClick\"", propertyRow);
        Assert.Contains("Click=\"OnItemPropertyExpansionClick\"", propertyRow);
        Assert.Contains("TextChanged=\"OnItemPropertyBoundTextChanged\"", properties);
        Assert.Contains("IsEnabled=\"{Binding IsAvailable}\"", propertyRow);
        Assert.Contains("IsEnabled=\"{Binding CanEditBounds}\"", propertyRow);
        Assert.Contains("Tag=\"GroupedModifierRow\"", groupedRow);
        Assert.Contains("PreviewMouseLeftButtonDown=\"OnStatsRowPreviewMouseLeftButtonDown\"", groupedRow);
        Assert.Contains("Binding=\"{Binding ContributorsVisible}\"", properties);
        Assert.Contains("Binding=\"{Binding HasSelectedChildren}\"", properties);
        Assert.Contains("Text=\"{Binding SelectedChildSummary}\"", properties);
        Assert.Contains("StatsChildIndentColumn", groupedRow);
        Assert.Contains("StatsContributorIndentColumn", groupedContributors);
        Assert.Contains("StatsStandaloneContributorRow", xaml);
        Assert.DoesNotContain("StatId", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ProviderStat", properties, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WindowXaml_UsesCompactFilterSelectorAndReadOnlySingleOptionLabel()
    {
        var xaml = LoadWindowXaml();
        var modifiers = ExtractElement(xaml, "<ListBox x:Name=\"StatsListBox\"", "</ListBox>");
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
    public void CanonicalImplicitModType_IsFixedAndNonImplicitVariantsRemainSelectable()
    {
        var implicitVariant = new PriceCheckerModifierFilterVariantViewModel
        {
            Identity = "implicit.example",
            Label = "Pseudo",
            Description = "Provider label must not control the canonical field.",
        };
        var explicitVariant = implicitVariant with
        {
            Identity = "explicit.example",
            Label = "Explicit",
        };

        foreach (var origin in new[] { "ordinary", "Eldritch", "synthesis", "corrupted" })
        {
            var modifier = new PriceCheckerModifierViewModel
            {
                SourceIndex = 0,
                Text = $"{origin} implicit",
                IsSelected = true,
                IsCanonicalImplicit = true,
                FilterVariants = [implicitVariant, explicitVariant],
                SelectedFilterVariant = implicitVariant,
            };

            Assert.Equal("Implicit", modifier.ModTypeLabel);
            Assert.False(modifier.CanSelectFilterVariant);
        }

        var nonImplicit = new PriceCheckerModifierViewModel
        {
            SourceIndex = 1,
            Text = "Explicit modifier",
            IsSelected = true,
            FilterVariants = [explicitVariant, implicitVariant],
            SelectedFilterVariant = explicitVariant,
        };
        Assert.Equal("Explicit", nonImplicit.ModTypeLabel);
        Assert.True(nonImplicit.CanSelectFilterVariant);

        var unique = nonImplicit with
        {
            SourceIndex = 2,
            Text = "Unique modifier",
            IsUniqueModifier = true,
        };
        var foulborn = unique with
        {
            SourceIndex = 3,
            IsFoulbornUniqueModifier = true,
        };
        Assert.Equal("Unique", unique.ModTypeLabel);
        Assert.Equal("Foulborn", foulborn.ModTypeLabel);
        Assert.False(unique.CanSelectFilterVariant);
        Assert.False(foulborn.CanSelectFilterVariant);

        var xaml = LoadWindowXaml();
        var modifiers = ExtractElement(xaml, "<ListBox x:Name=\"StatsListBox\"", "</ListBox>");
        Assert.Contains("Text=\"{Binding ModTypeLabel}\"", modifiers);
        Assert.Contains("Binding=\"{Binding HasStaticModType}\"", modifiers);
        Assert.Contains("IsEnabled=\"{Binding IsInteractionEnabled}\"", modifiers);
        Assert.Contains("Value=\"Collapsed\"", modifiers);
        Assert.Contains("IsHitTestVisible=\"False\"", modifiers);
        Assert.Contains(
            "IsCanonicalImplicit = IsImplicitPresentationModifier(modifier)",
            LoadSearchControllerCode());
        Assert.Contains("IsUniqueModifier = isUniqueModifier", LoadSearchControllerCode());
        Assert.Contains("IsInteractionEnabled = isInteractionEnabled", LoadSearchControllerCode());
    }

    [Fact]
    public void ModifierNameHover_OnlyTruncatedNamesWrapAndRestoreCompactPresentation()
    {
        RunOnSta(() =>
        {
            var longName = new TextBlock
            {
                Width = 80,
                FontSize = 12,
                Text = "A very long modifier name that needs more than one line",
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            };
            var shortName = new TextBlock
            {
                Width = 200,
                FontSize = 12,
                Text = "Short modifier",
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
            };
            var panel = new StackPanel { Children = { longName, shortName } };
            var window = new Window { Content = panel };
            window.Show();
            window.UpdateLayout();

            Assert.True(PriceCheckerWindow.IsModifierNameTruncated(longName));
            Assert.False(PriceCheckerWindow.IsModifierNameTruncated(shortName));

            PriceCheckerWindow.SetModifierNameHoverState(longName, isExpanded: true);
            Assert.Equal(TextWrapping.Wrap, longName.TextWrapping);
            Assert.Equal(TextTrimming.None, longName.TextTrimming);

            PriceCheckerWindow.SetModifierNameHoverState(longName, isExpanded: false);
            Assert.Equal(TextWrapping.NoWrap, longName.TextWrapping);
            Assert.Equal(TextTrimming.CharacterEllipsis, longName.TextTrimming);

            var xaml = LoadWindowXaml();
            Assert.Contains("x:Key=\"ModifierNameAreaStyle\"", xaml);
            Assert.Contains("Handler=\"OnModifierNameAreaMouseEnter\"", xaml);
            Assert.Contains("Handler=\"OnModifierNameAreaMouseLeave\"", xaml);
            Assert.Contains("Tag=\"ModifierNameText\"", xaml);
            Assert.DoesNotContain("ToolTip=\"{Binding Text}\"", xaml);
            Assert.Contains("VerticalAlignment=\"Top\"", xaml);
            window.Close();
        });
    }

    [Fact]
    public void CanonicalImplicitModType_RuntimeHidesTheSelectorWithoutChangingRowSelection()
    {
        RunOnSta(() =>
        {
            var implicitVariant = new PriceCheckerModifierFilterVariantViewModel
            {
                Identity = "implicit.example",
                Label = "Pseudo",
                Description = "Provider variant",
            };
            var explicitVariant = implicitVariant with { Identity = "explicit.example", Label = "Explicit" };
            var implicitModifier = new PriceCheckerModifierViewModel
            {
                SourceIndex = 0,
                Text = "Implicit modifier",
                IsSelected = true,
                IsCanonicalImplicit = true,
                FilterVariants = [implicitVariant, explicitVariant],
                SelectedFilterVariant = implicitVariant,
            };
            var explicitModifier = implicitModifier with
            {
                SourceIndex = 1,
                Text = "Explicit modifier",
                IsCanonicalImplicit = false,
                SelectedFilterVariant = explicitVariant,
            };
            var window = new PriceCheckerWindow();
            var selectionChanges = new List<PriceCheckerModifierSelectionChangedEventArgs>();
            window.ModifierSelectionChanged += (_, change) => selectionChanges.Add(change);
            window.UpdateSearch(new PriceCheckerSearchViewState
            {
                Modifiers = [implicitModifier, explicitModifier],
            });
            window.Show();
            window.UpdateLayout();

            var stats = Assert.IsType<ListBox>(window.FindName("StatsListBox"));
            var implicitContainer = Assert.IsType<ListBoxItem>(stats.ItemContainerGenerator.ContainerFromIndex(0));
            var explicitContainer = Assert.IsType<ListBoxItem>(stats.ItemContainerGenerator.ContainerFromIndex(1));
            var implicitSelector = FindDescendants<ComboBox>(implicitContainer)
                .Single(control => ReferenceEquals(control.DataContext, implicitModifier));
            var explicitSelector = FindDescendants<ComboBox>(explicitContainer)
                .Single(control => ReferenceEquals(control.DataContext, explicitModifier));

            Assert.Equal(Visibility.Collapsed, implicitSelector.Visibility);
            Assert.False(implicitSelector.IsVisible);
            Assert.Equal(Visibility.Visible, explicitSelector.Visibility);
            Assert.True(explicitSelector.IsEnabled);
            Assert.Contains(FindDescendants<TextBlock>(implicitContainer), text => text.Text == "Implicit");

            RaisePreviewLeftClick(FindText(implicitContainer, "Implicit"));
            Assert.Single(selectionChanges);
            Assert.Equal(implicitModifier.SourceIndex, selectionChanges[0].ModifierIndex);
            window.Close();
        });
    }

    [Fact]
    public void MinimumWidth_KeepsHeaderFiltersAndItemStateControlsOnOneLine()
    {
        RunOnSta(() =>
        {
            var filters = new[]
            {
                RequestedFilter(TradeSearchRequestedItemFilterKind.ItemLevel, "Item Level", "84"),
                RequestedFilter(TradeSearchRequestedItemFilterKind.Quality, "Quality", "20"),
                RequestedFilter(TradeSearchRequestedItemFilterKind.Links, "Links", "6"),
                RequestedFilter(TradeSearchRequestedItemFilterKind.Sockets, "Sockets", "6"),
            };
            var draft = new TradeSearchDraft
            {
                ItemClass = "Body Armours",
                Rarity = "Rare",
                DisplayName = "Header Test",
                ParsedBaseType = "Astral Plate",
                SocketText = "R-R-G-R-B-G",
                BaseRollPercentile = 100m,
                RequestedItemFilters = filters.ToImmutableArray(),
                Base = new TradeSearchBaseDraft
                {
                    ActiveCriterion = new BaseSearchCriterion
                    {
                        Mode = BaseSearchMode.Category,
                        Category = "Body Armour",
                    },
                },
                ItemStateCriteria = new TradeItemStateCriteria
                {
                    Mirrored = TradeTriState.No,
                    Corrupted = TradeTriState.No,
                    Identified = TradeTriState.Yes,
                },
            };
            var window = new PriceCheckerWindow
            {
                Width = PriceCheckerPlacementCalculator.UserPanelMinimumWidth,
                Height = 720,
            };
            window.UpdateContent(new PriceCheckerWindowState(
                draft,
                TradeSearchValidationResult.FromDiagnostics([]))
            {
                Presentation = new PriceCheckerItemPresentation
                {
                    IsRarityEditable = true,
                    CategoryDisplayLabel = "An intentionally very long provider category label that must be trimmed",
                },
            });
            window.Show();
            window.UpdateLayout();

            var header = Assert.IsType<StackPanel>(window.FindName("HeaderRequestedFiltersPanel"));
            var itemLevel = Assert.IsType<Border>(window.FindName("ItemLevelFilterBorder"));
            var baseRoll = Assert.IsType<TextBlock>(window.FindName("BaseRollMetadataText"));
            var baseCriterion = Assert.IsType<Button>(window.FindName("BaseCriterionButton"));
            var baseCriterionText = Assert.IsType<TextBlock>(window.FindName("BaseCriterionText"));
            var rarity = Assert.IsType<ComboBox>(window.FindName("RarityComboBox"));
            var stateButtons = new[]
            {
                Assert.IsType<Button>(window.FindName("MirroredStateButton")),
                Assert.IsType<Button>(window.FindName("CorruptedStateButton")),
                Assert.IsType<Button>(window.FindName("IdentifiedStateButton")),
            };
            Assert.Equal(PriceCheckerPlacementCalculator.UserPanelMinimumWidth, window.MinWidth);
            Assert.True(header.ActualWidth >= header.DesiredSize.Width - 0.5d);
            Assert.InRange(
                Math.Abs(itemLevel.TranslatePoint(new Point(0, 0), header).Y -
                    baseRoll.TranslatePoint(new Point(0, 0), header).Y),
                0d,
                3d);
            Assert.True(baseRoll.TranslatePoint(new Point(baseRoll.ActualWidth, 0), window).X <=
                window.ActualWidth - 12d);
            Assert.Equal(68d, rarity.ActualWidth);
            Assert.True(baseCriterion.ActualWidth >= 72d);
            Assert.Contains(baseCriterionText.Text, baseCriterion.ToolTip?.ToString(), StringComparison.Ordinal);
            Assert.True(rarity.TranslatePoint(new Point(rarity.ActualWidth, 0), window).X <=
                window.ActualWidth);
            Assert.All(stateButtons, button => Assert.True(
                button.TranslatePoint(new Point(button.ActualWidth, 0), window).X <= window.ActualWidth));
            Assert.All(stateButtons, button => Assert.Equal(
                stateButtons[0].TranslatePoint(new Point(0, 0), window).Y,
                button.TranslatePoint(new Point(0, 0), window).Y,
                3));
            window.Close();
        });
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
    public void StatsRowClickDecision_RejectsInteractiveAndNestedRowDescendants()
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

                var contributorSurface = new Grid { Tag = "ModifierContributorRow" };
                var contributorText = new TextBlock { Text = "30% increased Physical Damage" };
                var contributorTextBox = new TextBox();
                contributorSurface.Children.Add(contributorText);
                contributorSurface.Children.Add(contributorTextBox);
                surface.Children.Add(contributorSurface);

                Assert.False(PriceCheckerWindow.ShouldToggleStatsRowFrom(row, comboBox));
                Assert.False(PriceCheckerWindow.ShouldToggleStatsRowFrom(row, comboOptionContent));
                Assert.False(PriceCheckerWindow.ShouldToggleStatsRowFrom(row, textBox));
                Assert.False(PriceCheckerWindow.ShouldToggleStatsRowFrom(row, buttonContent));
                Assert.False(PriceCheckerWindow.ShouldToggleStatsRowFrom(row, contributorText));
                Assert.True(PriceCheckerWindow.ShouldToggleStatsRowFrom(row, plainSurface));
                Assert.True(PriceCheckerWindow.ShouldToggleStatsRowFrom(contributorSurface, contributorText));
                Assert.False(PriceCheckerWindow.ShouldToggleStatsRowFrom(
                    contributorSurface,
                    contributorTextBox));
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
    public void StatsRows_RoutedBackgroundAndInteractiveClicksDispatchExactlyTheirOwnActions()
    {
        RunOnSta(() =>
        {
            var contributor = new PriceCheckerModifierContributorViewModel
            {
                ParentSourceIndex = 0,
                ContributorIndex = 0,
                Text = "30% increased Physical Damage",
                IsInteractionEnabled = true,
                SupportsValueBounds = true,
            };
            var explicitVariant = new PriceCheckerModifierFilterVariantViewModel
            {
                Identity = "explicit.test",
                Label = "Explicit",
                Description = "Explicit test filter",
                SupportsValueBounds = true,
            };
            var grouped = new PriceCheckerModifierViewModel
            {
                SourceIndex = 0,
                Text = "146% increased Physical Damage",
                IsSelected = true,
                SupportsValueBounds = true,
                FilterVariants =
                [
                    explicitVariant,
                    explicitVariant with { Identity = "pseudo.test", Label = "Pseudo" },
                ],
                SelectedFilterVariant = explicitVariant,
                Contributors = [contributor],
            };
            var property = new PriceCheckerItemPropertyViewModel
            {
                SourceIndex = 0,
                Kind = TradeSearchItemPropertyKind.PhysicalDps,
                Label = "Physical DPS",
                IsAvailable = true,
                IsExpanded = true,
                Children = [grouped],
            };
            var disabledProperty = property with
            {
                SourceIndex = 1,
                Kind = TradeSearchItemPropertyKind.TotalDps,
                Label = "Total DPS",
                IsAvailable = false,
                IsExpanded = false,
                Children = [],
            };
            var standaloneAggregate = grouped with
            {
                SourceIndex = 1,
                Text = "31% increased Stun and Block Recovery",
                ShowsExpansionControl = true,
                IsExpanded = true,
                Contributors =
                [
                    contributor with
                    {
                        ParentSourceIndex = 1,
                        Text = "9% increased Stun and Block Recovery",
                    },
                ],
            };
            var leaf = grouped with
            {
                SourceIndex = 2,
                Text = "+53 to Dexterity",
                Contributors = [],
                ShowsExpansionControl = false,
            };
            var window = new PriceCheckerWindow();
            var propertyChanges = new List<PriceCheckerItemPropertySelectionChangedEventArgs>();
            var modifierChanges = new List<PriceCheckerModifierSelectionChangedEventArgs>();
            var propertyExpansions = new List<PriceCheckerItemPropertyExpansionChangedEventArgs>();
            window.ItemPropertySelectionChanged += (_, e) => propertyChanges.Add(e);
            window.ModifierSelectionChanged += (_, e) => modifierChanges.Add(e);
            window.ItemPropertyExpansionChanged += (_, e) => propertyExpansions.Add(e);
            window.UpdateSearch(new PriceCheckerSearchViewState
            {
                ItemProperties = [property, disabledProperty],
                Modifiers = [standaloneAggregate, leaf],
            });
            window.Show();
            window.Measure(new Size(900, 1200));
            window.Arrange(new Rect(0, 0, 900, 1200));
            window.UpdateLayout();

            var stats = Assert.IsType<ListBox>(window.FindName("StatsListBox"));
            var propertyContainer = Assert.IsType<ListBoxItem>(
                stats.ItemContainerGenerator.ContainerFromIndex(0));
            var disabledContainer = Assert.IsType<ListBoxItem>(
                stats.ItemContainerGenerator.ContainerFromIndex(1));
            var aggregateContainer = Assert.IsType<ListBoxItem>(
                stats.ItemContainerGenerator.ContainerFromIndex(2));
            var leafContainer = Assert.IsType<ListBoxItem>(
                stats.ItemContainerGenerator.ContainerFromIndex(3));

            RaisePreviewLeftClick(FindText(propertyContainer, "Physical DPS"));
            Assert.Single(propertyChanges);
            RaisePreviewLeftClick(FindText(disabledContainer, "Total DPS"));
            Assert.Single(propertyChanges);

            var propertyCheckBox = FindDescendants<CheckBox>(propertyContainer)
                .First(candidate => ReferenceEquals(candidate.DataContext, property));
            propertyCheckBox.IsChecked = true;
            propertyCheckBox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, propertyCheckBox));
            Assert.Equal(2, propertyChanges.Count);

            var propertyMinimum = FindDescendants<TextBox>(propertyContainer)
                .First(candidate => ReferenceEquals(candidate.DataContext, property));
            RaisePreviewLeftClick(propertyMinimum);
            Assert.Equal(2, propertyChanges.Count);

            var propertyExpand = FindDescendants<Button>(propertyContainer)
                .First(candidate => ReferenceEquals(candidate.DataContext, property));
            propertyExpand.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, propertyExpand));
            Assert.Single(propertyExpansions);
            Assert.Equal(2, propertyChanges.Count);

            var groupedSurface = FindDescendants<Grid>(propertyContainer)
                .Single(candidate => Equals(candidate.Tag, "GroupedModifierRow"));
            RaisePreviewLeftClick(FindText(groupedSurface, grouped.Text));
            Assert.Single(modifierChanges);
            Assert.Equal(grouped.SourceIndex, modifierChanges[0].ModifierIndex);

            var groupedCheckBox = FindDescendants<CheckBox>(groupedSurface).Single();
            groupedCheckBox.IsChecked = false;
            groupedCheckBox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, groupedCheckBox));
            Assert.Equal(2, modifierChanges.Count);

            foreach (var groupedBound in FindDescendants<TextBox>(groupedSurface))
            {
                RaisePreviewLeftClick(groupedBound);
            }
            RaisePreviewLeftClick(FindDescendants<ComboBox>(groupedSurface).Single());
            Assert.Equal(2, modifierChanges.Count);

            var groupedContributor = FindDescendants<Grid>(propertyContainer)
                .Single(candidate => Equals(candidate.Tag, "ModifierContributorRow"));
            RaisePreviewLeftClick(FindText(groupedContributor, contributor.Text));
            Assert.Equal(3, modifierChanges.Count);
            Assert.Equal(contributor.ContributorIndex, modifierChanges[2].ContributorIndex);
            var contributorCheckBox = FindDescendants<CheckBox>(groupedContributor).Single();
            contributorCheckBox.IsChecked = true;
            contributorCheckBox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, contributorCheckBox));
            Assert.Equal(4, modifierChanges.Count);

            RaisePreviewLeftClick(FindText(aggregateContainer, standaloneAggregate.Text));
            RaisePreviewLeftClick(FindText(leafContainer, leaf.Text));
            Assert.Equal(6, modifierChanges.Count);

            var aggregateCheckBox = FindDescendants<CheckBox>(aggregateContainer)
                .First(candidate => ReferenceEquals(candidate.DataContext, standaloneAggregate));
            aggregateCheckBox.IsChecked = true;
            aggregateCheckBox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, aggregateCheckBox));
            var leafCheckBox = FindDescendants<CheckBox>(leafContainer).Single();
            leafCheckBox.IsChecked = true;
            leafCheckBox.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, leafCheckBox));
            Assert.Equal(8, modifierChanges.Count);

            var aggregateExpand = FindDescendants<Button>(aggregateContainer)
                .First(candidate => ReferenceEquals(candidate.DataContext, standaloneAggregate));
            RaisePreviewLeftClick(aggregateExpand);
            Assert.Equal(8, modifierChanges.Count);

            window.Close();
        });
    }

    [Fact]
    public void StatsRows_RuntimeColumnsAlignTopLevelsAndIndentChildrenAndContributors()
    {
        RunOnSta(() =>
        {
            var propertyContributor = new PriceCheckerModifierContributorViewModel
            {
                ParentSourceIndex = 0,
                ContributorIndex = 0,
                Text = "52% increased Physical Damage",
                IsSelected = true,
                SupportsValueBounds = true,
                IsInteractionEnabled = true,
            };
            var grouped = new PriceCheckerModifierViewModel
            {
                SourceIndex = 0,
                Text = "91% increased Physical Damage",
                IsSelected = true,
                SupportsValueBounds = true,
                Contributors = [propertyContributor],
            };
            var total = new PriceCheckerItemPropertyViewModel
            {
                SourceIndex = 0,
                Kind = TradeSearchItemPropertyKind.TotalDps,
                Label = "Total DPS",
                IsSelected = true,
                IsAvailable = true,
            };
            var physical = total with
            {
                SourceIndex = 1,
                Kind = TradeSearchItemPropertyKind.PhysicalDps,
                Label = "Physical DPS",
                IsExpanded = true,
                Children = [grouped],
            };
            var standaloneContributor = propertyContributor with
            {
                ParentSourceIndex = 1,
                Text = "9% increased Stun and Block Recovery",
            };
            var standaloneAggregate = grouped with
            {
                SourceIndex = 1,
                Text = "31% increased Stun and Block Recovery",
                ShowsExpansionControl = true,
                IsExpanded = true,
                Contributors = [standaloneContributor],
            };
            var standaloneLeaf = grouped with
            {
                SourceIndex = 2,
                Text = "+6% to Fire Resistance",
                ShowsExpansionControl = false,
                Contributors = [],
            };
            var window = new PriceCheckerWindow { ShowActivated = true };
            window.UpdateSearch(new PriceCheckerSearchViewState
            {
                ItemProperties = [total, physical],
                Modifiers = [standaloneAggregate, standaloneLeaf],
            });
            window.Show();
            window.Measure(new Size(900, 1200));
            window.Arrange(new Rect(0, 0, 900, 1200));
            window.UpdateLayout();

            var stats = Assert.IsType<ListBox>(window.FindName("StatsListBox"));
            var checkBoxes = FindDescendants<CheckBox>(stats).ToArray();
            var totalX = LeftOf(checkBoxes.Single(control => ReferenceEquals(control.DataContext, total)), stats);
            var physicalX = LeftOf(checkBoxes.Single(control => ReferenceEquals(control.DataContext, physical)), stats);
            var groupedX = LeftOf(checkBoxes.Single(control => ReferenceEquals(control.DataContext, grouped)), stats);
            var propertyContributorX = LeftOf(
                checkBoxes.Single(control => ReferenceEquals(control.DataContext, propertyContributor)),
                stats);
            var aggregateX = LeftOf(
                checkBoxes.Single(control => ReferenceEquals(control.DataContext, standaloneAggregate)),
                stats);
            var leafX = LeftOf(
                checkBoxes.Single(control => ReferenceEquals(control.DataContext, standaloneLeaf)),
                stats);
            var standaloneContributorX = LeftOf(
                checkBoxes.Single(control => ReferenceEquals(control.DataContext, standaloneContributor)),
                stats);

            Assert.Equal(totalX, physicalX, 3);
            Assert.Equal(totalX, aggregateX, 3);
            Assert.Equal(totalX, leafX, 3);
            Assert.Equal(groupedX - physicalX, propertyContributorX - groupedX, 3);
            Assert.Equal(groupedX - physicalX, standaloneContributorX - aggregateX, 3);
            Assert.True(groupedX > physicalX);
            Assert.True(propertyContributorX > groupedX);

            var textBoxes = FindDescendants<TextBox>(stats).ToArray();
            var minimumXs = new object[]
                { physical, grouped, propertyContributor, standaloneAggregate, standaloneContributor }
                .Select(context => LeftOf(textBoxes.Single(control =>
                    ReferenceEquals(control.DataContext, context) &&
                    control.ToolTip is string toolTip &&
                    toolTip.StartsWith("Minimum", StringComparison.Ordinal)), stats))
                .ToArray();
            Assert.All(minimumXs, value => Assert.Equal(minimumXs[0], value, 3));

            var buttons = FindDescendants<Button>(stats).ToArray();
            Assert.Equal(Visibility.Collapsed, buttons.Single(control =>
                ReferenceEquals(control.DataContext, total)).Visibility);
            Assert.Equal(Visibility.Visible, buttons.Single(control =>
                ReferenceEquals(control.DataContext, physical)).Visibility);
            Assert.Equal(Visibility.Visible, buttons.Single(control =>
                ReferenceEquals(control.DataContext, standaloneAggregate)).Visibility);
            Assert.Equal(Visibility.Collapsed, buttons.Single(control =>
                ReferenceEquals(control.DataContext, standaloneLeaf)).Visibility);
            Assert.DoesNotContain(buttons, control => ReferenceEquals(control.DataContext, grouped));

            window.Close();
        });
    }

    [Fact]
    public void AggregateContributor_InactiveStateStrikesAndSubduesTextWithoutChangingValues()
    {
        RunOnSta(() =>
        {
            var inactiveContributor = new PriceCheckerModifierContributorViewModel
            {
                ParentSourceIndex = 0,
                ContributorIndex = 0,
                Text = "30% increased Physical Damage",
                ProvenanceLabel = "Explicit Prefix (Tier 3)",
                IsSelected = true,
                SupportsValueBounds = true,
                IsInteractionEnabled = false,
                InactiveReason = SearchComponentContributorInactiveReason.ParentModeDoesNotComposeContributors,
                MinimumText = "30",
                MaximumText = "40",
            };
            var inactiveAggregate = new PriceCheckerModifierViewModel
            {
                SourceIndex = 0,
                Text = "146% increased Physical Damage",
                IsSelected = true,
                SupportsValueBounds = true,
                Contributors = [inactiveContributor],
            };
            var property = new PriceCheckerItemPropertyViewModel
            {
                SourceIndex = 0,
                Kind = TradeSearchItemPropertyKind.PhysicalDps,
                Label = "Physical DPS",
                IsExpanded = true,
                IsAvailable = true,
                Children = [inactiveAggregate],
            };
            var leaf = new PriceCheckerModifierViewModel
            {
                SourceIndex = 1,
                Text = "+53 to Dexterity",
                IsSelected = true,
                SupportsValueBounds = true,
            };
            var window = new PriceCheckerWindow { ShowActivated = true };
            var searchRequests = 0;
            window.SearchRequested += (_, _) => searchRequests++;
            window.UpdateSearch(new PriceCheckerSearchViewState
            {
                ItemProperties = [property],
                Modifiers = [leaf],
            });
            window.Show();
            window.Measure(new Size(900, 1200));
            window.Arrange(new Rect(0, 0, 900, 1200));
            window.UpdateLayout();

            var stats = Assert.IsType<ListBox>(window.FindName("StatsListBox"));
            var inactivePrimary = FindText(stats, inactiveContributor.Text);
            var inactiveSecondary = FindText(stats, inactiveContributor.ProvenanceLabel);
            var inactiveCheckBox = FindDescendants<CheckBox>(stats).Single(control =>
                ReferenceEquals(control.DataContext, inactiveContributor));
            var inactiveBounds = FindDescendants<TextBox>(stats).Where(control =>
                ReferenceEquals(control.DataContext, inactiveContributor)).ToArray();

            Assert.True(inactiveContributor.IsInactive);
            Assert.False(inactiveContributor.IsInteractionEnabled);
            Assert.False(inactiveContributor.CanEditBounds);
            Assert.NotNull(inactivePrimary.TextDecorations);
            Assert.NotEmpty(inactivePrimary.TextDecorations!);
            Assert.Equal(Color.FromRgb(0x70, 0x70, 0x70),
                Assert.IsType<SolidColorBrush>(inactivePrimary.Foreground).Color);
            Assert.Equal(Color.FromRgb(0x70, 0x70, 0x70),
                Assert.IsType<SolidColorBrush>(inactiveSecondary.Foreground).Color);
            Assert.False(inactiveCheckBox.IsEnabled);
            Assert.All(inactiveBounds, bound => Assert.False(bound.IsEnabled));
            Assert.True(inactiveContributor.IsSelected);
            Assert.Equal("30", inactiveContributor.MinimumText);
            Assert.Equal("40", inactiveContributor.MaximumText);

            var activeContributor = inactiveContributor with
            {
                IsInteractionEnabled = true,
                InactiveReason = SearchComponentContributorInactiveReason.None,
            };
            var activeAggregate = inactiveAggregate with
            {
                IsSelected = false,
                Contributors = [activeContributor],
            };
            var activeProperty = property with { Children = [activeAggregate] };
            window.UpdateSearch(new PriceCheckerSearchViewState
            {
                ItemProperties = [activeProperty],
                Modifiers = [leaf],
            });
            window.UpdateLayout();

            var activePrimary = FindText(stats, activeContributor.Text);
            var activeSecondary = FindText(stats, activeContributor.ProvenanceLabel);
            var activeCheckBox = FindDescendants<CheckBox>(stats).Single(control =>
                ReferenceEquals(control.DataContext, activeContributor));
            var activeBounds = FindDescendants<TextBox>(stats).Where(control =>
                ReferenceEquals(control.DataContext, activeContributor)).ToArray();
            var leafPrimary = FindText(stats, leaf.Text);

            Assert.False(activeContributor.IsInactive);
            Assert.True(activeContributor.IsInteractionEnabled);
            Assert.True(activeContributor.CanEditBounds);
            Assert.True(activePrimary.TextDecorations is null || activePrimary.TextDecorations.Count == 0);
            Assert.Equal(Color.FromRgb(0xD8, 0xD8, 0xD8),
                Assert.IsType<SolidColorBrush>(activePrimary.Foreground).Color);
            Assert.Equal(Color.FromRgb(0x8F, 0x8F, 0x8F),
                Assert.IsType<SolidColorBrush>(activeSecondary.Foreground).Color);
            Assert.True(activeCheckBox.IsEnabled);
            Assert.All(activeBounds, bound => Assert.True(bound.IsEnabled));
            Assert.True(activeContributor.IsSelected);
            Assert.Equal("30", activeContributor.MinimumText);
            Assert.Equal("40", activeContributor.MaximumText);
            Assert.True(leafPrimary.TextDecorations is null || leafPrimary.TextDecorations.Count == 0);
            Assert.Equal(0, searchRequests);

            window.Close();
        });
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("0", true)]
    [InlineData("123", true)]
    [InlineData("12.5", true)]
    [InlineData("12,5", true)]
    [InlineData(".", true)]
    [InlineData(",", true)]
    [InlineData("abc", false)]
    [InlineData("1 2", false)]
    [InlineData("1.2.3", false)]
    [InlineData("1,2,3", false)]
    [InlineData("1.2,3", false)]
    [InlineData("+1", false)]
    [InlineData("-1", false)]
    [InlineData("1e3", false)]
    public void DecimalInput_TypingAllowsOnlyUnsignedProspectiveDecimalText(string text, bool expected)
    {
        Assert.Equal(expected, PriceCheckerDecimalInputBehavior.IsTextAllowed(text));
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("123", true)]
    [InlineData("12.5", true)]
    [InlineData("12,5", true)]
    [InlineData("", false)]
    [InlineData(".", false)]
    [InlineData(",", false)]
    [InlineData("1.", false)]
    [InlineData(" 1", false)]
    [InlineData("1 ", false)]
    [InlineData("1.2,3", false)]
    [InlineData("+1", false)]
    [InlineData("-1", false)]
    [InlineData("1e3", false)]
    public void DecimalInput_PasteAcceptsOnlyCompleteUnsignedIntegersOrDecimals(string text, bool expected)
    {
        Assert.Equal(expected, PriceCheckerDecimalInputBehavior.IsPasteTextAllowed(text));
    }

    [Fact]
    public void DecimalInput_UsesCaretAndSelectionForReplacementBackspaceAndDeleteResults()
    {
        Assert.True(PriceCheckerDecimalInputBehavior.IsProspectiveTextAllowed("12.34", 0, 5, "9,5"));
        Assert.Equal("12.4", PriceCheckerDecimalInputBehavior.ProspectiveText("12.34", 3, 1, ""));
        Assert.Equal("12.4", PriceCheckerDecimalInputBehavior.ProspectiveText("12.34", 3, 1, ""));
        Assert.Equal(".34", PriceCheckerDecimalInputBehavior.ProspectiveText("12.34", 0, 2, ""));
        Assert.True(PriceCheckerDecimalInputBehavior.IsProspectiveTextAllowed("12.34", 2, 1, ","));
        Assert.False(PriceCheckerDecimalInputBehavior.IsProspectiveTextAllowed("12.34", 2, 0, ","));
    }

    [Fact]
    public void WindowXaml_AppliesOneDecimalBehaviorToEveryStatsBoundField()
    {
        var xaml = LoadWindowXaml();
        var style = ExtractElement(xaml, "<Style x:Key=\"ModifierBoundTextBoxStyle\"", "</Style>");
        var stats = ExtractElement(xaml, "<ListBox x:Name=\"StatsListBox\"", "</ListBox>");

        Assert.Contains("PriceCheckerDecimalInputBehavior.IsEnabled", style);
        Assert.Equal(10, stats.Split(
            "Style=\"{StaticResource ModifierBoundTextBoxStyle}\"",
            StringSplitOptions.None).Length - 1);
        Assert.Contains("OnItemPropertyBoundTextChanged", stats);
        Assert.Contains("OnModifierBoundTextChanged", stats);
        Assert.Contains("PriceCheckerModifierContributorViewModel", LoadWindowCodeBehind());
    }

    [Theory]
    [InlineData(StatsBoundTarget.PropertyMinimum, "12.5")]
    [InlineData(StatsBoundTarget.PropertyMaximum, "12.5")]
    [InlineData(StatsBoundTarget.StandaloneMinimum, "12.5")]
    [InlineData(StatsBoundTarget.StandaloneMaximum, "12.5")]
    [InlineData(StatsBoundTarget.GroupedMinimum, "12.5")]
    [InlineData(StatsBoundTarget.GroupedMaximum, "12.5")]
    [InlineData(StatsBoundTarget.ContributorMinimum, "12.5")]
    [InlineData(StatsBoundTarget.ContributorMaximum, "12.5")]
    [InlineData(StatsBoundTarget.PropertyMinimum, "12,5")]
    public void StatsBounds_ContinuousInputKeepsTheSameFocusedTextBoxAndCaret(
        StatsBoundTarget target,
        string input)
    {
        RunOnSta(() =>
        {
            using var fixture = new BoundEditingWindowFixture(target);

            var expectedText = string.Empty;
            foreach (var character in input)
            {
                fixture.EnterText(character.ToString());
                expectedText += character;
                fixture.AssertStable(expectedText);
            }

            Assert.Equal(input.Length, fixture.BoundUpdateCount);
            Assert.Equal(0, fixture.SearchRequestCount);
            Assert.False(fixture.TradeButton.IsEnabled);
            Assert.Empty(fixture.OfferListBox.Items);
        });
    }

    [Fact]
    public void StatsBounds_SelectionReplacementBackspaceAndDeleteKeepFocusAndCaret()
    {
        RunOnSta(() =>
        {
            using var fixture = new BoundEditingWindowFixture(StatsBoundTarget.GroupedMinimum);
            fixture.EnterText("1");
            fixture.EnterText("2");
            fixture.EnterText("3");
            fixture.EnterText("4");
            fixture.AssertStable("1234");

            fixture.TextBox.Select(1, 2);
            fixture.EnterText("9");
            fixture.AssertStable("194", expectedCaretIndex: 2);

            EditingCommands.Backspace.Execute(null, fixture.TextBox);
            fixture.AssertStable("14", expectedCaretIndex: 1);

            EditingCommands.Delete.Execute(null, fixture.TextBox);
            fixture.AssertStable("1", expectedCaretIndex: 1);
        });
    }

    [Fact]
    public void StatsBounds_InvalidTextIsRejectedWithoutRefreshOrFocusLoss()
    {
        RunOnSta(() =>
        {
            using var fixture = new BoundEditingWindowFixture(StatsBoundTarget.ContributorMaximum);
            fixture.EnterText("1");
            fixture.EnterText("2");
            fixture.AssertStable("12");
            var updatesBeforeInvalidInput = fixture.BoundUpdateCount;

            fixture.EnterText("x");

            fixture.AssertStable("12");
            Assert.Equal(updatesBeforeInvalidInput, fixture.BoundUpdateCount);
            Assert.Equal(0, fixture.SearchRequestCount);
        });
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
    public void WindowXaml_KeepsCompactTradeButtonAlignedAndHidesAdvancedPlaceholder()
    {
        var xaml = LoadWindowXaml();
        var actionRow = ExtractElement(xaml, "<Grid Grid.Row=\"6\"", "</Grid>");
        var trade = ExtractElement(xaml, "<Button x:Name=\"TradeButton\"", "</Button>");
        var advanced = ExtractElement(actionRow, "<ToggleButton Grid.Column=\"2\"", "/>");

        Assert.Contains("<ToggleButton Grid.Column=\"2\"", actionRow);
        Assert.Contains("x:Name=\"AdvancedToggle\"", advanced);
        Assert.Contains("Visibility=\"Collapsed\"", advanced);
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
        var modifiers = ExtractElement(xaml, "<ListBox x:Name=\"StatsListBox\"", "</ListBox>");

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

    public enum StatsBoundTarget
    {
        PropertyMinimum,
        PropertyMaximum,
        StandaloneMinimum,
        StandaloneMaximum,
        GroupedMinimum,
        GroupedMaximum,
        ContributorMinimum,
        ContributorMaximum,
    }

    private sealed record BoundEditingRows(
        PriceCheckerItemPropertyViewModel Property,
        PriceCheckerModifierViewModel Grouped,
        PriceCheckerModifierContributorViewModel Contributor,
        PriceCheckerModifierViewModel Standalone);

    private sealed class BoundEditingWindowFixture : IDisposable
    {
        private readonly StatsBoundTarget target;
        private readonly BoundEditingRows rows;

        public BoundEditingWindowFixture(StatsBoundTarget target)
        {
            this.target = target;
            rows = CreateBoundEditingRows();
            Window = new PriceCheckerWindow
            {
                ShowActivated = true,
            };
            var initialState = CreateBoundEditingState(rows);
            Window.UpdateSearch(initialState);
            Window.Show();
            Window.Activate();
            Window.Measure(new Size(900, 1200));
            Window.Arrange(new Rect(0, 0, 900, 1200));
            Window.UpdateLayout();

            StatsListBox = Assert.IsType<ListBox>(Window.FindName("StatsListBox"));
            OfferListBox = Assert.IsType<ListBox>(Window.FindName("OfferListBox"));
            TradeButton = Assert.IsType<Button>(Window.FindName("TradeButton"));
            InitialItemsSource = StatsListBox.ItemsSource;
            object dataContext = target switch
            {
                StatsBoundTarget.PropertyMinimum or StatsBoundTarget.PropertyMaximum => rows.Property,
                StatsBoundTarget.StandaloneMinimum or StatsBoundTarget.StandaloneMaximum => rows.Standalone,
                StatsBoundTarget.GroupedMinimum or StatsBoundTarget.GroupedMaximum => rows.Grouped,
                StatsBoundTarget.ContributorMinimum or StatsBoundTarget.ContributorMaximum => rows.Contributor,
                _ => throw new ArgumentOutOfRangeException(nameof(target)),
            };
            var isMaximum = target is StatsBoundTarget.PropertyMaximum or
                StatsBoundTarget.StandaloneMaximum or
                StatsBoundTarget.GroupedMaximum or
                StatsBoundTarget.ContributorMaximum;
            TextBox = FindDescendants<TextBox>(StatsListBox).Single(candidate =>
                ReferenceEquals(candidate.DataContext, dataContext) &&
                candidate.ToolTip is string toolTip &&
                toolTip.StartsWith(isMaximum ? "Maximum" : "Minimum", StringComparison.Ordinal));

            Window.SearchRequested += (_, _) => SearchRequestCount++;
            Window.ItemPropertyBoundsChanged += (_, _) => RefreshAfterBoundEdit();
            Window.ModifierBoundsChanged += (_, _) => RefreshAfterBoundEdit();

            Assert.True(TextBox.Focus());
            Assert.Same(TextBox, Keyboard.Focus(TextBox));
            TextBox.Select(0, 0);
            AssertStable(string.Empty);
        }

        public PriceCheckerWindow Window { get; }

        public ListBox StatsListBox { get; }

        public ListBox OfferListBox { get; }

        public Button TradeButton { get; }

        public TextBox TextBox { get; }

        public object? InitialItemsSource { get; }

        public int BoundUpdateCount { get; private set; }

        public int SearchRequestCount { get; private set; }

        public void EnterText(string text)
        {
            var composition = new TextComposition(
                InputManager.Current,
                TextBox,
                text,
                TextCompositionAutoComplete.On);
            Assert.True(TextCompositionManager.StartComposition(composition));
        }

        public void AssertStable(string expectedText, int? expectedCaretIndex = null)
        {
            Assert.Equal(expectedText, TextBox.Text);
            Assert.Equal(expectedText, CanonicalText());
            Assert.Equal(expectedCaretIndex ?? expectedText.Length, TextBox.CaretIndex);
            Assert.Same(TextBox, Keyboard.FocusedElement);
            Assert.True(TextBox.IsKeyboardFocused);
            Assert.True(StatsListBox.IsAncestorOf(TextBox));
            Assert.Same(InitialItemsSource, StatsListBox.ItemsSource);
        }

        public void Dispose()
        {
            Window.Close();
        }

        private void RefreshAfterBoundEdit()
        {
            BoundUpdateCount++;
            Window.UpdateSearch(Assert.IsType<PriceCheckerSearchViewState>(Window.CurrentSearchState) with
            {
                CanOpenTrade = false,
                Offers = [],
            });
        }

        private string CanonicalText()
        {
            var state = Assert.IsType<PriceCheckerSearchViewState>(Window.CurrentSearchState);
            return target switch
            {
                StatsBoundTarget.PropertyMinimum => state.ItemProperties[0].MinimumText,
                StatsBoundTarget.PropertyMaximum => state.ItemProperties[0].MaximumText,
                StatsBoundTarget.StandaloneMinimum => state.Modifiers[0].MinimumText,
                StatsBoundTarget.StandaloneMaximum => state.Modifiers[0].MaximumText,
                StatsBoundTarget.GroupedMinimum => state.ItemProperties[0].Children[0].MinimumText,
                StatsBoundTarget.GroupedMaximum => state.ItemProperties[0].Children[0].MaximumText,
                StatsBoundTarget.ContributorMinimum =>
                    state.ItemProperties[0].Children[0].Contributors[0].MinimumText,
                StatsBoundTarget.ContributorMaximum =>
                    state.ItemProperties[0].Children[0].Contributors[0].MaximumText,
                _ => throw new ArgumentOutOfRangeException(nameof(target)),
            };
        }
    }

    private static BoundEditingRows CreateBoundEditingRows()
    {
        var contributor = new PriceCheckerModifierContributorViewModel
        {
            ParentSourceIndex = 0,
            ContributorIndex = 0,
            Text = "52% increased Physical Damage",
            IsSelected = true,
            SupportsValueBounds = true,
            IsInteractionEnabled = true,
        };
        var grouped = new PriceCheckerModifierViewModel
        {
            SourceIndex = 0,
            Text = "91% increased Physical Damage",
            IsSelected = true,
            SupportsValueBounds = true,
            Contributors = [contributor],
        };
        var property = new PriceCheckerItemPropertyViewModel
        {
            SourceIndex = 0,
            Kind = TradeSearchItemPropertyKind.PhysicalDps,
            Label = "Physical DPS",
            IsSelected = true,
            IsAvailable = true,
            IsExpanded = true,
            Children = [grouped],
        };
        var standalone = new PriceCheckerModifierViewModel
        {
            SourceIndex = 1,
            Text = "+53 to Dexterity",
            IsSelected = true,
            SupportsValueBounds = true,
        };
        return new BoundEditingRows(property, grouped, contributor, standalone);
    }

    private static PriceCheckerSearchViewState CreateBoundEditingState(BoundEditingRows rows)
    {
        return new PriceCheckerSearchViewState
        {
            CanSearch = true,
            CanOpenTrade = true,
            ItemProperties = [rows.Property],
            Modifiers = [rows.Standalone],
            Offers =
            [
                new PriceCheckerOfferViewModel
                {
                    Id = "offer-before-edit",
                    ItemName = "Horror Mangler",
                    SellerAccountName = "seller",
                    ListedText = "now",
                    ItemLevelText = "82",
                    PriceText = "1 divine",
                },
            ],
        };
    }

    private static void AssertTopLevelHierarchyColumns(string row, string expansionBinding)
    {
        AssertColumnGroups(
            row,
            "StatsExpansionSlotColumn",
            "StatsCheckboxColumn",
            "StatsModTypeColumn",
            "StatsMinimumColumn",
            "StatsMaximumColumn");
        Assert.Contains("<Button Grid.Column=\"0\"", row);
        Assert.Contains($"Binding=\"{{Binding {expansionBinding}}}\"", row);
        Assert.Contains("Property=\"Visibility\"", row);
        Assert.Contains("Value=\"Collapsed\"", row);
        Assert.Contains("<CheckBox Grid.Column=\"1\"", row);
        Assert.True(
            row.IndexOf("<Button Grid.Column=\"0\"", StringComparison.Ordinal) <
            row.IndexOf("<CheckBox Grid.Column=\"1\"", StringComparison.Ordinal));
    }

    private static void AssertColumnGroups(string row, params string[] expectedGroups)
    {
        Assert.Equal(
            expectedGroups.Length,
            row.Split("SharedSizeGroup=", StringSplitOptions.None).Length - 1);
        var previousIndex = -1;
        foreach (var group in expectedGroups)
        {
            var index = row.IndexOf($"SharedSizeGroup=\"{group}\"", StringComparison.Ordinal);
            Assert.True(index > previousIndex, $"Expected {group} after the previous hierarchy column.");
            previousIndex = index;
        }

        Assert.Contains("<ColumnDefinition Width=\"*\"", row);
        Assert.DoesNotContain("<ColumnDefinition Width=\"Auto\"", row);
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

    private static TradeSearchRequestedItemFilter RequestedFilter(
        TradeSearchRequestedItemFilterKind kind,
        string label,
        string value)
    {
        return new TradeSearchRequestedItemFilter
        {
            Kind = kind,
            Label = label,
            ObservedValue = int.Parse(value, CultureInfo.InvariantCulture),
            CurrentText = value,
            RequestedMinimum = int.Parse(value, CultureInfo.InvariantCulture),
            IsActive = true,
            LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
            ProviderResolutionStatus = TradeSearchItemPropertyProviderResolutionStatus.Exact,
        };
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

    private static string LoadSearchControllerCode()
    {
        return File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "PoEnhance.App",
            "Features",
            "PriceChecking",
            "PriceCheckerSearchController.cs"));
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static TextBlock FindText(DependencyObject root, string text)
    {
        return FindDescendants<TextBlock>(root).Single(candidate => candidate.Text == text);
    }

    private static double LeftOf(FrameworkElement element, UIElement relativeTo)
    {
        return element.TranslatePoint(new Point(0, 0), relativeTo).X;
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static void RaisePreviewLeftClick(UIElement source)
    {
        source.RaiseEvent(new MouseButtonEventArgs(
            Mouse.PrimaryDevice,
            Environment.TickCount,
            MouseButton.Left)
        {
            RoutedEvent = Mouse.PreviewMouseDownEvent,
            Source = source,
        });
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
