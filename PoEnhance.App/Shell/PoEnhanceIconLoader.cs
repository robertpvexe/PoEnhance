using System.Drawing;
using System.Windows;

namespace PoEnhance.App.Shell;

internal static class PoEnhanceIconLoader
{
    private static readonly Uri IconResourceUri = new(
        "pack://application:,,,/PoEnhance.App;component/Assets/poenhance.ico",
        UriKind.Absolute);

    public static Icon LoadDrawingIcon()
    {
        var resource = Application.GetResourceStream(IconResourceUri)
            ?? throw new InvalidOperationException("The embedded PoEnhance application icon is unavailable.");

        using (resource.Stream)
        using (var loaded = new Icon(resource.Stream))
        {
            return (Icon)loaded.Clone();
        }
    }
}
