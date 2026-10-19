using System.Diagnostics;
using System.Text;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks managed finite-owner decisions without constructing a native owner.
/// Native observations here are inputs to policy, never native qualification evidence.
/// </summary>
public sealed class GmWorkerNativeFinitePipePolicyTests
{
    /// <summary>
    /// A fast child can finish without Started only when every retirement observation agrees.
    /// </summary>
    [Fact]
    public void TerminalWithoutStartedRequiresCompleteEvidence()
    {
        var complete = CompletedObservation();
        Assert.Equal(NativeFiniteStartDecision.Completed, NativeLineageOwner.EvaluateFiniteStart(complete));
        NativeFiniteObservation[] missing =
        [
            complete with { Released = false }, complete with { RootExited = false },
            complete with { SupervisorExited = false }, complete with { SupervisorExitCode = null },
            complete with { StatusCompleted = false }, complete with { HelperErrorCompleted = false },
            complete with { Terminal = null },
            complete with { Terminal = complete.Terminal! with { RootExitCode = null } },
            complete with { Terminal = complete.Terminal! with { CleanupComplete = false } },
            complete with { Terminal = complete.Terminal! with { AuthorityRetained = true } }
        ];
        foreach (var observation in missing)
            Assert.Equal(NativeFiniteStartDecision.Pending, NativeLineageOwner.EvaluateFiniteStart(observation));
    }

    /// <summary>
    /// Release alone is not an exec failure, and later observations can admit either path.
    /// </summary>
    [Fact]
    public void ReleasedWithoutStartedOrTerminalRemainsPending()
    {
        var pending = new NativeFiniteObservation(true, false, false, false, null, false, false, null, null);
        Assert.Equal(NativeFiniteStartDecision.Pending, NativeLineageOwner.EvaluateFiniteStart(pending));
        Assert.Equal(NativeFiniteStartDecision.Started, NativeLineageOwner.EvaluateFiniteStart(pending with { Started = true }));
        Assert.Equal(NativeFiniteStartDecision.Completed, NativeLineageOwner.EvaluateFiniteStart(CompletedObservation()));
    }

    /// <summary>
    /// Complete retirement wakes the real await helper even when Started never arrives.
    /// </summary>
    [Fact]
    public async Task TerminalWithoutStartedUnblocksAwait()
    {
        var retirement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var snapshot = new NativeFiniteObservation(true, false, false, false, null, false, false, null, null);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var wait = NativeLineageOwner.AwaitFiniteStartAsync(() => snapshot,
            new TaskCompletionSource().Task, retirement.Task, new TaskCompletionSource<string>().Task, deadline.Token);
        Assert.False(wait.IsCompleted);
        snapshot = CompletedObservation();
        retirement.SetResult();
        Assert.Equal(NativeFiniteStartDecision.Completed, await wait);
    }

