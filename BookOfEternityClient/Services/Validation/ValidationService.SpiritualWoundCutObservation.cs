using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Separates completed evidence, unavailable diagnostics and a failed owning materialization.</summary>
    internal enum SpiritualPressureCutObservationStatus { Success, Unavailable, Failure }

    /// <summary>Immutable computation counters, never authority or a cached permission.</summary>
    /// <param name="Queries">Ordinary comparison requests.</param>
    /// <param name="SignedParentReads">Actual signed-parent resolutions.</param>
    /// <param name="OrdinaryEvaluations">Actual ordinary source computations.</param>
    /// <param name="ActualEvaluations">Actual owning source computations.</param>
    /// <param name="FreshInputChecks">Fresh ordinary physical-input checks.</param>
    /// <param name="FreshDependencyChecks">Fresh ordinary dependency checks.</param>
    internal sealed record SpiritualPressureCutCounts(int Queries, int SignedParentReads, int OrdinaryEvaluations,
        int ActualEvaluations, int FreshInputChecks, int FreshDependencyChecks);

    /// <summary>Cheap historical predicates sampled around the real cut without a nested query or journal export.</summary>
    /// <param name="WoundVersion">Actual current wound version.</param>
    /// <param name="DependencyCutVersion">Actual dependency-cut version.</param>
    /// <param name="SourceRevision">Actual source continuation revision.</param>
    /// <param name="NextOrdinal">Actual capture-owned next exchange ordinal.</param>
    /// <param name="MechanicsCurrent">Currentness of the exact warmed ordinary context.</param>
    /// <param name="ExactOwnersCurrent">Whether the actual owners still match the request.</param>
    internal sealed record SpiritualPressureCutStage(long WoundVersion, long DependencyCutVersion,
        long SourceRevision, int NextOrdinal, bool MechanicsCurrent, bool ExactOwnersCurrent);

    /// <summary>
    /// Detached composed evidence. Cold/warm comparisons precede speculation; before/after predicates surround only the real cut.
    /// No context, draft, lease, selection, preparation or mutation capability is returned.
    /// </summary>
    /// <param name="Cold">Detached pre-speculation cold result.</param>
    /// <param name="Warm">Detached pre-speculation warm result.</param>
    /// <param name="InitialCounts">Counters after selection, before the cold query.</param>
    /// <param name="ColdCounts">Counters after the cold query.</param>
    /// <param name="WarmCounts">Counters after the warm query.</param>
    /// <param name="AfterCutCounts">Counters immediately after the cut.</param>
    /// <param name="InitialWork">Source-reader work before the cold query.</param>
    /// <param name="ColdWork">Source-reader work after the cold query.</param>
    /// <param name="WarmWork">Source-reader work after the warm query.</param>
    /// <param name="AfterCutWork">Source-reader work immediately after the cut.</param>
    /// <param name="Before">Literal immediate pre-cut scalar observations.</param>
    /// <param name="After">Literal immediate post-cut scalar observations.</param>
    /// <param name="SameOrdinaryContext">Whether both stages retained the exact warmed context.</param>
    /// <param name="SameRoutingIdentity">Whether both stages retained the exact original routing epoch.</param>
    /// <param name="StaleReadReturnedNoData">Whether the actual stale branch refused wound DATA.</param>
    /// <param name="StaleReadCode">The sole actual stale-branch diagnostic code.</param>
    /// <param name="StaleReadIssueCount">Number of actual stale-branch diagnostics.</param>
    internal sealed record SpiritualPressureCutObservation(SpiritualOrdinaryPressureComparisonData Cold,
        SpiritualOrdinaryPressureComparisonData Warm, SpiritualPressureCutCounts InitialCounts,
        SpiritualPressureCutCounts ColdCounts, SpiritualPressureCutCounts WarmCounts,
        SpiritualPressureCutCounts AfterCutCounts, SpiritualPressureGroupComputationCounts InitialWork,
        SpiritualPressureGroupComputationCounts ColdWork, SpiritualPressureGroupComputationCounts WarmWork,
        SpiritualPressureGroupComputationCounts AfterCutWork, SpiritualPressureCutStage Before,
        SpiritualPressureCutStage After, bool SameOrdinaryContext, bool SameRoutingIdentity, bool StaleReadReturnedNoData,
        string? StaleReadCode, int StaleReadIssueCount);

    /// <summary>Historical evidence or an explicit refusal/failure description; null storage means no completed observation.</summary>
    /// <param name="Status">Whether evidence was collected, unavailable or the owner failed.</param>
    /// <param name="Observation">Complete detached evidence on success only.</param>
    /// <param name="Reason">Frozen refusal or failure description when evidence is absent.</param>
    internal sealed record SpiritualPressureCutObservationResult(SpiritualPressureCutObservationStatus Status,
        SpiritualPressureCutObservation? Observation, string? Reason);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        /// <summary>Retains one request privately until its exact owning invocation finishes or the capture closes.</summary>
        /// <param name="lease">Exact request lease, never returned in a report.</param>
        /// <param name="admission">Exact request admission, never returned in a report.</param>
        private sealed class RequestedPressureCutObservation(FileSystemManager.CanonicalWriteLease lease,
            WoundSourceAdmission admission)
        {
            /// <summary>Gets the privately retained exact request lease.</summary>
            internal FileSystemManager.CanonicalWriteLease Lease { get; } = lease;
            /// <summary>Gets the privately retained exact request admission.</summary>
            internal WoundSourceAdmission Admission { get; } = admission;
            /// <summary>Gets or sets detached pre-speculation comparison evidence.</summary>
            internal PressureCutWarmData? WarmData { get; set; }
            /// <summary>Gets or sets immediate pre-cut scalar evidence.</summary>
            internal SpiritualPressureCutStage? Before { get; set; }
            /// <summary>Gets or sets detached pre-cut witness facts.</summary>
            internal SpiritualPressureCutWitnessFacts? BeforeWitness { get; set; }
            /// <summary>Gets or sets complete detached stage evidence.</summary>
            internal SpiritualPressureCutObservation? Observation { get; set; }
            /// <summary>Gets or sets an immutable diagnostic refusal description.</summary>
            internal string? UnavailableReason { get; set; }
        }

        /// <summary>Retains already detached ordinary DATA and counters until the immediate cut samples are collected.</summary>
        /// <param name="Cold">Detached cold comparison DATA.</param>
        /// <param name="Warm">Detached warm comparison DATA.</param>
        /// <param name="InitialCounts">Counters before cold work.</param>
        /// <param name="ColdCounts">Counters after cold work.</param>
        /// <param name="WarmCounts">Counters after warm reuse.</param>
        /// <param name="InitialWork">Shared reader work before the cold query.</param>
        /// <param name="ColdWork">Shared reader work after the cold query.</param>
        /// <param name="WarmWork">Shared reader work after the warm query.</param>
        private sealed record PressureCutWarmData(SpiritualOrdinaryPressureComparisonData Cold,
            SpiritualOrdinaryPressureComparisonData Warm, SpiritualPressureCutCounts InitialCounts,
            SpiritualPressureCutCounts ColdCounts, SpiritualPressureCutCounts WarmCounts,
            SpiritualPressureGroupComputationCounts InitialWork, SpiritualPressureGroupComputationCounts ColdWork,
            SpiritualPressureGroupComputationCounts WarmWork);

        private RequestedPressureCutObservation? _requestedPressureCutObservation;
        private RequestedPressureCutObservation? _activePressureCutObservation;
        private SpiritualPressureCutObservationResult? _pressureCutObservation;

        /// <summary>Reads detached historical evidence without querying a live owner, including after disposal.</summary>
        /// <returns>The stored result, or null before an observation is recorded.</returns>
        internal SpiritualPressureCutObservationResult? ReadPressureCutObservation() =>
            Volatile.Read(ref _pressureCutObservation);

        /// <summary>Requests observation for one exact current admission without source work, mutation or new authority.</summary>
        /// <param name="lease">Actual active canonical lease retained for this request only.</param>
        /// <param name="admission">Exact retained current unmaterialized admission.</param>
        /// <returns>Completion after arming the one-shot request; invalid and duplicate requests never replace it.</returns>
        internal async Task RequestPressureCutObservationAsync(FileSystemManager.CanonicalWriteLease lease,
            WoundSourceAdmission admission)
        {
            ArgumentNullException.ThrowIfNull(admission);
            await _gate.WaitAsync();
            try
            {
                EnsureClosedPressureProbeContourUnderGate(lease);
                if (!OwnsWoundAdmission(admission) || _effects!.HasPendingWoundIntegration ||
                    _woundDeclines.ContainsKey(admission) ||
                    _woundSelections.TryGetValue(admission, out var selection) && _materializedWounds.ContainsKey(selection))
                    throw new InvalidOperationException("Cut observation requires the exact current unmaterialized wound admission.");
                if (_requestedPressureCutObservation != null || _activePressureCutObservation != null ||
                    Volatile.Read(ref _pressureCutObservation) != null)
                    throw new InvalidOperationException("A one-shot cut observation cannot be requested again.");
                _requestedPressureCutObservation = new(lease, admission);
            }
            finally { _gate.Release(); }
        }

        /// <summary>Consumes only the exact requested invocation; a foreign attempt cannot arm, consume or replace it.</summary>
        /// <param name="lease">Invocation lease to compare by reference.</param>
        /// <param name="admission">Invocation admission to compare by reference.</param>
        /// <returns>The consumed private request, or null with no diagnostic work.</returns>
        private RequestedPressureCutObservation? BeginPressureCutObservationUnderGate(
            FileSystemManager.CanonicalWriteLease lease, WoundSourceAdmission admission)
        {
            var requested = _requestedPressureCutObservation;
            if (requested == null || !ReferenceEquals(requested.Lease, lease) ||
                !ReferenceEquals(requested.Admission, admission)) return null;
            _requestedPressureCutObservation = null;
            _activePressureCutObservation = requested;
            return requested;
        }

        /// <summary>Uses the ordinary production implementation after selection and before the allocation speculation.</summary>
        /// <param name="lease">Exact invocation lease already bound to the active request.</param>
        /// <returns>Completion with detached warm DATA or an explicit unavailable reason.</returns>
        private async Task WarmRequestedPressureCutObservationUnderGateAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            var requested = _activePressureCutObservation!;
            var initial = ReadPressureCutCounts();
            var initialWork = ReadExistingPressureGroupComputationCounts();
            var cold = await ReadExistingPressureOrdinaryComparisonUnderGateAsync(lease);
            var coldCounts = ReadPressureCutCounts();
            var coldWork = ReadExistingPressureGroupComputationCounts();
            if (cold.Comparison == null || cold.Issues.Count != 0 || cold.PrefixBlocked)
            {
                requested.UnavailableReason = PressureCutComparisonRefusal("cold", cold);
                return;
            }
            var warm = await ReadExistingPressureOrdinaryComparisonUnderGateAsync(lease);
            var warmCounts = ReadPressureCutCounts();
            var warmWork = ReadExistingPressureGroupComputationCounts();
            if (warm.Comparison == null || warm.Issues.Count != 0 || warm.PrefixBlocked)
            {
                requested.UnavailableReason = PressureCutComparisonRefusal("warm", warm);
                return;
            }
            if (!_source.RetainPressureCutWitnessUnderGate(this, lease))
            {
                requested.UnavailableReason = "The exact warmed ordinary mechanics context is unavailable.";
                return;
            }
            requested.WarmData = new(cold.Comparison, warm.Comparison, initial, coldCounts, warmCounts,
                initialWork, coldWork, warmWork);
        }

        /// <summary>Samples only scalar versions/currentness immediately before the genuine draft-owned cut.</summary>
        /// <param name="lease">Exact invocation lease; no physical reread is made in this sample.</param>
        private void RecordPressureCutBeforeUnderGate(FileSystemManager.CanonicalWriteLease lease)
        {
            var requested = _activePressureCutObservation!;
            if (requested.WarmData == null) return;
            var witness = _source.ReadPressureCutWitnessUnderGate(this, lease, readStaleDiagnostic: false);
            if (witness == null)
            {
                requested.UnavailableReason = "The warmed witness was unavailable immediately before the cut.";
                return;
            }
            requested.BeforeWitness = witness;
            requested.Before = ReadPressureCutStage(witness);
        }

        /// <summary>Samples only the old context's cheap stale branch and scalar identities; never computes or exports in speculation.</summary>
        /// <param name="lease">Exact invocation lease; no physical reread is made in this sample.</param>
        private void RecordPressureCutAfterUnderGate(FileSystemManager.CanonicalWriteLease lease)
        {
            var requested = _activePressureCutObservation!;
            if (requested.WarmData is not { } warm || requested.Before is not { } before ||
                requested.BeforeWitness is not { } beforeWitness) return;
            var afterWitness = _source.ReadPressureCutWitnessUnderGate(this, lease, readStaleDiagnostic: true);
            if (afterWitness == null)
            {
                requested.UnavailableReason = "The warmed witness was unavailable immediately after the cut.";
                return;
            }
            requested.Observation = new(warm.Cold, warm.Warm, warm.InitialCounts, warm.ColdCounts, warm.WarmCounts,
                ReadPressureCutCounts(), warm.InitialWork, warm.ColdWork, warm.WarmWork,
                ReadExistingPressureGroupComputationCounts(), before, ReadPressureCutStage(afterWitness),
                beforeWitness.SameOrdinaryContext && afterWitness.SameOrdinaryContext,
                beforeWitness.SameRoutingIdentity && afterWitness.SameRoutingIdentity,
                afterWitness.StaleReadReturnedNoData, afterWitness.StaleReadCode, afterWitness.StaleReadIssueCount);
        }

        /// <summary>Freezes scalar diagnostics only; it neither checks files nor revalidates a source.</summary>
        /// <param name="witness">Detached source-owned facts from the current sample.</param>
        /// <returns>Detached versions, ordinal and currentness predicates.</returns>
        private SpiritualPressureCutStage ReadPressureCutStage(SpiritualPressureCutWitnessFacts witness) =>
            new(_effects!.WoundReadVersion, _effects.DependencyCutVersion, _source.ContinuationRevision,
                _nextResourceOrdinal, witness.MechanicsCurrent, witness.ExactOwnersCurrent);

        /// <summary>Copies existing counters, without initialization, source computations or a parallel cache.</summary>
        /// <returns>A detached copy of the six existing counters.</returns>
        private SpiritualPressureCutCounts ReadPressureCutCounts()
        {
            var value = ReadExistingStrainReuseCounts();
            return new(value.Queries, value.SignedParentReads, value.OrdinaryEvaluations, value.ActualEvaluations,
                value.FreshInputChecks, value.FreshDependencyChecks);
        }

        /// <summary>Authenticates the private diagnostic witness to the already locked exact invocation, not a caller proof.</summary>
        /// <param name="source">Exact source attempting to sample its private witness.</param>
        /// <param name="lease">Exact request lease to compare by reference.</param>
        /// <returns>True only for the active request's unchanged live owner chain.</returns>
        internal bool OwnsPressureCutWitnessUnderGate(SpiritualWoundSourceSession source,
            FileSystemManager.CanonicalWriteLease lease) => _gate.CurrentCount == 0 &&
            _activePressureCutObservation is { } active && ReferenceEquals(active.Lease, lease) &&
            ReferenceEquals(_source, source) && OwnsWoundAdmission(active.Admission) &&
            _resources != null && OwnsMechanicsOwner(_resources, source);

        /// <summary>Retains one complete historical outcome only after the normal owning operation has returned or failed.</summary>
        /// <param name="requested">Private request consumed by this invocation.</param>
        /// <param name="result">Actual owner result, absent only for an exception.</param>
        /// <param name="error">Actual owner exception to describe without swallowing or replacing it.</param>
        private void CompletePressureCutObservationUnderGate(RequestedPressureCutObservation requested,
            OriginalWoundMaterializationResult? result, Exception? error)
        {
            SpiritualPressureCutObservationResult recorded;
            if (error != null)
                recorded = new(SpiritualPressureCutObservationStatus.Failure, null,
                    error.GetType().Name + ": " + error.Message);
            else if (result!.Issues.Count != 0)
                recorded = new(SpiritualPressureCutObservationStatus.Failure, null,
                    string.Join("; ", result.Issues.Select(issue => issue.Code ?? issue.Message)));
            else if (!result.HasWound || requested.Observation == null || requested.UnavailableReason != null)
                recorded = new(SpiritualPressureCutObservationStatus.Unavailable, null,
                    requested.UnavailableReason ?? "The owning operation completed without an observed wound insertion.");
            else
                recorded = new(SpiritualPressureCutObservationStatus.Success, requested.Observation, null);
            Volatile.Write(ref _pressureCutObservation, recorded);
        }

        /// <summary>Releases the privately retained live witness regardless of owner outcome; historical DATA remains readable.</summary>
        private void ClearPressureCutObservationUnderGate()
        {
            _source.ClearPressureCutWitness(this);
            _activePressureCutObservation = null;
        }

        /// <summary>Marks an unconsumed request unavailable when its owner closes; active failures finish in their original catch path.</summary>
        private void CancelPendingPressureCutObservationOnDispose()
        {
            if (_requestedPressureCutObservation == null) return;
            _requestedPressureCutObservation = null;
            Volatile.Write(ref _pressureCutObservation, new(SpiritualPressureCutObservationStatus.Unavailable, null,
                "The capture was disposed before the requested materialization."));
        }

        /// <summary>Freezes an ordinary diagnostic refusal without retaining mutable ValidationIssue objects.</summary>
        /// <param name="stage">Cold or warm diagnostic stage.</param>
        /// <param name="result">Actual ordinary refusal, never a substituted result.</param>
        /// <returns>An immutable description without live or mutable diagnostic references.</returns>
        private static string PressureCutComparisonRefusal(string stage, SpiritualOrdinaryPressureComparisonResult result) =>
            "The " + stage + " ordinary comparison is unavailable: " + (result.PrefixBlocked ? "prefix blocked" :
                result.Issues.Count == 0 ? "no comparison DATA" : string.Join("; ", result.Issues.Select(issue => issue.Code ?? issue.Message)));
    }
}
