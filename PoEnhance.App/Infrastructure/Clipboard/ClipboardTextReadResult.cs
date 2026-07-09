namespace PoEnhance.App.Infrastructure.Clipboard;

internal sealed record ClipboardTextReadResult(
    ClipboardTextReadStatus Status,
    string? Text = null,
    Exception? Exception = null);
