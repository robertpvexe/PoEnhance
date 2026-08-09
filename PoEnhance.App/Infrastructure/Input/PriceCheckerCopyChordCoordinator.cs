using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Infrastructure.Input;

internal sealed class PriceCheckerCopyChordCoordinator
{
    private readonly IPriceCheckerCopyChordReleaseWaiter releaseWaiter;
    private readonly IPriceCheckerCopyChordSender copyChordSender;
    private int isExecuting;

    public PriceCheckerCopyChordCoordinator(
        IPriceCheckerCopyChordReleaseWaiter releaseWaiter,
        IPriceCheckerCopyChordSender copyChordSender)
    {
        this.releaseWaiter = releaseWaiter ?? throw new ArgumentNullException(nameof(releaseWaiter));
        this.copyChordSender = copyChordSender ?? throw new ArgumentNullException(nameof(copyChordSender));
    }

    public async Task<PriceCheckerCopyChordResult> CopyAfterTriggerReleaseAsync(
        ShortcutBinding shortcut,
        Func<bool> canCopy,
        Action beforeCopy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(shortcut);
        ArgumentNullException.ThrowIfNull(canCopy);
        ArgumentNullException.ThrowIfNull(beforeCopy);
        if (Interlocked.CompareExchange(ref isExecuting, 1, 0) != 0)
        {
            return new PriceCheckerCopyChordResult
            {
                Status = PriceCheckerCopyChordStatus.AlreadyInProgress,
            };
        }

        try
        {
            var releaseResult = await releaseWaiter.WaitForReleaseAsync(shortcut, cancellationToken)
                .ConfigureAwait(false);
            if (releaseResult != PriceCheckerCopyChordReleaseWaitResult.Released)
            {
                return new PriceCheckerCopyChordResult
                {
                    Status = releaseResult == PriceCheckerCopyChordReleaseWaitResult.Cancelled
                        ? PriceCheckerCopyChordStatus.Cancelled
                        : PriceCheckerCopyChordStatus.TriggerReleaseTimedOut,
                };
            }

            if (!canCopy())
            {
                return new PriceCheckerCopyChordResult
                {
                    Status = PriceCheckerCopyChordStatus.ForegroundLost,
                };
            }

            beforeCopy();
            var success = copyChordSender.TrySendAdvancedItemDescriptionCopyChord(
                out var sentInputCount,
                out var errorCode);
            return new PriceCheckerCopyChordResult
            {
                Status = success
                    ? PriceCheckerCopyChordStatus.Copied
                    : PriceCheckerCopyChordStatus.CopyInputFailed,
                SentInputCount = sentInputCount,
                ErrorCode = errorCode,
            };
        }
        finally
        {
            Volatile.Write(ref isExecuting, 0);
        }
    }
}

internal sealed record PriceCheckerCopyChordResult
{
    public PriceCheckerCopyChordStatus Status { get; init; }

    public uint SentInputCount { get; init; }

    public int ErrorCode { get; init; }
}

internal enum PriceCheckerCopyChordStatus
{
    Copied,
    TriggerReleaseTimedOut,
    Cancelled,
    CopyInputFailed,
    AlreadyInProgress,
    ForegroundLost,
}
