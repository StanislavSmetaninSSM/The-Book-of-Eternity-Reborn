using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalLocationMaterializationValidationTests
{
    [Theory]
    [InlineData("storage_update")]
    [InlineData("storage_remove")]
    [InlineData("threat_add")]
    [InlineData("threat_update")]
    [InlineData("threat_remove")]
    [InlineData("threat_complete")]
    public void GovernedCommands_EachStandaloneCommandMutatesCanonicalState(string command)
    {
        var baseline = CreateGovernedCommandBaseline();
        var result = Build(
            baseline.WorldMap,
            baseline.CurrentLocation,
            baseline.IdentityIndex,
            rawWorldMapUpdates: CreateGovernedCommand(command, baseline.LocationId),
            turn: 43);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        Assert.Contains(MortalLocationMaterializationContract.WorldMapPath, plan.TouchedPaths);
        Assert.Contains(MortalLocationMaterializationContract.CurrentLocationPath, plan.TouchedPaths);
        Assert.Contains(MortalLocationIdentityState.StatePath, plan.TouchedPaths);

        var location = FindLocation(plan.FinalWorldMap, baseline.LocationId);
        var current = plan.FinalCurrentLocation!;
        switch (command)
        {
            case "storage_update":
                var updatedStorage = FindStorage(location, "storage_update");
                Assert.Equal("Обновлённый сундук", updatedStorage["name"]!.GetValue<string>());
                Assert.Equal(12, updatedStorage["capacity"]!.GetValue<int>());
                Assert.Equal("Faction", updatedStorage["owner"]!["ownerType"]!.GetValue<string>());
                Assert.Equal("faction_road_wardens", updatedStorage["owner"]!["ownerId"]!.GetValue<string>());
                var currentStorage = FindStorage(current, "storage_update");
                Assert.Equal("Обновлённый сундук", currentStorage["name"]!.GetValue<string>());
                Assert.Equal(12, currentStorage["capacity"]!.GetValue<int>());
                Assert.Equal("faction_road_wardens", currentStorage["owner"]!["ownerId"]!.GetValue<string>());
                Assert.Equal("ITEM_CONTENT_MARKER", currentStorage["contents"]![0]!["marker"]!.GetValue<string>());
                break;
            case "storage_remove":
                Assert.DoesNotContain(location["locationStorages"]!.AsArray().OfType<JsonObject>(), storage =>
                    storage["storageId"]!.GetValue<string>() == "storage_remove");
                Assert.DoesNotContain(current["locationStorages"]!.AsArray().OfType<JsonObject>(), storage =>
                    storage["storageId"]!.GetValue<string>() == "storage_remove");
                break;
            case "threat_add":
                Assert.Contains(location["activeThreats"]!.AsArray().OfType<JsonObject>(), threat =>
                    threat["threatId"] is JsonValue value &&
                    value.TryGetValue<string>(out var threatId) &&
                    threatId.StartsWith("threat_", StringComparison.Ordinal) &&
                    threat["name"]!.GetValue<string>() == "Новая угроза");
                break;
            case "threat_update":
                var updated = FindThreat(location, "threat_update");
                Assert.Equal(9, updated["intensity"]!.GetValue<int>());
                Assert.Equal("Продолжающийся рейд", updated["currentActivity"]!["activityName"]!.GetValue<string>());
                Assert.Equal(45, updated["currentActivity"]!["timeSpentMinutes"]!.GetValue<int>());
                break;
            case "threat_remove":
                Assert.DoesNotContain(location["activeThreats"]!.AsArray().OfType<JsonObject>(), threat =>
                    threat["threatId"]!.GetValue<string>() == "threat_remove");
                break;
            case "threat_complete":
                Assert.Null(FindThreat(location, "threat_complete")["currentActivity"]);
                var history = Assert.Single(location["eventDescriptions"]!.AsArray().OfType<JsonObject>(), entry =>
                    entry["eventType"]?.GetValue<string>() == "threat_activity_completion");
                Assert.Equal("Завершаемая угроза", history["title"]!.GetValue<string>());
                Assert.Equal("Completed", history["finalState"]!.GetValue<string>());
                Assert.Equal("Рейд остановлен у старых ворот.", history["description"]!.GetValue<string>());
                break;
        }

        var indexEntry = FindLocationIndexEntry(plan.FinalIdentityIndex, baseline.LocationId);
        Assert.NotEmpty(indexEntry["transitions"]!.AsArray());
    }

    [Fact]
    public void GovernedCommands_MixedPackageAppliesEveryCommandAndLocationUpdate()
    {
        var baseline = CreateGovernedCommandBaseline();
        var updates = new JsonObject
        {
            ["locationUpdates"] = new JsonArray(new JsonObject
            {
                ["locationId"] = baseline.LocationId,
                ["displayName"] = "Площадь после тревоги"
            })
        };
        foreach (var command in new[]
                 {
                     "storage_update", "storage_remove", "threat_add",
                     "threat_update", "threat_remove", "threat_complete"
                 })
        {
            var commandRoot = CreateGovernedCommand(command, baseline.LocationId);
            foreach (var property in commandRoot)
                updates[property.Key] = property.Value?.DeepClone();
        }

        var result = Build(
            baseline.WorldMap,
            baseline.CurrentLocation,
            baseline.IdentityIndex,
            rawWorldMapUpdates: updates,
            turn: 43);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        var location = FindLocation(plan.FinalWorldMap, baseline.LocationId);
        Assert.Equal("Площадь после тревоги", location["displayName"]!.GetValue<string>());
        Assert.Equal("Обновлённый сундук", FindStorage(location, "storage_update")["name"]!.GetValue<string>());
        Assert.DoesNotContain(location["locationStorages"]!.AsArray().OfType<JsonObject>(), storage =>
            storage["storageId"]!.GetValue<string>() == "storage_remove");
        Assert.Equal(9, FindThreat(location, "threat_update")["intensity"]!.GetValue<int>());
        Assert.DoesNotContain(location["activeThreats"]!.AsArray().OfType<JsonObject>(), threat =>
            threat["threatId"]!.GetValue<string>() == "threat_remove");
        Assert.Null(FindThreat(location, "threat_complete")["currentActivity"]);
        Assert.Contains(location["activeThreats"]!.AsArray().OfType<JsonObject>(), threat =>
            threat["name"]!.GetValue<string>() == "Новая угроза" &&
            threat["threatId"]!.GetValue<string>().StartsWith("threat_", StringComparison.Ordinal));
    }

    [Fact]
    public void GovernedCommands_RemovingStorageWithBoundItemsFailsClosed()
    {
        var baseline = CreateGovernedCommandBaseline();
        var boundItem = MortalItemTestFixture.CreateCanonicalRoot("itm_bound_marker");
        boundItem["marker"] = "BOUND_ITEM";
        FindStorage(baseline.CurrentLocation, "storage_remove")["contents"] =
            new JsonArray(boundItem);

        var result = Build(
            baseline.WorldMap,
            baseline.CurrentLocation,
            baseline.IdentityIndex,
            rawWorldMapUpdates: CreateGovernedCommand("storage_remove", baseline.LocationId),
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_storage_removal_not_empty");
    }

    [Fact]
    public void GovernedCommands_RemovingStorageWithOffscreenBoundItemsFailsClosed()
    {
        var baseline = CreateGovernedCommandBaseline();
        var offscreen = MortalLocationStorageContentsState.BuildCanonicalRoot(
            new Dictionary<MortalLocationStorageKey, JsonArray>
            {
                [new MortalLocationStorageKey(
                    baseline.LocationId,
                    "storage_remove")] = new JsonArray(
                    MortalItemTestFixture.CreateCanonicalRoot("itm_offscreen_bound_marker"))
            });

        var result = Build(
            baseline.WorldMap,
            preTurnCurrentLocation: null,
            preTurnIdentityIndex: baseline.IdentityIndex,
            rawWorldMapUpdates: CreateGovernedCommand("storage_remove", baseline.LocationId),
            preTurnStorageContents: offscreen,
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_storage_removal_not_empty");
    }

    [Fact]
    public void GovernedCommands_UnknownWorldMapCommandFailsClosed()
    {
        var baseline = CreateGovernedCommandBaseline();

        var result = Build(
            baseline.WorldMap,
            baseline.CurrentLocation,
            baseline.IdentityIndex,
            rawWorldMapUpdates: new JsonObject
            {
                ["inventedLocationMutation"] = new JsonArray()
            },
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "mortal_location_world_map_command_unknown");
    }

    [Theory]
    [InlineData("intensity_wrong_type")]
    [InlineData("required_goal_null")]
    [InlineData("archetype_null")]
    [InlineData("archetype_invalid_enum")]
    [InlineData("impact_profile_null")]
    [InlineData("impact_value_wrong_type")]
    public void GovernedCommands_ThreatUpdateThatBreaksCanonicalThreatFailsClosed(string mutation)
    {
        var baseline = CreateGovernedCommandBaseline();
        var updates = CreateGovernedCommand("threat_update", baseline.LocationId);
        var patch = updates["threatsToUpdate"]![0]!["threatUpdate"]!.AsObject();
        ApplyMalformedThreatPatch(patch, mutation);

        var result = Build(
            baseline.WorldMap,
            baseline.CurrentLocation,
            baseline.IdentityIndex,
            rawWorldMapUpdates: updates,
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.FilePath.StartsWith(
                "worldMapUpdates.threatsToUpdate[0].threatUpdate.result",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("intensity_wrong_type")]
    [InlineData("required_goal_null")]
    [InlineData("archetype_null")]
    [InlineData("archetype_invalid_enum")]
    [InlineData("impact_profile_null")]
    [InlineData("impact_value_wrong_type")]
    public void CanonicalLocation_MalformedActiveThreatSemanticsFailValidation(string mutation)
    {
        var baseline = CreateGovernedCommandBaseline();
        var location = FindLocation(baseline.WorldMap, baseline.LocationId);
        var threat = FindThreat(location, "threat_update");
        ApplyMalformedThreatPatch(threat, mutation);
        using var document = JsonDocument.Parse(location.ToJsonString());

        var issues = MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            "game_state/world/world_map.json.locations[0]");

        Assert.Contains(issues, issue =>
            issue.FilePath.StartsWith(
                "game_state/world/world_map.json.locations[0].activeThreats[0]",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("storage_update", "storage_remove", "mortal_location_storage_transition_conflict")]
    [InlineData("threat_update", "threat_remove", "mortal_location_threat_transition_conflict")]
    [InlineData("threat_update", "threat_complete", "mortal_location_threat_transition_conflict")]
    [InlineData("threat_remove", "threat_complete", "mortal_location_threat_transition_conflict")]
    public void GovernedCommands_ConflictingChildTransitionsFailClosed(
        string first,
        string second,
        string expectedCode)
    {
        var baseline = CreateGovernedCommandBaseline();
        var updates = CreateGovernedCommand(first, baseline.LocationId);
        var secondRoot = CreateGovernedCommand(second, baseline.LocationId);

        foreach (var property in secondRoot)
            updates[property.Key] = property.Value?.DeepClone();

        if (first.StartsWith("storage_", StringComparison.Ordinal))
        {
            updates["storageUpdates"]![0]!["storageId"] = "storage_remove";
        }
        else
        {
            const string sharedThreatId = "threat_complete";
            if (updates["threatsToUpdate"] is JsonArray updateCommands)
                updateCommands[0]!["threatUpdate"]!["threatId"] = sharedThreatId;
            if (updates["threatsToRemove"] is JsonArray removalCommands)
                removalCommands[0]!["threatId"] = sharedThreatId;
            if (updates["completeThreatActivities"] is JsonArray completionCommands)
                completionCommands[0]!["threatId"] = sharedThreatId;
        }

        var result = Build(
            baseline.WorldMap,
            baseline.CurrentLocation,
            baseline.IdentityIndex,
            rawWorldMapUpdates: updates,
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Theory]
    [InlineData("storage_update")]
    [InlineData("storage_remove")]
    [InlineData("threat_add")]
    [InlineData("threat_update")]
    [InlineData("threat_remove")]
    [InlineData("threat_complete")]
    public void GovernedCommands_CaseVariantTargetIdentityFailsClosed(string command)
    {
        var baseline = CreateGovernedCommandBaseline();
        var updates = CreateGovernedCommand(command, baseline.LocationId.ToUpperInvariant());

        var result = Build(
            baseline.WorldMap,
            baseline.CurrentLocation,
            baseline.IdentityIndex,
            rawWorldMapUpdates: updates,
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code != null &&
            issue.Code.EndsWith("_target_unresolved", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("storage_update")]
    [InlineData("storage_remove")]
    [InlineData("threat_update")]
    [InlineData("threat_remove")]
    [InlineData("threat_complete")]
    public void GovernedCommands_CaseVariantChildIdentityFailsClosed(string command)
    {
        var baseline = CreateGovernedCommandBaseline();
        var updates = CreateGovernedCommand(command, baseline.LocationId);
        var operation = updates.First().Value!.AsArray()[0]!.AsObject();
        if (command == "storage_update" || command == "storage_remove")
            operation["storageId"] = operation["storageId"]!.GetValue<string>().ToUpperInvariant();
        else if (command == "threat_update")
            operation["threatUpdate"]!["threatId"] =
                operation["threatUpdate"]!["threatId"]!.GetValue<string>().ToUpperInvariant();
        else
            operation["threatId"] = operation["threatId"]!.GetValue<string>().ToUpperInvariant();

        var result = Build(
            baseline.WorldMap,
            baseline.CurrentLocation,
            baseline.IdentityIndex,
            rawWorldMapUpdates: updates,
            turn: 43);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue =>
            issue.Code != null &&
            issue.Code.EndsWith("_target_unresolved", StringComparison.Ordinal));
    }

    [Fact]
    public void GovernedCommands_NewThreatMayBindExactSameTurnLocationInitialId()
    {
        var location = CreateRawLocation(
            "locref_same_turn_threat_target",
            "mlocmat_same_turn_threat_target",
            x: 52,
            route: "world_map_creation");
        var updates = Updates(location);
        updates["threatsToAdd"] = new JsonArray(new JsonObject
        {
            ["targetLocationId"] = null,
            ["initialTargetLocationId"] = "locref_same_turn_threat_target",
            ["threat"] = CreateThreat(null, "Угроза новой локации", active: false)
        });

        var result = Build(rawWorldMapUpdates: updates, turn: 42);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Message)));
        var plan = Assert.IsType<MortalLocationAcceptedTurnPlan>(result.Plan);
        var acceptedLocation = Assert.Single(plan.FinalWorldMap["locations"]!.AsArray().OfType<JsonObject>());
        var threat = Assert.Single(acceptedLocation["activeThreats"]!.AsArray().OfType<JsonObject>());
        Assert.Equal("Угроза новой локации", threat["name"]!.GetValue<string>());
        Assert.StartsWith("threat_", threat["threatId"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GovernedCommands_StateDistributorAndNormalizerApplyCompleteMixedPackage()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var baseline = CreateGovernedCommandBaseline();
        await context.WritePreTurnCanonicalStateAsync(
            FindLocation(baseline.WorldMap, baseline.LocationId));
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            baseline.CurrentLocation);

        var backups = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (canonicalPath, node) in new[]
                 {
                     (MortalLocationMaterializationContract.WorldMapPath, (JsonNode)baseline.WorldMap),
                     (MortalLocationMaterializationContract.CurrentLocationPath, baseline.CurrentLocation),
                     (MortalLocationIdentityState.StatePath, baseline.IdentityIndex)
                 })
        {
            var backupPath = "test_backups/governed_commands/" + Path.GetFileName(canonicalPath);
            await context.WriteJsonAsync(backupPath, node);
            backups.Add(canonicalPath, backupPath);
        }
        await context.WriteJsonAsync(
            "input/turn_request.json",
            new JsonObject
            {
                ["sessionId"] = "session_governed_location_commands",
                ["requestId"] = "request_governed_location_commands",
                ["turnNumber"] = 43,
                ["playerAction"] = "Применить атомарные команды локации."
            });

        var updates = new JsonObject
        {
            ["locationUpdates"] = new JsonArray(new JsonObject
            {
                ["locationId"] = baseline.LocationId,
                ["displayName"] = "Площадь после тревоги"
            })
        };
        foreach (var command in new[]
                 {
                     "storage_update", "storage_remove", "threat_add",
                     "threat_update", "threat_remove", "threat_complete"
                 })
        {
            foreach (var property in CreateGovernedCommand(command, baseline.LocationId))
                updates[property.Key] = property.Value?.DeepClone();
        }

        using var updateDocument = JsonDocument.Parse(updates.ToJsonString());
        var response = new GameResponse
        {
            WorldMapUpdates = updateDocument.RootElement.Clone()
        };
        var distributor = new StateDistributor(
            context.FileSystem,
            NullLogger<StateDistributor>.Instance);

        var modified = await distributor.DistributeAsync(response);
        Assert.Contains(MortalLocationMaterializationContract.WorldMapPath, modified);
        await context.Normalizer.NormalizeMortalLocationsAsync(backups);

        var finalMap = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        Assert.False(finalMap.ContainsKey("worldMapUpdates"));
        var location = FindLocation(finalMap, baseline.LocationId);
        Assert.Equal("Площадь после тревоги", location["displayName"]!.GetValue<string>());
        Assert.Equal("Обновлённый сундук", FindStorage(location, "storage_update")["name"]!.GetValue<string>());
        Assert.DoesNotContain(location["locationStorages"]!.AsArray().OfType<JsonObject>(), storage =>
            storage["storageId"]!.GetValue<string>() == "storage_remove");
        Assert.Equal(9, FindThreat(location, "threat_update")["intensity"]!.GetValue<int>());
        Assert.DoesNotContain(location["activeThreats"]!.AsArray().OfType<JsonObject>(), threat =>
            threat["threatId"]!.GetValue<string>() == "threat_remove");
        Assert.Null(FindThreat(location, "threat_complete")["currentActivity"]);
        Assert.Contains(location["activeThreats"]!.AsArray().OfType<JsonObject>(), threat =>
            threat["name"]!.GetValue<string>() == "Новая угроза" &&
            threat["threatId"]!.GetValue<string>().StartsWith("threat_", StringComparison.Ordinal));

        var current = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath))!.AsObject();
        Assert.Equal(
            "ITEM_CONTENT_MARKER",
            FindStorage(current, "storage_update")["contents"]![0]!["marker"]!.GetValue<string>());
        Assert.DoesNotContain(current["locationStorages"]!.AsArray().OfType<JsonObject>(), storage =>
            storage["storageId"]!.GetValue<string>() == "storage_remove");
    }

    private static GovernedCommandBaseline CreateGovernedCommandBaseline()
    {
        const string locationId = "loc_governed_command_square";
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            locationId,
            "Площадь команд",
            discoveryTier: "visited",
            x: 40);
        location["locationStorages"] = new JsonArray(
            CreateStorage("storage_update", "Старый сундук"),
            CreateStorage("storage_remove", "Пустая корзина"));
        location["activeThreats"] = new JsonArray(
            CreateThreat("threat_update", "Обновляемая угроза", active: true),
            CreateThreat("threat_remove", "Удаляемая угроза", active: false),
            CreateThreat("threat_complete", "Завершаемая угроза", active: true));
        SetSectionPopulated(location, "storageMetadata");
        SetSectionPopulated(location, "activeThreats");
        MortalLocationTestFixture.ResealCanonicalLocation(location);

        var current = MortalLocationTestFixture.CreateCurrentProjection(location);
        var storedItem = MortalItemTestFixture.CreateCanonicalRoot("itm_content_marker");
        storedItem["marker"] = "ITEM_CONTENT_MARKER";
        FindStorage(current, "storage_update")["contents"] = new JsonArray(storedItem);
        FindStorage(current, "storage_remove")["contents"] = new JsonArray();
        return new GovernedCommandBaseline(
            MortalLocationTestFixture.CreateWorldMap(location),
            current,
            MortalLocationTestFixture.CreateIdentityIndex(location),
            locationId);
    }

    private static JsonObject CreateGovernedCommand(string command, string locationId) => command switch
    {
        "storage_update" => new JsonObject
        {
            ["storageUpdates"] = new JsonArray(new JsonObject
            {
                ["targetLocationId"] = locationId,
                ["storageId"] = "storage_update",
                ["update"] = new JsonObject
                {
                    ["newName"] = "Обновлённый сундук",
                    ["newDescription"] = "Сундук укрепили железными полосами.",
                    ["newCapacity"] = 12,
                    ["newOwner"] = new JsonObject
                    {
                        ["ownerType"] = "Faction",
                        ["ownerId"] = "faction_road_wardens",
                        ["ownerName"] = "Дорожная стража"
                    }
                }
            })
        },
        "storage_remove" => new JsonObject
        {
            ["storagesToRemove"] = new JsonArray(new JsonObject
            {
                ["targetLocationId"] = locationId,
                ["storageId"] = "storage_remove"
            })
        },
        "threat_add" => new JsonObject
        {
            ["threatsToAdd"] = new JsonArray(new JsonObject
            {
                ["targetLocationId"] = locationId,
                ["threat"] = CreateThreat(null, "Новая угроза", active: false)
            })
        },
        "threat_update" => new JsonObject
        {
            ["threatsToUpdate"] = new JsonArray(new JsonObject
            {
                ["targetLocationId"] = locationId,
                ["threatUpdate"] = new JsonObject
                {
                    ["threatId"] = "threat_update",
                    ["intensity"] = 9,
                    ["currentActivity"] = new JsonObject
                    {
                        ["timeSpentMinutes"] = 45
                    }
                }
            })
        },
        "threat_remove" => new JsonObject
        {
            ["threatsToRemove"] = new JsonArray(new JsonObject
            {
                ["targetLocationId"] = locationId,
                ["threatId"] = "threat_remove"
            })
        },
        "threat_complete" => new JsonObject
        {
            ["completeThreatActivities"] = new JsonArray(new JsonObject
            {
                ["targetLocationId"] = locationId,
                ["threatId"] = "threat_complete",
                ["threatName"] = "Завершаемая угроза",
                ["finalState"] = "Completed",
                ["narrativeSummary"] = "Рейд остановлен у старых ворот."
            })
        },
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    private static JsonObject CreateStorage(string storageId, string name) =>
        new()
        {
            ["storageId"] = storageId,
            ["name"] = name,
            ["description"] = "Тестовое хранилище.",
            ["capacity"] = 8,
            ["owner"] = null,
            ["contents"] = new JsonArray()
        };

    private static JsonObject CreateThreat(string? threatId, string name, bool active) =>
        new()
        {
            ["threatId"] = threatId,
            ["name"] = name,
            ["description"] = "Тестовая постоянная угроза.",
            ["intensity"] = 3,
            ["longTermGoal"] = "Захватить старые ворота.",
            ["currentActivity"] = active
                ? new JsonObject
                {
                    ["activityName"] = "Продолжающийся рейд",
                    ["description"] = "Угроза готовит новый налёт.",
                    ["totalTimeCostMinutes"] = 120,
                    ["timeSpentMinutes"] = 15,
                    ["currentStepNumber"] = 1,
                    ["totalStepsInActivity"] = 3
                }
                : null,
            ["threatArchetype"] = new JsonObject
            {
                ["motivation"] = "Domination",
                ["method"] = "Overt",
                ["customMotivation"] = null,
                ["customMethod"] = null
            },
            ["impactProfile"] = new JsonObject
            {
                ["primaryTargetType"] = "Location",
                ["primaryTargetId"] = null,
                ["primaryTargetName"] = "Старые ворота",
                ["primaryImpact"] = "Stability",
                ["baseImpactValue"] = 2
            }
        };

    private static void ApplyMalformedThreatPatch(JsonObject patch, string mutation)
    {
        switch (mutation)
        {
            case "intensity_wrong_type":
                patch["intensity"] = "bad";
                break;
            case "required_goal_null":
                patch["longTermGoal"] = null;
                break;
            case "archetype_null":
                patch["threatArchetype"] = null;
                break;
            case "archetype_invalid_enum":
                patch["threatArchetype"] = new JsonObject
                {
                    ["motivation"] = "InventedMotivation"
                };
                break;
            case "impact_profile_null":
                patch["impactProfile"] = null;
                break;
            case "impact_value_wrong_type":
                patch["impactProfile"] = new JsonObject
                {
                    ["baseImpactValue"] = "bad"
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }
    }

    private static JsonObject FindStorage(JsonObject location, string storageId) =>
        location["locationStorages"]!.AsArray().OfType<JsonObject>().Single(storage =>
            storage["storageId"]!.GetValue<string>() == storageId);

    private static JsonObject FindThreat(JsonObject location, string threatId) =>
        location["activeThreats"]!.AsArray().OfType<JsonObject>().Single(threat =>
            threat["threatId"]!.GetValue<string>() == threatId);

    private static void SetSectionPopulated(JsonObject location, string sectionName) =>
        location["materialization"]!["sections"]![sectionName] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };

    private sealed record GovernedCommandBaseline(
        JsonObject WorldMap,
        JsonObject CurrentLocation,
        JsonObject IdentityIndex,
        string LocationId);
}
