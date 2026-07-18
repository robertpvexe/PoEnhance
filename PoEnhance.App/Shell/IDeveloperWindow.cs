using System.Windows;

namespace PoEnhance.App.Shell;

internal interface IDeveloperWindow
{
    bool IsVisible { get; }

    bool ShowInTaskbar { get; set; }

    WindowState WindowState { get; set; }

    IntPtr EnsureHandle();

    bool Activate();

    void Show();

    void Hide();

    void CloseForApplicationExit();
}
