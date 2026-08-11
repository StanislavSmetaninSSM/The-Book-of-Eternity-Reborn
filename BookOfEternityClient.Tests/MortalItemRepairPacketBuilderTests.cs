using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalItemRepairPacketBuilderTests
{
    [Fact]
    public void Build_GroupsOneExactCoordinateAndExcludesClientOwnedIdentityTargets()
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
        var issues = new[]
        {
            CreateIssue(
                "game_state/inventory/items.json.UpdateInventory[0].materialization.sections.mechanics",
                "mortal_item_materialization_section_missing",
                coordinate,
                expected: "populated or empty_by_design",
                actual: "missing",
                targets: new[]
                {
                    "game_state/inventory/items.json",
                    "game_state/inventory/item_resources.json"
                },
                context),
            CreateIssue(
                "game_state/inventory/item_resources.json.entries[0].itemRef",
                "mortal_item_materialization_orphan_companion",
                coordinate,
                expected: "one exact creationRef",
                actual: "unresolved exact reference craft_result_42",
                targets: new[] { "game_state/inventory/item_resources.json" },
                context),
            CreateIssue(
                "game_state/inventory/item_identity_index.json.entries[0]",
                "mortal_item_materialization_gm_authored_client_field",
                coordinate,
                expected: "validated pre-turn client authority",
                actual: "GM-authored index entry",
                targets: new[] { MortalItemIdentityState.StatePath },
                context)
        };

        var packet = Assert.Single(MortalItemRepairPacketBuilder.Build(issues));

        Assert.Equal("mortal_item_materialization_repair", packet.Kind);
        Assert.Equal("critical", packet.Priority);
        Assert.Equal(new[] { coordinate }, packet.CanonicalActorNames);
        Assert.Equal("create", packet.TransitionClass);
        Assert.Equal("craft_output", packet.Route);
        Assert.Null(packet.SourceCarrier);
        Assert.Equal(context.DestinationCarrier, packet.DestinationCarrier);
        Assert.Equal(
            new[]
            {
                "game_state/inventory/item_resources.json",
                "game_state/inventory/items.json"
            },
            packet.TargetFiles);
        Assert.DoesNotContain(MortalItemIdentityState.StatePath, packet.TargetFiles);
        Assert.Equal(
            new[]
            {
                "game_state/inventory/items.json.UpdateInventory[0].materialization.sections.mechanics"
            },
            packet.MissingFields);
        Assert.Equal(2, packet.ExactFieldCorrections.Count);
        Assert.DoesNotContain(
            packet.ExactFieldCorrections,
            correction => correction.Path.Contains("item_identity_index.json", StringComparison.Ordinal));
        Assert.Equal(
            new[] { "game_state/inventory/item_resources.json" },
            packet.RequiredCompanionTargets);
        Assert.Contains("craft_request:req_craft_42", packet.ExpectedAuthority);
        Assert.Contains(
            packet.DoNotDo,
            rule => rule.Contains("item_identity_index.json", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_UsesExactCoordinatesInsteadOfDisplayNamesOrSharedFiles()
    {
        var first = CreateIssue(
            "game_state/inventory/items.json.UpdateInventory[0].name",
            "mortal_item_materialization_complete_field_missing",
            "mortal_item:new:drop_A",
            "non-empty name",
            "missing",
            new[] { "game_state/inventory/items.json" },
            CreateContext("mortal_item:new:drop_A", "loot_acquisition"));
        var second = CreateIssue(
            "game_state/inventory/items.json.UpdateInventory[1].name",
            "mortal_item_materialization_complete_field_missing",
            "mortal_item:new:drop_a",
            "non-empty name",
            "missing",
            new[] { "game_state/inventory/items.json" },
            CreateContext("mortal_item:new:drop_a", "loot_acquisition"));

        var packets = MortalItemRepairPacketBuilder.Build(new[] { first, second });

        Assert.Equal(2, packets.Count);
        Assert.Equal(
            new[] { "mortal_item:new:drop_A", "mortal_item:new:drop_a" },
            packets.Select(packet => Assert.Single(packet.CanonicalActorNames)));
    }

    [Fact]
    public void Build_GlobalIdentityAmbiguityUsesOneBoundedAuthorityPacket()
    {
        var issues = new[]
        {
            CreateIssue(
                "game_state/inventory/items.json.items[0].itemId",
                "mortal_item_materialization_identity_ambiguity",
                "mortal_item:existing:itm_A",
                "one ordinal identity",
                "itm_A aliases itm_a",
                new[] { "game_state/inventory/items.json" },
                CreateExistingContext("mortal_item:existing:itm_A")),
            CreateIssue(
                "game_state/npcs/npc_core.json.NPCsInScene[0].inventory[0].itemId",
                "mortal_item_materialization_identity_ambiguity",
                "mortal_item:existing:itm_a",
                "one ordinal identity",
                "itm_a aliases itm_A",
                new[] { "game_state/npcs/npc_core.json" },
                CreateExistingContext("mortal_item:existing:itm_a"))
        };

        var packet = Assert.Single(MortalItemRepairPacketBuilder.Build(issues));

        Assert.Equal("mortal_item_identity_authority_repair", packet.Kind);
        Assert.Equal(
            new[] { "mortal_item:existing:itm_A", "mortal_item:existing:itm_a" },
            packet.CanonicalActorNames);
        Assert.Equal(
            new[]
            {
                "game_state/inventory/items.json",
                "game_state/npcs/npc_core.json"
            },
            packet.TargetFiles);
    }

    [Theory]
    [InlineData("mortal_item_identity_index_tampered", "MortalItemIdentity", null)]
    [InlineData("mortal_item_materialization_gm_authored_client_field", "MortalItemMaterialization", "mortal_item:identity_authority")]
    public void Build_ProtectedIdentityAuthorityIssueDoesNotCreateGmPacket(
        string code,
        string section,
        string? coordinate)
    {
        var issue = new ValidationIssue(
            MortalItemIdentityState.StatePath,
            IssueSeverity.Error,
            "Client-owned Mortal item identity authority is invalid.",
            code: code,
            actor: coordinate ?? "mortal_item:index",
            section: section,
            expected: "validated pre-turn client authority",
            actual: "GM-authored identity entry",
            repairHint: "Restore the protected before-image.",
            repairTargetFiles: new[] { MortalItemIdentityState.StatePath });
        if (coordinate != null)
        {
            issue.MortalItemRepairContext = new MortalItemRepairContext(
                coordinate,
                "identity_authority",
                Route: null,
                SourceCarrier: null,
                DestinationCarrier: null,
                ExpectedAuthority: "validated pre-turn client authority",
                ActualEvidence: "GM-authored identity entry",
                RequiredCompanionTargets: Array.Empty<string>());
        }

        Assert.True(MortalItemRepairPacketBuilder.RequiresClientOwnedRollback(new[] { issue }));
        Assert.Empty(MortalItemRepairPacketBuilder.Build(new[] { issue }));
    }

    [Theory]
    [InlineData(
        "game_state/inventory/items.json.items[0].materializationReceipt",
        "mortal_item_materialization_invalid_receipt")]
    [InlineData(
        "game_state/inventory/items.json.items[0].materializationReceipt.seal",
        "mortal_item_materialization_receipt_seal_mismatch")]
    [InlineData(
        "game_state/inventory/item_identity_index.json.entries[0].transitions[1]",
        "mortal_item_identity_transition_history_rewrite")]
    public void Build_ReceiptSealAndClientTransitionIssuesRequireClientRollback(
        string path,
        string code)
    {
        const string coordinate = "mortal_item:existing:itm_protected";
        var issue = CreateIssue(
            path,
            code,
            coordinate,
            "validated client-authored before-image",
            "forged client authority",
            new[] { "game_state/inventory/items.json", MortalItemIdentityState.StatePath },
            CreateExistingContext(coordinate));

        Assert.True(MortalItemRepairPacketBuilder.RequiresClientOwnedRollback(new[] { issue }));
        Assert.Empty(MortalItemRepairPacketBuilder.Build(new[] { issue }));
    }

    [Theory]
    [InlineData("mortal_item:unknown")]
    [InlineData("mortal_item:unresolved:shared_inventory")]
    public void Build_UnresolvedCoordinateDoesNotCreateBroadItemPacket(string coordinate)
    {
        var issue = CreateIssue(
            "game_state/inventory/items.json",
            "mortal_item_materialization_invalid",
            coordinate,
            "one exact item coordinate",
            "coordinate could not be resolved",
            new[] { "game_state/inventory/items.json" },
            new MortalItemRepairContext(
                coordinate,
                "unresolved",
                Route: null,
                SourceCarrier: null,
                DestinationCarrier: null,
                ExpectedAuthority: null,
                ActualEvidence: "coordinate could not be resolved",
                RequiredCompanionTargets: Array.Empty<string>()));

        Assert.Empty(MortalItemRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalItemRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { issue }));
    }

    [Theory]
    [InlineData("mortal_item:new:unknown")]
    [InlineData("mortal_item:new:quest reward 1")]
    public void Build_ValidExactCreationReferenceProducesBoundedPacket(
        string coordinate)
    {
        var issue = CreateIssue(
            "game_state/inventory/items.json.UpdateInventory[0].description",
            "mortal_item_materialization_missing_field",
            coordinate,
            "complete description",
            "missing",
            new[] { "game_state/inventory/items.json" },
            CreateContext(coordinate, "player_acquisition"));

        var packet = Assert.Single(MortalItemRepairPacketBuilder.Build(new[] { issue }));

        Assert.Equal(new[] { coordinate }, packet.CanonicalActorNames);
        Assert.False(MortalItemRepairPacketBuilder.RequiresFailClosedRollback(
            new[] { issue }));
    }

    [Fact]
    public void Build_BoundsEveryCorrectionAndEvidenceValueWithoutDuplicatingContextEvidence()
    {
        const string coordinate = "mortal_item:new:bounded_creation";
        var longValue = new string('x', 900);
        var context = CreateContext(coordinate, "loot_acquisition") with
        {
            ExpectedAuthority = longValue,
            ActualEvidence = longValue
        };
        var issue = CreateIssue(
            $"game_state/inventory/items.json.UpdateInventory[0].{longValue}",
            $"mortal_item_materialization_{longValue}",
            coordinate,
            longValue,
            longValue,
            new[] { "game_state/inventory/items.json" },
            context);

        var packet = Assert.Single(MortalItemRepairPacketBuilder.Build(new[] { issue }));
        var correction = Assert.Single(packet.ExactFieldCorrections);

        Assert.All(
            new[]
            {
                correction.Path,
                correction.Expected,
                correction.Actual,
                correction.Code,
                correction.RepairHint,
                Assert.Single(packet.ExpectedAuthority),
                Assert.Single(packet.ActualEvidence)
            },
            value => Assert.InRange(value.Length, 1, 501));
    }

    [Fact]
    public void Build_ItemSpecificMaterializationIssueStaysOnItsExactItemCoordinate()
    {
        const string coordinate = "mortal_item:existing:itm_transition_mismatch";
        var issue = CreateIssue(
            "game_state/inventory/items.json.items[0].count",
            "mortal_item_materialization_route_authority_mismatch",
            coordinate,
            "one exact accepted transfer authority",
            "carrier mutation lacks exact authority",
            new[] { "game_state/inventory/items.json" },
            CreateExistingContext(coordinate));

        var packet = Assert.Single(MortalItemRepairPacketBuilder.Build(new[] { issue }));

        Assert.Equal("mortal_item_materialization_repair", packet.Kind);
        Assert.Equal(new[] { coordinate }, packet.CanonicalActorNames);
        Assert.Equal("transfer", packet.TransitionClass);
        Assert.Equal(new[] { "game_state/inventory/items.json" }, packet.TargetFiles);
    }

    [Theory]
    [InlineData("game_state/inventory/items.json", true)]
    [InlineData("game_state/inventory/item_bonds.json.entries[0]", true)]
    [InlineData("game_state/npcs/item_journals.json:entry", true)]
    [InlineData("game_state/world/current_location.json", true)]
    [InlineData("game_state/inventory/item_identity_index.json", false)]
    [InlineData("game_state/control/pending_turn_snapshot.json", false)]
    [InlineData("game_state/control/pending_craft_request.json", false)]
    [InlineData("output/narrative_response.json", false)]
    [InlineData("game_state/factions/faction_core.json", false)]
    public void GmTargetAllowlist_IsExplicitAndFailClosed(string path, bool expected)
    {
        Assert.Equal(expected, MortalItemRepairPacketBuilder.IsGmAuthorableTarget(path));
    }

    [Fact]
    public void ProtectedIdentityPath_IsRecognizedCaseInsensitivelyButNeverGmAuthorable()
    {
        const string path = "GAME_STATE/INVENTORY/ITEM_IDENTITY_INDEX.JSON.entries[0]";

        var issue = new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Client-owned identity authority was modified.");

        Assert.True(MortalItemRepairPacketBuilder.IsProtectedClientOwnedTarget(path));
        Assert.False(MortalItemRepairPacketBuilder.IsGmAuthorableTarget(path));
        Assert.Equal(IssueCategory.ClientOwnedSurface, issue.Category);
    }

    private static MortalItemRepairContext CreateContext(string coordinate, string route) =>
        new(
            coordinate,
            "create",
            route,
            SourceCarrier: null,
            DestinationCarrier: new MortalItemCarrierCoordinate(
                "player_inventory",
                "player",
                null,
                Array.Empty<string>()),
            ExpectedAuthority: $"turn_outcome:{coordinate}",
            ActualEvidence: "malformed raw creation",
            RequiredCompanionTargets: Array.Empty<string>());

    private static MortalItemRepairContext CreateExistingContext(string coordinate) =>
        new(
            coordinate,
            "transfer",
            Route: null,
            SourceCarrier: new MortalItemCarrierCoordinate(
                "player_inventory",
                "player",
                null,
                Array.Empty<string>()),
            DestinationCarrier: null,
            ExpectedAuthority: "validated pre-turn carrier",
            ActualEvidence: "ambiguous current identity",
            RequiredCompanionTargets: Array.Empty<string>());

    private static ValidationIssue CreateIssue(
        string path,
        string code,
        string coordinate,
        string expected,
        string actual,
        IReadOnlyList<string> targets,
        MortalItemRepairContext context)
    {
        var issue = new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Malformed Mortal item package.",
            code: code,
            actor: coordinate,
            section: "MortalItemMaterialization",
            expected: expected,
            actual: actual,
            repairHint: "Repair only the exact GM-owned field.",
            repairTargetFiles: targets);
        issue.MortalItemRepairContext = context;
        return issue;
    }
}
