using System.Text;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class BrowserRollbackLinuxTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-browser-linux-" + Guid.NewGuid().ToString("N"));
    private const string Existing = "game_state/meta/browser-existing.bin";
    private const string Absent = "game_state/meta/browser-absent.bin";
    private static readonly byte[] Before = [0xEF, 0xBB, 0xBF, 0, 0xFF, 41];
    private static readonly byte[] After = Encoding.UTF8.GetBytes("Unicode: Привет 世界\n");

    private async Task<(FileSystemManager Files, BrowserLocalWriteCoordinator Coordinator)> CreateAsync()
    {
        Assert.True(OperatingSystem.IsLinux(), "Linux body execution required.");
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance);
        var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        await state.BootstrapLocalStorageAsync();
        await files.WriteFileAtomicBytesAsync(Existing, Before);
        return (files, new(files, new LocalUiSessionLockService(files)));
    }

    [Fact]
    public async Task RealCoordinator_CommitsExistingAndAbsentMembers()
    {
        var (files, coordinator) = await CreateAsync();
        var ran = false;
        var result = await coordinator.ExecuteAtomicAsync(new("linux-browser", "Fixture", "commit"), [Existing, Absent], async lease =>
        {
            ran = true;
            await files.WriteFileAtomicBytesAsync(lease, Existing, After);
            await files.WriteFileAtomicBytesAsync(lease, Absent, After);
        });
        output.WriteLine(result.Message);
        Assert.True(result.Success, result.Message);
        Assert.True(ran);
        Assert.Equal(After, File.ReadAllBytes(files.ResolvePath(Existing)));
        Assert.Equal(After, File.ReadAllBytes(files.ResolvePath(Absent)));
        AssertNoEvidence(files);
    }

    [Fact]
    public async Task RealCoordinator_CallbackFailureRestoresExactBaselineAndAbsence()
    {
        var (files, coordinator) = await CreateAsync();
        var ran = false;
        var result = await coordinator.ExecuteAtomicAsync(new("linux-browser", "Fixture", "rollback"), [Existing, Absent], async lease =>
        {
            ran = true;
            await files.WriteFileAtomicBytesAsync(lease, Existing, After);
            await files.WriteFileAtomicBytesAsync(lease, Absent, After);
            throw new InvalidOperationException("causal callback failure");
        });
        output.WriteLine(result.Message);
        Assert.True(ran, result.Message);
        Assert.False(result.Success);
        Assert.Contains("rollback восстановлен", result.Message);
        Assert.Equal(Before, File.ReadAllBytes(files.ResolvePath(Existing)));
        Assert.False(File.Exists(files.ResolvePath(Absent)));
        AssertNoEvidence(files);
    }

    [Fact]
    public async Task RealCoordinator_ExplicitOriginalLeasePublishesWithoutAmbientAuthority()
    {
        var (files, coordinator) = await CreateAsync();
        var ran = false;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var result = await coordinator.ExecuteAtomicWithinTransactionAsync(lease, new("linux-browser", "Fixture", "held lease"), [Existing], async original =>
        {
            Assert.Same(lease, original);
            ran = true;
            await files.WriteFileAtomicBytesAsync(original, Existing, After);
        }).WaitAsync(TimeSpan.FromSeconds(5));
        output.WriteLine(result.Message);
        Assert.True(result.Success, result.Message);
        Assert.True(ran);
        Assert.Equal(After, File.ReadAllBytes(files.ResolvePath(Existing)));
        AssertNoEvidence(files);
    }

    private static void AssertNoEvidence(FileSystemManager files)
    {
        Assert.False(File.Exists(files.ResolvePath(LocalUiSessionLockService.LockPath)));
        var rollback = files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root);
        Assert.False(Directory.Exists(rollback) && Directory.EnumerateFileSystemEntries(rollback).Any());
        Assert.False(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        output.WriteLine("FixtureCleanup rootRemoved=" + !Directory.Exists(_root));
    }
}
