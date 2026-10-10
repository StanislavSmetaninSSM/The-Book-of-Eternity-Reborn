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
    internal sealed record Binding(string ActionId, string Generation, string Action, string Source, string Json);
    internal sealed record State(Binding Binding, string Phase, string Json, string? ProofJson);
    internal sealed record Staged(Binding Binding, string RequestJson, string ManifestJson, string AuthorityJson, string Json, string HistoryJson);
    internal sealed record StoryProof(string Path, int PrefixBytes, string PrefixHash, string RowJson);

    internal static State Parse(string json, string generation)
    {
        var root = StrictJsonAuthority.Deserialize<JsonObject>(json, new JsonSerializerOptions(), "pending player action")
            ?? throw new InvalidDataException("Ожидающее действие повреждено.");
        var phase = root["status"]?.GetValue<string>();
        var id = root["actionId"]?.GetValue<string>();
        var action = root["playerAction"]?.GetValue<string>();
        var source = root["source"]?.GetValue<string>();
        if (root["schemaVersion"]?.GetValue<int>() != 1 ||
            !Guid.TryParseExact(id, "N", out _) || root["sessionGeneration"]?.GetValue<string>() != generation ||
            string.IsNullOrWhiteSpace(action) || action.TrimStart().StartsWith('/') ||
            source is not ("browser-composer" or "browser-effect-action") ||
            phase is not ("queued" or "preparing" or "staged" or "terminalProcessing" or "accepted" or "settled") ||
            !DateTimeOffset.TryParse(root["submittedAtUtc"]?.GetValue<string>(), out _))
            throw new InvalidDataException("Ожидающее действие не связано с текущей сессией.");
        string? proofJson = null;
        var allowed = new HashSet<string>(["schemaVersion", "actionId", "sessionGeneration", "status", "playerAction", "submittedAtUtc", "source"]);
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
        }
        if (root.Any(pair => !allowed.Contains(pair.Key)))
            throw new InvalidDataException("Unexpected pending action field.");
        return new(new(id!, generation, action!, source!, json), phase!, json, proofJson);
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
            Required(proof, "authorityJson"), state.Json, Required(proof, "historyBeforeJson"));
    }

    internal static JsonObject StagingProof(Staged staged) => new()
    {
        ["requestJson"] = staged.RequestJson,
        ["manifestJson"] = staged.ManifestJson,
        ["authorityJson"] = staged.AuthorityJson,
        ["historyBeforeJson"] = staged.HistoryJson
    };

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
        string playerAction, string source, string submittedAtUtc)
    {
        fs.VerifyCurrentSessionOperation(lease);
        if (fs.FileExists(lease, PendingPath))
            throw new InvalidOperationException("Предыдущее действие ещё ожидает обработки.");
        if (string.IsNullOrWhiteSpace(playerAction) || playerAction.TrimStart().StartsWith('/'))
            throw new InvalidDataException("Pending ordinary action must contain prose.");
        if (source is not ("browser-composer" or "browser-effect-action"))
            throw new InvalidDataException("Unknown pending player action source.");
        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["actionId"] = Guid.NewGuid().ToString("N"),
            ["sessionGeneration"] = fs.GetOrCreateSessionGeneration(lease),
            ["status"] = "queued",
            ["playerAction"] = playerAction,
            ["submittedAtUtc"] = submittedAtUtc,
            ["source"] = source
        };
    }
}
