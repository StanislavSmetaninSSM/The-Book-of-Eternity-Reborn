using System.Diagnostics;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Measures only Task5-local operations while preserving an operation failure and its original throwing stack if timing output also fails.</summary>
    /// <typeparam name="T">The unchanged real operation result type.</typeparam>
    /// <param name="phase">The named phase, including its warm/cold ownership scope where applicable.</param>
    /// <param name="label">The independent case identity.</param>
    /// <param name="operation">The actual operation; allocating callbacks retain cleanup ownership before returning.</param>
    /// <returns>The unchanged real result, or the original operation error with any timing error retained as secondary metadata.</returns>
    private async Task<T> MeasureGuardianGroupZeroPhaseAsync<T>(string phase, string label, Func<Task<T>> operation)
    {
        var started = Stopwatch.GetTimestamp();
        Exception? primary = null;
        try { return await operation(); }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try { WritePressureOwnerTiming(phase, label, started); }
            catch (Exception timingError) when (primary is not null)
            {
                RetainPressureFixtureFailure(primary, "guardian_group_zero_timing_" + phase, timingError);
            }
        }
    }

    /// <summary>Measures a Task5-local resultless phase through the same primary-preserving timing boundary.</summary>
    /// <param name="phase">The named owned preparation, observation or cleanup stage.</param>
    /// <param name="label">The independent case identity.</param>
    /// <param name="operation">The real work; diagnostic failure after success still fails the phase.</param>
    /// <returns>Completion only when both the real work and timing output succeed.</returns>
    private Task MeasureGuardianGroupZeroPhaseAsync(string phase, string label, Func<Task> operation) =>
        MeasureGuardianGroupZeroPhaseAsync(phase, label, async () => { await operation(); return true; });

    /// <summary>Observes the genuine frontier through the Task5-local safe timer without initializing source computations or dropping failure details.</summary>
    /// <param name="capture">The currently retained genuine capture.</param>
    /// <param name="lease">Its real active physical lease.</param>
    /// <param name="label">The exact ownership/observation label.</param>
    /// <returns>Detached frontier DATA with unchanged source/evaluation/freshness counters.</returns>
    private async Task<ValidationService.SpiritualOrdinaryPressureProbeState> ReadGuardianGroupZeroSnapshotAsync(
        ValidationService.SpiritualOriginalTurnCapture capture, FileSystemManager.CanonicalWriteLease lease, string label)
    {
        var counts = capture.ReadExistingStrainReuseCounts();
        var state = await MeasureGuardianGroupZeroPhaseAsync("guardian_group_zero_snapshot", label,
            () => capture.ReadExistingPressureOrdinaryProbeStateAsync(lease));
        Assert.Equal(counts, capture.ReadExistingStrainReuseCounts());
        return state;
    }
}
