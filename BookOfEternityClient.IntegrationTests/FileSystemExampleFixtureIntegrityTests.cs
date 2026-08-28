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

        Assert.Equal(104, observed.Max(item => item.EntryCount));
        Assert.Equal(373_016, observed.Max(item => item.ExpandedBytes));
        Assert.Equal(60_825, observed.Max(item => item.LargestEntryBytes));
        Assert.Equal(3_735, observed.Max(item => item.NameUtf8Bytes));

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
    public async Task SplitAfterlifeEntityProfileExamples_PassCurrentProductionValidation()
    {
        var profileDirectory = Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "game_state",
            "afterlife",
            "entity_profiles");
        var profileFiles = Directory
            .EnumerateFiles(profileDirectory, "*.json", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(profileFiles);

        foreach (var profileFile in profileFiles)
        {
            var exampleRoot = JsonNode.Parse(await File.ReadAllTextAsync(profileFile))?.AsObject();
            Assert.NotNull(exampleRoot);
            var profiles = Assert.IsType<JsonArray>(
                exampleRoot![AfterlifeEntityProfileState.ProfilesProperty]);
            Assert.NotEmpty(profiles);
            Assert.All(
                profiles.OfType<JsonObject>(),
                profile => Assert.IsType<JsonArray>(profile["activeEffects"]));

            var validationRoot = Path.Combine(
                Path.GetTempPath(),
                "boe-filesystem-example-afterlife-profile-" + Guid.NewGuid().ToString("N"));
            try
            {
                var fs = new FileSystemManager(
                    validationRoot,
                    NullLogger<FileSystemManager>.Instance);
                fs.EnsureDirectoryStructure();
                await fs.WriteFileAtomicAsync(
                    AfterlifeEntityProfileState.StatePath,
                    exampleRoot.ToJsonString());
                var validator = new ValidationService(
                    fs,
                    NullLogger<ValidationService>.Instance);

                var issues = await validator.ValidateGameStateAsync(
                    IntegrationValidationProfiles.AfterlifeEntityProfile);

                Assert.DoesNotContain(
                    issues,
                    issue => issue.Code?.StartsWith(
                        "afterlife_entity_profile_",
                        StringComparison.OrdinalIgnoreCase) == true);
            }
            finally
            {
                if (Directory.Exists(validationRoot))
                    Directory.Delete(validationRoot, recursive: true);
            }
        }
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
    [InlineData("mortal_world_command_display_fixture.zip")]
    [InlineData("chaos_sea_command_display_fixture.zip")]
    [InlineData("shining_abode_command_display_fixture.zip")]
    public async Task ReusableSaveFixtures_ContainExactCanonicalResourceQuartet(
        string fixtureName)
    {
        var fixturePath = Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "saves",
            "manual_saves",
            fixtureName);
        using var archive = ZipFile.OpenRead(fixturePath);
        var definitionsEntry = Assert.Single(
            archive.Entries,
            entry => entry.FullName == ResourceMaterializationContract.DefinitionsPath);
        var stateEntry = Assert.Single(
            archive.Entries,
            entry => entry.FullName == ResourceMaterializationContract.StatePath);
        var historyEntry = Assert.Single(
            archive.Entries,
            entry => entry.FullName == ResourceMaterializationContract.HistoryPath);
        var authorityEntry = Assert.Single(
            archive.Entries,
            entry => entry.FullName ==
                     CanonicalResourceOwnerAuthorityComposer.AuthorityPath);

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            ReadArchiveText(definitionsEntry),
            allowMissingPristine: false);
        Assert.True(
            definitions.IsValid,
            string.Join(Environment.NewLine, definitions.Issues));
        var catalog = Assert.IsType<ResourceDefinitionCatalog>(definitions.Catalog);
        var state = ResourceStateContract.ParseCanonical(
            ReadArchiveText(stateEntry),
            catalog,
            allowMissingPristine: false);
        var history = ResourceHistoryState.ParseCanonical(
            ReadArchiveText(historyEntry),
            catalog,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        Assert.True(history.IsValid, string.Join(Environment.NewLine, history.Issues));
        var ledger = Assert.IsType<ResourceStateLedger>(state.Ledger);
        var canonicalHistory = Assert.IsType<ResourceHistoryState>(history.History);
        Assert.Empty(canonicalHistory.ValidateStateAgreement(ledger));
        var documents = archive.Entries
            .Where(static entry => !string.IsNullOrEmpty(entry.Name))
            .ToDictionary(
                static entry => entry.FullName,
                ReadArchiveText,
                StringComparer.Ordinal);
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            catalog,
            path => Task.FromResult(documents.GetValueOrDefault(path)),
            ledger,
            canonicalHistory,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        Assert.True(
            authority.IsValid,
            string.Join(Environment.NewLine, authority.Issues));
        Assert.Equal(
            authority.CanonicalAuthorityJson,
            ReadArchiveText(authorityEntry));
    }

    [Fact]
    public void ReusableSaveFixtures_PreserveResourceSemanticsWithoutLegacyMirrors()
    {
        using var mortal = OpenReusableSave("mortal_world_command_display_fixture.zip");
        var mortalState = ReadArchiveResourceState(mortal);
        var mortalNpcCore = Assert.IsType<JsonObject>(ReadOptionalArchiveObject(
            mortal,
            "game_state/npcs/npc_core.json"));
        var selene = Assert.Single(
            Assert.IsType<JsonArray>(mortalNpcCore["UpdateNPCs"]).OfType<JsonObject>());
        Assert.Equal("npc_magistra_selene", selene["NPCId"]?.GetValue<string>());
        Assert.False(selene.ContainsKey("initialId"));
        AssertSingleOwnerIdentity(
            mortal,
            EffectCarrierCatalog.EnemiesPath,
            "enemiesData",
            "combatantId",
            "combatant_fixture_enemies_0");
        AssertSingleOwnerIdentity(
            mortal,
            EffectCarrierCatalog.AlliesPath,
            "alliesData",
            "NPCId",
            "npc_valmont_steward_marius");
        AssertResource(mortalState, "mortal_world", ResourceOwnerKind.Player,
            "player_current", "health", 99m, 117m, ResourceLifecycleState.Active);
        AssertResource(mortalState, "mortal_world", ResourceOwnerKind.Player,
            "player_current", "energy", 73m, 122m, ResourceLifecycleState.Active);
        AssertResource(mortalState, "mortal_world", ResourceOwnerKind.Player,
            "player_current", "poise", 142m, 150m, ResourceLifecycleState.Active);
        AssertResource(mortalState, "mortal_world", ResourceOwnerKind.Npc,
            "npc_magistra_selene", "health", 100m, 100m, ResourceLifecycleState.Active);
        AssertResource(mortalState, "mortal_world", ResourceOwnerKind.Npc,
            "npc_valmont_steward_marius", "health", 92m, 100m, ResourceLifecycleState.Active);
        AssertResource(mortalState, "mortal_world", ResourceOwnerKind.Npc,
            "npc_valmont_steward_marius", "poise", 80m, 100m, ResourceLifecycleState.Active);
        AssertResource(mortalState, "mortal_world", ResourceOwnerKind.Npc,
            "npc_artifact_trader_voron", "health", 100m, 100m, ResourceLifecycleState.Active);
        AssertResource(mortalState, "mortal_world", ResourceOwnerKind.Npc,
            "npc_house_spy_iren", "health", 88m, 100m, ResourceLifecycleState.Active);
        AssertResource(mortalState, "mortal_world", ResourceOwnerKind.Combatant,
            "combatant_fixture_enemies_0", "health", 85m, 100m, ResourceLifecycleState.Active);
        AssertResource(mortalState, "mortal_world", ResourceOwnerKind.Combatant,
            "combatant_fixture_enemies_0", "poise", 70m, 100m, ResourceLifecycleState.Active);
        AssertResource(mortalState, "mortal_world", ResourceOwnerKind.Vehicle,
            "transport_valmont_carriage", "health", 100m, 100m, ResourceLifecycleState.Active);
        Assert.Equal(11, mortalState.Entries.Count);
        AssertNoLegacyResourceMirrors(mortal);

        using var chaos = OpenReusableSave("chaos_sea_command_display_fixture.zip");
        var chaosState = ReadArchiveResourceState(chaos);
        AssertSingleOwnerIdentity(
            chaos,
            EffectCarrierCatalog.EnemiesPath,
            "enemiesData",
            "combatantId",
            "combatant_fixture_enemies_0");
        AssertSingleOwnerIdentity(
            chaos,
            EffectCarrierCatalog.AlliesPath,
            "alliesData",
            "combatantId",
            "combatant_fixture_allies_0");
        AssertResource(chaosState, "mortal_world", ResourceOwnerKind.Player,
            "player_current", "health", 99m, 117m, ResourceLifecycleState.Active);
        AssertResource(chaosState, "mortal_world", ResourceOwnerKind.Player,
            "player_current", "energy", 73m, 122m, ResourceLifecycleState.Active);
        AssertResource(chaosState, "mortal_world", ResourceOwnerKind.Player,
            "player_current", "poise", 142m, 150m, ResourceLifecycleState.Active);
        AssertResource(chaosState, "mortal_world", ResourceOwnerKind.Combatant,
            "combatant_fixture_enemies_0", "health", 85m, 100m, ResourceLifecycleState.Active);
        AssertResource(chaosState, "mortal_world", ResourceOwnerKind.Combatant,
            "combatant_fixture_enemies_0", "poise", 70m, 100m, ResourceLifecycleState.Active);
        AssertResource(chaosState, "mortal_world", ResourceOwnerKind.Combatant,
            "combatant_fixture_allies_0", "health", 100m, 100m, ResourceLifecycleState.Active);
        AssertResource(chaosState, "mortal_world", ResourceOwnerKind.Combatant,
            "combatant_fixture_allies_0", "poise", 80m, 100m, ResourceLifecycleState.Active);
        AssertResource(chaosState, "mortal_world", ResourceOwnerKind.Vehicle,
            "transport_valmont_carriage", "health", 100m, 100m, ResourceLifecycleState.Active);
        foreach (var (itemId, current) in new (string ItemId, decimal Current)[]
                 {
                     ("a1b2c3d4-e5f6-7890-abcd-111111111111", 95m),
                     ("a1b2c3d4-e5f6-7890-abcd-222222222222", 100m),
                     ("a1b2c3d4-e5f6-7890-abcd-333333333333", 100m),
                     ("a1b2c3d4-e5f6-7890-abcd-444444444444", 90m),
                     ("a1b2c3d4-e5f6-7890-abcd-555555555555", 100m),
                     ("item_silver_chalk_stack_a", 100m),
                     ("item_silver_chalk_stack_b", 100m),
                     ("item_dark_travel_cloak", 100m),
                     ("item_gold_ring", 100m),
                     ("item_merchant_seal", 100m)
                 })
        {
            AssertResource(
                chaosState,
                "mortal_world",
                ResourceOwnerKind.Item,
                itemId,
                "durability",
                current,
                100m,
                ResourceLifecycleState.Active);
        }
        AssertResource(chaosState, "chaos_sea", ResourceOwnerKind.AfterlifeActor,
            "player_soul", "spiritual_action_points", 6m, 6m, ResourceLifecycleState.Active);
        var chaosConflict = Assert.IsType<JsonObject>(ReadOptionalArchiveObject(
            chaos,
            AfterlifeSpiritualConflictState.StatePath)?["activeConflict"]);
        Assert.Equal("conflict_chaos_hunter_001", chaosConflict["conflictId"]?.GetValue<string>());
        var chaosOppositionBinding = Assert.IsType<JsonObject>(
            chaosConflict["resourceOwnerBindings"]?["opposition"]);
        var chaosOppositionOwnerId = Assert.IsAssignableFrom<JsonValue>(
            chaosOppositionBinding["resourceOwnerId"]).GetValue<string>();
        Assert.True(ResourceMaterializationContract.IsExactIdentifier(chaosOppositionOwnerId));
        AssertResource(
            chaosState,
            "chaos_sea",
            ResourceOwnerKind.AfterlifeConflictSide,
            chaosOppositionOwnerId,
            "spiritual_action_points",
            6m,
            6m,
            ResourceLifecycleState.Active);
        AssertResource(chaosState, "chaos_sea", ResourceOwnerKind.AfterlifeActor,
            "guardian_azalia", "gacha_attempts", 3m, 3m, ResourceLifecycleState.Active);
        Assert.Equal(21, chaosState.Entries.Count);
        AssertNoLegacyResourceMirrors(chaos);

        using var shining = OpenReusableSave("shining_abode_command_display_fixture.zip");
        var shiningState = ReadArchiveResourceState(shining);
        AssertResource(shiningState, "mortal_world", ResourceOwnerKind.Player,
            "player_current", "health", 100m, 100m, ResourceLifecycleState.Active);
        AssertResource(shiningState, "mortal_world", ResourceOwnerKind.Player,
            "player_current", "energy", 100m, 100m, ResourceLifecycleState.Active);
        AssertResource(shiningState, "mortal_world", ResourceOwnerKind.Player,
            "player_current", "poise", 100m, 100m, ResourceLifecycleState.Active);
        AssertResource(shiningState, "shining_abode", ResourceOwnerKind.AfterlifeActor,
            "player_soul", "spiritual_action_points", 7m, 8m, ResourceLifecycleState.Active);
        var shiningConflict = Assert.IsType<JsonObject>(ReadOptionalArchiveObject(
            shining,
            AfterlifeSpiritualConflictState.StatePath)?["activeConflict"]);
        Assert.Equal("conflict_shining_oath_001", shiningConflict["conflictId"]?.GetValue<string>());
        var shiningOppositionBinding = Assert.IsType<JsonObject>(
            shiningConflict["resourceOwnerBindings"]?["opposition"]);
        var shiningOppositionOwnerId = Assert.IsAssignableFrom<JsonValue>(
            shiningOppositionBinding["resourceOwnerId"]).GetValue<string>();
        Assert.True(ResourceMaterializationContract.IsExactIdentifier(shiningOppositionOwnerId));
        AssertResource(
            shiningState,
            "shining_abode",
            ResourceOwnerKind.AfterlifeConflictSide,
            shiningOppositionOwnerId,
            "spiritual_action_points",
            6m,
            6m,
            ResourceLifecycleState.Active);
        var shiningRoot = Assert.IsType<JsonObject>(ReadOptionalArchiveObject(
            shining,
            ShiningAbodeState.StatePath));
        var shiningGacha = Assert.IsType<JsonObject>(shiningRoot["gachaSystem"]);
        var shiningGachaBinding = Assert.IsType<JsonObject>(
            shiningRoot["resourceOwnerBindings"]?["gachaReturn"]);
        Assert.Equal(
            shiningGacha["currentReturnCycleId"]?.GetValue<string>(),
            shiningGachaBinding["returnCycleId"]?.GetValue<string>());
        var shiningGachaOwnerId = Assert.IsAssignableFrom<JsonValue>(
            shiningGachaBinding["resourceOwnerId"]).GetValue<string>();
        Assert.True(ResourceMaterializationContract.IsExactIdentifier(shiningGachaOwnerId));
        // Preserve the one used attempt while the typed radiance formula replaces
        // the stale legacy cap of three with the canonical maximum of five.
        AssertResource(
            shiningState,
            "shining_abode",
            ResourceOwnerKind.AfterlifeScope,
            shiningGachaOwnerId,
            "gacha_attempts",
            4m,
            5m,
            ResourceLifecycleState.Active);
        Assert.Equal(6, shiningState.Entries.Count);
        AssertNoLegacyResourceMirrors(shining);
    }

    [Theory]
    [InlineData("mortal_world_command_display_fixture.zip")]
    [InlineData("chaos_sea_command_display_fixture.zip")]
    [InlineData("shining_abode_command_display_fixture.zip")]
    public void ReusableSaveFixtures_EffectsUseCurrentCarrierAndIdentityAuthority(string fixtureName)
    {
        var fixturePath = Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "saves",
            "manual_saves",
            fixtureName);
        using var archive = ZipFile.OpenRead(fixturePath);
        var input = new EffectCarrierCatalogInput(
            ReadOptionalArchiveObject(archive, EffectCarrierCatalog.PlayerPath),
            ReadOptionalArchiveObject(archive, EffectCarrierCatalog.NpcPath),
            ReadOptionalArchiveObject(archive, EffectCarrierCatalog.EnemiesPath),
            ReadOptionalArchiveObject(archive, EffectCarrierCatalog.AlliesPath),
            ReadOptionalArchiveObject(archive, EffectCarrierCatalog.AfterlifeProfilesPath),
            ReadOptionalArchiveObject(archive, EffectCarrierCatalog.SpiritualConflictPath));
        var catalog = EffectCarrierCatalog.Build(input);
        Assert.Empty(catalog.Issues);

        var sourceRoots = EffectAcceptedTurnInputComposer.SourceAuthorityPaths
            .ToDictionary(
                static path => path,
                path => (JsonNode?)ReadOptionalArchiveObject(archive, path),
                StringComparer.Ordinal);
        var sourceAuthority = EffectAcceptedTurnInputComposer
            .BuildCanonicalSourceAuthority(sourceRoots);
        Assert.Empty(sourceAuthority.Issues);
        var targetAuthority = EffectAcceptedTurnInputComposer
            .BuildCanonicalTargetAuthority(input, sourceRoots);
        Assert.Empty(targetAuthority.Issues);
        Assert.Empty(targetAuthority.ValidateNamedCombatantBindings(input));
        var bindingIssues = new List<ValidationIssue>();
        ValidationService.ValidateCanonicalEffectBindings(
            catalog,
            sourceAuthority,
            targetAuthority,
            bindingIssues);
        Assert.Empty(bindingIssues);

        var indexRoot = ReadOptionalArchiveObject(archive, EffectIdentityState.StatePath);
        if (indexRoot == null)
        {
            Assert.Empty(catalog.Occurrences);
        }
        else
        {
            using var indexDocument = JsonDocument.Parse(indexRoot.ToJsonString());
            var parsed = EffectIdentityState.Parse(indexDocument.RootElement, EffectIdentityState.StatePath);
            Assert.Empty(parsed.Issues);
            var index = Assert.IsType<EffectIdentityState>(parsed.State);
            var agreementIssues = new List<ValidationIssue>();
            ValidationService.ValidateEffectCarrierIndexAgreement(catalog, index, agreementIssues);
            Assert.Empty(agreementIssues);
        }

        var pending = ReadOptionalArchiveObject(archive, ResourcePendingResolutionState.PendingPath);
        if (pending != null)
        {
            Assert.Equal(ResourcePendingResolutionState.SchemaVersion, pending["schemaVersion"]!.GetValue<int>());
            Assert.IsType<JsonArray>(pending["requests"]);
            Assert.IsType<JsonArray>(pending["terminalReceipts"]);
        }

        var legacy = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name) && entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(entry => ReadArchiveText(entry))
            .FirstOrDefault(text =>
                text.Contains("playerActiveEffectsChanges", StringComparison.Ordinal) ||
                text.Contains("NPCEffectChanges", StringComparison.Ordinal));
        Assert.Null(legacy);
    }

    [Fact]
    public void CurrentNonEmptyPendingFixture_RoundTripsProductionContract()
    {
        const string fingerprintA =
            "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string fingerprintB =
            "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var effectId = "effect_fixture_bleeding";
        var triggerId = "trigger_fixture_periodic_damage";
        var eventRef = "turn_42_effect_fixture";
        var draft = new ResourcePendingResolutionDraft(
            "bounded_receipt",
            "session_fixture_pending",
            "request_fixture_turn_42",
            42,
            eventRef,
            effectId,
            new ResourcePendingAuthorityBinding("permanent", effectId),
            new JsonObject
            {
                ["kind"] = "wound",
                ["sourceId"] = "wound_fixture_torn_side",
                ["definitionKey"] = "bleeding_consequence"
            },
            new ResourcePendingAuthorityBinding("permanent", "wound_fixture_torn_side"),
            new JsonObject
            {
                ["kind"] = "player",
                ["targetId"] = "player_current"
            },
            new ResourcePendingAuthorityBinding("permanent", "player_current"),
            triggerId,
            new ResourceCoordinate(
                "mortal_world",
                ResourceOwnerKind.Player,
                "player_current",
                "health"),
            new ResourcePendingAuthorityBinding("permanent", "player_current"),
            ResourceOperation.Damage,
            0m,
            5m,
            fingerprintA,
            fingerprintB,
            fingerprintA,
            fingerprintA,
            "Рваная рана",
            "герой",
            "Здоровье",
            "урон",
            new ResourcePendingCausalAuthority(
                effectId,
                triggerId,
                eventRef,
                "turn_42_damage_fixture",
                "resource_operation_fixture_damage",
                7,
                3,
                true,
                2,
                "component_fixture_periodic_damage",
                null,
                fingerprintA,
                fingerprintB,
                0));
        var created = ResourcePendingResolutionState.CreatePending(
            canonicalJson: null,
            new[] { draft },
            ResourceDefinitionCatalog.CreateBuiltIn(),
            () => "resource_resolution_fixture_pending",
            new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero));

        Assert.True(
            created.IsValid,
            string.Join(Environment.NewLine, created.Issues.Select(issue => issue.Code)));
        var state = Assert.IsType<ResourcePendingResolutionState>(created.State);
        var canonical = state.ToCanonicalJson();
        var root = JsonNode.Parse(canonical)!.AsObject();
        Assert.Equal(ResourcePendingResolutionState.SchemaVersion, root["schemaVersion"]!.GetValue<int>());
        Assert.Single(root["requests"]!.AsArray());
        Assert.Empty(root["terminalReceipts"]!.AsArray());

        var parsed = ResourcePendingResolutionState.ParseCanonical(
            canonical,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        Assert.True(
            parsed.IsValid,
            string.Join(Environment.NewLine, parsed.Issues.Select(issue => issue.Code)));
        Assert.Equal(canonical, parsed.State!.ToCanonicalJson());
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
            foreach (var requiredString in new[] { "existedId", "name", "description", "image_prompt", "quality" })
            {
                if (!item.TryGetProperty(requiredString, out var value) ||
                    value.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(value.GetString()))
                    invalidItems.Add($"{id}: missing string {requiredString}");
            }

            foreach (var legacyResourceField in new[] { "durability", "maxDurability" })
            {
                if (item.TryGetProperty(legacyResourceField, out _))
                    invalidItems.Add($"{id}: forbidden legacy resource field {legacyResourceField}");
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
    public void GameSessionFixture_UsesCanonicalResourceRootsAndLegacyRepairFixture()
    {
        var resourceRoot = Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "game_state",
            "resources");
        var definitionsPath = Path.Combine(resourceRoot, "resource_definitions.json");
        var statePath = Path.Combine(resourceRoot, "resource_state.json");
        var historyPath = Path.Combine(resourceRoot, "resource_history.json");
        var authorityPath = Path.Combine(resourceRoot, "resource_owner_authority.json");
        var commandPath = Path.Combine(resourceRoot, "resource_commands.json");

        Assert.True(
            File.Exists(authorityPath),
            "The active session fixture must contain the complete canonical resource quartet.");

        var definitionsResult = ResourceDefinitionCatalog.ParseCanonical(
            File.ReadAllText(definitionsPath),
            allowMissingPristine: false);
        Assert.True(
            definitionsResult.IsValid,
            string.Join(Environment.NewLine, definitionsResult.Issues));
        var definitions = Assert.IsType<ResourceDefinitionCatalog>(definitionsResult.Catalog);

        var stateResult = ResourceStateContract.ParseCanonical(
            File.ReadAllText(statePath),
            definitions,
            allowMissingPristine: false);
        Assert.True(
            stateResult.IsValid,
            string.Join(Environment.NewLine, stateResult.Issues));

        var historyResult = ResourceHistoryState.ParseCanonical(
            File.ReadAllText(historyPath),
            definitions,
            allowMissingPristine: false);
        Assert.True(
            historyResult.IsValid,
            string.Join(Environment.NewLine, historyResult.Issues));
        Assert.Empty(
            Assert.IsType<ResourceHistoryState>(historyResult.History)
                .ValidateStateAgreement(
                    Assert.IsType<ResourceStateLedger>(stateResult.Ledger)));
        Assert.False(
            File.Exists(commandPath),
            "resource_commands.json is a transient accepted-turn envelope and must not be checked into canonical game state.");

        var fixtureRoot = Path.Combine(
            TestRepoPaths.ValidatorFixturesRoot,
            "resource_materialization");
        var fixturePath = Path.Combine(fixtureRoot, "fixture.json");
        var expectedErrorsPath = Path.Combine(fixtureRoot, "expected_errors.json");
        var brokenStatusPath = Path.Combine(fixtureRoot, "broken", "player_status.json");
        var fixedStatusPath = Path.Combine(fixtureRoot, "fixed", "player_status.json");

        Assert.True(File.Exists(fixturePath));
        Assert.True(File.Exists(expectedErrorsPath));
        Assert.True(File.Exists(brokenStatusPath));
        Assert.True(File.Exists(fixedStatusPath));

        using var fixtureDocument = JsonDocument.Parse(File.ReadAllText(fixturePath));
        Assert.Equal(
            "resource_materialization",
            fixtureDocument.RootElement.GetProperty("id").GetString());
        Assert.Equal(
            "StateOnly",
            fixtureDocument.RootElement.GetProperty("runner").GetString());
        Assert.Contains(
            fixtureDocument.RootElement.GetProperty("expectedBrokenCodes").EnumerateArray(),
            code => string.Equals(
                code.GetString(),
                "resource_legacy_player_gauge_forbidden",
                StringComparison.Ordinal));

        using var expectedErrorsDocument = JsonDocument.Parse(File.ReadAllText(expectedErrorsPath));
        Assert.Contains(
            expectedErrorsDocument.RootElement.GetProperty("expectedCodes").EnumerateArray(),
            code => string.Equals(
                code.GetString(),
                "resource_legacy_player_gauge_forbidden",
                StringComparison.Ordinal));

        using var brokenStatusDocument = JsonDocument.Parse(File.ReadAllText(brokenStatusPath));
        using var fixedStatusDocument = JsonDocument.Parse(File.ReadAllText(fixedStatusPath));
        Assert.True(brokenStatusDocument.RootElement.TryGetProperty("healthPercentage", out _));
        Assert.False(fixedStatusDocument.RootElement.TryGetProperty("healthPercentage", out _));
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

    private static JsonObject? ReadOptionalArchiveObject(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath);
        if (entry == null)
            return null;
        return JsonNode.Parse(ReadArchiveText(entry))?.AsObject() ??
               throw new InvalidDataException($"Archive entry '{entryPath}' must contain a JSON object.");
    }

    private static ZipArchive OpenReusableSave(string fixtureName)
    {
        var fixturePath = Path.Combine(
            TestRepoPaths.BaseSessionRoot,
            "saves",
            "manual_saves",
            fixtureName);
        Assert.True(File.Exists(fixturePath), $"Missing reusable save fixture: {fixturePath}");
        return ZipFile.OpenRead(fixturePath);
    }

    private static ResourceStateLedger ReadArchiveResourceState(ZipArchive archive)
    {
        var definitionsEntry = archive.GetEntry(ResourceMaterializationContract.DefinitionsPath);
        var stateEntry = archive.GetEntry(ResourceMaterializationContract.StatePath);
        Assert.NotNull(definitionsEntry);
        Assert.NotNull(stateEntry);

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            ReadArchiveText(definitionsEntry!),
            allowMissingPristine: false);
        Assert.True(
            definitions.IsValid,
            string.Join(Environment.NewLine, definitions.Issues));
        var catalog = Assert.IsType<ResourceDefinitionCatalog>(definitions.Catalog);
        var state = ResourceStateContract.ParseCanonical(
            ReadArchiveText(stateEntry!),
            catalog,
            allowMissingPristine: false);
        Assert.True(state.IsValid, string.Join(Environment.NewLine, state.Issues));
        return Assert.IsType<ResourceStateLedger>(state.Ledger);
    }

    private static void AssertSingleOwnerIdentity(
        ZipArchive archive,
        string path,
        string collectionProperty,
        string identityProperty,
        string expectedIdentity)
    {
        var root = Assert.IsType<JsonObject>(ReadOptionalArchiveObject(archive, path));
        var owner = Assert.Single(
            Assert.IsType<JsonArray>(root[collectionProperty]).OfType<JsonObject>());
        Assert.Equal(expectedIdentity, owner[identityProperty]?.GetValue<string>());
        Assert.False(owner.ContainsKey("combatantRef"));
        if (!string.Equals(identityProperty, "combatantId", StringComparison.Ordinal))
            Assert.False(owner.ContainsKey("combatantId"));
    }

    private static void AssertResource(
        ResourceStateLedger state,
        string realm,
        ResourceOwnerKind ownerKind,
        string ownerId,
        string resourceKey,
        decimal current,
        decimal maximum,
        ResourceLifecycleState lifecycle)
    {
        var entry = Assert.Single(state.Entries, candidate =>
            string.Equals(candidate.Coordinate.Realm, realm, StringComparison.Ordinal) &&
            candidate.Coordinate.OwnerKind == ownerKind &&
            string.Equals(
                candidate.Coordinate.ResourceOwnerId,
                ownerId,
                StringComparison.Ordinal) &&
            string.Equals(
                candidate.Coordinate.ResourceKey,
                resourceKey,
                StringComparison.Ordinal));
        Assert.Equal(current, entry.Current);
        Assert.Equal(maximum, entry.Maximum);
        Assert.Equal(lifecycle, entry.State);
    }

    private static void AssertNoLegacyResourceMirrors(ZipArchive archive)
    {
        Assert.Null(archive.GetEntry("game_state/inventory/item_resources.json"));

        AssertPropertiesAbsent(
            archive,
            new[]
            {
                "game_state/core/player_status.json",
                "game_state/player/player_status.json"
            },
            "healthPercentage",
            "energyPercentage",
            "poisePercentage",
            "currentHealthChange",
            "currentEnergyChange",
            "currentPoiseChange",
            "inventoryItemsResources");
        AssertPropertiesAbsent(
            archive,
            new[] { "game_state/npcs/npc_inventory.json" },
            "NPCInventoryResourcesChanges");
        AssertPropertiesAbsent(
            archive,
            new[] { "game_state/npcs/npc_core.json" },
            "currentHealthPercentage",
            "maxHealthPercentage");
        AssertPropertiesAbsent(
            archive,
            new[]
            {
                EffectCarrierCatalog.EnemiesPath,
                EffectCarrierCatalog.AlliesPath,
                StorageTransportMoveService.VehiclesPath
            },
            "currentHealth",
            "maxHealth",
            "currentPoise",
            "maxPoise",
            "healthStates");
        AssertPropertiesAbsent(
            archive,
            new[] { InventoryEquipmentService.ItemsPath },
            "durability",
            "maxDurability");
        AssertPropertiesAbsent(
            archive,
            new[] { AfterlifeEntityProfileState.StatePath },
            "actorRef");
        AssertPropertiesAbsent(
            archive,
            new[] { AfterlifeSpiritualConflictState.StatePath },
            "actionEconomy");
        AssertPropertiesAbsent(
            archive,
            new[]
            {
                "game_state/meta/guardians.json",
                ShiningAbodeState.StatePath
            },
            "chargesPerReturn",
            "chargesUsedThisReturn");
        AssertPropertiesAbsent(
            archive,
            new[]
            {
                "game_state/npcs/npc_core.json",
                InventoryEquipmentService.ItemsPath,
                EffectCarrierCatalog.EnemiesPath,
                EffectCarrierCatalog.AlliesPath,
                StorageTransportMoveService.VehiclesPath,
                AfterlifeEntityProfileState.StatePath,
                AfterlifeSpiritualConflictState.StatePath,
                ShiningAbodeState.StatePath
            },
            "resourceMaterialization");
    }

    private static void AssertPropertiesAbsent(
        ZipArchive archive,
        IEnumerable<string> paths,
        params string[] propertyNames)
    {
        foreach (var path in paths)
        {
            var root = ReadOptionalArchiveObject(archive, path);
            if (root == null)
                continue;
            AssertPropertiesAbsent(root, path, propertyNames);
        }
    }

    private static void AssertPropertiesAbsent(
        JsonNode node,
        string path,
        IReadOnlyCollection<string> propertyNames)
    {
        if (node is JsonObject objectNode)
        {
            foreach (var (propertyName, value) in objectNode)
            {
                Assert.DoesNotContain(propertyName, propertyNames);
                if (value != null)
                    AssertPropertiesAbsent(value, path + "." + propertyName, propertyNames);
            }
            return;
        }

        if (node is not JsonArray array)
            return;
        for (var index = 0; index < array.Count; index++)
        {
            if (array[index] != null)
                AssertPropertiesAbsent(array[index]!, $"{path}[{index}]", propertyNames);
        }
    }

    private static string ReadArchiveText(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
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
