using BookOfEternityClient.Configuration;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ConsoleE2ESandboxTests : IDisposable
{
    private readonly string _tempRoot;

    public ConsoleE2ESandboxTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "boe-e2e-sandbox-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void CreateFromFixture_CopiesGameSessionIntoDisposableUniqueBasePath()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureGameSessionPath = Path.Combine(repoRoot, "FileSystemExample", "game_session");

        using var sandbox = ConsoleE2ESandbox.CreateFromFixture(fixtureGameSessionPath, _tempRoot);

        Assert.True(Directory.Exists(sandbox.BasePath));
        Assert.True(Directory.Exists(sandbox.GameSessionPath));
        Assert.NotEqual(fixtureGameSessionPath, sandbox.GameSessionPath);
        Assert.True(File.Exists(Path.Combine(sandbox.GameSessionPath, "game_state", "meta", "soul_state.json")));
    }

    [Fact]
    public void CreateFromFixture_CopiesSystemGuardianLibraryAlongsideGameSession()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureGameSessionPath = Path.Combine(repoRoot, "FileSystemExample", "game_session");

        using var sandbox = ConsoleE2ESandbox.CreateFromFixture(fixtureGameSessionPath, _tempRoot);

        var sandboxLibraryPath = Path.Combine(sandbox.BasePath, "system_guardians");
        Assert.True(Directory.Exists(sandboxLibraryPath));
        Assert.True(Directory.Exists(Path.Combine(sandboxLibraryPath, "built_in")));
    }

    [Fact]
    public void CreateFromManualSaveArchive_ExtractsSaveAndCopiesSystemGuardianLibraryAlongsideGameSession()
    {
        var repoRoot = FindRepositoryRoot();
        var archivePath = Path.Combine(
            repoRoot,
            "FileSystemExample",
            "game_session",
            "saves",
            "manual_saves",
            "chaos_sea_command_display_fixture.zip");

        using var sandbox = ConsoleE2ESandbox.CreateFromManualSaveArchive(archivePath, _tempRoot);

        Assert.True(File.Exists(Path.Combine(sandbox.GameSessionPath, "game_state", "meta", "soul_state.json")));
        Assert.True(File.Exists(Path.Combine(
            sandbox.BasePath,
            "system_guardians",
            "built_in",
            "azalia",
            "manifest.json")));
    }

    [Fact]
    public void CreateFromFixture_IsolatesRunsAndDeletesSandboxByDefault()
    {
        var repoRoot = FindRepositoryRoot();
        var fixtureGameSessionPath = Path.Combine(repoRoot, "FileSystemExample", "game_session");

        string firstBasePath;
        string markerPath;
        using (var first = ConsoleE2ESandbox.CreateFromFixture(fixtureGameSessionPath, _tempRoot))
        {
            firstBasePath = first.BasePath;
            markerPath = Path.Combine(first.GameSessionPath, "e2e-marker.txt");
            File.WriteAllText(markerPath, "first run only");

            using var second = ConsoleE2ESandbox.CreateFromFixture(fixtureGameSessionPath, _tempRoot);
            Assert.NotEqual(first.BasePath, second.BasePath);
            Assert.False(File.Exists(Path.Combine(second.GameSessionPath, "e2e-marker.txt")));
        }

        Assert.False(Directory.Exists(firstBasePath));
        Assert.False(File.Exists(markerPath));
    }

    [Fact]
    public void CreateFromFixture_RejectsMissingFixtureWithDiagnostic()
    {
        var missingFixture = Path.Combine(_tempRoot, "missing", "game_session");

        var ex = Assert.Throws<DirectoryNotFoundException>(() =>
            ConsoleE2ESandbox.CreateFromFixture(missingFixture, _tempRoot));

        Assert.Contains("E2E fixture game_session was not found", ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "FileSystemExample", "game_session")))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root with FileSystemExample/game_session.");
    }
}
