using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public static class MortalBootstrapStateBuilder
{
    private static readonly string[] CodexCategoryOrder =
    [
        "cosmology",
        "geography",
        "history",
        "cultures",
        "creatures",
        "characters",
        "artifacts",
        "factions",
        "magic",
        "other"
    ];

    public static IReadOnlyDictionary<string, JsonObject> BuildFreshMortalBootstrapFiles(
        int incarnationNumber,
        int turnNumber,
        string? characterDescription,
        string? worldDescription,
        string? startingCircumstances,
        DateTimeOffset createdAtUtc)
    {
        var lifeNumber = Math.Max(incarnationNumber, 1);
        var turn = Math.Max(turnNumber, 1);
        var idSuffix = $"life_{lifeNumber:D3}";
        var timestamp = createdAtUtc.ToUniversalTime().ToString("o");
        var character = FirstNonEmpty(characterDescription, $"персонаж инкарнации #{lifeNumber}");
        var world = FirstNonEmpty(worldDescription, "новый смертный мир");
        var circumstances = FirstNonEmpty(startingCircumstances, "первая сцена смертной жизни ещё не уточнена");
        var shortCircumstances = TrimSentence(circumstances, 180);

        var files = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase)
        {
            ["lore/current_world/world_setting.json"] = new()
            {
                ["title"] = "Текущий смертный мир",
                ["summary"] = world,
                ["playerPremise"] = character,
                ["startingCircumstances"] = circumstances,
                ["incarnation"] = lifeNumber,
                ["createdAtUtc"] = timestamp
            },
            ["lore/current_world/geography.json"] = new()
            {
                ["regions"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["regionId"] = $"region_{idSuffix}_start",
                        ["name"] = "Стартовый регион",
                        ["description"] = world,
                        ["knownLocations"] = new JsonArray()
                    }
                }
            },
            ["lore/current_world/history.json"] = new()
            {
                ["entries"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["entryId"] = $"history_{idSuffix}_opening",
                        ["turn"] = turn,
                        ["summary"] = $"Начало инкарнации: {shortCircumstances}",
                        ["visibility"] = "player_known"
                    }
                }
            },
            ["lore/current_world/cultures.json"] = new()
            {
                ["cultures"] = new JsonArray(),
                ["summary"] = "Культурные детали текущего мира будут уточняться в первых смертных сценах."
            },
            ["lore/current_world/threats.json"] = new()
            {
                ["threats"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["threatId"] = $"threat_{idSuffix}_opening_pressure",
                        ["name"] = "Давление первой сцены",
                        ["summary"] = shortCircumstances,
                        ["visibleToPlayer"] = true
                    }
                }
            },
            ["lore/codex_entries.json"] = BuildCodexEntries(idSuffix, lifeNumber, timestamp, world),
            [MortalLocationMaterializationContract.CurrentLocationPath] = BuildPendingCurrentLocation(),
            [MortalLocationMaterializationContract.WorldMapPath] = BuildEmptyWorldMap(),
            [MortalLocationIdentityState.StatePath] = MortalLocationIdentityState.CreateEmptyRoot(),
            ["game_state/inventory/items.json"] = BuildInventory(),
            [MortalItemIdentityState.StatePath] = MortalItemIdentityState.CreateEmptyRoot(),
            ["game_state/inventory/item_resources.json"] = BuildEmptyEntries(),
            ["game_state/inventory/item_bonds.json"] = BuildEmptyEntries(),
            ["game_state/inventory/item_text_updates.json"] = BuildEmptyEntries(),
            ["game_state/npcs/item_journals.json"] = BuildEmptyEntries(),
            ["game_state/world/world_events.json"] = BuildWorldEvents(
                idSuffix,
                turn,
                timestamp,
                circumstances),
            ["game_state/player/experience.json"] = BuildExperience(),
            ["game_state/player/skills_active.json"] = BuildActiveSkills(),
            ["game_state/player/skills_passive.json"] = BuildPassiveSkills(),
            ["game_state/player/skill_mastery.json"] = BuildSkillMastery(),
            ["game_state/factions/faction_core.json"] = new()
            {
                ["factions"] = new JsonArray()
            },
            ["game_state/factions/faction_resources.json"] = new()
            {
                ["entries"] = new JsonArray()
            },
            ["game_state/quests/regular_quests.json"] = new()
            {
                ["quests"] = new JsonArray()
            }
        };

        return files;
    }

    private static JsonObject BuildExperience() => new();

    private static JsonObject BuildEmptyEntries() =>
        new()
        {
            ["entries"] = new JsonArray()
        };

    private static JsonObject BuildActiveSkills() =>
        new()
        {
            ["activeSkillChanges"] = new JsonArray(),
            ["removeActiveSkills"] = new JsonArray()
        };

    private static JsonObject BuildPassiveSkills() =>
        new()
        {
            ["passiveSkillChanges"] = new JsonArray(),
            ["removePassiveSkills"] = new JsonArray()
        };

    private static JsonObject BuildSkillMastery() =>
        new()
        {
            ["skillMasteryChanges"] = new JsonArray()
        };

    private static JsonObject BuildWorldEvents(
        string idSuffix,
        int turn,
        string timestamp,
        string circumstances) =>
        new()
        {
            ["worldEventsLog"] = new JsonArray
            {
                new JsonObject
                {
                    ["eventId"] = $"world_event_{idSuffix}_opening",
                    ["timestamp"] = timestamp,
                    ["turn"] = turn,
                    ["title"] = BuildOpeningWorldEventTitle(circumstances),
                    ["description"] = TrimSentence(circumstances, 320),
                    ["visibility"] = "local",
                    ["status"] = "active"
                }
            }
        };

    private static string BuildOpeningWorldEventTitle(string circumstances)
    {
        var firstClause = circumstances
            .Split([',', ':', ';', '.', '!', '?'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        var title = TrimSentence(firstClause ?? string.Empty, 100).TrimEnd('.');
        return title.Length == 0
            ? "Первое известие новой жизни"
            : char.ToUpperInvariant(title[0]) + title[1..];
    }

    private static JsonObject BuildInventory() =>
        new()
        {
            ["items"] = new JsonArray(),
            ["equippedItems"] = new JsonObject()
        };

    private static JsonObject BuildPendingCurrentLocation() =>
        new()
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locationId"] = null,
            ["state"] = "pending_materialization"
        };

    private static JsonObject BuildEmptyWorldMap() =>
        new()
        {
            ["schemaVersion"] = 1,
            ["realm"] = "mortal_world",
            ["locations"] = new JsonArray(),
            ["links"] = new JsonArray()
        };

    private static JsonObject BuildCodexEntries(
        string idSuffix,
        int incarnationNumber,
        string timestamp,
        string world)
    {
        var entries = new JsonArray
        {
            BuildCodexEntry(
                $"codex_{idSuffix}_world",
                "Текущий смертный мир",
                "geography",
                world,
                "current_world/world_setting.json",
                timestamp,
                incarnationNumber,
                "Стартовая запись текущего смертного мира",
                "current_world")
        };

        return new JsonObject
        {
            ["entries"] = entries,
            ["totalEntries"] = entries.Count,
            ["categories"] = BuildCodexCategoryCounts(entries)
        };
    }

    private static JsonObject BuildCodexEntry(
        string entryId,
        string title,
        string category,
        string content,
        string sourceFile,
        string discoveredAt,
        int incarnation,
        string discoveryContext,
        string tag) =>
        new()
        {
            ["entryId"] = entryId,
            ["title"] = title,
            ["category"] = category,
            ["content"] = content,
            ["summary"] = content,
            ["sourceFile"] = sourceFile,
            ["discoveryContext"] = discoveryContext,
            ["incarnation"] = incarnation,
            ["discoveredAt"] = discoveredAt,
            ["tags"] = new JsonArray("bootstrap", tag),
            ["relatedEntries"] = new JsonArray()
        };

    private static JsonObject BuildCodexCategoryCounts(JsonArray entries)
    {
        var counts = CodexCategoryOrder.ToDictionary(category => category, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.OfType<JsonObject>())
        {
            var category = entry["category"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(category))
                continue;

            if (!counts.ContainsKey(category))
                counts["other"]++;
            else
                counts[category]++;
        }

        var result = new JsonObject();
        foreach (var category in CodexCategoryOrder)
            result[category] = counts[category];
        return result;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string TrimSentence(string value, int maxLength)
    {
        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
            return trimmed.TrimEnd('.');

        return trimmed[..Math.Max(0, maxLength - 1)].TrimEnd(' ', '.', ',', ';', ':') + "…";
    }

}
