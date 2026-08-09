using PoEnhance.App.Infrastructure.Input;
using PoEnhance.App.Infrastructure.Shortcuts;

namespace PoEnhance.App.Tests.Infrastructure.Input;

public sealed class PriceCheckerCopyChordCoordinatorTests
{
    [Fact]
    public async Task CopyIsNotInjectedWhileTriggerIsStillHeldThenOccursOnceAfterRelease()
    {
        var release = new TaskCompletionSource<PriceCheckerCopyChordReleaseWaitResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new FakeCopySender();
        var coordinator = new PriceCheckerCopyChordCoordinator(
            new FakeReleaseWaiter((_, _) => release.Task),
            sender);
        var clipboardSamples = 0;

        var copy = coordinator.CopyAfterTriggerReleaseAsync(
            ShortcutBinding.DefaultPriceChecker,
            () => true,
            () => clipboardSamples++,
            CancellationToken.None);

        Assert.Empty(sender.Calls);
        Assert.Equal(0, clipboardSamples);
        release.SetResult(PriceCheckerCopyChordReleaseWaitResult.Released);

        Assert.Equal(PriceCheckerCopyChordStatus.Copied, (await copy).Status);
        Assert.Single(sender.Calls);
        Assert.Equal(1, clipboardSamples);
    }

    [Fact]
    public async Task HoldingOneActivationCannotStartASecondSyntheticCopy()
    {
        var release = new TaskCompletionSource<PriceCheckerCopyChordReleaseWaitResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var sender = new FakeCopySender();
        var coordinator = new PriceCheckerCopyChordCoordinator(
            new FakeReleaseWaiter((_, _) => release.Task),
            sender);

        var first = coordinator.CopyAfterTriggerReleaseAsync(
            ShortcutBinding.DefaultPriceChecker, () => true, () => { }, CancellationToken.None);
        var repeated = await coordinator.CopyAfterTriggerReleaseAsync(
            ShortcutBinding.DefaultPriceChecker, () => true, () => { }, CancellationToken.None);
        release.SetResult(PriceCheckerCopyChordReleaseWaitResult.Released);

        Assert.Equal(PriceCheckerCopyChordStatus.AlreadyInProgress, repeated.Status);
        Assert.Equal(PriceCheckerCopyChordStatus.Copied, (await first).Status);
        Assert.Single(sender.Calls);
    }

    [Fact]
    public async Task SecondDeliberateActivationAfterReleaseCreatesOneFurtherCopy()
    {
        var sender = new FakeCopySender();
        var coordinator = new PriceCheckerCopyChordCoordinator(
            new FakeReleaseWaiter((_, _) => Task.FromResult(
                PriceCheckerCopyChordReleaseWaitResult.Released)),
            sender);

        var first = await coordinator.CopyAfterTriggerReleaseAsync(
            ShortcutBinding.DefaultPriceChecker, () => true, () => { }, CancellationToken.None);
        var second = await coordinator.CopyAfterTriggerReleaseAsync(
            ShortcutBinding.DefaultPriceChecker, () => true, () => { }, CancellationToken.None);

        Assert.Equal(PriceCheckerCopyChordStatus.Copied, first.Status);
        Assert.Equal(PriceCheckerCopyChordStatus.Copied, second.Status);
        Assert.Equal(2, sender.Calls.Count);
    }

    [Fact]
    public async Task ConfigurableChordUsesTheSameCentralCopySenderWithoutCtrlDHardcoding()
    {
        var configured = new ShortcutBinding(
            ShortcutKey.F8,
            ShortcutModifiers.Control | ShortcutModifiers.Shift);
        var waiter = new FakeReleaseWaiter((_, _) => Task.FromResult(
            PriceCheckerCopyChordReleaseWaitResult.Released));
        var sender = new FakeCopySender();
        var coordinator = new PriceCheckerCopyChordCoordinator(waiter, sender);

        var result = await coordinator.CopyAfterTriggerReleaseAsync(
            configured, () => true, () => { }, CancellationToken.None);

        Assert.Equal(PriceCheckerCopyChordStatus.Copied, result.Status);
        Assert.Equal(configured, Assert.Single(waiter.Shortcuts));
        Assert.Single(sender.Calls);
    }

    [Fact]
    public Task TimeoutNeverSendsCopy() =>
        AssertNoCopyForReleaseOutcome(
            PriceCheckerCopyChordReleaseWaitResult.TimedOut,
            PriceCheckerCopyChordStatus.TriggerReleaseTimedOut);

    [Fact]
    public Task CancellationNeverSendsCopy() =>
        AssertNoCopyForReleaseOutcome(
            PriceCheckerCopyChordReleaseWaitResult.Cancelled,
            PriceCheckerCopyChordStatus.Cancelled);

