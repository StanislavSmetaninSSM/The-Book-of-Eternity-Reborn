using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalVehicleBirthReceiptTests
{
    [Theory]
    [InlineData("orphan", "mortal_item_materialization_container_parent_missing")]
    [InlineData("noncontainer", "mortal_item_materialization_container_parent_invalid")]
    [InlineData("chain", "mortal_item_materialization_container_path_chain_mismatch")]
    public void HistoricalCargo_RequiresAdmittedContainerGraph(string shape, string expectedCode)
    {
        var root = RootWithCargo();
        var receipt = root["birthReceipts"]![0]!.AsObject();
        var birth = receipt["birthState"]!.AsObject();
        var outer = birth["inventory"]![0]!.AsObject();
        if (shape == "chain")
        {
            MakeContainer(outer);
            var parent = BirthItem("parent_item", "cargo_item");
            MakeContainer(parent);
            // All parents exist and are containers; the leaf omits its outer ancestor.
            var leaf = BirthItem("leaf_item", "parent_item");
            birth["inventory"]!.AsArray().Add(parent);
            birth["inventory"]!.AsArray().Add(leaf);
            birth["cargoMeasurements"] = new JsonArray(new JsonObject
            {
                ["itemId"] = "cargo_item", ["count"] = 1, ["externalVolumeDm3"] = 1,
                ["measuredConfiguration"] = MeasurementBasis(outer,
                    MeasurementBasis(parent, MeasurementBasis(leaf)))
            });
        }
        else
            birth["inventory"]!.AsArray().Add(BirthItem("child_item", shape == "orphan" ? "missing_parent" : "cargo_item"));
        var catalog = HistoricalCatalog(birth);
        Assert.Empty(catalog.Issues);
        foreach (var item in catalog.Occurrences)
            Assert.Empty(MortalItemMaterializationContract.Validate(JsonSerializer.SerializeToElement(item.Item),
                item.JsonPath, MortalItemMaterializationPhase.CanonicalPostSeal));
        var containerIssues = new List<ValidationIssue>();
        ValidationService.ValidateCanonicalContainerPaths(catalog, containerIssues);
        Assert.Contains(containerIssues, issue => issue.Code == expectedCode);
        // The capacity algorithm intentionally assumes an admitted graph. Capture its
        // output to isolate that missing precondition from unrelated seal/shape errors.
        var capacity = MortalCargoBirthCapacity.Validate("vehicle_cart", birth, catalog,
            new Dictionary<string, string>(), 42, "fixture");
        Assert.True(capacity.IsValid, string.Join(Environment.NewLine, capacity.Issues.Select(i => i.Expected)));
        birth["cargoBirthEvidence"] = capacity.Evidence;
        birth.Remove("cargoMeasurements");
        Reseal(receipt);

        Assert.NotEmpty(MortalVehicleBirthReceiptState.ValidateLocal(root));
    }

    [Fact]
    public void HistoricalCargo_RejectsEmbeddedAcceptanceAfterVehicleBirth()
    {
        var root = RootWithCargo();
        var receipt = root["birthReceipts"]![0]!.AsObject();
        var item = receipt["birthState"]!["inventory"]![0]!.AsObject();
        item["materializationReceipt"]!["instanceKind"] = "split_derived";
        item["materializationReceipt"]!["parentItemIds"] = new JsonArray("parent_item");
        item["materializationReceipt"]!["acceptedAtTurn"] = 43;
        MortalItemTestFixture.ResealCanonical(item);
        Assert.Empty(MortalItemMaterializationContract.Validate(JsonSerializer.SerializeToElement(item),
            "historical_item", MortalItemMaterializationPhase.CanonicalPostSeal));
        Reseal(receipt);

        Assert.NotEmpty(MortalVehicleBirthReceiptState.ValidateLocal(root));
    }

    [Theory]
    [InlineData("birthReceipts")]
    [InlineData("cargoBirthEvidence")]
    public void ProjectionReferenceRewrite_PreservesHistoricalSubtreesAfterCargoLeaves(string field)
    {
        var root = RootWithCargo();
        root["vehicles"]![0]!["inventory"] = new JsonArray();
        var evidence = field == "birthReceipts" ? root[field]! : root["vehicles"]![0]![field]!;
        var before = evidence.DeepClone();
        // This is the actual rewriter used by item projection at every current root.
        // A global later creation map need not have a live occurrence in this vehicle.
        CanonicalStateNormalizer.RewriteMortalItemCreationReferences(root,
            new Dictionary<string, string> { ["cargo_item"] = "later_item" });

        var after = field == "birthReceipts" ? root[field]! : root["vehicles"]![0]![field]!;
        Assert.True(JsonNode.DeepEquals(before, after));
        Assert.Empty(MortalVehicleBirthReceiptState.ValidateLocal(root));
    }

    [Fact]
    public void ProjectionReferenceRewrite_StillResolvesCurrentReferences()
    {
        var root = new JsonObject { ["vehicles"] = new JsonArray(new JsonObject
        {
            ["vehicleId"] = "vehicle_cart", ["inventory"] = new JsonArray(new JsonObject { ["sourceItemId"] = "new_ref" })
        }) };
        Assert.True(CanonicalStateNormalizer.RewriteMortalItemCreationReferences(root,
            new Dictionary<string, string> { ["new_ref"] = "new_item" }));
        Assert.Equal("new_item", root["vehicles"]![0]!["inventory"]![0]!["sourceItemId"]!.GetValue<string>());
    }

    private static JsonObject BirthItem(string id, params string[] parents)
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot(id);
        item["contentsPath"] = new JsonArray(parents.Select(p => (JsonNode?)JsonValue.Create(p)).ToArray());
        item["materialization"]!["route"] = "new_vehicle_inventory";
        item["materialization"]!["sourceAuthority"] = new JsonObject { ["kind"] = "new_vehicle", ["authorityId"] = "new_cart" };
        MortalItemTestFixture.ResealCanonical(item);
        return item;
    }

    private static void MakeContainer(JsonObject item)
    {
        item["isContainer"] = true; item["capacity"] = 10; item["containerWeight"] = 0.1; item["weightReduction"] = 0;
        item["materialization"]!["sections"]!["container"] = new JsonObject { ["state"] = "populated", ["reason"] = null };
        MortalItemTestFixture.ResealCanonical(item);
    }

    // Explicit accepted measurement fixture, not a second validation algorithm.
    private static JsonObject MeasurementBasis(JsonObject item, params JsonObject[] children)
    {
        var shape = item.DeepClone().AsObject();
        foreach (var field in new[] { "itemId", "id", "initialId", "existedId", "creationRef", "materializationReceipt", "materialization",
            "resourceMaterialization", "count", "quantity", "contentsPath", "isCarried", "currentLocationId", "currentLocationName" })
            shape.Remove(field);
        var result = new JsonObject { ["item"] = shape, ["contents"] = new JsonArray(children.Select(c => (JsonNode)c).ToArray()) };
        if (item["itemId"]!.GetValue<string>() == "cargo_item") result["schemaVersion"] = 1;
        else { result["itemId"] = item["itemId"]!.DeepClone(); result["count"] = item["count"]!.DeepClone(); }
        return result;
    }

    private static MortalItemCarrierCatalog HistoricalCatalog(JsonObject birth) => MortalItemCarrierCatalog.Build(
        new(null, null, null, null, new JsonObject { ["vehicles"] = new JsonArray(birth.DeepClone()) }, new Dictionary<string, JsonObject>()));
}
