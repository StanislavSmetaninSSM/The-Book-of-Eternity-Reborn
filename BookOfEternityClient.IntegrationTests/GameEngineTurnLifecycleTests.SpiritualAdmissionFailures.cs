using System.Collections.Concurrent;
using System.Text;
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
    /// Rejects private spiritual authority failures before ordinary resource admission or a GM repair request.
    /// </summary>
    /// <param name="authorityKind">
    /// A live foreign ordinary item owner, live foreign private item owner, or retained C4 owner with changed input bytes.
    /// </param>
    /// <returns>
    /// A task completing after terminal rejection preserves the exact owner registration and every session file.
    /// </returns>
    [Theory]
    [InlineData("ordinary_item")]
    [InlineData("private_item")]
    [InlineData("retained_c4")]
    public async Task AcceptedTurnSpiritualAdmissionFailure_IsTerminalWithoutRepair(string authorityKind)
    {
        var mutations = new ConcurrentQueue<string>();
        var observeMutations = false;
        var hooks = new FileSystemManagerHooks
        {
            BeforeCanonicalMutationAsync = path =>
            {
                if (observeMutations)
                    mutations.Enqueue(path);
                return Task.CompletedTask;
            }
        };
        // A regression that dispatches repair must finish rather than wait for an external GM.
        var input = new QueuedConsoleInputSource([Key(ConsoleKey.Escape)]);
        var logger = new SpiritualLifecycleTestLogger();
        GameEngine? engine = null;
        object? rollback = null;
        var request = new TurnRequest
        {
            SessionId = "session_engine_spiritual", RequestId = "request_engine_spiritual_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [15, 5, 12, 8]
        };
        await using var context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(
            async original =>
            {
                engine = CreateGameEngine(input, fileSystem: original.FileSystem, logger: logger);
                await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
                await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
                request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                    NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
                rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "spiritual-admission-original");
                await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
                await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync",
                    request, rollback, "spiritual-admission-original");
            }, hooks: hooks);
        await AfterlifeResourceCutoverTests.WriteSpiritualGameEngineExchangeAsync(context);
        await WriteSpiritualLifecycleOutputsAsync(context, request);
        var resolution = await InvokePrivateTaskResultAsync(engine!, "ResolveActivePendingTurnSnapshotContextAsync");
        Assert.Equal("Usable", resolution.GetType().GetProperty("Status")!.GetValue(resolution)!.ToString());
        var snapshot = resolution.GetType().GetProperty("Context")!.GetValue(resolution)!;
        var journal = SpiritualWoundReplayJournal.CreateAppend("[]");
        MortalItemIdentityFactory? foreignFactory = null;
        ValidationService.SpiritualOriginalTurnCapture.SpiritualC4PublicationAuthority? publication = null;
        await InvokePrivateTaskResultAsync(engine!, "CaptureCurrentSessionGenerationAsync");
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.True(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(engine!,
                "BeginAcceptedSpiritualCaptureAsync", lease)), logger.Describe());
            Assert.True(context.FileSystem.FileExists(lease, SpiritualWoundCaptureCheckpointState.StatePath));
            if (authorityKind == "retained_c4")
            {
                var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
                Assert.Empty(opened.Issues);
                using var owner = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
                var completed = await owner.SubmitDecisionAsync(lease, JsonSerializer.SerializeToElement(new
                {
                    opportunityRef = owner.Offer!.OpportunityRef, decision = "none"
                }), null);
                Assert.Equal("completed_unpublished", completed.Disposition);
                Assert.Empty(completed.Issues);
                completed.Session?.Dispose();
                owner.Dispose();
                var prepared = await context.Validator.TryPrepareSpiritualC4PublicationAsync(lease);
                Assert.NotNull(prepared);
                Assert.Empty(prepared);
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(
                    context.FileSystem, lease, out publication));
                var command = await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
                Assert.NotNull(command);
                await context.FileSystem.WriteFileAtomicBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath,
                    command.Concat(Encoding.UTF8.GetBytes(" ")).ToArray());
                Assert.Contains(await publication.ValidateCurrentInputsAsync(context.FileSystem, lease),
                    issue => issue.Code == "spiritual_c4_publication_input_changed");
            }
            else
            {
                foreignFactory = authorityKind == "private_item"
                    ? new SpiritualWoundItemIdentityFactory(journal, new MortalItemIdentityFactory())
                    : new MortalItemIdentityFactory();
                var inventory = new JsonObject
                {
                    ["items"] = new JsonArray(MortalItemTestFixture.CreateCanonicalRoot("itm_foreign_admission"))
                };
                var catalog = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
                    inventory, null, null, null, null, new Dictionary<string, JsonObject>()));
                Assert.Empty(catalog.Issues);
                var roots = new Dictionary<string, JsonNode?> { [InventoryEquipmentService.ItemsPath] = inventory };
                MortalItemAcceptedTurnAuthority.RegisterValidatedItems(context.FileSystem, lease,
                    "foreign_session", "foreign_snapshot", catalog, ["itm_foreign_admission"],
                    MortalItemRouteAuthorityCatalog.CreateFrozen(new Dictionary<string, MortalItemRouteAuthority>()),
                    null, roots, roots, foreignFactory, "foreign_request", 42);
                Assert.Single(MortalItemAcceptedTurnAuthority.GetValidatedOwners(context.FileSystem, lease,
                    "foreign_session", "foreign_snapshot", foreignFactory));
            }
        }
        var before = await ReadSpiritualEntryGuardFilesAsync(context);
        observeMutations = true;
        var validation = InvokePrivateTaskResultAsync(engine!, "ValidateAcceptedTurnOutcomeWithRepairLoopAsync",
            "late response GM", snapshot, rollback, 42, request.ProgressionControl);
        object? disposition = null;
        Exception? validationFailure = null;
        Exception? cleanupFailure = null;
        try
        {
            disposition = await validation.WaitAsync(TimeSpan.FromSeconds(150));
        }
        catch (Exception error)
        {
            validationFailure = error;
        }
        finally
        {
            if (!validation.IsCompleted)
            {
                input.Enqueue(Key(ConsoleKey.Escape));
                try { await validation.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception error) { cleanupFailure = error; }
            }
        }
        Assert.True(validationFailure is null && cleanupFailure is null,
            $"Admission failure: {validationFailure}; cleanup failure: {cleanupFailure}. {logger.Describe()}");
        Assert.Equal(AcceptedTurnValidationDisposition.TerminalRejected,
            Assert.IsType<AcceptedTurnValidationDisposition>(disposition));
        Assert.DoesNotContain("game_state/control/validation_repair_request.json", mutations);
        Assert.DoesNotContain("game_state/control/validation_repair_ready.json", mutations);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            if (publication is not null)
            {
                Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(
                    context.FileSystem, lease, out var retained), logger.Describe());
                Assert.Same(publication, retained);
            }
            else
            {
                Assert.Equal("itm_foreign_admission", Assert.Single(MortalItemAcceptedTurnAuthority.GetValidatedOwners(
                    context.FileSystem, lease, "foreign_session", "foreign_snapshot", foreignFactory)).ItemId);
                Assert.True(foreignFactory!.IsHealthy);
            }
        }
        await AssertSpiritualEntryGuardFilesAsync(context, before);
    }
}
