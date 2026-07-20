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
    internal const int FirstQuickUseHotkeyId = 0x5100;
    internal const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;

    private readonly int hotkeyId;
    private readonly bool requiresPathOfExileForeground;
    private readonly Func<IntPtr, int, uint, uint, bool> registerHotKey;
    private readonly Func<IntPtr, int, bool> unregisterHotKey;
    private HwndSource? hwndSource;
    private IntPtr windowHandle;
    private bool isDisposed;
    private bool isPathOfExileForeground;
    private bool isRegistered;
    private bool isSuspended;

    public event EventHandler? Triggered;

    public GlobalHotkeyService()
        : this(PriceCheckerHotkeyId, requiresPathOfExileForeground: true)
    {
    }

    internal GlobalHotkeyService(int hotkeyId, bool requiresPathOfExileForeground)
        : this(
            hotkeyId,
            requiresPathOfExileForeground,
            RegisterHotKeyNative,
            UnregisterHotKeyNative)
    {
    }

    internal GlobalHotkeyService(
        int hotkeyId,
        bool requiresPathOfExileForeground,
        Func<IntPtr, int, uint, uint, bool> registerHotKey,
        Func<IntPtr, int, bool> unregisterHotKey)
    {
        this.hotkeyId = hotkeyId;
        this.requiresPathOfExileForeground = requiresPathOfExileForeground;
        this.registerHotKey = registerHotKey ?? throw new ArgumentNullException(nameof(registerHotKey));
        this.unregisterHotKey = unregisterHotKey ?? throw new ArgumentNullException(nameof(unregisterHotKey));
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

    public static GlobalHotkeyService CreateQuickUseService(
        int commandIndex,
        ShortcutBinding shortcut)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(commandIndex);
        ArgumentNullException.ThrowIfNull(shortcut);
        var service = new GlobalHotkeyService(
            FirstQuickUseHotkeyId + commandIndex,
            requiresPathOfExileForeground: true);
        service.SetShortcut(shortcut);
        return service;
    }

    public ShortcutBinding SelectedShortcut { get; private set; } = ShortcutBinding.DefaultPriceChecker;

    public ShortcutRegistrationState RegistrationState { get; private set; }
        = ShortcutRegistrationState.NotAttached;

    public bool RequiresPathOfExileForeground => requiresPathOfExileForeground;

    public bool SuppressesKeyRepeat => true;

    internal int HotkeyId => hotkeyId;

    internal int LastRegistrationErrorCode { get; private set; }

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

    public void SetSuspended(bool suspended)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (isSuspended == suspended)
        {
            return;
        }

        isSuspended = suspended;
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

        if (isSuspended)
        {
            UnregisterHotkey();
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
        Log.Information(
            "Hotkey registration attempted. HotkeyId={HotkeyId}; Shortcut={Shortcut}",
            hotkeyId,
            SelectedShortcut);
        if (registerHotKey(windowHandle, hotkeyId, modifiers, (uint)SelectedShortcut.PrimaryKey))
        {
            isRegistered = true;
            LastRegistrationErrorCode = 0;
            RegistrationState = ShortcutRegistrationState.Active;
            Log.Information(
                "Hotkey registration succeeded. HotkeyId={HotkeyId}; Shortcut={Shortcut}",
                hotkeyId,
                SelectedShortcut);
            return;
        }

        int errorCode = Marshal.GetLastWin32Error();
        LastRegistrationErrorCode = errorCode;
        RegistrationState = ShortcutRegistrationState.RegistrationFailed;
        Log.Warning(
            "Hotkey registration failed. HotkeyId={HotkeyId}; Shortcut={Shortcut}; Win32Error={Win32ErrorCode}; Reason={FailureReason}",
            hotkeyId,
            SelectedShortcut,
            errorCode,
            errorCode == 1409 ? "AlreadyRegistered" : "Win32Error");
    }

    private void UnregisterHotkey()
    {
        if (!isRegistered)
        {
            return;
        }

        if (!unregisterHotKey(windowHandle, hotkeyId))
        {
            int errorCode = Marshal.GetLastWin32Error();
            Log.Warning(
                "Hotkey unregistration failed. HotkeyId={HotkeyId}; Shortcut={Shortcut}; Win32Error={Win32ErrorCode}",
                hotkeyId,
                SelectedShortcut,
                errorCode);
        }
        else
        {
            Log.Information(
                "Hotkey unregistration succeeded. HotkeyId={HotkeyId}; Shortcut={Shortcut}",
                hotkeyId,
                SelectedShortcut);
        }

        isRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (TryDispatchWindowMessage(message, wParam.ToInt32()))
        {
            handled = true;
        }

        return IntPtr.Zero;
    }

    internal bool TryDispatchWindowMessage(int message, int receivedHotkeyId)
    {
        if (message != WmHotkey || receivedHotkeyId != hotkeyId)
        {
            return false;
        }

        Log.Information(
            "WM_HOTKEY received. HotkeyId={HotkeyId}; Shortcut={Shortcut}",
            hotkeyId,
            SelectedShortcut);
        Triggered?.Invoke(this, EventArgs.Empty);
        return true;
    }

    [DllImport("user32.dll", EntryPoint = "RegisterHotKey", SetLastError = true)]
    private static extern bool RegisterHotKeyNative(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", EntryPoint = "UnregisterHotKey", SetLastError = true)]
    private static extern bool UnregisterHotKeyNative(IntPtr hWnd, int id);
}
