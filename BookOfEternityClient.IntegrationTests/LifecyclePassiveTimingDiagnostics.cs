using System.Globalization;
using System.Runtime.ExceptionServices;
using BookOfEternityClient.Core;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Lifecycle output adapter over the existing persistent writer and original-event capture.
/// One detached consumer persists original events until caller resources have closed.
/// </summary>
internal sealed partial class LifecyclePassiveTimingDiagnostics(string caseName, ITestOutputHelper original,
    LifecycleTimingFaultForControl faultForControl = LifecycleTimingFaultForControl.None, bool gzipRecords = false)
    : ITestOutputHelper, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly List<string> _beforeBinding = [];
    private JointBundleDiagnosticOutput? _output;
    private OperationTimingAccumulator.Window? _window;
    private Task? _completion;
    private RecordedTransfer? _transfer;
    private DrainReceipt? _drained;

    // Typed context sentinel for cheap isolation controls only. It is never
    // populated by lifecycle/game wiring and cannot carry an authority object.
    internal static AsyncLocal<string?> ContextSentinelForControl { get; } = new();
    internal bool? ConsumerSawContextSentinel => _drained?.ContextSentinelPresent;
    internal long? ConsumerBatches => _drained?.Batches;
    internal long? LargestDetachedBatch => _drained?.LargestBatch;

    /// <summary>Gets the one diagnostic root registered after actual fixture allocation.</summary>
    internal string RegisteredRoot => RequireBound().ExpectedOwnedRoot;
    /// <summary>Gets the live-readable original-message and timing destination.</summary>
    internal string RecordsPath => RequireBound().RecordsPath;
    /// <summary>Gets the actual-root and assembly identity registered before functional work.</summary>
    internal string InitialPath => RequireBound().InitialPath;
    /// <summary>Gets the completion ledger; absence on process kill is not success.</summary>
    internal string ReportPath => RequireBound().ReportPath;

    /// <summary>Binds existing actual fixture DATA without creating or adopting a game root.</summary>
    /// <param name="actualRoot">The absolute already allocated fixture container.</param>
    internal void BindFixtureRoot(string actualRoot)
    {
        lock (_gate)
        {
            if (_output is not null || _completion is not null)
                throw new InvalidOperationException("Lifecycle diagnostic binding is available only once before completion.");
            _output = new JointBundleDiagnosticOutput(caseName, original,
                disposeWriterForControl: faultForControl == LifecycleTimingFaultForControl.ClosedWriter, actualFixtureRoot: actualRoot, gzipRecords: gzipRecords);
            _output.RequirePassiveTiming();
            foreach (var message in _beforeBinding) PersistMessage(message);
            _beforeBinding.Clear();
        }
    }

    /// <summary>Attaches one existing full-event window at its original measurement boundary.</summary>
    /// <param name="window">The sealed source recorder's actual window, never a game capability.</param>
    internal void Arm(OperationTimingAccumulator.Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        lock (_gate)
        {
            _ = RequireBound();
            if (_window is not null || _completion is not null)
                throw new InvalidOperationException("Lifecycle timing window can be armed only once before completion.");
            if (!window.CaptureEventStatus().Enabled || window.Capture().Closed)
                throw new InvalidDataException("Lifecycle timing requires an open original-event window.");
            _window = window;
            _transfer = new RecordedTransfer(window, _output!.Timings,
                failAfterDetach: faultForControl == LifecycleTimingFaultForControl.DrainAfterDetach);
            _transfer.Start();
        }
    }

    /// <summary>Persists every original full message and forwards it once to its original receiver.</summary>
    /// <param name="message">The unchanged original message, including all raw payload bytes encoded by its author.</param>
    public void WriteLine(string message)
    {
        lock (_gate)
        {
            if (_output is null) _beforeBinding.Add(message);
            else PersistMessage(message);
            // Preserve original forwarding and its original failure behavior.
            original.WriteLine(message);
        }
    }

    /// <summary>Formats once according to the original xUnit output contract.</summary>
    /// <param name="format">The original format string.</param>
    /// <param name="args">All original arguments, without truncation.</param>
    public void WriteLine(string format, params object[] args) =>
        WriteLine(string.Format(CultureInfo.CurrentCulture, format, args));

    private void PersistMessage(string message)
    {
        try { _output!.WriteLine(message); }
        catch (Exception error)
        {
            // A new persistent-sink fault cannot short-circuit the original body.
            _output!.Timings.RecordDiagnosticFailure(error, "lifecycle-message-write");
        }
    }

    /// <summary>
    /// Closes after body, real fixture disposal and probe/resource closing; shares one final drain and receipt.
    /// </summary>
    /// <param name="primaryFailure">
    /// The original body/resource failure, when unwinding. It remains the primary exception object.
    /// </param>
    /// <returns>Completion after final accounting, hash and actual-root closing receipt were attempted.</returns>
    internal Task CompleteAsync(Exception? primaryFailure = null)
    {
        TaskCompletionSource signal;
        lock (_gate)
        {
            if (_completion is not null) return _completion;
            signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = signal.Task; // publish before any writer/reference can reenter
        }
        _ = CompleteAndSignalAsync(signal, primaryFailure);
        return signal.Task;
    }

    private async Task CompleteAndSignalAsync(TaskCompletionSource signal, Exception? primaryFailure)
    {
        try { await CompleteCoreAsync(primaryFailure).ConfigureAwait(false); signal.SetResult(); }
        catch (Exception error) { signal.SetException(error); }
    }

    private async Task CompleteCoreAsync(Exception? primaryFailure)
    {
        var failures = new List<Exception>();
        JointBundleDiagnosticOutput? output = null;
        try
        {
            output = RequireBound();
            if (_window is { } window && _transfer is { } transfer)
            {
                // The caller has already closed functional resources and the
                // real fixture/probe. End recording before requesting settlement.
                window.Dispose();
                try { _drained = await transfer.StopAndJoinAsync().ConfigureAwait(false); }
                catch (Exception error) { Record(error, "lifecycle-consumer-join"); }
                Attempt(() => output.CompletePassiveTiming(window.Capture(), window.CaptureEventStatus()),
                    "lifecycle-passive-accounting");
            }
            else Record(new InvalidDataException("Lifecycle timing was never armed."), "lifecycle-window-missing");

            var removed = false;
            string[] cleanupNotes;
            try
            {
                // Unlike Exists, the existing non-following probe does not turn
                // a replacement file, broken link or access error into absence.
                var entry = TrustedLocalFileScope.Probe(output.ExpectedOwnedRoot);
                removed = entry == TrustedLocalFileScope.EntryKind.Missing;
                cleanupNotes = removed ? [] :
                    ["Actual registered root entry remains (" + entry + "); underlying disposer exception is unavailable."];
            }
            catch (Exception error)
            {
                cleanupNotes = ["Actual root absence could not be observed: " + error];
                Record(error, "lifecycle-root-observation");
            }
            if (!removed)
                Record(new InvalidDataException("Lifecycle fixture removal is unproven; see the actual-root closing receipt."),
                    "lifecycle-root-retained");
            Attempt(output.Finish, "lifecycle-finish");
            // Finish may report a fault after closing/hashing. Still persist the
            // actual receipt; never invent the swallowed disposer exception.
            Attempt(() => output.RecordFixtureClosing(output.ExpectedOwnedRoot, retained: !removed, removed, cleanupNotes),
                "lifecycle-fixture-receipt");
        }
        catch (Exception error) { Record(error, "lifecycle-completion"); }
        if (primaryFailure is not null) ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        if (failures.Count != 0) throw new AggregateException("Lifecycle diagnostics or fixture closing incomplete.", failures);

        void Record(Exception error, string stage)
        {
            failures.Add(error);
            output?.Timings.RecordDiagnosticFailure(error, stage);
        }
        void Attempt(Action action, string stage)
        {
            try { action(); }
            catch (Exception error) { Record(error, stage); }
        }
    }

    /// <summary>Retains an actual resource-close failure without replacing the original body error.</summary>
    /// <param name="error">The actual caught closing exception.</param>
    /// <param name="stage">The resource whose existing close was attempted.</param>
    internal void RecordClosingFailure(Exception error, string stage) =>
        _output?.Timings.RecordDiagnosticFailure(error, stage);

    private JointBundleDiagnosticOutput RequireBound() =>
        _output ?? throw new InvalidOperationException("Bind the actual lifecycle fixture before timing or completion.");

    /// <summary>Uses the same completion operation; callers supply any primary error before disposal.</summary>
    /// <returns>The same final-drain/closing result, including any previously retained failure.</returns>
    public ValueTask DisposeAsync() => new(CompleteAsync());
}

/// <summary>Closed diagnostic-only controls; no callbacks or game authority enter the consumer.</summary>
internal enum LifecycleTimingFaultForControl { None, ClosedWriter, DrainAfterDetach }
