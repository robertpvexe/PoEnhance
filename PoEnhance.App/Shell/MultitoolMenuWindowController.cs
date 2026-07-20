using System.Runtime.InteropServices;
using System.Windows;
using PoEnhance.App.Features.PriceChecking;
using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Shell;

internal sealed class MultitoolMenuWindowController
{
    private readonly IMultitoolMenuWindow window;
    private readonly IPathOfExileClientBoundsProvider clientBoundsProvider;
    private readonly Func<bool> confirmExit;
    private readonly Func<IntPtr, bool> bringToForeground;

    public MultitoolMenuWindowController(
        MultitoolMenuWindow window,
        IPathOfExileClientBoundsProvider clientBoundsProvider)
        : this(
            window,
            clientBoundsProvider,
            () => ExitConfirmationDialog.Confirm(window),
            SetForegroundWindow)
    {
    }

    internal MultitoolMenuWindowController(
        IMultitoolMenuWindow window,
        IPathOfExileClientBoundsProvider clientBoundsProvider,
        Func<bool> confirmExit,
        Func<IntPtr, bool> bringToForeground)
    {
        this.window = window;
        this.clientBoundsProvider = clientBoundsProvider;
        this.confirmExit = confirmExit;
        this.bringToForeground = bringToForeground;
        window.ExitRequested += OnExitRequested;
    }

    public event EventHandler? ConfirmedExitRequested;

    public bool IsVisible => window.IsVisible;

    public void Toggle()
    {
        if (window.IsVisible)
        {
            Hide();
            return;
        }

        ShowAndActivate();
    }

    public void ShowAndActivate()
    {
        var bounds = clientBoundsProvider.TryGetClientBounds(out var clientBounds)
            ? clientBounds
            : null;
        window.PositionForOpen(bounds);
        window.ShowInTaskbar = true;
        if (!window.IsVisible)
        {
            window.Show();
        }

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        _ = window.Activate();
        _ = bringToForeground(window.EnsureHandle());
    }

    public void Hide()
    {
        if (window.IsVisible)
        {
            window.Hide();
        }

        window.ShowInTaskbar = false;
    }

    public void UpdateRuntimeState(
        bool isPathOfExileRunning,
        ShortcutRegistrationState priceCheckerRegistrationState)
    {
        window.UpdateRuntimeState(isPathOfExileRunning, priceCheckerRegistrationState);
    }

    public void CloseForApplicationExit()
    {
        window.ExitRequested -= OnExitRequested;
        window.CloseForApplicationExit();
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        if (confirmExit())
        {
            ConfirmedExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);
}
