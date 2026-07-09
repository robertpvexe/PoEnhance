using System.Runtime.InteropServices;

namespace PoEnhance.App.Infrastructure.Clipboard;

internal sealed class ClipboardSequenceNumberReader
{
    public uint GetCurrentSequenceNumber()
    {
        return GetClipboardSequenceNumber();
    }

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
