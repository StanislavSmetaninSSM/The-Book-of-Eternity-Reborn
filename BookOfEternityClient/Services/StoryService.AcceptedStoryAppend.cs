using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public partial class StoryService
{
    private static readonly object AcceptedStoryWriterCapability = new();

    // Client-issued bookkeeping, carried by the existing trusted canonical QTE checkpoint.
    // Portable hashes check corruption/correlation; they are not anti-owner signatures.
    internal sealed class AcceptedStoryAppend
    {
        private readonly JsonObject _root;
        private readonly byte[] _after;
        private AcceptedStoryAppend(JsonObject root, byte[] after)
        {
            _root = root.DeepClone().AsObject();
            _after = after.ToArray();
        }
        internal string StoryPath => _root["storyPath"]!.GetValue<string>();
        internal JsonObject Persisted => _root.DeepClone().AsObject();
        internal byte[] AfterBytes => _after.ToArray();
        internal IReadOnlyDictionary<string, string> OriginalHistoryBindings =>
            _root["originalHistoryBindings"]!.AsObject().ToDictionary(pair => pair.Key, pair => pair.Value!.GetValue<string>(), StringComparer.OrdinalIgnoreCase);

        private static void Closed(JsonObject root, params string[] fields)
        {
            if (root.Count != fields.Length || root.Any(pair => !fields.Contains(pair.Key, StringComparer.Ordinal)))
                throw new InvalidDataException("qte_client_story_append_invalid: non-closed witness shape.");
        }
        private static string Exact(JsonObject root, string field)
        {
            var value = root[field]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(value) || value != value.Trim() || value.Any(char.IsControl))
                throw new InvalidDataException("qte_client_story_append_invalid: invalid " + field + ".");
            return value;
        }
        private static bool IsHistoryPath(string path) => path == "game_state/history/chat_log.json" ||
            (path.StartsWith("stories/", StringComparison.Ordinal) && path.EndsWith(".jsonl", StringComparison.Ordinal) &&
             path.Count(c => c == '/') == 1 && !path.Contains('\\') && !path.Contains("..", StringComparison.Ordinal));

        internal static AcceptedStoryAppend FromSuccessfulWriter(JsonObject source, JsonObject histories, string writerKind,
            string storyPath, byte[]? before, string row, byte[] after, object writerCapability)
        {
            // Called only after the actual writer's exact prefix+row readback succeeds.
            var beforeImage = new CanonicalBeforeImage(before != null, before);
            return Parse(new JsonObject
            {
                ["schemaVersion"] = 1, ["writerKind"] = writerKind, ["source"] = source.DeepClone(),
                ["storyPath"] = storyPath, ["originalHistoryBindings"] = histories.DeepClone(),
                ["before"] = new JsonObject { ["existed"] = beforeImage.Existed, ["bytesBase64"] = before == null ? null : Convert.ToBase64String(before), ["fingerprint"] = beforeImage.Fingerprint },
                ["rowJson"] = row,
                ["after"] = new JsonObject { ["byteLength"] = after.Length, ["fingerprint"] = new CanonicalBeforeImage(true, after).Fingerprint }
            }, writerCapability);
        }

        // Only the QTE owner invokes this after validating its canonical runtime/continuation crosslink.
        // No GM input route deserializes this node into a witness.
        internal static AcceptedStoryAppend Parse(JsonObject root, object writerCapability)
        {
            if (!ReferenceEquals(writerCapability, AcceptedStoryWriterCapability))
                throw new InvalidOperationException("Only the accepted client story writer or validated QTE checkpoint can issue this witness.");
            Closed(root, "schemaVersion", "writerKind", "source", "storyPath", "originalHistoryBindings", "before", "rowJson", "after");
            if (root["schemaVersion"]!.GetValue<int>() != 1 || Exact(root, "writerKind") is not ("console_turn" or "browser_turn"))
                throw new InvalidDataException("qte_client_story_append_invalid: unsupported schema/writer.");
            var source = root["source"]!.AsObject();
            Closed(source, "sessionId", "requestId", "snapshotToken", "turnNumber", "sourceLabel", "sessionGeneration");
            foreach (var field in new[] { "sessionId", "requestId", "snapshotToken", "sourceLabel", "sessionGeneration" }) _ = Exact(source, field);
            if (source["turnNumber"]!.GetValue<int>() <= 0 || !QteSceneService.IsEligibleOfferSourceLabel(Exact(source, "sourceLabel")))
                throw new InvalidDataException("qte_client_story_append_invalid: invalid original source.");
            var before = root["before"]!.AsObject();
            Closed(before, "existed", "bytesBase64", "fingerprint");
            var existed = before["existed"]!.GetValue<bool>();
            var payload = before["bytesBase64"]?.GetValue<string>();
            if (existed != (payload != null)) throw new InvalidDataException("qte_client_story_append_invalid: before existence differs.");
            var bytes = payload == null ? null : Convert.FromBase64String(payload);
            if (Exact(before, "fingerprint") != new CanonicalBeforeImage(existed, bytes).Fingerprint)
                throw new InvalidDataException("qte_client_story_append_invalid: before fingerprint differs.");
            var row = root["rowJson"]!.GetValue<string>();
            var rowNode = StrictJsonAuthority.Deserialize<JsonObject>(row, JsonOpts, "accepted client story row")!;
            if (rowNode.Any(pair => pair.Key is not ("requestId" or "sessionGeneration" or "turn" or "timestamp" or "realm" or "player" or "narrative" or "location" or "entityRefs")))
                throw new InvalidDataException("qte_client_story_append_invalid: unknown story field.");
            var entry = StrictJsonAuthority.Deserialize<StoryEntry>(row, JsonOpts, "accepted client story row")!;
            if (entry.Turn != source["turnNumber"]!.GetValue<int>() || string.IsNullOrWhiteSpace(entry.Realm) ||
                string.IsNullOrWhiteSpace(entry.Player) || !DateTimeOffset.TryParse(entry.Timestamp, out _) ||
                row != JsonSerializer.Serialize(entry, JsonOpts))
                throw new InvalidDataException("qte_client_story_append_invalid: story row/source differs.");
            if (Exact(root, "writerKind") == "console_turn" && (entry.RequestId != null || entry.SessionGeneration != null))
                throw new InvalidDataException("qte_client_story_append_invalid: console row gained browser coordinates.");
            if (Exact(root, "writerKind") == "browser_turn" && (entry.RequestId != Exact(source, "requestId") || entry.SessionGeneration != Exact(source, "sessionGeneration")))
                throw new InvalidDataException("qte_client_story_append_invalid: browser row/source differs.");
            var path = Exact(root, "storyPath");
            if (!IsHistoryPath(path) || !path.StartsWith("stories/", StringComparison.Ordinal))
                throw new InvalidDataException("qte_client_story_append_invalid: invalid story path.");
            var histories = root["originalHistoryBindings"]!.AsObject();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in histories)
                if (!IsHistoryPath(pair.Key) || !names.Add(pair.Key) || pair.Value is not JsonValue || pair.Value.GetValue<string>() is not { } hash ||
                    (hash.Length != 0 && (hash.Length != 64 || hash.Any(c => !Uri.IsHexDigit(c)))))
                    throw new InvalidDataException("qte_client_story_append_invalid: invalid original history binding.");
            if (!histories.ContainsKey("game_state/history/chat_log.json")) throw new InvalidDataException("qte_client_story_append_invalid: history coverage absent.");
            var baselineHash = histories[path]?.GetValue<string>();
            if (existed != (baselineHash != null) || (existed && baselineHash != PendingTurnSnapshotAuthority.ComputeSha256(Decode(bytes!))))
                throw new InvalidDataException("qte_client_story_append_invalid: prefix differs from original history binding.");
            var after = (bytes ?? Encoding.UTF8.GetPreamble()).Concat(Encoding.UTF8.GetBytes(row + "\n")).ToArray();
            var afterNode = root["after"]!.AsObject();
            Closed(afterNode, "byteLength", "fingerprint");
            if (afterNode["byteLength"]!.GetValue<int>() != after.Length || Exact(afterNode, "fingerprint") != new CanonicalBeforeImage(true, after).Fingerprint)
                throw new InvalidDataException("qte_client_story_append_invalid: exact append afterimage differs.");
            return new AcceptedStoryAppend(root, after);
        }

        internal async Task ValidateQteBindingAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
            string sessionId, string generation, int turn, JsonObject? sealedRequest, JsonObject? sealedSoul)
        {
            var source = _root["source"]!.AsObject();
            var row = StrictJsonAuthority.Deserialize<StoryEntry>(_root["rowJson"]!.GetValue<string>(), JsonOpts, "accepted client story row")!;
            if (Exact(source, "sessionId") != sessionId || Exact(source, "sessionGeneration") != generation || source["turnNumber"]!.GetValue<int>() != turn ||
                !files.IsCurrentSessionGeneration(lease, generation) || sealedRequest == null || sealedSoul == null ||
                sealedRequest["sessionId"]?.GetValue<string>() != sessionId || sealedRequest["requestId"]?.GetValue<string>() != Exact(source, "requestId") ||
                sealedRequest["turnNumber"]?.GetValue<int>() != turn || sealedRequest["playerAction"]?.GetValue<string>() != row.Player ||
                sealedSoul["currentRealm"]?.GetValue<string>() != row.Realm ||
                StoryPath != GetStoryPath(row.Realm!, sealedSoul["currentIncarnation"]?.GetValue<int>() ?? 0))
                throw new InvalidDataException("qte_client_story_append_source_mismatch: exact original source/path/owner differs.");
            if (files.FileExists("game_state/control/pending_turn_snapshot.json"))
            {
                var original = PendingTurnSnapshotReader.ReadCurrent(files, lease, ["game_state/meta/soul_state.json"]);
                if (!original.Success || original.Snapshot!.SessionId != sessionId || original.Snapshot.RequestId != Exact(source, "requestId") ||
                    original.Snapshot.SnapshotToken != Exact(source, "snapshotToken") || original.Snapshot.TurnNumber != turn)
                    throw new InvalidDataException("qte_client_story_append_source_mismatch: original source no longer validates.");
                var manifest = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>((await files.ReadFileAsync(lease, "game_state/control/pending_turn_snapshot.json"))!, LiveTurnPreparationService.ManifestJsonOptions, "original pending turn manifest")!;
                if (manifest.SourceLabel != Exact(source, "sourceLabel") || !JsonNode.DeepEquals(JsonSerializer.SerializeToNode(manifest.ClientOwnedValidationHashes), _root["originalHistoryBindings"]))
                    throw new InvalidDataException("qte_client_story_append_source_mismatch: original history binding differs.");
            }
            var actual = await files.ReadFileBytesAsync(lease, StoryPath);
            if (actual == null || !actual.AsSpan().SequenceEqual(_after))
                throw new InvalidDataException("client_owned_story_append_afterimage_mismatch at " + StoryPath);
        }

        private static string Decode(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
    }

    internal static async Task<AcceptedStoryAppend?> RehydrateQteStoryAppendAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, JsonObject continuation)
    {
        if (!continuation.ContainsKey("clientStoryAppend")) return null;
        // Trusted local canonical checkpoint model: require the actual current pair, not caller-supplied JSON/hash.
        var persisted = StrictJsonAuthority.Deserialize<JsonObject>((await files.ReadFileAsync(lease, QteDeferredEffectContinuation.StatePath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "canonical QTE checkpoint")!;
        var runtime = StrictJsonAuthority.Deserialize<JsonObject>((await files.ReadFileAsync(lease, QteSceneService.QteRuntimePath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "canonical QTE checkpoint")!;
        if (!JsonNode.DeepEquals(persisted, continuation) || runtime["activeScene"]?["deferredEffectContinuationId"]?.GetValue<string>() != continuation["continuationId"]?.GetValue<string>() ||
            runtime["activeScene"]?["deferredEffectContinuationFingerprint"]?.GetValue<string>() != continuation["authorityFingerprint"]?.GetValue<string>())
            throw new InvalidDataException("qte_deferred_continuation_runtime_mismatch: story witness requires the actual canonical QTE pair.");
        var proof = AcceptedStoryAppend.Parse(continuation["clientStoryAppend"]!.AsObject(), AcceptedStoryWriterCapability);
        JsonObject? Sealed(string path)
        {
            var binding = continuation["sealedRootBindings"]!.AsArray().Select(node => node!.AsObject()).SingleOrDefault(node => node["path"]?.GetValue<string>() == path);
            if (binding?["existed"]?.GetValue<bool>() != true) return null;
            return StrictJsonAuthority.Deserialize<JsonObject>(Encoding.UTF8.GetString(Convert.FromBase64String(binding["payloadBase64"]!.GetValue<string>())).TrimStart('\uFEFF'), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "sealed QTE source");
        }
        await proof.ValidateQteBindingAsync(files, lease, continuation["sessionId"]!.GetValue<string>(),
            continuation["sessionGeneration"]!.GetValue<string>(), continuation["acceptedSourceTurn"]!.GetValue<int>(),
            Sealed("input/turn_request.json"), Sealed("game_state/meta/soul_state.json"));
        return proof;
    }

    private async Task<(JsonObject Source, JsonObject Histories)?> CaptureAcceptedQteStorySourceAsync(
        FileSystemManager.CanonicalWriteLease lease, int turn, string realm, int incarnation, string action, string path, byte[]? before)
    {
        if (!_fs.FileExists(QteSceneService.QteOfferPath)) return null;
        var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, ["game_state/meta/soul_state.json"]);
        if (!read.Success) throw new InvalidDataException("qte_client_story_append_source_mismatch: original snapshot is invalid.");
        var manifest = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>((await _fs.ReadFileAsync(lease, "game_state/control/pending_turn_snapshot.json"))!, LiveTurnPreparationService.ManifestJsonOptions, "original pending turn manifest")!;
        if (!QteSceneService.IsEligibleOfferSourceLabel(manifest.SourceLabel) || read.Snapshot!.Realm != "mortal_world") return null;
        var soul = StrictJsonAuthority.Deserialize<JsonObject>(Encoding.UTF8.GetString(read.Snapshot!.ReadRequiredBytes("game_state/meta/soul_state.json")).TrimStart('\uFEFF'), SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "original soul source")!;
        if (read.Snapshot.TurnNumber != turn || manifest.PlayerAction != action || soul["currentRealm"]?.GetValue<string>() != realm ||
            (soul["currentIncarnation"]?.GetValue<int>() ?? 0) != incarnation || path != GetStoryPath(realm, incarnation))
            throw new InvalidDataException("qte_client_story_append_source_mismatch: accepted source inputs differ.");
        var originalHash = manifest.ClientOwnedValidationHashes.GetValueOrDefault(path);
        var currentText = await _fs.ReadFileAsync(lease, path);
        if ((before != null) != (originalHash != null) || (before != null && originalHash != PendingTurnSnapshotAuthority.ComputeSha256(currentText!)))
            throw new InvalidDataException("qte_client_story_append_source_mismatch: story was changed before client append.");
        return (new JsonObject { ["sessionId"] = read.Snapshot.SessionId, ["requestId"] = read.Snapshot.RequestId,
            ["snapshotToken"] = read.Snapshot.SnapshotToken, ["turnNumber"] = turn, ["sourceLabel"] = manifest.SourceLabel,
            ["sessionGeneration"] = _fs.GetOrCreateSessionGeneration(lease) }, JsonSerializer.SerializeToNode(manifest.ClientOwnedValidationHashes)!.AsObject());
    }

    internal async Task<AcceptedStoryAppend?> AppendAcceptedQteSourceTurnAsync(int turn, string realm, int incarnation,
        string action, string? narrative, string? location, IReadOnlyCollection<StoryEntityRef>? refs)
    {
        if (!_fs.FileExists(QteSceneService.QteOfferPath))
        {
            await AppendTurnAsync(turn, realm, incarnation, action, narrative, location, refs);
            return null;
        }
        var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        CoordinatedStatePublicationUncertainException? owningUncertainty = null;
        try { return await AppendAcceptedQteSourceTurnCoreAsync(lease, turn, realm, incarnation, action, narrative, location, refs); }
        catch (CoordinatedStatePublicationUncertainException failure) { owningUncertainty = failure; throw; }
        finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, lease, false, owningUncertainty); }
    }

    private async Task<AcceptedStoryAppend?> AppendAcceptedQteSourceTurnCoreAsync(FileSystemManager.CanonicalWriteLease lease,
        int turn, string realm, int incarnation, string action, string? narrative, string? location, IReadOnlyCollection<StoryEntityRef>? refs)
    {
        var path = GetStoryPath(realm, incarnation);
        var before = await _fs.ReadFileBytesAsync(lease, path);
        var source = await CaptureAcceptedQteStorySourceAsync(lease, turn, realm, incarnation, action, path, before);
        if (source == null)
        {
            await AppendTurnAsync(turn, realm, incarnation, action, narrative, location, refs);
            return null;
        }
        var row = JsonSerializer.Serialize(new StoryEntry { Turn = turn, Timestamp = DateTime.UtcNow.ToString("o"), Realm = realm,
            Player = action, Narrative = narrative ?? "", Location = location, EntityRefs = refs?.Where(reference => reference != null).ToList() }, JsonOpts);
        try
        {
            await _fs.AppendFileAtomicAsync(lease, path, row + "\n");
            var actual = await _fs.ReadFileBytesAsync(lease, path) ?? throw new IOException("Accepted story readback is absent.");
            var expected = (before ?? Encoding.UTF8.GetPreamble()).Concat(Encoding.UTF8.GetBytes(row + "\n")).ToArray();
            if (!actual.AsSpan().SequenceEqual(expected)) throw new IOException("Accepted story readback differs from exact prefix+row.");
            return AcceptedStoryAppend.FromSuccessfulWriter(source.Value.Source, source.Value.Histories, "console_turn", path, before, row, actual, AcceptedStoryWriterCapability);
        }
        catch (SessionReplacedException) { throw; }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException)
        {
            _logger.LogWarning(failure, "Failed to append accepted QTE source story for turn {Turn}", turn);
            return null;
        }
    }
}
