using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;
namespace BookOfEternityClient.Tests;

public sealed class MortalStorageAccessProjectionTests
{
    [Fact]
    public void ExactCoordinatesCopyDenialWithoutContentsOrGrantInference()
    {
        var map = Map(); var pending = Pending(); var before = map.ToJsonString();
        var result = MortalStorageAccessProjection.Build(map, pending);
        Assert.True(result.IsValid);
        var entry = Assert.Single(result.Root!["entries"]!.AsArray())!;
        Assert.Equal("location_one", entry["locationId"]!.GetValue<string>());
        Assert.Equal("storage_one", entry["storageId"]!.GetValue<string>());
        Assert.False(entry["hasFullAccess"]!.GetValue<bool>()); Assert.Null(entry["contents"]);
        Assert.True(JsonNode.DeepEquals(pending["grantStorageAccess"], result.Root["grantStorageAccess"]));
        entry["owner"]!["ownerName"] = "Detached";
        Assert.Equal(before, map.ToJsonString());
    }

    [Fact]
    public void MetadataChangeAndRemovalRebuildOnlyEffectiveEntries()
    {
        var map = Map(); var original = MortalStorageAccessProjection.Build(map, Pending()).Root!;
        map["locations"]![0]!["locationStorages"]![0]!["hasFullAccess"] = true;
        var changed = MortalStorageAccessProjection.Build(map, original).Root!;
        Assert.True(changed["entries"]![0]!["hasFullAccess"]!.GetValue<bool>());
        map["locations"]![0]!["locationStorages"] = new JsonArray();
        var removed = MortalStorageAccessProjection.Build(map, changed).Root!;
        Assert.Empty(removed["entries"]!.AsArray()); Assert.NotNull(removed["grantStorageAccess"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawProjectionCannotBeForgedEvenIfFinalMetadataAgrees(bool originallyAbsent)
    {
        var original = originallyAbsent ? null : MortalStorageAccessProjection.Build(Map(), Pending()).Root;
        var raw = MortalStorageAccessProjection.Build(Map(), Pending()).Root!;
        raw["entries"]![0]!["hasFullAccess"] = true;
        Assert.NotEmpty(MortalStorageAccessProjection.ValidateContinuity(original, raw));
    }

    [Fact]
    public void RawPendingCommandChangeDoesNotChangeProtectedEntries()
    {
        var original = MortalStorageAccessProjection.Build(Map(), null).Root!;
        var raw = original.DeepClone().AsObject(); raw["grantStorageAccess"] = Pending()["grantStorageAccess"]!.DeepClone();
        Assert.Empty(MortalStorageAccessProjection.ValidateContinuity(original, raw));
        Assert.Empty(MortalStorageAccessProjection.ValidateCanonical(Map(), raw));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateOrConfusableCoordinatesFailClosed(bool confusable)
    {
        var map = Map(); var storage = map["locations"]![0]!["locationStorages"]![0]!.DeepClone();
        if (confusable) storage["storageId"] = "STORAGE_ONE";
        map["locations"]![0]!["locationStorages"]!.AsArray().Add(storage);
        Assert.False(MortalStorageAccessProjection.Build(map, null).IsValid);
    }

    [Fact]
    public void SameStorageIdInDifferentExactLocationsRemainsDistinct()
    {
        var map = Map(); var location = map["locations"]![0]!.DeepClone(); location["locationId"] = "location_two";
        map["locations"]!.AsArray().Add(location);
        var result = MortalStorageAccessProjection.Build(map, null);
        Assert.True(result.IsValid); Assert.Equal(2, result.Root!["entries"]!.AsArray().Count);
    }

    [Fact]
    public void MissingOrStaleCanonicalProjectionIsNotPromoted()
    {
        Assert.NotEmpty(MortalStorageAccessProjection.ValidateCanonical(Map(), null));
        Assert.NotEmpty(MortalStorageAccessProjection.ValidateCanonical(Map(), MortalStorageAccessProjection.CreateEmptyRoot()));
    }

    [Theory]
    [InlineData("scalar")]
    [InlineData("realm")]
    [InlineData("missingStorages")]
    [InlineData("scalarStorages")]
    public void MalformedPhysicalMapFailsClosed(string fault)
    {
        var map = Map();
        if (fault == "scalar") map["locations"]!.AsArray().Add(17);
        else if (fault == "realm") map["locations"]![0]!["realm"] = "afterlife";
        else if (fault == "missingStorages") map["locations"]![0]!.AsObject().Remove("locationStorages");
        else map["locations"]![0]!["locationStorages"] = "invalid";
        var before = map.ToJsonString();
        Assert.False(MortalStorageAccessProjection.Build(map, null).IsValid);
        Assert.Equal(before, map.ToJsonString());
    }

    private static JsonObject Pending() => new() { ["grantStorageAccess"] = new JsonArray(new JsonObject
        { ["storageId"] = "storage_one", ["targetPlayerId"] = "player_current", ["targetPlayerName"] = "Player" }) };
    private static JsonObject Map() => new() { ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["locations"] = new JsonArray(new JsonObject
    { ["realm"] = "mortal_world", ["locationId"] = "location_one", ["locationStorages"] = new JsonArray(new JsonObject
      { ["storageId"] = "storage_one", ["name"] = "Chest", ["description"] = "Closed", ["image_prompt"] = "Chest", ["capacity"] = 1, ["volume"] = 2,
        ["owner"] = new JsonObject { ["ownerType"] = "Player", ["ownerId"] = "player_current", ["ownerName"] = "Player" },
        ["authorizedUsers"] = new JsonArray(), ["hasFullAccess"] = false, ["contents"] = new JsonArray() }) }) };
}
