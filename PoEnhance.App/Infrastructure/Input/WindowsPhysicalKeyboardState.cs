using System.Runtime.InteropServices;

namespace PoEnhance.App.Infrastructure.Input;

internal sealed class WindowsPhysicalKeyboardState : IPhysicalKeyboardState
{
    public bool IsPressed(ushort virtualKey) => GetAsyncKeyState(virtualKey) < 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
