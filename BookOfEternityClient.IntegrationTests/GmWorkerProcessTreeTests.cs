using System.Diagnostics;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "ProcessIntegration")]
public sealed class GmWorkerProcessTreeTests
{
    [Fact]
    public void ProcessTreeFactory_UnsupportedPlatformFailsClosedBeforeAttach()
    {
        var factory = new GmWorkerProcessTreeFactory(
            isWindows: () => false,
            windowsFactory: _ => throw new Xunit.Sdk.XunitException("Windows factory must not run."));

        var error = Assert.Throws<PlatformNotSupportedException>(() =>
            factory.Attach(new Process()));

        Assert.Contains("Windows Job Object", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProcessIsolation_DoesNotShipFalseUnixProcessGroupBoundary()
    {
        var assembly = typeof(GmWorkerProcessTreeFactory).Assembly;

        Assert.Null(assembly.GetType("BookOfEternityClient.Services.GmWorkers.ManagedProcessTree"));
        Assert.Null(assembly.GetType("BookOfEternityClient.Services.GmWorkers.UnixProcessGroupController"));
        Assert.Null(assembly.GetType("BookOfEternityClient.Services.GmWorkers.GmWorkerOwnerMonitor"));
    }

    [Fact]
    public async Task StopUnattachedProcessTree_WhenExitCannotBeConfirmed_IsBounded()
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "-NoLogo", "-NoProfile", "-Command", "Start-Sleep -Seconds 30" }
        })!;

        var error = await Assert.ThrowsAsync<TimeoutException>(() =>
            GmWorkerBridgePool.StopUnattachedProcessTreeAsync(
                process,
                TimeSpan.FromMilliseconds(50),
                (_, cancellationToken) => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)));

        Assert.Contains("unattached", error.Message, StringComparison.OrdinalIgnoreCase);
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task QuarantineReaper_OverlappingPassesCleanAndReleaseCapacityExactlyOnce()
    {
        var owner = new FakeQuarantineOwner
        {
            HoldConfirmation = true
        };
        var reaper = new GmWorkerQuarantineReaper(
            capacity: 1,
            retrySchedule: [],
            runInBackground: false);
        using var reservation = Assert.IsType<GmWorkerQuarantineReservation>(
            reaper.TryReserve());
        reservation.Transfer(owner);

        var firstPass = reaper.RunPassAsync();
        await owner.ConfirmationStarted.WaitAsync(
            TimeSpan.FromSeconds(5));
        var secondPass = reaper.RunPassAsync();
        var drainPass = reaper.DrainConfirmedAsync();
        owner.ReleaseConfirmation();
        await Task.WhenAll(
            firstPass,
            secondPass,
            drainPass);

        Assert.Equal(1, owner.ConfirmationCalls);
        Assert.Equal(1, owner.CleanupCalls);
        Assert.Equal(0, reaper.EntryCount);
        Assert.Equal(0, reaper.OwnedCapacity);
        using var replacement = Assert.IsType<GmWorkerQuarantineReservation>(
            reaper.TryReserve());
    }

    [Fact]
    public async Task QuarantineReaper_UnconfirmedOwnerRetainsBoundedCapacityUntilLaterDeath()
    {
        var owner = new FakeQuarantineOwner
        {
            DeathConfirmed = false,
            HoldConfirmation = true
        };
        var reaper = new GmWorkerQuarantineReaper(
            capacity: 1,
            retrySchedule: [],
            runInBackground: false);
        using var reservation = Assert.IsType<GmWorkerQuarantineReservation>(
            reaper.TryReserve());
        reservation.Transfer(owner);

        var firstPass = reaper.RunPassAsync();
        await owner.ConfirmationStarted.WaitAsync(
            TimeSpan.FromSeconds(5));
        var overlappingPass = reaper.RunPassAsync();
        owner.ReleaseConfirmation();
        await Task.WhenAll(
            firstPass,
            overlappingPass);

        Assert.Equal(1, owner.ConfirmationCalls);
        Assert.Equal(0, owner.CleanupCalls);
        Assert.Equal(1, reaper.EntryCount);
        Assert.Equal(1, reaper.OwnedCapacity);
        Assert.Null(reaper.TryReserve());

        owner.DeathConfirmed = true;
        await reaper.RunPassAsync();
        await reaper.RunPassAsync();

        Assert.Equal(2, owner.ConfirmationCalls);
        Assert.Equal(1, owner.CleanupCalls);
        Assert.Equal(0, reaper.EntryCount);
        Assert.Equal(0, reaper.OwnedCapacity);
    }

    private sealed class FakeQuarantineOwner : IGmWorkerQuarantineOwner
    {
        private int _confirmationCalls;
        private int _cleanupCalls;
        private readonly TaskCompletionSource _confirmationStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseConfirmation =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal bool DeathConfirmed { get; set; } = true;
        internal bool HoldConfirmation { get; set; }
        internal Task ConfirmationStarted =>
            _confirmationStarted.Task;
        internal int ConfirmationCalls =>
            Volatile.Read(ref _confirmationCalls);
        internal int CleanupCalls =>
            Volatile.Read(ref _cleanupCalls);

        public string Identity => "fake-quarantine-owner";

        internal void ReleaseConfirmation() =>
            _releaseConfirmation.TrySetResult();

        public async Task ConfirmDeathAsync()
        {
            Interlocked.Increment(ref _confirmationCalls);
            _confirmationStarted.TrySetResult();
            if (HoldConfirmation)
                await _releaseConfirmation.Task;
            if (!DeathConfirmed)
            {
                throw new TimeoutException(
                    "Synthetic process-tree death remains unconfirmed.");
            }
        }

        public Task CleanupConfirmedAsync()
        {
            Interlocked.Increment(ref _cleanupCalls);
            return Task.CompletedTask;
        }

        public Task RecordReaperFailureAsync(Exception failure) =>
            Task.CompletedTask;
    }
}
