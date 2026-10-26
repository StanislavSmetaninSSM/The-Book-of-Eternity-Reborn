using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    /// <summary>Shares expected DATA only after the actual caller's existing fresh admissions.</summary>
    /// <param name="input">The actual input, detached once for capture, admission and source.</param>
    /// <param name="authority">The same genuinely issued private joint continuation.</param>
    /// <returns>The full expected vector or original source refusal; no actual-plan verdict is retained.</returns>
    internal static ExpectedPreparationReplayProjection? ReadJointExpectedPreparationProjection(
        WoundAcceptedTurnInput input, object authority)
    {
        // This is the exact original joint dispatch. It alone counts richer source work.
        ExpectedPreparationReplayProjection? Source(WoundAcceptedTurnInput sourceInput)
        {
            using var sourceTiming = FileSystemManager.MeasureCurrentOperation("WoundPlanCache.PreparedExpectedReplay.Source");
            return ExpectedPreparationReplayProjection.Capture(PrepareJointRepairCandidate(
                WoundAcceptedTurnData.CloneInput(sourceInput)!, authority));
        }
        if (authority is not JointRepairContinuationAuthority joint) return Source(input);
        var operation = joint.StageReplayOperation; // protocol/closed-owner errors are not domain capture failures
        if (operation is null) return Source(input);
        WoundAcceptedTurnInput fixedInput;
        OperationStructuralSnapshot? captured;
        bool eligible;
        try
        {
            using var keyTiming = FileSystemManager.MeasureCurrentOperation("JointPreparation.Replay.Key");
            fixedInput = WoundAcceptedTurnData.CloneInput(input)!;
            eligible = joint.SemanticData.CanCapturePreparationProjection;
            captured = eligible ? WoundAcceptedTurnFingerprints.CapturePreparationReplayInput(fixedInput, joint.ReplayCompanions()) : null;
            var retained = captured is not null
                ? WoundAcceptedTurnFingerprints.CapturePreparationReplayInput(joint.AcquisitionInput, joint.ReplayCompanions()) : null;
            eligible = captured is not null && retained is not null && captured.Matches(retained);
        }
        catch (Exception error) when (IsReplayCaptureFailure(error))
        {
            // The original source chooses exception/issue precedence even if capture itself failed.
            return MortalWoundTreatmentSemanticData.ReadPreparationProjectionFallback(operation, () => Source(input));
        }
        if (eligible)
        {
            try
            {
                using var admissionTiming = FileSystemManager.MeasureCurrentOperation("JointPreparation.Replay.Admission");
                // Caller-specific d/e gates. Scalar hits retain only their original outer admissions.
                eligible = JointPreparationInputAgrees(fixedInput, authority, out _) &&
                    WoundAcceptedTurnPlannerCore.ValidateInput(fixedInput).Issues.Count == 0 &&
                    TryReadJointRepair(authority, out var current) && JointPreparationBaselineAgrees(current, out _, out _);
            }
            catch (Exception error) when (IsReplayCaptureFailure(error))
            {
                eligible = false; // moved preflight never supplies a newly prioritized refusal
            }
        }
        if (!eligible)
            return MortalWoundTreatmentSemanticData.ReadPreparationProjectionFallback(operation, () => Source(fixedInput));
        // The same fixed owned instance supplies admission, complete key, and original miss computation.
        return joint.SemanticData.ReadPreparationProjection(operation, captured!, () => Source(fixedInput));
    }
}
