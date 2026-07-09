namespace PoEnhance.App.Infrastructure.Clipboard;

internal sealed class WpfClipboardTextReader
{
    public ClipboardTextReadResult ReadText()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText())
            {
                return new ClipboardTextReadResult(ClipboardTextReadStatus.EmptyOrNoText);
            }

            string text = System.Windows.Clipboard.GetText();
            if (string.IsNullOrEmpty(text))
            {
                return new ClipboardTextReadResult(ClipboardTextReadStatus.EmptyOrNoText);
            }

            return new ClipboardTextReadResult(ClipboardTextReadStatus.TextAvailable, text);
        }
        catch (Exception exception)
        {
            return new ClipboardTextReadResult(
                ClipboardTextReadStatus.AccessFailed,
                Exception: exception);
        }
    }
}
