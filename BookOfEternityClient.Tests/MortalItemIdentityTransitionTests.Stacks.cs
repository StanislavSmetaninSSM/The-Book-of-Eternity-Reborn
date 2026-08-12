using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalItemIdentityTransitionTests
{
    [Fact]
    public async Task ExecuteAsync_SplitCreatesDerivedReceiptAndConservesQuantity()
    {
        await using var context = await TransitionContext.CreateAsync();
        var parent = Assert.Single(await ArrangePlayerStacksAsync(context, (context.Item, 10)));
        var parentReceipt = parent["materializationReceipt"]!.DeepClone();

        var result = await ExecuteAsync(
            context.FileSystem,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Split,
                new[] { context.ItemId },
                PlayerCarrier(),
                PlayerCarrier(),
                Quantity: 3,
                Turn: 43,
                AuthorityKind: "inventory_split",
                AuthorityId: "inventory_split_43_itm_move"));

        Assert.True(result.Success, result.Message);
        Assert.Equal(context.ItemId, result.ItemId);
        Assert.False(string.IsNullOrWhiteSpace(result.DerivedItemId));
        Assert.NotEqual(context.ItemId, result.DerivedItemId);

        var items = (await context.ReadInventoryAsync())["items"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(2, items.Length);
        var remaining = Assert.Single(items, item => ItemId(item) == context.ItemId);
        var child = Assert.Single(items, item => ItemId(item) == result.DerivedItemId);
        Assert.Equal(7, remaining["count"]!.GetValue<int>());
        Assert.Equal(3, child["count"]!.GetValue<int>());
        Assert.Equal(10, items.Sum(item => item["count"]!.GetValue<int>()));
        Assert.True(JsonNode.DeepEquals(parentReceipt, remaining["materializationReceipt"]));

        var childReceipt = child["materializationReceipt"]!.AsObject();
        Assert.Equal("split_derived", childReceipt["instanceKind"]!.GetValue<string>());
        Assert.Equal(context.ItemId, Assert.Single(childReceipt["parentItemIds"]!.AsArray())!.GetValue<string>());
        Assert.Equal(result.DerivedItemId, childReceipt["itemId"]!.GetValue<string>());
        Assert.NotEqual(
            parentReceipt!["receiptId"]!.GetValue<string>(),
            childReceipt["receiptId"]!.GetValue<string>());
        using (var childDocument = JsonDocument.Parse(child.ToJsonString()))
        {
            Assert.Empty(MortalItemMaterializationContract.Validate(
                childDocument.RootElement,
                "split_child",
                MortalItemMaterializationPhase.CanonicalPostSeal));
        }

        var index = MortalItemIdentityState.Parse(await context.ReadAsync(MortalItemIdentityState.StatePath));
        Assert.Empty(index.Issues);
        var parentEntry = index.EntriesByItemId[context.ItemId];
        var childEntry = index.EntriesByItemId[result.DerivedItemId!];
        Assert.True(JsonNode.DeepEquals(
            parentEntry["originMaterializationIds"],
            childEntry["originMaterializationIds"]));
        Assert.True(JsonNode.DeepEquals(
            parentEntry["originCreationRefs"],
            childEntry["originCreationRefs"]));
        Assert.Equal(context.ItemId, Assert.Single(childEntry["parentItemIds"]!.AsArray())!.GetValue<string>());
        AssertTransition(parentEntry, "split", 10, 7);
        AssertTransition(childEntry, "split", 10, 3);
    }

    [Fact]
    public async Task ExecuteAsync_DistinctSameTurnSplitAuthoritiesRemainIndependentCommands()
    {
        await using var context = await TransitionContext.CreateAsync();
        await ArrangePlayerStacksAsync(context, (context.Item, 4));

        var first = await ExecuteAsync(
            context.FileSystem,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Split,
                new[] { context.ItemId },
                PlayerCarrier(),
                PlayerCarrier(),
                Quantity: 1,
                Turn: 43,
                AuthorityKind: "inventory_split",
                AuthorityId: "inventory_split:43:itm_move:command_a"));
        var second = await ExecuteAsync(
            context.FileSystem,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Split,
                new[] { context.ItemId },
                PlayerCarrier(),
                PlayerCarrier(),
                Quantity: 1,
                Turn: 43,
                AuthorityKind: "inventory_split",
                AuthorityId: "inventory_split:43:itm_move:command_b"));

        Assert.True(first.Success, first.Message);
        Assert.True(second.Success, second.Message);
        Assert.NotEqual(first.DerivedItemId, second.DerivedItemId);
        var items = (await context.ReadInventoryAsync())["items"]!.AsArray()
            .OfType<JsonObject>()
            .ToArray();
        Assert.Equal(3, items.Length);
        Assert.Equal(4, items.Sum(item => item["count"]!.GetValue<int>()));
        var source = Assert.Single(items, item => ItemId(item) == context.ItemId);
        Assert.Equal(2, source["count"]!.GetValue<int>());
    }

    [Fact]
    public async Task ExecuteAsync_SplitIndexWriteFailureRestoresExactBeforeImages()
    {
        await using var context = await TransitionContext.CreateAsync();
        await ArrangePlayerStacksAsync(context, (context.Item, 10));
        var beforeInventory = await context.ReadAsync(StorageTransportMoveService.InventoryPath);
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
                            MortalItemIdentityState.StatePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        injected = true;
                        throw new IOException("injected split index write failure");
                    }
                    return Task.CompletedTask;
                }
            });

        var result = await ExecuteAsync(
            faultingFs,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Split,
                new[] { context.ItemId },
                PlayerCarrier(),
                PlayerCarrier(),
                Quantity: 3,
                Turn: 43,
                AuthorityKind: "inventory_split",
                AuthorityId: "inventory_split_rollback"));

        Assert.True(injected);
        Assert.False(result.Success);
        Assert.Equal(beforeInventory, await context.ReadAsync(StorageTransportMoveService.InventoryPath));
        Assert.Equal(beforeIndex, await context.ReadAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    public async Task ExecuteAsync_UnrelatedQuantityMismatchRejectsSplitWithoutWriting()
    {
        var corrupted = MortalItemTestFixture.CreateCanonicalRoot("itm_quantity_corrupted");
        var selected = MortalItemTestFixture.CreateCanonicalRoot("itm_quantity_selected");
        await using var context = await TransitionContext.CreateAsync();
        await ArrangePlayerStacksAsync(
            context,
            (corrupted, 2),
            (selected, 2));
        var inventory = await context.ReadInventoryAsync();
        var corruptedCarrier = Assert.Single(
            inventory["items"]!.AsArray().OfType<JsonObject>(),
            item => ItemId(item) == ItemId(corrupted));
        corruptedCarrier["count"] = 3;
        await context.WriteAsync(StorageTransportMoveService.InventoryPath, inventory);
        var beforeInventory = await context.ReadAsync(StorageTransportMoveService.InventoryPath);
        var beforeIndex = await context.ReadAsync(MortalItemIdentityState.StatePath);

        var result = await ExecuteAsync(
            context.FileSystem,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Split,
                new[] { ItemId(selected) },
                PlayerCarrier(),
                PlayerCarrier(),
                Quantity: 1,
                Turn: 43,
                AuthorityKind: "inventory_split",
                AuthorityId: "inventory_split_unrelated_quantity_mismatch"));

        Assert.False(result.Success);
        Assert.Equal(beforeInventory, await context.ReadAsync(StorageTransportMoveService.InventoryPath));
        Assert.Equal(beforeIndex, await context.ReadAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    public async Task ExecuteAsync_MergeKeepsSelectedReceiptAndRetiresContributors()
    {
        var contributor = MortalItemTestFixture.CreateCanonicalRoot("itm_merge_a");
        var selected = MortalItemTestFixture.CreateCanonicalRoot("itm_merge_z");
        await using var context = await TransitionContext.CreateAsync();
        var arranged = await ArrangePlayerStacksAsync(
            context,
            (contributor, 4),
            (selected, 6));
        var selectedReceipt = arranged[1]["materializationReceipt"]!.DeepClone();
        var expectedOrigins = arranged
            .Select(item => item["materialization"]!["materializationId"]!.GetValue<string>())
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var result = await ExecuteAsync(
            context.FileSystem,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Merge,
                new[] { selected["itemId"]!.GetValue<string>(), contributor["itemId"]!.GetValue<string>() },
                PlayerCarrier(),
                PlayerCarrier(),
                Quantity: 10,
                Turn: 43,
                AuthorityKind: "inventory_merge",
                AuthorityId: "inventory_merge_43_itm_merge_z",
                SurvivorItemId: selected["itemId"]!.GetValue<string>()));

        Assert.True(result.Success, result.Message);
        Assert.Equal(selected["itemId"]!.GetValue<string>(), result.ItemId);
        Assert.Null(result.DerivedItemId);
        var survivor = Assert.Single((await context.ReadInventoryAsync())["items"]!.AsArray())!.AsObject();
        Assert.Equal(selected["itemId"]!.GetValue<string>(), ItemId(survivor));
        Assert.Equal(10, survivor["count"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(selectedReceipt, survivor["materializationReceipt"]));

        var index = MortalItemIdentityState.Parse(await context.ReadAsync(MortalItemIdentityState.StatePath));
        Assert.Empty(index.Issues);
        var survivorEntry = index.EntriesByItemId[ItemId(selected)];
        Assert.Equal(expectedOrigins, survivorEntry["originMaterializationIds"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .ToArray());
        Assert.Equal(2, survivorEntry["originCreationRefs"]!.AsArray().Count);
        AssertTransition(survivorEntry, "merge", 6, 10);

        var contributorEntry = index.EntriesByItemId[ItemId(contributor)];
        Assert.Equal("merged", contributorEntry["state"]!.GetValue<string>());
        Assert.Null(contributorEntry["currentCarrier"]);
        Assert.Equal(ItemId(selected), contributorEntry["mergedIntoItemId"]!.GetValue<string>());
        AssertTransition(contributorEntry, "merge", 4, 0);
    }

    [Fact]
    public async Task ExecuteAsync_MergeSemanticMismatchFailsWithoutWriting()
    {
        var selected = MortalItemTestFixture.CreateCanonicalRoot("itm_merge_selected");
        var incompatible = MortalItemTestFixture.CreateCanonicalRoot("itm_merge_incompatible");
        incompatible["description"] = "Другой управляемый смысл предмета.";
        await using var context = await TransitionContext.CreateAsync();
        await ArrangePlayerStacksAsync(context, (selected, 2), (incompatible, 3));
        var beforeInventory = await context.ReadAsync(StorageTransportMoveService.InventoryPath);
        var beforeIndex = await context.ReadAsync(MortalItemIdentityState.StatePath);

        var result = await ExecuteAsync(
            context.FileSystem,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Merge,
                new[] { ItemId(selected), ItemId(incompatible) },
                PlayerCarrier(),
                PlayerCarrier(),
                Quantity: 5,
                Turn: 43,
                AuthorityKind: "inventory_merge",
                AuthorityId: "inventory_merge_mismatch",
                SurvivorItemId: ItemId(selected)));

        Assert.False(result.Success);
        Assert.Equal(beforeInventory, await context.ReadAsync(StorageTransportMoveService.InventoryPath));
        Assert.Equal(beforeIndex, await context.ReadAsync(MortalItemIdentityState.StatePath));
    }

    [Fact]
    public async Task ExecuteAsync_DestroyRetiresIdentityAndClearsEquipment()
    {
        await using var context = await TransitionContext.CreateAsync();
        await ArrangePlayerStacksAsync(context, (context.Item, 2));
        var inventory = await context.ReadInventoryAsync();
        inventory["equippedItems"] = new JsonObject { ["mainHand"] = context.ItemId };
        await context.WriteAsync(StorageTransportMoveService.InventoryPath, inventory);

        var result = await ExecuteAsync(
            context.FileSystem,
            new MortalItemTransitionIntent(
                MortalItemTransitionKind.Destroy,
                new[] { context.ItemId },
                PlayerCarrier(),
                DestinationCarrier: null,
                Quantity: 2,
                Turn: 43,
                AuthorityKind: "inventory_discard",
                AuthorityId: "inventory_discard_43_itm_move"));

        Assert.True(result.Success, result.Message);
        var afterInventory = await context.ReadInventoryAsync();
        Assert.Empty(afterInventory["items"]!.AsArray());
        Assert.Null(afterInventory["equippedItems"]!["mainHand"]);
        var index = MortalItemIdentityState.Parse(await context.ReadAsync(MortalItemIdentityState.StatePath));
        var entry = index.EntriesByItemId[context.ItemId];
        Assert.Equal("destroyed", entry["state"]!.GetValue<string>());
        Assert.Null(entry["currentCarrier"]);
        Assert.Null(entry["mergedIntoItemId"]);
        AssertTransition(entry, "destroy", 2, 0);
    }

    [Fact]
    public void ValidateAgainst_RejectsSplitWhoseBeforeQuantityBreaksHistory()
    {
        var (before, current, entry, carrier) = ArrangeQuantityHistory(quantity: 10);
        MortalItemIdentityState.AppendTransition(
            entry,
            MortalItemIdentityState.CreateTransition(
                "split",
                turn: 43,
                new[] { MortalItemTestFixture.ItemId },
                carrier,
                carrier,
                quantityBefore: 9,
                quantityAfter: 7,
                authorityKind: "inventory_split",
                authorityId: "inventory_split_invalid_history"));

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_quantity_transition_mismatch");
    }

    [Fact]
    public void ValidateAgainst_RejectsMergeRetirementWhoseBeforeQuantityBreaksHistory()
    {
        var (before, current, entry, carrier) = ArrangeQuantityHistory(quantity: 4);
        entry["state"] = "merged";
        entry["currentCarrier"] = null;
        entry["mergedIntoItemId"] = "itm_merge_survivor";
        MortalItemIdentityState.AppendTransition(
            entry,
            MortalItemIdentityState.CreateTransition(
                "merge",
                turn: 43,
                new[] { MortalItemTestFixture.ItemId, "itm_merge_survivor" },
                carrier,
                destinationCarrier: null,
                quantityBefore: 3,
                quantityAfter: 0,
                authorityKind: "inventory_merge",
                authorityId: "inventory_merge_invalid_history"));

        var issues = MortalItemIdentityState.ValidateAgainst(
            MortalItemIdentityState.Parse(before),
            MortalItemIdentityState.Parse(current));

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_item_identity_quantity_transition_mismatch");
    }

    private static async Task<MortalItemTransitionResult> ExecuteAsync(
        FileSystemManager fs,
        MortalItemTransitionIntent intent)
    {
        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        return await new MortalItemTransitionWriter(fs).ExecuteAsync(writeLease, intent);
    }

    private static async Task<JsonObject[]> ArrangePlayerStacksAsync(
        TransitionContext context,
        params (JsonObject Item, int Count)[] stacks)
    {
        var canonical = stacks.Select(stack =>
        {
            var item = stack.Item.DeepClone().AsObject();
            item["count"] = stack.Count;
            return item;
        }).ToArray();
        await context.WriteAsync(
            StorageTransportMoveService.InventoryPath,
            new JsonObject
            {
                ["items"] = new JsonArray(canonical.Select(item => (JsonNode?)item.DeepClone()).ToArray()),
                ["equippedItems"] = new JsonObject()
            });
        await context.WriteAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(canonical));
        return canonical;
    }

    private static (JsonObject Before, JsonObject Current, JsonObject Entry, JsonObject Carrier)
        ArrangeQuantityHistory(int quantity)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        item["count"] = quantity;
        var before = MortalItemTestFixture.CreateIndex(item);
        var current = before.DeepClone().AsObject();
        var entry = current["entries"]![0]!.AsObject();
        var carrier = entry["currentCarrier"]!.DeepClone().AsObject();
        return (before, current, entry, carrier);
    }

    private static string ItemId(JsonObject item) => item["itemId"]!.GetValue<string>();

    private static void AssertTransition(
        JsonObject entry,
        string kind,
        int quantityBefore,
        int quantityAfter)
    {
        var transition = entry["transitions"]!.AsArray()[^1]!.AsObject();
        Assert.Equal(kind, transition["kind"]!.GetValue<string>());
        Assert.Equal(quantityBefore, transition["quantityBefore"]!.GetValue<int>());
        Assert.Equal(quantityAfter, transition["quantityAfter"]!.GetValue<int>());
    }
}
