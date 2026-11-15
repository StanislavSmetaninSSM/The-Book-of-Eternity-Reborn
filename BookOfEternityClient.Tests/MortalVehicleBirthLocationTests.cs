using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalVehicleBirthLocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactCanonicalOrSameTurnTargetResolvesWithoutMutation(bool sameTurn)
    {
        var raw = Vehicles(sameTurn ? "new_place" : "location_port");
        var map = Map("location_port");
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal) { ["new_place"] = "location_port" };
        var before = raw.ToJsonString(); var mapBefore = map.ToJsonString();
        var result = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthLocations(raw, map, mappings);
        Assert.True(result.IsValid); Assert.Empty(result.Issues);
        Assert.Equal("location_port", Assert.Single(result.LocationIdsByVehicleRef).Value);
        Assert.Equal(before, raw.ToJsonString()); Assert.Equal(mapBefore, map.ToJsonString());
        Assert.Equal("location_port", mappings["new_place"]);
    }

    [Theory]
    [InlineData("missing", "location_port", false, "unknown")]
    [InlineData("LOCATION_PORT", "location_port", false, "confusable")]
    [InlineData("location_port", "location_port", true, "ambiguous")]
    [InlineData("new_place", "location_port", false, "unknown")]
    [InlineData("cafe\u0301", "caf\u00e9", false, "confusable")]
    public void InvalidTargetHasIndexedIssueAndNoResolvedOutput(string target, string canonical, bool duplicate, string suffix)
    {
        var raw = Vehicles(target); var before = raw.ToJsonString();
        var map = duplicate ? Map(canonical, canonical) : Map(canonical);
        var mappings = new Dictionary<string, string>(StringComparer.Ordinal) { ["new_place"] = "missing_mapping_target" };
        var result = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthLocations(raw, map, mappings);
        Assert.False(result.IsValid); Assert.Empty(result.LocationIdsByVehicleRef);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("resource_owner_vehicle_birth_location_" + suffix, issue.Code);
        Assert.Equal(StorageTransportMoveService.VehiclesPath + ".UpdateVehicles[0].currentLocationId", issue.FilePath);
        Assert.Equal(before, raw.ToJsonString());
    }

    [Fact]
    public void MappingAliasNeverResolvesByCaseFolding()
    {
        var result = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthLocations(Vehicles("NEW_PLACE"), Map("location_port"),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["new_place"] = "location_port" });
        Assert.Equal("resource_owner_vehicle_birth_location_confusable", Assert.Single(result.Issues).Code);
        Assert.Empty(result.LocationIdsByVehicleRef);
    }

    [Fact]
    public void ExactUnicodeIdentityIsPreserved()
    {
        var result = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthLocations(Vehicles("港_север"), Map("港_север"), EmptyMapping());
        Assert.True(result.IsValid); Assert.Equal("港_север", result.LocationIdsByVehicleRef["new_cart_0"]);
    }

    [Fact]
    public void RepeatedUnknownTargetsRetainOriginalIndicesAndExistingUpdatesAreIgnored()
    {
        var raw = Vehicles("missing", "missing");
        raw["UpdateVehicles"]!.AsArray().Insert(0, new JsonObject { ["vehicleId"] = "existing", ["currentLocationId"] = "unrelated" });
        var result = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthLocations(raw, Map("location_port"), EmptyMapping());
        Assert.Equal(new[] { StorageTransportMoveService.VehiclesPath + ".UpdateVehicles[1].currentLocationId",
            StorageTransportMoveService.VehiclesPath + ".UpdateVehicles[2].currentLocationId" }, result.Issues.Select(i => i.FilePath));
    }

    [Fact]
    public void NullLocationAndUnrelatedExistingVehicleNeedNoBinding()
    {
        var raw = Vehicles("location_port"); raw["UpdateVehicles"]![0]!["currentLocationId"] = null;
        raw["UpdateVehicles"]!.AsArray().Add(new JsonObject { ["vehicleId"] = "existing", ["currentLocationId"] = "unknown" });
        var result = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthLocations(raw, Map(), EmptyMapping());
        Assert.True(result.IsValid); Assert.Empty(result.LocationIdsByVehicleRef);
    }

    [Fact]
    public void SameTurnMappingStillRequiresUniqueCanonicalTarget()
    {
        var result = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthLocations(Vehicles("new_place"), Map("location_port", "location_port"),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["new_place"] = "location_port" });
        Assert.Equal("resource_owner_vehicle_birth_location_ambiguous", Assert.Single(result.Issues).Code);
        Assert.Empty(result.LocationIdsByVehicleRef);
    }

    [Fact]
    public void RepeatedValidTargetProducesIndependentVehicleBindings()
    {
        var result = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthLocations(Vehicles("location_port", "location_port"), Map("location_port"), EmptyMapping());
        Assert.True(result.IsValid); Assert.Equal(2, result.LocationIdsByVehicleRef.Count);
        Assert.All(result.LocationIdsByVehicleRef.Values, id => Assert.Equal("location_port", id));
    }

    private static Dictionary<string, string> EmptyMapping() => new(StringComparer.Ordinal);
    [Fact]
    public void ValidBindingDoesNotEscapeWhenAnotherBirthIsInvalid()
    {
        var raw = Vehicles("location_port");
        var bad = raw["UpdateVehicles"]![0]!.DeepClone();
        bad["vehicleRef"] = "new_bad"; bad["currentLocationId"] = "missing";
        raw["UpdateVehicles"]!.AsArray().Add(bad);
        var result = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthLocations(raw,
            Map("location_port"), new Dictionary<string, string>());
        Assert.False(result.IsValid);
        Assert.Empty(result.LocationIdsByVehicleRef);
        Assert.Single(result.Issues);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectedLocationRequiresMortalMapAndLocationRealm(bool mapRealm)
    {
        var map = Map("location_port");
        if (mapRealm) map["realm"] = "afterlife";
        else map["locations"]![0]!["realm"] = "afterlife";
        var result = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthLocations(
            Vehicles("location_port"), map, EmptyMapping());
        Assert.Equal("resource_owner_vehicle_birth_location_realm_invalid", Assert.Single(result.Issues).Code);
        Assert.Empty(result.LocationIdsByVehicleRef);
    }

    private static JsonObject Map(params string[] ids) => new() { ["realm"] = "mortal_world", ["locations"] = new JsonArray(ids.Select(id => (JsonNode?)new JsonObject { ["locationId"] = id, ["realm"] = "mortal_world" }).ToArray()) };
    private static JsonObject Vehicles(params string[] locations) => new() { ["UpdateVehicles"] = new JsonArray(locations.Select((id, i) => (JsonNode?)new JsonObject
        { ["vehicleRef"] = "new_cart_" + i, ["currentLocationId"] = id }).ToArray()) };
}
