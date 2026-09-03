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

/// <summary>Durable RED oracle for the frozen T070-B.4 pure item planners.</summary>
public sealed class MortalItemConsumptionPlannerTests
{
    private const int Turn = 42;
    private const string PlayerPath = "game_state/inventory/items.json";
    private const string NpcPath = "game_state/npcs/npc_core.json";
    private const string NpcCommandsPath = "game_state/npcs/npc_inventory.json";
    private static readonly string[] ProjectionRootPaths =
    {
        PlayerPath,
        NpcPath,
        NpcCommandsPath,
        MortalItemAcceptedTransferCatalog.PlayerRemovalPath,
        StorageTransportMoveService.CurrentLocationPath,
        MortalLocationStorageContentsState.StatePath,
        StorageTransportMoveService.VehiclesPath,
        MortalItemIdentityState.StatePath,
        "game_state/quests/quest_history.json",
        "game_state/inventory/item_bonds.json",
        "game_state/inventory/item_text_updates.json",
        "game_state/inventory/recipes.json",
        "game_state/npcs/item_journals.json"
    };

    [Fact]
    public void Plan_PartialStackPreservesIdentityReceiptAndCarrier()
    {
        var fixture = CreateFixture("itm_partial", 3);
        var beforeItem = FindItem(RootMap(fixture.Roots), fixture.ItemId);
        var beforeReceipt = beforeItem["materializationReceipt"]!.DeepClone();
        var beforeCarrier = Entry(fixture.Index, fixture.ItemId)["currentCarrier"]!.DeepClone();
        const string transitionId = "mitrn_t070b4_partial_0001";

        var result = Plan(fixture, BuildCommand(1, fixture.ItemId, 1, transitionId));

        AssertValid(result);
        var afterItem = FindItem(result.Roots, fixture.ItemId);
        Assert.Equal(2, afterItem["count"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(beforeReceipt, afterItem["materializationReceipt"]));
        var afterEntry = Entry(result.Index!, fixture.ItemId);
        Assert.Equal("active", afterEntry["state"]!.GetValue<string>());
        Assert.Equal(beforeItem["materializationReceipt"]!["receiptId"]!.GetValue<string>(),
            afterEntry["receiptId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(beforeCarrier, afterEntry["currentCarrier"]));
        AssertConsume(Assert.Single(result.Transitions), transitionId, fixture.ItemId,
            3, 2, beforeCarrier, beforeCarrier);
        Assert.Equal(transitionId, LastTransition(afterEntry)["transitionId"]!.GetValue<string>());
        Assert.Empty(result.Capacities);
        Assert.Empty(result.TerminalOwners);
    }

    [Fact]
    public void Plan_FullStackConsumesIdentityClearsEquipmentAndReturnsTerminalOwner()
    {
        var fixture = CreateFixture("itm_terminal", 1,
            item =>
            {
                Populate(item, "equipment");
                item["equipmentSlot"] = "MainHand";
            },
            (root, id) => root["equippedItems"]!["MainHand"] = id,
            Resource("itm_terminal", 5m, 3m));
        var catalog = MortalItemCarrierCatalog.Build(fixture.Roots);
        var equipment = Assert.Single(catalog.ByCompanionReference[fixture.ItemId]);
        Assert.Equal(PlayerPath, equipment.FilePath);
        Assert.Equal("MainHand", equipment.PropertyName);
        Assert.NotNull(equipment.ExpectedCarrier);
        var beforeCarrier = Entry(fixture.Index, fixture.ItemId)["currentCarrier"]!.DeepClone();
        const string transitionId = "mitrn_t070b4_terminal_0001";

        var result = Plan(fixture, BuildCommand(1, fixture.ItemId, 1, transitionId));

        AssertValid(result);
        Assert.False(HasItem(result.Roots, fixture.ItemId));
        Assert.DoesNotContain(fixture.ItemId, result.Roots.Values.SelectMany(Strings));
        var playerAfterImage = result.Roots[PlayerPath];
        Assert.True(playerAfterImage["equippedItems"]!["MainHand"] is null);
        var afterEntry = Entry(result.Index!, fixture.ItemId);
        Assert.Equal("consumed", afterEntry["state"]!.GetValue<string>());
        Assert.Null(afterEntry["currentCarrier"]);
        AssertConsume(Assert.Single(result.Transitions), transitionId, fixture.ItemId,
            1, 0, beforeCarrier, null);
        Assert.Equal(transitionId, LastTransition(afterEntry)["transitionId"]!.GetValue<string>());
        Assert.Empty(result.Capacities);
        Assert.Equal(new ResourceOwnerKey("mortal_world", ResourceOwnerKind.Item, fixture.ItemId),
            Assert.Single(result.TerminalOwners));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Plan_FullNpcStackClearsOnlyExactSourceEquipmentAndPreservesNpcSiblings(
        bool legacyEquipmentSurface)
    {
        var fixture = CreateNpcEquipmentFixture(
            "itm_npc_terminal",
            legacyEquipmentSurface);
        var catalog = MortalItemCarrierCatalog.Build(fixture.Roots);
        var equipment = Assert.Single(catalog.ByCompanionReference[fixture.ItemId]);
        Assert.Equal(NpcPath, equipment.FilePath);
        Assert.Equal("mainHand", equipment.PropertyName);
        Assert.Equal("npc_inventory", equipment.ExpectedCarrier?.Kind);
        Assert.Equal("npc_terminal_owner", equipment.ExpectedCarrier?.OwnerId);
        Assert.Empty(equipment.ExpectedCarrier!.ContainerPath);
        var beforeCarrier = Entry(fixture.Index, fixture.ItemId)["currentCarrier"]!.DeepClone();
        var otherNpcBefore = Assert.Single(Npcs(
            fixture.Roots.NpcCore!,
            "npc_terminal_other")).DeepClone();
        const string transitionId = "mitrn_t070b4_npc_terminal_0001";

        var result = Plan(fixture, BuildCommand(1, fixture.ItemId, 1, transitionId));

        AssertValid(result);
        Assert.False(HasItem(result.Roots, fixture.ItemId));
        Assert.DoesNotContain(fixture.ItemId, result.Roots.Values.SelectMany(Strings));
        var npcAfterImage = result.Roots[NpcPath];
        var ownerCopiesAfter = Npcs(npcAfterImage, "npc_terminal_owner");
        Assert.Equal(2, ownerCopiesAfter.Count);
        Assert.True(JsonNode.DeepEquals(ownerCopiesAfter[0], ownerCopiesAfter[1]));
        Assert.All(ownerCopiesAfter, ownerAfter =>
        {
            Assert.Empty(ownerAfter["inventory"]!.AsArray());
            Assert.Null(ownerAfter["equippedItems"]!["mainHand"]);
            Assert.Equal("itm_npc_equipment_sibling",
                ownerAfter["equippedItems"]!["offHand"]!.GetValue<string>());
            Assert.Null(ownerAfter["equipment"]!["mainHand"]);
            Assert.Equal("itm_npc_legacy_equipment_sibling",
                ownerAfter["equipment"]!["offHand"]!.GetValue<string>());
            Assert.Equal("preserve owner sibling", ownerAfter["unchanged"]!.GetValue<string>());
        });
        Assert.True(JsonNode.DeepEquals(
            otherNpcBefore,
            Assert.Single(Npcs(npcAfterImage, "npc_terminal_other"))));
        var afterEntry = Entry(result.Index!, fixture.ItemId);
        Assert.Equal("consumed", afterEntry["state"]!.GetValue<string>());
        Assert.Null(afterEntry["currentCarrier"]);
        AssertConsume(Assert.Single(result.Transitions), transitionId, fixture.ItemId,
            1, 0, beforeCarrier, null);
        Assert.Equal(transitionId, LastTransition(afterEntry)["transitionId"]!.GetValue<string>());
        Assert.Empty(result.Capacities);
        Assert.Equal(new ResourceOwnerKey("mortal_world", ResourceOwnerKind.Item, fixture.ItemId),
            Assert.Single(result.TerminalOwners));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Plan_FullNpcStackRejectsCrossCarrierOrConfusableEquipmentReference(
        bool confusable)
    {
        var fixture = CreateNpcEquipmentFixture("itm_npc_guarded");
        var roots = Clone(fixture.Roots);
        var owners = Npcs(roots.NpcCore!, "npc_terminal_owner");
        var other = Assert.Single(Npcs(roots.NpcCore!, "npc_terminal_other"));
        if (!confusable)
        {
            Assert.All(owners, owner =>
                owner["equippedItems"]!["mainHand"] = null);
        }
        other["equippedItems"]!["mainHand"] = confusable
            ? fixture.ItemId.ToUpperInvariant()
            : fixture.ItemId;
        var catalog = MortalItemCarrierCatalog.Build(roots);
        if (confusable)
        {
            Assert.Contains(catalog.Issues, issue =>
                issue.Code == "mortal_item_materialization_identity_ambiguity");
        }
        else
        {
            Assert.Empty(catalog.Issues);
            var reference = Assert.Single(catalog.ByCompanionReference[fixture.ItemId]);
            Assert.Equal("npc_terminal_other", reference.ExpectedCarrier?.OwnerId);
        }

        var result = PlanWithRoots(
            fixture,
            roots,
            BuildCommand(1, fixture.ItemId, 1, "mitrn_t070b4_npc_guarded_0001"));

        AssertInvalidEmpty(result);
    }

    [Fact]
    public void FinalBaseline_PlayerItemAllowsIdenticalPermanentNpcCrossSectionMirror()
    {
        var currentNpcRoot = CreateMirroredNpcRoot();
        var backupNpcRoot = CreateMirroredNpcRoot();
        foreach (var current in Npcs(currentNpcRoot, "npc_baseline_mirror"))
        {
            current["equipment"] = new JsonObject
            {
                ["mainHand"] = null,
                ["offHand"] = "itm_baseline_equipment_current"
            };
        }
        currentNpcRoot[NpcCoreChangesContract.PropertyName] = new JsonArray(
            new JsonObject
            {
                ["NPCId"] = "npc_baseline_mirror",
                ["reason"] = "The item transfer and this profile update share one turn.",
                ["profile"] = new JsonObject
                {
                    ["worldview"] = "Mirrors preserve exact evidence."
                }
            });
        foreach (var backup in Npcs(backupNpcRoot, "npc_baseline_mirror"))
        {
            backup["equipment"] = new JsonObject
            {
                ["mainHand"] = "itm_baseline_equipment_removed",
                ["offHand"] = "itm_baseline_equipment_backup"
            };
        }
        var input = CreateBaselineInput(currentNpcRoot, backupNpcRoot);

        var result = MortalItemPublicationBaselinePlanner.Project(input);

        Assert.Empty(result.Issues);
        AssertFingerprint(result.Fingerprint);
        Assert.Equal(
            "itm_baseline_player",
            FindItem(result.FinalCarrierRoots, "itm_baseline_player")["itemId"]!
                .GetValue<string>());
        var npcRoot = Assert.IsType<JsonObject>(result.FinalCarrierRoots[NpcPath]);
        var copies = Npcs(npcRoot, "npc_baseline_mirror");
        Assert.Equal(2, copies.Count);
        Assert.True(JsonNode.DeepEquals(copies[0], copies[1]));
        Assert.Null(copies[0]["equipment"]!["mainHand"]);
        Assert.Equal("itm_baseline_equipment_current",
            copies[0]["equipment"]!["offHand"]!.GetValue<string>());
        Assert.Equal("Mirrors preserve exact evidence.",
            copies[0]["worldview"]!.GetValue<string>());
        Assert.False(npcRoot.ContainsKey(NpcCoreChangesContract.PropertyName));
    }

    [Fact]
    public void FinalBaseline_PlayerItemAllowsSameTurnNewNpcAbsentFromBackup()
    {
        var existingNpc = new JsonObject
        {
            ["NPCId"] = "npc_baseline_existing_control",
            ["name"] = "Existing baseline control",
            ["inventory"] = new JsonArray(),
            ["equippedItems"] = new JsonObject()
        };
        var newNpc = new JsonObject
        {
            ["initialId"] = "npc_baseline_same_turn_new",
            ["name"] = "Same-turn baseline NPC",
            ["inventory"] = new JsonArray(),
            ["equippedItems"] = new JsonObject()
        };
        var current = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(newNpc),
            ["NPCsInScene"] = new JsonArray(existingNpc.DeepClone()),
            [NpcCoreChangesContract.PropertyName] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_baseline_existing_control",
                ["reason"] = "Exercise the real NPC semantic comparison baseline.",
                ["profile"] = new JsonObject
                {
                    ["worldview"] = "New actors remain new during comparison."
                }
            })
        };
        var backup = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(),
            ["NPCsInScene"] = new JsonArray(existingNpc.DeepClone())
        };

        var result = MortalItemPublicationBaselinePlanner.Project(
            CreateBaselineInput(current, backup));

        Assert.Empty(result.Issues);
        AssertFingerprint(result.Fingerprint);
        Assert.Equal(
            "itm_baseline_player",
            FindItem(result.FinalCarrierRoots, "itm_baseline_player")["itemId"]!
                .GetValue<string>());
        var npcRoot = Assert.IsType<JsonObject>(result.FinalCarrierRoots[NpcPath]);
        var created = Assert.Single(npcRoot["UpdateNPCs"]!.AsArray());
        Assert.Equal("npc_baseline_same_turn_new",
            created!["initialId"]!.GetValue<string>());
        var existing = Assert.Single(Npcs(npcRoot, "npc_baseline_existing_control"));
        Assert.Equal("New actors remain new during comparison.",
            existing["worldview"]!.GetValue<string>());
        Assert.False(npcRoot.ContainsKey(NpcCoreChangesContract.PropertyName));
    }

    [Theory]
    [InlineData("UpdateNPCs", "NPCsInScene")]
    [InlineData("NPCsInScene", "UpdateNPCs")]
    public void FinalBaseline_PlayerItemAllowsExactExistingNpcSectionMove(
        string currentSection,
        string backupSection)
    {
        var actor = new JsonObject
        {
            ["NPCId"] = "npc_baseline_section_move",
            ["name"] = "Section-moving baseline NPC",
            ["inventory"] = new JsonArray(),
            ["equippedItems"] = new JsonObject()
        };
        JsonObject Root(string section)
        {
            var update = new JsonArray();
            var scene = new JsonArray();
            (section == "UpdateNPCs" ? update : scene).Add(actor.DeepClone());
            return new JsonObject
            {
                ["UpdateNPCs"] = update,
                ["NPCsInScene"] = scene
            };
        }

        var result = MortalItemPublicationBaselinePlanner.Project(
            CreateBaselineInput(Root(currentSection), Root(backupSection)));

        Assert.Empty(result.Issues);
        var npcRoot = Assert.IsType<JsonObject>(result.FinalCarrierRoots[NpcPath]);
        Assert.Single(npcRoot[currentSection]!.AsArray());
        Assert.Empty(npcRoot[backupSection]!.AsArray());
        Assert.Equal("npc_baseline_section_move",
            Assert.Single(Npcs(npcRoot, "npc_baseline_section_move"))["NPCId"]!
                .GetValue<string>());
    }

    [Theory]
    [InlineData("divergent_cross_section")]
    [InlineData("same_section_duplicate")]
    [InlineData("confusable_cross_section")]
    [InlineData("homoglyph_cross_section")]
    public void FinalBaseline_RejectsInvalidNpcDuplicateTopology(string mutation)
    {
        var npcRoot = CreateMirroredNpcRoot();
        switch (mutation)
        {
            case "divergent_cross_section":
                npcRoot["NPCsInScene"]![0]!["name"] = "Divergent mirror";
                break;
            case "same_section_duplicate":
                npcRoot["UpdateNPCs"]!.AsArray().Add(
                    npcRoot["UpdateNPCs"]![0]!.DeepClone());
                npcRoot["NPCsInScene"] = new JsonArray();
                break;
            case "confusable_cross_section":
                npcRoot["NPCsInScene"]![0]!["NPCId"] = "NPC_BASELINE_MIRROR";
                break;
            case "homoglyph_cross_section":
                npcRoot["NPCsInScene"]![0]!["NPCId"] =
                    "np\u0441_baseline_mirror";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = MortalItemPublicationBaselinePlanner.Project(
            CreateBaselineInput(npcRoot));

        Assert.NotEmpty(result.Issues);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_publication_baseline_semantic_npc_invalid");
        Assert.Empty(result.FinalCarrierRoots);
    }

    [Theory]
    [InlineData("backup_only_existing")]
    [InlineData("current_only_permanent")]
    public void FinalBaseline_RejectsNpcActorSetChangeWithoutCreationOrDeleteAuthority(
        string mutation)
    {
        var mirrored = CreateMirroredNpcRoot();
        var empty = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(),
            ["NPCsInScene"] = new JsonArray()
        };
        var current = mutation == "backup_only_existing" ? empty : mirrored;
        var backup = mutation == "backup_only_existing" ? mirrored : empty;

        var result = MortalItemPublicationBaselinePlanner.Project(
            CreateBaselineInput(current, backup));

        Assert.NotEmpty(result.Issues);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_item_publication_baseline_semantic_npc_invalid");
        Assert.Empty(result.FinalCarrierRoots);
    }

    [Theory]
    [InlineData("divergent_cross_section")]
    [InlineData("same_section_duplicate")]
    [InlineData("confusable_cross_section")]
    [InlineData("conflicting_alias")]
    public void MortalNpcCommandIndex_RejectsInvalidNpcDuplicateTopology(
        string mutation)
    {
        var npcRoot = CreateMirroredNpcRoot();
        switch (mutation)
        {
            case "divergent_cross_section":
                npcRoot["NPCsInScene"]![0]!["name"] = "Divergent command mirror";
                break;
            case "same_section_duplicate":
                npcRoot["UpdateNPCs"]!.AsArray().Add(
                    npcRoot["UpdateNPCs"]![0]!.DeepClone());
                npcRoot["NPCsInScene"] = new JsonArray();
                break;
            case "confusable_cross_section":
                npcRoot["NPCsInScene"]![0]!["NPCId"] = "NPC_BASELINE_MIRROR";
                break;
            case "conflicting_alias":
                foreach (var owner in Npcs(npcRoot, "npc_baseline_mirror"))
                    owner["id"] = "npc_conflicting_alias";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var index = CanonicalStateNormalizer.MortalNpcCommandIndex.Build(npcRoot);

        Assert.False(index.TryGetOwners("npc_baseline_mirror", out var owners));
        Assert.Empty(owners);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void TransferPlanner_UpdatesEveryIdenticalPermanentNpcMirror(
        bool fromNpc,
        bool legacyEquipmentSurface)
    {
        const string itemId = "itm_transfer_npc_mirror";
        const string npcId = "npc_transfer_mirror";
        var item = MortalItemTestFixture.CreateCanonicalRoot(itemId);
        item["count"] = 1;
        MortalItemTestFixture.ResealCanonical(item);
        var player = fromNpc
            ? new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equippedItems"] = new JsonObject()
            }
            : MortalItemTestFixture.CreateCarrier(item, "player_inventory", "player");
        var npc = new JsonObject
        {
            ["NPCId"] = npcId,
            ["name"] = "Transfer mirror",
            ["inventory"] = fromNpc
                ? new JsonArray(item.DeepClone())
                : new JsonArray(),
            ["equippedItems"] = new JsonObject
            {
                ["mainHand"] = fromNpc && !legacyEquipmentSurface ? itemId : null,
                ["offHand"] = "itm_transfer_mirror_sibling"
            },
            ["equipment"] = new JsonObject
            {
                ["mainHand"] = fromNpc && legacyEquipmentSurface ? itemId : null,
                ["offHand"] = "itm_transfer_legacy_mirror_sibling"
            }
        };
        var npcRoot = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(npc.DeepClone()),
            ["NPCsInScene"] = new JsonArray(npc.DeepClone())
        };
        var source = fromNpc
            ? new MortalItemCarrierCoordinate(
                "npc_inventory", npcId, null, Array.Empty<string>())
            : new MortalItemCarrierCoordinate(
                "player_inventory", "player", null, Array.Empty<string>());
        var destination = fromNpc
            ? new MortalItemCarrierCoordinate(
                "player_inventory", "player", null, Array.Empty<string>())
            : new MortalItemCarrierCoordinate(
                "npc_inventory", npcId, null, Array.Empty<string>());
        var index = MortalItemTestFixture.CreateIndexForCarrier(
            item,
            source.Kind,
            source.OwnerId);
        var roots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal)
        {
            [PlayerPath] = player,
            [NpcPath] = npcRoot,
            [MortalItemIdentityState.StatePath] = index
        };
        var transfer = new MortalItemAcceptedTransfer(
            itemId,
            source,
            destination,
            1,
            Turn,
            "mortal_item_transfer",
            "mita_transfer_npc_mirror",
            fromNpc
                ? MortalItemTransferCommandSurface.PlayerUpdate
                : MortalItemTransferCommandSurface.NpcAdd,
            0,
            fromNpc
                ? MortalItemTransferCommandSurface.NpcRemoval
                : MortalItemTransferCommandSurface.PlayerRemoval,
            0);

        var result = MortalItemTransferPlanner.Plan(
            roots,
            MortalItemIdentityState.Parse(index.ToJsonString()),
            new[] { transfer },
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [itemId] = "mitrn_transfer_npc_mirror"
            },
            removeAcceptedCommands: false);

