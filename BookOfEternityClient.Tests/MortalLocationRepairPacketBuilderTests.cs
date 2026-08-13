using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLocationRepairPacketBuilderTests
{
    [Fact]
    public void Build_GroupsOneExactCurrentCreationAndExcludesProtectedTargets()
    {
        var context = CreateContext(
            "currentLocationData",
            "mortal_location",
            initialId: "locref_turn_12_black_ford",
            repairableFields: new[] { "description", "materialization.sections.presentation" });
        var issues = new[]
        {
            CreateIssue(
                "game_state/world/current_location.json.currentLocationData.description",
                "mortal_location_materialization_governed_field_missing",
                actor: null,
                expected: "non-empty description",
                actual: "missing",
                targets: new[] { MortalLocationMaterializationContract.CurrentLocationPath },
                context),
            CreateIssue(
                "game_state/world/current_location.json.currentLocationData.materialization.sections.presentation",
                "mortal_location_materialization_section_disposition_mismatch",
                actor: null,
                expected: "populated or empty_by_design",
                actual: "missing",
                targets: new[] { MortalLocationMaterializationContract.CurrentLocationPath },
                context),
        };

        var packet = Assert.Single(MortalLocationRepairPacketBuilder.Build(issues));

        Assert.Equal("mortal_location_materialization_repair", packet.Kind);
        Assert.Equal("blocking", packet.Priority);
        Assert.Equal(
            new[] { "mortal_location:new:locref_turn_12_black_ford" },
            packet.CanonicalActorNames);
        Assert.Equal("current_scene_creation", packet.TransitionClass);
        Assert.Equal("current_scene_creation", packet.Route);
        Assert.Equal("currentLocationData", packet.RawCarrier);
        Assert.Equal("currentLocationData", packet.RawCoordinate);
        Assert.Equal(
            new[] { MortalLocationMaterializationContract.CurrentLocationPath },
            packet.TargetFiles);
        Assert.DoesNotContain(MortalLocationIdentityState.StatePath, packet.TargetFiles);
        Assert.Equal(
            new[]
            {
                "game_state/world/current_location.json.currentLocationData.description",
                "game_state/world/current_location.json.currentLocationData.materialization.sections.presentation"
            },
            packet.MissingFields);
        Assert.Equal(2, packet.ExactFieldCorrections.Count);
        Assert.Contains(packet.DoNotDo, rule =>
            rule.Contains(MortalLocationIdentityState.StatePath, StringComparison.Ordinal));
    }

    [Fact]
    public void Build_SeparatesExactCaseSensitiveRemoteLocationAndLinkCoordinates()
    {
        var upper = CreateIssue(
            "game_state/world/world_map.json.worldMapUpdates.newLocations[0].description",
            "mortal_location_materialization_governed_field_missing",
            "mortal_location:new:LOCREF_A",
            "complete description",
            "missing",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            CreateContext(
                "worldMapUpdates.newLocations[0]",
                "mortal_location",
                "LOCREF_A",
                new[] { "description" }));
        var lower = CreateIssue(
            "game_state/world/world_map.json.worldMapUpdates.newLinks[1].description",
            "mortal_location_materialization_governed_field_missing",
            "mortal_location_link:new:locref_a",
            "complete description",
            "missing",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            CreateContext(
                "worldMapUpdates.newLinks[1]",
                "mortal_location_link",
                "locref_a",
                new[] { "description" }));

        var packets = MortalLocationRepairPacketBuilder.Build(new[] { upper, lower });

        Assert.Equal(2, packets.Count);
        Assert.Equal(
            new[]
            {
                "mortal_location:new:LOCREF_A",
                "mortal_location_link:new:locref_a"
            },
            packets.Select(packet => Assert.Single(packet.CanonicalActorNames)));
        Assert.Equal(
            new[] { "worldMapUpdates.newLocations[0]", "worldMapUpdates.newLinks[1]" },
            packets.Select(packet => packet.RawCoordinate));
    }

    [Fact]
    public void Build_ClassifiesInvalidAndConflictEvidenceDeterministically()
    {
        const string actor = "mortal_location:existing:loc_exact_tower";
        var context = CreateContext(
            "worldMapUpdates.locationUpdates[2]",
            "mortal_location",
            initialId: null,
            repairableFields: new[] { "coordinates", "description" },
            existingId: "loc_exact_tower");
        var issues = new[]
        {
            CreateIssue(
                "game_state/world/world_map.json.worldMapUpdates.locationUpdates[2].coordinates",
                "mortal_location_materialization_coordinate_collision",
                actor,
                "unique coordinate",
                "x=2,y=1,z=0 already used",
                new[] { MortalLocationMaterializationContract.WorldMapPath },
                context),
            CreateIssue(
                "game_state/world/world_map.json.worldMapUpdates.locationUpdates[2].description",
                "mortal_location_materialization_physical_shape_invalid",
                actor,
                "valid semantic field",
                "number",
                new[] { MortalLocationMaterializationContract.WorldMapPath },
                context)
        };

        var packet = Assert.Single(MortalLocationRepairPacketBuilder.Build(issues));

        Assert.Equal("narrow_location_update", packet.TransitionClass);
        Assert.Equal(
            new[]
            {
                "game_state/world/world_map.json.worldMapUpdates.locationUpdates[2].description"
            },
            packet.InvalidFields);
        Assert.Equal(
            new[]
            {
                "game_state/world/world_map.json.worldMapUpdates.locationUpdates[2].coordinates"
            },
            packet.Conflicts);
        Assert.Equal(
            packet.ExactFieldCorrections.OrderBy(item => item.Path, StringComparer.Ordinal).Select(item => item.Path),
            packet.ExactFieldCorrections.Select(item => item.Path));
    }

    [Theory]
    [InlineData(
        "currentLocationData",
        "mortal_location",
        "mortal_location:existing:loc_current_exact",
        "current_selection",
        "game_state/world/current_location.json")]
    [InlineData(
        "worldMapUpdates.locationDiscoveryTransitions[1]",
        "mortal_location",
        "mortal_location:existing:loc_revealed_exact",
        "location_discovery_transition",
        "game_state/world/world_map.json")]
    [InlineData(
        "worldMapUpdates.linkUpdates[2]",
        "mortal_location_link",
        "mortal_location_link:existing:link_opened_exact",
        "link_update",
        "game_state/world/world_map.json")]
    [InlineData(
        "worldMapUpdates.linkRemovals[3]",
        "mortal_location_link",
        "mortal_location_link:existing:link_retired_exact",
        "link_removal",
        "game_state/world/world_map.json")]
    public void Build_AcceptsEveryExactExistingLifecycleRoute(
        string coordinate,
        string entityKind,
        string actor,
        string expectedTransitionClass,
        string targetFile)
    {
        var issue = CreateIssue(
            targetFile + "." + coordinate + ".description",
            "mortal_location_materialization_governed_field_missing",
            actor,
            "complete description",
            "missing",
            new[] { targetFile },
            CreateContext(
                coordinate,
                entityKind,
                initialId: null,
                repairableFields: new[] { "description" },
                existingId: actor[(actor.LastIndexOf(':') + 1)..]));

        var packet = Assert.Single(MortalLocationRepairPacketBuilder.Build(new[] { issue }));

        Assert.Equal(expectedTransitionClass, packet.TransitionClass);
        Assert.Equal(coordinate, packet.RawCoordinate);
        Assert.Equal(new[] { targetFile }, packet.TargetFiles);
        Assert.False(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Theory]
    [InlineData("mortal_location_materialization_historical_replay", "mortal_location:new:locref_replay")]
    [InlineData("mortal_location_materialization_duplicate_creation_route", "mortal_location:new:locref_duplicate")]
    [InlineData("mortal_location_materialization_confusable_canonical_identity", "mortal_location:existing:loc_case")]
    [InlineData("mortal_location_materialization_gm_authored_client_field", "mortal_location:new:locref_forged")]
    public void Build_ReplayDuplicateConfusableAndClientFieldFailClosed(
        string code,
        string actor)
    {
        var issue = CreateIssue(
            "game_state/world/current_location.json.currentLocationData.materializationReceipt",
            code,
            actor,
            "new unambiguous GM-owned semantic evidence",
            "protected or ambiguous authority",
            new[]
            {
                MortalLocationMaterializationContract.CurrentLocationPath,
                MortalLocationIdentityState.StatePath
            },
            CreateContext(
                "currentLocationData",
                "mortal_location",
                actor[(actor.LastIndexOf(':') + 1)..],
                Array.Empty<string>()));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Fact]
    public void Build_GmAuthoredClientProtocolFieldFailsClosedWithoutReceiptPathHint()
    {
        var issue = CreateIssue(
            "game_state/world/current_location.json.currentLocationData.requestId",
            "mortal_location_materialization_gm_authored_client_field",
            "mortal_location:new:locref_forged_request",
            "field absent before client sealing",
            "present",
            new[] { MortalLocationMaterializationContract.CurrentLocationPath },
            CreateContext(
                "currentLocationData",
                "mortal_location",
                "locref_forged_request",
                Array.Empty<string>()));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Fact]
    public void Build_NewActorMustMatchContextAndCarryExactMaterializationOrigin()
    {
        var actorMismatch = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath +
            ".worldMapUpdates.newLocations[0].description",
            "mortal_location_materialization_governed_field_missing",
            "mortal_location:new:locref_actor_a",
            "complete description",
            "missing",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            new MortalLocationRepairContext(
                "worldMapUpdates.newLocations[0]",
                "mortal_location",
                "locref_actor_b",
                "mlocmat_actor_b",
                new[] { "description" }));
        var missingOrigin = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath +
            ".worldMapUpdates.newLocations[1].materialization",
            "mortal_location_materialization_invalid_envelope",
            "mortal_location:new:locref_missing_origin",
            "materializationId, initialId, sourceTurn>=1",
            "materializationId missing",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            new MortalLocationRepairContext(
                "worldMapUpdates.newLocations[1]",
                "mortal_location",
                "locref_missing_origin",
                null,
                Array.Empty<string>()));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { actorMismatch }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { actorMismatch }));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { missingOrigin }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { missingOrigin }));
    }

    [Fact]
    public void Build_ExistingActorMustMatchExactContextIdentity()
    {
        var issue = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath +
            ".worldMapUpdates.locationUpdates[0].description",
            "mortal_location_materialization_governed_field_missing",
            "mortal_location:existing:loc_actor_a",
            "complete description",
            "missing",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            new MortalLocationRepairContext(
                "worldMapUpdates.locationUpdates[0]",
                "mortal_location",
                null,
                null,
                new[] { "description" },
                ExistingId: "loc_actor_b"));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Theory]
    [InlineData("mortal_location_update_target_unresolved")]
    [InlineData("mortal_location_update_target_ambiguous")]
    [InlineData("mortal_location_discovery_target_unresolved")]
    [InlineData("mortal_location_discovery_target_ambiguous")]
    [InlineData("mortal_location_link_update_target_unresolved")]
    [InlineData("mortal_location_link_update_target_ambiguous")]
    [InlineData("mortal_location_link_removal_target_unresolved")]
    [InlineData("mortal_location_link_removal_target_ambiguous")]
    [InlineData("mortal_location_materialization_existing_target_unresolved")]
    public void Build_UnresolvedOrAmbiguousPermanentTargetFailsClosed(string code)
    {
        var isLink = code.Contains("_link_", StringComparison.Ordinal);
        var collection = isLink ? "linkUpdates" : "locationUpdates";
        var entityKind = isLink ? "mortal_location_link" : "mortal_location";
        var identity = isLink ? "link_missing" : "loc_missing";
        var actor = entityKind + ":existing:" + identity;
        var coordinate = $"worldMapUpdates.{collection}[0]";
        var issue = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath + "." + coordinate,
            code,
            actor,
            "one exact active permanent target",
            identity,
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            CreateContext(
                coordinate,
                entityKind,
                initialId: null,
                repairableFields: Array.Empty<string>(),
                existingId: identity));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Theory]
    [InlineData(
        "mortal_location_movement_not_authorized",
        MortalLocationMaterializationContract.CurrentLocationPath,
        "currentLocationData")]
    [InlineData(
        "mortal_location_storage_removal_not_empty",
        MortalLocationMaterializationContract.WorldMapPath,
        "worldMapUpdates.storagesToRemove[0]")]
    public void Build_UnauthorizedMovementAndNonEmptyStorageRemovalFailClosed(
        string code,
        string targetFile,
        string coordinate)
    {
        const string identity = "loc_exact_protected_operation";
        var issue = CreateIssue(
            targetFile + "." + coordinate,
            code,
            "mortal_location:existing:" + identity,
            "one authorized exact operation",
            "operation is not authorized by accepted pre-turn state",
            new[] { targetFile },
            CreateContext(
                coordinate,
                "mortal_location",
                initialId: null,
                repairableFields: Array.Empty<string>(),
                existingId: identity));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Fact]
    public void Build_ExistingFullResendCanBeReducedWithoutRetargetingIdentity()
    {
        const string coordinate = "currentLocationData";
        const string actor = "mortal_location:existing:loc_exact_resend";
        var issue = CreateIssue(
            MortalLocationMaterializationContract.CurrentLocationPath + "." + coordinate,
            "mortal_location_materialization_existing_full_resend",
            actor,
            "locationId plus current operational fields only",
            "full canonical location object",
            new[] { MortalLocationMaterializationContract.CurrentLocationPath },
            new MortalLocationRepairContext(
                coordinate,
                "mortal_location",
                null,
                null,
                new[] { "remove forbidden full-resend fields" },
                ExistingId: "loc_exact_resend"));

        var packet = Assert.Single(MortalLocationRepairPacketBuilder.Build(new[] { issue }));

        Assert.Equal(actor, Assert.Single(packet.CanonicalActorNames));
        Assert.Equal("current_selection", packet.TransitionClass);
        Assert.Equal(
            new[] { MortalLocationMaterializationContract.CurrentLocationPath },
            packet.TargetFiles);
        Assert.False(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Theory]
    [InlineData("worldMapUpdates.linkRemovals[0].linkId", "reason")]
    [InlineData("worldMapUpdates.newLinks[0].source", "sourceLocationId")]
    public void Build_IssueOutsideExplicitRepairableFieldsFailsClosed(
        string issueCoordinate,
        string repairableField)
    {
        var isRemoval = issueCoordinate.Contains("linkRemovals", StringComparison.Ordinal);
        var rawCoordinate = isRemoval
            ? "worldMapUpdates.linkRemovals[0]"
            : "worldMapUpdates.newLinks[0]";
        var actor = isRemoval
            ? "mortal_location_link:existing:link_exact"
            : "mortal_location_link:new:linkref_exact";
        var issue = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath + "." + issueCoordinate,
            isRemoval
                ? "mortal_location_link_transition_conflict"
                : "mortal_location_link_endpoint_unresolved",
            actor,
            "exact governed field",
            "invalid",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            CreateContext(
                rawCoordinate,
                "mortal_location_link",
                initialId: isRemoval ? null : "linkref_exact",
                repairableFields: new[] { repairableField },
                existingId: isRemoval ? "link_exact" : null));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Fact]
    public void Build_InvalidEndpointSelectorFailsClosedEvenWithExactCarrierContext()
    {
        const string rawCoordinate = "worldMapUpdates.newLinks[0]";
        var issue = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath + "." + rawCoordinate + ".sourceLocationId",
            "mortal_location_link_endpoint_selector_invalid",
            "mortal_location_link:new:linkref_exact",
            "exactly one nullable exact endpoint selector",
            "sourceLocationId=loc_exact; sourceInitialId= locref_alias ",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            CreateContext(
                rawCoordinate,
                "mortal_location_link",
                initialId: "linkref_exact",
                repairableFields: new[] { "sourceLocationId" }));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Theory]
    [InlineData(
        "worldMapUpdates.newLocations[0].description",
        "mortal_location",
        "mortal_location:new:locref_nested_coordinate")]
    [InlineData(
        "worldMapUpdates.newLocations[x]",
        "mortal_location",
        "mortal_location:new:locref_non_numeric_coordinate")]
    [InlineData(
        "worldMapUpdates.newLinks[0]",
        "mortal_location",
        "mortal_location:new:locref_wrong_entity_kind")]
    [InlineData(
        "worldMapUpdates.locationUpdates[0]",
        "mortal_location",
        "mortal_location:new:locref_wrong_lifecycle")]
    [InlineData(
        "currentLocationData",
        "mortal_location_link",
        "mortal_location_link:new:linkref_wrong_current_kind")]
    public void Build_MalformedCoordinateOrRouteActorMismatchFailsClosed(
        string coordinate,
        string entityKind,
        string actor)
    {
        var issue = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath + ".description",
            "mortal_location_materialization_governed_field_missing",
            actor,
            "complete description",
            "missing",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            CreateContext(
                coordinate,
                entityKind,
                actor[(actor.LastIndexOf(':') + 1)..],
                new[] { "description" }));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Fact]
    public void Build_RouteTargetFileMismatchFailsClosed()
    {
        var issue = CreateIssue(
            MortalLocationMaterializationContract.CurrentLocationPath +
            ".worldMapUpdates.newLinks[0].description",
            "mortal_location_materialization_governed_field_missing",
            "mortal_location_link:new:linkref_wrong_target",
            "complete description",
            "missing",
            new[] { MortalLocationMaterializationContract.CurrentLocationPath },
            CreateContext(
                "worldMapUpdates.newLinks[0]",
                "mortal_location_link",
                "linkref_wrong_target",
                new[] { "description" }));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Fact]
    public void Build_RawCoordinateMustMatchEveryCorrectionPathExactly()
    {
        var issue = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath +
            ".worldMapUpdates.newLocations[3].description",
            "mortal_location_materialization_governed_field_missing",
            "mortal_location:new:locref_coordinate_two",
            "complete description",
            "missing",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            CreateContext(
                "worldMapUpdates.newLocations[2]",
                "mortal_location",
                "locref_coordinate_two",
                new[] { "description" }));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Theory]
    [InlineData("mortal_location:unknown")]
    [InlineData("mortal_location_link:unknown")]
    [InlineData("mortal_location:unresolved:shared")]
    public void Build_UnresolvedCoordinateDoesNotCreateBroadPacket(string actor)
    {
        var issue = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath,
            "mortal_location_materialization_invalid_root",
            actor,
            "one exact candidate",
            "unresolved",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            CreateContext("worldMapUpdates", "mortal_location", null, Array.Empty<string>()));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Fact]
    public void Build_UnresolvedBootstrapLocationIssueFailsClosedWithoutSyntheticContext()
    {
        var issue = new ValidationIssue(
            "currentLocationData",
            IssueSeverity.Error,
            "The exact reserved starting location is missing.",
            code: "mortal_bootstrap_location_start_required",
            section: "mortal_bootstrap",
            expected: "one exact reserved currentLocationData candidate",
            actual: "missing",
            repairHint: "Do not invent a different reservation or permanent identity.");

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Theory]
    [InlineData("mortal_location:new:unknown", "mortal_location", "unknown")]
    [InlineData("mortal_location_link:new:quest passage 1", "mortal_location_link", "quest passage 1")]
    public void Build_LiteralValidIdentityRemainsActionable(
        string actor,
        string entityKind,
        string initialId)
    {
        var collection = entityKind == "mortal_location"
            ? "newLocations"
            : "newLinks";
        var issue = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath +
            $".worldMapUpdates.{collection}[0].description",
            "mortal_location_materialization_governed_field_missing",
            actor,
            "complete description",
            "missing",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            CreateContext(
                $"worldMapUpdates.{collection}[0]",
                entityKind,
                initialId,
                new[] { "description" }));

        Assert.Single(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.False(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Fact]
    public void Build_DoesNotAbsorbItemOwnedIssueOrItemTarget()
    {
        var location = CreateIssue(
            MortalLocationMaterializationContract.CurrentLocationPath + ".currentLocationData.locationStorages[0].name",
            "mortal_location_materialization_governed_field_missing",
            "mortal_location:new:locref_storage",
            "storage name",
            "missing",
            new[]
            {
                MortalLocationMaterializationContract.CurrentLocationPath,
                "game_state/inventory/items.json"
            },
            CreateContext(
                "currentLocationData",
                "mortal_location",
                "locref_storage",
                new[] { "locationStorages[0].name" }));
        var item = new ValidationIssue(
            "game_state/inventory/items.json.UpdateInventory[0].description",
            IssueSeverity.Error,
            "Malformed item.",
            code: "mortal_item_materialization_complete_field_missing",
            actor: "mortal_item:new:itemref_storage",
            section: "MortalItemMaterialization",
            expected: "description",
            actual: "missing",
            repairTargetFiles: new[] { "game_state/inventory/items.json" });

        var packet = Assert.Single(MortalLocationRepairPacketBuilder.Build(new[] { location, item }));

        Assert.Equal(
            new[] { MortalLocationMaterializationContract.CurrentLocationPath },
            packet.TargetFiles);
        Assert.DoesNotContain("game_state/inventory/items.json", packet.RequiredCompanionTargets);
        Assert.DoesNotContain(packet.ExactFieldCorrections, correction =>
            correction.Path.Contains("inventory/items.json", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("game_state/world/current_location.json", true)]
    [InlineData("game_state/world/world_map.json.worldMapUpdates.newLinks[0]", true)]
    [InlineData("game_state/world/location_identity_index.json", false)]
    [InlineData(MortalLocationStorageContentsState.StatePath, false)]
    [InlineData("game_state/control/pending_turn_snapshot.json", false)]
    [InlineData("game_state/control/mortal_bootstrap_scaffold.json", false)]
    [InlineData("game_state/inventory/items.json", false)]
    [InlineData("game_state/npcs/npc_core.json", false)]
    [InlineData("output/narrative_response.json", false)]
    public void GmTargetAllowlist_IsExplicitAndFailClosed(string path, bool expected)
    {
        Assert.Equal(expected, MortalLocationRepairPacketBuilder.IsGmAuthorableTarget(path));
    }

    [Fact]
    public void ProtectedLocationIdentityPathIsNeverGmAuthorable()
    {
        const string path = "GAME_STATE/WORLD/LOCATION_IDENTITY_INDEX.JSON.locationEntries[0]";

        Assert.True(MortalLocationRepairPacketBuilder.IsProtectedClientOwnedTarget(path));
        Assert.False(MortalLocationRepairPacketBuilder.IsGmAuthorableTarget(path));
    }

    [Theory]
    [InlineData(MortalLocationIdentityState.StatePath)]
    [InlineData(MortalLocationStorageContentsState.StatePath)]
    [InlineData("game_state/control/pending_turn_snapshot.json")]
    [InlineData(PendingTurnSnapshotAuthority.AuthorityPath)]
    public void Build_ProtectedRepairTargetFailsClosedInsteadOfBeingSilentlyDropped(
        string protectedTarget)
    {
        var issue = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath +
            ".worldMapUpdates.newLocations[0].description",
            "mortal_location_materialization_governed_field_missing",
            "mortal_location:new:locref_protected_target",
            "complete description",
            "missing",
            new[]
            {
                MortalLocationMaterializationContract.WorldMapPath,
                protectedTarget
            },
            CreateContext(
                "worldMapUpdates.newLocations[0]",
                "mortal_location",
                "locref_protected_target",
                new[] { "description" }));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    [Fact]
    public void Build_DuplicateEndpointPropertyFailsClosedBecauseItsOccurrenceIsNotAddressable()
    {
        var issue = CreateIssue(
            MortalLocationMaterializationContract.WorldMapPath +
            ".worldMapUpdates.newLinks[0].sourceLocationId",
            "mortal_location_materialization_duplicate_property",
            "mortal_location_link:new:linkref_duplicate_source",
            "unique exact property names",
            "sourceLocationId",
            new[] { MortalLocationMaterializationContract.WorldMapPath },
            CreateContext(
                "worldMapUpdates.newLinks[0]",
                "mortal_location_link",
                "linkref_duplicate_source",
                new[] { "sourceLocationId" }));

        Assert.Empty(MortalLocationRepairPacketBuilder.Build(new[] { issue }));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { issue }));
    }

    private static MortalLocationRepairContext CreateContext(
        string carrierPath,
        string entityKind,
        string? initialId,
        IReadOnlyList<string> repairableFields,
        string? existingId = null) =>
        new(
            carrierPath,
            entityKind,
            initialId,
            initialId == null ? null : "mlocmat_" + initialId.Replace(' ', '_'),
            repairableFields,
            existingId);

    private static ValidationIssue CreateIssue(
        string path,
        string code,
        string? actor,
        string expected,
        string actual,
        IReadOnlyList<string> targets,
        MortalLocationRepairContext context)
    {
        var issue = new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Malformed Mortal location package.",
            code: code,
            actor: actor,
            section: "MortalLocationMaterialization",
            expected: expected,
            actual: actual,
            repairHint: "Repair only the exact GM-owned field.",
            repairTargetFiles: targets);
        issue.MortalLocationRepairContext = context;
        return issue;
    }
}
