using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>Client-owned single-slot input, never GM/domain authority.</summary>
internal static class PendingPlayerActionService
{
    internal const string PendingPath = "input/pending_player_action.json";

    // Strings are deliberately detached from mutable JsonNode/state objects.
    internal sealed record Binding(string ActionId, string Generation, string Action, string Source, string Json,
        SpiritualSelfTreatmentSelection? SpiritualSelfTreatment = null);
    internal sealed record State(Binding Binding, string Phase, string Json, string? ProofJson);
    internal sealed record Staged(Binding Binding, string RequestJson, string ManifestJson, string AuthorityJson, string Json, string HistoryJson,
        string? SpiritualWoundRequestBytesBase64 = null);
    internal sealed record StoryProof(string Path, int PrefixBytes, string PrefixHash, string RowJson)
    {
        internal StoryService.AcceptedStoryAppend? QteStoryAppend { get; init; }
    }

    internal static State Parse(string json, string generation)
    {
        var root = StrictJsonAuthority.Deserialize<JsonObject>(json, new JsonSerializerOptions(), "pending player action")
            ?? throw new InvalidDataException("Ожидающее действие повреждено.");
        var phase = root["status"]?.GetValue<string>();
        var id = root["actionId"]?.GetValue<string>();
        var action = root["playerAction"]?.GetValue<string>();
        var source = root["source"]?.GetValue<string>();
        var selection = root.ContainsKey("spiritualSelfTreatment") ? SpiritualSelfTreatmentSelection.Parse(root["spiritualSelfTreatment"]) : null;
        if ((selection is not null) != (source == "browser-wound-action"))
            throw new InvalidDataException("The typed wound action requires its own exact selection source.");
        if (root["schemaVersion"]?.GetValue<int>() != 1 ||
            !Guid.TryParseExact(id, "N", out _) || root["sessionGeneration"]?.GetValue<string>() != generation ||
            string.IsNullOrWhiteSpace(action) || action.TrimStart().StartsWith('/') ||
            source is not ("browser-composer" or "browser-effect-action" or "browser-wound-action") ||
            phase is not ("queued" or "preparing" or "staged" or "terminalProcessing" or "accepted" or "settled") ||
            !DateTimeOffset.TryParse(root["submittedAtUtc"]?.GetValue<string>(), out _))
            throw new InvalidDataException("Ожидающее действие не связано с текущей сессией.");
        string? proofJson = null;
        var allowed = new HashSet<string>(["schemaVersion", "actionId", "sessionGeneration", "status", "playerAction", "submittedAtUtc", "source"]);
        if (selection is not null) allowed.Add("spiritualSelfTreatment");
        if (phase != "queued")
        {
            allowed.UnionWith(["phaseProof", "phaseProofHash"]);
            proofJson = root["phaseProof"]?.GetValue<string>() ?? throw new InvalidDataException("Missing action phase proof.");
            if (Hash(Encoding.UTF8.GetBytes(proofJson)) != root["phaseProofHash"]?.GetValue<string>())
                throw new InvalidDataException("Action phase integrity mismatch.");
            var proof = StrictJsonAuthority.Deserialize<JsonObject>(proofJson, new JsonSerializerOptions(), "action phase proof")!;
            if (proof["schemaVersion"]?.GetValue<int>() != 1 || proof["phase"]?.GetValue<string>() != phase ||
                proof["actionId"]?.GetValue<string>() != id || proof["sessionGeneration"]?.GetValue<string>() != generation ||
                proof["playerAction"]?.GetValue<string>() != action)
                throw new InvalidDataException("Action phase binding mismatch.");
            if (selection is null ? proof.ContainsKey("spiritualSelfTreatment") :
                !JsonNode.DeepEquals(selection.ToJson(), proof["spiritualSelfTreatment"]))
                throw new InvalidDataException("Action phase wound selection mismatch.");
            var requiresBlob = selection is not null && phase is not "preparing";
            if (requiresBlob != proof.ContainsKey("spiritualWoundRequestBytesBase64"))
                throw new InvalidDataException("Original wound bytes belong only to a staged typed action.");
            if (requiresBlob)
            {
                var blob = Required(proof, "spiritualWoundRequestBytesBase64");
                var bytes = Convert.FromBase64String(blob);
                if (bytes.Length == 0 || Convert.ToBase64String(bytes) != blob)
                    throw new InvalidDataException("Original wound bytes are not canonical base64.");
            }
        }
        if (root.Any(pair => !allowed.Contains(pair.Key)))
            throw new InvalidDataException("Unexpected pending action field.");
        return new(new(id!, generation, action!, source!, json, selection), phase!, json, proofJson);
    }

