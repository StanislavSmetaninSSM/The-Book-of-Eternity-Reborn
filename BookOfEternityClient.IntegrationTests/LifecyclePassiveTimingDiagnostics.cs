using System.Globalization;
using BookOfEternityClient.Core;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Lifecycle output adapter over the existing persistent writer and original-event capture.
/// T599 baseline deliberately drains only after the caller's resources have closed.
/// </summary>
internal sealed class LifecyclePassiveTimingDiagnostics(string caseName, ITestOutputHelper original)
    : ITestOutputHelper, IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly List<string> _beforeBinding = [];
    private JointBundleDiagnosticOutput? _output;
    private OperationTimingAccumulator.Window? _window;
    private Task? _completion;

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
            _output = new JointBundleDiagnosticOutput(caseName, original, actualFixtureRoot: actualRoot);
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
            // Baseline: the existing component transport also waits for the
            // functional call to return. No continuing consumer exists in T599.
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
        TaskCompletionSource completion;
        lock (_gate)
        {
            if (_completion is not null) return _completion;
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _completion = completion.Task;
        }
        Exception? closingFailure = null;
        try { CompleteAfterReturn(); }
        catch (Exception error) { closingFailure = error; }
        if ((primaryFailure ?? closingFailure) is { } failure) completion.SetException(failure);
        else completion.SetResult();
        return completion.Task;
    }

    private void CompleteAfterReturn()
    {
        var output = RequireBound();
        var failures = new List<Exception>();
        if (_window is { } window)
        {
            window.Dispose();
            Attempt(() =>
            {
                foreach (var row in window.DrainEvents()) output.Timings.ObserveRecorded(row);
                output.CompletePassiveTiming(window.Capture(), window.CaptureEventStatus());
            }, "lifecycle-final-drain");
        }
        else Attempt(() => throw new InvalidDataException("Lifecycle timing was never armed."), "lifecycle-window-missing");

        // Observe the caller's real cleanup outcome. The adapter never deletes
        // or retries cleanup, and never invents the swallowed disposer exception.
        var removed = !Directory.Exists(output.ExpectedOwnedRoot);
        var cleanupNotes = removed ? Array.Empty<string>() :
            new[] { "Actual registered root remains after caller cleanup; underlying disposer exception is unavailable." };
        Attempt(output.Finish, "lifecycle-finish");
        // Finish can throw after writing its report. Still attempt the exact
        // root receipt, retaining both original failures rather than skipping it.
        Attempt(() => output.RecordFixtureClosing(output.ExpectedOwnedRoot, retained: !removed, removed, cleanupNotes),
            "lifecycle-fixture-receipt");
        if (!removed) failures.Add(new InvalidDataException("Lifecycle fixture removal is unproven; see the actual-root closing receipt."));
        if (failures.Count != 0) throw new AggregateException("Lifecycle diagnostics or fixture closing incomplete.", failures);

        void Attempt(Action action, string stage)
        {
            try { action(); }
            catch (Exception error)
            {
                failures.Add(error);
                output.Timings.RecordDiagnosticFailure(error, stage);
            }
        }
    }

    private JointBundleDiagnosticOutput RequireBound() =>
        _output ?? throw new InvalidOperationException("Bind the actual lifecycle fixture before timing or completion.");

    /// <summary>Uses the same completion operation; callers supply any primary error before disposal.</summary>
    /// <returns>The same final-drain/closing result, including any previously retained failure.</returns>
    public ValueTask DisposeAsync() => new(CompleteAsync());
}
