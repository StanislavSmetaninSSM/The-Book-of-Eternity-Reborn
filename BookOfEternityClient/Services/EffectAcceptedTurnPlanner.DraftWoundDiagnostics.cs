using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class EffectAcceptedTurnPlanner
{
    /// <summary>Projects each already retained detached wound/effect image once, preserving absent DATA.</summary>
    /// <param name="data">Existing immutable DATA, or null before a wound view has been retained.</param>
    /// <returns>Plain serialization values, never a state reader or owner.</returns>
    internal static object? ReadClosedPressureWoundDiagnosticData(WoundOperationBeforeData? data) => data == null ? null : new
    {
        data.WoundCarriers, data.WoundIdentity, data.WoundHistory, data.EffectCarriers, data.EffectIdentity
    };

    /// <summary>Projects a retained writer's complete source binding without serializing its internal-property owner.</summary>
    /// <param name="source">Existing immutable source-binding receipt.</param>
    /// <returns>Plain source metadata and its detached definition.</returns>
    private static object ReadClosedPressureSourceBindingDiagnosticData(EffectDraftSourceBinding source) => new
    {
        source.Key, source.Materializable, source.Active, source.SameTurn, source.SourceRef,
        source.RequiredApplicationAuthority, source.SatisfiedPredicates, Definition = source.ReadDefinition()
    };

    /// <summary>Projects all detached application receipt facts, including created images and writer ranges.</summary>
    /// <param name="application">Actual retained application receipt.</param>
    /// <returns>Plain complete application DATA without an application owner.</returns>
    private static object ReadClosedPressureApplicationDiagnosticData(EffectDraftApplicationReceipt application) => new
    {
        application.Disposition, application.EffectId, application.TransitionId, application.EventRef,
        application.CreatedEventRef, application.CausalEventRef,
        Source = ReadClosedPressureSourceBindingDiagnosticData(application.Source),
        application.Target, application.Carrier,
        ResultIdentity = ReadClosedPressureReplayIdentityDiagnosticData(application.ResultIdentity),
        ReplacedIdentity = ReadClosedPressureReplayIdentityDiagnosticData(application.ReplacedIdentity),
        application.SkillScopeFingerprint, application.ProvenanceKind, application.ProducerEffectId,
        Parameters = application.ReadParameters(), CreatedEffect = application.ReadCreatedEffect(),
        CreatedIdentity = application.ReadCreatedIdentity(), application.CarrierEdits,
        application.IdentityWrites, application.Allocations
    };

    /// <summary>Projects all retained replacement-agreement facts without executing agreement checks.</summary>
    /// <param name="agreement">Actual retained writer agreement.</param>
    /// <returns>Plain agreement identities, anchors and progress.</returns>
    private static object ReadClosedPressureAgreementDiagnosticData(EffectIdentityReplacementAgreement agreement) => new
    {
        agreement.EventRef, Result = ReadClosedPressureReplayIdentityDiagnosticData(agreement.Result),
        Replaced = ReadClosedPressureReplayIdentityDiagnosticData(agreement.Replaced), agreement.WriteCount, agreement.Anchors
    };

    /// <summary>Projects the two internal identity fields without constructing or validating an identity.</summary>
    /// <param name="identity">Existing identity, or null for absence.</param>
    /// <returns>Plain identity facts, preserving null.</returns>
    private static object? ReadClosedPressureReplayIdentityDiagnosticData(EffectReplayIdentity? identity) => identity == null ? null : new
    {
        identity.EffectId, identity.Authority
    };

    /// <summary>Projects a retained reaction's complete values and internal replacement identity without executing it.</summary>
    /// <param name="reaction">Already retained reaction DATA.</param>
    /// <returns>Complete plain reaction metadata and authored payloads.</returns>
    private static object ReadClosedPressureReactionDiagnosticData(EffectReactionExecution reaction) => new
    {
        reaction.EventRef, reaction.TriggerEventRef, reaction.CausalEventRef, reaction.Turn,
        reaction.EventKind, reaction.Target, reaction.EffectId, reaction.TriggerId, reaction.ComponentId,
        reaction.ResultKind, reaction.Dependency, reaction.AfterComponentId, reaction.MaxExpansion,
        DownstreamSource = reaction.DownstreamSource is not { } source ? null : new
        {
            source.Key, source.Definition, source.Materializable, source.Active, source.SameTurn, source.SourceRef,
            SatisfiedPredicates = source.SatisfiedPredicates.Order(StringComparer.Ordinal).ToArray(), source.RequiredApplicationAuthority
        },
        reaction.Parameters, reaction.DownstreamSourceKey, reaction.ComponentPriority,
        ReplacementTarget = ReadClosedPressureReplayIdentityDiagnosticData(reaction.ReplacementTarget)
    };

    /// <summary>Projects one existing authoritative release, including all reaction DATA.</summary>
    /// <param name="released">Existing closed-prefix release.</param>
    /// <returns>Plain release coordinates and its complete reaction facts.</returns>
    private static object ReadClosedPressureReleasedReactionDiagnosticData(ReleasedEffectReaction released) => new
    {
        released.Boundary, released.Activation, Reaction = ReadClosedPressureReactionDiagnosticData(released.Reaction),
        released.Stage, released.MechanicsOrdinal
    };

    internal sealed partial class EffectAcceptedDraft
    {
        /// <summary>Holds this draft's existing exclusion while the exact resource owner completes an atomic observation.</summary>
        /// <param name="lease">Current canonical lease for the already locked capture/source pair.</param>
        /// <param name="capture">Capture holding its gate.</param>
        /// <param name="source">Source holding its continuation gate.</param>
        /// <param name="resources">Capture-owned resource executor.</param>
        /// <param name="interval">Capture-derived latest closed interval.</param>
        /// <param name="ordinal">Capture-derived positive next ordinal.</param>
        /// <returns>Immutable observation only; refusal never faults or initializes this draft.</returns>
        internal ValidationService.SpiritualClosedPressureComparisonProbeState ReadClosedPressureComparisonProbeState(
            FileSystemManager.CanonicalWriteLease lease, ValidationService.SpiritualOriginalTurnCapture capture,
            ValidationService.SpiritualWoundSourceSession source, AcceptedMechanicsPlanner.ResourceExecutionSession resources,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, int ordinal)
        {
            if (System.Threading.Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
                throw new InvalidOperationException("A closed-frontier observation cannot re-enter the effect draft.");
            try
            {
                EnsureClosedPressureDraftContourUnderBusy(capture, source, resources);
                return resources.ReadClosedPressureComparisonProbeState(lease, capture, source, this, interval, ordinal);
            }
            finally { System.Threading.Volatile.Write(ref _busy, 0); }
        }

        /// <summary>Checks retained draft facts without initialization, preparation or state validation.</summary>
        /// <param name="capture">Exact current capture holding its gate.</param>
        /// <param name="source">Exact current source holding its gate.</param>
        /// <param name="resources">Exact current resource executor.</param>
        private void EnsureClosedPressureDraftContourUnderBusy(ValidationService.SpiritualOriginalTurnCapture capture,
            ValidationService.SpiritualWoundSourceSession source, AcceptedMechanicsPlanner.ResourceExecutionSession resources)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_busy != 1 || _faulted || _completed || HasPendingWoundIntegration || _openPhase != null ||
                _pendingWoundInsertion != null || _capturingWoundBefore != null ||
                !ReferenceEquals(capture.ReadOwnedWoundDraft(resources, source), this) ||
                !capture.OwnsWoundReadBase(this, plan))
                throw new InvalidOperationException("Diagnostics require the exact usable closed effect draft with no insertion or phase in flight.");
        }

        /// <summary>Serializes only actual retained draft DATA while all four observation exclusions are held.</summary>
        /// <param name="capture">Exact already locked capture.</param>
        /// <param name="source">Exact already locked source.</param>
        /// <param name="resources">Exact already locked resource executor.</param>
        /// <returns>Detached status, phases, wound registrations and full writer/journal images.</returns>
        internal string ReadClosedPressureDraftDiagnosticJsonUnderBusy(ValidationService.SpiritualOriginalTurnCapture capture,
            ValidationService.SpiritualWoundSourceSession source, AcceptedMechanicsPlanner.ResourceExecutionSession resources)
        {
            EnsureClosedPressureDraftContourUnderBusy(capture, source, resources);
            return JsonSerializer.Serialize(new
            {
                InitializationAttempted = _initializationAttempted, Disposed = _disposed, Faulted = _faulted,
                Completed = _completed, OpenPhase = _openPhase, WoundVersion = _woundVersion,
                InstalledWoundVersion = _installedWoundVersion, DependencyCutVersion = _dependencyCutVersion,
                PendingWoundIntegration = HasPendingWoundIntegration, PendingWoundInsertion = _pendingWoundInsertion != null,
                CapturingWoundBefore = _capturingWoundBefore != null, Issues = issues.ToArray(),
                // Null is a real uninitialized state. Reading the retained base below does not create a current view.
                CurrentWoundState = ReadClosedPressureWoundDiagnosticData(_currentWoundState),
                BaseCarriers = plan.ResourceTriggerCarriers, BaseIdentity = plan.IdentityIndexAfterImage, BaseEventInput = plan.EventInput,
                EventInput = eventInput, Turn = turn,
                Phases = _phases.Select(phase => new
                {
                    phase.Phase, phase.CarrierEdits,
                    Applications = phase.Applications.Select(ReadClosedPressureApplicationDiagnosticData).ToArray(),
                    phase.IdentityWrites, phase.Allocations,
                    ReplacementAgreements = phase.ReplacementAgreements.Select(ReadClosedPressureAgreementDiagnosticData).ToArray(),
                    phase.ProcessedEventRefs,
                    AddedSources = phase.AddedSources.Select(ReadClosedPressureSourceBindingDiagnosticData).ToArray(),
                    phase.AddedTargets, phase.SkillScopeFingerprint
                }).ToArray(),
                BeforeStates = _woundBeforeStates.Values.Select(before => before.ReadClosedPressureDiagnosticData()).ToArray(),
                Insertions = _woundInsertions.Values.OrderBy(insertion => insertion.VersionAfter).Select(insertion => new
                {
                    insertion.VersionAfter, insertion.IsCreation, insertion.IsCombatHealing,
                    WoundJson = WoundMaterializationContract.SerializeCanonical(insertion.Wound),
                    Before = ReadClosedPressureWoundDiagnosticData(insertion.OperationBefore),
                    insertion.CarrierEdits, insertion.IdentityWrites, insertion.Allocations,
                    Applications = insertion.Applications.Select(ReadClosedPressureApplicationDiagnosticData).ToArray(),
                    Interval = insertion.Interval == null ? null : new
                    {
                        insertion.Interval.ConflictId, insertion.Interval.ExchangeId, insertion.Interval.Ordinal
                    }
                }).ToArray(),
                LastInsertionVersion = _lastWoundInsertion?.VersionAfter,
                TriggerJournal = _woundTriggerJournal.OrderBy(pair => pair.Value.IdentityWrite.Ordinal)
                    .Select(pair => new { Boundary = pair.Key.Item1, Activation = pair.Key.Item2, Receipt = pair.Value.ReadClosedPressureDiagnosticData() }).ToArray(),
                ReactionJournal = _woundReactionJournal.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new { EventRef = pair.Key, Receipt = pair.Value.ReadClosedPressureDiagnosticData() }).ToArray(),
                Workspace = workspace == null ? null : new
                {
                    Carriers = workspace.ToInput(), workspace.AfterImages, workspace.EditCount, workspace.ApplicationCount,
                    Edits = workspace.ReadEditsFrom(0),
                    Applications = workspace.ReadApplicationsFrom(0).Select(ReadClosedPressureApplicationDiagnosticData).ToArray()
                },
                Identity = identityRoot == null ? null : new
                {
                    Snapshot = identityRoot.ReadSnapshot(), identityRoot.AllocatedEffectIds, identityRoot.AllocatedTransitionIds,
                    identityRoot.Writes, identityRoot.Allocations,
                    Agreements = identityRoot.ReplacementAgreements.Select(ReadClosedPressureAgreementDiagnosticData).ToArray()
                },
                ProcessedEventRefs = processedEventRefs?.Order(StringComparer.Ordinal).ToArray(),
                TransitionIds = transitionIds?.ToArray(), EffectIds = effectIds?.ToArray(),
                ActiveEffects = activeEffects?.ToArray(), UsedSources = usedSources?.ToArray(), UsedTargets = usedTargets?.ToArray(),
                ExecutionKeys = executionKeys?.Order(StringComparer.Ordinal).ToArray(),
                AcceptedExecutions = acceptedExecutions?.ToArray(),
                DeferredReactions = deferredReactions?.Select(ReadClosedPressureReactionDiagnosticData).ToArray(),
                ReactionEventRefs = reactionEventRefs?.Order(StringComparer.Ordinal).ToArray(),
                ReactionSourceBindings = reactionSourceBindings == null ? null : AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(reactionSourceBindings.Values),
                AuthoritativeReleasedReactions = authoritativeReleasedReactions?.Select(ReadClosedPressureReleasedReactionDiagnosticData).ToArray(),
                ReactionApplicationPlans = reactionApplicationPlans == null ? null : reactionApplicationPlans.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new
                    {
                        EventRef = pair.Key, pair.Value.Source, pair.Value.Coordinate, pair.Value.StackPolicy,
                        pair.Value.ReplacementExpectation, pair.Value.WoundOwnershipDomain, pair.Value.PriorReactionEventRef
                    }).ToArray(),
                ReactionApplicationResults = reactionApplicationResults == null ? null : reactionApplicationResults.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new
                    {
                        EventRef = pair.Key, ResultIdentity = ReadClosedPressureReplayIdentityDiagnosticData(pair.Value.ResultIdentity),
                        ReplacedIdentity = ReadClosedPressureReplayIdentityDiagnosticData(pair.Value.ReplacedIdentity)
                    }).ToArray(),
                PreReactionEffects = preReactionEffects == null ? null : preReactionEffects.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new { EffectId = pair.Key, Effect = pair.Value }).ToArray(),
                ReactionExpansionUsage = reactionExpansionUsage == null ? null : AcceptedEffectBoundaryTranscript.OrderDiagnosticValues(
                    reactionExpansionUsage.Select(pair => new { Key = pair.Key, Usage = pair.Value })),
                PhaseStartOffsets = new { _carrierStart, _applicationStart, _writeStart, _allocationStart, _agreementStart, _sourceStart, _targetStart },
                ProcessedBefore = _processedBefore?.Order(StringComparer.Ordinal).ToArray()
            });
        }
    }
}
