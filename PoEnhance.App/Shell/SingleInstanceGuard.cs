using System.Threading;

namespace PoEnhance.App.Shell;

internal sealed class SingleInstanceGuard : IDisposable
{
    internal const string DefaultMutexName = @"Local\PoEnhance.0.1.Application";

    private readonly Mutex mutex;
    private bool ownsMutex;
    private bool isDisposed;

    private SingleInstanceGuard(Mutex mutex)
    {
        this.mutex = mutex;
        ownsMutex = true;
    }

    public static bool TryAcquire(out SingleInstanceGuard? guard)
    {
        return TryAcquire(DefaultMutexName, out guard);
    }

    internal static bool TryAcquire(string mutexName, out SingleInstanceGuard? guard)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);

        var mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            guard = null;
            return false;
        }

        guard = new SingleInstanceGuard(mutex);
        return true;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        if (ownsMutex)
        {
            try
            {
                mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }

            ownsMutex = false;
        }

        mutex.Dispose();
        isDisposed = true;
    }
}
