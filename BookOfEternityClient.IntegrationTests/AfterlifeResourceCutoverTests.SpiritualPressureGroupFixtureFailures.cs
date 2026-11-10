using System.Runtime.CompilerServices;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private static readonly string[] PressureFixtureSuccessOrder =
    ["work", "window", "disposal_files", "capture_explicit", "disposed_inert",
        "capture_implicit", "no_private_pair", "lease", "context", "root"];

    /// <summary>Retains the original success order, including both existing capture disposal calls.</summary>
    [Fact]
    public async Task SpiritualPressureFixture_SuccessKeepsOriginalStageOrder()
    {
        var probe = new PressureFixtureStageProbe();
        await ExecutePressureGroupFixtureStagesAsync(probe.Stages());
        Assert.Equal(PressureFixtureSuccessOrder, probe.Calls);
    }

    /// <summary>A lone body error or cancellation keeps its exact object and original throwing frame.</summary>
    /// <param name="cancelled">Whether the body error is cancellation rather than an ordinary operation failure.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpiritualPressureFixture_OnlyWorkFailureKeepsOriginalException(bool cancelled)
    {
        var primary = cancelled ? (Exception)new OperationCanceledException("fixture body cancellation")
            : new IOException("fixture body failure");
        var probe = new PressureFixtureStageProbe { WorkFailure = primary };
        var error = await Record.ExceptionAsync(() => ExecutePressureGroupFixtureStagesAsync(probe.Stages()));
        Assert.Same(primary, error);
        Assert.Contains(nameof(ThrowPressureFixtureFailure), error!.StackTrace!);
        Assert.Empty(error.Data);
        Assert.Equal(1, probe.Calls.Count(stage => stage == "capture_explicit"));
        Assert.Equal(1, probe.Calls.Count(stage => stage == "capture_implicit"));
        Assert.Contains("lease", probe.Calls);
        Assert.Contains("context", probe.Calls);
        Assert.Contains("root", probe.Calls);
    }

    /// <summary>Each cleanup failure leaves every later eligible cleanup stage attempted without replacing the first error.</summary>
    /// <param name="failingStage">One exact cleanup stage, never an arbitrary fixture callback.</param>
    [Theory]
    [InlineData("window")]
    [InlineData("disposal_files")]
    [InlineData("capture_explicit")]
    [InlineData("disposed_inert")]
    [InlineData("capture_implicit")]
    [InlineData("no_private_pair")]
    [InlineData("lease")]
    [InlineData("context")]
    [InlineData("root")]
    public async Task SpiritualPressureFixture_CleanupFailureAttemptsAllEligibleStages(string failingStage)
    {
        var injected = new IOException("cleanup failure: " + failingStage);
        var probe = new PressureFixtureStageProbe();
        probe.CleanupFailures.Add(failingStage, injected);
        var error = await Record.ExceptionAsync(() => ExecutePressureGroupFixtureStagesAsync(probe.Stages()));
        Assert.Same(injected, error);
        Assert.Contains(nameof(ThrowPressureFixtureFailure), error!.StackTrace!);
        Assert.Empty(error.Data);
        var expected = PressureFixtureSuccessOrder.Where(stage =>
            failingStage != "disposal_files" || stage != "disposed_inert").ToArray();
        Assert.True(expected.SequenceEqual(probe.Calls),
            "RECON1_FIXTURE_ELIGIBLE_STAGES: " + string.Join(",", probe.Calls));
    }

    /// <summary>Body and all eligible cleanup failures remain exact ordered exception objects, with the body primary.</summary>
    [Fact]
    public async Task SpiritualPressureFixture_WorkFailurePreservesEveryCleanupFailure()
    {
        var primary = new InvalidOperationException("primary fixture operation");
        var probe = new PressureFixtureStageProbe { WorkFailure = primary };
        foreach (var stage in PressureFixtureSuccessOrder.Where(stage => stage is not "work" and not "disposed_inert"))
            probe.CleanupFailures.Add(stage, new IOException("secondary " + stage));
        var error = await Record.ExceptionAsync(() => ExecutePressureGroupFixtureStagesAsync(probe.Stages()));
        Assert.True(ReferenceEquals(primary, error), "RECON1_FIXTURE_PRIMARY_PRESERVED");
        Assert.Contains(nameof(ThrowPressureFixtureFailure), error!.StackTrace!);
        AssertPressureFixtureSecondaryFailures(error, probe.CleanupFailures);
        Assert.Equal(PressureFixtureSuccessOrder.Where(stage => stage != "disposed_inert"), probe.Calls);
    }

    /// <summary>Cancellation and existing arbitrary metadata survive multiple cleanup failures without key collision.</summary>
    [Fact]
    public async Task SpiritualPressureFixture_CancellationKeepsExistingDataAndNewCleanupFailures()
    {
        var primary = new OperationCanceledException("primary cancelled fixture operation");
        var metadataKey = new object();
        var metadataValue = new object();
        const string occupiedKey = "PressureGroup-window-0";
        var occupiedValue = new object();
        primary.Data.Add(metadataKey, metadataValue);
        primary.Data.Add(occupiedKey, occupiedValue);
        var window = new IOException("cancel cleanup window");
        var lease = new IOException("cancel cleanup lease");
        var probe = new PressureFixtureStageProbe { WorkFailure = primary };
        probe.CleanupFailures.Add("window", window);
        probe.CleanupFailures.Add("lease", lease);
        var error = await Record.ExceptionAsync(() => ExecutePressureGroupFixtureStagesAsync(probe.Stages()));
        Assert.True(ReferenceEquals(primary, error), "RECON1_FIXTURE_CANCELLATION_PRIMARY_PRESERVED");
        Assert.Contains(nameof(ThrowPressureFixtureFailure), error!.StackTrace!);
        Assert.Same(metadataValue, error.Data[metadataKey]);
        Assert.Same(occupiedValue, error.Data[occupiedKey]);
        var addedKeys = error.Data.Keys.Cast<object>().Where(key =>
            !ReferenceEquals(key, metadataKey) && !Equals(key, occupiedKey)).ToArray();
        Assert.Equal(2, addedKeys.Length);
        Assert.StartsWith("PressureGroup-window-", Assert.IsType<string>(addedKeys[0]));
        Assert.StartsWith("PressureGroup-lease-", Assert.IsType<string>(addedKeys[1]));
        Assert.Same(window, error.Data[addedKeys[0]]);
        Assert.Same(lease, error.Data[addedKeys[1]]);
        Assert.Equal(PressureFixtureSuccessOrder, probe.Calls);
    }

    /// <summary>Partial allocation cleans only retained owners, including a context retained before preparation failed.</summary>
    /// <param name="allocatedOwners">None, context, context plus lease, or context plus lease plus capture.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task SpiritualPressureFixture_PartialAllocationRetainsEligibleCleanup(int allocatedOwners)
    {
        var primary = new IOException("partial preparation failure");
        var probe = new PressureFixtureStageProbe { WorkFailure = primary, AllocatedOwners = allocatedOwners };
        var error = await Record.ExceptionAsync(() => ExecutePressureGroupFixtureStagesAsync(probe.Stages()));
        Assert.Same(primary, error);
        var expected = new List<string> { "work" };
        if (allocatedOwners >= 3) expected.Add("capture_implicit");
        if (allocatedOwners >= 2) expected.AddRange(["no_private_pair", "lease"]);
        if (allocatedOwners >= 1) expected.AddRange(["context", "root"]);
        Assert.True(expected.SequenceEqual(probe.Calls),
            "RECON1_FIXTURE_PARTIAL_OWNERS: " + string.Join(",", probe.Calls));
    }

    /// <summary>The existing comparison helper must not let an eligible closing assertion replace its query failure.</summary>
    /// <param name="current">Whether the original helper must attempt the closing assertion.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpiritualPressureFixture_ComparisonFailureKeepsPrimaryAndCleanup(bool current)
    {
        var primary = new IOException("comparison source read");
        var cleanup = new InvalidOperationException("comparison inertness assertion");
        var calls = 0;
        var error = await Record.ExceptionAsync(() => ExecutePressureGroupComparisonStagesAsync(
            () => { ThrowPressureFixtureFailure(primary); return Task.FromResult(new ValidationService.SpiritualOrdinaryPressureComparisonResult(null, [])); },
            () => current,
            () => { calls++; ThrowPressureFixtureFailure(cleanup); return Task.CompletedTask; }));
        Assert.Equal(current ? 1 : 0, calls);
        Assert.True(ReferenceEquals(primary, error), "RECON1_COMPARISON_PRIMARY_PRESERVED");
        Assert.Contains(nameof(ThrowPressureFixtureFailure), error!.StackTrace!);
        AssertPressureFixtureSecondaryFailures(error, current
            ? new Dictionary<string, Exception> { ["comparison_window"] = cleanup }
            : new Dictionary<string, Exception>());
    }

    /// <summary>Successful comparison returns the same detached result and preserves original closing eligibility.</summary>
    /// <param name="current">Whether the original current-owner closing assertion is eligible.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpiritualPressureFixture_ComparisonSuccessKeepsClosingEligibility(bool current)
    {
        var expected = new ValidationService.SpiritualOrdinaryPressureComparisonResult(null, []);
        var calls = 0;
        var actual = await ExecutePressureGroupComparisonStagesAsync(() => Task.FromResult(expected),
            () => current, () => { calls++; return Task.CompletedTask; });
        Assert.Same(expected, actual);
        Assert.Equal(current ? 1 : 0, calls);
    }

    /// <summary>Throws from one stable non-inlined frame so stack preservation is observable without reflection.</summary>
    /// <param name="error">Exact exception object authored by the case.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowPressureFixtureFailure(Exception error) => throw error;

    /// <summary>Requires the existing first-failure/Data idiom with exact stage labels and ordered secondary objects.</summary>
    /// <param name="primary">The unchanged first exception.</param>
    /// <param name="expected">Eligible secondary failures in fixed cleanup order.</param>
    private static void AssertPressureFixtureSecondaryFailures(Exception primary, IReadOnlyDictionary<string, Exception> expected)
    {
        Assert.Equal(expected.Count, primary.Data.Count);
        var actualKeys = primary.Data.Keys.Cast<string>().ToArray();
        var actualValues = primary.Data.Values.Cast<Exception>().ToArray();
        var index = 0;
        foreach (var (stage, error) in expected)
        {
            Assert.StartsWith("PressureGroup-" + stage + "-", actualKeys[index]);
            Assert.Same(error, actualValues[index]);
            index++;
        }
    }

    /// <summary>Cheap fixed-stage effects for this fixture alone; never substitutes live game authority.</summary>
    private sealed class PressureFixtureStageProbe
    {
        internal readonly List<string> Calls = [];
        internal readonly Dictionary<string, Exception> CleanupFailures = new(StringComparer.Ordinal);
        internal Exception? WorkFailure;
        internal int AllocatedOwners = 4;
        private bool _disposalFiles;

        /// <summary>Returns the same fixed stage contract used by the actual retained-pressure fixture.</summary>
        /// <returns>Callbacks recording effects and exact injected exceptions.</returns>
        internal PressureGroupFixtureStages Stages() => new(
            Work: () =>
            {
                Calls.Add("work");
                if (WorkFailure is not null) ThrowPressureFixtureFailure(WorkFailure);
                return Task.CompletedTask;
            },
            HasWindow: () => AllocatedOwners >= 4,
            HasCapture: () => AllocatedOwners >= 3,
            HasLease: () => AllocatedOwners >= 2,
            HasContext: () => AllocatedOwners >= 1,
            AssertWindowInert: () => { Run("window"); return Task.CompletedTask; },
            CaptureDisposalFiles: () => { Run("disposal_files"); _disposalFiles = true; },
            DisposeCaptureExplicitly: () => Run("capture_explicit"),
            AssertDisposedCaptureInert: () => { if (_disposalFiles) Run("disposed_inert"); },
            DisposeCaptureImplicitly: () => Run("capture_implicit"),
            AssertNoPrivatePair: () => Run("no_private_pair"),
            DisposeLease: () => { Run("lease"); return ValueTask.CompletedTask; },
            DisposeContext: () => { Run("context"); return Task.CompletedTask; },
            AssertRootRemoved: () => Run("root"));

        /// <summary>Records one attempted stage before throwing its exact case-authored failure.</summary>
        /// <param name="stage">One fixed fixture stage.</param>
        private void Run(string stage)
        {
            Calls.Add(stage);
            if (CleanupFailures.TryGetValue(stage, out var error)) ThrowPressureFixtureFailure(error);
        }
    }
}
