using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.Core.Trade;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class PriceCheckerRequestedItemFilterPresentationTests
{
    [Fact]
    public void WindowXaml_OrdersCompactResponsiveHeaderAndKeepsSocketsInformationalLast()
    {
        var xaml = LoadWindowXaml();
        var header = ExtractElement(xaml, "<WrapPanel Grid.Row=\"1\"", "</WrapPanel>");
        var itemLevel = header.IndexOf("ItemLevelFilterBorder", StringComparison.Ordinal);
        var quality = header.IndexOf("QualityFilterBorder", StringComparison.Ordinal);
        var links = header.IndexOf("LinksFilterBorder", StringComparison.Ordinal);
        var sockets = header.IndexOf("SocketMetadataText", StringComparison.Ordinal);

        Assert.True(itemLevel >= 0 && itemLevel < quality && quality < links && links < sockets);
        Assert.Contains("Text=\"Item Level:\"", header);
        Assert.Contains("Text=\"Quality:\"", header);
        Assert.Contains("Text=\"Links:\"", header);
        Assert.DoesNotContain("CheckBox", header);
        Assert.Equal(3, header.Split("RequestedItemFilterBorderStyle", StringSplitOptions.None).Length - 1);
        Assert.Equal(3, header.Split("RequestedItemFilterTextBoxStyle", StringSplitOptions.None).Length - 1);
        Assert.Contains("<TextBlock x:Name=\"SocketMetadataText\"", header);
        Assert.DoesNotContain("TextBox", header[sockets..]);
    }

    [Fact]
    public void WindowXaml_ActiveBorderCoversLabelInputAndHasLayoutStableTransparentInactiveState()
    {
        var xaml = LoadWindowXaml();
        var style = ExtractElement(xaml, "<Style x:Key=\"RequestedItemFilterBorderStyle\"", "</Style>");
        var itemLevel = ExtractElement(xaml, "<Border x:Name=\"ItemLevelFilterBorder\"", "</Border>");

        Assert.Contains("Property=\"Padding\"", style);
        Assert.Contains("Value=\"4,2\"", style);
        Assert.Contains("Property=\"BorderBrush\"", style);
        Assert.Contains("Value=\"Transparent\"", style);
        Assert.Contains("Property=\"BorderThickness\"", style);
        Assert.Contains("Value=\"1\"", style);
        Assert.Contains("Property=\"CornerRadius\"", style);
        Assert.Contains("Value=\"3\"", style);
        Assert.Contains("Property=\"Tag\"", style);
        Assert.Contains("Value=\"Active\"", style);
        Assert.Contains("Value=\"White\"", style);
        Assert.DoesNotContain("IsKeyboardFocus", style);
        Assert.Contains("Text=\"Item Level:\"", itemLevel);
        Assert.Contains("x:Name=\"ItemLevelFilterTextBox\"", itemLevel);
    }

    [Fact]
    public void WindowXaml_Q20IndicatorIsACompactSecondaryItemPropertyBadge()
    {
        var xaml = LoadWindowXaml();
        var stats = ExtractElement(xaml, "<ListBox x:Name=\"StatsListBox\"", "</ListBox>");
        var propertyRow = ExtractElement(stats, "<Grid x:Name=\"StatsTopLevelPropertyRow\"", "</Grid>");

        Assert.Contains("HasCalculationBasisLabel", propertyRow);
        Assert.Contains("Text=\"{Binding CalculationBasisLabel}\"", propertyRow);
        Assert.Contains("FontSize=\"10\"", propertyRow);
        Assert.Contains("CornerRadius=\"2\"", propertyRow);
        Assert.DoesNotContain("Q20", propertyRow);
    }

    [Fact]
    public void WindowRuntime_ActiveStateControlsWholeBorderWhileFocusAloneDoesNot()
    {
        RunOnSta(() =>
        {
            var window = new PriceCheckerWindow { ShowActivated = true };
            var inactive = Draft();
            window.UpdateContent(State(inactive));
            window.Show();
            window.UpdateLayout();

            var border = Assert.IsType<Border>(window.FindName("QualityFilterBorder"));
            var textBox = Assert.IsType<TextBox>(window.FindName("QualityFilterTextBox"));
            Assert.Equal(Colors.Transparent, ((SolidColorBrush)border.BorderBrush).Color);

            textBox.Focus();
            window.UpdateLayout();
            Assert.Equal(Colors.Transparent, ((SolidColorBrush)border.BorderBrush).Color);

            var active = Replace(inactive, Filter(inactive, TradeSearchRequestedItemFilterKind.Quality) with
            {
                IsActive = true,
            });
            window.UpdateContent(State(active));
            window.UpdateLayout();
            Assert.Equal(Colors.White, ((SolidColorBrush)border.BorderBrush).Color);

            window.Close();
        });
    }

    [Fact]
    public void WindowRuntime_LabelClickTogglesButInputClickDoesNotAndFocusedEditEmitsValue()
    {
        RunOnSta(() =>
        {
            var window = new PriceCheckerWindow { ShowActivated = true };
            var activation = new List<PriceCheckerRequestedItemFilterActivationChangedEventArgs>();
            var values = new List<PriceCheckerRequestedItemFilterValueChangedEventArgs>();
            window.RequestedItemFilterActivationChanged += (_, e) => activation.Add(e);
            window.RequestedItemFilterValueChanged += (_, e) => values.Add(e);
            var draft = Draft();
            window.UpdateContent(State(draft));
            window.Show();
            window.UpdateLayout();

            var border = Assert.IsType<Border>(window.FindName("ItemLevelFilterBorder"));
            var panel = Assert.IsType<StackPanel>(border.Child);
            var label = Assert.IsType<TextBlock>(panel.Children[0]);
            var textBox = Assert.IsType<TextBox>(window.FindName("ItemLevelFilterTextBox"));

            RaisePreviewLeftClick(label);
            var toggle = Assert.Single(activation);
            Assert.Equal(TradeSearchRequestedItemFilterKind.ItemLevel, toggle.Kind);
            Assert.True(toggle.IsActive);

            var active = Replace(draft, Filter(draft, TradeSearchRequestedItemFilterKind.ItemLevel) with
            {
                IsActive = true,
            });
            window.UpdateContent(State(active));
            RaisePreviewLeftClick(textBox);
            Assert.Single(activation);

            textBox.Focus();
            textBox.CaretIndex = textBox.Text.Length;
            textBox.Text += "0";
            var edit = Assert.Single(values);
            Assert.Equal(TradeSearchRequestedItemFilterKind.ItemLevel, edit.Kind);
            Assert.Equal("850", edit.Text);

            window.Close();
        });
    }

    [Fact]
    public void WindowRuntime_ContentUpdatesReuseTextBoxAndPreserveFocusedCaret()
    {
        RunOnSta(() =>
        {
            var window = new PriceCheckerWindow { ShowActivated = true };
            var original = Draft();
            window.UpdateContent(State(original));
            window.Show();
            window.UpdateLayout();
            var textBox = Assert.IsType<TextBox>(window.FindName("LinksFilterTextBox"));
            textBox.Focus();
            textBox.CaretIndex = 1;

            var updated = Replace(original, Filter(original, TradeSearchRequestedItemFilterKind.Links) with
            {
                CurrentText = "36",
                RequestedMinimum = 36,
                IsActive = true,
            });
            window.UpdateContent(State(updated));

            Assert.Same(textBox, window.FindName("LinksFilterTextBox"));
            Assert.Equal("36", textBox.Text);
            Assert.Equal(1, textBox.CaretIndex);
            window.Close();
        });
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("123", true)]
    [InlineData("-1", false)]
    [InlineData("1.5", false)]
    [InlineData("１２", false)]
    public void IntegerInputBehavior_UsesUnsignedAsciiIntegerGrammar(string text, bool allowed)
    {
        Assert.Equal(allowed, PriceCheckerIntegerInputBehavior.IsTextAllowed(text));
    }

    [Theory]
    [InlineData("42", true)]
    [InlineData("", false)]
    [InlineData("4 2", false)]
    [InlineData("4.2", false)]
    public void IntegerInputBehavior_PasteAcceptsOnlyNonEmptyUnsignedAsciiDigits(string text, bool allowed)
    {
        Assert.Equal(allowed, PriceCheckerIntegerInputBehavior.IsPasteTextAllowed(text));
    }

    [Fact]
    public void IntegerInputBehavior_ProspectiveEditPreservesContinuousSelectionAndCaretSemantics()
    {
        Assert.Equal("123", PriceCheckerIntegerInputBehavior.ProspectiveText("13", 1, 0, "2"));
        Assert.Equal("14", PriceCheckerIntegerInputBehavior.ProspectiveText("123", 1, 2, "4"));
    }

    private static PriceCheckerWindowState State(TradeSearchDraft draft) =>
        new(draft, TradeSearchValidationResult.FromDiagnostics([]));

    private static TradeSearchDraft Draft() => new()
    {
        DisplayName = "Header Test",
        ParsedBaseType = "Reaver Axe",
        SocketText = "G-R-R",
        RequestedItemFilters =
        [
            Requested(TradeSearchRequestedItemFilterKind.ItemLevel, "Item Level", 85),
            Requested(TradeSearchRequestedItemFilterKind.Quality, "Quality", 0),
            Requested(TradeSearchRequestedItemFilterKind.Links, "Links", 3),
        ],
    };

    private static TradeSearchRequestedItemFilter Requested(
        TradeSearchRequestedItemFilterKind kind,
        string label,
        int value) => new()
    {
        Kind = kind,
        Label = label,
        ObservedValue = value,
        CurrentText = value.ToString(System.Globalization.CultureInfo.InvariantCulture),
        RequestedMinimum = value,
        LocalValidationStatus = TradeSearchRequestedItemFilterValidationStatus.Valid,
    };

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

    private static string LoadWindowXaml() => File.ReadAllText(Path.Combine(
        FindRepositoryRoot(),
        "PoEnhance.App",
        "Features",
        "PriceChecking",
        "PriceCheckerWindow.xaml"));

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
