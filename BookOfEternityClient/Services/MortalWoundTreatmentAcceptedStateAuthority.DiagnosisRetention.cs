using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentAcceptedStateAuthority
{
    private static readonly object DiagnosisRetentionCapability = new();
    internal static bool IsDiagnosisRetentionCapability(object capability) => ReferenceEquals(capability, DiagnosisRetentionCapability);

    // The existing pending treatment DATA channel receives a real held diagnosis
    // request, with no fabricated repair packet. The signed selection is immutable.
    internal async Task RetainDiagnosisRequestAsync(MortalWoundTreatmentAttemptRequest request)
    {
        RequireOriginalDiagnosisRequest(request);
        if (!HasCurrentAdmissionAuthority() || !request.Coordinates.MatchesAcceptedState(this) ||
            !MortalWoundTreatmentDetachedSealValidator.IsValid(request) ||
            request.ModeAuthority is not MortalWoundProcedureCheckAuthority check || !HasLiveProcedureReservationAgreement(check))
            throw new InvalidDataException("Only the actual resolving owner may retain its exact live diagnosis claims.");
        var path = WoundAcceptedTurnSnapshotContract.PendingResolutionPath;
        var bytes = await _fileSystem.ReadFileBytesAsync(_writeLease, path);
        var root = bytes is null ? new JsonObject
        {
            ["schemaVersion"] = 1, ["sessionId"] = _binding.SessionId, ["requestId"] = _binding.RequestId,
            ["snapshotToken"] = _binding.SnapshotToken, ["repairPackets"] = new JsonArray(), ["repairReceipts"] = new JsonArray()
        } : JsonNode.Parse(FileSystemManager.DecodeFileBytes(bytes))?.AsObject()
            ?? throw new InvalidDataException("The existing pending wound owner is unreadable.");
        if (root.Count is < 6 or > 7 || root["schemaVersion"]?.GetValue<int>() != 1 ||
            root["sessionId"]?.GetValue<string>() != _binding.SessionId || root["requestId"]?.GetValue<string>() != _binding.RequestId ||
            root["snapshotToken"]?.GetValue<string>() != _binding.SnapshotToken ||
            root["repairPackets"] is not JsonArray { Count: 0 } || root["repairReceipts"] is not JsonArray { Count: 0 } ||
            root.Any(value => value.Key is not ("schemaVersion" or "sessionId" or "requestId" or "snapshotToken" or
                "repairPackets" or "repairReceipts" or "submittedTreatmentRequests")))
            throw new InvalidDataException("Diagnosis hold cannot overwrite another pending repair/publication owner.");
        var row = new JsonObject
        {
            ["operationKey"] = request.Coordinates.OperationKey, ["attemptId"] = request.Coordinates.AttemptId,
            ["requestFingerprint"] = request.RequestFingerprint,
            ["request"] = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request)
        };
        if (root["submittedTreatmentRequests"] is JsonArray previous)
        {
            if (previous.Count != 1 || !JsonNode.DeepEquals(previous[0], row))
                throw new InvalidDataException("The pending diagnosis hold already belongs to a different exact request.");
        }
        else root["submittedTreatmentRequests"] = new JsonArray(row);
        var changed = JsonSerializer.SerializeToElement(root);
        var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(JsonSerializer.SerializeToElement(_diagnosisInput), changed,
            new WoundHistoryParseResult(_history, Array.Empty<ValidationIssue>()));
        if (!catalog.IsValid || catalog.HeldRequests.Count != 1 || catalog.HeldRequests[0].RequestFingerprint != request.RequestFingerprint)
            throw new InvalidDataException("The complete pending diagnosis hold does not reconstruct its original request.");
        RequireOriginalDiagnosisRequest(request);
        var targetBytes = FileSystemManager.EncodeUtf8WithPreamble(root.ToJsonString());
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(_fileSystem, _writeLease,
            new CoordinatedStateWriteHelper.PlannedWrite(path, null, root.ToJsonString(), RequireCurrentBaseline: true,
                ExactPrevious: new CanonicalBeforeImage(bytes is not null, bytes))))
            throw new InvalidDataException("The pending diagnosis hold changed during exact retention.");
        var readback = await _fileSystem.ReadFileBytesAsync(_writeLease, path);
        if (readback is null || !readback.AsSpan().SequenceEqual(targetBytes))
            throw new CoordinatedStatePublicationUncertainException(new InvalidDataException("The pending diagnosis hold did not preserve its complete exact after-image."));
        RequireOriginalDiagnosisRequest(request);
        if (!AcceptedTurnAuthorityRegistry.RegisterHeldMortalDiagnosis(_fileSystem, _writeLease, this,
                DiagnosisRetentionCapability, request))
            throw new InvalidDataException("Persisted diagnosis could not confirm its original exact private claims.");
    }

    internal MortalWoundTreatmentAttemptRequest? ReadHeldDiagnosisRequest(MortalWoundTreatmentAttemptCoordinates coordinates)
    {
        var request = AcceptedTurnAuthorityRegistry.ReadHeldMortalDiagnosis(_fileSystem, _writeLease, this, coordinates);
        if (request is not null) RequireOriginalDiagnosisRequest(request);
        return request;
    }

    internal (WoundAcceptedTurnInput Input, IReadOnlyDictionary<string, JsonNode?> SourceRoots) ReadDiagnosisPublicationBaselines(
        MortalWoundTreatmentAttemptRequest request)
    {
        RequireOriginalDiagnosisRequest(request);
        var read = WoundAcceptedTurnPlanner.ReadRecoveryPublicationBaselines(_fileSystem, _writeLease, _binding);
        if (read.Input is null || read.SourceRoots is null || read.Issues.Count != 0)
            throw new InvalidDataException("Diagnosis publication requires complete current canonical wound/effect baselines.");
        RequireOriginalDiagnosisRequest(request);
        return (read.Input, read.SourceRoots);
    }
}
