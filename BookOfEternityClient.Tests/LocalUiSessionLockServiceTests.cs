using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class LocalUiSessionLockServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ManualTimeProvider _timeProvider = new(new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero));

    public LocalUiSessionLockServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-local-ui-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task AcquireOrRefreshAsync_NewLock_WritesOwnerAndHeartbeat()
    {
        var service = new LocalUiSessionLockService(_fs, _timeProvider);

        var result = await service.AcquireOrRefreshAsync(Owner("console-main"), "духовное действие");

        Assert.True(result.Acquired, result.BlockerMessage);
        var lockRoot = await ReadLockRootAsync();
        Assert.Equal("console-main", lockRoot["ownerId"]!.GetValue<string>());
        Assert.Equal("console", lockRoot["ownerKind"]!.GetValue<string>());
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime.ToString("O"), lockRoot["heartbeatAtUtc"]!.GetValue<string>());
    }

    [Fact]
    public async Task AcquireOrRefreshAsync_SameOwner_RefreshesHeartbeat()
    {
        var service = new LocalUiSessionLockService(_fs, _timeProvider);
        await service.AcquireOrRefreshAsync(Owner("console-main"), "первое действие");
        _timeProvider.Advance(TimeSpan.FromSeconds(30));

        var result = await service.AcquireOrRefreshAsync(Owner("console-main"), "второе действие");

        Assert.True(result.Acquired, result.BlockerMessage);
        var lockRoot = await ReadLockRootAsync();
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime.ToString("O"), lockRoot["heartbeatAtUtc"]!.GetValue<string>());
        Assert.Equal("второе действие", lockRoot["lastOperation"]!.GetValue<string>());
    }

    [Fact]
    public async Task AcquireOrRefreshAsync_ActiveOtherOwner_BlocksMutation()
    {
        var service = new LocalUiSessionLockService(_fs, _timeProvider);
        await service.AcquireOrRefreshAsync(Owner("console-main", "Консоль"), "духовное действие");

        var result = await service.AcquireOrRefreshAsync(Owner("browser-tab", "Браузер"), "казначейство");

        Assert.False(result.Acquired);
        Assert.Contains("заблокировано", result.BlockerMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Консоль", result.BlockerMessage, StringComparison.OrdinalIgnoreCase);
        var lockRoot = await ReadLockRootAsync();
        Assert.Equal("console-main", lockRoot["ownerId"]!.GetValue<string>());
    }

    [Fact]
    public async Task AcquireOrRefreshAsync_StaleOtherOwner_ReplacesLock()
    {
        var service = new LocalUiSessionLockService(_fs, _timeProvider);
        await service.AcquireOrRefreshAsync(Owner("console-main"), "духовное действие");
        _timeProvider.Advance(TimeSpan.FromMinutes(3));

        var result = await service.AcquireOrRefreshAsync(Owner("browser-tab", "Браузер"), "казначейство");

        Assert.True(result.Acquired, result.BlockerMessage);
        var lockRoot = await ReadLockRootAsync();
        Assert.Equal("browser-tab", lockRoot["ownerId"]!.GetValue<string>());
        Assert.Equal("Браузер", lockRoot["ownerLabel"]!.GetValue<string>());
    }

    [Fact]
    public async Task AcquireOrRefreshAsync_MalformedStaleLock_ReplacesLock()
    {
        await _fs.WriteFileAtomicAsync(LocalUiSessionLockService.LockPath, "{ not-json");
        File.SetLastWriteTimeUtc(_fs.ResolvePath(LocalUiSessionLockService.LockPath), _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(-3));
        var service = new LocalUiSessionLockService(_fs, _timeProvider);

        var result = await service.AcquireOrRefreshAsync(Owner("browser-tab", "Браузер"), "казначейство");

        Assert.True(result.Acquired, result.BlockerMessage);
        var lockRoot = await ReadLockRootAsync();
        Assert.Equal("browser-tab", lockRoot["ownerId"]!.GetValue<string>());
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

    private static LocalUiSessionLockOwner Owner(string id, string label = "Консоль") =>
        new(id, "console", label, TimeSpan.FromMinutes(2));

    private async Task<JsonObject> ReadLockRootAsync()
    {
        var json = await _fs.ReadFileAsync(LocalUiSessionLockService.LockPath);
        Assert.False(string.IsNullOrWhiteSpace(json));
        return JsonNode.Parse(json!)!.AsObject();
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan offset) => _utcNow = _utcNow.Add(offset);
    }
}
