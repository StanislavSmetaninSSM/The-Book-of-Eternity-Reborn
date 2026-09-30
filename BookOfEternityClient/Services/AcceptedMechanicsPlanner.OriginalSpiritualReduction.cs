namespace BookOfEternityClient.Services;

internal static partial class AcceptedMechanicsPlanner
{
    /// <summary>
    /// Reports a completed unpublished original spiritual reduction or the diagnostics that rejected its seal.
    /// </summary>
    /// <param name="Reduction">
    /// Exact ordinary reduction retained by the completed live resource owner, or <see langword="null"/>.
    /// </param>
    /// <param name="Issues">
    /// Validation diagnostics; empty when <paramref name="Reduction"/> is available.
    /// </param>
    internal sealed record OriginalSpiritualReductionResult(
        CompletedOrdinaryMechanicsReduction? Reduction,
        IReadOnlyList<ValidationIssue> Issues)
    {
        /// <summary>
        /// Gets whether the result contains one completed reduction and no diagnostics.
        /// </summary>
        internal bool Success => Reduction is not null && Issues.Count == 0;
    }

    internal sealed partial class ResourceExecutionSession
    {
        private EffectAcceptedTurnPlanner.EffectAcceptedDraft? _originalEffectOwner;
        private ValidationService.SpiritualWoundSourceSession? _originalSourceOwner;

        /// <summary>
        /// Binds the exact effect draft and spiritual source owner retained by the original capture.
        /// </summary>
        /// <param name="effectOwner">
        /// Draft created for this session's exact captured base effect plan.
        /// </param>
        /// <param name="sourceOwner">
        /// Current source session retained by the same original capture.
        /// </param>
        internal void BindOriginalCompletionOwners(
            EffectAcceptedTurnPlanner.EffectAcceptedDraft effectOwner,
            ValidationService.SpiritualWoundSourceSession sourceOwner)
        {
            ArgumentNullException.ThrowIfNull(effectOwner);
            ArgumentNullException.ThrowIfNull(sourceOwner);
            Enter();
            try
            {
                EnsureUsable();
                var context = _pendingInput?.PlanningContext;
                if (!_live || _started || context?.EffectPlan is null ||
                    _originalEffectOwner is not null || _originalSourceOwner is not null ||
                    _routing is null || !_routing.OwnsContext(context.EffectPlan, context.Owners) ||
                    !effectOwner.OwnsBase(context.EffectPlan) || !sourceOwner.IsCurrentOwner)
                {
                    throw new InvalidOperationException(
                        "Bind the exact original effect and source owners before resource execution.");
                }
                _originalEffectOwner = effectOwner;
                _originalSourceOwner = sourceOwner;
            }
            finally
            {
                Exit();
            }
        }

        /// <summary>
        /// Checks whether this session retains the exact original effect and source owners.
        /// </summary>
        /// <param name="effectOwner">
        /// Effect draft expected to be bound to this resource session.
        /// </param>
        /// <param name="sourceOwner">
        /// Spiritual source session expected to be bound to this resource session.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for the two exact bound owner objects; otherwise,
        /// <see langword="false"/>.
        /// </returns>
        internal bool OwnsOriginalCompletionOwners(
            EffectAcceptedTurnPlanner.EffectAcceptedDraft effectOwner,
            ValidationService.SpiritualWoundSourceSession sourceOwner) =>
            !_disposed && !_faulted && ReferenceEquals(_originalEffectOwner, effectOwner) &&
            ReferenceEquals(_originalSourceOwner, sourceOwner);

        /// <summary>
        /// Checks whether the bound effect draft issued one plan from this session's final transcript.
        /// </summary>
        /// <param name="effectOwner">
        /// Exact effect draft retained by the original capture.
        /// </param>
        /// <param name="effects">
        /// Candidate completed effect plan.
        /// </param>
        /// <returns>
        /// <see langword="true"/> only for the bound draft's exact completion over this session's
        /// final transcript and installed routing epoch; otherwise, <see langword="false"/>.
        /// </returns>
        internal bool OwnsOriginalEffectCompletion(
            EffectAcceptedTurnPlanner.EffectAcceptedDraft effectOwner,
            EffectAcceptedTurnPlan effects)
        {
            ArgumentNullException.ThrowIfNull(effectOwner);
            ArgumentNullException.ThrowIfNull(effects);
            var transcript = Result?.EffectBoundaryTranscript;
            var completionRouting = _woundRegistrations.Count == 0 ? null : _routing;
            return _originalSourceOwner is not null &&
                OwnsOriginalCompletionOwners(effectOwner, _originalSourceOwner) &&
                transcript is not null &&
                effectOwner.OwnsCompletion(effects, completionRouting, transcript);
        }

