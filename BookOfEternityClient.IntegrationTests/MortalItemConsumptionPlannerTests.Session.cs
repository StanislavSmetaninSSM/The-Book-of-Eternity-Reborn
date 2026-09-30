using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies signed snapshot and filesystem authority for Mortal item planning.
/// </summary>
[Trait("Category", "RegressionIntegration")]
public sealed class MortalItemConsumptionPlannerTests : MortalItemConsumptionPlannerTestFixture
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Project_SameTurnCreateAndTransferUseSnapshotDeterministicReceiptAndTransitionIds(
        bool vehiclesUseLegacyArrayRoot)
    {
        var scenario = await ProductionProjectionScenarioAsync(vehiclesUseLegacyArrayRoot);
        var owner = ExactType("BookOfEternityClient.Services.MortalItemCanonicalProjectionPlanner");
        var inputType = ExactType("BookOfEternityClient.Services.MortalItemCanonicalProjectionInput");
        var resultType = ExactType("BookOfEternityClient.Services.MortalItemCanonicalProjectionResult");
        ProjectionInputShape(inputType);
        ProjectionResultShape(resultType);
        AssertProjectionRootRegistry(owner);
        AssertSnapshotProjectionRoots(scenario, vehiclesUseLegacyArrayRoot);
        var method = ExactMethod(owner, "Project", inputType, resultType);
        var first = ProjectionInput(inputType, scenario);
        var second = ProjectionInput(inputType, scenario);
        var receiptId = SnapshotOwnedMapValue(
            scenario.Snapshot, scenario.CreationRef, "mirec_");
        var createTransitionId = SnapshotOwnedMapValue(
            scenario.Snapshot, scenario.CreationRef, "mitrn_");
        var transferTransitionId = SnapshotOwnedMapValue(
            scenario.Snapshot, scenario.TransferredItemId, "mitrn_");

        var firstResult = ProjectionResult(method.Invoke(null, new[] { first })!);
        var secondResult = ProjectionResult(method.Invoke(null, new[] { second })!);

        Assert.True(firstResult.IsValid);
        Assert.Empty(firstResult.Issues);
        AssertProjectionEqual(firstResult, secondResult);
        var created = FindItem(firstResult.Roots, scenario.CreatedItemId);
        Assert.Equal(receiptId,
            created["materializationReceipt"]!["receiptId"]!.GetValue<string>());
        var createdEntry = Entry(firstResult.Index, scenario.CreatedItemId);
        Assert.Equal(createTransitionId,
            Assert.Single(createdEntry["transitions"]!.AsArray().OfType<JsonObject>())
                ["transitionId"]!.GetValue<string>());
        var transferredEntry = Entry(firstResult.Index, scenario.TransferredItemId);
        Assert.Equal(transferTransitionId,
            LastTransition(transferredEntry)["transitionId"]!.GetValue<string>());
        Assert.Equal("player_inventory",
            transferredEntry["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.NotNull(FindItem(firstResult.Roots, scenario.TransferredItemId));
        Assert.Equal(
            ProjectionRootPaths.OrderBy(static path => path, StringComparer.Ordinal),
            firstResult.Roots.Keys.OrderBy(static path => path, StringComparer.Ordinal));
        Assert.Equal(
            vehiclesUseLegacyArrayRoot ? typeof(JsonArray) : typeof(JsonObject),
            firstResult.Roots[StorageTransportMoveService.VehiclesPath]!.GetType());
        Assert.Null(firstResult.Roots["game_state/inventory/recipes.json"]);
        Assert.True(JsonNode.DeepEquals(
            firstResult.Roots[MortalItemIdentityState.StatePath],
            firstResult.Index));
        AssertFingerprint(firstResult.Fingerprint);

        var missingRoots = scenario.CurrentRoots
            .Where(pair => !string.Equals(
                pair.Key,
                "game_state/inventory/recipes.json",
                StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(),
                StringComparer.Ordinal);
        AssertProjectionInvalidEmpty(ProjectionResult(method.Invoke(
            null,
            new[] { ProjectionInput(inputType, scenario, currentRoots: missingRoots) })!));

        var extraRoots = scenario.CurrentRoots.ToDictionary(
            pair => pair.Key,
            pair => pair.Value?.DeepClone(),
            StringComparer.Ordinal);
        extraRoots.Add("game_state/inventory/unregistered_projection_root.json",
            new JsonObject());
        AssertProjectionInvalidEmpty(ProjectionResult(method.Invoke(
            null,
            new[] { ProjectionInput(inputType, scenario, currentRoots: extraRoots) })!));

        var missingBackupRoots = scenario.BackupRoots
            .Where(pair => !string.Equals(
                pair.Key,
                "game_state/inventory/recipes.json",
                StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value?.DeepClone(),
                StringComparer.Ordinal);
        AssertProjectionInvalidEmpty(ProjectionResult(method.Invoke(
            null,
            new[]
            {
                ProjectionInput(
                    inputType,
                    scenario,
                    backupRoots: missingBackupRoots)
            })!));

        var extraBackupRoots = scenario.BackupRoots.ToDictionary(
            pair => pair.Key,
            pair => pair.Value?.DeepClone(),
            StringComparer.Ordinal);
        extraBackupRoots.Add("game_state/inventory/unregistered_backup_root.json",
            new JsonObject());
        AssertProjectionInvalidEmpty(ProjectionResult(method.Invoke(
            null,
            new[]
            {
                ProjectionInput(
                    inputType,
                    scenario,
                    backupRoots: extraBackupRoots)
            })!));

        AssertProjectionInvalidEmpty(ProjectionResult(method.Invoke(
            null,
            new[]
            {
                ProjectionInput(
                    inputType,
                    scenario,
                    identityRoot: MortalItemIdentityState.CreateEmptyRoot())
            })!));
    }

    [Fact]
    public async Task AcceptedCreationIdentityIds_UseFiveCollectorProductionOrderInsteadOfCreationRefOrder()
    {
        const int turn = 44;
        const string sessionId = "session_t070b4_five_collectors";
        var snapshotToken = Hex("snapshot_t070b4_five_collectors");
        var expectedCreationRefs = new[]
        {
            "new_item_z_player",
            "new_item_y_npc_core",
            "new_item_x_npc_command",
            "new_item_w_current_location",
            "new_item_v_offscreen_storage"
        };
        Assert.False(expectedCreationRefs.SequenceEqual(
            expectedCreationRefs.OrderBy(static value => value, StringComparer.Ordinal)));

        var input = FiveCollectorInput(turn, expectedCreationRefs);
        var catalog = MortalItemCarrierCatalog.Build(input);
        Assert.Empty(catalog.Issues);
        var occurrences = catalog.Occurrences
            .Where(static value => value.ItemId is null && value.CreationRef is not null)
            .ToArray();
        Assert.Equal(expectedCreationRefs, occurrences.Select(static value => value.CreationRef));

        var currentRoots = FiveCollectorProjectionRoots(input);
        var backupRoots = ProjectionRootPaths.ToDictionary(
            static path => path,
            static _ => (JsonNode?)null,
            StringComparer.Ordinal);
        var root = Path.Combine(
            Path.GetTempPath(),
            "boe-t070b4-five-collectors-" + Guid.NewGuid().ToString("N"));
        var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        try
        {
            var fileSystem = new FileSystemManager(
                root,
                NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            await SeedFiveCollectorRouteAuthorityAsync(fileSystem, input, turn);
            var routeCatalog = await MortalItemRouteAuthorityCatalog.BuildAsync(fileSystem);
            Assert.Empty(routeCatalog.Issues);
            Assert.Equal(
                expectedCreationRefs.OrderBy(static value => value, StringComparer.Ordinal),
                routeCatalog.ByCreationRef.Keys.OrderBy(
                    static value => value,
                    StringComparer.Ordinal));
            MortalItemAcceptedTurnNormalizationSnapshot snapshot;
            await using (var lease = await fileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                RegisterFiveCollectorItems(
                    fileSystem,
                    lease,
                    sessionId,
                    snapshotToken,
                    catalog,
                    routeCatalog,
                    currentRoots,
                    backupRoots);
                Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
                    fileSystem,
                    lease,
                    sessionId,
                    snapshotToken,
                    turn,
                    out snapshot));
            }

            var receiptIds = new HashSet<string>(StringComparer.Ordinal);
            var transitionIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < expectedCreationRefs.Length; index++)
            {
                var creationRef = expectedCreationRefs[index];
                var ordinal = index + 1;
                var route = routeCatalog.ByCreationRef[creationRef];
                var receiptId = SnapshotOwnedMapValue(snapshot, creationRef, "mirec_");
                var transitionId = SnapshotOwnedMapValue(snapshot, creationRef, "mitrn_");
                Assert.Equal(
                    ExpectedAcceptedCreationIdentityId(
                        "mirec_",
                        "accepted_root_receipt",
                        sessionId,
                        snapshotToken,
                        turn,
                        creationRef,
                        route,
                        ordinal),
                    receiptId);
                Assert.Equal(
                    ExpectedAcceptedCreationIdentityId(
                        "mitrn_",
                        "accepted_create_transition",
                        sessionId,
                        snapshotToken,
                        turn,
                        creationRef,
                        route,
                        ordinal),
                    transitionId);
                Assert.True(receiptIds.Add(receiptId));
                Assert.True(transitionIds.Add(transitionId));
            }

            var projected = MortalItemCanonicalProjectionPlanner.Project(
                new MortalItemCanonicalProjectionInput(
                    turn,
                    snapshot,
                    routeCatalog,
                    currentRoots,
                    backupRoots,
                    MortalItemIdentityState.Parse(
                        MortalItemIdentityState.CreateEmptyRoot())));

            Assert.True(projected.IsValid, string.Join(
                Environment.NewLine,
                projected.Issues.Select(issue => $"{issue.Code}: {issue.Actual}")));
            Assert.Empty(projected.Issues);
            var npcAfter = Assert.IsType<JsonObject>(
                projected.ItemPhaseAfterImages[NpcPath]);
            var commandOwnerCopies = Npcs(npcAfter, "npc_five_command");
            Assert.Equal(2, commandOwnerCopies.Count);
            Assert.True(JsonNode.DeepEquals(
                commandOwnerCopies[0],
                commandOwnerCopies[1]));
            var commandItemId = SnapshotOwnedMapValue(
                snapshot,
                expectedCreationRefs[2],
                "itm_");
            Assert.All(commandOwnerCopies, owner =>
            {
                var created = Assert.Single(owner["inventory"]!.AsArray());
                Assert.Equal(commandItemId, created!["itemId"]!.GetValue<string>());
                Assert.Equal(commandItemId,
                    owner["equippedItems"]!["mainHand"]!.GetValue<string>());
                Assert.Empty(owner["equipment"]!.AsObject());
            });
            var commandsAfter = Assert.IsType<JsonObject>(
                projected.ItemPhaseAfterImages[NpcCommandsPath]);
            Assert.False(commandsAfter.ContainsKey("NPCInventoryAdds"));
            Assert.False(commandsAfter.ContainsKey("NPCEquipmentChanges"));
            Assert.Single(projected.IdentityIndexAfterImage["entries"]!.AsArray()
                .OfType<JsonObject>(), entry => string.Equals(
                    entry["itemId"]?.GetValue<string>(),
                    commandItemId,
                    StringComparison.Ordinal));
        }
        finally
        {
            var fullRoot = Path.GetFullPath(root);
            if (!fullRoot.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullRoot).StartsWith(
                    "boe-t070b4-five-collectors-",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unsafe five-collector test root '{fullRoot}'.");
            }
            if (Directory.Exists(fullRoot))
                Directory.Delete(fullRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RouteCatalog_RejectsDirectRawCreationInPermanentNpcMirrorAfterCatalogCoalescing()
    {
        const int turn = 45;
        const string npcId = "npc_mirrored_raw_creation";
        const string creationRef = "new_item_mirrored_raw_creation";
        var rawItem = MortalItemTestFixture.CreateRawRoot(
            "new_npc_inventory",
            "new_npc",
            npcId,
            turn,
            creationRef,
            "mat_item_mirrored_raw_creation");
        var owner = new JsonObject
        {
            ["NPCId"] = npcId,
            ["name"] = "Permanent mirrored raw owner",
            ["inventory"] = new JsonArray(rawItem.DeepClone()),
            ["equippedItems"] = new JsonObject()
        };
        var npcRoot = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(owner.DeepClone()),
            ["NPCsInScene"] = new JsonArray(owner.DeepClone())
        };
        var physicalCatalog = MortalItemCarrierCatalog.Build(
            new MortalItemCarrierCatalogInput(
                null,
                npcRoot,
                null,
                null,
                null,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)));
        Assert.Empty(physicalCatalog.Issues);
        Assert.Single(physicalCatalog.ByCreationRef[creationRef]);

        var root = Path.Combine(
            Path.GetTempPath(),
            "boe-t070b4-mirrored-raw-route-" + Guid.NewGuid().ToString("N"));
        var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        try
        {
            var fileSystem = new FileSystemManager(
                root,
                NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            await SeedProjectionBootstrapAsync(fileSystem);
            await WriteJsonAsync(fileSystem, NpcPath, npcRoot);
            await WriteJsonAsync(fileSystem, NpcCommandsPath, new JsonObject());
            await WriteJsonAsync(fileSystem, "input/turn_request.json", new JsonObject
            {
                ["sessionId"] = "session_t070b4_mirrored_raw_route",
                ["requestId"] = "request_t070b4_mirrored_raw_route",
                ["turnNumber"] = turn,
                ["playerAction"] = "Prove direct raw permanent-NPC creation is not routable."
            });
            const string snapshotPath =
                "game_state/control/mirrored_raw_route/npc_core.json";
            var backupOwner = owner.DeepClone().AsObject();
            backupOwner["inventory"] = new JsonArray();
            await WriteJsonAsync(fileSystem, snapshotPath, new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(backupOwner.DeepClone()),
                ["NPCsInScene"] = new JsonArray(backupOwner.DeepClone())
            });
            await WriteJsonAsync(
                fileSystem,
                "game_state/control/pending_turn_snapshot.json",
                new JsonObject
                {
                    ["files"] = new JsonObject
                    {
                        [NpcPath] = snapshotPath
                    }
                });

            var routeCatalog = await MortalItemRouteAuthorityCatalog.BuildAsync(fileSystem);

            Assert.False(routeCatalog.ByCreationRef.ContainsKey(creationRef));
            var issue = Assert.Single(routeCatalog.Issues);
            Assert.Equal("mortal_item_materialization_route_authority_missing", issue.Code);
            Assert.Equal(creationRef, issue.CreationRef);
        }
        finally
        {
            var fullRoot = Path.GetFullPath(root);
            if (!fullRoot.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullRoot).StartsWith(
                    "boe-t070b4-mirrored-raw-route-",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unsafe mirrored raw-route test root '{fullRoot}'.");
            }
            if (Directory.Exists(fullRoot))
                Directory.Delete(fullRoot, recursive: true);
        }
    }

}
