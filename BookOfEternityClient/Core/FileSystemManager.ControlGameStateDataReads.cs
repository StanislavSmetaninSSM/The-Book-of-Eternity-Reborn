using System.Diagnostics;

namespace BookOfEternityClient.Core;

public partial class FileSystemManager
{
    private readonly AsyncLocal<ControlGameStateReadActivity?> _controlGameStateReadActivity = new();
    private long _clientControlCalls, _clientControlHits, _clientControlFastHits, _clientControlTicks;
    private long _realmCalls, _realmHits, _realmFastHits, _realmTicks;

    /// <summary>
    /// Selects source DATA reuse for the two control phases; false supplies an internal fixture control.
    /// </summary>
    internal bool ControlGameStateDataReuseEnabled { get; set; } = true;

    /// <summary>
    /// Executes an existing control phase with its original rules and the shared temporary source service.
    /// </summary>
    /// <param name="realm">
    /// Whether this is RealmSegregation rather than ClientOwnedControlFiles.
    /// </param>
    /// <param name="originalOnly">
    /// Whether the phase requires its original nested-lease route instead of an additional source lease.
    /// </param>
    /// <param name="validate">
    /// The original validation body, including ordered issues and exceptions; no verdict is cached.
    /// </param>
    /// <returns>
    /// Completion after the original body and, for eligible reuse, its existing fresh source fence.
    /// </returns>
    internal async Task ReadControlGameStateDataAsync(bool realm, bool originalOnly, Func<Task> validate)
    {
        ArgumentNullException.ThrowIfNull(validate);
        var previous = _controlGameStateReadActivity.Value;
        var activity = new ControlGameStateReadActivity();
        _controlGameStateReadActivity.Value = activity;
        var started = Stopwatch.GetTimestamp();
        try
        {
            if (originalOnly || !ControlGameStateDataReuseEnabled) await validate();
            else await ReadGameStateDataAsync(async () => { await validate(); return true; });
        }
        finally
        {
            activity.Closed = true;
            _controlGameStateReadActivity.Value = previous;
            var ticks = Stopwatch.GetTimestamp() - started;
            var hits = Interlocked.Read(ref activity.SourceHits);
            if (realm)
            {
                Interlocked.Increment(ref _realmCalls);
                Interlocked.Add(ref _realmHits, hits);
                Interlocked.Add(ref _realmFastHits, Interlocked.Read(ref activity.FastSourceHits));
                Interlocked.Add(ref _realmTicks, ticks);
            }
            else
            {
                Interlocked.Increment(ref _clientControlCalls);
                Interlocked.Add(ref _clientControlHits, hits);
                Interlocked.Add(ref _clientControlFastHits, Interlocked.Read(ref activity.FastSourceHits));
                Interlocked.Add(ref _clientControlTicks, ticks);
            }
        }
    }

    /// <summary>
    /// Records one returned service hit without retaining data or invoking diagnostic callbacks.
    /// </summary>
    /// <param name="fast">
    /// Whether the selected GameState fast route returned the hit instead of the ordinary source reader.
    /// </param>
    private void RecordControlGameStateSourceHit(bool fast)
    {
        if (_controlGameStateReadActivity.Value is not { Closed: false } activity) return;
        Interlocked.Increment(ref activity.SourceHits);
        if (fast) Interlocked.Increment(ref activity.FastSourceHits);
    }

    /// <summary>
    /// Captures passive cumulative phase work, including failed calls, without running observers.
    /// </summary>
    /// <returns>
    /// Per-phase elapsed wall time and actual implicit service hits; source verification remains included in time.
    /// </returns>
    internal ControlGameStateStatistics CaptureControlGameStateStatistics() => new(
        new(Interlocked.Read(ref _clientControlCalls), Interlocked.Read(ref _clientControlHits), Interlocked.Read(ref _clientControlFastHits),
            Interlocked.Read(ref _clientControlTicks) * 1000d / Stopwatch.Frequency),
        new(Interlocked.Read(ref _realmCalls), Interlocked.Read(ref _realmHits), Interlocked.Read(ref _realmFastHits),
            Interlocked.Read(ref _realmTicks) * 1000d / Stopwatch.Frequency));

    /// <summary>
    /// Identifies a functional phase's passive hit observations until that phase closes.
    /// </summary>
    private sealed class ControlGameStateReadActivity
    {
        internal long SourceHits, FastSourceHits;
        internal volatile bool Closed;
    }

    /// <summary>
    /// Contains cumulative work for one selected control phase, without retaining source DATA or a verdict.
    /// </summary>
    /// <param name="Calls">
    /// Completed or failed phase invocations.
    /// </param>
    /// <param name="SourceHits">
    /// Actual returned service hits through fast and ordinary source readers, excluding fresh exit reads.
    /// </param>
    /// <param name="FastSourceHits">
    /// Hits through the selected phase adapter that avoid the original ordinary reader entry.
    /// </param>
    /// <param name="ElapsedMilliseconds">
    /// Summed wall time of those invocations, including entry and fresh source verification.
    /// </param>
    internal readonly record struct ControlGameStatePhaseStatistics(long Calls, long SourceHits, long FastSourceHits, double ElapsedMilliseconds);

    /// <summary>
    /// Separates cumulative ClientOwnedControlFiles and RealmSegregation observations.
    /// </summary>
    /// <param name="ClientOwnedControlFiles">
    /// Client-owned control phase observations, including original proof-route fallbacks.
    /// </param>
    /// <param name="RealmSegregation">
    /// Realm phase observations, including unsupported source-route fallbacks.
    /// </param>
    internal readonly record struct ControlGameStateStatistics(
        ControlGameStatePhaseStatistics ClientOwnedControlFiles, ControlGameStatePhaseStatistics RealmSegregation);
}
