using System.Windows;
using PoEnhance.App.Infrastructure.Input;
using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Shortcuts;
using Serilog;

namespace PoEnhance.App.Features.QuickUse;

internal sealed class QuickUseHotkeyRuntime : IDisposable
{
    private readonly ApplicationLeagueSetting settings;
    private readonly IQuickUseCommandSender commandSender;
    private readonly Func<int, ShortcutBinding, IGlobalHotkeyService> createHotkeyService;
    private readonly Func<bool> isPathOfExileForeground;
    private readonly Func<bool> isMultitoolMenuVisible;
    private readonly List<ActiveCommand> activeCommands = [];
    private Window? attachmentWindow;
    private bool currentForegroundState;
    private bool isSuspended;
    private bool isDisposed;

    public QuickUseHotkeyRuntime(
        ApplicationLeagueSetting settings,
        IQuickUseCommandSender commandSender,
        Func<bool> isPathOfExileForeground,
        Func<bool> isMultitoolMenuVisible)
        : this(
            settings,
            commandSender,
            GlobalHotkeyService.CreateQuickUseService,
            isPathOfExileForeground,
            isMultitoolMenuVisible)
    {
    }

    internal QuickUseHotkeyRuntime(
        ApplicationLeagueSetting settings,
        IQuickUseCommandSender commandSender,
        Func<int, ShortcutBinding, IGlobalHotkeyService> createHotkeyService,
        Func<bool> isPathOfExileForeground,
        Func<bool> isMultitoolMenuVisible)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.commandSender = commandSender ?? throw new ArgumentNullException(nameof(commandSender));
        this.createHotkeyService = createHotkeyService
            ?? throw new ArgumentNullException(nameof(createHotkeyService));
        this.isPathOfExileForeground = isPathOfExileForeground
            ?? throw new ArgumentNullException(nameof(isPathOfExileForeground));
        this.isMultitoolMenuVisible = isMultitoolMenuVisible
            ?? throw new ArgumentNullException(nameof(isMultitoolMenuVisible));
        settings.QuickUseCommandsChanged += OnQuickUseCommandsChanged;
    }

    internal int ActiveBindingCount => activeCommands.Count;

    public void Attach(Window window)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        attachmentWindow = window ?? throw new ArgumentNullException(nameof(window));
        RebuildRegistrations();
    }

    public void UpdatePathOfExileForegroundState(bool isForeground)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        currentForegroundState = isForeground;
        foreach (var activeCommand in activeCommands)
        {
            activeCommand.Service.UpdatePathOfExileForegroundState(isForeground);
        }
    }

    public void SetSuspended(bool suspended)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        isSuspended = suspended;
        foreach (var activeCommand in activeCommands)
        {
            activeCommand.Service.SetSuspended(suspended);
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        settings.QuickUseCommandsChanged -= OnQuickUseCommandsChanged;
        DisposeRegistrations();
        isDisposed = true;
    }

    private void OnQuickUseCommandsChanged(object? sender, EventArgs e)
    {
        RebuildRegistrations();
    }

    private void RebuildRegistrations()
    {
        DisposeRegistrations();
        if (attachmentWindow is null)
        {
            return;
        }

        var commands = settings.QuickUseCommands;
        Log.Information(
            "Quick Use active settings snapshot received. Rows={QuickUseRows}",
            commands.Count);
        for (var index = 0; index < commands.Count; index++)
        {
            var command = commands[index];
            if (command.Hotkey is null || string.IsNullOrWhiteSpace(command.Command))
            {
                continue;
            }

            var service = createHotkeyService(index, command.Hotkey);
            var activeCommand = new ActiveCommand(service, command);
            service.Triggered += activeCommand.OnTriggered;
            activeCommand.Triggered += OnCommandTriggered;
            service.Attach(attachmentWindow);
            service.UpdatePathOfExileForegroundState(currentForegroundState);
            service.SetSuspended(isSuspended);
            activeCommands.Add(activeCommand);
            Log.Information(
                "Quick Use binding activated. RowIndex={RowIndex}; Shortcut={Shortcut}",
                index,
                command.Hotkey);
        }

        Log.Information(
            "Quick Use active registrations refreshed. ActiveBindings={ActiveBindings}",
            activeCommands.Count);
    }

    private void OnCommandTriggered(object? sender, QuickUseCommandSetting command)
    {
        if (!isPathOfExileForeground())
        {
            Log.Information(
                "Quick Use execution blocked because Path of Exile is not foreground. Shortcut={Shortcut}",
                command.Hotkey);
            return;
        }

        if (isMultitoolMenuVisible())
        {
            Log.Information(
                "Quick Use execution blocked because the multitool menu is visible. Shortcut={Shortcut}",
                command.Hotkey);
            return;
        }

        Log.Information(
            "Quick Use command resolved. Shortcut={Shortcut}; CommandLength={CommandLength}; PressEnter={PressEnter}",
            command.Hotkey,
            command.Command.Length,
            command.PressEnter);
        if (!commandSender.TrySendQuickUseCommand(
                command.Command,
                command.PressEnter,
                out var sentInputCount,
                out var errorCode))
        {
            Log.Warning(
                "Quick Use command input failed after {SentInputCount} inputs. Win32 error: {Win32ErrorCode}",
                sentInputCount,
                errorCode);
            return;
        }

        Log.Information(
            "Quick Use command input completed. Shortcut={Shortcut}; SentInputCount={SentInputCount}; PressEnter={PressEnter}",
            command.Hotkey,
            sentInputCount,
            command.PressEnter);
    }

    private void DisposeRegistrations()
    {
        foreach (var activeCommand in activeCommands)
        {
            activeCommand.Service.Triggered -= activeCommand.OnTriggered;
            activeCommand.Triggered -= OnCommandTriggered;
            activeCommand.Service.Dispose();
        }

        activeCommands.Clear();
    }

    private sealed class ActiveCommand(
        IGlobalHotkeyService service,
        QuickUseCommandSetting command)
    {
        public event EventHandler<QuickUseCommandSetting>? Triggered;

        public IGlobalHotkeyService Service { get; } = service;

        public void OnTriggered(object? sender, EventArgs e)
        {
            Triggered?.Invoke(this, command);
        }
    }
}
