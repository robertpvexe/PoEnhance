using System.Diagnostics;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class SystemExternalUrlLauncher : IExternalUrlLauncher
{
    public bool TryOpen(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        try
        {
            return Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
            {
                UseShellExecute = true,
            }) is not null;
        }
        catch
        {
            return false;
        }
    }
}