    private static async Task AssertNoCopyForReleaseOutcome(
        PriceCheckerCopyChordReleaseWaitResult releaseResult,
        PriceCheckerCopyChordStatus expectedStatus)
    {
        var sender = new FakeCopySender();
        var coordinator = new PriceCheckerCopyChordCoordinator(
            new FakeReleaseWaiter((_, _) => Task.FromResult(releaseResult)),
            sender);

        var result = await coordinator.CopyAfterTriggerReleaseAsync(
            ShortcutBinding.DefaultPriceChecker, () => true, () => { }, CancellationToken.None);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task ForegroundLossAfterReleasePreventsCopy()
    {
        var sender = new FakeCopySender();
        var coordinator = new PriceCheckerCopyChordCoordinator(
            new FakeReleaseWaiter((_, _) => Task.FromResult(
                PriceCheckerCopyChordReleaseWaitResult.Released)),
            sender);

        var result = await coordinator.CopyAfterTriggerReleaseAsync(
            ShortcutBinding.DefaultPriceChecker, () => false, () => { }, CancellationToken.None);

        Assert.Equal(PriceCheckerCopyChordStatus.ForegroundLost, result.Status);
        Assert.Empty(sender.Calls);
    }

    [Fact]
    public async Task PartialSendInputPreservesTheExistingVisibleFailureResult()
    {
        var sender = new FakeCopySender(success: false, sentInputCount: 3, errorCode: 5);
        var coordinator = new PriceCheckerCopyChordCoordinator(
            new FakeReleaseWaiter((_, _) => Task.FromResult(
                PriceCheckerCopyChordReleaseWaitResult.Released)),
            sender);

        var result = await coordinator.CopyAfterTriggerReleaseAsync(
            ShortcutBinding.DefaultPriceChecker, () => true, () => { }, CancellationToken.None);

        Assert.Equal(PriceCheckerCopyChordStatus.CopyInputFailed, result.Status);
        Assert.Equal((uint)3, result.SentInputCount);
        Assert.Equal(5, result.ErrorCode);
    }

    [Fact]
    public async Task WindowsReleaseWaiterObservesConfiguredPrimaryAndModifiers()
    {
        var physicalState = new FakePhysicalKeyboardState(
            0x71, // F8
            0xA2, // left Ctrl
            0xA1); // right Shift
        var clock = new AdvancingTimeProvider(DateTimeOffset.UtcNow);
        var waiter = new PriceCheckerCopyChordReleaseWaiter(
            physicalState,
            clock,
            (_, _) =>
            {
                physicalState.Clear();
                clock.Advance(PriceCheckerCopyChordReleaseWaiter.PollInterval);
                return Task.CompletedTask;
            });

        var result = await waiter.WaitForReleaseAsync(
            new ShortcutBinding(
                ShortcutKey.F8,
                ShortcutModifiers.Control | ShortcutModifiers.Shift),
            CancellationToken.None);

        Assert.Equal(PriceCheckerCopyChordReleaseWaitResult.Released, result);
        Assert.Contains((ushort)ShortcutKey.F8, physicalState.CheckedKeys);
        Assert.Contains((ushort)0xA2, physicalState.CheckedKeys);
        Assert.Contains((ushort)0xA3, physicalState.CheckedKeys);
        Assert.Contains((ushort)0xA0, physicalState.CheckedKeys);
        Assert.Contains((ushort)0xA1, physicalState.CheckedKeys);
        Assert.DoesNotContain((ushort)0xA4, physicalState.CheckedKeys);
    }

    [Fact]
    public async Task ShutdownCancellationTerminatesPendingReleaseWaitWithoutCopy()
    {
        var physicalState = new FakePhysicalKeyboardState((ushort)ShortcutKey.D, 0xA2);
        var waiter = new PriceCheckerCopyChordReleaseWaiter(
            physicalState,
            TimeProvider.System,
            Task.Delay);
        var sender = new FakeCopySender();
        var coordinator = new PriceCheckerCopyChordCoordinator(waiter, sender);
        using var cancellation = new CancellationTokenSource();

        var copy = coordinator.CopyAfterTriggerReleaseAsync(
            ShortcutBinding.DefaultPriceChecker, () => true, () => { }, cancellation.Token);
        cancellation.Cancel();

        Assert.Equal(PriceCheckerCopyChordStatus.Cancelled, (await copy).Status);
        Assert.Empty(sender.Calls);
    }

    private sealed class FakeCopySender : IPriceCheckerCopyChordSender
    {
        private readonly bool success;
        private readonly uint sentInputCount;
        private readonly int errorCode;

        public FakeCopySender(bool success = true, uint sentInputCount = 4, int errorCode = 0)
        {
            this.success = success;
            this.sentInputCount = sentInputCount;
            this.errorCode = errorCode;
        }

        public List<int> Calls { get; } = [];

        public bool TrySendAdvancedItemDescriptionCopyChord(out uint sentInputCount, out int errorCode)
        {
            Calls.Add(1);
            sentInputCount = this.sentInputCount;
            errorCode = this.errorCode;
            return success;
        }
    }

    private sealed class FakeReleaseWaiter(
        Func<ShortcutBinding, CancellationToken, Task<PriceCheckerCopyChordReleaseWaitResult>> wait)
        : IPriceCheckerCopyChordReleaseWaiter
    {
        public List<ShortcutBinding> Shortcuts { get; } = [];

        public Task<PriceCheckerCopyChordReleaseWaitResult> WaitForReleaseAsync(
            ShortcutBinding shortcut,
            CancellationToken cancellationToken)
        {
            Shortcuts.Add(shortcut);
            return wait(shortcut, cancellationToken);
        }
    }

    private sealed class FakePhysicalKeyboardState(params ushort[] pressedKeys) : IPhysicalKeyboardState
    {
        private readonly HashSet<ushort> pressed = [.. pressedKeys];

        public List<ushort> CheckedKeys { get; } = [];

        public bool IsPressed(ushort virtualKey)
        {
            CheckedKeys.Add(virtualKey);
            return pressed.Contains(virtualKey);
        }

        public void Clear() => pressed.Clear();
    }

    private sealed class AdvancingTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan elapsed) => now += elapsed;
    }
}
