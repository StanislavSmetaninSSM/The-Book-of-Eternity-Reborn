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
        var currentLocationId = $"loc_{idSuffix}_start";
        var nearbyExitId = $"loc_{idSuffix}_nearby_exit";
        var locationName = "Стартовая сцена новой жизни";
        var exitName = "Ближайший выход из стартовой сцены";
        var shortCircumstances = TrimSentence(circumstances, 180);
        var turnAnchor = $"#[{turn}].";

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
                        ["knownLocations"] = new JsonArray(currentLocationId, nearbyExitId)
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
            ["game_state/world/current_location.json"] = BuildCurrentLocation(
                currentLocationId,
                nearbyExitId,
                locationName,
                exitName,
                shortCircumstances,
                $"{turnAnchor} Начало смертной жизни: {shortCircumstances}"),
            ["game_state/world/world_map.json"] = BuildWorldMap(
                currentLocationId,
                nearbyExitId,
                locationName,
                exitName,
                shortCircumstances,
                $"{turnAnchor} Первый ориентир новой жизни отмечен по выбранным обстоятельствам."),
            ["game_state/inventory/items.json"] = BuildInventory(),
            [MortalItemIdentityState.StatePath] = MortalItemIdentityState.CreateEmptyRoot(),
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
            ["equipment"] = new JsonObject()
        };

    private static JsonObject BuildCurrentLocation(
        string currentLocationId,
        string nearbyExitId,
        string locationName,
        string exitName,
        string shortCircumstances,
        string lastEventsDescription) =>
        new()
        {
            ["locationId"] = currentLocationId,
            ["name"] = locationName,
            ["displayName"] = locationName,
            ["region"] = "Стартовый регион",
            ["description"] = shortCircumstances,
            ["coordinates"] = Coordinates(0, 0, 0),
            ["knownExits"] = new JsonArray
            {
                new JsonObject
                {
                    ["targetLocationId"] = nearbyExitId,
                    ["targetName"] = exitName,
                    ["direction"] = "наружу",
                    ["isKnown"] = true,
                    ["summary"] = "Первый очевидный путь из стартовой сцены."
                }
            },
            ["adjacencyMap"] = new JsonArray
            {
                new JsonObject
                {
                    ["targetLocationId"] = nearbyExitId,
                    ["targetName"] = exitName,
                    ["direction"] = "наружу",
                    ["isKnown"] = true,
                    ["name"] = "Путь из стартовой сцены",
                    ["shortDescription"] = "Первый известный выход, который ГМ может уточнить художественно.",
                    ["linkState"] = "known",
                    ["targetCoordinates"] = Coordinates(1, 0, 0)
                }
            },
            ["factionControl"] = new JsonArray(),
            ["locationStorages"] = new JsonArray(),
            ["activeThreats"] = new JsonArray(),
            ["lastEventsDescription"] = lastEventsDescription
        };

    private static JsonObject BuildWorldMap(
        string currentLocationId,
        string nearbyExitId,
        string locationName,
        string exitName,
        string shortCircumstances,
        string lastEventsDescription) =>
        new()
        {
            ["newLocations"] = new JsonArray
            {
                new JsonObject
                {
                    ["locationId"] = currentLocationId,
                    ["name"] = locationName,
                    ["displayName"] = locationName,
                    ["region"] = "Стартовый регион",
                    ["description"] = shortCircumstances,
                    ["coordinates"] = Coordinates(0, 0, 0),
                    ["exits"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["targetLocationId"] = nearbyExitId,
                            ["direction"] = "наружу",
                            ["isKnown"] = true
                        }
                    },
                    ["lastEventsDescription"] = lastEventsDescription
                },
                new JsonObject
                {
                    ["locationId"] = nearbyExitId,
                    ["name"] = exitName,
                    ["displayName"] = exitName,
                    ["region"] = "Стартовый регион",
                    ["description"] = "Ближайшая ещё не раскрытая точка выхода из стартовой сцены.",
                    ["coordinates"] = Coordinates(1, 0, 0),
                    ["exits"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["targetLocationId"] = currentLocationId,
                            ["direction"] = "обратно",
                            ["isKnown"] = true
                        }
                    },
                    ["lastEventsDescription"] = lastEventsDescription
                }
            },
            ["newLinks"] = new JsonArray
            {
                new JsonObject
                {
                    ["sourceLocationId"] = currentLocationId,
                    ["targetLocationId"] = nearbyExitId,
                    ["name"] = "Путь из стартовой сцены",
                    ["shortDescription"] = "Связь между стартовой точкой и ближайшим выходом.",
                    ["linkState"] = "known",
                    ["direction"] = "наружу",
                    ["isKnown"] = true,
                    ["targetName"] = exitName,
                    ["targetCoordinates"] = Coordinates(1, 0, 0)
                }
            },
            ["worldMapUpdates"] = new JsonObject
            {
                ["currentLocationId"] = currentLocationId,
                ["lastEventsDescription"] = lastEventsDescription
            }
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

    private static JsonObject Coordinates(int x, int y, int z) => new()
    {
        ["x"] = x,
        ["y"] = y,
        ["z"] = z
    };

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
