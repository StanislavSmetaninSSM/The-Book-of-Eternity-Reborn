using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.UI;

internal static class ExplorerMortalWorldNewsCommandResultBuilder
{
    private const string EventsPath = "game_state/world/world_events.json";
    private const string FlagsPath = "game_state/world/world_flags.json";
    private const string ProgressionPath = "game_state/world/progression.json";
    private const string CurrentLocationPath = "game_state/world/current_location.json";
    private const string WorldMapPath = "game_state/world/world_map.json";
    private const string NpcActivitiesPath = "game_state/npcs/npc_activities.json";
    private const string FactionProjectsPath = "game_state/factions/faction_projects.json";

    public static async Task<ExplorerCommandResult> BuildAsync(string command, FileSystemManager fs)
    {
        var normalizedCommand = command.Trim();
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(normalizedCommand);
        var state = new WorldNewsState(
            await ReadJson(fs, EventsPath),
            await ReadJson(fs, FlagsPath),
            await ReadJson(fs, ProgressionPath),
            await ReadJson(fs, CurrentLocationPath),
            await ReadJson(fs, WorldMapPath),
            await ReadJson(fs, NpcActivitiesPath),
            await ReadJson(fs, FactionProjectsPath));

        var events = EnumerateWorldEvents(state.Events.Node).ToList();
        var flags = EnumerateWorldFlags(state.Flags.Node).ToList();
        var progression = EnumerateProgressionEntries(state.Progression.Node).ToList();
        var threats = EnumerateLocationThreats(state.CurrentLocation.Node, state.WorldMap.Node).ToList();
        var npcActivities = EnumerateNpcActivities(state.NpcActivities.Node).ToList();
        var factionProjects = EnumerateFactionProjects(state.FactionProjects.Node).ToList();
        var request = ParseWorldNewsDetailRequest(ExtractCommandRemainder(normalizedCommand));

        return request.Kind == WorldNewsDetailKind.Overview
            ? BuildOverview(normalizedCommand, commandToken, state, events, threats, npcActivities, factionProjects, flags, progression)
            : BuildDetail(normalizedCommand, commandToken, state, events, flags, progression, request);
    }

