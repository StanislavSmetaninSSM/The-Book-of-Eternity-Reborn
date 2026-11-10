using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    /// <summary>Contains immutable comparison DATA from the genuine unstaged original spiritual resource prefix.</summary>
    /// <param name="ResourceStateJson">Complete canonical current resource entries.</param>
    /// <param name="ResourceHistoryJson">Complete canonical baseline and pending resource history.</param>
    /// <param name="ResourceTranscriptJson">Applied, replayed and emitted resource evidence and retained execution progress.</param>
    /// <param name="EffectPrefixJson">Complete retained unsealed effect evidence and boundary progress.</param>
    /// <param name="NextOrdinal">The actual next exchange ordinal authenticated by the specific observation entrypoint.</param>
    internal sealed record ExistingPressureOrdinaryDiagnosticSnapshot(
        string ResourceStateJson,
        string ResourceHistoryJson,
        string ResourceTranscriptJson,
        string EffectPrefixJson,
        int NextOrdinal);

    internal sealed partial class ResourceExecutionSession
    {
        /// <summary>Reads comparison-only DATA at the already prepared original prefix without executing or allocating identities.</summary>
        /// <returns>Detached immutable images, rejecting staged, pending, completed or wound-inserted execution.</returns>
        internal ExistingPressureOrdinaryDiagnosticSnapshot ReadExistingPressureOrdinaryDiagnosticSnapshot()
        {
            Enter();
            try
            {
                EnsureUsable();
                if (!_live || !_started || !_originalPrefixRequested || _nextOrdinal != 0 ||
                    _acceptedExchangeIds.Count != 0 || _staged != null || _active != null ||
                    _conflictId != null || _pendingExchange || _pendingResourceObservation != null ||
                    _pendingPacket != null || _livePendingState is { Requests.Count: > 0 } ||
                    _stagedRawProducerBatch != null || _activeRawProducerBatch != null ||
                    _missingAuditObservation != null || _missingAuditExtension != null ||
                    _preparedMissingSide != null || _missingSideAliases.Count != 0 || _missingSideAliasVersion != 0 ||
                    _woundRegistrations.Count != 0 || _knownWoundInstanceIds != null ||
                    _currentCheckpoint != null || CheckpointCount != 0)
                    throw new InvalidOperationException(
                        "Existing-pressure diagnostics require the unstaged original resource prefix before exchange zero.");
                return _state.ReadExistingPressureOrdinaryDiagnosticSnapshot(_nextOrdinal,
                    _livePendingState?.ToCanonicalJson(), _routing?.ReadExistingPressureOrdinaryDiagnosticJson());
            }
            finally { Exit(); }
        }

        /// <summary>Completes one latest-closed observation while all outer owner exclusions remain held.</summary>
        /// <param name="lease">Current canonical lease belonging to the locked capture.</param>
        /// <param name="capture">Capture already holding its exclusive gate.</param>
        /// <param name="source">Capture-owned source already holding its continuation gate.</param>
        /// <param name="draft">Capture-owned draft already holding its busy exclusion.</param>
        /// <param name="interval">Capture-derived latest closed resource interval.</param>
        /// <param name="ordinal">Capture-derived positive next ordinal.</param>
        /// <returns>Detached complete source/resource/draft/routing/allocation DATA.</returns>
        internal ValidationService.SpiritualClosedPressureComparisonProbeState ReadClosedPressureComparisonProbeState(
            FileSystemManager.CanonicalWriteLease lease, ValidationService.SpiritualOriginalTurnCapture capture,
            ValidationService.SpiritualWoundSourceSession source, EffectAcceptedTurnPlanner.EffectAcceptedDraft draft,
            SpiritualExchangeInterval interval, int ordinal)
        {
            Enter();
            try
            {
                EnsureUsable();
                if (!_live || !_started || !_originalPrefixRequested || ordinal <= 0 || _nextOrdinal != ordinal ||
                    _acceptedExchangeIds.Count != ordinal || !_acceptedExchangeIds.Contains(interval.ExchangeId) ||
                    _conflictId != interval.ConflictId || interval.Ordinal != ordinal - 1 ||
                    !_state.IsLatestClosedInterval(interval) || _routing == null || _staged != null || _active != null ||
                    _pendingExchange || _pendingResourceObservation != null || _pendingPacket != null ||
                    _livePendingState is { Requests.Count: > 0 } || _stagedRawProducerBatch != null || _activeRawProducerBatch != null ||
                    _missingAuditObservation != null || _missingAuditExtension != null || _preparedMissingSide != null ||
                    _missingSideAliases.Count != 0 || _missingSideAliasVersion != 0 ||
                    _currentCheckpoint != null || CheckpointCount != 0 || TerminalPreparation != null ||
                    !capture.OwnsMechanicsOwner(this, source) ||
                    !ReferenceEquals(capture.ReadOwnedWoundDraft(this, source), draft))
                    throw new InvalidOperationException("Diagnostics require the exact live latest closed resource interval without staged or pending work.");

                var woundVersion = draft.WoundReadVersion;
                var cutVersion = draft.DependencyCutVersion;
                var routingJson = _routing.ReadClosedPressureComparisonDiagnosticJson();
                var resource = _state.ReadClosedPressureComparisonDiagnosticSnapshot(interval, ordinal,
                    _livePendingState?.ToCanonicalJson(), routingJson, new
                    {
                        NextOrdinal = _nextOrdinal, ConflictId = _conflictId,
                        AcceptedExchangeIds = _acceptedExchangeIds.Order(StringComparer.Ordinal).ToArray(),
                        Live = _live, Started = _started, OriginalPrefixRequested = _originalPrefixRequested,
                        StopAtClosedBoundary = _stopAtClosedBoundary, StopAtExchange = _stopAtExchange,
                        Staged = _staged != null, Active = _active != null, PendingExchange = _pendingExchange,
                        PendingResourceObservation = _pendingResourceObservation != null, PendingPacket = _pendingPacket,
                        PendingContextBound = _pendingInput != null, MissingAuditObservation = _missingAuditObservation != null,
                        MissingAuditExtension = _missingAuditExtension != null, PreparedMissingSide = _preparedMissingSide != null,
                        StagedRawProducerBatch = _stagedRawProducerBatch != null, ActiveRawProducerBatch = _activeRawProducerBatch != null,
                        MissingSideAliasVersion = _missingSideAliasVersion,
                        MissingSideAliases = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(_missingSideAliases
                            .Select(pair => new { From = pair.Key, To = pair.Value })),
                        CurrentCheckpoint = _currentCheckpoint != null, CheckpointCount,
                        KnownWoundInstanceIds = _knownWoundInstanceIds?.Order(StringComparer.Ordinal).ToArray(),
                        WoundRegistrations = _woundRegistrations.OrderBy(pair => pair.Key.VersionAfter).Select(pair => new
                        {
                            pair.Key.VersionAfter, pair.Value.NewInstanceIds,
                            Interval = pair.Key.Interval == null ? null : new
                            {
                                pair.Key.Interval.ConflictId, pair.Key.Interval.ExchangeId, pair.Key.Interval.Ordinal
                            }
                        }).ToArray()
                    });
                var sourceJson = source.ReadClosedPressureSourceDiagnosticJsonUnderGate(capture, this, ordinal, woundVersion, cutVersion);
                var captureJson = capture.ReadClosedPressureCaptureDiagnosticJsonUnderGate(lease, source);
                var draftJson = draft.ReadClosedPressureDraftDiagnosticJsonUnderBusy(capture, source, this);
                var allocations = capture.ReadExistingPressureOrdinaryAllocationUnderGate(lease, source);
                var frontier = new ValidationService.SpiritualOrdinaryPressureProbeState(sourceJson,
                    resource.ResourceStateJson, resource.ResourceHistoryJson, resource.ResourceTranscriptJson,
                    resource.EffectPrefixJson, ordinal, allocations.Cursor, allocations.JournalJson);
                // The source guard above proves all three actual projection inventories are empty under its held gate.
                return new(frontier, captureJson, draftJson, source.ContinuationRevision, woundVersion, cutVersion,
                    WoundAcceptedTurnFingerprintWriter.Compute(new[] { "closed_pressure_routing_data_v1", routingJson }),
                    new ValidationService.SpiritualPressureProjectionInventory(0, 0, 0));
            }
            finally { Exit(); }
        }

        internal sealed partial class SpiritualMechanicsContext
        {
            /// <summary>Projects retained historical context facts without checking freshness or initializing its lazy wound view.</summary>
            /// <returns>Detached descriptor JSON only; no context or routing epoch object is exported.</returns>
            internal string ReadClosedPressureComparisonDescriptorJson() => JsonSerializer.Serialize(new
            {
                Ordinal, WoundVersion = _woundVersion, DependencyCutVersion = _dependencyCutVersion,
                Contributions, AcceptedContributions = _acceptedContributions, ActionPointBefore = _actionPointBefore,
                WoundView = EffectAcceptedTurnPlanner.ReadClosedPressureWoundDiagnosticData(_woundView),
                EpochKind = _epoch is EffectAcceptedTurnPlan ? "base" : "wound_insertion",
                BasePlanInputFingerprint = (_epoch as EffectAcceptedTurnPlan)?.InputFingerprint,
                InsertionVersion = (_epoch as EffectAcceptedTurnPlanner.EffectAcceptedDraft.EffectDraftWoundInsertion)?.VersionAfter
            });
        }
    }

    private sealed partial class ResourceExecutionState
    {
        /// <summary>Serializes already initialized original-prefix facts without rebuilding ledgers, validating or advancing work.</summary>
        /// <param name="nextOrdinal">Actual exchange ordinal checked under the session exclusion gate.</param>
        /// <param name="pendingStateJson">Existing resolved receipt state, or null if no pending state was initialized.</param>
        /// <param name="routingStateJson">Existing original-routing identity DATA, or null for a resource-only session.</param>
        /// <returns>Complete immutable resource and effect images for the bounded comparison frontier.</returns>
        internal ExistingPressureOrdinaryDiagnosticSnapshot ReadExistingPressureOrdinaryDiagnosticSnapshot(
            int nextOrdinal, string? pendingStateJson, string? routingStateJson)
        {
            if (_originalPrefix == null || workingLedger == null || workingHistory == null ||
                events == null || appliedTransitions == null || replayTransitions == null ||
                directBaselineState == null || effectTranscriptBuilder == null || arbiter == null ||
                activeBatch != null || intervals.Count != 0 || intervalEffectStart != null ||
                intervalAppliedStart != 0 || intervalReplayStart != 0 || intervalEventStart != 0 ||
                _pendingAcceptedOutputs.Length != 0 || _receiptContinuation != null ||
                unresolvedPendingBoundaries.Count != 0)
                throw new InvalidOperationException(
                    "Existing-pressure diagnostics require an initialized closed original prefix with no exchange progress.");

            return ReadPressureDiagnosticPayload(nextOrdinal, pendingStateJson, routingStateJson);
        }

        /// <summary>Reads positive closed resource state only after proving the retained scheduler and all graph operations are quiescent.</summary>
        /// <param name="interval">Exact latest retained interval.</param>
        /// <param name="nextOrdinal">Positive next ordinal authenticated by the session.</param>
        /// <param name="pendingStateJson">Existing fully resolved receipt state, preserving null.</param>
        /// <param name="routingStateJson">Pure current routing image read under the same exclusions.</param>
        /// <param name="sessionData">Plain detached session facts, never an owner or callback.</param>
        /// <returns>Immutable resource/history/effect and full retained-progress images.</returns>
        internal ExistingPressureOrdinaryDiagnosticSnapshot ReadClosedPressureComparisonDiagnosticSnapshot(
            SpiritualExchangeInterval interval, int nextOrdinal, string? pendingStateJson, string routingStateJson, object sessionData)
        {
            if (_originalPrefix == null || workingLedger == null || workingHistory == null ||
                events == null || appliedTransitions == null || replayTransitions == null || directBaselineState == null ||
                effectTranscriptBuilder == null || arbiter == null || graphPreparation?.Graph == null || scheduler == null ||
                nextOrdinal <= 0 || intervals.Count != nextOrdinal || !IsLatestClosedInterval(interval) ||
                intervals.Where((row, ordinal) => row.Ordinal != ordinal).Any() || intervalEffectStart == null ||
                activeBatch != null || _pendingAcceptedOutputs.Length != 0 || _receiptContinuation != null ||
                unresolvedPendingBoundaries.Count != 0 || scheduler.HasPendingNodes ||
                graphPreparation.Graph.OrderedNodes.Any(node => !completedOperationIds.Contains(node.OperationId)) ||
                remainingOperationsByBoundary.Values.Any(remaining => remaining.Count != 0))
                throw new InvalidOperationException("Diagnostics require a fully completed retained graph at the latest closed resource interval.");

            var common = ReadPressureDiagnosticPayload(nextOrdinal, pendingStateJson, routingStateJson: null);
            return common with
            {
                ResourceTranscriptJson = JsonSerializer.Serialize(new
                {
                    RoutingStateJson = routingStateJson, Session = sessionData,
                    ExecutionStateJson = common.ResourceTranscriptJson,
                    Intervals = intervals.Select(row => new
                    {
                        row.ConflictId, row.ExchangeId, row.Ordinal, row.AppliedRange, row.ReplayRange, row.EventRange,
                        row.Boundaries, row.BoundaryCloses, row.CausalClosures, row.Activations, row.Rejections,
                        row.Components, row.ResourceMutations, row.Releases, row.TerminalReservations,
                        EffectBeforeFingerprint = row.EffectBefore.Fingerprint, EffectAfterFingerprint = row.EffectAfter.Fingerprint
                    }).ToArray(),
                    IntervalAppliedStart = intervalAppliedStart, IntervalReplayStart = intervalReplayStart,
                    IntervalEventStart = intervalEventStart, IntervalEffectStartFingerprint = intervalEffectStart.Fingerprint,
                    GraphNodes = graphPreparation.Graph.OrderedNodes,
                    SchedulerHasPendingNodes = scheduler.HasPendingNodes,
                    PendingAcceptedOutputs = _pendingAcceptedOutputs,
                    ReceiptContinuation = _receiptContinuation != null,
                    UnresolvedPendingBoundaries = unresolvedPendingBoundaries.Order().ToArray()
                })
            };
        }

        /// <summary>Shares already initialized ledger/history/transcript serialization, without owning an entry guard.</summary>
        /// <param name="nextOrdinal">Ordinal authenticated by the calling guard.</param>
        /// <param name="pendingStateJson">Already retained receipt image.</param>
        /// <param name="routingStateJson">Already retained routing image, or null.</param>
        /// <returns>Pure shared diagnostic payload with no state reconstruction.</returns>
        private ExistingPressureOrdinaryDiagnosticSnapshot ReadPressureDiagnosticPayload(
            int nextOrdinal, string? pendingStateJson, string? routingStateJson)
        {
            var transcript = JsonSerializer.Serialize(new
            {
                NextOrdinal = nextOrdinal,
                ExecutionSequence = executionSequence,
                AppliedTransitions = appliedTransitions.ToArray(),
                ReplayTransitions = replayTransitions.ToArray(),
                Events = events.ToArray(),
                PendingHistoryTransitions = workingHistory.PendingTransitions,
                DirectBaselineStateJson = directBaselineState.ToCanonicalJson(),
                PostDirectStateJson = postDirectState?.ToCanonicalJson(),
                PendingStateJson = pendingStateJson,
                RoutingStateJson = routingStateJson,
                PendingWave = _livePendingWave,
                ProducedEvents = producedEvents.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new
                {
                    OperationId = pair.Key, Events = pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray()
                }).ToArray(),
                AppliedTriggerMutationKeys = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(
                    appliedTriggerMutationKeys.Select(pair => new
                    {
                        Activation = pair.Key,
                        Mutations = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(pair.Value)
                    })),
                AcceptedCandidates = acceptedCandidates.Select(value => new
                {
                    Candidate = AcceptedEffectBoundaryTranscript.ReadCandidateDiagnosticData(value.Candidate),
                    value.Activation
                }).ToArray(),
                AcceptedCandidateIdentities = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(acceptedCandidateIdentities),
                ObservedCandidates = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(observedCandidates
                    .Select(AcceptedEffectBoundaryTranscript.ReadCandidateDiagnosticData)),
                AcceptedBoundaries = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(
                    acceptedBoundaryByIdentity.Select(pair => new { Activation = pair.Key, Boundary = pair.Value })),
                AcceptedActivations = AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(
                    acceptedActivationByIdentity.Select(pair => new { Identity = pair.Key, Activation = pair.Value })),
                AcceptedByBoundary = acceptedByBoundary.OrderBy(pair => pair.Key).Select(pair => new
                {
                    BoundaryOrdinal = pair.Key,
                    Accepted = pair.Value.Select(value => new
                    {
                        Candidate = AcceptedEffectBoundaryTranscript.ReadCandidateDiagnosticData(value.Candidate),
                        value.Activation, value.Boundary
                    }).ToArray()
                }).ToArray(),
                RemainingOperations = remainingOperationsByBoundary.OrderBy(pair => pair.Key).Select(pair => new
                {
                    BoundaryOrdinal = pair.Key, Operations = pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray()
                }).ToArray(),
                CompletedOperations = completedOperationIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ParentBoundaries = parentBoundaryByOrdinal.OrderBy(pair => pair.Key)
                    .Select(pair => new { BoundaryOrdinal = pair.Key, ParentBoundaryOrdinal = pair.Value }).ToArray(),
                ClosedBoundaries = closedBoundaries.OrderBy(value => value).ToArray(),
                UseSeeds = useSeeds.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new { EffectId = pair.Key, Seed = pair.Value }).ToArray(),
                UseProgressJson = arbiter.ReadDiagnosticJson()
            });
            return new ExistingPressureOrdinaryDiagnosticSnapshot(
                ResourceStateLedger.SerializeCanonicalEntries(workingLedger.Entries),
                workingHistory.ReadDiagnosticJson(), transcript,
                effectTranscriptBuilder.ReadClosedPrefixDiagnosticJson(), nextOrdinal);
        }
    }
}
