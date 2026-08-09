using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Infrastructure.Input;

internal sealed class PriceCheckerCopyChordReleaseWaiter : IPriceCheckerCopyChordReleaseWaiter
{
    internal static readonly TimeSpan ReleaseTimeout = TimeSpan.FromMilliseconds(500);
    internal static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    private const ushort VirtualKeyLeftControl = 0xA2;
    private const ushort VirtualKeyRightControl = 0xA3;
    private const ushort VirtualKeyLeftAlt = 0xA4;
    private const ushort VirtualKeyRightAlt = 0xA5;
    private const ushort VirtualKeyLeftShift = 0xA0;
    private const ushort VirtualKeyRightShift = 0xA1;
    private const ushort VirtualKeyLeftWindows = 0x5B;
    private const ushort VirtualKeyRightWindows = 0x5C;

    private readonly IPhysicalKeyboardState physicalKeyboardState;
    private readonly TimeProvider timeProvider;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;

    public PriceCheckerCopyChordReleaseWaiter()
        : this(new WindowsPhysicalKeyboardState(), TimeProvider.System, Task.Delay)
    {
    }

    internal PriceCheckerCopyChordReleaseWaiter(
        IPhysicalKeyboardState physicalKeyboardState,
        TimeProvider timeProvider,
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        this.physicalKeyboardState = physicalKeyboardState ??
            throw new ArgumentNullException(nameof(physicalKeyboardState));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.delayAsync = delayAsync ?? throw new ArgumentNullException(nameof(delayAsync));
    }

    public async Task<PriceCheckerCopyChordReleaseWaitResult> WaitForReleaseAsync(
        ShortcutBinding shortcut,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        var deadline = timeProvider.GetUtcNow() + ReleaseTimeout;
        while (!IsReleased(shortcut))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return PriceCheckerCopyChordReleaseWaitResult.Cancelled;
            }

            var remaining = deadline - timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                return PriceCheckerCopyChordReleaseWaitResult.TimedOut;
            }

            try
            {
                await delayAsync(
                        remaining < PollInterval ? remaining : PollInterval,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return PriceCheckerCopyChordReleaseWaitResult.Cancelled;
            }
        }

        return PriceCheckerCopyChordReleaseWaitResult.Released;
    }

    private bool IsReleased(ShortcutBinding shortcut)
    {
        if (physicalKeyboardState.IsPressed((ushort)shortcut.PrimaryKey))
        {
            return false;
        }

        return (!shortcut.Modifiers.HasFlag(ShortcutModifiers.Control) ||
                NeitherPressed(VirtualKeyLeftControl, VirtualKeyRightControl)) &&
            (!shortcut.Modifiers.HasFlag(ShortcutModifiers.Alt) ||
                NeitherPressed(VirtualKeyLeftAlt, VirtualKeyRightAlt)) &&
            (!shortcut.Modifiers.HasFlag(ShortcutModifiers.Shift) ||
                NeitherPressed(VirtualKeyLeftShift, VirtualKeyRightShift)) &&
            (!shortcut.Modifiers.HasFlag(ShortcutModifiers.Windows) ||
                NeitherPressed(VirtualKeyLeftWindows, VirtualKeyRightWindows));
    }

    private bool NeitherPressed(ushort first, ushort second) =>
        !physicalKeyboardState.IsPressed(first) && !physicalKeyboardState.IsPressed(second);
}
