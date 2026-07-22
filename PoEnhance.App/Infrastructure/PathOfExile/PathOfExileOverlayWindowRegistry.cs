namespace PoEnhance.App.Infrastructure.PathOfExile;

internal sealed class PathOfExileOverlayWindowRegistry
{
    private readonly Lock syncRoot = new();
    private readonly HashSet<IntPtr> windowHandles = [];

    public static PathOfExileOverlayWindowRegistry Shared { get; } = new();

    public void Register(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        lock (syncRoot)
        {
            windowHandles.Add(windowHandle);
        }
    }

    public void Unregister(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        lock (syncRoot)
        {
            windowHandles.Remove(windowHandle);
        }
    }

    public bool Contains(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        lock (syncRoot)
        {
            return windowHandles.Contains(windowHandle);
        }
    }
}
