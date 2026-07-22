using System.Collections.Immutable;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using PoEnhance.App.Features.PriceChecking;

namespace PoEnhance.App.Tests.Features.PriceChecking;

public sealed class ItemCardPreviewWindowLayoutTests
{
    [Fact]
    public void NormalSevenModifierCardGeneratesAllLinesBeforeNaturalHeightIsSelected()
    {
        RunOnSta(() =>
        {
            var window = new ItemCardPreviewWindow(OfferCardWindowMode.Preview);
            var size = window.UpdateContent(NormalSnapshot(), maximumWidth: 1600, maximumHeight: 1440);
            var modifiers = Find<ItemsControl>(window, "ModifierSectionsControl");
            var modifierBlock = Find<Border>(window, "ModifierBlockBorder");
            var content = Find<StackPanel>(window, "ItemContentPanel");
            var diagnostic = Assert.IsType<OfferCardWindowLayoutDiagnostic>(window.LastLayoutDiagnostic);
            var modifierLines = FindModifierLineTextBlocks(modifiers).ToArray();
            var sectionContentPanels = FindDescendants<StackPanel>(modifiers)
                .Where(panel => panel.DataContext is OfferCardPreviewModifierSection &&
                    (panel.Margin.Left == 96d || panel.Margin.Left == 5d))
                .ToArray();
            var implicitContent = Assert.Single(sectionContentPanels,
                panel => ((OfferCardPreviewModifierSection)panel.DataContext).Provenance ==
                    OfferCardModifierProvenance.Implicit);
            var normalContent = sectionContentPanels
                .Where(panel => ((OfferCardPreviewModifierSection)panel.DataContext).Provenance !=
                    OfferCardModifierProvenance.Implicit)
                .ToArray();

            Assert.Equal(GeneratorStatus.ContainersGenerated, modifiers.ItemContainerGenerator.Status);
            Assert.Equal(7, modifierLines.Length);
            Assert.Equal(new Thickness(62d, 5d, 62d, 5d), modifierBlock.Margin);
            Assert.Equal(new Thickness(5d, 0d, 5d, 0d), implicitContent.Margin);
            Assert.Equal(6, normalContent.Length);
            Assert.All(normalContent, panel =>
                Assert.Equal(new Thickness(96d, 0d, 28d, 0d), panel.Margin));
            var implicitLine = Assert.Single(modifierLines, line => line.Text == "Modifier line 2");
            Assert.True(implicitLine.ActualHeight <= implicitLine.FontSize * 2d);
            Assert.Equal(
                implicitContent.Margin.Left,
                implicitContent.TranslatePoint(new Point(0d, 0d), content).X,
                precision: 6);
            Assert.All(modifierLines, line =>
            {
                Assert.Equal(TextAlignment.Left, line.TextAlignment);
                Assert.Equal(HorizontalAlignment.Stretch, line.HorizontalAlignment);
                Assert.Equal(TextWrapping.Wrap, line.TextWrapping);
            });
            Assert.Equal(7, diagnostic.ModifierLineCount);
            Assert.Equal(size.Height, diagnostic.SelectedWindowHeight);
            Assert.False(diagnostic.IsVerticalScrollingEnabled);
            Assert.True(size.Height < 1440d * OfferCardWindowSizeCalculator.MaximumClientHeightRatio);
            Assert.True(diagnostic.TooltipBody.DesiredHeight > 7d * 15d);
            Assert.False(diagnostic.MeasuredAfterFirstCompletedLayoutPass);
            Assert.True(diagnostic.MeasuredAfterDataBindingLayout);

            var modifierSeparator = Assert.Single(
                FindDescendants<Border>(modifiers),
                border => border.Height == 1d && border.ActualWidth > 0d);
            Assert.Equal(
                modifierBlock.TranslatePoint(new Point(0d, 0d), content).X,
                modifierSeparator.TranslatePoint(new Point(0d, 0d), content).X,
                precision: 6);
            Assert.Equal(modifierBlock.ActualWidth, modifierSeparator.ActualWidth, precision: 6);
        });
    }

