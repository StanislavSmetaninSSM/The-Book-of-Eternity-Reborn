using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Detached private provenance only. A parsed checkpoint never grants reservation,
/// accepted acquisition, continuation, cache or publication authority. The live joint
/// owner must reconstruct both authorities under its current lease before using it.
/// </summary>
internal sealed class MortalJointRepairCheckpoint
{
    private const string Kind = "mortal_qte_guaranteed_severity_repair";
    private const string Domain = "book_of_eternity.mortal_joint_repair_checkpoint.v1";
    private const int MaximumBlobBytes = 2 * 1024 * 1024;
    private static readonly string[] Fields =
    [
        "schemaVersion", "kind", "sessionGeneration", "sessionId", "requestId", "snapshotToken", "turnNumber",
        "originalManifestHash", "originalAuthorityHash", "opportunityRef", "sourceOperationKey", "sourceEventRef",
        "sourceFingerprint", "opportunityFingerprint", "operationKey", "attemptId", "requestFingerprint", "commandHash",
        "rejectedDraftBase64", "rejectedDraftHash", "narrativeBase64", "narrativeHash",
        "packetFingerprints", "receiptFingerprints", "fingerprint"
    ];
    private readonly JsonObject _root;
    private MortalJointRepairCheckpoint(JsonObject root) => _root = root.DeepClone().AsObject();
    internal JsonObject ToJsonObject() => _root.DeepClone().AsObject();
    internal sealed record ReadResult(MortalJointRepairCheckpoint? Checkpoint, IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Checkpoint is not null && Issues.Count == 0;
    }

