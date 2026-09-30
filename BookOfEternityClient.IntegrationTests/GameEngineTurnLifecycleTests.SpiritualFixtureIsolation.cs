using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    /// <summary>
    /// Materializes independent spiritual roots from one unsigned preparation while retaining fresh hooks and real engine snapshots.
    /// </summary>
    /// <returns>
    /// A task completing after mutation, root deletion, training isolation and independently captured originals are verified.
    /// </returns>
    [Fact]
    public async Task SpiritualPreparedFixture_MaterializesIndependentRootsAndCapturesFreshOriginals()
    {
        const string soulPath = "game_state/meta/soul_state.json";
        const string requestPath = "input/turn_request.json";
        var repositoryBefore = ReadSpiritualFixtureRepositoryFingerprints();
        var captures = new List<string>();
        var firstRequestWrites = 0;
        var secondRequestWrites = 0;
        var firstHooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (path == requestPath) Interlocked.Increment(ref firstRequestWrites);
                return Task.CompletedTask;
            }
        };
        var secondHooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (path == requestPath) Interlocked.Increment(ref secondRequestWrites);
                return Task.CompletedTask;
            }
        };
        await using var first = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(
            context => CaptureSpiritualFixtureIsolationOriginalAsync(context, "fixture_first", captures),
            hooks: firstHooks);
        await using var second = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(
            context => CaptureSpiritualFixtureIsolationOriginalAsync(context, "fixture_second", captures),
            training: true, hooks: secondHooks);

        Assert.Equal(1, AfterlifeResourceCutoverTests.SpiritualGameEnginePreparationCount);
        Assert.Equal(new[] { first.RootPath, second.RootPath }, captures);
        Assert.NotEqual(first.RootPath, second.RootPath);
        Assert.NotSame(first.FileSystem, second.FileSystem);
        Assert.NotSame(first.Validator, second.Validator);
        Assert.NotSame(first.Normalizer, second.Normalizer);
        Assert.Equal(1, firstRequestWrites);
        Assert.Equal(1, secondRequestWrites);
        Assert.Equal("fixture_first", (await first.ReadJsonAsync(requestPath))!["requestId"]!.GetValue<string>());
        Assert.Equal("fixture_second", (await second.ReadJsonAsync(requestPath))!["requestId"]!.GetValue<string>());
        Assert.Equal("hostile", (await first.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!["dangerMode"]!.GetValue<string>());
        Assert.Equal("training", (await second.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!["dangerMode"]!.GetValue<string>());

        var secondSoul = await second.FileSystem.ReadFileBytesAsync(soulPath);
        var secondDefinitions = await second.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.DefinitionsPath);
        await first.WriteExactJsonAsync(soulPath, "{\"soulName\":\"first-only\"}");
        File.Delete(first.FileSystem.ResolvePath(ResourceMaterializationContract.DefinitionsPath));
        Assert.Equal(secondSoul, await second.FileSystem.ReadFileBytesAsync(soulPath));
        Assert.Equal(secondDefinitions, await second.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.DefinitionsPath));
        await first.DisposeAsync();
        Assert.False(Directory.Exists(first.RootPath));

        await using var third = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(
            context => CaptureSpiritualFixtureIsolationOriginalAsync(context, "fixture_third", captures));
        Assert.Equal(1, AfterlifeResourceCutoverTests.SpiritualGameEnginePreparationCount);
        Assert.Equal(new[] { first.RootPath, second.RootPath, third.RootPath }, captures);
        Assert.Equal(secondSoul, await third.FileSystem.ReadFileBytesAsync(soulPath));
        Assert.Equal(secondDefinitions, await third.FileSystem.ReadFileBytesAsync(ResourceMaterializationContract.DefinitionsPath));
        Assert.Equal("hostile", (await third.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!["activeConflict"]!["dangerMode"]!.GetValue<string>());
        Assert.Equal("fixture_third", (await third.ReadJsonAsync(requestPath))!["requestId"]!.GetValue<string>());
        Assert.Equal(secondSoul, await second.FileSystem.ReadFileBytesAsync(soulPath));
        Assert.Equal(repositoryBefore.OrderBy(pair => pair.Key), ReadSpiritualFixtureRepositoryFingerprints().OrderBy(pair => pair.Key));
    }

    /// <summary>
    /// Captures a new original through the actual engine after confirming the prepared root has no previous request or private transport.
    /// </summary>
    /// <param name="context">
    /// Fresh mutable fixture receiving its own engine and snapshot capture.
    /// </param>
    /// <param name="requestId">
    /// Distinct request identity recorded in this root's original input and signed manifest.
    /// </param>
    /// <param name="captures">
    /// Invocation log receiving this callback's root exactly once.
    /// </param>
    /// <returns>
    /// A task completing after the newly captured snapshot resolves as usable with the supplied request identity.
    /// </returns>
    private async Task CaptureSpiritualFixtureIsolationOriginalAsync(
        ResourceMaterializationTestContext context, string requestId, ICollection<string> captures)
    {
        foreach (var path in new[]
        {
            "input/turn_request.json", "game_state/control/pending_turn_snapshot.json",
            PendingTurnSnapshotAuthority.AuthorityPath, SpiritualWoundCaptureCheckpointState.StatePath,
            SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath
        })
            Assert.False(context.FileSystem.FileExists(path), path);
        captures.Add(context.RootPath);
        var engine = CreateGameEngine(new QueuedConsoleInputSource([]), fileSystem: context.FileSystem);
        await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
        await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
        var request = new TurnRequest
        {
            SessionId = "session_" + requestId, RequestId = requestId, TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = "2026-08-15T00:00:00Z",
            PreGeneratedDices1d20 = [15, 5, 12, 8],
            ProgressionControl = await new ProgressionScheduleService(context.FileSystem,
                NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea")
        };
        var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", requestId);
        await context.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
        await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollback, requestId);
        var resolution = await InvokePrivateTaskResultAsync(engine, "ResolveActivePendingTurnSnapshotContextAsync");
        Assert.Equal("Usable", resolution.GetType().GetProperty("Status")!.GetValue(resolution)!.ToString());
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync("game_state/control/pending_turn_snapshot.json"));
        Assert.Equal(request.SessionId, manifest["sessionId"]!.GetValue<string>());
        Assert.Equal(requestId, manifest["requestId"]!.GetValue<string>());
        Assert.NotNull(await context.FileSystem.ReadFileBytesAsync(PendingTurnSnapshotAuthority.AuthorityPath));
    }

    /// <summary>
    /// Reads every repository baseline file fingerprint without modifying the shared example session.
    /// </summary>
    /// <returns>
    /// Relative paths and exact byte fingerprints used to detect fixture writes or deletions in the repository baseline.
    /// </returns>
    private static IReadOnlyDictionary<string, string> ReadSpiritualFixtureRepositoryFingerprints() =>
        Directory.EnumerateFiles(TestRepoPaths.BaseSessionRoot, "*", SearchOption.AllDirectories)
            .ToDictionary(path => Path.GetRelativePath(TestRepoPaths.BaseSessionRoot, path),
                path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), StringComparer.Ordinal);
}
