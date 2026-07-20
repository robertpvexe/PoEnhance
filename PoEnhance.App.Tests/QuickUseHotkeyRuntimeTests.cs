using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;
using PoEnhance.App.Features.QuickUse;
using PoEnhance.App.Infrastructure.Input;
using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Tests;

public sealed class QuickUseHotkeyRuntimeTests
{
    [Fact]
    public void QuickUseHotkeyIds_AreStableUniqueAndDoNotOverlapFixedApplicationIds()
    {
        var quickUseIds = Enumerable.Range(0, 6)
            .Select(index => GlobalHotkeyService.FirstQuickUseHotkeyId + index)
            .ToArray();

        Assert.Equal(quickUseIds.Length, quickUseIds.Distinct().Count());
        Assert.DoesNotContain(GlobalHotkeyService.PriceCheckerHotkeyId, quickUseIds);
        Assert.DoesNotContain(GlobalHotkeyService.DeveloperWindowHotkeyId, quickUseIds);
        Assert.DoesNotContain(GlobalHotkeyService.MultitoolMenuHotkeyId, quickUseIds);
    }

    [Fact]
    public void Runtime_ExecutesOnlyWithPathOfExileForegroundAndMenuHidden()
    {
        RunOnSta(() =>
        {
            var isForeground = false;
            var isMenuVisible = false;
            var services = new List<FakeHotkeyService>();
            var sender = new FakeCommandSender();
            using var runtime = CreateRuntime(
                ApplicationLeagueSetting.CreateTransient(),
                sender,
                services,
                () => isForeground,
                () => isMenuVisible);
            runtime.Attach(new Window());

            Assert.Equal(3, runtime.ActiveBindingCount);
            Assert.Equal(
                [ShortcutKey.F5, ShortcutKey.F6, ShortcutKey.F7],
                services.Select(service => service.SelectedShortcut.PrimaryKey));
            services[0].RaiseTriggered();
            Assert.Empty(sender.Calls);

            isForeground = true;
            runtime.UpdatePathOfExileForegroundState(true);
            services[0].RaiseTriggered();
            Assert.Single(sender.Calls);
            Assert.Equal(("/hideout", true), sender.Calls[0]);

            isMenuVisible = true;
            services[0].RaiseTriggered();
            Assert.Single(sender.Calls);

            isMenuVisible = false;
            services[0].RaiseTriggered();
            Assert.Equal(2, sender.Calls.Count);
        });
    }

    [Fact]
    public void Runtime_RespectsSubmitOptionAndInvokesOneFixedCommandPerHotkey()
    {
        RunOnSta(() =>
        {
            var setting = ApplicationLeagueSetting.CreateTransient("Standard");
            QuickUseCommandSetting[] commands =
            [
                new("/submitted", true, new ShortcutBinding(ShortcutKey.F8, ShortcutModifiers.None), false),
                new("/left open", false, new ShortcutBinding(ShortcutKey.F9, ShortcutModifiers.None), true),
            ];
            Assert.True(setting.TrySaveQuickUseCommands(commands, out var error), error);
            var services = new List<FakeHotkeyService>();
            var sender = new FakeCommandSender();
            using var runtime = CreateRuntime(setting, sender, services, () => true, () => false);
            runtime.Attach(new Window());
            runtime.UpdatePathOfExileForegroundState(true);

            services[0].RaiseTriggered();
            services[1].RaiseTriggered();

            Assert.Equal(
                [("/submitted", true), ("/left open", false)],
                sender.Calls);
        });
    }

    [Fact]
    public void SavedSettings_RebuildLiveRegistrationsAndDisposeOldOnShutdown()
    {
        RunOnSta(() =>
        {
            var setting = ApplicationLeagueSetting.CreateTransient("Standard");
            var services = new List<FakeHotkeyService>();
            var runtime = CreateRuntime(
                setting,
                new FakeCommandSender(),
                services,
                () => true,
                () => false);
            runtime.Attach(new Window());
            var initialServices = services.ToArray();

            QuickUseCommandSetting[] replacement =
            [
                new("/replacement", true, new ShortcutBinding(ShortcutKey.F10, ShortcutModifiers.None), false),
            ];
            Assert.True(setting.TrySaveQuickUseCommands(replacement, out var error), error);

            Assert.All(initialServices, service => Assert.Equal(1, service.DisposeCount));
            Assert.Equal(1, runtime.ActiveBindingCount);
            var replacementService = services[^1];
            Assert.Equal(0, replacementService.DisposeCount);

            runtime.Dispose();
            Assert.Equal(1, replacementService.DisposeCount);
            runtime.Dispose();
            Assert.Equal(1, replacementService.DisposeCount);
        });
    }