    [Fact]
    public void ReusingPreviewRecalculatesHeightForLongerThenShorterContentWithoutDeferredStaleness()
    {
        RunOnSta(() =>
        {
            var window = new ItemCardPreviewWindow(OfferCardWindowMode.Preview);
            var shortSize = window.UpdateContent(ShortSnapshot(), 1600, 1440);
            var longSize = window.UpdateContent(NormalSnapshot(), 1600, 1440);
            var shortSizeAgain = window.UpdateContent(ShortSnapshot(), 1600, 1440);

            Assert.True(longSize.Height > shortSize.Height);
            Assert.Equal(shortSize.Height, shortSizeAgain.Height, precision: 6);
            Assert.Equal("short", window.CurrentSnapshot?.OfferId);
            Assert.Equal("short", window.LastLayoutDiagnostic?.OfferId);
        });
    }

    [Fact]
    public void GenuinelyLongCardCapsAndKeepsItsFinalModifierInTheSingleBodyScroller()
    {
        RunOnSta(() =>
        {
            var window = new ItemCardPreviewWindow(OfferCardWindowMode.Preview);
            var size = window.UpdateContent(LongSnapshot(), 1600, 720);
            var scrollViewer = Find<ScrollViewer>(window, "ContentScrollViewer");
            var diagnostic = Assert.IsType<OfferCardWindowLayoutDiagnostic>(window.LastLayoutDiagnostic);

            Assert.Equal(720d * OfferCardWindowSizeCalculator.MaximumClientHeightRatio, size.Height, precision: 6);
            Assert.True(diagnostic.IsVerticalScrollingEnabled);
            Assert.Equal(ScrollBarVisibility.Auto, scrollViewer.VerticalScrollBarVisibility);
            Assert.True(scrollViewer.ExtentHeight > scrollViewer.ViewportHeight);
            Assert.Single(
                FindModifierLineTextBlocks(Find<ItemsControl>(window, "ModifierSectionsControl")),
                textBlock => textBlock.Text == "Modifier line 40");
        });
    }

    [Fact]
    public void WrappedImplicitModifierUsesTheFullLeftAlignedStretchedColumn()
    {
        RunOnSta(() =>
        {
            var snapshot = NormalSnapshot() with
            {
                OfferId = "wrapped",
                ModifierSections =
                [
                    new OfferCardModifierSection
                    {
                        Provenance = OfferCardModifierProvenance.Implicit,
                        Lines = [string.Join(' ', Enumerable.Repeat("A very long modifier line", 16))],
                    },
                ],
            };
            var window = new ItemCardPreviewWindow(OfferCardWindowMode.Preview);
            window.UpdateContent(snapshot, maximumWidth: 700, maximumHeight: 1440);
            var modifiers = Find<ItemsControl>(window, "ModifierSectionsControl");
            var implicitContent = Assert.Single(
                FindDescendants<StackPanel>(modifiers),
                panel => panel.DataContext is OfferCardPreviewModifierSection
                    {
                        Provenance: OfferCardModifierProvenance.Implicit
                    }
                    && panel.Margin == new Thickness(5d, 0d, 5d, 0d));
            var line = Assert.Single(
                FindDescendants<TextBlock>(modifiers),
                textBlock => textBlock.Text?.StartsWith("A very long modifier line", StringComparison.Ordinal) == true);

            Assert.InRange(implicitContent.ActualWidth, modifiers.ActualWidth - 11d, modifiers.ActualWidth - 9d);
            Assert.Equal(TextAlignment.Left, line.TextAlignment);
            Assert.Equal(HorizontalAlignment.Stretch, line.HorizontalAlignment);
            Assert.Equal(TextWrapping.Wrap, line.TextWrapping);
            Assert.True(line.ActualHeight > line.FontSize * 2d);
            Assert.Equal(
                line.TranslatePoint(new Point(0d, 0d), modifiers).X,
                line.TranslatePoint(new Point(0d, line.ActualHeight), modifiers).X,
                precision: 6);
        });
    }

