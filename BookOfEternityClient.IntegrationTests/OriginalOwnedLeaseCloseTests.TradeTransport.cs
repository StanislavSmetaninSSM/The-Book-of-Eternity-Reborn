using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    [Theory]
    [InlineData("npc_buy", false)] [InlineData("npc_buy", true)]
    [InlineData("npc_sell", false)] [InlineData("npc_sell", true)]
    [InlineData("npc_buyback", false)] [InlineData("npc_buyback", true)]
    [InlineData("storage_deposit", false)] [InlineData("storage_deposit", true)]
    [InlineData("storage_retrieve", false)] [InlineData("storage_retrieve", true)]
    [InlineData("vehicle_deposit", false)] [InlineData("vehicle_deposit", true)]
    [InlineData("vehicle_retrieve", false)] [InlineData("vehicle_retrieve", true)]
    public async Task OriginalTradeTransportOwnersRetainGenuinePublicationUncertaintyOnClose(string mode, bool uncertain)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-original-trade-transport-close-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launchArmed = false; var acquisitionPauses = 0; var publicationPauses = 0;
        string? target = null;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (launchArmed && phase == TrustedLocalPublicationPhase.IntentPublished && publicationPauses == 0)
                    {
                        using var actual = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
                        if (actual.RootElement.GetProperty("Members").EnumerateArray().Any(m => m.GetProperty("Path").GetString() == target))
                        { publicationPauses++; entered.TrySetResult(); allow.Task.GetAwaiter().GetResult(); }
                    }
                    cut.Hooks.LocalPublicationObserver!(phase, index);
                },
                BeforeCanonicalWriteLockOpenAsync = async () =>
                {
                    await cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                    if (launchArmed && acquisitionPauses == 0)
                    { acquisitionPauses++; acquireEntered.TrySetResult(); await allowAcquire.Task; }
                },
                BeforeCanonicalMutationBoundaryAsync = cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
                AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
                LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
                SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync
            });
        cut.Attach(files); files.EnsureDirectoryStructure();
        await using (var seed = await files.AcquireCanonicalWriteLeaseAsync()) files.GetOrCreateSessionGeneration(seed);
        var (beforeIndex, itemId, sourcePath, buybackId, moneyBefore) = await SeedOriginalCloseTradeTransportAsync(files, mode);
        target = files.ResolvePath(sourcePath); cut.Select = (path, _) => path == target;
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        Dictionary<string, byte[]> ReadCanonicalFiles() => Directory.EnumerateFiles(Path.Combine(files.GameSessionPath, "game_state"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        var beforeFiles = ReadCanonicalFiles();
        var closeFailure = new IOException("Actual trade/transport original owner late close.");
        var closer = new ThrowingClose(closeFailure); FileSystemManager.CanonicalWriteLease? original = null;
        var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null && Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        var boundary = mode switch
        {
            "npc_buy" => "NpcTradeService+<BuyCoreAsync>",
            "npc_sell" => "NpcTradeService+<SellCoreAsync>",
            "npc_buyback" => "NpcTradeService+<BuyBackCoreAsync>",
            "storage_deposit" or "storage_retrieve" => "StorageTransportMoveService+<MoveStorageItemCoreAsync>",
            _ => "StorageTransportMoveService+<MoveVehicleItemCoreAsync>"
        };
        var trader = new NpcTradeService(files, NullLogger<NpcTradeService>.Instance);
        Task? operation = null; Exception? failure = null; object? result = null; string? actualOwnerState = null;
        try
        {
            launchArmed = true;
            operation = mode switch
            {
                "npc_buy" => trader.BuyAsync("npc_merchant", "slot_1", 7),
                "npc_sell" => trader.SellAsync("npc_merchant", itemId, 7),
                "npc_buyback" => trader.BuyBackAsync("npc_merchant", buybackId!, 7),
                "storage_deposit" => StorageTransportMoveService.MoveStorageItemAsync(files, "deposit", "id:storage_1", "id:" + itemId),
                "storage_retrieve" => StorageTransportMoveService.MoveStorageItemAsync(files, "retrieve", "id:storage_1", "id:" + itemId),
                "vehicle_deposit" => StorageTransportMoveService.MoveVehicleItemAsync(files, "deposit", "id:wagon_1", "id:" + itemId),
                _ => StorageTransportMoveService.MoveVehicleItemAsync(files, "retrieve", "id:wagon_1", "id:" + itemId)
            };
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            (original, actualOwnerState) = InspectOriginalNestedOwningLease(operation, boundary);
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe; cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
            if (failure == null) result = mode.StartsWith("npc_", StringComparison.Ordinal)
                ? await Assert.IsAssignableFrom<Task<NpcTradeService.NpcTradeOperationResult>>(operation)
                : (object)await Assert.IsAssignableFrom<Task<StorageTransportMoveOutcome>>(operation);
        }
        finally
        {
            allowAcquire.TrySetResult(); allow.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(() => operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
        }
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var targetAfter = CleanupPublicationCut.ReadOptional(target);
        var generationAfter = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        var afterFiles = ReadCanonicalFiles();
        output.WriteLine(JsonSerializer.Serialize(new { mode, uncertain, root, boundary, actualOwnerState, acquisitionPauses, publicationPauses,
            attachments, closer.Calls, result, failure = failure?.ToString(), samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure), lockAvailable,
            activeAfter = original!.IsActive, ambientClosed = original.AmbientRegistration == null,
            mainClosed = original.MainAdmission == null, contextClosed = original.ExternalPublicationContext == null,
            generationBefore, generationAfter, targetAfter, beforeFiles, afterFiles, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext);
        Assert.Equal(generationBefore, generationAfter);

        if (uncertain)
        {
            Assert.Null(result); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls); Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]); cut.AssertReachedAndStopped(); Assert.Equal(0, cut.ClosingLeases);
            Assert.Equal(beforeFiles.Keys.Order(StringComparer.Ordinal), afterFiles.Keys.Order(StringComparer.Ordinal));
            foreach (var path in beforeFiles.Keys.Where(path => path != target)) Assert.Equal(beforeFiles[path], afterFiles[path]);
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls); Assert.False(File.Exists(cut.JournalPath));
            if (mode.StartsWith("npc_", StringComparison.Ordinal))
            {
                var actual = Assert.IsType<NpcTradeService.NpcTradeOperationResult>(result);
                Assert.True(actual.Success, actual.Message); Assert.True(actual.StateChanged); Assert.Contains("Orbit herb", actual.Message, StringComparison.Ordinal);
            }
            else
            {
                var actual = Assert.IsType<StorageTransportMoveOutcome>(result); Assert.True(actual.Success, actual.Message);
                Assert.Equal("Orbit herb", actual.ItemName); Assert.Equal(mode.StartsWith("storage_", StringComparison.Ordinal) ? "Cedar chest" : "Old wagon", actual.TargetName);
            }
            var index = MortalItemIdentityState.Parse(LocalSettingsPreparation.DecodeText(File.ReadAllBytes(files.ResolvePath(MortalItemIdentityState.StatePath))));
            Assert.Empty(index.Issues); Assert.Empty(MortalItemIdentityState.ValidateAgainst(beforeIndex, index));
            var selected = index.EntriesByItemId[itemId]; Assert.Equal("active", selected["state"]!.GetValue<string>());
            var transition = selected["transitions"]!.AsArray()[^1]!.AsObject(); Assert.Equal("transfer", transition["kind"]!.GetValue<string>());
            var expectedKind = mode == "npc_sell" ? "npc_inventory" : mode.EndsWith("deposit", StringComparison.Ordinal) ? (mode.StartsWith("storage_", StringComparison.Ordinal) ? "location_storage" : "vehicle_inventory") : "player_inventory";
            var expectedOwner = expectedKind == "npc_inventory" ? "npc_merchant" : expectedKind == "location_storage" ? "loc_market" : expectedKind == "vehicle_inventory" ? "wagon_1" : "player";
            Assert.Equal(expectedKind, selected["currentCarrier"]!["kind"]!.GetValue<string>());
            Assert.Equal(expectedOwner, selected["currentCarrier"]!["ownerId"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(selected["currentCarrier"], transition["destinationCarrier"]));
            Assert.Equal(mode.StartsWith("storage_", StringComparison.Ordinal) ? "local_storage_move" : mode.StartsWith("vehicle_", StringComparison.Ordinal) ? "local_vehicle_move" : mode == "npc_buy" ? "npc_trade_buy" : mode == "npc_sell" ? "npc_trade_sell" : "npc_trade_buyback", transition["authorityKind"]!.GetValue<string>());
            var inventory = JsonNode.Parse((await files.ReadFileAsync(InventoryEquipmentService.ItemsPath))!)!.AsObject();
            var playerHasItem = inventory["items"]!.AsArray().OfType<JsonObject>().Any(i => i["itemId"]!.GetValue<string>() == itemId);
            Assert.Equal(expectedKind == "player_inventory", playerHasItem);
            if (mode.StartsWith("npc_", StringComparison.Ordinal))
            {
                var npcRoot = JsonNode.Parse((await files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!.AsObject();
                var npc = Assert.Single(npcRoot["UpdateNPCs"]!.AsArray().OfType<JsonObject>());
                Assert.Equal(expectedKind == "npc_inventory", npc["inventory"]!.AsArray().OfType<JsonObject>().Any(i => i["itemId"]!.GetValue<string>() == itemId));
                var status = JsonNode.Parse((await files.ReadFileAsync("game_state/core/player_status.json"))!)!.AsObject();
                var amount = mode == "npc_buy" ? NpcTradeService.ComputeBuyPriceForValidation(20, 12, 14, "Neutral") : NpcTradeService.ComputeSellPriceForValidation(8, 12, 14, "Neutral");
                Assert.Equal(mode == "npc_sell" ? moneyBefore + amount : moneyBefore - amount, status["money"]!.GetValue<int>());
                if (mode == "npc_buy") Assert.True(npc["tradeInventory"]!["items"]![0]!["soldOut"]!.GetValue<bool>());
                else
                {
                    var entry = Assert.Single(npc["buybackInventory"]!.AsArray().OfType<JsonObject>(), e => e["itemId"]!.GetValue<string>() == itemId);
                    Assert.Equal(mode == "npc_sell" ? "available" : "rebought", entry["status"]!.GetValue<string>());
                    Assert.Equal(amount, entry["soldForPrice"]!.GetValue<int>());
                    if (mode == "npc_buyback") Assert.Equal(7, entry["reboughtAtTurn"]!.GetValue<int>());
                }
            }
            else
            {
                var carrierRoot = JsonNode.Parse((await files.ReadFileAsync(mode.StartsWith("storage_", StringComparison.Ordinal) ? StorageTransportMoveService.CurrentLocationPath : StorageTransportMoveService.VehiclesPath))!)!.AsObject();
                var array = mode.StartsWith("storage_", StringComparison.Ordinal) ? carrierRoot["locationStorages"]![0]!["contents"]!.AsArray() : carrierRoot["vehicles"]![0]!["inventory"]!.AsArray();
                Assert.Equal(mode.EndsWith("deposit", StringComparison.Ordinal), array.OfType<JsonObject>().Any(i => i["itemId"]!.GetValue<string>() == itemId));
            }
            var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
            Assert.Empty(await validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync());
        }
    }

    private static async Task<(MortalItemIdentityParseResult Index, string ItemId, string SourcePath, string? BuybackId, int MoneyBefore)> SeedOriginalCloseTradeTransportAsync(FileSystemManager files, string mode)
    {
        await files.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
        await files.WriteFileAtomicAsync("game_state/core/player_status.json", "{\"money\":500,\"trade\":12}");
        await files.WriteFileAtomicAsync("game_state/world/world_time.json", "{\"currentTimeInMinutes\":100}");
        JsonObject SealedItem(string id, bool stock = false)
        {
            var raw = MortalItemTestFixture.CreateRawRoot(route: stock ? "new_npc_inventory" : "player_acquisition", authorityKind: stock ? "new_npc" : "turn_outcome", authorityId: stock ? "npc_merchant" : "turn_5", sourceTurn: 5, creationRef: "new_" + id, materializationId: "mat_" + id);
            raw["name"] = "Orbit herb"; raw["description"] = "Supported original owner transfer item";
            raw["type"] = "Tool"; raw["quality"] = "Common"; raw["rarity"] = "Common"; raw["tradeItemClass"] = "Functional";
            raw["price"] = 20; raw["baseSellPrice"] = 8; raw["count"] = 1;
            var receipt = MortalItemIdentityState.CreateRootReceipt(raw, id, 5);
            raw["itemId"] = id; raw["existedId"] = id; raw.Remove("creationRef"); raw["materializationReceipt"] = receipt;
            return raw;
        }
        var item = SealedItem("itm_transfer"); var itemId = "itm_transfer";
        var isNpc = mode.StartsWith("npc_", StringComparison.Ordinal); var retrieves = mode.EndsWith("retrieve", StringComparison.Ordinal);
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity("loc_market", "Market square", discoveryTier: "visited");
        if (!isNpc)
        {
            location["locationStorages"] = new JsonArray(MortalLocationTestFixture.CreateStorageMetadata("storage_1", "Cedar chest", true));
            location["materialization"]!["sections"]!["storageMetadata"] = new JsonObject { ["disposition"] = "populated", ["reason"] = null };
            MortalLocationTestFixture.ResealCanonicalLocation(location);
        }
        var current = MortalLocationTestFixture.CreateCurrentProjection(location);
        if (!isNpc) current["locationStorages"]![0]!["contents"] = mode == "storage_retrieve" ? new JsonArray(item.DeepClone()) : new JsonArray();
        await files.WriteFileAtomicAsync(MortalLocationMaterializationContract.WorldMapPath, MortalLocationTestFixture.CreateWorldMap(location).ToJsonString());
        await files.WriteFileAtomicAsync(MortalLocationMaterializationContract.CurrentLocationPath, current.ToJsonString());
        await files.WriteFileAtomicAsync(MortalLocationIdentityState.StatePath, MortalLocationTestFixture.CreateIdentityIndex(location).ToJsonString());
        await files.WriteFileAtomicAsync(InventoryEquipmentService.ItemsPath, new JsonObject { ["items"] = retrieves ? new JsonArray() : new JsonArray(item.DeepClone()), ["equippedItems"] = new JsonObject() }.ToJsonString());
        var sourcePath = retrieves ? mode == "storage_retrieve" ? StorageTransportMoveService.CurrentLocationPath : StorageTransportMoveService.VehiclesPath : InventoryEquipmentService.ItemsPath;
        string? buybackId = null; var moneyBefore = 500;
        JsonObject indexRoot;
        if (isNpc)
        {
            var stock = Enumerable.Range(1, 7).Select(i => SealedItem("stock_" + i, true)).ToArray();
            var price = NpcTradeService.ComputeBuyPriceForValidation(20, 12, 14, "Neutral");
            var slots = new JsonArray(stock.Select((stockItem, i) => (JsonNode?)new JsonObject
            {
                ["slotId"] = "slot_" + (i + 1), ["itemId"] = stockItem["itemId"]!.DeepClone(), ["price"] = price, ["merchantProfile"] = "GeneralGoods", ["soldOut"] = false,
                ["itemData"] = new JsonObject { ["itemId"] = stockItem["itemId"]!.DeepClone(), ["name"] = "Orbit herb", ["description"] = "Supported original owner transfer item", ["type"] = "Tool", ["quality"] = "Common", ["rarity"] = "Common", ["tradeItemClass"] = "Functional", ["price"] = 20, ["baseSellPrice"] = 8 }
            }).ToArray());
            var npc = new JsonObject
            {
                ["npcId"] = "npc_merchant", ["name"] = "Merchant", ["currentLocationId"] = "loc_market", ["currentLocation"] = "Market square", ["level"] = 10, ["relationshipLevel"] = 80,
                ["characteristics"] = new JsonObject { ["modifiedTrade"] = 14 }, ["tradeState"] = new JsonObject { ["canTrade"] = true, ["merchantProfile"] = "GeneralGoods" },
                ["inventory"] = new JsonArray(stock.Select(i => (JsonNode?)i.DeepClone()).ToArray()),
                ["tradeInventory"] = new JsonObject { ["tradeCycleId"] = "world_trade_0", ["generatedAtWorldDate"] = 100, ["refreshAfterWorldDate"] = 43200, ["generationTradeTier"] = "Good", ["pricingTradeTier"] = "Neutral", ["items"] = slots },
                ["tradeInventoryReceipts"] = new JsonArray(new JsonObject { ["requestId"] = "npc_trade_seed", ["npcId"] = "npc_merchant", ["npcName"] = "Merchant", ["tradeCycleId"] = "world_trade_0", ["merchantProfile"] = "GeneralGoods", ["status"] = "ready", ["itemCount"] = 7, ["resolvedAtTurn"] = 5, ["resolvedAtUtc"] = "2026-10-09T00:00:00Z" })
            };
            await files.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, new JsonObject { ["UpdateNPCs"] = new JsonArray(npc) }.ToJsonString());
            indexRoot = MortalItemTestFixture.CreateIndexForCarriers(stock.Select(i => (i, "npc_inventory", "npc_merchant", (string?)null)).Append((item, "player_inventory", "player", null)).ToArray());
            if (mode == "npc_buy") { itemId = "stock_1"; sourcePath = NpcCoreChangesContract.NpcCorePath; }
        }
        else
        {
            await files.WriteFileAtomicAsync(StorageTransportMoveService.VehiclesPath, new JsonObject { ["vehicles"] = new JsonArray(new JsonObject { ["vehicleId"] = "wagon_1", ["name"] = "Old wagon", ["availability"] = "Active", ["inventory"] = mode == "vehicle_retrieve" ? new JsonArray(item.DeepClone()) : new JsonArray() }) }.ToJsonString());
            indexRoot = retrieves ? MortalItemTestFixture.CreateIndexForCarrier(item, mode == "storage_retrieve" ? "location_storage" : "vehicle_inventory", mode == "storage_retrieve" ? "loc_market" : "wagon_1", mode == "storage_retrieve" ? "storage_1" : null) : MortalItemTestFixture.CreateIndex(item);
        }
        await files.WriteFileAtomicAsync(MortalItemIdentityState.StatePath, indexRoot.ToJsonString());
        var resources = ResourceBootstrapStateBuilder.BuildPristine(); Assert.True(resources.IsValid);
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(resources.Definitions, files.ReadFileAsync, resources.State, resources.History, CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(authority.IsValid, string.Join(Environment.NewLine, authority.Issues));
        await files.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, authority.CanonicalAuthorityJson!);
        if (mode == "npc_buyback")
        {
            var sold = await new NpcTradeService(files, NullLogger<NpcTradeService>.Instance).SellAsync("npc_merchant", itemId, 6); Assert.True(sold.Success, sold.Message);
            var actualNpc = JsonNode.Parse((await files.ReadFileAsync(NpcCoreChangesContract.NpcCorePath))!)!["UpdateNPCs"]![0]!;
            buybackId = Assert.Single(actualNpc["buybackInventory"]!.AsArray().OfType<JsonObject>())["buybackEntryId"]!.GetValue<string>();
            moneyBefore = JsonNode.Parse((await files.ReadFileAsync("game_state/core/player_status.json"))!)!["money"]!.GetValue<int>();
            sourcePath = NpcCoreChangesContract.NpcCorePath;
        }
        var before = MortalItemIdentityState.Parse(await files.ReadFileAsync(MortalItemIdentityState.StatePath)); Assert.Empty(before.Issues);
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance); Assert.Empty(await validator.ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync());
        return (before, itemId, sourcePath, buybackId, moneyBefore);
    }
}
