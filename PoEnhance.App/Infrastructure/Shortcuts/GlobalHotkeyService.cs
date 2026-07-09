using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Serilog;

namespace PoEnhance.App.Infrastructure.Shortcuts;

internal sealed class GlobalHotkeyService : IDisposable
{
    private const int HotkeyId = 0x5045;
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private HwndSource? hwndSource;
    private IntPtr windowHandle;
    private bool isDisposed;
    private bool isPathOfExileForeground;
    private bool isRegistered;

    public event EventHandler? Triggered;

    public ShortcutBinding SelectedShortcut { get; private set; } = ShortcutBinding.DefaultPriceChecker;

    public ShortcutRegistrationState RegistrationState { get; private set; }
        = ShortcutRegistrationState.InactiveBecausePathOfExileIsNotForeground;

    public void Attach(Window window)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (hwndSource is not null)
        {
            return;
        }

        windowHandle = new WindowInteropHelper(window).Handle;
        hwndSource = HwndSource.FromHwnd(windowHandle);
        hwndSource?.AddHook(WndProc);
    }

    public void SetShortcut(ShortcutBinding shortcut)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (SelectedShortcut == shortcut)
        {
            return;
        }

        UnregisterHotkey();
        SelectedShortcut = shortcut;
        UpdateRegistrationForForegroundState();
    }

    public void UpdatePathOfExileForegroundState(bool isForeground)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (isPathOfExileForeground == isForeground)
        {
            return;
        }

        isPathOfExileForeground = isForeground;
        UpdateRegistrationForForegroundState();
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        UnregisterHotkey();

        if (hwndSource is not null)
        {
            hwndSource.RemoveHook(WndProc);
            hwndSource = null;
        }

        isDisposed = true;
    }

    private void UpdateRegistrationForForegroundState()
    {
        if (!isPathOfExileForeground)
        {
            UnregisterHotkey();
            RegistrationState = ShortcutRegistrationState.InactiveBecausePathOfExileIsNotForeground;
            return;
        }

        if (isRegistered)
        {
            return;
        }

        RegisterHotkey();
    }

    private void RegisterHotkey()
    {
        if (windowHandle == IntPtr.Zero)
        {
            RegistrationState = ShortcutRegistrationState.RegistrationFailed;
            Log.Warning("Shortcut registration failed because the window handle is not available");
            return;
        }

        var modifiers = ModNoRepeat | (uint)SelectedShortcut.Modifiers;
        if (RegisterHotKey(windowHandle, HotkeyId, modifiers, (uint)SelectedShortcut.PrimaryKey))
        {
            isRegistered = true;
            RegistrationState = ShortcutRegistrationState.Active;
            return;
        }

        int errorCode = Marshal.GetLastWin32Error();
        RegistrationState = ShortcutRegistrationState.RegistrationFailed;
        Log.Warning(
            "Shortcut registration failed for {ShortcutKey}. Win32 error: {Win32ErrorCode}",
            SelectedShortcut,
            errorCode);
    }

    private void UnregisterHotkey()
    {
        if (!isRegistered)
        {
            return;
        }

        if (!UnregisterHotKey(windowHandle, HotkeyId))
        {
            int errorCode = Marshal.GetLastWin32Error();
            Log.Warning("Shortcut unregistration failed. Win32 error: {Win32ErrorCode}", errorCode);
        }

        isRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Triggered?.Invoke(this, EventArgs.Empty);
        }

        return IntPtr.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
