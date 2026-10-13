using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Local completion evidence for one already-published T070 decision. Only the
/// taken common-plan publisher creates it, in the same batch as its mechanics.
/// It grants no new command, reduction, allocation or archive-resume authority.
/// </summary>
internal static class MortalQteDecisionPublication
{
    internal const string StatePath = "game_state/wounds/mortal_qte_decision_publication.json";
    private const string HashDomain = "T070-MORTAL-QTE-COMMON-PUBLICATION-V1\n";
    private const string DecisionPayloadDomain = "T070-MORTAL-QTE-PUBLIC-DECISION-V1\n";
    internal static readonly string[] QuartetPaths =
    [
        ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
        ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath
    ];
    private static readonly string[] FootprintPaths = QuartetPaths.Concat(new[]
    {
        MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath,
        WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
        WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath, WoundCarrierCatalog.EnemiesPath,
        WoundCarrierCatalog.AlliesPath, WoundCarrierCatalog.AfterlifeProfilesPath,
        EffectAcceptedTurnPlan.IdentityIndexPath, EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath,
        EffectCarrierCatalog.EnemiesPath, EffectCarrierCatalog.AlliesPath, EffectCarrierCatalog.AfterlifeProfilesPath,
        EffectCarrierCatalog.SpiritualConflictPath, ResourcePendingResolutionState.PendingPath
    }).Distinct(StringComparer.Ordinal).OrderBy(static path => path, StringComparer.Ordinal).ToArray();
    private static readonly string[] Fields =
    [
        "schemaVersion", "generation", "sessionId", "requestId", "snapshotToken", "turn",
        "manifestHash", "authorityHash", "receiptId", "receiptFingerprint", "decisionFingerprint",
        "decisionPayloadFingerprint", "operationKey", "inputFingerprint", "originalQuartet", "publishedFootprint", "consumedCommands", "fingerprint"
    ];

