using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Completed-common dispatch precedes this method. This selected pending
    // branch precedes system ensure, quarantine, ordinary N/I and baseline restore.
    private async Task<AcceptedTurnValidationDisposition?> TryResumeMortalJointInterfaceBeforeCoreAsync(
        string source, ValidatedPendingTurnSnapshotContext? suppliedFrame, int expectedTurn)
    {
        MortalJointRepairCheckpoint? checkpoint = null;
        MortalJointInterfacePublication.Record? retained = null;
        ValidatedPendingTurnSnapshotContext? frame = null;
        try
        {
            await using (var inspectionLease = await _fs.AcquireCanonicalWriteLeaseAsync())
            {
                var bytes = await _fs.ReadFileBytesAsync(inspectionLease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
                if (bytes is null) return null;
                var pending = ReadOrdinaryTreatmentRoot(bytes, "current selected interface wave");
                if (pending["jointRepairCheckpoint"] is not { } node)
                {
                    if (pending.ContainsKey(MortalJointInterfacePublication.PendingField)) throw new MortalJointRepairPublicationHoldException();
                    return null;
                }
                var parsed = MortalJointRepairCheckpoint.Parse(JsonSerializer.SerializeToElement(node));
                if (!parsed.IsValid || parsed.Checkpoint is null) throw new MortalJointRepairPublicationHoldException();
                checkpoint = parsed.Checkpoint;
                if (!checkpoint.UsesInterfacePublication)
                {
                    if (pending.ContainsKey(MortalJointInterfacePublication.PendingField)) throw new MortalJointRepairPublicationHoldException();
                    return null; // existing genuine v1 operation, never upgraded
                }
                var binding = checkpoint.ToJsonObject();
                if (pending[MortalJointInterfacePublication.PendingField] is not { } data)
                {
                    // Only this process's actually installed and not-yet-consumed
                    // private FIRST may enter its existing initial repair loop.
                    if (!_mortalJointInterfacePreparationHeld && !_mortalJointInterfaceFreshConsumed &&
                        _mortalJointInterfaceFreshCheckpoint == binding["fingerprint"]!.GetValue<string>()) return null;
                    throw new MortalJointRepairPublicationHoldException();
                }
                retained = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(data));
                retained.RequireLegacyPending(pending);
                if (retained.HasErrors) return HeldWithFrozenIssues(retained.Issues);
            }
            var resolution = await ResolveActivePendingTurnSnapshotContextAsync();
            frame = resolution.Context;
            if (frame is null || !SameMortalJointOriginalFrame(suppliedFrame, frame, expectedTurn))
                throw new InvalidDataException("joint_interface_resume_requires_new_current_original_frame");
            _mortalJointRepairCheckpoint = checkpoint;
            if (!MatchesMortalJointRepairFrame(frame, expectedTurn)) throw new InvalidDataException("joint_interface_resume_frame_changed");
            _mortalJointInterfaceValidationIteration++;
            MortalJointInterfacePublication.Record current;
            var lease = await _fs.AcquireCanonicalWriteLeaseAsync(); Exception? failure = null;
            try
            {
                RequireOrdinaryJointRepairLease(lease);
                var expected = checkpoint.ToJsonObject();
                var capture = await ReadCurrentJointRejectedCaptureAsync(lease, expected);
                _ = await MortalJointRepairCheckpoint.RevalidateCorrectionAsync(_fs, lease, capture);
                using var snapshotScope = _validator.UsePrevalidatedPendingTurnSnapshotScope(frame.Manifest);
                var upstream = await CollectAcceptedTurnUpstreamRawStateIssuesAsync();
                if (upstream.RejectSpiritualBoundary || upstream.Issues.Any(issue => issue.Severity == IssueSeverity.Error))
                    return HeldWithFrozenIssues(upstream.Issues);
                var narrative = await _validator.ValidateMortalJointAcceptedNarrativeReadOnlyAsync();
                if (narrative.Any(issue => issue.Severity == IssueSeverity.Error)) return HeldWithFrozenIssues(narrative);
                // Warning-only N omission matches the existing pre-intake path.
                // No N/I normalizer, ensure, spiritual continuation, scheduler or
                // old take is called to turn a stored I result into upstream=true.
                MortalJointRepairRawAdmission? admitted = null;
                var resource = await _validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease,
                    inspectMortalJointRepair: async currentLease =>
                        admitted = await InspectOrdinaryMortalJointRepairAsync(currentLease, frame, expectedTurn));
                if (admitted is not { Disposition: MortalJointRepairRawDisposition.Owned } ||
                    resource.Any(issue => issue.Severity == IssueSeverity.Error)) return HeldWithFrozenIssues(resource);
                await admitted.ValidateCurrentAsync(_fs, lease, frame.SessionId, frame.RequestId,
                    frame.Payload.ManifestPayloadHash, expectedTurn);
                var before = await _fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath)
                    ?? throw new InvalidDataException("joint_interface_resume_pending_missing");
                var exact = ReadOrdinaryTreatmentRoot(before, "resume exact publication wave");
                var observed = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(exact[MortalJointInterfacePublication.PendingField]));
                if (observed.Fingerprint != retained.Fingerprint) throw new InvalidDataException("joint_interface_resume_record_changed");
                async Task RequireCurrentOwner()
                {
                    ThrowIfRepairSessionReplaced(lease, expected["sessionGeneration"]!.GetValue<string>());
                    await RequireJointOriginalBytesAsync(lease, expected);
                    var fresh = await ReadCurrentJointRejectedCaptureAsync(lease, expected);
                    _ = await MortalJointRepairCheckpoint.RevalidateCorrectionAsync(_fs, lease, fresh);
                    await admitted.ValidateCurrentAsync(_fs, lease, frame.SessionId, frame.RequestId,
                        frame.Payload.ManifestPayloadHash, expectedTurn);
                }
                current = await MortalJointInterfacePublication.CommitPublishedAsync(_fs, lease, before, RequireCurrentOwner);
            }
            catch (Exception error) { failure = error; throw; }
            finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, false, failure); }
            // This is a NEW current adapter's owning release, never evidence of a
            // historical return or a revived Ready/attempt from the old process.
            _mortalJointInterfaceData = current;
            _mortalJointInterfaceResumeCheckpoint = checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>();
            _mortalJointInterfacePreparationHeld = false;
            _mortalJointRepairCorrectionReady = true;
            if (_mortalJointRepairCanonicalRefresh is { Disposition: MortalJointRepairPublicationDisposition.PublicationUncertain } diagnostic &&
                SameMortalJointOriginalFrame(diagnostic.Frame, frame, expectedTurn))
                _mortalJointRepairCanonicalRefresh = null;
            // Retire ONLY obsolete bookkeeping after this selected2 NEW current
            // admission, exact stored-result publication/readback and own release.
            // No old attempt, Ready, take or historical return is revived. The
            // actual canonical entry still reconstructs/consumes every owner.
            var baseline = await LoadCanonicalBaselineSnapshotAsync(expectedTurn, frame)
                ?? throw new InvalidDataException("joint_interface_resume_current_baseline_missing");
            var publication = await PublishCurrentMortalJointRepairAsync(frame, expectedTurn, baseline);
            return await ValidateMortalJointPostPublicationAsync(source, frame, publication);
        }
        catch (MortalJointRepairPublicationHoldException)
        {
            _mortalJointInterfacePreparationHeld = true;
            return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
        }
        catch (Exception error) when (checkpoint?.UsesInterfacePublication == true && error is
            InvalidDataException or IOException or InvalidOperationException or ArgumentException or JsonException or AggregateException)
        {
            _mortalJointInterfacePreparationHeld = true;
            _logger.LogError(error, "Selected joint I result retained; no ordinary normalization/restore fallback.");
            return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
        }

        AcceptedTurnValidationDisposition HeldWithFrozenIssues(IReadOnlyList<ValidationIssue> issues)
        {
            _mortalJointInterfacePreparationHeld = true;
            ReportMortalJointInterfaceFrozenIssues(issues);
            return AcceptedTurnValidationDisposition.MortalJointRepairPublicationHeldBlocked;
        }
    }

    // Both the direct generic restore and the early wrapper are protected. A
    // bypassed entry dispatcher cannot erase a Prepared/Published graph.
    private async Task RequireNoMortalJointInterfaceRestoreAsync(FileSystemManager.CanonicalWriteLease lease,
        bool blockSelectedMissing = false)
    {
        var bytes = await _fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        if (bytes is null) return;
        var pending = ReadOrdinaryTreatmentRoot(bytes, "joint I retention before restore");
        if (pending[MortalJointInterfacePublication.PendingField] is not null) throw new MortalJointRepairPublicationHoldException();
        if (pending["jointRepairCheckpoint"] is not { } node) return;
        var parsed = MortalJointRepairCheckpoint.Parse(JsonSerializer.SerializeToElement(node));
        if (!parsed.IsValid) throw new MortalJointRepairPublicationHoldException();
        if (parsed.Checkpoint?.UsesInterfacePublication == true && (blockSelectedMissing || _mortalJointInterfacePreparationHeld ||
            _mortalJointInterfaceFreshCheckpoint != parsed.Checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>() ||
            _mortalJointInterfaceFreshConsumed && _mortalJointInterfaceFreshCheckpoint ==
                parsed.Checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>()))
            throw new MortalJointRepairPublicationHoldException();
    }
}
