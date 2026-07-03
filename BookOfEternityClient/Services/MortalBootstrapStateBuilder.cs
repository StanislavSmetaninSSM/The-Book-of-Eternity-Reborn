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

        return new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase)
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
            ["game_state/inventory/items.json"] = BuildInventory(
                idSuffix,
                currentLocationId,
                locationName,
                shortCircumstances),
            ["game_state/player/experience.json"] = BuildExperience(),
            ["game_state/factions/faction_core.json"] = BuildFactionCore(factionId, factionName, shortCircumstances, turn, timestamp),
            ["game_state/factions/faction_resources.json"] = new()
            {
                ["factionResources"] = new JsonArray(),
                ["metaResources"] = new JsonArray(),
                ["strategicGoods"] = new JsonArray()
            },
            ["game_state/quests/regular_quests.json"] = BuildRegularQuests(questId, currentLocationId, factionId, shortCircumstances, turn)
        };
    }

    private static JsonObject BuildExperience() =>
        new()
        {
            ["playerLevel"] = 1,
            ["level"] = 1,
            ["currentExperience"] = 0,
            ["experience"] = 0,
            ["totalExperience"] = 0,
            ["experienceForNextLevel"] = 100,
            ["experienceGained"] = 0
        };

    private static JsonObject BuildInventory(
        string idSuffix,
        string currentLocationId,
        string currentLocationName,
        string shortCircumstances)
    {
        var itemId = $"item_{idSuffix}_opening_anchor";
        return new JsonObject
        {
            ["items"] = new JsonArray
            {
                new JsonObject
                {
                    ["itemId"] = itemId,
                    ["existedId"] = itemId,
                    ["name"] = BuildOpeningAnchorName(shortCircumstances),
                    ["description"] = $"Стартовая зацепка новой смертной жизни: {shortCircumstances}. Её подробности можно раскрыть позже, не теряя связь с первым выбором.",
                    ["image_prompt"] = "dark fantasy opening scene anchor item, late medieval room, moody candlelight, detailed realistic object",
                    ["quality"] = "Common",
                    ["price"] = 0,
                    ["count"] = 1,
                    ["weight"] = 0.1,
                    ["volume"] = 0.01,
                    ["contentsPath"] = null,
                    ["isContainer"] = false,
                    ["isConsumption"] = false,
                    ["requiresTwoHands"] = false,
                    ["durability"] = "100%",
                    ["type"] = BuildOpeningAnchorType(shortCircumstances),
                    ["group"] = "Стартовые зацепки",
                    ["textContent"] = new JsonArray($"Стартовая зацепка пока описана обстоятельствами сцены: {shortCircumstances}."),
                    ["journalEntries"] = null,
                    ["equipmentSlot"] = null,
                    ["accessoryForSlot"] = null,
                    ["currentLocationId"] = currentLocationId,
                    ["currentLocationName"] = currentLocationName,
                    ["isCarried"] = false,
                    ["isEquipped"] = false,
                    ["visibility"] = "known"
                }
            },
            ["equipment"] = new JsonObject(),
            ["totalWeight"] = 0,
            ["maxWeight"] = 45
        };
    }

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
            ["factionControl"] = new JsonArray
            {
                new JsonObject
                {
                    ["factionId"] = factionId,
                    ["factionName"] = factionName,
                    ["controlType"] = "Social",
                    ["controlLevel"] = 10
                }
            },
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
                    ["developmentArchetype"] = "local_starting_context",
                    ["level"] = 1,
                    ["experience"] = 0,
                    ["experienceForNextLevel"] = 100,
                    ["isPlayerFaction"] = false,
                    ["isPlayerMember"] = false,
                    ["reputation"] = 0,
                    ["influence"] = 10,
                    ["powerProfile"] = new JsonObject
                    {
                        ["political"] = 1,
                        ["economic"] = 1,
                        ["military"] = 1,
                        ["occult"] = 1,
                        ["social"] = 3,
                        ["covert"] = 1,
                        ["logistics"] = 1,
                        ["stability"] = 5,
                        ["arcane_tech"] = 1,
                        ["exploration"] = 1,
                    ["summary"] = "Местная сила служит опорой для первых связей сцены; её роль можно развить в ходе игры."
                    },
                    ["resources"] = new JsonObject
                    {
                        ["wealth"] = 0,
                        ["manpower"] = 0,
                        ["information"] = 1,
                        ["magic"] = 0,
                        ["metaResources"] = new JsonArray(),
                        ["strategicGoods"] = new JsonArray()
                    },
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
                    ["customStates"] = new JsonArray(),
                    ["image_prompt"] = "dark fantasy local faction seal, medieval parchment, subtle gold ink"
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

    private static string BuildOpeningAnchorName(string shortCircumstances)
    {
        if (ContainsAny(shortCircumstances, "перчат", "glove") &&
            ContainsAny(shortCircumstances, "письм", "letter"))
            return "Руническая перчатка и запечатанное письмо";

        if (ContainsAny(shortCircumstances, "письм", "letter"))
            return "Запечатанное письмо стартовой сцены";

        if (ContainsAny(shortCircumstances, "книг", "book", "дневник", "journal"))
            return "Книга стартовой сцены";

        if (ContainsAny(shortCircumstances, "перчат", "glove"))
            return "Руническая перчатка стартовой сцены";

        return "Стартовая зацепка новой жизни";
    }

    private static string BuildOpeningAnchorType(string shortCircumstances) =>
        ContainsAny(shortCircumstances, "письм", "letter", "книг", "book", "дневник", "journal", "записк", "note")
            ? "Документ"
            : "Квестовый предмет";

    private static bool ContainsAny(string value, params string[] needles) =>
        needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
}
