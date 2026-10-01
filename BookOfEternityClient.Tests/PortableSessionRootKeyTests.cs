using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableSessionRootKeyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-root-key-" + Guid.NewGuid().ToString("N"));
    private FileSystemManager Manager(string? root = null) => new(root ?? _root, NullLogger<FileSystemManager>.Instance);

    [Theory]
    [InlineData(@"C:\session", @"\\?\C:\session")]
    [InlineData(@"C:\session", @"\\?\C:\session\")]
    [InlineData(@"\\server\share\session", @"\\?\UNC\server\share\session")]
    [InlineData(@"\\server\share\session", "//?/UNC/server/share/session/")]
    public void RootKeysUnifySupportedDriveAndUncSpellings(string ordinary, string extended)
    {
        Assert.Equal(ordinary, CanonicalRootIdentityInterner.NormalizeRootKey(ordinary, windows: true));
        Assert.Equal(ordinary, CanonicalRootIdentityInterner.NormalizeRootKey(extended, windows: true));
    }

    [Fact]
    public void LinuxRootKeysKeepCaseDistinctNamesSeparate()
    {
        Assert.NotEqual(
            CanonicalRootIdentityInterner.NormalizeRootKey("/scope/Case", windows: false),
            CanonicalRootIdentityInterner.NormalizeRootKey("/scope/case", windows: false));
        if (!OperatingSystem.IsLinux()) return;
        var first = Manager(Path.Combine(_root, "Case"));
        var second = Manager(Path.Combine(_root, "case"));
        Assert.NotSame(first.CanonicalRootAuthorityIdentity, second.CanonicalRootAuthorityIdentity);
    }

    [Fact]
    public async Task LinuxClearRetainsDistinctMemberImagesAcrossRollbackThenDeletesBoth()
    {
        if (!OperatingSystem.IsLinux()) return;
        var files = Manager(); files.EnsureDirectoryStructure();
        const string upper = "game_state/core/Case.json", lower = "game_state/core/case.json";
        await files.WriteFileAtomicBytesAsync(upper, [0xFF, 0]);
        await files.WriteFileAtomicBytesAsync(lower, []);
        var beforeGeneration = File.ReadAllBytes(files.SessionGenerationPath);
        var interrupted = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (phase == TrustedLocalPublicationPhase.CommitStaged) throw new CaseCut();
                }
            });
        await Assert.ThrowsAsync<CaseCut>(() => interrupted.ClearGameStateAsync());
        Assert.Equal(new byte[] { 0xFF, 0 }, File.ReadAllBytes(files.ResolvePath(upper)));
        Assert.Empty(File.ReadAllBytes(files.ResolvePath(lower)));
        Assert.Equal(beforeGeneration, File.ReadAllBytes(files.SessionGenerationPath));
        await files.ClearGameStateAsync();
        Assert.False(File.Exists(files.ResolvePath(upper)));
        Assert.False(File.Exists(files.ResolvePath(lower)));
    }

    [Fact]
    public async Task WindowsAliasesShareRevisionIdentityWithoutRewritingBaseOrJournalPaths()
    {
        if (!OperatingSystem.IsWindows()) return;
        var ordinary = Manager(); ordinary.EnsureDirectoryStructure();
        var extended = Manager(ExtendedRoot());
        Assert.NotEqual(ordinary.BasePath, extended.BasePath);
        Assert.NotEqual(ordinary.SessionGenerationPath, extended.SessionGenerationPath);
        Assert.Same(ordinary.CanonicalRootAuthorityIdentity, extended.CanonicalRootAuthorityIdentity);
        var before = ordinary.CanonicalRootAuthorityIdentity.SessionGenerationRevision;
        extended.CanonicalRootAuthorityIdentity.AdvanceSessionGenerationRevision();
        Assert.Equal(before + 1, ordinary.CanonicalRootAuthorityIdentity.SessionGenerationRevision);
        string generation;
        await using (var lease = await ordinary.AcquireCanonicalWriteLeaseAsync())
            generation = ordinary.GetOrCreateSessionGeneration(lease);
        await using (var lease = await extended.AcquireCanonicalWriteLeaseAsync())
            Assert.Equal(generation, extended.ReadLocalGenerationSnapshot(lease).Binding.Id);
    }

    [Fact]
    public async Task WindowsAliasBoundWriterCannotMutateTheReplacementSessionBeforeFinalFence()
    {
        if (!OperatingSystem.IsWindows()) return;
        var ordinary = Manager(); ordinary.EnsureDirectoryStructure();
        var extended = Manager(ExtendedRoot());
        string generation;
        await using (var lease = await ordinary.AcquireCanonicalWriteLeaseAsync())
            generation = ordinary.GetOrCreateSessionGeneration(lease);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        const string target = "game_state/core/alias-boundary.json";
        var oldOperation = Task.Run(() => SessionOperationContext.RunBoundAsync(ordinary, generation, async () =>
        {
            started.TrySetResult();
            await release.Task;
            await extended.WriteFileAtomicBytesAsync(target, [99]);
        }));
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await ordinary.ClearGameStateAsync();
            await ordinary.WriteFileAtomicBytesAsync(target, [42]);
            release.TrySetResult();
            await Assert.ThrowsAsync<SessionReplacedException>(() => oldOperation.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(ordinary.ResolvePath(target)));
        }
        finally
        {
            release.TrySetResult();
            try { await oldOperation.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (SessionReplacedException) { }
        }
    }

    private string ExtendedRoot() => _root.StartsWith(@"\\", StringComparison.Ordinal)
        ? @"\\?\UNC\" + _root[2..] : @"\\?\" + _root;
    private sealed class CaseCut : Exception { }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
