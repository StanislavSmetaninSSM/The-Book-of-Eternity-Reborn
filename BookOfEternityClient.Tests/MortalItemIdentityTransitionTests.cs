using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalItemIdentityTransitionTests
{
    [Fact]
    public async Task ExecuteAsync_PlayerToStoragePreservesEvidenceAndMovesOneCarrier()
    {
        await using var context = await TransitionContext.CreateAsync();
        var receiptBefore = context.Item["materializationReceipt"]!.DeepClone();
        var envelopeBefore = context.Item["materialization"]!.DeepClone();

        var result = await context.TransferAsync(
            context.ItemId,
            PlayerCarrier(),
            Coordinate("location_storage", "loc_test", "storage_test"));

        Assert.True(result.Success, result.Message);
        var moved = Assert.Single((await context.ReadLocationAsync())["locationStorages"]![0]!["contents"]!.AsArray());
        Assert.Equal(context.ItemId, moved!["itemId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(receiptBefore, moved["materializationReceipt"]));
        Assert.True(JsonNode.DeepEquals(envelopeBefore, moved["materialization"]));
        Assert.Empty((await context.ReadInventoryAsync())["items"]!.AsArray());

        var index = MortalItemIdentityState.Parse(await context.ReadAsync(MortalItemIdentityState.StatePath));
        Assert.Empty(index.Issues);
        var entry = index.EntriesByItemId[context.ItemId];
        Assert.Equal("location_storage", entry["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.Equal(2, entry["transitions"]!.AsArray().Count);
    }

    [Fact]
    public async Task ExecuteAsync_ExactIdSelectsOneOfTwoSameNamedItems()
    {
        var other = MortalItemTestFixture.CreateCanonicalRoot("itm_same_name_other");
        await using var context = await TransitionContext.CreateAsync(other);

        var result = await context.TransferAsync(
            context.ItemId,
            PlayerCarrier(),
            Coordinate("vehicle_inventory", "vehicle_test"));

        Assert.True(result.Success, result.Message);
        var remaining = (await context.ReadInventoryAsync())["items"]!.AsArray();
        Assert.Equal("itm_same_name_other", Assert.Single(remaining)!["itemId"]!.GetValue<string>());
        var moved = Assert.Single((await context.ReadVehiclesAsync())["vehicles"]![0]!["inventory"]!.AsArray());
        Assert.Equal(context.ItemId, moved!["itemId"]!.GetValue<string>());
    }

    [Fact]
    public async Task ExecuteAsync_PlayerStorageVehicleRoundTripKeepsOneIdentity()
    {
        await using var context = await TransitionContext.CreateAsync();
        var player = PlayerCarrier();
        var storage = Coordinate("location_storage", "loc_test", "storage_test");
        var vehicle = Coordinate("vehicle_inventory", "vehicle_test");

        Assert.True((await context.TransferAsync(context.ItemId, player, storage)).Success);
        Assert.True((await context.TransferAsync(context.ItemId, storage, player)).Success);
        Assert.True((await context.TransferAsync(context.ItemId, player, vehicle)).Success);
        Assert.True((await context.TransferAsync(context.ItemId, vehicle, player)).Success);

        var item = Assert.Single((await context.ReadInventoryAsync())["items"]!.AsArray());
        Assert.Equal(context.ItemId, item!["itemId"]!.GetValue<string>());
        Assert.Empty((await context.ReadLocationAsync())["locationStorages"]![0]!["contents"]!.AsArray());
        Assert.Empty((await context.ReadVehiclesAsync())["vehicles"]![0]!["inventory"]!.AsArray());
        var index = MortalItemIdentityState.Parse(await context.ReadAsync(MortalItemIdentityState.StatePath));
        var entry = index.EntriesByItemId[context.ItemId];
        Assert.Equal("player_inventory", entry["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.Equal(5, entry["transitions"]!.AsArray().Count);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicatePreTurnCarrierFailsWithoutWriting()
    {
        await using var context = await TransitionContext.CreateAsync();
        var location = await context.ReadLocationAsync();
        location["locationStorages"]![0]!["contents"]!.AsArray().Add(context.Item.DeepClone());
        await context.WriteAsync(StorageTransportMoveService.CurrentLocationPath, location);
        var beforeInventory = await context.ReadAsync(StorageTransportMoveService.InventoryPath);
        var beforeLocation = await context.ReadAsync(StorageTransportMoveService.CurrentLocationPath);
        var beforeIndex = await context.ReadAsync(MortalItemIdentityState.StatePath);

        var result = await context.TransferAsync(
            context.ItemId,
            PlayerCarrier(),
            Coordinate("vehicle_inventory", "vehicle_test"));

        Assert.False(result.Success);
        Assert.Equal(beforeInventory, await context.ReadAsync(StorageTransportMoveService.InventoryPath));
        Assert.Equal(beforeLocation, await context.ReadAsync(StorageTransportMoveService.CurrentLocationPath));
        Assert.Equal(beforeIndex, await context.ReadAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    public async Task ExecuteAsync_RetiredIdentityCannotBeMovedOrReused()
    {
        await using var context = await TransitionContext.CreateAsync();
        var index = MortalItemIdentityState.Parse(await context.ReadAsync(MortalItemIdentityState.StatePath)).Root;
        var entry = index["entries"]![0]!.AsObject();
        entry["state"] = "destroyed";
        entry["currentCarrier"] = null;
        await context.WriteAsync(MortalItemIdentityState.StatePath, index);
        var beforeInventory = await context.ReadAsync(StorageTransportMoveService.InventoryPath);

        var result = await context.TransferAsync(
            context.ItemId,
            PlayerCarrier(),
            Coordinate("location_storage", "loc_test", "storage_test"));

        Assert.False(result.Success);
        Assert.Equal(beforeInventory, await context.ReadAsync(StorageTransportMoveService.InventoryPath));
    }

    [Fact]
    public async Task ExecuteAsync_SecondCarrierWriteFailureRestoresExactBeforeImages()
    {
        await using var context = await TransitionContext.CreateAsync();
        var beforeInventory = await context.ReadAsync(StorageTransportMoveService.InventoryPath);
        var beforeLocation = await context.ReadAsync(StorageTransportMoveService.CurrentLocationPath);
        var beforeIndex = await context.ReadAsync(MortalItemIdentityState.StatePath);
        var injected = false;
        var faultingFs = new FileSystemManager(
            context.RootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (!injected && string.Equals(
                            path,
                            StorageTransportMoveService.CurrentLocationPath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        injected = true;
                        throw new IOException("injected second carrier write failure");
                    }
                    return Task.CompletedTask;
                }
            });

        MortalItemTransitionResult result;
        await using (var writeLease = await faultingFs.AcquireCanonicalWriteLeaseAsync())
        {
            result = await new MortalItemTransitionWriter(faultingFs).ExecuteAsync(
                writeLease,
                new MortalItemTransitionIntent(
                    MortalItemTransitionKind.Transfer,
                    new[] { context.ItemId },
                    PlayerCarrier(),
                    Coordinate("location_storage", "loc_test", "storage_test"),
                    Quantity: 1,
                    Turn: 43,
                    AuthorityKind: "storage_move",
                    AuthorityId: "storage_move_rollback"));
        }

        Assert.True(injected);
        Assert.False(result.Success);
        Assert.Equal(beforeInventory, await context.ReadAsync(StorageTransportMoveService.InventoryPath));
        Assert.Equal(beforeLocation, await context.ReadAsync(StorageTransportMoveService.CurrentLocationPath));
        Assert.Equal(beforeIndex, await context.ReadAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    public void ValidateAgainst_TransferPreservesExactIdentityAndQuantity()
    {
        var (before, current, entry, source, destination) = ArrangeTransfer();
        MortalItemIdentityState.AppendTransition(
            entry,
            CreateTransfer(source, destination, new[] { MortalItemTestFixture.ItemId }, 1, 1));

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateAgainst_RejectsTransferWhoseSourceIdentityIsNotTheMovedItem()
    {
        var (before, current, entry, source, destination) = ArrangeTransfer();
        MortalItemIdentityState.AppendTransition(
            entry,
            CreateTransfer(source, destination, new[] { "itm_other" }, 1, 1));

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_transfer_transition_mismatch");
    }

    [Fact]
    public void ValidateAgainst_RejectsTransferThatChangesQuantity()
    {
        var (before, current, entry, source, destination) = ArrangeTransfer();
        MortalItemIdentityState.AppendTransition(
            entry,
            CreateTransfer(source, destination, new[] { MortalItemTestFixture.ItemId }, 1, 2));

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_transfer_transition_mismatch");
    }

    [Fact]
    public void ValidateAgainst_RejectsTransferWhoseBeforeQuantityBreaksHistory()
    {
        var (before, current, entry, source, destination) = ArrangeTransfer();
        MortalItemIdentityState.AppendTransition(
            entry,
            CreateTransfer(source, destination, new[] { MortalItemTestFixture.ItemId }, 2, 2));

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_transfer_transition_mismatch");
    }

    private static (
        JsonObject Before,
        JsonObject Current,
        JsonObject Entry,
        JsonObject Source,
        JsonObject Destination) ArrangeTransfer()
    {
        var before = MortalItemTestFixture.CreateIndex(
            MortalItemTestFixture.CreateCanonicalRoot());
        var current = before.DeepClone().AsObject();
        var entry = current["entries"]![0]!.AsObject();
        var source = entry["currentCarrier"]!.DeepClone().AsObject();
        var destination = Carrier("location_storage", "loc_test", "storage_test");
        entry["currentCarrier"] = destination.DeepClone();
        return (before, current, entry, source, destination);
    }

    private static JsonObject CreateTransfer(
        JsonObject source,
        JsonObject destination,
        IEnumerable<string> sourceItemIds,
        int quantityBefore,
        int quantityAfter) =>
        MortalItemIdentityState.CreateTransition(
            "transfer",
            turn: 43,
            sourceItemIds,
            source,
            destination,
            quantityBefore,
            quantityAfter,
            authorityKind: "storage_move",
            authorityId: "storage_move_43");

    private static JsonObject Carrier(string kind, string ownerId, string? containerId = null) =>
        new()
        {
            ["kind"] = kind,
            ["ownerId"] = ownerId,
            ["containerId"] = containerId,
            ["containerPath"] = new JsonArray()
        };

    private static MortalItemCarrierCoordinate PlayerCarrier() =>
        new("player_inventory", "player", null, Array.Empty<string>());

    private static MortalItemCarrierCoordinate Coordinate(
        string kind,
        string ownerId,
        string? containerId = null) =>
        new(kind, ownerId, containerId, Array.Empty<string>());

    private sealed class TransitionContext : IAsyncDisposable
    {
        private readonly string _rootPath;

        private TransitionContext(string rootPath, FileSystemManager fs, JsonObject item)
        {
            _rootPath = rootPath;
            FileSystem = fs;
            Item = item;
        }

        internal FileSystemManager FileSystem { get; }
        internal string RootPath => _rootPath;
        internal JsonObject Item { get; }
        internal string ItemId => Item["itemId"]!.GetValue<string>();

        internal static async Task<TransitionContext> CreateAsync(params JsonObject[] additionalItems)
        {
            var rootPath = Path.Combine(
                Path.GetTempPath(),
                "boe-mortal-item-transition-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rootPath);
            var fs = new FileSystemManager(rootPath, NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            var item = MortalItemTestFixture.CreateCanonicalRoot("itm_move");
            var allItems = new[] { item }.Concat(additionalItems).ToArray();
            var context = new TransitionContext(rootPath, fs, item);
            await context.WriteAsync(
                StorageTransportMoveService.InventoryPath,
                new JsonObject
                {
                    ["items"] = new JsonArray(allItems.Select(value => (JsonNode?)value.DeepClone()).ToArray()),
                    ["equippedItems"] = new JsonObject()
                });
            await context.WriteAsync(
                StorageTransportMoveService.CurrentLocationPath,
                new JsonObject
                {
                    ["locationId"] = "loc_test",
                    ["locationStorages"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["storageId"] = "storage_test",
                            ["name"] = "Тестовое хранилище",
                            ["contents"] = new JsonArray()
                        }
                    }
                });
            await context.WriteAsync(
                StorageTransportMoveService.VehiclesPath,
                new JsonObject
                {
                    ["vehicles"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["vehicleId"] = "vehicle_test",
                            ["name"] = "Тестовый транспорт",
                            ["inventory"] = new JsonArray()
                        }
                    }
                });
            await context.WriteAsync(
                NpcCoreChangesContract.NpcCorePath,
                new JsonObject { ["NPCsInScene"] = new JsonArray() });
            await context.WriteAsync(
                MortalItemIdentityState.StatePath,
                MortalItemTestFixture.CreateIndex(allItems));
            return context;
        }

        internal async Task<MortalItemTransitionResult> TransferAsync(
            string itemId,
            MortalItemCarrierCoordinate source,
            MortalItemCarrierCoordinate destination)
        {
            await using var writeLease = await FileSystem.AcquireCanonicalWriteLeaseAsync();
            return await new MortalItemTransitionWriter(FileSystem).ExecuteAsync(
                writeLease,
                new MortalItemTransitionIntent(
                    MortalItemTransitionKind.Transfer,
                    new[] { itemId },
                    source,
                    destination,
                    Quantity: 1,
                    Turn: 43,
                    AuthorityKind: "storage_move",
                    AuthorityId: "storage_move_43"));
        }

        internal async Task<JsonObject> ReadInventoryAsync() =>
            JsonNode.Parse(await ReadAsync(StorageTransportMoveService.InventoryPath))!.AsObject();

        internal async Task<JsonObject> ReadLocationAsync() =>
            JsonNode.Parse(await ReadAsync(StorageTransportMoveService.CurrentLocationPath))!.AsObject();

        internal async Task<JsonObject> ReadVehiclesAsync() =>
            JsonNode.Parse(await ReadAsync(StorageTransportMoveService.VehiclesPath))!.AsObject();

        internal async Task<string> ReadAsync(string path) =>
            await FileSystem.ReadFileAsync(path) ?? throw new InvalidOperationException($"Missing {path}.");

        internal async Task WriteAsync(string path, JsonObject root) =>
            await FileSystem.WriteFileAtomicAsync(
                path,
                root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        public ValueTask DisposeAsync()
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
            return ValueTask.CompletedTask;
        }
    }
}
