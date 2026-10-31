using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentAcceptedStateAuthority
{
    // Shares the existing authenticated original and retained-attempt owner.
    // Historical diagnosis field names do not broaden either codec's purpose.
    /// <summary>Copies the actual immutable physical selection admitted by this owner.</summary>
    internal JsonObject ReadGuidedTreatmentOriginalInput() => HasOriginalGuidedTreatmentInput
        ? ReadDiagnosisOriginalInput() : throw new InvalidDataException("No actual guided physical original.");

    /// <summary>Refuses competing or stale pending ownership before any fresh attempt allocation.</summary>
    internal CanonicalBeforeImage RequireUnheldGuidedPendingImage()
    {
        if (!HasOriginalGuidedTreatmentInput || !HasCurrentAdmissionAuthority() || _diagnosisOriginalEvidence is null || _diagnosisIssuedSnapshot is null)
            throw new InvalidDataException("Physical allocation has no actual current guided original.");
        _diagnosisOriginalEvidence.RequireOwner(_fileSystem, _writeLease, _diagnosisIssuedSnapshot);
        _diagnosisOriginalEvidence.RequireCurrent(_fileSystem, _writeLease, _diagnosisIssuedSnapshot);
        var command = _fileSystem.ReadFileBytesSync(_writeLease, AcceptedMechanicsPlan.WoundCommandPath);
        if (command is null || _diagnosisCommandBytes is null || !command.AsSpan().SequenceEqual(_diagnosisCommandBytes))
            throw new InvalidDataException("Physical selection changed before allocation.");
        var original = ReadDiagnosisOriginalPending();
        var current = _fileSystem.ReadFileBytesSync(_writeLease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        if (original.Bytes is { } expected ? current is null || !expected.AsSpan().SequenceEqual(current) : current is not null)
            throw new InvalidDataException("An unowned physical pending image changed before allocation.");
        if (current is not null)
        {
            var root = StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(current),
                new JsonSerializerOptions(), "original empty pending") ?? throw new InvalidDataException("Pending root is not an object.");
            var fields = new[] { "schemaVersion", "sessionId", "requestId", "snapshotToken", "repairPackets", "repairReceipts" };
            if (root.Count != fields.Length || fields.Any(field => !root.ContainsKey(field)) ||
                root["schemaVersion"]?.GetValue<int>() != 1 || root["sessionId"]?.GetValue<string>() != Binding.SessionId ||
                root["requestId"]?.GetValue<string>() != Binding.RequestId || root["snapshotToken"]?.GetValue<string>() != Binding.SnapshotToken ||
                root["repairPackets"] is not JsonArray { Count: 0 } || root["repairReceipts"] is not JsonArray { Count: 0 })
                throw new InvalidDataException("Another pending owner must settle before a guided physical attempt.");
        }
        return new(current is not null, current);
    }

    /// <summary>Reads an exact recovered physical request without creating reservations.</summary>
    internal MortalWoundTreatmentAttemptRequest? ReadHeldGuidedTreatmentRequest(MortalWoundTreatmentAttemptCoordinates coordinates)
    {
        if (!HasOriginalGuidedTreatmentInput) throw new InvalidDataException("Not a guided physical owner.");
        var request = AcceptedTurnAuthorityRegistry.ReadHeldMortalDiagnosis(_fileSystem, _writeLease, this, coordinates);
        if (request is not null) RequireOriginalGuidedTreatmentRequest(request);
        return request;
    }

    /// <summary>
    /// Publishes a distinct exact guided hold and only then confirms its live
    /// claims. Submitted requests and genuine repair waves keep their old contract.
    /// </summary>
    internal async Task RetainGuidedTreatmentRequestAsync(MortalWoundTreatmentAttemptRequest request)
    {
        RequireOriginalGuidedTreatmentRequest(request);
        if (!HasCurrentAdmissionAuthority() || !request.Coordinates.MatchesAcceptedState(this) ||
            !MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
            request.ModeAuthority is MortalWoundProcedureCheckAuthority check && !HasLiveProcedureReservationAgreement(check))
            throw new InvalidDataException("Only the current physical owner may retain its complete actual mode claims.");
        var path = WoundAcceptedTurnSnapshotContract.PendingResolutionPath;
        var before = await _fileSystem.ReadFileBytesAsync(_writeLease, path);
        if (!AcceptedTurnAuthorityRegistry.DiagnosisPendingBeforeAgrees(_fileSystem, _writeLease, this,
                DiagnosisRetentionCapability, request, before))
            throw new InvalidDataException("The physical hold cannot replace another pending owner.");
        var root = before is null ? new JsonObject
        {
            ["schemaVersion"] = 1, ["sessionId"] = _binding.SessionId, ["requestId"] = _binding.RequestId,
            ["snapshotToken"] = _binding.SnapshotToken, ["repairPackets"] = new JsonArray(), ["repairReceipts"] = new JsonArray()
        } : StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(before),
            new JsonSerializerOptions(), "guided pending hold") ?? throw new InvalidDataException("Pending physical hold is not an object.");
        if (root.Any(pair => pair.Key is not ("schemaVersion" or "sessionId" or "requestId" or "snapshotToken" or
                "repairPackets" or "repairReceipts" or "guidedTreatmentRequest")) ||
            root["repairPackets"] is not JsonArray { Count: 0 } || root["repairReceipts"] is not JsonArray { Count: 0 })
            throw new InvalidDataException("Initial guided retention cannot invent or overwrite an ordinary repair/submission owner.");
        var row = new JsonObject
        {
            ["kind"] = "guided_treatment_hold", ["inputFingerprint"] = MortalWoundGuidedTreatmentInputCodec.Fingerprint(_diagnosisInput!),
            ["operationKey"] = request.Coordinates.OperationKey, ["attemptId"] = request.Coordinates.AttemptId,
            ["requestFingerprint"] = request.RequestFingerprint,
            ["request"] = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request)
        };
        if (root["guidedTreatmentRequest"] is { } existing && !JsonNode.DeepEquals(existing, row))
            throw new InvalidDataException("A physical held request cannot be replaced by another draw, mode or agreement.");
        root["guidedTreatmentRequest"] = row;
        using var changed = JsonDocument.Parse(root.ToJsonString());
        var catalog = MortalWoundTreatmentPersistedRequestCatalog.ParseWithGuidedOriginal(
            JsonSerializer.SerializeToElement(_diagnosisInput), changed.RootElement,
            new(_history, Array.Empty<ValidationIssue>()), _diagnosisInput);
        if (!catalog.IsValid || catalog.HeldRequests.Count != 1 || catalog.HeldRequests[0].RequestFingerprint != request.RequestFingerprint)
            throw new InvalidDataException("The complete physical hold does not reconstruct its exact original request.");
        var bytes = FileSystemManager.EncodeUtf8WithPreamble(root.ToJsonString());
        RequireOriginalGuidedTreatmentRequest(request);
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(_fileSystem, _writeLease,
            new CoordinatedStateWriteHelper.PlannedWrite(path, null, root.ToJsonString(), RequireCurrentBaseline: true,
                ExactPrevious: new(before is not null, before))))
            throw new InvalidDataException("Physical pending before-image changed before retention.");
        var readback = await _fileSystem.ReadFileBytesAsync(_writeLease, path);
        if (readback is null || !readback.AsSpan().SequenceEqual(bytes))
            throw new CoordinatedStatePublicationUncertainException(new InvalidDataException("Physical hold readback differs."));
        RequireOriginalGuidedTreatmentRequest(request);
        if (!AcceptedTurnAuthorityRegistry.RegisterHeldMortalDiagnosis(_fileSystem, _writeLease, this,
            DiagnosisRetentionCapability, request, readback))
            throw new InvalidDataException("The durable physical hold cannot yet confirm its exact live claims; preserve it for recovery.");
    }

    /// <summary>Proves the signed-input role under the actual owner, frame, request and current issued evidence.</summary>
    internal bool HasCurrentGuidedTreatmentPreparedCommandRole(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        MortalWoundTreatmentAttemptRequest request, string sessionId, string requestId, int turn,
        string manifestHash, string computedManifestHash)
    {
        RequireOriginalGuidedTreatmentRequest(request);
        var role = HasCurrentGuidedTreatmentInputRole(files, lease, sessionId, requestId, turn, manifestHash, computedManifestHash);
        if (ReadHeldGuidedTreatmentRequest(request.Coordinates) is not { } held || held.RequestFingerprint != request.RequestFingerprint)
            throw new InvalidDataException("Physical prepared role lost its exact private hold.");
        return role;
    }

    /// <summary>Authenticates only the prepared input role before claims; it never creates a hold.</summary>
    internal bool HasCurrentGuidedTreatmentInputRole(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        string sessionId, string requestId, int turn, string manifestHash, string computedManifestHash)
    {
        if (!IsLeaseBoundTo(files, lease) || !HasCurrentAdmissionAuthority() || _diagnosisIssuedSnapshot is null ||
            !HasOriginalGuidedTreatmentInput || _diagnosisOriginalEvidence is null || _binding.SessionId != sessionId || _binding.RequestId != requestId ||
            _binding.Turn != turn || _binding.SnapshotToken != manifestHash || manifestHash != computedManifestHash ||
            _diagnosisIssuedSnapshot.SnapshotToken != manifestHash)
            throw new InvalidDataException("Physical prepared role requires its exact live original and frame.");
        _diagnosisOriginalEvidence.RequireCurrent(files, lease, _diagnosisIssuedSnapshot);
        var command = files.ReadFileBytesSync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        if (_diagnosisCommandBytes is null || command is null || !command.AsSpan().SequenceEqual(_diagnosisCommandBytes))
            throw new InvalidDataException("Physical prepared role lost its signed current command bytes.");
        return _diagnosisOriginalEvidence.HasExactPreparedWoundCommandRole();
    }
}
