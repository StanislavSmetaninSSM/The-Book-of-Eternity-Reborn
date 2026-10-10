using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>Client-owned single-slot input, never GM/domain authority.</summary>
internal static class PendingPlayerActionService
{
    internal const string PendingPath = "input/pending_player_action.json";

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
