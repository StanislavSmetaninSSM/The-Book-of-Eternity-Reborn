using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

// The negative oracle consumes the existing DEBUG-only protected-rejection diagnostic.
#if DEBUG
public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CombinedMortalBirth_RealTurnPreservesHistoricalActorAndPublishesOnlyAdmittedCandidate(bool unresolvedEffect)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        const string currentPath = "game_state/world/current_location.json";
        const string mapPath = "game_state/world/world_map.json";
        const string npcPath = "game_state/npcs/npc_core.json";
        const string journalsPath = "game_state/npcs/npc_journals.json";
        const string sceneRef = "locref_council_supply_court_42";
        const string storageId = "storage_council_materials";
        const string actorRef = "npc_council_store_representative";
        var before = new Dictionary<string, byte[]?>();
        JsonNode? mira = null;
        JsonNode? originalMap = null;
        TurnRequest? dispatched = null;
        string? snapshot = null;
        var calls = 0;
        GameEngineTestRuntime? runtime = null;
        runtime = await CreateMortalLivingWorldJourneyAsync(async request =>
        {
            Assert.Equal(1, ++calls);
            dispatched = request;
            Assert.Equal(42, request.TurnNumber);
            snapshot = (await ReadCombinedJsonAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))["manifestPayloadHash"]!.GetValue<string>();
            foreach (var path in CanonicalStateNormalizer.NormalizerRollbackTrackedFiles.Concat(new[]
                     { currentPath, mapPath, npcPath, journalsPath, StorageTransportMoveService.VehiclesPath,
                         MortalLivingWorldSource.WorldTimePath, "game_state/inventory/items.json", "game_state/factions/faction_core.json", "game_state/factions/faction_projects.json" }).Distinct(StringComparer.Ordinal))
                before[path] = await _fs.ReadFileBytesAsync(path);
            var core = (await ReadCombinedJsonAsync(npcPath)).AsObject();
            mira = core["NPCsInScene"]![0]!.DeepClone();
            originalMap = await ReadCombinedJsonAsync(mapPath);
            var owner = new JsonObject { ["ownerType"] = "Faction", ["ownerId"] = "faction_journey_council", ["ownerName"] = "Crossing Council" };
            var scene = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
            scene["initialId"] = sceneRef;
            scene["materialization"]!["initialId"] = sceneRef;
            scene["name"] = "Council supply court"; scene["displayName"] = "Council supply court";
            scene["activeEffectDefinitions"] = unresolvedEffect
                ? new JsonArray(EffectMaterializationTestFixture.CreateDefinition()) : new JsonArray();
            var chalk = MortalItemTestFixture.CreateRawRoot("storage_placement", "location_storage", sceneRef + ":" + storageId,
                creationRef: "new_council_chalk", materializationId: "mat_council_chalk_42");
            chalk["name"] = "One piece of chalk"; chalk["weight"] = 0.02; chalk["volume"] = 0.01;
            chalk["resourceMaterialization"] = CombinedResource("durability");
            var storage = MortalLocationTestFixture.CreateStorageMetadata(storageId, "Council locked supply chest", false, new JsonArray(chalk));
            storage["owner"] = owner.DeepClone(); storage["capacity"] = 12; storage["volume"] = 80;
            scene["locationStorages"] = new JsonArray(storage);
            foreach (var section in new[] { "storageMetadata", "topology" })
                scene["materialization"]!["sections"]![section] = new JsonObject { ["disposition"] = "populated", ["reason"] = null };
            await _fs.WriteFileAtomicAsync(currentPath, new JsonObject { ["currentLocationData"] = scene }.ToJsonString());
            var map = originalMap.DeepClone();
            var newLinks = new JsonArray();
            for (var direction = 0; direction != 2; ++direction)
            {
                var link = MortalLocationTestFixture.CreateRawLink(JourneyLocationId, JourneyLocationId);
                var linkRef = "linkref_council_supply_" + direction;
                link["initialId"] = linkRef;
                link["materialization"]!["initialId"] = linkRef;
                link["materialization"]!["materializationId"] = "mat_council_supply_link_" + direction;
                link[direction == 0 ? "targetLocationId" : "sourceLocationId"] = null;
                link[direction == 0 ? "targetInitialId" : "sourceInitialId"] = sceneRef;
                newLinks.Add(link);
            }
            map["worldMapUpdates"] = new JsonObject { ["newLinks"] = newLinks };
            Assert.True(JsonNode.DeepEquals(originalMap["links"], map["links"]));
            await _fs.WriteFileAtomicAsync(mapPath, map.ToJsonString());
            var vehicles = await ReadCombinedJsonAsync(StorageTransportMoveService.VehiclesPath);
            Assert.Empty(vehicles["birthReceipts"]!.AsArray());
            vehicles["UpdateVehicles"] = new JsonArray(new JsonObject
            {
                ["vehicleRef"] = "council_handcart_42", ["name"] = "Council handcart", ["description"] = "An empty two wheeled handcart beside the council supply chest.",
                ["image_prompt"] = "Plain wooden two wheeled handcart in a supply courtyard", ["type"] = "Vehicle", ["isSentient"] = false,
                ["availability"] = "Parked", ["currentLocationId"] = sceneRef, ["speedBonus"] = 0,
                ["actions"] = new JsonArray(), ["resistances"] = new JsonArray(), ["inventory"] = new JsonArray(),
                ["capacity"] = 12, ["volume"] = 200, ["owner"] = owner.DeepClone(), ["controller"] = null,
                ["authorizedUsers"] = new JsonArray(), ["hasFullAccess"] = false, ["resourceMaterialization"] = CombinedResource("health")
            });
            await _fs.WriteFileAtomicAsync(StorageTransportMoveService.VehiclesPath, vehicles.ToJsonString());
            var actor = EffectMaterializationTestFixture.CreateSameTurnMortalActor(actorRef);
            actor["name"] = "Council supply steward";
            // Same characteristic vocabulary as the actual seed; values are explicit authored
            // steward DATA, not copied player values or a change to world's definitions.
            var worldCharacteristics = (await ReadCombinedJsonAsync("game_state/misc/characteristics.json")).AsObject();
            Assert.Equal(12, worldCharacteristics.Count);
            actor["characteristics"] = new JsonObject(worldCharacteristics.Select(pair =>
                new KeyValuePair<string, JsonNode?>(pair.Key, JsonValue.Create(5))));
            actor["progressionProfile"] = new JsonObject { ["schemaVersion"] = 1, ["mode"] = "none" };
            actor["experience"] = 0; actor["experienceForNextLevel"] = 0;
            actor["teacherProfile"] = new JsonObject { ["canTeach"] = false, ["skills"] = new JsonArray() };
            actor["materialization"]!["capabilities"]!["canTeach"] = false;
            actor["currentLocationId"] = null; actor["initialLocationId"] = sceneRef; actor["currentLocationName"] = "Council supply court";
            actor["resourceMaterialization"] = CombinedResource("health");
            core["UpdateNPCs"] = new JsonArray(actor);
            await _fs.WriteFileAtomicAsync(npcPath, core.ToJsonString());
            var journals = await ReadCombinedJsonAsync(journalsPath);
            journals["NPCJournals"]!.AsArray().Add(new JsonObject
            {
                ["npcId"] = actorRef, ["npcName"] = "Council supply steward", ["lastJournalNote"] = "I explained council ownership without granting access.",
                ["journalEntries"] = new JsonArray(new JsonObject { ["entryId"] = "steward_inspection_42", ["timestamp"] = DateTime.UtcNow.ToString("O"),
                    ["event"] = "Supply inspection", ["description"] = "I heard the visitor ask permission before touching our supplies.", ["emotionalImpact"] = "cautious trust", ["relationshipChange"] = "0" })
            });
            await _fs.WriteFileAtomicAsync(journalsPath, journals.ToJsonString());
            var time = await ReadCombinedJsonAsync(MortalLivingWorldSource.WorldTimePath);
            time["currentTimeInMinutes"] = 500; time["timeOfDay"] = "08:20";
            await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, time.ToJsonString());
            await WriteMortalLivingWorldJourneyOutputAsync(request,
                "Вы осматриваете двор, телегу и закрытый ящик совета. Распорядитель объясняет порядок выдачи. Вы ничего не берёте.",
                [currentPath, mapPath, npcPath, journalsPath, StorageTransportMoveService.VehiclesPath, MortalLivingWorldSource.WorldTimePath],
                """
                ## NPC Scope
                - Mode: Scene-local
                - Relevant actors: Council supply steward
                - Why relevant: Present in the supply court.
                - Actors outside scope: Mira the scout
                - Why outside scope: Remains in the original chambers; neither her activity nor journal changes.

                ## Actor Brain
                ### Council supply steward
                - Current location: locref_council_supply_court_42; present in the new courtyard.
                - Situation: The visitor asks to inspect the cart and chest without taking anything.
                - Profile inputs: Observant, cautious and consistent; no combat, trade or teaching capability.
                - Motivation: Keep council supplies accounted for.
                - Constraints: Inspection is not permission to take or move council property.
                - Thoughts: I can explain ownership and invite a concrete request before any future issuance.
                - Strategy options:
                1. Explain the access restrictions. Benefit: clarity. Risk: the visitor may be disappointed.
                2. Give away supplies immediately. Benefit: convenience. Risk: violates council ownership.
                - Chosen strategy: Explain restrictions without transferring property.
                - Rejected alternatives: Unrequested transfer would violate the council's authority.
                - Actions: Show the cart and locked chest; no item movement or access grant.
                - State changes: Only NPCJournals[].journalEntries[] receives my first observation; no relationship, skill or activity change.
                """);
        }, SeedMortalLivingWorldJourneyActorsAsync);
        // Ordinary rollback restores the image before staging, not the scheduler image
        // signed later at TerminalWaitStarted. Keep that exact byte expectation separately.
        var preStagingSchedule = await _fs.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath);
        // This existing collection has DisableParallelization=true: no other test in this
        // process overlaps the temporary diagnostic sink/nonce. Restore both even on failure.
        var artifact = Path.Combine(TestDiagnosticPaths.Root, "combined-mortal-birth", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(artifact);
        var nonce = Guid.NewGuid().ToString("N");
        const string nonceVariable = "BOE_TEST_GAME_LOOP_FAILURE_NONCE";
        var originalNonce = Environment.GetEnvironmentVariable(nonceVariable);
        var originalError = Console.Error;
        using var capturedError = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            Environment.SetEnvironmentVariable(nonceVariable, nonce);
            Console.SetError(TextWriter.Synchronized(new CombinedBirthDiagnosticTee(originalError, capturedError)));
            await runtime.Engine.ProcessPlayerTurn("Осмотреть двор, телегу и кладовую совета с распорядителем, ничего не брать без разрешения.");
        }
        finally
        {
            Console.SetError(originalError);
            Environment.SetEnvironmentVariable(nonceVariable, originalNonce);
            await File.WriteAllTextAsync(Path.Combine(artifact, "stderr.txt"), capturedError.ToString());
        }
        Assert.Equal(1, calls);
        await File.WriteAllTextAsync(Path.Combine(artifact, "logger.txt"), _journeyLogger.Describe());
        await File.WriteAllTextAsync(Path.Combine(artifact, "canonical-failure.json"),
            await _fs.ReadFileAsync("game_state/control/validation_diagnostic_failure_report.json") ?? "null");
        _directGachaOutput?.WriteLine("Combined candidate evidence: " + artifact);
        var refusalRecords = capturedError.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim()).Where(line => line.StartsWith("{", StringComparison.Ordinal))
            .Select(line => JsonNode.Parse(line)!)
            .Where(record => record["kind"]?.GetValue<string>() == "boe-game-loop-fixture-rejection" &&
                             record["nonce"]?.GetValue<string>() == nonce).ToArray();
        if (unresolvedEffect)
        {
            var refusal = Assert.Single(refusalRecords);
            Assert.Equal("protected-materialization", refusal["stage"]!.GetValue<string>());
            Assert.True(refusal["flags"]!["effect"]!.GetValue<bool>());
            Assert.True(refusal["rollbackAvailable"]!.GetValue<bool>());
            Assert.Contains(refusal["errors"]!.AsArray(), issue =>
                issue!["code"]?.GetValue<string>() == "effect_source_authority_invalid_owner_identity" &&
                issue["filePath"]?.GetValue<string>() == currentPath + ".activeEffectDefinitions");
            Assert.Equal(41, runtime.GameLoop.TurnNumber);
            foreach (var pair in before)
            {
                var expected = pair.Key == ProgressionScheduleService.SchedulePath ? preStagingSchedule : pair.Value;
                var actual = await _fs.ReadFileBytesAsync(pair.Key);
                if (!(expected is null ? actual is null : actual is not null && expected.AsSpan().SequenceEqual(actual)))
                {
                    var stem = pair.Key.Replace('/', '_');
                    if (expected is not null) await File.WriteAllBytesAsync(Path.Combine(artifact, stem + ".expected"), expected);
                    if (actual is not null) await File.WriteAllBytesAsync(Path.Combine(artifact, stem + ".actual"), actual);
                    Assert.Fail("Rollback bytes differ: " + pair.Key + "; full images retained at " + artifact);
                }
            }
            var rejectedStory = await _fs.ReadFileAsync("stories/mortal_life_2.jsonl");
            Assert.True(string.IsNullOrWhiteSpace(rejectedStory));
            return;
        }
        Assert.Empty(refusalRecords);
        Assert.True(runtime.GameLoop.TurnNumber == 42, _journeyLogger.Describe());
        var current = await ReadCombinedJsonAsync(currentPath);
        var mapAfter = await ReadCombinedJsonAsync(mapPath);
        var locationId = current["locationId"]!.GetValue<string>();
        var location = Assert.Single(mapAfter["locations"]!.AsArray(), row => row!["locationId"]?.GetValue<string>() == locationId)!;
        Assert.Equal(42, location["materializationReceipt"]!["sourceTurn"]!.GetValue<int>());
        Assert.Equal(originalMap!["locations"]!.AsArray().Count + 1, mapAfter["locations"]!.AsArray().Count);
        Assert.Equal(originalMap["links"]!.AsArray().Count + 2, mapAfter["links"]!.AsArray().Count);
        foreach (var old in originalMap["locations"]!.AsArray())
            Assert.True(JsonNode.DeepEquals(old, mapAfter["locations"]!.AsArray().Single(row => row!["locationId"]!.GetValue<string>() == old!["locationId"]!.GetValue<string>())));
        var finalCore = await ReadCombinedJsonAsync(npcPath);
        var retained = finalCore["NPCsInScene"]!.AsArray().Single(row => row!["NPCId"]?.GetValue<string>() == JourneyScoutId)!;
        Assert.True(JsonNode.DeepEquals(mira, retained));
        var scope = await new LocalInteractionScopeService(_fs).ResolveAsync("Mortal World");
        Assert.False(LocalInteractionScopeService.IsMortalActorLocal(scope, retained.AsObject()));
        // Full actors may remain in either canonical carrier; only creation commands
        // and one-time envelopes are consumed, not the canonical UpdateNPCs array.
        var steward = Assert.Single(GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections
            .SelectMany(section => (finalCore[section] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            .Where(row => row["NPCId"]?.GetValue<string>() != JourneyScoutId));
        var stewardId = steward["NPCId"]!.GetValue<string>();
        Assert.Equal(locationId, steward["currentLocationId"]!.GetValue<string>());
        Assert.True(LocalInteractionScopeService.IsMortalActorLocal(scope, steward.AsObject()));
        Assert.Null(steward["resourceMaterialization"]);
        var cartRoot = await ReadCombinedJsonAsync(StorageTransportMoveService.VehiclesPath);
        var cart = Assert.Single(cartRoot["vehicles"]!.AsArray())!;
        var receipt = Assert.Single(cartRoot["birthReceipts"]!.AsArray())!;
        Assert.Equal(locationId, cart["currentLocationId"]!.GetValue<string>());
        Assert.Equal(cart["vehicleId"]!.GetValue<string>(), receipt["vehicleId"]!.GetValue<string>());
        Assert.Equal(42, receipt["sourceTurn"]!.GetValue<int>());
        Assert.Equal(dispatched!.RequestId, receipt["sourceAuthority"]!["requestId"]!.GetValue<string>());
        Assert.Equal(dispatched.SessionId, receipt["sourceAuthority"]!["sessionId"]!.GetValue<string>());
        Assert.Equal(snapshot, receipt["sourceAuthority"]!["snapshotToken"]!.GetValue<string>());
        Assert.False(cart["hasFullAccess"]!.GetValue<bool>());
        Assert.Null(cart["resourceMaterialization"]); Assert.Null(cartRoot["UpdateVehicles"]);
        var health = Assert.Single(receipt["resourceInitialization"]!.AsArray())!;
        Assert.Equal("health", health["resourceKey"]!.GetValue<string>()); Assert.Equal(100, health["maximum"]!.GetValue<int>());
        var projectedStorage = Assert.Single(current["locationStorages"]!.AsArray())!;
        Assert.False(projectedStorage["hasFullAccess"]!.GetValue<bool>());
        var acceptedChalk = Assert.Single(projectedStorage["contents"]!.AsArray())!;
        Assert.NotNull(acceptedChalk["itemId"]); Assert.NotNull(acceptedChalk["materializationReceipt"]);
        Assert.Null(acceptedChalk["resourceMaterialization"]);
        Assert.Equal(1, acceptedChalk["count"]!.GetValue<int>());
        var itemId = acceptedChalk["itemId"]!.GetValue<string>();
        var resourceState = await ReadCombinedJsonAsync(ResourceMaterializationContract.StatePath);
        foreach (var pair in new[] { (stewardId, "health"), (cart["vehicleId"]!.GetValue<string>(), "health"), (itemId, "durability") })
        {
            var entry = Assert.Single(resourceState["entries"]!.AsArray(), row =>
                row!["resourceOwnerId"]?.GetValue<string>() == pair.Item1 && row["resourceKey"]?.GetValue<string>() == pair.Item2)!;
            Assert.Equal(100m, entry["current"]!.GetValue<decimal>());
            Assert.Equal(100m, entry["maximum"]!.GetValue<decimal>());
            Assert.Equal(42, entry["chronology"]!["createdAtTurn"]!.GetValue<int>());
        }
        foreach (var path in new[] { "game_state/inventory/items.json", "game_state/factions/faction_core.json", "game_state/factions/faction_projects.json" })
            if (before.TryGetValue(path, out var original)) Assert.Equal(original, await _fs.ReadFileBytesAsync(path));
        Assert.Null(mapAfter["worldMapUpdates"]);
        foreach (var oldLink in originalMap["links"]!.AsArray())
            Assert.True(JsonNode.DeepEquals(oldLink, mapAfter["links"]!.AsArray().Single(row =>
                row!["linkId"]!.GetValue<string>() == oldLink!["linkId"]!.GetValue<string>())));
        var createdLinks = mapAfter["links"]!.AsArray().Where(row =>
            row!["materializationReceipt"]?["initialId"]?.GetValue<string>()?.StartsWith("linkref_council_supply_", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(2, createdLinks.Length);
        Assert.Single(createdLinks, row => row!["sourceLocationId"]!.GetValue<string>() == JourneyLocationId &&
            row["targetLocationId"]!.GetValue<string>() == locationId);
        Assert.Single(createdLinks, row => row!["sourceLocationId"]!.GetValue<string>() == locationId &&
            row["targetLocationId"]!.GetValue<string>() == JourneyLocationId);
        foreach (var link in createdLinks)
        {
            Assert.NotNull(link!["linkId"]);
            Assert.Equal(42, link["materializationReceipt"]!["sourceTurn"]!.GetValue<int>());
            Assert.Contains(locationId, new[] { link["sourceLocationId"]!.GetValue<string>(), link["targetLocationId"]!.GetValue<string>() });
        }
        var moves = await StorageTransportMoveService.ReadStorageMoveContextAsync(_fs);
        Assert.True(moves.Success, moves.Message);
        Assert.DoesNotContain(moves.Storages, storage => storage.StorageId == storageId);
        Assert.Single((await _fs.ReadFileAsync("stories/mortal_life_2.jsonl"))!.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.False(_fs.FileExists(LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        Assert.False(_fs.FileExists("game_state/control/validation_repair_request.json"));
        Assert.False(_fs.FileExists("game_state/control/validation_diagnostic_failure_report.json"));
    }

    /// <summary>Tees the existing diagnostic stream without replacing the production emitter or limiting captured evidence.</summary>
    private sealed class CombinedBirthDiagnosticTee(TextWriter original, TextWriter captured) : TextWriter
    {
        /// <inheritdoc/>
        public override System.Text.Encoding Encoding => original.Encoding;
        /// <inheritdoc/>
        public override void Write(char value) { original.Write(value); captured.Write(value); }
        /// <inheritdoc/>
        public override void Write(string? value) { original.Write(value); captured.Write(value); }
        /// <inheritdoc/>
        public override void WriteLine(string? value) { original.WriteLine(value); captured.WriteLine(value); }
        /// <inheritdoc/>
        public override void Flush() { original.Flush(); captured.Flush(); }
    }

    /// <summary>Creates authored birth capacity only; current values and receipts remain client-owned.</summary>
    private static JsonObject CombinedResource(string key) => new()
    { ["resources"] = new JsonArray(new JsonObject { ["resourceKey"] = key, ["maximum"] = 100 }) };

    /// <summary>Reads current fixture DATA without manufacturing a pending source or publication permission.</summary>
    private async Task<JsonNode> ReadCombinedJsonAsync(string path) => JsonNode.Parse((await _fs.ReadFileAsync(path))!)!;
}

#endif
