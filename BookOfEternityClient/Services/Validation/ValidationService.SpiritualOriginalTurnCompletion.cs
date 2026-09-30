using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private EffectAcceptedTurnPlanningResult? _completedEffects;
        private AcceptedMechanicsPlanner.OriginalSpiritualReductionResult? _completedOrdinaryReduction;

        /// <summary>
        /// Drains the retained spiritual resource owner and completes its shared effect draft once.
        /// The result remains unpublished and grants no accepted-plan authority.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease used to reauthenticate every original input and retained decision.
        /// </param>
        /// <returns>
        /// The retained completed effect result, or diagnostics produced before revoking a failed mutation attempt.
        /// </returns>
        internal async Task<EffectAcceptedTurnPlanningResult> CompleteEffectsAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try { return await CompleteEffectsUnderGateAsync(lease); }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Completes the retained effects while the caller holds the capture gate.
        /// </summary>
        /// <param name="lease">
        /// Active lease used to authenticate the current owners and source frontier.
        /// </param>
        /// <returns>
        /// The retained completed effects, or diagnostics without a successful completion.
        /// </returns>
        private async Task<EffectAcceptedTurnPlanningResult> CompleteEffectsUnderGateAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            var attempted = false;
            try
            {
                EnsureCurrent(lease);
                var issues = await CheckRetainedInputsAsync(lease);
                if (issues.Count != 0)
                    return new(null, issues);
                if (_resources == null || _effects == null)
                    return Failure("spiritual_effect_completion_not_started",
                        "the begun original spiritual resource and effect owners");
                var sourceIssues = await _source.CheckCompletionInputsAsync(
                    lease, _nextResourceOrdinal, _resources, _effects);
                if (sourceIssues.Count != 0)
                    return new(null, sourceIssues);
                if (_completedEffects != null)
                    return _completedEffects;
                if (_lastResourceStep is { PendingResource: not null } or { PendingExchange: not null })
                    return Failure("spiritual_resource_wait_unresolved",
                        "the exact retained resource or missing-side wait resolved before completion");
                if (_source.PendingRequirements.Any(value =>
                        value.Kind != SpiritualSourceRequirement.TerminalClosure || _terminal == null))
                    return Failure("spiritual_source_requirement_unresolved",
                        "every retained spiritual source requirement resolved before completion");
                if (_nextResourceOrdinal != _source.CheckedExchanges.Count)
                    return Failure("spiritual_resource_exchange_incomplete",
                        "every checked spiritual exchange executed in chronological order");
                if (_effects.HasPendingWoundIntegration)
                    return Failure("spiritual_wound_routing_required",
                        "every inserted wound registered in the retained resource owner");

                foreach (var source in _source.Sources.Where(
                             static value => value.Calculation.MaximumSeverityRank > 0 ||
                                 value.GuaranteedSeverityRank != null))
                {
                    if (!_woundAdmissions.TryGetValue(source, out var admission) ||
                        (!_woundOpportunities.TryGetValue(admission, out var result) ||
                         result.Satisfaction is null) &&
                        (!_woundDeclines.ContainsKey(admission) &&
                         (!_woundSelections.TryGetValue(admission, out var selection) ||
                          !_materializedWounds.ContainsKey(selection))))
                    {
                        return Failure("spiritual_wound_decision_required",
                            "one exact decline or materialized decision for every positive-ceiling source");
                    }
                }

                using var allocationScope = _allocations.Clock.BeginSpeculation();
                attempted = true;
                var resources = _resources.Result ?? _resources.Drain();
                if (!resources.IsValid)
                {
                    RevokeUnderLease(lease);
                    return new(null, resources.Issues);
                }
                if (resources.EffectBoundaryTranscript == null)
                {
                    RevokeUnderLease(lease);
                    return Failure("spiritual_effect_transcript_required",
                        "the final transcript from the retained spiritual resource owner");
                }
                EffectAcceptedTurnPlanningResult completed;
                if (_materializedWounds.Count == 0)
                {
                    completed = _effects.Complete(resources.EffectBoundaryTranscript);
                }
                else
                {
                    var routing = _resources.Routing;
                    if (routing == null)
                    {
                        RevokeUnderLease(lease);
                        return Failure("spiritual_wound_routing_required",
                            "the retained spiritual resource routing owner");
                    }
                    completed = _effects.CompleteWithWoundRouting(
                        resources.EffectBoundaryTranscript, routing);
                }
                if (!completed.Success)
                {
                    RevokeUnderLease(lease);
                    return completed;
                }
                if (_terminal != null)
                {
                    _terminal.Complete(completed.Plan!);
                    _source.CompleteTerminal(_terminal);
                }
                _completedEffects = completed;
                allocationScope.Commit();
                return completed;
            }
            catch
            {
                if (attempted)
                    RevokeUnderLease(lease);
                throw;
            }
        }

        /// <summary>
        /// Seals the completed spiritual resource and effect owners as one unpublished ordinary reduction.
        /// The result contains no durable spiritual decision stage and grants no accepted-plan authority.
        /// </summary>
        /// <param name="lease">
        /// Active canonical write lease used to reauthenticate the retained input and source frontier.
        /// </param>
        /// <returns>
        /// The retained ordinary reduction, or diagnostics produced before revoking a failed final seal.
        /// </returns>
        internal async Task<AcceptedMechanicsPlanner.OriginalSpiritualReductionResult>
            CompleteOrdinaryReductionAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try { return await CompleteOrdinaryReductionUnderGateAsync(lease); }
            finally { _gate.Release(); }
        }

        /// <summary>
        /// Seals the ordinary reduction under the held capture gate, retaining all final allocations.
        /// </summary>
        /// <param name="lease">
        /// Active lease used to reauthenticate the completed resource and effect owners.
        /// </param>
        /// <returns>
        /// The retained unpublished reduction, or diagnostics from its actual owners.
        /// </returns>
        private async Task<AcceptedMechanicsPlanner.OriginalSpiritualReductionResult>
            CompleteOrdinaryReductionUnderGateAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            var effects = await CompleteEffectsUnderGateAsync(lease);
            if (!effects.Success || effects.Plan is null)
            {
                return new AcceptedMechanicsPlanner.OriginalSpiritualReductionResult(
                    null,
                    effects.Issues);
            }

            try
            {
                EnsureCurrent(lease);
                var issues = await CheckRetainedInputsAsync(lease);
                if (issues.Count != 0)
                {
                    return new AcceptedMechanicsPlanner.OriginalSpiritualReductionResult(
                        null,
                        issues);
                }
                if (_resources is null || _effects is null)
                {
                    RevokeUnderLease(lease);
                    return ReductionFailure(
                        "spiritual_original_reduction_incomplete",
                        "the completed retained resource and effect owners");
                }
                var resources = _resources;
                var effectOwner = _effects;
                if (_completedOrdinaryReduction is not null)
                {
                    var sourceIssues = await _source.CheckCompletionInputsAsync(
                        lease, _nextResourceOrdinal, resources, effectOwner);
                    return sourceIssues.Count == 0 ? _completedOrdinaryReduction :
                        new AcceptedMechanicsPlanner.OriginalSpiritualReductionResult(null, sourceIssues);
                }
                using var allocationScope = _allocations.Clock.BeginSpeculation();
                var preparedSource = await _source.PrepareContinuationAsync(lease);
                if (preparedSource.Ticket is null)
                {
                    return new AcceptedMechanicsPlanner.OriginalSpiritualReductionResult(
                        null,
                        preparedSource.Issues);
                }
                var sourceCompletion = preparedSource.Ticket.ValidateCompletion(
                    _source,
                    lease,
                    _nextResourceOrdinal,
                    resources,
                    effectOwner);
                if (!sourceCompletion.Success)
                {
                    return new AcceptedMechanicsPlanner.OriginalSpiritualReductionResult(
                        null,
                        sourceCompletion.Issues);
                }
                var sourceProjection = sourceCompletion.Projection!;
                var consumptionIssues = sourceProjection.Consume(
                    _source,
                    lease,
                    _nextResourceOrdinal,
                    resources,
                    effectOwner,
                    effects.Plan);
                if (consumptionIssues.Count != 0)
                {
                    RevokeUnderLease(lease);
                    return new AcceptedMechanicsPlanner.OriginalSpiritualReductionResult(
                        null,
                        consumptionIssues);
                }

                var reduced = resources.SealOriginalSpiritualReduction(
                    effects.Plan,
                    sourceProjection,
                    satisfactionReceiptIdentity: _terminal != null || _woundOpportunities.Values.Any(
                        static value => value.Satisfaction is not null)
                        ? _satisfactionReceiptIdentity : null);
                if (!reduced.Success)
                {
                    RevokeUnderLease(lease);
                    return reduced;
                }
                if (_materializedWounds.Count != 0)
                {
                    if (_registeredWoundInsertions.Count != _materializedWounds.Count ||
                        resources.Routing is null ||
                        resources.Result?.EffectBoundaryTranscript is not { } transcript)
                    {
                        RevokeUnderLease(lease);
                        return ReductionFailure("spiritual_live_wound_chain_incomplete",
                            "every materialized decision registered by the completed owners");
                    }
                    var selected = _registeredWoundInsertions
                        .OrderBy(pair => pair.Value.VersionAfter)
                        .Select(pair => (pair.Key, pair.Value))
                        .ToArray();
                    var proof = SpiritualLiveWoundCompletion.Seal(effectOwner,
                        effects.Plan, resources.Routing, transcript, resources, selected);
                    if (proof is null)
                    {
                        RevokeUnderLease(lease);
                        return ReductionFailure("spiritual_live_wound_chain_mismatch",
                            "the exact chronological registered wound reducer outputs");
                    }
                    reduced = new AcceptedMechanicsPlanner.OriginalSpiritualReductionResult(
                        reduced.Reduction!.WithLiveWoundCompletion(proof), []);
                }
                _completedOrdinaryReduction = reduced;
                allocationScope.Commit();
                return reduced;
            }
            catch
            {
                RevokeUnderLease(lease);
                throw;
            }
        }

        /// <summary>
        /// Creates one spiritual-conflict diagnostic for a rejected ordinary-reduction seal.
        /// </summary>
        /// <param name="code">
        /// Stable diagnostic code identifying the failed reduction invariant.
        /// </param>
        /// <param name="expected">
        /// Description of the retained evidence required for reduction.
        /// </param>
        /// <returns>
        /// A failed original-spiritual reduction result.
        /// </returns>
        private static AcceptedMechanicsPlanner.OriginalSpiritualReductionResult ReductionFailure(
            string code,
            string expected) =>
            new(null, new[]
            {
                SourceIssue(AfterlifeSpiritualConflictState.StatePath, code, expected)
            });

        /// <summary>
        /// Creates one spiritual-conflict diagnostic for a rejected effect completion.
        /// </summary>
        /// <param name="code">
        /// Stable diagnostic code identifying the failed completion invariant.
        /// </param>
        /// <param name="expected">
        /// Description of the retained evidence required for completion.
        /// </param>
        /// <returns>
        /// A failed effect-planning result associated with the spiritual conflict state.
        /// </returns>
        private static EffectAcceptedTurnPlanningResult Failure(string code, string expected) =>
            new(null, new[]
            {
                SourceIssue(AfterlifeSpiritualConflictState.StatePath, code, expected)
            });
    }
}
