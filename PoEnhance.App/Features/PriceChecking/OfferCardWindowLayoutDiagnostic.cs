using System.Windows;
using System.Windows.Controls;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed record OfferCardWindowLayoutDiagnostic(
    OfferCardWindowMode Mode,
    string OfferId,
    string? ItemName,
    int ModifierLineCount,
    int PropertyLineCount,
    int RequirementsLineCount,
    double RequestedClientWidth,
    double RequestedClientHeight,
    double SelectedWindowWidth,
    double SelectedWindowHeight,
    OfferCardWindowElementLayout Root,
    OfferCardWindowElementLayout Header,
    OfferCardWindowElementLayout TooltipBody,
    OfferCardWindowElementLayout ScrollViewer,
    double ScrollViewerExtentHeight,
    double ScrollViewerViewportHeight,
    OfferCardWindowElementLayout Footer,
    double MaximumHeight,
    bool IsVerticalScrollingEnabled,
    bool MeasuredAfterFirstCompletedLayoutPass,
    bool MeasuredAfterDataBindingLayout)
{
    public static OfferCardWindowLayoutDiagnostic Create(
        OfferCardWindowMode mode,
        OfferCardSnapshot snapshot,
        OfferCardPreviewPresentation presentation,
        double requestedClientWidth,
        double requestedClientHeight,
        double selectedWindowWidth,
        double selectedWindowHeight,
        FrameworkElement root,
        FrameworkElement header,
        FrameworkElement tooltipBody,
        ScrollViewer scrollViewer,
        FrameworkElement footer,
        double maximumHeight,
        bool isVerticalScrollingEnabled,
        bool measuredAfterFirstCompletedLayoutPass)
    {
        return new OfferCardWindowLayoutDiagnostic(
            mode,
            snapshot.OfferId,
            presentation.ItemName,
            presentation.ModifierSections.Sum(section => section.Lines.Length),
            presentation.Properties.Length,
            presentation.HasRequirementsLine ? 1 : 0,
            requestedClientWidth,
            requestedClientHeight,
            selectedWindowWidth,
            selectedWindowHeight,
            OfferCardWindowElementLayout.Create(root),
            OfferCardWindowElementLayout.Create(header),
            OfferCardWindowElementLayout.Create(tooltipBody),
            OfferCardWindowElementLayout.Create(scrollViewer),
            scrollViewer.ExtentHeight,
            scrollViewer.ViewportHeight,
            OfferCardWindowElementLayout.Create(footer),
            maximumHeight,
            isVerticalScrollingEnabled,
            measuredAfterFirstCompletedLayoutPass,
            MeasuredAfterDataBindingLayout: true);
    }
}

internal sealed record OfferCardWindowElementLayout(
    double DesiredWidth,
    double DesiredHeight,
    double ActualWidth,
    double ActualHeight)
{
    public static OfferCardWindowElementLayout Create(FrameworkElement element) => new(
        element.DesiredSize.Width,
        element.DesiredSize.Height,
        element.ActualWidth,
        element.ActualHeight);
}
