using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLocationMaterializationValidationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StorageAddition_EmptyBirthPreservesOriginalReceiptAndAddsHistory(bool current)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var map = MortalLocationTestFixture.CreateWorldMap(location);
        var index = MortalLocationTestFixture.CreateIdentityIndex(location);
        var projection = current ? MortalLocationTestFixture.CreateCurrentProjection(location) : null;
        var storage = MortalLocationTestFixture.CreateStorageMetadata("storage_added", "Новый сундук", false, new JsonArray());
        storage["capacity"] = 0; storage["volume"] = 0;
        var updates = new JsonObject { ["storagesToAdd"] = new JsonArray(new JsonObject
        { ["targetLocationId"] = MortalLocationTestFixture.LocationId, ["storage"] = storage }) };
        var beforeMap = map.ToJsonString(); var beforeIndex = index.ToJsonString(); var beforeUpdates = updates.ToJsonString();
        var result = Build(map, projection, index, rawWorldMapUpdates: updates, turn: 43);
        Assert.True(result.Success, string.Join("\n", result.Issues.Select(issue => issue.Code + ": " + issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        var finalLocation = Assert.Single(plan.FinalWorldMap["locations"]!.AsArray())!;
        var metadata = Assert.Single(finalLocation["locationStorages"]!.AsArray())!;
        Assert.Equal("storage_added", metadata["storageId"]!.GetValue<string>());
        Assert.False(metadata["hasFullAccess"]!.GetValue<bool>());
        Assert.Null(metadata["contents"]);
        Assert.True(JsonNode.DeepEquals(location["materialization"], finalLocation["materialization"]));
        Assert.True(JsonNode.DeepEquals(location["materializationReceipt"], finalLocation["materializationReceipt"]));
        var transition = Assert.Single(plan.FinalIdentityIndex["locationEntries"]![0]!["transitions"]!.AsArray())!;
        Assert.Equal("storage_addition", transition["kind"]!.GetValue<string>());
        Assert.Equal("storage_added", transition["childId"]!.GetValue<string>());
        Assert.Equal(43, transition["turn"]!.GetValue<int>());
        Assert.Equal("turn_outcome", transition["sourceAuthorityKind"]!.GetValue<string>());
        Assert.Equal("turn_43", transition["sourceAuthorityId"]!.GetValue<string>());
        Assert.Equal("worldMapUpdates.storagesToAdd[0]", transition["operationRef"]!.GetValue<string>());
        Assert.Equal("absent", transition["beforeState"]!["state"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(metadata, transition["afterState"]));
        Assert.Empty(MortalLocationIdentityState.Parse(plan.FinalIdentityIndex).ValidateCanonicalState(plan.FinalWorldMap));
        if (current) Assert.Single(plan.FinalCurrentLocation!["locationStorages"]!.AsArray());
        else Assert.Null(plan.FinalCurrentLocation);
        Assert.DoesNotContain(MortalLocationStorageContentsState.StatePath, plan.TouchedPaths);
        Assert.Equal(beforeMap, map.ToJsonString()); Assert.Equal(beforeIndex, index.ToJsonString()); Assert.Equal(beforeUpdates, updates.ToJsonString());
    }
    [Theory]
    [InlineData("unknown", "mortal_location_storage_addition_target_unresolved")]
    [InlineData("case", "mortal_location_storage_addition_target_unresolved")]
    [InlineData("space", "mortal_location_storage_addition_target_unresolved")]
    [InlineData("temporary", "mortal_location_storage_addition_target_unresolved")]
    [InlineData("missingContents", "mortal_location_storage_addition_contents_invalid")]
    [InlineData("nonempty", "mortal_location_storage_addition_contents_invalid")]
    [InlineData("nullContents", "mortal_location_storage_addition_contents_invalid")]
    [InlineData("measurement", "mortal_location_storage_addition_invalid")]
    [InlineData("missingAccess", "mortal_location_storage_addition_invalid")]
    [InlineData("unknownPlayer", "mortal_location_storage_semantic_invalid")]
    [InlineData("unknownFaction", "mortal_location_storage_owner_target_unknown")]
    [InlineData("duplicate", "mortal_location_storage_addition_identity_reused")]
    [InlineData("confusable", "mortal_location_storage_addition_identity_reused")]
    [InlineData("addUpdate", "mortal_location_storage_transition_conflict")]
    [InlineData("addRemove", "mortal_location_storage_transition_conflict")]
    public void StorageAddition_InvalidRequestFailsIndexedWithoutMutation(string fault, string code)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var map = MortalLocationTestFixture.CreateWorldMap(location); var index = MortalLocationTestFixture.CreateIdentityIndex(location);
        var updates = EmptyStorageAddition(MortalLocationTestFixture.LocationId);
        var op = updates["storagesToAdd"]![0]!.AsObject(); var storage = op["storage"]!.AsObject();
        if (fault == "unknown") op["targetLocationId"] = "missing";
        if (fault == "case") op["targetLocationId"] = MortalLocationTestFixture.LocationId.ToUpperInvariant();
        if (fault == "space") op["targetLocationId"] = " " + MortalLocationTestFixture.LocationId;
        if (fault == "temporary") op["targetLocationId"] = MortalLocationTestFixture.LocationInitialId;
        if (fault == "missingContents") storage.Remove("contents");
        if (fault == "nullContents") storage["contents"] = null;
        if (fault == "nonempty") storage["contents"] = new JsonArray(new JsonObject { ["itemId"] = "not_transferred" });
        if (fault == "missingAccess") storage.Remove("hasFullAccess");
        if (fault == "unknownPlayer") storage["owner"] = new JsonObject { ["ownerType"] = "Player", ["ownerId"] = "player_unknown", ["ownerName"] = "Unknown" };
        if (fault == "unknownFaction") storage["owner"] = new JsonObject { ["ownerType"] = "Faction", ["ownerId"] = "faction_unknown", ["ownerName"] = "Unknown" };
        if (fault == "measurement") storage["measuredConfiguration"] = new JsonObject();
        if (fault is "duplicate" or "confusable")
        {
            var second = op.DeepClone();
            if (fault == "confusable") second["storage"]!["storageId"] = "STORAGE_ADDED";
            updates["storagesToAdd"]!.AsArray().Add(second);
        }
        if (fault == "addUpdate") updates["storageUpdates"] = new JsonArray(new JsonObject
        { ["targetLocationId"] = MortalLocationTestFixture.LocationId, ["storageId"] = "storage_added", ["update"] = new JsonObject { ["newName"] = "Changed" } });
        if (fault == "addRemove") updates["storagesToRemove"] = new JsonArray(new JsonObject
        { ["targetLocationId"] = MortalLocationTestFixture.LocationId, ["storageId"] = "storage_added" });
        var before = updates.ToJsonString(); var beforeMap = map.ToJsonString(); var beforeIndex = index.ToJsonString();
        var result = Build(map, MortalLocationTestFixture.CreateCurrentProjection(location), index, rawWorldMapUpdates: updates, turn: 43);
        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == code && issue.FilePath.StartsWith("worldMapUpdates.storagesToAdd[", StringComparison.Ordinal));
        Assert.Null(result.Plan); Assert.Equal(before, updates.ToJsonString()); Assert.Equal(beforeMap, map.ToJsonString()); Assert.Equal(beforeIndex, index.ToJsonString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StorageAddition_RemovedHistoricalIdentityCannotBeReused(bool confusable)
    {
        var baseline = CreateGovernedCommandBaseline();
        var removed = Build(baseline.WorldMap, baseline.CurrentLocation, baseline.IdentityIndex,
            rawWorldMapUpdates: new JsonObject { ["storagesToRemove"] = new JsonArray(new JsonObject
            { ["targetLocationId"] = baseline.LocationId, ["storageId"] = "storage_remove" }) }, turn: 43);
        Assert.True(removed.Success, string.Join("\n", removed.Issues.Select(i => i.Code + ": " + i.Message)));
        var plan = removed.Plan!; var before = plan.FinalIdentityIndex.ToJsonString();
        var result = Build(plan.FinalWorldMap, plan.FinalCurrentLocation, plan.FinalIdentityIndex,
            rawWorldMapUpdates: EmptyStorageAddition(baseline.LocationId, confusable ? "STORAGE_REMOVE" : "storage_remove"), turn: 44);
        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.Code == "mortal_location_storage_addition_identity_reused");
        Assert.Equal(before, plan.FinalIdentityIndex.ToJsonString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StorageAddition_TravelProjectsOnlyArrivalAndPreservesOtherMetadata(bool atArrival)
    {
        var baseline = CreateAcceptedDirectedTopology();
        var target = atArrival ? baseline.TargetLocationId : baseline.SourceLocationId;
        var result = Build(baseline.FinalWorldMap, baseline.FinalCurrentLocation, baseline.FinalIdentityIndex,
            rawCurrentLocationData: new JsonObject { ["locationId"] = baseline.TargetLocationId,
                ["lastEventsDescription"] = "Переход", ["currentWeather"] = new JsonObject(), ["currentInteractions"] = new JsonArray() },
            rawWorldMapUpdates: EmptyStorageAddition(target), turn: 43);
        Assert.True(result.Success, string.Join("\n", result.Issues.Select(i => i.Code + ": " + i.Message)));
        var plan = result.Plan!;
        Assert.Equal(baseline.TargetLocationId, plan.FinalCurrentLocation!["locationId"]!.GetValue<string>());
        Assert.Equal(atArrival, plan.FinalCurrentLocation["locationStorages"]!.AsArray().OfType<JsonObject>().Any(s => s["storageId"]!.GetValue<string>() == "storage_added"));
        Assert.DoesNotContain(MortalLocationStorageContentsState.StatePath, plan.TouchedPaths);
        Assert.Empty(MortalLocationIdentityState.Parse(plan.FinalIdentityIndex).ValidateCanonicalState(plan.FinalWorldMap));
    }

    [Fact]
    public void StorageAddition_RepeatedPlanReusesOneTransitionAndChangedInputInvalidates()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var input = new MortalLocationAcceptedTurnInput(MortalLocationTestFixture.CreateWorldMap(location),
            MortalLocationTestFixture.CreateCurrentProjection(location), MortalLocationTestFixture.CreateIdentityIndex(location),
            null, EmptyStorageAddition(MortalLocationTestFixture.LocationId), 43);
        var cache = new MortalLocationAcceptedTurnPlanCache(); var factory = CreateIdentityFactory();
        var first = cache.GetOrBuild(input, factory); Assert.True(first.Success);
        var second = cache.GetOrBuild(input, factory); Assert.Same(first, second);
        Assert.Single(first.Plan!.FinalIdentityIndex["locationEntries"]![0]!["transitions"]!.AsArray());
        input.RawWorldMapUpdates!["storagesToAdd"]![0]!["storage"]!["name"] = "Другой сундук";
        var third = cache.GetOrBuild(input, factory); Assert.True(third.Success); Assert.NotSame(first, third);
        Assert.Equal("Другой сундук", third.Plan!.FinalWorldMap["locations"]![0]!["locationStorages"]![0]!["name"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StorageAddition_ExistingIdentityRejectedButOtherLocationCoordinateAllowed(bool otherLocation)
    {
        var baseline = CreateGovernedCommandBaseline();
        var map = baseline.WorldMap.DeepClone().AsObject(); var index = baseline.IdentityIndex.DeepClone().AsObject();
        var target = baseline.LocationId;
        if (otherLocation)
        {
            var second = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity("loc_other", "Другое место", x: 100);
            map["locations"]!.AsArray().Add(second);
            index["locationEntries"]!.AsArray().Add(MortalLocationTestFixture.CreateIdentityIndex(second)["locationEntries"]![0]!.DeepClone());
            target = "loc_other";
        }
        var result = Build(map, baseline.CurrentLocation, index, rawWorldMapUpdates: EmptyStorageAddition(target, "storage_update"), turn: 43);
        if (otherLocation)
        {
            Assert.True(result.Success, string.Join("\n", result.Issues.Select(i => i.Code + ": " + i.Message)));
            Assert.True(JsonNode.DeepEquals(baseline.CurrentLocation["locationStorages"], result.Plan!.FinalCurrentLocation!["locationStorages"]));
        }
        else
        {
            Assert.False(result.Success);
            Assert.Contains(result.Issues, i => i.Code == "mortal_location_storage_addition_identity_reused");
        }
    }

    private static JsonObject EmptyStorageAddition(string locationId, string storageId = "storage_added") => new()
    { ["storagesToAdd"] = new JsonArray(new JsonObject { ["targetLocationId"] = locationId,
        ["storage"] = MortalLocationTestFixture.CreateStorageMetadata(storageId, "Новый сундук", false, new JsonArray()) }) };

}
