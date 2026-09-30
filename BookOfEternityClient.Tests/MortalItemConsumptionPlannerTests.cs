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
/// Verifies the frozen T070-B.4 pure item planners using isolated fixture values.
/// </summary>
public sealed class MortalItemConsumptionPlannerTests : MortalItemConsumptionPlannerTestFixture
{
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

}
