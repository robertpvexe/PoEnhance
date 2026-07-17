namespace PoEnhance.App.Features.PriceChecking;

internal interface IPriceCheckerWindow
{
    event EventHandler? Closed;

    event EventHandler? PanelActivated;

    event EventHandler? PanelDeactivated;

    event EventHandler? PanelInteraction;

    event EventHandler? SearchRequested;

    event EventHandler? LoadMoreRequested;

    event EventHandler? TradeRequested;

    event EventHandler<PriceCheckerOfferCapacityChangedEventArgs>? OfferCapacityChanged;

    event EventHandler<PriceCheckerItemPropertySelectionChangedEventArgs>? ItemPropertySelectionChanged;

    event EventHandler<PriceCheckerItemPropertyBoundsChangedEventArgs>? ItemPropertyBoundsChanged;

    event EventHandler<PriceCheckerItemPropertyExpansionChangedEventArgs>? ItemPropertyExpansionChanged;

    event EventHandler<PriceCheckerRequestedItemFilterActivationChangedEventArgs>?
        RequestedItemFilterActivationChanged
        {
            add { }
            remove { }
        }

    event EventHandler<PriceCheckerRequestedItemFilterValueChangedEventArgs>?
        RequestedItemFilterValueChanged
        {
            add { }
            remove { }
        }

    event EventHandler<PriceCheckerModifierSelectionChangedEventArgs>? ModifierSelectionChanged;

    event EventHandler<PriceCheckerModifierBoundsChangedEventArgs>? ModifierBoundsChanged;

    event EventHandler<PriceCheckerModifierFilterVariantChangedEventArgs>? ModifierFilterVariantChanged;

    event EventHandler<PriceCheckerModifierExpansionChangedEventArgs>? ModifierExpansionChanged;

    event EventHandler? BaseCriterionToggleRequested;

    event EventHandler<bool>? PinStateChanged;

    event EventHandler<PriceCheckerHorizontalDragEventArgs>? HorizontalDragDelta;

    event EventHandler? HorizontalDragCompleted;

    event EventHandler? HorizontalResizeStarted;

    event EventHandler<PriceCheckerHorizontalResizeEventArgs>? HorizontalResizeDelta;

    event EventHandler? HorizontalResizeCompleted;

    event EventHandler? ResetItemRequested;

    bool IsClosed { get; }

    bool IsPinned { get; }

    PriceCheckerWindowState? CurrentState { get; }

    PriceCheckerPlacement? CurrentPlacement { get; }

    PriceCheckerPlacement? GetDisplayedPlacement();

    void UpdateContent(PriceCheckerWindowState state);

    void UpdateSearch(PriceCheckerSearchViewState state);

    void ApplyPlacement(PriceCheckerPlacement placement);

    void ShowInactive();

    void Close();
}
