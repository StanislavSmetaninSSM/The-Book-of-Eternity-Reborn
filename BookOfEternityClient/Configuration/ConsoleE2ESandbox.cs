namespace BookOfEternityClient.Configuration;

/// <summary>
/// Creates disposable, per-run base paths for console E2E runs.
/// The client receives <see cref="BasePath" /> as its legacy path argument and reads state from BasePath/game_session.
/// </summary>
public sealed class ConsoleE2ESandbox : IDisposable
{
    private readonly bool _deleteOnDispose;
    private bool _disposed;

    private ConsoleE2ESandbox(string basePath, bool deleteOnDispose)
    {
        BasePath = basePath;
        GameSessionPath = Path.Combine(basePath, "game_session");
        _deleteOnDispose = deleteOnDispose;
    }

    public string BasePath { get; }

    public string GameSessionPath { get; }

    public static ConsoleE2ESandbox CreateFromFixture(
        string fixtureGameSessionPath,
        string? artifactRoot = null,
        bool preserveArtifacts = false)
    {
        if (!Directory.Exists(fixtureGameSessionPath))
            throw new DirectoryNotFoundException(
                $"E2E fixture game_session was not found: {fixtureGameSessionPath}");

        var root = artifactRoot ?? Path.Combine(Path.GetTempPath(), "boe-console-e2e");
        Directory.CreateDirectory(root);

        var sandboxBasePath = Path.Combine(root, "run-" + Guid.NewGuid().ToString("N"));
        var sandboxGameSessionPath = Path.Combine(sandboxBasePath, "game_session");
        Directory.CreateDirectory(sandboxBasePath);
        CopyDirectory(fixtureGameSessionPath, sandboxGameSessionPath);

        return new ConsoleE2ESandbox(sandboxBasePath, deleteOnDispose: !preserveArtifacts);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!_deleteOnDispose || !Directory.Exists(BasePath))
            return;

        Directory.Delete(BasePath, recursive: true);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory))
        {
            var destinationFile = Path.Combine(destinationDirectory, Path.GetFileName(sourceFile));
            File.Copy(sourceFile, destinationFile, overwrite: false);
        }

        foreach (var sourceSubdirectory in Directory.EnumerateDirectories(sourceDirectory))
        {
            var destinationSubdirectory = Path.Combine(destinationDirectory, Path.GetFileName(sourceSubdirectory));
            CopyDirectory(sourceSubdirectory, destinationSubdirectory);
        }
    }
}