        Assert.True(result.IsValid, string.Join(
            Environment.NewLine,
            result.Issues.Select(issue => $"{issue.Code}: {issue.Actual}")));
        Assert.Empty(result.Issues);
        var npcAfter = Assert.IsType<JsonObject>(result.Roots[NpcPath]);
        var copies = Npcs(npcAfter, npcId);
        Assert.Equal(2, copies.Count);
        Assert.True(JsonNode.DeepEquals(copies[0], copies[1]));
        Assert.All(copies, copy =>
        {
            Assert.Equal(fromNpc ? 0 : 1, copy["inventory"]!.AsArray().Count);
            Assert.Null(copy["equippedItems"]!["mainHand"]);
            Assert.Equal("itm_transfer_mirror_sibling",
                copy["equippedItems"]!["offHand"]!.GetValue<string>());
            Assert.Null(copy["equipment"]!["mainHand"]);
            Assert.Equal("itm_transfer_legacy_mirror_sibling",
                copy["equipment"]!["offHand"]!.GetValue<string>());
        });
        Assert.Equal(fromNpc ? 1 : 0,
            result.Roots[PlayerPath]!["items"]!.AsArray().Count);
        var entry = Entry(result.IdentityIndexAfterImage, itemId);
        Assert.Equal(destination.Kind, entry["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.Equal(destination.OwnerId,
            entry["currentCarrier"]!["ownerId"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("container")]
    [InlineData("quest")]
    [InlineData("bond")]
    [InlineData("other")]
    public void Plan_FullStackRejectsContainerQuestBondOrOtherCompanionWithoutAfterImages(string kind)
    {
        var companion = CreateCompanionFixture(kind);
        var catalog = MortalItemCarrierCatalog.Build(companion.Fixture.Roots);
        Assert.Empty(catalog.Issues);
        var references = catalog.ByCompanionReference[companion.Fixture.ItemId];
        Assert.NotEmpty(references);
        Assert.All(references, reference => Assert.Equal(companion.Path, reference.FilePath));

        var result = Plan(companion.Fixture,
            BuildCommand(1, companion.Fixture.ItemId, 1, $"mitrn_t070b4_{kind}_0001"));

        AssertInvalidEmpty(result);
        Assert.Contains(result.Issues, issue =>
            issue.Code?.Contains("companion", StringComparison.Ordinal) == true ||
            issue.Message.Contains("companion", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Plan_RepeatedClaimsEmitSequentialTransitionsInFinalizationOrder()
    {
        var fixture = CreateFixture("itm_repeated", 2);
        var carrier = Entry(fixture.Index, fixture.ItemId)["currentCarrier"]!.DeepClone();
        const string firstId = "mitrn_t070b4_repeated_0001";
        const string secondId = "mitrn_t070b4_repeated_0002";

        var result = Plan(fixture,
            BuildCommand(1, fixture.ItemId, 1, firstId),
            BuildCommand(2, fixture.ItemId, 1, secondId));

        AssertValid(result);
        Assert.Equal(new[] { firstId, secondId }, result.Transitions
            .Select(value => value["transitionId"]!.GetValue<string>()));
        AssertConsume(result.Transitions[0], firstId, fixture.ItemId, 2, 1, carrier, carrier);
        AssertConsume(result.Transitions[1], secondId, fixture.ItemId, 1, 0, carrier, null);
        Assert.False(HasItem(result.Roots, fixture.ItemId));
        var entry = Entry(result.Index!, fixture.ItemId);
        Assert.Equal("consumed", entry["state"]!.GetValue<string>());
        Assert.Equal(new[] { firstId, secondId }, entry["transitions"]!.AsArray()
            .OfType<JsonObject>().TakeLast(2)
            .Select(value => value["transitionId"]!.GetValue<string>()));
        Assert.Equal(new ResourceOwnerKey("mortal_world", ResourceOwnerKind.Item, fixture.ItemId),
            Assert.Single(result.TerminalOwners));
    }

    [Fact]
    public void Plan_CommonFinalizationOrdinalGapsRemainInStrictOrder()
    {
        var fixture = CreateFixture("itm_common_ordinal_gap", 2);
        const string firstId = "mitrn_t070b4_common_gap_0002";
        const string secondId = "mitrn_t070b4_common_gap_0004";

        var result = Plan(
            fixture,
            BuildCommand(2, fixture.ItemId, 1, firstId),
            BuildCommand(4, fixture.ItemId, 1, secondId));

        AssertValid(result);
        Assert.Equal(
            new[] { firstId, secondId },
            result.Transitions.Select(value =>
                value["transitionId"]!.GetValue<string>()));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    public void Plan_DuplicateOrDecreasingCommonFinalizationOrdinalsReject(
        int firstOrdinal,
        int secondOrdinal)
    {
        var fixture = CreateFixture("itm_common_ordinal_invalid", 2);

        var result = Plan(
            fixture,
            BuildCommand(
                firstOrdinal,
                fixture.ItemId,
                1,
                "mitrn_t070b4_common_invalid_first"),
            BuildCommand(
                secondOrdinal,
                fixture.ItemId,
                1,
                "mitrn_t070b4_common_invalid_second"));

        AssertInvalidEmpty(result);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                     "mortal_item_consumption_ordinal_not_contiguous");
    }

    [Fact]
    public void Plan_ResourceBearingPartialScalesMaximumAndCurrentExactly()
    {
        var state = Resource("itm_resource_exact", 8m, 6m);
        var fixture = CreateFixture("itm_resource_exact", 4, resource: state);
        var definition = Definition(fixture.Definitions, "durability");

        var result = Plan(fixture,
            BuildCommand(1, fixture.ItemId, 2, "mitrn_t070b4_resource_exact_0001"));

        AssertValid(result);
        Assert.Equal(2, FindItem(result.Roots, fixture.ItemId)["count"]!.GetValue<int>());
        var capacity = Assert.Single(result.Capacities);
        Assert.Equal(state.Coordinate, capacity.Coordinate);
        Assert.Equal(ResourceCapacityOperation.Reconfigure, capacity.Operation);
        Assert.NotNull(capacity.ResolvedCapacity);
        Assert.Equal(4m, capacity.ResolvedCapacity!.Maximum);
        Assert.Equal(ResourceCapacityKind.InstanceFixed, capacity.ResolvedCapacity.Binding.Kind);
        Assert.Equal(ResourceCurrentDisposition.ScaleRatioExact, capacity.CurrentDisposition);
        Assert.Equal(ResourceMutationPhase.RegisteredSystemOutcome, capacity.Phase);
        Assert.Equal(70, capacity.Priority);
        Assert.Equal(fixture.Source.SourceId, capacity.OriginId);
        Assert.Equal(fixture.Source, capacity.SourceEvidence);
        Assert.Equal(fixture.PolicyFingerprint, capacity.PolicyFingerprint);
        Assert.Equal(1m, definition.Quantum);
        Assert.Equal(3m, ExactScale(state.Current, 2, 4, definition.Quantum));
        AssertOrdinal(capacity.EventRef, 1);
        Assert.Empty(result.TerminalOwners);
    }

    [Fact]
    public void Plan_SuspendedLiveItemResourceScalesExactlyAndRemainsActionable()
    {
        var state = Resource("itm_resource_suspended", 8m, 6m) with
        {
            State = ResourceLifecycleState.Suspended
        };
        var fixture = CreateFixture("itm_resource_suspended", 4, resource: state);

        var result = Plan(fixture,
            BuildCommand(1, fixture.ItemId, 2, "mitrn_t070b4_resource_suspended_0001"));

        AssertValid(result);
        var capacity = Assert.Single(result.Capacities);
        Assert.Equal(4m, capacity.ResolvedCapacity!.Maximum);
        Assert.Equal(ResourceCurrentDisposition.ScaleRatioExact, capacity.CurrentDisposition);
        Assert.Equal(ResourceCapacityOperation.Reconfigure, capacity.Operation);
    }

    [Theory]
    [InlineData("inexact_quantum")]
    [InlineData("non_instance_fixed")]
    public void Plan_InexactOrNonInstanceFixedCapacityRejectsWithoutAfterImages(string axis)
    {
        var kind = axis == "inexact_quantum"
            ? ResourceCapacityKind.InstanceFixed
            : ResourceCapacityKind.RegisteredFormula;
        var state = Resource("itm_resource_" + axis, 1m, 1m, kind);
        var fixture = CreateFixture(state.Coordinate.ResourceOwnerId, 3, resource: state);
        Assert.Equal(kind, Assert.Single(fixture.State.Entries).CapacityBinding.Kind);
        Assert.Equal(1m, Definition(fixture.Definitions, "durability").Quantum);

        var result = Plan(fixture,
            BuildCommand(1, fixture.ItemId, 1, $"mitrn_t070b4_{axis}_0001"));

        AssertInvalidEmpty(result);
        Assert.Contains(result.Issues, issue =>
            issue.Code?.Contains("capacity", StringComparison.Ordinal) == true ||
            issue.Message.Contains("capacity", StringComparison.OrdinalIgnoreCase) ||
            issue.Message.Contains("quantum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Plan_IsWriteFreeAndDeterministicAcrossDetachedInputs()
    {
        var canonicalRoot = Path.Combine(FindRepo(), "FileSystemExample", "game_session", "game_state");
        var filesBefore = ReadFiles(canonicalRoot);
        var firstFixture = CreateFixture("itm_deterministic", 3,
            resource: Resource("itm_deterministic", 6m, 3m));
        var secondFixture = CreateFixture("itm_deterministic", 3,
            resource: Resource("itm_deterministic", 6m, 3m));
        var firstInput = Describe(firstFixture);
        var secondInput = Describe(secondFixture);
        var commands = new[]
        {
            BuildCommand(1, "itm_deterministic", 1, "mitrn_t070b4_deterministic_0001"),
            BuildCommand(2, "itm_deterministic", 2, "mitrn_t070b4_deterministic_0002")
        };

        var first = Plan(firstFixture, commands);
        var second = Plan(secondFixture, commands);

        AssertPlanEqual(first, second);
        Assert.Equal(firstInput, Describe(firstFixture));
        Assert.Equal(secondInput, Describe(secondFixture));
        AssertFilesEqual(filesBefore, ReadFiles(canonicalRoot));
    }

    [Fact]
    public void Plan_InvalidIssuesAreDeeplyDetachedFromCallerOwnedRepairContext()
    {
        var fixture = CreateFixture("itm_detached_issue", 1);
        var companionTargets = new[] { "game_state/inventory/item_bonds.json" };
        var sourcePath = new[] { "source_container" };
        var destinationPath = new[] { "destination_container" };
        var callerIssue = new ValidationIssue(
            MortalItemIdentityState.StatePath,
            IssueSeverity.Error,
            "Caller-owned invalid identity state.",
            code: "mortal_item_test_invalid_identity",
            repairTargetFiles: companionTargets)
        {
            MortalItemRepairContext = new MortalItemRepairContext(
                "entries[itm_detached_issue]",
                "consume",
                "player_inventory",
                new MortalItemCarrierCoordinate(
                    "player_inventory", "player", "source_container", sourcePath),
                new MortalItemCarrierCoordinate(
                    "player_inventory", "player", "destination_container", destinationPath),
                "expected_authority",
                "actual_evidence",
                companionTargets)
        };
        var identity = fixture.Identity with { Issues = new[] { callerIssue } };

        var result = PlanWithIdentity(fixture, identity,
            BuildCommand(1, fixture.ItemId, 1, "mitrn_t070b4_detached_issue_0001"));

        AssertInvalidEmpty(result);
        var detached = Assert.Single(result.Issues);
        Assert.NotSame(callerIssue, detached);
        Assert.NotSame(
            callerIssue.MortalItemRepairContext!.RequiredCompanionTargets,
            detached.MortalItemRepairContext!.RequiredCompanionTargets);
        Assert.NotSame(
            callerIssue.MortalItemRepairContext.SourceCarrier!.ContainerPath,
            detached.MortalItemRepairContext.SourceCarrier!.ContainerPath);
        Assert.NotSame(
            callerIssue.MortalItemRepairContext.DestinationCarrier!.ContainerPath,
            detached.MortalItemRepairContext.DestinationCarrier!.ContainerPath);
        companionTargets[0] = "caller_mutated_after_planning.json";
        sourcePath[0] = "caller_mutated_source_path";
        destinationPath[0] = "caller_mutated_destination_path";
        callerIssue.MortalItemRepairContext = null;
        Assert.Equal(
            "game_state/inventory/item_bonds.json",
            Assert.Single(detached.MortalItemRepairContext!.RequiredCompanionTargets));
        Assert.Equal(
            "source_container",
            Assert.Single(detached.MortalItemRepairContext.SourceCarrier!.ContainerPath));
        Assert.Equal(
            "destination_container",
            Assert.Single(detached.MortalItemRepairContext.DestinationCarrier!.ContainerPath));
    }

    [Fact]
    public void Plan_CompanionRootPathCollisionRejectsWithoutThrowingOrAfterImages()
    {
        var fixture = CreateFixture("itm_root_collision", 1);
        var cloned = Clone(fixture.Roots);
        var companionRoots = cloned.CompanionRoots.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value,
            StringComparer.Ordinal);
        companionRoots.Add(PlayerPath, new JsonObject
        {
            ["shadow"] = "must not replace the standard root"
        });
        var roots = cloned with { CompanionRoots = companionRoots };

        var result = PlanWithRoots(fixture, roots,
            BuildCommand(1, fixture.ItemId, 1, "mitrn_t070b4_root_collision_0001"));

        AssertInvalidEmpty(result);
        Assert.Contains(result.Issues, issue =>
            issue.Code?.Contains("root", StringComparison.Ordinal) == true ||
            issue.Message.Contains("root", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Plan_FingerprintBindsCompleteIssuePayloadAndCapacityReceiptId()
    {
        var fixture = CreateFixture("itm_fingerprint_payload", 1);
        var input = CreatePlanningInput(
            fixture,
            [BuildCommand(1, fixture.ItemId, 1, "mitrn_t070b4_fingerprint_payload_0001")]);
        var issueA = new ValidationIssue(
            MortalItemIdentityState.StatePath,
            IssueSeverity.Error,
            "First complete issue payload.",
            code: "mortal_item_fingerprint_issue",
            expected: "same",
            actual: "same");
        var issueB = new ValidationIssue(
            MortalItemIdentityState.StatePath,
            IssueSeverity.Error,
            "Second complete issue payload.",
            code: "mortal_item_fingerprint_issue",
            expected: "same",
            actual: "same");
        var coordinate = new ResourceCoordinate(
            "mortal_world", ResourceOwnerKind.Item, fixture.ItemId, "durability");
        var capacityA = new ResourceCapacityIntent(
            "turn_42:mortal_item_consume:0001:itm_fingerprint_payload:durability",
            fixture.Source.SourceKind,
            fixture.Source.SourceId,
            coordinate,
            ResourceCapacityOperation.Retire,
            null,
            null,
            ResourceMutationPhase.RegisteredSystemOutcome,
            70,
            fixture.Source,
            fixture.PolicyFingerprint,
            "receipt_a");
        var capacityB = capacityA with { ReceiptId = "receipt_b" };

        var issueFingerprintA = InvokeFingerprint(input, ResultForFingerprint(
            issues: [issueA]));
        var issueFingerprintB = InvokeFingerprint(input, ResultForFingerprint(
            issues: [issueB]));
        var receiptFingerprintA = InvokeFingerprint(input, ResultForFingerprint(
            capacities: [capacityA]));
        var receiptFingerprintB = InvokeFingerprint(input, ResultForFingerprint(
            capacities: [capacityB]));

        Assert.NotEqual(issueFingerprintA, issueFingerprintB);
        Assert.NotEqual(receiptFingerprintA, receiptFingerprintB);
    }

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

    private static void AssertProjectionRootRegistry(Type owner)
    {
        var property = owner.GetProperty(
            "ProjectionRootPaths",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        Assert.Equal(typeof(IReadOnlyList<string>), property!.PropertyType);
        Assert.Equal(
            ProjectionRootPaths,
            Assert.IsAssignableFrom<IReadOnlyList<string>>(property.GetValue(null))
                .ToArray());
    }

    private static void AssertSnapshotProjectionRoots(
        ProjectionScenario scenario,
        bool vehiclesUseLegacyArrayRoot)
    {
        var current = ReadSnapshotProjectionRoots(
            scenario.Snapshot,
            "CloneCurrentProjectionRoots");
        var backup = ReadSnapshotProjectionRoots(
            scenario.Snapshot,
            "CloneBackupProjectionRoots");
        AssertProjectionInputRoots(scenario.CurrentRoots, current);
        AssertProjectionInputRoots(scenario.BackupRoots, backup);
        var expectedVehicleType = vehiclesUseLegacyArrayRoot
            ? typeof(JsonArray)
            : typeof(JsonObject);
        Assert.Equal(expectedVehicleType,
            current[StorageTransportMoveService.VehiclesPath]!.GetType());
        Assert.Equal(expectedVehicleType,
            backup[StorageTransportMoveService.VehiclesPath]!.GetType());

        current[PlayerPath]!.AsObject()["detachedProbe"] = true;
        var fresh = ReadSnapshotProjectionRoots(
            scenario.Snapshot,
            "CloneCurrentProjectionRoots");
        Assert.False(fresh[PlayerPath]!.AsObject().ContainsKey("detachedProbe"));

        backup[PlayerPath]!.AsObject()["detachedBackupProbe"] = true;
        var freshBackup = ReadSnapshotProjectionRoots(
            scenario.Snapshot,
            "CloneBackupProjectionRoots");
        Assert.False(freshBackup[PlayerPath]!.AsObject()
            .ContainsKey("detachedBackupProbe"));
    }

    private static IReadOnlyDictionary<string, JsonNode?> ReadSnapshotProjectionRoots(
        MortalItemAcceptedTurnNormalizationSnapshot snapshot,
        string methodName)
    {
        var method = snapshot.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null);
        Assert.NotNull(method);
        Assert.Equal(
            typeof(IReadOnlyDictionary<string, JsonNode?>),
            method!.ReturnType);
        return Assert.IsAssignableFrom<IReadOnlyDictionary<string, JsonNode?>>(
            method.Invoke(snapshot, null));
    }

    private static void AssertProjectionInputRoots(
        IReadOnlyDictionary<string, JsonNode?> expected,
        IReadOnlyDictionary<string, JsonNode?> actual)
    {
        Assert.Equal(
            ProjectionRootPaths.OrderBy(static path => path, StringComparer.Ordinal),
            actual.Keys.OrderBy(static path => path, StringComparer.Ordinal));
        Assert.Equal(
            expected.Keys.OrderBy(static path => path, StringComparer.Ordinal),
            actual.Keys.OrderBy(static path => path, StringComparer.Ordinal));
        foreach (var pair in expected)
            Assert.True(JsonNode.DeepEquals(pair.Value, actual[pair.Key]), pair.Key);
    }

    private static Fixture CreateFixture(string id, int count,
        Action<JsonObject>? configureItem = null,
        Action<JsonObject, string>? configureRoot = null,
        ResourceStateEntry? resource = null)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot(id);
        item["count"] = count;
        configureItem?.Invoke(item);
        MortalItemTestFixture.ResealCanonical(item);
        var root = MortalItemTestFixture.CreateCarrier(item, "player_inventory", "player");
        configureRoot?.Invoke(root, id);
        var index = MortalItemTestFixture.CreateIndexForCarrier(item, "player_inventory", "player");
        return BuildFixture(id,
            new MortalItemCarrierCatalogInput(root, null, null, null, null,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)), index, resource);
    }

    private static Fixture CreateNpcEquipmentFixture(
        string id,
        bool legacyEquipmentSurface = false)
    {
        const string ownerId = "npc_terminal_owner";
        var item = MortalItemTestFixture.CreateCanonicalRoot(id);
        item["count"] = 1;
        Populate(item, "equipment");
        item["equipmentSlot"] = "MainHand";
        MortalItemTestFixture.ResealCanonical(item);
        var owner = new JsonObject
        {
            ["NPCId"] = ownerId,
            ["name"] = "Terminal NPC owner",
            ["inventory"] = new JsonArray(item.DeepClone()),
            ["equippedItems"] = new JsonObject
            {
                ["mainHand"] = legacyEquipmentSurface ? null : id,
                ["offHand"] = "itm_npc_equipment_sibling"
            },
            ["equipment"] = new JsonObject
            {
                ["mainHand"] = legacyEquipmentSurface ? id : null,
                ["offHand"] = "itm_npc_legacy_equipment_sibling"
            },
            ["unchanged"] = "preserve owner sibling"
        };
        var npcRoot = new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(owner.DeepClone()),
            ["NPCsInScene"] = new JsonArray(
                owner.DeepClone(),
                new JsonObject
                {
                    ["NPCId"] = "npc_terminal_other",
                    ["name"] = "Other preserved NPC",
                    ["inventory"] = new JsonArray(),
                    ["equippedItems"] = new JsonObject
                    {
                        ["mainHand"] = "itm_other_npc_equipment"
                    },
                    ["unchanged"] = "preserve other NPC"
                })
        };
        var index = MortalItemTestFixture.CreateIndexForCarrier(
            item,
            "npc_inventory",
            ownerId);
        return BuildFixture(
            id,
            new MortalItemCarrierCatalogInput(
                null,
                npcRoot,
                null,
                null,
                null,
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)),
            index,
            null);
    }

    private static MortalItemPublicationBaselineInput CreateBaselineInput(
        JsonObject npcRoot,
        JsonObject? backupNpcRoot = null)
    {
        const string itemId = "itm_baseline_player";
        var fixture = CreateFixture(itemId, 1);
        var roots = ProjectionRootPaths.ToDictionary(
            static path => path,
            static _ => (JsonNode?)null,
            StringComparer.Ordinal);
        roots[PlayerPath] = fixture.Roots.PlayerInventory!.DeepClone();
        roots[NpcPath] = npcRoot.DeepClone();
        roots[MortalItemIdentityState.StatePath] = fixture.Index.DeepClone();
        var itemPhase = new MortalItemCanonicalProjectionResult(
            roots,
            fixture.Index.DeepClone().AsObject(),
            Array.Empty<ValidationIssue>(),
            Fingerprint("baseline_item_phase"));
        var backupRoots = roots.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value?.DeepClone(),
            StringComparer.Ordinal);
        if (backupNpcRoot != null)
            backupRoots[NpcPath] = backupNpcRoot.DeepClone();
        return new MortalItemPublicationBaselineInput(
            itemPhase,
            new MortalTreatmentItemCommandEnvelope(
                null,
                null,
                null,
                null,
                null,
                null,
                null),
            new NpcCoreChangesContract.Authority(
                new HashSet<string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                new Dictionary<string, string>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal)),
            new CanonicalBeforeImage(false, null),
            new CanonicalBeforeImage(false, null),
            MortalItemNpcTradeTailDisposition.SkipUntouchedTreatmentContinuation,
            backupRoots);
    }

    private static JsonObject CreateMirroredNpcRoot()
    {
        var npc = new JsonObject
        {
            ["NPCId"] = "npc_baseline_mirror",
            ["name"] = "Baseline mirror",
            ["inventory"] = new JsonArray(),
            ["equippedItems"] = new JsonObject(),
            ["unchanged"] = "preserve baseline mirror"
        };
        return new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(npc.DeepClone()),
            ["NPCsInScene"] = new JsonArray(npc.DeepClone())
        };
    }

    private static Companion CreateCompanionFixture(string kind)
    {
        var id = "itm_companion_" + kind;
        var item = MortalItemTestFixture.CreateCanonicalRoot(id);
        item["count"] = 1;
        var companions = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        JsonObject player;
        JsonObject index;
        string path;
        if (kind == "container")
        {
            Populate(item, "container");
            item["isContainer"] = true;
            item["capacity"] = 10;
            MortalItemTestFixture.ResealCanonical(item);
            var child = MortalItemTestFixture.CreateCanonicalRoot("itm_companion_container_child");
            child["contentsPath"] = new JsonArray(id);
            MortalItemTestFixture.ResealCanonical(child);
            player = new JsonObject
            {
                ["items"] = new JsonArray(item.DeepClone(), child.DeepClone()),
                ["equippedItems"] = new JsonObject()
            };
            index = MergeIndexes(
                MortalItemTestFixture.CreateIndexForCarrier(item, "player_inventory", "player"),
                MortalItemTestFixture.CreateIndexForCarrier(child, "player_inventory", "player",
                    containerPath: new JsonArray(id)));
            path = PlayerPath;
        }
        else
        {
            if (kind == "quest")
            {
                Populate(item, "questRole");
                item["questLinks"] = new JsonArray("quest_t070b4");
                path = "game_state/quests/quest_history.json";
                companions[path] = new JsonObject
                {
                    ["questHistory"] = new JsonArray(),
                    ["questRewards"] = new JsonArray(new JsonObject
                    {
                        ["questId"] = "quest_t070b4",
                        ["itemsReceived"] = new JsonArray(new JsonObject { ["itemId"] = id })
                    }),
                    ["questChains"] = new JsonArray()
                };
            }
            else if (kind == "bond")
            {
                Populate(item, "bondsAndFateCards");
                item["ownerBondLevelCurrent"] = 1;
                item["ownerBondLevelMax"] = 10;
                path = "game_state/inventory/item_bonds.json";
                companions[path] = new JsonObject
                {
                    ["itemBondLevelChanges"] = new JsonArray(new JsonObject
                    {
                        ["itemId"] = id, ["newBondLevel"] = 1,
                        ["changeReason"] = "T070-B.4 durable bond fixture."
                    })
                };
            }
            else if (kind == "other")
            {
                path = "game_state/npcs/item_journals.json";
                companions[path] = new JsonObject
                {
                    ["entries"] = new JsonArray(new JsonObject
                    {
                        ["itemId"] = id,
                        ["journalEntries"] = new JsonArray(new JsonObject
                        {
                            ["turn"] = 41, ["description"] = "Durable journal reference."
                        })
                    })
                };
            }
            else throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            MortalItemTestFixture.ResealCanonical(item);
            player = MortalItemTestFixture.CreateCarrier(item, "player_inventory", "player");
            index = MortalItemTestFixture.CreateIndexForCarrier(item, "player_inventory", "player");
        }
        return new Companion(path, BuildFixture(id,
            new MortalItemCarrierCatalogInput(player, null, null, null, null, companions),
            index, null));
    }

    private static Fixture BuildFixture(string id, MortalItemCarrierCatalogInput roots,
        JsonObject index, ResourceStateEntry? resource)
    {
        var identity = MortalItemIdentityState.Parse(index.ToJsonString());
        Assert.Empty(identity.Issues);
        return new Fixture(id, roots, index, identity, ResourceDefinitionCatalog.CreateBuiltIn(),
            new ResourceStateLedger(resource == null ? [] : [resource]),
            new ResourceSourceEvidence("mortal_wound_treatment", "mwta_t070b4_attempt",
                Fingerprint("capacity_source")), Fingerprint("capacity_policy"));
    }

    private static ResourceStateEntry Resource(string id, decimal maximum, decimal current,
        ResourceCapacityKind kind = ResourceCapacityKind.InstanceFixed) => new(
        new ResourceCoordinate("mortal_world", ResourceOwnerKind.Item, id, "durability"),
        current, maximum,
        new ResourceCapacityBinding(kind, "capacity_" + id + "_durability",
            Fingerprint("binding_" + id + "_" + kind)),
        ResourceLifecycleState.Active,
        new ResourceChronology(41, "turn_41:resource:item_durability",
            "resource_transition_initialize_" + id,
            "turn_41:resource:item_durability", 41));

    private static Command BuildCommand(int ordinal, string id, int quantity, string transitionId) =>
        new(ordinal, id, quantity, Fingerprint("claim_" + ordinal + "_" + id), transitionId,
            "mortal_wound_treatment", "mwta_t070b4_finalization_" + ordinal);

    private static Result Plan(Fixture fixture, params Command[] commands) =>
        InvokePlan(CreatePlanningInput(fixture, commands));

    private static Result PlanWithIdentity(
        Fixture fixture,
        MortalItemIdentityParseResult identity,
        params Command[] commands) =>
        InvokePlan(CreatePlanningInput(fixture, commands, identityState: identity));

    private static Result PlanWithRoots(
        Fixture fixture,
        MortalItemCarrierCatalogInput roots,
        params Command[] commands) =>
        InvokePlan(CreatePlanningInput(fixture, commands, carrierRoots: roots));

    private static object CreatePlanningInput(
        Fixture fixture,
        IReadOnlyList<Command> commands,
        MortalItemCarrierCatalogInput? carrierRoots = null,
        MortalItemIdentityParseResult? identityState = null)
    {
        var inputType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningInput");
        var commandType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionCommand");
        var resultType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningResult");
        PlanShape(inputType, commandType, resultType);
        var commandArray = Array.CreateInstance(commandType, commands.Count);
        var commandCtor = Ctor(commandType, typeof(int), typeof(string), typeof(int),
            typeof(string), typeof(string), typeof(string), typeof(string));
        for (var i = 0; i < commands.Count; i++)
        {
            var value = commands[i];
            commandArray.SetValue(commandCtor.Invoke([
                value.Ordinal, value.ItemId, value.Quantity, value.ClaimFingerprint,
                value.TransitionId, value.AuthorityKind, value.AuthorityId]), i);
        }
        var input = Ctor(inputType, typeof(int), typeof(string),
            typeof(MortalItemCarrierCatalogInput), typeof(MortalItemIdentityParseResult),
            typeof(IReadOnlyList<>).MakeGenericType(commandType),
            typeof(ResourceDefinitionCatalog), typeof(ResourceStateLedger),
            typeof(ResourceSourceEvidence), typeof(string)).Invoke([
                Turn, Fingerprint("baseline_" + fixture.ItemId),
                carrierRoots ?? Clone(fixture.Roots),
                identityState ?? MortalItemIdentityState.Parse(fixture.Index.ToJsonString()), commandArray,
                fixture.Definitions, new ResourceStateLedger(fixture.State.Entries),
                fixture.Source, fixture.PolicyFingerprint]);
        return input;
    }

    private static Result InvokePlan(object input)
    {
        var planner = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanner");
        var inputType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningInput");
        var resultType = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanningResult");
        var method = ExactMethod(planner, "Plan", inputType, resultType);
        return ReadResult(method.Invoke(null, [input])!);
    }

    private static object ResultForFingerprint(
        IReadOnlyList<ResourceCapacityIntent>? capacities = null,
        IReadOnlyList<ValidationIssue>? issues = null) =>
        new MortalItemConsumptionPlanningResult(
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            null,
            Array.Empty<JsonObject>(),
            capacities ?? Array.Empty<ResourceCapacityIntent>(),
            Array.Empty<ResourceOwnerKey>(),
            issues ?? Array.Empty<ValidationIssue>(),
            string.Empty);

    private static string InvokeFingerprint(object input, object result)
    {
        var planner = ExactType("BookOfEternityClient.Services.MortalItemConsumptionPlanner");
        var method = Assert.Single(planner.GetMethods(
            BindingFlags.Static | BindingFlags.NonPublic), value =>
            value.Name == "Fingerprint" && value.GetParameters().Length == 2);
        return Assert.IsType<string>(method.Invoke(null, [input, result]));
    }

    private static Result ReadResult(object value) => new(
        Read<IReadOnlyDictionary<string, JsonObject>>(value, "CarrierAfterImages"),
        ReadNullableObject(value, "IdentityIndexAfterImage"),
        Read<IReadOnlyList<JsonObject>>(value, "IdentityTransitions"),
        Read<IReadOnlyList<ResourceCapacityIntent>>(value, "CapacityTransitions"),
        Read<IReadOnlyList<ResourceOwnerKey>>(value, "TerminalOwners"),
        Read<IReadOnlyList<ValidationIssue>>(value, "Issues"),
        Read<string>(value, "Fingerprint"),
        Read<bool>(value, "IsValid"));

    private static void PlanShape(Type input, Type command, Type result)
    {
        Assert.True(input.IsSealed && command.IsSealed && result.IsSealed);
        Property(input, "Turn", typeof(int));
        Property(input, "BaselineFingerprint", typeof(string));
        Property(input, "CarrierRoots", typeof(MortalItemCarrierCatalogInput));
        Property(input, "IdentityState", typeof(MortalItemIdentityParseResult));
        Property(input, "Commands", typeof(IReadOnlyList<>).MakeGenericType(command));
        Property(input, "Definitions", typeof(ResourceDefinitionCatalog));
        Property(input, "ResourceState", typeof(ResourceStateLedger));
        Property(input, "CapacitySourceEvidence", typeof(ResourceSourceEvidence));
        Property(input, "CapacityPolicyFingerprint", typeof(string));
        Property(command, "FinalizationOrdinal", typeof(int));
        Property(command, "ItemId", typeof(string));
        Property(command, "Quantity", typeof(int));
        Property(command, "ClaimFingerprint", typeof(string));
        Property(command, "TransitionId", typeof(string));
        Property(command, "AuthorityKind", typeof(string));
        Property(command, "AuthorityId", typeof(string));
        Property(result, "CarrierAfterImages", typeof(IReadOnlyDictionary<string, JsonObject>));
        Property(result, "IdentityIndexAfterImage", typeof(JsonObject));
        Property(result, "IdentityTransitions", typeof(IReadOnlyList<JsonObject>));
        Property(result, "CapacityTransitions", typeof(IReadOnlyList<ResourceCapacityIntent>));
        Property(result, "TerminalOwners", typeof(IReadOnlyList<ResourceOwnerKey>));
        Property(result, "Issues", typeof(IReadOnlyList<ValidationIssue>));
        Property(result, "Fingerprint", typeof(string));
        Property(result, "IsValid", typeof(bool));
    }

    private static async Task<ProjectionScenario> ProductionProjectionScenarioAsync(
        bool vehiclesUseLegacyArrayRoot)
    {
        const int turn = 43;
        const string sessionId = "session_t070b4_projection";
        const string creationRef = "new_item_projection_created";
        const string transferredId = "itm_projection_transferred";
        const string npcId = "npc_projection";
        var root = Path.Combine(Path.GetTempPath(),
            "boe-t070b4-projection-" + Guid.NewGuid().ToString("N"));
        var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        try
        {
            var fileSystem = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            fileSystem.EnsureDirectoryStructure();
            await SeedProjectionBootstrapAsync(fileSystem);
            await WriteJsonAsync(fileSystem, NpcCommandsPath, new JsonObject());
            await WriteJsonAsync(
                fileSystem,
                MortalItemAcceptedTransferCatalog.PlayerRemovalPath,
                new JsonObject());
            await WriteJsonAsync(
                fileSystem,
                MortalLocationStorageContentsState.StatePath,
                MortalLocationStorageContentsState.CreateEmptyRoot());
            await WriteJsonAsync(
                fileSystem,
                StorageTransportMoveService.VehiclesPath,
                vehiclesUseLegacyArrayRoot
                    ? new JsonArray()
                    : new JsonObject { ["vehicles"] = new JsonArray() });
            await WriteJsonAsync(
                fileSystem,
                "game_state/quests/quest_history.json",
                new JsonObject { ["questHistory"] = new JsonArray() });
            var transferred = MortalItemTestFixture.CreateCanonicalRootAtTurn(
                transferredId, 42, "npc_acquisition", "npc_inventory_add",
                "npc_inventory_add:42:0:npc_projection",
                name: "Transferred projection item");
            var npcBefore = ProjectionNpcRoot(npcId, transferred);
            var indexRoot = MortalItemTestFixture.CreateIndexForCarrier(
                transferred, "npc_inventory", npcId);
            await WriteJsonAsync(fileSystem, NpcPath, npcBefore);
            await WriteJsonAsync(fileSystem, MortalItemIdentityState.StatePath, indexRoot);
            var backup = await ReadProjectionRootsAsync(fileSystem);
            await CapturePendingSnapshotAsync(fileSystem, sessionId, turn);

            var playerBeforeJson = await fileSystem.ReadFileAsync(PlayerPath);
            Assert.NotNull(playerBeforeJson);
            var playerBefore = Assert.IsType<JsonObject>(JsonNode.Parse(
                playerBeforeJson!));
            var raw = MortalItemTestFixture.CreateRawRoot(
                "player_acquisition", "turn_outcome", $"turn_{turn}", turn,
                creationRef, "mat_item_projection_created");
            var playerCurrent = playerBefore.DeepClone().AsObject();
            playerCurrent["UpdateInventory"] = new JsonArray(
                transferred.DeepClone(), raw.DeepClone());
            var commandsCurrent = new JsonObject
            {
                ["NPCInventoryRemovals"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = npcId,
                    ["NPCName"] = "Projection owner",
                    ["itemId"] = transferredId
                })
            };
            await WriteJsonAsync(fileSystem, PlayerPath, playerCurrent);
            await WriteJsonAsync(fileSystem, NpcCommandsPath, commandsCurrent);

            var validator = new ValidationService(
                fileSystem, NullLogger<ValidationService>.Instance);
            var validationIssues = await validator
                .ValidateAcceptedTurnRawMortalItemMaterializationAsync();
            Assert.True(
                validationIssues.All(issue => issue.Severity != IssueSeverity.Error),
                string.Join(" | ", validationIssues.Select(issue =>
                    $"{issue.Code}@{issue.FilePath}: {issue.Expected} -> {issue.Actual}")));

            MortalItemAcceptedTurnNormalizationSnapshot snapshot;
            string createdId;
            var snapshotToken = await PendingSnapshotTokenAsync(fileSystem);
            await using (var lease = await fileSystem.AcquireCanonicalWriteLeaseAsync())
            {
                Assert.True(MortalItemAcceptedTurnAuthority.TryGetAllocatedItemId(
                    fileSystem, lease, sessionId, snapshotToken,
                    creationRef, out createdId));
                Assert.True(MortalItemAcceptedTurnAuthority.TryCaptureNormalizationSnapshot(
                    fileSystem, lease, sessionId, snapshotToken,
                    turn, out snapshot));
            }
            var routes = await MortalItemRouteAuthorityCatalog.BuildAsync(fileSystem);
            Assert.Empty(routes.Issues);
            var current = await ReadProjectionRootsAsync(fileSystem);
            return new ProjectionScenario(turn, snapshot, routes, current, backup,
                indexRoot, createdId, creationRef, transferredId);
        }
        finally
        {
            var fullRoot = Path.GetFullPath(root);
            if (!fullRoot.StartsWith(expectedParent, StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(fullRoot).StartsWith(
                    "boe-t070b4-projection-", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unsafe projection test root '{fullRoot}'.");
            if (Directory.Exists(fullRoot)) Directory.Delete(fullRoot, recursive: true);
        }
    }

    private static MortalItemCarrierCatalogInput FiveCollectorInput(
        int turn,
        IReadOnlyList<string> creationRefs)
    {
        Assert.Equal(5, creationRefs.Count);
        var player = Raw(
            creationRefs[0],
            "player_acquisition",
            "turn_outcome",
            $"turn_{turn}");
        var npcCoreItem = Raw(
            creationRefs[1],
            "new_npc_inventory",
            "new_npc",
            "npc_five_new");
        var npcCommandItem = Raw(
            creationRefs[2],
            "npc_acquisition",
            "npc_inventory_add",
            $"npc_inventory_add:{turn}:0:npc_five_command");
        var currentLocationItem = Raw(
            creationRefs[3],
            "storage_placement",
            "location_storage",
            "loc_five_storage:storage_five_current");
        var offscreenItem = Raw(
            creationRefs[4],
            "storage_placement",
            "location_storage",
            "loc_five_storage:storage_five_offscreen");
        var mirroredCommandOwner = new JsonObject
        {
            ["NPCId"] = "npc_five_command",
            ["name"] = "Five collector mirrored command owner",
            ["inventory"] = new JsonArray(),
            ["equippedItems"] = new JsonObject(),
            ["equipment"] = new JsonObject()
        };

        return new MortalItemCarrierCatalogInput(
            new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equippedItems"] = new JsonObject(),
                ["UpdateInventory"] = new JsonArray(player)
            },
            new JsonObject
            {
                ["UpdateNPCs"] = new JsonArray(
                    new JsonObject
                    {
                        ["initialId"] = "npc_five_new",
                        ["name"] = "Five collector new NPC",
                        ["inventory"] = new JsonArray(npcCoreItem),
                        ["equippedItems"] = new JsonObject()
                    },
                    mirroredCommandOwner.DeepClone()),
                ["NPCsInScene"] = new JsonArray(mirroredCommandOwner.DeepClone())
            },
            new JsonObject
            {
                ["NPCInventoryAdds"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_five_command",
                    ["NPCName"] = "Five collector command owner",
                    ["item"] = npcCommandItem,
                    ["destinationContainerId"] = null
                }),
                ["NPCEquipmentChanges"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_five_command",
                    ["itemId"] = creationRefs[2],
                    ["action"] = "equip",
                    ["targetSlots"] = new JsonArray("mainHand")
                })
            },
            new JsonObject
            {
                ["locationId"] = "loc_five_storage",
                ["locationStorages"] = new JsonArray(
                    new JsonObject
                    {
                        ["storageId"] = "storage_five_current",
                        ["contents"] = new JsonArray(currentLocationItem)
                    },
                    new JsonObject
                    {
                        ["storageId"] = "storage_five_offscreen",
                        ["contents"] = new JsonArray()
                    })
            },
            null,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            MortalLocationStorageContentsState.BuildCanonicalRoot(
                new Dictionary<MortalLocationStorageKey, JsonArray>
                {
                    [new MortalLocationStorageKey(
                        "loc_five_storage",
                        "storage_five_offscreen")] = new JsonArray(offscreenItem)
                }));

        JsonObject Raw(
            string creationRef,
            string route,
            string authorityKind,
            string authorityId) =>
            MortalItemTestFixture.CreateRawRoot(
                route,
                authorityKind,
                authorityId,
                turn,
                creationRef,
                "mat_" + creationRef);
    }

    private static IReadOnlyDictionary<string, JsonNode?> FiveCollectorProjectionRoots(
        MortalItemCarrierCatalogInput input)
    {
        var roots = ProjectionRootPaths.ToDictionary(
            static path => path,
            static _ => (JsonNode?)null,
            StringComparer.Ordinal);
        roots[PlayerPath] = input.PlayerInventory!.DeepClone();
        roots[NpcPath] = input.NpcCore!.DeepClone();
        roots[NpcCommandsPath] = input.NpcInventoryCommands!.DeepClone();
        roots[MortalItemAcceptedTransferCatalog.PlayerRemovalPath] = new JsonObject();
        roots[StorageTransportMoveService.CurrentLocationPath] =
            input.CurrentLocation!.DeepClone();
        roots[MortalLocationStorageContentsState.StatePath] =
            input.OffscreenLocationStorageContents!.DeepClone();
        roots[StorageTransportMoveService.VehiclesPath] =
            new JsonObject { ["vehicles"] = new JsonArray() };
        roots[MortalItemIdentityState.StatePath] =
            MortalItemIdentityState.CreateEmptyRoot();
        roots["game_state/quests/quest_history.json"] =
            new JsonObject { ["questHistory"] = new JsonArray() };
        roots["game_state/inventory/item_bonds.json"] = new JsonObject();
        roots["game_state/inventory/item_text_updates.json"] = new JsonObject();
        roots["game_state/npcs/item_journals.json"] = new JsonObject();
        return roots;
    }

    private static async Task SeedFiveCollectorRouteAuthorityAsync(
        FileSystemManager fileSystem,
        MortalItemCarrierCatalogInput input,
        int turn)
    {
        await WriteJsonAsync(fileSystem, PlayerPath, input.PlayerInventory!);
        await WriteJsonAsync(fileSystem, NpcPath, input.NpcCore!);
        await WriteJsonAsync(fileSystem, NpcCommandsPath, input.NpcInventoryCommands!);
        await WriteJsonAsync(
            fileSystem,
            StorageTransportMoveService.CurrentLocationPath,
            input.CurrentLocation!);
        await WriteJsonAsync(
            fileSystem,
            MortalLocationStorageContentsState.StatePath,
            input.OffscreenLocationStorageContents!);
        await WriteJsonAsync(
            fileSystem,
            StorageTransportMoveService.VehiclesPath,
            new JsonObject { ["vehicles"] = new JsonArray() });
        await WriteJsonAsync(
            fileSystem,
            "game_state/quests/quest_history.json",
            new JsonObject { ["questHistory"] = new JsonArray() });
        await WriteJsonAsync(fileSystem, "input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "session_t070b4_five_collectors",
            ["requestId"] = "request_t070b4_five_collectors",
            ["turnNumber"] = turn,
            ["playerAction"] = "Validate all five item creation collectors."
        });

        const string npcSnapshotPath =
            "game_state/control/five_collectors_snapshot/npc_core.json";
        const string locationSnapshotPath =
            "game_state/control/five_collectors_snapshot/current_location.json";
        var mirroredCommandOwner = new JsonObject
        {
            ["NPCId"] = "npc_five_command",
            ["name"] = "Five collector mirrored command owner",
            ["inventory"] = new JsonArray(),
            ["equippedItems"] = new JsonObject(),
            ["equipment"] = new JsonObject()
        };
        await WriteJsonAsync(fileSystem, npcSnapshotPath, new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(mirroredCommandOwner.DeepClone()),
            ["NPCsInScene"] = new JsonArray(mirroredCommandOwner.DeepClone())
        });
        await WriteJsonAsync(fileSystem, locationSnapshotPath, new JsonObject
        {
            ["locationId"] = "loc_five_storage",
            ["locationStorages"] = new JsonArray(
                new JsonObject
                {
                    ["storageId"] = "storage_five_current",
                    ["contents"] = new JsonArray()
                },
                new JsonObject
                {
                    ["storageId"] = "storage_five_offscreen",
                    ["contents"] = new JsonArray()
                })
        });
        await WriteJsonAsync(
            fileSystem,
            "game_state/control/pending_turn_snapshot.json",
            new JsonObject
            {
                ["files"] = new JsonObject
                {
                    [NpcPath] = npcSnapshotPath,
                    [StorageTransportMoveService.CurrentLocationPath] =
                        locationSnapshotPath
                }
            });
    }

