using System.Runtime.InteropServices;
using Serilog;

namespace PoEnhance.App.Infrastructure.Input;

internal sealed class KeyboardInputSender : IQuickUseCommandSender
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const ushort VirtualKeyC = 0x43;
    private const ushort VirtualKeyLeftAlt = 0xA4;
    private const ushort VirtualKeyLeftControl = 0xA2;
    private const ushort VirtualKeyReturn = 0x0D;
    private const int ExpectedInputCount = 6;

    public bool TrySendAdvancedItemDescriptionCopyChord(out uint sentInputCount, out int errorCode)
    {
        var inputs = new[]
        {
            CreateKeyboardInput(VirtualKeyLeftControl, keyUp: false),
            CreateKeyboardInput(VirtualKeyLeftAlt, keyUp: false),
            CreateKeyboardInput(VirtualKeyC, keyUp: false),
            CreateKeyboardInput(VirtualKeyC, keyUp: true),
            CreateKeyboardInput(VirtualKeyLeftAlt, keyUp: true),
            CreateKeyboardInput(VirtualKeyLeftControl, keyUp: true),
        };

        sentInputCount = SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<INPUT>());

        if (sentInputCount == ExpectedInputCount)
        {
            errorCode = 0;
            return true;
        }

        errorCode = Marshal.GetLastWin32Error();
        return false;
    }

    public bool TrySendQuickUseCommand(
        string command,
        bool pressEnter,
        out uint sentInputCount,
        out int errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        var inputs = BuildQuickUseInputSequence(command, pressEnter)
            .Select(stroke => stroke.UnicodeCharacter is char character
                ? CreateUnicodeKeyboardInput(character, stroke.IsKeyUp)
                : CreateKeyboardInput(stroke.VirtualKey, stroke.IsKeyUp))
            .ToArray();

        sentInputCount = SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<INPUT>());
        if (sentInputCount == inputs.Length)
        {
            errorCode = 0;
            Log.Information(
                "Quick Use SendInput completed. ExpectedInputCount={ExpectedInputCount}; SentInputCount={SentInputCount}; Win32Error={Win32Error}; PressEnter={PressEnter}",
                inputs.Length,
                sentInputCount,
                errorCode,
                pressEnter);
            return true;
        }

        errorCode = Marshal.GetLastWin32Error();
        Log.Warning(
            "Quick Use SendInput incomplete. ExpectedInputCount={ExpectedInputCount}; SentInputCount={SentInputCount}; Win32Error={Win32Error}; PressEnter={PressEnter}",
            inputs.Length,
            sentInputCount,
            errorCode,
            pressEnter);
        return false;
    }

    internal static IReadOnlyList<KeyboardInputStroke> BuildQuickUseInputSequence(
        string command,
        bool pressEnter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        var inputs = new List<KeyboardInputStroke>(
            2 + (command.Length * 2) + (pressEnter ? 2 : 0))
        {
            new(VirtualKeyReturn, null, IsKeyUp: false),
            new(VirtualKeyReturn, null, IsKeyUp: true),
        };

        foreach (var character in command)
        {
            inputs.Add(new(0, character, IsKeyUp: false));
            inputs.Add(new(0, character, IsKeyUp: true));
        }

        if (pressEnter)
        {
            inputs.Add(new(VirtualKeyReturn, null, IsKeyUp: false));
            inputs.Add(new(VirtualKeyReturn, null, IsKeyUp: true));
        }

        return inputs;
    }

    private static INPUT CreateKeyboardInput(ushort virtualKey, bool keyUp)
    {
        return new INPUT
        {
            Type = InputKeyboard,
            Keyboard = new KEYBDINPUT
            {
                VirtualKey = virtualKey,
                ScanCode = 0,
                Flags = keyUp ? KeyEventKeyUp : 0,
                Time = 0,
                ExtraInfo = UIntPtr.Zero,
            },
        };
    }

    private static INPUT CreateUnicodeKeyboardInput(char character, bool keyUp)
    {
        return new INPUT
        {
            Type = InputKeyboard,
            Keyboard = new KEYBDINPUT
            {
                VirtualKey = 0,
                ScanCode = character,
                Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0),
                Time = 0,
                ExtraInfo = UIntPtr.Zero,
            },
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint cInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUT
    {
        [FieldOffset(0)]
        public uint Type;

        [FieldOffset(8)]
        public KEYBDINPUT Keyboard;

        [FieldOffset(8)]
        private MOUSEINPUT Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}

internal readonly record struct KeyboardInputStroke(
    ushort VirtualKey,
    char? UnicodeCharacter,
    bool IsKeyUp);