    internal static byte[] SerializePublicationRoot(JsonNode root)
    {
        var body = Encoding.UTF8.GetBytes(root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        return Encoding.UTF8.GetPreamble().Concat(body).ToArray();
    }

    internal static async Task<byte[]?> CreateAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        AcceptedMechanicsPlan plan, AcceptedMechanicsPlanBinding binding,
        IReadOnlyList<CanonicalLocalFileChange> prepared)
    {
        if (plan.AwaitsPendingResolution || !plan.ConsumedPaths.Contains(AcceptedMechanicsPlan.WoundCommandPath, StringComparer.Ordinal) ||
            !plan.OwnerCompanionAfterImages.TryGetValue(MortalWoundOpportunityReceiptState.StatePath, out var receiptRoot)) return null;
        var receipts = MortalWoundOpportunityReceiptState.Parse(receiptRoot.ToJsonString(), MortalWoundOpportunityReceiptState.StatePath);
        var matches = receipts.State?.Receipts.Where(row => row.SessionId == binding.SessionId && row.RequestId == binding.RequestId &&
            row.SnapshotToken == binding.SnapshotToken && row.Turn == binding.Turn && row.ConsumedEventSelection.Evidence.AdapterKind == "qte").ToArray();
        if (!receipts.IsValid || matches is not { Length: 1 }) throw Invalid("receipt_binding");
        var snapshot = ReadSnapshot(fs, lease);
        if (snapshot.SessionId != binding.SessionId || snapshot.RequestId != binding.RequestId ||
            snapshot.SnapshotToken != binding.SnapshotToken || snapshot.TurnNumber != binding.Turn || snapshot.Realm != "mortal_world")
            throw Invalid("original_binding");
        var original = new JsonObject();
        foreach (var path in QuartetPaths)
        {
            var bytes = snapshot.ReadRequiredBytes(path);
            if (!plan.BeforeImages.TryGetValue(path, out var before) || before.Bytes is null || !before.Bytes.AsSpan().SequenceEqual(bytes))
                throw Invalid("original_quartet");
            original[path] = Digest(bytes);
        }
        if (!plan.BeforeImages.TryGetValue(StatePath, out var markerBefore) ||
            !Same(markerBefore.Bytes, await fs.ReadFileBytesAsync(lease, StatePath))) throw Invalid("marker_beforeimage");
        if (!plan.BeforeImages.TryGetValue(MortalWoundOpportunityAdapter.DecisionPath, out var decisionBefore) ||
            !decisionBefore.Existed || decisionBefore.Bytes is null ||
            !Same(decisionBefore.Bytes, await fs.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath)))
            throw Invalid("decision_beforeimage");
        var decisionPayloadFingerprint = ComputeDecisionPayloadFingerprint(decisionBefore.Bytes);
        var footprint = new JsonObject();
        foreach (var path in FootprintPaths)
        {
            var change = prepared.SingleOrDefault(row => row.RelativePath == path);
            footprint[path] = Digest(change is null ? await fs.ReadFileBytesAsync(lease, path) : change.After);
        }
        var consumed = new JsonArray();
        foreach (var path in new[] { ResourceMaterializationContract.CommandPath, EffectAcceptedTurnPlan.CommandPath,
                     AcceptedMechanicsPlan.WoundCommandPath, MortalWoundOpportunityAdapter.DecisionPath })
            if (prepared.Any(row => row.RelativePath == path && row.After is null)) consumed.Add(path);
        if (!consumed.Any(node => node?.GetValue<string>() == MortalWoundOpportunityAdapter.DecisionPath) ||
            !consumed.Any(node => node?.GetValue<string>() == AcceptedMechanicsPlan.WoundCommandPath)) throw Invalid("command_consumption");
        var receipt = matches[0];
        var root = new JsonObject
        {
            ["schemaVersion"] = 1, ["generation"] = fs.GetOrCreateSessionGeneration(lease),
            ["sessionId"] = binding.SessionId, ["requestId"] = binding.RequestId,
            ["snapshotToken"] = binding.SnapshotToken, ["turn"] = binding.Turn,
            ["manifestHash"] = Digest(await fs.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath)),
            ["authorityHash"] = Digest(await fs.ReadFileBytesAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath)),
            ["receiptId"] = receipt.ReceiptId, ["receiptFingerprint"] = receipt.ReceiptFingerprint,
            ["decisionFingerprint"] = receipt.DecisionFingerprint, ["operationKey"] = receipt.OperationKey,
            ["decisionPayloadFingerprint"] = decisionPayloadFingerprint,
            ["inputFingerprint"] = plan.InputFingerprint, ["originalQuartet"] = original,
            ["publishedFootprint"] = footprint, ["consumedCommands"] = consumed
        };
        root["fingerprint"] = Fingerprint(root);
        return SerializePublicationRoot(root);
    }

    internal enum CurrentPublicationState { NotCommitted, VerifiedCompleted, IntegrityConflict }
    internal sealed record CurrentPublicationInspection(CurrentPublicationState State, bool MayResubmit, string Reason);
    private sealed record CompletedProof(JsonObject Root, MortalWoundOpportunityReceipt Receipt);
    private static readonly string[] PartitionPaths =
    [MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath, WoundHistoryState.HistoryPath];
    private static readonly string[] MechanicalCommandPaths =
    [ResourceMaterializationContract.CommandPath, EffectAcceptedTurnPlan.CommandPath, AcceptedMechanicsPlan.WoundCommandPath];

    // This inspection grants no command/publication authority. A past commit and permission
    // to resubmit today's bytes are separate facts, including when foreign commands return.
    internal static async Task<CurrentPublicationInspection> InspectCurrentAsync(FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease lease, string sessionId, string requestId, string snapshotToken, int turn)
    {
        fs.EnsureCanonicalWriteLeaseActive(lease);
        try
        {
            // Historical Mortal markers must not govern an unrelated afterlife validation loop.
            // The ordinary official soul read retains the existing older presence-set semantics.
            var realmRead = PendingTurnSnapshotReader.ReadCurrent(fs, lease, ["game_state/meta/soul_state.json"]);
            if (!realmRead.Success || realmRead.Snapshot is null) throw Invalid("snapshot");
            if (realmRead.Snapshot.Realm != "mortal_world")
                return new(CurrentPublicationState.NotCommitted, false, "non_mortal_original");
            var snapshot = ReadInspectionSnapshot(fs, lease);
            if (snapshot.SessionId != sessionId || snapshot.RequestId != requestId ||
                snapshot.SnapshotToken != snapshotToken || snapshot.TurnNumber != turn)
                throw Invalid("inspection_binding");
            var baselinePaths = PartitionPaths.Prepend(StatePath);
            var unchanged = true;
            foreach (var path in baselinePaths)
                unchanged &= Same(ReadObservedBytes(snapshot, path), await fs.ReadFileBytesAsync(lease, path));
            if (unchanged) return new(CurrentPublicationState.NotCommitted, false, "original_baseline");
            var proof = await ReadCompletedProofAsync(fs, lease, snapshot);
            foreach (var path in MechanicalCommandPaths)
                if (await fs.ReadFileBytesAsync(lease, path) is not null)
                    return new(CurrentPublicationState.VerifiedCompleted, false, "foreign_mechanical_command");
            var decision = await fs.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath);
            var exact = false;
            try { exact = decision is not null && Text(proof.Root, "decisionPayloadFingerprint") == ComputeDecisionPayloadFingerprint(decision); }
            catch (Exception error) when (error is System.Text.Json.JsonException or InvalidDataException or InvalidOperationException or DecoderFallbackException) { }
            return new(CurrentPublicationState.VerifiedCompleted, exact, exact ? "exact_public_decision" : "changed_or_malformed_public_decision");
        }
        catch (Exception error) when (error is not CoordinatedStatePublicationUncertainException &&
                                      error is (System.Text.Json.JsonException or InvalidDataException or DecoderFallbackException or
                                          InvalidOperationException or KeyNotFoundException or FormatException))
        {
            return new(CurrentPublicationState.IntegrityConflict, false, error.Message);
        }
    }

    internal static async Task ValidateExactReplayAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        MortalWoundOpportunityReceiptConsumePlanResult replay)
    {
        if (replay.Disposition != "exact_replay" || replay.Receipt is not { } receipt) throw Invalid("not_exact_replay");
        var proof = await ReadCompletedProofAsync(fs, lease, ReadInspectionSnapshot(fs, lease));
        if (receipt.SessionId != proof.Receipt.SessionId || receipt.RequestId != proof.Receipt.RequestId ||
            receipt.SnapshotToken != proof.Receipt.SnapshotToken || receipt.Turn != proof.Receipt.Turn ||
            receipt.ReceiptId != proof.Receipt.ReceiptId || receipt.ReceiptFingerprint != proof.Receipt.ReceiptFingerprint ||
            receipt.DecisionFingerprint != proof.Receipt.DecisionFingerprint || receipt.OperationKey != proof.Receipt.OperationKey)
            throw Invalid("source_or_receipt");
        var currentDecision = await fs.ReadFileBytesAsync(lease, MortalWoundOpportunityAdapter.DecisionPath) ?? throw Invalid("decision_missing");
        if (Text(proof.Root, "decisionPayloadFingerprint") != ComputeDecisionPayloadFingerprint(currentDecision))
            throw Invalid("changed_decision_payload");
        foreach (var path in MechanicalCommandPaths)
            if (await fs.ReadFileBytesAsync(lease, path) is not null) throw Invalid("new_command");
    }

    private static async Task<CompletedProof> ReadCompletedProofAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        PendingTurnSnapshotReadAuthority inspection)
    {
        var bytes = await fs.ReadFileBytesAsync(lease, StatePath) ?? throw Invalid("missing");
        var root = StrictJsonAuthority.Deserialize<JsonObject>(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "T070 common publication evidence") ?? throw Invalid("null");
        if (root.Count != Fields.Length || root.Any(pair => !Fields.Contains(pair.Key, StringComparer.Ordinal)) ||
            root["schemaVersion"]?.GetValue<int>() != 1) throw Invalid("shape");
        var payload = root.DeepClone().AsObject(); payload.Remove("fingerprint");
        if (Text(root, "fingerprint") != Fingerprint(payload) || !fs.IsCurrentSessionGeneration(lease, Text(root, "generation")))
            throw Invalid("seal_or_generation");
        var snapshot = ReadSnapshot(fs, lease);
        if (snapshot.Realm != "mortal_world" || inspection.SessionId != snapshot.SessionId || inspection.RequestId != snapshot.RequestId ||
            inspection.SnapshotToken != snapshot.SnapshotToken || inspection.TurnNumber != snapshot.TurnNumber ||
            Text(root, "sessionId") != snapshot.SessionId || Text(root, "requestId") != snapshot.RequestId ||
            Text(root, "snapshotToken") != snapshot.SnapshotToken || root["turn"]?.GetValue<int>() != snapshot.TurnNumber ||
            Text(root, "manifestHash") != Digest(await fs.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath)) ||
            Text(root, "authorityHash") != Digest(await fs.ReadFileBytesAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath)) ||
            string.IsNullOrWhiteSpace(Text(root, "inputFingerprint"))) throw Invalid("source_or_receipt");
        var original = MortalQteWoundOpportunityProjection.ReadSnapshot(fs, lease);
        if (original.SessionId != snapshot.SessionId || original.RequestId != snapshot.RequestId ||
            original.SnapshotToken != snapshot.SnapshotToken || original.TurnNumber != snapshot.TurnNumber || original.Realm != snapshot.Realm)
            throw Invalid("original_binding");
        var offered = MortalQteWoundOpportunityProjection.Project(original); // Full signed source batches/events/carrier agreement before selection.
        var issues = new List<ValidationIssue>();
        if (!MortalWoundOpportunityAdapter.TryReadPublishedReceiptPartition(fs, lease, original, issues, out var receipts) || receipts is null)
            throw Invalid("receipt_partition: " + string.Join(";", issues.Select(issue => issue.Code)));
        var matching = receipts.Receipts.Where(row => row.SessionId == snapshot.SessionId && row.RequestId == snapshot.RequestId &&
            row.SnapshotToken == snapshot.SnapshotToken && row.Turn == snapshot.TurnNumber && row.ReceiptId == Text(root, "receiptId")).ToArray();
        if (matching.Length != 1) throw Invalid("receipt_binding");
        var receipt = matching[0];
        var signedOccurrences = MortalWoundOccurrenceState.Parse(MortalQteWoundOpportunityProjection.ReadObservedRoot(original,
            MortalWoundOccurrenceState.StatePath), MortalWoundOccurrenceState.StatePath);
        var source = signedOccurrences.State?.Occurrences.SingleOrDefault(row => row.OccurrenceId == receipt.OpportunityId);
        if (!signedOccurrences.IsValid || source is null || offered.Count != 1 ||
            offered[0]!["opportunityRef"]?.GetValue<string>() != source.OpportunityRef || source.AdapterKind != "qte" || source.Domain != "physical" ||
            source.ProfileKey != "mortal_qte_terminal_health_damage_v1" || source.Owner.Realm != "mortal_world" ||
            source.Owner.OwnerKind != "player" || source.Owner.OwnerId != "player_current" || source.Owner.CarrierPath != WoundCarrierCatalog.PlayerPath ||
            Text(root, "receiptFingerprint") != receipt.ReceiptFingerprint || Text(root, "decisionFingerprint") != receipt.DecisionFingerprint ||
            Text(root, "operationKey") != receipt.OperationKey) throw Invalid("source_or_receipt");
        var quartet = ClosedMap(root["originalQuartet"], QuartetPaths);
        foreach (var path in QuartetPaths)
            if (quartet[path]?.GetValue<string>() != Digest(snapshot.ReadRequiredBytes(path))) throw Invalid("original_quartet");
        var published = ClosedMap(root["publishedFootprint"], FootprintPaths);
        foreach (var path in FootprintPaths)
            if (published[path]?.GetValue<string>() != Digest(await fs.ReadFileBytesAsync(lease, path))) throw Invalid("published_footprint");
        var allowedCommands = MechanicalCommandPaths.Append(MortalWoundOpportunityAdapter.DecisionPath).ToArray();
        if (root["consumedCommands"] is not JsonArray commands || commands.Count is < 2 or > 4 ||
            commands.Select(node => node?.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() != commands.Count ||
            commands.Any(node => !allowedCommands.Contains(node?.GetValue<string>(), StringComparer.Ordinal)) ||
            !commands.Any(node => node?.GetValue<string>() == MortalWoundOpportunityAdapter.DecisionPath) ||
            !commands.Any(node => node?.GetValue<string>() == AcceptedMechanicsPlan.WoundCommandPath)) throw Invalid("consumed_commands");
        return new(root, receipt);
    }

    private static PendingTurnSnapshotReadAuthority ReadInspectionSnapshot(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        var read = PendingTurnSnapshotReader.ReadCurrent(fs, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(["game_state/meta/soul_state.json"], PartitionPaths.Prepend(StatePath)));
        return read.Success && read.Snapshot is not null ? read.Snapshot : throw Invalid("snapshot");
    }

    private static byte[]? ReadObservedBytes(PendingTurnSnapshotReadAuthority snapshot, string path) =>
        snapshot.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal) ? snapshot.ReadRequiredBytes(path) :
        snapshot.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal) ? null : throw Invalid("unobserved_root");

    internal static async Task ValidateUnchangedMarkerBeforeAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        var snapshot = ReadSnapshot(fs, lease);
        var previous = snapshot.CoveredLogicalPaths.Contains(StatePath, StringComparer.Ordinal)
            ? snapshot.ReadRequiredBytes(StatePath) : null;
        if (!Same(previous, await fs.ReadFileBytesAsync(lease, StatePath))) throw Invalid("marker_changed_before_publication");
    }

    private static PendingTurnSnapshotReadAuthority ReadSnapshot(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        var result = PendingTurnSnapshotReader.ReadCurrent(fs, lease,
            PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(QuartetPaths.Concat(["game_state/meta/soul_state.json"]), [StatePath]));
        return result.Success && result.Snapshot is not null ? result.Snapshot : throw Invalid("snapshot");
    }

    private static JsonObject ClosedMap(JsonNode? node, IReadOnlyCollection<string> fields) =>
        node is JsonObject map && map.Count == fields.Count && map.All(pair => fields.Contains(pair.Key, StringComparer.Ordinal))
            ? map : throw Invalid("footprint_shape");
    private static string Text(JsonObject root, string field) => root[field] is JsonValue value && value.TryGetValue<string>(out var text)
        ? text : throw Invalid("field_" + field);
    private static string Digest(byte[]? bytes) => bytes is null ? "absent" : Convert.ToHexString(SHA256.HashData(bytes));
    private static string ComputeDecisionPayloadFingerprint(byte[] bytes)
    {
        var decision = StrictJsonAuthority.Deserialize<JsonObject>(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "T070 public decision payload") ?? throw Invalid("decision_null");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            DecisionPayloadDomain + WoundAcceptedTurnFingerprintWriter.CanonicalJson(decision))));
    }
    private static string Fingerprint(JsonObject payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        HashDomain + payload.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed))));
    private static bool Same(byte[]? a, byte[]? b) => a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b);
    private static InvalidDataException Invalid(string reason) => new("mortal_qte_completed_publication_invalid: " + reason);
}