    private static void RegisterFiveCollectorItems(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease lease,
        string sessionId,
        string snapshotToken,
        MortalItemCarrierCatalog catalog,
        MortalItemRouteAuthorityCatalog routeCatalog,
        IReadOnlyDictionary<string, JsonNode?> currentRoots,
        IReadOnlyDictionary<string, JsonNode?> backupRoots)
    {
        var method = Assert.Single(typeof(MortalItemAcceptedTurnAuthority).GetMethods(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
            static candidate => string.Equals(
                candidate.Name,
                "RegisterValidatedItems",
                StringComparison.Ordinal));
        var parameters = method.GetParameters();
        Assert.Equal(10, parameters.Length);
        var arguments = new object?[parameters.Length];
        arguments[0] = fileSystem;
        arguments[1] = lease;
        arguments[2] = sessionId;
        arguments[3] = snapshotToken;
        arguments[4] = catalog;
        arguments[5] = Array.Empty<string>();
        arguments[6] = routeCatalog;
        arguments[7] = null;
        arguments[8] = ConvertProjectionRoots(
            parameters[8].ParameterType,
            currentRoots);
        arguments[9] = ConvertProjectionRoots(
            parameters[9].ParameterType,
            backupRoots);
        method.Invoke(null, arguments);
    }

    private static object ConvertProjectionRoots(
        Type targetType,
        IReadOnlyDictionary<string, JsonNode?> roots)
    {
        var dictionaryInterface = targetType.IsGenericType
            ? targetType
            : Assert.Single(targetType.GetInterfaces(), static candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));
        Assert.Equal(typeof(IReadOnlyDictionary<,>),
            dictionaryInterface.GetGenericTypeDefinition());
        var genericArguments = dictionaryInterface.GetGenericArguments();
        Assert.Equal(typeof(string), genericArguments[0]);
        Assert.True(genericArguments[1] == typeof(JsonObject) ||
                    genericArguments[1] == typeof(JsonNode));
        var dictionaryType = typeof(Dictionary<,>).MakeGenericType(genericArguments);
        var result = Assert.IsAssignableFrom<IDictionary>(
            Activator.CreateInstance(dictionaryType));
        foreach (var pair in roots)
        {
            var node = pair.Value?.DeepClone();
            Assert.True(node is null || genericArguments[1].IsInstanceOfType(node));
            result.Add(pair.Key, node);
        }
        return result;
    }

