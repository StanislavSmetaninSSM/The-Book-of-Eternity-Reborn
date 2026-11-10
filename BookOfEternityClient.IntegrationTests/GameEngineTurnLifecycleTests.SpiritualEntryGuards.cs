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
    /// Rejects checkpoint admission without revoking an existing ordinary or private item owner.
    /// </summary>
    /// <param name="privateAuthority">
    /// Whether the foreign registration uses a real attempt-scoped spiritual item factory.
    /// </param>
    /// <returns>
    /// A task completing after the exact foreign owner and all physical images remain unchanged.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpiritualEntry_UpstreamCheckpointDiagnosticsPreserveForeignItemAuthority(
        bool privateAuthority)
    {
        var fixture = await CreateSpiritualEntryGuardOriginalAsync(absentConflict: false);
        await using var context = fixture.Context;
        await context.WriteExactJsonAsync(SpiritualWoundCaptureCheckpointState.StatePath, "{}");
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        MortalItemIdentityFactory factory = privateAuthority
            ? new SpiritualWoundItemIdentityFactory(journal, new MortalItemIdentityFactory())
            : new MortalItemIdentityFactory();
        var inventory = new JsonObject
        {
            ["items"] = new JsonArray(MortalItemTestFixture.CreateCanonicalRoot("itm_foreign_guard"))
        };
        var catalog = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            inventory, null, null, null, null, new Dictionary<string, JsonObject>()));
        Assert.Empty(catalog.Issues);
        var roots = new Dictionary<string, JsonNode?>
        {
            [InventoryEquipmentService.ItemsPath] = inventory
        };
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            MortalItemAcceptedTurnAuthority.RegisterValidatedItems(context.FileSystem, lease,
                "foreign_session", "foreign_snapshot", catalog, ["itm_foreign_guard"],
                MortalItemRouteAuthorityCatalog.CreateFrozen(new Dictionary<string, MortalItemRouteAuthority>()),
                null, roots, roots, factory, "foreign_request", 42);
            Assert.Equal("itm_foreign_guard", Assert.Single(MortalItemAcceptedTurnAuthority.GetValidatedOwners(
                context.FileSystem, lease, "foreign_session", "foreign_snapshot", factory)).ItemId);
            Assert.Equal(!privateAuthority, MortalItemAcceptedTurnAuthority.HasValidatedItems(context.FileSystem, lease));
        }
        var before = await ReadSpiritualEntryGuardFilesAsync(context);

        var result = await InvokePrivateTaskResultAsync(fixture.Engine,
            "ValidateAcceptedTurnUpstreamRawItemsAsync");

        Assert.True(Assert.IsType<bool>(result.GetType().GetProperty("RejectSpiritualBoundary")!.GetValue(result)));
        var issue = Assert.Single(Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(
            result.GetType().GetProperty("Issues")!.GetValue(result)));
        Assert.Equal("spiritual_original_intake_claim_conflict", issue.Code);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Equal("itm_foreign_guard", Assert.Single(MortalItemAcceptedTurnAuthority.GetValidatedOwners(
                context.FileSystem, lease, "foreign_session", "foreign_snapshot", factory)).ItemId);
            Assert.True(AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
                context.FileSystem, lease, factory, checkPlansAndItems: true));
            Assert.False(AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(
                context.FileSystem, lease, new MortalItemIdentityFactory(), checkPlansAndItems: true));
        }
        Assert.True(journal.IsHealthy);
        await AssertSpiritualEntryGuardFilesAsync(context, before);
    }

    /// <summary>
    /// Prevents public transport from replacing absent private authority while leaving ordinary malformed Ready diagnostics on their existing route.
    /// </summary>
    /// <param name="path">
    /// Existing repair request or Ready surface to seed without a C2 checkpoint.
    /// </param>
    /// <param name="json">
    /// Untrusted transport text; an actual continuation property cannot establish authority.
    /// </param>
    /// <param name="ordinary">
    /// Whether the unchanged turn must return to ordinary validation rather than reject a claimed continuation.
    /// </param>
    /// <returns>
    /// A task completing after the real entry guard preserves every physical byte.
    /// </returns>
    [Theory]
    [InlineData("game_state/control/validation_repair_ready.json", "{", true)]
    [InlineData("game_state/control/validation_repair_ready.json", "[]", true)]
    [InlineData("game_state/control/validation_repair_request.json", "{\"spiritualWoundContinuation\":null}", false)]
    [InlineData("game_state/control/validation_repair_ready.json", "{\"SpiritualWoundContinuation\":null}", false)]
    public async Task SpiritualEntry_TransportWithoutCheckpointPreservesOrdinaryDiagnostics(
        string path, string json, bool ordinary)
    {
        var fixture = await CreateSpiritualEntryGuardOriginalAsync(absentConflict: false);
        await using var context = fixture.Context;
        await context.WriteExactJsonAsync(path, json);
        var before = await ReadSpiritualEntryGuardFilesAsync(context);

        var result = await InvokePrivateTaskResultAsync(fixture.Engine, "ContinueAcceptedSpiritualTurnAsync",
            "entry-guard", 42, fixture.Snapshot);

        Assert.Equal(ordinary ? "Admitted" : "Rejected", result.ToString());
        await AssertSpiritualEntryGuardFilesAsync(context, before);
    }

    /// <summary>
    /// Routes a start carrying exchanges to real source admission even when the signed original conflict was absent.
    /// </summary>
    /// <param name="hasExchange">
    /// Whether the authored start contains an exchange; a start without exchanges remains ordinary.
    /// </param>
    /// <returns>
    /// A task completing after read-only intent classification preserves all signed and current bytes.
    /// </returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpiritualEntry_SignedAbsentConflictDoesNotHideNewExchangeIntent(bool hasExchange)
    {
        var fixture = await CreateSpiritualEntryGuardOriginalAsync(absentConflict: true);
        await using var context = fixture.Context;
        var manifest = JsonNode.Parse((await context.FileSystem.ReadFileAsync(
            "game_state/control/pending_turn_snapshot.json"))!)!;
        Assert.Null(manifest["files"]?[AfterlifeSpiritualConflictState.StatePath]);
        var seed = new JsonObject
        {
            ["exchangeLog"] = hasExchange
                ? new JsonArray(new JsonObject { ["exchangeId"] = "new_exchange" })
                : new JsonArray()
        };
        await context.WriteExactJsonAsync(AfterlifeSpiritualConflictState.StatePath, new JsonObject
        {
            [AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
            {
                ["mode"] = AfterlifeSpiritualConflictState.ModeStart,
                ["conflictSeed"] = seed
            }
        }.ToJsonString());
        var before = await ReadSpiritualEntryGuardFilesAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var result = await InvokePrivateTaskResultAsync(fixture.Engine, "HasSignedSpiritualExchangeIntentAsync",
            lease, fixture.Snapshot);

        Assert.Equal(hasExchange, Assert.IsType<bool>(result));
        await AssertSpiritualEntryGuardFilesAsync(context, before);
    }

    /// <summary>
    /// Creates an authenticated original for entry classification without preparing or consuming any exchange.
    /// </summary>
    /// <param name="absentConflict">
    /// Whether the original snapshot must authenticate conflict-file absence.
    /// </param>
    /// <returns>
    /// The disposable filesystem context, actual engine and authenticated private snapshot context.
    /// </returns>
    private async Task<(ResourceMaterializationTestContext Context, GameEngine Engine, object Snapshot)>
        CreateSpiritualEntryGuardOriginalAsync(bool absentConflict, FileSystemManagerHooks? hooks = null)
    {
        GameEngine? engine = null;
        var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
        {
            engine = CreateGameEngine(new QueuedConsoleInputSource([]), hooks is null ? null : settings => { settings.MusicEnabled = false; settings.SoundEnabled = false; settings.GmBridgeAutoStart = false; settings.ImageProvider = "off"; }, fileSystem: original.FileSystem);
            await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
            if (absentConflict)
            {
                original.FileSystem.DeleteFile(AfterlifeSpiritualConflictState.StatePath);
                // The modern GameEngine initializes this file before signing. Use the real
                // authority signer fixture to cover an authenticated older absent baseline.
                await original.CaptureValidatedPendingSnapshotAsync(
                    turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8],
                    sessionId: "session_entry_guard", requestId: "request_entry_guard_42");
                return;
            }
            var request = new TurnRequest
            {
                SessionId = "session_entry_guard", RequestId = "request_entry_guard_42", TurnNumber = 42,
                PlayerAction = "Проверить духовное состояние.", Timestamp = DateTime.UtcNow.ToString("O"),
                PreGeneratedDices1d20 = [15, 5, 12, 8],
                ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                    NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea")
            };
            var rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "entry-guard");
            await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
            await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync", request, rollback, "entry-guard");
        }, hooks: hooks);
        var resolution = await InvokePrivateTaskResultAsync(engine!, "ResolveActivePendingTurnSnapshotContextAsync");
        Assert.Equal("Usable", resolution.GetType().GetProperty("Status")!.GetValue(resolution)!.ToString());
        return (context, engine!, resolution.GetType().GetProperty("Context")!.GetValue(resolution)!);
    }

    /// <summary>
    /// Reads every physical file for an exact preservation comparison, including the signed original snapshots.
    /// </summary>
    /// <param name="context">
    /// Isolated session being inspected.
    /// </param>
    /// <returns>
    /// Exact bytes keyed by physical session-relative path.
    /// </returns>
    private static async Task<Dictionary<string, byte[]>> ReadSpiritualEntryGuardFilesAsync(ResourceMaterializationTestContext context)
    {
        var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(context.FileSystem.GameSessionPath, "*", SearchOption.AllDirectories))
            result.Add(Path.GetRelativePath(context.FileSystem.GameSessionPath, path), await File.ReadAllBytesAsync(path));
        return result;
    }

    /// <summary>
    /// Requires unchanged file inventory and exact bytes after read-only entry classification or rejection.
    /// </summary>
    /// <param name="context">
    /// Isolated session after the operation.
    /// </param>
    /// <param name="before">
    /// Complete physical images captured before entry classification.
    /// </param>
    /// <returns>
    /// A task completing after all file identities and bytes match.
    /// </returns>
    private static async Task AssertSpiritualEntryGuardFilesAsync(ResourceMaterializationTestContext context,
        Dictionary<string, byte[]> before)
    {
        var after = await ReadSpiritualEntryGuardFilesAsync(context);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        foreach (var pair in before)
            Assert.Equal(pair.Value, after[pair.Key]);
    }
}
