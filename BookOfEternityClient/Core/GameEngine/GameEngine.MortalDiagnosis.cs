using System.Text.Json;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private async Task<IReadOnlyList<ValidationIssue>> PrepareOrdinaryMortalDiagnosisAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame, int expectedTurn,
        WoundApplicationService.MortalDiagnosisAdmission admission)
    {
        try
        {
            var owner = admission.Owner; var request = admission.Request;
            if (frame.ProgressionControl is null || !owner.IsLeaseBoundTo(_fs, lease) ||
                owner.Binding.SessionId != frame.SessionId || owner.Binding.RequestId != frame.RequestId ||
                owner.Binding.Turn != expectedTurn || frame.TurnNumber != expectedTurn ||
                owner.Binding.SnapshotToken != frame.Payload.ManifestPayloadHash ||
                !MortalWoundDiagnosisInputCodec.IsBound(owner.ReadDiagnosisOriginalInput(),
                    new(frame.SessionId, frame.RequestId, expectedTurn, frame.ProgressionControl)))
                throw new InvalidDataException("Diagnosis preparation differs from its actual complete Engine validation frame.");
            owner.RequireOriginalDiagnosisRequest(request);
            var original = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, new[] { "game_state/meta/soul_state.json" });
            if (!original.Success || original.Snapshot is not { } snapshot) return original.Issues;
            var mechanical = await ReadOrdinaryGuaranteedTreatmentMechanicalEnvelopeAsync(lease, snapshot);
            if (mechanical.Response is null) return mechanical.Issues;
            var scene = await _fs.ReadFileBytesAsync(lease, WoundNarrativeOutputPath) ??
                throw new InvalidDataException("Diagnosis requires the actual ordinary narrative response.");
            var narrative = ReadOrdinaryTreatmentRoot(scene, "diagnosis narrative");
            if (narrative["response"] is not System.Text.Json.Nodes.JsonValue value ||
                !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException("Diagnosis ordinary narrative response is empty.");
            var publication = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(_fs, lease,
                mechanical.Response, owner, request);
            return publication.IsValid ? Array.Empty<ValidationIssue>() : publication.Issues;
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (InvalidDataException or InvalidOperationException or ArgumentException or JsonException or OverflowException))
        {
            return OrdinaryTreatmentRefusal("mortal_diagnosis_ordinary_input_invalid", failure.Message);
        }
    }
}