    [Fact]
    public void CaptureSuspension_UsesTheExistingHotkeyServices()
    {
        RunOnSta(() =>
        {
            var services = new List<FakeHotkeyService>();
            using var runtime = CreateRuntime(
                ApplicationLeagueSetting.CreateTransient(),
                new FakeCommandSender(),
                services,
                () => true,
                () => false);
            runtime.Attach(new Window());
            runtime.UpdatePathOfExileForegroundState(true);

            runtime.SetSuspended(true);
            Assert.All(services, service => Assert.Equal(
                ShortcutRegistrationState.NotAttached,
                service.RegistrationState));

            runtime.SetSuspended(false);
            Assert.All(services, service => Assert.Equal(
                ShortcutRegistrationState.InactiveBecausePathOfExileIsNotForeground,
                service.RegistrationState));
        });
    }

    [Fact]
    public void GlobalService_ReportsRegistrationFailureWithDynamicQuickUseId()
    {
        RunOnSta(() =>
        {
            var attemptedId = 0;
            uint attemptedKey = 0;
            using var service = new GlobalHotkeyService(
                GlobalHotkeyService.FirstQuickUseHotkeyId,
                requiresPathOfExileForeground: true,
                (_, id, _, key) =>
                {
                    attemptedId = id;
                    attemptedKey = key;
                    Marshal.SetLastPInvokeError(1409);
                    return false;
                },
                (_, _) => true);
            service.SetShortcut(new ShortcutBinding(ShortcutKey.F5, ShortcutModifiers.None));
            service.Attach(new Window());

            service.UpdatePathOfExileForegroundState(true);

            Assert.Equal(GlobalHotkeyService.FirstQuickUseHotkeyId, attemptedId);
            Assert.Equal((uint)ShortcutKey.F5, attemptedKey);
            Assert.Equal(ShortcutRegistrationState.RegistrationFailed, service.RegistrationState);
            Assert.Equal(1409, service.LastRegistrationErrorCode);
        });
    }

    [Fact]
    public void GlobalService_DynamicWmHotkeyIdRoutesOnlyToItsCommand()
    {
        using var service = GlobalHotkeyService.CreateQuickUseService(
            commandIndex: 2,
            shortcut: new ShortcutBinding(ShortcutKey.F7, ShortcutModifiers.None));
        var triggerCount = 0;
        service.Triggered += (_, _) => triggerCount++;

        Assert.False(service.TryDispatchWindowMessage(
            GlobalHotkeyService.WmHotkey,
            GlobalHotkeyService.FirstQuickUseHotkeyId));
        Assert.True(service.TryDispatchWindowMessage(
            GlobalHotkeyService.WmHotkey,
            GlobalHotkeyService.FirstQuickUseHotkeyId + 2));
        Assert.False(service.TryDispatchWindowMessage(
            message: 0,
            receivedHotkeyId: GlobalHotkeyService.FirstQuickUseHotkeyId + 2));
        Assert.Equal(1, triggerCount);
    }

    [Fact]
    public void OneFailedBinding_DoesNotPreventOtherPresetsFromBecomingActive()
    {
        RunOnSta(() =>
        {
            var services = new List<FakeHotkeyService>();
            using var runtime = new QuickUseHotkeyRuntime(
                ApplicationLeagueSetting.CreateTransient(),
                new FakeCommandSender(),
                (index, binding) =>
                {
                    var service = new FakeHotkeyService(binding, shouldFail: index == 0);
                    services.Add(service);
                    return service;
                },
                () => true,
                () => false);
            runtime.Attach(new Window());

            runtime.UpdatePathOfExileForegroundState(true);

            Assert.Equal(3, services.Count);
            Assert.Equal(ShortcutRegistrationState.RegistrationFailed, services[0].RegistrationState);
            Assert.Equal(ShortcutRegistrationState.Active, services[1].RegistrationState);
            Assert.Equal(ShortcutRegistrationState.Active, services[2].RegistrationState);
        });
    }

