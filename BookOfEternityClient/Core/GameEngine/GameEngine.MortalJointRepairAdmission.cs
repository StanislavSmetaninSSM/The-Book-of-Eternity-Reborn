using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private static readonly object MortalJointRepairRawIssuer = new();
    private MortalJointRepairCheckpoint? _mortalJointRepairCheckpoint;
    private bool _mortalJointRepairCorrectionReady;
    private string _mortalJointRepairTerminalReason = "validation_failed";

    private bool MatchesMortalJointRepairFrame(ValidatedPendingTurnSnapshotContext? frame, int turn)
    {
        if (_mortalJointRepairCheckpoint is not { } checkpoint || frame is null) return false;
        var binding = checkpoint.ToJsonObject();
        return frame.TurnNumber == turn && binding["turnNumber"]!.GetValue<int>() == turn &&
            binding["sessionId"]!.GetValue<string>() == frame.SessionId &&
            binding["requestId"]!.GetValue<string>() == frame.RequestId &&
            binding["snapshotToken"]!.GetValue<string>() == frame.Payload.ManifestPayloadHash;
    }

    internal enum MortalJointRepairRawDisposition { NotApplicable, Rejected, Owned }

    // Read-only, lease-local intake only. It grants no plan take/publication. The
    // canonical owner must fully reconstruct under its own lease before planning.
    internal sealed class MortalJointRepairRawAdmission
    {
        private readonly FileSystemManager _files;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly MortalJointRepairCheckpoint.RejectedCapture? _capture;
        private readonly Dictionary<string, byte[]?> _guards;
        private readonly ValidationIssue[] _issues;
        internal MortalJointRepairRawAdmission(object issuer, FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalJointRepairRawDisposition disposition, MortalJointRepairCheckpoint.RejectedCapture? capture,
            IReadOnlyDictionary<string, byte[]?> guards, IReadOnlyList<ValidationIssue> issues)
        {
            if (!ReferenceEquals(issuer, MortalJointRepairRawIssuer)) throw new InvalidOperationException("Joint raw intake requires its actual engine issuer.");
            _files = files; _lease = lease; Disposition = disposition; _capture = capture;
            _guards = guards.ToDictionary(row => row.Key, row => row.Value?.ToArray(), StringComparer.Ordinal);
            _issues = issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray();
        }
        internal MortalJointRepairRawDisposition Disposition { get; }
        internal IReadOnlyList<ValidationIssue> Issues => _issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray();
        internal async Task ValidateCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            string sessionId, string requestId, string snapshotToken, int turn)
        {
            files.EnsureCanonicalWriteLeaseActive(lease);
            if (!ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease))
                throw new InvalidDataException("Joint raw intake cannot cross a root/lease boundary.");
            if (_capture is null)
            {
                if (Disposition == MortalJointRepairRawDisposition.Owned)
                    throw new InvalidDataException("Owned intake requires genuine rejected-source reconstruction.");
                return;
            }
            _capture.EnsureCurrent(files, lease);
            var binding = _capture.Accepted.Binding;
            if (binding.SessionId != sessionId || binding.RequestId != requestId || binding.SnapshotToken != snapshotToken || binding.Turn != turn)
                throw new InvalidDataException("Joint intake does not match the ordinary admitted original.");
            foreach (var (path, before) in _guards)
                if (!JointRestoreBytesEqual(await files.ReadFileBytesAsync(lease, path), before))
                    throw new InvalidDataException("Joint raw intake changed before its early return: " + path);
            if (Disposition == MortalJointRepairRawDisposition.Owned)
                _ = await MortalJointRepairCheckpoint.RevalidateCorrectionAsync(files, lease, _capture);
        }
    }

    private async Task<MortalJointRepairRawAdmission> InspectOrdinaryMortalJointRepairAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame, int expectedTurn)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        var guards = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        MortalJointRepairRawAdmission Result(MortalJointRepairRawDisposition disposition,
            MortalJointRepairCheckpoint.RejectedCapture? capture, IReadOnlyList<ValidationIssue> issues) =>
            new(MortalJointRepairRawIssuer, _fs, lease, disposition, capture, guards, issues);
        try
        {
            var commandBytes = await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
            var pendingBytes = await _fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            var draftBytes = await _fs.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath);
            var hasCheckpoint = _mortalJointRepairCheckpoint is not null;
            var pending = pendingBytes is null ? null : ReadOrdinaryTreatmentRoot(pendingBytes, "joint intake private wave");
            hasCheckpoint |= pending?["jointRepairCheckpoint"] is not null;
            if (!hasCheckpoint && (commandBytes is null || draftBytes is null))
                return Result(MortalJointRepairRawDisposition.NotApplicable, null, Array.Empty<ValidationIssue>());
            if (commandBytes is null)
                return Result(MortalJointRepairRawDisposition.Rejected, null, JointRepairAdmissionIssue("The exact held treatment command is missing."));
            var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(
                ReadOrdinaryTreatmentRoot(commandBytes, "joint intake command")));
            if (!hasCheckpoint && parsed.TreatmentCommands.Count == 0)
                return Result(MortalJointRepairRawDisposition.NotApplicable, null, Array.Empty<ValidationIssue>());
            if (!parsed.Success || parsed.TreatmentCommands.Count != 1 || parsed.Commands.Count != 0 || parsed.AcceptedTransitionCommands.Count != 0)
                return Result(MortalJointRepairRawDisposition.Rejected, null, JointRepairAdmissionIssue("One exact homogeneous guaranteed treatment command is required."));
            var captured = pending?["jointRepairCheckpoint"] is not null
                ? await MortalJointRepairCheckpoint.ReconstructAsync(_fs, lease, frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, expectedTurn)
                : await MortalJointRepairCheckpoint.CaptureAsync(_fs, lease, frame.SessionId, frame.RequestId, frame.Payload.ManifestPayloadHash, expectedTurn);
            if (!captured.IsValid || captured.Capture is not { } capture)
                return Result(MortalJointRepairRawDisposition.Rejected, null,
                    captured.Issues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue).ToArray());
            if (frame.TurnNumber != expectedTurn || _mortalJointRepairCheckpoint is { } retained &&
                !JsonNode.DeepEquals(retained.ToJsonObject(), capture.Checkpoint.ToJsonObject()))
                return Result(MortalJointRepairRawDisposition.Rejected, null, JointRepairAdmissionIssue("The original source/request/checkpoint cannot change during this repair."));
            _mortalJointRepairCheckpoint ??= capture.Checkpoint;
            foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath, WoundAcceptedTurnSnapshotContract.PendingResolutionPath,
                MortalWoundOpportunityAdapter.DecisionPath, WoundNarrativeOutputPath,
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath })
                guards[path] = await _fs.ReadFileBytesAsync(lease, path);
            if (pending?["jointRepairCheckpoint"] is null || draftBytes is null)
                return Result(MortalJointRepairRawDisposition.Rejected, capture, capture.RejectedIssues.Select(BindMortalQteDraftRepairTarget).ToArray());
            var current = ReadOrdinaryTreatmentRoot(draftBytes, "joint public correction");
            var rejected = ReadOrdinaryTreatmentRoot(Convert.FromBase64String(capture.Checkpoint.ToJsonObject()["rejectedDraftBase64"]!.GetValue<string>()), "joint retained rejected draft");
            var normalized = current.DeepClone().AsObject();
            if (normalized["proposal"] is JsonObject proposal && rejected["proposal"] is JsonObject rejectedProposal)
                proposal["severity"] = rejectedProposal["severity"]?.DeepClone();
            if (!JsonNode.DeepEquals(normalized, rejected))
                return Result(MortalJointRepairRawDisposition.Rejected, capture, JointRepairAdmissionIssue("Only the packet-owned severity leaf may be resubmitted."));
            if (capture.Packet.MatchesRejectedDecision(current))
                return Result(MortalJointRepairRawDisposition.Rejected, capture, capture.RejectedIssues.Select(BindMortalQteDraftRepairTarget).ToArray());
            var scene = guards[WoundNarrativeOutputPath] is { } sceneBytes ? ReadOrdinaryTreatmentRoot(sceneBytes, "joint resubmitted scene") : null;
            if (!capture.Packet.MatchesCorrectedDecision(current, scene?["response"]?.GetValue<string>()))
                // Preserve the original source-bound packet and hold across a still
                // invalid severity attempt; never replace it with a looser wave.
                return Result(MortalJointRepairRawDisposition.Rejected, capture, capture.RejectedIssues.Select(BindMortalQteDraftRepairTarget).ToArray());
            // Packet matching limits field ownership; it does not establish the
            // actual severity range. A still-invalid bounded leaf retains the
            // FIRST packet/checkpoint instead of minting a different repair wave.
            var attempted = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(_fs, lease,
                CanonicalJsonUtf8.DecodeOneOptionalBom(draftBytes), scene!["response"]!.GetValue<string>(),
                out var replay, out var opportunity);
            if (attempted is { Success: false } && replay is null && opportunity is not null &&
                capture.Packet.MatchesOpportunity(opportunity) && attempted.Issues.Count > 0 &&
                attempted.Issues.All(issue => issue.Code == "wound_severity_above_opportunity"))
                return Result(MortalJointRepairRawDisposition.Rejected, capture, capture.RejectedIssues.Select(BindMortalQteDraftRepairTarget).ToArray());
            _ = await MortalJointRepairCheckpoint.RevalidateCorrectionAsync(_fs, lease, capture);
            return Result(MortalJointRepairRawDisposition.Owned, capture, Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or ArgumentException or JsonException)
        { return Result(MortalJointRepairRawDisposition.Rejected, null, JointRepairAdmissionIssue(error.Message)); }
    }

    private async Task<IReadOnlyList<WoundRepairRetryObligation>> CaptureMortalJointRepairRetryAsync(IReadOnlyList<WoundRepairPacket> packets)
    {
        if (_mortalJointRepairCheckpoint is not { } checkpoint || packets.Count != 1) return Array.Empty<WoundRepairRetryObligation>();
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            var capture = await ReadCurrentJointRejectedCaptureAsync(lease, checkpoint.ToJsonObject());
            if (packets[0].SemanticFingerprint != capture.Packet.SemanticFingerprint || !packets[0].MatchesOpportunity(capture.Opportunity))
                return Array.Empty<WoundRepairRetryObligation>();
            return [new WoundRepairRetryObligation(0, packets[0], null, JointCheckpoint: checkpoint)];
        }
        catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or ArgumentException or JsonException)
        { return Array.Empty<WoundRepairRetryObligation>(); }
    }
    private async Task<bool> HasExactMortalJointRepairResubmissionAsync(MortalJointRepairCheckpoint checkpoint)
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        try
        {
            var capture = await ReadCurrentJointRejectedCaptureAsync(lease, checkpoint.ToJsonObject());
            _ = await MortalJointRepairCheckpoint.RevalidateCorrectionAsync(_fs, lease, capture);
            return true;
        }
        catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or ArgumentException or JsonException)
        { return false; }
    }
    private static IReadOnlyList<ValidationIssue> JointRepairAdmissionIssue(string reason) =>
        [MortalWoundOpportunityAdapter.PrivateSourceIssue(WoundAcceptedTurnPlannerCore.NewIssue(
            "mortal_joint_repair_admission_refused", reason, "one actual same-original source-bound joint repair wave and held request", "blocked"))];
}
