using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal sealed partial class MortalItemMaterializationTestContext
{
    private const int RouteTurn = 42;
    private const string ExistingNpcId = "npc_route_existing";
    private const string NewNpcInitialId = "npc_route_new";
    private const string RouteLocationId = "loc_route_existing";
    private const string RouteTargetLocationId = "loc_route_target";
    private const string RouteStorageId = "storage_route_existing";
    internal const string SameTurnRouteStorageId = "storage_route_same_turn";
    private const string CraftRequestId = "craft_request_route_42";
    private const string TradeRequestId = "trade_request_route_42";
    private const string QuestRewardId = "quest_reward_route_42";

    internal async Task<MortalItemRouteArrangement> ArrangeRouteAsync(
        string route,
        string authorityKind,
        JsonObject rawItem)
    {
        ArgumentNullException.ThrowIfNull(rawItem);

        await BuildMortalBootstrapAsync();
        await ArrangeRouteBaselineAsync(route);
        await CaptureValidatedPendingSnapshotAsync(RouteTurn);
        var item = rawItem.DeepClone().AsObject();
        var authorityId = ResolveRouteAuthorityId(route, item);
        item["materialization"]!["route"] = route;
        item["materialization"]!["sourceTurn"] = RouteTurn;
        item["materialization"]!["sourceAuthority"]!["kind"] = authorityKind;
        item["materialization"]!["sourceAuthority"]!["authorityId"] = authorityId;

        switch (route)
        {
            case "player_acquisition":
                await WritePlayerUpdateAsync(item);
                break;
            case "npc_acquisition":
                await ArrangeExistingNpcAcquisitionAsync(item);
                break;
            case "new_npc_inventory":
                await ArrangeNewNpcInventoryAsync(item);
                break;
            case "loot_acquisition":
                await ArrangeLootAcquisitionAsync(item);
                break;
            case "craft_output":
                await WritePlayerUpdateAsync(item);
                break;
            case "trade_output":
                await ArrangeTradeOutputAsync(item);
                break;
            case "quest_reward":
                await ArrangeQuestRewardAsync(item);
                break;
            case "storage_placement":
                await ArrangeStoragePlacementAsync(item);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(route), route, null);
        }

        return new MortalItemRouteArrangement(
            route,
            authorityKind,
            authorityId,
            item["creationRef"]!.GetValue<string>());
    }

    internal async Task<MortalItemRouteArrangement>
        ArrangeSameTurnCurrentLocationStorageRouteAsync(JsonObject rawItem)
    {
        ArgumentNullException.ThrowIfNull(rawItem);

        await BuildMortalBootstrapAsync();
        await CaptureValidatedPendingSnapshotAsync(RouteTurn);

        var item = rawItem.DeepClone().AsObject();
        var authorityId =
            $"{MortalLocationTestFixture.LocationInitialId}:{SameTurnRouteStorageId}";
        item["materialization"]!["route"] = "storage_placement";
        item["materialization"]!["sourceTurn"] = RouteTurn;
        item["materialization"]!["sourceAuthority"]!["kind"] = "location_storage";
        item["materialization"]!["sourceAuthority"]!["authorityId"] = authorityId;

        var location = MortalLocationTestFixture.CreateRawLocation(
            "current_scene_creation");
        location["locationStorages"] = new JsonArray(
            MortalLocationTestFixture.CreateStorageMetadata(
                SameTurnRouteStorageId,
                "Сундук новой сцены",
                hasFullAccess: true,
                contents: CloneRouteArray(item)));
        location["materialization"]!["sections"]!["storageMetadata"] =
            new JsonObject
            {
                ["disposition"] = "populated",
                ["reason"] = null
            };
        await WriteJsonAsync(
            StorageTransportMoveService.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = location });

        return new MortalItemRouteArrangement(
            "storage_placement",
            "location_storage",
            authorityId,
            item["creationRef"]!.GetValue<string>());
    }

    internal async Task ArrangeRemoteLocationStorageItemAsync(JsonObject rawItem)
    {
        ArgumentNullException.ThrowIfNull(rawItem);

        await BuildMortalBootstrapAsync();
        await CaptureValidatedPendingSnapshotAsync(RouteTurn);

        var item = rawItem.DeepClone().AsObject();
        item["materialization"]!["route"] = "storage_placement";
        item["materialization"]!["sourceTurn"] = RouteTurn;
        item["materialization"]!["sourceAuthority"]!["kind"] = "location_storage";
        item["materialization"]!["sourceAuthority"]!["authorityId"] =
            $"{MortalLocationTestFixture.LocationInitialId}:{SameTurnRouteStorageId}";

        var location = MortalLocationTestFixture.CreateRawLocation(
            "world_map_creation");
        location["locationStorages"] = new JsonArray(
            MortalLocationTestFixture.CreateStorageMetadata(
                SameTurnRouteStorageId,
                "Удалённый сундук",
                hasFullAccess: true,
                contents: CloneRouteArray(item)));
        location["materialization"]!["sections"]!["storageMetadata"] =
            new JsonObject
            {
                ["disposition"] = "populated",
                ["reason"] = null
            };
        await WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["worldMapUpdates"] = new JsonObject
                {
                    ["newLocations"] = new JsonArray(location),
                    ["newLinks"] = new JsonArray()
                }
            });
    }

    internal async Task<MortalItemRouteOutcome> ValidateNormalizeAndValidateAsync()
    {
        var rawIssues = await Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync();
        if (rawIssues.Any(issue => issue.Severity == IssueSeverity.Error))
        {
            return new MortalItemRouteOutcome(
                rawIssues,
                Array.Empty<ValidationIssue>(),
                0,
                0);
        }

        var postSealIssues = await NormalizeAcceptedTurnWithIssuesAsync();
        var receiptCount = 0;
        foreach (var path in new[]
                 {
                     InventoryEquipmentService.ItemsPath,
                     NpcCoreChangesContract.NpcCorePath,
                     StorageTransportMoveService.CurrentLocationPath
                 })
        {
            receiptCount += CountMortalItemReceipts(await ReadJsonAsync(path));
        }

        var index = await ReadJsonAsync(MortalItemIdentityState.StatePath) as JsonObject;
        var activeEntries = index?["entries"]?.AsArray()
            .OfType<JsonObject>()
            .Count(entry => string.Equals(
                entry["state"]?.GetValue<string>(),
                "active",
                StringComparison.Ordinal)) ?? 0;
        return new MortalItemRouteOutcome(
            rawIssues,
            postSealIssues,
            receiptCount,
            activeEntries);
    }

    internal async Task<MortalItemRouteArrangement>
        ArrangeExistingNpcContainerAcquisitionAsync()
    {
        const string containerId = "itm_npc_route_container";
        await BuildMortalBootstrapAsync();
        var container = MortalItemTestFixture.CreateCanonicalRoot(containerId);
        container["isContainer"] = true;
        container["capacity"] = 10;
        container["materialization"]!["sections"]!["container"] = new JsonObject
        {
            ["state"] = "populated",
            ["reason"] = null
        };
        container["materialization"]!["sourceTurn"] = 41;
        container["materialization"]!["sourceAuthority"]!["authorityId"] = "turn_41";
        var receipt = MortalItemIdentityState.CreateRootReceipt(
            container,
            containerId,
            41);
        container["materializationReceipt"] = receipt;
        await WriteJsonAsync(
            NpcCoreChangesContract.NpcCorePath,
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(),
                ["NPCsInScene"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = ExistingNpcId,
                        ["name"] = "NPC с контейнером",
                        ["inventory"] = CloneRouteArray(container),
                        ["equippedItems"] = new JsonObject()
                    })
            });
        await WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndexForCarrier(
                container,
                "npc_inventory",
                ExistingNpcId));
        await CaptureValidatedPendingSnapshotAsync(RouteTurn);

        var item = MortalItemTestFixture.CreateRawRoot(
            "npc_acquisition",
            "npc_inventory_add",
            $"npc_inventory_add:{RouteTurn}:0:{ExistingNpcId}");
        await WriteJsonAsync(
            "game_state/npcs/npc_inventory.json",
            new JsonObject
            {
                ["NPCInventoryAdds"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = ExistingNpcId,
                        ["NPCName"] = "NPC с контейнером",
                        ["item"] = item.DeepClone(),
                        ["destinationContainerId"] = containerId
                    })
            });
        return new MortalItemRouteArrangement(
            "npc_acquisition",
            "npc_inventory_add",
            $"npc_inventory_add:{RouteTurn}:0:{ExistingNpcId}",
            item["creationRef"]!.GetValue<string>());
    }

    internal async Task<MortalItemRouteArrangement> ArrangeCraftRouteWithStatusAsync(
        string status)
    {
        await BuildMortalBootstrapAsync();
        await WriteJsonAsync(
            CraftRequestState.PendingRequestPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["requestId"] = CraftRequestId,
                ["status"] = status,
                ["recipeId"] = "recipe_route_test",
                ["craftIntent"] = "Создать тестовый предмет.",
                ["sourceItemIds"] = new JsonArray()
            });
        await CaptureValidatedPendingSnapshotAsync(RouteTurn);

        var item = MortalItemTestFixture.CreateRawRoot(
            "craft_output",
            "craft_request",
            CraftRequestId);
        await WritePlayerUpdateAsync(item);
        return new MortalItemRouteArrangement(
            "craft_output",
            "craft_request",
            CraftRequestId,
            item["creationRef"]!.GetValue<string>());
    }

    internal async Task ForgeTradeReceiptMerchantProfileAsync(string merchantProfile)
    {
        var root = (await ReadJsonAsync(NpcCoreChangesContract.NpcCorePath))!.AsObject();
        var receipt = root[NpcTradeRequestState.UpdateReceiptsProperty]!
            .AsArray()
            .OfType<JsonObject>()
            .Single();
        receipt["merchantProfile"] = merchantProfile;
        await WriteJsonAsync(NpcCoreChangesContract.NpcCorePath, root);
    }

    internal async Task ForgeTradeOfferNameAsync(string name)
    {
        var root = (await ReadJsonAsync(NpcCoreChangesContract.NpcCorePath))!.AsObject();
        var npc = root["NPCsInScene"]!.AsArray().OfType<JsonObject>().Single();
        var offer = npc["tradeInventory"]!["items"]!
            .AsArray()
            .OfType<JsonObject>()
            .Single();
        offer["itemData"]!["name"] = name;
        await WriteJsonAsync(NpcCoreChangesContract.NpcCorePath, root);
    }

    internal async Task ForgeTradeOfferSlotIdAsync(string slotId)
    {
        var root = (await ReadJsonAsync(NpcCoreChangesContract.NpcCorePath))!.AsObject();
        var npc = root["NPCsInScene"]!.AsArray().OfType<JsonObject>().Single();
        var offer = npc["tradeInventory"]!["items"]!
            .AsArray()
            .OfType<JsonObject>()
            .Single();
        offer["slotId"] = slotId;
        await WriteJsonAsync(NpcCoreChangesContract.NpcCorePath, root);
    }

    internal async Task DuplicateQuestRewardItemDetailAsync()
    {
        var root = (await ReadJsonAsync("game_state/quests/quest_history.json"))!.AsObject();
        var items = root["questRewards"]![0]!["itemsReceived"]!.AsArray();
        items.Add(items[0]!.DeepClone());
        await WriteJsonAsync("game_state/quests/quest_history.json", root);
    }

    internal async Task MarkQuestRewardItemUnavailableAsync()
    {
        var root = (await ReadJsonAsync("game_state/quests/quest_history.json"))!.AsObject();
        var item = root["questRewards"]![0]!["itemsReceived"]![0]!.AsObject();
        item["authorityStatus"] = "PriorIncarnation";
        item["reason"] = "Предмет остался в прошлой жизни.";
        await WriteJsonAsync("game_state/quests/quest_history.json", root);
    }

    internal async Task MovePlayerRawCreationToNewStorageAsync()
    {
        var inventory = (await ReadJsonAsync(
            InventoryEquipmentService.ItemsPath))!.AsObject();
        var updates = inventory["UpdateInventory"]!.AsArray();
        var rawItem = AssertSingleObject(updates).DeepClone().AsObject();
        inventory.Remove("UpdateInventory");
        await WriteJsonAsync(InventoryEquipmentService.ItemsPath, inventory);

        var locationRoot = (await ReadJsonAsync(
            StorageTransportMoveService.CurrentLocationPath))!.AsObject();
        var location = locationRoot["currentLocationData"] as JsonObject ?? locationRoot;
        location["locationStorages"] = new JsonArray(
            MortalLocationTestFixture.CreateStorageMetadata(
                "storage_same_turn_unmaterialized",
                "Новое хранилище без pre-turn authority",
                hasFullAccess: true,
                contents: new JsonArray(rawItem)));
        await WriteJsonAsync(StorageTransportMoveService.CurrentLocationPath, locationRoot);
    }

    internal async Task MovePlayerRawCreationToExistingStorageAsync()
    {
        var inventory = (await ReadJsonAsync(
            InventoryEquipmentService.ItemsPath))!.AsObject();
        var updates = inventory["UpdateInventory"]!.AsArray();
        var rawItem = AssertSingleObject(updates).DeepClone().AsObject();
        inventory.Remove("UpdateInventory");
        await WriteJsonAsync(InventoryEquipmentService.ItemsPath, inventory);

        var locationRoot = (await ReadJsonAsync(
            StorageTransportMoveService.CurrentLocationPath))!.AsObject();
        var location = locationRoot["currentLocationData"] as JsonObject ?? locationRoot;
        var storage = location["locationStorages"]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(candidate => string.Equals(
                candidate["storageId"]!.GetValue<string>(),
                RouteStorageId,
                StringComparison.Ordinal));
        storage["contents"]!.AsArray().Add(rawItem);
        await WriteJsonAsync(StorageTransportMoveService.CurrentLocationPath, locationRoot);
    }

    internal async Task AddSameLocationRefreshWrapperAsync()
    {
        var root = (await ReadJsonAsync(
            StorageTransportMoveService.CurrentLocationPath))!.AsObject();
        root["currentLocationData"] = new JsonObject
        {
            ["locationId"] = RouteLocationId,
            ["lastEventsDescription"] = "Герой остаётся в маршрутной локации.",
            ["currentWeather"] = new JsonObject { ["summary"] = "Тихий дождь" },
            ["currentInteractions"] = new JsonArray()
        };
        await WriteJsonAsync(StorageTransportMoveService.CurrentLocationPath, root);
    }

    internal async Task AddCurrentStorageRemovalCommandAsync()
    {
        var root = (await ReadJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        root["worldMapUpdates"] = new JsonObject
        {
            ["storagesToRemove"] = new JsonArray(new JsonObject
            {
                ["targetLocationId"] = RouteLocationId,
                ["storageId"] = RouteStorageId
            })
        };
        await WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, root);
    }

    internal async Task<MortalItemRouteArrangement>
        ArrangeStoragePlacementWithAuthorizedMovementAsync(JsonObject rawItem)
    {
        ArgumentNullException.ThrowIfNull(rawItem);

        await BuildMortalBootstrapAsync();
        var source = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            RouteLocationId,
            "Маршрутная локация",
            discoveryTier: "visited");
        source["locationStorages"] = new JsonArray(
            MortalLocationTestFixture.CreateStorageMetadata(
                RouteStorageId,
                "Маршрутное хранилище",
                hasFullAccess: true,
                contents: new JsonArray()));
        source["materialization"]!["sections"]!["storageMetadata"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        MortalLocationTestFixture.ResealCanonicalLocation(source);
        var target = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            RouteTargetLocationId,
            "Маршрутная цель",
            discoveryTier: "discovered",
            x: 1);
        var link = MortalLocationTestFixture.CreateCanonicalLink(
            RouteLocationId,
            RouteTargetLocationId);
        var mapSource = source.DeepClone().AsObject();
        mapSource["locationStorages"]![0]!.AsObject().Remove("contents");
        var map = MortalLocationTestFixture.CreateWorldMap(mapSource, target);
        map["links"]!.AsArray().Add(link.DeepClone());
        var index = MortalLocationTestFixture.CreateIdentityIndex(source, link);
        index["locationEntries"]!.AsArray().Add(
            MortalLocationTestFixture.CreateIdentityIndex(target)
                ["locationEntries"]![0]!.DeepClone());

        await WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        await WriteJsonAsync(
            StorageTransportMoveService.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(source));
        await WriteJsonAsync(MortalLocationIdentityState.StatePath, index);
        await CaptureValidatedPendingSnapshotAsync(RouteTurn);

        var item = rawItem.DeepClone().AsObject();
        var authorityId = $"{RouteLocationId}:{RouteStorageId}";
        item["materialization"]!["route"] = "storage_placement";
        item["materialization"]!["sourceTurn"] = RouteTurn;
        item["materialization"]!["sourceAuthority"]!["kind"] = "location_storage";
        item["materialization"]!["sourceAuthority"]!["authorityId"] = authorityId;
        var current = (await ReadJsonAsync(
            StorageTransportMoveService.CurrentLocationPath))!.AsObject();
        current["locationStorages"]![0]!["contents"] = new JsonArray(item);
        current["currentLocationData"] = new JsonObject
        {
            ["locationId"] = RouteTargetLocationId,
            ["lastEventsDescription"] = "Герой дошёл до маршрутной цели.",
            ["currentWeather"] = new JsonObject { ["summary"] = "Тихий ветер" },
            ["currentInteractions"] = new JsonArray()
        };
        await WriteJsonAsync(StorageTransportMoveService.CurrentLocationPath, current);

        return new MortalItemRouteArrangement(
            "storage_placement",
            "location_storage",
            authorityId,
            item["creationRef"]!.GetValue<string>());
    }

    private static JsonObject AssertSingleObject(JsonArray array)
    {
        if (array.Count != 1 || array[0] is not JsonObject result)
            throw new InvalidOperationException("Expected exactly one raw item object.");
        return result;
    }

    internal async Task SetNpcDestinationContainerAsync(string containerId)
    {
        var root = (await ReadJsonAsync(
            "game_state/npcs/npc_inventory.json"))!.AsObject();
        root["NPCInventoryAdds"]![0]!["destinationContainerId"] = containerId;
        await WriteJsonAsync("game_state/npcs/npc_inventory.json", root);
    }

    internal async Task ForgeRawRouteAuthorityIdAsync(
        string creationRef,
        string forgedAuthorityId)
    {
        var matches = 0;
        foreach (var path in new[]
                 {
                     InventoryEquipmentService.ItemsPath,
                     NpcCoreChangesContract.NpcCorePath,
                     "game_state/npcs/npc_inventory.json",
                     StorageTransportMoveService.CurrentLocationPath
                 })
        {
            var root = await ReadJsonAsync(path);
            if (root == null)
                continue;
            var changed = ForgeAuthority(
                root,
                creationRef,
                forgedAuthorityId,
                ref matches);
            if (changed)
                await WriteJsonAsync(path, root);
        }

        if (matches != 1)
        {
            throw new InvalidOperationException(
                $"Expected one raw item '{creationRef}', found {matches}.");
        }
    }

    internal async Task ForgeAcceptedCreateTransitionAuthorityAsync(
        string forgedAuthorityId)
    {
        var index = (await ReadJsonAsync(MortalItemIdentityState.StatePath))!.AsObject();
        var entry = index["entries"]!.AsArray().OfType<JsonObject>().Single();
        var transition = entry["transitions"]!.AsArray()
            .OfType<JsonObject>()
            .Single(item => string.Equals(
                item["kind"]!.GetValue<string>(),
                "create",
                StringComparison.Ordinal));
        transition["authorityId"] = forgedAuthorityId;
        await WriteJsonAsync(MortalItemIdentityState.StatePath, index);
    }

    internal async Task AppendAcceptedTransferTransitionAsync()
    {
        var index = (await ReadJsonAsync(MortalItemIdentityState.StatePath))!.AsObject();
        var entry = index["entries"]!.AsArray().OfType<JsonObject>().Single();
        var itemId = entry["itemId"]!.GetValue<string>();
        var carrier = entry["currentCarrier"]!.AsObject().DeepClone().AsObject();
        MortalItemIdentityState.AppendTransition(
            entry,
            MortalItemIdentityState.CreateTransition(
                "transfer",
                RouteTurn + 1,
                new[] { itemId },
                carrier,
                carrier,
                quantityBefore: 1,
                quantityAfter: 1,
                authorityKind: "player_command",
                authorityId: "player_transfer_after_quest_reward"));
        await WriteJsonAsync(MortalItemIdentityState.StatePath, index);
    }

    private async Task ArrangeExistingNpcAcquisitionAsync(JsonObject item)
    {
        await WriteJsonAsync(
            "game_state/npcs/npc_inventory.json",
            new JsonObject
            {
                ["NPCInventoryAdds"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = ExistingNpcId,
                        ["NPCName"] = "Маршрутный NPC",
                        ["item"] = item.DeepClone(),
                        ["destinationContainerId"] = null
                    })
            });
    }

    private Task ArrangeNewNpcInventoryAsync(JsonObject item) =>
        WriteJsonAsync(
            NpcCoreChangesContract.NpcCorePath,
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = null,
                        ["initialId"] = NewNpcInitialId,
                        ["name"] = "Новый маршрутный NPC",
                        ["inventory"] = CloneRouteArray(item),
                        ["equippedItems"] = new JsonObject(),
                        ["materialization"] = new JsonObject
                        {
                            ["schemaVersion"] = 1,
                            ["materializationId"] = "mat_actor_route_new",
                            ["actorType"] = "mortal_npc",
                            ["actorId"] = NewNpcInitialId,
                            ["materializedAtTurn"] = RouteTurn,
                            ["state"] = "complete"
                        }
                    }),
                ["NPCsInScene"] = new JsonArray()
            });

    private async Task ArrangeLootAcquisitionAsync(JsonObject item)
    {
        var turn = (await ReadJsonAsync("input/turn_request.json"))!.AsObject();
        turn["additionalContext"] = new JsonObject
        {
            ["lootForCurrentTurn"] = new JsonArray(
                new JsonObject
                {
                    ["baseName"] = item["name"]!.GetValue<string>()
                })
        };
        await WriteJsonAsync("input/turn_request.json", turn);
        await WritePlayerUpdateAsync(item);
    }

    private async Task ArrangeTradeOutputAsync(JsonObject item)
    {
        var root = (await ReadJsonAsync(NpcCoreChangesContract.NpcCorePath))!.AsObject();
        var npc = root["NPCsInScene"]!.AsArray().OfType<JsonObject>().Single();
        var creationRef = item["creationRef"]!.GetValue<string>();
        item["tradeItemClass"] = "Functional";
        item["baseSellPrice"] = 4;
        var offerItemData = item.DeepClone().AsObject();
        offerItemData.Remove("existedId");
        offerItemData.Remove("creationRef");
        offerItemData.Remove("materialization");
        offerItemData["itemId"] = "trade_offer_item_route_001";
        npc["tradeInventory"] = new JsonObject
        {
            ["tradeCycleId"] = "trade_cycle_route_42",
            ["generatedAtWorldDate"] = 1,
            ["refreshAfterWorldDate"] = 2,
            ["items"] = new JsonArray(
                new JsonObject
                {
                    ["slotId"] = creationRef,
                    ["itemId"] = "trade_offer_item_route_001",
                    ["price"] = 10,
                    ["merchantProfile"] = "GeneralGoods",
                    ["soldOut"] = false,
                    ["itemData"] = offerItemData
                })
        };
        root[NpcTradeRequestState.UpdateReceiptsProperty] = new JsonArray(
            new JsonObject
            {
                ["requestId"] = TradeRequestId,
                ["npcId"] = ExistingNpcId,
                ["npcName"] = "Маршрутный торговец",
                ["tradeCycleId"] = "trade_cycle_route_42",
                ["merchantProfile"] = "GeneralGoods",
                ["status"] = NpcTradeRequestState.ReceiptStatusReady,
                ["itemCount"] = 1,
                ["resolvedAtTurn"] = RouteTurn,
                ["resolvedAtUtc"] = "2026-08-11T00:00:01Z"
            });
        await WriteJsonAsync(NpcCoreChangesContract.NpcCorePath, root);
        await WritePlayerUpdateAsync(item);
    }

    private async Task ArrangeQuestRewardAsync(JsonObject item)
    {
        await WriteJsonAsync(
            "game_state/quests/quest_history.json",
            new JsonObject
            {
                ["questHistory"] = new JsonArray(
                    new JsonObject
                    {
                        ["questId"] = "quest_route_42",
                        ["questName"] = "Маршрутная награда",
                        ["outcome"] = "completed",
                        ["completionDate"] = "2026-08-11T00:00:00Z",
                        ["experience"] = 0,
                        ["incarnationNumber"] = 1
                    }),
                ["questRewards"] = new JsonArray(
                    new JsonObject
                    {
                        ["questId"] = "quest_route_42",
                        ["rewardId"] = QuestRewardId,
                        ["itemsReceived"] = new JsonArray(
                            new JsonObject
                            {
                                ["creationRef"] = item["creationRef"]!.GetValue<string>(),
                                ["displayName"] = item["name"]!.GetValue<string>()
                            })
                    }),
                ["questChains"] = new JsonArray()
            });
        await WritePlayerUpdateAsync(item);
    }

    private async Task ArrangeStoragePlacementAsync(JsonObject item)
    {
        var root = (await ReadJsonAsync(
            StorageTransportMoveService.CurrentLocationPath))!.AsObject();
        var location = root["currentLocationData"] as JsonObject ?? root;
        var storage = location["locationStorages"]!.AsArray()[0]!.AsObject();
        storage["contents"] = CloneRouteArray(item);
        await WriteJsonAsync(StorageTransportMoveService.CurrentLocationPath, root);
    }

    private async Task ArrangeRouteBaselineAsync(string route)
    {
        switch (route)
        {
            case "npc_acquisition":
                await WriteExistingNpcBaselineAsync("Маршрутный NPC");
                break;
            case "trade_output":
                await WriteExistingNpcBaselineAsync("Маршрутный торговец");
                await WriteTradeRequestBaselineAsync();
                await WriteStorageBaselineAsync();
                break;
            case "craft_output":
                await WriteJsonAsync(
                    CraftRequestState.PendingRequestPath,
                    new JsonObject
                    {
                        ["schemaVersion"] = 1,
                        ["requestId"] = CraftRequestId,
                        ["status"] = "pending_gm_resolution",
                        ["recipeId"] = "recipe_route_test",
                        ["craftIntent"] = "Создать тестовый предмет.",
                        ["sourceItemIds"] = new JsonArray()
                    });
                break;
            case "storage_placement":
                await WriteStorageBaselineAsync();
                break;
        }
    }

    private Task WriteExistingNpcBaselineAsync(string name) =>
        WriteJsonAsync(
            NpcCoreChangesContract.NpcCorePath,
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(),
                ["NPCsInScene"] = new JsonArray(
                    new JsonObject
                    {
                        ["NPCId"] = ExistingNpcId,
                        ["name"] = name,
                        ["inventory"] = new JsonArray(),
                        ["equippedItems"] = new JsonObject()
                    })
            });

    private Task WriteTradeRequestBaselineAsync() =>
        WriteJsonAsync(
            NpcTradeRequestState.PendingRequestPath,
            new JsonObject
            {
                [NpcTradeRequestState.RequestsProperty] = new JsonArray(
                    new JsonObject
                    {
                        ["requestId"] = TradeRequestId,
                        ["npcId"] = ExistingNpcId,
                        ["npcName"] = "Маршрутный торговец",
                        ["merchantProfile"] = "GeneralGoods",
                        ["tradeCycleId"] = "trade_cycle_route_42",
                        ["derivedTradeSlotCount"] = 1,
                        ["createdAtTurn"] = RouteTurn,
                        ["createdAtUtc"] = "2026-08-11T00:00:00Z",
                        ["createdAtWorldDate"] = 1,
                        ["refreshAfterWorldDate"] = 2
                    })
            });

    private async Task WriteStorageBaselineAsync()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            RouteLocationId,
            "Маршрутная локация");
        location["locationStorages"] = new JsonArray(
            MortalLocationTestFixture.CreateStorageMetadata(
                RouteStorageId,
                "Маршрутное хранилище",
                hasFullAccess: true,
                contents: new JsonArray()));
        location["materialization"]!["sections"]!["storageMetadata"] =
            new JsonObject
            {
                ["disposition"] = "populated",
                ["reason"] = null
        };
        MortalLocationTestFixture.ResealCanonicalLocation(location);
        var mapLocation = location.DeepClone().AsObject();
        foreach (var storage in mapLocation["locationStorages"]!
                     .AsArray()
                     .OfType<JsonObject>())
        {
            storage.Remove("contents");
        }
        await WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(mapLocation));
        await WriteJsonAsync(
            StorageTransportMoveService.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(location));
        await WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationTestFixture.CreateIdentityIndex(location));
    }

    private static string ResolveRouteAuthorityId(string route, JsonObject item) =>
        route switch
        {
            "player_acquisition" => $"turn_{RouteTurn}",
            "npc_acquisition" => $"npc_inventory_add:{RouteTurn}:0:{ExistingNpcId}",
            "new_npc_inventory" => NewNpcInitialId,
            "loot_acquisition" =>
                $"loot_template:{RouteTurn}:0:{item["name"]!.GetValue<string>()}",
            "craft_output" => CraftRequestId,
            "trade_output" => TradeRequestId,
            "quest_reward" => QuestRewardId,
            "storage_placement" => $"{RouteLocationId}:{RouteStorageId}",
            _ => throw new ArgumentOutOfRangeException(nameof(route), route, null)
        };

    private static int CountMortalItemReceipts(JsonNode? node)
    {
        if (node == null)
            return 0;

        return node switch
        {
            JsonObject obj =>
                (obj["itemId"] is JsonValue &&
                 obj[MortalItemMaterializationContract.ReceiptProperty] is JsonObject
                    ? 1
                    : 0) +
                obj.Sum(pair => CountMortalItemReceipts(pair.Value)),
            JsonArray array => array.Sum(CountMortalItemReceipts),
            _ => 0
        };
    }

    private static bool ForgeAuthority(
        JsonNode node,
        string creationRef,
        string forgedAuthorityId,
        ref int matches)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                if (string.Equals(
                        obj["creationRef"]?.GetValue<string>(),
                        creationRef,
                        StringComparison.Ordinal) &&
                    obj["materialization"]?["sourceAuthority"] is JsonObject authority)
                {
                    authority["authorityId"] = forgedAuthorityId;
                    matches++;
                    changed = true;
                }
                foreach (var pair in obj)
                {
                    if (pair.Value != null &&
                        !string.Equals(
                            pair.Key,
                            "tradeInventory",
                            StringComparison.Ordinal))
                    {
                        changed |= ForgeAuthority(
                            pair.Value,
                            creationRef,
                            forgedAuthorityId,
                            ref matches);
                    }
                }
                break;
            case JsonArray array:
                foreach (var child in array)
                {
                    if (child != null)
                        changed |= ForgeAuthority(
                            child,
                            creationRef,
                            forgedAuthorityId,
                            ref matches);
                }
                break;
        }

        return changed;
    }

    private static JsonArray CloneRouteArray(params JsonObject[] items) =>
        new(items.Select(item => (JsonNode?)item.DeepClone()).ToArray());
}

internal sealed record MortalItemRouteArrangement(
    string Route,
    string AuthorityKind,
    string AuthorityId,
    string CreationRef);

internal sealed record MortalItemRouteOutcome(
    IReadOnlyList<ValidationIssue> RawIssues,
    IReadOnlyList<ValidationIssue> PostSealIssues,
    int NewReceipts,
    int NewActiveIndexEntries)
{
    internal IEnumerable<ValidationIssue> Errors =>
        RawIssues.Concat(PostSealIssues)
            .Where(issue => issue.Severity == IssueSeverity.Error);
}