        /// <summary>
        /// Seals this completed live executor and its exact completed effect plan as an unpublished ordinary reduction.
        /// </summary>
        /// <param name="effects">
        /// Completed effect plan issued by this session's bound draft from its final transcript.
        /// </param>
        /// <param name="sourceCompletion">
        /// Owner-issued final spiritual source projection validated under the active canonical lease.
        /// </param>
        /// <param name="satisfactionReceiptIdentity">
        /// Capture-owned identity for a no-transition guarantee or terminal closure that requires a C3 receipt join.
        /// </param>
        /// <returns>
        /// A detached ordinary reduction, or diagnostics when ownership, completion or projection agreement fails.
        /// </returns>
        internal OriginalSpiritualReductionResult SealOriginalSpiritualReduction(
            EffectAcceptedTurnPlan effects,
            ValidationService.SpiritualWoundSourceSession.PreparedContinuation.CompletionProjection
                sourceCompletion, object? satisfactionReceiptIdentity = null)
        {
            ArgumentNullException.ThrowIfNull(effects);
            ArgumentNullException.ThrowIfNull(sourceCompletion);
            Enter();
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                var input = _pendingInput;
                var context = input?.PlanningContext;
                var resources = Result;
                if (_faulted || !_live || input is null || context is null ||
                    resources is not { IsValid: true } || _execution is not null ||
                    _pendingExchange || _pendingResourceObservation is not null ||
                    _pendingPacket is not null || _active is not null || _staged is not null)
                {
                    return Failure(
                        "spiritual_original_reduction_incomplete",
                        "one exact completed live resource owner with no unresolved continuation");
                }

                var originalEffects = context.EffectPlan;
                var originalFingerprint = originalEffects is null
                    ? null
                    : WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(
                        originalEffects);
                var finalFingerprint =
                    WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects);
                if (originalEffects is null || _routing is null ||
                    _originalEffectOwner is null || _originalSourceOwner is null ||
                    !_routing.OwnsContext(originalEffects, context.Owners) ||
                    resources.EffectBoundaryTranscript?.PlanAuthority !=
                        CreateEffectPlanAuthority(originalEffects) ||
                    !OwnsOriginalEffectCompletion(_originalEffectOwner, effects) ||
                    !sourceCompletion.OwnsConsumption(
                        this,
                        _originalEffectOwner,
                        _originalSourceOwner,
                        effects) ||
                    !effects.IsAcceptedBoundaryComplete ||
                    !string.Equals(
                        effects.AcceptedBoundaryBasePlanFingerprint,
                        originalFingerprint,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        effects.AcceptedBoundaryFinalPlanFingerprint,
                        finalFingerprint,
                        StringComparison.Ordinal))
                {
                    return Failure(
                        "spiritual_original_reduction_effect_mismatch",
                        "the completed effect plan from this resource owner's exact final transcript");
                }

                var ownerAgreementIssues = (TerminalPreparation?.FinalOwners ?? context.Owners).ValidateCanonicalAgreement(
                    resources.StateAfterImage,
                    resources.HistoryAfterImage);
                if (ownerAgreementIssues.Count != 0)
                    return new OriginalSpiritualReductionResult(null, ownerAgreementIssues);

                var projectedOwners = ProjectRegisteredSystemOutcomes(context, resources);
                if (!projectedOwners.IsValid)
                {
                    return new OriginalSpiritualReductionResult(
                        null,
                        projectedOwners.Issues);
                }
                var companionAfterImages = projectedOwners.CompanionAfterImages.ToDictionary(
                    static pair => pair.Key,
                    static pair => pair.Value.DeepClone().AsObject(),
                    StringComparer.Ordinal);
                companionAfterImages[AfterlifeSpiritualConflictState.StatePath] =
                    sourceCompletion.ConflictAfterImage;
                var finalInput = TerminalPreparation?.OriginalInput ?? input;

                return new OriginalSpiritualReductionResult(
                    new CompletedOrdinaryMechanicsReduction(
                        finalInput,
                        AcceptedMechanicsPlanFingerprints.ComputeInput(
                            finalInput.CreateBinding()),
                        finalInput.PlanningContext!,
                        _originalResourceInput.Definitions,
                        resources,
                        effects,
                        _livePendingState,
                        companionAfterImages,
                        projectedOwners.OwnerTransitions,
                        hasLiveWoundWork: _woundRegistrations.Count != 0,
                        issuanceKey: CompletedOrdinaryIssuanceKey,
                        satisfactionReceiptIdentity: satisfactionReceiptIdentity),
                    Array.Empty<ValidationIssue>());
            }
            finally
            {
                Exit();
            }
        }

        /// <summary>
        /// Creates one failed original-spiritual reduction result.
        /// </summary>
        /// <param name="code">
        /// Stable diagnostic code identifying the failed seal invariant.
        /// </param>
        /// <param name="expected">
        /// Description of the exact retained evidence required by the seal.
        /// </param>
        /// <returns>
        /// A failed reduction result associated with the spiritual conflict state.
        /// </returns>
        private static OriginalSpiritualReductionResult Failure(
            string code,
            string expected)
        {
            var issues = new List<ValidationIssue>();
            ResourceMaterializationContract.AddIssue(
                issues,
                AfterlifeSpiritualConflictState.StatePath,
                code,
                expected,
                "missing, stale or mismatched");
            return new OriginalSpiritualReductionResult(null, issues);
        }
    }
}
