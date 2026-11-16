using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal sealed partial class MortalItemMaterializationTestContext
{
    internal const string OffscreenBirthCurrentId = "loc_offscreen_birth_a";
    internal const string OffscreenBirthTargetId = "loc_offscreen_birth_b";
    internal const string OffscreenBirthStorageId = "storage_offscreen_birth";
    internal const string OffscreenBirthRootRef = "new_offscreen_root";

    internal async Task ArrangeOffscreenStorageBirthAsync(bool measured = false, bool access = true,
        int capacity = 1, double volume = 1)
    {
        await BuildMortalBootstrapAsync();
        var a = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(OffscreenBirthCurrentId, "Current scene", x: 0);
        var b = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(OffscreenBirthTargetId, "Remote storehouse", x: 1);
        var map = MortalLocationTestFixture.CreateWorldMap(a, b);
        var index = MortalLocationTestFixture.CreateIdentityIndex(a);
        index["locationEntries"]!.AsArray().Add(MortalLocationTestFixture.CreateIdentityIndex(b)["locationEntries"]![0]!.DeepClone());
        var accessState = MortalStorageAccessProjection.Build(map, null);
        if (!accessState.IsValid) throw new InvalidDataException("Offscreen birth fixture has invalid original access.");
        await WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        await WriteJsonAsync(StorageTransportMoveService.CurrentLocationPath, MortalLocationTestFixture.CreateCurrentProjection(a));
        await WriteJsonAsync(MortalLocationIdentityState.StatePath, index);
        await WriteJsonAsync(MortalStorageAccessProjection.StatePath, accessState.Root!);
        await CaptureValidatedPendingSnapshotAsync(42);

        JsonObject Item(string reference, double physicalVolume)
        {
            var item = MortalItemTestFixture.CreateRawRoot("storage_placement", "location_storage",
                OffscreenBirthTargetId + ":" + OffscreenBirthStorageId,
                creationRef: reference, materializationId: "mat_" + reference);
            item["volume"] = physicalVolume;
            return item;
        }
        var root = Item(OffscreenBirthRootRef, measured ? 0.5d : 1d);
        var contents = new JsonArray(root);
        var storage = MortalLocationTestFixture.CreateStorageMetadata(OffscreenBirthStorageId,
            "Offscreen chest", access, contents);
        storage["capacity"] = capacity; storage["volume"] = volume;
        if (measured)
        {
            root["isContainer"] = true; root["capacity"] = 4;
            root["materialization"]!["sections"]!["container"] = new JsonObject { ["state"] = "populated", ["reason"] = null };
            var child = Item("new_offscreen_child", 0.2d);
            child["isContainer"] = true; child["capacity"] = 4;
            child["contentsPath"] = new JsonArray(OffscreenBirthRootRef);
            child["materialization"]!["sections"]!["container"] = new JsonObject { ["state"] = "populated", ["reason"] = null };
            var leaf = Item("new_offscreen_leaf", 0.1d);
            leaf["count"] = 3;
            leaf["contentsPath"] = new JsonArray(OffscreenBirthRootRef, "new_offscreen_child");
            contents.Add(child); contents.Add(leaf);
            JsonObject Shape(JsonObject item)
            {
                var shape = item.DeepClone().AsObject();
                foreach (var field in shape.Select(pair => pair.Key).Where(MortalCargoBirthCapacity.IsIdentityShapeField).ToArray()) shape.Remove(field);
                return shape;
            }
            var basis = new JsonObject { ["schemaVersion"] = 1, ["item"] = Shape(root), ["contents"] = new JsonArray(
                new JsonObject { ["creationRef"] = "new_offscreen_child", ["count"] = 1, ["item"] = Shape(child), ["contents"] = new JsonArray(
                    new JsonObject { ["creationRef"] = "new_offscreen_leaf", ["count"] = 3, ["item"] = Shape(leaf), ["contents"] = new JsonArray() }) }) };
            storage["cargoMeasurements"] = new JsonArray(new JsonObject { ["creationRef"] = OffscreenBirthRootRef,
                ["count"] = 1, ["externalVolumeDm3"] = 1d, ["measuredConfiguration"] = basis });
        }
        // CreateStorageMetadata deliberately detached its original contents argument.
        storage["contents"] = contents.DeepClone();
        foreach (var item in contents.OfType<JsonObject>())
        {
            var issues = MortalItemMaterializationContract.Validate(JsonSerializer.SerializeToElement(item),
                "fixture.storage.contents", MortalItemMaterializationPhase.RawPreSeal);
            if (issues.Count != 0) throw new InvalidDataException("Invalid independent offscreen item fixture: " +
                string.Join(";", issues.Select(issue => issue.Code + ":" + issue.FilePath)));
        }
        await WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, new JsonObject
        {
            ["worldMapUpdates"] = new JsonObject { ["storagesToAdd"] = new JsonArray(new JsonObject
                { ["targetLocationId"] = OffscreenBirthTargetId, ["storage"] = storage }) }
        });
    }
}
