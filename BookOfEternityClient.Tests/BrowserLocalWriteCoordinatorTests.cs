using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BrowserLocalWriteCoordinatorTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ManualTimeProvider _timeProvider = new(new DateTimeOffset(2026, 5, 21, 10, 0, 0, TimeSpan.Zero));

    public BrowserLocalWriteCoordinatorTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-write-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task BuildStatusAsync_PendingTurnArtifacts_BlockBrowserWrites()
    {
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "{}");
        await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot.json", "{}");
        var coordinator = CreateCoordinator();

        var status = await coordinator.BuildStatusAsync();

        Assert.False(status.CanStartBrowserWrite);
        Assert.True(status.PendingTurn.HasActiveGmTurn);
        Assert.Contains(status.PendingTurn.Artifacts, static item =>
            item.Exists && string.Equals(item.Path, "input/turn_request.json", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(status.PendingTurn.Artifacts, static item =>
            item.Exists && string.Equals(item.Path, "game_state/control/pending_turn_snapshot.json", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteAsync_ActiveOtherOwner_BlocksWithoutRunningWrite()
    {
        var lockService = new LocalUiSessionLockService(_fs, _timeProvider);
        await lockService.AcquireOrRefreshAsync(Owner("console-owner", "Консоль"), "console write");
        var coordinator = CreateCoordinator(lockService);
        var ran = false;

        var result = await coordinator.ExecuteAsync(
            new BrowserLocalWriteRequest("browser-owner", "Browser", "browser write"),
            ["game_state/meta/test_state.json"],
            () =>
            {
                ran = true;
                return Task.CompletedTask;
            });

        Assert.False(result.Success);
        Assert.False(ran);
        Assert.Contains("заблокировано", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists("game_state/meta/test_state.json"));
    }

    [Fact]
    public async Task BuildStatusAsync_FreshMalformedLock_BlocksBrowserWrites()
    {
        await _fs.WriteFileAtomicAsync(LocalUiSessionLockService.LockPath, "{ not-json");
        File.SetLastWriteTimeUtc(_fs.ResolvePath(LocalUiSessionLockService.LockPath), _timeProvider.GetUtcNow().UtcDateTime);
        var coordinator = CreateCoordinator();

        var status = await coordinator.BuildStatusAsync();

        Assert.False(status.CanStartBrowserWrite);
        Assert.True(status.LocalUiLock.Exists);
        Assert.False(status.LocalUiLock.IsReadable);
        Assert.False(status.LocalUiLock.IsStale);
    }

    [Fact]
    public async Task ExecuteAsync_StaleOtherOwner_TakesOverAndRunsWrite()
    {
        var lockService = new LocalUiSessionLockService(_fs, _timeProvider);
        await lockService.AcquireOrRefreshAsync(Owner("console-owner", "Консоль"), "console write");
        _timeProvider.Advance(TimeSpan.FromMinutes(3));
        var coordinator = CreateCoordinator(lockService);

        var result = await coordinator.ExecuteAsync(
            new BrowserLocalWriteRequest("browser-owner", "Browser", "browser write"),
            ["game_state/meta/test_state.json"],
            () => _fs.WriteFileAtomicAsync("game_state/meta/test_state.json", "{\"ok\":true}"));

        Assert.True(result.Success, result.Message);
        Assert.Equal("{\"ok\":true}", await _fs.ReadFileAsync("game_state/meta/test_state.json"));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task ExecuteAsync_FailedWrite_RestoresRollbackFilesAndReleasesLock()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/existing.json", "{\"value\":1}");
        var coordinator = CreateCoordinator();

        var result = await coordinator.ExecuteAsync(
            new BrowserLocalWriteRequest("browser-owner", "Browser", "browser write"),
            ["game_state/meta/existing.json", "game_state/meta/new_file.json"],
            async () =>
            {
                await _fs.WriteFileAtomicAsync("game_state/meta/existing.json", "{\"value\":2}");
                await _fs.WriteFileAtomicAsync("game_state/meta/new_file.json", "{\"created\":true}");
                throw new InvalidOperationException("simulated browser write failure");
            });

        Assert.False(result.Success);
        Assert.Contains("simulated browser write failure", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("{\"value\":1}", await _fs.ReadFileAsync("game_state/meta/existing.json"));
        Assert.False(_fs.FileExists("game_state/meta/new_file.json"));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignored
        }
    }

    private BrowserLocalWriteCoordinator CreateCoordinator(LocalUiSessionLockService? lockService = null) =>
        new(_fs, lockService ?? new LocalUiSessionLockService(_fs, _timeProvider), _timeProvider);

    private static LocalUiSessionLockOwner Owner(string id, string label) =>
        new(id, "console", label, TimeSpan.FromMinutes(2));

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan offset) => _utcNow = _utcNow.Add(offset);
    }
}
