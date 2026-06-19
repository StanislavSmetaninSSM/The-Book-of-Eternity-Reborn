using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FileSystemExampleFixtureIntegrityTests
{
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
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
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
            root.TryGetProperty("normalizedWeatherState", out var weather) &&
            weather.ValueKind == JsonValueKind.Object,
            "FileSystemExample current_location.json must include normalizedWeatherState with GM-safe shape.");
        Assert.True(
            weather.TryGetProperty("description", out var description) &&
            description.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(description.GetString()),
            "normalizedWeatherState.description must be a non-empty player-facing string.");
        Assert.True(
            weather.TryGetProperty("tendency", out var tendency) &&
            tendency.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(tendency.GetString()),
            "normalizedWeatherState.tendency must be a non-empty tendency string.");
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
            "normalizedWeatherState.tendency must use canonical weather command values, not descriptive aliases.");
    }

    private static string ToFixtureRelativePath(string fullPath)
    {
        return Path.GetRelativePath(TestRepoPaths.BaseSessionRoot, fullPath).Replace(Path.DirectorySeparatorChar, '/');
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
