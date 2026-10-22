using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    // Detached calculation data only. The existing resource/common issuer binds
    // this pair; parsing or PlanConsumeAndAppend alone grants no authority.
    private sealed record JointQteContribution(string Fingerprint,
        IReadOnlyDictionary<string, JsonObject> AfterImages);

    /// <summary>
    /// Recomputes detached QTE data with both baseline checks and a fresh exit proof for an eligible reserved source read set.
    /// </summary>
    /// <param name="files">
    /// The filesystem containing the actual current original and corrected command.
    /// </param>
    /// <param name="lease">
    /// The active canonical write lease used for the current source checks and reads.
    /// </param>
    /// <param name="baseline">
    /// The private common baseline whose exact source must remain valid through preparation.
    /// </param>
    /// <returns>
    /// A detached fingerprint and companion images, finalized only after the exit check succeeds.
    /// </returns>
    private static async Task<JointQteContribution> RecomputeJointQteContributionAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease, JointCommonBaselineAuthority baseline)
    {
        var readSet = files.TryBeginReservedValidationReadSet(lease);
        Exception? primaryFailure = null;
        try
        {
            if (readSet != null) await readSet.OpenAsync();
            var prepared = await JointRepairValidationCoordinator.RecomputeWithBaselineAsync(
                () => ValidateJointRepairCommonBaselineAsync(files, lease, baseline),
                () => PrepareJointQteContributionAsync(files, lease, baseline));
            if (readSet != null) await readSet.CompleteAsync();
            return new(WoundAcceptedTurnFingerprintWriter.Compute(prepared.Fields), prepared.AfterImages);
        }
        catch (Exception error) { primaryFailure = error; throw; }
        finally
        {
            try { if (readSet != null) await readSet.DisposeAsync(); }
            catch when (primaryFailure != null) { /* Preserve the original computation or proof refusal. */ }
        }
    }

    /// <summary>
    /// Reads and prepares the real QTE contribution without finalizing a result or issuing authority.
    /// </summary>
    /// <param name="files">
    /// The filesystem containing the authenticated original and corrected command.
    /// </param>
    /// <param name="lease">
    /// The active canonical write lease required by reconstruction and original reads.
    /// </param>
    /// <param name="baseline">
    /// The private baseline checked by the surrounding entry and exit operations.
    /// </param>
    /// <returns>
    /// Detached fingerprint fields and successor images for finalization after the exit check.
    /// </returns>
    private static async Task<(List<string?> Fields, IReadOnlyDictionary<string, JsonObject> AfterImages)>
        PrepareJointQteContributionAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            JointCommonBaselineAuthority baseline)
    {
        using var operationTiming = files.MeasureOperation("JointQte.PrepareAsync");
        if (!TryReadJointRepair(baseline.Continuation, out var joint))
            throw new InvalidDataException("The QTE contribution requires the actual joint continuation.");
        var input = joint.AcquisitionInput;
        var binding = input.Binding;
        using var heavyTimingJointQteReconstructAsync = files.MeasureOperation("JointQte.ReconstructAsync");
        var reconstructed = await MortalJointRepairCheckpoint.ReconstructAsync(files, lease,
            binding.SessionId, binding.RequestId, binding.SnapshotToken, binding.Turn);
        heavyTimingJointQteReconstructAsync?.Dispose();
        if (!reconstructed.IsValid || reconstructed.Capture is not { } capture ||
            capture.Accepted.AcceptedStateFingerprint != joint.Accepted.AcceptedStateFingerprint ||
            capture.Request.RequestFingerprint != joint.Request.RequestFingerprint ||
            capture.Resolution.ResolutionAuthorityFingerprint != joint.Resolution.ResolutionAuthorityFingerprint ||
            !JsonNode.DeepEquals(capture.Checkpoint.ToJsonObject(), joint.Checkpoint.ToJsonObject()))
            throw new InvalidDataException("The fresh original/request no longer reproduces the joint QTE owner.");
        using var heavyTimingJointQteRevalidateCorrectionAsync = files.MeasureOperation("JointQte.RevalidateCorrectionAsync");
        var corrected = await MortalJointRepairCheckpoint.RevalidateCorrectionAsync(files, lease, capture);
        heavyTimingJointQteRevalidateCorrectionAsync?.Dispose();
        using (files.EnterOperationTimingContext())
        {
            if (!corrected.Success || corrected.SourceBinding is not { } source ||
                WoundAcceptedTurnFingerprints.ComputeBinding(source) != WoundAcceptedTurnFingerprints.ComputeBinding(binding) ||
                corrected.CommandRoot is not { } commandRoot || !JsonNode.DeepEquals(commandRoot, joint.AcquisitionCommandRoot))
                throw new InvalidDataException("The real corrected QTE command changed after joint preparation.");
            using var heavyTimingJointQteRecomposeDecision = files.MeasureOperation("JointQte.RecomposeDecision");
            var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(commandRoot),
                corrected.MaterializedOpportunities);
            if (!parsed.Success || parsed.Commands.Count != 1 || parsed.TreatmentCommands.Count != 0 ||
                parsed.AcceptedTransitionCommands.Count != 0)
                throw new InvalidDataException("The joint QTE contribution requires exactly its authenticated acquisition command.");
            var command = parsed.Commands[0];
            if (command.Opportunity.ProfileKey != "mortal_qte_terminal_health_damage_v1" ||
                command.Decision.GetProperty("decision").GetString() != "materialize")
                throw new InvalidDataException("The joint QTE contribution must select its exact physical CREATE profile.");
            var fresh = WoundResponseInputComposer.RecomposeCommandRoot(binding, parsed,
                Array.Empty<WoundOpportunityDecisionReceipt>());
            if (!fresh.Success || fresh.DecisionReceipts.Count != 1 || corrected.DecisionReceipts.Count != 1 ||
                fresh.DecisionReceipts[0] != corrected.DecisionReceipts[0])
                throw new InvalidDataException("The actual corrected command must reproduce its exact decision receipt.");

            heavyTimingJointQteRecomposeDecision?.Dispose();
            using var heavyTimingJointQteReadOriginal = files.MeasureOperation("JointQte.ReadOriginal");
            var original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
            if (original.Realm != "mortal_world" || original.SessionId != binding.SessionId ||
                original.RequestId != binding.RequestId || original.SnapshotToken != binding.SnapshotToken || original.TurnNumber != binding.Turn)
                throw new InvalidDataException("The joint QTE partition requires the same official original.");
            heavyTimingJointQteReadOriginal?.Dispose();
            // ReadObservedRoot admits only authenticated present bytes or the closed
            // signed absence. The empty receipt view is the existing pristine reader
            // contract, never an invented physical before-image or replacement root.
            using var heavyTimingJointQteParsePartitionAndHistory = files.MeasureOperation("JointQte.ParsePartitionAndHistory");
            var pendingJson = MortalQteWoundOpportunityProjection.ReadObservedRoot(original, MortalWoundOccurrenceState.StatePath);
            if (pendingJson is null) throw new InvalidDataException("The actual QTE source occurrence is absent from the signed original.");
            var pending = MortalWoundOccurrenceState.Parse(pendingJson, MortalWoundOccurrenceState.StatePath);
            var receipts = MortalWoundOpportunityReceiptState.Parse(
                MortalQteWoundOpportunityProjection.ReadObservedRoot(original, MortalWoundOpportunityReceiptState.StatePath) ??
                    "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}", MortalWoundOpportunityReceiptState.StatePath);
            var history = WoundHistoryState.Parse(baseline.Bundle.FinalPlan.HistoryAfterImage.ToJsonString(), WoundHistoryState.HistoryPath);
            if (!pending.IsValid || pending.State is null || !receipts.IsValid || receipts.State is null ||
                !history.IsValid || history.State is null ||
                !pending.State.Occurrences.Any(row => row.OccurrenceId == command.Opportunity.OpportunityId && row.AdapterKind == "qte"))
                throw new InvalidDataException("The full signed QTE partition and joint final history must be valid.");
            heavyTimingJointQteParsePartitionAndHistory?.Dispose();
            using var heavyTimingJointQteConsumeAndAppend = files.MeasureOperation("JointQte.ConsumeAndAppend");
            var receipt = fresh.DecisionReceipts[0];
            var create = history.State.Transitions.Where(row => row.OperationKey == receipt.OperationKey).ToArray();
            if (create.Length != 1)
                throw new InvalidDataException("The actual QTE decision must select exactly its CREATE in the full joint history.");
            var consumed = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(pending.State, receipts.State,
                new MortalWoundOpportunityReceiptDraft(new(binding.SessionId, binding.RequestId, binding.SnapshotToken, binding.Turn),
                    command.Opportunity.OpportunityId, command.Opportunity.AuthorityFingerprint, "materialize",
                    receipt.DecisionFingerprint, receipt.OperationKey, create[0].WoundId, create[0].TransitionId), history.State);
            if (consumed.Disposition != "appended" || consumed.Issues.Count != 0 || consumed.OccurrenceState is null ||
                consumed.ReceiptState is null || consumed.Receipt is null)
                throw new InvalidDataException("The joint QTE contribution must freshly consume its exact occurrence and append its receipt.");
            heavyTimingJointQteConsumeAndAppend?.Dispose();
            using var heavyTimingJointQteBuildSuccessorsAndFields = files.MeasureOperation("JointQte.BuildSuccessorsAndFields");
            var before = baseline.Mechanical.BeforeImages;
            if (!before.TryGetValue(MortalWoundOpportunityAdapter.DecisionPath, out var draft) || !draft.Existed || draft.Bytes is null)
                throw new InvalidDataException("The QTE contribution requires its exact actual public-draft before-image.");
            var successors = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
            {
                [MortalWoundOccurrenceState.StatePath] = JsonNode.Parse(MortalWoundOccurrenceState.SerializeCanonical(consumed.OccurrenceState))!.AsObject(),
                [MortalWoundOpportunityReceiptState.StatePath] = JsonNode.Parse(MortalWoundOpportunityReceiptState.SerializeCanonical(consumed.ReceiptState))!.AsObject()
            };
            var fields = new List<string?> { "book_of_eternity.mortal_joint_repair.qte_contribution.v1",
                baseline.Fingerprint, joint.Fingerprint, WoundAcceptedTurnFingerprints.ComputeBinding(binding),
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(commandRoot), draft.Fingerprint,
                MortalWoundOpportunityAdapter.DecisionPath, consumed.Receipt.ReceiptFingerprint };
            foreach (var row in successors.OrderBy(row => row.Key, StringComparer.Ordinal))
            { fields.Add(row.Key); fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(row.Value)); }
            heavyTimingJointQteBuildSuccessorsAndFields?.Dispose();
            return (fields, successors);
        }
    }
}
