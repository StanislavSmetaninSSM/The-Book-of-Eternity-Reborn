using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

// Filesystem fixtures only: no host, worker, CLI or other child process is launched.
public sealed class GmWorkerDetachedWorkspaceTests
{
    [Fact]
    public async Task Stage_Read_Dispose_PreservesExactBinaryEmptyUnicodeAndTaskBytes()
    {
        using var fixture = new Fixture();
        var files = new Dictionary<string, byte[]>
        {
            ["context/binary.bin"] = [0, 255, 128, 13, 10, 0],
            ["context/empty.bin"] = [],
            ["context/снег.txt"] = Encoding.UTF8.GetBytes("\ufeffsnow\r\nснег\n")
        };
        var task = fixture.Task(files);
        var workspace = await fixture.Create(task);
        var workspaceRoot = Path.GetDirectoryName(workspace.GameSessionPath)!;
        try
        {
            Assert.False(workspace.GameSessionPath.StartsWith(fixture.Fs.GameSessionPath + Path.DirectorySeparatorChar, StringComparison.Ordinal));
            foreach (var (path, bytes) in files)
            {
                Assert.Equal(bytes, await workspace.ReadFileBytesAsync(path));
                Assert.Equal(bytes, File.ReadAllBytes(fixture.Canonical(path)));
            }
            Assert.Equal(Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(task)), File.ReadAllBytes(workspace.TaskPath));
            Assert.Null(await workspace.ReadProposalBytesAsync());
        }
        finally { await workspace.DisposeAsync(); }
        Assert.False(Directory.Exists(workspaceRoot));
        await workspace.DisposeAsync();
        fixture.AssertCanonical(files);
    }

    [Fact]
    public async Task MissingContextAndMissingNestedReadRemainAbsent()
    {
        using var fixture = new Fixture();
        var task = fixture.Task() with { ContextFiles = [new() { Path = "absent/nested.bin", Sha256 = "missing" }] };
        await using var workspace = await fixture.Create(task);
        Assert.Null(await workspace.ReadFileBytesAsync("absent/nested.bin"));
        Assert.False(Directory.Exists(Path.Combine(workspace.GameSessionPath, "absent")));
    }

    [Theory]
    [InlineData("mismatch")]
    [InlineData("disappeared")]
    [InlineData("unexpected-present")]
    public async Task ContextPinFailureAfterFirstWriteRollsBackOnlyOwnedWorkspace(string kind)
    {
        using var fixture = new Fixture();
        var files = new Dictionary<string, byte[]> { ["first.bin"] = [1, 2], ["second.bin"] = [3, 4] };
        var task = fixture.Task(files);
        var second = task.ContextFiles[1] with { Sha256 = kind == "unexpected-present" ? "missing" : new string('0', 64) };
        if (kind == "disappeared") File.Delete(fixture.Canonical("second.bin"));
        task = task with { ContextFiles = [task.ContextFiles[0], second] };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Create(task));
        fixture.AssertNoWorkspace();
        Assert.Equal(files["first.bin"], File.ReadAllBytes(fixture.Canonical("first.bin")));
        if (kind != "disappeared") Assert.Equal(files["second.bin"], File.ReadAllBytes(fixture.Canonical("second.bin")));
    }

    [Fact]
    public async Task DuplicateContextPathCannotOverwriteAndPartialStagingIsRemoved()
    {
        using var fixture = new Fixture();
        var files = new Dictionary<string, byte[]> { ["same.bin"] = [7, 8] };
        var task = fixture.Task(files);
        task = task with { ContextFiles = [task.ContextFiles[0], task.ContextFiles[0]] };
        await Assert.ThrowsAnyAsync<IOException>(() => fixture.Create(task));
        fixture.AssertNoWorkspace();
        fixture.AssertCanonical(files);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute")]
    [InlineData("nested/../outside")]
    [InlineData("nested\\outside")]
    public async Task UnsafeContextAndArtifactPathsAreRejected(string path)
    {
        using var fixture = new Fixture();
        var task = fixture.Task() with { ContextFiles = [new() { Path = path, Sha256 = "missing" }] };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Create(task));
        fixture.AssertNoWorkspace();
        await using var workspace = await fixture.Create(fixture.Task());
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.ReadFileBytesAsync(path));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CancellationBeforeOrDuringStagingRemovesPartialWorkspace(int cancelAtCreate)
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var files = new Dictionary<string, byte[]> { ["one.bin"] = [1], ["two.bin"] = [2] };
        var task = fixture.Task(files);
        var count = 0;
        if (cancelAtCreate == 0) cancellation.Cancel();
        var hooks = new GmWorkerExecutionWorkspaceHooks
        {
            BeforeWorkspaceFileCreateAsync = _ =>
            {
                if (++count == cancelAtCreate) cancellation.Cancel();
                return Task.CompletedTask;
            }
        };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Create(task, hooks, cancellation.Token));
        fixture.AssertNoWorkspace();
        fixture.AssertCanonical(files);
    }

    [Fact]
    public async Task PartialCreationFailureKeepsOriginalErrorAndCleansOwnedTree()
    {
        using var fixture = new Fixture();
        var task = fixture.Task(new() { ["one.bin"] = [1], ["two.bin"] = [2] });
        var count = 0;
        var expected = new IOException("synthetic staging failure");
        var hooks = new GmWorkerExecutionWorkspaceHooks
        {
            BeforeWorkspaceFileCreateAsync = _ => ++count == 2 ? Task.FromException(expected) : Task.CompletedTask
        };
        var actual = await Assert.ThrowsAsync<IOException>(() => fixture.Create(task, hooks));
        Assert.Same(expected, actual);
        fixture.AssertNoWorkspace();
    }

    [Theory]
    [InlineData(false, -1)]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(true, -1)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    public async Task ArtifactReadEnforcesExactByteLimit(bool proposal, int delta)
    {
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create(fixture.Task());
        var limit = proposal ? GmWorkerBridgePool.MaxProposalBytes : GmWorkerBridgePool.MaxContentRefBytes;
        var bytes = new byte[limit + delta];
        bytes[0] = 0xff;
        bytes[^1] = 0x7f;
        File.WriteAllBytes(proposal ? workspace.ProposalPath : Path.Combine(workspace.GameSessionPath, "artifact.bin"), bytes);
        Task<byte[]?> Read() => proposal ? workspace.ReadProposalBytesAsync() : workspace.ReadFileBytesAsync("artifact.bin");
        if (delta > 0) await Assert.ThrowsAsync<InvalidDataException>(Read);
        else Assert.Equal(bytes, await Read());
    }

    [Fact]
    public async Task CancellationAfterReadBoundaryIsObservedBeforeOpening()
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var hooks = new GmWorkerExecutionWorkspaceHooks
        {
            BeforeWorkspaceFileOpenAsync = _ => { cancellation.Cancel(); return Task.CompletedTask; }
        };
        await using var workspace = await fixture.Create(fixture.Task(), hooks);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workspace.ReadFileBytesAsync("missing.bin", cancellation.Token));
    }

    [Theory]
    [InlineData("file-link")]
    [InlineData("parent-link")]
    [InlineData("fifo")]
    [InlineData("directory")]
    public async Task LinuxReadRejectsLinkAndSpecialKindsWithoutFollowingOrOpening(string kind)
    {
        if (!OperatingSystem.IsLinux()) return; // Native Linux fixture; no Windows execution claim.
        using var fixture = new Fixture();
        var outside = Path.Combine(fixture.Root, "outside");
        Directory.CreateDirectory(outside);
        var outsideFile = Path.Combine(outside, "value.bin");
        File.WriteAllBytes(outsideFile, [8, 9]);
        await using var workspace = await fixture.Create(fixture.Task());
        var path = Path.Combine(workspace.GameSessionPath, "bad");
        var relative = "bad";
        if (kind == "file-link") File.CreateSymbolicLink(path, outsideFile);
        else if (kind == "parent-link") { Directory.CreateSymbolicLink(path, outside); relative = "bad/value.bin"; }
        else if (kind == "fifo") Assert.Equal(0, MkFifo(path, 0x180));
        else Directory.CreateDirectory(path);
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => workspace.ReadFileBytesAsync(relative));
            Assert.Equal(new byte[] { 8, 9 }, File.ReadAllBytes(outsideFile));
        }
        finally
        {
            if (kind is "parent-link" or "directory") Directory.Delete(path);
            else File.Delete(path);
        }
    }

    [Fact]
    public async Task LinuxWriteRevalidatesParentAfterBoundaryAndRetainsUnexpectedLink()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        var outside = Path.Combine(fixture.Root, "outside");
        Directory.CreateDirectory(outside);
        string? link = null;
        var hooks = new GmWorkerExecutionWorkspaceHooks
        {
            BeforeWorkspaceFileCreateAsync = path =>
            {
                link = Path.GetDirectoryName(path)!;
                Directory.Delete(link);
                Directory.CreateSymbolicLink(link, outside);
                return Task.CompletedTask;
            }
        };
        var task = fixture.Task(new() { ["nested/value.bin"] = [1, 2] });
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Create(task, hooks));
        Assert.Empty(Directory.GetFileSystemEntries(outside));
        Assert.NotNull(link);
        Assert.NotNull(new DirectoryInfo(link!).LinkTarget);
        Directory.Delete(link!); // Remove only the test-created obstruction; fixture then owns ordinary cleanup.
    }

    [Theory]
    [InlineData("link")]
    [InlineData("fifo")]
    public async Task LinuxDisposePreflightsWholeTreeThenRetriesAfterObstructionRemoved(string kind)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        var workspace = await fixture.Create(fixture.Task(new() { ["ordinary.bin"] = [1, 2] }));
        var root = Path.GetDirectoryName(workspace.GameSessionPath)!;
        var path = Path.Combine(workspace.GameSessionPath, "obstruction");
        var outside = Path.Combine(fixture.Root, "outside.bin");
        File.WriteAllBytes(outside, [9, 8]);
        if (kind == "link") File.CreateSymbolicLink(path, outside);
        else Assert.Equal(0, MkFifo(path, 0x180));
        try
        {
            var error = await Assert.ThrowsAsync<AggregateException>(() => workspace.DisposeAsync().AsTask());
            Assert.Contains(error.InnerExceptions, ex => ex is InvalidDataException);
            Assert.True(File.Exists(workspace.TaskPath));
            Assert.Equal(new byte[] { 1, 2 }, File.ReadAllBytes(Path.Combine(workspace.GameSessionPath, "ordinary.bin")));
            Assert.Equal(new byte[] { 9, 8 }, File.ReadAllBytes(outside));
        }
        finally { File.Delete(path); await workspace.DisposeAsync(); }
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task DisposeFailureCanRetryAndNeverDeletesSiblingWorkspace()
    {
        using var fixture = new Fixture();
        var calls = 0;
        var hooks = new GmWorkerExecutionWorkspaceHooks
        {
            BeforeWorkspaceDeleteAsync = _ => ++calls == 1 ? Task.FromException(new IOException("synthetic cleanup failure")) : Task.CompletedTask
        };
        var first = await fixture.Create(fixture.Task(), hooks);
        await using var second = await fixture.Create(fixture.Task());
        await Assert.ThrowsAsync<AggregateException>(() => first.DisposeAsync().AsTask());
        Assert.True(File.Exists(first.TaskPath));
        await first.DisposeAsync();
        await first.DisposeAsync();
        Assert.Equal(2, calls);
        Assert.False(Directory.Exists(Path.GetDirectoryName(first.GameSessionPath)));
        Assert.True(File.Exists(second.TaskPath));
    }

    [Fact]
    public async Task LinuxCaseDistinctNamesKeepTheirExactBytes()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        await using var workspace = await fixture.Create(fixture.Task(new() { ["Case.bin"] = [1], ["case.bin"] = [2] }));
        Assert.Equal(new byte[] { 1 }, await workspace.ReadFileBytesAsync("Case.bin"));
        Assert.Equal(new byte[] { 2 }, await workspace.ReadFileBytesAsync("case.bin"));
        Assert.Null(await workspace.ReadFileBytesAsync("CASE.bin"));
    }

    [Fact]
    public async Task LinuxHardlinkedArtifactReadAndUnlinkCleanupPreserveOutsideBytes()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        var outside = Path.Combine(fixture.Root, "outside.bin");
        File.WriteAllBytes(outside, [9, 8]);
        var workspace = await fixture.Create(fixture.Task());
        Assert.Equal(0, Link(outside, Path.Combine(workspace.GameSessionPath, "alias.bin")));
        try { Assert.Equal(new byte[] { 9, 8 }, await workspace.ReadFileBytesAsync("alias.bin")); }
        finally { await workspace.DisposeAsync(); }
        Assert.Equal(new byte[] { 9, 8 }, File.ReadAllBytes(outside));
    }

    [Fact]
    public async Task LinuxCreateNewRejectsHardlinkWithoutChangingOutsideBytes()
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        var outside = Path.Combine(fixture.Root, "outside.bin");
        File.WriteAllBytes(outside, [9, 8]);
        var hooks = new GmWorkerExecutionWorkspaceHooks
        {
            BeforeWorkspaceFileCreateAsync = path => { Assert.Equal(0, Link(outside, path)); return Task.CompletedTask; }
        };
        await Assert.ThrowsAnyAsync<IOException>(() => fixture.Create(fixture.Task(new() { ["value.bin"] = [1, 2] }), hooks));
        fixture.AssertNoWorkspace();
        Assert.Equal(new byte[] { 9, 8 }, File.ReadAllBytes(outside));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LinuxRuntimeBaseLinkIsRejectedBeforeCreatingOutsideDescendants(bool linkAtHook)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        var outside = Path.Combine(fixture.Root, "outside");
        Directory.CreateDirectory(outside);
        string? link = null;
        GmWorkerExecutionWorkspaceHooks? hooks = null;
        if (linkAtHook)
        {
            hooks = new() { BeforeRuntimeRootCreateAsync = (parent, _) =>
            {
                Directory.Delete(parent);
                Directory.CreateSymbolicLink(parent, outside);
                link = parent;
                return Task.CompletedTask;
            }};
        }
        else
        {
            link = fixture.RuntimeBase;
            Directory.CreateSymbolicLink(link, outside);
        }
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Create(fixture.Task(), hooks));
            Assert.Empty(Directory.GetFileSystemEntries(outside));
        }
        finally { if (link != null) Directory.Delete(link); }
    }

    [Fact]
    public async Task RuntimeInsideCanonicalSessionIsRejectedWithoutMutation()
    {
        using var fixture = new Fixture();
        var path = Path.Combine(fixture.Fs.GameSessionPath, "forbidden-runtime");
        await Assert.ThrowsAsync<InvalidOperationException>(() => GmWorkerExecutionWorkspace.CreateAsync(fixture.Fs, fixture.Task(), default, configuredRuntimeBase: path));
        Assert.False(Directory.Exists(path));
    }

    [Theory]
    [InlineData("write", "same-length")]
    [InlineData("write", "grow")]
    [InlineData("write", "truncate")]
    [InlineData("read", "same-length")]
    [InlineData("read", "grow")]
    [InlineData("read", "truncate")]
    public async Task LinuxChangedImageAtTransferBoundaryIsRejected(string operation, string mutation)
    {
        if (!OperatingSystem.IsLinux()) return;
        using var fixture = new Fixture();
        var files = new Dictionary<string, byte[]> { ["value.bin"] = [1, 2, 3] };
        Task Mutate(string path)
        {
            File.WriteAllBytes(path, mutation == "same-length" ? [9, 8, 7] : mutation == "grow" ? [1, 2, 3, 4] : [1]);
            return Task.CompletedTask;
        }
        var hooks = new GmWorkerExecutionWorkspaceHooks
        {
            AfterWorkspaceFileWriteAsync = operation == "write" ? Mutate : null,
            AfterWorkspaceFileReadAsync = operation == "read" ? Mutate : null
        };
        if (operation == "write")
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Create(fixture.Task(files), hooks));
            fixture.AssertNoWorkspace();
        }
        else
        {
            await using var workspace = await fixture.Create(fixture.Task(files), hooks);
            await Assert.ThrowsAsync<InvalidDataException>(() => workspace.ReadFileBytesAsync("value.bin"));
        }
        fixture.AssertCanonical(files);
    }

    [Theory]
    [InlineData("write")]
    [InlineData("read")]
    [InlineData("read-chunk")]
    public async Task CancellationAtTransferBoundaryCannotReturnSuccess(string boundary)
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var hooks = new GmWorkerExecutionWorkspaceHooks
        {
            AfterWorkspaceFileWriteAsync = boundary == "write" ? _ => { cancellation.Cancel(); return Task.CompletedTask; } : null,
            AfterWorkspaceFileReadAsync = boundary == "read" ? _ => { cancellation.Cancel(); return Task.CompletedTask; } : null,
            AfterWorkspaceReadChunkAsync = boundary == "read-chunk" ? (_, _) => { cancellation.Cancel(); return Task.CompletedTask; } : null
        };
        var task = fixture.Task(new() { ["value.bin"] = new byte[256 * 1024] });
        if (boundary == "write")
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Create(task, hooks, cancellation.Token));
            fixture.AssertNoWorkspace();
        }
        else
        {
            await using var workspace = await fixture.Create(task, hooks);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => workspace.ReadFileBytesAsync("value.bin", cancellation.Token));
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "boe-detached-workspace-" + Guid.NewGuid().ToString("N"));
        internal FileSystemManager Fs { get; }
        internal string RuntimeBase => Path.Combine(Root, "runtime");
        internal Fixture()
        {
            Directory.CreateDirectory(Root);
            Fs = new FileSystemManager(Path.Combine(Root, "canonical"), NullLogger<FileSystemManager>.Instance);
            Fs.EnsureDirectoryStructure();
        }
        internal string Canonical(string path) => Path.Combine(Fs.GameSessionPath, path.Replace('/', Path.DirectorySeparatorChar));
        internal WorkerTaskPacket Task(Dictionary<string, byte[]>? files = null)
        {
            files ??= new();
            foreach (var (path, bytes) in files)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Canonical(path))!);
                File.WriteAllBytes(Canonical(path), bytes);
            }
            return GmWorkerBridgeTestFixtures.AnalysisTask() with
            {
                ContextFiles = files.Select(pair => new WorkerFileReference
                { Path = pair.Key, Sha256 = Convert.ToHexString(SHA256.HashData(pair.Value)).ToLowerInvariant() }).ToList()
            };
        }
        internal Task<GmWorkerExecutionWorkspace> Create(WorkerTaskPacket task, GmWorkerExecutionWorkspaceHooks? hooks = null, CancellationToken token = default) =>
            GmWorkerExecutionWorkspace.CreateAsync(Fs, task, token, hooks, RuntimeBase);
        internal void AssertNoWorkspace()
        {
            var runtime = GmWorkerExecutionWorkspace.ResolveRuntimeRoot(Fs.BasePath, RuntimeBase);
            if (Directory.Exists(runtime)) Assert.Empty(Directory.GetFileSystemEntries(runtime));
        }
        internal void AssertCanonical(Dictionary<string, byte[]> files)
        {
            foreach (var (path, bytes) in files) Assert.Equal(bytes, File.ReadAllBytes(Canonical(path)));
        }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(string existing, string created);

    [DllImport("libc", EntryPoint = "mkfifo", SetLastError = true)]
    private static extern int MkFifo(string path, uint mode);
}
