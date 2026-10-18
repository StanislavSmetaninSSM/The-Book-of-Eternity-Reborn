using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // NI is a current ordinary owner, independent of optional continuous HOT.
    // Historical Stage coordinates are immutable provenance, never a revival of
    // its old ConsoleOwner. The enclosing NEW Main must stay current through use.
    private sealed class MortalJointInterfaceReady(HotRepairReadyRead actualRead, HotOrdinaryOriginal stage,
        FileSystemManager.MainAdmission owner, ValidatedPendingTurnSnapshotContext frame,
        string checkpointFingerprint, HotSourceObservation source)
    {
        internal readonly HotRepairReadyRead ActualRead = actualRead;
        internal readonly HotOrdinaryOriginal Stage = stage;
        internal readonly FileSystemManager.MainAdmission Owner = owner;
        internal readonly ValidatedPendingTurnSnapshotContext Frame = frame;
        internal readonly string CheckpointFingerprint = checkpointFingerprint;
        internal readonly long StageOrdinal = stage.Ordinal, ReadyOrdinal = actualRead.Ordinal;
        internal HotSourceObservation Source = source;
        internal bool DeleteReturned, ReleaseReturned, Faulted;
    }

    private MortalJointInterfaceReady? _mortalJointInterfaceReady;

    private async Task CaptureMatchingMortalJointInterfaceReadyAsync(HotRepairReadyRead read,
        ValidationRepairReady ready, PendingTurnSnapshotResolution resolution, string generation, bool workerRoute)
    {
        if (_mortalJointRepairCheckpoint is not { UsesInterfacePublication: true } checkpoint) return;
        _mortalJointInterfaceReady = null;
        try
        {
            var expected = checkpoint.ToJsonObject();
            if (workerRoute || _mortalJointInterfacePreparationHeld || _mortalJointInterfaceFreshConsumed ||
                _mortalJointInterfaceFreshCheckpoint != expected["fingerprint"]!.GetValue<string>() ||
                resolution.Context is not { } frame || !IsMatchingRepairReady(ready, frame) ||
                read.Origin is not { Preparation: not null, Ordinal: > 0 } stage)
                throw new InvalidDataException("joint_interface_ready_requires_actual_first_and_stage");
            await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
            ThrowIfRepairSessionReplaced(lease, generation);
            RequireOrdinaryJointRepairLease(lease);
            var main = lease.MainAdmission ?? throw new InvalidDataException("joint_interface_ready_main_missing");
            main.Validate(lease);
            var owner = main.Parent;
            if (owner is not { Closed: false, Acquired: true } || !main.SharesAccess(owner))
                throw new InvalidDataException("joint_interface_ready_enclosing_main_missing");
            var source = await ReadMortalJointInterfaceReadySourceAsync(lease, stage, frame, checkpoint);
            if (!HotBytesEqual(read.Bytes, await _fs.ReadFileBytesAsync(lease, ValidationRepairReadyPath)))
                throw new InvalidDataException("joint_interface_ready_changed_after_actual_read");
            _mortalJointInterfaceReady = new(read, stage, owner, frame, expected["fingerprint"]!.GetValue<string>(), source);
        }
        catch { _mortalJointInterfacePreparationHeld = true; throw; }
    }

    private async Task<HotSourceObservation> ReadMortalJointInterfaceReadySourceAsync(
        FileSystemManager.CanonicalWriteLease lease, HotOrdinaryOriginal stage,
        ValidatedPendingTurnSnapshotContext frame, MortalJointRepairCheckpoint checkpoint)
    {
        RequireOrdinaryJointRepairLease(lease);
        var main = lease.MainAdmission ?? throw new InvalidDataException("joint_interface_source_main_missing");
        main.Validate(lease);
        var expected = checkpoint.ToJsonObject();
        ThrowIfRepairSessionReplaced(lease, expected["sessionGeneration"]!.GetValue<string>());
        if (!ReferenceEquals(stage, _hotOrdinaryOriginal) || stage.Preparation is null || stage.Ordinal <= 0 ||
            stage.State is not ("Staged" or "StagedUnderLiveOwner") ||
            stage.SessionId != frame.SessionId || stage.RequestId != frame.RequestId || stage.SnapshotToken != frame.Payload.ManifestPayloadHash ||
            stage.Turn != frame.TurnNumber || stage.Generation != expected["sessionGeneration"]!.GetValue<string>() ||
            !MatchesMortalJointRepairFrame(frame, frame.TurnNumber))
            throw new InvalidDataException("joint_interface_source_actual_stage_binding");
        await RequireJointOriginalBytesAsync(lease, expected);
        var observed = PendingTurnSnapshotReader.ReadObservedCurrent(_fs, lease, new PendingTurnSnapshotPathSelection(
            ["game_state/meta/soul_state.json"], HotInputPaths.Where(path => path != "game_state/meta/soul_state.json")));
        if (!observed.Success || observed.Snapshot is not { } original || observed.Evidence is not { } evidence ||
            original.SessionId != frame.SessionId || original.RequestId != frame.RequestId ||
            original.SnapshotToken != frame.Payload.ManifestPayloadHash || original.TurnNumber != frame.TurnNumber)
            throw new InvalidDataException("joint_interface_source_current_original_missing");
        var current = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in HotInputPaths.Concat(HotOutputPaths).Concat(HotControlPaths))
            current.Add(path, await _fs.ReadFileBytesAsync(lease, path));
        var generation = _fs.ReadLocalGenerationSnapshot(lease);
        if (!HotBytesEqual(generation.Bytes, stage.GenerationBytes) ||
            !HotBytesEqual(current[PendingTurnSnapshotManifestPath], stage.ManifestBytes) ||
            !HotBytesEqual(current[PendingTurnSnapshotAuthority.AuthorityPath], stage.AuthorityBytes) ||
            !HotBytesEqual(current["input/turn_request.json"], stage.RequestBytes))
            throw new InvalidDataException("joint_interface_source_immutable_stage_images_changed");
        var rows = new JsonArray();
        foreach (var path in HotInputPaths)
        {
            var present = original.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal);
            var absent = original.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal);
            rows.Add(new JsonObject { ["path"] = path, ["sourceStatus"] = present ? "Present" : absent ? "Absent" : "UncoveredInSignedInventory",
                ["present"] = present ? true : absent ? false : null,
                ["base64"] = present ? Convert.ToBase64String(original.ReadRequiredBytes(path)) : null });
        }
        evidence.RequireCurrent(_fs, lease, original);
        await RequireHotCurrentImagesAsync(lease, current);
        return new(original, evidence, evidence.ExportDiagnosticData(), current, rows);
    }

    private async Task RecheckMortalJointInterfaceReadyAsync(FileSystemManager.CanonicalWriteLease lease,
        MortalJointInterfaceReady ready, bool requireReadyAbsent)
    {
        var main = lease.MainAdmission ?? throw new InvalidDataException("joint_interface_ready_recheck_main_missing");
        main.Validate(lease);
        if (!ReferenceEquals(ready, _mortalJointInterfaceReady) || ready.Faulted || ready.Owner.Closed ||
            !ready.Owner.Acquired || !main.SharesAccess(ready.Owner) ||
            _mortalJointRepairCheckpoint is not { UsesInterfacePublication: true } checkpoint ||
            ready.CheckpointFingerprint != checkpoint.ToJsonObject()["fingerprint"]!.GetValue<string>())
            throw new InvalidDataException("joint_interface_ready_current_owning_main_changed");
        var previous = ready.Source;
        var fresh = await ReadMortalJointInterfaceReadySourceAsync(lease, ready.Stage, ready.Frame, checkpoint);
        if (!JsonNode.DeepEquals(fresh.OriginalOperations, previous.OriginalOperations))
            throw new InvalidDataException("joint_interface_ready_original_operations_changed");
        foreach (var (path, before) in previous.Current)
            if (!HotBytesEqual(before, fresh.Current[path]) &&
                !(path == MortalJointInterfacePublication.InterfacePath && IsOwnedMortalJointInterfaceReadyTransition(
                    ready.StageOrdinal, ready.ReadyOrdinal, before, fresh.Current[path])))
                throw new InvalidDataException("joint_interface_ready_submitted_source_changed: " + path);
        var currentReady = await _fs.ReadFileBytesAsync(lease, ValidationRepairReadyPath);
        if (requireReadyAbsent ? currentReady is not null : !HotBytesEqual(currentReady, ready.ActualRead.Bytes))
            throw new InvalidDataException("joint_interface_ready_actual_consumption_changed");
        ready.Source = fresh with { Current = previous.Current.ToDictionary(row => row.Key, row => row.Value?.ToArray(), StringComparer.Ordinal) };
    }

    private void FailMortalJointInterfaceReady(HotRepairReadyRead? actualRead)
    {
        if (_mortalJointInterfaceReady is not { } ready || !ReferenceEquals(ready.ActualRead, actualRead)) return;
        ready.Faulted = true;
        _mortalJointInterfacePreparationHeld = true;
    }
}
