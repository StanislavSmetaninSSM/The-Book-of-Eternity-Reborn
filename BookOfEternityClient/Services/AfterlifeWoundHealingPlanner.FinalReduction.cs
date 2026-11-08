using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class AfterlifeWoundHealingPlanner
{
    internal sealed partial class Continuation
    {
        private SpiritualNaturalFinalReductionData? _finalReductionData;

        /// <summary>Reuses only this completed player's ordinary natural reduction DATA after the original handoff checks.</summary>
        /// <param name="prepared">Freshly checked preparation and its separate complete baseline.</param>
        /// <param name="accepted">Freshly checked effect payload and exact private execution proof.</param>
        /// <param name="compute">The original pure final reducer; never a publication or permission check.</param>
        /// <returns>The original detached result, with fresh computation for all unsupported or closed owners.</returns>
        internal WoundAcceptedTurnPlanningResult ReadNaturalFinalReduction(WoundPreparedAcceptedTurnPlan prepared,
            WoundEffectBatchAcceptedPlan accepted, Func<WoundAcceptedTurnPlanningResult> compute)
        {
            if (FileSystemManager.HasCurrentOperationTimingObserver || CaptureFinalReductionSource() is null)
            {
                RetireFinalReductionData();
                return compute();
            }
            var effectInput = accepted.EffectInput;
            var effectPlan = accepted.EffectPlan;
            var admittedInput = new OperationStructuralSnapshot(
            [
                prepared.WoundPreparationFingerprint, prepared.BaselineAuthority.AuthoritySeal,
                prepared.BindingFingerprint, prepared.InputFingerprint, accepted.WoundPreparationFingerprint,
                accepted.EffectInputFingerprint, accepted.EffectAcceptedTurnPlanFingerprint,
                EffectAcceptedTurnPlanner.GetSpiritualHealingExecutionFingerprint(effectPlan)
            ],
            [
                this, effectPlan.SpiritualHealingExecutionAuthority, effectInput.SourceAuthority,
                effectInput.TargetAuthority, effectInput.SkillScopeAuthority, effectInput.WoundApplicationLocations,
                effectInput.PreallocatedCombatantIdentities, effectInput.TargetAuthorityInput?.CombatantIdentities
            ]);
            return (_finalReductionData ??= new(CaptureFinalReductionSource)).Read(admittedInput, compute);
        }

        /// <summary>Retires this operation's DATA on publication, closing, or any observed admission/source failure.</summary>
        internal void RetireFinalReductionData()
        {
            _finalReductionData?.Dispose();
            _finalReductionData = null;
        }

        /// <summary>Captures complete pure-result dependencies without filesystem reads or retained permission.</summary>
        /// <returns>Detached structural evidence only for a completed player-only ordinary natural execution.</returns>
        private OperationStructuralSnapshot? CaptureFinalReductionSource()
        {
            if (!HasOrdinaryNaturalPlayerData()) return null;
            return new(
            [
                Fingerprint, Mode, ProgramReference, SourceFingerprint, CycleKey, PersistenceSource.ToJsonString(),
                _controlJson, JsonSerializer.Serialize(_stages), _composedEffectInputFingerprint,
                SchedulerMutation.Fingerprint,
                CanonicalJsonUtf8.DecodeOneOptionalBom(SchedulerMutation.ScheduleBefore.Bytes!),
                CanonicalJsonUtf8.DecodeOneOptionalBom(SchedulerMutation.ReportBefore.Bytes!),
                SchedulerMutation.ScheduleAfterJson, SchedulerMutation.ScheduleAfter.Fingerprint
            ],
            [this, _files, _lease, _snapshot, _original, _natural, SchedulerMutation]);
        }

        /// <summary>Limits same-call detached handoff DATA reads to the existing ordinary natural shape without callbacks.</summary>
        /// <remarks>This is not an authority check; every preparation, payload and execution check remains fresh.</remarks>
        internal bool CanReuseNaturalHandoffData =>
            !_files.HasOperationTimingObserver && HasOrdinaryNaturalPlayerData();

        /// <summary>Recognizes the completed, open, player-only ordinary natural DATA shape without capturing its dependencies.</summary>
        /// <returns>False for active treatment, clean start, actors, participation, incomplete or closed execution.</returns>
        private bool HasOrdinaryNaturalPlayerData()
        {
            if (_published || !IsNatural || _naturalConflictDraft is not null || _naturalConflictAfter is not null ||
                _participationOutcomes is not null || _participationChanges is not null || _participationSnapshot is not null ||
                _participationOriginal is not null || _participationSources is not null || _parsedParticipationRoots is not null ||
                _executed.Count != _stages.Count || _stages.Count == 0 || _wounds.Count == 0 ||
                _wounds.Any(w => w.Owner.OwnerKind != "player_soul" || w.Owner.OwnerId != "player_soul")) return false;
            return true;
        }
    }
}
