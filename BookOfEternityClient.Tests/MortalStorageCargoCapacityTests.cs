using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Pure storage-coordinate adapter over the shared cargo arithmetic, not publication authority.</summary>
public sealed class MortalStorageCargoCapacityTests
{
    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 0.999, false)]
    [InlineData(1, 1, true)]
    [InlineData(2, 2, true)]
    public void ExactLimitsCoverOnlyTheSelectedStorage(int slots, double volume, bool accepted)
    {
        var birth = Storage("selected", slots, volume, Item("one", 1));
        var other = Storage("other", 0, 0, Item("unrelated", 999));
        var before = birth.ToJsonString();
        var result = MortalCargoBirthCapacity.ValidateStorage("loc_a", "selected", birth, Catalog("loc_a", birth, other),
            new Dictionary<string, string>(), 42, "currentLocationData.locationStorages[0]");
        Assert.Equal(accepted, result.IsValid);
        Assert.Equal(before, birth.ToJsonString());
        if (accepted)
        {
            Assert.Equal("location_storage", result.Evidence!["carrierKind"]!.GetValue<string>());
            Assert.Equal("loc_a", result.Evidence["locationId"]!.GetValue<string>());
            Assert.Equal("selected", result.Evidence["storageId"]!.GetValue<string>());
            Assert.Single(result.Evidence["cargoMeasurements"]!.AsArray());
            Assert.Null(result.Evidence["vehicleId"]);
        }
    }

    [Fact]
    public void AnotherLocationWithSameStorageIdDoesNotEnterTheLoad()
    {
        var birth = Storage("shared", 1, 1, Item("one", 1));
        var root = new JsonObject { ["locationId"] = "loc_a", ["locationStorages"] = new JsonArray(birth.DeepClone()) };
        var remote = MortalLocationStorageContentsState.BuildCanonicalRoot(new Dictionary<MortalLocationStorageKey, JsonArray>
        {
            [new("loc_b", "shared")] = new JsonArray(Item("remote", 99))
        });
        var catalog = MortalItemCarrierCatalog.Build(new(null, null, null, root, null,
            new Dictionary<string, JsonObject>(), remote));
        var result = MortalCargoBirthCapacity.ValidateStorage("loc_a", "shared", birth, catalog,
            new Dictionary<string, string>(), 42, "storage");
        Assert.True(result.IsValid);
        Assert.Equal("one", Assert.Single(result.Evidence!["cargoMeasurements"]!.AsArray())!["itemId"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(0.3, true)]
    [InlineData(0.29999999999999993, false)]
    public void FractionalBoundaryUsesExistingExactArithmetic(double limit, bool accepted)
    {
        var birth = Storage("box", 2, limit, Item("one", 0.1), Item("two", 0.2));
        Assert.Equal(accepted, MortalCargoBirthCapacity.ValidateStorage("loc_a", "box", birth, Catalog("loc_a", birth),
            new Dictionary<string, string>(), 42, "storage").IsValid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WholeStackNeedsAnExactExplicitMeasurement(bool measured)
    {
        var item = Item("stack", 100); item["count"] = 50;
        var birth = Storage("box", 1, 1, item);
        if (measured) birth["cargoMeasurements"] = new JsonArray(Measurement(item, 1));
        Assert.Equal(measured, MortalCargoBirthCapacity.ValidateStorage("loc_a", "box", birth, Catalog("loc_a", birth),
            new Dictionary<string, string>(), 42, "storage").IsValid);
    }

    [Fact]
    public void ChangedCountOrShapeCannotRefreshAnOldMeasurement()
    {
        var item = Item("stack", 100); item["count"] = 2;
        var birth = Storage("box", 1, 1, item);
        birth["cargoMeasurements"] = new JsonArray(Measurement(item, 1));
        birth["contents"]![0]!["count"] = 3;
        Assert.False(MortalCargoBirthCapacity.ValidateStorage("loc_a", "box", birth, Catalog("loc_a", birth),
            new Dictionary<string, string>(), 42, "storage").IsValid);
        birth["contents"]![0]!["count"] = 2;
        birth["contents"]![0]!["volume"] = 200;
        Assert.False(MortalCargoBirthCapacity.ValidateStorage("loc_a", "box", birth, Catalog("loc_a", birth),
            new Dictionary<string, string>(), 42, "storage").IsValid);
    }

    private static JsonObject Item(string id, double volume) => new()
    {
        ["itemId"] = id, ["count"] = 1, ["volume"] = volume, ["isContainer"] = false, ["contentsPath"] = null,
        ["materialization"] = new JsonObject { ["materializationId"] = "mat_" + id }
    };

    private static JsonObject Storage(string id, int slots, double volume, params JsonObject[] items) => new()
    {
        ["storageId"] = id, ["capacity"] = slots, ["volume"] = volume,
        ["contents"] = new JsonArray(items.Select(item => (JsonNode)item.DeepClone()).ToArray())
    };

    private static MortalItemCarrierCatalog Catalog(string locationId, params JsonObject[] storages) => MortalItemCarrierCatalog.Build(
        new(null, null, null, new JsonObject { ["locationId"] = locationId,
            ["locationStorages"] = new JsonArray(storages.Select(storage => (JsonNode)storage.DeepClone()).ToArray()) },
            null, new Dictionary<string, JsonObject>()));

    private static JsonObject Measurement(JsonObject item, double volume)
    {
        var shape = item.DeepClone().AsObject();
        foreach (var field in new[] { "itemId", "count", "contentsPath", "materialization" }) shape.Remove(field);
        return new JsonObject { ["itemId"] = item["itemId"]!.DeepClone(), ["count"] = item["count"]!.DeepClone(),
            ["externalVolumeDm3"] = volume, ["measuredConfiguration"] = new JsonObject
            { ["schemaVersion"] = 1, ["item"] = shape, ["contents"] = new JsonArray() } };
    }
}
