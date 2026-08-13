using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLocationPlayerProjectionTests
{
    [Fact]
    public void Create_OmitsHiddenAndRejectedLocationsAndRequiresExactCurrentProjection()
    {
        var visited = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_visited_ford",
            "Чёрный брод",
            "visited",
            x: 10,
            y: 20);
        var rumored = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_rumored_tower",
            "Башня в тумане",
            "rumored",
            x: 11,
            y: 20);
        var hidden = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_hidden_vault",
            "Скрытое хранилище",
            "hidden",
            x: 12,
            y: 20);
        var rejected = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_rejected_ruin",
            "Ложные руины",
            "visited",
            x: 13,
            y: 20);
        rejected.Remove("materializationReceipt");

        var projection = MortalLocationPlayerProjection.Create(
            CreateWorldMap([visited, rumored, hidden, rejected]),
            MortalLocationTestFixture.CreateCurrentProjection(visited),
            CreateIdentityIndex([visited, rumored, hidden]));

        Assert.Equal("loc_visited_ford", projection.CurrentLocationId);
        Assert.Equal(2, projection.Locations.Count);
        Assert.DoesNotContain(projection.Locations, static location =>
            location.Identity is "loc_hidden_vault" or "loc_rejected_ruin");

        var visitedProjection = Assert.Single(
            projection.Locations,
            static location => location.Identity == "loc_visited_ford");
        Assert.True(visitedProjection.IsCurrent);
        Assert.Equal("loc_visited_ford", visitedProjection.DetailSelector);
        Assert.Equal("Холодная река пересекает старый тракт между двумя каменистыми берегами.",
            visitedProjection.Data["description"]!.GetValue<string>());
        Assert.Null(visitedProjection.Data["locationId"]);
        Assert.Null(visitedProjection.Data["materialization"]);
        Assert.Null(visitedProjection.Data["materializationReceipt"]);

        var mismatchedCurrent = MortalLocationTestFixture.CreateCurrentProjection(visited);
        mismatchedCurrent["description"] = "Подменённое описание текущего места.";

        var mismatchProjection = MortalLocationPlayerProjection.Create(
            CreateWorldMap([visited, rumored, hidden]),
            mismatchedCurrent,
            CreateIdentityIndex([visited, rumored, hidden]));

        Assert.Null(mismatchProjection.CurrentLocationId);
        Assert.DoesNotContain(mismatchProjection.Locations, static location => location.IsCurrent);
    }

    [Fact]
    public void Create_ProjectsRumorSummaryWithoutExactPlacementOrActions()
    {
        var rumored = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_rumored_ford",
            "Брод из дорожных слухов",
            "rumored",
            x: 73,
            y: -41,
            z: 2);

        var projection = MortalLocationPlayerProjection.Create(
            CreateWorldMap([rumored]),
            currentLocation: null,
            CreateIdentityIndex([rumored]));

        var location = Assert.Single(projection.Locations);
        Assert.Equal("rumored", location.DiscoveryTier);
        Assert.Equal("Брод из дорожных слухов", location.Label);
        Assert.Equal(
            "На старом тракте рассказывают о холодной переправе.",
            location.RumorSummary);
        Assert.Null(location.DetailSelector);
        Assert.Null(location.Data["description"]);
        Assert.Null(location.Data["coordinates"]);
        Assert.Null(location.Data["region"]);
        Assert.Null(location.Data["features"]);
        Assert.Null(location.Data["locationId"]);
    }

    [Fact]
    public void Create_RecursivelySuppressesInternalDtosAndPreservesAdjacentWorldSemantics()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_semantic_ford",
            "Брод знамений",
            "visited",
            x: 30,
            y: 7);
        location["features"] = new JsonArray(
            "чёрные камни",
            new JsonObject
            {
                ["kind"] = "weather_omen",
                ["title"] = "Знак над водой",
                ["route"] = "через камыши",
                ["steps"] = new JsonArray("дождаться сумерек")
            },
            new JsonObject
            {
                ["metadataDiagnosticOnly"] = false,
                ["revalidationAttempt"] = 2,
                ["gmInstructions"] = "Исправить validation contract",
                ["summaryGroups"] = new JsonArray("mortal_location_materialization_invalid"),
                ["harnessRepairPackets"] = new JsonArray(),
                ["errors"] = new JsonArray(new JsonObject
                {
                    ["code"] = "mortal_location_materialization_invalid",
                    ["actor"] = "loc_semantic_ford",
                    ["message"] = "game_state/world/world_map.json"
                })
            },
            MortalLocationStorageContentsState.BuildCanonicalRoot(
                new Dictionary<MortalLocationStorageKey, JsonArray>
                {
                    [new MortalLocationStorageKey(
                        "loc_private_offscreen",
                        "storage_private_offscreen")] = new JsonArray(
                        CreatePrivateOffscreenItem())
                }));
        location["settingSpecific"] = new JsonObject
        {
            ["legend"] = "Вода запоминает имена путников.",
            ["route"] = "по следам белой цапли",
            ["actorId"] = "npc_internal_keeper",
            ["filePath"] = "game_state/npcs/npc_core.json",
            ["copiedEnvelope"] = location["materialization"]!.DeepClone()
        };

        var projection = MortalLocationPlayerProjection.Create(
            CreateWorldMap([location]),
            MortalLocationTestFixture.CreateCurrentProjection(location),
            CreateIdentityIndex([location]));

        var data = Assert.Single(projection.Locations).Data;
        var feature = Assert.Single(
            data["features"]!.AsArray().OfType<JsonObject>(),
            static item => item["kind"]?.GetValue<string>() == "weather_omen");
        Assert.Equal("Знак над водой", feature["title"]!.GetValue<string>());
        Assert.Equal("через камыши", feature["route"]!.GetValue<string>());
        var settingSpecific = data["settingSpecific"]!.AsObject();
        Assert.Equal(
            "Вода запоминает имена путников.",
            settingSpecific["legend"]!.GetValue<string>());
        Assert.Equal("по следам белой цапли", settingSpecific["route"]!.GetValue<string>());
        var json = data.ToJsonString();
        Assert.DoesNotContain("gmInstructions", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mortal_location_materialization_invalid", json, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npc_internal_keeper", json, StringComparison.Ordinal);
        Assert.DoesNotContain("materializationId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("receiptId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PRIVATE OFFSCREEN STORAGE ITEM", json, StringComparison.Ordinal);
        Assert.DoesNotContain("storage_private_offscreen", json, StringComparison.Ordinal);
    }

    private static JsonObject CreatePrivateOffscreenItem()
    {
        var item = MortalItemTestFixture.CreateCanonicalRoot(
            "itm_private_location_offscreen_projection");
        item["name"] = "PRIVATE OFFSCREEN STORAGE ITEM";
        MortalItemTestFixture.ResealCanonical(item);
        return item;
    }

    [Fact]
    public void Create_BindsDuplicateLabelsAndNumericIdentityByExactOrdinalSelectors()
    {
        var source = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "2",
            "Две одинаковые арки",
            "visited",
            x: 1,
            y: 1);
        var target = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_second_arch",
            "Две одинаковые арки",
            "discovered",
            x: 2,
            y: 1);
        var link = MortalLocationTestFixture.CreateCanonicalLink("2", "loc_second_arch");

        var projection = MortalLocationPlayerProjection.Create(
            CreateWorldMap([source, target], [link]),
            MortalLocationTestFixture.CreateCurrentProjection(source),
            CreateIdentityIndex([source, target], [link]));

        Assert.True(projection.TryGetLocation("2", out var numeric));
        Assert.Equal("2", numeric!.DetailSelector);
        Assert.True(projection.TryGetLocation("loc_second_arch", out var second));
        Assert.Equal("loc_second_arch", second!.DetailSelector);
        Assert.False(projection.TryGetLocation("LOC_SECOND_ARCH", out _));

        var visibleLink = Assert.Single(projection.Links);
        Assert.Equal(MortalLocationTestFixture.LinkId, visibleLink.LinkSelector);
        Assert.Equal("2", visibleLink.SourceIdentity);
        Assert.Equal("loc_second_arch", visibleLink.TargetIdentity);
        Assert.Equal("loc_second_arch", visibleLink.TravelTargetSelector);

        var rendered = string.Join('\n', projection.Locations.Select(static item => item.Data.ToJsonString())) +
                       visibleLink.Data.ToJsonString();
        Assert.DoesNotContain("loc_second_arch", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(MortalLocationTestFixture.LinkId, rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_OmitsRumoredOrReceiptlessLinksAndNeverInventsReverseTopology()
    {
        var source = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_link_source",
            "Старая площадь",
            "visited",
            x: 5,
            y: 5);
        var target = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_link_target",
            "Северная башня",
            "discovered",
            x: 6,
            y: 5);
        var link = MortalLocationTestFixture.CreateCanonicalLink("loc_link_source", "loc_link_target");
        link["discovery"] = new JsonObject
        {
            ["tier"] = "rumored",
            ["audience"] = "player_known",
            ["rumorSummary"] = "Говорят о дороге к башне."
        };

        var rumoredProjection = MortalLocationPlayerProjection.Create(
            CreateWorldMap([source, target], [link]),
            MortalLocationTestFixture.CreateCurrentProjection(source),
            CreateIdentityIndex([source, target], [link]));
        Assert.Empty(rumoredProjection.Links);

        link["discovery"] = new JsonObject
        {
            ["tier"] = "discovered",
            ["audience"] = "player_known",
            ["rumorSummary"] = null
        };
        link.Remove("materializationReceipt");

        var rejectedProjection = MortalLocationPlayerProjection.Create(
            CreateWorldMap([source, target], [link]),
            MortalLocationTestFixture.CreateCurrentProjection(source),
            CreateIdentityIndex([source, target]));
        Assert.Empty(rejectedProjection.Links);
    }

    [Fact]
    public void Create_CurrentStorageProjectsAcceptedItemsOnlyWithoutBreakingMapReconciliation()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            "loc_storage_projection",
            "Двор с путевым ларём",
            "visited",
            x: 21,
            y: 6);
        location["locationStorages"] = new JsonArray(new JsonObject
        {
            ["storageId"] = "storage_projection_chest",
            ["name"] = "Путевой ларь",
            ["description"] = "Ларь под навесом.",
            ["hasFullAccess"] = true
        });
        location["materialization"]!["sections"]!["storageMetadata"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        MortalLocationTestFixture.ResealCanonicalLocation(location);

        var acceptedItem = MortalItemTestFixture.CreateCanonicalRoot("itm_location_projection_accepted");
        acceptedItem["name"] = "Принятый путевой журнал";
        MortalItemTestFixture.ResealCanonical(acceptedItem);
        var rejectedItem = MortalItemTestFixture.CreateRawRoot(
            creationRef: "new_item_location_projection_rejected",
            materializationId: "mat_item_location_projection_rejected");
        rejectedItem["name"] = "НЕ ПОКАЗЫВАТЬ СЫРОЙ ПРЕДМЕТ";

        var current = MortalLocationTestFixture.CreateCurrentProjection(location);
        current["locationStorages"]![0]!["contents"] = new JsonArray(acceptedItem, rejectedItem);

        var projection = MortalLocationPlayerProjection.Create(
            CreateWorldMap([location]),
            current,
            CreateIdentityIndex([location]));

        Assert.Equal("loc_storage_projection", projection.CurrentLocationId);
        var storage = Assert.Single(
            Assert.Single(projection.Locations).Data["locationStorages"]!.AsArray().OfType<JsonObject>());
        var item = Assert.Single(storage["contents"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("Принятый путевой журнал", item["name"]!.GetValue<string>());
        Assert.Null(item["itemId"]);
        Assert.DoesNotContain("НЕ ПОКАЗЫВАТЬ", storage.ToJsonString(), StringComparison.Ordinal);
    }

    private static JsonObject CreateWorldMap(
        IReadOnlyCollection<JsonObject> locations,
        IReadOnlyCollection<JsonObject>? links = null)
    {
        var map = MortalLocationTestFixture.CreateWorldMap(locations.ToArray());
        map["links"] = new JsonArray(
            (links ?? Array.Empty<JsonObject>())
            .Select(static link => (JsonNode?)link.DeepClone())
            .ToArray());
        return map;
    }

    private static JsonObject CreateIdentityIndex(
        IReadOnlyCollection<JsonObject> locations,
        IReadOnlyCollection<JsonObject>? links = null)
    {
        var index = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locationEntries"] = new JsonArray(),
            ["linkEntries"] = new JsonArray()
        };
        var locationEntries = index["locationEntries"]!.AsArray();
        foreach (var location in locations)
        {
            var single = MortalLocationTestFixture.CreateIdentityIndex(location);
            locationEntries.Add(single["locationEntries"]![0]!.DeepClone());
        }

        var linkEntries = index["linkEntries"]!.AsArray();
        foreach (var link in links ?? Array.Empty<JsonObject>())
        {
            var sourceId = link["sourceLocationId"]!.GetValue<string>();
            var source = locations.Single(location =>
                location["locationId"]!.GetValue<string>() == sourceId);
            var single = MortalLocationTestFixture.CreateIdentityIndex(source, link);
            linkEntries.Add(single["linkEntries"]![0]!.DeepClone());
        }

        return index;
    }
}
