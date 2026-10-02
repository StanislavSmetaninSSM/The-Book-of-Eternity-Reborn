using System.IO.Compression;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises ordinary save outcomes through the public service and actual shared publication boundaries.
/// </summary>
public sealed class PortableSaveOutcomeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-save-outcome-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Preserves the established archive decision and prevents uncertain autosave retention.
    /// </summary>
    /// <param name="fault">
    /// Selects known rollback, committed cleanup failure, or unknown published archive bytes.
    /// </param>
    /// <param name="autosave">
    /// Uses the immediate autosave caller when true, or ordinary manual save otherwise.
    /// </param>
    [Theory]
    [InlineData("rollback", false)]
    [InlineData("committed-cleanup", false)]
    [InlineData("uncertain", false)]
    [InlineData("uncertain", true)]
    public async Task PublicSavePreservesPublicationOutcomeThroughImmediateCaller(string fault, bool autosave)
    {
        var reached = 0;
        var retention = 0;
        FileSystemManager? files = null;
        var folder = autosave ? "saves/autosaves" : "saves/manual_saves";
        files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (fault == "committed-cleanup" ? phase != TrustedLocalPublicationPhase.Committed :
                        phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
                    reached++;
                    if (fault == "uncertain")
                    {
                        var created = Assert.Single(Directory.GetFiles(files!.ResolvePath(folder), "*.zip")
                            .Where(path => Path.GetFileName(path) != "sentinel.zip"));
                        File.WriteAllBytes(created, [83, 0, 255]);
                    }
                    throw new InvalidDataException("deliberate save decision boundary");
                }
            });
        var state = PortableSaveFixture.Seed(files);
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        SeedLibrary(files);
        var service = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance, new SaveLoadServiceHooks
        {
            BeforeAutosaveCleanupLeaseAcquisitionAsync = () => { retention++; return Task.CompletedTask; }
        });
        Task<bool> Save() => autosave ? service.AutosaveAsync(7) : service.SaveGameAsync("outcome", "exact decision");

        if (fault == "uncertain") await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(Save);
        else Assert.Equal(fault == "committed-cleanup", await Save());

        Assert.Equal(1, reached);
        Assert.Equal(0, retention);
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        AssertLibrary(files, folder, created: fault != "rollback");
        var journal = Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        if (fault == "uncertain")
        {
            Assert.True(File.Exists(journal));
            var created = Assert.Single(Directory.GetFiles(files.ResolvePath(folder), "*.zip")
                .Where(path => Path.GetFileName(path) != "sentinel.zip"));
            Assert.Equal(new byte[] { 83, 0, 255 }, File.ReadAllBytes(created));
        }
        else if (fault == "committed-cleanup")
        {
            var created = Assert.Single(Directory.GetFiles(files.ResolvePath(folder), "*.zip")
                .Where(path => Path.GetFileName(path) != "sentinel.zip"));
            using var archive = ZipFile.OpenRead(created);
            Assert.NotNull(archive.GetEntry("save_manifest.json"));
        }
        else Assert.False(File.Exists(journal));
    }

    /// <summary>
    /// Closes the finished archive before the publication hook and preserves success if logging later fails.
    /// </summary>
    [Fact]
    public async Task ClosedArchiveAndCommittedResultSurviveLoggingFailure()
    {
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        var state = PortableSaveFixture.Seed(files);
        SeedLibrary(files);
        var closed = 0;
        var logger = new ThrowingSuccessLogger();
        var service = new SaveLoadService(files, state, logger, new SaveLoadServiceHooks
        {
            BeforeSaveCommitAsync = () =>
            {
                var path = Assert.Single(Directory.GetFiles(Path.Combine(files.RuntimeRootPath, "save-staging"), "save.zip", SearchOption.AllDirectories));
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                Assert.NotNull(archive.GetEntry("save_manifest.json"));
                closed++;
                return Task.CompletedTask;
            }
        });

        Assert.True(await service.SaveGameAsync("closed", "commit survives logging"));

        Assert.Equal(1, closed);
        Assert.Equal(1, logger.SuccessAttempts);
        AssertLibrary(files, "saves/manual_saves", created: true);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(files.RuntimeRootPath, "save-staging")));
    }

    /// <summary>
    /// Seeds one independent member in each save-library scope.
    /// </summary>
    /// <param name="files">
    /// Resolves the owned synthetic session.
    /// </param>
    private static void SeedLibrary(FileSystemManager files)
    {
        foreach (var folder in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            File.WriteAllBytes(files.ResolvePath($"saves/{folder}/sentinel.zip"), [9, 0, 255]);
    }

    /// <summary>
    /// Checks complete library membership and original sentinel bytes.
    /// </summary>
    /// <param name="files">
    /// Resolves the owned library.
    /// </param>
    /// <param name="folder">
    /// The sole scope allowed to gain a new archive.
    /// </param>
    /// <param name="created">
    /// Whether exactly one additional archive must exist.
    /// </param>
    private static void AssertLibrary(FileSystemManager files, string folder, bool created)
    {
        foreach (var scope in new[] { "manual_saves", "autosaves", "checkpoint_saves" })
            Assert.Equal(new byte[] { 9, 0, 255 }, File.ReadAllBytes(files.ResolvePath($"saves/{scope}/sentinel.zip")));
        var all = Directory.GetFiles(files.ResolvePath("saves"), "*", SearchOption.AllDirectories);
        Assert.Equal(created ? 4 : 3, all.Length);
        if (created) Assert.Single(all.Where(path => Path.GetFileName(path) != "sentinel.zip" &&
            Path.GetDirectoryName(path) == files.ResolvePath(folder)));
    }

    /// <summary>
    /// Fails only the success log after a durable save decision.
    /// </summary>
    private sealed class ThrowingSuccessLogger : ILogger<SaveLoadService>
    {
        /// <summary>
        /// Counts actual attempts to report the completed save.
        /// </summary>
        internal int SuccessAttempts { get; private set; }
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;
        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Information)
            {
                SuccessAttempts++;
                throw new InvalidOperationException("deliberate post-commit log failure");
            }
        }
    }

    /// <summary>
    /// Removes only this test's independently owned synthetic root.
    /// </summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
