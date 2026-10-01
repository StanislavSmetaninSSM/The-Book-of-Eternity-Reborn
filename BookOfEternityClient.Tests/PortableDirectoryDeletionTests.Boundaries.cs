using System.Runtime.InteropServices;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PortableDirectoryDeletionTests
{
    private static string Journal(FileSystemManager files) => Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
    private sealed class CutFailure : Exception { }

    [Theory]
    [InlineData("root-file")]
    [InlineData("root-link")]
    [InlineData("late-link")]
    [InlineData("late-dangling-link")]
    public async Task InvalidTreePreflightRetainsAllMembersAndOutsideBytes(string invalid)
    {
        var intents = 0;
        var files = Manager(new FileSystemManagerHooks { LocalPublicationObserver = (_, _) => intents++ });
        var generation = SeedGeneration(files);
        var outside = Path.Combine(_root, "outside.bin"); File.WriteAllBytes(outside, [42]);
        if (invalid == "root-file") Seed(files, Tree, [11]);
        else if (invalid == "root-link")
        {
            Directory.CreateDirectory(files.GameSessionPath);
            Directory.CreateSymbolicLink(files.ResolvePath(Tree), _root);
        }
        else
        {
            Seed(files, Tree + "/a.bin", [0xFF, 0]);
            Directory.CreateDirectory(files.ResolvePath(Tree + "/nested"));
            File.CreateSymbolicLink(files.ResolvePath(Tree + "/nested/z.bin"),
                invalid == "late-link" ? outside : outside + "-absent");
        }
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidDataException>(() => files.DeleteDirectoryTree(lease, Tree));
        Assert.Equal(0, intents);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(outside));
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        if (invalid.StartsWith("late-")) Assert.Equal(new byte[] { 0xFF, 0 }, File.ReadAllBytes(files.ResolvePath(Tree + "/a.bin")));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData(".")]
    [InlineData("")]
    public async Task InvalidRelativeDirectoryDoesNotMutate(string relative)
    {
        var files = Manager(); Seed(files, Tree + "/keep", [1]);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidDataException>(() => files.DeleteDirectoryTree(lease, relative));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(files.ResolvePath(Tree + "/keep")));
    }

    [Fact]
    public async Task RealNullForeignAndDisposedLeasesCannotDelete()
    {
        var files = Manager(); Seed(files, Tree + "/keep", [1]);
        var foreign = Manager();
        await using (var foreignLease = await foreign.AcquireCanonicalWriteLeaseAsync())
            Assert.Throws<InvalidOperationException>(() => files.DeleteDirectoryTree(foreignLease, Tree));
        Assert.Throws<ArgumentNullException>(() => files.DeleteDirectoryTree(null!, Tree));
        var disposed = await files.AcquireCanonicalWriteLeaseAsync(); await disposed.DisposeAsync();
        Assert.Throws<InvalidOperationException>(() => files.DeleteDirectoryTree(disposed, Tree));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(files.ResolvePath(Tree + "/keep")));
        Assert.False(File.Exists(files.SessionGenerationPath));
    }

    [Fact]
    public async Task StaleBoundGenerationRejectsBeforeDeletingAnyMember()
    {
        var files = Manager(); SeedGeneration(files); Seed(files, Tree + "/keep", [1]);
        await Assert.ThrowsAsync<SessionReplacedException>(() => SessionOperationContext.RunBoundAsync(files, _generation, async () =>
        {
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            File.WriteAllBytes(files.SessionGenerationPath,
                JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") }));
            Assert.Throws<SessionReplacedException>(() => files.DeleteDirectoryTree(lease, Tree));
        }));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(files.ResolvePath(Tree + "/keep")));
        Assert.False(File.Exists(Journal(files)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidGenerationRejectsEvenAnEmptyTree(bool directory)
    {
        var files = Manager(); Directory.CreateDirectory(files.ResolvePath(Tree + "/empty"));
        if (directory) Directory.CreateDirectory(files.SessionGenerationPath);
        else { Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!); File.WriteAllText(files.SessionGenerationPath, "{}"); }
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidDataException>(() => files.DeleteDirectoryTree(lease, Tree));
        Assert.True(Directory.Exists(files.ResolvePath(Tree + "/empty")));
    }

    [Theory]
    [InlineData("root-before")]
    [InlineData("root-after")]
    [InlineData("member-before")]
    [InlineData("member-after")]
    public async Task MutationHooksCannotIntroduceAnUnjournaledMember(string boundary)
    {
        var touched = false; var intents = 0; FileSystemManager? files = null;
        Task Mutate(string relative, bool after)
        {
            if (after != boundary.EndsWith("after") ||
                relative != (boundary.StartsWith("root") ? Tree : Tree + "/a.bin")) return Task.CompletedTask;
            touched = true; Seed(files!, Tree + "/nested/new.bin", [42]);
            return Task.CompletedTask;
        }
        files = Manager(new FileSystemManagerHooks
        {
            BeforeCanonicalMutationBoundaryAsync = relative => Mutate(relative, false),
            AfterCanonicalMutationBoundaryValidatedAsync = relative => Mutate(relative, true),
            LocalPublicationObserver = (_, _) => intents++
        });
        SeedGeneration(files); Seed(files, Tree + "/a.bin", [1]);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidDataException>(() => files.DeleteDirectoryTree(lease, Tree));
        Assert.True(touched); Assert.Equal(0, intents);
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(files.ResolvePath(Tree + "/a.bin")));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath(Tree + "/nested/new.bin")));
    }

    [Fact]
    public async Task ReadBoundaryLinkReplacementRejectsBeforeOpeningOrDeletingOtherMembers()
    {
        FileSystemManager? files = null; var touched = false;
        var outside = Path.Combine(_root, "outside.bin");
        files = Manager(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = relative =>
            {
                if (relative != Tree + "/z.bin") return Task.CompletedTask;
                var path = files!.ResolvePath(relative); File.Delete(path); File.CreateSymbolicLink(path, outside);
                touched = true; return Task.CompletedTask;
            }
        });
        SeedGeneration(files); Seed(files, Tree + "/a.bin", [1]); Seed(files, Tree + "/z.bin", [2]);
        File.WriteAllBytes(outside, [42]);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidDataException>(() => files.DeleteDirectoryTree(lease, Tree));
        Assert.True(touched); Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(files.ResolvePath(Tree + "/a.bin")));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(outside)); Assert.False(File.Exists(Journal(files)));
    }

    [Theory]
    [InlineData("IntentStaged", false)]
    [InlineData("IntentPublished", false)]
    [InlineData("MemberPublished", false)]
    [InlineData("CommitStaged", false)]
    [InlineData("Committed", true)]
    [InlineData("CleanupMember", true)]
    [InlineData("CleanupComplete", true)]
    public async Task PublicationCutsKeepExactWholeBeforeOrCommittedAbsence(string cut, bool committed)
    {
        var reached = 0;
        var files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, _) =>
            {
                Assert.NotEqual(TrustedLocalPublicationPhase.MemberStaged, phase);
                if (phase.ToString() != cut) return;
                reached++; throw new CutFailure();
            }
        });
        var generation = SeedGeneration(files);
        var images = new Dictionary<string, byte[]> { ["a.bin"] = [0xFF, 0], ["nested/bom"] = [0xEF, 0xBB, 0xBF, 1], ["nested/empty"] = [] };
        foreach (var (name, bytes) in images) Seed(files, Tree + "/" + name, bytes);
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var failure = Record.Exception(() => files.DeleteDirectoryTree(lease, Tree));
            Assert.Equal(1, reached);
            if (committed) Assert.Null(failure); else Assert.IsType<CutFailure>(failure);
            // Retained committed evidence still needs its exact scratch parents.
            if (committed && cut != "CleanupComplete")
            {
                Assert.True(File.Exists(Journal(files)));
                Assert.True(Directory.Exists(files.ResolvePath(Tree + "/nested")));
            }
        }
        var recovered = Manager(); await using var recovery = await recovered.AcquireCanonicalWriteLeaseAsync();
        foreach (var (name, bytes) in images)
        {
            var path = files.ResolvePath(Tree + "/" + name);
            Assert.Equal(!committed, File.Exists(path));
            if (!committed) Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath)); Assert.False(File.Exists(Journal(files)));
    }

    [Fact]
    public async Task AbsentGenerationJoinsTheSameDeletionDecisionAndRollsBackAsAbsence()
    {
        FileSystemManager? files = null; var reached = false;
        files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.IntentPublished) return;
                using var doc = JsonDocument.Parse(File.ReadAllBytes(Journal(files!)));
                Assert.False(doc.RootElement.GetProperty("GenerationBefore").GetProperty("Exists").GetBoolean());
                Assert.Equal(2, doc.RootElement.GetProperty("Members").GetArrayLength());
                reached = true; throw new CutFailure();
            }
        });
        Seed(files, Tree + "/a.bin", [1]);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<CutFailure>(() => files.DeleteDirectoryTree(lease, Tree));
        Assert.True(reached); Assert.False(File.Exists(files.SessionGenerationPath));
        Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(files.ResolvePath(Tree + "/a.bin")));
    }

    [Fact]
    public async Task UnknownLaterMemberPreservesPartialOutcomeAndRetainedEvidence()
    {
        FileSystemManager? files = null; var reached = false;
        files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                if (phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
                File.WriteAllBytes(files!.ResolvePath(Tree + "/z.bin"), [42]); reached = true; throw new CutFailure();
            }
        });
        SeedGeneration(files); Seed(files, Tree + "/a.bin", [1]); Seed(files, Tree + "/z.bin", [2]);
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            Assert.Throws<CanonicalDirectoryDeletionUncertainException>(() => files.DeleteDirectoryTree(lease, Tree));
        Assert.True(reached); Assert.False(File.Exists(files.ResolvePath(Tree + "/a.bin")));
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath(Tree + "/z.bin")));
        var evidence = File.ReadAllBytes(Journal(files));
        await Assert.ThrowsAsync<InvalidDataException>(async () => { await using var lease = await Manager().AcquireCanonicalWriteLeaseAsync(); });
        Assert.Equal(evidence, File.ReadAllBytes(Journal(files)));
    }

    [Fact]
    public async Task HardLinkedMemberDeletionPreservesTheOutsideName()
    {
        var files = Manager(); SeedGeneration(files); Seed(files, Tree + "/member", [0xFF, 0]);
        var outside = Path.Combine(_root, "outside.bin");
        if (OperatingSystem.IsWindows()) Assert.True(CreateHardLink(outside, files.ResolvePath(Tree + "/member"), IntPtr.Zero));
        else Assert.Equal(0, Link(files.ResolvePath(Tree + "/member"), outside));
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync(); files.DeleteDirectoryTree(lease, Tree);
        Assert.False(Directory.Exists(files.ResolvePath(Tree)));
        Assert.Equal(new byte[] { 0xFF, 0 }, File.ReadAllBytes(outside));
    }

    [Fact]
    public async Task PlatformDistinctNamesRemainSeparateJournalMembers()
    {
        FileSystemManager? files = null; var reached = false;
        files = Manager(new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.IntentPublished) return;
                using var doc = JsonDocument.Parse(File.ReadAllBytes(Journal(files!)));
                var members = doc.RootElement.GetProperty("Members"); Assert.Equal(2, members.GetArrayLength());
                Assert.Equal(new byte[] { 1 }, members[0].GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64());
                Assert.Equal(new byte[] { 2 }, members[1].GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64());
                reached = true;
            }
        });
        SeedGeneration(files); Seed(files, Tree + "/A", [1]); Seed(files, Tree + (OperatingSystem.IsWindows() ? "/B" : "/a"), [2]);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync(); files.DeleteDirectoryTree(lease, Tree);
        Assert.True(reached); Assert.False(Directory.Exists(files.ResolvePath(Tree)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedCleanupAndDiagnosticFailureNeverUndoOrDeleteUnknownBytes(bool cleanupDebt)
    {
        FileSystemManager? files = null; var reached = false;
        files = new FileSystemManager(_root, new ThrowingLogger(), PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (phase != (cleanupDebt ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.CleanupComplete)) return;
                    reached = true;
                    if (cleanupDebt) throw new CutFailure();
                    Seed(files!, Tree + "/nested/unknown.bin", [42]);
                }
            });
        SeedGeneration(files); Seed(files, Tree + "/nested/member", [1]);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync(); files.DeleteDirectoryTree(lease, Tree);
        Assert.True(reached); Assert.False(File.Exists(files.ResolvePath(Tree + "/nested/member")));
        Assert.True(Directory.Exists(files.ResolvePath(Tree + "/nested")));
        Assert.Equal(cleanupDebt, File.Exists(Journal(files)));
        if (!cleanupDebt) Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(files.ResolvePath(Tree + "/nested/unknown.bin")));
    }

    private sealed class ThrowingLogger : ILogger<FileSystemManager>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => throw new CutFailure();
    }
    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string created);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string created, string existing, IntPtr security);
}
