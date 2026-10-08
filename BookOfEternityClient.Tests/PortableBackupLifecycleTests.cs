using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableBackupLifecycleTests : IDisposable
{
    private const string Source = "game_state/core/backup-source.bin";
    private const string Backup = "game_state/core/arbitrary.test-backup";
    private static readonly byte[] Exact = [0xEF, 0xBB, 0xBF, 0xFF, 0, 0xFE];
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-backup-lifecycle-" + Guid.NewGuid().ToString("N"));
    private FileSystemManager Manager(FileSystemManagerHooks? hooks = null) =>
        new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreateBackupPreservesExactSourceAndPublishesIndependentBytes(bool asynchronous)
    {
        var commits = 0;
        var files = Manager(new FileSystemManagerHooks { LocalPublicationObserver = (phase, _) =>
            { if (phase == TrustedLocalPublicationPhase.Committed) commits++; } });
        Seed(files, Source, Exact);
        var backup = asynchronous ? await files.CreateBackupAsync(Source) : files.CreateBackup(Source);
        Assert.NotNull(backup);
        Assert.NotEqual(files.ResolvePath(Source), backup);
        Assert.Equal(Exact, File.ReadAllBytes(backup));
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Source)));
        Assert.Equal(1, commits); // Backup and initial generation share one decision.
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IndependentlySeededRestorePublishesTargetAndBackupAbsenceTogether(bool asynchronous)
    {
        var commits = 0;
        var files = Manager(new FileSystemManagerHooks { LocalPublicationObserver = (phase, _) =>
            { if (phase == TrustedLocalPublicationPhase.Committed) commits++; } });
        SeedGeneration(files);
        Seed(files, Source, [42]);
        Seed(files, Backup, Exact);
        if (asynchronous) await files.RestoreBackupAsync(files.ResolvePath(Backup), Source);
        else files.RestoreBackup(files.ResolvePath(Backup), Source);
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Source)));
        Assert.False(File.Exists(files.ResolvePath(Backup)));
        Assert.Equal(1, commits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupBackupPublishesEvidenceDeletion(bool asynchronous)
    {
        var commits = 0;
        var files = Manager(new FileSystemManagerHooks { LocalPublicationObserver = (phase, _) =>
            { if (phase == TrustedLocalPublicationPhase.Committed) commits++; } });
        SeedGeneration(files);
        Seed(files, Source, [42]);
        Seed(files, Backup, Exact);
        if (asynchronous) await files.CleanupBackupAsync(files.ResolvePath(Backup));
        else files.CleanupBackup(files.ResolvePath(Backup));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath(Source)));
        Assert.False(File.Exists(files.ResolvePath(Backup)));
        Assert.Equal(1, commits);
    }

    private static void Seed(FileSystemManager files, string relative, byte[] bytes)
    {
        var path = files.ResolvePath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private static byte[] SeedGeneration(FileSystemManager files)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") });
        Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!);
        File.WriteAllBytes(files.SessionGenerationPath, bytes);
        return bytes;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
