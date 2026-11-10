using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the shared validation policy with local leaf operations and no game setup.
/// </summary>
public sealed class JointRepairValidationCoordinatorTests
{
    /// <summary>
    /// Requires precisely the QTE entry and exit checks around one calculation.
    /// </summary>
    /// <param name="suspend">
    /// Whether the calculation genuinely suspends before completing.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resource_UsesExactlyTwoBaselineChecks(bool suspend)
    {
        var calls = new List<string>();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task Baseline() { calls.Add("baseline"); return Task.CompletedTask; }
        var result = new object();
        var operation = JointRepairValidationCoordinator.ValidateResourceAsync(() => calls.Add("seal"),
            () => JointRepairValidationCoordinator.RecomputeWithBaselineAsync(Baseline, async () =>
            {
                calls.Add("compute");
                entered.SetResult();
                if (suspend) await release.Task;
                return result;
            }), actual => { Assert.Same(result, actual); calls.Add("compare"); });
        if (suspend)
        {
            await entered.Task;
            Assert.False(operation.IsCompleted);
            Assert.DoesNotContain("compare", calls);
            release.SetResult();
        }
        await operation;
        Assert.Equal(2, calls.Count(x => x == "baseline"));
        Assert.Equal(new[] { "seal", "baseline", "compute", "baseline", "compare" }, calls);
    }

    /// <summary>
    /// Refuses changed substituted data at either retained QTE boundary before comparison.
    /// </summary>
    /// <param name="exitDrift">
    /// Whether data changes during computation rather than just before QTE entry.
    /// </param>
    /// <param name="suspend">
    /// Whether the boundary operation genuinely suspends before observing the changed data.
    /// </param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Resource_RefusesEntryAndExitDrift(bool exitDrift, bool suspend)
    {
        var current = true;
        var computes = 0;
        var compares = 0;
        var finalized = false;
        var error = new InvalidDataException("Substituted guarded data changed.");
        async Task Baseline()
        {
            if (suspend) await Task.Yield();
            if (!current) throw error;
        }
        var actual = await Assert.ThrowsAsync<InvalidDataException>(() => JointRepairValidationCoordinator.ValidateResourceAsync(() => { },
            async () =>
            {
                if (!exitDrift) current = false;
                var prepared = await JointRepairValidationCoordinator.RecomputeWithBaselineAsync(Baseline, async () =>
                {
                    computes++;
                    if (suspend) await Task.Yield();
                    current = false;
                    return new object();
                });
                finalized = true;
                return prepared;
            }, _ => compares++));
        Assert.Same(error, actual);
        Assert.Equal(exitDrift ? 1 : 0, computes);
        Assert.Equal(0, compares);
        Assert.False(finalized);
        Assert.False(current);
    }

    /// <summary>
    /// Preserves QTE failure identity and short-circuit order, including skipped exit on compute failure.
    /// </summary>
    /// <param name="stage">
    /// The entry, compute or exit operation that fails.
    /// </param>
    /// <param name="suspend">
    /// Whether the failed operation suspends before throwing.
    /// </param>
    [Theory]
    [InlineData("entry", false)]
    [InlineData("entry", true)]
    [InlineData("compute", false)]
    [InlineData("compute", true)]
    [InlineData("exit", false)]
    [InlineData("exit", true)]
    public async Task Qte_PropagatesFailureWithoutLaterOperations(string stage, bool suspend)
    {
        var calls = new List<string>();
        var error = new IOException(stage);
        var checks = 0;
        async Task Step(string name)
        {
            calls.Add(name);
            if (suspend) await Task.Yield();
            if (name == stage) throw error;
        }
        var actual = await Assert.ThrowsAsync<IOException>(() => JointRepairValidationCoordinator.RecomputeWithBaselineAsync(
            () => Step(++checks == 1 ? "entry" : "exit"),
            async () => { await Step("compute"); return new object(); }));
        Assert.Same(error, actual);
        Assert.Equal(stage switch
        {
            "entry" => new[] { "entry" },
            "compute" => new[] { "entry", "compute" },
            _ => new[] { "entry", "compute", "exit" }
        }, calls);
    }

    /// <summary>
    /// Keeps seal failure before work and propagates companion-comparison failure after work.
    /// </summary>
    /// <param name="sealFails">
    /// Whether the initial seal or final comparison throws.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resource_PropagatesSealAndComparisonFailure(bool sealFails)
    {
        var computed = false;
        var compared = false;
        var error = new InvalidDataException("Rejected.");
        var actual = await Assert.ThrowsAsync<InvalidDataException>(() => JointRepairValidationCoordinator.ValidateResourceAsync(
            () => { if (sealFails) throw error; },
            () => { computed = true; return Task.FromResult(new object()); },
            _ => { compared = true; throw error; }));
        Assert.Same(error, actual);
        Assert.Equal(!sealFails, computed);
        Assert.Equal(!sealFails, compared);
    }

}
