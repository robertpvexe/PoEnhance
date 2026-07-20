using System.Threading;
using System.Windows.Input;
using PoEnhance.App.Infrastructure.Settings;
using PoEnhance.App.Infrastructure.Shortcuts;
using PoEnhance.App.Shell;

namespace PoEnhance.App.Tests;

public sealed class QuickUseSettingsTests
{
    [Fact]
    public void SettingsMarkup_UsesThemedComboPopupAndAdjacentLeagueApplyLayout()
    {
        var xaml = ReadRepositoryFile("PoEnhance.App", "Shell", "MultitoolMenuWindow.xaml");

        Assert.Contains("x:Key=\"SettingsComboBoxStyle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Key=\"SettingsComboBoxItemStyle\"", xaml, StringComparison.Ordinal);
        Assert.Contains("<ControlTemplate TargetType=\"ComboBox\">", xaml, StringComparison.Ordinal);
        Assert.Contains("<Popup x:Name=\"PART_Popup\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"#202431\"", xaml, StringComparison.Ordinal);
        Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", xaml, StringComparison.Ordinal);
        Assert.Contains(
            "<Button x:Name=\"ApplyLeagueButton\"\n                                    Grid.Row=\"1\"\n                                    Grid.Column=\"2\"",
            xaml,
            StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CustomLeagueTextBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Grid.Row=\"1\"\n                                     Grid.Column=\"1\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public void CustomRows_CanBeAddedAndRemovedWhilePresetsCannotBeRemoved()
    {
        RunOnSta(() =>
        {
            var window = new MultitoolMenuWindow(ApplicationLeagueSetting.CreateTransient());

            Assert.Equal(6, window.PendingQuickUseCommands.Count);
            window.AddPendingQuickUseCommand();
            Assert.Equal(7, window.PendingQuickUseCommands.Count);
            Assert.True(window.RemovePendingQuickUseCommand(6));
            Assert.Equal(6, window.PendingQuickUseCommands.Count);
            Assert.False(window.RemovePendingQuickUseCommand(0));
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void HotkeyCapture_SupportsAssignmentCancellationAndClearing()
    {
        RunOnSta(() =>
        {
            var window = new MultitoolMenuWindow(ApplicationLeagueSetting.CreateTransient());

            window.BeginHotkeyCapture(0);
            Assert.True(window.CapturePendingHotkey(Key.F8, ModifierKeys.Control));
            Assert.Equal(
                new ShortcutBinding(ShortcutKey.F8, ShortcutModifiers.Control),
                window.PendingQuickUseCommands[0].Hotkey);

            window.BeginHotkeyCapture(0);
            Assert.True(window.CapturePendingHotkey(Key.Escape, ModifierKeys.None));
            Assert.Equal(
                new ShortcutBinding(ShortcutKey.F8, ShortcutModifiers.Control),
                window.PendingQuickUseCommands[0].Hotkey);

            window.BeginHotkeyCapture(0);
            Assert.True(window.CapturePendingHotkey(Key.Back, ModifierKeys.None));
            Assert.Null(window.PendingQuickUseCommands[0].Hotkey);
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void HotkeyCapture_NotifiesHostToSuspendAndRestoreExistingRegistrations()
    {
        RunOnSta(() =>
        {
            var window = new MultitoolMenuWindow(ApplicationLeagueSetting.CreateTransient());
            var states = new List<bool>();
            window.HotkeyCaptureStateChanged += (_, isCapturing) => states.Add(isCapturing);

            window.BeginHotkeyCapture(0);
            Assert.True(window.CapturePendingHotkey(Key.Escape, ModifierKeys.None));

            Assert.Equal([true, false], states);
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void HotkeyCapture_RejectsBareModifiersDuplicatesAndReservedBindings()
    {
        RunOnSta(() =>
        {
            var window = new MultitoolMenuWindow(ApplicationLeagueSetting.CreateTransient());

            window.BeginHotkeyCapture(1);
            Assert.False(window.CapturePendingHotkey(Key.LeftCtrl, ModifierKeys.Control));
            Assert.Contains("non-modifier", window.QuickUseFeedback, StringComparison.Ordinal);
            Assert.False(window.CapturePendingHotkey(Key.F5, ModifierKeys.None));
            Assert.Contains("already assigned", window.QuickUseFeedback, StringComparison.Ordinal);
            Assert.False(window.CapturePendingHotkey(Key.D, ModifierKeys.Control));
            Assert.Contains("reserved", window.QuickUseFeedback, StringComparison.Ordinal);
            Assert.True(window.CapturePendingHotkey(Key.Escape, ModifierKeys.None));
            window.CloseForApplicationExit();
        });
    }

    [Fact]
    public void PendingRows_DoNotChangeActiveSettingsUntilSave()
    {
        RunOnSta(() =>
        {
            var setting = ApplicationLeagueSetting.CreateTransient("Standard");
            var window = new MultitoolMenuWindow(setting);
            var changeCount = 0;
            setting.QuickUseCommandsChanged += (_, _) => changeCount++;

            window.AddPendingQuickUseCommand();
            window.BeginHotkeyCapture(0);
            Assert.True(window.CapturePendingHotkey(Key.F8, ModifierKeys.None));

            Assert.Equal(6, setting.QuickUseCommands.Count);
            Assert.Equal(ShortcutKey.F5, setting.QuickUseCommands[0].Hotkey?.PrimaryKey);
            Assert.Equal(0, changeCount);

            Assert.True(window.SavePendingQuickUseCommands());
            Assert.Equal(7, setting.QuickUseCommands.Count);
            Assert.Equal(ShortcutKey.F8, setting.QuickUseCommands[0].Hotkey?.PrimaryKey);
            Assert.Equal(1, changeCount);
            Assert.Equal("Quick Use settings saved.", window.QuickUseFeedback);
            window.CloseForApplicationExit();
        });
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(RepositoryPath(pathParts));
    }

    private static string RepositoryPath(params string[] pathParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PoEnhance.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine([directory.FullName, .. pathParts]);
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
}