    /// <summary>
    /// Failed retirement cannot be mistaken for success or leave the start waiter pending.
    /// </summary>
    [Fact]
    public async Task FailedRetirementRejectsAdmission()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var snapshot = CompletedObservation() with { RootExited = false };
        var result = await NativeLineageOwner.AwaitFiniteStartAsync(() => snapshot,
            new TaskCompletionSource().Task, Task.FromException(new IOException("observation failed")),
            new TaskCompletionSource<string>().Task, deadline.Token);
        Assert.Equal(NativeFiniteStartDecision.Rejected, result);
    }

    /// <summary>
    /// Lost authority wins over an already completed Started notification.
    /// </summary>
    [Fact]
    public async Task AuthorityLossWinsOverStarted()
    {
        var result = await NativeLineageOwner.AwaitFiniteStartAsync(
            () => CompletedObservation() with { Started = true }, Task.CompletedTask,
            Task.CompletedTask, Task.FromResult("status-lost"), CancellationToken.None);
        Assert.Equal(NativeFiniteStartDecision.Rejected, result);
    }

    /// <summary>
    /// A confirmed Started event does not require a finite root to remain alive.
    /// </summary>
    [Fact]
    public void StartedDoesNotRequireStillLiveRoot()
    {
        var observation = new NativeFiniteObservation(true, true, true, false, null, false, false, null, null);
        Assert.Equal(NativeFiniteStartDecision.Started, NativeLineageOwner.EvaluateFiniteStart(observation));
        Assert.Equal(NativeFiniteStartDecision.Pending,
            NativeLineageOwner.EvaluateFiniteStart(observation with { Released = false }));
    }

    /// <summary>
    /// Sticky uncertainty and failed helper retirement reject both admission paths.
    /// </summary>
    [Fact]
    public void UncertainNeverBecomesSuccess()
    {
        var complete = CompletedObservation() with { Started = true };
        NativeFiniteObservation[] rejected =
        [
            complete with { Uncertainty = "control-lost" },
            complete with { SupervisorExitCode = 9 },
            complete with { Terminal = complete.Terminal! with { State = GmWorkerStopState.Uncertain } }
        ];
        foreach (var observation in rejected)
        {
            Assert.Equal(NativeFiniteStartDecision.Rejected, NativeLineageOwner.EvaluateFiniteStart(observation));
            Assert.False(NativeLineageOwner.CanAcceptFiniteClosedControl(observation));
        }
    }

    /// <summary>
    /// A closed stop pipe is acceptable only after complete natural retirement.
    /// </summary>
    [Fact]
    public void ClosedControlRequiresNaturalRetirement()
    {
        var complete = CompletedObservation();
        Assert.True(NativeLineageOwner.CanAcceptFiniteClosedControl(complete));
        NativeFiniteObservation[] rejected =
        [
            complete with { RootExited = false }, complete with { StatusCompleted = false },
            complete with { HelperErrorCompleted = false }, complete with { SupervisorExited = false },
            complete with { SupervisorExitCode = null }, complete with { Released = false },
            complete with { Terminal = complete.Terminal! with { Reason = "stop-requested" } },
            complete with { Terminal = complete.Terminal! with { AuthorityRetained = true } },
            complete with { Terminal = complete.Terminal! with { CleanupComplete = false } },
            complete with { Terminal = complete.Terminal! with { RootExitCode = null } }
        ];
        foreach (var observation in rejected)
            Assert.False(NativeLineageOwner.CanAcceptFiniteClosedControl(observation));
    }

    /// <summary>
    /// Positive retirement cannot discard a control write that is still using original owner resources.
    /// </summary>
    [Fact]
    public void UnsettledControlWrite_RemainsUncertain()
    {
        Assert.False(NativeLineageOwner.CanAcceptFiniteClosedControl(CompletedObservation(), controlSettled: false));
        Assert.True(NativeLineageOwner.CanAcceptFiniteClosedControl(CompletedObservation(), controlSettled: true));
    }

    /// <summary>
    /// Helper exit zero cannot supply the missing root result.
    /// </summary>
    [Fact]
    public void HelperZeroIsNotRootExit()
    {
        var complete = CompletedObservation();
        Assert.Equal(73, complete.Terminal!.RootExitCode);
        Assert.Equal(NativeFiniteStartDecision.Pending,
            NativeLineageOwner.EvaluateFiniteStart(complete with { Terminal = null }));
        Assert.Equal(NativeFiniteStartDecision.Completed,
            NativeLineageOwner.EvaluateFiniteStart(complete with { Terminal = complete.Terminal with { RootExitCode = 0 } }));
    }

    /// <summary>
    /// Finite launch preserves the complete child environment and literal argument boundaries.
    /// </summary>
    [Fact]
    public void FiniteSupervisorCopiesChildEnvironmentAndArguments()
    {
        var host = new ProcessStartInfo("/runtime with spaces/dotnet") { WorkingDirectory = "/private root" };
        host.Environment.Clear();
        host.Environment["TMPDIR"] = "/private root/tmp";
        host.Environment["CRASH5_TEST_ENV"] = "child-only";
        host.ArgumentList.Add("exec");
        host.ArgumentList.Add("/test assembly/test.dll");
        var actual = NativeLineageOwner.CreatePipeSupervisorStartInfo(host, "/package/supervisor", "run", "/private/owner", true);
        Assert.Equal(2, actual.Environment.Count);
        Assert.Equal("/private root/tmp", actual.Environment["TMPDIR"]);
        Assert.Equal("child-only", actual.Environment["CRASH5_TEST_ENV"]);
        Assert.Equal(new[] { "run", "100", "2500", "--host-v2", "/private/owner", "/runtime with spaces/dotnet", "exec", "/test assembly/test.dll" }, actual.ArgumentList);
        Assert.Equal("/private root", actual.WorkingDirectory);
        Assert.False(actual.UseShellExecute);
        Assert.True(actual.RedirectStandardInput && actual.RedirectStandardOutput && actual.RedirectStandardError);
        actual.Environment["CRASH5_TEST_ENV"] = "different";
        Assert.Equal("child-only", host.Environment["CRASH5_TEST_ENV"]);
    }

    /// <summary>
    /// The existing pipe launch keeps its inherited environment instead of adopting child overrides.
    /// </summary>
    [Fact]
    public void LegacyPipeStartDefaultsRemainUnchanged()
    {
        var host = new ProcessStartInfo("/dotnet");
        var key = "CRASH5_" + Guid.NewGuid().ToString("N");
        host.Environment[key] = "must-not-copy";
        var actual = NativeLineageOwner.CreatePipeSupervisorStartInfo(host, "/supervisor", "run", "/owner", false);
        Assert.False(actual.Environment.ContainsKey(key));
        Assert.Equal(new[] { "run", "100", "2500", "--host-v2", "/owner", "/dotnet" }, actual.ArgumentList);
    }

    /// <summary>
    /// Finite pipes retain complete distinct streams while the legacy consumer remains capped.
    /// </summary>
    [Fact]
    public async Task FiniteOutputIsCompleteAndLegacyOutputRemainsCapped()
    {
        var output = new string('O', 131072);
        var error = new string('E', 131072);
        using var stdout = new MemoryStream(Encoding.UTF8.GetBytes(output));
        using var stderr = new MemoryStream(Encoding.UTF8.GetBytes(error));
        var results = await Task.WhenAll(NativeLineageOwner.ReadPipeOutputAsync(stdout, true),
            NativeLineageOwner.ReadPipeOutputAsync(stderr, true));
        Assert.Equal(output, results[0]);
        Assert.Equal(error, results[1]);
        Assert.True(stdout.CanRead && stderr.CanRead);
        using var legacy = new MemoryStream(Encoding.UTF8.GetBytes(output));
        Assert.Equal(new string('O', 65536) + Environment.NewLine + "[worker output truncated after 65536 characters]",
            await NativeLineageOwner.ReadPipeOutputAsync(legacy, false));
    }

    /// <summary>
    /// Creates a literal complete retirement input for managed policy checks.
    /// </summary>
    /// <returns>
    /// Distinct root exit73 and helper exit0 observations, without a Started event.
    /// </returns>
    private static NativeFiniteObservation CompletedObservation() => new(true, false, true, true, 0, true, true,
        new("test-run", GmWorkerBackend.NativeLineage, GmWorkerBackendSelector.NativeGuarantee,
            GmWorkerStopState.StoppedWithinScope, "root-exited", true, false, 73), null);
}
