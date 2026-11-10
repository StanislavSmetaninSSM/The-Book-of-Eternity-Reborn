using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Called only by the owner-admitted resource seam, with the actual validation frame
    // and its participating lease. Persisted command graphs select sources; they do not
    // replace a fresh accepted-state export or grant result/publication authority.
    private async Task<IReadOnlyList<ValidationIssue>?> PrepareOrdinaryMortalGuaranteedTreatmentAsync(
        FileSystemManager.CanonicalWriteLease lease, ValidatedPendingTurnSnapshotContext frame, int expectedTurn)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        var commandBytes = await _fs.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        if (commandBytes is null) return null;
        try
        {
            var command = ReadOrdinaryTreatmentRoot(commandBytes, "ordinary treatment command");
            var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(command));
            if (!parsed.Success) return parsed.Issues;
            if (parsed.TreatmentCommands.Count == 0) return null;
            if (parsed.TreatmentCommands.Count != 1 || parsed.Commands.Count != 0 || parsed.AcceptedTransitionCommands.Count != 0)
                return OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_family_unsupported",
                    "The ordinary owner requires one homogeneous guaranteed held treatment command.");

            var originalRead = PendingTurnSnapshotReader.ReadCurrent(_fs, lease,
                new[] { "game_state/meta/soul_state.json", ResourceMaterializationContract.DefinitionsPath,
                    ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                    WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath });
            if (!originalRead.Success || originalRead.Snapshot is not { } original) return originalRead.Issues;
            if (original.Realm != "mortal_world" || original.SessionId != frame.SessionId || original.RequestId != frame.RequestId ||
                original.SnapshotToken != frame.Payload.ManifestPayloadHash || original.TurnNumber != expectedTurn || frame.TurnNumber != expectedTurn)
                return OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_frame_mismatch",
                    "The fresh official original must match this actual Mortal validation frame and expected turn.");

            var history = WoundHistoryState.Parse(await _fs.ReadFileAsync(lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
            if (!history.IsValid) return history.Issues;
            var pendingBytes = await _fs.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            var pending = pendingBytes is null ? null : ReadOrdinaryTreatmentRoot(pendingBytes, "ordinary treatment pending requests");
            var persisted = MortalWoundTreatmentPersistedRequestCatalog.Parse(JsonSerializer.SerializeToElement(command),
                pending is null ? null : JsonSerializer.SerializeToElement(pending), history);
            if (!persisted.IsValid) return persisted.Issues;
            if (persisted.Requests.Count != 1 || persisted.HeldRequests.Count != 1 || persisted.FinalizedRequests.Count != 0)
                return OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_request_set_unsupported",
                    "One exact held request is required; finalized requests remain with their existing replay owner.");
            var coordinate = persisted.HeldRequests[0].Coordinates;
            if (persisted.HeldRequests[0].Mode != "guaranteed" || coordinate.SessionId != original.SessionId ||
                coordinate.RequestId != original.RequestId || coordinate.SnapshotToken != original.SnapshotToken ||
                coordinate.Turn != expectedTurn || coordinate.Realm != original.Realm ||
                coordinate.SessionGeneration != _fs.GetOrCreateSessionGeneration(lease))
                return OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_request_binding_invalid",
                    "Only the current generation's exact original-bound guaranteed held request is supported.");

            var mechanical = await ReadOrdinaryGuaranteedTreatmentMechanicalEnvelopeAsync(lease, original);
            if (mechanical.Response is null) return mechanical.Issues;

            var context = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
            {
                ["schemaVersion"] = 1, ["realm"] = coordinate.Realm,
                ["targetKind"] = coordinate.TargetKind, ["targetId"] = coordinate.TargetId,
                ["providerKind"] = coordinate.ProviderKind, ["providerId"] = coordinate.ProviderId,
                ["currentLocationId"] = coordinate.LocationId
            }.ToJsonString(), AcceptedMechanicsPlan.WoundCommandPath + ".commands[0].authority.request.coordinates");
            if (!context.IsValid || context.Context is null) return context.Issues;
            var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(_fs, lease, context.Context, coordinate.WoundId);
            if (!exported.IsValid || exported.Authority is not { } acceptedState) return exported.Issues;
            if (acceptedState.Binding.SessionId != original.SessionId || acceptedState.Binding.RequestId != original.RequestId ||
                acceptedState.Binding.SnapshotToken != original.SnapshotToken || acceptedState.Binding.Turn != expectedTurn ||
                acceptedState.Binding.Realm != original.Realm || acceptedState.SessionGeneration != coordinate.SessionGeneration)
                return OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_export_binding_invalid",
                    "The fresh current-world export must retain the exact official original and physical generation.");
            var restored = acceptedState.RestorePersistedTreatmentRequests(history);
            if (!restored.IsValid) return restored.Issues;
            if (restored.HeldRequests.Count != 1 || restored.Requests.Count != 1 || restored.FinalizedRequests.Count != 0 ||
                restored.HeldRequests[0].RequestFingerprint != persisted.HeldRequests[0].RequestFingerprint)
                return OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_restored_request_mismatch",
                    "The registry must restore precisely the complete persisted held request without a new reservation identity.");
            var request = restored.HeldRequests[0];
            var resolved = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, acceptedState.CurrentWound, acceptedState);
            if (resolved.Disposition != "Resolved" || resolved.Resolution is not { } resolution)
                return resolved.Issues.Count != 0 ? resolved.Issues : OrdinaryTreatmentRefusal(
                    "mortal_wound_treatment_ordinary_resolution_unavailable", "The fresh guaranteed reducer did not produce the exact held result.");
            var sceneBytes = await _fs.ReadFileBytesAsync(lease, WoundNarrativeOutputPath);
            if (sceneBytes is null) return OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_scene_missing", "The actual ordinary narrative output is required.");
            var scene = ReadOrdinaryTreatmentRoot(sceneBytes, "ordinary treatment narrative");
            if (scene["response"] is not JsonValue responseValue || !responseValue.TryGetValue<string>(out var sceneText) ||
                string.IsNullOrWhiteSpace(sceneText) || sceneText != parsed.TreatmentCommands[0].FinalSceneText)
                return OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_scene_mismatch", "The actual ordinary scene must exactly match the staged treatment result.");
            var reconstructed = WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(acceptedState.Binding, resolution, sceneText);
            if (!JsonNode.DeepEquals(command, reconstructed))
                return OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_result_mismatch", "The full staged request and resolved result must match the fresh owned reducer result.");

            var publication = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(
                _fs, lease, mechanical.Response, acceptedState, request, resolution);
            return publication.IsValid ? Array.Empty<ValidationIssue>() : publication.Issues;
        }
        catch (Exception failure) when (failure is JsonException or InvalidDataException or DecoderFallbackException)
        {
            return OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_input_invalid", failure.Message);
        }
    }

    private static JsonObject ReadOrdinaryTreatmentRoot(byte[] bytes, string label) =>
        StrictJsonAuthority.Deserialize<JsonObject>(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, label) ?? throw new InvalidDataException(label + " must be an object.");

    private async Task<(GameResponse? Response, IReadOnlyList<ValidationIssue> Issues)>
        ReadOrdinaryGuaranteedTreatmentMechanicalEnvelopeAsync(FileSystemManager.CanonicalWriteLease lease,
            PendingTurnSnapshotReadAuthority binding)
    {
        var paths = AcceptedMechanicsWoundCommonInputComposer.TreatmentOrdinaryGuardPaths;
        var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, new PendingTurnSnapshotPathSelection(
            new[] { "game_state/meta/soul_state.json" }, paths.Where(static path => path != "game_state/meta/soul_state.json")));
        if (!read.Success || read.Snapshot is not { } original) return (null, read.Issues);
        if (original.SessionId != binding.SessionId || original.RequestId != binding.RequestId ||
            original.SnapshotToken != binding.SnapshotToken || original.TurnNumber != binding.TurnNumber || original.Realm != binding.Realm)
            return (null, OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_guard_binding_invalid", "The mechanical read must retain the actual official original binding."));
        foreach (var path in paths)
        {
            var current = await _fs.ReadFileBytesAsync(lease, path);
            // Its effective resource outcome is checked by the existing owner intake.
            // Ordinary guardian narrative markers still follow normal canonical cleanup;
            // the complete current root is nevertheless captured in the common fence.
            if (path == "game_state/meta/guardians.json") continue;
            // The publisher has no ordinary raw resource/effect command importer. Even
            // a present empty/null/unknown command file is not this absence-only contour.
            if (path == ResourceMaterializationContract.CommandPath || path == EffectAcceptedTurnPlan.CommandPath || path == MortalWoundOpportunityAdapter.DecisionPath)
            {
                if (current is not null)
                    return (null, OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_concurrent_work_unsupported", "Concurrent raw resource/effect/QTE decision work must remain with its ordinary owner: " + path));
                continue;
            }
            if (!original.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal))
            {
                if (current is not null)
                    return (null, OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_uncovered_input_present", "A current mechanical input has no selected original baseline: " + path));
                // Uncovered optional absence is NOT source authority and creates no
                // pristine canonical value. This only proves no current concurrent input;
                // observed wound absence/rollback coverage is separately required by intake.
                continue;
            }
            if (current is null || !JsonNode.DeepEquals(ReadOrdinaryTreatmentNode(original.ReadRequiredBytes(path), path),
                    ReadOrdinaryTreatmentNode(current, path)))
                return (null, OrdinaryTreatmentRefusal("mortal_wound_treatment_ordinary_mechanical_input_changed", "This first contour cannot carry a changed mechanical source/afterimage: " + path));
        }
        // This is the proven EMPTY concurrent command envelope, not a stripped display
        // response. Actual narrative is separately exact-bound; existing canonical periodic
        // effects still run once inside the existing treatment publication's effect stage.
        return (new GameResponse(), Array.Empty<ValidationIssue>());
    }

    private static JsonNode ReadOrdinaryTreatmentNode(byte[] bytes, string path) =>
        StrictJsonAuthority.Deserialize<JsonNode>(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, path) ?? throw new InvalidDataException("Present mechanical JSON null is invalid: " + path);

    private static IReadOnlyList<ValidationIssue> OrdinaryTreatmentRefusal(string code, string message) =>
        new[] { new ValidationIssue(AcceptedMechanicsPlan.WoundCommandPath, IssueSeverity.Error, message, code,
            actor: "Client", section: "wound_materialization") };
}
