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
    internal bool UsesInterfacePublication => _root["schemaVersion"]!.GetValue<int>() == 2;
    private static readonly object CaptureIssuer = new();
    // Lease-local reconstruction proof for REJECTED provenance only, never accepted common authority.
    internal sealed class RejectedCapture
    {
        private readonly FileSystemManager _files;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly byte[] _commandBytes;
        private readonly JsonObject _pending;
        private readonly ValidationIssue[] _issues;
        internal RejectedCapture(object issuer, FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            MortalJointRepairCheckpoint checkpoint, PendingTurnSnapshotReadAuthority original,
            MortalWoundTreatmentAcceptedStateAuthority accepted, MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResolution resolution, WoundOpportunityAuthority opportunity, WoundRepairPacket packet,
            byte[] commandBytes, JsonObject pending, IReadOnlyList<ValidationIssue> issues)
        {
            if (!ReferenceEquals(issuer, CaptureIssuer)) throw new InvalidOperationException("Joint rejected proof requires its private issuer.");
            _files = files; _lease = lease; Checkpoint = checkpoint; Original = original; Accepted = accepted;
            Request = request; Resolution = resolution; Opportunity = WoundAcceptedTurnData.CloneOpportunity(opportunity); Packet = packet;
            _commandBytes = commandBytes.ToArray(); _pending = pending.DeepClone().AsObject();
            _issues = issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray();
        }
        internal MortalJointRepairCheckpoint Checkpoint { get; }
        internal PendingTurnSnapshotReadAuthority Original { get; }
        internal MortalWoundTreatmentAcceptedStateAuthority Accepted { get; }
        internal MortalWoundTreatmentAttemptRequest Request { get; }
        internal MortalWoundTreatmentResolution Resolution { get; }
        internal WoundOpportunityAuthority Opportunity { get; }
        internal WoundRepairPacket Packet { get; }
        internal byte[] CommandBytes => _commandBytes.ToArray();
        internal JsonObject Pending => _pending.DeepClone().AsObject();
        internal IReadOnlyList<ValidationIssue> RejectedIssues => _issues.Select(WoundAcceptedTurnData.CloneIssue).ToArray();
        internal void EnsureCurrent(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            files.EnsureCanonicalWriteLeaseActive(lease);
            if (!ReferenceEquals(files, _files) || !ReferenceEquals(lease, _lease) ||
                !files.IsCurrentSessionGeneration(lease, _rootGeneration()))
                throw new InvalidDataException("Joint rejected proof cannot survive lease/root/generation turnover.");
        }
        private string _rootGeneration() => Checkpoint._root["sessionGeneration"]!.GetValue<string>();
    }
    internal sealed record ReadResult(MortalJointRepairCheckpoint? Checkpoint, IReadOnlyList<ValidationIssue> Issues,
        RejectedCapture? Capture = null)
    {
        internal bool IsValid => Checkpoint is not null && Issues.Count == 0;

        /// <summary>Gets private diagnostic DATA excluded from public result serialization and classification.</summary>
        internal MortalJointRepairReadDiagnostic? Diagnostic { get; private set; }

        /// <summary>Retains existing refusal details without changing this result or its outward issues.</summary>
        /// <param name="origin">Named actual source boundary.</param>
        /// <param name="sourceIssues">Earlier producer issues, if the outward result intentionally maps them.</param>
        /// <param name="caughtException">Exact originally caught exception, if any.</param>
        /// <returns>This same result with best-effort private diagnostic DATA.</returns>
        internal ReadResult WithDiagnostics(string origin, IReadOnlyList<ValidationIssue>? sourceIssues = null,
            Exception? caughtException = null)
        {
            try { Diagnostic = MortalJointRepairReadDiagnostic.Capture(origin, Issues, sourceIssues, caughtException); }
            catch { /* Diagnostic allocation must not replace the original read result. */ }
            return this;
        }

        /// <summary>Reads already-computed producer issues inside the diagnostic guard, never as a new source operation.</summary>
        /// <param name="origin">Named actual refusal boundary.</param>
        /// <param name="readSourceIssues">Getter for existing producer issue DATA; failure is diagnostic only.</param>
        /// <param name="caughtException">Original caught exception, if any.</param>
        /// <returns>This same refused result, including incomplete diagnostic capture when the getter fails.</returns>
        internal ReadResult WithDiagnosticsFromSource(string origin, Func<IReadOnlyList<ValidationIssue>?> readSourceIssues,
            Exception? caughtException = null)
        {
            try { Diagnostic = MortalJointRepairReadDiagnostic.CaptureFromSource(origin, Issues, readSourceIssues, caughtException); }
            catch { /* Diagnostic allocation must not replace the original read result. */ }
            return this;
        }

        /// <summary>Creates the caller's unchanged generic refusal, carrying private DATA only when captured.</summary>
        /// <param name="message">The caller's existing public failure message.</param>
        /// <returns>An InvalidDataException with the exact original message and no new inner exception.</returns>
        internal InvalidDataException CreateFailure(string message)
        {
            var failure = new InvalidDataException(message);
            Diagnostic?.AttachTo(failure);
            return failure;
        }
    }

    internal static Task<ReadResult> CaptureAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        string sessionId, string requestId, string snapshotToken, int expectedTurn) =>
        CaptureCoreAsync(files, lease, sessionId, requestId, snapshotToken, expectedTurn, null);

    // Separate actual FIRST producer; the existing CaptureAsync remains v1.
    internal static Task<ReadResult> CaptureInterfaceFirstAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        string sessionId, string requestId, string snapshotToken, int expectedTurn) =>
        CaptureCoreAsync(files, lease, sessionId, requestId, snapshotToken, expectedTurn, null, 2);

    internal static async Task<ReadResult> ReconstructAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        string sessionId, string requestId, string snapshotToken, int expectedTurn)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        try
        {
            var bytes = await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            if (bytes is null) return Refuse("pending_missing", "The actual installed private wave is required.");
            return await CaptureCoreAsync(files, lease, sessionId, requestId, snapshotToken, expectedTurn, Root(bytes, "actual joint private pending"));
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or DecoderFallbackException)
        { return Refuse("pending_invalid", error.Message, caughtException: error); }
    }

    private static async Task<ReadResult> CaptureCoreAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        string sessionId, string requestId, string snapshotToken, int expectedTurn, JsonObject? retainedPending, int initialVersion = 1)
    {
        using var namedOperationTimer37 = files.MeasureOperation("Checkpoint.CaptureCoreAsync");
        files.EnsureCanonicalWriteLeaseActive(lease);
        var boundary = "ReadSnapshot";
        try
        {
            using var namedOperationTimer38 = files.MeasureOperation("Checkpoint.ReadSnapshot");
            var original = MortalQteWoundOpportunityProjection.ReadSnapshot(files, lease);
            namedOperationTimer38?.Dispose();
            boundary = "ReadSessionGeneration";
            using var namedOperationTimer39 = files.MeasureOperation("Checkpoint.ReadSessionGeneration");
            var generation = files.ReadLocalGenerationSnapshot(lease).Binding.Id;
            namedOperationTimer39?.Dispose();
            if (original.Realm != "mortal_world" || original.TurnNumber != expectedTurn || original.SessionId != sessionId ||
                original.RequestId != requestId || original.SnapshotToken != snapshotToken || string.IsNullOrWhiteSpace(generation))
                return Refuse("binding", "The fresh official original and physical generation must match the current frame.");
            boundary = "ReadCommand";
            using var namedOperationTimer40 = files.MeasureOperation("Checkpoint.ReadCommand");
            var commandBytes = await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
            namedOperationTimer40?.Dispose();
            MortalJointRepairCheckpoint? retainedCheckpoint = null;
            byte[]? draftBytes; byte[]? sceneBytes;
            boundary = "ReadRetainedCheckpoint";
            if (retainedPending is not null)
            {
                if (retainedPending["jointRepairCheckpoint"] is not JsonObject stored)
                    return Refuse("pending_checkpoint_missing", "An actual private wave must carry its closed rejected-source checkpoint.");
                var parsedCheckpoint = Parse(JsonSerializer.SerializeToElement(stored));
                if (!parsedCheckpoint.IsValid) return parsedCheckpoint;
                retainedCheckpoint = parsedCheckpoint.Checkpoint!;
                if (retainedPending[MortalJointInterfacePublication.PendingField] is not null)
                    _ = MortalJointInterfacePublication.LegacyPending(retainedPending);
                draftBytes = Convert.FromBase64String(stored["rejectedDraftBase64"]!.GetValue<string>());
                sceneBytes = Convert.FromBase64String(stored["narrativeBase64"]!.GetValue<string>());
            }
            else
            {
                boundary = "ReadActualOutputs";
                draftBytes = await files.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath);
                sceneBytes = await files.ReadFileBytesAsync(lease, "output/narrative_response.json");
                if (await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath) is not null)
                    return Refuse("occupied_pending", "Initial joint capture cannot replace an existing private pending wave.");
            }
            if (commandBytes is null || draftBytes is null || sceneBytes is null)
                return Refuse("input_missing", "Actual homogeneous command and retained rejected draft/scene are required.");
            boundary = "NormalizeNarrative";
            MortalJointAcceptedOutputBasis.RequireNormalizedNarrative(sceneBytes);
            boundary = "ReadCommandRoot";
            var command = Root(commandBytes, "joint treatment command");
            boundary = "ParseCommandRoot";
            var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(command));
            if (!parsed.Success) return new ReadResult(null, parsed.Issues).WithDiagnostics("CaptureCoreAsync/ParseCommandRoot");
            if (parsed.TreatmentCommands.Count != 1 || parsed.Commands.Count != 0 || parsed.AcceptedTransitionCommands.Count != 0)
                return Refuse("command_family", "One complete homogeneous treatment command is required.");
            boundary = "ReadHistory";
            var history = WoundHistoryState.Parse(await files.ReadFileAsync(lease, WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
            if (!history.IsValid) return new ReadResult(null, history.Issues).WithDiagnostics("CaptureCoreAsync/ReadHistory");
            boundary = "ParseRequestCatalog";
            var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(JsonSerializer.SerializeToElement(command),
                retainedPending is null ? null : JsonSerializer.SerializeToElement(retainedPending), history);
            if (!catalog.IsValid) return new ReadResult(null, catalog.Issues).WithDiagnostics("CaptureCoreAsync/ParseRequestCatalog");
            if (catalog.Requests.Count != 1 || catalog.HeldRequests.Count != 1 || catalog.FinalizedRequests.Count != 0)
                return Refuse("request_set", "One exact already-issued held request is required.");
            var persisted = catalog.HeldRequests[0]; var coordinate = persisted.Coordinates;
            if (persisted.Mode != "guaranteed" || coordinate.Realm != "mortal_world" || coordinate.TargetKind != "player" ||
                coordinate.TargetId != "player_current" || coordinate.SessionGeneration != generation ||
                coordinate.SessionId != sessionId || coordinate.RequestId != requestId || coordinate.SnapshotToken != snapshotToken || coordinate.Turn != expectedTurn)
                return Refuse("request_binding", "The existing player guaranteed request must bind this actual original and generation.");
            boundary = "ParseTreatmentContext";
            var context = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
            {
                ["schemaVersion"] = 1, ["realm"] = coordinate.Realm, ["targetKind"] = coordinate.TargetKind,
                ["targetId"] = coordinate.TargetId, ["providerKind"] = coordinate.ProviderKind,
                ["providerId"] = coordinate.ProviderId, ["currentLocationId"] = coordinate.LocationId
            }.ToJsonString(), "joint treatment context");
            if (!context.IsValid || context.Context is null) return new ReadResult(null, context.Issues).WithDiagnostics("CaptureCoreAsync/ParseTreatmentContext");
            boundary = "ExportCurrentTreatmentAuthority";
            using var namedOperationTimer41 = files.MeasureOperation("Checkpoint.ExportCurrentTreatmentAuthority");
            var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(files, lease, context.Context, coordinate.WoundId);
            namedOperationTimer41?.Dispose();
            if (!exported.IsValid || exported.Authority is not { } accepted) return new ReadResult(null, exported.Issues).WithDiagnostics("CaptureCoreAsync/ExportCurrentTreatmentAuthority");
            if (accepted.SessionGeneration != generation || accepted.Binding.SessionId != sessionId || accepted.Binding.RequestId != requestId ||
                accepted.Binding.SnapshotToken != snapshotToken || accepted.Binding.Turn != expectedTurn || accepted.Binding.Realm != "mortal_world")
                return Refuse("accepted_binding", "Fresh treatment accepted-state authority must independently retain the same frame.");
            boundary = "RestorePersistedTreatmentRequests";
            using var namedOperationTimer42 = files.MeasureOperation("Checkpoint.RestorePersistedTreatmentRequests");
            var restored = accepted.RestorePersistedTreatmentRequests(history);
            namedOperationTimer42?.Dispose();
            if (!restored.IsValid) return new ReadResult(null, restored.Issues).WithDiagnostics("CaptureCoreAsync/RestorePersistedTreatmentRequests");
            if (restored.Requests.Count != 1 || restored.HeldRequests.Count != 1 || restored.FinalizedRequests.Count != 0 ||
                restored.HeldRequests[0].RequestFingerprint != persisted.RequestFingerprint)
                return Refuse("restored_request", "Fresh restoration must return the same complete request, not a new reservation.");
            var request = restored.HeldRequests[0];
            boundary = "CreateGuaranteedAttempt";
            using var namedOperationTimer43 = files.MeasureOperation("Checkpoint.CreateGuaranteedAttempt");
            var resolved = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, accepted.CurrentWound, accepted);
            namedOperationTimer43?.Dispose();
            if (resolved.Disposition != "Resolved" || resolved.Resolution is not { } resolution) return new ReadResult(null,
                resolved.Issues.Count == 0 ? Refuse("resolution", "The exact existing request must produce its guaranteed result.").Issues : resolved.Issues)
                .WithDiagnostics("CaptureCoreAsync/CreateGuaranteedAttempt");
            boundary = "FinalizeResource";
            using var namedOperationTimer44 = files.MeasureOperation("Checkpoint.FinalizeResource");
            var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
            namedOperationTimer44?.Dispose();
            if (!finalization.IsValid) return new ReadResult(null, finalization.Issues).WithDiagnostics("CaptureCoreAsync/FinalizeResource");
            boundary = "RecomposeTreatmentCommand";
            var scene = Root(sceneBytes, "joint ordinary narrative");
            var sceneText = scene["response"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(sceneText) || sceneText != parsed.TreatmentCommands[0].FinalSceneText ||
                !JsonNode.DeepEquals(command, WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(accepted.Binding, resolution, sceneText)))
                return Refuse("command_result", "The complete command/result and scene must match the fresh reducer.");
            boundary = "ReadRejectedDraft";
            var draft = Root(draftBytes, "rejected joint acquisition");
            if (draft["decision"]?.GetValue<string>() != "materialize")
                return Refuse("decision_kind", "Only a genuine materialize severity repair is this first contour.");
            boundary = "ComposeRejectedQteResponse";
            using var namedOperationTimer45 = files.MeasureOperation("Checkpoint.ComposeRejectedQteResponse");
            var refusal = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(files, lease, Text(draftBytes), sceneText,
                out var replay, out var opportunity);
            namedOperationTimer45?.Dispose();
            if (refusal is null || refusal.Success || replay is not null || opportunity is null || opportunity.WorseningTarget is not null)
                return Refuse("rejected_source", "A real rejected CREATE from the actual QTE adapter is required.", sourceResult: refusal);
            var actionable = refusal.Issues.Where(issue => issue.Code == "wound_severity_above_opportunity" &&
                issue.WoundRepairContext is not null && WoundRepairPacketBuilder.IsRepairableIssue(issue)).ToArray();
            if (actionable.Length == 0 || refusal.Issues.Any(issue => issue.Code != "wound_severity_above_opportunity"))
                return Refuse("severity_only", "Private/source/other proposal failures cannot mint this severity checkpoint.", sourceResult: refusal);
            boundary = "BuildSeverityPacket";
            var packets = WoundRepairPacketBuilder.Build(actionable);
            if (packets.Count != 1 || !packets[0].MatchesOpportunity(opportunity) || !packets[0].MatchesRejectedDecision(draft))
                return Refuse("packet", "Exactly one source-bound real severity packet is required.");
            boundary = "ParseOriginalOccurrences";
            var occurrences = MortalWoundOccurrenceState.Parse(Text(original.ReadRequiredBytes(MortalWoundOccurrenceState.StatePath)), MortalWoundOccurrenceState.StatePath);
            if (!occurrences.IsValid || occurrences.State is null) return new ReadResult(null, occurrences.Issues).WithDiagnostics("CaptureCoreAsync/ParseOriginalOccurrences");
            if (occurrences.State.Occurrences.Count != 1 || occurrences.State.Occurrences[0].OpportunityRef != opportunity.PublicRef)
                return Refuse("occurrence_set", "Exactly one real selected occurrence is required.");
            var occurrence = occurrences.State.Occurrences[0];
            if (occurrence.AcceptedEvents.Count(value => value.EventRef == opportunity.EventRef) != 1)
                return Refuse("source_event", "The selected real opportunity event must be unique in its signed source vector.");
            boundary = "ReadManifestBytes";
            using var namedOperationTimer46 = files.MeasureOperation("Checkpoint.ReadManifestBytes");
            var manifest = await files.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            namedOperationTimer46?.Dispose();
            boundary = "ReadAuthorityBytes";
            using var namedOperationTimer47 = files.MeasureOperation("Checkpoint.ReadAuthorityBytes");
            var authority = await files.ReadFileBytesAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath);
            namedOperationTimer47?.Dispose();
            if (manifest is null || authority is null) return Refuse("original_files", "The actual original manifest and authority bytes are required.");
            boundary = "ComposePendingRoot";
            var pending = WoundRepairPacketBuilder.ComposePendingRoot(accepted.Binding, packets, parsed);
            var receiptFingerprints = pending["repairReceipts"]!.AsArray().Select(receipt =>
                (JsonNode?)JsonValue.Create(receipt!["semanticFingerprint"]!.GetValue<string>())).ToArray();
            var root = new JsonObject
            {
                ["schemaVersion"] = retainedCheckpoint?._root["schemaVersion"]!.GetValue<int>() ?? initialVersion, ["kind"] = Kind, ["sessionGeneration"] = generation,
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
            if (root["schemaVersion"]!.GetValue<int>() == 2) root["interfaceObservationProtocolVersion"] = 1;
            root["fingerprint"] = Fingerprint(JsonSerializer.SerializeToElement(root));
            boundary = "ParseBuiltCheckpoint";
            var checkedValue = Parse(JsonSerializer.SerializeToElement(root));
            if (!checkedValue.IsValid) return checkedValue;
            boundary = "CompareRetainedWave";
            if (retainedCheckpoint is not null && !JsonNode.DeepEquals(root, retainedCheckpoint.ToJsonObject()))
                return Refuse("provenance_changed", "Fresh original/request/source/packet reconstruction disagrees with the retained checkpoint.");
            pending["jointRepairCheckpoint"] = root.DeepClone();
            if (retainedPending?[MortalJointInterfacePublication.PendingField] is { } result)
                pending[MortalJointInterfacePublication.PendingField] = result.DeepClone();
            if (retainedPending is not null && !JsonNode.DeepEquals(pending, retainedPending))
                return Refuse("wave_changed", "Only the entire exact source-issued wave and complete request graph may reconstruct.");
            return new(checkedValue.Checkpoint, Array.Empty<ValidationIssue>(), new RejectedCapture(CaptureIssuer, files, lease,
                checkedValue.Checkpoint!, original, accepted, request, resolution, opportunity, packets[0], commandBytes, pending, actionable));
        }
        catch (Exception error) when (error is JsonException or InvalidDataException or DecoderFallbackException or FormatException or InvalidOperationException)
        {
            return Refuse("input_invalid", error.Message, caughtException: error, boundary: boundary);
        }
    }

    internal static async Task<WoundResponseInputCompositionResult> RevalidateCorrectionAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, RejectedCapture capture)
    {
        using var namedOperationTimer48 = files.MeasureOperation("Correction.RevalidateCorrectionAsync");
        capture.EnsureCurrent(files, lease);
        using var namedOperationTimer49 = files.MeasureOperation("Correction.ReadCommand");
        var currentCommand = await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        namedOperationTimer49?.Dispose();
        using var namedOperationTimer50 = files.MeasureOperation("Correction.ReadPending");
        var currentPending = await files.ReadFileBytesAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
        namedOperationTimer50?.Dispose();
        using var namedOperationTimer51 = files.MeasureOperation("Correction.ReadDraft");
        var draftBytes = await files.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath);
        namedOperationTimer51?.Dispose();
        using var namedOperationTimer52 = files.MeasureOperation("Correction.ReadNarrative");
        var sceneBytes = await files.ReadFileBytesAsync(lease, "output/narrative_response.json");
        namedOperationTimer52?.Dispose();
        if (currentCommand is null || !currentCommand.AsSpan().SequenceEqual(capture.CommandBytes) || currentPending is null ||
            !JsonNode.DeepEquals(Root(currentPending, "joint current pending"), capture.Pending) || draftBytes is null || sceneBytes is null)
            throw new InvalidDataException("The actual joint command/pending/public resubmission must retain the complete owned graph.");
        MortalJointAcceptedOutputBasis.RequireNormalizedNarrative(sceneBytes);
        var draft = Root(draftBytes, "joint corrected public draft"); var scene = Root(sceneBytes, "joint corrected scene");
        var expectedScene = Root(Convert.FromBase64String(capture.Checkpoint._root["narrativeBase64"]!.GetValue<string>()), "retained rejected scene");
        var actualScene = scene.DeepClone().AsObject(); actualScene.Remove("timestamp"); expectedScene.Remove("timestamp");
        if (!JsonNode.DeepEquals(actualScene, expectedScene) || capture.Packet.RequiresResponseCorrection ||
            !capture.Packet.MatchesCorrectedDecision(draft, scene["response"]?.GetValue<string>()))
            throw new InvalidDataException("Only the packet-authorized severity leaf may change; the scene remains exact except timestamp.");
        using var namedOperationTimer53 = files.MeasureOperation("Correction.ComposeCorrectedQteResponse");
        var composed = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(files, lease, Text(draftBytes), scene["response"]!.GetValue<string>(),
            out var replay, out var opportunity);
        namedOperationTimer53?.Dispose();
        if (composed is null || !composed.Success || replay is not null || opportunity is null || !capture.Packet.MatchesOpportunity(opportunity))
            throw new InvalidDataException("The real adapter must independently admit the corrected acquisition within its complete sealed range.");
        return composed;
    }

    internal static IReadOnlyList<ValidationIssue> ValidatePendingEnvelope(JsonElement root)
    {
        if (!root.TryGetProperty("jointRepairCheckpoint", out var value))
            return root.TryGetProperty(MortalJointInterfacePublication.PendingField, out _)
                ? Refuse("interface_without_checkpoint", "An interface result cannot exist without its exact protocol checkpoint.").Issues
                : Array.Empty<ValidationIssue>();
        var parsed = Parse(value); if (!parsed.IsValid) return parsed.Issues;
        try
        {
            if (!new[] { "sessionId", "requestId", "snapshotToken" }.All(name => root.GetProperty(name).GetString() == value.GetProperty(name).GetString()) ||
                root.GetProperty("repairPackets").GetArrayLength() != 1 || root.GetProperty("repairReceipts").GetArrayLength() != 1 ||
                root.GetProperty("submittedTreatmentRequests").GetArrayLength() != 1)
                return Refuse("pending_binding", "The checkpoint requires one complete exact-frame packet/receipt/request wave.").Issues;
            var request = root.GetProperty("submittedTreatmentRequests")[0];
            if (!new[] { "operationKey", "attemptId", "requestFingerprint" }.All(name => request.GetProperty(name).GetString() == value.GetProperty(name).GetString()) ||
                root.GetProperty("repairPackets")[0].GetProperty("semanticFingerprint").GetString() != value.GetProperty("packetFingerprints")[0].GetString() ||
                root.GetProperty("repairReceipts")[0].GetProperty("semanticFingerprint").GetString() != value.GetProperty("receiptFingerprints")[0].GetString())
                return Refuse("pending_graph", "Packet/receipt/full request identities disagree with the checkpoint.").Issues;
            if (root.TryGetProperty(MortalJointInterfacePublication.PendingField, out var result))
            {
                var publication = MortalJointInterfacePublication.Read(result);
                publication.RequireLegacyPending(JsonNode.Parse(root.GetRawText())!.AsObject());
            }
            return Array.Empty<ValidationIssue>();
        }
        catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or FormatException or InvalidDataException or JsonException)
        { return Refuse("pending_shape", error.Message).Issues; }
    }

    internal static ReadResult Parse(JsonElement root)
    {
        try
        {
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var version) ||
                version.GetInt32() is not (1 or 2)) return Refuse("version", "Unsupported checkpoint version.");
            var fields = version.GetInt32() == 1 ? Fields : Fields.Append("interfaceObservationProtocolVersion").ToArray();
            if (!root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)
                .SequenceEqual(fields.Order(StringComparer.Ordinal)))
                return Refuse("shape", "One complete closed checkpoint with unique fields is required.");
            if (version.GetInt32() == 2 && root.GetProperty("interfaceObservationProtocolVersion").GetInt32() != 1 ||
                root.GetProperty("kind").GetString() != Kind || root.GetProperty("turnNumber").GetInt32() < 0)
                return Refuse("version", "Unsupported checkpoint version, kind or turn.");
            foreach (var name in Fields.Except(["schemaVersion", "turnNumber", "packetFingerprints", "receiptFingerprints"]))
            {
                var value = root.GetProperty(name).GetString();
                if (string.IsNullOrWhiteSpace(value) ||
                    name.EndsWith("Hash", StringComparison.Ordinal) && !IsContentHash(value) ||
                    (name.EndsWith("Fingerprint", StringComparison.Ordinal) || name == "fingerprint") &&
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
            return Refuse("shape", error.Message, caughtException: error);
        }
    }
    private static string Fingerprint(JsonElement root)
    {
        var selected = root.GetProperty("schemaVersion").GetInt32() == 2;
        var values = new List<string?> { selected ? "book_of_eternity.mortal_joint_repair_checkpoint.v2" : Domain };
        var fields = selected ? Fields.Append("interfaceObservationProtocolVersion") : Fields.AsEnumerable();
        foreach (var name in fields.Where(name => name != "fingerprint"))
        {
            var value = root.GetProperty(name); values.Add(name);
            if (value.ValueKind == JsonValueKind.Array) values.AddRange(value.EnumerateArray().Select(item => item.GetString()));
            else values.Add(value.ValueKind == JsonValueKind.Number ? value.GetInt32().ToString(CultureInfo.InvariantCulture) : value.GetString());
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(values);
    }
    // Exact file-content digests are raw uppercase SHA256; semantic seals keep the existing sha256: format.
    private static bool IsContentHash(string value) => value.Length == 64 && value.All(character =>
        character is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Text(byte[] bytes) => new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
    private static JsonObject Root(byte[] bytes, string label) => StrictJsonAuthority.Deserialize<JsonObject>(Text(bytes),
        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, label) ?? throw new InvalidDataException(label + " must be an object.");
    /// <summary>Preserves the original public refusal and separately retains private diagnostic DATA.</summary>
    /// <param name="code">Unchanged refusal suffix.</param>
    /// <param name="message">Unchanged outward issue message.</param>
    /// <param name="sourceResult">Existing producer result whose issue getter is read only inside guarded diagnostics.</param>
    /// <param name="caughtException">Original exception caught by the existing filter, if any.</param>
    /// <param name="boundary">Named operation that failed within a multi-step read.</param>
    /// <param name="origin">Compiler-provided name of the existing caller.</param>
    /// <returns>The same refused checkpoint result, with best-effort internal diagnostics only.</returns>
    private static ReadResult Refuse(string code, string message, WoundResponseInputCompositionResult? sourceResult = null,
        Exception? caughtException = null, string? boundary = null,
        [System.Runtime.CompilerServices.CallerMemberName] string origin = "")
    {
        var result = new ReadResult(null, [MortalWoundOpportunityAdapter.PrivateSourceIssue(new ValidationIssue(
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath, IssueSeverity.Error, message, "mortal_joint_checkpoint_" + code,
            actor: "Client", section: "wound_materialization"))]);
        try { return result.WithDiagnosticsFromSource(origin + "/" + (boundary ?? code), () => sourceResult?.Issues, caughtException); }
        catch { return result; } // Diagnostic naming, source-issue copying or allocation cannot replace the original refusal.
    }
}
