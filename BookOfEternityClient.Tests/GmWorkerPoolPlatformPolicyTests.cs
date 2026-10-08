using System.Diagnostics;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

// Managed policy checks only. These do not qualify a Windows Job on Linux.
public sealed class GmWorkerPoolPlatformPolicyTests
{
    [Fact]
    public async Task NativeCompletion_DoesNotWaitForDiagnosticDrainBeforeScopedStop()
    {
        var result = await GmWorkerProcessCompletionArbiter.WaitAsync(Task.FromResult(0),
            _ => throw new InvalidOperationException("Native completion invoked diagnostic drain."),
            new TaskCompletionSource().Task, CancellationToken.None, CancellationToken.None, waitForDiagnosticDrain: false);
        Assert.Equal(GmWorkerProcessCompletionOutcomeKind.Completed, result.Kind);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task DefaultWindowsCompletion_PreservesDiagnosticDrainBeforeStop()
    {
        var drain = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = GmWorkerProcessCompletionArbiter.WaitAsync(Task.FromResult(0),
            _ => { entered.TrySetResult(); return drain.Task; }, new TaskCompletionSource().Task,
            CancellationToken.None, CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(result.IsCompleted);
        drain.TrySetResult();
        Assert.Equal(GmWorkerProcessCompletionOutcomeKind.Completed, (await result).Kind);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public async Task ObservationException_RetriesOnlyRetainedAssignedWindowsJob(bool windows, bool retainedJob)
    {
        var owner = new PolicyOwner(windows, retainedJob);
        var authority = new GmWorkerExecutionAuthority(owner.Identity, GmWorkerBridgeTestFixtures.AnalysisTask());
        await Assert.ThrowsAsync<IOException>(() => authority.StopForCleanupAsync(owner));
        Assert.Equal(!retainedJob, authority.IsUncertain);
        Assert.Throws<InvalidOperationException>(() => authority.RequirePublication());
        if (retainedJob)
        {
            Assert.False((await authority.StopForCleanupAsync(owner)).NoLaunch);
            await authority.SettleOutputsAsync(owner);
            Assert.True(authority.RequireCleanupEvidence().Stop!.CleanupComplete);
        }
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() => authority.StopForCleanupAsync(owner));
        Assert.Equal(2, owner.StopCalls);
        Assert.Throws<InvalidOperationException>(() => authority.RequirePublication());
    }

    [Fact]
    public async Task WindowsOwner_DoesNotTreatArbitraryProcessTreeAsRetainedJob()
    {
        var owner = new GmWorkerWindowsOwnedLaunch(new ProcessStartInfo("never-started"));
        Assert.False(owner.RetainsAssignedWindowsJob);
        owner.Attach(new FakeTreeFactory());
        Assert.False(owner.RetainsAssignedWindowsJob);
        await owner.StopAndObserveAsync();
        await owner.SettleOutputsAsync();
        await owner.DisposeAsync();
        Assert.False(owner.RetainsAssignedWindowsJob);
    }

    private sealed class PolicyOwner(bool windows, bool retainedJob) : GmWorkerOwnedLaunch
    {
        internal int StopCalls;
        internal override GmWorkerExecutionIdentity Identity { get; } = new("managed-policy",
            windows ? GmWorkerBackend.WindowsJob : GmWorkerBackend.NativeLineage,
            windows ? "windows-job" : GmWorkerBackendSelector.NativeGuarantee);
        internal override bool RetainsAssignedWindowsJob => retainedJob;
        internal override int HostProcessId => throw new NotSupportedException();
        internal override int SupervisorProcessId => throw new NotSupportedException();
        internal override int? AdmittedHostProcessId => null;
        internal override Task HostExited => Task.CompletedTask;
        internal override Task WaitUntilReadyAsync(GmWorkerProcessHostLaunch host, CancellationToken token) => throw new NotSupportedException();
        internal override Task<int> WaitForWorkerCompletionAsync(GmWorkerProcessHostLaunch host, CancellationToken token) => throw new NotSupportedException();
        internal override Task<GmWorkerStopEvidence> StopAndObserveAsync() => ++StopCalls == 1
            ? Task.FromException<GmWorkerStopEvidence>(new IOException("managed observation failure"))
            : Task.FromResult(new GmWorkerStopEvidence(Identity.RunId, Identity.Backend, Identity.Guarantee,
                GmWorkerStopState.StoppedWithinScope, "managed policy only", true, false, 0));
        internal override Task<GmWorkerOwnedOutputs> SettleOutputsAsync() => Task.FromResult(new GmWorkerOwnedOutputs("", ""));
        public override ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeTreeFactory : IGmWorkerProcessTreeFactory
    {
        public IGmWorkerProcessTree Attach(Process _) => new FakeTree();
    }
    private sealed class FakeTree : IGmWorkerProcessTree
    {
        public Task StopAndWaitAsync() => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
