using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class AfterlifeLocalActionGuard
{
    private const string TurnRequestPath = "input/turn_request.json";
    private const string PendingTurnSnapshotManifestPath = "game_state/control/pending_turn_snapshot.json";
    private const string PendingTurnSnapshotDirectory = "game_state/control/pending_turn_snapshot";

    public static string? TryDescribeActiveGmTurnLifecycleBlocker(
        FileSystemManager fs,
        string operationLabel,
        string affectedSurfaces)
    {
        var artifacts = new List<string>();
        if (fs.FileExists(TurnRequestPath))
            artifacts.Add(TurnRequestPath);
        if (fs.FileExists(PendingTurnSnapshotManifestPath))
            artifacts.Add(PendingTurnSnapshotManifestPath);
        if (HasAnyPendingTurnSnapshotFile(fs))
            artifacts.Add(PendingTurnSnapshotDirectory);

        return artifacts.Count == 0
            ? null
            : $"{operationLabel} заблокировано: найден активный GM-turn lifecycle. " +
              $"Операция меняет {affectedSurfaces}, поэтому она запрещена до завершения, отмены или repair текущего хода. " +
              $"Найдено: {string.Join(", ", artifacts)}.";
    }

    public static bool HasAnyPendingTurnSnapshotFile(FileSystemManager fs)
    {
        var snapshotDirectoryPath = fs.ResolvePath(PendingTurnSnapshotDirectory);
        if (!Directory.Exists(snapshotDirectoryPath))
            return false;

        try
        {
            return Directory.EnumerateFiles(snapshotDirectoryPath, "*", SearchOption.AllDirectories).Any();
        }
        catch
        {
            return true;
        }
    }
}
