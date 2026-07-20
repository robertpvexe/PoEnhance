using System.Windows;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Shell;

internal interface IMultitoolMenuWindow
{
    event EventHandler? ExitRequested;

    bool IsVisible { get; }

    bool ShowInTaskbar { get; set; }

    WindowState WindowState { get; set; }

    IntPtr EnsureHandle();

    bool Activate();

    void Show();

    void Hide();

    void PositionForOpen(PathOfExileClientBounds? pathOfExileBounds);

    void UpdateRuntimeState(
        bool isPathOfExileRunning,
        ShortcutRegistrationState priceCheckerRegistrationState);

    void CloseForApplicationExit();
}
