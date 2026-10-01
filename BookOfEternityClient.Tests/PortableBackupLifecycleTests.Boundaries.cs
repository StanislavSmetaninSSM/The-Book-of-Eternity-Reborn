using System.Runtime.InteropServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableBackupLifecycleTests
{
    private static string Journal(FileSystemManager files) => Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
    private sealed class CutFailure : Exception { }

    [Fact]
    public async Task AbsentSourceAndCleanupDoNotCreateGenerationOrJournal()
    {
        var calls = 0;
        var files = Manager(new FileSystemManagerHooks { LocalPublicationObserver = (_, _) => calls++ });
        Assert.Null(await files.CreateBackupAsync(Source));
        await files.CleanupBackupAsync(files.ResolvePath(Backup));
        Assert.False(File.Exists(files.SessionGenerationPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(Journal(files))));
        Assert.Equal(0, calls);
        await Assert.ThrowsAsync<FileNotFoundException>(() => files.RestoreBackupAsync(files.ResolvePath(Backup), Source));
        Assert.False(File.Exists(files.SessionGenerationPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyBackupRestoresExactEmptyBytesOverPresentOrAbsentTarget(bool targetExists)
    {
        var files = Manager();
        var generation = SeedGeneration(files);
        Seed(files, Backup, []);
        if (targetExists) Seed(files, Source, Exact);
        await files.RestoreBackupAsync(files.ResolvePath(Backup), Source);
        Assert.Empty(File.ReadAllBytes(files.ResolvePath(Source)));
        Assert.False(File.Exists(files.ResolvePath(Backup)));
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
    }

    [Fact]
    public async Task CollidingBackupNameIsRejectedBeforeIntentAndPreservesExistingBytes()
    {
        FileSystemManager? files = null; string? collision = null; var events = 0;
        files = Manager(new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = relative =>
            {
                if (relative.StartsWith(Source + ".backup.", StringComparison.Ordinal))
                { collision = files!.ResolvePath(relative); File.WriteAllBytes(collision, [42]); }
                return Task.CompletedTask;
            },
            LocalPublicationObserver = (_, _) => events++
        });
        Seed(files, Source, Exact);
        await Assert.ThrowsAsync<InvalidDataException>(() => files.CreateBackupAsync(Source));
        Assert.NotNull(collision); Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(collision));
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Source)));
        Assert.Equal(0, events); Assert.False(File.Exists(files.SessionGenerationPath));
    }

    [Fact]
    public async Task RestoreSameNameIsRejectedBeforeDeletingItsOnlyCopy()
    {
        var files = Manager(); Seed(files, Source, Exact);
        await Assert.ThrowsAsync<InvalidDataException>(() => files.RestoreBackupAsync(files.ResolvePath(Source), Source));
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Source)));
        Assert.False(File.Exists(files.SessionGenerationPath));
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("root")]
    [InlineData("directory")]
    [InlineData("link")]
    [InlineData("dangling-link")]
    [InlineData("ancestor-link")]
    public async Task InvalidBackupInputIsRejectedWithoutChangingTargetOrOutside(string kind)
    {
        var files = Manager(); SeedGeneration(files); Seed(files, Source, Exact);
        var outside = Path.Combine(_root, "outside"); Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "sentinel"); File.WriteAllBytes(sentinel, [42]);
        var backup = files.ResolvePath(Backup);
        switch (kind)
        {
            case "outside": backup = sentinel; break;
            case "root": backup = files.GameSessionPath; break;
            case "directory": Directory.CreateDirectory(backup); break;
            case "link": File.CreateSymbolicLink(backup, sentinel); break;
            case "dangling-link": File.CreateSymbolicLink(backup, sentinel + "-missing"); break;
            case "ancestor-link":
                var ancestor = files.ResolvePath("backup-link"); Directory.CreateSymbolicLink(ancestor, outside);
                backup = Path.Combine(ancestor, "sentinel"); break;
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => files.RestoreBackupAsync(backup, Source));
        await Assert.ThrowsAsync<InvalidDataException>(() => files.CleanupBackupAsync(backup));
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Source)));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(sentinel));
        Assert.False(File.Exists(Journal(files)));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("target")]
    public async Task InvalidSourceOrTargetTypeIsRejectedBeforeBackupMutation(string member)
    {
        var files = Manager(); SeedGeneration(files); Seed(files, Backup, Exact);
        Directory.CreateDirectory(files.ResolvePath(Source));
        if (member == "source") await Assert.ThrowsAsync<InvalidDataException>(() => files.CreateBackupAsync(Source));
        else await Assert.ThrowsAsync<InvalidDataException>(() => files.RestoreBackupAsync(files.ResolvePath(Backup), Source));
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Backup))); Assert.False(File.Exists(Journal(files)));
    }

    [Theory]
    [InlineData("create")]
    [InlineData("restore")]
    [InlineData("cleanup")]
    public async Task RealForeignDisposedAndNullLeasesAreRejected(string operation)
    {
        var files = Manager(); Seed(files, Source, Exact); Seed(files, Backup, [42]);
        void Invoke(FileSystemManager.CanonicalWriteLease lease)
        {
            if (operation == "create") files.CreateBackup(lease, Source);
            else if (operation == "restore") files.RestoreBackup(lease, files.ResolvePath(Backup), Source);
            else files.CleanupBackup(lease, files.ResolvePath(Backup));
        }
        var foreign = Manager();
        await using (var lease = await foreign.AcquireCanonicalWriteLeaseAsync())
            Assert.Throws<InvalidOperationException>(() => Invoke(lease));
        Assert.Throws<ArgumentNullException>(() => Invoke(null!));
        var disposed = await files.AcquireCanonicalWriteLeaseAsync(); await disposed.DisposeAsync();
        Assert.Throws<InvalidOperationException>(() => Invoke(disposed));
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Source)));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath(Backup)));
    }

    [Fact]
    public async Task SuppliedLeaseHonorsChangedBoundGenerationBeforeNoOp()
    {
        var files = Manager(); SeedGeneration(files);
        var generation = files.GetOrCreateSessionGeneration();
        await Assert.ThrowsAsync<SessionReplacedException>(() => SessionOperationContext.RunBoundAsync(files, generation, async () =>
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            SeedGeneration(files);
            Assert.Throws<SessionReplacedException>(() => files.CreateBackup(lease, Source));
        }));
        Assert.False(File.Exists(Journal(files)));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("generation")]
    public async Task CapturedSourceAndGenerationAreRecheckedAfterMutationHook(string changed)
    {
        FileSystemManager? files = null; var reached = false; var events = 0;
        files = Manager(new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = relative =>
            {
                if (!relative.StartsWith(Source + ".backup.", StringComparison.Ordinal)) return Task.CompletedTask;
                reached = true;
                if (changed == "source") Seed(files!, Source, [42]); else SeedGeneration(files!);
                return Task.CompletedTask;
            },
            LocalPublicationObserver = (_, _) => events++
        });
        SeedGeneration(files); Seed(files, Source, Exact);
        await Assert.ThrowsAsync<InvalidDataException>(() => files.CreateBackupAsync(Source));
        Assert.True(reached); Assert.Equal(0, events); Assert.False(File.Exists(Journal(files)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SameLeaseNoOpResolvesCommittedCleanupDebtAndBlocksUnknownEvidence(bool unknown)
    {
        var calls = 0;
        var files = Manager(new FileSystemManagerHooks { LocalPublicationObserver = (phase, _) =>
        { if (phase == TrustedLocalPublicationPhase.Committed) { calls++; throw new CutFailure(); } } });
        SeedGeneration(files); Seed(files, Backup, Exact);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.CleanupBackup(lease, files.ResolvePath(Backup));
        Assert.Equal(1, calls); Assert.False(File.Exists(files.ResolvePath(Backup)));
        var journal = File.ReadAllBytes(Journal(files));
        if (unknown) Seed(files, Backup, [42]);
        var error = Record.Exception(() => files.CreateBackup(lease, "absent-source"));
        if (unknown)
        {
            Assert.IsType<CoordinatedStatePublicationUncertainException>(error);
            Assert.Equal(journal, File.ReadAllBytes(Journal(files)));
            Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath(Backup)));
        }
        else { Assert.Null(error); Assert.False(File.Exists(Journal(files))); }
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task HardLinkedSourceTargetAndBackupPreserveEveryOutsideName()
    {
        var files = Manager(); SeedGeneration(files); Seed(files, Source, Exact); Seed(files, Backup, [42]);
        var sourceAlias = Path.Combine(_root, "source-outside"); var backupAlias = Path.Combine(_root, "backup-outside");
        HardLink(files.ResolvePath(Source), sourceAlias); HardLink(files.ResolvePath(Backup), backupAlias);
        var created = await files.CreateBackupAsync(Source); Assert.Equal(Exact, File.ReadAllBytes(created!));
        await files.RestoreBackupAsync(files.ResolvePath(Backup), Source);
        await files.CleanupBackupAsync(created!);
        Assert.Equal(Exact, File.ReadAllBytes(sourceAlias));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(backupAlias));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath(Source)));
    }

    private static void HardLink(string existing, string created)
    {
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(created, existing, IntPtr.Zero));
        else Assert.Equal(0, Link(existing, created));
    }
    [DllImport("libc", EntryPoint = "link", SetLastError = true)] private static extern int Link(string existing, string created);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateHardLink(string created, string existing, IntPtr security);
}
