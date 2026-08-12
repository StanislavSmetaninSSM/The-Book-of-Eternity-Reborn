using System.Text.Json.Nodes;
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
