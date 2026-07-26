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
        var factionId = $"faction_{idSuffix}_initial_context";
        var questId = $"quest_{idSuffix}_opening_hook";
        var locationName = "Стартовая сцена новой жизни";
        var exitName = "Ближайший выход из стартовой сцены";
        var factionName = "Силы стартовой сцены";
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
            ["lore/codex_entries.json"] = BuildCodexEntries(idSuffix, lifeNumber, timestamp, world, factionName),
            ["game_state/world/current_location.json"] = BuildCurrentLocation(
                currentLocationId,
                nearbyExitId,
                factionId,
                factionName,
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
            ["game_state/world/world_events.json"] = BuildWorldEvents(
                idSuffix,
                turn,
                timestamp,
                circumstances),
            ["game_state/player/experience.json"] = BuildExperience(),
            ["game_state/player/skills_active.json"] = BuildActiveSkills(),
            ["game_state/player/skills_passive.json"] = BuildPassiveSkills(),
            ["game_state/player/skill_mastery.json"] = BuildSkillMastery(),
            ["game_state/factions/faction_core.json"] = BuildFactionCore(factionId, factionName, shortCircumstances, turn, timestamp),
            ["game_state/factions/faction_resources.json"] = new()
            {
                ["entries"] = new JsonArray()
            },
            ["game_state/quests/regular_quests.json"] = BuildRegularQuests(questId, currentLocationId, factionId, shortCircumstances, turn)
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
        string factionId,
        string factionName,
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
            ["type"] = "indoor",
            ["locationType"] = "indoor",
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
                    ["isBlocked"] = false,
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
                    ["travelMode"] = "walk",
                    ["isKnown"] = true,
                    ["name"] = "Путь из стартовой сцены",
                    ["shortDescription"] = "Первый известный выход, который ГМ может уточнить художественно.",
                    ["linkType"] = "passage",
                    ["linkState"] = "known",
                    ["targetCoordinates"] = Coordinates(1, 0, 0),
                    ["estimatedInternalDifficultyProfile"] = DifficultyProfile(1, 1, 1, 2, "Выход известен, но подробности сцены определит ГМ."),
                    ["estimatedExternalDifficultyProfile"] = DifficultyProfile(1, 1, 1, 2, "За пределами стартовой сцены мир ещё не раскрыт.")
                }
            },
            ["factionControl"] = new JsonArray(),
            ["locationStorages"] = new JsonArray(),
            ["activeThreats"] = new JsonArray(),
            ["internalDifficultyProfile"] = DifficultyProfile(1, 1, 1, 2, "Стартовая сцена безопасна как baseline; ГМ может добавить давление валидным обновлением."),
            ["externalDifficultyProfile"] = DifficultyProfile(1, 1, 1, 2, "Ближайшее окружение ещё не раскрыто."),
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
                    ["type"] = "indoor",
                    ["locationType"] = "indoor",
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
                    ["type"] = "indoor",
                    ["locationType"] = "indoor",
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
                    ["linkType"] = "passage",
                    ["linkState"] = "known",
                    ["direction"] = "наружу",
                    ["isKnown"] = true,
                    ["targetName"] = exitName,
                    ["targetCoordinates"] = Coordinates(1, 0, 0),
                    ["estimatedInternalDifficultyProfile"] = DifficultyProfile(1, 1, 1, 2, "Безопасный первый переход."),
                    ["estimatedExternalDifficultyProfile"] = DifficultyProfile(1, 1, 1, 2, "Подробности окружения появятся в ходе игры.")
                }
            },
            ["worldMapUpdates"] = new JsonObject
            {
                ["currentLocationId"] = currentLocationId,
                ["lastEventsDescription"] = lastEventsDescription
            }
        };

    private static JsonObject BuildFactionCore(
        string factionId,
        string factionName,
        string shortCircumstances,
        int turn,
        string timestamp) =>
        new()
        {
            ["factions"] = new JsonArray
            {
                new JsonObject
                {
                    ["factionId"] = factionId,
                    ["name"] = factionName,
                    ["displayName"] = factionName,
                    ["description"] = $"Минимальная стартовая фракционная опора для первой смертной сцены: {shortCircumstances}",
                    ["type"] = "local_context",
                    ["status"] = "active",
                    ["visibility"] = "known",
                    ["ranks"] = new JsonObject
                    {
                        ["entries"] = new JsonArray(),
                        ["hierarchySummary"] = "Ранги пока не раскрыты."
                    },
                    ["rankBranches"] = new JsonArray(),
                    ["relations"] = new JsonArray(),
                    ["controlledTerritories"] = new JsonArray(),
                    ["projects"] = new JsonArray(),
                    ["chronicle"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["entryId"] = $"chron_{factionId}_bootstrap",
                            ["title"] = "Стартовая сцена",
                            ["summary"] = "Фракционная опора появилась вместе с первыми обстоятельствами смертной жизни.",
                            ["turn"] = turn,
                            ["timestamp"] = timestamp,
                            ["visibility"] = "known"
                        }
                    },
                    ["customStates"] = new JsonArray()
                }
            }
        };

    private static JsonObject BuildRegularQuests(
        string questId,
        string currentLocationId,
        string factionId,
        string shortCircumstances,
        int turn) =>
        new()
        {
            ["quests"] = new JsonArray
            {
                new JsonObject
                {
                    ["questId"] = questId,
                    ["questName"] = "Первые минуты новой жизни",
                    ["title"] = "Первые минуты новой жизни",
                    ["status"] = "Active",
                    ["category"] = "opening",
                    ["summary"] = shortCircumstances,
                    ["description"] = $"Разобраться в обстоятельствах первой сцены: {shortCircumstances}",
                    ["questBackground"] = "Первая цель новой жизни удерживает выбранные обстоятельства старта и помогает не потерять ближайшие зацепки.",
                    ["questGiver"] = "Обстоятельства новой жизни",
                    ["objectives"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["objectiveId"] = $"obj_{questId}_orient",
                            ["description"] = "Осмотреться и понять ближайшую угрозу или возможность.",
                            ["status"] = "Active"
                        }
                    },
                    ["relatedLocationIds"] = new JsonArray(currentLocationId),
                    ["relatedFactionIds"] = new JsonArray(factionId),
                    ["relatedItemIds"] = new JsonArray(),
                    ["startedAtTurn"] = turn,
                    ["lastUpdatedTurn"] = turn,
                    ["visibility"] = "known",
                    ["detailsLog"] = new JsonArray($"#[{turn}]. Первая цель новой жизни связала выбранные обстоятельства стартовой сцены.")
                }
            }
        };

    private static JsonObject BuildCodexEntries(string idSuffix, int incarnationNumber, string timestamp, string world, string factionName)
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
                "current_world"),
            BuildCodexEntry(
                $"codex_{idSuffix}_starting_faction",
                factionName,
                "factions",
                "Стартовая фракционная опора текущей сцены. ГМ может уточнить её название, роль и связи валидным обновлением.",
                "game_state/factions/faction_core.json",
                timestamp,
                incarnationNumber,
                "Начало текущей смертной жизни",
                "faction")
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

    private static JsonObject DifficultyProfile(int combat, int environment, int social, int exploration, string summary) => new()
    {
        ["combat"] = combat,
        ["environment"] = environment,
        ["social"] = social,
        ["exploration"] = exploration,
        ["summary"] = summary
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
