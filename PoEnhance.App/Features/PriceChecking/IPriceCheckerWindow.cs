namespace PoEnhance.App.Features.PriceChecking;

internal interface IPriceCheckerWindow
{
    event EventHandler? Closed;

    event EventHandler? PanelActivated;

    event EventHandler? PanelDeactivated;

    event EventHandler? PanelInteraction;

    event EventHandler? SearchRequested;

    event EventHandler<PriceCheckerLeagueChangedEventArgs>? LeagueChanged;

    event EventHandler<bool>? PinStateChanged;

    event EventHandler<PriceCheckerHorizontalDragEventArgs>? HorizontalDragDelta;

    event EventHandler? HorizontalDragCompleted;

    event EventHandler? ResetPositionRequested;

    bool IsClosed { get; }

    bool IsPinned { get; }

    PriceCheckerWindowState? CurrentState { get; }

    PriceCheckerPlacement? CurrentPlacement { get; }

    void UpdateContent(PriceCheckerWindowState state);

    void UpdateSearch(PriceCheckerSearchViewState state);

    void ApplyPlacement(PriceCheckerPlacement placement);

    void ShowInactive();

    void Close();
}
