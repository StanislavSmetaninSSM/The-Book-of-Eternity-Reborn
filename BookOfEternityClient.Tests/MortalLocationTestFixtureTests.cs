using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLocationTestFixtureTests
{
    [Fact]
    public void CreateCanonicalLocationWithIdentity_PassesContractIndexAndPlanner()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "forest_lodge",
            "Лесная сторожка");
        using var locationDocument = JsonDocument.Parse(location.ToJsonString());
        Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLocation(
            locationDocument.RootElement,
            MortalLocationMaterializationContract.WorldMapPath + ".locations[0]"));
        var map = MortalLocationTestFixture.CreateWorldMap(location);
        var current = MortalLocationTestFixture.CreateCurrentProjection(location);
        var index = MortalLocationTestFixture.CreateIdentityIndex(location);
        var parsedIndex = MortalLocationIdentityState.Parse(index);
        Assert.Empty(parsedIndex.Issues);
        Assert.Empty(parsedIndex.ValidateCanonicalState(map));

        var planning = MortalLocationAcceptedTurnPlanner.Build(new MortalLocationAcceptedTurnInput(
            map,
            current,
            index,
            RawCurrentLocationData: null,
            RawWorldMapUpdates: null,
            Turn: 1));

        Assert.True(planning.Success, string.Join(Environment.NewLine, planning.Issues.Select(issue => issue.Message)));
    }

    [Fact]
    public void AcceptedTurnPlanner_DefaultIdentityAllocationIsStableForOneAcceptedInput()
    {
        var raw = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        var input = new MortalLocationAcceptedTurnInput(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["realm"] = "mortal_world",
                ["locations"] = new JsonArray(),
                ["links"] = new JsonArray()
            },
            PreTurnCurrentLocation: null,
            PreTurnIdentityIndex: MortalLocationIdentityState.CreateEmptyRoot(),
            RawCurrentLocationData: raw,
            RawWorldMapUpdates: null,
            Turn: 42);

        var cache = new MortalLocationAcceptedTurnPlanCache();
        var validationPlan = cache.GetOrBuild(input);
        var itemCompositionPlan = cache.GetOrBuild(input);
        var commitPlan = cache.GetOrBuild(input);

        Assert.True(validationPlan.Success);
        Assert.True(itemCompositionPlan.Success);
        Assert.True(commitPlan.Success);
        Assert.True(JsonNode.DeepEquals(
            validationPlan.Plan!.FinalWorldMap,
            itemCompositionPlan.Plan!.FinalWorldMap));
        Assert.True(JsonNode.DeepEquals(
            validationPlan.Plan.FinalWorldMap,
            commitPlan.Plan!.FinalWorldMap));
        Assert.True(JsonNode.DeepEquals(
            validationPlan.Plan.FinalIdentityIndex,
            commitPlan.Plan.FinalIdentityIndex));
        Assert.Same(validationPlan.Plan, itemCompositionPlan.Plan);
        Assert.Same(validationPlan.Plan, commitPlan.Plan);
    }

    private static readonly string[] LocationSections =
    {
        "presentation",
        "physical",
        "placement",
        "discovery",
        "difficulty",
        "chronicle",
        "factionControl",
        "actorBindings",
        "storageMetadata",
        "activeThreats",
        "loreBindings",
        "customStates",
        "topology"
    };

    [Fact]
    public void CreateRawLocation_HasNullPermanentIdAndEverySection()
    {
        var location = MortalLocationTestFixture.CreateRawLocation();

        Assert.True(location.ContainsKey("locationId"));
        Assert.Null(location["locationId"]);
        Assert.Equal(MortalLocationTestFixture.LocationInitialId, location["initialId"]!.GetValue<string>());
        Assert.False(location.ContainsKey("materializationReceipt"));

        foreach (var field in new[]
                 {
                     "features",
                     "eventDescriptions",
                     "factionControl",
                     "actorBindings",
                     "locationStorages",
                     "activeThreats",
                     "loreBindings",
                     "customStates"
                 })
        {
            Assert.IsType<JsonArray>(location[field]);
        }

        var envelope = location["materialization"]!.AsObject();
        Assert.Equal("mortal_location", envelope["entityKind"]!.GetValue<string>());
        Assert.Equal("mortal_world", envelope["realm"]!.GetValue<string>());
        Assert.Equal("world_map_creation", envelope["route"]!.GetValue<string>());
        Assert.Equal(MortalLocationTestFixture.LocationMaterializationId, envelope["materializationId"]!.GetValue<string>());
        Assert.Equal(MortalLocationTestFixture.LocationInitialId, envelope["initialId"]!.GetValue<string>());
        Assert.Equal(
            LocationSections.OrderBy(static value => value, StringComparer.Ordinal),
            envelope["sections"]!.AsObject().Select(static pair => pair.Key).OrderBy(static value => value, StringComparer.Ordinal));
    }

    [Fact]
    public void CreateCanonicalLocation_HasReceiptAndNoTemporarySelectors()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();

        Assert.Equal(MortalLocationTestFixture.LocationId, location["locationId"]!.GetValue<string>());
        Assert.False(location.ContainsKey("initialId"));
        Assert.False(location.ContainsKey("parentInitialId"));

        var receipt = location["materializationReceipt"]!.AsObject();
        Assert.Equal(MortalLocationTestFixture.LocationReceiptId, receipt["receiptId"]!.GetValue<string>());
        Assert.Equal(MortalLocationTestFixture.LocationId, receipt["locationId"]!.GetValue<string>());
        Assert.Equal(MortalLocationTestFixture.LocationInitialId, receipt["initialId"]!.GetValue<string>());
        Assert.Equal(MortalLocationTestFixture.LocationMaterializationId, receipt["materializationId"]!.GetValue<string>());
        Assert.Matches("^sha256:[0-9a-f]{64}$", receipt["seal"]!.GetValue<string>());
    }

    [Fact]
    public void CreateCanonicalLink_BindsExactEndpointsAndIndex()
    {
        const string targetLocationId = "loc_test_watchtower";
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var link = MortalLocationTestFixture.CreateCanonicalLink(
            MortalLocationTestFixture.LocationId,
            targetLocationId);
        var index = MortalLocationTestFixture.CreateIdentityIndex(location, link);

        Assert.Equal(MortalLocationTestFixture.LinkId, link["linkId"]!.GetValue<string>());
        Assert.Equal(MortalLocationTestFixture.LocationId, link["sourceLocationId"]!.GetValue<string>());
        Assert.Equal(targetLocationId, link["targetLocationId"]!.GetValue<string>());
        Assert.False(link.ContainsKey("sourceInitialId"));
        Assert.False(link.ContainsKey("targetInitialId"));

        var linkEntry = Assert.Single(index["linkEntries"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(MortalLocationTestFixture.LinkId, linkEntry["linkId"]!.GetValue<string>());
        Assert.Equal(MortalLocationTestFixture.LocationId, linkEntry["sourceLocationId"]!.GetValue<string>());
        Assert.Equal(targetLocationId, linkEntry["targetLocationId"]!.GetValue<string>());

        var identities = new[]
        {
            MortalLocationTestFixture.LocationInitialId,
            MortalLocationTestFixture.LocationId,
            MortalLocationTestFixture.LocationMaterializationId,
            MortalLocationTestFixture.LocationReceiptId,
            MortalLocationTestFixture.LinkInitialId,
            MortalLocationTestFixture.LinkId,
            MortalLocationTestFixture.LinkMaterializationId,
            MortalLocationTestFixture.LinkReceiptId
        };
        Assert.Equal(identities.Length, identities.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void CreateCurrentProjection_MatchesCanonicalSharedFields()
    {
        var canonical = MortalLocationTestFixture.CreateCanonicalLocation();
        var current = MortalLocationTestFixture.CreateCurrentProjection(canonical);

        foreach (var field in new[]
                 {
                     "locationId",
                     "realm",
                     "name",
                     "displayName",
                     "purpose",
                     "description",
                     "image_prompt",
                     "locationType",
                     "biome",
                     "biomeDescription",
                     "indoorType",
                     "features",
                     "region",
                     "parentLocationId",
                     "coordinates",
                     "discovery",
                     "internalDifficulty",
                     "externalDifficulty",
                     "lastEventsDescription",
                     "eventDescriptions",
                     "factionControl",
                     "actorBindings",
                     "locationStorages",
                     "activeThreats",
                     "loreBindings",
                     "customStates",
                     "materialization",
                     "materializationReceipt"
                 })
        {
            Assert.True(JsonNode.DeepEquals(canonical[field], current[field]), $"Shared field '{field}' differs.");
        }

        Assert.IsType<JsonObject>(current["currentWeather"]);
        Assert.IsType<JsonArray>(current["currentInteractions"]);
    }

    [Fact]
    public void CreateReceiptlessNegative_IsExplicitlyInvalid()
    {
        var invalid = MortalLocationTestFixture.CreateReceiptlessNegative();

        Assert.StartsWith("[INVALID FIXTURE: receiptless]", invalid["name"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(MortalLocationTestFixture.LocationId, invalid["locationId"]!.GetValue<string>());
        Assert.False(invalid.ContainsKey("initialId"));
        Assert.False(invalid.ContainsKey("materializationReceipt"));
    }

    [Fact]
    public void Factories_ReturnIndependentJsonTrees()
    {
        var first = MortalLocationTestFixture.CreateCanonicalLocation();
        var second = MortalLocationTestFixture.CreateCanonicalLocation();
        first["coordinates"]!["x"] = 999;
        first["materialization"]!["sections"]!["presentation"]!["reason"] = "mutation";

        Assert.Equal(14, second["coordinates"]!["x"]!.GetValue<int>());
        Assert.Null(second["materialization"]!["sections"]!["presentation"]!["reason"]);

        var map = MortalLocationTestFixture.CreateWorldMap(second);
        second["name"] = "Изменённое имя";
        Assert.Equal(
            "Чёрный брод",
            map["locations"]![0]!["name"]!.GetValue<string>());
    }
}
