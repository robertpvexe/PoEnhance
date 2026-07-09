using System.Runtime.InteropServices;

namespace PoEnhance.App.Infrastructure.Input;

internal sealed class KeyboardInputSender
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const ushort VirtualKeyC = 0x43;
    private const ushort VirtualKeyLeftAlt = 0xA4;
    private const ushort VirtualKeyLeftControl = 0xA2;
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
