using System.Collections.Concurrent;
using System.Diagnostics;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

internal sealed partial class LifecyclePassiveTimingDiagnostics
{
    /// <summary>One serialized original-event transfer for the existing lifecycle or explicit component DATA queue.</summary>
    internal sealed class RecordedTransfer
    {
        private readonly OperationTimingAccumulator.Window _window;
        private readonly JointCleanupProfileDiagnosticCapture _capture;
        private readonly ConcurrentQueue<(string Name, string Boundary, double Milliseconds, long SourceScopeId)>? _observations;
        private readonly bool _failAfterDetach;
        private readonly CancellationTokenSource _stop = new();
        private Task<DrainReceipt>? _consumer;
        private Task<DrainReceipt?>? _completion;
        private TransferState _state;
        private long _batches, _largestBatch;
        private bool _injected;

        /// <summary>Serializes complete detach/queue/write batches with synchronous ordinal/snapshot/message boundaries.</summary>
        internal object BoundaryGate { get; } = new();

        /// <summary>Binds only the existing recorder, capture and optional diagnostic DATA queue.</summary>
        /// <param name="window">The actual original-event window.</param>
        /// <param name="capture">The same existing persistent capture/writer.</param>
        /// <param name="observations">Optional same component queue; never a game capability or functional callback.</param>
        /// <param name="failAfterDetach">Existing closed diagnostic fault; a lost batch stays incomplete.</param>
        internal RecordedTransfer(OperationTimingAccumulator.Window window, JointCleanupProfileDiagnosticCapture capture,
            ConcurrentQueue<(string Name, string Boundary, double Milliseconds, long SourceScopeId)>? observations = null,
            bool failAfterDetach = false)
        {
            _window = window; _capture = capture; _observations = observations; _failAfterDetach = failAfterDetach;
        }

        /// <summary>Selects the component scaffold's original after-return policy; no continuing worker starts before actual RED.</summary>
        /// <param name="window">The component's actual recorder window.</param>
        /// <param name="capture">Its same persistent capture.</param>
        /// <param name="observations">Its existing exact observation queue.</param>
        /// <returns>One deliberately unstarted normal transfer owner, with ordinary final drain at close.</returns>
        internal static RecordedTransfer CreateForComponent(OperationTimingAccumulator.Window window,
            JointCleanupProfileDiagnosticCapture capture,
            ConcurrentQueue<(string Name, string Boundary, double Milliseconds, long SourceScopeId)> observations) =>
            new(window, capture, observations);

        /// <summary>Starts at most one existing detached loop without inheriting the caller's execution context.</summary>
        internal void Start()
        {
            lock (BoundaryGate)
            {
                if (_state != TransferState.Unstarted || _completion is not null)
                    throw new InvalidOperationException("Recorded transfer can start only once before completion.");
                try
                {
                    if (ExecutionContext.IsFlowSuppressed()) _consumer = StartConsumer(this);
                    else
                    {
                        using var suppression = ExecutionContext.SuppressFlow();
                        _consumer = StartConsumer(this); // synchronous scheduling only; no await under suppression
                    }
                    _state = TransferState.Running;
                }
                catch (Exception error)
                {
                    _state = TransferState.SchedulingFailed;
                    _capture.RecordDiagnosticFailure(error, "lifecycle-consumer-start");
                    _consumer = Task.FromException<DrainReceipt>(error);
                }
            }
        }

        /// <summary>Transfers pending rows through this one owner; faults defer in the existing component ledger.</summary>
        internal void DrainAndFlush()
        {
            try { Drain(); }
            catch (Exception error) { _capture.RecordDiagnosticFailure(error, "passive-timing-drain"); }
        }

        /// <summary>Shares one stop/join; normal unstarted RED drains once, failed scheduling claims no worker or tail.</summary>
        /// <returns>The actual worker's receipt, or null for deliberately unstarted normal component mode.</returns>
        internal Task<DrainReceipt?> StopAndJoinAsync()
        {
            TaskCompletionSource<DrainReceipt?> signal;
            lock (BoundaryGate)
            {
                if (_completion is not null) return _completion;
                signal = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _completion = signal.Task;
            }
            _ = CompleteAndSignalAsync(signal);
            return signal.Task;
        }

        private async Task CompleteAndSignalAsync(TaskCompletionSource<DrainReceipt?> signal)
        {
            try
            {
                _stop.Cancel();
                if (_consumer is { } consumer) signal.SetResult(await consumer.ConfigureAwait(false));
                else
                {
                    // Deliberately unstarted normal scaffold, not a scheduling-failure recovery.
                    DrainAndFlush();
                    signal.SetResult(null);
                }
            }
            catch (Exception error) { signal.SetException(error); }
            finally { _stop.Dispose(); }
        }

        private static Task<DrainReceipt> StartConsumer(RecordedTransfer input) =>
            Task.Factory.StartNew(static state => ConsumeAsync((RecordedTransfer)state!), input,
                CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default).Unwrap();

        private static async Task<DrainReceipt> ConsumeAsync(RecordedTransfer input)
        {
            var contextPresent = ContextSentinelForControl.Value is not null;
            try
            {
                while (!input._stop.IsCancellationRequested)
                {
                    input.Drain();
                    try { await Task.Delay(25, input._stop.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (input._stop.IsCancellationRequested) { break; }
                }
            }
            catch (Exception error)
            {
                input._capture.RecordDiagnosticFailure(error, "lifecycle-continuing-drain");
                // Preserve the original functional flow and the single worker-owned final-tail attempt.
                try { await Task.Delay(Timeout.InfiniteTimeSpan, input._stop.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (input._stop.IsCancellationRequested) { }
            }
            finally
            {
                try { input.Drain(); }
                catch (Exception error) { input._capture.RecordDiagnosticFailure(error, "lifecycle-final-drain"); }
            }
            return new(contextPresent, input._batches, input._largestBatch);
        }

        private void Drain()
        {
            lock (BoundaryGate)
            {
                var batch = _window.DrainEvents();
                if (batch.Length == 0) return;
                _batches++;
                _largestBatch = Math.Max(_largestBatch, batch.LongLength);
                if (_failAfterDetach && !_injected)
                {
                    _injected = true;
                    throw new IOException("Injected lifecycle drain failure after actual event detach.");
                }
                for (var index = 0; index < batch.Length; index++)
                {
                    var row = batch[index];
                    _observations?.Enqueue((row.Name, row.State, row.ElapsedTicks * 1000d / Stopwatch.Frequency, row.ScopeId));
                    _capture.ObserveRecorded(row);
                    if ((index + 1) % 1024 == 0) _capture.Flush();
                }
                _capture.Flush();
            }
        }

        /// <summary>Unstarted is normal RED; SchedulingFailed is never a successful worker or final-tail claim.</summary>
        private enum TransferState { Unstarted, Running, SchedulingFailed }
    }

    /// <summary>Actual detached-worker context and batch scalars only; no caller capability or exported event array.</summary>
    /// <param name="ContextSentinelPresent">Whether the existing cheap context sentinel was actually seen by the worker.</param>
    /// <param name="Batches">Number of actual nonempty transferred batches.</param>
    /// <param name="LargestBatch">Largest actual detached batch.</param>
    internal sealed record DrainReceipt(bool ContextSentinelPresent, long Batches, long LargestBatch);
}
