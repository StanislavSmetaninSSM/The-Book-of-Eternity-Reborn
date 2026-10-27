using System.Diagnostics;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    private sealed partial class Fixture
    {
        private readonly OperationTimingAccumulator? _passiveTimings;
        private readonly OperationTimingAccumulator.Window? _passiveWindow;
        private readonly LifecyclePassiveTimingDiagnostics.RecordedTransfer? _passiveTransfer;

        /// <summary>Transfers every pending original passive event before sampling its retained observation ordinal.</summary>
        /// <returns>The current complete transferred row count; default callback mode retains its original raw count.</returns>
        internal int CaptureObservationOrdinal()
        {
            if (_passiveTransfer is { } transfer)
                lock (transfer.BoundaryGate)
                {
                    transfer.DrainAndFlush();
                    return _observations.Count;
                }
            FlushPassiveTimings();
            return _observations.Count;
        }

        /// <summary>Transfers completed original copy-phase events, then flushes the same writer for pre-close visibility.</summary>
        /// <returns>The retained ordinal; ordinary drain/write failures remain deferred in the existing diagnostic ledger.</returns>
        internal int CaptureCopyPhaseBoundary()
        {
            if (_passiveTransfer is { } transfer)
                lock (transfer.BoundaryGate)
                {
                    var transferred = CaptureObservationOrdinal();
                    _output.Timings.Flush();
                    return transferred;
                }
            var ordinal = CaptureObservationOrdinal();
            _output.Timings.Flush();
            return ordinal;
        }

        /// <summary>
        /// Transfers original scalar events only after a functional call has returned; no writer is called by recording.
        /// </summary>
        private void FlushPassiveTimings()
        {
            if (_passiveTransfer is { } transfer)
            {
                transfer.DrainAndFlush();
                return;
            }
            if (_passiveWindow is null) return;
            try
            {
                foreach (var row in _passiveWindow.DrainEvents())
                {
                    _observations.Enqueue((row.Name, row.State, row.ElapsedTicks * 1000d / Stopwatch.Frequency, row.ScopeId));
                    _output.Timings.ObserveRecorded(row);
                }
            }
            catch (Exception failure)
            {
                // A detached-batch or storage failure cannot become complete capture.
                _output.Timings.RecordDiagnosticFailure(failure, "passive-timing-drain");
            }
        }

        /// <summary>
        /// Closes registrations after resource closing, then checks full event and persistent-write accounting.
        /// </summary>
        /// <returns>Completion after any shared transfer settled; default manual closing remains synchronous.</returns>
        private Task CompletePassiveTimingsAsync()
        {
            if (_passiveTransfer is { } transfer) return CompleteTransferredPassiveTimingsAsync(transfer);
            if (_passiveWindow is null) return Task.CompletedTask;
            _passiveWindow.Dispose();
            FlushPassiveTimings();
            _output.CompletePassiveTiming(_passiveWindow.Capture(), _passiveWindow.CaptureEventStatus());
            return Task.CompletedTask;
        }

        /// <summary>Closes the real recorder after resources, settles the one transfer, then always attempts original accounting.</summary>
        /// <param name="transfer">The same component DATA transfer owner registered at construction.</param>
        /// <returns>Completion before writer Finish and the unchanged component root/error policy.</returns>
        private async Task CompleteTransferredPassiveTimingsAsync(LifecyclePassiveTimingDiagnostics.RecordedTransfer transfer)
        {
            _passiveWindow!.Dispose();
            try { await transfer.StopAndJoinAsync().ConfigureAwait(false); }
            catch (Exception failure) { _output.Timings.RecordDiagnosticFailure(failure, "passive-consumer-join"); }
            _output.CompletePassiveTiming(_passiveWindow.Capture(), _passiveWindow.CaptureEventStatus());
        }
    }
}
