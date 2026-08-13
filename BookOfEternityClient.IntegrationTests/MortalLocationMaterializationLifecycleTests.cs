using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed partial class MortalLocationMaterializationLifecycleTests
{
    [Theory]
    [InlineData(MortalLocationMaterializationContract.WorldMapPath)]
    [InlineData(MortalLocationMaterializationContract.CurrentLocationPath)]
    [InlineData(MortalLocationIdentityState.StatePath)]
    [InlineData(MortalBootstrapLocationScaffold.StatePath)]
    [InlineData(InventoryEquipmentService.ItemsPath)]
    [InlineData(MortalItemIdentityState.StatePath)]
    [InlineData(FactionCoreChangesContract.FactionCorePath)]
    [InlineData(NpcCoreChangesContract.NpcCorePath)]
    public async Task AcceptedRefresh_CombinedWriteFailureRestoresEveryTrackedPathByteExact(
        string failurePath)
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var backups = await ArrangeCombinedAcceptedTurnAsync(context);
        var before = await context.CaptureBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        if (failurePath == MortalLocationMaterializationContract.WorldMapPath)
        {
            var rawMap = (await context.ReadJsonAsync(failurePath))!.AsObject();
            Assert.Single(rawMap["worldMapUpdates"]!["newLinks"]!.AsArray());
        }
        context.ArmInjectedWriteFailure(failurePath);

        var failure = await Record.ExceptionAsync(() =>
            AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
                context.FileSystem,
                context.Normalizer,
                context.Validator,
                backups));

        Assert.NotNull(failure);
        Assert.True(
            failure.ToString().Contains(
                "Injected Mortal location write failure",
                StringComparison.Ordinal),
            failure.ToString());
        Assert.Equal(failurePath, context.InjectedPublishedPath);
        var after = await context.CaptureBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        MortalLocationMaterializationAssertions.AssertExactBytes(before, after);
    }

    [Fact]
    public async Task AcceptedRefresh_ItemPostSealFailureRestoresLocationAndAllTrackedPathsByteExact()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var backups = await ArrangeCurrentCreationAsync(context);
        await WriteUnrelatedTrackedSentinelsAsync(context);
        var invalidItem = MortalItemTestFixture.CreateCanonicalRoot("itm_location_atomic_probe");
        invalidItem["materializationReceipt"]!["seal"] = "forged_post_seal";
        await context.WriteJsonAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(invalidItem),
                ["equippedItems"] = new JsonObject()
            });
        await context.WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(invalidItem));
        var before = await context.CaptureBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        var issues = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem,
            context.Normalizer,
            context.Validator,
            backups);

        Assert.Contains(issues, issue =>
            issue.Severity == IssueSeverity.Error &&
            (issue.Code?.Contains("seal", StringComparison.Ordinal) == true ||
             issue.Code?.Contains("receipt", StringComparison.Ordinal) == true));
        var after = await context.CaptureBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);
        MortalLocationMaterializationAssertions.AssertExactBytes(before, after);
    }

    [Fact]
    public async Task AcceptedRefresh_CorrectedRetrySettlesOneLocationExactlyOnce()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var backups = await ArrangeCurrentCreationAsync(context);
        await WriteUnrelatedTrackedSentinelsAsync(context);
        var item = MortalItemTestFixture.CreateCanonicalRoot("itm_location_retry_probe");
        item["materializationReceipt"]!["seal"] = "forged_retry_seal";
        await WriteCanonicalItemAsync(context, item);
        var failedAttempt = await context.CaptureBytesAsync(
            CanonicalStateNormalizer.NormalizerRollbackTrackedFiles);

        var failedIssues = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem,
            context.Normalizer,
            context.Validator,
            backups);

        Assert.Contains(failedIssues, issue => issue.Severity == IssueSeverity.Error);
        MortalLocationMaterializationAssertions.AssertExactBytes(
            failedAttempt,
            await context.CaptureBytesAsync(
                CanonicalStateNormalizer.NormalizerRollbackTrackedFiles));

        MortalItemTestFixture.ResealCanonical(item);
        await WriteCanonicalItemAsync(context, item);
        var retryIssues = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem,
            context.Normalizer,
            context.Validator,
            backups);

        Assert.DoesNotContain(retryIssues, issue => issue.Severity == IssueSeverity.Error);
        var map = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        Assert.Single(map["locations"]!.AsArray());
        var identity = (await context.ReadJsonAsync(
            MortalLocationIdentityState.StatePath))!.AsObject();
        Assert.Single(identity["locationEntries"]!.AsArray());
        Assert.Empty(identity["linkEntries"]!.AsArray());
        var current = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath))!.AsObject();
        Assert.False(current.ContainsKey("currentLocationData"));
        Assert.NotNull(current["materializationReceipt"]);
    }

    [Fact]
    public async Task AcceptedRefresh_SameTurnNpcLocationReferenceRewritesToPermanentIdentity()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        const string existingLocationId = "loc_npc_rewrite_baseline";
        const string npcId = "npc_same_turn_location_rewrite";
        var existingLocation = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(
            existingLocationId,
            "Старая застава",
            x: 0,
            y: 0);
        await context.WritePreTurnCanonicalStateAsync(existingLocation);

        var npcRoot = MortalActorTestFixtures.CreateNpcCoreRoot(
            npcId,
            existingLocationId,
            "Старая застава");
        var actor = Assert.Single(npcRoot["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        npcRoot.Remove("NPCsInScene");
        npcRoot["UpdateNPCs"] = new JsonArray(actor.DeepClone());
        await context.WriteJsonAsync(NpcCoreChangesContract.NpcCorePath, npcRoot);
        await context.WriteJsonAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equippedItems"] = new JsonObject()
            });
        await context.WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot());
        await context.CaptureValidatedPendingSnapshotAsync();

        var newLocation = MortalLocationTestFixture.CreateRawLocation();
        newLocation["actorBindings"] = new JsonArray(new JsonObject
        {
            ["actorId"] = npcId,
            ["role"] = "resident",
            ["description"] = "Персонаж физически перешёл к новому броду."
        });
        newLocation["materialization"]!["sections"]!["actorBindings"] =
            new JsonObject
            {
                ["disposition"] = "populated",
                ["reason"] = null
            };
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: new JsonObject
            {
                ["newLocations"] = new JsonArray(newLocation.DeepClone())
            });
        var currentNpcRoot = npcRoot.DeepClone().AsObject();
        currentNpcRoot[NpcCoreChangesContract.PropertyName] = new JsonArray(
            new JsonObject
            {
                ["NPCId"] = npcId,
                ["reason"] = "Персонаж перешёл в созданную в этом ходе локацию.",
                ["location"] = new JsonObject
                {
                    ["currentLocationId"] = null,
                    ["initialLocationId"] = MortalLocationTestFixture.LocationInitialId
                }
            });
        await context.WriteJsonAsync(NpcCoreChangesContract.NpcCorePath, currentNpcRoot);

        var backups = new[]
            {
                MortalLocationMaterializationContract.WorldMapPath,
                MortalLocationMaterializationContract.CurrentLocationPath,
                MortalLocationIdentityState.StatePath,
                NpcCoreChangesContract.NpcCorePath
            }
            .ToDictionary(
                static path => path,
                static path => $"game_state/control/pending_turn_snapshot/{path}",
                StringComparer.Ordinal);

        var issues = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateAsync(
            context.FileSystem,
            context.Normalizer,
            context.Validator,
            backups);

        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var worldMap = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        var createdLocation = Assert.Single(
            worldMap["locations"]!.AsArray().OfType<JsonObject>(),
            location => string.Equals(
                location["materializationReceipt"]?["initialId"]?.GetValue<string>(),
                MortalLocationTestFixture.LocationInitialId,
                StringComparison.Ordinal));
        var permanentLocationId = createdLocation["locationId"]!.GetValue<string>();

        var normalizedNpcRoot = (await context.ReadJsonAsync(
            NpcCoreChangesContract.NpcCorePath))!.AsObject();
        Assert.False(normalizedNpcRoot.ContainsKey(NpcCoreChangesContract.PropertyName));
        var normalizedActor = Assert.Single(
            normalizedNpcRoot["UpdateNPCs"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(permanentLocationId, normalizedActor["currentLocationId"]!.GetValue<string>());
        Assert.Null(normalizedActor["initialLocationId"]);
        var actorBinding = Assert.Single(
            createdLocation["actorBindings"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(npcId, actorBinding["actorId"]!.GetValue<string>());
    }

    [Fact]
    public async Task LocationNormalizer_SameTurnFactionControlWritesExactEffectiveIdentity()
    {
        await using var context = await MortalLocationMaterializationTestContext.CreateAsync();
        var emptyMap = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            emptyMap);
        await context.WriteJsonAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationIdentityState.CreateEmptyRoot());
        await context.WriteJsonAsync(
            FactionCoreChangesContract.FactionCorePath,
            new JsonObject { ["factions"] = new JsonArray() });
        await context.CaptureValidatedPendingSnapshotAsync();

        const string factionInitialId = "factionref_same_turn_ford_watch";
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["factionControl"] = new JsonArray(new JsonObject
        {
            ["factionId"] = null,
            ["initialFactionId"] = factionInitialId,
            ["controlLevel"] = 35,
            ["description"] = "Дозор удерживает переправу."
        });
        location["materialization"]!["sections"]!["factionControl"] =
            new JsonObject
            {
                ["disposition"] = "populated",
                ["reason"] = null
            };
        await context.WriteRawTurnStateAsync(
            currentLocationData: null,
            worldMapUpdates: new JsonObject
            {
                ["newLocations"] = new JsonArray(location)
            });
        await context.WriteJsonAsync(
            FactionCoreChangesContract.FactionCorePath,
            new JsonObject
            {
                ["factionDataChanges"] = new JsonArray(new JsonObject
                {
                    ["factionId"] = null,
                    ["initialId"] = factionInitialId,
                    ["isNewFaction"] = true,
                    ["materialization"] = new JsonObject
                    {
                        ["factionType"] = "mortal_faction",
                        ["factionId"] = factionInitialId,
                        ["state"] = "complete"
                    }
                })
            });
        var backups = new[]
            {
                MortalLocationMaterializationContract.WorldMapPath,
                MortalLocationIdentityState.StatePath
            }
            .ToDictionary(
                static path => path,
                static path => $"game_state/control/pending_turn_snapshot/{path}",
                StringComparer.Ordinal);

        await context.Normalizer.NormalizeMortalLocationsAsync(backups);

        var worldMap = (await context.ReadJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath))!.AsObject();
        var createdLocation = Assert.Single(
            worldMap["locations"]!.AsArray().OfType<JsonObject>());
        var control = Assert.Single(
            createdLocation["factionControl"]!.AsArray().OfType<JsonObject>());
        Assert.Equal(factionInitialId, control["factionId"]!.GetValue<string>());
        Assert.False(control.ContainsKey("initialFactionId"));
    }

    private static async Task<IReadOnlyDictionary<string, string>> ArrangeCurrentCreationAsync(
        MortalLocationMaterializationTestContext context)
    {
        var map = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };
        var index = MortalLocationIdentityState.CreateEmptyRoot();
        const string mapBackup = "test_backups/location_atomic/world_map.json";
        const string indexBackup = "test_backups/location_atomic/location_identity_index.json";
        await context.WriteJsonAsync(mapBackup, map);
        await context.WriteJsonAsync(indexBackup, index);
        await context.WriteJsonAsync(MortalLocationMaterializationContract.WorldMapPath, map);
        await context.WriteJsonAsync(MortalLocationIdentityState.StatePath, index);
        await context.WriteJsonAsync(
            "input/turn_request.json",
            new JsonObject
            {
                ["sessionId"] = "session_location_atomic",
                ["requestId"] = "request_location_atomic",
                ["turnNumber"] = 42,
                ["playerAction"] = "Материализовать Чёрный брод."
            });

        var raw = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        raw["locationStorages"] = new JsonArray(
            MortalLocationTestFixture.CreateStorageMetadata(
                "storage_atomic_chest",
                "Сундук у брода",
                hasFullAccess: true,
                contents: new JsonArray()));
        raw["materialization"]!["sections"]!["storageMetadata"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = raw });

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [MortalLocationMaterializationContract.WorldMapPath] = mapBackup,
            [MortalLocationIdentityState.StatePath] = indexBackup
        };
    }

    private static async Task<IReadOnlyDictionary<string, string>> ArrangeCombinedAcceptedTurnAsync(
        MortalLocationMaterializationTestContext context)
    {
        const int turn = 42;
        const int incarnation = 31;
        const string sessionId = "session_mortal_location_materialization";
        const string requestId = "request_mortal_location_materialization";
        const string factionId = "faction_location_atomic_watch";
        const string npcId = "npc_location_atomic_witness";

        var request = MortalBootstrapLocationScaffold.CreatePendingRequest(
            incarnation,
            sessionId,
            requestId,
            turn);
        var scaffold = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["purpose"] = "fresh_mortal_world_bootstrap",
            ["requestId"] = requestId,
            ["locationMaterializationRequest"] = request.DeepClone()
        };
        var map = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };
        var current = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locationId"] = null,
            ["state"] = "pending_materialization"
        };
        var items = new JsonObject
        {
            ["items"] = new JsonArray(),
            ["equippedItems"] = new JsonObject()
        };
        var factions = new JsonObject
        {
            ["factions"] = new JsonArray(
                new JsonObject
                {
                    ["factionId"] = factionId,
                    ["name"] = "Смотрители атомарного брода",
                    ["description"] = "Свидетели тестового перехода.",
                    ["image_prompt"] = "dark fantasy river wardens, realistic illustration",
                    ["factionColor"] = "#315A88"
                })
        };
        var startReservation = request["startReservation"]!.AsObject();
        var npcRoot = MortalActorTestFixtures.CreateNpcCoreRoot(
            actorId: npcId,
            currentLocationId: startReservation["reservedLocationId"]!.GetValue<string>(),
            currentLocationName: "Чёрный брод");
        var npc = Assert.Single(npcRoot["NPCsInScene"]!.AsArray().OfType<JsonObject>());
        npc["factionAffiliations"] = new JsonArray();

        var baseline = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
        {
            [MortalLocationMaterializationContract.WorldMapPath] = map,
            [MortalLocationMaterializationContract.CurrentLocationPath] = current,
            [MortalLocationIdentityState.StatePath] = MortalLocationIdentityState.CreateEmptyRoot(),
            [MortalBootstrapLocationScaffold.StatePath] = scaffold,
            [InventoryEquipmentService.ItemsPath] = items,
            [MortalItemIdentityState.StatePath] = MortalItemIdentityState.CreateEmptyRoot(),
            [FactionCoreChangesContract.FactionCorePath] = factions,
            [NpcCoreChangesContract.NpcCorePath] = npcRoot
        };
        foreach (var pair in baseline)
            await context.WriteJsonAsync(pair.Key, pair.Value);

        await context.CaptureValidatedPendingSnapshotAsync(turn);

        var start = CreateBootstrapLocation(
            request,
            "startReservation",
            "mlocmat_location_atomic_start");
        var neighbor = CreateBootstrapLocation(
            request,
            "neighborReservation",
            "mlocmat_location_atomic_neighbor");
        neighbor["discovery"] = new JsonObject
        {
            ["tier"] = "discovered",
            ["audience"] = "player_known",
            ["rumorSummary"] = null
        };
        var link = CreateBootstrapLink(
            request,
            "mlinkmat_location_atomic_start_to_neighbor");
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            new JsonObject { ["currentLocationData"] = start });
        await context.WriteJsonAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            new JsonObject
            {
                ["worldMapUpdates"] = new JsonObject
                {
                    ["newLocations"] = new JsonArray(neighbor),
                    ["newLinks"] = new JsonArray(link),
                    ["threatsToAdd"] = new JsonArray(new JsonObject
                    {
                        ["targetLocationId"] = null,
                        ["initialTargetLocationId"] = neighbor["initialId"]!.DeepClone(),
                        ["threat"] = CreateAtomicThreat()
                    })
                }
            });

        var rawItem = MortalItemTestFixture.CreateRawRoot(
            route: "player_acquisition",
            authorityKind: "turn_outcome",
            authorityId: "turn_42",
            sourceTurn: turn,
            creationRef: "new_item_location_atomic_reward",
            materializationId: "mat_item_location_atomic_reward");
        items = items.DeepClone().AsObject();
        items["UpdateInventory"] = new JsonArray(rawItem);
        await context.WriteJsonAsync(InventoryEquipmentService.ItemsPath, items);

        factions = factions.DeepClone().AsObject();
        factions["factionDataChanges"] = new JsonArray(
            new JsonObject
            {
                ["factionId"] = factionId,
                ["currentAgenda"] = "Засвидетельствовать безопасный атомарный переход."
            });
        await context.WriteJsonAsync(FactionCoreChangesContract.FactionCorePath, factions);

        npcRoot = npcRoot.DeepClone().AsObject();
        npcRoot[NpcCoreChangesContract.PropertyName] = new JsonArray(
            new JsonObject
            {
                ["NPCId"] = npcId,
                ["reason"] = "Персонаж засвидетельствовал переход.",
                ["profile"] = new JsonObject
                {
                    ["worldview"] = "Надёжность доказывается полным откатом каждого файла."
                }
            });
        await context.WriteJsonAsync(NpcCoreChangesContract.NpcCorePath, npcRoot);

        return baseline.Keys.ToDictionary(
            static path => path,
            static path => $"game_state/control/pending_turn_snapshot/{path}",
            StringComparer.Ordinal);
    }

    private static JsonObject CreateBootstrapLocation(
        JsonObject scaffold,
        string reservationName,
        string materializationId)
    {
        var reservation = scaffold[reservationName]!.AsObject();
        var initialId = reservation["initialId"]!.GetValue<string>();
        var location = MortalLocationTestFixture.CreateRawLocation(
            reservation["route"]!.GetValue<string>());
        location["initialId"] = initialId;
        location["coordinates"] = reservation["coordinates"]!.DeepClone();
        location["materialization"]!["initialId"] = initialId;
        location["materialization"]!["materializationId"] = materializationId;
        location["materialization"]!["sourceTurn"] = scaffold["turnNumber"]!.DeepClone();
        location["materialization"]!["sourceAuthority"] =
            scaffold["sourceAuthority"]!.DeepClone();
        location["materialization"]!["sections"]!["topology"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        return location;
    }

    private static JsonObject CreateBootstrapLink(
        JsonObject scaffold,
        string materializationId)
    {
        var reservation = scaffold["linkReservation"]!.AsObject();
        var link = MortalLocationTestFixture.CreateRawLink(
            "source_placeholder",
            "target_placeholder");
        link["initialId"] = reservation["initialId"]!.DeepClone();
        link["sourceLocationId"] = null;
        link["sourceInitialId"] = reservation["sourceInitialId"]!.DeepClone();
        link["targetLocationId"] = null;
        link["targetInitialId"] = reservation["targetInitialId"]!.DeepClone();
        link["materialization"]!["initialId"] = reservation["initialId"]!.DeepClone();
        link["materialization"]!["materializationId"] = materializationId;
        link["materialization"]!["sourceTurn"] = scaffold["turnNumber"]!.DeepClone();
        link["materialization"]!["sourceAuthority"] =
            scaffold["sourceAuthority"]!.DeepClone();
        return link;
    }

    private static JsonObject CreateAtomicThreat() =>
        new()
        {
            ["threatId"] = null,
            ["name"] = "Атомарная угроза у брода",
            ["description"] = "Угроза доказывает rollback governed-команд.",
            ["intensity"] = 2,
            ["longTermGoal"] = "Перекрыть дальний берег.",
            ["currentActivity"] = null,
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
                ["primaryTargetName"] = "Дальний берег",
                ["primaryImpact"] = "Stability",
                ["baseImpactValue"] = 1
            }
        };

    private static async Task WriteUnrelatedTrackedSentinelsAsync(
        MortalLocationMaterializationTestContext context)
    {
        await context.WriteJsonAsync(
            "game_state/npcs/npc_core.json",
            new JsonObject { ["NPCs"] = new JsonArray() });
        await context.WriteJsonAsync(
            "game_state/factions/faction_core.json",
            new JsonObject { ["factions"] = new JsonArray() });
        await context.WriteJsonAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(),
                ["equippedItems"] = new JsonObject()
            });
        await context.WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot());
    }

    private static async Task WriteCanonicalItemAsync(
        MortalLocationMaterializationTestContext context,
        JsonObject item)
    {
        await context.WriteJsonAsync(
            InventoryEquipmentService.ItemsPath,
            new JsonObject
            {
                ["items"] = new JsonArray(item.DeepClone()),
                ["equippedItems"] = new JsonObject()
            });
        await context.WriteJsonAsync(
            MortalItemIdentityState.StatePath,
            MortalItemTestFixture.CreateIndex(item));
    }
}
