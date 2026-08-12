using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class CanonicalStateNormalizerTests
{
    [Fact]
    public void MortalLocations_CanonicalPathsAreRegisteredBeforeMortalItems()
    {
        var paths = new[]
        {
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationIdentityState.StatePath
        };

        foreach (var path in paths)
        {
            Assert.Contains(path, CanonicalStateNormalizer.CanonicalAccumulatedFiles);
            Assert.Contains(path, CanonicalStateNormalizer.NormalizerBackupInputFiles);
            Assert.Contains(path, CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        }

        Assert.True(
            Array.IndexOf(
                CanonicalStateNormalizer.CanonicalAccumulatedFiles,
                MortalLocationMaterializationContract.WorldMapPath) <
            Array.IndexOf(
                CanonicalStateNormalizer.CanonicalAccumulatedFiles,
                MortalItemIdentityState.StatePath));
    }

    [Fact]
    public async Task MortalLocations_CurrentCreationWritesCanonicalMapProjectionReceiptAndIndex()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var emptyMap = EmptyMortalWorldMap();
        var emptyIndex = MortalLocationIdentityState.CreateEmptyRoot();
        var backups = await WriteMortalLocationBaselineAsync(context, emptyMap, null, emptyIndex);
        await WriteAcceptedTurnAsync(context);
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, emptyMap);
        await context.WriteJsonAsync(MortalLocationIdentityState.StatePath, emptyIndex);

        var raw = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        var rawContents = new JsonArray(
            new JsonObject
            {
                ["creationRef"] = "itemref_storage_marker",
                ["marker"] = "RAW_STORAGE_CONTENT"
            });
        raw["locationStorages"] = new JsonArray(
            new JsonObject
            {
                ["storageId"] = "storage_black_ford_chest",
                ["name"] = "Сундук у брода",
                ["description"] = "Закрытый дорожный сундук.",
                ["ownerActorId"] = null,
                ["capacity"] = new JsonObject { ["slots"] = 8 },
                ["access"] = new JsonObject
                {
                    ["state"] = "open",
                    ["reason"] = null
                },
                ["contents"] = rawContents.DeepClone()
            });
        raw["materialization"]!["sections"]!["storageMetadata"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        raw["currentWeather"] = new JsonObject
        {
            ["summary"] = "Холодная морось",
            ["visibility"] = "normal"
        };
        raw["currentInteractions"] = new JsonArray("осмотреть чёрные камни");
        raw["currentChronology"] = new JsonArray("герой впервые вышел к переправе");
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = raw });

        await context.Normalizer.NormalizeMortalLocationsAsync(backups);

        var map = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        Assert.Equal(4, map.Count);
        Assert.False(map.ContainsKey("worldMapUpdates"));
        var canonical = Assert.Single(map["locations"]!.AsArray().OfType<JsonObject>());
        Assert.StartsWith("loc_", canonical["locationId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(canonical.ContainsKey("initialId"));
        Assert.False(canonical.ContainsKey("parentInitialId"));
        Assert.False(canonical.ContainsKey("currentWeather"));
        Assert.False(canonical.ContainsKey("currentInteractions"));
        Assert.False(canonical.ContainsKey("currentChronology"));
        var mapStorage = Assert.Single(canonical["locationStorages"]!.AsArray().OfType<JsonObject>());
        Assert.False(mapStorage.ContainsKey("contents"));

        var receipt = canonical["materializationReceipt"]!.AsObject();
        Assert.StartsWith("mlocrec_", receipt["receiptId"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal(canonical["locationId"]!.GetValue<string>(), receipt["locationId"]!.GetValue<string>());
        Assert.Equal(
            MortalLocationMaterializationContract.ComputeSeal(
                canonical["materialization"]!.AsObject(),
                receipt),
            receipt["seal"]!.GetValue<string>());

        var current = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath))!.AsObject();
        Assert.False(current.ContainsKey("currentLocationData"));
        Assert.Equal(canonical["locationId"]!.GetValue<string>(), current["locationId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(raw["currentWeather"], current["currentWeather"]));
        Assert.True(JsonNode.DeepEquals(raw["currentInteractions"], current["currentInteractions"]));
        Assert.True(JsonNode.DeepEquals(raw["currentChronology"], current["currentChronology"]));
        var currentStorage = Assert.Single(current["locationStorages"]!.AsArray().OfType<JsonObject>());
        Assert.True(JsonNode.DeepEquals(rawContents, currentStorage["contents"]));

        var index = (await context.ReadJsonAsync(MortalLocationIdentityState.StatePath))!.AsObject();
        var entry = Assert.Single(index["locationEntries"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(canonical["locationId"]!.GetValue<string>(), entry["locationId"]!.GetValue<string>());
        Assert.Equal(receipt["receiptId"]!.GetValue<string>(), entry["receiptId"]!.GetValue<string>());
        Assert.Equal(MortalLocationTestFixture.LocationInitialId, entry["initialId"]!.GetValue<string>());
        Assert.Empty(index["linkEntries"]!.AsArray());
    }

    [Fact]
    public async Task MortalLocations_RemoteCreationsAndLinkRewriteExactTemporaryEndpoints()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var emptyMap = EmptyMortalWorldMap();
        var emptyIndex = MortalLocationIdentityState.CreateEmptyRoot();
        var backups = await WriteMortalLocationBaselineAsync(context, emptyMap, null, emptyIndex);
        await WriteAcceptedTurnAsync(context);
        await context.WriteJsonAsync(MortalLocationIdentityState.StatePath, emptyIndex);

        var source = CreateRemoteLocation(
            "locref_remote_source",
            "mlocmat_remote_source",
            coordinateX: 31);
        var target = CreateRemoteLocation(
            "locref_remote_target",
            "mlocmat_remote_target",
            coordinateX: 32);
        MarkTopologyPopulated(source);
        MarkTopologyPopulated(target);
        var link = MortalLocationTestFixture.CreateRawLink("unused_source", "unused_target");
        link["sourceLocationId"] = null;
        link["sourceInitialId"] = "locref_remote_source";
        link["targetLocationId"] = null;
        link["targetInitialId"] = "locref_remote_target";
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["worldMapUpdates"] = new JsonObject
                {
                    ["newLocations"] = new JsonArray(source, target),
                    ["newLinks"] = new JsonArray(link)
                }
            });

        await context.Normalizer.NormalizeMortalLocationsAsync(backups);

        var map = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        Assert.False(map.ContainsKey("worldMapUpdates"));
        var locations = map["locations"]!.AsArray().OfType<JsonObject>().ToArray();
        Assert.Equal(2, locations.Length);
        var byInitialId = locations.ToDictionary(
            location => location["materializationReceipt"]!["initialId"]!.GetValue<string>(),
            location => location["locationId"]!.GetValue<string>(),
            StringComparer.Ordinal);
        var canonicalLink = Assert.Single(map["links"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(byInitialId["locref_remote_source"], canonicalLink["sourceLocationId"]!.GetValue<string>());
        Assert.Equal(byInitialId["locref_remote_target"], canonicalLink["targetLocationId"]!.GetValue<string>());
        Assert.False(canonicalLink.ContainsKey("sourceInitialId"));
        Assert.False(canonicalLink.ContainsKey("targetInitialId"));

        var index = (await context.ReadJsonAsync(MortalLocationIdentityState.StatePath))!.AsObject();
        Assert.Equal(2, index["locationEntries"]!.AsArray().Count);
        var linkEntry = Assert.Single(index["linkEntries"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(canonicalLink["linkId"]!.GetValue<string>(), linkEntry["linkId"]!.GetValue<string>());
        Assert.Equal(canonicalLink["sourceLocationId"]!.GetValue<string>(), linkEntry["sourceLocationId"]!.GetValue<string>());
        Assert.Equal(canonicalLink["targetLocationId"]!.GetValue<string>(), linkEntry["targetLocationId"]!.GetValue<string>());
        Assert.Null(await context.ReadJsonAsync(MortalLocationMaterializationContract.CurrentLocationPath));
    }

    [Fact]
    public async Task MortalLocations_InvalidPlanLeavesEveryLocationSurfaceByteExact()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var emptyMap = EmptyMortalWorldMap();
        var emptyIndex = MortalLocationIdentityState.CreateEmptyRoot();
        var backups = await WriteMortalLocationBaselineAsync(context, emptyMap, null, emptyIndex);
        await WriteAcceptedTurnAsync(context);
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, emptyMap);
        await context.WriteJsonAsync(MortalLocationIdentityState.StatePath, emptyIndex);
        var invalid = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        invalid.Remove("customStates");
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = invalid });
        var before = await context.CaptureBytesAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationIdentityState.StatePath);

        var error = await Assert.ThrowsAsync<InvalidDataException>(
            () => context.Normalizer.NormalizeMortalLocationsAsync(backups));

        Assert.Contains("mortal_location_materialization_governed_field_missing", error.Message, StringComparison.Ordinal);
        var after = await context.CaptureBytesAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationIdentityState.StatePath);
        MortalLocationMaterializationAssertions.AssertExactBytes(before, after);
    }

    private static async Task<IReadOnlyDictionary<string, string>> WriteMortalLocationBaselineAsync(
        MortalLocationMaterializationTestContext context,
        JsonObject worldMap,
        JsonObject? currentLocation,
        JsonObject identityIndex)
    {
        var backups = new Dictionary<string, string>(StringComparer.Ordinal);
        await WriteBaselineAsync(
            context,
            MortalLocationMaterializationContract.WorldMapPath,
            "test_backups/mortal_locations/world_map.json",
            worldMap,
            backups);
        await WriteBaselineAsync(
            context,
            MortalLocationIdentityState.StatePath,
            "test_backups/mortal_locations/location_identity_index.json",
            identityIndex,
            backups);
        if (currentLocation != null)
        {
            await WriteBaselineAsync(
                context,
                MortalLocationMaterializationContract.CurrentLocationPath,
                "test_backups/mortal_locations/current_location.json",
                currentLocation,
                backups);
        }
        return backups;
    }

    private static async Task WriteBaselineAsync(
        MortalLocationMaterializationTestContext context,
        string canonicalPath,
        string backupPath,
        JsonObject value,
        IDictionary<string, string> backups)
    {
        await context.WriteJsonAsync(backupPath, value);
        backups.Add(canonicalPath, backupPath);
    }

    private static Task WriteAcceptedTurnAsync(
        MortalLocationMaterializationTestContext context) =>
        context.WriteJsonAsync(
            "input/turn_request.json",
            new JsonObject
            {
                ["sessionId"] = "session_mortal_location_normalizer",
                ["requestId"] = "request_mortal_location_normalizer",
                ["turnNumber"] = 42,
                ["playerAction"] = "Материализовать смертную локацию."
            });

    private static JsonObject EmptyMortalWorldMap() =>
        new()
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };

    private static JsonObject CreateRemoteLocation(
        string initialId,
        string materializationId,
        int coordinateX)
    {
        var location = MortalLocationTestFixture.CreateRawLocation("world_map_creation");
        location["initialId"] = initialId;
        location["name"] = "Локация " + initialId;
        location["displayName"] = "Локация " + initialId;
        location["coordinates"]!["x"] = coordinateX;
        location["materialization"]!["initialId"] = initialId;
        location["materialization"]!["materializationId"] = materializationId;
        return location;
    }

    private static void MarkTopologyPopulated(JsonObject location)
    {
        location["materialization"]!["sections"]!["topology"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
    }
}
