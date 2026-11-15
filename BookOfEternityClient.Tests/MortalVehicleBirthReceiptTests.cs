using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Detached receipt DATA policy. No world setup or publication authority.</summary>
public sealed partial class MortalVehicleBirthReceiptTests
{
    [Fact]
    public void CompleteReceipt_IsDetachedAndDoesNotTrackLiveUpdates()
    {
        var root = Root();
        Assert.Empty(MortalVehicleBirthReceiptState.ValidateLocal(root));
        root["vehicles"]![0]!["name"] = "Renamed cart";
        root["vehicles"]![0]!["inventory"]!.AsArray().Add(new JsonObject { ["itemId"] = "later_item" });
        Assert.Equal("Cart", root["birthReceipts"]![0]!["birthState"]!["name"]!.GetValue<string>());
        Assert.Empty(MortalVehicleBirthReceiptState.ValidateLocal(root));
    }

    [Theory]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("image_prompt")]
    [InlineData("type")]
    [InlineData("isSentient")]
    [InlineData("availability")]
    [InlineData("currentLocationId")]
    [InlineData("speedBonus")]
    [InlineData("actions")]
    [InlineData("resistances")]
    public void HistoricalBirth_RejectsMissingCoreFactEvenWhenResealed(string field)
    {
        var root = Root();
        var receipt = root["birthReceipts"]![0]!.AsObject();
        receipt["birthState"]!.AsObject().Remove(field);
        receipt["seal"] = MortalVehicleBirthReceiptState.ComputeSeal(receipt);
        Assert.NotEmpty(MortalVehicleBirthReceiptState.ValidateLocal(root));
    }

    [Theory]
    [InlineData("schemaVersion", "2")]
    [InlineData("vehicleId", "\"different\"")]
    [InlineData("sourceTurn", "0")]
    [InlineData("sourceAuthority", "null")]
    [InlineData("birthState", "false")]
    [InlineData("resourceInitialization", "[]")]
    [InlineData("seal", "\"wrong\"")]
    public void MalformedReceipt_RefusesWithoutThrowingOrMutation(string field, string json)
    {
        var root = Root();
        root["birthReceipts"]![0]![field] = JsonNode.Parse(json);
        var before = root.ToJsonString();
        Assert.NotEmpty(MortalVehicleBirthReceiptState.ValidateLocal(root));
        Assert.Equal(before, root.ToJsonString());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("scalar")]
    [InlineData("duplicate")]
    public void ReceiptCollection_RequiresCurrentExplicitUniqueEvidence(string mutation)
    {
        var root = Root();
        switch (mutation)
        {
            case "missing": root.Remove("birthReceipts"); break;
            case "null": root["birthReceipts"] = null; break;
            case "scalar": root["birthReceipts"] = 1; break;
            case "duplicate": root["birthReceipts"]!.AsArray().Add(root["birthReceipts"]![0]!.DeepClone()); break;
        }
        Assert.NotEmpty(MortalVehicleBirthReceiptState.ValidateLocal(root));
    }

    [Fact]
    public void Continuity_PreservesRetiredReceiptAndRejectsRewriting()
    {
        var before = Root();
        var after = before.DeepClone().AsObject();
        after["vehicles"] = new JsonArray();
        Assert.Empty(MortalVehicleBirthReceiptState.ValidateContinuity(before, after));
        after["birthReceipts"]![0]!["sourceTurn"] = 43;
        Assert.NotEmpty(MortalVehicleBirthReceiptState.ValidateContinuity(before, after));
    }

    [Theory]
    [InlineData("vehicle_cart")]
    [InlineData("VEHICLE_CART")]
    public void Preparation_ReservesRetiredExactAndConfusableBirthIdentity(string allocated)
    {
        var before = Root();
        before["vehicles"] = new JsonArray();
        before["activeVehicleId"] = null;
        var raw = before.DeepClone().AsObject();
        var birth = before["birthReceipts"]![0]!["birthState"]!.DeepClone().AsObject();
        birth.Remove("vehicleId"); birth.Remove("cargoBirthEvidence");
        birth["vehicleRef"] = "new_cart";
        raw["UpdateVehicles"] = new JsonArray(birth);
        var source = raw.ToJsonString();
        var result = MortalVehicleBirthPreparation.Prepare(before, raw, new FixedIdentity(allocated));
        Assert.False(result.IsValid);
        Assert.Null(result.Preparation);
        Assert.Equal(source, raw.ToJsonString());
    }

    [Fact]
    public void Guidance_DeclaresClientOwnedHistoricalReceiptsAndWorkedExample()
    {
        foreach (var path in new[] { "Rules/Block_10.txt", "Rules/Block_CLI_Operations.txt", "BookOfEternityClient/game_master_daemon.ps1" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            Assert.Contains("vehicles.json.birthReceipts is client-owned immutable creation evidence.", text, StringComparison.Ordinal);
            Assert.Contains("including receipts for removed vehicles", text, StringComparison.Ordinal);
            Assert.Contains("Later legitimate vehicle, cargo or access changes do not rewrite historical birth facts.", text, StringComparison.Ordinal);
        }
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_Block_10.V.txt"));
        Assert.Contains("Do not write birthReceipts: the client supplies the resolved historical receipt", example, StringComparison.Ordinal);
    }

    private sealed class FixedIdentity(string id) : VehicleIdentityFactory
    {
        internal override string CreateVehicleId() => id;
    }

    private static JsonObject Root()
    {
        var vehicle = JsonNode.Parse("""
            {"vehicleId":"vehicle_cart","name":"Cart","description":"Supply cart","image_prompt":"Wooden cart",
             "type":"Vehicle","isSentient":false,"availability":"Active","currentLocationId":null,
             "speedBonus":0,"actions":[],"resistances":[],"inventory":[],"capacity":12,"volume":100,
             "owner":null,"controller":null,"authorizedUsers":[],"hasFullAccess":true,
             "cargoBirthEvidence":{"schemaVersion":1,"vehicleId":"vehicle_cart","sourceTurn":42,"cargoMeasurements":[]}}
            """)!.AsObject();
        var receipt = new JsonObject
        {
            ["schemaVersion"] = 1, ["vehicleId"] = "vehicle_cart", ["vehicleRef"] = "new_cart", ["sourceTurn"] = 42,
            ["sourceAuthority"] = new JsonObject { ["sessionId"] = "session", ["requestId"] = "request", ["snapshotToken"] = "snapshot" },
            ["birthState"] = vehicle.DeepClone(),
            ["resourceInitialization"] = JsonNode.Parse("""[{"resourceKey":"health","maximum":100,"sourceKind":"owner_materialization","sourceId":"new_cart","authorityFingerprint":"sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}]""")
        };
        receipt["seal"] = MortalVehicleBirthReceiptState.ComputeSeal(receipt);
        return new JsonObject { ["vehicles"] = new JsonArray(vehicle), ["activeVehicleId"] = "vehicle_cart", ["birthReceipts"] = new JsonArray(receipt) };
    }
}
