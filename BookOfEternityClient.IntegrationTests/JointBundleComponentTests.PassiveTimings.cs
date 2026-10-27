using System.Diagnostics;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    private sealed partial class Fixture
    {
        private readonly OperationTimingAccumulator? _passiveTimings;
        private readonly OperationTimingAccumulator.Window? _passiveWindow;

        /// <summary>Transfers every pending original passive event before sampling its retained observation ordinal.</summary>
        /// <returns>The current complete transferred row count; default callback mode retains its original raw count.</returns>
        internal int CaptureObservationOrdinal()
        {
            FlushPassiveTimings();
            return _observations.Count;
        }

        /// <summary>Transfers completed original copy-phase events, then flushes the same writer for pre-close visibility.</summary>
        /// <returns>The retained ordinal; ordinary drain/write failures remain deferred in the existing diagnostic ledger.</returns>
        internal int CaptureCopyPhaseBoundary()
        {
            var ordinal = CaptureObservationOrdinal();
            _output.Timings.Flush();
            return ordinal;
        }

        /// <summary>
        /// Transfers original scalar events only after a functional call has returned; no writer is called by recording.
        /// </summary>
        private void FlushPassiveTimings()
        {
            if (_passiveWindow is null) return;
            try
            {
                foreach (var row in _passiveWindow.DrainEvents())
                {
                    _observations.Enqueue((row.Name, row.State, row.ElapsedTicks * 1000d / Stopwatch.Frequency));
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
        private void CompletePassiveTimings()
        {
            if (_passiveWindow is null) return;
            _passiveWindow.Dispose();
            FlushPassiveTimings();
            _output.CompletePassiveTiming(_passiveWindow.Capture(), _passiveWindow.CaptureEventStatus());
        }
    }
}
