using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

// Reduced authored DATA controls motivated by mm6, not a replay of its packet,
// signed original, route admission, or proof of its unrecorded rejection reason.
public sealed class MortalRawSceneDiagnosticTests(ITestOutputHelper output)
{
    [Fact]
    public void CurrentSceneMissingEventDescriptionsHasConcreteShapeIssue()
    {
        var scene = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        // The submitted scene supplied these arrays instead of required eventDescriptions.
        scene.Remove("eventDescriptions");
        scene["currentInteractions"] = new JsonArray();
        scene["currentChronology"] = new JsonArray();
        using var raw = JsonDocument.Parse(scene.ToJsonString());
        var issues = MortalLocationMaterializationContract.ValidateRawLocation(raw.RootElement,
            "currentLocationData", "current_scene_creation");
        Report("unbound location DATA", issues);
        Assert.Contains(issues, i => i.Code == "mortal_location_materialization_governed_field_missing" &&
            i.FilePath == "currentLocationData.eventDescriptions");
        scene["eventDescriptions"] = new JsonArray();
        using var corrected = JsonDocument.Parse(scene.ToJsonString());
        Assert.Empty(MortalLocationMaterializationContract.ValidateRawLocation(corrected.RootElement,
            "currentLocationData", "current_scene_creation"));
    }

    [Fact]
    public void RawLinkMayNameSameTurnTargetInitialIdWithoutPermanentId()
    {
        var link = MortalLocationTestFixture.CreateRawLink("loc_existing", "loc_placeholder");
        link["targetLocationId"] = null;
        link["targetInitialId"] = "locref_new_court";
        using var raw = JsonDocument.Parse(link.ToJsonString());
        var issues = MortalLocationMaterializationContract.ValidateRawLink(raw.RootElement,
            "worldMapUpdates.newLinks[0]", "world_map_link_creation");
        Report("link shape only; endpoint resolution not exercised", issues);
        Assert.Empty(issues);
    }

    [Fact]
    public void StorageItemEnvelopeMayCarryLocationStorageSourceAndDurabilityDefinition()
    {
        var item = MortalItemTestFixture.CreateRawRoot("storage_placement", "location_storage",
            "locref_new_court:storage_materials");
        item["resourceMaterialization"] = new JsonObject
        {
            ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = "durability", ["maximum"] = 100 })
        };
        using var raw = JsonDocument.Parse(item.ToJsonString());
        var issues = MortalItemMaterializationContract.Validate(raw.RootElement,
            "currentLocationData.locationStorages[0].contents[0]", MortalItemMaterializationPhase.RawPreSeal);
        Report("item envelope only; source/destination admission not exercised", issues);
        Assert.Empty(issues);
    }

    [Fact]
    public async Task NpcInNewSceneRequiresThatSceneInitialAnchor()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-raw-scene-diagnostic-" + Guid.NewGuid().ToString("N"));
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            files.EnsureDirectoryStructure();
            await files.WriteFileAtomicAsync("game_state/world/current_location.json",
                "{\"currentLocationData\":{\"locationId\":null,\"initialId\":\"locref_new_court\"}}");
            var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
            var npc = new JsonObject
            {
                ["NPCId"] = "npc_existing_observer", ["name"] = "Observer",
                ["currentLocationId"] = "loc_old_room", ["initialLocationId"] = null
            };
            var response = new JsonObject { ["NPCsInScene"] = new JsonArray(npc) };
            using var raw = JsonDocument.Parse(response.ToJsonString());
            // Deliberately field-scoped: minimal NPC DATA is not a complete actor declaration.
            var all = validator.ValidateResponse(raw.RootElement);
            var anchor = Assert.Single(all.Where(i => i.Code == "npc_scene_missing_initial_location_id"));
            Report("only the concrete NPC anchor issue", new[] { anchor });
            Assert.False(MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(new[] { anchor }));
            Assert.False(MortalItemRepairPacketBuilder.RequiresFailClosedRollback(new[] { anchor }));
            npc["currentLocationId"] = null;
            npc["initialLocationId"] = "locref_new_court";
            using var corrected = JsonDocument.Parse(response.ToJsonString());
            Assert.DoesNotContain(validator.ValidateResponse(corrected.RootElement),
                i => i.Code == "npc_scene_missing_initial_location_id" || i.Code == "npc_scene_initial_location_mismatch");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    }

    private void Report(string scope, IReadOnlyList<ValidationIssue> issues) =>
        output.WriteLine(JsonSerializer.Serialize(new
        {
            scope,
            // An unbound contract issue may be unrepairable until the ordinary intake
            // attaches its original coordinate. This is not the live branch decision.
            LocationPolicyFailClosed = MortalLocationRepairPacketBuilder.RequiresFailClosedRollback(issues),
            ItemPolicyFailClosed = MortalItemRepairPacketBuilder.RequiresFailClosedRollback(issues),
            Issues = issues.Select(i => new { i.Code, i.FilePath, i.Message, i.Category, i.Actor, i.Expected, i.Actual })
        }));
}
