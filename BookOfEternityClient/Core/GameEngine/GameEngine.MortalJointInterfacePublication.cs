using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Only the actual initial wave install/readback/owning release/runtime refresh
    // can arm this one-shot private permission. Durable DATA cannot recreate it.
    private string? _mortalJointInterfaceFreshCheckpoint;
    private bool _mortalJointInterfaceFreshConsumed;
    private bool _mortalJointInterfacePreparationHeld;
    private long _mortalJointInterfaceValidationIteration;
    private string? _mortalJointInterfaceResumeCheckpoint;
    private MortalJointInterfacePublication.Record? _mortalJointInterfaceData;

    private void ArmFirstMortalJointInterfacePreparation(MortalJointRepairCheckpoint checkpoint)
    {
        if (!checkpoint.UsesInterfacePublication) return;
        var fingerprint = checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>();
        if (_mortalJointInterfaceFreshCheckpoint == fingerprint) return; // never rearm an attempted operation
        _mortalJointInterfaceFreshCheckpoint = fingerprint;
        _mortalJointInterfaceFreshConsumed = false;
        _mortalJointInterfacePreparationHeld = false;
        _mortalJointInterfaceResumeCheckpoint = null;
        _mortalJointInterfaceData = null;
    }

    private async Task<MortalJointInterfacePublication.Record?> PrepareCurrentMortalJointInterfaceAsync(
        ValidatedPendingTurnSnapshotContext frame)
    {
        var checkpoint = _mortalJointRepairCheckpoint ?? throw new InvalidDataException("joint_interface_actual_checkpoint_missing");
        if (!checkpoint.UsesInterfacePublication) return null;
        var expected = checkpoint.ToJsonObject(); var fingerprint = expected["fingerprint"]!.GetValue<string>();
        if (_mortalJointInterfaceData is { } prior)
        {
            await using var currentLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            await RequireMortalJointInterfacePendingAsync(currentLease, prior, frame);
            return prior;
        }
        if (_mortalJointInterfacePreparationHeld || _mortalJointInterfaceFreshCheckpoint != fingerprint ||
            _mortalJointInterfaceFreshConsumed)
            throw new InvalidDataException("joint_interface_missing_result_has_no_fresh_permission");
        // Consume BEFORE I/freshness reads, projector or UTC. Any failure, including
        // a same-engine failure without Prepared bytes, remains sticky held.
        _mortalJointInterfaceFreshConsumed = true;
        try
        {
            var result = await PrepareAndPublishWithOwningReleaseAsync();
            _mortalJointInterfaceData = result;
            if (result.HasErrors) _mortalJointInterfacePreparationHeld = true;
            return result;
        }
        catch { _mortalJointInterfacePreparationHeld = true; throw; }

        async Task<MortalJointInterfacePublication.Record> PrepareAndPublishWithOwningReleaseAsync()
        {
            var lease = await _fs.AcquireCanonicalWriteLeaseAsync(); Exception? failure = null;
            try
            {
                RequireOrdinaryJointRepairLease(lease);
                var capture = await ReadCurrentJointRejectedCaptureAsync(lease, expected);
                _ = await MortalJointRepairCheckpoint.RevalidateCorrectionAsync(_fs, lease, capture);
                var ready = _hotRepairReady;
                if (ready is not { Faulted: false, Status: "ConsumedReadyMatched", ReadyDeleteReturned: true,
                        ReadyReleaseReturned: true, Origin: { } origin, Source: { } source })
                    throw new InvalidDataException("joint_interface_fresh_requires_actual_consumed_ready");
                await RecheckHotReadySourceAsync(lease, ready, requireReadyAbsent: true);
                if (_mortalJointInterfaceValidationIteration <= 0) throw new InvalidDataException("joint_interface_iteration_missing");
                var binding = new JsonObject
                {
                    ["sessionGeneration"] = expected["sessionGeneration"]!.DeepClone(), ["sessionId"] = frame.SessionId,
                    ["requestId"] = frame.RequestId, ["snapshotToken"] = frame.Payload.ManifestPayloadHash,
                    ["turnNumber"] = frame.TurnNumber, ["realm"] = "mortal_world", ["stageOrdinal"] = origin.Ordinal,
                    ["readyOrdinal"] = ready.Ordinal, ["iterationOrdinal"] = _mortalJointInterfaceValidationIteration,
                    ["checkpointFingerprint"] = fingerprint, ["operationKey"] = expected["operationKey"]!.DeepClone(),
                    ["attemptId"] = expected["attemptId"]!.DeepClone(), ["requestFingerprint"] = expected["requestFingerprint"]!.DeepClone()
                };
                var computed = await _validator.PrepareMortalJointInterfaceAsync(lease);
                if (!HotBytesEqual(computed.Before, source.Current[MortalJointInterfacePublication.InterfacePath]))
                    throw new InvalidDataException("joint_interface_consumed_input_does_not_match_ready");
                var pending = await _fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath)
                    ?? throw new InvalidDataException("joint_interface_wave_missing");
                var prepared = MortalJointInterfacePublication.CreatePrepared(binding, capture.Pending,
                    MortalJointInterfaceSourceFingerprint(source), computed);
                async Task RequireOwner()
                {
                    ThrowIfRepairSessionReplaced(lease, expected["sessionGeneration"]!.GetValue<string>());
                    await RequireJointOriginalBytesAsync(lease, expected);
                    _ = await ReadCurrentJointRejectedCaptureAsync(lease, expected);
                    await RecheckHotReadySourceAsync(lease, ready, requireReadyAbsent: true);
                }
                var preparedBytes = await MortalJointInterfacePublication.CommitPreparedAsync(_fs, lease, pending, prepared, RequireOwner);
                if (prepared.HasErrors) return prepared; // frozen error/I-before; no I/common/recompute
                return await MortalJointInterfacePublication.CommitPublishedAsync(_fs, lease, preparedBytes, RequireOwner);
            }
            catch (Exception error) { failure = error; throw; }
            finally
            {
                // completed=false deliberately preserves a close failure. A
                // committed record remains durable, but cannot fake this return.
                await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, false, failure);
            }
        }
    }

    private static string MortalJointInterfaceSourceFingerprint(HotSourceObservation source)
    {
        var values = new List<string?> { "book_of_eternity.mortal_joint_interface.actual_ready_source.v1",
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(source.OriginalOperations) };
        foreach (var (path, bytes) in source.Current.OrderBy(row => row.Key, StringComparer.Ordinal))
        { values.Add(path); values.Add(bytes is null ? null : Convert.ToHexString(SHA256.HashData(bytes))); }
        return WoundAcceptedTurnFingerprintWriter.Compute(values);
    }

    private bool IsOwnedMortalJointInterfaceReadyTransition(HotRepairReadyRead ready, byte[]? original, byte[]? current)
    {
        if (_mortalJointInterfaceData is not { State: "Published", HasErrors: false } result ||
            _mortalJointRepairCheckpoint is not { UsesInterfacePublication: true } checkpoint) return false;
        var binding = result.Binding;
        return binding["checkpointFingerprint"]!.GetValue<string>() == checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>() &&
            ready.Origin is { } origin && binding["stageOrdinal"]!.GetValue<long>() == origin.Ordinal &&
            binding["readyOrdinal"]!.GetValue<long>() == ready.Ordinal &&
            binding["iterationOrdinal"]!.GetValue<long>() == _mortalJointInterfaceValidationIteration &&
            HotBytesEqual(original, result.BeforeBytes) && HotBytesEqual(current, result.AfterBytes);
    }

    private async Task RequireMortalJointInterfacePendingAsync(FileSystemManager.CanonicalWriteLease lease,
        MortalJointInterfacePublication.Record result, ValidatedPendingTurnSnapshotContext frame)
    {
        if (_mortalJointRepairCheckpoint is not { UsesInterfacePublication: true } checkpoint || result.State != "Published" || result.HasErrors ||
            !MatchesMortalJointRepairFrame(frame, frame.TurnNumber)) throw new InvalidDataException("joint_interface_consumer_binding");
        result.RequireCheckpoint(checkpoint.ToJsonObject());
        var raw = await _fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath)
            ?? throw new InvalidDataException("joint_interface_consumer_wave_missing");
        var pending = ReadOrdinaryTreatmentRoot(raw, "actual interface consumer wave");
        var current = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(pending[MortalJointInterfacePublication.PendingField]));
        current.RequireLegacyPending(pending);
        if (current.Fingerprint != result.Fingerprint || !_fs.IsCurrentSessionGeneration(lease, result.Binding["sessionGeneration"]!.GetValue<string>()))
            throw new InvalidDataException("joint_interface_consumer_record_changed");
        await RequireJointOriginalBytesAsync(lease, checkpoint.ToJsonObject());
        await MortalJointInterfacePublication.RequireImagesAsync(_fs, lease, raw, result.AfterBytes);
    }

    // Mandatory selected-protocol decision occurs while actual attempt is still
    // Capturing. Optional HOT source failure cannot admit missing/invalid I DATA.
    private async Task ConsumeCurrentMortalJointInterfaceAsync(FileSystemManager.CanonicalWriteLease lease,
        MortalJointPublicationAttempt attempt)
    {
        if (_mortalJointRepairCheckpoint is not { UsesInterfacePublication: true } checkpoint) return;
        attempt.RequireActualOwner(_fs, lease);
        if (_mortalJointInterfacePreparationHeld || _mortalJointInterfaceData is not { } result)
            throw new InvalidDataException("joint_interface_current_consumer_unavailable");
        var resolved = await ResolveActivePendingTurnSnapshotContextAsync();
        var frame = resolved.Context ?? throw new InvalidDataException("joint_interface_current_frame_missing");
        await RequireMortalJointInterfacePendingAsync(lease, result, frame);
        if (_mortalJointInterfaceResumeCheckpoint == checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>())
            return; // new current admission, not reconstructed old Ready/HOT availability
        if (_hotRepairReady is not { Faulted: false, Source: { } source } ready ||
            !IsOwnedMortalJointInterfaceReadyTransition(ready, source.Current[MortalJointInterfacePublication.InterfacePath], result.AfterBytes))
            throw new InvalidDataException("joint_interface_ready_transition_missing");
        if (result.SourceAssociationFingerprint != MortalJointInterfaceSourceFingerprint(source))
            throw new InvalidDataException("joint_interface_actual_ready_source_association_changed");
        await RecheckHotReadySourceAsync(lease, ready, requireReadyAbsent: true);
    }

    private async Task<IReadOnlyList<ValidationIssue>> ReadMortalJointInterfaceAtLateSlotAsync(
        ValidatedPendingTurnSnapshotContext frame)
    {
        MortalJointInterfacePublication.Record? result;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
            result = await MortalQteDecisionPublication.ReadCurrentJointInterfaceDataAsync(_fs, lease,
                frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, frame.TurnNumber);
        if (result is null) return await _validator.ValidateAcceptedTurnInterfacePayloadAsync(); // genuine child1
        _mortalJointInterfaceData = result;
        return result.Issues; // exactly once at this original I slot, after late N
    }

    private async Task<IReadOnlyList<ValidationIssue>> ReadMortalJointInterfaceAtGenericSlotAsync()
    {
        MortalJointInterfacePublication.Record? current;
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            await RequireNoMortalJointInterfaceRestoreAsync(lease, blockSelectedMissing: true);
            current = await MortalQteDecisionPublication.ReadJointInterfaceAtOrdinarySlotAsync(_fs, lease);
        }
        if (current is not null) return current.Issues;
        return await _validator.ValidateAcceptedTurnInterfacePayloadAsync(); // original public owning lease after inspection closed
    }
}
