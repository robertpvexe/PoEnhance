using System.Windows;
using System.Windows.Threading;

namespace PoEnhance.App.Features.PriceChecking;

internal sealed class WpfPriceCheckerDeferredActionScheduler : IPriceCheckerDeferredActionScheduler
{
    public void Schedule(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            action();
            return;
        }

        _ = dispatcher.BeginInvoke(action, DispatcherPriority.Background);
    }
}
