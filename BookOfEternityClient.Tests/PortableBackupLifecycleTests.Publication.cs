using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableBackupLifecycleTests
{
    [Theory]
    [InlineData("create", "IntentPublished", false)]
    [InlineData("create", "MemberStaged", false)]
    [InlineData("create", "MemberPublished", false)]
    [InlineData("create", "Committed", true)]
    [InlineData("restore", "IntentPublished", false)]
    [InlineData("restore", "MemberStaged", false)]
    [InlineData("restore", "MemberPublished", false)]
    [InlineData("restore", "CommitStaged", false)]
    [InlineData("restore", "Committed", true)]
    [InlineData("restore", "CleanupMember", true)]
    [InlineData("cleanup", "MemberPublished", false)]
    [InlineData("cleanup", "Committed", true)]
    public async Task NonTransientCutsPreserveWholeBeforeOrCommittedResult(string operation, string cut, bool committed)
    {
        var reached = 0;
        var files = new FileSystemManager(_root, new FailingWarningLogger(), PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { LocalPublicationObserver = (phase, _) =>
            { if (phase.ToString() == cut) { reached++; throw new CutFailure(); } } });
        var generation = SeedGeneration(files); Seed(files, Source, [42]);
        if (operation != "create") Seed(files, Backup, Exact);
        string? created = null;
        var error = await Record.ExceptionAsync(async () =>
        {
            if (operation == "create") created = await files.CreateBackupAsync(Source);
            else if (operation == "restore") await files.RestoreBackupAsync(files.ResolvePath(Backup), Source);
            else await files.CleanupBackupAsync(files.ResolvePath(Backup));
        });
        Assert.Equal(1, reached);
        if (committed) Assert.Null(error); else Assert.IsType<CutFailure>(error);
        await using var recovered = await Manager().AcquireCanonicalWriteLeaseAsync();
        Assert.Equal(operation == "restore" && committed ? Exact : new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath(Source)));
        if (operation == "create")
        {
            var backups = Directory.GetFiles(Path.GetDirectoryName(files.ResolvePath(Source))!, "backup-source.bin.backup.*");
            if (committed) { Assert.Equal(created, Assert.Single(backups)); Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(created!)); }
            else Assert.Empty(backups);
        }
        else
        {
            Assert.Equal(!committed, File.Exists(files.ResolvePath(Backup)));
            if (!committed) Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Backup)));
        }
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.False(File.Exists(Journal(files)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task RestoreUnknownBytesRetainPartialMembersAndUnresolvedClassification(int cutMember)
    {
        FileSystemManager? files = null; var reached = 0;
        files = Manager(new FileSystemManagerHooks { LocalPublicationObserver = (phase, index) =>
        {
            if (phase != TrustedLocalPublicationPhase.MemberPublished || index != cutMember) return;
            reached++; Seed(files!, Backup, [99]); throw new CutFailure();
        } });
        SeedGeneration(files); Seed(files, Source, [42]); Seed(files, Backup, Exact);
        await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => files.RestoreBackupAsync(files.ResolvePath(Backup), Source));
        Assert.Equal(1, reached); Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Source)));
        Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(files.ResolvePath(Backup)));
        var evidence = File.ReadAllBytes(Journal(files));
        await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await Manager().AcquireCanonicalWriteLeaseAsync(); });
        Assert.Equal(evidence, File.ReadAllBytes(Journal(files)));
    }

    [Fact]
    public async Task FreshGenerationAndBackupRollBackAsOneDecision()
    {
        var reached = 0;
        var files = Manager(new FileSystemManagerHooks { LocalPublicationObserver = (phase, index) =>
        { if (phase == TrustedLocalPublicationPhase.MemberPublished && index == 1) { reached++; throw new CutFailure(); } } });
        Seed(files, Source, Exact);
        await Assert.ThrowsAsync<CutFailure>(() => files.CreateBackupAsync(Source));
        Assert.Equal(1, reached); Assert.False(File.Exists(files.SessionGenerationPath));
        Assert.Equal(Exact, File.ReadAllBytes(files.ResolvePath(Source)));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(files.ResolvePath(Source))!, "backup-source.bin.backup.*"));
    }

    private sealed class FailingWarningLogger : ILogger<FileSystemManager>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (level == LogLevel.Warning) throw new InvalidOperationException("Warning sink failed after decision."); }
    }
}
