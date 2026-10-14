using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Core;

public partial class GameEngine
{
    // Refusal-only guards for the approved first joint contour. Absence here
    // creates no canonical state, request, claim or pristine authority.
    internal static IReadOnlyList<string> MortalJointInactiveTailPaths { get; } = Array.AsReadOnly(new[]
    {
        "game_state/player/status_changes.json", "game_state/player/stat_points.json",
        "game_state/player/experience.json", "game_state/core/player_status.json",
        QteSceneService.QteOfferPath, QteSceneService.QteRuntimePath, QteDeferredEffectContinuation.StatePath
    });

    internal static async Task ValidateCurrentMortalJointInactiveTailAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, int decisionTurn)
    {
        files.EnsureCanonicalWriteLeaseActive(lease);
        var images = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in MortalJointInactiveTailPaths) images.Add(path, await files.ReadFileBytesAsync(lease, path));
        RequireInactiveMortalJointTail(images, decisionTurn);
    }

    private static void RequireInactiveMortalJointTail(IReadOnlyDictionary<string, byte[]?> images, int decisionTurn)
    {
        if (decisionTurn <= 0) throw new InvalidDataException("Joint tail requires its actual positive decision turn.");
        JsonObject? Read(string path) => images[path] is { } bytes
            ? StrictJsonAuthority.Deserialize<JsonObject>(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, path) ?? throw new InvalidDataException("Present joint tail JSON null: " + path)
            : null;
        var changes = Read("game_state/player/status_changes.json");
        if (changes?["statsIncreased"] is { } stats && (stats is not JsonArray array || array.Count != 0))
            throw new InvalidDataException("The first joint contour cannot own pending statsIncreased work.");

        // Match the actual CheckLevelUp source priority. Missing optional level
        // fields have its existing baseline of one; a present invalid field is
        // refused instead of manufacturing processed-level evidence.
        var experience = Read("game_state/player/experience.json");
        var level = OptionalPositiveLevel(experience, "level");
        if (level is null) level = OptionalPositiveLevel(experience, "playerLevel");
        if ((level ?? 1) <= 1) level = OptionalPositiveLevel(Read("game_state/core/player_status.json"), "level") ?? 1;
        var points = Read("game_state/player/stat_points.json") ??
            throw new InvalidDataException("Joint completion requires an existing durable processed-level marker; no stat initialization.");
        if (points["levelUpStatPointsAwardedThroughLevel"] is not JsonValue processed ||
            !processed.TryGetValue<int>(out var through) || through < level.Value)
            throw new InvalidDataException("The first joint contour cannot own an unprocessed level-up or stat-point initialization/award.");
        if (points.ContainsKey("unspentStatPoints") && (points["unspentStatPoints"] is not JsonValue unspent ||
            !unspent.TryGetValue<int>(out var quantity) || quantity < 0))
            throw new InvalidDataException("Joint tail stat-point state is malformed.");

        if (images[QteSceneService.QteOfferPath] is not null)
            throw new InvalidDataException("The first joint contour cannot own a new QTE offer for its decision turn.");
        if (images[QteSceneService.QteRuntimePath] is { } runtimeBytes)
        {
            var runtime = StrictJsonAuthority.Deserialize<QteSceneService.QteRuntimeState>(CanonicalJsonUtf8.DecodeOneOptionalBom(runtimeBytes),
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "actual joint QTE runtime") ??
                throw new InvalidDataException("Present joint QTE runtime is null.");
            if (runtime.PendingOffer is not null || runtime.ActiveScene is not null)
                throw new InvalidDataException("The first joint contour cannot own a pending/active QTE runtime handoff.");
        }
        if (Read(QteDeferredEffectContinuation.StatePath) is { } deferred)
        {
            if (deferred["state"]?.GetValue<string>() != "terminal" ||
                deferred["clientStoryAppend"]?["source"] is not JsonObject source ||
                source["turnNumber"] is not JsonValue turn || !turn.TryGetValue<int>(out var priorTurn) ||
                priorTurn <= 0 || priorTurn >= decisionTurn)
                throw new InvalidDataException("Only the different prior finalized QTE source may remain in the joint completion tail.");
        }
    }

    private static int? OptionalPositiveLevel(JsonObject? root, string field)
    {
        if (root is null || !root.ContainsKey(field)) return null;
        if (root[field] is JsonValue value && value.TryGetValue<int>(out var level) && level > 0) return level;
        throw new InvalidDataException("Joint tail canonical level is malformed: " + field);
    }
}
