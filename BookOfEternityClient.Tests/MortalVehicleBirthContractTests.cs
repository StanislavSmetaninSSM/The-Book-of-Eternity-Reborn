using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>First-birth semantic admission through the real owner composer, not a detached validator.</summary>
public sealed class MortalVehicleBirthContractTests
{
    [Fact]
    public void Compose_CompleteVehicleBirthRetainsSemanticsAndAllocatesHealthOwner()
    {
        var before = EmptyRoot();
        var current = BirthRoot();
        var beforeJson = before.ToJsonString();
        var currentJson = current.ToJsonString();
        var result = Compose(before, current);

        Assert.Empty(result.Issues);
        var authority = Assert.IsType<ResourceOwnerAuthority>(result.Authority);
        var owner = Assert.Single(authority.Entries.Values,
            entry => entry.Key.OwnerKind == ResourceOwnerKind.Vehicle);
        Assert.True(owner.SameTurn);
        Assert.Equal("new_cart", owner.SameTurnRef);
        Assert.Equal(new[] { "health" }, owner.ResourceCapabilities);
        var after = result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath];
        var vehicle = Assert.Single(after["vehicles"]!.AsArray())!.AsObject();
        Assert.Equal(owner.Key.ResourceOwnerId, vehicle["vehicleId"]!.GetValue<string>());
        Assert.Equal(owner.Key.ResourceOwnerId, after["activeVehicleId"]!.GetValue<string>());
        foreach (var field in new[] { "capacity", "volume", "owner", "controller", "authorizedUsers", "hasFullAccess" })
            Assert.True(JsonNode.DeepEquals(current["UpdateVehicles"]![0]![field], vehicle[field]), field);
        Assert.False(vehicle.ContainsKey("vehicleRef"));
        Assert.False(vehicle.ContainsKey("resourceMaterialization"));
        Assert.Equal(beforeJson, before.ToJsonString());
        Assert.Equal(currentJson, current.ToJsonString());
    }

    [Theory]
    [InlineData("capacity")]
    [InlineData("volume")]
    [InlineData("owner")]
    [InlineData("controller")]
    [InlineData("authorizedUsers")]
    [InlineData("hasFullAccess")]
    public void Compose_RejectsMissingBirthSemanticField(string field)
    {
        var current = BirthRoot();
        current["UpdateVehicles"]![0]!.AsObject().Remove(field);
        AssertRejectedWithoutMutation(current);
    }

    [Theory]
    [InlineData("capacity", "-1")]
    [InlineData("capacity", "0.5")]
    [InlineData("capacity", "2147483648")]
    [InlineData("capacity", "\"12\"")]
    [InlineData("capacity", "null")]
    [InlineData("capacity", "true")]
    [InlineData("volume", "-0.5")]
    [InlineData("volume", "\"100\"")]
    [InlineData("volume", "null")]
    [InlineData("volume", "true")]
    [InlineData("volume", "1e400")]
    public void Compose_RejectsInvalidCargoLimitNumber(string field, string rawJson)
    {
        var current = BirthRoot();
        // 1e400 is valid JSON syntax but not a finite double; avoid non-JSON NaN literals.
        current["UpdateVehicles"]![0]![field] = JsonNode.Parse(rawJson);
        AssertRejectedWithoutMutation(current);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("controller")]
    [InlineData("authorizedUser")]
    public void Compose_RejectsUnresolvedPlayerAuthority(string mutation)
    {
        var current = BirthRoot();
        var vehicle = current["UpdateVehicles"]![0]!.AsObject();
        switch (mutation)
        {
            case "owner":
                vehicle["owner"]!["ownerId"] = "player_unknown";
                break;
            case "controller":
                vehicle["controller"]!["actorId"] = "player_unknown";
                break;
            case "authorizedUser":
                vehicle["authorizedUsers"]!.AsArray().Add(new JsonObject
                {
                    ["playerId"] = "player_unknown", ["playerName"] = "Unknown player"
                });
                break;
            default:
                throw new InvalidOperationException("Unknown test mutation.");
        }
        AssertRejectedWithoutMutation(current);
    }

    [Theory]
    [InlineData("owner", "true")]
    [InlineData("owner", "{\"ownerType\":\"Unknown\",\"ownerId\":\"player_current\",\"ownerName\":\"Player\"}")]
    [InlineData("controller", "{\"actorType\":\"player\",\"actorId\":\" player_current\"}")]
    [InlineData("controller", "{\"actorType\":\"player\",\"actorId\":\"player_current\",\"permission\":true}")]
    [InlineData("authorizedUsers", "[{\"playerId\":\"player_current\",\"playerName\":\"Player\"},{\"playerId\":\"player_current\",\"playerName\":\"Player\"}]")]
    [InlineData("hasFullAccess", "\"true\"")]
    public void Compose_RejectsMalformedExplicitBirthFacts(string field, string json)
    {
        var current = BirthRoot();
        current["UpdateVehicles"]![0]![field] = JsonNode.Parse(json);
        AssertRejectedWithoutMutation(current);
    }

    [Fact]
    public void Compose_ExplicitUnownedUncontrolledBirthDoesNotInferActors()
    {
        var current = BirthRoot();
        var vehicle = current["UpdateVehicles"]![0]!;
        vehicle["owner"] = null;
        vehicle["controller"] = null;
        vehicle["authorizedUsers"] = null;
        var result = Compose(EmptyRoot(), current);
        Assert.Empty(result.Issues);
        var after = Assert.Single(result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath]["vehicles"]!.AsArray())!;
        Assert.Null(after["owner"]);
        Assert.Null(after["controller"]);
        Assert.Null(after["authorizedUsers"]);
    }

    [Fact]
    public void BirthGuidance_DeclaresLimitsAndExistingPlayerAuthority()
    {
        foreach (var path in new[] { "Rules/Block_2.txt", "Rules/Block_10.txt", "Rules/Block_CLI_Operations.txt", "BookOfEternityClient/game_master_daemon.ps1" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            Assert.Contains("Mortal vehicle birth requires capacity", text, StringComparison.Ordinal);
            Assert.Contains("Player identities must be exactly player_current", text, StringComparison.Ordinal);
            Assert.Contains("not permission to bypass accepted ownership/access checks", text, StringComparison.Ordinal);
        }
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_Block_10.V.txt"));
        Assert.Contains("player_current", example, StringComparison.Ordinal);
    }

    [Fact]
    public void CargoGuidance_DeclaresExactBirthRouteAndCompleteExample()
    {
        foreach (var path in new[] { "Rules/Block_2.txt", "Rules/Block_19.A.txt", "Rules/Block_CLI_Operations.txt", "BookOfEternityClient/game_master_daemon.ps1", "Examples/E_CLI_Step_Main.txt" })
        {
            var text = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            Assert.Contains("new_vehicle_inventory", text, StringComparison.Ordinal);
            Assert.Contains("UpdateVehicles[i].vehicleRef", text, StringComparison.Ordinal);
            Assert.Contains("does not authorize existing-item transfers", text, StringComparison.Ordinal);
        }
        foreach (var path in new[] { "Rules/Block_10.txt", "BookOfEternityClient/game_master_daemon.ps1" })
        {
            var references = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            Assert.Contains("exact accepted Mortal faction", references, StringComparison.Ordinal);
            Assert.Contains("complete Mortal NPC materialization evidence", references, StringComparison.Ordinal);
            Assert.Contains("same-turn initialId", references, StringComparison.Ordinal);
        }
        var daemon = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient/game_master_daemon.ps1"));
        Assert.Contains("mortal_vehicle_birth_cargo_example", daemon, StringComparison.Ordinal);
        foreach (var path in new[] { "Rules/Block_2.txt", "Rules/Block_10.txt", "Rules/Block_19.A.txt", "Rules/Block_CLI_Operations.txt", "BookOfEternityClient/game_master_daemon.ps1" })
        {
            var instructions = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, path));
            Assert.Contains("measuredConfiguration", instructions, StringComparison.Ordinal);
            Assert.Contains("immutable birth evidence", instructions, StringComparison.Ordinal);
        }
        var example = File.ReadAllText(Path.Combine(TestRepoPaths.RepoRoot, "Examples/E_CLI_New_Vehicle_Cargo_Materialization.txt"));
        var root = JsonNode.Parse(example[example.IndexOf("\n{", StringComparison.Ordinal)..])!.AsObject();
        var vehicle = Assert.Single(root["UpdateVehicles"]!.AsArray())!.AsObject();
        var item = Assert.Single(vehicle["inventory"]!.AsArray())!.AsObject();
        Assert.Equal(vehicle["vehicleRef"]!.GetValue<string>(), item["materialization"]!["sourceAuthority"]!["authorityId"]!.GetValue<string>());
        using var document = System.Text.Json.JsonDocument.Parse(item.ToJsonString());
        Assert.Empty(MortalItemMaterializationContract.Validate(document.RootElement,
            "vehicles.UpdateVehicles[0].inventory[0]", MortalItemMaterializationPhase.RawPreSeal));
        var canonical = item.DeepClone().AsObject();
        canonical.Remove("creationRef");
        canonical["itemId"] = "doc_item";
        var catalog = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(null, null, null, null,
            new JsonObject { ["vehicles"] = new JsonArray(new JsonObject
            { ["vehicleId"] = "doc_vehicle", ["inventory"] = new JsonArray(canonical) }) },
            new Dictionary<string, JsonObject>()));
        var measurement = MortalCargoBirthCapacity.Validate("doc_vehicle", vehicle, catalog,
            new Dictionary<string, string> { [item["creationRef"]!.GetValue<string>()] = "doc_item" }, 42, "example");
        Assert.Empty(measurement.Issues);
        Assert.Equal(2, measurement.Evidence!["cargoMeasurements"]![0]!["count"]!.GetValue<int>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Compose_RejectsHistoricalCargoEvidenceMutation(bool command)
    {
        var before = Compose(EmptyRoot(), BirthRoot()).OwnerCompanionAfterImages[
            StorageTransportMoveService.VehiclesPath].DeepClone().AsObject();
        var vehicle = before["vehicles"]![0]!.AsObject();
        vehicle["cargoBirthEvidence"] = new JsonObject { ["schemaVersion"] = 1, ["sourceTurn"] = 42 };
        var current = before.DeepClone().AsObject();
        if (command)
            current["UpdateVehicles"] = new JsonArray(new JsonObject
            {
                ["vehicleId"] = vehicle["vehicleId"]!.DeepClone(),
                ["cargoBirthEvidence"] = new JsonObject { ["schemaVersion"] = 1, ["sourceTurn"] = 99 }
            });
        else current["vehicles"]![0]!["cargoBirthEvidence"]!["sourceTurn"] = 99;
        var original = before.ToJsonString();
        var proposal = current.ToJsonString();

        var result = Compose(before, current);

        Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_birth_evidence_immutable");
        Assert.Null(result.Authority);
        Assert.Equal(original, before.ToJsonString());
        Assert.Equal(proposal, current.ToJsonString());
    }

    [Fact]
    public void Compose_LaterCargoChangeDoesNotReevaluateHistoricalMeasurement()
    {
        var before = Compose(EmptyRoot(), BirthRoot()).OwnerCompanionAfterImages[
            StorageTransportMoveService.VehiclesPath].DeepClone().AsObject();
        var vehicle = before["vehicles"]![0]!.AsObject();
        vehicle["cargoBirthEvidence"] = new JsonObject { ["schemaVersion"] = 1, ["sourceTurn"] = 42 };
        vehicle["inventory"] = new JsonArray(new JsonObject { ["itemId"] = "existing_item", ["count"] = 2 });
        var current = before.DeepClone().AsObject();
        current["vehicles"]![0]!["inventory"]![0]!["count"] = 1;
        current["UpdateVehicles"] = new JsonArray(new JsonObject
        { ["vehicleId"] = vehicle["vehicleId"]!.DeepClone(), ["name"] = "Renamed cart" });
        var result = Compose(before, current);
        Assert.Empty(result.Issues);
        Assert.True(JsonNode.DeepEquals(vehicle["cargoBirthEvidence"],
            result.OwnerCompanionAfterImages[StorageTransportMoveService.VehiclesPath]["vehicles"]![0]!["cargoBirthEvidence"]));
        // This cheap owner-composition check is not an authorized item consumption test.
    }

    private static void AssertRejectedWithoutMutation(JsonObject current)
    {
        var before = EmptyRoot();
        var beforeJson = before.ToJsonString();
        var currentJson = current.ToJsonString();
        var result = Compose(before, current);
        Assert.Contains(result.Issues, issue => issue.Code == "resource_owner_vehicle_birth_invalid");
        Assert.Null(result.Authority);
        Assert.Empty(result.OwnerCompanionAfterImages);
        Assert.Equal(beforeJson, before.ToJsonString());
        Assert.Equal(currentJson, current.ToJsonString());
    }

    private static ResourceOwnerCompositionResult Compose(JsonObject before, JsonObject current) =>
        MortalResourceOwnerComposer.Compose(new MortalResourceOwnerCompositionInput(
            ResourceDefinitionCatalog.CreateBuiltIn(), Roots(before), Roots(current)));

    private static MortalResourceOwnerRoots Roots(JsonObject vehicles) => new(
        new JsonObject { ["NPCsInScene"] = new JsonArray() },
        new JsonObject { ["enemiesData"] = new JsonArray() },
        new JsonObject { ["alliesData"] = new JsonArray() }, vehicles);

    private static JsonObject EmptyRoot() => new() { ["vehicles"] = new JsonArray() };

    private static JsonObject BirthRoot() => JsonNode.Parse("""
        {
          "vehicles": [],
          "activeVehicleChange": "new_cart",
          "UpdateVehicles": [{
            "vehicleRef": "new_cart",
            "name": "Cart",
            "description": "A serviceable cart used to carry supplies.",
            "image_prompt": "A wooden supply cart with iron-rimmed wheels",
            "type": "Vehicle",
            "isSentient": false,
            "availability": "Active",
            "currentLocationId": null,
            "speedBonus": 0,
            "actions": [],
            "resistances": [],
            "inventory": [],
            "capacity": 12,
            "volume": 100.0,
            "owner": { "ownerType": "Player", "ownerId": "player_current", "ownerName": "Current player" },
            "controller": { "actorType": "player", "actorId": "player_current" },
            "authorizedUsers": [],
            "hasFullAccess": true,
            "resourceMaterialization": { "resources": [{ "resourceKey": "health", "maximum": 100 }] }
          }]
        }
        """)!.AsObject();
}