    [Fact]
    public void PinnedAndPreviewHeadersShareTheDarkSurfaceAndOnlyPinStateDiffers()
    {
        RunOnSta(() =>
        {
            var preview = new ItemCardPreviewWindow(OfferCardWindowMode.Preview);
            var pinned = new ItemCardPreviewWindow(OfferCardWindowMode.Pinned);
            preview.UpdateContent(ShortSnapshot(), 1600, 1440);
            pinned.UpdateContent(ShortSnapshot(), 1600, 1440);

            var previewHeader = Find<Border>(preview, "HeaderBorder");
            var pinnedHeader = Find<Border>(pinned, "HeaderBorder");
            var previewRoot = Find<Border>(preview, "PreviewRoot");
            var pinnedRoot = Find<Border>(pinned, "PreviewRoot");
            var previewPin = Find<ToggleButton>(preview, "PinButton");
            var pinnedPin = Find<ToggleButton>(pinned, "PinButton");
            var pinnedDragThumb = Find<Thumb>(pinned, "HeaderDragThumb");

            Assert.Equal(BrushText(previewHeader.Background), BrushText(pinnedHeader.Background));
            Assert.Equal(BrushText(previewRoot.Background), BrushText(pinnedRoot.Background));
            Assert.False(previewPin.IsChecked);
            Assert.True(pinnedPin.IsChecked);
            Assert.Equal(Visibility.Visible, pinnedDragThumb.Visibility);
            Assert.Equal(Brushes.Transparent, pinnedDragThumb.Background);
            Assert.Null(pinnedDragThumb.FocusVisualStyle);
        });
    }

    private static OfferCardSnapshot NormalSnapshot() => new()
    {
        OfferId = "normal",
        Name = "Dusk Shell",
        TypeLine = "Titan Plate",
        Frame = OfferCardFrameKind.Rare,
        ItemLevel = 84,
        Properties =
        [
            Property("Armour", "815"),
            Property("Quality", "+20%"),
        ],
        Requirements =
        [
            Property("Level", "68"),
            Property("Str", "191"),
        ],
        Sockets =
        [
            new OfferCardSocket { Index = 0, Group = 0, Colour = "R" },
            new OfferCardSocket { Index = 1, Group = 0, Colour = "G" },
            new OfferCardSocket { Index = 2, Group = 1, Colour = "B" },
        ],
        ModifierSections =
            Enum.GetValues<OfferCardModifierProvenance>()
                .Select((provenance, index) => new OfferCardModifierSection
                {
                    Provenance = provenance,
                    Lines = [$"Modifier line {index + 1}"],
                })
                .ToImmutableArray(),
        Price = new OfferCardPrice { Amount = 10, Currency = "chaos" },
        Seller = new OfferCardSeller { AccountName = "Seller" },
    };

    private static OfferCardSnapshot ShortSnapshot() => new()
    {
        OfferId = "short",
        TypeLine = "Cobalt Jewel",
        Frame = OfferCardFrameKind.Magic,
    };

    private static OfferCardSnapshot LongSnapshot() => NormalSnapshot() with
    {
        OfferId = "long",
        ModifierSections =
        [
            new OfferCardModifierSection
            {
                Provenance = OfferCardModifierProvenance.Explicit,
                Lines = Enumerable.Range(1, 40)
                    .Select(index => $"Modifier line {index}")
                    .ToImmutableArray(),
            },
        ],
    };

    private static OfferCardProperty Property(string label, string value) => new()
    {
        DisplayName = label,
        Values =
        [
            new OfferCardPropertyValue { Text = value, DisplayStyleCode = 0 },
        ],
    };

    private static T Find<T>(FrameworkElement root, string name)
        where T : FrameworkElement => Assert.IsType<T>(root.FindName(name));

    private static IEnumerable<TextBlock> FindModifierLineTextBlocks(DependencyObject root) =>
        FindDescendants<TextBlock>(root)
            .Where(textBlock => textBlock.Text?.StartsWith("Modifier line ", StringComparison.Ordinal) == true);

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

    private static string BrushText(Brush brush) => brush.ToString();

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
}
