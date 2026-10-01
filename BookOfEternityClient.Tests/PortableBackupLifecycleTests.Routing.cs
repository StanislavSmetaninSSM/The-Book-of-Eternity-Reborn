using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableBackupLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceLinkBeforeOrAtReadBoundaryCannotBeBackedUp(bool atBoundary)
    {
        FileSystemManager? files = null; var reached = 0; var events = 0;
        var outside = Path.Combine(_root, "outside");
        files = Manager(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = relative =>
            {
                if (!atBoundary || relative != Source) return Task.CompletedTask;
                reached++; File.Delete(files!.ResolvePath(Source)); File.CreateSymbolicLink(files.ResolvePath(Source), outside);
                return Task.CompletedTask;
            },
            LocalPublicationObserver = (_, _) => events++
        });
        SeedGeneration(files); Seed(files, Source, Exact); File.WriteAllBytes(outside, [42]);
        if (!atBoundary) { File.Delete(files.ResolvePath(Source)); File.CreateSymbolicLink(files.ResolvePath(Source), outside); }
        await Assert.ThrowsAsync<InvalidDataException>(() => files.CreateBackupAsync(Source));
        Assert.Equal(atBoundary ? 1 : 0, reached); Assert.Equal(0, events);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(outside));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalRecorderAndRecoveryKeepPhysicalCoreWithoutNewCallbacks(bool recovery)
    {
        var ordinary = 0; var physical = 0;
        var files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (_, _) => ordinary++,
            AfterPhysicalFilePublishedAsync = _ => { physical++; return Task.CompletedTask; }
        });
        SeedGeneration(files); Seed(files, Source, Exact);
        var recorder = new CountingRecorder();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        if (recovery) lease.IsLegacyStorageRecovery = true; else lease.MutationIntentRecorder = recorder;
        if (OperatingSystem.IsWindows())
        {
            var backup = Assert.IsType<string>(files.CreateBackup(lease, Source));
            Assert.Equal(Exact, File.ReadAllBytes(backup));
            files.RestoreBackup(lease, backup, Source);
            files.CleanupBackup(lease, backup);
            Assert.True(physical > 0); Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Source)));
        }
        else
        {
            Assert.Throws<PlatformNotSupportedException>(() => files.CreateBackup(lease, Source));
            Assert.Equal(0, physical); Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Source)));
        }
        Assert.Equal(0, ordinary); Assert.Equal(0, recorder.Callbacks); Assert.False(File.Exists(Journal(files)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoreCannotMixOriginalEvidenceAndOrdinaryTarget(bool originalTarget)
    {
        var files = Manager(); SeedGeneration(files);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var original = ExplorerLocalTurnRollbackArtifacts.Root + "/retained.bin";
        Seed(files, original, Exact); Seed(files, Source, [42]);
        Assert.Throws<InvalidOperationException>(() => files.RestoreBackup(lease,
            files.ResolvePath(originalTarget ? Source : original), originalTarget ? original : Source));
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(original)));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath(Source)));
        Assert.False(File.Exists(Journal(files)));
    }

    [Fact]
    public async Task ExistingOriginalEvidenceBlocksEvenOrdinaryAbsenceWithoutDeletingEvidence()
    {
        var files = Manager(); await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var evidence = ExplorerLocalTurnRollbackArtifacts.Root + "/retained.bin"; Seed(files, evidence, Exact);
        Assert.Throws<InvalidDataException>(() => files.CreateBackup(lease, Source));
        Assert.Throws<InvalidDataException>(() => files.CleanupBackup(lease, files.ResolvePath(Backup)));
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(evidence)));
        Assert.False(File.Exists(files.SessionGenerationPath)); Assert.False(File.Exists(Journal(files)));
    }

    [Fact]
    public async Task LinuxCaseDistinctBackupAndTargetAreIndependentNames()
    {
        Assert.True(OperatingSystem.IsLinux(), "This owner requires actual Linux case-sensitive execution.");
        var files = Manager(); SeedGeneration(files);
        const string lower = "game_state/core/case.bin"; const string upper = "game_state/core/CASE.bin";
        Seed(files, lower, [42]); Seed(files, upper, Exact);
        await files.RestoreBackupAsync(files.ResolvePath(upper), lower);
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(lower))); Assert.False(File.Exists(files.ResolvePath(upper)));
    }

    [Fact]
    public async Task WindowsExtendedRootBackupLifecycleAndCaseAliasAreValidated()
    {
        Assert.True(OperatingSystem.IsWindows(), "This owner requires actual Windows body execution.");
        Directory.CreateDirectory(_root);
        var files = new FileSystemManager(@"\\?\" + _root, NullLogger<FileSystemManager>.Instance);
        SeedGeneration(files); Seed(files, Source, Exact);
        var backup = await files.CreateBackupAsync(Source); Assert.NotNull(backup);
        await files.RestoreBackupAsync(backup, Source);
        await files.CleanupBackupAsync(backup);
        var caseAlias = files.ResolvePath(Source).Replace("backup-source.bin", "BACKUP-SOURCE.BIN", StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidDataException>(() => files.RestoreBackupAsync(caseAlias, Source));
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Source))); Assert.False(File.Exists(Journal(files)));
    }

    private sealed class CountingRecorder : ICanonicalMutationIntentRecorder
    {
        internal int Callbacks { get; private set; }
        public Task RecordMutationIntentAsync(string path, byte[]? desired) { Callbacks++; return Task.CompletedTask; }
        public Task RecordMutationNonPublicationAsync(string path) { Callbacks++; return Task.CompletedTask; }
        public Task RecordMutationPublicationAsync(string path, CanonicalMutationPublication publication) { Callbacks++; return Task.CompletedTask; }
    }
}
