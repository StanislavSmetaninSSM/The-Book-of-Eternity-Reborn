using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>
/// Detached original ordinary-turn coordinates. Creation reads the official current
/// snapshot; persisted recovery belongs only to the validated canonical QTE pair.
/// </summary>
internal sealed class QtePhysicalInjurySource
{
    private readonly JsonObject _projection;

    private QtePhysicalInjurySource(JsonObject projection) =>
        _projection = projection.DeepClone().AsObject();

    internal string SessionId => Exact(_projection, "sessionId");
    internal string RequestId => Exact(_projection, "requestId");
    internal string SnapshotToken => Exact(_projection, "snapshotToken");
    internal int Turn => _projection["turn"]!.GetValue<int>();
    internal string Generation => Exact(_projection, "sessionGeneration");
    internal JsonObject Persisted => _projection.DeepClone().AsObject();

    internal static bool IsDeclared(QteSceneService.QteOffer offer) =>
        offer.TerminalOutcomes.Any(static outcome => outcome.PhysicalInjury is not null);

    internal static readonly IReadOnlyList<string> WoundRootPaths = new[]
    {
        MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath,
        WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
        WoundCarrierCatalog.PlayerPath, WoundCarrierCatalog.NpcPath,
        WoundCarrierCatalog.EnemiesPath, WoundCarrierCatalog.AlliesPath,
        WoundCarrierCatalog.AfterlifeProfilesPath
    };

