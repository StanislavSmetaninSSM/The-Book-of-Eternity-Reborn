using Xunit;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalItemMaterializationValidationTests
{
    [Trait("Category", "FullValidation")]
    public sealed class Companions
    {
        [Theory]
        [InlineData("equipment")]
        [InlineData("item_text")]
        [InlineData("journal")]
        [InlineData("bond")]
        [InlineData("recipe")]
        [InlineData("quest_reward")]
        [InlineData("storage_reference")]
        public async Task SameTurnReference_ResolvesOnlyToPermanentItemId(
            string companion)
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var route = string.Equals(
                companion,
                "storage_reference",
                StringComparison.Ordinal)
                ? "storage_placement"
                : "player_acquisition";
            var authorityKind = string.Equals(
                route,
                "storage_placement",
                StringComparison.Ordinal)
                ? "location_storage"
                : "turn_outcome";
            var arrangement = await context.ArrangeRouteAsync(
                route,
                authorityKind,
                MortalItemTestFixture.CreateRawRoot(route, authorityKind));
            await context.WriteSameTurnCompanionReferenceAsync(
                companion,
                arrangement.CreationRef);

            await context.NormalizeAcceptedTurnAsync();

            var itemId = await context.ReadSingleActiveMortalItemIdAsync();
            var surface = await context.ReadSameTurnCompanionSurfaceAsync(companion);
            Assert.True(
                MortalItemMaterializationTestContext.ContainsExactString(surface, itemId),
                $"{companion} did not contain the assigned permanent item ID.");
            Assert.False(
                MortalItemMaterializationTestContext.ContainsExactString(
                    surface,
                    arrangement.CreationRef),
                $"{companion} retained the temporary creationRef.");
        }

        [Fact]
        public async Task ExistingNpcEquipmentCommand_ResolvesAndAppliesPermanentItemId()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                "npc_acquisition",
                "npc_inventory_add",
                MortalItemTestFixture.CreateRawRoot(
                    "npc_acquisition",
                    "npc_inventory_add"));
            await context.WriteExistingNpcSameTurnEquipmentCommandAsync(
                arrangement.CreationRef);

            await context.NormalizeAcceptedTurnAsync();

            var itemId = await context.ReadSingleActiveMortalItemIdAsync();
            Assert.Equal(
                itemId,
                await context.ReadExistingNpcEquippedItemAsync("mainHand"));
        }

        [Fact]
        public async Task ExistingNpcEquipmentCommand_PreservesUnrelatedCommand()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                "npc_acquisition",
                "npc_inventory_add",
                MortalItemTestFixture.CreateRawRoot(
                    "npc_acquisition",
                    "npc_inventory_add"));
            await context.WriteExistingNpcSameTurnEquipmentCommandAsync(
                arrangement.CreationRef);
            await context.AppendUnrelatedNpcEquipmentCommandAsync();

            await context.NormalizeAcceptedTurnAsync();

            var itemId = await context.ReadSingleActiveMortalItemIdAsync();
            Assert.Equal(
                itemId,
                await context.ReadExistingNpcEquippedItemAsync("mainHand"));
            var commandRoot = (await context.ReadJsonAsync(
                "game_state/npcs/npc_inventory.json"))!.AsObject();
            var remaining = Assert.Single(
                commandRoot["NPCEquipmentChanges"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>());
            Assert.Equal(
                "itm_unrelated_existing_command",
                remaining["itemId"]!.GetValue<string>());
        }

        [Fact]
        public async Task NewNpcEquipmentReference_ResolvesToPermanentItemId()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var arrangement = await context.ArrangeRouteAsync(
                "new_npc_inventory",
                "new_npc",
                MortalItemTestFixture.CreateRawRoot(
                    "new_npc_inventory",
                    "new_npc"));
            await context.WriteNewNpcSameTurnEquipmentReferenceAsync(
                arrangement.CreationRef);

            await context.NormalizeAcceptedTurnAsync();

            var itemId = await context.ReadSingleActiveMortalItemIdAsync();
            Assert.Equal(
                itemId,
                await context.ReadNewNpcEquippedItemAsync("mainHand"));
        }

        [Fact]
        public async Task SameTurnParentContainerPath_ResolvesOnlyToPermanentItemId()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            var references = await context.ArrangeSameTurnPlayerContainerAsync();

            await context.NormalizeAcceptedTurnAsync();

            var root = (await context.ReadJsonAsync(
                InventoryEquipmentService.ItemsPath))!.AsObject();
            var items = root["items"]!.AsArray().OfType<System.Text.Json.Nodes.JsonObject>().ToArray();
            var parent = Assert.Single(items, item =>
                item["isContainer"]?.GetValue<bool>() == true);
            var child = Assert.Single(items, item =>
                item["name"]?.GetValue<string>() == "Вложенный тестовый предмет");
            Assert.Equal(
                parent["itemId"]!.GetValue<string>(),
                Assert.Single(child["contentsPath"]!.AsArray())!.GetValue<string>());
            Assert.False(MortalItemMaterializationTestContext.ContainsExactString(
                child["contentsPath"],
                references.ParentCreationRef));
        }

        [Fact]
        public async Task CanonicalOrphanCompanion_IsRejectedWithExactTarget()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeEmptyMortalTurnAsync();
            await context.WriteJsonAsync(
                "game_state/inventory/item_text_updates.json",
                new System.Text.Json.Nodes.JsonObject
                {
                    ["entries"] = new System.Text.Json.Nodes.JsonArray(
                        new System.Text.Json.Nodes.JsonObject
                        {
                            ["itemId"] = "itm_missing_companion_owner",
                            ["textContent"] = new System.Text.Json.Nodes.JsonArray(
                                "Текст без предмета.")
                        })
                });

            var issues = await context.Validator
                .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_orphan_companion" &&
                issue.FilePath.Contains(
                    "item_text_updates.json",
                    StringComparison.Ordinal));
        }

        [Fact]
        public async Task CanonicalHistoricalUnavailableQuestItem_IsNotTreatedAsOrphan()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeEmptyMortalTurnAsync();
            await context.WriteJsonAsync(
                "game_state/quests/quest_history.json",
                new System.Text.Json.Nodes.JsonObject
                {
                    ["questHistory"] = new System.Text.Json.Nodes.JsonArray(),
                    ["questRewards"] = new System.Text.Json.Nodes.JsonArray(
                        new System.Text.Json.Nodes.JsonObject
                        {
                            ["questId"] = "quest_prior_incarnation",
                            ["itemsReceived"] = new System.Text.Json.Nodes.JsonArray(
                                new System.Text.Json.Nodes.JsonObject
                                {
                                    ["itemId"] = "itm_destroyed_prior_incarnation",
                                    ["displayName"] = "Утраченная реликвия",
                                    ["authorityStatus"] = "PriorIncarnation",
                                    ["reason"] = "Предмет остался в прошлой жизни."
                                })
                        }),
                    ["questChains"] = new System.Text.Json.Nodes.JsonArray()
                });

            var issues = await context.Validator
                .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

            Assert.DoesNotContain(issues, issue =>
                issue.Code == "mortal_item_materialization_orphan_companion");
        }

        [Fact]
        public async Task CanonicalNpcEquipmentCannotReferencePlayerOwnedItem()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.BuildMortalBootstrapAsync();
            var playerItem = MortalItemTestFixture.CreateCanonicalRoot(
                "itm_player_owned_not_npc_equipment");
            await context.WriteCanonicalPlayerItemAsync(
                playerItem,
                MortalItemTestFixture.CreateIndex(playerItem));
            await context.WriteJsonAsync(
                NpcCoreChangesContract.NpcCorePath,
                new System.Text.Json.Nodes.JsonObject
                {
                    ["UpdateNPCs"] = new System.Text.Json.Nodes.JsonArray(),
                    ["NPCsInScene"] = new System.Text.Json.Nodes.JsonArray(
                        new System.Text.Json.Nodes.JsonObject
                        {
                            ["NPCId"] = "npc_wrong_equipment_owner",
                            ["inventory"] = new System.Text.Json.Nodes.JsonArray(),
                            ["equippedItems"] = new System.Text.Json.Nodes.JsonObject
                            {
                                ["mainHand"] = "itm_player_owned_not_npc_equipment"
                            }
                        })
                });
            await context.CaptureValidatedPendingSnapshotAsync();

            var issues = await context.Validator
                .ValidateAcceptedTurnCanonicalMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_companion_owner_mismatch" &&
                issue.FilePath.Contains("npc_core.json", StringComparison.Ordinal));
        }

        [Fact]
        public async Task RawUnresolvedCompanionCreationRef_IsRejected()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeEmptyMortalTurnAsync();
            await context.WriteJsonAsync(
                "game_state/inventory/item_bonds.json",
                new System.Text.Json.Nodes.JsonObject
                {
                    ["itemBondLevelChanges"] = new System.Text.Json.Nodes.JsonArray(
                        new System.Text.Json.Nodes.JsonObject
                        {
                            ["creationRef"] = "new_item_missing_companion_owner",
                            ["newBondLevel"] = 1,
                            ["changeReason"] = "Несуществующий предмет."
                        })
                });

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_orphan_companion" &&
                issue.FilePath.Contains("item_bonds.json", StringComparison.Ordinal));
        }

        [Fact]
        public async Task RawCompanionCannotReuseHistoricalEnvelopeCreationRef()
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.BuildMortalBootstrapAsync();
            var existing = MortalItemTestFixture.CreateCanonicalRoot("itm_existing_history_ref");
            await context.WriteCanonicalPlayerItemAsync(
                existing,
                MortalItemTestFixture.CreateIndex(existing));
            await context.CaptureValidatedPendingSnapshotAsync();
            var historicalCreationRef = existing["materialization"]!["creationRef"]!
                .GetValue<string>();
            await context.WriteJsonAsync(
                "game_state/inventory/item_bonds.json",
                new System.Text.Json.Nodes.JsonObject
                {
                    ["itemBondLevelChanges"] = new System.Text.Json.Nodes.JsonArray(
                        new System.Text.Json.Nodes.JsonObject
                        {
                            ["creationRef"] = historicalCreationRef,
                            ["newBondLevel"] = 1,
                            ["changeReason"] = "Нельзя переиспользовать старый creationRef."
                        })
                });

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_orphan_companion" &&
                issue.Actor == $"mortal_item:new:{historicalCreationRef}");
        }

        [Theory]
        [InlineData("equipment")]
        [InlineData("storage")]
        [InlineData("contents_path")]
        public async Task RawUnresolvedInlineCreationRef_IsRejectedBeforeSealing(
            string surface)
        {
            await using var context = await MortalItemMaterializationTestContext.CreateAsync();
            await context.ArrangeUnresolvedInlineCompanionAsync(surface);

            var issues = await context.Validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();

            Assert.Contains(issues, issue =>
                issue.Code == "mortal_item_materialization_orphan_companion" &&
                issue.Actor == "mortal_item:unresolved:new_item_missing_inline_owner");
        }
    }
}
