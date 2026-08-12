using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalItemMaterializationValidationTests
{
    [Trait("Category", "FullValidation")]
    public sealed class Routes
    {
        [Theory]
        [InlineData("player_acquisition", "turn_outcome")]
        [InlineData("npc_acquisition", "npc_inventory_add")]
        [InlineData("new_npc_inventory", "new_npc")]
        [InlineData("loot_acquisition", "loot_template")]
        [InlineData("craft_output", "craft_request")]
        [InlineData("trade_output", "npc_trade_receipt")]
        [InlineData("quest_reward", "quest_reward")]
        [InlineData("storage_placement", "location_storage")]
        public async Task CompleteRoute_SealsExactlyOneItem(
            string route,
            string authorityKind)
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                route,
                authorityKind,
                MortalItemTestFixture.CreateRawRoot(route, authorityKind));

            var authority = await MortalItemRouteAuthorityCatalog.BuildAsync(
                context.FileSystem);
            var bound = authority.ByCreationRef[arrangement.CreationRef];
            Assert.Equal(route, bound.Route);
            Assert.Equal(authorityKind, bound.AuthorityKind);
            Assert.Equal(arrangement.AuthorityId, bound.AuthorityId);

            var outcome = await context.ValidateNormalizeAndValidateAsync();

            Assert.Empty(outcome.Errors);
            Assert.Equal(1, outcome.NewReceipts);
            Assert.Equal(1, outcome.NewActiveIndexEntries);
        }

        [Theory]
        [InlineData("player_acquisition", "turn_outcome")]
        [InlineData("npc_acquisition", "npc_inventory_add")]
        [InlineData("new_npc_inventory", "new_npc")]
        [InlineData("loot_acquisition", "loot_template")]
        [InlineData("craft_output", "craft_request")]
        [InlineData("trade_output", "npc_trade_receipt")]
        [InlineData("quest_reward", "quest_reward")]
        [InlineData("storage_placement", "location_storage")]
        public async Task IncompleteRoute_RestoresEveryTrackedFileByteForByte(
            string route,
            string authorityKind)
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var item = MortalItemTestFixture.CreateRawRoot(route, authorityKind);
            item.Remove("description");
            await context.ArrangeRouteAsync(route, authorityKind, item);
            var before = await context.CaptureExactBytesAsync(
                CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => context.NormalizeAcceptedTurnAsync());

            await context.AssertExactBytesAsync(before);
        }

        [Fact]
        public async Task IncompleteCraftRoute_IssueCarriesExactRepairContext()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var item = MortalItemTestFixture.CreateRawRoot(
                "craft_output",
                "craft_request");
            item.Remove("description");
            var arrangement = await context.ArrangeRouteAsync(
                "craft_output",
                "craft_request",
                item);

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            var issue = Assert.Single(issues, candidate =>
                candidate.Code == "mortal_item_materialization_complete_field_missing" &&
                candidate.FilePath.EndsWith(".description", StringComparison.Ordinal));
            var repair = Assert.IsType<MortalItemRepairContext>(
                issue.MortalItemRepairContext);
            Assert.Equal(
                $"mortal_item:new:{arrangement.CreationRef}",
                repair.Coordinate);
            Assert.Equal("create", repair.TransitionClass);
            Assert.Equal("craft_output", repair.Route);
            Assert.Null(repair.SourceCarrier);
            Assert.Equal(
                new MortalItemCarrierCoordinate(
                    "player_inventory",
                    "player",
                    null,
                    Array.Empty<string>()),
                repair.DestinationCarrier);
            Assert.Equal(
                $"craft_request:{arrangement.AuthorityId}",
                repair.ExpectedAuthority);
        }

        [Theory]
        [InlineData("player_acquisition", "turn_outcome")]
        [InlineData("npc_acquisition", "npc_inventory_add")]
        [InlineData("new_npc_inventory", "new_npc")]
        [InlineData("loot_acquisition", "loot_template")]
        [InlineData("craft_output", "craft_request")]
        [InlineData("trade_output", "npc_trade_receipt")]
        [InlineData("quest_reward", "quest_reward")]
        [InlineData("storage_placement", "location_storage")]
        public async Task ForgedRouteAuthority_IsRejectedBeforeSealing(
            string route,
            string authorityKind)
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                route,
                authorityKind,
                MortalItemTestFixture.CreateRawRoot(route, authorityKind));
            await context.ForgeRawRouteAuthorityIdAsync(
                arrangement.CreationRef,
                "forged_route_authority");

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Severity == IssueSeverity.Error &&
                issue.Code == "mortal_item_materialization_route_authority_mismatch" &&
                issue.Actor == $"mortal_item:new:{arrangement.CreationRef}");
        }

        [Fact]
        public async Task PlayerCarrier_DirectCanonicalInsertionIsRejectedBeforeSealing()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeEmptyMortalTurnAsync();
            var root = (await context.ReadJsonAsync(
                InventoryEquipmentService.ItemsPath))!.AsObject();
            var rawItem = MortalItemTestFixture.CreateRawRoot();
            root["items"]!.AsArray().Add(rawItem);
            await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath, root);

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_route_authority_mismatch" &&
                issue.Actor ==
                $"mortal_item:new:{rawItem["creationRef"]!.GetValue<string>()}");
        }

        [Fact]
        public async Task QuestRewardRoute_BindsDetailToAcceptedCreateTransition()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeRouteAsync(
                "quest_reward",
                "quest_reward",
                MortalItemTestFixture.CreateRawRoot(
                    "quest_reward",
                    "quest_reward"));
            await context.NormalizeAcceptedTurnAsync();

            var issues = await context.Validator.ValidateGameStateAsync(
                IntegrationValidationProfiles.QuestReward);

            Assert.DoesNotContain(issues, issue =>
                issue.Code ==
                "mortal_item_materialization_quest_reward_authority_mismatch");
        }

        [Fact]
        public async Task QuestRewardRoute_WithForgedCreateTransition_IsRejected()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeRouteAsync(
                "quest_reward",
                "quest_reward",
                MortalItemTestFixture.CreateRawRoot(
                    "quest_reward",
                    "quest_reward"));
            await context.NormalizeAcceptedTurnAsync();
            await context.ForgeAcceptedCreateTransitionAuthorityAsync(
                "forged_quest_reward");

            var issues = await context.Validator.ValidateGameStateAsync(
                IntegrationValidationProfiles.QuestReward);

            Assert.Contains(issues, issue =>
                issue.Code ==
                "mortal_item_materialization_quest_reward_authority_mismatch");
        }

        [Fact]
        public async Task QuestRewardRoute_RemainsBoundAfterLaterTransferTransition()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeRouteAsync(
                "quest_reward",
                "quest_reward",
                MortalItemTestFixture.CreateRawRoot(
                    "quest_reward",
                    "quest_reward"));
            await context.NormalizeAcceptedTurnAsync();
            await context.AppendAcceptedTransferTransitionAsync();

            var issues = await context.Validator.ValidateGameStateAsync(
                IntegrationValidationProfiles.QuestReward);

            Assert.DoesNotContain(issues, issue =>
                issue.Code ==
                "mortal_item_materialization_quest_reward_authority_mismatch");
        }

        [Fact]
        public async Task ExistingNpcContainerDestination_BindsCanonicalPathAndIndex()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeExistingNpcContainerAcquisitionAsync();

            var outcome = await context.ValidateNormalizeAndValidateAsync();

            Assert.Empty(outcome.Errors);
            var index = (await context.ReadJsonAsync(
                MortalItemIdentityState.StatePath))!.AsObject();
            var created = index["entries"]!.AsArray()
                .OfType<JsonObject>()
                .Single(entry => !string.Equals(
                    entry["itemId"]!.GetValue<string>(),
                    "itm_npc_route_container",
                    StringComparison.Ordinal));
            Assert.Equal(
                "itm_npc_route_container",
                Assert.Single(created["currentCarrier"]!["containerPath"]!.AsArray())!
                    .GetValue<string>());
        }

        [Fact]
        public async Task ExistingNpcContainerDestination_WithUnknownParentIsRejectedBeforeSealing()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                "npc_acquisition",
                "npc_inventory_add",
                MortalItemTestFixture.CreateRawRoot(
                    "npc_acquisition",
                    "npc_inventory_add"));
            await context.SetNpcDestinationContainerAsync("itm_missing_npc_container");

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_orphan_companion" &&
                issue.Actor == "mortal_item:unresolved:itm_missing_npc_container" &&
                issue.FilePath.Contains("npc_inventory.json", StringComparison.Ordinal));
        }

        [Fact]
        public async Task TradeRoute_WithReceiptContractMismatch_IsRejectedBeforeSealing()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                "trade_output",
                "npc_trade_receipt",
                MortalItemTestFixture.CreateRawRoot(
                    "trade_output",
                    "npc_trade_receipt"));
            await context.ForgeTradeReceiptMerchantProfileAsync("forged_profile");

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_route_authority_missing" &&
                issue.Actor == $"mortal_item:new:{arrangement.CreationRef}");
        }

        [Fact]
        public async Task TradeRoute_WithUnrelatedOfferIdentity_IsRejectedBeforeSealing()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                "trade_output",
                "npc_trade_receipt",
                MortalItemTestFixture.CreateRawRoot(
                    "trade_output",
                    "npc_trade_receipt"));
            await context.ForgeTradeOfferSlotIdAsync("unrelated_trade_offer");

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_route_authority_missing" &&
                issue.Actor == $"mortal_item:new:{arrangement.CreationRef}");
        }

        [Fact]
        public async Task TradeRoute_WithMismatchedOfferSemantics_IsRejectedBeforeSealing()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                "trade_output",
                "npc_trade_receipt",
                MortalItemTestFixture.CreateRawRoot(
                    "trade_output",
                    "npc_trade_receipt"));
            await context.ForgeTradeOfferNameAsync("Подменённый товар");

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_route_authority_missing" &&
                issue.Actor == $"mortal_item:new:{arrangement.CreationRef}");
        }

        [Fact]
        public async Task TradeRoute_CurrentSchemaOfferRemainsTemplateAfterSealing()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                "trade_output",
                "npc_trade_receipt",
                MortalItemTestFixture.CreateRawRoot(
                    "trade_output",
                    "npc_trade_receipt"));

            await context.NormalizeAcceptedTurnAsync();

            var npcRoot = (await context.ReadJsonAsync(
                NpcCoreChangesContract.NpcCorePath))!.AsObject();
            var offer = npcRoot["NPCsInScene"]![0]!["tradeInventory"]!["items"]![0]!
                .AsObject();
            Assert.Equal(arrangement.CreationRef, offer["slotId"]!.GetValue<string>());
            var itemData = offer["itemData"]!.AsObject();
            Assert.Equal("trade_offer_item_route_001", itemData["itemId"]!.GetValue<string>());
            Assert.False(itemData.ContainsKey("creationRef"));
            Assert.False(itemData.ContainsKey("materialization"));
            Assert.False(itemData.ContainsKey("materializationReceipt"));

            var playerRoot = (await context.ReadJsonAsync(
                InventoryEquipmentService.ItemsPath))!.AsObject();
            var accepted = Assert.Single(playerRoot["items"]!.AsArray().OfType<JsonObject>());
            Assert.NotEqual(
                itemData["itemId"]!.GetValue<string>(),
                accepted["itemId"]!.GetValue<string>());
            Assert.NotNull(accepted["materializationReceipt"]);
        }

        [Fact]
        public async Task TradeRoute_WithExistingStorageDestination_IsRejectedBeforeSealing()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                "trade_output",
                "npc_trade_receipt",
                MortalItemTestFixture.CreateRawRoot(
                    "trade_output",
                    "npc_trade_receipt"));
            await context.MovePlayerRawCreationToExistingStorageAsync();

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_route_authority_mismatch" &&
                issue.Actor == $"mortal_item:new:{arrangement.CreationRef}");
        }

        [Fact]
        public async Task CraftRoute_WithNonPendingSnapshotRequest_IsRejectedBeforeSealing()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeCraftRouteWithStatusAsync("cancelled");

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_route_authority_missing" &&
                issue.Actor == $"mortal_item:new:{arrangement.CreationRef}");
        }

        [Fact]
        public async Task QuestRewardRoute_WithDuplicateDetail_IsRejectedBeforeSealing()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                "quest_reward",
                "quest_reward",
                MortalItemTestFixture.CreateRawRoot(
                    "quest_reward",
                    "quest_reward"));
            await context.DuplicateQuestRewardItemDetailAsync();

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_route_authority_missing" &&
                issue.Actor == $"mortal_item:new:{arrangement.CreationRef}");
        }

        [Fact]
        public async Task QuestRewardRoute_WithUnavailableDetail_IsRejectedBeforeSealing()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                "quest_reward",
                "quest_reward",
                MortalItemTestFixture.CreateRawRoot(
                    "quest_reward",
                    "quest_reward"));
            await context.MarkQuestRewardItemUnavailableAsync();

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_route_authority_missing" &&
                issue.Actor == $"mortal_item:new:{arrangement.CreationRef}");
        }

        [Theory]
        [InlineData("loot_acquisition", "loot_template")]
        [InlineData("craft_output", "craft_request")]
        [InlineData("trade_output", "npc_trade_receipt")]
        [InlineData("quest_reward", "quest_reward")]
        public async Task NonStorageRoute_WithNewSameTurnStorage_IsRejectedBeforeSealing(
            string route,
            string authorityKind)
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                route,
                authorityKind,
                MortalItemTestFixture.CreateRawRoot(route, authorityKind));
            await context.MovePlayerRawCreationToNewStorageAsync();

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_route_authority_missing" &&
                issue.Actor == $"mortal_item:new:{arrangement.CreationRef}");
        }
    }
}
