using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    private async Task<IReadOnlyList<ValidationIssue>> PrepareOrdinaryMortalGuidedTreatmentAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame, int expectedTurn,
        WoundApplicationService.MortalTreatmentAdmission admission)
    {
        try
        {
            var owner = admission.Owner; var request = admission.Request;
            if (frame.ProgressionControl is null || !owner.IsLeaseBoundTo(_fs, lease) ||
                owner.Binding.SessionId != frame.SessionId || owner.Binding.RequestId != frame.RequestId ||
                owner.Binding.Turn != expectedTurn || frame.TurnNumber != expectedTurn ||
                owner.Binding.SnapshotToken != frame.Payload.ManifestPayloadHash ||
                !MortalWoundGuidedTreatmentInputCodec.IsBound(owner.ReadGuidedTreatmentOriginalInput(),
                    new(frame.SessionId, frame.RequestId, expectedTurn, frame.ProgressionControl)))
                throw new InvalidDataException("Physical treatment differs from its full actual Engine original.");
            owner.RequireOriginalGuidedTreatmentRequest(request);
            var original = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, new[] { "game_state/meta/soul_state.json" });
            if (!original.Success || original.Snapshot is not { } snapshot) return original.Issues;
            var mechanical = await ReadOrdinaryGuaranteedTreatmentMechanicalEnvelopeAsync(lease, snapshot);
            if (mechanical.Response is null) return mechanical.Issues;
            var sceneBytes = await _fs.ReadFileBytesAsync(lease, WoundNarrativeOutputPath) ??
                throw new InvalidDataException("Physical treatment requires the actual ordinary narrative.");
            var scene = ReadOrdinaryTreatmentRoot(sceneBytes, "physical treatment narrative");
            if (scene["response"] is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException("Physical treatment narrative is empty.");
            var history = new WoundHistoryParseResult(owner.History, Array.Empty<ValidationIssue>());
            var resolved = request.Mode switch
            {
                "procedure" => MortalWoundTreatmentPlanner.CreateProcedureAttempt(request, history, owner.CurrentWound, owner),
                "course" => MortalWoundTreatmentPlanner.CreateCourseMilestoneAttempt(request, history, owner.CurrentWound, owner),
                "guaranteed" => MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, owner.CurrentWound, owner),
                _ => throw new InvalidDataException("Unsupported retained physical mode.")
            };
            if (resolved.Disposition != "Resolved" || resolved.Resolution is not { } resolution)
                return resolved.Issues.Count != 0 ? resolved.Issues : OrdinaryTreatmentRefusal(
                    "mortal_guided_treatment_resolution_unavailable", "The exact retained physical attempt cannot resolve.");
            var publication = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(_fs, lease,
                mechanical.Response, owner, request, resolution);
            return publication.IsValid ? Array.Empty<ValidationIssue>() : publication.Issues;
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (InvalidDataException or InvalidOperationException or ArgumentException or JsonException or OverflowException))
        { return OrdinaryTreatmentRefusal("mortal_guided_treatment_ordinary_input_invalid", failure.Message); }
    }
}
