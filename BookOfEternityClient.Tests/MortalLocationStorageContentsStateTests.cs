using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLocationStorageContentsStateTests
{
    [Fact]
    public void Parse_NullStateIsCanonicalEmptyLegacyBaseline()
    {
        var parsed = MortalLocationStorageContentsState.Parse(null);

        Assert.Empty(parsed.Issues);
        Assert.Empty(parsed.Entries);
        Assert.Equal(1, parsed.Root["schemaVersion"]!.GetValue<int>());
        Assert.Empty(parsed.Root["entries"]!.AsArray());
    }

    [Fact]
    public void Parse_AcceptsOneExactNonEmptyOffscreenCoordinate()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot("itm_offscreen");
        var parsed = MortalLocationStorageContentsState.Parse(Root(
            Entry("loc_remote", "storage_chest", item)));

        Assert.Empty(parsed.Issues);
        var pair = Assert.Single(parsed.Entries);
        Assert.Equal(new MortalLocationStorageKey("loc_remote", "storage_chest"), pair.Key);
        Assert.Equal("itm_offscreen", pair.Value[0]!["itemId"]!.GetValue<string>());
    }

    [Fact]
    public void Parse_RejectsDuplicateExactCoordinate()
    {
        var parsed = MortalLocationStorageContentsState.Parse(Root(
            Entry("loc_remote", "storage_chest", MortalItemTestFixture.CreateCanonicalRoot("itm_first")),
            Entry("loc_remote", "storage_chest", MortalItemTestFixture.CreateCanonicalRoot("itm_second"))));

        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "mortal_location_storage_contents_coordinate_duplicate" &&
            issue.FilePath.EndsWith("entries[1]", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_RejectsCaseConfusableCoordinate()
    {
        var parsed = MortalLocationStorageContentsState.Parse(Root(
            Entry("loc_remote", "storage_chest", MortalItemTestFixture.CreateCanonicalRoot("itm_first")),
            Entry("LOC_REMOTE", "storage_chest", MortalItemTestFixture.CreateCanonicalRoot("itm_second"))));

        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "mortal_location_storage_contents_coordinate_confusable" &&
            issue.FilePath.EndsWith("entries[1]", StringComparison.Ordinal));
    }

    [Fact]
    public void Parse_RejectsEmptyContentsEntry()
    {
        var parsed = MortalLocationStorageContentsState.Parse(Root(new JsonObject
        {
            ["locationId"] = "loc_remote",
            ["storageId"] = "storage_chest",
            ["contents"] = new JsonArray()
        }));

        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "mortal_location_storage_contents_empty_entry" &&
            issue.FilePath.EndsWith("entries[0].contents", StringComparison.Ordinal));
        Assert.Empty(parsed.Entries);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("extra")]
    public void Parse_RejectsNonCanonicalRoot(string scenario)
    {
        var root = Root(Entry(
            "loc_remote",
            "storage_chest",
            MortalItemTestFixture.CreateCanonicalRoot("itm_root_shape")));
        if (scenario == "schema")
            root["schemaVersion"] = 2;
        else
            root["debug"] = true;

        var parsed = MortalLocationStorageContentsState.Parse(root);

        Assert.Contains(parsed.Issues, issue =>
            issue.Code == "mortal_location_storage_contents_invalid_root");
        Assert.Empty(parsed.Entries);
    }

    [Theory]
    [InlineData("{", "mortal_location_storage_contents_invalid_json")]
    [InlineData("[]", "mortal_location_storage_contents_invalid_root")]
    public void ParseJson_RejectsMalformedOrNonObjectState(
        string json,
        string expectedCode)
    {
        var parsed = MortalLocationStorageContentsState.ParseJson(json);

        Assert.Contains(parsed.Issues, issue => issue.Code == expectedCode);
        Assert.Empty(parsed.Entries);
    }

    [Fact]
    public void BuildCanonicalRoot_SortsCoordinatesAndDeepClonesContents()
    {
        var sourceItem = MortalItemTestFixture.CreateCanonicalRoot("itm_sorted");
        var sourceContents = new JsonArray(sourceItem);
        var entries = new Dictionary<MortalLocationStorageKey, JsonArray>
        {
            [new MortalLocationStorageKey("loc_z", "storage_a")] =
                new JsonArray(MortalItemTestFixture.CreateCanonicalRoot("itm_z")),
            [new MortalLocationStorageKey("loc_a", "storage_z")] = sourceContents,
            [new MortalLocationStorageKey("loc_a", "storage_a")] =
                new JsonArray(MortalItemTestFixture.CreateCanonicalRoot("itm_a"))
        };

        var root = MortalLocationStorageContentsState.BuildCanonicalRoot(entries);
        sourceItem["name"] = "Изменённый источник";

        var canonical = root["entries"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Collection(
            canonical,
            entry => AssertCoordinate(entry, "loc_a", "storage_a"),
            entry =>
            {
                AssertCoordinate(entry, "loc_a", "storage_z");
                Assert.NotEqual(
                    "Изменённый источник",
                    entry["contents"]![0]!["name"]!.GetValue<string>());
            },
            entry => AssertCoordinate(entry, "loc_z", "storage_a"));
    }

    private static JsonObject Root(params JsonObject[] entries) => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray(entries
            .Select(static entry => (JsonNode?)entry)
            .ToArray())
    };

    private static JsonObject Entry(
        string locationId,
        string storageId,
        JsonObject item) => new()
    {
        ["locationId"] = locationId,
        ["storageId"] = storageId,
        ["contents"] = new JsonArray(item)
    };

    private static void AssertCoordinate(
        JsonObject entry,
        string locationId,
        string storageId)
    {
        Assert.Equal(locationId, entry["locationId"]!.GetValue<string>());
        Assert.Equal(storageId, entry["storageId"]!.GetValue<string>());
    }
}
