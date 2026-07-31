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
        var writeLease = fs.AcquireCanonicalWriteLeaseAsync(
                CanonicalWritePurpose.PublicationReadQuiescence)
            .GetAwaiter()
            .GetResult();
        try
        {
            return Build(fs, writeLease);
        }
        finally
        {
            writeLease.DisposeAsync()
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }
    }

    internal static BrowserPendingTurnStatus Build(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var artifacts = new List<BrowserPendingTurnArtifactStatus>
        {
            FileArtifact(fs, writeLease, TurnRequestPath, "Запрос хода GM"),
            FileArtifact(fs, writeLease, TurnCompletePath, "Готов успешный ответ"),
            FileArtifact(fs, writeLease, TurnErrorPath, "Готов terminal error"),
            FileArtifact(fs, writeLease, PendingTurnSnapshotManifestPath, "Validated pending snapshot"),
            DirectoryArtifact(fs, writeLease, PendingTurnSnapshotDirectory, "Копии snapshot файлов"),
            DirectoryArtifact(fs, writeLease, ExplorerRollbackDirectory, "Локальные rollback backup")
        };

        var hasActive = artifacts.Any(static item => item.Exists);
        return new BrowserPendingTurnStatus(
            HasActiveGmTurn: hasActive,
            Artifacts: artifacts,
            Message: hasActive
                ? "Обнаружен активный ход ГМа или rollback/snapshot artifact. Запись из браузера должна дождаться завершения, отмены или repair."
                : "Активный ход ГМа не обнаружен.");
    }

    private static BrowserPendingTurnArtifactStatus FileArtifact(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string path,
        string label) =>
        new(label, path, fs.FileExists(writeLease, path), "file");

    private static BrowserPendingTurnArtifactStatus DirectoryArtifact(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        string path,
        string label) =>
        new(
            label,
            path,
            fs.DirectoryHasContent(writeLease, path),
            "directory");
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
