using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class FileSystemExampleFixtureIntegrityTests
{
    [Fact]
    public void RealmSaveFixtures_RemainFarBelowTrustedArchiveBudgets()
    {
        var fixtureNames = new[]
        {
            "mortal_world_command_display_fixture.zip",
            "chaos_sea_command_display_fixture.zip",
            "shining_abode_command_display_fixture.zip"
        };
        var observed = new List<(
            int EntryCount,
            long ExpandedBytes,
            long LargestEntryBytes,
            long NameUtf8Bytes)>();
        foreach (var fixtureName in fixtureNames)
        {
            var fixturePath = Path.Combine(
                TestRepoPaths.BaseSessionRoot,
                "saves",
                "manual_saves",
                fixtureName);
            using var archive = ZipFile.OpenRead(fixturePath);
            var descriptors = archive.Entries
                .Select(entry =>
                    new SaveLoadService.SaveArchiveEntryDescriptor(
                        entry.FullName,
                        string.IsNullOrEmpty(entry.Name),
                        entry.Length,
                        entry.CompressedLength))
                .ToArray();

            SaveLoadService.ValidateTrustedArchiveBudget(descriptors);
            var files = descriptors
                .Where(entry => !entry.IsDirectory)
                .ToArray();
            observed.Add(
                (
                    descriptors.Length,
                    files.Sum(entry => entry.Length),
                    files.Max(entry => entry.Length),
                    descriptors.Sum(entry =>
                        (long)Encoding.UTF8.GetByteCount(entry.Path))));
        }

        Assert.Equal(100, observed.Max(item => item.EntryCount));
        Assert.Equal(324_297, observed.Max(item => item.ExpandedBytes));
        Assert.Equal(61_375, observed.Max(item => item.LargestEntryBytes));
        Assert.Equal(3_552, observed.Max(item => item.NameUtf8Bytes));

        var budget = SaveLoadService.TrustedArchiveBudget;
        Assert.True(
            observed.Max(item => item.EntryCount) <
            budget.MaxEntryCount);
        Assert.True(
            observed.Max(item => item.ExpandedBytes) <
            budget.MaxTotalExpandedBytes);
        Assert.True(
            observed.Max(item => item.LargestEntryBytes) <
            budget.MaxEntryExpandedBytes);
        Assert.True(
            observed.Max(item => item.NameUtf8Bytes) <
            budget.MaxTotalEntryNameUtf8Bytes);
    }

    [Fact]
    public void GameSessionFixtureJsonFiles_AreNonEmptyAndParseable()
    {
        var jsonFiles = Directory
            .EnumerateFiles(TestRepoPaths.BaseSessionRoot, "*.json", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(jsonFiles);

        var invalidFiles = new List<string>();
        foreach (var jsonFile in jsonFiles)
        {
            var content = File.ReadAllText(jsonFile);
            if (string.IsNullOrWhiteSpace(content))
            {
                invalidFiles.Add($"{ToFixtureRelativePath(jsonFile)}: empty file");
                continue;
            }

            try
            {
                using var _ = JsonDocument.Parse(content);
            }
            catch (JsonException ex)
            {
                invalidFiles.Add($"{ToFixtureRelativePath(jsonFile)}: {ex.Message}");
            }
        }

        Assert.True(
            invalidFiles.Count == 0,
            "FileSystemExample/game_session must not contain empty or malformed JSON files. Invalid files:" +
            Environment.NewLine + string.Join(Environment.NewLine, invalidFiles));
    }

    [Fact]
    public void GameSessionFixture_DoesNotTrackStalePendingTurnSnapshots()
    {
        var pendingSnapshotArtifacts = Directory
            .EnumerateFileSystemEntries(TestRepoPaths.BaseSessionRoot, "pending_turn_snapshot*", SearchOption.AllDirectories)
            .Select(ToFixtureRelativePath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            pendingSnapshotArtifacts.Length == 0,
            "FileSystemExample/game_session must remain free of stale pending_turn_snapshot artifacts. Found:" +
            Environment.NewLine + string.Join(Environment.NewLine, pendingSnapshotArtifacts));
    }

    [Fact]
    public void GameSessionFixture_DoesNotContainStaleTurnSignals()
    {
        var staleTurnSignals = new[]
            {
                "input/turn_request.json",
                "ready/turn_complete.json",
                "ready/turn_error.json"
            }
            .Where(relativePath => File.Exists(Path.Combine(TestRepoPaths.BaseSessionRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();

        Assert.True(
            staleTurnSignals.Length == 0,
            "FileSystemExample/game_session must not contain stale live-turn input/ready signals. Found:" +
            Environment.NewLine + string.Join(Environment.NewLine, staleTurnSignals));
    }

    [Fact]
    public void GameSessionFixture_DoesNotContainPendingNextLifeSetup()
    {
        var staleNextLifeSetup = new[]
            {
                "game_state/control/incarnation_world_setup.json",
                "game_state/control/next_life_scenario_core.json",
                "game_state/control/archive_candidate_manifest.json",
                "game_state/control/guardian_corrections.json",
                "lore/current_world/world_directives.json"
            }
            .Where(relativePath => File.Exists(Path.Combine(TestRepoPaths.BaseSessionRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();

        Assert.True(
            staleNextLifeSetup.Length == 0,
            "FileSystemExample/game_session is an active mortal-world fixture and must not carry pending next-life setup/control surfaces. Found:" +
            Environment.NewLine + string.Join(Environment.NewLine, staleNextLifeSetup));
    }

    [Fact]
    public void GameSessionFixture_AfterlifeArchiveEntriesDeclareSourceLife()
    {
        var soulStatePath = Path.Combine(TestRepoPaths.BaseSessionRoot, "game_state", "meta", "soul_state.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(soulStatePath));
        if (!doc.RootElement.TryGetProperty("afterlifeArchive", out var archive) ||
            !archive.TryGetProperty("stored", out var stored) ||
            stored.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var invalidEntries = stored
            .EnumerateArray()
            .Select((entry, index) => new { Entry = entry, Index = index })
            .Where(item =>
                item.Entry.ValueKind != JsonValueKind.Object ||
                !item.Entry.TryGetProperty("sourceLife", out var sourceLife) ||
                sourceLife.ValueKind != JsonValueKind.Number ||
                !sourceLife.TryGetInt32(out var parsedSourceLife) ||
                parsedSourceLife < 0)
            .Select(item =>
                item.Entry.ValueKind == JsonValueKind.Object &&
                item.Entry.TryGetProperty("archiveId", out var archiveId) &&
                archiveId.ValueKind == JsonValueKind.String
                    ? archiveId.GetString() ?? $"stored[{item.Index}]"
                    : $"stored[{item.Index}]")
            .ToArray();

        Assert.True(
            invalidEntries.Length == 0,
            "FileSystemExample afterlifeArchive.stored entries must declare numeric sourceLife. Invalid entries:" +
            Environment.NewLine + string.Join(Environment.NewLine, invalidEntries));
    }

    [Fact]
    public void GameSessionFixture_InventoryUsesCanonicalEquipmentSlots()
    {
        var inventoryPath = Path.Combine(TestRepoPaths.BaseSessionRoot, "game_state", "inventory", "items.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(inventoryPath));
        var equippedItems = doc.RootElement.GetProperty("equippedItems");
        var allowedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Head", "Chest", "Legs", "Feet", "Hands", "Wrists", "Neck", "Waist", "Back",
            "Finger1", "Finger2", "MainHand", "OffHand",
            "Underwear_Top", "Underwear_Bottom",
            "Accessory1", "Accessory2", "Accessory3", "Accessory4"
        };
        var invalidSlots = equippedItems
            .EnumerateObject()
            .Select(property => property.Name)
            .Where(slot => !allowedSlots.Contains(slot))
            .ToArray();

        Assert.True(
            invalidSlots.Length == 0,
            "FileSystemExample inventory equippedItems must use canonical slot names. Invalid:" +
            Environment.NewLine + string.Join(Environment.NewLine, invalidSlots));
    }

    [Fact]
    public void GameSessionFixture_MortalItemsUseCurrentMaterializationAndIdentityIndex()
    {
        var inventoryPath = Path.Combine(TestRepoPaths.BaseSessionRoot, "game_state", "inventory", "items.json");
        var indexPath = Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "game_state",
            "inventory",
            "item_identity_index.json");
        Assert.True(File.Exists(indexPath), "Current Mortal item fixtures require item_identity_index.json.");

        using var inventoryDoc = JsonDocument.Parse(File.ReadAllText(inventoryPath));
        var items = inventoryDoc.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.NotEmpty(items);
        foreach (var item in items)
        {
            var issues = MortalItemMaterializationContract.Validate(
                item,
                "FileSystemExample inventory item",
                MortalItemMaterializationPhase.CanonicalPostSeal);
            Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues.Select(issue => issue.Message)));
        }

        var index = MortalItemIdentityState.Parse(File.ReadAllText(indexPath));
        Assert.Empty(index.Issues);
        Assert.Equal(items.Length, index.EntriesByItemId.Count);
        foreach (var item in items)
        {
            Assert.True(MortalItemMaterializationContract.TryReadAcceptedIdentity(item, out var itemId));
            Assert.True(index.EntriesByItemId.TryGetValue(itemId, out var entry));
            Assert.Equal(
                item.GetProperty("materializationReceipt").GetProperty("receiptId").GetString(),
                entry!["receiptId"]!.GetValue<string>());
            Assert.Equal("active", entry["state"]!.GetValue<string>());
            Assert.Equal("player_inventory", entry["currentCarrier"]!["kind"]!.GetValue<string>());
        }
    }

    [Fact]
    public void MortalCommandDisplaySaveFixture_MortalItemsUseCurrentMaterializationAndIdentityIndex()
    {
        var fixturePath = Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "saves",
            "manual_saves",
            "mortal_world_command_display_fixture.zip");
        using var archive = ZipFile.OpenRead(fixturePath);
        var inventory = ReadArchiveObject(archive, "game_state/inventory/items.json");
        var npcCore = ReadArchiveObject(archive, "game_state/npcs/npc_core.json");
        var npcCommands = ReadArchiveObject(archive, "game_state/npcs/npc_inventory.json");
        var currentLocation = ReadArchiveObject(archive, "game_state/world/current_location.json");
        var vehicles = ReadArchiveObject(archive, "game_state/misc/vehicles.json");
        var indexRoot = ReadArchiveObject(archive, MortalItemIdentityState.StatePath);

        Assert.False(inventory.ContainsKey("equipment"));
        Assert.IsType<JsonObject>(inventory["equippedItems"]);
        Assert.Empty(npcCommands["NPCInventoryAdds"]?.AsArray() ?? new JsonArray());

        var catalog = MortalItemCarrierCatalog.Build(new MortalItemCarrierCatalogInput(
            inventory,
            npcCore,
            npcCommands,
            currentLocation,
            vehicles,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal)));
        Assert.Empty(catalog.Issues);
        Assert.Equal(15, catalog.Occurrences.Count);

        var index = MortalItemIdentityState.Parse(indexRoot.ToJsonString());
        Assert.Empty(index.Issues);
        Assert.Equal(catalog.Occurrences.Count, index.EntriesByItemId.Count);

        foreach (var occurrence in catalog.Occurrences)
        {
            Assert.True(
                MortalItemMaterializationContract.TryReadAcceptedIdentity(occurrence.Item, out var itemId),
                $"{occurrence.JsonPath} must be a receipt-bearing accepted item.");
            using var itemDocument = JsonDocument.Parse(occurrence.Item.ToJsonString());
            var issues = MortalItemMaterializationContract.Validate(
                itemDocument.RootElement,
                occurrence.JsonPath,
                MortalItemMaterializationPhase.CanonicalPostSeal);
            Assert.True(
                issues.Count == 0,
                string.Join(Environment.NewLine, issues.Select(issue => issue.Message)));

            Assert.True(index.EntriesByItemId.TryGetValue(itemId, out var entry));
            Assert.Equal("active", entry!["state"]!.GetValue<string>());
            Assert.Equal(
                occurrence.Item["materializationReceipt"]!["receiptId"]!.GetValue<string>(),
                entry["receiptId"]!.GetValue<string>());
            var carrier = entry["currentCarrier"]!.AsObject();
            Assert.Equal(occurrence.Carrier.Kind, carrier["kind"]!.GetValue<string>());
            Assert.Equal(occurrence.Carrier.OwnerId, carrier["ownerId"]!.GetValue<string>());
            Assert.Equal(occurrence.Carrier.ContainerId, carrier["containerId"]?.GetValue<string>());
        }
    }

    [Fact]
    public void MortalCommandDisplaySaveFixture_MortalLocationsUseCurrentMaterializationAndIdentityIndex()
    {
        var fixturePath = Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "saves",
            "manual_saves",
            "mortal_world_command_display_fixture.zip");
        using var archive = ZipFile.OpenRead(fixturePath);
        var map = ReadArchiveObject(archive, MortalLocationMaterializationContract.WorldMapPath);
        var current = ReadArchiveObject(archive, MortalLocationMaterializationContract.CurrentLocationPath);
        var indexRoot = ReadArchiveObject(archive, MortalLocationIdentityState.StatePath);

        var locations = map["locations"]?.AsArray() ??
                        throw new InvalidDataException("Reusable Mortal save must contain canonical locations[].");
        var links = map["links"]?.AsArray() ??
                    throw new InvalidDataException("Reusable Mortal save must contain canonical links[].");
        Assert.NotEmpty(locations);

        foreach (var location in locations.OfType<JsonObject>())
        {
            using var document = JsonDocument.Parse(location.ToJsonString());
            Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLocation(
                document.RootElement,
                "reusable Mortal save location"));
        }

        foreach (var link in links.OfType<JsonObject>())
        {
            using var document = JsonDocument.Parse(link.ToJsonString());
            Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLink(
                document.RootElement,
                "reusable Mortal save link"));
        }

        using (var currentDocument = JsonDocument.Parse(current.ToJsonString()))
        {
            Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalCurrentLocation(
                currentDocument.RootElement,
                "reusable Mortal save current location"));
        }

        var currentLocationId = current["locationId"]!.GetValue<string>();
        var mapCurrent = Assert.Single(
            locations.OfType<JsonObject>(),
            location => string.Equals(
                location["locationId"]?.GetValue<string>(),
                currentLocationId,
                StringComparison.Ordinal));
        foreach (var (field, mapValue) in mapCurrent)
        {
            Assert.True(
                current.TryGetPropertyValue(field, out var currentValue) &&
                MortalLocationMaterializationContract.SharedCurrentProjectionValueEquals(
                    field,
                    mapValue,
                    currentValue),
                $"Reusable Mortal save current field '{field}' must match canonical map metadata.");
        }

        var index = MortalLocationIdentityState.Parse(indexRoot);
        Assert.Empty(index.Issues);
        Assert.Empty(index.ValidateCanonicalState(map));
        Assert.True(index.LocationEntriesById.ContainsKey(currentLocationId));
    }

    [Theory]
    [InlineData("fixed")]
    [InlineData("broken")]
    public void ItemBondFateCardFixture_UsesCurrentMaterializationAndMatchingIdentityIndex(string variant)
    {
        var fixtureRoot = Path.Combine(
            TestRepoPaths.ValidatorFixturesRoot,
            "item_bond_fate_card_contract",
            variant);
        var inventoryPath = Path.Combine(fixtureRoot, "items.json");
        var indexPath = Path.Combine(fixtureRoot, "item_identity_index.json");
        Assert.True(File.Exists(indexPath), $"{variant} item bond fixture requires item_identity_index.json.");

        using var inventoryDoc = JsonDocument.Parse(File.ReadAllText(inventoryPath));
        var root = inventoryDoc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.GetProperty("equippedItems").ValueKind);
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        var issues = MortalItemMaterializationContract.Validate(
            item,
            $"item_bond_fate_card_contract/{variant}/items[0]",
            MortalItemMaterializationPhase.CanonicalPostSeal);
        Assert.True(issues.Count == 0, string.Join(Environment.NewLine, issues.Select(issue => issue.Message)));
        Assert.True(MortalItemMaterializationContract.TryReadAcceptedIdentity(item, out var itemId));

        var index = MortalItemIdentityState.Parse(File.ReadAllText(indexPath));
        Assert.Empty(index.Issues);
        var entry = Assert.Single(index.EntriesByItemId);
        Assert.Equal(itemId, entry.Key);
        Assert.Equal(
            item.GetProperty("materializationReceipt").GetProperty("receiptId").GetString(),
            entry.Value["receiptId"]!.GetValue<string>());
        Assert.Equal("active", entry.Value["state"]!.GetValue<string>());
        Assert.Equal("player_inventory", entry.Value["currentCarrier"]!["kind"]!.GetValue<string>());
    }

    [Fact]
    public void GameSessionFixture_ItemJournalsReferenceExistingItems()
    {
        var inventoryPath = Path.Combine(TestRepoPaths.BaseSessionRoot, "game_state", "inventory", "items.json");
        var journalsPath = Path.Combine(TestRepoPaths.BaseSessionRoot, "game_state", "npcs", "item_journals.json");
        using var inventoryDoc = JsonDocument.Parse(File.ReadAllText(inventoryPath));
        using var journalsDoc = JsonDocument.Parse(File.ReadAllText(journalsPath));
        var knownItemIds = inventoryDoc.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object &&
                           item.TryGetProperty("itemId", out var id) &&
                           id.ValueKind == JsonValueKind.String)
            .Select(item => item.GetProperty("itemId").GetString() ?? string.Empty)
            .ToHashSet(StringComparer.Ordinal);
        var unknownItemIds = journalsDoc.RootElement
            .GetProperty("entries")
            .EnumerateArray()
            .Where(entry => entry.ValueKind == JsonValueKind.Object &&
                            entry.TryGetProperty("itemId", out var id) &&
                            id.ValueKind == JsonValueKind.String)
            .Select(entry => entry.GetProperty("itemId").GetString() ?? string.Empty)
            .Where(itemId => !knownItemIds.Contains(itemId))
            .ToArray();

        Assert.True(
            unknownItemIds.Length == 0,
            "FileSystemExample item journals must reference items present in inventory/items.json. Unknown:" +
            Environment.NewLine + string.Join(Environment.NewLine, unknownItemIds));
    }

    [Fact]
    public async Task GameSessionFixture_ValidatorAcceptsCurrentInventoryItemJournalReferences()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "boe-filesystem-fixture-validation-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(rootPath, "game_session"));
            var fs = new FileSystemManager(rootPath, NullLogger<FileSystemManager>.Instance);
            var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);

            var issues = await validator.ValidateGameStateAsync();

            Assert.DoesNotContain(issues, issue =>
                string.Equals(issue.Code, "item_journal_unknown_item_reference", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public async Task GameSessionFixture_ValidatorRejectsLegacySoulRelicAliases()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), "boe-filesystem-fixture-soul-relic-validation-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(TestRepoPaths.BaseSessionRoot, Path.Combine(rootPath, "game_session"));
            var soulStatePath = Path.Combine(rootPath, "game_session", "game_state", "meta", "soul_state.json");
            await File.WriteAllTextAsync(soulStatePath, """
            {
              "soulName": "Пепельная Искра",
              "currentRealm": "Mortal World",
              "currentIncarnation": 2,
              "inkFeathers": { "current": 80, "total": 120 },
              "soulRelics": {
                "equipped": [
                  { "id": "relic-ember-lantern", "name": "Фонарь Угасшего Пламени", "tier": "Uncommon" }
                ],
                "stored": []
              }
            }
            """);

            var fs = new FileSystemManager(rootPath, NullLogger<FileSystemManager>.Instance);
            var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);

            var issues = await validator.ValidateGameStateAsync();

            Assert.Contains(issues, issue =>
                string.Equals(issue.Code, "soul_relic_invalid_canonical_shape", StringComparison.OrdinalIgnoreCase) &&
                issue.FilePath.Contains("game_state/meta/soul_state.json.soulRelics.equipped[0]", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(rootPath))
                Directory.Delete(rootPath, recursive: true);
        }
    }

    [Fact]
    public void GameSessionFixture_InventoryItemsExposePersistedItemContractMinimum()
    {
        var inventoryPath = Path.Combine(TestRepoPaths.BaseSessionRoot, "game_state", "inventory", "items.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(inventoryPath));
        var invalidItems = new List<string>();
        foreach (var item in doc.RootElement.GetProperty("items").EnumerateArray())
        {
            var id = item.TryGetProperty("itemId", out var itemId) && itemId.ValueKind == JsonValueKind.String
                ? itemId.GetString()
                : "<missing itemId>";
            foreach (var requiredString in new[] { "existedId", "name", "description", "image_prompt", "quality", "durability" })
            {
                if (!item.TryGetProperty(requiredString, out var value) ||
                    value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()))
                    invalidItems.Add($"{id}: missing string {requiredString}");
            }

            foreach (var requiredBoolean in new[] { "isContainer", "isConsumption", "requiresTwoHands" })
            {
                if (!item.TryGetProperty(requiredBoolean, out var value) ||
                    value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    invalidItems.Add($"{id}: missing boolean {requiredBoolean}");
            }

            foreach (var requiredNumber in new[] { "price", "count", "weight", "volume" })
            {
                if (!item.TryGetProperty(requiredNumber, out var value) ||
                    value.ValueKind != JsonValueKind.Number)
                    invalidItems.Add($"{id}: missing numeric {requiredNumber}");
            }

            if (!item.TryGetProperty("contentsPath", out var contentsPath) ||
                contentsPath.ValueKind is not (JsonValueKind.Null or JsonValueKind.Array))
                invalidItems.Add($"{id}: missing nullable contentsPath");

            if (!item.TryGetProperty("equipmentSlot", out _))
                invalidItems.Add($"{id}: missing equipmentSlot");
            if (!item.TryGetProperty("accessoryForSlot", out _))
                invalidItems.Add($"{id}: missing accessoryForSlot");
        }

        Assert.True(
            invalidItems.Count == 0,
            "FileSystemExample inventory items must expose persisted item contract fields. Invalid:" +
            Environment.NewLine + string.Join(Environment.NewLine, invalidItems));
    }

    [Fact]
    public void GameSessionFixture_UsesCanonicalCharacterChronicleRoot()
    {
        var chroniclePath = Path.Combine(TestRepoPaths.BaseSessionRoot, "game_state", "meta", "character_chronicle.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(chroniclePath));
        Assert.True(
            doc.RootElement.TryGetProperty("entries", out var entries) &&
            entries.ValueKind == JsonValueKind.Array,
            "FileSystemExample character_chronicle.json must expose canonical top-level entries array.");
        Assert.False(
            doc.RootElement.TryGetProperty("chapters", out _),
            "FileSystemExample character_chronicle.json must not use legacy top-level chapters.");
    }

    [Fact]
    public void GameSessionFixture_HasAchievementsBootstrap()
    {
        var achievementsPath = Path.Combine(TestRepoPaths.BaseSessionRoot, "game_state", "meta", "achievements.json");
        Assert.True(File.Exists(achievementsPath), "FileSystemExample must include game_state/meta/achievements.json bootstrap.");
    }

    [Fact]
    public void GameSessionFixture_HasMortalWorldLoreBootstrap()
    {
        var requiredFiles = new[]
        {
            "lore/codex_entries.json",
            "lore/current_world/geography.json",
            "lore/current_world/history.json",
            "lore/current_world/cultures.json",
            "lore/current_world/threats.json"
        };
        var missingFiles = requiredFiles
            .Where(relativePath => !File.Exists(Path.Combine(TestRepoPaths.BaseSessionRoot, relativePath.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();

        Assert.True(
            missingFiles.Length == 0,
            "FileSystemExample must include mortal-world lore bootstrap files. Missing:" +
            Environment.NewLine + string.Join(Environment.NewLine, missingFiles));
    }

    [Fact]
    public void GameSessionFixture_CurrentLocationExposesCanonicalEventAndWeatherExamples()
    {
        var locationPath = Path.Combine(TestRepoPaths.BaseSessionRoot, "game_state", "world", "current_location.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(locationPath));
        var root = doc.RootElement;

        var lastEvents = root.TryGetProperty("lastEventsDescription", out var eventsNode) &&
                         eventsNode.ValueKind == JsonValueKind.String
            ? eventsNode.GetString() ?? string.Empty
            : string.Empty;

        Assert.StartsWith("#", lastEvents);
        Assert.Contains(" г., ", lastEvents, StringComparison.Ordinal);

        Assert.True(
            root.TryGetProperty("currentWeather", out var weather) &&
            weather.ValueKind == JsonValueKind.Object,
            "FileSystemExample current_location.json must include currentWeather with GM-safe shape.");
        Assert.True(
            weather.TryGetProperty("description", out var description) &&
            description.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(description.GetString()),
            "currentWeather.description must be a non-empty player-facing string.");
        Assert.True(
            weather.TryGetProperty("tendency", out var tendency) &&
            tendency.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(tendency.GetString()),
            "currentWeather.tendency must be a non-empty tendency string.");
        var allowedTendencies = new HashSet<string>(StringComparer.Ordinal)
        {
            "IMPROVE",
            "WORSEN",
            "JUMP_TO_CLEAR",
            "JUMP_TO_CLOUDY",
            "JUMP_TO_FOGGY",
            "JUMP_TO_LIGHT_RAIN",
            "JUMP_TO_HEAVY_RAIN",
            "JUMP_TO_STORM",
            "JUMP_TO_LIGHT_SNOW",
            "JUMP_TO_HEAVY_SNOW",
            "JUMP_TO_SANDSTORM",
            "JUMP_TO_BLIZZARD",
            "JUMP_TO_SCORCHING_SUN",
            "NO_CHANGE"
        };
        Assert.True(
            allowedTendencies.Contains(tendency.GetString() ?? string.Empty),
            "currentWeather.tendency must use canonical weather command values, not descriptive aliases.");
    }

    [Fact]
    public void GameSessionFixture_MortalLocationUsesCurrentMaterializationAndIdentityIndex()
    {
        var worldRoot = Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "game_state",
            "world");
        var current = JsonNode.Parse(File.ReadAllText(
            Path.Combine(worldRoot, "current_location.json")))!.AsObject();
        var map = JsonNode.Parse(File.ReadAllText(
            Path.Combine(worldRoot, "world_map.json")))!.AsObject();
        var indexRoot = JsonNode.Parse(File.ReadAllText(
            Path.Combine(worldRoot, "location_identity_index.json")))!.AsObject();

        var location = Assert.Single(
            map["locations"]!.AsArray().OfType<JsonObject>());
        using var locationDocument = JsonDocument.Parse(location.ToJsonString());
        using var currentDocument = JsonDocument.Parse(current.ToJsonString());
        Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLocation(
            locationDocument.RootElement,
            "FileSystemExample world_map location"));
        Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLocation(
            currentDocument.RootElement,
            "FileSystemExample current location"));

        var locationId = location["locationId"]!.GetValue<string>();
        Assert.Equal(locationId, current["locationId"]!.GetValue<string>());
        Assert.Equal(
            location["materializationReceipt"]!["receiptId"]!.GetValue<string>(),
            current["materializationReceipt"]!["receiptId"]!.GetValue<string>());
        Assert.Empty(map["links"]!.AsArray());

        var index = MortalLocationIdentityState.Parse(indexRoot);
        Assert.Empty(index.Issues);
        Assert.Empty(index.ValidateCanonicalState(map));
        Assert.True(index.LocationEntriesById.ContainsKey(locationId));
    }

    [Fact]
    public void ValidatorFixture_MortalLocationBackupUsesCurrentCanonicalMapAndIndex()
    {
        var fixtureRoot = Path.Combine(
            TestRepoPaths.ValidatorFixturesRoot,
            "_shared",
            "mortal_location");
        var map = JsonNode.Parse(File.ReadAllText(
            Path.Combine(fixtureRoot, "world_map_backup.json")))!.AsObject();
        var indexRoot = JsonNode.Parse(File.ReadAllText(
            Path.Combine(fixtureRoot, "location_identity_index_backup.json")))!.AsObject();

        foreach (var location in map["locations"]!.AsArray().OfType<JsonObject>())
        {
            using var document = JsonDocument.Parse(location.ToJsonString());
            Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLocation(
                document.RootElement,
                "shared validator fixture location"));
        }

        foreach (var link in map["links"]!.AsArray().OfType<JsonObject>())
        {
            using var document = JsonDocument.Parse(link.ToJsonString());
            Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLink(
                document.RootElement,
                "shared validator fixture link"));
        }

        var index = MortalLocationIdentityState.Parse(indexRoot);
        Assert.Empty(index.Issues);
        Assert.Empty(index.ValidateCanonicalState(map));
    }

    private static string ToFixtureRelativePath(string fullPath)
    {
        return Path.GetRelativePath(TestRepoPaths.BaseSessionRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
    }

    private static JsonObject ReadArchiveObject(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath);
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
        return JsonNode.Parse(reader.ReadToEnd())?.AsObject() ??
               throw new InvalidDataException($"Archive entry '{entryPath}' must contain a JSON object.");
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir));

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            File.Copy(file, file.Replace(sourceDir, destinationDir), overwrite: true);
    }
}
