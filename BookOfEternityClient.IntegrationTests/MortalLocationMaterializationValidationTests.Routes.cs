using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLocationMaterializationValidationTests
{
    [Fact]
    public async Task ValidateAcceptedTurnRawState_UsesLocationContractBeforeNormalization()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var raw = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        raw.Remove("customStates");
        await context.WriteRawTurnStateAsync(raw, worldMapUpdates: null);

        var issues = await context.Validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        var issue = Assert.Single(issues, candidate =>
            candidate.FilePath ==
                "game_state/world/current_location.json.currentLocationData.customStates" &&
            candidate.Code == "mortal_location_materialization_governed_field_missing");
        var repair = Assert.IsType<BookOfEternityClient.Services.MortalLocationRepairContext>(
            issue.MortalLocationRepairContext);
        Assert.Equal("currentLocationData", repair.CarrierPath);
        Assert.Equal(new[] { "customStates" }, repair.RepairableFields);
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_PhysicalShapeRepairNamesEveryExactLeaf()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var raw = CreateRawLocation(
            "locref_physical_shape_repair",
            "mlocmat_physical_shape_repair",
            x: 70,
            route: "world_map_creation");
        raw["biome"] = null;
        raw["biomeDescription"] = null;
        raw["indoorType"] = "buried_archive";
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: Updates(raw));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        var physicalIssues = issues
            .Where(issue => issue.Code == "mortal_location_materialization_physical_shape_invalid")
            .OrderBy(issue => issue.FilePath, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(3, physicalIssues.Length);
        Assert.Equal(
            new[]
            {
                "game_state/world/world_map.json.worldMapUpdates.newLocations[0].biome",
                "game_state/world/world_map.json.worldMapUpdates.newLocations[0].biomeDescription",
                "game_state/world/world_map.json.worldMapUpdates.newLocations[0].indoorType"
            },
            physicalIssues.Select(issue => issue.FilePath));
        var repairContext = Assert.IsType<MortalLocationRepairContext>(
            physicalIssues[0].MortalLocationRepairContext);
        Assert.Equal(
            new[] { "biome", "biomeDescription", "indoorType" },
            repairContext.RepairableFields);
        Assert.Equal(42, repairContext.ExpectedSourceTurn);
        Assert.Equal("turn_outcome", repairContext.ExpectedSourceAuthorityKind);
        Assert.Equal("turn_42", repairContext.ExpectedSourceAuthorityId);

        Assert.False(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
        var packet = Assert.Single(MortalLocationRepairPacketBuilder.Build(issues));
        Assert.Equal(
            physicalIssues.Select(issue => issue.FilePath),
            packet.ExactFieldCorrections.Select(correction => correction.Path));
        Assert.DoesNotContain(
            packet.ExactFieldCorrections,
            correction => correction.Path.EndsWith(".locationType", StringComparison.Ordinal));
        Assert.Contains("sourceTurn=42", packet.ExpectedAuthority);
        Assert.Contains("sourceAuthority=turn_outcome:turn_42", packet.ExpectedAuthority);
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_AmbiguousPhysicalShapeFailsClosed()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var raw = CreateRawLocation(
            "locref_ambiguous_physical_shape",
            "mlocmat_ambiguous_physical_shape",
            x: 69,
            route: "world_map_creation");
        raw["locationType"] = "liminal";
        raw["biome"] = null;
        raw["biomeDescription"] = null;
        raw["indoorType"] = null;
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: Updates(raw));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        var physicalIssue = Assert.Single(issues, issue =>
            issue.Code == "mortal_location_materialization_physical_shape_ambiguous");
        var repairContext = Assert.IsType<MortalLocationRepairContext>(
            physicalIssue.MortalLocationRepairContext);
        Assert.Empty(repairContext.RepairableFields);
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(issues));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_SelfParentRepairsOnlyExactParentSelector()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var raw = CreateRawLocation(
            "locref_self_parent_repair",
            "mlocmat_self_parent_repair",
            x: 68,
            route: "world_map_creation");
        raw["parentInitialId"] = "locref_self_parent_repair";
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: Updates(raw));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        var cycle = Assert.Single(issues, issue =>
            issue.Code == "mortal_location_materialization_parent_cycle");
        Assert.Equal(
            "game_state/world/world_map.json.worldMapUpdates.newLocations[0].parentInitialId",
            cycle.FilePath);
        var repairContext = Assert.IsType<MortalLocationRepairContext>(
            cycle.MortalLocationRepairContext);
        Assert.Equal(new[] { "parentInitialId" }, repairContext.RepairableFields);
        Assert.False(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
        var packet = Assert.Single(MortalLocationRepairPacketBuilder.Build(issues));
        var correction = Assert.Single(packet.ExactFieldCorrections);
        Assert.Equal(cycle.FilePath, correction.Path);
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_RunsPlannerAndBuildsExactCoordinateConflictRepair()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var first = CreateRawLocation(
            "locref_raw_collision_a",
            "mlocmat_raw_collision_a",
            x: 71,
            route: "world_map_creation");
        var second = CreateRawLocation(
            "locref_raw_collision_b",
            "mlocmat_raw_collision_b",
            x: 71,
            route: "world_map_creation");
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: Updates(first, second));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        var collision = Assert.Single(issues, issue =>
            issue.Code == "mortal_location_materialization_coordinate_collision" &&
            issue.FilePath ==
            "game_state/world/world_map.json.worldMapUpdates.newLocations[1].coordinates");
        var repairContext = Assert.IsType<MortalLocationRepairContext>(
            collision.MortalLocationRepairContext);
        Assert.Equal("worldMapUpdates.newLocations[1]", repairContext.CarrierPath);
        Assert.Equal("locref_raw_collision_b", repairContext.InitialId);
        var packet = Assert.Single(MortalLocationRepairPacketBuilder.Build(issues));
        Assert.Equal(
            "mortal_location:new:locref_raw_collision_b",
            Assert.Single(packet.CanonicalActorNames));
        Assert.Equal(
            new[] { "game_state/world/world_map.json.worldMapUpdates.newLocations[1].coordinates" },
            packet.Conflicts);
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_ExactExistingFullResendIsReducible()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var canonical = MortalLocationTestFixture.CreateCanonicalLocation();
        await context.WritePreTurnCanonicalStateAsync(canonical);
        await context.CaptureValidatedPendingSnapshotAsync();
        var resend = new JsonObject
        {
            ["locationId"] = MortalLocationTestFixture.LocationId,
            ["description"] = canonical["description"]!.DeepClone(),
            ["lastEventsDescription"] = "Герой вернулся к броду.",
            ["currentWeather"] = new JsonObject(),
            ["currentInteractions"] = new JsonArray()
        };
        await context.WriteRawTurnStateAsync(resend, worldMapUpdates: null);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        var fullResend = Assert.Single(issues, issue =>
            issue.Code == "mortal_location_materialization_existing_full_resend");
        Assert.DoesNotContain(issues, issue =>
            issue.Code?.Contains("identity", StringComparison.Ordinal) == true ||
            issue.Code?.Contains("receipt", StringComparison.Ordinal) == true);
        Assert.False(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
        var packet = Assert.Single(MortalLocationRepairPacketBuilder.Build(issues));
        Assert.Equal("mortal_location:existing:" + MortalLocationTestFixture.LocationId, fullResend.Actor);
        Assert.Equal("current_selection", packet.TransitionClass);
        Assert.Equal(
            new[] { MortalLocationMaterializationContract.CurrentLocationPath },
            packet.TargetFiles);
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_ChangedExistingEnvelopeFailsClosed()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var canonical = MortalLocationTestFixture.CreateCanonicalLocation();
        await context.WritePreTurnCanonicalStateAsync(canonical);
        await context.CaptureValidatedPendingSnapshotAsync();
        var forged = canonical.DeepClone().AsObject();
        forged["materialization"]!["materializationId"] = "mlocmat_forged_existing_resend";
        await context.WriteRawTurnStateAsync(forged, worldMapUpdates: null);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_materialization_identity_conflict" &&
            issue.FilePath.EndsWith(".materialization", StringComparison.Ordinal));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_ExactHistoricalEnvelopeResendFailsClosed()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var canonical = MortalLocationTestFixture.CreateCanonicalLocation();
        await context.WritePreTurnCanonicalStateAsync(canonical);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteRawTurnStateAsync(canonical.DeepClone().AsObject(), worldMapUpdates: null);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_materialization_identity_conflict" &&
            issue.FilePath.EndsWith(".materialization", StringComparison.Ordinal));
        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_materialization_receipt_conflict" &&
            issue.FilePath.EndsWith(".materializationReceipt", StringComparison.Ordinal));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(issues));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_ExistingResendClientProtocolFieldFailsClosed()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var canonical = MortalLocationTestFixture.CreateCanonicalLocation();
        await context.WritePreTurnCanonicalStateAsync(canonical);
        await context.CaptureValidatedPendingSnapshotAsync();
        var resend = new JsonObject
        {
            ["locationId"] = MortalLocationTestFixture.LocationId,
            ["lastEventsDescription"] = "Герой вернулся к броду.",
            ["currentWeather"] = new JsonObject(),
            ["currentInteractions"] = new JsonArray(),
            ["requestId"] = "forged_existing_resend_request"
        };
        await context.WriteRawTurnStateAsync(resend, worldMapUpdates: null);

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_materialization_gm_authored_client_field" &&
            issue.FilePath.EndsWith(".requestId", StringComparison.Ordinal));
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(issues));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
    }

    [Fact]
    public async Task ValidateCrossReferences_ExistingCoordinateMismatchBuildsOnlyBoundedLocationPacket()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var canonical = MortalLocationTestFixture.CreateCanonicalLocation();
        await context.WritePreTurnCanonicalStateAsync(canonical);
        await context.CaptureValidatedPendingSnapshotAsync();
        await context.WriteRawTurnStateAsync(
            new JsonObject
            {
                ["locationId"] = MortalLocationTestFixture.LocationId,
                ["coordinates"] = new JsonObject { ["x"] = 999, ["y"] = 0, ["z"] = 0 },
                ["lastEventsDescription"] = "Герой вернулся к броду.",
                ["currentWeather"] = new JsonObject(),
                ["currentInteractions"] = new JsonArray()
            },
            worldMapUpdates: null);

        var issues = await context.Validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.CrossReferences));
        var mismatch = Assert.Single(issues, issue =>
            issue.Code == "current_location_coordinates_mismatch");
        Assert.Equal(
            "mortal_location:existing:" + MortalLocationTestFixture.LocationId,
            mismatch.Actor);
        Assert.NotNull(mismatch.MortalLocationRepairContext);

        var builder = typeof(GameEngine).GetMethod(
            "BuildValidationRepairHarnessPackets",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(builder);
        var packets = builder!.Invoke(null, new object?[] { new[] { mismatch }, null });
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            packets,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var packet = Assert.Single(document.RootElement.EnumerateArray());

        Assert.Equal("mortal_location_materialization_repair", packet.GetProperty("kind").GetString());
        Assert.Equal("current_selection", packet.GetProperty("transitionClass").GetString());
        Assert.DoesNotContain(
            "mortal_bootstrap_scaffold.json",
            packet.GetRawText(),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_DuplicateCreationRouteIsFailClosedBeforeNormalization()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var current = CreateRawLocation(
            "locref_raw_duplicate",
            "mlocmat_raw_duplicate",
            x: 72,
            route: "current_scene_creation");
        var remote = current.DeepClone().AsObject();
        remote["materialization"]!["route"] = "world_map_creation";
        await context.WriteRawTurnStateAsync(current, Updates(remote));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_materialization_duplicate_creation_route");
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_InvalidEnvelopeParentFailsClosedWithoutBroadRepair()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            });
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();
        var raw = CreateRawLocation(
            "locref_invalid_envelope_parent",
            "mlocmat_invalid_envelope_parent",
            x: 73,
            route: "world_map_creation");
        raw["materialization"]!["schemaVersion"] = 2;
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: Updates(raw));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        var invalidEnvelope = Assert.Single(issues, issue =>
            issue.Code == "mortal_location_materialization_invalid_envelope" &&
            issue.FilePath.EndsWith(".materialization", StringComparison.Ordinal));
        var repairContext = Assert.IsType<MortalLocationRepairContext>(
            invalidEnvelope.MortalLocationRepairContext);
        Assert.DoesNotContain("materialization", repairContext.RepairableFields);
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(issues));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_LinkUpdateRemovalConflictCannotRepairLinkIdentity()
    {
        var accepted = CreateAcceptedDirectedTopology();
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            accepted.FinalWorldMap);
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            accepted.FinalCurrentLocation);
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            accepted.FinalIdentityIndex);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: new JsonObject
            {
                ["linkUpdates"] = new JsonArray(new JsonObject
                {
                    ["linkId"] = accepted.LinkId,
                    ["access"] = new JsonObject
                    {
                        ["state"] = "conditional",
                        ["reason"] = "Нужен бронзовый ключ.",
                        ["requirements"] = new JsonArray(new JsonObject
                        {
                            ["kind"] = "item_capability",
                            ["value"] = "bronze_key"
                        })
                    }
                }),
                ["linkRemovals"] = new JsonArray(new JsonObject
                {
                    ["linkId"] = accepted.LinkId,
                    ["sourceLocationId"] = accepted.SourceLocationId,
                    ["targetLocationId"] = accepted.TargetLocationId,
                    ["reason"] = "Проход обрушился в тот же ход."
                })
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        var conflict = Assert.Single(issues, issue =>
            issue.Code == "mortal_location_link_transition_conflict");
        Assert.EndsWith(
            ".worldMapUpdates.linkRemovals[0].linkId",
            conflict.FilePath,
            StringComparison.Ordinal);
        var repairContext = Assert.IsType<MortalLocationRepairContext>(
            conflict.MortalLocationRepairContext);
        Assert.DoesNotContain("linkId", repairContext.RepairableFields);
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(issues));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_DanglingLinkEndpointRepairsExactSelectorField()
    {
        var accepted = CreateAcceptedDirectedTopology();
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            accepted.FinalWorldMap);
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            accepted.FinalCurrentLocation);
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            accepted.FinalIdentityIndex);
        await context.CaptureValidatedPendingSnapshotAsync(turn: 43);
        var link = CreateRawLink(
            "linkref_dangling_exact_selector",
            "mlinkmat_dangling_exact_selector",
            "unused_source",
            "unused_target");
        link["sourceLocationId"] = "loc_missing_exact_endpoint";
        link["sourceInitialId"] = null;
        link["targetLocationId"] = accepted.TargetLocationId;
        link["targetInitialId"] = null;
        link["materialization"]!["sourceTurn"] = 43;
        link["materialization"]!["sourceAuthority"] = new JsonObject
        {
            ["kind"] = "turn_outcome",
            ["authorityId"] = "turn_43"
        };
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: new JsonObject
            {
                ["newLocations"] = new JsonArray(),
                ["newLinks"] = new JsonArray(link)
            });

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        var endpoint = Assert.Single(issues, issue =>
            issue.Code == "mortal_location_link_endpoint_unresolved");
        Assert.EndsWith(
            ".worldMapUpdates.newLinks[0].sourceLocationId",
            endpoint.FilePath,
            StringComparison.Ordinal);
        var packets = MortalLocationRepairPacketBuilder.Build(issues);
        Assert.True(
            packets.Count == 1,
            string.Join(
                Environment.NewLine,
                issues.Select(issue =>
                    $"{issue.Code} | {issue.FilePath} | actor={issue.Actor ?? "<null>"} | " +
                    $"carrier={issue.MortalLocationRepairContext?.CarrierPath ?? "<null>"} | " +
                    $"fields={string.Join(',', issue.MortalLocationRepairContext?.RepairableFields ?? Array.Empty<string>())}")));
        var packet = packets[0];
        var correction = Assert.Single(packet.ExactFieldCorrections);
        Assert.EndsWith(".sourceLocationId", correction.Path, StringComparison.Ordinal);
        Assert.DoesNotContain(".source", packet.ExactFieldCorrections.Select(item => item.Path));
    }

    [Fact]
    public void Build_CurrentCreationAddsOneCanonicalMapLocationAndSelectsIt()
    {
        var raw = CreateRawLocation(
            "locref_current",
            "mlocmat_current",
            x: 3,
            route: "current_scene_creation");

        var result = Build(rawCurrentLocationData: raw);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<BookOfEternityClient.Services.MortalLocationAcceptedTurnPlan>(result.Plan);
        var location = Assert.Single(plan.FinalWorldMap["locations"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(location["locationId"]!.GetValue<string>(), plan.FinalCurrentLocation!["locationId"]!.GetValue<string>());
        Assert.Single(plan.FinalIdentityIndex["locationEntries"]!.AsArray());
        Assert.False(location.ContainsKey("initialId"));
        Assert.NotNull(location["materializationReceipt"]);
    }

    [Fact]
    public void Build_OrdinaryCreationRejectsForgedTurnOutcomeAuthorityId()
    {
        var raw = CreateRawLocation(
            "locref_forged_turn_authority",
            "mlocmat_forged_turn_authority",
            x: 74,
            route: "world_map_creation");
        raw["materialization"]!["sourceAuthority"]!["authorityId"] = "forged_authority";

        var result = Build(rawWorldMapUpdates: Updates(raw), turn: 42);

        Assert.False(result.Success);
        var issue = Assert.Single(result.Issues, candidate =>
            candidate.Code == "mortal_location_materialization_source_authority_mismatch");
        Assert.EndsWith(
            ".materialization.sourceAuthority",
            issue.FilePath,
            StringComparison.Ordinal);
        Assert.Equal("turn_outcome:turn_42", issue.Expected);
    }

    [Fact]
    public async Task ValidateAcceptedTurnRawState_ForgedTurnAuthorityFailsClosedWithoutRepairPacket()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            EmptyWorldMap());
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync(turn: 42);
        var raw = CreateRawLocation(
            "locref_forged_turn_authority_packet",
            "mlocmat_forged_turn_authority_packet",
            x: 75,
            route: "world_map_creation");
        raw["materialization"]!["sourceAuthority"]!["authorityId"] = "forged_authority";
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: Updates(raw));

        var issues = await context.Validator
            .ValidateAcceptedTurnRawMortalLocationMaterializationAsync();

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_materialization_source_authority_mismatch");
        Assert.Empty(MortalLocationRepairPacketBuilder.Build(issues));
        Assert.True(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues));
    }

    [Fact]
    public void Build_TwoRemoteCreationsRemainIndependentAndDoNotSelectCurrent()
    {
        var first = CreateRawLocation(
            "locref_remote_a",
            "mlocmat_remote_a",
            x: 4,
            route: "world_map_creation");
        var second = CreateRawLocation(
            "locref_remote_b",
            "mlocmat_remote_b",
            x: 5,
            route: "world_map_creation");

        var result = Build(rawWorldMapUpdates: Updates(first, second));

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<BookOfEternityClient.Services.MortalLocationAcceptedTurnPlan>(result.Plan);
        Assert.Equal(2, plan.FinalWorldMap["locations"]!.AsArray().Count);
        Assert.Null(plan.FinalCurrentLocation);
        Assert.Equal(2, plan.LocationIdsByInitialId.Count);
        Assert.Equal(2, plan.FinalIdentityIndex["locationEntries"]!.AsArray().Count);
        Assert.Equal(2, plan.LocationIdsByInitialId.Values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Build_SameCreationInCurrentAndRemoteRoutesFailsClosed()
    {
        var current = CreateRawLocation(
            "locref_duplicate_route",
            "mlocmat_duplicate_route",
            x: 6,
            route: "current_scene_creation");
        var remote = current.DeepClone().AsObject();
        remote["materialization"]!["route"] = "world_map_creation";

        var result = Build(
            rawCurrentLocationData: current,
            rawWorldMapUpdates: Updates(remote));

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_duplicate_creation_route");
    }

    [Fact]
    public void Build_ExistingMovementFullResendFailsClosed()
    {
        var canonical = MortalLocationTestFixture.CreateCanonicalLocation();
        var rawMove = new JsonObject
        {
            ["locationId"] = MortalLocationTestFixture.LocationId,
            ["description"] = "Полная семантика не должна пересылаться через движение.",
            ["lastEventsDescription"] = "Герой вернулся к броду.",
            ["currentWeather"] = new JsonObject(),
            ["currentInteractions"] = new JsonArray()
        };

        var result = Build(
            preTurnWorldMap: MortalLocationTestFixture.CreateWorldMap(canonical),
            preTurnCurrentLocation: MortalLocationTestFixture.CreateCurrentProjection(canonical),
            preTurnIdentityIndex: MortalLocationTestFixture.CreateIdentityIndex(canonical),
            rawCurrentLocationData: rawMove);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_existing_full_resend");
    }

    [Fact]
    public void Build_SameTurnDirectedLinkResolvesExactTemporaryEndpoints()
    {
        var source = CreateRawLocation(
            "locref_link_source",
            "mlocmat_link_source",
            x: 13,
            route: "world_map_creation");
        var target = CreateRawLocation(
            "locref_link_target",
            "mlocmat_link_target",
            x: 14,
            route: "world_map_creation");
        source["materialization"]!["sections"]!["topology"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        target["materialization"]!["sections"]!["topology"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        var link = MortalLocationTestFixture.CreateRawLink("unused", "unused");
        link["sourceLocationId"] = null;
        link["sourceInitialId"] = "locref_link_source";
        link["targetLocationId"] = null;
        link["targetInitialId"] = "locref_link_target";

        var updates = Updates(source, target);
        updates["newLinks"]!.AsArray().Add(link);
        var result = Build(rawWorldMapUpdates: updates);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<BookOfEternityClient.Services.MortalLocationAcceptedTurnPlan>(result.Plan);
        var acceptedLink = Assert.Single(plan.FinalWorldMap["links"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(
            plan.LocationIdsByInitialId["locref_link_source"],
            acceptedLink["sourceLocationId"]!.GetValue<string>());
        Assert.Equal(
            plan.LocationIdsByInitialId["locref_link_target"],
            acceptedLink["targetLocationId"]!.GetValue<string>());
        Assert.NotNull(acceptedLink["materializationReceipt"]);
        Assert.Single(plan.FinalIdentityIndex["linkEntries"]!.AsArray());
    }
}