    internal static async Task<ReadResult> CaptureAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        string sessionId, string requestId, string snapshotToken, int expectedTurn)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        try
        {
            var original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
            var generation = files.ReadLocalGenerationSnapshot(lease).Binding.Id;
            if (original.Realm != "mortal_world" || original.TurnNumber != expectedTurn || original.SessionId != sessionId ||
                original.RequestId != requestId || original.SnapshotToken != snapshotToken || string.IsNullOrWhiteSpace(generation))
                return Refuse("binding", "The fresh official original and physical generation must match the current frame.");
            var commandBytes = await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
            var draftBytes = await files.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath);
            var sceneBytes = await files.ReadFileBytesAsync(lease, "output/narrative_response.json");
            if (commandBytes is null || draftBytes is null || sceneBytes is null)
                return Refuse("input_missing", "Actual homogeneous command, rejected public draft and ordinary scene are required.");
            // Initial capture alone: a previous/foreign wave is not silently overwritten.
            if (await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath) is not null)
                return Refuse("occupied_pending", "Initial joint capture cannot replace an existing private pending wave.");
            var command = Root(commandBytes, "joint treatment command");
            var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(command));
            if (!parsed.Success) return new(null, parsed.Issues);
            if (parsed.TreatmentCommands.Count != 1 || parsed.Commands.Count != 0 || parsed.AcceptedTransitionCommands.Count != 0)
                return Refuse("command_family", "One complete homogeneous treatment command is required.");
            var history = WoundHistoryState.Parse(await files.ReadFileAsync(lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
            if (!history.IsValid) return new(null, history.Issues);
            var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(JsonSerializer.SerializeToElement(command), null, history);
            if (!catalog.IsValid) return new(null, catalog.Issues);
            if (catalog.Requests.Count != 1 || catalog.HeldRequests.Count != 1 || catalog.FinalizedRequests.Count != 0)
                return Refuse("request_set", "One exact already-issued held request is required.");
            var persisted = catalog.HeldRequests[0]; var coordinate = persisted.Coordinates;
            if (persisted.Mode != "guaranteed" || coordinate.Realm != "mortal_world" || coordinate.TargetKind != "player" ||
                coordinate.TargetId != "player_current" || coordinate.SessionGeneration != generation ||
                coordinate.SessionId != sessionId || coordinate.RequestId != requestId || coordinate.SnapshotToken != snapshotToken || coordinate.Turn != expectedTurn)
                return Refuse("request_binding", "The existing player guaranteed request must bind this actual original and generation.");
            var context = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
            {
                ["schemaVersion"] = 1, ["realm"] = coordinate.Realm, ["targetKind"] = coordinate.TargetKind,
                ["targetId"] = coordinate.TargetId, ["providerKind"] = coordinate.ProviderKind,
                ["providerId"] = coordinate.ProviderId, ["currentLocationId"] = coordinate.LocationId
            }.ToJsonString(), "joint treatment context");
            if (!context.IsValid || context.Context is null) return new(null, context.Issues);
            var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(files, lease, context.Context, coordinate.WoundId);
            if (!exported.IsValid || exported.Authority is not { } accepted) return new(null, exported.Issues);
            if (accepted.SessionGeneration != generation || accepted.Binding.SessionId != sessionId || accepted.Binding.RequestId != requestId ||
                accepted.Binding.SnapshotToken != snapshotToken || accepted.Binding.Turn != expectedTurn || accepted.Binding.Realm != "mortal_world")
                return Refuse("accepted_binding", "Fresh treatment accepted-state authority must independently retain the same frame.");
            var restored = accepted.RestorePersistedTreatmentRequests(history);
            if (!restored.IsValid) return new(null, restored.Issues);
            if (restored.Requests.Count != 1 || restored.HeldRequests.Count != 1 || restored.FinalizedRequests.Count != 0 ||
                restored.HeldRequests[0].RequestFingerprint != persisted.RequestFingerprint)
                return Refuse("restored_request", "Fresh restoration must return the same complete request, not a new reservation.");
            var request = restored.HeldRequests[0];
            var resolved = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, accepted.CurrentWound, accepted);
            if (resolved.Disposition != "Resolved" || resolved.Resolution is not { } resolution) return new(null,
                resolved.Issues.Count == 0 ? Refuse("resolution", "The exact existing request must produce its guaranteed result.").Issues : resolved.Issues);
            var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
            if (!finalization.IsValid) return new(null, finalization.Issues);
            var scene = Root(sceneBytes, "joint ordinary narrative");
            var sceneText = scene["response"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(sceneText) || sceneText != parsed.TreatmentCommands[0].FinalSceneText ||
                !JsonNode.DeepEquals(command, WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(accepted.Binding, resolution, sceneText)))
                return Refuse("command_result", "The complete command/result and scene must match the fresh reducer.");
            var draft = Root(draftBytes, "rejected joint acquisition");
            if (draft["decision"]?.GetValue<string>() != "materialize")
                return Refuse("decision_kind", "Only a genuine materialize severity repair is this first contour.");
            var refusal = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(files, lease, Text(draftBytes), sceneText,
                out var replay, out var opportunity);
            if (refusal is null || refusal.Success || replay is not null || opportunity is null || opportunity.WorseningTarget is not null)
                return Refuse("rejected_source", "A real rejected CREATE from the actual QTE adapter is required.");
            var actionable = refusal.Issues.Where(issue => issue.Code == "wound_severity_above_opportunity" &&
                issue.WoundRepairContext is not null && WoundRepairPacketBuilder.IsRepairableIssue(issue)).ToArray();
            if (actionable.Length == 0 || refusal.Issues.Any(issue => issue.Code != "wound_severity_above_opportunity"))
                return Refuse("severity_only", "Private/source/other proposal failures cannot mint this severity checkpoint.");
            var packets = WoundRepairPacketBuilder.Build(actionable);
            if (packets.Count != 1 || !packets[0].MatchesOpportunity(opportunity) || !packets[0].MatchesRejectedDecision(draft))
                return Refuse("packet", "Exactly one source-bound real severity packet is required.");
            var occurrences = MortalWoundOccurrenceState.Parse(Text(original.ReadRequiredBytes(MortalWoundOccurrenceState.StatePath)), MortalWoundOccurrenceState.StatePath);
            if (!occurrences.IsValid || occurrences.State is null) return new(null, occurrences.Issues);
            if (occurrences.State.Occurrences.Count != 1 || occurrences.State.Occurrences[0].OpportunityRef != opportunity.PublicRef)
                return Refuse("occurrence_set", "Exactly one real selected occurrence is required.");
            var occurrence = occurrences.State.Occurrences[0];
            if (occurrence.AcceptedEvents.Count(value => value.EventRef == opportunity.EventRef) != 1)
                return Refuse("source_event", "The selected real opportunity event must be unique in its signed source vector.");
            var manifest = await files.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            var authority = await files.ReadFileBytesAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath);
            if (manifest is null || authority is null) return Refuse("original_files", "The actual original manifest and authority bytes are required.");
            var pending = WoundRepairPacketBuilder.ComposePendingRoot(accepted.Binding, packets, parsed);
            var receiptFingerprints = pending["repairReceipts"]!.AsArray().Select(receipt =>
                (JsonNode?)JsonValue.Create(receipt!["semanticFingerprint"]!.GetValue<string>())).ToArray();
            var root = new JsonObject
            {
                ["schemaVersion"] = 1, ["kind"] = Kind, ["sessionGeneration"] = generation,
                ["sessionId"] = sessionId, ["requestId"] = requestId, ["snapshotToken"] = snapshotToken, ["turnNumber"] = expectedTurn,
                ["originalManifestHash"] = Hash(manifest), ["originalAuthorityHash"] = Hash(authority),
                ["opportunityRef"] = opportunity.PublicRef, ["sourceOperationKey"] = occurrence.ProducerOperationKey,
                ["sourceEventRef"] = opportunity.EventRef,
                ["sourceFingerprint"] = MortalWoundOccurrenceState.ComputeOccurrenceFingerprint(occurrence),
                ["opportunityFingerprint"] = WoundOpportunityAuthority.RecomputeAuthorityFingerprint(opportunity),
                ["operationKey"] = coordinate.OperationKey, ["attemptId"] = coordinate.AttemptId, ["requestFingerprint"] = request.RequestFingerprint,
                ["commandHash"] = Hash(commandBytes), ["rejectedDraftBase64"] = Convert.ToBase64String(draftBytes), ["rejectedDraftHash"] = Hash(draftBytes),
                ["narrativeBase64"] = Convert.ToBase64String(sceneBytes), ["narrativeHash"] = Hash(sceneBytes),
                ["packetFingerprints"] = new JsonArray(packets.Select(packet => (JsonNode?)JsonValue.Create(packet.SemanticFingerprint)).ToArray()),
                ["receiptFingerprints"] = new JsonArray(receiptFingerprints)
            };
            root["fingerprint"] = Fingerprint(JsonSerializer.SerializeToElement(root));
            return Parse(JsonSerializer.SerializeToElement(root));
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or DecoderFallbackException or FormatException or InvalidOperationException)
        {
            return Refuse("input_invalid", error.Message);
        }
    }

    internal static ReadResult Parse(JsonElement root)
    {
        try
        {
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(Fields.Order(StringComparer.Ordinal)) is false)
                return Refuse("shape", "One complete closed version-1 checkpoint with unique fields is required.");
            if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("kind").GetString() != Kind || root.GetProperty("turnNumber").GetInt32() < 0)
                return Refuse("version", "Unsupported checkpoint version, kind or turn.");
            foreach (var name in Fields.Except(["schemaVersion", "turnNumber", "packetFingerprints", "receiptFingerprints"]))
            {
                var value = root.GetProperty(name).GetString();
                if (string.IsNullOrWhiteSpace(value) || (name.EndsWith("Hash", StringComparison.Ordinal) || name.EndsWith("Fingerprint", StringComparison.Ordinal) || name == "fingerprint") &&
                    !ResourceMaterializationContract.IsAuthorityFingerprint(value)) return Refuse("field", "Invalid checkpoint field: " + name);
            }
            foreach (var name in new[] { "packetFingerprints", "receiptFingerprints" })
            {
                var values = root.GetProperty(name);
                if (values.ValueKind != JsonValueKind.Array || values.GetArrayLength() != 1 ||
                    !ResourceMaterializationContract.IsAuthorityFingerprint(values[0].GetString())) return Refuse("wave", "Exactly one packet/receipt fingerprint is required.");
            }
            foreach (var (blob, hash) in new[] { ("rejectedDraftBase64", "rejectedDraftHash"), ("narrativeBase64", "narrativeHash") })
            {
                var encoded = root.GetProperty(blob).GetString()!;
                if (encoded.Length > MaximumBlobBytes * 2) return Refuse("blob", "Checkpoint blob limit exceeded.");
                var bytes = Convert.FromBase64String(encoded);
                if (bytes.Length is 0 or > MaximumBlobBytes || Hash(bytes) != root.GetProperty(hash).GetString()) return Refuse("blob", "Retained blob hash does not match its exact bytes.");
                _ = Root(bytes, blob);
            }
            if (Fingerprint(root) != root.GetProperty("fingerprint").GetString()) return Refuse("fingerprint", "The closed checkpoint payload fingerprint does not match.");
            return new(new MortalJointRepairCheckpoint(JsonNode.Parse(root.GetRawText())!.AsObject()), Array.Empty<ValidationIssue>());
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or FormatException or InvalidOperationException or DecoderFallbackException)
        {
            return Refuse("shape", error.Message);
        }
    }
    private static string Fingerprint(JsonElement root)
    {
        var values = new List<string?> { Domain };
        foreach (var name in Fields.Where(name => name != "fingerprint"))
        {
            var value = root.GetProperty(name); values.Add(name);
            if (value.ValueKind == JsonValueKind.Array) values.AddRange(value.EnumerateArray().Select(item => item.GetString()));
            else values.Add(value.ValueKind == JsonValueKind.Number ? value.GetInt32().ToString(CultureInfo.InvariantCulture) : value.GetString());
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(values);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Text(byte[] bytes) => new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
    private static JsonObject Root(byte[] bytes, string label) => StrictJsonAuthority.Deserialize<JsonObject>(Text(bytes),
        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, label) ?? throw new InvalidDataException(label + " must be an object.");
    private static ReadResult Refuse(string code, string message) => new(null, [MortalWoundOpportunityAdapter.PrivateSourceIssue(new ValidationIssue(
        WoundAcceptedTurnSnapshotContract.PendingResolutionPath, IssueSeverity.Error, message, "mortal_joint_checkpoint_" + code,
        actor: "Client", section: "wound_materialization"))]);
}