    internal static async Task<QtePhysicalInjurySource> CaptureCurrentAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        string sessionId, string requestId, string snapshotToken, int turn)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        var current = ReadOriginal(files, lease);
        if (current.SessionId != sessionId || current.RequestId != requestId ||
            current.SnapshotToken != snapshotToken || current.TurnNumber != turn ||
            current.Realm != "mortal_world")
            throw Invalid("original_source_mismatch");
        var request = await files.ReadFileBytesAsync(lease, "input/turn_request.json") ??
            throw Invalid("original_request_missing");
        var manifest = JsonNode.Parse((await files.ReadFileAsync(lease,
            "game_state/control/pending_turn_snapshot.json"))!)!.AsObject();
        if (manifest["sourceLabel"]?.GetValue<string>() != QteSceneService.OrdinaryPlayerTurnSourceLabel)
            throw Invalid("original_source_label_mismatch");
        var generation = SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out var bound)
            ? bound : files.GetOrCreateSessionGeneration(lease);
        if (!files.IsCurrentSessionGeneration(lease, generation)) throw Invalid("generation_mismatch");
        return new QtePhysicalInjurySource(new JsonObject
        {
            ["schemaVersion"] = 1, ["sessionId"] = current.SessionId,
            ["requestId"] = current.RequestId, ["snapshotToken"] = current.SnapshotToken,
            ["turn"] = current.TurnNumber, ["realm"] = current.Realm,
            ["sourceLabel"] = QteSceneService.OrdinaryPlayerTurnSourceLabel,
            ["sessionGeneration"] = generation,
            ["originalRequestFingerprint"] = new CanonicalBeforeImage(true, request).Fingerprint
        });
    }

    internal async Task ValidateCurrentAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, int acceptedTurn)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        if (Turn != acceptedTurn || !files.IsCurrentSessionGeneration(lease, Generation))
            throw Invalid("generation_or_turn_mismatch");
        if (!files.FileExists("input/turn_request.json") &&
            !files.FileExists("game_state/control/pending_turn_snapshot.json") &&
            !files.FileExists(PendingTurnSnapshotAuthority.AuthorityPath))
        {
            await ValidateCompletedCleanupAsync(files, lease);
            return;
        }
        var current = await CaptureCurrentAsync(files, lease, SessionId, RequestId, SnapshotToken, Turn);
        if (!JsonNode.DeepEquals(_projection, current._projection)) throw Invalid("original_source_changed");
    }

    // Detached historical data survives actual terminal cleanup. This is not permission
    // to select or reduce again: active/awaiting states still require all original files.
    private async Task ValidateCompletedCleanupAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease)
    {
        var json = await files.ReadFileAsync(lease, QteDeferredEffectContinuation.StatePath) ??
            throw Invalid("original_source_missing");
        var continuation = StrictJsonAuthority.Deserialize<JsonObject>(json,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "completed QTE source lifetime") ??
            throw Invalid("original_source_invalid");
        var fingerprint = continuation["authorityFingerprint"]?.GetValue<string>();
        var detached = continuation.DeepClone().AsObject();
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(fingerprint) ||
            QteDeferredEffectContinuation.RefreshAuthorityFingerprint(detached) != fingerprint ||
            continuation["state"]?.GetValue<string>() != "terminal" ||
            continuation["sessionGeneration"]?.GetValue<string>() != Generation ||
            continuation["acceptedSourceTurn"]?.GetValue<int>() != Turn ||
            !JsonNode.DeepEquals(continuation["physicalInjurySource"], _projection) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(continuation["terminalFingerprint"]?.GetValue<string>()))
            throw Invalid("completed_source_lifetime_invalid");
        var runtime = StrictJsonAuthority.Deserialize<JsonObject>(
            (await files.ReadFileAsync(lease, QteSceneService.QteRuntimePath))!,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "completed QTE runtime");
        var history = StrictJsonAuthority.Deserialize<JsonArray>(
            (await files.ReadFileAsync(lease, QteSceneService.QteHistoryPath))!,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "completed QTE history");
        if (runtime is null || runtime["activeScene"] is not null ||
            continuation["selectedTerminalBinding"] is not JsonObject selected || history is null ||
            history.Count(row => row is JsonObject entry &&
                entry["qteId"]?.GetValue<string>() == continuation["qteId"]?.GetValue<string>() &&
                entry["acceptedAtTurn"]?.GetValue<int>() == Turn &&
                entry["outcomeId"]?.GetValue<string>() == selected["outcomeId"]?.GetValue<string>() &&
                entry["grade"]?.GetValue<string>() == selected["grade"]?.GetValue<string>()) != 1)
            throw Invalid("completed_source_lifetime_invalid");
    }

    // The caller must already have validated the continuation body and active runtime crosslink.
    internal static QtePhysicalInjurySource? ReadValidatedContinuation(JsonObject continuation,
        QteSceneService.QteOffer offer)
    {
        var declared = IsDeclared(offer);
        if (!continuation.ContainsKey("physicalInjurySource"))
        {
            if (declared) throw Invalid("original_source_missing");
            return null;
        }
        if (!declared || continuation["physicalInjurySource"] is not JsonObject source)
            throw Invalid("original_source_invalid");
        var fields = new[] { "schemaVersion", "sessionId", "requestId", "snapshotToken", "turn",
            "realm", "sourceLabel", "sessionGeneration", "originalRequestFingerprint" };
        if (source.Count != fields.Length || source.Any(pair => !fields.Contains(pair.Key, StringComparer.Ordinal)) ||
            source["schemaVersion"] is not JsonValue version || !version.TryGetValue<int>(out var schema) || schema != 1 ||
            source["turn"] is not JsonValue turnNode || !turnNode.TryGetValue<int>(out var turn) || turn <= 0 ||
            Exact(source, "realm") != "mortal_world" ||
            Exact(source, "sourceLabel") != QteSceneService.OrdinaryPlayerTurnSourceLabel ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(Exact(source, "originalRequestFingerprint")))
            throw Invalid("original_source_invalid");
        foreach (var field in fields.Where(static field => field is not ("schemaVersion" or "turn")))
            _ = Exact(source, field);
        var result = new QtePhysicalInjurySource(source);
        if (result.SessionId != continuation["sessionId"]?.GetValue<string>() ||
            result.Generation != continuation["sessionGeneration"]?.GetValue<string>() ||
            result.Turn != continuation["acceptedSourceTurn"]?.GetValue<int>())
            throw Invalid("original_source_binding_mismatch");
        return result;
    }

    private static PendingTurnSnapshotReadAuthority ReadOriginal(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease)
    {
        var result = PendingTurnSnapshotReader.ReadCurrent(files, lease,
            [ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath]);
        return result.Success && result.Snapshot is not null ? result.Snapshot :
            throw Invalid("original_snapshot_invalid");
    }

    private static string Exact(JsonObject root, string field)
    {
        if (root[field] is not JsonValue node || !node.TryGetValue<string>(out var text) ||
            !ResourceMaterializationContract.IsExactIdentifier(text)) throw Invalid("original_source_invalid");
        return text;
    }

    private static InvalidDataException Invalid(string code) =>
        new("qte_physical_injury_" + code + ": original accepted source authority is unavailable or changed.");
}
