using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models.GameState;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public static class ExplorerMortalWorldCommandResultBuilder
{
    private enum CommandKind
    {
        Inventory,
        Npcs,
        Quests,
        Map,
        CurrentLocation,
        Factions,
        Skills,
        Stats,
        WorldNews,
        RivalThreads,
        GuardianCorrections,
        Locations,
        Transport,
        Effects,
        Combat,
        Weather,
        Books,
        StorageAccess,
        Interactions
    }

    private static readonly IReadOnlyDictionary<string, CommandKind> CommandKinds =
        new Dictionary<string, CommandKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["/inv"] = CommandKind.Inventory,
            ["/inventory"] = CommandKind.Inventory,
            ["/инв"] = CommandKind.Inventory,
            ["/инвентарь"] = CommandKind.Inventory,
            ["/npc"] = CommandKind.Npcs,
            ["/npcs"] = CommandKind.Npcs,
            ["/characters"] = CommandKind.Npcs,
            ["/нпс"] = CommandKind.Npcs,
            ["/персонажи"] = CommandKind.Npcs,
            ["/quests"] = CommandKind.Quests,
            ["/квесты"] = CommandKind.Quests,
            ["/map"] = CommandKind.Map,
            ["/карта"] = CommandKind.Map,
            ["/where_am_i"] = CommandKind.CurrentLocation,
            ["/где_я"] = CommandKind.CurrentLocation,
            ["/factions"] = CommandKind.Factions,
            ["/фракции"] = CommandKind.Factions,
            ["/skills"] = CommandKind.Skills,
            ["/навыки"] = CommandKind.Skills,
            ["/stats"] = CommandKind.Stats,
            ["/статы"] = CommandKind.Stats,
            ["/характеристики"] = CommandKind.Stats,
            ["/world_news"] = CommandKind.WorldNews,
            ["/новости_мира"] = CommandKind.WorldNews,
            ["/rival_threads"] = CommandKind.RivalThreads,
            ["/чужие_нити"] = CommandKind.RivalThreads,
            ["/guardian_corrections"] = CommandKind.GuardianCorrections,
            ["/коррективы_хранителя"] = CommandKind.GuardianCorrections,
            ["/locations"] = CommandKind.Locations,
            ["/локации"] = CommandKind.Locations,
            ["/transport"] = CommandKind.Transport,
            ["/транспорт"] = CommandKind.Transport,
            ["/effects"] = CommandKind.Effects,
            ["/эффекты"] = CommandKind.Effects,
            ["/combat"] = CommandKind.Combat,
            ["/бой"] = CommandKind.Combat,
            ["/weather"] = CommandKind.Weather,
            ["/погода"] = CommandKind.Weather,
            ["/books"] = CommandKind.Books,
            ["/книги"] = CommandKind.Books,
            ["/читать"] = CommandKind.Books,
            ["/storage_access"] = CommandKind.StorageAccess,
            ["/доступ_к_хранилищам"] = CommandKind.StorageAccess,
            ["/interactions"] = CommandKind.Interactions,
            ["/взаимодействия"] = CommandKind.Interactions
        };

    public static bool CanBuild(string command) =>
        CommandKinds.ContainsKey(ExplorerCommandCatalog.ExtractCommandToken(command.Trim()));

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs)
    {
        var normalizedCommand = command.Trim();
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(normalizedCommand);
        if (!CommandKinds.TryGetValue(commandToken, out var kind))
            return null;

        await stateManager.RefreshGameStateAsync();

        return kind switch
        {
            CommandKind.Inventory => await BuildInventory(commandToken, fs),
            CommandKind.Npcs => await BuildNpcs(commandToken, fs),
            CommandKind.Quests => await BuildBundle(commandToken, fs, "Квесты", [
                new("game_state/quests/regular_quests.json", "quests|activeQuests", "Активных"),
                new("game_state/quests/regular_quests.json", "completedQuests", "Завершённых"),
                new("game_state/quests/quest_history.json", "questHistory|entries", "Исторических записей"),
                new("game_state/quests/plot_outline.json", "plotOutline|entries", "Сюжетных записей")
            ]),
            CommandKind.Map => await BuildMap(commandToken, fs),
            CommandKind.CurrentLocation => await BuildBundle(commandToken, fs, "Где я", [
                new("game_state/world/current_location.json", "locationName", "Локация"),
                new("game_state/world/current_location.json", "region", "Регион"),
                new("game_state/world/current_location.json", "description", "Описание")
            ]),
            CommandKind.Factions => await BuildBundle(commandToken, fs, "Фракции", [
                new("game_state/factions/faction_core.json", "factions", "Фракций"),
                new("game_state/factions/faction_projects.json", "entries", "Проектов"),
                new("game_state/factions/faction_chronicles.json", "entries", "Хроник"),
                new("game_state/factions/faction_custom.json", "entries", "Особых состояний")
            ]),
            CommandKind.Skills => await BuildBundle(commandToken, fs, "Навыки", [
                new("game_state/player/skills_active.json", "activeSkillChanges|skills", "Активных"),
                new("game_state/player/skills_passive.json", "passiveSkillChanges|skills", "Пассивных")
            ]),
            CommandKind.Stats => await BuildStats(commandToken, fs, stateManager),
            CommandKind.WorldNews => await BuildBundle(commandToken, fs, "Новости мира", [
                new("game_state/world/world_events.json", "worldEventsLog|events", "Событий"),
                new("game_state/world/world_flags.json", "worldStateFlags|flags", "Флагов"),
                new("game_state/world/progression.json", "entries", "Записей прогресса")
            ]),
            CommandKind.RivalThreads => await BuildBundle(commandToken, fs, "Чужие нити", [
                new(RivalSoulArcService.StatePath, "rivalSoulArcs", "Арк соперников"),
                new(RivalSoulArcService.StatePath, "arcs", "Арк")
            ]),
            CommandKind.GuardianCorrections => await BuildBundle(commandToken, fs, "Коррективы Хранителя", [
                new(GuardianCorrectionService.StatePath, "corrections", "Корректив")
            ]),
            CommandKind.Locations => await BuildLocations(commandToken, fs),
            CommandKind.Transport => await BuildTransport(commandToken, fs),
            CommandKind.Effects => await BuildEffects(commandToken, fs, stateManager),
            CommandKind.Combat => await BuildBundle(commandToken, fs, "Бой", [
                new("game_state/combat/enemies.json", "enemiesData|enemies", "Врагов"),
                new("game_state/combat/allies.json", "alliesData|allies", "Союзников"),
                new("game_state/combat/combat_log.json", "combat_log_markdown|entries", "Записей журнала")
            ]),
            CommandKind.Weather => await BuildBundle(commandToken, fs, "Время и погода", [
                new("game_state/world/world_time.json", "timeOfDay|currentTime", "Время"),
                new("game_state/world/weather.json", "tendency|currentState", "Погода"),
                new("game_state/world/weather.json", "description", "Описание")
            ]),
            CommandKind.Books => await BuildBooks(normalizedCommand, fs),
            CommandKind.StorageAccess => await BuildBundle(commandToken, fs, "Доступ к хранилищам", [
                new("game_state/misc/storage_access.json", "grantStorageAccess|storages", "Хранилищ"),
                new("game_state/misc/storage_access.json", "entries", "Записей")
            ]),
            CommandKind.Interactions => await BuildBundle(commandToken, fs, "Взаимодействия игроков", [
                new("game_state/misc/player_interactions.json", "otherPlayersInteractions|interactions", "Взаимодействий"),
                new("game_state/misc/player_interactions.json", "entries", "Записей")
            ]),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildStats(string command, FileSystemManager fs, StateManager stateManager)
    {
        var state = stateManager.CurrentState;
        var blocks = new List<UiBlock>
        {
            Panel("Характеристики",
                Grid(
                    ("Здоровье", EmptyFallback(state.PlayerStatus.HealthPercentage)),
                    ("Энергия", EmptyFallback(state.PlayerStatus.EnergyPercentage)),
                    ("Равновесие", EmptyFallback(state.PlayerStatus.PoisePercentage))))
        };

        await AddRawJsonIfPresent(blocks, fs, "game_state/misc/characteristics.json", "JSON: characteristics");
        await AddRawJsonIfPresent(blocks, fs, "game_state/player/computed_characteristics.json", "JSON: computed_characteristics");
        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildMap(string command, FileSystemManager fs)
    {
        var map = await LocalMapViewService.BuildCurrentRealmMapAsync(fs);
        var blocks = new List<UiBlock>
        {
            new UiMapBlock
            {
                Title = "Карта",
                Map = map
            },
            new UiTableBlock
            {
                Title = "Сводка карты",
                Columns = ["Показатель", "Значение"],
                Rows =
                [
                    new UiTableRow { Cells = ["Царство", map.Realm] },
                    new UiTableRow { Cells = ["Локаций", map.Nodes.Count.ToString()] },
                    new UiTableRow { Cells = ["Связей", map.Links.Count.ToString()] },
                    new UiTableRow { Cells = ["Уровней z", map.ZLevels.Count.ToString()] }
                ]
            }
        };

        if (string.Equals(map.Realm, "Chaos Sea", StringComparison.OrdinalIgnoreCase))
        {
            await AddRawJsonIfPresent(blocks, fs, "game_state/meta/soul_state.json", "JSON: soul_state");
            await AddRawJsonIfPresent(blocks, fs, "game_state/meta/guardians.json", "JSON: guardians");
        }
        else if (string.Equals(map.Realm, "Shining Abode", StringComparison.OrdinalIgnoreCase))
        {
            await AddRawJsonIfPresent(blocks, fs, "game_state/meta/soul_state.json", "JSON: soul_state");
            await AddRawJsonIfPresent(blocks, fs, "game_state/meta/shining_abode_state.json", "JSON: shining_abode_state");
        }
        else
        {
            await AddRawJsonIfPresent(blocks, fs, "game_state/world/current_location.json", "JSON: current_location");
            await AddRawJsonIfPresent(blocks, fs, "game_state/world/world_map.json", "JSON: world_map");
        }

        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildEffects(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        const string title = "Эффекты";
        const string effectsPath = "game_state/player/effects.json";
        SummarySpec[] specs =
        [
            new(effectsPath, "activeEffects", "Активных эффектов"),
            new(effectsPath, "wounds", "Ран"),
            new(effectsPath, "temporaryConditions", "Временных состояний")
        ];

        var read = await ReadJson(fs, effectsPath);
        var blocks = new List<UiBlock>();
        var rows = BuildEffectsSummaryRows(read, specs);
        if (rows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = title,
                Columns = ["Раздел", "Состояние"],
                Rows = rows
            });
        }

        var detailRows = BuildEffectDetailRows(read.Node);
        if (detailRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Подробности эффектов",
                Columns = ["Раздел", "Название", "Описание", "Длительность"],
                Rows = detailRows
            });
        }

        if (!HasVisibleStructuredEffectDetails(read.Node))
            blocks.AddRange(BuildMortalStatusFallbackBlocks(stateManager.CurrentState.PlayerStatus));

        if (blocks.Count == 0)
            blocks.Add(Message(UiNotificationSeverity.Info, title, "Данные ещё не созданы."));

        if (read.Node != null)
            blocks.Add(Raw("Полная запись эффектов", read.Node));
        else if (read.FileExists && !string.IsNullOrWhiteSpace(read.Error))
            blocks.Add(Message(UiNotificationSeverity.Warning, title, $"Запись эффектов найдена, но не разобрана как JSON. {read.Error}"));

        return Completed(command, blocks);
    }

    private static List<UiTableRow> BuildEffectsSummaryRows(JsonReadResult read, IReadOnlyList<SummarySpec> specs)
    {
        var rows = new List<UiTableRow>();
        if (read.Node == null)
            return rows;

        if (read.Node is JsonArray array)
        {
            rows.Add(new UiTableRow
            {
                Cells = ["Активных эффектов", array.Count.ToString()]
            });
            return rows;
        }

        if (read.Node is not JsonObject root)
            return rows;

        foreach (var spec in specs)
        {
            if (!root.TryGetPropertyValue(spec.PropertyName, out var value))
                continue;

            rows.Add(new UiTableRow
            {
                Cells =
                [
                    spec.Label,
                    DescribeEffectsNode(value)
                ]
            });
        }

        return rows;
    }

    private static List<UiTableRow> BuildEffectDetailRows(JsonNode? node)
    {
        var rows = new List<UiTableRow>();
        if (node is JsonArray array)
        {
            AddEffectDetailRows(rows, "Активный эффект", array);
            return rows;
        }

        if (node is not JsonObject root)
            return rows;

        AddEffectDetailRows(rows, "Активный эффект", root["activeEffects"] as JsonArray);
        AddEffectDetailRows(rows, "Рана", root["wounds"] as JsonArray);
        AddEffectDetailRows(rows, "Временное состояние", root["temporaryConditions"] as JsonArray);
        return rows;
    }

    private static void AddEffectDetailRows(List<UiTableRow> rows, string section, JsonArray? effects)
    {
        if (effects == null)
            return;

        foreach (var effect in effects.OfType<JsonObject>())
        {
            var name = FirstNonEmpty(
                GetNodeString(effect, "name"),
                GetNodeString(effect, "effectName"),
                GetNodeString(effect, "title"),
                "Безымянный эффект");
            var description = FirstNonEmpty(
                GetNodeString(effect, "effectDescription"),
                GetNodeString(effect, "description"),
                GetNodeString(effect, "source"),
                "не указано");
            var duration = FirstNonEmpty(
                GetNodeString(effect, "duration"),
                GetNodeString(effect, "expiresAt"),
                "не указано");

            rows.Add(new UiTableRow
            {
                Cells = [section, name, description, duration]
            });
        }
    }

    private static string DescribeEffectsNode(JsonNode? node) => node switch
    {
        JsonArray array => array.Count.ToString(),
        JsonObject obj => $"{obj.Count} полей",
        JsonValue value when TryGetScalarString(value, out var text) => EmptyFallback(text),
        _ => "не указано"
    };

    private static bool HasVisibleStructuredEffectDetails(JsonNode? node)
    {
        if (node is JsonArray array)
            return array.Count > 0;

        if (node is not JsonObject root)
            return false;

        foreach (var propertyName in new[] { "activeEffects", "wounds", "temporaryConditions" })
        {
            if (root.TryGetPropertyValue(propertyName, out var value) && HasVisibleEffectValue(value))
                return true;
        }

        return false;
    }

    private static bool HasVisibleEffectValue(JsonNode? node) => node switch
    {
        JsonArray array => array.Count > 0,
        JsonObject obj => obj.Count > 0,
        JsonValue value when TryGetScalarString(value, out var text) => !string.IsNullOrWhiteSpace(text),
        _ => false
    };

    private static IReadOnlyList<UiBlock> BuildMortalStatusFallbackBlocks(PlayerStatusState status)
    {
        var rows = MortalStatusEffectFallback.BuildRows(status);
        if (rows.Count == 0)
            return [];

        return
        [
            Message(
                UiNotificationSeverity.Info,
                "Эффекты",
                MortalStatusEffectFallback.Message),
            new UiTableBlock
            {
                Title = "Видимые состояния",
                Columns = ["Раздел", "Подробности"],
                Rows = rows
                    .Select(static row => new UiTableRow { Cells = [row.Label, row.Details] })
                    .ToList()
            }
        ];
    }

    private static async Task<ExplorerCommandResult> BuildNpcs(string command, FileSystemManager fs)
    {
        SummarySpec[] specs =
        [
            new("game_state/npcs/npc_relationships.json", "entries", "Отношений"),
            new("game_state/npcs/npc_activities.json", "entries", "Активностей"),
            new("game_state/npcs/npc_custom_states.json", "entries", "Особых состояний")
        ];

        var coreRead = await ReadJson(fs, "game_state/npcs/npc_core.json");
        if (!NpcJournalFallbackProjection.HasVisibleNpcCore(coreRead.Node))
        {
            var fallbackEntries = await NpcJournalFallbackProjection.ReadAsync(fs);
            if (fallbackEntries.Count > 0)
            {
                var rows = NpcJournalFallbackProjection.BuildConsoleRows(fallbackEntries);
                return Completed(command, NpcJournalFallbackProjection.BuildBlocks(rows));
            }
        }

        var reads = new Dictionary<string, JsonReadResult>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/npcs/npc_core.json"] = coreRead
        };
        foreach (var path in specs.Select(static spec => spec.Path).Distinct(StringComparer.OrdinalIgnoreCase))
            reads[path] = await ReadJson(fs, path);

        reads["game_state/npcs/npc_goals.json"] = await ReadJson(fs, "game_state/npcs/npc_goals.json");
        reads["game_state/npcs/npc_inventory.json"] = await ReadJson(fs, "game_state/npcs/npc_inventory.json");
        reads["game_state/npcs/npc_effects.json"] = await ReadJson(fs, "game_state/npcs/npc_effects.json");
        reads["game_state/npcs/npc_skills.json"] = await ReadJson(fs, "game_state/npcs/npc_skills.json");
        reads["game_state/npcs/npc_personality.json"] = await ReadJson(fs, "game_state/npcs/npc_personality.json");
        reads["game_state/npcs/npc_journals.json"] = await ReadJson(fs, "game_state/npcs/npc_journals.json");
        reads[NpcInteractionJournalState.StatePath] = await ReadJson(fs, NpcInteractionJournalState.StatePath);
        reads["game_state/npcs/npc_masks.json"] = await ReadJson(fs, "game_state/npcs/npc_masks.json");
        reads["game_state/npcs/npc_memory.json"] = await ReadJson(fs, "game_state/npcs/npc_memory.json");
        reads["game_state/npcs/npc_fate_cards.json"] = await ReadJson(fs, "game_state/npcs/npc_fate_cards.json");

        var blocks = new List<UiBlock>();
        var summaryRows = NpcDetailSectionProjection.BuildNpcOverviewRows(coreRead.Node).ToList();
        foreach (var spec in specs)
        {
            var read = reads[spec.Path];
            var status = DescribeSpec(read, spec.PropertyName);
            if (status == "отсутствует")
                continue;

            summaryRows.Add(new UiTableRow
            {
                Cells = [spec.Label, status]
            });
        }

        if (summaryRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Персонажи",
                Columns = ["Раздел", "Состояние"],
                Rows = summaryRows
            });
        }

        var projections = NpcDetailSectionProjection.BuildAll(
            coreRead.Node,
            new NpcDetailSectionDocuments(
                Relationships: reads["game_state/npcs/npc_relationships.json"].Node,
                Goals: reads["game_state/npcs/npc_goals.json"].Node,
                Activities: reads["game_state/npcs/npc_activities.json"].Node,
                Inventory: reads["game_state/npcs/npc_inventory.json"].Node,
                Effects: reads["game_state/npcs/npc_effects.json"].Node,
                Skills: reads["game_state/npcs/npc_skills.json"].Node,
                Personality: reads["game_state/npcs/npc_personality.json"].Node,
                Journals: reads["game_state/npcs/npc_journals.json"].Node,
                InteractionJournal: reads[NpcInteractionJournalState.StatePath].Node,
                Masks: reads["game_state/npcs/npc_masks.json"].Node,
                Memory: reads["game_state/npcs/npc_memory.json"].Node,
                FateCards: reads["game_state/npcs/npc_fate_cards.json"].Node,
                CustomStates: reads["game_state/npcs/npc_custom_states.json"].Node));

        if (projections.Count > 0)
        {
            blocks.Add(NpcDetailSectionProjection.BuildSectionSummaryTable(projections));
            blocks.AddRange(projections.SelectMany(static projection => projection.Sections.SelectMany(static section => section.Blocks)));
        }

        foreach (var read in reads.Values)
        {
            if (read.FileExists && read.Node == null && !string.IsNullOrWhiteSpace(read.Error))
                blocks.Add(Message(UiNotificationSeverity.Warning, "Персонажи", "Одна из записей НПС найдена, но не разобрана как JSON."));
        }

        if (blocks.Count == 0)
            blocks.Add(Message(UiNotificationSeverity.Info, "Персонажи", "Данные ещё не созданы."));

        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildBundle(string command, FileSystemManager fs, string title, IReadOnlyList<SummarySpec> specs)
    {
        var grouped = specs.GroupBy(static spec => spec.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        var reads = new Dictionary<string, JsonReadResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in grouped)
            reads[group.Key] = await ReadJson(fs, group.Key);

        var rows = new List<UiTableRow>();
        foreach (var spec in specs)
        {
            var read = reads[spec.Path];
            var status = DescribeSpec(read, spec.PropertyName);
            if (status == "отсутствует")
                continue;

            rows.Add(new UiTableRow
            {
                Cells =
                [
                    spec.Label,
                    status
                ]
            });
        }

        var blocks = new List<UiBlock>();
        if (rows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = title,
                Columns = ["Раздел", "Состояние"],
                Rows = rows
            });
        }
        else
        {
            blocks.Add(Message(UiNotificationSeverity.Info, title, "Данные ещё не созданы."));
        }

        foreach (var (path, read) in reads)
        {
            if (read.Node != null)
                blocks.Add(Raw($"Полный JSON {path}", read.Node));
            else if (read.FileExists)
                blocks.Add(Message(UiNotificationSeverity.Warning, title, $"Файл найден, но не разобран как JSON: {path}. {read.Error}"));
        }

        return Completed(command, blocks);
    }

    private static string DescribeSpec(JsonReadResult read, string propertyName)
    {
        if (read.Node == null)
            return read.FileExists ? "повреждён" : "отсутствует";

        var node = ResolveSpecNode(read.Node, propertyName);
        if (node == null)
            return "отсутствует";

        return DescribeSummaryNode(node);
    }

    private static JsonNode? ResolveSpecNode(JsonNode root, string propertyName)
    {
        if (root is not JsonObject obj)
            return null;

        foreach (var candidate in propertyName.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (obj.TryGetPropertyValue(candidate, out var value) && value != null)
                return value;
        }

        return null;
    }

    private static string DescribeSummaryNode(JsonNode node)
    {
        return node switch
        {
            JsonArray array => DescribeSummaryArray(array),
            JsonObject obj => DescribeSummaryObject(obj),
            JsonValue value when TryGetScalarString(value, out var text) => EmptyFallback(text),
            _ => "найдено"
        };
    }

    private static string DescribeSummaryArray(JsonArray array)
    {
        if (array.Count == 0)
            return "0";

        var preview = string.Join(", ", array
            .Take(3)
            .Select(PreviewSummaryNode)
            .Where(static value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(preview) ? array.Count.ToString() : $"{array.Count}: {preview}";
    }

    private static string DescribeSummaryObject(JsonObject obj)
    {
        if (obj.Count == 0)
            return "0 полей";

        var preview = PreviewSummaryNode(obj);
        return string.IsNullOrWhiteSpace(preview) ? $"{obj.Count} полей" : $"{obj.Count} полей: {preview}";
    }

    private static string PreviewSummaryNode(JsonNode? node)
    {
        if (node == null)
            return string.Empty;

        if (node is JsonArray array)
        {
            return string.Join(", ", array
                .Take(3)
                .Select(PreviewSummaryNode)
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
        }

        if (node is JsonObject obj)
        {
            var direct = FirstNonEmpty(
                GetNodeString(obj, "title"),
                GetNodeString(obj, "questName"),
                GetNodeString(obj, "skillName"),
                GetNodeString(obj, "displayName"),
                GetNodeString(obj, "displayNameOrMoniker"),
                GetNodeString(obj["rivalSoul"], "displayNameOrMoniker"),
                GetNodeString(obj["sponsorGuardianRef"], "displayName"),
                GetNodeString(obj, "name"),
                GetNodeString(obj, "enemyName"),
                GetNodeString(obj, "allyName"),
                GetNodeString(obj, "storageName"),
                GetNodeString(obj, "message"),
                GetNodeString(obj, "summary"),
                GetNodeString(obj, "description"),
                GetNodeString(obj, "objective"));
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;

            foreach (var child in obj)
            {
                var nested = PreviewSummaryNode(child.Value);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }

            return string.Empty;
        }

        return TryGetScalarString(node, out var text) ? EmptyFallback(text) : string.Empty;
    }

    private static async Task<ExplorerCommandResult> BuildLocations(string command, FileSystemManager fs)
    {
        const string title = "Локации";
        var currentRead = await ReadJson(fs, "game_state/world/current_location.json");
        var mapRead = await ReadJson(fs, "game_state/world/world_map.json");
        var rows = new List<UiTableRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (UnwrapCurrentLocationNode(currentRead.Node) is { } current)
        {
            AddLocationRow(rows, seen, "Текущая", current, DescribeCurrentLocation(current));

            if (current["adjacencyMap"] is JsonArray adjacency)
            {
                foreach (var entry in adjacency.OfType<JsonObject>())
                {
                    var name = FirstNonEmpty(
                        GetLocationNodeString(entry, "name", "targetLocationName"),
                        GetLocationNodeString(entry, "targetLocationId"),
                        "Неизвестная локация");
                    var details = JoinLocationDetails(
                        GetLocationNodeString(entry, "direction"),
                        GetLocationNodeString(entry, "distance"),
                        DescribeLinkState(GetLocationNodeString(entry, "linkState")));
                    var key = FirstNonEmpty(GetLocationNodeString(entry, "targetLocationId"), name);
                    if (seen.Add($"adjacent:{key}"))
                    {
                        rows.Add(new UiTableRow
                        {
                            Cells = ["Рядом", name, EmptyFallback(details)]
                        });
                    }
                }
            }
        }

        foreach (var location in EnumerateWorldMapLocationObjects(mapRead.Node, "newLocations"))
            AddLocationRow(rows, seen, "Открыта", location, DescribeWorldMapLocation(location));

        foreach (var location in EnumerateWorldMapLocationObjects(mapRead.Node, "locationUpdates"))
            AddLocationRow(rows, seen, "Обновлена", location, DescribeWorldMapLocation(location));

        var blocks = new List<UiBlock>();
        if (rows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = title,
                Columns = ["Раздел", "Локация", "Сведения"],
                Rows = rows
            });
        }
        else
        {
            blocks.Add(Message(UiNotificationSeverity.Info, title, "Локации пока не обнаружены."));
        }

        AddLocationRawState(blocks, title, currentRead);
        AddLocationRawState(blocks, title, mapRead);
        return Completed(command, blocks);
    }

    private static JsonObject? UnwrapCurrentLocationNode(JsonNode? node)
    {
        if (node is not JsonObject root)
            return null;

        return root["currentLocationData"] as JsonObject ?? root;
    }

    private static IEnumerable<JsonObject> EnumerateWorldMapLocationObjects(JsonNode? node, string propertyName)
    {
        if (node is not JsonObject root)
            yield break;

        foreach (var location in EnumerateLocationArray(root, propertyName))
            yield return location;

        if (root["worldMapUpdates"] is not JsonObject wrappedRoot)
            yield break;

        foreach (var location in EnumerateLocationArray(wrappedRoot, propertyName))
            yield return location;
    }

    private static IEnumerable<JsonObject> EnumerateLocationArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is not JsonArray array)
            yield break;

        foreach (var node in array.OfType<JsonObject>())
            yield return node;
    }

    private static void AddLocationRow(
        List<UiTableRow> rows,
        HashSet<string> seen,
        string section,
        JsonObject location,
        string details)
    {
        var name = FirstNonEmpty(GetLocationNodeString(location, "name", "locationName", "displayName"), "Безымянная локация");
        var key = StableLocationNodeKey(location, name);
        if (!seen.Add(key))
            return;

        rows.Add(new UiTableRow
        {
            Cells = [section, name, EmptyFallback(details)]
        });
    }

    private static string StableLocationNodeKey(JsonObject location, string fallbackName) =>
        FirstNonEmpty(
            GetLocationNodeString(location, "locationId", "id", "targetLocationId"),
            fallbackName).Trim();

    private static string DescribeCurrentLocation(JsonObject location) =>
        JoinLocationDetails(
            GetLocationNodeString(location, "region"),
            GetLocationNodeString(location, "locationType"),
            GetLocationNodeString(location, "description", "shortDescription"));

    private static string DescribeWorldMapLocation(JsonObject location) =>
        JoinLocationDetails(
            GetLocationNodeString(location, "locationType"),
            GetLocationNodeString(location, "indoorType"),
            GetLocationNodeString(location, "shortDescription", "description"),
            GetLocationNodeString(location, "lastEventsDescription"));

    private static string DescribeLinkState(string linkState)
    {
        if (string.IsNullOrWhiteSpace(linkState) ||
            string.Equals(linkState, "safe", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return $"состояние пути: {linkState}";
    }

    private static string JoinLocationDetails(params string[] parts) =>
        string.Join("; ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)).Select(static part => part.Trim()));

    private static string GetLocationNodeString(JsonNode? node, params string[] properties)
    {
        foreach (var property in properties)
        {
            var value = GetNodeString(node, property);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static void AddLocationRawState(List<UiBlock> blocks, string title, JsonReadResult read)
    {
        if (read.Node != null)
            blocks.Add(Raw($"Полный JSON {read.Path}", read.Node));
        else if (read.FileExists)
            blocks.Add(Message(UiNotificationSeverity.Warning, title, $"Файл найден, но не разобран как JSON: {read.Path}. {read.Error}"));
    }

    private static async Task<ExplorerCommandResult> BuildTransport(string command, FileSystemManager fs)
    {
        const string title = "Транспорт";
        var vehiclesRead = await ReadJson(fs, "game_state/misc/vehicles.json");
        var mapRead = await ReadJson(fs, "game_state/world/world_map.json");
        var currentRead = await ReadJson(fs, "game_state/world/current_location.json");
        var blocks = new List<UiBlock>();

        var vehicleRows = EnumerateVehicleObjects(vehiclesRead.Node)
            .Select(static vehicle => new UiTableRow
            {
                Cells =
                [
                    FirstNonEmpty(GetNodeString(vehicle, "name"), GetNodeString(vehicle, "vehicleName"), "Безымянный транспорт"),
                    DescribeVehicleType(vehicle),
                    DescribeVehicleNode(vehicle)
                ]
            })
            .ToList();

        if (vehicleRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = title,
                Columns = ["Транспорт", "Тип", "Сведения"],
                Rows = vehicleRows
            });
        }

        var summaryRows = new List<UiTableRow>();
        AddTransportSummaryRow(summaryRows, vehiclesRead, "vehicles|UpdateVehicles", "Транспорта");
        AddTransportSummaryRow(summaryRows, mapRead, "transportRoutes", "Маршрутов");
        AddTransportSummaryRow(summaryRows, currentRead, "availableTransport", "Доступного транспорта");
        if (summaryRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Сводка транспорта",
                Columns = ["Раздел", "Состояние"],
                Rows = summaryRows
            });
        }

        if (blocks.Count == 0)
            blocks.Add(Message(UiNotificationSeverity.Info, title, "Транспорт пока не обнаружен."));

        AddTransportRawState(blocks, title, vehiclesRead);
        AddTransportRawState(blocks, title, mapRead);
        AddTransportRawState(blocks, title, currentRead);
        return Completed(command, blocks);
    }

    private static IEnumerable<JsonObject> EnumerateVehicleObjects(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var vehicle in array.OfType<JsonObject>())
                yield return vehicle;
            yield break;
        }

        if (node is not JsonObject root)
            yield break;

        foreach (var propertyName in new[] { "vehicles", "UpdateVehicles" })
        {
            if (root[propertyName] is not JsonArray vehicles)
                continue;

            foreach (var vehicle in vehicles.OfType<JsonObject>())
                yield return vehicle;
        }
    }

    private static string DescribeVehicleNode(JsonObject vehicle)
    {
        var parts = new[]
        {
            DescribeVehicleAvailability(vehicle),
            FirstNonEmpty(GetNodeString(vehicle, "currentLocationId"), GetNodeString(vehicle, "currentLocation")),
            GetNodeString(vehicle, "description") ?? string.Empty,
            GetNodeString(vehicle, "capacity") ?? string.Empty
        };

        return EmptyFallback(JoinLocationDetails(parts));
    }

    private static string DescribeVehicleType(JsonObject vehicle)
    {
        var type = FirstNonEmpty(GetNodeString(vehicle, "type"), GetNodeString(vehicle, "vehicleType"));
        return type.Trim().ToLowerInvariant() switch
        {
            "" => "не указано",
            "mount" => "Ездовое животное",
            "vehicle" => "Транспорт",
            "summonable" => "Призываемый",
            _ => type.Trim()
        };
    }

    private static string DescribeVehicleAvailability(JsonObject vehicle)
    {
        var availability = FirstNonEmpty(GetNodeString(vehicle, "availability"), GetNodeString(vehicle, "status"));
        if (!string.IsNullOrWhiteSpace(availability))
        {
            return availability.Trim().ToLowerInvariant() switch
            {
                "active" => "Активен (оседлан/управляется)",
                "parked" => "Припаркован",
                "pocket" => "В кармане (призываемый)",
                _ => availability.Trim()
            };
        }

        if (TryGetNodeBool(vehicle, "isActive", out var isActive))
            return isActive ? "Активен" : "Неактивен";

        return string.Empty;
    }

    private static void AddTransportSummaryRow(
        List<UiTableRow> rows,
        JsonReadResult read,
        string propertyName,
        string label)
    {
        var status = DescribeSpec(read, propertyName);
        if (status == "отсутствует")
            return;

        rows.Add(new UiTableRow { Cells = [label, status] });
    }

    private static void AddTransportRawState(List<UiBlock> blocks, string title, JsonReadResult read)
    {
        if (read.Node != null)
            blocks.Add(Raw($"Полный JSON {read.Path}", read.Node));
        else if (read.FileExists)
            blocks.Add(Message(UiNotificationSeverity.Warning, title, $"Файл найден, но не разобран как JSON: {read.Path}. {read.Error}"));
    }

    private static async Task<ExplorerCommandResult> BuildBooks(string command, FileSystemManager fs)
    {
        const string title = "Книжная полка";
        var inventoryRead = await ReadJson(fs, "game_state/inventory/items.json");
        var textRead = await ReadJson(fs, "game_state/inventory/item_text_updates.json");
        var journalRead = await ReadJson(fs, "game_state/npcs/item_journals.json");
        var shelf = ReadableInventoryDocumentShelfProjection.Build(
            inventoryRead.Node,
            textRead.Node,
            journalRead.Node);
        var selector = ExtractCommandRemainder(command);
        var blocks = new List<UiBlock>();

        if (!string.IsNullOrWhiteSpace(selector))
        {
            var selected = ReadableInventoryDocumentShelfProjection.FindBySelector(shelf, selector);
            if (selected == null)
            {
                blocks.Add(Message(UiNotificationSeverity.Warning, title, "Такой документ не найден на книжной полке."));
            }
            else
            {
                blocks.Add(BuildBookDetailBlock(selected));
                blocks.Add(new UiTextBlock { Text = "Вернуться к списку можно командой /books.", Tone = UiTone.Muted });
            }

            AddBookReadWarning(blocks, title, inventoryRead);
            AddBookReadWarning(blocks, title, textRead);
            AddBookReadWarning(blocks, title, journalRead);

            return Completed(command, blocks, [
                new UiAction
                {
                    Id = "books-back",
                    Label = "Назад к книжной полке",
                    Command = "/books",
                    Style = UiActionStyle.Secondary,
                    RequiresConfirmation = false
                }
            ]);
        }

        if (shelf.Items.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = title,
                Columns = ["Документ", "Источник", "Доступ", "Кратко"],
                Rows = shelf.Items
                    .Select(static item => new UiTableRow
                    {
                        Cells = [item.Title, item.Source, item.AccessStatus, item.Summary]
                    })
                    .ToList()
            });
        }
        else
        {
            blocks.Add(Message(UiNotificationSeverity.Info, title, "Данные ещё не созданы."));
        }

        AddBookReadWarning(blocks, title, inventoryRead);
        AddBookReadWarning(blocks, title, textRead);
        AddBookReadWarning(blocks, title, journalRead);

        return Completed(command, blocks, BuildBookReadActions(shelf));
    }

    private static UiPanelBlock BuildBookDetailBlock(ReadableDocumentShelfItem item)
    {
        var blocks = new List<UiBlock>
        {
            new UiKeyValueGridBlock
            {
                Items =
                [
                    new UiKeyValueItem { Key = "Источник", Value = item.Source },
                    new UiKeyValueItem { Key = "Доступ", Value = item.AccessStatus }
                ]
            }
        };

        if (item.HasReadableContent)
        {
            for (var index = 0; index < item.Entries.Count; index++)
            {
                var prefix = item.Entries.Count == 1
                    ? string.Empty
                    : $"Запись {index + 1}. ";
                blocks.Add(new UiTextBlock { Text = prefix + item.Entries[index], Tone = UiTone.Default });
            }
        }
        else
        {
            blocks.Add(new UiMessageBlock
            {
                Severity = UiNotificationSeverity.Warning,
                Title = "Текст недоступен",
                Message = item.UnreadableReason ?? "Текст пока недоступен."
            });
        }

        return new UiPanelBlock
        {
            Title = $"Чтение: {item.Title}",
            Blocks = blocks
        };
    }

    private static IReadOnlyList<UiAction> BuildBookReadActions(ReadableDocumentShelf shelf)
    {
        var actions = new List<UiAction>();
        foreach (var item in shelf.Items)
        {
            actions.Add(new UiAction
            {
                Id = "books-read-" + item.Selector,
                Label = $"Читать «{item.Title}»",
                Command = "/books " + ReadableInventoryDocumentShelfProjection.FormatCommandArgument(item.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["documentId"] = item.Selector,
                    ["title"] = item.Title,
                    ["source"] = item.Source,
                    ["accessStatus"] = item.AccessStatus,
                    ["entryCount"] = item.Entries.Count
                }
            });
        }

        return actions;
    }

    private static async Task<ExplorerCommandResult> BuildInventory(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, "game_state/inventory/items.json");
        if (read.Node == null)
        {
            return Completed(command, [
                Message(UiNotificationSeverity.Info, "Инвентарь", "Инвентарь пуст или данные ещё не созданы.")
            ]);
        }

        var blocks = new List<UiBlock>();
        var inventoryContext = await InventoryEquipmentService.ReadContextAsync(fs);
        var root = inventoryContext?.Root ?? read.Node as JsonObject;
        if (root == null)
        {
            return Completed(command, [
                Message(UiNotificationSeverity.Warning, "Инвентарь", "Файл инвентаря найден, но его корень не похож на обычный инвентарь.")
            ]);
        }

        var totalWeight = GetNodeString(root, "totalWeight");
        var maxWeight = GetNodeString(root, "maxWeight");
        if (!string.IsNullOrEmpty(totalWeight))
        {
            var weightText = !string.IsNullOrEmpty(maxWeight)
                ? $"⚖ {totalWeight} / {maxWeight}"
                : $"⚖ {totalWeight}";
            blocks.Add(new UiTextBlock { Text = weightText, Tone = UiTone.Muted });
        }

        var money = GetNodeString(root, "money");
        if (!string.IsNullOrEmpty(money) && money != "0")
            blocks.Add(new UiTextBlock { Text = $"💰 Деньги: {money}", Tone = UiTone.Default });

        if (root["resources"] is JsonObject resources && resources.Count > 0)
        {
            var resourceItems = new List<UiKeyValueItem>();
            foreach (var prop in resources)
            {
                if (prop.Key is "money" or "gold" or "coins")
                    continue;

                var value = prop.Value?.ToString() ?? string.Empty;
                if (!string.IsNullOrEmpty(value) && value != "0")
                    resourceItems.Add(new UiKeyValueItem { Key = $"💎 {prop.Key}", Value = value });
            }

            if (resourceItems.Count > 0)
                blocks.Add(new UiKeyValueGridBlock { Items = resourceItems });
        }

        var equipment = (root["equipment"] ?? root["equippedItems"]) as JsonObject;
        if (equipment != null)
        {
            var equipmentRows = new List<UiKeyValueItem>();
            foreach (var prop in equipment)
            {
                if (prop.Value == null || prop.Value.GetValueKind() == JsonValueKind.Null)
                {
                    equipmentRows.Add(new UiKeyValueItem { Key = FormatSlotName(prop.Key), Value = "— пусто —" });
                    continue;
                }

                var itemName = DescribeEquipmentValue(prop.Value, inventoryContext);
                equipmentRows.Add(new UiKeyValueItem { Key = FormatSlotName(prop.Key), Value = itemName });
            }

            if (equipmentRows.Count > 0)
                blocks.Add(Panel("⚔️ Экипировка", new UiKeyValueGridBlock { Items = equipmentRows }));
        }

        if (root["items"] is JsonArray itemsArray && itemsArray.Count > 0)
        {
            var rows = new List<UiTableRow>();
            foreach (var item in itemsArray)
            {
                if (item == null)
                    continue;

                var name = GetNodeString(item, "name") ?? GetNodeString(item, "itemName") ?? "???";
                var type = GetNodeString(item, "type") ?? string.Empty;
                var quantity = GetNodeString(item, "count") ?? GetNodeString(item, "quantity") ?? "1";
                var durability = GetNodeString(item, "durability") ?? string.Empty;

                var flags = new List<string>();
                if (item["isBroken"]?.GetValueKind() == JsonValueKind.True)
                    flags.Add("⚠ СЛОМАН");
                if (item["isEmpty"]?.GetValueKind() == JsonValueKind.True)
                    flags.Add("⚠ ПУСТО");
                if (!string.IsNullOrEmpty(durability))
                {
                    var durabilityText = durability.Replace("%", string.Empty).Trim();
                    if (int.TryParse(durabilityText, out var durabilityValue) && durabilityValue == 0)
                        flags.Add("⚠ СЛОМАН");
                }

                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        name,
                        type,
                        quantity != "1" ? quantity : "1",
                        durability,
                        flags.Count > 0 ? string.Join(" ", flags) : "✓"
                    ]
                });
            }

            blocks.Add(new UiTableBlock
            {
                Title = $"📦 Предметы ({itemsArray.Count})",
                Columns = ["Название", "Тип", "Кол-во", "Прочность", "Статус"],
                Rows = rows
            });
        }
        else
        {
            blocks.Add(new UiTextBlock { Text = "Инвентарь пуст.", Tone = UiTone.Muted });
        }

        blocks.Add(Raw("Полный JSON items.json", read.Node));
        await AddRawJsonIfPresent(blocks, fs, "game_state/inventory/item_resources.json", "Ресурсы предметов");
        await AddRawJsonIfPresent(blocks, fs, "game_state/inventory/item_bonds.json", "Связи предметов");
        await AddRawJsonIfPresent(blocks, fs, "game_state/inventory/item_text_updates.json", "Тексты предметов");

        return Completed(command, blocks, BuildInventoryActions(inventoryContext));
    }

    private static IReadOnlyList<UiAction> BuildInventoryActions(InventoryEquipmentContext? inventory)
    {
        if (inventory == null)
            return [];

        var actions = new List<UiAction>();
        foreach (var item in inventory.Items
                     .Where(static item => item.IsEquippable &&
                                           string.IsNullOrWhiteSpace(item.EquippedSlot) &&
                                           !item.IsSoulRelic &&
                                           !item.IsBroken))
        {
            var identity = FirstNonEmpty(item.Identity, item.Name);
            actions.Add(new UiAction
            {
                Id = InventoryEquipmentService.BuildActionId("inventory-equip", identity),
                Label = $"Экипировать «{item.Name}»",
                Command = "/экипировать " + InventoryEquipmentService.FormatCommandArgument(identity),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["itemIdentity"] = item.Identity,
                    ["itemName"] = item.Name,
                    ["slot"] = item.ResolvedSlot
                }
            });
        }

        foreach (var equipped in inventory.Equipped
                     .Where(static item => item.IsOrdinaryInventoryItem)
                     .OrderBy(static item => SlotOrder(item.SlotKey)))
        {
            actions.Add(new UiAction
            {
                Id = InventoryEquipmentService.BuildActionId("inventory-unequip", equipped.SlotKey),
                Label = $"Снять «{equipped.ItemName}»",
                Command = "/снять " + InventoryEquipmentService.FormatCommandArgument(equipped.SlotKey),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["slot"] = equipped.SlotKey,
                    ["itemIdentity"] = equipped.ItemIdentity,
                    ["itemName"] = equipped.ItemName
                }
            });
        }

        return actions;
    }

    private static async Task AddRawJsonIfPresent(List<UiBlock> blocks, FileSystemManager fs, string path, string title)
    {
        var read = await ReadJson(fs, path);
        if (read.Node != null)
            blocks.Add(Raw(title, read.Node));
    }

    private static void AddBookReadWarning(List<UiBlock> blocks, string title, JsonReadResult read)
    {
        if (read.FileExists && read.Node == null)
            blocks.Add(Message(UiNotificationSeverity.Warning, title, "Одна из записей текстов найдена, но не разобрана как JSON."));
    }

    private static string FormatSlotName(string slotKey) =>
        InventoryEquipmentService.FormatSlotName(slotKey switch
        {
            "armor_head" => "head",
            "armor_chest" => "body",
            "armor_feet" => "feet",
            "armor_legs" => "feet",
            "weapon_main" => "mainHand",
            "weapon_secondary" => "offHand",
            "accessory_1" => "ring1",
            "accessory_2" => "ring2",
            _ => slotKey
        });

    private static string DescribeEquipmentValue(JsonNode? value, InventoryEquipmentContext? inventory)
    {
        if (TryGetScalarString(value, out var scalar))
        {
            var matched = inventory == null ? null : InventoryEquipmentService.FindItem(inventory.Items, scalar);
            return matched?.Name ?? scalar;
        }

        var itemName = GetNodeString(value, "name") ?? GetNodeString(value, "itemName");
        if (!string.IsNullOrWhiteSpace(itemName))
            return itemName;

        var itemIdentity = FirstNonEmpty(
            GetNodeString(value, "existedId"),
            GetNodeString(value, "itemId"),
            GetNodeString(value, "id"));
        if (!string.IsNullOrWhiteSpace(itemIdentity) && inventory != null)
        {
            var matched = InventoryEquipmentService.FindItem(inventory.Items, itemIdentity);
            if (matched != null)
                return matched.Name;
        }

        return string.IsNullOrWhiteSpace(itemIdentity) ? value?.ToString() ?? "— пусто —" : itemIdentity;
    }

    private static string? GetNodeString(JsonNode? node, string property)
    {
        if (node is not JsonObject obj)
            return null;

        return TryGetScalarString(obj[property], out var value) ? value : null;
    }

    private static bool TryGetNodeBool(JsonNode? node, string property, out bool value)
    {
        value = false;
        if (node is not JsonObject obj ||
            obj[property] is not JsonValue jsonValue ||
            !jsonValue.TryGetValue<bool>(out var boolValue))
        {
            return false;
        }

        value = boolValue;
        return true;
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

    private static UiPanelBlock Panel(string title, params UiBlock[] blocks) =>
        new()
        {
            Title = title,
            Blocks = blocks.ToList()
        };

    private static UiKeyValueGridBlock Grid(params (string Key, string Value)[] items) =>
        new()
        {
            Items = items
                .Select(static item => new UiKeyValueItem { Key = item.Key, Value = EmptyFallback(item.Value) })
                .ToList()
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

        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            value = boolValue ? "true" : "false";
            return true;
        }

        return false;
    }

    private static string EmptyFallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "не указано" : value.Trim();

    private static string ExtractCommandRemainder(string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? parts[1].Trim() : string.Empty;
    }

    private static int SlotOrder(string slotKey)
    {
        var index = 0;
        foreach (var key in InventoryEquipmentService.SlotLabels.Keys)
        {
            if (string.Equals(key, slotKey, StringComparison.OrdinalIgnoreCase))
                return index;
            index++;
        }

        return int.MaxValue;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private sealed record SummarySpec(string Path, string PropertyName, string Label);

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);
}
