using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLocationMaterializationValidationTests
{
    [Fact]
    public void Lifecycle_ExactMovementRebuildsVisitedCurrentProjectionAndPreservesAuthority()
    {
        var accepted = CreateAcceptedDirectedTopology(targetDiscovery: "discovered");
        var targetBefore = FindLocation(accepted.FinalWorldMap, accepted.TargetLocationId);
        var envelopeBefore = targetBefore["materialization"]!.DeepClone();
        var receiptBefore = targetBefore["materializationReceipt"]!.DeepClone();
        var move = new JsonObject
        {
            ["locationId"] = accepted.TargetLocationId,
            ["lastEventsDescription"] = "Герой вошёл в башню после заката.",
            ["currentWeather"] = new JsonObject { ["summary"] = "Сильный ветер" },
            ["currentInteractions"] = new JsonArray()
        };

        var result = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawCurrentLocationData: move,
            turn: 43);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        Assert.Equal(accepted.TargetLocationId, plan.FinalCurrentLocation!["locationId"]!.GetValue<string>());
        Assert.Equal("visited", plan.FinalCurrentLocation["discovery"]!["tier"]!.GetValue<string>());
        var targetAfter = FindLocation(plan.FinalWorldMap, accepted.TargetLocationId);
        Assert.Equal("visited", targetAfter["discovery"]!["tier"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(envelopeBefore, targetAfter["materialization"]));
        Assert.True(JsonNode.DeepEquals(receiptBefore, targetAfter["materializationReceipt"]));
        Assert.DoesNotContain(plan.FinalWorldMap["links"]!.AsArray().OfType<JsonObject>(), link =>
            link["sourceLocationId"]!.GetValue<string>() == accepted.TargetLocationId &&
            link["targetLocationId"]!.GetValue<string>() == accepted.SourceLocationId);
        Assert.NotEmpty(FindLocationIndexEntry(plan.FinalIdentityIndex, accepted.TargetLocationId)["transitions"]!.AsArray());
    }

    [Fact]
    public void Lifecycle_CaseVariantMovementIdentityFailsClosed()
    {
        var accepted = CreateAcceptedDirectedTopology();

        var result = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawCurrentLocationData: new JsonObject
            {
                ["locationId"] = accepted.TargetLocationId.ToUpperInvariant(),
                ["lastEventsDescription"] = "Ложный переход по регистровому псевдониму.",
                ["currentWeather"] = new JsonObject(),
                ["currentInteractions"] = new JsonArray()
            },
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_materialization_existing_target_unresolved");
    }

    [Theory]
    [InlineData("no_link")]
    [InlineData("reverse_only")]
    [InlineData("sealed")]
    [InlineData("conditional")]
    [InlineData("hidden_link")]
    [InlineData("hidden_target")]
    public void Lifecycle_MovementRequiresOneVisibleOpenOutgoingLink(string scenario)
    {
        var accepted = CreateAcceptedDirectedTopology(
            targetDiscovery: scenario == "hidden_target" ? "hidden" : "discovered",
            linkDiscovery: scenario == "hidden_link" ? "hidden" : "discovered",
            includeLink: scenario != "no_link",
            reverseLink: scenario == "reverse_only",
            accessState: scenario is "sealed" or "conditional" ? scenario : "open");

        var result = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawCurrentLocationData: new JsonObject
            {
                ["locationId"] = accepted.TargetLocationId,
                ["lastEventsDescription"] = "Попытка пройти без подтверждённого маршрута.",
                ["currentWeather"] = new JsonObject(),
                ["currentInteractions"] = new JsonArray()
            },
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_movement_not_authorized");
    }

    [Fact]
    public void Lifecycle_SameLocationOperationalRefreshDoesNotRequireTraversalLink()
    {
        var accepted = CreateAcceptedDirectedTopology(includeLink: false);

        var result = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawCurrentLocationData: new JsonObject
            {
                ["locationId"] = accepted.SourceLocationId,
                ["lastEventsDescription"] = "Герой остаётся у старого брода.",
                ["currentWeather"] = new JsonObject { ["summary"] = "Морось" },
                ["currentInteractions"] = new JsonArray()
            },
            turn: 43);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        Assert.Equal(accepted.SourceLocationId, plan.FinalCurrentLocation!["locationId"]!.GetValue<string>());
        Assert.Equal("Герой остаётся у старого брода.", plan.FinalCurrentLocation["lastEventsDescription"]!.GetValue<string>());
    }

    [Fact]
    public void Lifecycle_StorageItems_SameLocationMinimalRefreshPreservesAcceptedCurrentStorageItems()
    {
        var accepted = CreateGovernedCommandBaseline();
        var expectedItem = FindStorage(accepted.CurrentLocation, "storage_update")
            ["contents"]![0]!.DeepClone();
        foreach (var storage in FindLocation(accepted.WorldMap, accepted.LocationId)
                     ["locationStorages"]!
                     .AsArray()
                     .OfType<JsonObject>())
        {
            storage.Remove("contents");
        }

        var result = Build(
            accepted.WorldMap,
            accepted.CurrentLocation,
            accepted.IdentityIndex,
            rawCurrentLocationData: new JsonObject
            {
                ["locationId"] = accepted.LocationId,
                ["lastEventsDescription"] = "Герой остаётся на площади команд.",
                ["currentWeather"] = new JsonObject { ["summary"] = "Тихий дождь" },
                ["currentInteractions"] = new JsonArray()
            },
            turn: 43);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        var contents = Assert.IsType<JsonArray>(
            FindStorage(plan.FinalCurrentLocation!, "storage_update")["contents"]);
        Assert.Single(contents);
        Assert.True(JsonNode.DeepEquals(expectedItem, contents[0]));
        Assert.DoesNotContain(
            FindLocation(plan.FinalWorldMap, accepted.LocationId)["locationStorages"]!
                .AsArray()
                .OfType<JsonObject>(),
            storage => storage.ContainsKey("contents"));
    }

    [Fact]
    public void Lifecycle_StorageItems_ChangedLocationAtomicallyParksSourceAndHydratesTargetStorageItems()
    {
        var accepted = CreateAcceptedDirectedTopology();
        var sourceLocation = FindLocation(accepted.FinalWorldMap, accepted.SourceLocationId);
        var targetLocation = FindLocation(accepted.FinalWorldMap, accepted.TargetLocationId);
        var sourceStorage = CreateStorage("storage_source", "Сундук у брода");
        var targetStorage = CreateStorage("storage_target", "Сундук в башне");
        sourceStorage.Remove("contents");
        targetStorage.Remove("contents");
        sourceLocation["locationStorages"] = new JsonArray(sourceStorage);
        targetLocation["locationStorages"] = new JsonArray(targetStorage);
        SetSectionPopulated(sourceLocation, "storageMetadata");
        SetSectionPopulated(targetLocation, "storageMetadata");
        MortalLocationTestFixture.ResealCanonicalLocation(sourceLocation);
        MortalLocationTestFixture.ResealCanonicalLocation(targetLocation);

        var sourceItem = MortalItemTestFixture.CreateCanonicalRoot("itm_source_storage");
        var targetItem = MortalItemTestFixture.CreateCanonicalRoot("itm_target_storage");
        var preTurnCurrent = MortalLocationTestFixture.CreateCurrentProjection(sourceLocation);
        FindStorage(preTurnCurrent, "storage_source")["contents"] =
            new JsonArray(sourceItem.DeepClone());
        var preTurnOffscreen = MortalLocationStorageContentsState.BuildCanonicalRoot(
            new Dictionary<MortalLocationStorageKey, JsonArray>
            {
                [new MortalLocationStorageKey(
                    accepted.TargetLocationId,
                    "storage_target")] = new JsonArray(targetItem.DeepClone())
            });

        var result = Build(
            accepted.FinalWorldMap,
            preTurnCurrent,
            accepted.FinalIdentityIndex,
            rawCurrentLocationData: new JsonObject
            {
                ["locationId"] = accepted.TargetLocationId,
                ["lastEventsDescription"] = "Герой вошёл в башню после заката.",
                ["currentWeather"] = new JsonObject { ["summary"] = "Сильный ветер" },
                ["currentInteractions"] = new JsonArray()
            },
            preTurnStorageContents: preTurnOffscreen,
            turn: 43);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        var targetContents = Assert.IsType<JsonArray>(
            FindStorage(plan.FinalCurrentLocation!, "storage_target")["contents"]);
        Assert.Single(targetContents);
        Assert.True(JsonNode.DeepEquals(targetItem, targetContents[0]));

        var parkedSource = Assert.Single(
            plan.FinalStorageContents["entries"]!
                .AsArray()
                .OfType<JsonObject>());
        Assert.Equal(accepted.SourceLocationId, parkedSource["locationId"]!.GetValue<string>());
        Assert.Equal("storage_source", parkedSource["storageId"]!.GetValue<string>());
        Assert.Single(parkedSource["contents"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(sourceItem, parkedSource["contents"]![0]));
        Assert.DoesNotContain(
            plan.FinalStorageContents["entries"]!
                .AsArray()
                .OfType<JsonObject>(),
            entry => entry["locationId"]!.GetValue<string>() == accepted.TargetLocationId);
        Assert.DoesNotContain(
            plan.FinalWorldMap["locations"]!
                .AsArray()
                .OfType<JsonObject>()
                .SelectMany(location => location["locationStorages"]!
                    .AsArray()
                    .OfType<JsonObject>()),
            storage => storage.ContainsKey("contents"));
    }

    [Fact]
    public void Lifecycle_StorageItems_ExistingMovementPayloadCannotEchoClientOwnedContents()
    {
        var accepted = CreateAcceptedDirectedTopology();

        var result = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawCurrentLocationData: new JsonObject
            {
                ["locationId"] = accepted.TargetLocationId,
                ["lastEventsDescription"] = "GM пытается перенести предмет вместе с описанием перехода.",
                ["currentWeather"] = new JsonObject(),
                ["currentInteractions"] = new JsonArray(),
                ["locationStorages"] = new JsonArray(new JsonObject
                {
                    ["storageId"] = "storage_target",
                    ["contents"] = new JsonArray(
                        MortalItemTestFixture.CreateCanonicalRoot("itm_gm_echo_forbidden"))
                })
            },
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_movement_client_owned_item_contents_forbidden" &&
            issue.FilePath.EndsWith(
                "locationStorages[0].contents",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Lifecycle_NarrowLocationUpdatePreservesPlacementReceiptEnvelopeAndTopology()
    {
        var accepted = CreateAcceptedDirectedTopology();
        var before = FindLocation(accepted.FinalWorldMap, accepted.TargetLocationId);
        var updates = new JsonObject
        {
            ["locationUpdates"] = new JsonArray(new JsonObject
            {
                ["locationId"] = accepted.TargetLocationId,
                ["displayName"] = "Башня под грозой",
                ["description"] = "Молнии освещают каменные зубцы старой башни.",
                ["lastEventsDescription"] = "Над башней началась гроза."
            })
        };

        var result = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawWorldMapUpdates: updates,
            turn: 43);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        var after = FindLocation(plan.FinalWorldMap, accepted.TargetLocationId);
        Assert.Equal("Башня под грозой", after["displayName"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(before["coordinates"], after["coordinates"]));
        Assert.True(JsonNode.DeepEquals(before["parentLocationId"], after["parentLocationId"]));
        Assert.True(JsonNode.DeepEquals(before["materialization"], after["materialization"]));
        Assert.True(JsonNode.DeepEquals(before["materializationReceipt"], after["materializationReceipt"]));
        Assert.Single(plan.FinalWorldMap["links"]!.AsArray());
        Assert.NotEmpty(FindLocationIndexEntry(plan.FinalIdentityIndex, accepted.TargetLocationId)["transitions"]!.AsArray());
    }

    [Theory]
    [InlineData("coordinates")]
    [InlineData("parentLocationId")]
    [InlineData("realm")]
    [InlineData("materialization")]
    [InlineData("materializationReceipt")]
    [InlineData("knownExits")]
    [InlineData("adjacencyMap")]
    public void Lifecycle_LocationUpdateRejectsImmutableOrDerivedField(string field)
    {
        var accepted = CreateAcceptedDirectedTopology();
        var patch = new JsonObject
        {
            ["locationId"] = accepted.TargetLocationId,
            [field] = field switch
            {
                "coordinates" => new JsonObject { ["x"] = 99, ["y"] = 0, ["z"] = 0 },
                "parentLocationId" => accepted.SourceLocationId,
                "realm" => "mortal_world",
                "materialization" => new JsonObject(),
                "materializationReceipt" => new JsonObject(),
                _ => new JsonArray()
            }
        };

        var result = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawWorldMapUpdates: new JsonObject
            {
                ["locationUpdates"] = new JsonArray(patch)
            },
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_update_field_forbidden");
    }

    [Fact]
    public void Lifecycle_DiscoveryTransitionRequiresExactPrestateAndMovesForward()
    {
        var accepted = CreateAcceptedDirectedTopology(targetDiscovery: "hidden", linkDiscovery: "hidden");
        var targetBefore = FindLocation(accepted.FinalWorldMap, accepted.TargetLocationId);
        var updates = new JsonObject
        {
            ["locationDiscoveryTransitions"] = new JsonArray(new JsonObject
            {
                ["locationId"] = accepted.TargetLocationId,
                ["fromTier"] = "hidden",
                ["toTier"] = "rumored",
                ["toAudience"] = "player_known",
                ["rumorSummary"] = "За болотами видели огни старой башни.",
                ["reason"] = "Герой получил точный слух от картографа."
            })
        };

        var result = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawWorldMapUpdates: updates,
            turn: 43);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        var targetAfter = FindLocation(plan.FinalWorldMap, accepted.TargetLocationId);
        Assert.Equal("rumored", targetAfter["discovery"]!["tier"]!.GetValue<string>());
        Assert.Equal("За болотами видели огни старой башни.", targetAfter["discovery"]!["rumorSummary"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(targetBefore["materialization"], targetAfter["materialization"]));
        Assert.True(JsonNode.DeepEquals(targetBefore["materializationReceipt"], targetAfter["materializationReceipt"]));
    }

    [Theory]
    [InlineData("discovered", "hidden", "visited")]
    [InlineData("hidden", "rumored", "discovered")]
    public void Lifecycle_DiscoveryTransitionRejectsStaleOrBackwardEdge(
        string actualTier,
        string fromTier,
        string toTier)
    {
        var accepted = CreateAcceptedDirectedTopology(targetDiscovery: actualTier);
        var updates = new JsonObject
        {
            ["locationDiscoveryTransitions"] = new JsonArray(new JsonObject
            {
                ["locationId"] = accepted.TargetLocationId,
                ["fromTier"] = fromTier,
                ["toTier"] = toTier,
                ["toAudience"] = "player_known",
                ["rumorSummary"] = null,
                ["reason"] = "Недопустимый тестовый переход."
            })
        };

        var result = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawWorldMapUpdates: updates,
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code is "mortal_location_discovery_precondition_mismatch" or
                "mortal_location_discovery_transition_invalid");
    }

    [Fact]
    public void Lifecycle_LinkUpdateChangesOnlyAccessAndDiscoveryAndPreservesAuthority()
    {
        var accepted = CreateAcceptedDirectedTopology(linkDiscovery: "hidden");
        var before = FindLink(accepted.FinalWorldMap, accepted.LinkId);
        var updates = new JsonObject
        {
            ["linkUpdates"] = new JsonArray(new JsonObject
            {
                ["linkId"] = accepted.LinkId,
                ["access"] = new JsonObject
                {
                    ["state"] = "conditional",
                    ["reason"] = "Нужен бронзовый ключ.",
                    ["requirements"] = new JsonArray(new JsonObject
                    {
                        ["kind"] = "item_capability",
                        ["value"] = "bronze_key"
                    })
                },
                ["discovery"] = new JsonObject
                {
                    ["fromTier"] = "hidden",
                    ["toTier"] = "rumored",
                    ["toAudience"] = "player_known",
                    ["rumorSummary"] = "В стене башни может быть тайный проход."
                }
            })
        };

        var result = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawWorldMapUpdates: updates,
            turn: 43);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        var after = FindLink(plan.FinalWorldMap, accepted.LinkId);
        Assert.Equal("conditional", after["access"]!["state"]!.GetValue<string>());
        Assert.Equal("rumored", after["discovery"]!["tier"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(before["sourceLocationId"], after["sourceLocationId"]));
        Assert.True(JsonNode.DeepEquals(before["targetLocationId"], after["targetLocationId"]));
        Assert.True(JsonNode.DeepEquals(before["linkType"], after["linkType"]));
        Assert.True(JsonNode.DeepEquals(before["materialization"], after["materialization"]));
        Assert.True(JsonNode.DeepEquals(before["materializationReceipt"], after["materializationReceipt"]));
        Assert.NotEmpty(FindLinkIndexEntry(plan.FinalIdentityIndex, accepted.LinkId)["transitions"]!.AsArray());
    }

    [Fact]
    public void Lifecycle_LinkRemovalRetiresExactIdentityAndReplayFailsClosed()
    {
        var accepted = CreateAcceptedDirectedTopology();
        var removal = new JsonObject
        {
            ["linkRemovals"] = new JsonArray(new JsonObject
            {
                ["linkId"] = accepted.LinkId,
                ["sourceLocationId"] = accepted.SourceLocationId,
                ["targetLocationId"] = accepted.TargetLocationId,
                ["reason"] = "Обвал окончательно уничтожил проход."
            })
        };

        var removed = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawWorldMapUpdates: removal,
            turn: 43);

        Assert.True(removed.Success, string.Join(Environment.NewLine, removed.Issues.Select(issue => issue.Message)));
        var removalPlan = Assert.IsType<MortalLocationAcceptedTurnPlan>(removed.Plan);
        Assert.Empty(removalPlan.FinalWorldMap["links"]!.AsArray());
        var retired = FindLinkIndexEntry(removalPlan.FinalIdentityIndex, accepted.LinkId);
        Assert.Equal("retired", retired["state"]!.GetValue<string>());
        Assert.NotEmpty(retired["transitions"]!.AsArray());

        var replayLink = MortalLocationTestFixture.CreateRawLink(
            accepted.SourceLocationId,
            accepted.TargetLocationId);
        replayLink["initialId"] = "linkref_lifecycle_source_to_target";
        replayLink["materialization"]!["initialId"] = "linkref_lifecycle_source_to_target";
        replayLink["materialization"]!["materializationId"] = "mlinkmat_lifecycle_source_to_target";
        var replay = Build(
            removalPlan.FinalWorldMap,
            removalPlan.FinalCurrentLocation,
            removalPlan.FinalIdentityIndex,
            rawWorldMapUpdates: new JsonObject
            {
                ["newLinks"] = new JsonArray(replayLink)
            },
            turn: 44);

        Assert.False(replay.Success);
        Assert.Contains(replay.Issues, issue =>
            issue.Code == "mortal_location_materialization_historical_replay");
    }

    [Fact]
    public void Lifecycle_LinkRemovalRequiresExactEndpoints()
    {
        var accepted = CreateAcceptedDirectedTopology();

        var result = Build(
            accepted.FinalWorldMap,
            accepted.FinalCurrentLocation,
            accepted.FinalIdentityIndex,
            rawWorldMapUpdates: new JsonObject
            {
                ["linkRemovals"] = new JsonArray(new JsonObject
                {
                    ["linkId"] = accepted.LinkId,
                    ["sourceLocationId"] = accepted.SourceLocationId.ToUpperInvariant(),
                    ["targetLocationId"] = accepted.TargetLocationId,
                    ["reason"] = "Ложная регистровая ссылка."
                })
            },
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_link_transition_precondition_mismatch");
    }

    private static AcceptedDirectedTopology CreateAcceptedDirectedTopology(
        string targetDiscovery = "discovered",
        string linkDiscovery = "discovered",
        bool includeLink = true,
        bool reverseLink = false,
        string accessState = "open")
    {
        var source = CreateRawLocation(
            "locref_lifecycle_source",
            "mlocmat_lifecycle_source",
            x: 30,
            route: "current_scene_creation");
        var target = CreateRawLocation(
            "locref_lifecycle_target",
            "mlocmat_lifecycle_target",
            x: 31,
            route: "world_map_creation");
        target["discovery"] = Discovery(targetDiscovery);
        JsonObject? link = null;
        if (includeLink)
        {
            MarkTopologyPopulated(source, target);
            link = CreateRawLink(
                "linkref_lifecycle_source_to_target",
                "mlinkmat_lifecycle_source_to_target",
                reverseLink
                    ? target["initialId"]!.GetValue<string>()
                    : source["initialId"]!.GetValue<string>(),
                reverseLink
                    ? source["initialId"]!.GetValue<string>()
                    : target["initialId"]!.GetValue<string>());
            link["discovery"] = Discovery(linkDiscovery);
            link["access"] = accessState switch
            {
                "open" => new JsonObject
                {
                    ["state"] = "open",
                    ["reason"] = null,
                    ["requirements"] = new JsonArray()
                },
                "conditional" => new JsonObject
                {
                    ["state"] = "conditional",
                    ["reason"] = "Нужен ключ от башни.",
                    ["requirements"] = new JsonArray(new JsonObject
                    {
                        ["kind"] = "item_capability",
                        ["value"] = "tower_key"
                    })
                },
                "sealed" => new JsonObject
                {
                    ["state"] = "sealed",
                    ["reason"] = "Проход запечатан.",
                    ["requirements"] = new JsonArray()
                },
                _ => throw new ArgumentOutOfRangeException(nameof(accessState))
            };
        }

        var updates = Updates(target);
        if (link != null)
            updates["newLinks"]!.AsArray().Add(link);

        var result = Build(
            rawCurrentLocationData: source,
            rawWorldMapUpdates: updates);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        return new AcceptedDirectedTopology(
            plan.FinalWorldMap,
            plan.FinalCurrentLocation!,
            plan.FinalIdentityIndex,
            plan.LocationIdsByInitialId[source["initialId"]!.GetValue<string>()],
            plan.LocationIdsByInitialId[target["initialId"]!.GetValue<string>()],
            link == null
                ? string.Empty
                : plan.LinkIdsByInitialId[link["initialId"]!.GetValue<string>()]);
    }

    private static JsonObject FindLocation(JsonObject map, string locationId) =>
        map["locations"]!.AsArray().OfType<JsonObject>().Single(location =>
            location["locationId"]!.GetValue<string>() == locationId);

    private static JsonObject FindLink(JsonObject map, string linkId) =>
        map["links"]!.AsArray().OfType<JsonObject>().Single(link =>
            link["linkId"]!.GetValue<string>() == linkId);

    private static JsonObject FindLocationIndexEntry(JsonObject index, string locationId) =>
        index["locationEntries"]!.AsArray().OfType<JsonObject>().Single(entry =>
            entry["locationId"]!.GetValue<string>() == locationId);

    private static JsonObject FindLinkIndexEntry(JsonObject index, string linkId) =>
        index["linkEntries"]!.AsArray().OfType<JsonObject>().Single(entry =>
            entry["linkId"]!.GetValue<string>() == linkId);

    private sealed record AcceptedDirectedTopology(
        JsonObject FinalWorldMap,
        JsonObject FinalCurrentLocation,
        JsonObject FinalIdentityIndex,
        string SourceLocationId,
        string TargetLocationId,
        string LinkId);
}
