using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class PortableSaveCreationTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-portable-save-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PublicSaveReachesCompletedArchiveAndPublishesOneCreateOnlyDecision()
    {
        var commits = 0;
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            { LocalPublicationObserver = (phase, _) => { if (phase == TrustedLocalPublicationPhase.Committed) commits++; } });
        var state = PortableSaveFixture.Seed(files);
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var before = Snapshot(files.GameSessionPath);
        var prepared = 0;
        var logger = new PortableSaveFixture.CaptureLogger();
        var service = new SaveLoadService(files, state, logger, new SaveLoadServiceHooks
        {
            BeforeSaveCommitAsync = () => { prepared++; return Task.CompletedTask; }
        });

        var saved = await service.SaveGameAsync("portable-create", "actual public save");

        output.WriteLine("Prepared hook: {0}; committed B1 decisions: {1}; saved: {2}", prepared, commits, saved);
        foreach (var error in logger.Errors) output.WriteLine(error.ToString());
        Assert.Equal(1, prepared); // Mandatory roots/resource/soul checks and ZIP construction really completed.
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        AssertPreserved(before);
        Assert.True(saved, "The real public save reached its completed-archive boundary but did not publish.");
        Assert.Equal(1, commits);
        var archivePath = Assert.Single(Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip"));
        using var archive = ZipFile.OpenRead(archivePath);
        Assert.NotNull(archive.GetEntry("save_manifest.json"));
        Assert.NotNull(archive.GetEntry("game_state/meta/soul_state.json"));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(files.RuntimeRootPath, "save-staging")));
    }

    [Fact]
    public async Task FailedPreparationCleansActualOwnedRuntimeSaveScratch()
    {
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        var state = PortableSaveFixture.Seed(files);
        var before = Snapshot(files.GameSessionPath);
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var prepared = 0;
        var logger = new PortableSaveFixture.CaptureLogger();
        var service = new SaveLoadService(files, state, logger, new SaveLoadServiceHooks
        {
            BeforeSaveCommitAsync = () => { prepared++; throw new InvalidOperationException("owned preparation cut"); }
        });

        Assert.False(await service.SaveGameAsync("portable-cut", "actual private scratch cleanup"));

        output.WriteLine("Prepared hook: {0}", prepared);
        foreach (var error in logger.Errors) output.WriteLine(error.ToString());
        Assert.Equal(1, prepared);
        AssertPreserved(before);
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.Empty(Directory.GetFiles(files.ResolvePath("saves/manual_saves"), "*.zip"));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(files.RuntimeRootPath, "save-staging")));
    }

    private static Dictionary<string, string> Snapshot(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
    private static void AssertPreserved(Dictionary<string, string> before)
    {
        foreach (var (path, hash) in before) Assert.Equal(hash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
