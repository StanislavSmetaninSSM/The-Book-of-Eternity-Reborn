using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class StoryService
{
    private static readonly object MortalJointStoryIssuer = new();
    internal enum MortalJointStoryDisposition { OriginalUnappended, AppendedAndVerified, AlreadyAppended, Refused, PublicationUncertain }
    internal sealed record MortalJointStoryResult(MortalJointStoryDisposition Disposition, MortalJointStoryProof? Proof, string? Failure);

    // Only this owner can mint the suffix proof after current original/full-union
    // validation and exact story readback. It is never parsed from a GM packet,
    // diagnostic marker or old console/browser/QTE append witness.
    internal sealed class MortalJointStoryProof
    {
        private readonly StoryService _owner;
        private readonly FileSystemManager.CanonicalWriteLease _lease;
        private readonly MortalQteDecisionPublication.JointProgressionPhaseCapability _applied;
        private readonly byte[] _after;
        private readonly Dictionary<string, string> _histories;
        internal MortalJointStoryProof(object issuer, StoryService owner, FileSystemManager.CanonicalWriteLease lease,
            MortalQteDecisionPublication.JointProgressionPhaseCapability applied, string path, string row,
            byte[] after, IReadOnlyDictionary<string, string> histories)
        {
            if (!ReferenceEquals(issuer, MortalJointStoryIssuer)) throw new InvalidOperationException("The joint story owner must issue its proof.");
            _owner = owner; _lease = lease; _applied = applied; StoryPath = path; RowJson = row;
            _after = after.ToArray(); _histories = histories.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        }
        internal string StoryPath { get; }
        internal string RowJson { get; }
        internal byte[] AfterBytes => _after.ToArray();
        internal IReadOnlyDictionary<string, string> OriginalHistoryBindings =>
            new Dictionary<string, string>(_histories, StringComparer.OrdinalIgnoreCase);
        internal bool MatchesOriginal(string sessionId, string requestId, string snapshotToken, int turn) =>
            _applied.SessionIdForOwner == sessionId && _applied.RequestIdForOwner == requestId &&
            _applied.SnapshotTokenForOwner == snapshotToken && _applied.TurnForOwner == turn;
        internal async Task ValidateForHistoryValidationAsync(FileSystemManager files)
        {
            if (!ReferenceEquals(files, _owner._fs)) throw new InvalidDataException("joint_story_history_foreign_root");
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            // The old lease-local capability is only a comparison constraint.
            // Reconstruct actual original/G/union/phase authority on this lease.
            var fresh = await MortalQteDecisionPublication.ReadJointProgressionPhaseAsync(files, lease,
                _applied.SessionIdForOwner, _applied.RequestIdForOwner, _applied.SnapshotTokenForOwner, _applied.TurnForOwner);
            if (fresh.Phase != "applied" || !SameJointStoryBytes(fresh.MarkerBefore, _applied.MarkerBefore))
                throw new InvalidDataException("joint_story_history_completed_owner_changed");
            var current = await _owner.ReadMortalJointStorySourceAsync(lease, fresh);
            RequireSameCurrent(current);
        }
        private void RequireSameCurrent(JointStorySource current)
        {
            if (!current.AlreadyAppended || current.Path != StoryPath || current.Row != RowJson ||
                !SameJointStoryBytes(current.Current, _after) || current.Histories.Count != _histories.Count ||
                current.Histories.Any(pair => !_histories.TryGetValue(pair.Key, out var hash) || hash != pair.Value))
                throw new InvalidDataException("joint_story_proof_current_changed");
        }
        internal async Task ValidateCurrentAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
        {
            if (!ReferenceEquals(files, _owner._fs) || !ReferenceEquals(lease, _lease))
                throw new InvalidDataException("joint_story_proof_owner_changed");
            var current = await _owner.ReadMortalJointStorySourceAsync(lease, _applied);
            RequireSameCurrent(current);
        }
    }

    internal Task<MortalJointStoryResult> ReadMortalJointStoryAsync(FileSystemManager.CanonicalWriteLease lease,
        MortalQteDecisionPublication.JointProgressionPhaseCapability applied) => CompleteMortalJointStoryAsync(lease, applied, append: false);
    internal Task<MortalJointStoryResult> AppendMortalJointStoryAsync(FileSystemManager.CanonicalWriteLease lease,
        MortalQteDecisionPublication.JointProgressionPhaseCapability applied) => CompleteMortalJointStoryAsync(lease, applied, append: true);

    /// <summary>
    /// Reads authenticated current story data, optionally appends the exact row, then verifies readback.
    /// </summary>
    /// <param name="lease">
    /// The active participating lease used by the supplied phase and this story owner.
    /// </param>
    /// <param name="applied">
    /// A freshly issued applied-phase capability for this filesystem and lease.
    /// </param>
    /// <param name="append">
    /// Whether an unappended row may be written; <see langword="false"/> only observes its current disposition.
    /// </param>
    /// <returns>
    /// The actual story disposition and current proof when verified, or the existing refusal/uncertainty result.
    /// </returns>
    private async Task<MortalJointStoryResult> CompleteMortalJointStoryAsync(FileSystemManager.CanonicalWriteLease lease,
        MortalQteDecisionPublication.JointProgressionPhaseCapability applied, bool append)
    {
        var publicationAttempted = false;
        try
        {
            using var t318Timer1 = _fs.MeasureOperation("JointStoryComplete.ReadInitialSourceAsync");
            var source = await ReadMortalJointStorySourceAsync(lease, applied);
            t318Timer1?.Dispose();
            if (!source.AlreadyAppended && !append) return new(MortalJointStoryDisposition.OriginalUnappended, null, null);
            if (!source.AlreadyAppended)
            {
                // Check exact before-images again immediately before the existing
                // trusted append. No new lease or independent mechanical writer.
                using var t318Timer2 = _fs.MeasureOperation("JointStoryComplete.ReadBeforeAppendSourceAsync");
                var fresh = await ReadMortalJointStorySourceAsync(lease, applied);
                t318Timer2?.Dispose();
                if (fresh.AlreadyAppended || fresh.Path != source.Path || fresh.Row != source.Row ||
                    !SameJointStoryBytes(fresh.Current, source.Current)) throw new InvalidDataException("joint_story_before_changed");
                publicationAttempted = true;
                using var t318Timer3 = _fs.MeasureOperation("JointStoryComplete.AppendAtomicAsync");
                await _fs.AppendFileAtomicAsync(lease, source.Path, source.Row + "\n");
                t318Timer3?.Dispose();
            }
            using var t318Timer4 = _fs.MeasureOperation("JointStoryComplete.ReadBackSourceAsync");
            var readback = await ReadMortalJointStorySourceAsync(lease, applied);
            t318Timer4?.Dispose();
            if (!readback.AlreadyAppended || readback.Path != source.Path || readback.Row != source.Row ||
                !SameJointStoryBytes(readback.Current, source.After)) throw new InvalidDataException("joint_story_exact_readback_failed");
            var proof = new MortalJointStoryProof(MortalJointStoryIssuer, this, lease, applied,
                source.Path, source.Row, source.After, source.Histories);
            using var t318Timer5 = _fs.MeasureOperation("JointStoryComplete.ValidateIssuedProofAsync");
            await proof.ValidateCurrentAsync(_fs, lease);
            t318Timer5?.Dispose();
            return new(source.AlreadyAppended ? MortalJointStoryDisposition.AlreadyAppended :
                MortalJointStoryDisposition.AppendedAndVerified, proof, null);
        }
        catch (SessionReplacedException) { throw; }
        catch (CoordinatedStatePublicationUncertainException error)
        {
            return new(MortalJointStoryDisposition.PublicationUncertain, null, error.Message);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or FormatException)
        {
            // Never restore the union, release/rearm a claim or erase the ready,
            // original, outputs or backup on a failed/uncertain story operation.
            return new(publicationAttempted ? MortalJointStoryDisposition.PublicationUncertain :
                MortalJointStoryDisposition.Refused, null, error.Message);
        }
    }

    private sealed record JointStorySource(string Path, string Row, byte[]? Current, byte[] After,
        bool AlreadyAppended, IReadOnlyDictionary<string, string> Histories);

    /// <summary>
    /// Verifies the current original, output and histories against the supplied phase and computes exact story data.
    /// </summary>
    /// <param name="lease">
    /// The active participating lease for all reads and current checks.
    /// </param>
    /// <param name="applied">
    /// The existing phase capability, revalidated against current production data before and after the source reads.
    /// </param>
    /// <returns>
    /// Verified current row, before/after bytes and history bindings; no append is performed here.
    /// </returns>
    private async Task<JointStorySource> ReadMortalJointStorySourceAsync(FileSystemManager.CanonicalWriteLease lease,
        MortalQteDecisionPublication.JointProgressionPhaseCapability applied)
    {
        using var t318Timer6 = _fs.MeasureOperation("JointStorySource.VerifyCurrentSessionOperation");
        _fs.VerifyCurrentSessionOperation(lease);
        t318Timer6?.Dispose();
        using var t318Timer8 = _fs.MeasureOperation("JointStorySource.ValidateAppliedBeforeAsync");
        await applied.ValidateCurrentAsync(_fs, lease);
        t318Timer8?.Dispose();
        if (applied.Phase != "applied") throw new InvalidDataException("joint_story_progression_not_applied");
        using var t318Timer7 = _fs.MeasureOperation("JointStorySource.ReadOriginal");
        var read = PendingTurnSnapshotReader.ReadCurrent(_fs, lease, ["game_state/meta/soul_state.json"]);
        t318Timer7?.Dispose();
        if (!read.Success || read.Snapshot is not { } original || original.Realm != "mortal_world" ||
            original.SessionId != applied.SessionIdForOwner || original.RequestId != applied.RequestIdForOwner ||
            original.SnapshotToken != applied.SnapshotTokenForOwner || original.TurnNumber != applied.TurnForOwner)
            throw new InvalidDataException("joint_story_original_binding");
        using var t318Timer10 = _fs.MeasureOperation("JointStorySource.ReadManifestAsync");
        var manifest = StrictJsonAuthority.Deserialize<LiveTurnPendingSnapshotManifest>(
            (await _fs.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath)) ??
                throw new InvalidDataException("joint_story_manifest_missing"), LiveTurnPreparationService.ManifestJsonOptions,
            "joint actual original manifest") ?? throw new InvalidDataException("joint_story_manifest_null");
        t318Timer10?.Dispose();
        using var t318Timer11 = _fs.MeasureOperation("JointStorySource.ReadSourceObjectsAsync");
        var beforeSoul = ReadJointStoryObject(original.ReadRequiredBytes("game_state/meta/soul_state.json"), "joint original soul");
        var images = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        async Task<JsonObject?> ReadObject(string path)
        {
            var bytes = await _fs.ReadFileBytesAsync(lease, path); images[path] = bytes?.ToArray();
            return bytes is null ? null : ReadJointStoryObject(bytes, path);
        }
        var soul = await ReadObject("game_state/meta/soul_state.json") ?? throw new InvalidDataException("joint_story_soul_missing");
        var realm = ReadJointStoryString(beforeSoul, "currentRealm") ?? throw new InvalidDataException("joint_story_original_realm_missing");
        var incarnation = ReadJointStoryIncarnation(beforeSoul);
        if (ReadJointStoryString(soul, "currentRealm") != realm || ReadJointStoryIncarnation(soul) != incarnation ||
            string.IsNullOrWhiteSpace(manifest.PlayerAction)) throw new InvalidDataException("joint_story_current_realm_action_changed");
        var narrativeRoot = await ReadObject("output/narrative_response.json") ?? throw new InvalidDataException("joint_story_narrative_missing");
        var narrative = PlayerFacingTextNormalizer.NormalizeEscapedLineBreakArtifacts(ReadJointStoryString(narrativeRoot, "response"));
        if (string.IsNullOrWhiteSpace(narrative)) throw new InvalidDataException("joint_story_narrative_empty");
        t318Timer11?.Dispose();
        using var t318Timer12 = _fs.MeasureOperation("JointStorySource.BindContinuationAsync");
        var bound = await MortalQteDecisionPublication.TryBindExactJointContinuationAsync(_fs, lease,
            original.SessionId, original.RequestId, original.SnapshotToken, original.TurnNumber);
        if (bound is null || !bound.Success) throw new InvalidDataException("joint_story_owned_output_refused");
        t318Timer12?.Dispose();
        using var t318Timer13 = _fs.MeasureOperation("JointStorySource.ReadReferencesAsync");
        _ = await ReadObject(JointStoryRowInputs.LocationPath) ?? throw new InvalidDataException("joint_story_location_missing");
        // Keep conditional reads and their validation order behind actual output proof.
        foreach (var referencePath in GetMortalJointStoryReferenceReadPaths(manifest.PlayerAction))
            await ReadObject(referencePath);
        t318Timer13?.Dispose();
        using var t318Timer14 = _fs.MeasureOperation("JointStorySource.CalculateRow");
        var generation = _fs.ReadLocalGenerationSnapshot(lease).Binding.Id;
        var calculation = CalculateMortalJointStoryRow(new(generation, original.RequestId, original.TurnNumber,
            realm, incarnation, manifest.PlayerAction, applied.PreparedAtUtcForOwner,
            images[JointStoryRowInputs.SoulPath]!, images[JointStoryRowInputs.NarrativePath]!, images[JointStoryRowInputs.LocationPath]!,
            images.GetValueOrDefault(JointStoryRowInputs.GuardiansPath), images.GetValueOrDefault(JointStoryRowInputs.ResidentsPath),
            images.GetValueOrDefault(JointStoryRowInputs.NpcPath)));
        var row = calculation.RowJson;
        var path = calculation.Path;
        t318Timer14?.Dispose();
        using var t318Timer15 = _fs.MeasureOperation("JointStorySource.ClassifyHistoryAsync");
        var histories = manifest.ClientOwnedValidationHashes ?? throw new InvalidDataException("joint_story_original_histories_missing");
        RequireJointStoryHistoryInventory(histories);
        var current = await _fs.ReadFileBytesAsync(lease, path);
        var (prefix, already) = ClassifyJointStoryPrefix(path, current, row, histories, original.TurnNumber, original.RequestId, generation);
        var after = (prefix ?? Encoding.UTF8.GetPreamble()).Concat(Encoding.UTF8.GetBytes(row + "\n")).ToArray();
        t318Timer15?.Dispose();
        using var t318Timer16 = _fs.MeasureOperation("JointStorySource.VerifyOtherHistoriesAsync");
        foreach (var history in histories.Where(pair => !string.Equals(pair.Key, path, StringComparison.OrdinalIgnoreCase)))
        {
            var bytes = await _fs.ReadFileBytesAsync(lease, history.Key);
            if (!MatchesJointStoryOriginalText(history.Value, bytes)) throw new InvalidDataException("joint_story_unrelated_history_changed: " + history.Key);
        }
        t318Timer16?.Dispose();
        using var t318Timer17 = _fs.MeasureOperation("JointStorySource.VerifyHistoryInventory");
        var actualNames = GameEngine.SelectOriginalStoryContinuityFiles(_fs.EnumerateFiles(lease, "*"));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in actualNames)
        {
            if (!names.Add(name) || (string.Equals(name, path, StringComparison.OrdinalIgnoreCase) && name != path))
                throw new InvalidDataException("joint_story_ambiguous_history: " + name);
            if (!string.Equals(name, path, StringComparison.OrdinalIgnoreCase) && !histories.ContainsKey(name))
                throw new InvalidDataException("joint_story_unlisted_history: " + name);
        }
        t318Timer17?.Dispose();
        using var t318Timer18 = _fs.MeasureOperation("JointStorySource.RecheckSourceImagesAsync");
        foreach (var image in images)
            if (!SameJointStoryBytes(image.Value, await _fs.ReadFileBytesAsync(lease, image.Key)))
                throw new InvalidDataException("joint_story_output_source_changed: " + image.Key);
        t318Timer18?.Dispose();
        using var t318Timer9 = _fs.MeasureOperation("JointStorySource.ValidateAppliedAfterAsync");
        await applied.ValidateCurrentAsync(_fs, lease);
        t318Timer9?.Dispose();
        return new(path, row, current, after, already, new Dictionary<string, string>(histories, StringComparer.OrdinalIgnoreCase));
    }

    private static (byte[]? Prefix, bool Appended) ClassifyJointStoryPrefix(string path, byte[]? current, string row,
        IReadOnlyDictionary<string, string> histories, int turn, string requestId, string generation)
    {
        var expectedHash = histories.GetValueOrDefault(path);
        byte[]? prefix;
        bool appended;
        if (MatchesJointStoryOriginalText(expectedHash, current)) { prefix = current?.ToArray(); appended = false; }
        else
        {
            var suffix = Encoding.UTF8.GetBytes(row + "\n");
            if (current is null || current.Length < suffix.Length || !current.AsSpan(current.Length - suffix.Length).SequenceEqual(suffix))
                throw new InvalidDataException("joint_story_suffix_not_exact");
            var rawPrefix = current.AsSpan(0, current.Length - suffix.Length).ToArray();
            if (expectedHash is null or "")
            {
                if (!rawPrefix.AsSpan().SequenceEqual(Encoding.UTF8.GetPreamble())) throw new InvalidDataException("joint_story_absent_prefix_changed");
                prefix = null;
            }
            else
            {
                if (!MatchesJointStoryOriginalText(expectedHash, rawPrefix)) throw new InvalidDataException("joint_story_original_prefix_hash");
                prefix = rawPrefix;
            }
            appended = true;
        }
        if (prefix is not null && CanonicalJsonUtf8.DecodeOneOptionalBom(prefix).Length != 0 && prefix[^1] != (byte)'\n')
            throw new InvalidDataException("joint_story_prefix_line_not_terminated");
        if (prefix is not null)
            foreach (var line in CanonicalJsonUtf8.DecodeOneOptionalBom(prefix).Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var prior = StrictJsonAuthority.Deserialize<StoryEntry>(line, JsonOpts, "joint original story prefix") ??
                    throw new InvalidDataException("joint_story_prefix_null_row");
                if (prior.Turn >= turn || (prior.RequestId == requestId && prior.SessionGeneration == generation))
                    throw new InvalidDataException("joint_story_original_contains_decision_turn");
            }
        return (prefix, appended);
    }

    private static bool MatchesJointStoryOriginalText(string? hash, byte[]? bytes) =>
        string.IsNullOrEmpty(hash) ? bytes is null : bytes is not null && string.Equals(hash,
            PendingTurnSnapshotAuthority.ComputeSha256(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes)), StringComparison.OrdinalIgnoreCase);

    private static void RequireJointStoryHistoryInventory(IReadOnlyDictionary<string, string> histories)
    {
        if (!histories.ContainsKey("game_state/history/chat_log.json")) throw new InvalidDataException("joint_story_chat_binding_missing");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in histories)
            if (pair.Value is null || !names.Add(pair.Key) || (pair.Key != "game_state/history/chat_log.json" &&
                !(pair.Key.StartsWith("stories/", StringComparison.OrdinalIgnoreCase) && pair.Key.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))) ||
                pair.Key.Contains('\\') || pair.Key.Split('/').Any(part => part is "" or "." or "..") ||
                (pair.Value.Length != 0 && (pair.Value.Length != 64 || pair.Value.Any(c => !Uri.IsHexDigit(c)))))
                throw new InvalidDataException("joint_story_original_history_inventory");
    }

    private static JsonObject ReadJointStoryObject(byte[] bytes, string label) =>
        StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, label) ?? throw new InvalidDataException("joint_story_null_root: " + label);
    private static string? ReadJointStoryString(JsonObject root, string name) =>
        root[name] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
    private static int ReadJointStoryIncarnation(JsonObject root)
    {
        if (root["currentIncarnation"] is null) return 0; // same ordinary GetStoryPath fallback
        if (root["currentIncarnation"] is JsonValue value && value.TryGetValue<int>(out var incarnation) && incarnation >= 0) return incarnation;
        throw new InvalidDataException("joint_story_incarnation_invalid");
    }
    private static bool SameJointStoryBytes(byte[]? left, byte[]? right) =>
        left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);

    private static (string? GuardianId, string? ResidentId, string? NpcId) ReadMortalJointStoryTags(string action) =>
        (ReadJointStoryTag(action, "guardianId") ?? ReadJointStoryTag(action, "sourceGuardianId"),
         ReadJointStoryTag(action, "residentId") ?? ReadJointStoryTag(action, "sourceResidentId"),
         ReadJointStoryTag(action, "npcId") ?? ReadJointStoryTag(action, "NPCId") ?? ReadJointStoryTag(action, "sourceNpcId"));

    private static IEnumerable<string> GetMortalJointStoryReferenceReadPaths(string action)
    {
        var tags = ReadMortalJointStoryTags(action);
        if (tags.GuardianId is not null) yield return JointStoryRowInputs.GuardiansPath;
        if (tags.ResidentId is not null) yield return JointStoryRowInputs.ResidentsPath;
        if (tags.NpcId is not null) yield return JointStoryRowInputs.NpcPath;
    }

    private static List<StoryEntityRef>? ReadMortalJointStoryRefs(string action, Func<string, JsonObject?> read)
    {
        var refs = new List<StoryEntityRef>();
        var (guardianId, residentId, npcId) = ReadMortalJointStoryTags(action);
        if (guardianId is not null)
        {
            var root = read(JointStoryRowInputs.GuardiansPath);
            var guardian = root?["activeGuardian"] as JsonObject;
            if (guardian is null || !string.Equals(ReadJointStoryString(guardian, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase))
                guardian = (root?["guardians"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(item =>
                    string.Equals(ReadJointStoryString(item, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase));
            refs.Add(new() { EntityType = "guardian", EntityId = guardianId, DisplayName = guardian is null ? null :
                GuardianManifestation.GetDisplayName(guardian) ?? ReadJointStoryString(guardian, "canonicalName") ?? ReadJointStoryString(guardian, "name") });
        }
        if (residentId is not null)
        {
            var root = read(JointStoryRowInputs.ResidentsPath);
            var resident = (root?["entries"] as JsonArray)?.OfType<JsonObject>().FirstOrDefault(item =>
                string.Equals(ReadJointStoryString(item, "residentId"), residentId, StringComparison.OrdinalIgnoreCase));
            refs.Add(new() { EntityType = "resident", EntityId = residentId, DisplayName = resident is null ? null : ReadJointStoryString(resident, "displayName") });
        }
        if (npcId is not null)
        {
            var root = read(JointStoryRowInputs.NpcPath);
            JsonObject? npc = null;
            if (root is not null)
            {
                using var document = JsonDocument.Parse(root.ToJsonString());
                foreach (var item in GuardianPolicyContracts.EnumerateCanonicalNpcObjects(document.RootElement))
                    if (string.Equals(JointNpcText(item, "NPCId") ?? JointNpcText(item, "npcId") ?? JointNpcText(item, "id"), npcId, StringComparison.OrdinalIgnoreCase))
                    { npc = JsonNode.Parse(item.GetRawText())!.AsObject(); break; }
            }
            refs.Add(new() { EntityType = "npc", EntityId = npcId, DisplayName = npc is null ? null :
                ReadJointStoryString(npc, "NPCName") ?? ReadJointStoryString(npc, "npcName") ?? ReadJointStoryString(npc, "name") ?? ReadJointStoryString(npc, "displayName") });
        }
        return refs.Count == 0 ? null : refs.GroupBy(item => item.EntityType + ":" + item.EntityId, StringComparer.OrdinalIgnoreCase).Select(group => group.First()).ToList();
    }
    private static string? JointNpcText(JsonElement root, string name) => root.TryGetProperty(name, out var node) &&
        node.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(node.GetString()) ? node.GetString() : null;
    private static string? ReadJointStoryTag(string action, string key)
    {
        var start = action.IndexOf(key + "=", StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += key.Length + 1; var end = start;
        while (end < action.Length && action[end] is not (',' or ')' or ']' or ' ' or '\r' or '\n' or '\t')) end++;
        return end > start ? action[start..end].Trim() : null;
    }
}
