using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task WorkerAuditUnknown_OriginalEngineDispatcherDoesNotReturnFallbackOutcome()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new WorkerStorageProbe();
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, probe.Hooks);
        probe.Attach(files);
        var engine = CreateGameEngine(new LoreRealmInput([], 0), InertRealmSettings, fileSystem: files);
        string generation;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            generation = files.ReadExistingSessionGeneration(lease)!;
        Assert.False(string.IsNullOrWhiteSpace(generation));
        probe.Target = files.ResolvePath(GmWorkerAuditLog.AuditLogPath);
        probe.Cut.Armed = true;
        GmWorkerValidationRepairDispatchResult? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await
            (Task<GmWorkerValidationRepairDispatchResult>)typeof(GameEngine)
                .GetMethod("RunWorkerValidationRepairIfAvailableAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(engine, [Array.Empty<ValidationIssue>(), ("storage-session", "storage-request", 12),
                    "2026-10-09T00:00:00Z", 1, generation, null])!);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
        {
            mode = "engine_router_audit", Failure = failure?.ToString(), result, Cut = probe.Cut.Evidence()
        }));
        probe.Cut.AssertReachedAndStopped();
        Assert.Same(probe.Cut.OriginalUncertainty, failure);
        Assert.Null(result);
    }
}
