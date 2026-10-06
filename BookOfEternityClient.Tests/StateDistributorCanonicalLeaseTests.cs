using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class StateDistributorCanonicalLeaseTests : IDisposable
{
    private const string WeatherPath = "game_state/world/weather.json";
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;

    public StateDistributorCanonicalLeaseTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "boe-state-distributor-lease-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = CreateFileSystem();
    }

    [Fact]
    public async Task DistributeAsync_FailureAfterBackupCapture_DoesNotOverwriteConcurrentAcceptedWriter()
    {
        await _fs.WriteFileAtomicAsync(WeatherPath, "{\"marker\":\"baseline\"}");
        var backupsCaptured = NewSignal();
        var releaseFailure = NewSignal();
        var writerContended = NewSignal();
        var distributor = new StateDistributor(
            _fs,
            NullLogger<StateDistributor>.Instance,
            new StateDistributorHooks
            {
                AfterBackupsCapturedAsync = async () =>
                {
                    backupsCaptured.TrySetResult(true);
                    await releaseFailure.Task;
                    throw new IOException("Injected failure after backup capture.");
                }
            });
        var writerFs = CreateFileSystem(new FileSystemManagerHooks
        {
            MainOwnerLockContendedAsync = () =>
            {
                writerContended.TrySetResult(true);
                return Task.CompletedTask;
            },
            CanonicalWriteLockContendedAsync = () =>
            {
                writerContended.TrySetResult(true);
                return Task.CompletedTask;
            }
        });

        var distributionTask = distributor.DistributeAsync(CreateWeatherResponse());
        var backupBoundary = await Task.WhenAny(distributionTask, backupsCaptured.Task).WaitAsync(TimeSpan.FromSeconds(5));
        if (backupBoundary == distributionTask) await distributionTask;
        Assert.Same(backupsCaptured.Task, backupBoundary);
        var writerTask = writerFs.WriteFileAtomicAsync(
            WeatherPath,
            "{\"marker\":\"accepted-concurrent-writer\"}");
        var firstObserved = await Task.WhenAny(writerTask, writerContended.Task)
            .WaitAsync(TimeSpan.FromSeconds(5));
        releaseFailure.TrySetResult(true);

        await Assert.ThrowsAsync<IOException>(() => distributionTask);
        await writerTask;

        Assert.Same(writerContended.Task, firstObserved);
        Assert.Equal(
            "accepted-concurrent-writer",
            ReadMarker(await _fs.ReadFileAsync(WeatherPath)));
    }

    [Fact]
    public async Task DistributeAsync_FailureAfterFirstWrite_DoesNotRollbackConcurrentAcceptedWriter()
    {
        await _fs.WriteFileAtomicAsync(WeatherPath, "{\"marker\":\"baseline\"}");
        var mutationApplied = NewSignal();
        var releaseFailure = NewSignal();
        var writerContended = NewSignal();
        var distributor = new StateDistributor(
            _fs,
            NullLogger<StateDistributor>.Instance,
            new StateDistributorHooks
            {
                AfterFileMutationAppliedAsync = async path =>
                {
                    if (!path.Equals(WeatherPath, StringComparison.OrdinalIgnoreCase))
                        return;
                    mutationApplied.TrySetResult(true);
                    await releaseFailure.Task;
                    throw new IOException("Injected failure after first mutation.");
                }
            });
        var writerFs = CreateFileSystem(new FileSystemManagerHooks
        {
            CanonicalWriteLockContendedAsync = () =>
            {
                writerContended.TrySetResult(true);
                return Task.CompletedTask;
            }
        });

        var distributionTask = distributor.DistributeAsync(CreateWeatherResponse());
        var mutationBoundary = await Task.WhenAny(distributionTask, mutationApplied.Task).WaitAsync(TimeSpan.FromSeconds(5));
        if (mutationBoundary == distributionTask) await distributionTask;
        Assert.Same(mutationApplied.Task, mutationBoundary);
        var writerTask = writerFs.WriteFileAtomicAsync(
            WeatherPath,
            "{\"marker\":\"accepted-concurrent-writer\"}");
        var firstObserved = await Task.WhenAny(writerTask, writerContended.Task)
            .WaitAsync(TimeSpan.FromSeconds(5));
        releaseFailure.TrySetResult(true);

        await Assert.ThrowsAsync<IOException>(() => distributionTask);
        await writerTask;

        Assert.Same(writerContended.Task, firstObserved);
        Assert.Equal(
            "accepted-concurrent-writer",
            ReadMarker(await _fs.ReadFileAsync(WeatherPath)));
        Assert.Empty(Directory.GetFiles(
            Path.GetDirectoryName(_fs.ResolvePath(WeatherPath))!,
            "weather.json.backup.*"));
    }

    [Fact]
    public async Task DistributeAsync_BackupCleanupFailure_DoesNotRollbackCommittedDistribution()
    {
        await _fs.WriteFileAtomicAsync(WeatherPath, "{\"marker\":\"baseline\"}");
        var distributor = new StateDistributor(
            _fs,
            NullLogger<StateDistributor>.Instance,
            new StateDistributorHooks
            {
                BeforeBackupCleanupAsync = () =>
                    throw new IOException("Injected backup cleanup failure.")
            });

        var modified = await distributor.DistributeAsync(CreateWeatherResponse());

        Assert.Contains(WeatherPath, modified);
        Assert.Contains(
            "weatherChange",
            await _fs.ReadFileAsync(WeatherPath),
            StringComparison.Ordinal);
        Assert.NotEmpty(Directory.GetFiles(
            Path.GetDirectoryName(_fs.ResolvePath(WeatherPath))!,
            "weather.json.backup.*"));
    }

    [Fact]
    public async Task DistributeAsync_MalformedExistingJson_FailsClosedAndPreservesExactBytes()
    {
        var malformedBytes = new byte[] { 0xEF, 0xBB, 0xBF, (byte)'{', (byte)'"', (byte)'b', (byte)'r', (byte)'o', (byte)'k', (byte)'e', (byte)'n' };
        await _fs.WriteFileAtomicBytesAsync(WeatherPath, malformedBytes);
        var distributor = new StateDistributor(
            _fs,
            NullLogger<StateDistributor>.Instance);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => distributor.DistributeAsync(CreateWeatherResponse()));

        Assert.Equal(malformedBytes, await _fs.ReadFileBytesAsync(WeatherPath));
    }

    [Fact]
    public async Task DistributeAsync_EffectCommandsRemainClosedTransientRootWithoutTimestampMetadata()
    {
        var distributor = new StateDistributor(
            _fs,
            NullLogger<StateDistributor>.Instance);
        var response = new GameResponse
        {
            EffectChanges = JsonSerializer.Deserialize<JsonElement[]>(
                "[{\"operation\":\"apply\"}]")!,
            EffectResolutionReceipts = Array.Empty<JsonElement>(),
            EffectEventReports = Array.Empty<JsonElement>()
        };

        var modified = await distributor.DistributeAsync(response);

        Assert.Contains(EffectAcceptedTurnPlan.CommandPath, modified);
        using var document = JsonDocument.Parse(
            await _fs.ReadFileAsync(EffectAcceptedTurnPlan.CommandPath) ?? "{}");
        Assert.Equal(
            new[] { "effectChanges", "effectEventReports", "effectResolutionReceipts" },
            document.RootElement.EnumerateObject()
                .Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task DistributeAsync_ResourceCommandsPreserveAllThreeArraysInOneTransientRoot()
    {
        var distributor = new StateDistributor(
            _fs,
            NullLogger<StateDistributor>.Instance);
        var response = new GameResponse
        {
            ResourceDefinitionCreations = JsonSerializer.Deserialize<JsonElement[]>(
                "[{\"resourceKey\":\"focus\"}]")!,
            ResourceCapacityChanges = JsonSerializer.Deserialize<JsonElement[]>(
                "[{\"operation\":\"initialize\"}]")!,
            ResourceChanges = JsonSerializer.Deserialize<JsonElement[]>(
                "[{\"operation\":\"spend\"}]")!
        };

        var modified = await distributor.DistributeAsync(response);

        Assert.Contains(ResourceMaterializationContract.CommandPath, modified);
        using var document = JsonDocument.Parse(
            await _fs.ReadFileAsync(ResourceMaterializationContract.CommandPath) ?? "{}");
        var root = document.RootElement;
        Assert.Equal("focus", root
            .GetProperty("resourceDefinitionCreations")[0]
            .GetProperty("resourceKey")
            .GetString());
        Assert.Equal("initialize", root
            .GetProperty("resourceCapacityChanges")[0]
            .GetProperty("operation")
            .GetString());
        Assert.Equal("spend", root
            .GetProperty("resourceChanges")[0]
            .GetProperty("operation")
            .GetString());
        Assert.Equal(
            new[]
            {
                "resourceCapacityChanges",
                "resourceChanges",
                "resourceDefinitionCreations"
            },
            root.EnumerateObject()
                .Select(static property => property.Name)
                .OrderBy(static name => name, StringComparer.Ordinal));
    }

    [Fact]
    public async Task DistributeAsync_LegacyLooseWoundFieldsCannotWriteCanonicalCarriers()
    {
        var distributor = new StateDistributor(
            _fs,
            NullLogger<StateDistributor>.Instance);
        var response = JsonSerializer.Deserialize<GameResponse>(
            """
            {
              "playerWoundChanges": [{ "woundName": "legacy player injection" }],
              "NPCWoundChanges": [{ "NPCId": "npc_legacy", "wounds": [] }]
            }
            """)!;

        var modified = await distributor.DistributeAsync(response);

        Assert.DoesNotContain(
            "game_state/player/wounds.json",
            modified,
            StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "game_state/npcs/npc_effects.json",
            modified,
            StringComparer.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists("game_state/player/wounds.json"));
        Assert.False(_fs.FileExists("game_state/npcs/npc_effects.json"));
    }

    [Fact]
    public async Task DistributeAsync_UnboundWoundDecisionsFailBeforeAnyOutputWrite()
    {
        var distributor = new StateDistributor(
            _fs,
            NullLogger<StateDistributor>.Instance);
        var response = new GameResponse
        {
            Response = "Сцена не должна быть опубликована.",
            WoundDecisions = JsonSerializer.Deserialize<JsonElement[]>(
                "[{\"opportunityRef\":\"wound_opportunity_public\",\"decision\":\"none\"}]")!
        };

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => distributor.DistributeAsync(response));

        Assert.Contains(
            "wound",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.False(_fs.FileExists("output/narrative_response.json"));
    }

    private FileSystemManager CreateFileSystem(FileSystemManagerHooks? hooks = null)
    {
        var fs = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            hooks);
        fs.EnsureDirectoryStructure();
        return fs;
    }

    private static GameResponse CreateWeatherResponse() =>
        new()
        {
            WeatherChange = JsonSerializer.Deserialize<JsonElement>(
                "{\"tendency\":\"NO_CHANGE\",\"description\":\"transaction lease regression\"}")
        };

    private static string? ReadMarker(string? json)
    {
        using var document = JsonDocument.Parse(json ?? "{}");
        return document.RootElement.TryGetProperty("marker", out var marker)
            ? marker.GetString()
            : null;
    }

    private static TaskCompletionSource<bool> NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Best effort cleanup for temp test data.
        }
    }
}