    private static ExplorerCommandResult BuildOverview(
        string command,
        string commandToken,
        WorldNewsState state,
        IReadOnlyList<WorldEventSnapshot> events,
        IReadOnlyList<LocationThreatSnapshot> threats,
        IReadOnlyList<NpcActivitySnapshot> npcActivities,
        IReadOnlyList<FactionProjectSnapshot> factionProjects,
        IReadOnlyList<WorldFlagSnapshot> flags,
        IReadOnlyList<ProgressionSnapshot> progression)
    {
        var blocks = new List<UiBlock>();
        var summaryRows = BuildWorldNewsSummaryRows(state, events.Count, threats.Count, npcActivities.Count, factionProjects.Count, flags.Count, progression.Count);
        if (summaryRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Новости мира",
                Columns = ["Раздел", "Состояние"],
                Rows = summaryRows
            });
        }

        if (events.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Мировые события",
                Columns = ["Событие", "Когда и где", "Статус", "Подробно"],
                Rows = events.Select(item => new UiTableRow
                {
                    Cells =
                    [
                        item.Title,
                        EmptyFallback(DescribeEventWhenWhere(item.Node)),
                        EmptyFallback(DescribeEventStatus(item.Node)),
                        BuildWorldNewsDetailCommand(commandToken, WorldNewsDetailKind.Event, item.Selector)
                    ]
                }).ToList()
            });
        }

        if (threats.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Угрозы локаций",
                Columns = ["Локация", "Угроза", "Опасность", "Описание"],
                Rows = threats.Select(static item => new UiTableRow
                {
                    Cells = [item.Location, item.Name, EmptyFallback(DescribeThreatSeverity(item.Severity)), EmptyFallback(item.Description)]
                }).ToList()
            });
        }

        if (npcActivities.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Активности НПС",
                Columns = ["НПС", "Активность", "Где", "Состояние"],
                Rows = npcActivities.Select(static item => new UiTableRow
                {
                    Cells = [item.NpcName, item.Activity, EmptyFallback(item.Location), EmptyFallback(DescribeWorldNewsStatus(item.Status))]
                }).ToList()
            });
        }

        if (factionProjects.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Проекты фракций",
                Columns = ["Фракция", "Проект", "Состояние", "Описание"],
                Rows = factionProjects.Select(static item => new UiTableRow
                {
                    Cells = [EmptyFallback(item.Faction), item.Project, EmptyFallback(DescribeWorldNewsStatus(item.Status)), EmptyFallback(item.Description)]
                }).ToList()
            });
        }

        if (flags.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Флаги мира",
                Columns = ["Флаг", "Область", "Состояние", "Подробно"],
                Rows = flags.Select(item => new UiTableRow
                {
                    Cells =
                    [
                        item.Title,
                        EmptyFallback(DescribeFlagScope(item.Node)),
                        EmptyFallback(DescribeFlagState(item.Node)),
                        BuildWorldNewsDetailCommand(commandToken, WorldNewsDetailKind.Flag, item.Selector)
                    ]
                }).ToList()
            });
        }

        if (progression.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Прогресс мира",
                Columns = ["Запись", "Стадия", "Что изменилось", "Подробно"],
                Rows = progression.Select(item => new UiTableRow
                {
                    Cells =
                    [
                        item.Title,
                        EmptyFallback(item.Stage),
                        EmptyFallback(FirstWorldNewsNodeString(item.Node, "changeReason", "lastChangeReason", "reason", "description", "summary")),
                        BuildWorldNewsDetailCommand(commandToken, WorldNewsDetailKind.Progression, item.Selector)
                    ]
                }).ToList()
            });
        }

        AddWorldNewsReadWarnings(blocks, state);

        if (blocks.Count == 0)
            blocks.Add(Message(UiNotificationSeverity.Info, "Новости мира", "Данные ещё не созданы."));

        AddWorldNewsRawState(blocks, state.Events, "Полная запись мировых событий");
        AddWorldNewsRawState(blocks, state.Flags, "Полная запись флагов мира");
        AddWorldNewsRawState(blocks, state.Progression, "Полная запись прогресса мира");
        return Completed(command, blocks, BuildWorldNewsOverviewActions(commandToken, events, flags, progression));
    }

    private static ExplorerCommandResult BuildDetail(
        string command,
        string commandToken,
        WorldNewsState state,
        IReadOnlyList<WorldEventSnapshot> events,
        IReadOnlyList<WorldFlagSnapshot> flags,
        IReadOnlyList<ProgressionSnapshot> progression,
        WorldNewsDetailRequest request)
    {
        var blocks = new List<UiBlock>();
        switch (request.Kind)
        {
            case WorldNewsDetailKind.Event:
            {
                var item = FindWorldEvent(events, request.Selector);
                blocks.Add(item == null
                    ? Message(UiNotificationSeverity.Warning, "Событие не найдено", "Такое событие не отмечено в текущих новостях мира.")
                    : BuildWorldEventDetailPanel(item));
                break;
            }
            case WorldNewsDetailKind.Flag:
            {
                var item = FindWorldFlag(flags, request.Selector);
                blocks.Add(item == null
                    ? Message(UiNotificationSeverity.Warning, "Флаг мира не найден", "Такой флаг не отмечен в текущих новостях мира.")
                    : BuildWorldFlagDetailPanel(item));
                break;
            }
            case WorldNewsDetailKind.Progression:
            {
                var item = FindProgression(progression, request.Selector);
                blocks.Add(item == null
                    ? Message(UiNotificationSeverity.Warning, "Прогресс не найден", "Такая запись прогресса не отмечена в текущих новостях мира.")
                    : BuildProgressionDetailPanel(item));
                break;
            }
            case WorldNewsDetailKind.Unknown:
                blocks.Add(Message(
                    UiNotificationSeverity.Warning,
                    "Новости мира",
                    "Не удалось понять, что открыть. Используйте /новости_мира событие <метка>, /новости_мира флаг <метка> или /новости_мира прогресс <метка>."));
                break;
        }

        AddWorldNewsReadWarnings(blocks, state);
        blocks.Add(new UiTextBlock { Text = $"Вернуться к сводке можно командой {commandToken}.", Tone = UiTone.Muted });
        return Completed(command, blocks, [
            new UiAction
            {
                Id = "world-news-back",
                Label = "Назад к новостям мира",
                Command = commandToken,
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false
            }
        ]);
    }

    private static List<UiTableRow> BuildWorldNewsSummaryRows(
        WorldNewsState state,
        int eventCount,
        int threatCount,
        int npcActivityCount,
        int factionProjectCount,
        int flagCount,
        int progressionCount)
    {
        var rows = new List<UiTableRow>();
        AddSummaryRow(rows, "Мировые события", state.Events, eventCount, "событие", "события", "событий");
        AddSummaryRow(rows, "Угрозы локаций", state.CurrentLocation.FileExists || state.WorldMap.FileExists, StateHasReadError(state.CurrentLocation) || StateHasReadError(state.WorldMap), threatCount, "угроза", "угрозы", "угроз");
        AddSummaryRow(rows, "Активности НПС", state.NpcActivities, npcActivityCount, "активность", "активности", "активностей");
        AddSummaryRow(rows, "Проекты фракций", state.FactionProjects, factionProjectCount, "проект", "проекта", "проектов");
        AddSummaryRow(rows, "Флаги мира", state.Flags, flagCount, "флаг", "флага", "флагов");
        AddSummaryRow(rows, "Прогресс мира", state.Progression, progressionCount, "запись", "записи", "записей");
        return rows;
    }

    private static void AddSummaryRow(
        List<UiTableRow> rows,
        string label,
        JsonReadResult read,
        int count,
        string singular,
        string paucal,
        string plural) =>
        AddSummaryRow(rows, label, read.FileExists, StateHasReadError(read), count, singular, paucal, plural);

    private static void AddSummaryRow(
        List<UiTableRow> rows,
        string label,
        bool fileExists,
        bool hasReadError,
        int count,
        string singular,
        string paucal,
        string plural)
    {
        if (!fileExists && count == 0)
            return;

        rows.Add(new UiTableRow
        {
            Cells =
            [
                label,
                hasReadError ? "повреждено" : count == 0 ? "пусто" : DescribeCount(count, singular, paucal, plural)
            ]
        });
    }

    private static UiPanelBlock BuildWorldEventDetailPanel(WorldEventSnapshot item)
    {
        var detailItems = new List<UiKeyValueItem>
        {
            new() { Key = "Метка", Value = item.Selector }
        };

        AddDetailItem(detailItems, "Когда", FirstWorldNewsNodeString(item.Node, "timestamp", "dateTime", "date", "time"));
        AddDetailItem(detailItems, "Где", FirstWorldNewsNodeString(item.Node, "location", "eventLocation", "locationName", "region"));
        AddDetailItem(detailItems, "Статус", DescribeEventStatus(item.Node));
        AddDetailItem(detailItems, "Кратко", FirstWorldNewsNodeString(item.Node, "summary", "narrativeSummary"));
        AddDetailItem(detailItems, "Описание", FirstWorldNewsNodeString(item.Node, "description", "details"));
        AddDetailItem(detailItems, "Участники", JoinNodeValues(item.Node["involvedNPCs"] ?? item.Node["actors"] ?? item.Node["participants"]));
        AddDetailItem(detailItems, "Фракции", JoinNodeValues(item.Node["affectedFactions"] ?? item.Node["factions"]));
        AddDetailItem(detailItems, "Локации", JoinNodeValues(item.Node["affectedLocations"] ?? item.Node["locations"]));
        AddDetailItem(detailItems, "Последствия", JoinNodeValues(item.Node["consequences"] ?? item.Node["effects"]));
        AddDetailItem(detailItems, "Итог", FirstWorldNewsNodeString(item.Node, "outcome", "result"));
        AddDetailItem(detailItems, "Продолжение", FirstWorldNewsNodeString(item.Node, "followUp", "followUpEvent", "nextStep"));
        AddDetailItem(detailItems, "Влияние", DescribeNodeForDetail(item.Node["impact"] ?? item.Node["impactProfile"]));

        return new UiPanelBlock
        {
            Title = $"Событие: {item.Title}",
            Blocks = [new UiKeyValueGridBlock { Items = detailItems }]
        };
    }

    private static UiPanelBlock BuildWorldFlagDetailPanel(WorldFlagSnapshot item)
    {
        var detailItems = new List<UiKeyValueItem>
        {
            new() { Key = "Метка", Value = item.Selector }
        };

        AddDetailItem(detailItems, "Область", DescribeFlagScope(item.Node));
        AddDetailItem(detailItems, "Состояние", DescribeFlagState(item.Node));
        AddDetailItem(detailItems, "Значение", DescribeFlagValue(item.Node["value"]));
        AddDetailItem(detailItems, "Описание", FirstWorldNewsNodeString(item.Node, "description", "summary", "note"));
        AddDetailItem(detailItems, "Последствие", FirstWorldNewsNodeString(item.Node, "consequence", "consequences", "effect", "currentEffect"));

        return new UiPanelBlock
        {
            Title = $"Флаг мира: {item.Title}",
            Blocks = [new UiKeyValueGridBlock { Items = detailItems }]
        };
    }

    private static UiPanelBlock BuildProgressionDetailPanel(ProgressionSnapshot item)
    {
        var detailItems = new List<UiKeyValueItem>
        {
            new() { Key = "Метка", Value = item.Selector },
            new() { Key = "Раздел", Value = item.Scope }
        };

        AddDetailItem(detailItems, "Стадия", item.Stage);
        AddDetailItem(detailItems, "Состояние", DescribeWorldNewsStatus(item.Status));
        AddDetailItem(detailItems, "Когда", FirstWorldNewsNodeString(item.Node, "timestamp", "date", "time", "updatedAt"));
        AddDetailItem(detailItems, "Описание", FirstWorldNewsNodeString(item.Node, "description", "summary"));
        AddDetailItem(detailItems, "Причина изменения", FirstWorldNewsNodeString(item.Node, "changeReason", "lastChangeReason", "reason", "source"));
        AddDetailItem(detailItems, "Последствие", FirstWorldNewsNodeString(item.Node, "consequence", "consequences", "outcome", "effect"));
        AddDetailItem(detailItems, "Следующая веха", FirstWorldNewsNodeString(item.Node, "nextMilestone", "milestone", "nextStep"));

        return new UiPanelBlock
        {
            Title = $"Прогресс мира: {item.Title}",
            Blocks = [new UiKeyValueGridBlock { Items = detailItems }]
        };
    }

    private static IReadOnlyList<UiAction> BuildWorldNewsOverviewActions(
        string commandToken,
        IReadOnlyList<WorldEventSnapshot> events,
        IReadOnlyList<WorldFlagSnapshot> flags,
        IReadOnlyList<ProgressionSnapshot> progression)
    {
        var actions = new List<UiAction>();
        foreach (var item in events)
        {
            actions.Add(new UiAction
            {
                Id = "world-news-event-" + ToActionIdPart(item.Selector),
                Label = $"Открыть событие «{item.Title}»",
                Command = BuildWorldNewsDetailCommand(commandToken, WorldNewsDetailKind.Event, item.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["kind"] = "event",
                    ["selector"] = item.Selector,
                    ["title"] = item.Title
                }
            });
        }

        foreach (var item in flags)
        {
            actions.Add(new UiAction
            {
                Id = "world-news-flag-" + ToActionIdPart(item.Selector),
                Label = $"Осмотреть флаг «{item.Title}»",
                Command = BuildWorldNewsDetailCommand(commandToken, WorldNewsDetailKind.Flag, item.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["kind"] = "flag",
                    ["selector"] = item.Selector,
                    ["title"] = item.Title
                }
            });
        }

        foreach (var item in progression)
        {
            actions.Add(new UiAction
            {
                Id = "world-news-progression-" + ToActionIdPart(item.Selector),
                Label = $"Открыть прогресс «{item.Title}»",
                Command = BuildWorldNewsDetailCommand(commandToken, WorldNewsDetailKind.Progression, item.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["kind"] = "progression",
                    ["selector"] = item.Selector,
                    ["title"] = item.Title
                }
            });
        }

        return actions;
    }

    private static IEnumerable<WorldEventSnapshot> EnumerateWorldEvents(JsonNode? node)
    {
        var index = 0;
        foreach (var item in EnumerateWorldNewsObjects(node, "worldEventsLog", "events", "worldEvents", "entries", "eventLog"))
        {
            index++;
            var title = FirstNonEmpty(
                FirstWorldNewsNodeString(item, "eventTitle", "title", "name"),
                FirstWorldNewsNodeString(item, "summary", "narrativeSummary", "description"),
                $"Событие {index}");
            var selector = NormalizeWorldNewsSelector(FirstNonEmpty(
                FirstWorldNewsNodeString(item, "eventId", "worldEventId", "id", "key"),
                index.ToString()));
            yield return new WorldEventSnapshot(index, selector, title, item);
        }
    }

    private static IEnumerable<WorldFlagSnapshot> EnumerateWorldFlags(JsonNode? node)
    {
        var index = 0;
        foreach (var item in EnumerateWorldNewsObjects(node, "worldStateFlags", "flags", "worldFlags", "entries"))
        {
            index++;
            var title = FirstNonEmpty(
                FirstWorldNewsNodeString(item, "displayName", "flagName", "title", "name"),
                FirstWorldNewsNodeString(item, "flagId", "id", "key"),
                $"Флаг {index}");
            var selector = NormalizeWorldNewsSelector(FirstNonEmpty(
                FirstWorldNewsNodeString(item, "flagId", "id", "key"),
                index.ToString()));
            yield return new WorldFlagSnapshot(index, selector, title, item);
        }
    }

    private static IEnumerable<ProgressionSnapshot> EnumerateProgressionEntries(JsonNode? node)
    {
        var index = 0;
        foreach (var (scope, item) in EnumerateProgressionObjects(node))
        {
            index++;
            var title = FirstNonEmpty(
                FirstWorldNewsNodeString(item, "trackerName", "progressionName", "title", "name"),
                $"Запись прогресса {index}");
            var selector = NormalizeWorldNewsSelector(FirstNonEmpty(
                FirstWorldNewsNodeString(item, "progressionId", "trackerId", "entryId", "id", "key"),
                index.ToString()));
            yield return new ProgressionSnapshot(
                index,
                selector,
                title,
                scope,
                FirstWorldNewsNodeString(item, "stageName", "currentStage", "stage"),
                FirstWorldNewsNodeString(item, "status", "state"),
                item);
        }
    }

    private static IEnumerable<LocationThreatSnapshot> EnumerateLocationThreats(JsonNode? currentLocation, JsonNode? worldMap)
    {
        foreach (var threat in EnumerateThreatsForLocation(currentLocation))
            yield return threat;

        if (worldMap is not JsonObject root)
            yield break;

        var mapRoot = root["worldMapUpdates"] as JsonObject ?? root;
        foreach (var location in EnumerateWorldNewsObjects(mapRoot, "newLocations", "locationUpdates", "locations"))
        {
            foreach (var threat in EnumerateThreatsForLocation(location))
                yield return threat;
        }
    }

    private static IEnumerable<LocationThreatSnapshot> EnumerateThreatsForLocation(JsonNode? node)
    {
        var location = ResolveLocationRoot(node);
        if (location == null)
            yield break;

        var locationName = FirstNonEmpty(
            FirstWorldNewsNodeString(location, "name", "locationName", "title"),
            "Неизвестная локация");
        foreach (var threat in EnumerateWorldNewsObjects(location["activeThreats"], "activeThreats", "threats", "entries"))
        {
            var name = FirstNonEmpty(
                FirstWorldNewsNodeString(threat, "threatName", "name", "title"),
                $"Угроза {locationName}");
            yield return new LocationThreatSnapshot(
                locationName,
                name,
                FirstWorldNewsNodeString(threat, "dangerLevel", "severity", "status"),
                FirstWorldNewsNodeString(threat, "description", "summary"));
        }
    }

    private static IEnumerable<NpcActivitySnapshot> EnumerateNpcActivities(JsonNode? node)
    {
        foreach (var item in EnumerateWorldNewsObjects(node, "entries", "activities", "npcActivities", "npc_activities"))
        {
            var details = item["activityUpdate"] as JsonObject ?? item;
            var npcName = FirstNonEmpty(
                FirstWorldNewsNodeString(item, "NPCName", "npcName", "name", "displayName"),
                FirstWorldNewsNodeString(details, "NPCName", "npcName", "name", "displayName"));
            var activity = FirstNonEmpty(
                FirstWorldNewsNodeString(details, "activityName", "currentActivity", "activity", "name", "title"),
                FirstWorldNewsNodeString(item, "activityName", "currentActivity", "activity"));
            if (string.IsNullOrWhiteSpace(npcName) && string.IsNullOrWhiteSpace(activity))
                continue;

            yield return new NpcActivitySnapshot(
                EmptyFallback(npcName),
                EmptyFallback(activity),
                FirstNonEmpty(FirstWorldNewsNodeString(details, "location", "locationId"), FirstWorldNewsNodeString(item, "location", "locationId")),
                FirstNonEmpty(FirstWorldNewsNodeString(details, "activeState", "status", "state"), FirstWorldNewsNodeString(item, "activeState", "status", "state")),
                FirstNonEmpty(FirstWorldNewsNodeString(details, "description", "summary"), FirstWorldNewsNodeString(item, "description", "summary")));
        }
    }

    private static IEnumerable<FactionProjectSnapshot> EnumerateFactionProjects(JsonNode? node)
    {
        foreach (var item in EnumerateWorldNewsObjects(node, "entries", "projects", "factionProjects", "activeProjects"))
        {
            var project = FirstWorldNewsNodeString(item, "projectName", "title", "name");
            if (string.IsNullOrWhiteSpace(project))
                continue;

            yield return new FactionProjectSnapshot(
                FirstWorldNewsNodeString(item, "factionName", "faction", "name"),
                project,
                FirstWorldNewsNodeString(item, "activeState", "finalState", "status", "state"),
                FirstNonEmpty(FirstWorldNewsNodeString(item, "description", "summary"), FirstWorldNewsNodeString(item, "narrativeSummary", "outcome")));
        }
    }

    private static IEnumerable<JsonObject> EnumerateWorldNewsObjects(JsonNode? node, params string[] propertyNames)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array.OfType<JsonObject>())
                yield return item;
            yield break;
        }

        if (node is not JsonObject root)
            yield break;

        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetPropertyValue(propertyName, out var value) || value == null)
                continue;

            if (value is JsonArray nestedArray)
            {
                foreach (var item in nestedArray.OfType<JsonObject>())
                    yield return item;
            }
            else if (value is JsonObject nestedObject)
            {
                yield return nestedObject;
            }
        }
    }

    private static IEnumerable<(string Scope, JsonObject Item)> EnumerateProgressionObjects(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array.OfType<JsonObject>())
                yield return ("Мир", item);
            yield break;
        }

        if (node is not JsonObject root)
            yield break;

        foreach (var item in EnumerateWorldNewsObjects(root["entries"], "entries"))
            yield return ("Мир", item);
        foreach (var item in EnumerateWorldNewsObjects(root["progression"], "progression", "trackers", "worldProgression"))
            yield return ("Мир", item);
        foreach (var item in EnumerateWorldNewsObjects(root["updateWorldProgressionTracker"], "updateWorldProgressionTracker"))
            yield return ("Мир", item);
        foreach (var item in EnumerateWorldNewsObjects(root["updateFactionProgressionTracker"], "updateFactionProgressionTracker"))
            yield return ("Фракции", item);
    }

    private static JsonObject? ResolveLocationRoot(JsonNode? node)
    {
        if (node is not JsonObject root)
            return null;

        return root["currentLocation"] as JsonObject
               ?? root["location"] as JsonObject
               ?? root;
    }

    private static WorldEventSnapshot? FindWorldEvent(IReadOnlyList<WorldEventSnapshot> items, string selector)
    {
        var normalized = NormalizeWorldNewsSelector(selector);
        return items.FirstOrDefault(item =>
            string.Equals(item.Selector, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Index.ToString(), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeWorldNewsSelector(item.Title), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static WorldFlagSnapshot? FindWorldFlag(IReadOnlyList<WorldFlagSnapshot> items, string selector)
    {
        var normalized = NormalizeWorldNewsSelector(selector);
        return items.FirstOrDefault(item =>
            string.Equals(item.Selector, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Index.ToString(), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeWorldNewsSelector(item.Title), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static ProgressionSnapshot? FindProgression(IReadOnlyList<ProgressionSnapshot> items, string selector)
    {
        var normalized = NormalizeWorldNewsSelector(selector);
        return items.FirstOrDefault(item =>
            string.Equals(item.Selector, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Index.ToString(), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeWorldNewsSelector(item.Title), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static WorldNewsDetailRequest ParseWorldNewsDetailRequest(string remainder)
    {
        if (string.IsNullOrWhiteSpace(remainder))
            return new WorldNewsDetailRequest(WorldNewsDetailKind.Overview, string.Empty);

        var (kindToken, selector) = SplitFirstArgument(remainder);
        var kind = ParseWorldNewsDetailKind(kindToken);
        if (kind is WorldNewsDetailKind.Unknown && IsSectionToken(kindToken))
        {
            var (sectionToken, sectionSelector) = SplitFirstArgument(selector);
            kind = ParseWorldNewsDetailKind(sectionToken);
            selector = sectionSelector;
        }

        return new WorldNewsDetailRequest(kind, NormalizeWorldNewsSelector(selector));
    }

    private static WorldNewsDetailKind ParseWorldNewsDetailKind(string token) =>
        token.Trim().ToLowerInvariant() switch
        {
            "event" or "events" or "news" or "событие" or "события" or "новость" => WorldNewsDetailKind.Event,
            "flag" or "flags" or "world_flag" or "world_flags" or "флаг" or "флаги" or "состояние" => WorldNewsDetailKind.Flag,
            "progression" or "progress" or "tracker" or "прогресс" or "прогрессия" or "трекер" or "запись" => WorldNewsDetailKind.Progression,
            _ => WorldNewsDetailKind.Unknown
        };

    private static bool IsSectionToken(string token) =>
        token.Trim().Equals("section", StringComparison.OrdinalIgnoreCase) ||
        token.Trim().Equals("раздел", StringComparison.OrdinalIgnoreCase);

    private static string BuildWorldNewsDetailCommand(string commandToken, WorldNewsDetailKind kind, string selector)
    {
        var detailWord = IsEnglishWorldNewsCommand(commandToken)
            ? kind switch
            {
                WorldNewsDetailKind.Event => "event",
                WorldNewsDetailKind.Flag => "flag",
                WorldNewsDetailKind.Progression => "progression",
                _ => "section"
            }
            : kind switch
            {
                WorldNewsDetailKind.Event => "событие",
                WorldNewsDetailKind.Flag => "флаг",
                WorldNewsDetailKind.Progression => "прогресс",
                _ => "раздел"
            };

        return commandToken + " " + detailWord + " " + FormatWorldNewsCommandArgument(selector);
    }

    private static bool IsEnglishWorldNewsCommand(string commandToken) =>
        string.Equals(commandToken, "/world_news", StringComparison.OrdinalIgnoreCase);

    private static string DescribeEventWhenWhere(JsonObject item) =>
        JoinWorldNewsDetails(
            FirstWorldNewsNodeString(item, "timestamp", "dateTime", "date", "time"),
            FirstWorldNewsNodeString(item, "location", "eventLocation", "locationName", "region"));

    private static string DescribeEventStatus(JsonObject item) =>
        JoinWorldNewsDetails(
            DescribeWorldNewsStatus(FirstWorldNewsNodeString(item, "status", "phase", "state")),
            DescribeWorldNewsVisibility(FirstWorldNewsNodeString(item, "visibility")),
            FirstWorldNewsNodeString(item, "category", "eventCategory", "type"));

    private static string DescribeFlagScope(JsonObject item) =>
        FirstNonEmpty(
            FirstWorldNewsNodeString(item, "scope", "location", "locationName", "region", "factionName", "npcName"),
            "мир");

    private static string DescribeFlagState(JsonObject item) =>
        JoinWorldNewsDetails(
            DescribeWorldNewsStatus(FirstWorldNewsNodeString(item, "status", "state")),
            DescribeFlagValue(item["value"]));

    private static string DescribeFlagValue(JsonNode? node)
    {
        if (!TryGetScalarString(node, out var value))
            return DescribeNodeForDetail(node);

        return value.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "true" => "да",
            "false" => "нет",
            "active" => "активно",
            "inactive" => "не активно",
            _ => value.Trim()
        };
    }

    private static string DescribeWorldNewsStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "active" => "идёт",
            "ongoing" => "продолжается",
            "completed" => "завершено",
            "resolved" => "разрешено",
            "failed" => "провалено",
            "abandoned" => "оставлено",
            "hidden" => "скрыто",
            "low" => "низкая",
            "medium" => "средняя",
            "high" => "высокая",
            _ => status.Trim()
        };

    private static string DescribeWorldNewsVisibility(string visibility) =>
        visibility.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "public" => "известно всем",
            "regional" => "региональные слухи",
            "player_known" => "известно герою",
            "secret" => "скрыто от героя",
            "faction-internal" => "внутри фракции",
            _ => visibility.Trim()
        };

    private static string DescribeThreatSeverity(string severity) =>
        severity.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "low" => "низкая",
            "medium" => "средняя",
            "high" => "высокая",
            "critical" => "крайняя",
            _ => severity.Trim()
        };

    private static string DescribeNodeForDetail(JsonNode? node)
    {
        if (node == null)
            return string.Empty;
        if (TryGetScalarString(node, out var scalar))
            return scalar;
        if (node is JsonArray array)
            return string.Join("; ", array.Select(DescribeNodeForDetail).Where(static value => !string.IsNullOrWhiteSpace(value)));
        if (node is JsonObject obj)
        {
            var label = FirstWorldNewsNodeString(obj, "name", "title", "displayName", "summary", "description", "value");
            if (!string.IsNullOrWhiteSpace(label))
                return label;

            return string.Join("; ", obj
                .Select(static property => $"{property.Key}: {DescribeNodeForDetail(property.Value)}")
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
        }

        return string.Empty;
    }

    private static string JoinNodeValues(JsonNode? node) => DescribeNodeForDetail(node);

    private static void AddDetailItem(List<UiKeyValueItem> items, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            items.Add(new UiKeyValueItem { Key = key, Value = value.Trim() });
    }

    private static string FirstWorldNewsNodeString(JsonNode? node, params string[] properties)
    {
        foreach (var property in properties)
        {
            var value = GetNodeString(node, property);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string? GetNodeString(JsonNode? node, string property)
    {
        if (node is not JsonObject obj)
            return null;

        return TryGetScalarString(obj[property], out var value) ? value : null;
    }

    private static bool TryGetScalarString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<string>(out var text))
        {
            value = text;
            return true;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            value = intValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            value = longValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<double>(out var doubleValue))
        {
            value = doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            value = boolValue ? "true" : "false";
            return true;
        }

        return false;
    }

    private static string ExtractCommandRemainder(string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? parts[1].Trim() : string.Empty;
    }

    private static (string First, string Remainder) SplitFirstArgument(string value)
    {
        var parts = value.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1])
        };
    }

    private static string FormatWorldNewsCommandArgument(string selector)
    {
        if (selector.All(static ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.'))
            return selector;

        return "\"" + selector.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string NormalizeWorldNewsSelector(string selector)
    {
        var trimmed = selector.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            trimmed = trimmed[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);

        return trimmed;
    }

    private static string ToActionIdPart(string value)
    {
        var chars = value
            .Select(static ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '-')
            .ToArray();
        var result = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(result) ? "item" : result;
    }

    private static string EmptyFallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "не указано" : value.Trim();

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string JoinWorldNewsDetails(params string[] parts) =>
        string.Join("; ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)).Select(static part => part.Trim()));

    private static string DescribeCount(int count, string singular, string paucal, string plural)
    {
        var abs = Math.Abs(count);
        var lastTwo = abs % 100;
        var last = abs % 10;
        var word = lastTwo is >= 11 and <= 14
            ? plural
            : last switch
            {
                1 => singular,
                2 or 3 or 4 => paucal,
                _ => plural
            };
        return $"{count} {word}";
    }

    private static void AddWorldNewsReadWarnings(List<UiBlock> blocks, WorldNewsState state)
    {
        AddWorldNewsReadWarning(blocks, state.Events, "мировых событий");
        AddWorldNewsReadWarning(blocks, state.Flags, "флагов мира");
        AddWorldNewsReadWarning(blocks, state.Progression, "прогресса мира");
        AddWorldNewsReadWarning(blocks, state.CurrentLocation, "текущей локации");
        AddWorldNewsReadWarning(blocks, state.WorldMap, "карты мира");
        AddWorldNewsReadWarning(blocks, state.NpcActivities, "активностей НПС");
        AddWorldNewsReadWarning(blocks, state.FactionProjects, "проектов фракций");
    }

    private static void AddWorldNewsReadWarning(List<UiBlock> blocks, JsonReadResult read, string section)
    {
        if (StateHasReadError(read))
            blocks.Add(Message(UiNotificationSeverity.Warning, "Новости мира", $"Запись {section} найдена, но не разобрана как JSON."));
    }

    private static bool StateHasReadError(JsonReadResult read) =>
        read.FileExists && read.Node == null && !string.IsNullOrWhiteSpace(read.Error);

    private static void AddWorldNewsRawState(List<UiBlock> blocks, JsonReadResult read, string title)
    {
        if (read.Node != null)
            blocks.Add(Raw(title, read.Node));
    }

    private static async Task<JsonReadResult> ReadJson(FileSystemManager fs, string path)
    {
        var raw = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return new JsonReadResult(path, FileExists: fs.FileExists(path), Node: null, Error: string.Empty);

        try
        {
            return new JsonReadResult(path, FileExists: true, Node: JsonNode.Parse(raw), Error: string.Empty);
        }
        catch (JsonException ex)
        {
            return new JsonReadResult(path, FileExists: true, Node: null, Error: ex.Message);
        }
    }

    private static ExplorerCommandResult Completed(
        string command,
        IEnumerable<UiBlock> blocks,
        IEnumerable<UiAction>? actions = null) =>
        new()
        {
            Command = command,
            State = CommandExecutionState.Completed,
            Blocks = blocks.ToList(),
            Actions = actions?.ToList() ?? []
        };

    private static UiMessageBlock Message(UiNotificationSeverity severity, string title, string message) =>
        new()
        {
            Severity = severity,
            Title = title,
            Message = message
        };

    private static UiRawJsonBlock Raw(string title, JsonNode node) =>
        new()
        {
            Title = title,
            Json = node.DeepClone()
        };

    private enum WorldNewsDetailKind
    {
        Overview,
        Event,
        Flag,
        Progression,
        Unknown
    }

    private sealed record WorldNewsState(
        JsonReadResult Events,
        JsonReadResult Flags,
        JsonReadResult Progression,
        JsonReadResult CurrentLocation,
        JsonReadResult WorldMap,
        JsonReadResult NpcActivities,
        JsonReadResult FactionProjects);

    private sealed record WorldEventSnapshot(int Index, string Selector, string Title, JsonObject Node);

    private sealed record WorldFlagSnapshot(int Index, string Selector, string Title, JsonObject Node);

    private sealed record ProgressionSnapshot(
        int Index,
        string Selector,
        string Title,
        string Scope,
        string Stage,
        string Status,
        JsonObject Node);

    private sealed record LocationThreatSnapshot(string Location, string Name, string Severity, string Description);

    private sealed record NpcActivitySnapshot(string NpcName, string Activity, string Location, string Status, string Description);

    private sealed record FactionProjectSnapshot(string Faction, string Project, string Status, string Description);

    private sealed record WorldNewsDetailRequest(WorldNewsDetailKind Kind, string Selector);

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);
}
