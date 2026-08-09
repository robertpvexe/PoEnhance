using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Infrastructure.Input;

internal interface IPriceCheckerCopyChordReleaseWaiter
{
    Task<PriceCheckerCopyChordReleaseWaitResult> WaitForReleaseAsync(
        ShortcutBinding shortcut,
        CancellationToken cancellationToken);
}

internal enum PriceCheckerCopyChordReleaseWaitResult
{
    Released,
    TimedOut,
    Cancelled,
}