    [Fact]
    public void QuickUseInputPath_DoesNotUseClipboard()
    {
        var senderCode = ReadRepositoryFile(
            "PoEnhance.App",
            "Infrastructure",
            "Input",
            "KeyboardInputSender.cs");
        var runtimeCode = ReadRepositoryFile(
            "PoEnhance.App",
            "Features",
            "QuickUse",
            "QuickUseHotkeyRuntime.cs");

        Assert.DoesNotContain("Clipboard", senderCode, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Clipboard", runtimeCode, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("KeyEventUnicode", senderCode, StringComparison.Ordinal);
        Assert.Contains("VirtualKeyReturn", senderCode, StringComparison.Ordinal);
    }

    [Fact]
    public void KeyboardSender_SubmitEnabledAddsFinalEnterAndDisabledLeavesChatOpen()
    {
        var submitted = KeyboardInputSender.BuildQuickUseInputSequence("/x", pressEnter: true);
        var leftOpen = KeyboardInputSender.BuildQuickUseInputSequence("/x", pressEnter: false);

        Assert.Equal(8, submitted.Count);
        Assert.Equal(6, leftOpen.Count);
        Assert.Equal(0x0D, submitted[0].VirtualKey);
        Assert.False(submitted[0].IsKeyUp);
        Assert.Equal(0x0D, submitted[1].VirtualKey);
        Assert.True(submitted[1].IsKeyUp);
        Assert.Equal('/', submitted[2].UnicodeCharacter);
        Assert.Equal('x', submitted[4].UnicodeCharacter);
        Assert.Equal(0x0D, submitted[^2].VirtualKey);
        Assert.Equal(0x0D, submitted[^1].VirtualKey);
        Assert.Equal(0, leftOpen[^2].VirtualKey);
        Assert.Equal('x', leftOpen[^2].UnicodeCharacter);
        Assert.Equal('x', leftOpen[^1].UnicodeCharacter);
        Assert.False(leftOpen[^2].IsKeyUp);
        Assert.True(leftOpen[^1].IsKeyUp);
    }

    private static QuickUseHotkeyRuntime CreateRuntime(
        ApplicationLeagueSetting setting,
        FakeCommandSender sender,
        List<FakeHotkeyService> services,
        Func<bool> isForeground,
        Func<bool> isMenuVisible)
    {
        return new QuickUseHotkeyRuntime(
            setting,
            sender,
            (_, binding) =>
            {
                var service = new FakeHotkeyService(binding);
                services.Add(service);
                return service;
            },
            isForeground,
            isMenuVisible);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine([directory.FullName, .. pathParts]));
    }

    private static void RunOnSta(Action action)
    {
        Exception? captured = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                captured = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            throw new Xunit.Sdk.XunitException(captured.ToString());
        }
    }

    private sealed class FakeCommandSender : IQuickUseCommandSender
    {
        public List<(string Command, bool PressEnter)> Calls { get; } = [];

        public bool TrySendQuickUseCommand(
            string command,
            bool pressEnter,
            out uint sentInputCount,
            out int errorCode)
        {
            Calls.Add((command, pressEnter));
            sentInputCount = 1;
            errorCode = 0;
            return true;
        }
    }

    private sealed class FakeHotkeyService(
        ShortcutBinding shortcut,
        bool shouldFail = false) : IGlobalHotkeyService
    {
        public event EventHandler? Triggered;

        public ShortcutBinding SelectedShortcut { get; private set; } = shortcut;

        public ShortcutRegistrationState RegistrationState { get; private set; }
            = ShortcutRegistrationState.NotAttached;

        public bool RequiresPathOfExileForeground => true;

        public bool SuppressesKeyRepeat => true;

        public int DisposeCount { get; private set; }

        public void Attach(Window window)
        {
            RegistrationState = ShortcutRegistrationState.InactiveBecausePathOfExileIsNotForeground;
        }

        public void SetShortcut(ShortcutBinding newShortcut)
        {
            SelectedShortcut = newShortcut;
        }

        public void SetSuspended(bool isSuspended)
        {
            RegistrationState = isSuspended
                ? ShortcutRegistrationState.NotAttached
                : ShortcutRegistrationState.InactiveBecausePathOfExileIsNotForeground;
        }

        public void UpdatePathOfExileForegroundState(bool isForeground)
        {
            RegistrationState = !isForeground
                ? ShortcutRegistrationState.InactiveBecausePathOfExileIsNotForeground
                : shouldFail
                    ? ShortcutRegistrationState.RegistrationFailed
                    : ShortcutRegistrationState.Active;
        }

        public void RaiseTriggered()
        {
            Triggered?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            DisposeCount++;
        }
    }
}
