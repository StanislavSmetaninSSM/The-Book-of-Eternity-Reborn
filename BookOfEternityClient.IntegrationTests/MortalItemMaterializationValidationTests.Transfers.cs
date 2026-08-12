using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalItemMaterializationValidationTests
{
    [Trait("Category", "FullValidation")]
    public sealed class Transfers
    {
        [Fact]
        public async Task AcceptedNpcToPlayerCommands_MoveExactItemAndPreserveCompanions()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var before = await context.ArrangeNpcToPlayerTransferAsync();
            var receipt = before["materializationReceipt"]!.DeepClone();

            var rawIssues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
            Assert.DoesNotContain(rawIssues, issue => issue.Severity == IssueSeverity.Error);
            await context.NormalizeAcceptedTurnAsync();

            var player = (await context.ReadJsonAsync(
                InventoryEquipmentService.ItemsPath))!.AsObject();
            var moved = Assert.Single(player["items"]!.AsArray().OfType<JsonObject>());
            Assert.Equal(MortalItemMaterializationTestContext.TransferItemId, moved["itemId"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(receipt, moved["materializationReceipt"]));
            var npc = (await context.ReadJsonAsync(
                NpcCoreChangesContract.NpcCorePath))!.AsObject();
            Assert.Empty(npc["NPCsInScene"]![0]!["inventory"]!.AsArray());
            Assert.Null(npc["NPCsInScene"]![0]!["equippedItems"]!["mainHand"]);
            var entry = await context.ReadTransferIndexEntryAsync();
            Assert.Equal("player_inventory", entry["currentCarrier"]!["kind"]!.GetValue<string>());
            Assert.Equal(2, entry["transitions"]!.AsArray().Count);
            var journals = await context.ReadJsonAsync("game_state/npcs/item_journals.json");
            Assert.True(MortalItemMaterializationTestContext.ContainsExactString(
                journals,
                MortalItemMaterializationTestContext.TransferItemId));
        }

        [Fact]
        public async Task AcceptedPlayerToNpcCommands_MoveExactItemWithoutRecreation()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var before = await context.ArrangePlayerToNpcTransferAsync();
            var receipt = before["materializationReceipt"]!.DeepClone();

            var rawIssues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
            Assert.DoesNotContain(rawIssues, issue => issue.Severity == IssueSeverity.Error);
            await context.NormalizeAcceptedTurnAsync();

            var player = (await context.ReadJsonAsync(
                InventoryEquipmentService.ItemsPath))!.AsObject();
            Assert.Empty(player["items"]!.AsArray());
            var npc = (await context.ReadJsonAsync(
                NpcCoreChangesContract.NpcCorePath))!.AsObject();
            var moved = Assert.Single(npc["NPCsInScene"]![0]!["inventory"]!.AsArray());
            Assert.True(JsonNode.DeepEquals(receipt, moved!["materializationReceipt"]));
            var entry = await context.ReadTransferIndexEntryAsync();
            Assert.Equal("npc_inventory", entry["currentCarrier"]!["kind"]!.GetValue<string>());
            Assert.Equal(MortalItemMaterializationTestContext.TransferNpcId, entry["currentCarrier"]!["ownerId"]!.GetValue<string>());
        }

        [Fact]
        public async Task RemovedExistingItem_RecreatedWithNullIdentity_IsRejected()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeNpcToPlayerTransferAsync(recreateWithNullIdentity: true);

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_transfer_recreation_forbidden");
        }

        [Fact]
        public async Task TransferPayload_CannotRewriteImmutableReceipt()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeNpcToPlayerTransferAsync();
            await context.ForgePlayerTransferReceiptAsync();

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_immutable_receipt_rewrite");
        }
    }
}