    private static string ExpectedAcceptedCreationIdentityId(
        string prefix,
        string domain,
        string sessionId,
        string snapshotToken,
        int turn,
        string creationRef,
        MortalItemRouteAuthority route,
        int ordinal)
    {
        var routeFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_item.route_authority",
            "1",
            route.Route,
            route.AuthorityKind,
            route.AuthorityId,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(new JsonObject
            {
                ["kind"] = route.Destination.Kind,
                ["ownerId"] = route.Destination.OwnerId,
                ["containerId"] = route.Destination.ContainerId,
                ["containerPath"] = new JsonArray(route.Destination.ContainerPath
                    .Select(static value => (JsonNode?)value)
                    .ToArray())
            }),
            string.Join("\0", route.SourceItemIds)
        });
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_item.accepted_turn_identity",
            "1",
            domain,
            sessionId,
            snapshotToken,
            turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
            creationRef,
            routeFingerprint,
            ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
        return prefix + fingerprint["sha256:".Length..];
    }

    private static object ProjectionInput(
        Type inputType,
        ProjectionScenario scenario,
        IReadOnlyDictionary<string, JsonNode?>? currentRoots = null,
        IReadOnlyDictionary<string, JsonNode?>? backupRoots = null,
        JsonObject? identityRoot = null)
    {
        var current = (currentRoots ?? scenario.CurrentRoots).ToDictionary(pair => pair.Key,
            pair => pair.Value?.DeepClone(), StringComparer.Ordinal);
        var backup = (backupRoots ?? scenario.BackupRoots).ToDictionary(pair => pair.Key,
            pair => pair.Value?.DeepClone(), StringComparer.Ordinal);
        var identity = MortalItemIdentityState.Parse(
            (identityRoot ?? scenario.IdentityRoot).ToJsonString());
        Assert.Empty(identity.Issues);
        return Ctor(inputType, typeof(int),
            typeof(MortalItemAcceptedTurnNormalizationSnapshot),
            typeof(MortalItemRouteAuthorityCatalog),
            typeof(IReadOnlyDictionary<string, JsonNode?>),
            typeof(IReadOnlyDictionary<string, JsonNode?>),
            typeof(MortalItemIdentityParseResult)).Invoke([
                scenario.Turn, scenario.Snapshot, scenario.RouteCatalog,
                current, backup, identity]);
    }

    private static async Task<IReadOnlyDictionary<string, JsonNode?>>
        ReadProjectionRootsAsync(FileSystemManager fileSystem)
    {
        var roots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var path in ProjectionRootPaths)
        {
            var json = await fileSystem.ReadFileAsync(path);
            roots.Add(path, json == null ? null : JsonNode.Parse(json));
        }
        return roots;
    }

    private static async Task SeedProjectionBootstrapAsync(FileSystemManager fileSystem)
    {
        var files = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(
            1, 1, "Projection test mortal.", "Projection test world.",
            "Projection test beginning.",
            DateTimeOffset.Parse("2026-08-11T00:00:00Z"));
        foreach (var pair in files) await WriteJsonAsync(fileSystem, pair.Key, pair.Value);
        var resources = ResourceBootstrapStateBuilder.BuildPristine();
        Assert.True(resources.IsValid, string.Join(Environment.NewLine, resources.Issues));
        await WriteJsonAsync(fileSystem, ResourceMaterializationContract.DefinitionsPath,
            JsonNode.Parse(resources.Definitions!.ToCanonicalJson())!);
        await WriteJsonAsync(fileSystem, ResourceMaterializationContract.StatePath,
            JsonNode.Parse(resources.State!.ToCanonicalJson())!);
        await WriteJsonAsync(fileSystem, ResourceMaterializationContract.HistoryPath,
            JsonNode.Parse(resources.History!.ToCanonicalJson())!);
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            resources.Definitions, fileSystem.ReadFileAsync, resources.State,
            resources.History, CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        Assert.True(authority.IsValid, string.Join(Environment.NewLine, authority.Issues));
        await WriteJsonAsync(fileSystem, CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            JsonNode.Parse(authority.CanonicalAuthorityJson!)!);
    }

    private static async Task CapturePendingSnapshotAsync(
        FileSystemManager fileSystem, string sessionId, int turn)
    {
        const string requestId = "request_t070b4_projection";
        const string playerAction = "Validate projection create and transfer.";
        await WriteJsonAsync(fileSystem, "input/turn_request.json", new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turn,
            ["playerAction"] = playerAction
        });
        var files = new JsonObject();
        var hashes = new JsonObject();
        var baselines = new JsonArray();
        foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.Ordinal))
        {
            var bytes = await fileSystem.ReadFileBytesAsync(path);
            if (bytes == null) continue;
            var snapshotPath = $"game_state/control/pending_turn_snapshot/{path}";
            await fileSystem.WriteFileAtomicBytesAsync(snapshotPath, bytes);
            files[path] = snapshotPath;
            hashes[path] = PendingTurnSnapshotAuthority.ComputeSha256(bytes);
            baselines.Add(path);
        }
        var manifest = new JsonObject
        {
            ["sessionId"] = sessionId,
            ["requestId"] = requestId,
            ["turnNumber"] = turn,
            ["requestTimestamp"] = "2026-08-11T00:00:00Z",
            ["playerAction"] = playerAction,
            ["files"] = files,
            ["snapshotFileHashes"] = hashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = new JsonObject(),
            ["rollbackBaselineFiles"] = baselines,
            ["sourceLabel"] = "T070-B.4 projection oracle",
            ["manifestPayloadHash"] = string.Empty
        };
        manifest["manifestPayloadHash"] =
            PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await WriteJsonAsync(fileSystem,
            "game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority
            .SyncAuthorityForCurrentManifestAsync(fileSystem);
    }

    private static async Task<string> PendingSnapshotTokenAsync(FileSystemManager fileSystem)
    {
        var json = await fileSystem.ReadFileAsync(
            "game_state/control/pending_turn_snapshot.json");
        Assert.NotNull(json);
        return JsonNode.Parse(json!)!["manifestPayloadHash"]!.GetValue<string>();
    }

    private static JsonObject ProjectionNpcRoot(string npcId, JsonObject item) => new()
    {
        ["UpdateNPCs"] = new JsonArray(),
        ["NPCsInScene"] = new JsonArray(new JsonObject
        {
            ["NPCId"] = npcId,
            ["name"] = "Projection owner",
            ["inventory"] = new JsonArray(item.DeepClone()),
            ["equippedItems"] = new JsonObject
            {
                ["mainHand"] = item["itemId"]!.GetValue<string>()
            }
        })
    };

    private static Task WriteJsonAsync(
        FileSystemManager fileSystem, string path, JsonNode node) =>
        fileSystem.WriteFileAtomicAsync(path, node.ToJsonString());

    private static string SnapshotOwnedMapValue(
        MortalItemAcceptedTurnNormalizationSnapshot snapshot,
        string expectedKey,
        string valuePrefix)
    {
        var matches = new HashSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Visit(snapshot);
        return Assert.Single(matches);

        void Visit(object? value)
        {
            if (value == null || value is string || !visited.Add(value)) return;
            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    Match(entry.Key, entry.Value);
                    Visit(entry.Key); Visit(entry.Value);
                }
                return;
            }
            if (value is IEnumerable enumerable)
            {
                foreach (var entry in enumerable)
                {
                    if (entry != null)
                    {
                        var pairType = entry.GetType();
                        if (pairType.IsGenericType &&
                            pairType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                            Match(pairType.GetProperty("Key")!.GetValue(entry),
                                pairType.GetProperty("Value")!.GetValue(entry));
                    }
                    Visit(entry);
                }
                return;
            }
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum || type == typeof(decimal) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset)) return;
            foreach (var field in type.GetFields(
                         BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                Visit(field.GetValue(value));
        }

        void Match(object? key, object? value)
        {
            if (key is string text && value is string id &&
                string.Equals(text, expectedKey, StringComparison.Ordinal) &&
                id.StartsWith(valuePrefix, StringComparison.Ordinal))
                matches.Add(id);
        }
    }

    private static void ProjectionInputShape(Type type)
    {
        Property(type, "Turn", typeof(int));
        Property(type, "Snapshot", typeof(MortalItemAcceptedTurnNormalizationSnapshot));
        Property(type, "RouteCatalog", typeof(MortalItemRouteAuthorityCatalog));
        Property(type, "CurrentRoots", typeof(IReadOnlyDictionary<string, JsonNode?>));
        Property(type, "BackupRoots", typeof(IReadOnlyDictionary<string, JsonNode?>));
        Property(type, "IdentityState", typeof(MortalItemIdentityParseResult));
    }

    private static void ProjectionResultShape(Type type)
    {
        Property(type, "ItemPhaseAfterImages", typeof(IReadOnlyDictionary<string, JsonNode?>));
        Property(type, "IdentityIndexAfterImage", typeof(JsonObject));
        Property(type, "Issues", typeof(IReadOnlyList<ValidationIssue>));
        Property(type, "Fingerprint", typeof(string));
        Property(type, "IsValid", typeof(bool));
    }

    private static ProjectionOutput ProjectionResult(object value) => new(
        Read<IReadOnlyDictionary<string, JsonNode?>>(value, "ItemPhaseAfterImages"),
        Read<JsonObject>(value, "IdentityIndexAfterImage"),
        Read<IReadOnlyList<ValidationIssue>>(value, "Issues"),
        Read<string>(value, "Fingerprint"),
        Read<bool>(value, "IsValid"));

    private static void AssertProjectionInvalidEmpty(ProjectionOutput result)
    {
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
        Assert.Empty(result.Roots);
        AssertFingerprint(result.Fingerprint);
    }

    private static void AssertValid(Result result)
    {
        Assert.True(result.IsValid, string.Join(
            Environment.NewLine,
            result.Issues.Select(issue => $"{issue.Code}: {issue.Actual}")));
        Assert.Empty(result.Issues);
        Assert.NotNull(result.Index);
        Assert.NotEmpty(result.Roots);
        AssertFingerprint(result.Fingerprint);
    }

    private static void AssertInvalidEmpty(Result result)
    {
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Issues);
        Assert.Empty(result.Roots);
        Assert.Null(result.Index);
        Assert.Empty(result.Transitions);
        Assert.Empty(result.Capacities);
        Assert.Empty(result.TerminalOwners);
        AssertFingerprint(result.Fingerprint);
    }

    private static void AssertConsume(JsonObject value, string transitionId, string itemId,
        int before, int after, JsonNode source, JsonNode? destination)
    {
        Assert.Equal(transitionId, value["transitionId"]!.GetValue<string>());
        Assert.Equal("consume", value["kind"]!.GetValue<string>());
        Assert.Equal(Turn, value["turn"]!.GetValue<int>());
        Assert.Equal(new[] { itemId }, value["sourceItemIds"]!.AsArray()
            .Select(node => node!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(source, value["sourceCarrier"]));
        Assert.True(JsonNode.DeepEquals(destination, value["destinationCarrier"]));
        Assert.Equal(before, value["quantityBefore"]!.GetValue<int>());
        Assert.Equal(after, value["quantityAfter"]!.GetValue<int>());
        Assert.Equal("mortal_wound_treatment", value["authorityKind"]!.GetValue<string>());
        Assert.StartsWith("mwta_t070b4_finalization_", value["authorityId"]!.GetValue<string>());
    }

    private static void AssertPlanEqual(Result expected, Result actual)
    {
        AssertRoots(expected.Roots, actual.Roots);
        Assert.True(JsonNode.DeepEquals(expected.Index, actual.Index));
        Assert.Equal(expected.Transitions.Count, actual.Transitions.Count);
        for (var i = 0; i < expected.Transitions.Count; i++)
            Assert.True(JsonNode.DeepEquals(expected.Transitions[i], actual.Transitions[i]));
        Assert.Equal(expected.Capacities.Count, actual.Capacities.Count);
        for (var i = 0; i < expected.Capacities.Count; i++)
            AssertCapacity(expected.Capacities[i], actual.Capacities[i]);
        Assert.Equal(expected.TerminalOwners, actual.TerminalOwners);
        Assert.Equal(expected.Issues.Count, actual.Issues.Count);
        for (var i = 0; i < expected.Issues.Count; i++) AssertIssue(expected.Issues[i], actual.Issues[i]);
        Assert.Equal(expected.Fingerprint, actual.Fingerprint);
        Assert.Equal(expected.IsValid, actual.IsValid);
    }

    private static void AssertProjectionEqual(ProjectionOutput expected, ProjectionOutput actual)
    {
        AssertProjectionRoots(expected.Roots, actual.Roots);
        Assert.True(JsonNode.DeepEquals(expected.Index, actual.Index));
        Assert.Equal(expected.Issues.Count, actual.Issues.Count);
        for (var i = 0; i < expected.Issues.Count; i++) AssertIssue(expected.Issues[i], actual.Issues[i]);
        Assert.Equal(expected.Fingerprint, actual.Fingerprint);
        Assert.Equal(expected.IsValid, actual.IsValid);
    }

    private static void AssertRoots(IReadOnlyDictionary<string, JsonObject> expected,
        IReadOnlyDictionary<string, JsonObject> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(v => v, StringComparer.Ordinal),
            actual.Keys.OrderBy(v => v, StringComparer.Ordinal));
        foreach (var pair in expected)
            Assert.True(JsonNode.DeepEquals(pair.Value, actual[pair.Key]), pair.Key);
    }

    private static void AssertProjectionRoots(
        IReadOnlyDictionary<string, JsonNode?> expected,
        IReadOnlyDictionary<string, JsonNode?> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(v => v, StringComparer.Ordinal),
            actual.Keys.OrderBy(v => v, StringComparer.Ordinal));
        foreach (var pair in expected)
            Assert.True(JsonNode.DeepEquals(pair.Value, actual[pair.Key]), pair.Key);
    }

    private static void AssertCapacity(ResourceCapacityIntent expected, ResourceCapacityIntent actual)
    {
        Assert.Equal(expected.EventRef, actual.EventRef);
        Assert.Equal(expected.OriginKind, actual.OriginKind);
        Assert.Equal(expected.OriginId, actual.OriginId);
        Assert.Equal(expected.Coordinate, actual.Coordinate);
        Assert.Equal(expected.Operation, actual.Operation);
        Assert.Equal(expected.ResolvedCapacity?.Maximum, actual.ResolvedCapacity?.Maximum);
        Assert.Equal(expected.ResolvedCapacity?.Binding, actual.ResolvedCapacity?.Binding);
        Assert.Equal(expected.ResolvedCapacity?.Initialization, actual.ResolvedCapacity?.Initialization);
        Assert.Equal(expected.CurrentDisposition, actual.CurrentDisposition);
        Assert.Equal(expected.Phase, actual.Phase);
        Assert.Equal(expected.Priority, actual.Priority);
        Assert.Equal(expected.SourceEvidence, actual.SourceEvidence);
        Assert.Equal(expected.PolicyFingerprint, actual.PolicyFingerprint);
        Assert.Equal(expected.ReceiptId, actual.ReceiptId);
    }

    private static void AssertIssue(ValidationIssue expected, ValidationIssue actual)
    {
        Assert.Equal(expected.FilePath, actual.FilePath);
        Assert.Equal(expected.Severity, actual.Severity);
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expected.Code, actual.Code);
        Assert.Equal(expected.Actor, actual.Actor);
        Assert.Equal(expected.Section, actual.Section);
        Assert.Equal(expected.Expected, actual.Expected);
        Assert.Equal(expected.Actual, actual.Actual);
        Assert.Equal(expected.RepairHint, actual.RepairHint);
        Assert.Equal(expected.RepairTargetFiles, actual.RepairTargetFiles);
        Assert.Equal(expected.FactionRepairClassification, actual.FactionRepairClassification);
        Assert.Equal(expected.MortalItemRepairContext, actual.MortalItemRepairContext);
        Assert.Equal(expected.MortalLocationRepairContext, actual.MortalLocationRepairContext);
        Assert.Equal(expected.EffectRepairContext, actual.EffectRepairContext);
        Assert.Equal(expected.WoundRepairContext, actual.WoundRepairContext);
    }

    private static MortalItemCarrierCatalogInput Clone(MortalItemCarrierCatalogInput value) => new(
        value.PlayerInventory?.DeepClone().AsObject(), value.NpcCore?.DeepClone().AsObject(),
        value.NpcInventoryCommands?.DeepClone().AsObject(), value.CurrentLocation?.DeepClone().AsObject(),
        value.Vehicles?.DeepClone().AsObject(), value.CompanionRoots.ToDictionary(
            pair => pair.Key, pair => pair.Value.DeepClone().AsObject(), StringComparer.Ordinal),
        value.OffscreenLocationStorageContents?.DeepClone().AsObject());

    private static string Describe(Fixture fixture) => string.Join("|",
        RootMap(fixture.Roots).OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + "=" + WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value))
            .Append(WoundAcceptedTurnFingerprintWriter.CanonicalJson(fixture.Index))
            .Append(fixture.State.ToCanonicalJson()).Append(fixture.State.Fingerprint));

    private static IReadOnlyDictionary<string, JsonObject> RootMap(MortalItemCarrierCatalogInput roots)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        Add(PlayerPath, roots.PlayerInventory); Add(NpcPath, roots.NpcCore);
        Add(NpcCommandsPath, roots.NpcInventoryCommands);
        Add("game_state/world/current_location.json", roots.CurrentLocation);
        Add("game_state/misc/vehicles.json", roots.Vehicles);
        Add("game_state/world/location_storage_contents.json", roots.OffscreenLocationStorageContents);
        foreach (var pair in roots.CompanionRoots) result.Add(pair.Key, pair.Value);
        return result;
        void Add(string path, JsonObject? root) { if (root != null) result.Add(path, root); }
    }

    private static JsonObject FindItem(IReadOnlyDictionary<string, JsonObject> roots, string id) =>
        Assert.Single(roots.Values.SelectMany(Objects), value =>
            string.Equals(value["itemId"]?.GetValue<string>(), id, StringComparison.Ordinal) &&
            value["materializationReceipt"] is JsonObject);

    private static JsonObject FindItem(IReadOnlyDictionary<string, JsonNode?> roots, string id) =>
        Assert.Single(roots.Values.SelectMany(Objects), value =>
            string.Equals(value["itemId"]?.GetValue<string>(), id, StringComparison.Ordinal) &&
            value["materializationReceipt"] is JsonObject);

    private static bool HasItem(IReadOnlyDictionary<string, JsonObject> roots, string id) =>
        roots.Values.SelectMany(Objects).Any(value =>
            string.Equals(value["itemId"]?.GetValue<string>(), id, StringComparison.Ordinal) &&
            value["materializationReceipt"] is JsonObject);

    private static IReadOnlyList<JsonObject> Npcs(JsonObject root, string npcId) =>
        new[] { "UpdateNPCs", "NPCsInScene" }
            .SelectMany(section => root[section] is JsonArray values
                ? values.OfType<JsonObject>()
                : Enumerable.Empty<JsonObject>())
            .Where(npc => string.Equals(
                npc["NPCId"]?.GetValue<string>(),
                npcId,
                StringComparison.Ordinal))
            .ToArray();

    private static IEnumerable<JsonObject> Objects(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            yield return obj;
            foreach (var child in obj.SelectMany(pair => Objects(pair.Value))) yield return child;
        }
        else if (node is JsonArray array)
            foreach (var child in array.SelectMany(Objects)) yield return child;
    }

    private static IEnumerable<string> Strings(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var value in obj.SelectMany(pair => Strings(pair.Value))) yield return value;
        else if (node is JsonArray array)
            foreach (var value in array.SelectMany(Strings)) yield return value;
        else if (node is JsonValue value && value.TryGetValue<string>(out var text)) yield return text;
    }

    private static JsonObject Entry(JsonObject root, string id) =>
        Assert.Single(root["entries"]!.AsArray().OfType<JsonObject>(), value =>
            string.Equals(value["itemId"]?.GetValue<string>(), id, StringComparison.Ordinal));
    private static JsonObject LastTransition(JsonObject entry) =>
        Assert.IsType<JsonObject>(entry["transitions"]!.AsArray()[^1]);
    private static JsonObject MergeIndexes(params JsonObject[] indexes) => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray(indexes.SelectMany(index => index["entries"]!.AsArray())
            .Select(value => value!.DeepClone()).ToArray())
    };
    private static void Populate(JsonObject item, string section) =>
        item["materialization"]!["sections"]![section] = new JsonObject
        { ["state"] = "populated", ["reason"] = null };
    private static ResourceDefinition Definition(ResourceDefinitionCatalog catalog, string key)
    { Assert.True(catalog.TryResolveExact(key, out var value)); return Assert.IsType<ResourceDefinition>(value); }
    private static decimal ExactScale(decimal value, int remaining, int source, decimal quantum)
    { var result = checked(value * remaining) / source; Assert.Equal(0m, result % quantum); return result; }
    private static void AssertOrdinal(string eventRef, int ordinal) => Assert.Single(Regex.Matches(
        eventRef, $@"(?<!\d){ordinal:D4}(?!\d)"));

    private static Type ExactType(string fullName)
    {
        var assembly = typeof(MortalItemIdentityState).Assembly;
        var candidate = assembly.GetType(fullName, false, false);
        Assert.NotNull(candidate);
        Assert.Equal(fullName, candidate!.FullName);
        Assert.Same(assembly, candidate.Assembly);
        return candidate;
    }
    private static MethodInfo ExactMethod(Type owner, string name, Type input, Type? result = null)
    {
        var method = Assert.Single(owner.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public),
            value => value.Name == name && value.GetParameters().Length == 1 &&
                     value.GetParameters()[0].ParameterType == input &&
                     (result == null || value.ReturnType == result));
        Assert.True(method.IsStatic && method.IsAssembly);
        return method;
    }
    private static ConstructorInfo Ctor(Type type, params Type[] parameters)
    {
        var value = type.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
            null, parameters, null); Assert.NotNull(value); return value!;
    }
    private static PropertyInfo Property(Type type, string name, Type expected)
    {
        var value = type.GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(value); Assert.Equal(expected, value!.PropertyType); return value;
    }
    private static T Read<T>(object value, string name) => Assert.IsAssignableFrom<T>(
        value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!
            .GetValue(value));
    private static JsonObject? ReadNullableObject(object value, string name)
    {
        var result = value.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.GetValue(value);
        return result == null ? null : Assert.IsType<JsonObject>(result);
    }
    private static void AssertFingerprint(string value) => Assert.True(
        ResourceMaterializationContract.IsAuthorityFingerprint(value),
        $"Expected sha256 plus 64 lowercase hexadecimal digits, got '{value}'.");
    private static string Fingerprint(string value) => "sha256:" + Hex(value);
    private static string Hex(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static IReadOnlyDictionary<string, byte[]> ReadFiles(string root)
    {
        Assert.True(Directory.Exists(root), root);
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .OrderBy(value => value, StringComparer.Ordinal).ToDictionary(
                value => Path.GetRelativePath(root, value).Replace('\\', '/'),
                File.ReadAllBytes, StringComparer.Ordinal);
    }
    private static void AssertFilesEqual(IReadOnlyDictionary<string, byte[]> expected,
        IReadOnlyDictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys, actual.Keys);
        foreach (var pair in expected) Assert.True(pair.Value.SequenceEqual(actual[pair.Key]), pair.Key);
    }
    private static string FindRepo()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current != null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")) &&
                Directory.Exists(Path.Combine(current.FullName, "FileSystemExample"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed record Fixture(string ItemId, MortalItemCarrierCatalogInput Roots,
        JsonObject Index, MortalItemIdentityParseResult Identity,
        ResourceDefinitionCatalog Definitions, ResourceStateLedger State,
        ResourceSourceEvidence Source, string PolicyFingerprint);
    private sealed record Companion(string Path, Fixture Fixture);
    private sealed record Command(int Ordinal, string ItemId, int Quantity,
        string ClaimFingerprint, string TransitionId, string AuthorityKind, string AuthorityId);
    private sealed record Result(IReadOnlyDictionary<string, JsonObject> Roots, JsonObject? Index,
        IReadOnlyList<JsonObject> Transitions, IReadOnlyList<ResourceCapacityIntent> Capacities,
        IReadOnlyList<ResourceOwnerKey> TerminalOwners, IReadOnlyList<ValidationIssue> Issues,
        string Fingerprint, bool IsValid);
    private sealed record ProjectionScenario(int Turn,
        MortalItemAcceptedTurnNormalizationSnapshot Snapshot,
        MortalItemRouteAuthorityCatalog RouteCatalog,
        IReadOnlyDictionary<string, JsonNode?> CurrentRoots,
        IReadOnlyDictionary<string, JsonNode?> BackupRoots,
        JsonObject IdentityRoot,
        string CreatedItemId,
        string CreationRef,
        string TransferredItemId);
    private sealed record ProjectionOutput(IReadOnlyDictionary<string, JsonNode?> Roots,
        JsonObject Index, IReadOnlyList<ValidationIssue> Issues, string Fingerprint, bool IsValid);
}
