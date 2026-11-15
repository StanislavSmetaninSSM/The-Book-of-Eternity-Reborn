using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalCargoBirthCapacityTests
{
    [Theory]
    [InlineData(0.1, 0.2, 0.3, true)]
    [InlineData(0.1, 0.2, 0.29999999999999993, false)]
    [InlineData(double.Epsilon, double.Epsilon, double.Epsilon, false)]
    [InlineData(double.Epsilon, 0, double.Epsilon, true)]
    [InlineData(double.MaxValue, 0, double.MaxValue, true)]
    [InlineData(double.MaxValue, double.MaxValue, double.MaxValue, false)]
    public void ExactRoundtripSum(double a, double b, double limit, bool valid)
    {
        var birth = Birth(limit);
        Assert.Equal(valid, Validate(birth, Item("a", a), Item("b", b)).IsValid);
    }

    [Theory]
    [InlineData("count")]
    [InlineData("shape")]
    [InlineData("custom")]
    [InlineData("containment")]
    public void ChangedConfigurationRejectsPersistedBasis(string change)
    {
        var item = Item("a", 1); item["count"] = 2;
        var birth = Birth(10); birth["cargoMeasurements"] = new JsonArray(Row(item));
        // Reparse simulates a fresh process: no object-identity or warm-cache dependency.
        birth = JsonNode.Parse(birth.ToJsonString())!.AsObject();
        switch (change)
        {
            case "count": item["count"] = 3; break;
            case "shape": item["volume"] = 2; break;
            case "custom": item["custom"] = new JsonObject { ["count"] = 7 }; break;
            case "containment":
                var child = Item("child", 1); child["contentsPath"] = new JsonArray("a");
                Assert.False(Validate(birth, item, child).IsValid); return;
        }
        Assert.False(Validate(birth, item).IsValid);
        birth["cargoMeasurements"] = new JsonArray(Row(item));
        Assert.True(Validate(birth, item).IsValid);
    }

    [Fact]
    public void AllocationReferencesNormalizeWithoutChangingCaller()
    {
        var item = Item("a", 1); item["count"] = 2; item["sourceItemId"] = "b";
        var row = Row(item); row["itemId"] = null; row.Remove("itemId"); row["creationRef"] = "new_a";
        row["measuredConfiguration"]!["item"]!["sourceItemId"] = "new_b";
        var birth = Birth(10); birth["cargoMeasurements"] = new JsonArray(row);
        birth["inventory"] = new JsonArray(item.DeepClone());
        var before = birth.ToJsonString();
        var result = MortalCargoBirthCapacity.Validate("cart", birth, Catalog(item),
            new Dictionary<string,string> { ["new_a"] = "a", ["new_b"] = "b" }, 42, "vehicles.UpdateVehicles[0]");
        Assert.True(result.IsValid);
        Assert.Equal(before, birth.ToJsonString());
        result.Evidence!["cargoMeasurements"]!.AsArray().Clear();
        Assert.Equal(before, birth.ToJsonString());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("extra")]
    [InlineData("unknown")]
    [InlineData("redundant")]
    public void InvalidMeasurementRowsFail(string mode)
    {
        var item = Item("a", 1); item["count"] = mode == "redundant" ? 1 : 2;
        var row = Row(item); var rows = new JsonArray();
        if (mode != "missing") rows.Add(row);
        if (mode == "duplicate") rows.Add(row.DeepClone());
        if (mode == "extra") row["unlisted"] = true;
        if (mode == "unknown") row["itemId"] = "absent";
        var birth = Birth(10); birth["cargoMeasurements"] = rows;
        Assert.False(Validate(birth, item).IsValid);
    }

    [Fact]
    public void EmptyCargoAndDirectStackLimit()
    {
        var birth = Birth(0); birth["capacity"] = 0;
        Assert.True(Validate(birth).IsValid);
        Assert.False(Validate(birth, Item("a", 0)).IsValid);
    }

    [Fact]
    public void ContainerCountsOuterFootprintOnceAndBindsChildren()
    {
        var container = Item("a", 900); container["isContainer"] = true;
        var child = Item("b", 800); child["contentsPath"] = new JsonArray("a");
        var second = Item("c", 700); second["contentsPath"] = new JsonArray("a");
        var row = Row(container);
        var contents = row["measuredConfiguration"]!["contents"]!.AsArray();
        foreach (var item in new[] { second, child })
        {
            var childRow = Row(item);
            contents.Add(new JsonObject { ["itemId"] = item["itemId"]!.DeepClone(), ["count"] = 1,
                ["item"] = childRow["measuredConfiguration"]!["item"]!.DeepClone(), ["contents"] = new JsonArray() });
        }
        var birth = Birth(1); birth["capacity"] = 1; birth["cargoMeasurements"] = new JsonArray(row);
        var valid = Validate(birth, container, child, second);
        Assert.True(valid.IsValid);
        var fingerprint = valid.Evidence!["cargoMeasurements"]![0]!["configurationFingerprint"]!.GetValue<string>();
        var moved = contents[0]!; contents.RemoveAt(0); contents.Add(moved);
        Assert.Equal(fingerprint, Validate(birth, container, child, second).Evidence!["cargoMeasurements"]![0]!["configurationFingerprint"]!.GetValue<string>());
        child["custom"] = new JsonObject { ["count"] = 9 };
        Assert.False(Validate(birth, container, child, second).IsValid);
        child.Remove("custom");
        child["contentsPath"] = new JsonArray("a", "c");
        Assert.False(Validate(birth, container, child, second).IsValid);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("1e400")]
    [InlineData("\"1\"")]
    [InlineData("null")]
    public void InvalidNumericVolumesAreRejected(string json)
    {
        var item = Item("a", 1); item["volume"] = JsonNode.Parse(json);
        Assert.False(Validate(Birth(10), item).IsValid);
    }

    [Fact]
    public void NegativeZeroIsNormalized()
    {
        var result = Validate(Birth(0), Item("a", -0d));
        Assert.True(result.IsValid);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(result.Evidence!["cargoMeasurements"]![0]!["externalVolumeDm3"]!.GetValue<double>()));
    }

    [Fact]
    public void NonemptyRawCargoCannotUseEmptyProjectedCatalog()
    {
        var birth = Birth(10); birth["inventory"] = new JsonArray(Item("a", 1));
        var result = MortalCargoBirthCapacity.Validate("cart", birth, Catalog(), new Dictionary<string,string>(), 42, "vehicles.UpdateVehicles[0]");
        Assert.False(result.IsValid);
        Assert.Null(result.Evidence);
    }

    [Fact]
    public void SingleUnitEvidenceBindsShapeAndRemainsDetached()
    {
        var item = Item("a", 1);
        var result = Validate(Birth(1), item);
        Assert.True(result.IsValid);
        var row = result.Evidence!["cargoMeasurements"]![0]!;
        var hash = row["configurationFingerprint"]!.GetValue<string>();
        item["name"] = "changed";
        var fresh = Validate(Birth(1), item);
        Assert.NotEqual(hash, fresh.Evidence!["cargoMeasurements"]![0]!["configurationFingerprint"]!.GetValue<string>());
        Assert.Null(row["measuredConfiguration"]!["item"]!["name"]);
    }

    [Fact]
    public void MeasuredMultiUnitStackUsesOneSlot()
    {
        var item = Item("a", 100); item["count"] = 99;
        var birth = Birth(1); birth["capacity"] = 1; birth["cargoMeasurements"] = new JsonArray(Row(item));
        Assert.True(Validate(birth, item).IsValid);
    }

    private static JsonObject Birth(double volume) => new() { ["capacity"] = 2, ["volume"] = volume };
    private static JsonObject Item(string id, double volume) => new()
    { ["itemId"] = id, ["count"] = 1, ["volume"] = volume, ["isContainer"] = false, ["contentsPath"] = null };
    private static JsonObject Row(JsonObject item)
    {
        var shape = item.DeepClone().AsObject();
        foreach (var key in new[] { "itemId", "count", "contentsPath" }) shape.Remove(key);
        return new JsonObject { ["itemId"] = item["itemId"]!.DeepClone(), ["count"] = item["count"]!.DeepClone(),
            ["externalVolumeDm3"] = 1, ["measuredConfiguration"] = new JsonObject
            { ["schemaVersion"] = 1, ["item"] = shape, ["contents"] = new JsonArray() } };
    }
    private static MortalItemCarrierCatalog Catalog(params JsonObject[] items) => MortalItemCarrierCatalog.Build(
        new(null, null, null, null, new JsonObject { ["vehicles"] = new JsonArray(new JsonObject
        { ["vehicleId"] = "cart", ["inventory"] = new JsonArray(items.Select(i => (JsonNode)i.DeepClone()).ToArray()) }) },
        new Dictionary<string,JsonObject>()));
    private static MortalCargoBirthCapacityResult Validate(JsonObject birth, params JsonObject[] items)
    {
        var raw = birth.DeepClone().AsObject();
        raw["inventory"] = new JsonArray(items.Select(i => (JsonNode)i.DeepClone()).ToArray());
        return MortalCargoBirthCapacity.Validate("cart", raw, Catalog(items), new Dictionary<string,string>(), 42, "vehicles.UpdateVehicles[0]");
    }
}
