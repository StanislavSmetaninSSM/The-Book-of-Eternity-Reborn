using BookOfEternityClient.Core;

namespace BookOfEternityClient.WebUi;

public static class BrowserPendingTurnInspector
{
    public const string TurnRequestPath = "input/turn_request.json";
    public const string TurnCompletePath = "ready/turn_complete.json";
    public const string TurnErrorPath = "ready/turn_error.json";
    public const string PendingTurnSnapshotManifestPath = "game_state/control/pending_turn_snapshot.json";
    public const string PendingTurnSnapshotDirectory = "game_state/control/pending_turn_snapshot";
    public const string ExplorerRollbackDirectory = "game_state/control/explorer_local_turn_rollback";

    public static BrowserPendingTurnStatus Build(FileSystemManager fs)
    {
        var artifacts = new List<BrowserPendingTurnArtifactStatus>
        {
            FileArtifact(fs, TurnRequestPath, "Запрос хода GM"),
            FileArtifact(fs, TurnCompletePath, "Готов успешный ответ"),
            FileArtifact(fs, TurnErrorPath, "Готов terminal error"),
            FileArtifact(fs, PendingTurnSnapshotManifestPath, "Validated pending snapshot"),
            DirectoryArtifact(fs, PendingTurnSnapshotDirectory, "Копии snapshot файлов"),
            DirectoryArtifact(fs, ExplorerRollbackDirectory, "Локальные rollback backup")
        };

        var hasActive = artifacts.Any(static item => item.Exists);
        return new BrowserPendingTurnStatus(
            HasActiveGmTurn: hasActive,
            Artifacts: artifacts,
            Message: hasActive
                ? "Обнаружен активный GM-turn или rollback/snapshot artifact. Browser-write должен дождаться завершения, отмены или repair."
                : "Активный GM-turn не обнаружен.");
    }

    private static BrowserPendingTurnArtifactStatus FileArtifact(FileSystemManager fs, string path, string label) =>
        new(label, path, fs.FileExists(path), "file");

    private static BrowserPendingTurnArtifactStatus DirectoryArtifact(FileSystemManager fs, string path, string label) =>
        new(label, path, DirectoryHasContent(fs, path), "directory");

    private static bool DirectoryHasContent(FileSystemManager fs, string path)
    {
        var fullPath = fs.ResolvePath(path);
        return Directory.Exists(fullPath) && Directory.EnumerateFileSystemEntries(fullPath, "*", SearchOption.AllDirectories).Any();
    }
}

public sealed record BrowserPendingTurnStatus(
    bool HasActiveGmTurn,
    IReadOnlyList<BrowserPendingTurnArtifactStatus> Artifacts,
    string Message);

public sealed record BrowserPendingTurnArtifactStatus(
    string Label,
    string Path,
    bool Exists,
    string Kind);
