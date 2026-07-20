using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Serilog;

namespace PoEnhance.App.Infrastructure.Shortcuts;

internal sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    internal const int PriceCheckerHotkeyId = 0x5045;
    internal const int DeveloperWindowHotkeyId = 0x5046;
    internal const int MultitoolMenuHotkeyId = 0x5047;
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private readonly int hotkeyId;
    private readonly bool requiresPathOfExileForeground;
    private HwndSource? hwndSource;
    private IntPtr windowHandle;
    private bool isDisposed;
    private bool isPathOfExileForeground;
    private bool isRegistered;

    public event EventHandler? Triggered;

    public GlobalHotkeyService()
        : this(PriceCheckerHotkeyId, requiresPathOfExileForeground: true)
    {
    }

    internal GlobalHotkeyService(int hotkeyId, bool requiresPathOfExileForeground)
    {
        this.hotkeyId = hotkeyId;
        this.requiresPathOfExileForeground = requiresPathOfExileForeground;
    }

    public static GlobalHotkeyService CreateDeveloperWindowService()
    {
        var service = new GlobalHotkeyService(
            DeveloperWindowHotkeyId,
            requiresPathOfExileForeground: false);
        service.SetShortcut(ShortcutBinding.DeveloperWindow);
        return service;
    }

    public static GlobalHotkeyService CreateMultitoolMenuService()
    {
        var service = new GlobalHotkeyService(
            MultitoolMenuHotkeyId,
            requiresPathOfExileForeground: true);
        service.SetShortcut(ShortcutBinding.MultitoolMenu);
        return service;
    }

    public ShortcutBinding SelectedShortcut { get; private set; } = ShortcutBinding.DefaultPriceChecker;

    public ShortcutRegistrationState RegistrationState { get; private set; }
        = ShortcutRegistrationState.NotAttached;

    public bool RequiresPathOfExileForeground => requiresPathOfExileForeground;

    public bool SuppressesKeyRepeat => true;

    public void Attach(Window window)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);

        if (hwndSource is not null)
        {
            return;
        }

        windowHandle = new WindowInteropHelper(window).EnsureHandle();
        hwndSource = HwndSource.FromHwnd(windowHandle);
        hwndSource?.AddHook(WndProc);
        UpdateRegistrationForForegroundState();
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
        if (windowHandle == IntPtr.Zero)
        {
            RegistrationState = ShortcutRegistrationState.NotAttached;
            return;
        }

        if (requiresPathOfExileForeground && !isPathOfExileForeground)
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
        if (RegisterHotKey(windowHandle, hotkeyId, modifiers, (uint)SelectedShortcut.PrimaryKey))
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

        if (!UnregisterHotKey(windowHandle, hotkeyId))
        {
            int errorCode = Marshal.GetLastWin32Error();
            Log.Warning("Shortcut unregistration failed. Win32 error: {Win32ErrorCode}", errorCode);
        }

        isRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == hotkeyId)
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
