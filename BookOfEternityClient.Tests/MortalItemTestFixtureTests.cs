using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalItemTestFixtureTests
{
    private static readonly string[] ExpectedSections =
    {
        "presentation",
        "physical",
        "mechanics",
        "equipment",
        "container",
        "consumption",
        "readableOrSentient",
        "craftingAndDisassembly",
        "bondsAndFateCards",
        "questRole",
        "provenance",
        "ownershipAndPlacement"
    };

    [Fact]
    public void CreateRawRoot_UsesCurrentPreSealIdentityAndCompleteSectionShape()
    {
        var item = MortalItemTestFixture.CreateRawRoot();

        Assert.True(item.ContainsKey("existedId"));
        Assert.Null(item["existedId"]);
        Assert.Equal(MortalItemTestFixture.CreationRef, item["creationRef"]!.GetValue<string>());
        Assert.False(item.ContainsKey("itemId"));
        Assert.False(item.ContainsKey("id"));
        Assert.False(item.ContainsKey("initialId"));
        Assert.False(item.ContainsKey("materializationReceipt"));

        var materialization = item["materialization"]!.AsObject();
        Assert.Equal(MortalItemTestFixture.CreationRef, materialization["creationRef"]!.GetValue<string>());
        Assert.Equal("Mortal", materialization["realm"]!.GetValue<string>());
        Assert.Equal("complete", materialization["state"]!.GetValue<string>());

        var sections = materialization["sections"]!.AsObject();
        Assert.Equal(ExpectedSections, sections.Select(pair => pair.Key));
        Assert.All(ExpectedSections, section =>
        {
            var disposition = sections[section]!.AsObject();
            Assert.True(disposition.ContainsKey("state"));
            Assert.True(disposition.ContainsKey("reason"));
        });
    }

    [Fact]
    public void CreateCanonicalRoot_HasEqualIdsReceiptAndMatchingIndex()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        var index = MortalItemTestFixture.CreateIndex(item);

        Assert.Equal(MortalItemTestFixture.ItemId, item["itemId"]!.GetValue<string>());
        Assert.Equal(MortalItemTestFixture.ItemId, item["existedId"]!.GetValue<string>());
        Assert.False(item.ContainsKey("creationRef"));

        var receipt = item["materializationReceipt"]!.AsObject();
        Assert.Equal(MortalItemTestFixture.ReceiptId, receipt["receiptId"]!.GetValue<string>());
        Assert.Equal(MortalItemTestFixture.ItemId, receipt["itemId"]!.GetValue<string>());
        Assert.Equal(MortalItemTestFixture.MaterializationId, receipt["materializationId"]!.GetValue<string>());
        Assert.StartsWith("sha256:", receipt["seal"]!.GetValue<string>(), StringComparison.Ordinal);

        var entry = Assert.Single(index["entries"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(MortalItemTestFixture.ItemId, entry["itemId"]!.GetValue<string>());
        Assert.Equal(MortalItemTestFixture.ReceiptId, entry["receiptId"]!.GetValue<string>());
        Assert.Equal("active", entry["state"]!.GetValue<string>());
        Assert.Equal("player_inventory", entry["currentCarrier"]!["kind"]!.GetValue<string>());
        Assert.Single(entry["transitions"]!.AsArray());
    }

    [Theory]
    [InlineData("player_inventory", "player", null, "items")]
    [InlineData("npc_inventory", "npc_test", null, "NPCsInScene")]
    [InlineData("location_storage", "loc_test", "storage_test", "currentLocationData")]
    [InlineData("vehicle_inventory", "vehicle_test", null, "vehicles")]
    public void CreateCarrier_UsesOneSupportedDurableRoot(
        string kind,
        string ownerId,
        string? containerId,
        string expectedRootProperty)
    {
        var root = MortalItemTestFixture.CreateCarrier(
            MortalItemTestFixture.CreateCanonicalRoot(),
            kind,
            ownerId,
            containerId);

        Assert.True(root.ContainsKey(expectedRootProperty));
        Assert.Equal(1, CountItemOccurrences(root));
    }

    [Fact]
    public void CreateReceiptlessNegative_RejectsReceiptlessCanonicalInput()
    {
        var item = MortalItemTestFixture.CreateReceiptlessNegative();

        Assert.Equal(MortalItemTestFixture.ItemId, item["itemId"]!.GetValue<string>());
        Assert.Equal(MortalItemTestFixture.ItemId, item["existedId"]!.GetValue<string>());
        Assert.False(item.ContainsKey("materialization"));
        Assert.False(item.ContainsKey("materializationReceipt"));
    }

    [Fact]
    public void CreateFragmentOnly_ReturnsLabeledWrapperNotCanonicalItem()
    {
        var wrapper = MortalItemTestFixture.CreateFragmentOnly("Фрагмент записи");

        Assert.Equal("fragment_only", wrapper["fixtureKind"]!.GetValue<string>());
        var fragment = wrapper["fragment"]!.AsObject();
        Assert.Equal("Фрагмент записи", fragment["name"]!.GetValue<string>());
        Assert.False(fragment.ContainsKey("existedId"));
        Assert.False(fragment.ContainsKey("materialization"));
    }

    private static int CountItemOccurrences(JsonNode? node)
    {
        if (node is JsonArray array)
            return array.Sum(CountItemOccurrences);
        if (node is not JsonObject obj)
            return 0;

        var self = obj.ContainsKey("itemId") && obj.ContainsKey("materializationReceipt") ? 1 : 0;
        return self + obj.Sum(pair => CountItemOccurrences(pair.Value));
    }
}