    internal static string CreatePhase(State current, string phase, JsonObject? additional = null)
    {
        var root = JsonNode.Parse(current.Json)!.AsObject();
        var proof = additional?.DeepClone().AsObject() ?? new JsonObject();
        proof["schemaVersion"] = 1;
        proof["phase"] = phase;
        proof["actionId"] = current.Binding.ActionId;
        proof["sessionGeneration"] = current.Binding.Generation;
        proof["playerAction"] = current.Binding.Action;
        if (current.Binding.SpiritualSelfTreatment is { } selection)
            proof["spiritualSelfTreatment"] = selection.ToJson();
        else if (proof.ContainsKey("spiritualSelfTreatment") || proof.ContainsKey("spiritualWoundRequestBytesBase64"))
            throw new InvalidDataException("Prose action cannot carry a healing continuation.");
        var proofJson = proof.ToJsonString();
        root["status"] = phase;
        root["phaseProof"] = proofJson;
        root["phaseProofHash"] = Hash(Encoding.UTF8.GetBytes(proofJson));
        return root.ToJsonString();
    }

    internal static Staged ReadStaged(State state)
    {
        if (state.Phase is not ("staged" or "terminalProcessing" or "accepted" or "settled"))
            throw new InvalidDataException("Action has no original staging proof.");
        var proof = JsonNode.Parse(state.ProofJson!)!.AsObject();
        return new(state.Binding, Required(proof, "requestJson"), Required(proof, "manifestJson"),
            Required(proof, "authorityJson"), state.Json, Required(proof, "historyBeforeJson"), proof["spiritualWoundRequestBytesBase64"]?.GetValue<string>());
    }

    internal static JsonObject StagingProof(Staged staged)
    {
        var proof = new JsonObject
        {
            ["requestJson"] = staged.RequestJson, ["manifestJson"] = staged.ManifestJson,
            ["authorityJson"] = staged.AuthorityJson, ["historyBeforeJson"] = staged.HistoryJson
        };
        if (staged.SpiritualWoundRequestBytesBase64 is not null)
            proof["spiritualWoundRequestBytesBase64"] = staged.SpiritualWoundRequestBytesBase64;
        return proof;
    }

    internal static async Task<State?> ReadAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease)
    {
        fs.VerifyCurrentSessionOperation(lease);
        var json = await fs.ReadFileAsync(lease, PendingPath);
        return string.IsNullOrWhiteSpace(json) ? null : Parse(json, fs.GetOrCreateSessionGeneration(lease));
    }

    internal static async Task PublishAsync(FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        string previous, string? next, params CoordinatedStateWriteHelper.PlannedWrite[] accompanying)
    {
        fs.VerifyCurrentSessionOperation(lease);
        var writes = accompanying.Append(new CoordinatedStateWriteHelper.PlannedWrite(
            PendingPath, previous, next, RequireCurrentBaseline: true)).ToArray();
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(fs, lease, writes))
            throw new InvalidOperationException("Ожидающее действие изменилось; повторная отправка остановлена.");
        fs.ObserveBrowserOriginalPhasePublication(previous,next);
    }

    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Required(JsonObject value, string key) => value[key]?.GetValue<string>()
        ?? throw new InvalidDataException("Missing original action evidence: " + key);

    internal static JsonObject PrepareQueued(
        FileSystemManager fs, FileSystemManager.CanonicalWriteLease lease,
        string playerAction, string source, string submittedAtUtc,
        SpiritualSelfTreatmentSelection? spiritualSelfTreatment = null)
    {
        fs.VerifyCurrentSessionOperation(lease);
        if (fs.FileExists(lease, PendingPath))
            throw new InvalidOperationException("Предыдущее действие ещё ожидает обработки.");
        if (string.IsNullOrWhiteSpace(playerAction) || playerAction.TrimStart().StartsWith('/'))
            throw new InvalidDataException("Pending ordinary action must contain prose.");
        if (source is not ("browser-composer" or "browser-effect-action" or "browser-wound-action"))
            throw new InvalidDataException("Unknown pending player action source.");
        if ((spiritualSelfTreatment is not null) != (source == "browser-wound-action"))
            throw new InvalidDataException("Wound input requires its exact typed selection.");
        if (spiritualSelfTreatment is not null) _ = SpiritualSelfTreatmentSelection.Parse(spiritualSelfTreatment.ToJson());
        if (spiritualSelfTreatment?.Aid is { ReceiveAid: true, PlayerConfirmed: false })
            throw new InvalidDataException("Guardian help requires the player's actual guided confirmation before queueing.");
        var queued = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["actionId"] = Guid.NewGuid().ToString("N"),
            ["sessionGeneration"] = fs.GetOrCreateSessionGeneration(lease),
            ["status"] = "queued",
            ["playerAction"] = playerAction,
            ["submittedAtUtc"] = submittedAtUtc,
            ["source"] = source
        };
        if (spiritualSelfTreatment is not null) queued["spiritualSelfTreatment"] = spiritualSelfTreatment.ToJson();
        return queued;
    }
}
