using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLocationMaterializationValidationTests
{
    [Theory]
    [InlineData("region")]
    [InlineData("coordinates")]
    [InlineData("internalDifficulty")]
    [InlineData("externalDifficulty")]
    [InlineData("lastEventsDescription")]
    [InlineData("features")]
    [InlineData("eventDescriptions")]
    [InlineData("factionControl")]
    [InlineData("actorBindings")]
    [InlineData("locationStorages")]
    [InlineData("activeThreats")]
    [InlineData("loreBindings")]
    [InlineData("customStates")]
    public void Build_MissingGovernedSemanticFailsClosed(string field)
    {
        var raw = CreateRawLocation(
            "locref_missing_" + field,
            "mlocmat_missing_" + field,
            x: 2,
            route: "world_map_creation");
        raw.Remove(field);

        var result = Build(rawWorldMapUpdates: Updates(raw));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_governed_field_missing");
    }

    [Theory]
    [InlineData("hidden")]
    [InlineData("rumored")]
    [InlineData("discovered")]
    [InlineData("visited")]
    public void Build_CompleteDiscoveryTierIsAccepted(string discoveryTier)
    {
        var raw = CreateRawLocation(
            "locref_discovery_" + discoveryTier,
            "mlocmat_discovery_" + discoveryTier,
            x: 10,
            route: "world_map_creation");
        raw["discovery"] = discoveryTier switch
        {
            "hidden" => new JsonObject
            {
                ["tier"] = "hidden",
                ["audience"] = "gm_only",
                ["rumorSummary"] = null
            },
            "rumored" => new JsonObject
            {
                ["tier"] = "rumored",
                ["audience"] = "player_known",
                ["rumorSummary"] = "Говорят о далёком месте."
            },
            _ => new JsonObject
            {
                ["tier"] = discoveryTier,
                ["audience"] = "player_known",
                ["rumorSummary"] = null
            }
        };

        var result = Build(rawWorldMapUpdates: Updates(raw));

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
    }

    [Fact]
    public void Build_CompleteIndoorLocationIsAccepted()
    {
        var raw = CreateRawLocation(
            "locref_indoor_archive",
            "mlocmat_indoor_archive",
            x: 12,
            route: "world_map_creation");
        raw["locationType"] = "indoor";
        raw["biome"] = null;
        raw["biomeDescription"] = null;
        raw["indoorType"] = "subterranean_archive";

        var result = Build(rawWorldMapUpdates: Updates(raw));

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
    }

    [Fact]
    public void Build_ReceiptlessPreTurnLocationFailsClosed()
    {
        var canonical = MortalLocationTestFixture.CreateCanonicalLocation();
        var receiptless = MortalLocationTestFixture.CreateReceiptlessNegative();
        var result = Build(
            preTurnWorldMap: MortalLocationTestFixture.CreateWorldMap(receiptless),
            preTurnIdentityIndex: MortalLocationTestFixture.CreateIdentityIndex(canonical));

        Assert.False(result.Success);
        Assert.Null(result.Plan);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_receipt_required");
    }

    [Fact]
    public void Build_NewLocationCoordinateCollisionFailsBeforeIdAllocation()
    {
        var first = CreateRawLocation(
            "locref_collision_a",
            "mlocmat_collision_a",
            x: 7,
            route: "world_map_creation");
        var second = CreateRawLocation(
            "locref_collision_b",
            "mlocmat_collision_b",
            x: 7,
            route: "world_map_creation");
        var result = Build(rawWorldMapUpdates: Updates(first, second));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_coordinate_collision");
    }

    [Fact]
    public void Build_SameTurnParentCycleFailsClosed()
    {
        var first = CreateRawLocation(
            "locref_parent_a",
            "mlocmat_parent_a",
            x: 8,
            route: "world_map_creation");
        var second = CreateRawLocation(
            "locref_parent_b",
            "mlocmat_parent_b",
            x: 9,
            route: "world_map_creation");
        first["parentInitialId"] = "locref_parent_b";
        second["parentInitialId"] = "locref_parent_a";

        var result = Build(rawWorldMapUpdates: Updates(first, second));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_parent_cycle");
    }

    [Fact]
    public void Build_ExplicitlyIsolatedRemoteLocationIsAccepted()
    {
        var isolated = CreateRawLocation(
            "locref_isolated",
            "mlocmat_isolated",
            x: 11,
            route: "world_map_creation");

        var result = Build(rawWorldMapUpdates: Updates(isolated));

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        var accepted = Assert.Single(plan.FinalWorldMap["locations"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("empty_by_design", accepted["materialization"]!["sections"]!["topology"]!["disposition"]!.GetValue<string>());
        Assert.NotNull(accepted["materializationReceipt"]);
    }

    private static MortalLocationAcceptedTurnPlanningResult Build(
        JsonObject? preTurnWorldMap = null,
        JsonObject? preTurnCurrentLocation = null,
        JsonObject? preTurnIdentityIndex = null,
        JsonObject? rawCurrentLocationData = null,
        JsonObject? rawWorldMapUpdates = null,
        JsonObject? preTurnStorageContents = null,
        JsonObject? rawFactionCore = null,
        int turn = 42)
    {
        var input = new MortalLocationAcceptedTurnInput(
            preTurnWorldMap ?? EmptyWorldMap(),
            preTurnCurrentLocation,
            preTurnIdentityIndex ?? MortalLocationIdentityState.CreateEmptyRoot(),
            rawCurrentLocationData,
            rawWorldMapUpdates,
            Turn: turn,
            RawFactionCore: rawFactionCore,
            PreTurnStorageContents: preTurnStorageContents);
        return MortalLocationAcceptedTurnPlanner.Build(input, CreateIdentityFactory());
    }

    private static MortalLocationIdentityFactory CreateIdentityFactory()
    {
        var next = 1;
        return new MortalLocationIdentityFactory(() =>
            new Guid(next++, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    private static JsonObject EmptyWorldMap() =>
        new()
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };

    private static JsonObject Updates(params JsonObject[] locations) =>
        new()
        {
            ["newLocations"] = new JsonArray(
                locations.Select(static location => (JsonNode?)location.DeepClone()).ToArray()),
            ["newLinks"] = new JsonArray()
        };

    private static JsonObject CreateRawLocation(
        string initialId,
        string materializationId,
        int x,
        string route)
    {
        var location = MortalLocationTestFixture.CreateRawLocation(route);
        location["initialId"] = initialId;
        location["name"] = "Локация " + initialId;
        location["displayName"] = "Локация " + initialId;
        location["coordinates"]!["x"] = x;
        location["materialization"]!["initialId"] = initialId;
        location["materialization"]!["materializationId"] = materializationId;
        return location;
    }
}
