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
        "operationKey", "inputFingerprint", "originalQuartet", "publishedFootprint", "consumedCommands", "fingerprint"
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
            ["inputFingerprint"] = plan.InputFingerprint, ["originalQuartet"] = original,
            ["publishedFootprint"] = footprint, ["consumedCommands"] = consumed
        };
        root["fingerprint"] = Fingerprint(root);
        return SerializePublicationRoot(root);
    }

    internal static async Task ValidateExactReplayAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        MortalWoundOpportunityReceiptConsumePlanResult replay)
    {
        if (replay.Disposition != "exact_replay" || replay.Receipt is not { } receipt) throw Invalid("not_exact_replay");
        var bytes = await fs.ReadFileBytesAsync(lease, StatePath) ?? throw Invalid("missing");
        var root = StrictJsonAuthority.Deserialize<JsonObject>(new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF'),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "T070 common publication evidence") ?? throw Invalid("null");
        if (root.Count != Fields.Length || root.Any(pair => !Fields.Contains(pair.Key, StringComparer.Ordinal)) ||
            root["schemaVersion"]?.GetValue<int>() != 1) throw Invalid("shape");
        var claimed = Text(root, "fingerprint");
        var payload = root.DeepClone().AsObject(); payload.Remove("fingerprint");
        if (claimed != Fingerprint(payload) || !fs.IsCurrentSessionGeneration(lease, Text(root, "generation"))) throw Invalid("seal_or_generation");
        var snapshot = ReadSnapshot(fs, lease);
        if (snapshot.Realm != "mortal_world" || Text(root, "sessionId") != snapshot.SessionId || Text(root, "requestId") != snapshot.RequestId ||
            Text(root, "snapshotToken") != snapshot.SnapshotToken || root["turn"]?.GetValue<int>() != snapshot.TurnNumber ||
            receipt.SessionId != snapshot.SessionId || receipt.RequestId != snapshot.RequestId || receipt.SnapshotToken != snapshot.SnapshotToken ||
            receipt.Turn != snapshot.TurnNumber || Text(root, "receiptId") != receipt.ReceiptId || Text(root, "receiptFingerprint") != receipt.ReceiptFingerprint ||
            Text(root, "decisionFingerprint") != receipt.DecisionFingerprint || Text(root, "operationKey") != receipt.OperationKey ||
            Text(root, "manifestHash") != Digest(await fs.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath)) ||
            Text(root, "authorityHash") != Digest(await fs.ReadFileBytesAsync(lease, PendingTurnSnapshotAuthority.AuthorityPath)) ||
            string.IsNullOrWhiteSpace(Text(root, "inputFingerprint"))) throw Invalid("source_or_receipt");
        var original = ClosedMap(root["originalQuartet"], QuartetPaths);
        foreach (var path in QuartetPaths)
            if (original[path]?.GetValue<string>() != Digest(snapshot.ReadRequiredBytes(path))) throw Invalid("original_quartet");
        var published = ClosedMap(root["publishedFootprint"], FootprintPaths);
        foreach (var path in FootprintPaths)
            if (published[path]?.GetValue<string>() != Digest(await fs.ReadFileBytesAsync(lease, path))) throw Invalid("published_footprint");
        var allowedCommands = new[] { ResourceMaterializationContract.CommandPath, EffectAcceptedTurnPlan.CommandPath,
            AcceptedMechanicsPlan.WoundCommandPath, MortalWoundOpportunityAdapter.DecisionPath };
        if (root["consumedCommands"] is not JsonArray commands || commands.Count is < 2 or > 4 ||
            commands.Select(node => node?.GetValue<string>()).Distinct(StringComparer.Ordinal).Count() != commands.Count ||
            commands.Any(node => !allowedCommands.Contains(node?.GetValue<string>(), StringComparer.Ordinal)) ||
            !commands.Any(node => node?.GetValue<string>() == MortalWoundOpportunityAdapter.DecisionPath) ||
            !commands.Any(node => node?.GetValue<string>() == AcceptedMechanicsPlan.WoundCommandPath)) throw Invalid("consumed_commands");
        // The identical public decision may be resubmitted; no raw mechanical command may return.
        foreach (var path in allowedCommands.Where(path => path != MortalWoundOpportunityAdapter.DecisionPath))
            if (await fs.ReadFileBytesAsync(lease, path) is not null) throw Invalid("new_command");
    }

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
    private static string Fingerprint(JsonObject payload) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        HashDomain + payload.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed))));
    private static bool Same(byte[]? a, byte[]? b) => a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b);
    private static InvalidDataException Invalid(string reason) => new("mortal_qte_completed_publication_invalid: " + reason);
}
