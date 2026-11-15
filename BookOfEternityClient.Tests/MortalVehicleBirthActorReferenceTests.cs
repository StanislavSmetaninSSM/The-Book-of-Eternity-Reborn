using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalVehicleBirthActorReferenceTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CompleteNpcAndFactionReferencesResolve(bool newNpc, bool newFaction)
    {
        var npc = Npc(newNpc); var factions = Factions(newFaction);
        var authority = Compose(npc, newNpc);
        var vehicles = Vehicles();
        var before = vehicles.ToJsonString(); var npcBefore = npc.ToJsonString(); var factionBefore = factions.ToJsonString();
        Assert.Empty(MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthActorReferences(vehicles, npc, factions, authority));
        Assert.Equal(before, vehicles.ToJsonString()); Assert.Equal(npcBefore, npc.ToJsonString()); Assert.Equal(factionBefore, factions.ToJsonString());
    }

    [Theory]
    [InlineData("npcUnknown", "unknown")]
    [InlineData("npcCase", "confusable")]
    [InlineData("factionUnknown", "unknown")]
    [InlineData("factionCase", "confusable")]
    [InlineData("factionName", "name_mismatch")]
    [InlineData("factionDuplicate", "ambiguous")]
    [InlineData("npcRealm", "realm_invalid")]
    public void InvalidSelectedReferenceIsIndexedAndBounded(string mutation, string suffix)
    {
        var npc = Npc(false); var factions = Factions(false); var vehicles = Vehicles();
        switch (mutation)
        {
            case "npcUnknown": vehicles["UpdateVehicles"]![0]!["controller"]!["actorId"] = "missing"; break;
            case "npcCase": vehicles["UpdateVehicles"]![0]!["controller"]!["actorId"] = "NPC_DRIVER"; break;
            case "factionUnknown": vehicles["UpdateVehicles"]![0]!["owner"]!["ownerId"] = "missing"; break;
            case "factionCase": vehicles["UpdateVehicles"]![0]!["owner"]!["ownerId"] = "FACTION_OWNER"; break;
            case "factionName": vehicles["UpdateVehicles"]![0]!["owner"]!["ownerName"] = "Other name"; break;
            case "factionDuplicate": factions["factions"]!.AsArray().Add(factions["factions"]![0]!.DeepClone()); break;
            case "npcRealm": npc["NPCsInScene"]![0]!["materialization"]!["actorType"] = "guardian"; break;
        }
        var issues = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthActorReferences(vehicles, npc, factions, Compose(npc, false));
        Assert.Contains(issues, i => i.Code == "resource_owner_vehicle_birth_reference_" + suffix &&
            i.FilePath.StartsWith("game_state/misc/vehicles.json.UpdateVehicles[0].", StringComparison.Ordinal));
        Assert.Empty(ResourceRepairPacketBuilder.Build(issues));
        Assert.True(ResourceRepairPacketBuilder.RequiresFailClosedRollback(issues));
    }

    [Fact]
    public void LegitimateCrossSectionMirrorsRemainAccepted()
    {
        var npc = Npc(false);
        npc["UpdateNPCs"] = new JsonArray(npc["NPCsInScene"]![0]!.DeepClone());
        Assert.Empty(MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthActorReferences(Vehicles(), npc, Factions(false), Compose(npc, false)));
    }

    [Fact]
    public void UnselectedActorEnvelopeIsNotValidatedAndExistingVehiclesAreIgnored()
    {
        var npc = Npc(false);
        var unused = EffectMaterializationTestFixture.CreateSameTurnMortalActor("unused");
        unused["NPCId"] = "unused"; unused.Remove("initialId"); unused.Remove("materialization");
        npc["NPCsInScene"]!.AsArray().Add(unused);
        var vehicles = Vehicles();
        vehicles["UpdateVehicles"]!.AsArray().Add(new JsonObject { ["vehicleId"] = "existing", ["controller"] = new JsonObject { ["actorType"] = "npc", ["actorId"] = "missing" } });
        Assert.Empty(MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthActorReferences(vehicles, npc, Factions(false), Compose(npc, false)));
    }

    [Fact]
    public void RepeatedSelectedInvalidActorReportsEachVehicleCoordinate()
    {
        var npc = Npc(false); npc["NPCsInScene"]![0]!["materialization"]!["actorType"] = "guardian";
        var vehicles = Vehicles(); var second = vehicles["UpdateVehicles"]![0]!.DeepClone(); second["vehicleRef"] = "new_second";
        vehicles["UpdateVehicles"]!.AsArray().Add(second);
        var issues = MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthActorReferences(vehicles, npc, Factions(false), Compose(npc, false));
        Assert.Equal(2, issues.Count);
        Assert.Contains(issues, i => i.FilePath == "game_state/misc/vehicles.json.UpdateVehicles[0].controller.actorId");
        Assert.Contains(issues, i => i.FilePath == "game_state/misc/vehicles.json.UpdateVehicles[1].controller.actorId");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawCreationReferencesDoNotBecomeActorEnvelopeErrors(bool equipped)
    {
        var npc = Npc(true);
        var actor = npc["UpdateNPCs"]![0]!.AsObject();
        actor["inventory"] = new JsonArray(MortalItemTestFixture.CreateRawRoot(
            route: "new_npc_inventory", authorityKind: "new_npc", authorityId: "npc_driver",
            sourceTurn: 42, creationRef: "driver_item", materializationId: "driver_item_birth"));
        actor["materialization"]!["capabilities"]!["ownsItems"] = true;
        actor["materialization"]!["sections"]!["inventory"] = new JsonObject { ["state"] = "populated" };
        if (equipped) actor["equippedItems"] = new JsonObject { ["mainHand"] = "driver_item" };
        Assert.Empty(MortalLocationAcceptedTurnPlanner.ValidateVehicleBirthActorReferences(
            Vehicles(), npc, Factions(false), Compose(npc, true)));
    }

    private static JsonObject Npc(bool fresh)
    {
        var actor = EffectMaterializationTestFixture.CreateSameTurnMortalActor("npc_driver");
        if (!fresh) { actor["NPCId"] = "npc_driver"; actor.Remove("initialId"); }
        else actor["resourceMaterialization"] = new JsonObject { ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = "health", ["maximum"] = 100 }) };
        return new JsonObject { [fresh ? "UpdateNPCs" : "NPCsInScene"] = new JsonArray(actor) };
    }
    private static JsonObject Factions(bool fresh)
    {
        var faction = EffectMaterializationTestFixture.CreateSameTurnMortalFaction("faction_owner");
        faction.Remove("activeEffectDefinitions"); faction["name"] = "Owner faction";
        if (!fresh) { faction["factionId"] = "faction_owner"; faction.Remove("initialId"); faction.Remove("isNewFaction"); }
        return new JsonObject { [fresh ? "factionDataChanges" : "factions"] = new JsonArray(faction) };
    }
    private static JsonObject Vehicles() => new() { ["UpdateVehicles"] = new JsonArray(new JsonObject
    { ["vehicleRef"] = "new_cart", ["owner"] = new JsonObject { ["ownerType"] = "Faction", ["ownerId"] = "faction_owner", ["ownerName"] = "Owner faction" },
      ["controller"] = new JsonObject { ["actorType"] = "npc", ["actorId"] = "npc_driver" } }) };
    private static ResourceOwnerAuthority Compose(JsonObject npc, bool fresh)
    {
        var result = MortalResourceOwnerComposer.Compose(new MortalResourceOwnerCompositionInput(ResourceDefinitionCatalog.CreateBuiltIn(),
            Roots(fresh ? new JsonObject() : npc), Roots(npc)));
        Assert.Empty(result.Issues);
        return Assert.IsType<ResourceOwnerAuthority>(result.Authority);
    }
    private static MortalResourceOwnerRoots Roots(JsonObject npc) => new(npc,
        new JsonObject { ["enemiesData"] = new JsonArray() }, new JsonObject { ["alliesData"] = new JsonArray() },
        new JsonObject { ["vehicles"] = new JsonArray() });
}
