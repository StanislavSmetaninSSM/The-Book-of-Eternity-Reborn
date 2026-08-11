using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task WriteValidationRepairRequestAsync_MortalItemErrors_UsesExactBoundedPacket()
    {
        const string coordinate = "mortal_item:new:craft_result_42";
        var context = new MortalItemRepairContext(
            coordinate,
            "create",
            "craft_output",
            SourceCarrier: null,
            DestinationCarrier: new MortalItemCarrierCoordinate(
                "player_inventory",
                "player",
                null,
                Array.Empty<string>()),
            ExpectedAuthority: "craft_request:req_craft_42",
            ActualEvidence: "sourceAuthority.authorityId=missing",
            RequiredCompanionTargets: new[]
            {
                "game_state/inventory/item_resources.json"
            });
        var materializationIssue = new ValidationIssue(
            "game_state/inventory/items.json.UpdateInventory[0].materialization.sections.mechanics",
            IssueSeverity.Error,
            "Mortal item mechanics section is missing.",
            code: "mortal_item_materialization_section_missing",
            actor: coordinate,
            section: "MortalItemMaterialization",
            expected: "populated or empty_by_design",
            actual: "missing",
            repairHint: "Complete only the exact mechanics section.",
            repairTargetFiles: new[]
            {
                "game_state/inventory/items.json",
                "game_state/inventory/item_resources.json"
            });
        materializationIssue.MortalItemRepairContext = context;
        var protectedIssue = new ValidationIssue(
            MortalItemIdentityState.StatePath,
            IssueSeverity.Error,
            "The GM changed client-owned item identity authority.",
            code: "mortal_item_materialization_gm_authored_client_field",
            actor: coordinate,
            section: "MortalItemMaterialization",
            expected: "validated pre-turn index",
            actual: "forged entry",
            repairHint: "Restore the protected before-image.",
            repairTargetFiles: new[] { MortalItemIdentityState.StatePath });
        protectedIssue.MortalItemRepairContext = context;

        var engine = CreateGameEngine();
        var method = typeof(GameEngine).GetMethod(
            "WriteValidationRepairRequestAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
            engine,
            new object[]
            {
                "повторной проверки repair",
                new List<ValidationIssue> { materializationIssue, protectedIssue },
                1
            })!);

        await task;

        var requestJson = await _fs.ReadFileAsync(
            "game_state/control/validation_repair_request.json");
        Assert.False(string.IsNullOrWhiteSpace(requestJson));
        using var document = JsonDocument.Parse(requestJson!);
        var packet = Assert.Single(
            document.RootElement.GetProperty("harnessRepairPackets").EnumerateArray());
        Assert.Equal(
            "mortal_item_materialization_repair",
            packet.GetProperty("kind").GetString());
        Assert.Equal("create", packet.GetProperty("transitionClass").GetString());
        Assert.Equal("craft_output", packet.GetProperty("route").GetString());
        Assert.Equal(JsonValueKind.Null, packet.GetProperty("sourceCarrier").ValueKind);
        Assert.Equal(
            "player_inventory",
            packet.GetProperty("destinationCarrier").GetProperty("kind").GetString());
        Assert.Equal(
            new[] { coordinate },
            packet.GetProperty("canonicalActorNames")
                .EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Equal(
            new[]
            {
                "game_state/inventory/item_resources.json",
                "game_state/inventory/items.json"
            },
            packet.GetProperty("targetFiles")
                .EnumerateArray()
                .Select(value => value.GetString()));
        Assert.DoesNotContain(
            MortalItemIdentityState.StatePath,
            packet.GetProperty("targetFiles")
                .EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Equal(
            new[] { "game_state/inventory/item_resources.json" },
            packet.GetProperty("requiredCompanionTargets")
                .EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Contains(
            "craft_request:req_craft_42",
            packet.GetProperty("expectedAuthority")
                .EnumerateArray()
                .Select(value => value.GetString()));
        Assert.Contains(
            packet.GetProperty("exactFieldCorrections").EnumerateArray(),
            correction =>
                correction.GetProperty("code").GetString() ==
                    "mortal_item_materialization_section_missing");
        Assert.DoesNotContain(
            packet.GetProperty("exactFieldCorrections").EnumerateArray(),
            correction => correction.GetProperty("code").GetString() ==
                          "mortal_item_materialization_gm_authored_client_field");
    }

    [Fact]
    public async Task WaitForContractRepairAsync_ProtectedMortalItemAuthorityFailsClosedBeforeGmDispatch()
    {
        var issue = new ValidationIssue(
            MortalItemIdentityState.StatePath,
            IssueSeverity.Error,
            "The GM changed client-owned item identity authority.",
            code: "mortal_item_materialization_gm_authored_client_field",
            actor: "mortal_item:index",
            section: "MortalItemMaterialization",
            expected: "validated pre-turn index",
            actual: "forged entry",
            repairHint: "Restore the protected before-image.",
            repairTargetFiles: new[] { MortalItemIdentityState.StatePath });
        var engine = CreateGameEngine();
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "protected Mortal item authority",
            new List<ValidationIssue> { issue },
            1,
            null,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
    }

    [Theory]
    [InlineData("mortal_item:unknown")]
    [InlineData("mortal_item:unresolved:shared_inventory")]
    public async Task WaitForContractRepairAsync_UnresolvedMortalItemFailsClosedBeforeGmDispatch(
        string coordinate)
    {
        var issue = new ValidationIssue(
            "game_state/inventory/items.json",
            IssueSeverity.Error,
            "The Mortal item coordinate cannot be resolved exactly.",
            code: "mortal_item_materialization_invalid_carrier_root",
            actor: coordinate,
            section: "MortalItemMaterialization",
            expected: "one exact item coordinate",
            actual: "unresolved",
            repairHint: "Fail closed instead of broad repair.",
            repairTargetFiles: new[] { "game_state/inventory/items.json" });
        var engine = CreateGameEngine();
        var repairSessionGeneration = await GetOrCreateSessionGenerationAsync();

        var accepted = await InvokePrivateAsync<bool>(
            engine,
            "WaitForContractRepairAsync",
            "unresolved Mortal item",
            new List<ValidationIssue> { issue },
            1,
            null,
            repairSessionGeneration);

        Assert.False(accepted);
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
    }
}

[Trait("Category", "FullValidation")]
public sealed class MortalItemMaterializationLifecycleTests
{
    private const string SettlementPath = "game_state/player/item_settlement_probe.json";

    [Fact]
    public async Task IncompleteCraftRepair_RetainsSnapshotAndAcceptsCorrectedSameRequestExactlyOnce()
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        var raw = MortalItemTestFixture.CreateRawRoot(
            "craft_output",
            "craft_request");
        raw.Remove("description");
        var arrangement = await context.ArrangeRouteAsync(
            "craft_output",
            "craft_request",
            raw);
        var retainedAuthority = await context.CaptureExactBytesAsync(
            new[]
            {
                "game_state/control/pending_turn_snapshot.json",
                PendingTurnSnapshotAuthority.AuthorityPath,
                CraftRequestState.PendingRequestPath
            });
        var failedState = await context.CaptureExactBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedTurnAsync());

        await context.AssertExactBytesAsync(failedState);
        await context.AssertExactBytesAsync(retainedAuthority);
        var inventory = (await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath))!.AsObject();
        var rawItem = Assert.Single(
            inventory["UpdateInventory"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(
            arrangement.CreationRef,
            rawItem["creationRef"]!.GetValue<string>());
        rawItem["description"] = "Исправленное описание того же результата ремесла.";
        await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath, inventory);

        var accepted = await context.ValidateNormalizeAndValidateAsync();

        Assert.Empty(accepted.Errors);
        Assert.Equal(1, accepted.NewReceipts);
        Assert.Equal(1, accepted.NewActiveIndexEntries);
        var canonical = (await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath))!.AsObject();
        var item = Assert.Single(canonical["items"]!.AsArray().OfType<JsonObject>());
        Assert.False(canonical.ContainsKey("UpdateInventory"));
        Assert.Equal(
            arrangement.CreationRef,
            item[MortalItemMaterializationContract.ReceiptProperty]!["creationRef"]!
                .GetValue<string>());
        var index = MortalItemIdentityState.Parse(
            await context.FileSystem.ReadFileAsync(MortalItemIdentityState.StatePath));
        var entry = Assert.Single(index.EntriesByItemId).Value;
        Assert.Single(entry["transitions"]!.AsArray());
    }

    [Theory]
    [InlineData("loot_acquisition", "loot_template", "loot_request_42")]
    [InlineData("craft_output", "craft_request", "craft_request_42")]
    [InlineData("trade_output", "npc_trade_receipt", "trade_request_42")]
    [InlineData("quest_reward", "quest_reward", "quest_reward_42")]
    public async Task RepeatedCreationAuthority_SettlesAndMaterializesAtMostOnce(
        string route,
        string authorityKind,
        string authorityId)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        await context.WriteJsonAsync(
            SettlementPath,
            new JsonObject { ["settlements"] = 0 });
        var raw = MortalItemTestFixture.CreateRawRoot(
            route,
            authorityKind,
            authorityId);
        var mutation = new MortalItemTransitionMutation(
            new[] { SettlementPath },
            mutationContext =>
            {
                var root = mutationContext.GetRequiredRoot(SettlementPath);
                root["settlements"] =
                    (root["settlements"]?.GetValue<int>() ?? 0) + 1;
                return null;
            });

        var first = await CreateAsync(
            context.FileSystem,
            raw,
            authorityKind,
            authorityId,
            mutation);

        Assert.True(first.Success, first.Message);
        var acceptedBytes = await context.CaptureExactBytesAsync(
            new[]
            {
                InventoryEquipmentService.ItemsPath,
                MortalItemIdentityState.StatePath,
                SettlementPath
            });

        var replay = await CreateAsync(
            context.FileSystem,
            raw,
            authorityKind,
            authorityId,
            mutation);

        Assert.False(replay.Success);
        await context.AssertExactBytesAsync(acceptedBytes);
        var inventory = (await context.ReadJsonAsync(
            InventoryEquipmentService.ItemsPath))!.AsObject();
        var item = Assert.Single(inventory["items"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(first.ItemId, item["itemId"]!.GetValue<string>());
        Assert.NotNull(item[MortalItemMaterializationContract.ReceiptProperty]);
        var index = MortalItemIdentityState.Parse(
            await context.FileSystem.ReadFileAsync(MortalItemIdentityState.StatePath));
        var entry = Assert.Single(index.EntriesByItemId).Value;
        Assert.Single(entry["transitions"]!.AsArray());
        Assert.Equal(
            1,
            (await context.ReadJsonAsync(SettlementPath))!["settlements"]!.GetValue<int>());
    }

    [Theory]
    [InlineData("destroy", "exact")]
    [InlineData("destroy", "confusable")]
    [InlineData("destroy", "missing_materialization_id")]
    [InlineData("merge", "exact")]
    [InlineData("merge", "confusable")]
    [InlineData("merge", "missing_materialization_id")]
    public async Task RepeatedCreationAfterIdentityRetirement_DoesNotResettleOrRegrant(
        string retirement,
        string replayVariant)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        await context.WriteJsonAsync(
            SettlementPath,
            new JsonObject { ["settlements"] = 0 });
        var raw = MortalItemTestFixture.CreateRawRoot(
            "player_acquisition",
            "turn_outcome",
            "retired_creation_authority");
        var mutation = new MortalItemTransitionMutation(
            new[] { SettlementPath },
            mutationContext =>
            {
                var root = mutationContext.GetRequiredRoot(SettlementPath);
                root["settlements"] =
                    (root["settlements"]?.GetValue<int>() ?? 0) + 1;
                return null;
            });
        var first = await CreateAsync(
            context.FileSystem,
            raw,
            "turn_outcome",
            "retired_creation_authority",
            mutation);
        Assert.True(first.Success, first.Message);
        Assert.NotNull(first.ItemId);

        var playerCarrier = new MortalItemCarrierCoordinate(
            "player_inventory",
            "player",
            null,
            Array.Empty<string>());
        MortalItemTransitionResult retired;
        if (retirement == "destroy")
        {
            retired = await ExecuteAsync(
                context.FileSystem,
                new MortalItemTransitionIntent(
                    MortalItemTransitionKind.Destroy,
                    new[] { first.ItemId! },
                    playerCarrier,
                    DestinationCarrier: null,
                    Quantity: 1,
                    Turn: 43,
                    AuthorityKind: "inventory_discard",
                    AuthorityId: "retire_original_creation"));
        }
        else
        {
            var survivorRaw = MortalItemTestFixture.CreateRawRoot(
                "player_acquisition",
                "turn_outcome",
                "survivor_creation_authority",
                creationRef: "new_item_survivor",
                materializationId: "mat_item_survivor");
            var survivor = await CreateAsync(
                context.FileSystem,
                survivorRaw,
                "turn_outcome",
                "survivor_creation_authority",
                mutation: null);
            Assert.True(survivor.Success, survivor.Message);
            Assert.NotNull(survivor.ItemId);
            retired = await ExecuteAsync(
                context.FileSystem,
                new MortalItemTransitionIntent(
                    MortalItemTransitionKind.Merge,
                    new[] { first.ItemId!, survivor.ItemId! },
                    playerCarrier,
                    playerCarrier,
                    Quantity: 2,
                    Turn: 43,
                    AuthorityKind: "inventory_merge",
                    AuthorityId: "retire_original_by_merge",
                    SurvivorItemId: survivor.ItemId));
        }

        Assert.True(retired.Success, retired.Message);
        var retiredBytes = await context.CaptureExactBytesAsync(
            new[]
            {
                InventoryEquipmentService.ItemsPath,
                MortalItemIdentityState.StatePath,
                SettlementPath
            });

        var replayRaw = raw.DeepClone().AsObject();
        var confusableAlias = replayVariant == "confusable";
        replayRaw["creationRef"] = confusableAlias
            ? MortalItemTestFixture.CreationRef.ToUpperInvariant()
            : MortalItemTestFixture.CreationRef;
        replayRaw["materialization"]!["creationRef"] = replayRaw["creationRef"]!.DeepClone();
        if (replayVariant == "missing_materialization_id")
        {
            replayRaw["materialization"]!.AsObject().Remove("materializationId");
        }
        else
        {
            replayRaw["materialization"]!["materializationId"] = confusableAlias
                ? MortalItemTestFixture.MaterializationId.ToUpperInvariant()
                : "mat_item_retired_creation_replay";
        }
        var replay = await CreateAsync(
            context.FileSystem,
            replayRaw,
            "turn_outcome",
            "retired_creation_authority",
            mutation);

        Assert.False(replay.Success);
        await context.AssertExactBytesAsync(retiredBytes);
        Assert.Equal(
            1,
            (await context.ReadJsonAsync(SettlementPath))!["settlements"]!.GetValue<int>());
        var index = MortalItemIdentityState.Parse(
            await context.FileSystem.ReadFileAsync(MortalItemIdentityState.StatePath));
        Assert.Contains(
            index.EntriesByItemId.Values,
            entry =>
                entry["originMaterializationIds"]!.AsArray().Any(origin =>
                    string.Equals(
                        origin?.GetValue<string>(),
                        MortalItemTestFixture.MaterializationId,
                        StringComparison.Ordinal)));

        replayRaw["materialization"]!["sourceTurn"] = 44;
        replayRaw["materialization"]!["sourceAuthority"]!["authorityId"] = "turn_44";
        await context.CaptureValidatedPendingSnapshotAsync(44);
        await context.WritePlayerUpdateAsync(replayRaw);
        var rawReplayIssues = await context.Validator
            .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        Assert.Contains(rawReplayIssues, issue => issue.Code ==
            (confusableAlias
                ? "mortal_item_materialization_historical_identity_ambiguity"
                : "mortal_item_materialization_creation_replay"));
        Assert.True(MortalItemRepairPacketBuilder.RequiresFailClosedRollback(
            rawReplayIssues));
        Assert.Empty(MortalItemRepairPacketBuilder.Build(rawReplayIssues));
        var rawReplayBytes = await context.CaptureExactBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => context.NormalizeAcceptedTurnAsync());

        await context.AssertExactBytesAsync(rawReplayBytes);
    }

    [Theory]
    [InlineData("transfer")]
    [InlineData("split")]
    [InlineData("merge")]
    public async Task RepeatedTransitionAuthority_MutatesQuantityAndLineageAtMostOnce(
        string operation)
    {
        await using var context = await MortalItemMaterializationTestContext.CreateAsync();
        await context.BuildMortalBootstrapAsync();
        var selected = MortalItemTestFixture.CreateCanonicalRoot("itm_repair_selected");
        selected["count"] = operation == "merge" ? 2 : 4;
        var contributor = MortalItemTestFixture.CreateCanonicalRoot("itm_repair_contributor");
        contributor["count"] = 3;
        var items = operation == "merge"
            ? new[] { selected, contributor }
            : new[] { selected };
        await context.WriteCanonicalPlayerItemsAsync(
            MortalItemTestFixture.CreateIndex(items),
            items);
        await context.WriteJsonAsync(
            NpcCoreChangesContract.NpcCorePath,
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(),
                ["NPCsInScene"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = "npc_repair_destination",
                        ["inventory"] = new JsonArray(),
                        ["equippedItems"] = new JsonObject()
                    })
            });

        var source = new MortalItemCarrierCoordinate(
            "player_inventory",
            "player",
            null,
            Array.Empty<string>());
        var intent = operation switch
        {
            "transfer" => new MortalItemTransitionIntent(
                MortalItemTransitionKind.Transfer,
                new[] { "itm_repair_selected" },
                source,
                new MortalItemCarrierCoordinate(
                    "npc_inventory",
                    "npc_repair_destination",
                    null,
                    Array.Empty<string>()),
                Quantity: 4,
                Turn: 43,
                AuthorityKind: "repair_replay",
                AuthorityId: "repair_request_transfer_42"),
            "split" => new MortalItemTransitionIntent(
                MortalItemTransitionKind.Split,
                new[] { "itm_repair_selected" },
                source,
                source,
                Quantity: 1,
                Turn: 43,
                AuthorityKind: "repair_replay",
                AuthorityId: "repair_request_split_42"),
            "merge" => new MortalItemTransitionIntent(
                MortalItemTransitionKind.Merge,
                new[] { "itm_repair_selected", "itm_repair_contributor" },
                source,
                source,
                Quantity: 5,
                Turn: 43,
                AuthorityKind: "repair_replay",
                AuthorityId: "repair_request_merge_42",
                SurvivorItemId: "itm_repair_selected"),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
        };

        var first = await ExecuteAsync(context.FileSystem, intent);

        Assert.True(first.Success, first.Message);
        var acceptedBytes = await context.CaptureExactBytesAsync(
            new[]
            {
                InventoryEquipmentService.ItemsPath,
                NpcCoreChangesContract.NpcCorePath,
                MortalItemIdentityState.StatePath
            });

        var replay = await ExecuteAsync(context.FileSystem, intent);

        Assert.False(replay.Success);
        await context.AssertExactBytesAsync(acceptedBytes);
        var index = MortalItemIdentityState.Parse(
            await context.FileSystem.ReadFileAsync(MortalItemIdentityState.StatePath));
        Assert.Empty(index.Issues);
        var authorityUses = index.EntriesByItemId.Values
            .SelectMany(entry => entry["transitions"]!.AsArray().OfType<JsonObject>())
            .Count(transition =>
                transition["authorityId"]?.GetValue<string>() == intent.AuthorityId);
        Assert.Equal(operation == "transfer" ? 1 : 2, authorityUses);
        var activeQuantity = index.EntriesByItemId.Values
            .Where(entry => entry["state"]?.GetValue<string>() == "active")
            .Sum(entry => entry["transitions"]!.AsArray()[^1]!["quantityAfter"]!.GetValue<int>());
        Assert.Equal(operation == "merge" ? 5 : 4, activeQuantity);
    }

    private static async Task<MortalItemTransitionResult> CreateAsync(
        FileSystemManager fileSystem,
        JsonObject raw,
        string authorityKind,
        string authorityId,
        MortalItemTransitionMutation? mutation)
    {
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        return await new MortalItemTransitionWriter(fileSystem).CreateAsync(
            writeLease,
            raw,
            new MortalItemCarrierCoordinate(
                "player_inventory",
                "player",
                null,
                Array.Empty<string>()),
            acceptedTurn: 42,
            authorityKind,
            authorityId,
            mutation);
    }

    private static async Task<MortalItemTransitionResult> ExecuteAsync(
        FileSystemManager fileSystem,
        MortalItemTransitionIntent intent)
    {
        await using var writeLease = await fileSystem.AcquireCanonicalWriteLeaseAsync();
        return await new MortalItemTransitionWriter(fileSystem).ExecuteAsync(
            writeLease,
            intent);
    }
}
