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
            CommandKind.Quests => await BuildReferenceBundle(normalizedCommand, fs, new ReferenceCommandDefinition(
                Title: "Квесты",
                DetailTitlePrefix: "Квест",
                ActionIdPrefix: "quests",
                EnglishCommand: "/quests",
                EnglishDetailToken: "quest",
                RussianDetailToken: "квест",
                NotFoundTitle: "Квест не найден",
                NotFoundMessage: "Такой квест не отмечен в текущих записях.",
                Specs:
                [
                new("game_state/quests/regular_quests.json", "quests|activeQuests", "Активных"),
                new("game_state/quests/regular_quests.json", "completedQuests", "Завершённых"),
                new("game_state/quests/quest_history.json", "questHistory|entries", "Исторических записей"),
                new("game_state/quests/plot_outline.json", "plotOutline|entries", "Сюжетных записей")
                ])),
            CommandKind.Map => await BuildMap(commandToken, fs),
            CommandKind.CurrentLocation => await BuildBundle(commandToken, fs, "Где я", [
                new("game_state/world/current_location.json", "locationName", "Локация"),
                new("game_state/world/current_location.json", "region", "Регион"),
                new("game_state/world/current_location.json", "description", "Описание")
            ]),
            CommandKind.Factions => await BuildReferenceBundle(normalizedCommand, fs, new ReferenceCommandDefinition(
                Title: "Фракции",
                DetailTitlePrefix: "Фракция",
                ActionIdPrefix: "factions",
                EnglishCommand: "/factions",
                EnglishDetailToken: "faction",
                RussianDetailToken: "фракция",
                NotFoundTitle: "Фракция не найдена",
                NotFoundMessage: "Такая фракция не отмечена в текущих записях.",
                Specs:
                [
                new("game_state/factions/faction_core.json", "factions", "Фракций"),
                new("game_state/factions/faction_projects.json", "entries", "Проектов"),
                new("game_state/factions/faction_chronicles.json", "entries", "Хроник"),
                new("game_state/factions/faction_custom.json", "entries", "Особых состояний")
                ])),
            CommandKind.Skills => await BuildReferenceBundle(normalizedCommand, fs, new ReferenceCommandDefinition(
                Title: "Навыки",
                DetailTitlePrefix: "Навык",
                ActionIdPrefix: "skills",
                EnglishCommand: "/skills",
                EnglishDetailToken: "skill",
                RussianDetailToken: "навык",
                NotFoundTitle: "Навык не найден",
                NotFoundMessage: "Такой навык не отмечен в текущих записях.",
                Specs:
                [
                new("game_state/player/skills_active.json", "activeSkillChanges|skills", "Активных"),
                new("game_state/player/skills_passive.json", "passiveSkillChanges|skills", "Пассивных")
                ])),
            CommandKind.Stats => await BuildStats(commandToken, fs, stateManager),
            CommandKind.WorldNews => await ExplorerMortalWorldNewsCommandResultBuilder.BuildAsync(normalizedCommand, fs),
            CommandKind.RivalThreads => await BuildReferenceBundle(normalizedCommand, fs, new ReferenceCommandDefinition(
                Title: "Чужие нити",
                DetailTitlePrefix: "Чужая нить",
                ActionIdPrefix: "rival-threads",
                EnglishCommand: "/rival_threads",
                EnglishDetailToken: "thread",
                RussianDetailToken: "нить",
                NotFoundTitle: "Чужая нить не найдена",
                NotFoundMessage: "Такая нить соперника не отмечена в текущих записях.",
                Specs:
                [
                new(RivalSoulArcService.StatePath, "rivalSoulArcs", "Арк соперников"),
                new(RivalSoulArcService.StatePath, "arcs", "Арк")
                ])),
            CommandKind.GuardianCorrections => await BuildReferenceBundle(normalizedCommand, fs, new ReferenceCommandDefinition(
                Title: "Коррективы Хранителя",
                DetailTitlePrefix: "Корректива Хранителя",
                ActionIdPrefix: "guardian-corrections",
                EnglishCommand: "/guardian_corrections",
                EnglishDetailToken: "correction",
                RussianDetailToken: "корректировка",
                NotFoundTitle: "Корректива не найдена",
                NotFoundMessage: "Такая корректива Хранителя не отмечена в текущих записях.",
                Specs:
                [
                new(GuardianCorrectionService.StatePath, "corrections", "Корректив")
                ])),
            CommandKind.Locations => await BuildLocations(normalizedCommand, fs),
            CommandKind.Transport => await BuildTransport(normalizedCommand, fs),
            CommandKind.Effects => await BuildEffects(commandToken, fs, stateManager),
            CommandKind.Combat => await BuildCombat(normalizedCommand, fs),
            CommandKind.Weather => await BuildBundle(commandToken, fs, "Время и погода", [
                new("game_state/world/world_time.json", "timeOfDay|currentTime", "Время"),
                new("game_state/world/weather.json", "tendency|currentState", "Погода"),
                new("game_state/world/weather.json", "description", "Описание")
            ]),
            CommandKind.Books => await BuildBooks(normalizedCommand, fs),
            CommandKind.StorageAccess => await BuildReferenceBundle(normalizedCommand, fs, new ReferenceCommandDefinition(
                Title: "Доступ к хранилищам",
                DetailTitlePrefix: "Доступ к хранилищу",
                ActionIdPrefix: "storage-access",
                EnglishCommand: "/storage_access",
                EnglishDetailToken: "storage",
                RussianDetailToken: "хранилище",
                NotFoundTitle: "Доступ к хранилищу не найден",
                NotFoundMessage: "Такая запись доступа к хранилищу не отмечена в текущих данных.",
                Specs:
                [
                new("game_state/misc/storage_access.json", "grantStorageAccess|storages", "Хранилищ"),
                new("game_state/misc/storage_access.json", "entries", "Записей")
                ])),
            CommandKind.Interactions => await BuildInteractions(normalizedCommand, fs),
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

    private static async Task<ExplorerCommandResult> BuildReferenceBundle(
        string command,
        FileSystemManager fs,
        ReferenceCommandDefinition definition)
    {
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(command);
        var grouped = definition.Specs.GroupBy(static spec => spec.Path, StringComparer.OrdinalIgnoreCase).ToArray();
        var reads = new Dictionary<string, JsonReadResult>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in grouped)
            reads[group.Key] = await ReadJson(fs, group.Key);

        var entries = BuildReferenceEntries(definition, reads).ToList();
        var request = ParseReferenceDetailRequest(ExtractCommandRemainder(command), definition);
        if (request.Kind != ReferenceDetailKind.Overview)
            return BuildReferenceDetail(command, commandToken, definition, reads.Values, entries, request);

        var rows = new List<UiTableRow>();
        foreach (var spec in definition.Specs)
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
                Title = definition.Title,
                Columns = ["Раздел", "Состояние"],
                Rows = rows
            });
        }
        else
        {
            blocks.Add(Message(UiNotificationSeverity.Info, definition.Title, "Данные ещё не созданы."));
        }

        if (entries.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = $"{definition.Title}: записи",
                Columns = ["Запись", "Раздел", "Кратко", "Подробно"],
                Rows = entries.Select(entry => new UiTableRow
                {
                    Cells =
                    [
                        entry.Title,
                        entry.Section,
                        EmptyFallback(entry.Summary),
                        BuildReferenceDetailCommand(commandToken, definition, entry.Selector)
                    ]
                }).ToList()
            });
        }

        AddReferenceReadWarnings(blocks, definition.Title, reads.Values);
        AddReferenceRawState(blocks, definition.Title, reads.Values);
        return Completed(command, blocks, BuildReferenceDetailActions(commandToken, definition, entries));
    }

    private static ExplorerCommandResult BuildReferenceDetail(
        string command,
        string commandToken,
        ReferenceCommandDefinition definition,
        IEnumerable<JsonReadResult> reads,
        IReadOnlyList<ReferenceEntrySnapshot> entries,
        ReferenceDetailRequest request)
    {
        var blocks = new List<UiBlock>();
        if (request.Kind == ReferenceDetailKind.Unknown)
        {
            blocks.Add(Message(
                UiNotificationSeverity.Warning,
                definition.Title,
                $"Не удалось понять, что открыть. Используйте {commandToken} {DetailTokenForCommand(commandToken, definition)} <метка>."));
        }
        else
        {
            var entry = FindReferenceEntry(entries, request.Selector);
            blocks.Add(entry == null
                ? Message(UiNotificationSeverity.Warning, definition.NotFoundTitle, definition.NotFoundMessage)
                : BuildReferenceDetailPanel(definition, entry));
        }

        AddReferenceReadWarnings(blocks, definition.Title, reads);
        blocks.Add(new UiTextBlock { Text = $"Вернуться к обзору можно командой {commandToken}.", Tone = UiTone.Muted });
        return Completed(command, blocks, [
            new UiAction
            {
                Id = definition.ActionIdPrefix + "-back",
                Label = $"Назад: {definition.Title}",
                Command = commandToken,
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false
            }
        ]);
    }

    private static UiPanelBlock BuildReferenceDetailPanel(
        ReferenceCommandDefinition definition,
        ReferenceEntrySnapshot entry)
    {
        var detailItems = new List<UiKeyValueItem>
        {
            new() { Key = "Раздел", Value = entry.Section },
            new() { Key = "Метка", Value = entry.Selector }
        };

        AddReferenceDetailItem(detailItems, "Состояние", DescribeReferenceStatus(FirstReferenceNodeString(entry.Node, "status", "state", "stage", "phase", "accessLevel", "availability")));
        AddReferenceDetailItem(detailItems, "Где", FirstReferenceNodeString(entry.Node, "location", "locationName", "region", "place", "currentLocation"));
        AddReferenceDetailItem(detailItems, "Когда", FirstReferenceNodeString(entry.Node, "timestamp", "time", "date", "updatedAt", "completionDate"));
        AddReferenceDetailItem(detailItems, "Кто связан", JoinReferenceDetails(
            FirstReferenceNodeString(entry.Node, "questGiver", "owner", "playerName", "targetPlayerName"),
            DescribeReferenceNamedObject(entry.Node["sponsorGuardianRef"]),
            DescribeReferenceNamedObject(entry.Node["rivalSoul"])));
        AddReferenceDetailItem(detailItems, "Кратко", FirstNonEmpty(entry.Summary, FirstReferenceNodeString(entry.Node, "skillDescription", "description", "summary", "objective", "visibleReason", "scenarioCore")));
        AddReferenceDetailItem(detailItems, "Награда", DescribeNodeForReferenceDetail(entry.Node["rewardInfo"] ?? entry.Node["rewards"] ?? entry.Node["reward"]));
        AddReferenceDetailItem(detailItems, "Подробности", DescribeReferencePayload(entry.Node));

        return new UiPanelBlock
        {
            Title = $"{definition.DetailTitlePrefix}: {entry.Title}",
            Blocks = [new UiKeyValueGridBlock { Items = detailItems }]
        };
    }

    private static IEnumerable<ReferenceEntrySnapshot> BuildReferenceEntries(
        ReferenceCommandDefinition definition,
        IReadOnlyDictionary<string, JsonReadResult> reads)
    {
        var index = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in definition.Specs)
        {
            var read = reads[spec.Path];
            var node = read.Node == null
                ? null
                : ResolveSpecNode(read.Node, spec.PropertyName) ?? (read.Node is JsonArray ? read.Node : null);
            if (node == null)
                continue;

            foreach (var obj in EnumerateReferenceObjects(node))
            {
                index++;
                var title = BuildReferenceTitle(obj, spec.Label, index);
                var selector = NormalizeCombatSelector(FirstNonEmpty(
                    FirstReferenceNodeString(
                        obj,
                        "questId",
                        "skillId",
                        "factionId",
                        "locationId",
                        "targetLocationId",
                        "arcId",
                        "rivalSoulArcId",
                        "correctionId",
                        "storageId",
                        "vehicleId",
                        "entryId",
                        "id",
                        "key"),
                    FirstReferenceNodeString(obj["rivalSoul"], "rivalSoulId", "id"),
                    NormalizeReferenceSelector(title),
                    index.ToString()));
                if (!seen.Add(selector))
                    continue;

                yield return new ReferenceEntrySnapshot(
                    Index: index,
                    Selector: selector,
                    Title: title,
                    Section: spec.Label,
                    Summary: DescribeReferenceSummary(obj),
                    Node: obj);
            }
        }
    }

    private static IEnumerable<JsonObject> EnumerateReferenceObjects(JsonNode node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array.OfType<JsonObject>())
                yield return item;
            yield break;
        }

        if (node is JsonObject obj)
            yield return obj;
    }

    private static IReadOnlyList<UiAction> BuildReferenceDetailActions(
        string commandToken,
        ReferenceCommandDefinition definition,
        IReadOnlyList<ReferenceEntrySnapshot> entries)
    {
        var actions = new List<UiAction>();
        foreach (var entry in entries)
        {
            actions.Add(new UiAction
            {
                Id = definition.ActionIdPrefix + "-detail-" + ToActionIdPart(entry.Selector),
                Label = $"Открыть: {entry.Title}",
                Command = BuildReferenceDetailCommand(commandToken, definition, entry.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["kind"] = definition.EnglishDetailToken,
                    ["selector"] = entry.Selector,
                    ["title"] = entry.Title,
                    ["section"] = entry.Section
                }
            });
        }

        return actions;
    }

    private static ReferenceEntrySnapshot? FindReferenceEntry(
        IReadOnlyList<ReferenceEntrySnapshot> entries,
        string selector)
    {
        var normalized = NormalizeCombatSelector(selector);
        return entries.FirstOrDefault(entry =>
            string.Equals(entry.Selector, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.Index.ToString(), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeReferenceSelector(entry.Title), NormalizeReferenceSelector(normalized), StringComparison.OrdinalIgnoreCase));
    }

    private static ReferenceDetailRequest ParseReferenceDetailRequest(
        string remainder,
        ReferenceCommandDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(remainder))
            return new ReferenceDetailRequest(ReferenceDetailKind.Overview, string.Empty);

        var (kindToken, selector) = SplitFirstCombatArgument(remainder);
        if (string.IsNullOrWhiteSpace(selector) || !IsReferenceDetailToken(kindToken, definition))
            return new ReferenceDetailRequest(ReferenceDetailKind.Unknown, string.Empty);

        return new ReferenceDetailRequest(ReferenceDetailKind.Detail, NormalizeCombatSelector(selector));
    }

    private static bool IsReferenceDetailToken(string token, ReferenceCommandDefinition definition)
    {
        var normalized = token.Trim().ToLowerInvariant();
        return string.Equals(normalized, definition.EnglishDetailToken, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, definition.RussianDetailToken, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildReferenceDetailCommand(
        string commandToken,
        ReferenceCommandDefinition definition,
        string selector) =>
        commandToken + " " + DetailTokenForCommand(commandToken, definition) + " " + FormatCombatCommandArgument(selector);

    private static string DetailTokenForCommand(string commandToken, ReferenceCommandDefinition definition) =>
        string.Equals(commandToken, definition.EnglishCommand, StringComparison.OrdinalIgnoreCase)
            ? definition.EnglishDetailToken
            : definition.RussianDetailToken;

    private static string BuildReferenceTitle(JsonObject node, string section, int index) =>
        FirstNonEmpty(
            FirstReferenceNodeString(node, "title", "questName", "skillName", "factionName", "storageName", "vehicleName", "locationName", "displayName", "displayNameOrMoniker", "name"),
            FirstReferenceNodeString(node["rivalSoul"], "displayNameOrMoniker", "displayName", "name"),
            FirstReferenceNodeString(node["sponsorGuardianRef"], "displayName", "name"),
            FirstReferenceNodeString(node, "objective", "summary", "description"),
            $"{section} {index}");

    private static string DescribeReferenceSummary(JsonObject node) =>
        FirstNonEmpty(
            JoinReferenceDetails(
                DescribeReferenceStatus(FirstReferenceNodeString(node, "status", "state", "stage", "phase", "accessLevel", "availability")),
                FirstReferenceNodeString(node, "summary", "description", "skillDescription", "objective", "visibleReason", "scenarioCore")),
            DescribeNodeForReferenceDetail(node["objectives"]));

    private static string DescribeReferencePayload(JsonObject node)
    {
        var parts = new List<string>();
        foreach (var property in node)
        {
            if (IsKnownReferenceDetailProperty(property.Key) || IsTechnicalReferenceProperty(property.Key))
                continue;

            var value = DescribeNodeForReferenceDetail(property.Value);
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{DescribeReferenceFieldLabel(property.Key)}: {value}");
        }

        return string.Join("; ", parts);
    }

    private static string DescribeNodeForReferenceDetail(JsonNode? node)
    {
        if (node == null)
            return string.Empty;

        if (TryGetScalarString(node, out var scalar))
            return scalar;

        if (node is JsonArray array)
            return string.Join("; ", array.Select(DescribeNodeForReferenceDetail).Where(static part => !string.IsNullOrWhiteSpace(part)));

        if (node is JsonObject obj)
        {
            var parts = new List<string>();
            foreach (var property in obj)
            {
                if (IsTechnicalReferenceProperty(property.Key))
                    continue;

                var value = DescribeNodeForReferenceDetail(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add($"{DescribeReferenceFieldLabel(property.Key)}: {value}");
            }

            return string.Join("; ", parts);
        }

        return string.Empty;
    }

    private static string DescribeReferenceNamedObject(JsonNode? node)
    {
        if (node is not JsonObject obj)
            return string.Empty;

        return FirstReferenceNodeString(obj, "displayNameOrMoniker", "displayName", "name", "canonicalName");
    }

    private static string DescribeReferenceStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "active" or "open" or "current" => "активно",
            "completed" or "complete" or "closed" => "завершено",
            "failed" => "провалено",
            "pending" or "waiting" => "ожидает",
            "contested" => "оспаривается",
            "owner" => "полный доступ",
            "shared" => "совместный доступ",
            "read" or "reader" => "доступ на чтение",
            _ => status.Trim()
        };

    private static bool IsKnownReferenceDetailProperty(string propertyName) =>
        propertyName is "title" or "questName" or "skillName" or "factionName" or "locationName" or
            "storageName" or "vehicleName" or "displayName" or "displayNameOrMoniker" or "name" or
            "status" or "state" or "stage" or "phase" or "accessLevel" or "availability" or
            "summary" or "description" or "skillDescription" or "objective" or "visibleReason" or
            "scenarioCore" or "location" or "region" or "place" or "currentLocation" or
            "timestamp" or "time" or "date" or "updatedAt" or "completionDate" or
            "questGiver" or "owner" or "playerName" or "targetPlayerName" or
            "rewardInfo" or "rewards" or "reward";

    private static bool IsTechnicalReferenceProperty(string propertyName) =>
        propertyName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
        propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("visibleToPlayer", StringComparison.OrdinalIgnoreCase) ||
        propertyName.StartsWith("_", StringComparison.OrdinalIgnoreCase);

    private static string DescribeReferenceFieldLabel(string propertyName) =>
        propertyName switch
        {
            "objectives" => "задачи",
            "objective" => "цель",
            "description" or "skillDescription" => "описание",
            "summary" => "кратко",
            "status" or "state" or "stage" or "phase" => "состояние",
            "questGiver" => "выдал",
            "rewardInfo" or "rewards" or "reward" => "награда",
            "visibleReward" => "награда",
            "masteryContext" => "мастерство",
            "category" or "type" => "тип",
            "level" or "masteryLevel" => "уровень",
            "reputation" => "репутация",
            "playerRank" => "ранг героя",
            "rivalSoul" => "соперник",
            "visibleClue" => "улика",
            "sponsorGuardianRef" => "хранитель",
            "budget" => "ресурс",
            "accessLevel" => "доступ",
            "visibleReason" => "причина",
            "location" or "locationName" or "currentLocation" or "region" => "где",
            "capacity" => "вместимость",
            "displayName" or "displayNameOrMoniker" or "name" => "имя",
            _ => "деталь"
        };

    private static void AddReferenceDetailItem(List<UiKeyValueItem> items, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            items.Add(new UiKeyValueItem { Key = key, Value = value.Trim() });
    }

    private static void AddReferenceReadWarnings(
        List<UiBlock> blocks,
        string title,
        IEnumerable<JsonReadResult> reads)
    {
        foreach (var read in reads)
        {
            if (read.FileExists && read.Node == null && !string.IsNullOrWhiteSpace(read.Error))
                blocks.Add(Message(UiNotificationSeverity.Warning, title, "Одна из записей найдена, но её не удалось прочитать как JSON."));
        }
    }

    private static void AddReferenceRawState(
        List<UiBlock> blocks,
        string title,
        IEnumerable<JsonReadResult> reads)
    {
        foreach (var read in reads)
        {
            if (read.Node != null)
                blocks.Add(Raw($"Полная запись: {title}", read.Node));
        }
    }

    private static string FirstReferenceNodeString(JsonNode? node, params string[] properties)
    {
        foreach (var property in properties)
        {
            var value = GetNodeString(node, property);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string JoinReferenceDetails(params string?[] values) =>
        string.Join("; ", values.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value!.Trim()));

    private static string NormalizeReferenceSelector(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(static ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        var result = new string(chars).Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "item" : result;
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
        var definition = new ReferenceCommandDefinition(
            Title: title,
            DetailTitlePrefix: "Локация",
            ActionIdPrefix: "locations",
            EnglishCommand: "/locations",
            EnglishDetailToken: "location",
            RussianDetailToken: "локация",
            NotFoundTitle: "Локация не найдена",
            NotFoundMessage: "Такая локация не отмечена в текущих записях.",
            Specs: []);
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(command);
        var currentRead = await ReadJson(fs, "game_state/world/current_location.json");
        var mapRead = await ReadJson(fs, "game_state/world/world_map.json");
        var rows = new List<UiTableRow>();
        var entries = new List<ReferenceEntrySnapshot>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (UnwrapCurrentLocationNode(currentRead.Node) is { } current)
        {
            AddLocationRow(rows, entries, seen, commandToken, definition, "Текущая", current, DescribeCurrentLocation(current));

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
                        var selector = NormalizeCombatSelector(FirstNonEmpty(GetLocationNodeString(entry, "targetLocationId"), NormalizeReferenceSelector(name)));
                        rows.Add(new UiTableRow
                        {
                            Cells =
                            [
                                "Рядом",
                                name,
                                EmptyFallback(details),
                                BuildReferenceDetailCommand(commandToken, definition, selector)
                            ]
                        });
                        entries.Add(new ReferenceEntrySnapshot(
                            entries.Count + 1,
                            selector,
                            name,
                            "Рядом",
                            details,
                            entry));
                    }
                }
            }
        }

        foreach (var location in EnumerateWorldMapLocationObjects(mapRead.Node, "newLocations"))
            AddLocationRow(rows, entries, seen, commandToken, definition, "Открыта", location, DescribeWorldMapLocation(location));

        foreach (var location in EnumerateWorldMapLocationObjects(mapRead.Node, "locationUpdates"))
            AddLocationRow(rows, entries, seen, commandToken, definition, "Обновлена", location, DescribeWorldMapLocation(location));

        var request = ParseReferenceDetailRequest(ExtractCommandRemainder(command), definition);
        if (request.Kind != ReferenceDetailKind.Overview)
            return BuildReferenceDetail(command, commandToken, definition, [currentRead, mapRead], entries, request);

        var blocks = new List<UiBlock>();
        if (rows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = title,
                Columns = ["Раздел", "Локация", "Сведения", "Подробно"],
                Rows = rows
            });
        }
        else
        {
            blocks.Add(Message(UiNotificationSeverity.Info, title, "Локации пока не обнаружены."));
        }

        AddLocationRawState(blocks, title, currentRead);
        AddLocationRawState(blocks, title, mapRead);
        return Completed(command, blocks, BuildReferenceDetailActions(commandToken, definition, entries));
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
        List<ReferenceEntrySnapshot> entries,
        HashSet<string> seen,
        string commandToken,
        ReferenceCommandDefinition definition,
        string section,
        JsonObject location,
        string details)
    {
        var name = FirstNonEmpty(GetLocationNodeString(location, "name", "locationName", "displayName"), "Безымянная локация");
        var key = StableLocationNodeKey(location, name);
        if (!seen.Add(key))
            return;

        var selector = NormalizeCombatSelector(key);
        rows.Add(new UiTableRow
        {
            Cells = [section, name, EmptyFallback(details), BuildReferenceDetailCommand(commandToken, definition, selector)]
        });
        entries.Add(new ReferenceEntrySnapshot(
            entries.Count + 1,
            selector,
            name,
            section,
            details,
            location));
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
            blocks.Add(Raw($"Полная запись: {title}", read.Node));
        else if (read.FileExists)
            blocks.Add(Message(UiNotificationSeverity.Warning, title, "Одна из записей локаций найдена, но не разобрана как JSON."));
    }

    private static async Task<ExplorerCommandResult> BuildTransport(string command, FileSystemManager fs)
    {
        const string title = "Транспорт";
        var definition = new ReferenceCommandDefinition(
            Title: title,
            DetailTitlePrefix: "Транспорт",
            ActionIdPrefix: "transport",
            EnglishCommand: "/transport",
            EnglishDetailToken: "vehicle",
            RussianDetailToken: "транспорт",
            NotFoundTitle: "Транспорт не найден",
            NotFoundMessage: "Такая запись транспорта не отмечена в текущих данных.",
            Specs: []);
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(command);
        var vehiclesRead = await ReadJson(fs, "game_state/misc/vehicles.json");
        var mapRead = await ReadJson(fs, "game_state/world/world_map.json");
        var currentRead = await ReadJson(fs, "game_state/world/current_location.json");
        var blocks = new List<UiBlock>();
        var entries = EnumerateVehicleObjects(vehiclesRead.Node)
            .Select((vehicle, index) => CreateVehicleSnapshot(index + 1, vehicle))
            .ToList();
        var request = ParseReferenceDetailRequest(ExtractCommandRemainder(command), definition);
        if (request.Kind != ReferenceDetailKind.Overview)
            return BuildTransportDetail(command, commandToken, definition, [vehiclesRead, mapRead, currentRead], entries, request);

        var vehicleRows = entries
            .Select(entry => new UiTableRow
            {
                Cells =
                [
                    entry.Title,
                    DescribeVehicleType(entry.Node),
                    DescribeVehicleNode(entry.Node),
                    BuildReferenceDetailCommand(commandToken, definition, entry.Selector)
                ]
            })
            .ToList();

        if (vehicleRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = title,
                Columns = ["Транспорт", "Тип", "Сведения", "Подробно"],
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
        return Completed(command, blocks, BuildReferenceDetailActions(commandToken, definition, entries));
    }

    private static ExplorerCommandResult BuildTransportDetail(
        string command,
        string commandToken,
        ReferenceCommandDefinition definition,
        IEnumerable<JsonReadResult> reads,
        IReadOnlyList<ReferenceEntrySnapshot> entries,
        ReferenceDetailRequest request)
    {
        var blocks = new List<UiBlock>();
        if (request.Kind == ReferenceDetailKind.Unknown)
        {
            blocks.Add(Message(
                UiNotificationSeverity.Warning,
                definition.Title,
                $"Не удалось понять, что открыть. Используйте {commandToken} {DetailTokenForCommand(commandToken, definition)} <метка>."));
        }
        else
        {
            var entry = FindReferenceEntry(entries, request.Selector);
            blocks.Add(entry == null
                ? Message(UiNotificationSeverity.Warning, definition.NotFoundTitle, definition.NotFoundMessage)
                : BuildVehicleDetailPanel(entry));
        }

        AddReferenceReadWarnings(blocks, definition.Title, reads);
        blocks.Add(new UiTextBlock { Text = $"Вернуться к обзору можно командой {commandToken}.", Tone = UiTone.Muted });
        return Completed(command, blocks, [
            new UiAction
            {
                Id = "transport-back",
                Label = "Назад: Транспорт",
                Command = commandToken,
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false
            }
        ]);
    }

    private static ReferenceEntrySnapshot CreateVehicleSnapshot(int index, JsonObject vehicle)
    {
        var title = FirstNonEmpty(GetNodeString(vehicle, "name"), GetNodeString(vehicle, "vehicleName"), "Безымянный транспорт");
        var selector = NormalizeCombatSelector(FirstNonEmpty(
            FirstReferenceNodeString(vehicle, "vehicleId", "id", "key"),
            NormalizeReferenceSelector(title),
            index.ToString()));

        return new ReferenceEntrySnapshot(
            Index: index,
            Selector: selector,
            Title: title,
            Section: "Транспорт",
            Summary: DescribeVehicleNode(vehicle),
            Node: vehicle);
    }

    private static UiPanelBlock BuildVehicleDetailPanel(ReferenceEntrySnapshot entry)
    {
        var vehicle = entry.Node;
        var items = new List<UiKeyValueItem>
        {
            new() { Key = "Тип", Value = DescribeVehicleType(vehicle) },
            new() { Key = "Доступность", Value = EmptyFallback(DescribeVehicleAvailability(vehicle)) }
        };

        AddReferenceDetailItem(items, "Где", FirstReferenceNodeString(vehicle, "currentLocation", "currentLocationId", "locationName"));
        AddReferenceDetailItem(items, "Вместимость", FirstReferenceNodeString(vehicle, "capacity"));
        AddReferenceDetailItem(items, "Описание", FirstReferenceNodeString(vehicle, "description", "summary", "notes"));
        AddReferenceDetailItem(items, "Подробности", DescribeVehicleExtraPayload(vehicle));

        return new UiPanelBlock
        {
            Title = $"Транспорт: {entry.Title}",
            Blocks = [new UiKeyValueGridBlock { Items = items }]
        };
    }

    private static string DescribeVehicleExtraPayload(JsonObject vehicle)
    {
        var parts = new List<string>();
        foreach (var property in vehicle)
        {
            if (property.Key is "name" or "vehicleName" or "type" or "vehicleType" or "availability" or "status" or
                "isActive" or "currentLocation" or "currentLocationId" or "locationName" or "capacity" or
                "description" or "summary" or "notes" ||
                IsTechnicalReferenceProperty(property.Key))
            {
                continue;
            }

            var value = DescribeNodeForReferenceDetail(property.Value);
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{DescribeReferenceFieldLabel(property.Key)}: {value}");
        }

        return string.Join("; ", parts);
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
            blocks.Add(Raw($"Полная запись: {title}", read.Node));
        else if (read.FileExists)
            blocks.Add(Message(UiNotificationSeverity.Warning, title, "Одна из записей транспорта найдена, но не разобрана как JSON."));
    }

    private static async Task<ExplorerCommandResult> BuildInteractions(string command, FileSystemManager fs)
    {
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(command);
        var state = new InteractionState(await ReadJson(fs, "game_state/misc/player_interactions.json"));
        var players = EnumerateInteractionPlayers(state.PlayerInteractions.Node).ToList();
        var records = players.SelectMany(static player => player.Records).ToList();
        var request = ParseInteractionDetailRequest(ExtractCommandRemainder(command));

        return request.Kind == InteractionDetailKind.Overview
            ? BuildInteractionsOverview(command, commandToken, state, players, records)
            : BuildInteractionDetail(command, commandToken, state, players, records, request);
    }

    private static ExplorerCommandResult BuildInteractionsOverview(
        string command,
        string commandToken,
        InteractionState state,
        IReadOnlyList<InteractionPlayerSnapshot> players,
        IReadOnlyList<InteractionRecordSnapshot> records)
    {
        var blocks = new List<UiBlock>
        {
            new UiTableBlock
            {
                Title = "Взаимодействия игроков",
                Columns = ["Раздел", "Состояние"],
                Rows =
                [
                    new UiTableRow { Cells = ["Игроки", DescribeCombatCount(players.Count, "игрок", "игрока", "игроков")] },
                    new UiTableRow { Cells = ["Записи", DescribeCombatCount(records.Count, "запись", "записи", "записей")] }
                ]
            }
        };

        if (players.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Игроки",
                Columns = ["Игрок", "Связь / контекст", "Состояние", "Подробно"],
                Rows = players.Select(player => new UiTableRow
                {
                    Cells =
                    [
                        player.Name,
                        EmptyFallback(DescribeInteractionPlayerContext(player.Node)),
                        EmptyFallback(DescribeInteractionPlayerStatus(player.Node)),
                        BuildInteractionPlayerDetailCommand(commandToken, player.Selector)
                    ]
                }).ToList()
            });
        }

        if (records.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Записи взаимодействий",
                Columns = ["Запись", "Игрок", "Состояние", "Подробно"],
                Rows = records.Take(12).Select(record => new UiTableRow
                {
                    Cells =
                    [
                        record.Title,
                        record.PlayerName,
                        EmptyFallback(DescribeInteractionRecordStatus(record.Node)),
                        BuildInteractionRecordDetailCommand(commandToken, record.Selector)
                    ]
                }).ToList()
            });
        }

        AddInteractionReadWarnings(blocks, state);
        if (players.Count == 0 && records.Count == 0)
            blocks.Add(Message(UiNotificationSeverity.Info, "Взаимодействия игроков", "Данные взаимодействий ещё не созданы."));

        AddInteractionRawState(blocks, state.PlayerInteractions, "Полная запись взаимодействий игроков");
        return Completed(command, blocks, BuildInteractionOverviewActions(commandToken, players, records));
    }

    private static ExplorerCommandResult BuildInteractionDetail(
        string command,
        string commandToken,
        InteractionState state,
        IReadOnlyList<InteractionPlayerSnapshot> players,
        IReadOnlyList<InteractionRecordSnapshot> records,
        InteractionDetailRequest request)
    {
        var blocks = new List<UiBlock>();
        var actions = new List<UiAction>
        {
            new()
            {
                Id = "interactions-back",
                Label = "Назад к взаимодействиям",
                Command = commandToken,
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false
            }
        };

        switch (request.Kind)
        {
            case InteractionDetailKind.Player:
            {
                var player = FindInteractionPlayer(players, request.Selector);
                if (player == null)
                {
                    blocks.Add(Message(UiNotificationSeverity.Warning, "Игрок не найден", "Такая запись игрока не отмечена во взаимодействиях."));
                }
                else
                {
                    blocks.Add(BuildInteractionPlayerDetailPanel(commandToken, player));
                    actions.AddRange(BuildInteractionRecordActions(commandToken, player.Records));
                }
                break;
            }
            case InteractionDetailKind.Record:
            {
                var record = FindInteractionRecord(records, request.Selector);
                blocks.Add(record == null
                    ? Message(UiNotificationSeverity.Warning, "Запись не найдена", "Такая запись взаимодействия не отмечена в текущих данных.")
                    : BuildInteractionRecordDetailPanel(record));
                break;
            }
            case InteractionDetailKind.Unknown:
                blocks.Add(Message(
                    UiNotificationSeverity.Warning,
                    "Взаимодействия игроков",
                    "Не удалось понять, что открыть. Используйте /взаимодействия игрок <метка> или /взаимодействия запись <метка>."));
                break;
        }

        AddInteractionReadWarnings(blocks, state);
        blocks.Add(new UiTextBlock { Text = $"Вернуться к обзору можно командой {commandToken}.", Tone = UiTone.Muted });
        return Completed(command, blocks, actions);
    }

    private static UiPanelBlock BuildInteractionPlayerDetailPanel(string commandToken, InteractionPlayerSnapshot player)
    {
        var detailItems = new List<UiKeyValueItem>
        {
            new() { Key = "Метка", Value = player.Selector }
        };

        AddInteractionDetailItem(detailItems, "Связь", FirstInteractionNodeString(player.Node, "relationship", "relation", "relationshipSummary", "attitude"));
        AddInteractionDetailItem(detailItems, "Контекст", FirstInteractionNodeString(player.Node, "context", "sceneContext", "interactionContext", "role", "faction", "location"));
        AddInteractionDetailItem(detailItems, "Состояние", DescribeInteractionPlayerStatus(player.Node));
        AddInteractionDetailItem(detailItems, "Кратко", FirstInteractionNodeString(player.Node, "summary", "description", "notes", "message"));
        AddInteractionDetailItem(detailItems, "Зацепки", JoinNodeValues(player.Node["currentHooks"] ?? player.Node["hooks"] ?? player.Node["activeHooks"]));

        var blocks = new List<UiBlock>
        {
            new UiKeyValueGridBlock { Items = detailItems }
        };

        if (player.Records.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Записи этого игрока",
                Columns = ["Запись", "Состояние", "Кратко", "Подробно"],
                Rows = player.Records.Select(record => new UiTableRow
                {
                    Cells =
                    [
                        record.Title,
                        EmptyFallback(DescribeInteractionRecordStatus(record.Node)),
                        EmptyFallback(DescribeInteractionRecordSummary(record.Node)),
                        BuildInteractionRecordDetailCommand(commandToken, record.Selector)
                    ]
                }).ToList()
            });
        }

        return new UiPanelBlock
        {
            Title = $"Игрок: {player.Name}",
            Blocks = blocks
        };
    }

    private static UiPanelBlock BuildInteractionRecordDetailPanel(InteractionRecordSnapshot record)
    {
        var detailItems = new List<UiKeyValueItem>
        {
            new() { Key = "Метка", Value = record.Selector },
            new() { Key = "Игрок", Value = record.PlayerName }
        };

        AddInteractionDetailItem(detailItems, "Состояние", DescribeInteractionRecordStatus(record.Node));
        AddInteractionDetailItem(detailItems, "Когда", JoinInteractionDetails(
            FirstInteractionNodeString(record.Node, "timestamp", "time", "date", "updatedAt"),
            FirstInteractionNodeString(record.Node, "turn", "turnNumber")));
        AddInteractionDetailItem(detailItems, "Где", FirstInteractionNodeString(record.Node, "location", "locationName", "scene", "place"));
        AddInteractionDetailItem(detailItems, "Участники", JoinNodeValues(record.Node["participants"] ?? record.Node["actors"] ?? record.Node["involvedPlayers"]));
        AddInteractionDetailItem(detailItems, "Кратко", FirstInteractionNodeString(record.Node, "summary", "message", "narrativeSummary"));
        AddInteractionDetailItem(detailItems, "Описание", FirstInteractionNodeString(record.Node, "description", "details"));
        AddInteractionDetailItem(detailItems, "Заметки", FirstInteractionNodeString(record.Node, "notes", "note"));
        AddInteractionDetailItem(detailItems, "Итог", FirstInteractionNodeString(record.Node, "outcome", "result", "resolution"));
        AddInteractionDetailItem(detailItems, "Последствия", DescribeNodeForInteractionDetail(record.Node["consequences"] ?? record.Node["effects"] ?? record.Node["impact"]));
        AddInteractionDetailItem(detailItems, "Следующий шаг", FirstInteractionNodeString(record.Node, "nextStep", "followUp", "hook", "visibleNextStep"));
        AddInteractionDetailItem(detailItems, "Подробности", DescribeInteractionRecordPayload(record.Node));
        AddInteractionDetailItem(detailItems, "Метки", JoinNodeValues(record.Node["tags"]));

        return new UiPanelBlock
        {
            Title = $"Запись взаимодействия: {record.Title}",
            Blocks = [new UiKeyValueGridBlock { Items = detailItems }]
        };
    }

    private static IReadOnlyList<UiAction> BuildInteractionOverviewActions(
        string commandToken,
        IReadOnlyList<InteractionPlayerSnapshot> players,
        IReadOnlyList<InteractionRecordSnapshot> records)
    {
        var actions = new List<UiAction>();
        foreach (var player in players)
        {
            actions.Add(new UiAction
            {
                Id = "interactions-player-" + ToActionIdPart(player.Selector),
                Label = $"Осмотреть игрока «{player.Name}»",
                Command = BuildInteractionPlayerDetailCommand(commandToken, player.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["kind"] = "player",
                    ["selector"] = player.Selector,
                    ["name"] = player.Name
                }
            });
        }

        actions.AddRange(BuildInteractionRecordActions(commandToken, records.Take(12)));
        return actions;
    }

    private static IEnumerable<UiAction> BuildInteractionRecordActions(
        string commandToken,
        IEnumerable<InteractionRecordSnapshot> records)
    {
        foreach (var record in records)
        {
            yield return new UiAction
            {
                Id = "interactions-record-" + ToActionIdPart(record.Selector),
                Label = $"Открыть запись «{record.Title}»",
                Command = BuildInteractionRecordDetailCommand(commandToken, record.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["kind"] = "record",
                    ["selector"] = record.Selector,
                    ["title"] = record.Title,
                    ["player"] = record.PlayerName
                }
            };
        }
    }

    private static IEnumerable<InteractionPlayerSnapshot> EnumerateInteractionPlayers(JsonNode? node)
    {
        var index = 0;
        foreach (var (key, playerNode) in EnumerateInteractionPlayerNodes(node))
        {
            if (!IsVisibleInteractionNode(playerNode))
                continue;

            index++;
            var name = FirstNonEmpty(
                FirstInteractionNodeString(playerNode, "displayName", "playerName", "name", "characterName", "targetPlayerName"),
                key,
                $"Игрок {index}");
            var identity = FirstNonEmpty(
                FirstInteractionNodeString(playerNode, "playerId", "characterId", "targetPlayerId", "sourcePlayerId", "id"),
                key,
                index.ToString());
            var selector = NormalizeInteractionSelector(identity);
            var records = EnumerateInteractionRecordsForPlayer(selector, name, playerNode).ToList();
            yield return new InteractionPlayerSnapshot(index, selector, name, playerNode, records);
        }
    }

    private static IEnumerable<(string Key, JsonObject Node)> EnumerateInteractionPlayerNodes(JsonNode? node)
    {
        if (node is JsonObject root)
        {
            if (root["otherPlayersInteractions"] is { } otherPlayers)
            {
                foreach (var item in EnumerateInteractionPlayerNodes(otherPlayers))
                    yield return item;
            }

            foreach (var propertyName in new[] { "players", "otherPlayers", "playerEntries" })
            {
                if (root[propertyName] is { } players)
                {
                    foreach (var item in EnumerateInteractionPlayerNodes(players))
                        yield return item;
                }
            }

            foreach (var propertyName in new[] { "interactions", "entries" })
            {
                if (root[propertyName] is JsonArray records && records.Count > 0)
                    yield return ("Записи взаимодействий", WrapInteractionRecords("Записи взаимодействий", records));
            }

            if (LooksLikeInteractionPlayer(root))
                yield return (FirstInteractionNodeString(root, "playerId", "id"), root);
            else if (root["otherPlayersInteractions"] == null && root["players"] == null && root["interactions"] == null && root["entries"] == null)
            {
                foreach (var property in root)
                {
                    var player = NormalizeInteractionPlayerNode(property.Key, property.Value);
                    if (player != null)
                        yield return (property.Key, player);
                }
            }

            yield break;
        }

        if (node is JsonArray array)
        {
            var index = 0;
            var objectItems = array.OfType<JsonObject>().ToList();
            if (objectItems.Any(LooksLikeInteractionPlayer))
            {
                foreach (var item in objectItems)
                {
                    index++;
                    yield return (FirstNonEmpty(FirstInteractionNodeString(item, "playerId", "id"), index.ToString()), item);
                }
            }
            else if (array.Count > 0)
            {
                yield return ("Записи взаимодействий", WrapInteractionRecords("Записи взаимодействий", array));
            }
        }
    }

    private static JsonObject? NormalizeInteractionPlayerNode(string key, JsonNode? value)
    {
        if (value is JsonObject obj)
        {
            var clone = obj.DeepClone() as JsonObject ?? [];
            if (string.IsNullOrWhiteSpace(FirstInteractionNodeString(clone, "playerId", "displayName", "playerName", "name", "id")))
            {
                clone["playerId"] = key;
                clone["displayName"] = key;
            }

            return clone;
        }

        if (value is JsonArray array)
            return WrapInteractionRecords(key, array);

        if (TryGetScalarString(value, out var scalar) && !string.IsNullOrWhiteSpace(scalar))
        {
            return new JsonObject
            {
                ["playerId"] = key,
                ["displayName"] = key,
                ["summary"] = scalar
            };
        }

        return null;
    }

    private static JsonObject WrapInteractionRecords(string key, JsonArray records) =>
        new()
        {
            ["playerId"] = key,
            ["displayName"] = key,
            ["records"] = records.DeepClone()
        };

    private static bool LooksLikeInteractionPlayer(JsonObject node) =>
        !string.IsNullOrWhiteSpace(FirstInteractionNodeString(node, "playerId", "displayName", "playerName", "characterId", "targetPlayerId", "name")) ||
        node["records"] is JsonArray ||
        node["interactions"] is JsonArray ||
        node["interactionRecords"] is JsonArray;

    private static IEnumerable<InteractionRecordSnapshot> EnumerateInteractionRecordsForPlayer(
        string playerSelector,
        string playerName,
        JsonObject playerNode)
    {
        var index = 0;
        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var propertyName in new[]
        {
            "records",
            "interactions",
            "interactionRecords",
            "interactionLog",
            "history",
            "events",
            "payloads",
            "entries",
            "sharedQuestHooks"
        })
        {
            if (playerNode[propertyName] is not JsonArray array)
                continue;

            consumed.Add(propertyName);
            foreach (var record in EnumerateInteractionRecordObjects(propertyName, array))
            {
                if (!IsVisibleInteractionNode(record))
                    continue;

                index++;
                yield return CreateInteractionRecordSnapshot(index, playerSelector, playerName, propertyName, record);
            }
        }

        foreach (var property in playerNode)
        {
            if (consumed.Contains(property.Key) || IsInteractionPlayerMetadataProperty(property.Key))
                continue;

            if (property.Value is JsonArray array)
            {
                foreach (var record in EnumerateInteractionRecordObjects(property.Key, array))
                {
                    if (!IsVisibleInteractionNode(record))
                        continue;

                    index++;
                    yield return CreateInteractionRecordSnapshot(index, playerSelector, playerName, property.Key, record);
                }
            }
            else if (property.Value is JsonObject obj && LooksLikeInteractionRecord(obj))
            {
                if (!IsVisibleInteractionNode(obj))
                    continue;

                index++;
                yield return CreateInteractionRecordSnapshot(index, playerSelector, playerName, property.Key, obj);
            }
        }
    }

    private static IEnumerable<JsonObject> EnumerateInteractionRecordObjects(string section, JsonArray array)
    {
        var index = 0;
        foreach (var item in array)
        {
            index++;
            if (item is JsonObject obj)
            {
                yield return obj;
            }
            else if (TryGetScalarString(item, out var text) && !string.IsNullOrWhiteSpace(text))
            {
                yield return new JsonObject
                {
                    ["title"] = $"{DescribeInteractionSection(section)} {index}",
                    ["summary"] = text
                };
            }
        }
    }

    private static InteractionRecordSnapshot CreateInteractionRecordSnapshot(
        int index,
        string playerSelector,
        string playerName,
        string section,
        JsonObject record)
    {
        var title = FirstNonEmpty(
            FirstInteractionNodeString(record, "title", "interactionTitle", "recordTitle", "eventTitle", "questName", "actionName", "name"),
            FirstInteractionNodeString(record, "summary", "message", "description"),
            $"{DescribeInteractionSection(section)} {index}");
        var selector = NormalizeInteractionSelector(FirstNonEmpty(
            FirstInteractionNodeString(record, "interactionId", "recordId", "payloadId", "entryId", "eventId", "id", "key"),
            $"{playerSelector}-{index}"));
        return new InteractionRecordSnapshot(index, selector, title, playerSelector, playerName, record);
    }

    private static bool LooksLikeInteractionRecord(JsonObject node) =>
        !string.IsNullOrWhiteSpace(FirstInteractionNodeString(node, "interactionId", "recordId", "title", "summary", "message", "questName", "actionName", "description"));

    private static bool IsInteractionPlayerMetadataProperty(string propertyName) =>
        propertyName is "playerId" or "characterId" or "targetPlayerId" or "sourcePlayerId" or "id" or
            "displayName" or "playerName" or "name" or "characterName" or "targetPlayerName" or
            "relationship" or "relation" or "relationshipSummary" or "attitude" or "context" or
            "sceneContext" or "interactionContext" or "role" or "faction" or "location" or
            "status" or "state" or "availability" or "visibility" or "summary" or "description" or
            "notes" or "message" or "currentHooks" or "hooks" or "activeHooks" or "visibleToPlayer";

    private static InteractionPlayerSnapshot? FindInteractionPlayer(IReadOnlyList<InteractionPlayerSnapshot> players, string selector)
    {
        var normalized = NormalizeInteractionSelector(selector);
        return players.FirstOrDefault(player =>
            string.Equals(player.Selector, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(player.Index.ToString(), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeInteractionSelector(player.Name), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static InteractionRecordSnapshot? FindInteractionRecord(IReadOnlyList<InteractionRecordSnapshot> records, string selector)
    {
        var normalized = NormalizeInteractionSelector(selector);
        return records.FirstOrDefault(record =>
            string.Equals(record.Selector, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(record.Index.ToString(), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeInteractionSelector(record.Title), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static InteractionDetailRequest ParseInteractionDetailRequest(string remainder)
    {
        if (string.IsNullOrWhiteSpace(remainder))
            return new InteractionDetailRequest(InteractionDetailKind.Overview, string.Empty);

        var (kindToken, selector) = SplitFirstCombatArgument(remainder);
        var kind = kindToken.Trim().ToLowerInvariant() switch
        {
            "player" or "players" or "character" or "игрок" or "игрока" or "персонаж" => InteractionDetailKind.Player,
            "record" or "entry" or "interaction" or "payload" or "запись" or "событие" or "взаимодействие" => InteractionDetailKind.Record,
            _ => InteractionDetailKind.Unknown
        };

        return new InteractionDetailRequest(kind, NormalizeInteractionSelector(selector));
    }

    private static string BuildInteractionPlayerDetailCommand(string commandToken, string selector)
    {
        var word = string.Equals(commandToken, "/interactions", StringComparison.OrdinalIgnoreCase)
            ? "player"
            : "игрок";
        return commandToken + " " + word + " " + FormatCombatCommandArgument(selector);
    }

    private static string BuildInteractionRecordDetailCommand(string commandToken, string selector)
    {
        var word = string.Equals(commandToken, "/interactions", StringComparison.OrdinalIgnoreCase)
            ? "record"
            : "запись";
        return commandToken + " " + word + " " + FormatCombatCommandArgument(selector);
    }

    private static string NormalizeInteractionSelector(string selector) =>
        NormalizeCombatSelector(selector);

    private static void AddInteractionDetailItem(List<UiKeyValueItem> items, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            items.Add(new UiKeyValueItem { Key = key, Value = value.Trim() });
    }

    private static string DescribeInteractionRecordPayload(JsonObject record)
    {
        var parts = new List<string>();
        foreach (var property in record)
        {
            if (IsKnownInteractionRecordDetailProperty(property.Key) || IsTechnicalInteractionProperty(property.Key))
                continue;

            var value = DescribeNodeForInteractionDetail(property.Value);
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{DescribeInteractionFieldLabel(property.Key)}: {value}");
        }

        return string.Join("; ", parts);
    }

    private static string DescribeInteractionPlayerContext(JsonObject player) =>
        JoinInteractionDetails(
            FirstInteractionNodeString(player, "relationship", "relation", "relationshipSummary", "attitude"),
            FirstInteractionNodeString(player, "context", "sceneContext", "interactionContext", "role", "faction", "location"));

    private static string DescribeInteractionPlayerStatus(JsonObject player) =>
        JoinInteractionDetails(
            DescribeInteractionStatus(FirstInteractionNodeString(player, "status", "state", "availability")),
            DescribeInteractionVisibility(FirstInteractionNodeString(player, "visibility")));

    private static string DescribeInteractionRecordStatus(JsonObject record) =>
        JoinInteractionDetails(
            DescribeInteractionStatus(FirstInteractionNodeString(record, "status", "state", "stage", "phase")),
            DescribeInteractionVisibility(FirstInteractionNodeString(record, "visibility")));

    private static string DescribeInteractionRecordSummary(JsonObject record) =>
        FirstNonEmpty(
            FirstInteractionNodeString(record, "summary", "message", "description", "notes"),
            FirstInteractionNodeString(record, "outcome", "result", "nextStep", "followUp"));

    private static string DescribeInteractionStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "active" or "open" or "current" => "активно",
            "pending" or "waiting" => "ожидает",
            "resolved" or "complete" or "completed" or "closed" => "завершено",
            "blocked" => "заблокировано",
            "available" => "доступно",
            "unavailable" => "недоступно",
            _ => status.Trim()
        };

    private static string DescribeInteractionVisibility(string visibility) =>
        visibility.Trim().ToLowerInvariant() switch
        {
            "" or "visible" or "public" or "player" or "player_visible" => string.Empty,
            "private" => "частная сцена",
            "hidden" => "скрыто",
            "gm_only" => "скрыто от игрока",
            _ => visibility.Trim()
        };

    private static string DescribeInteractionSection(string section) =>
        section.Trim().ToLowerInvariant() switch
        {
            "records" or "entries" => "Запись",
            "interactions" or "interactionrecords" or "interactionlog" => "Взаимодействие",
            "history" => "История",
            "events" => "Событие",
            "sharedquesthooks" => "Крючок квеста",
            "payloads" => "Запись",
            _ => "Запись"
        };

    private static string DescribeNodeForInteractionDetail(JsonNode? node)
    {
        if (node == null)
            return string.Empty;

        if (TryGetScalarString(node, out var scalar))
            return scalar;

        if (node is JsonArray array)
            return string.Join("; ", array.Select(DescribeNodeForInteractionDetail).Where(static part => !string.IsNullOrWhiteSpace(part)));

        if (node is JsonObject obj)
        {
            var parts = new List<string>();
            foreach (var property in obj)
            {
                if (IsTechnicalInteractionProperty(property.Key))
                    continue;

                var value = DescribeNodeForInteractionDetail(property.Value);
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add($"{DescribeInteractionFieldLabel(property.Key)}: {value}");
            }

            return string.Join("; ", parts);
        }

        return string.Empty;
    }

    private static bool IsTechnicalInteractionProperty(string propertyName) =>
        propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("id", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("visibleToPlayer", StringComparison.OrdinalIgnoreCase);

    private static bool IsKnownInteractionRecordDetailProperty(string propertyName) =>
        propertyName is "title" or "interactionTitle" or "recordTitle" or "eventTitle" or "questName" or "actionName" or "name" or
            "summary" or "message" or "narrativeSummary" or "description" or "details" or
            "status" or "state" or "stage" or "phase" or "visibility" or
            "timestamp" or "time" or "date" or "updatedAt" or "turn" or "turnNumber" or
            "location" or "locationName" or "scene" or "place" or
            "participants" or "actors" or "involvedPlayers" or
            "notes" or "note" or "outcome" or "result" or "resolution" or
            "consequences" or "effects" or "impact" or
            "nextStep" or "followUp" or "hook" or "visibleNextStep" or "tags";

    private static string DescribeInteractionFieldLabel(string propertyName) =>
        propertyName switch
        {
            "summary" => "кратко",
            "description" => "описание",
            "notes" or "note" => "заметка",
            "outcome" or "result" => "итог",
            "consequence" or "consequences" => "последствия",
            "nextStep" or "followUp" => "следующий шаг",
            "location" or "locationName" => "где",
            "timestamp" or "time" or "date" => "когда",
            "status" or "state" => "состояние",
            "participants" or "actors" => "участники",
            "tags" => "метки",
            "UpdateInventory" or "updateInventory" => "инвентарь",
            "itemName" or "item" => "предмет",
            "quantity" or "count" or "amount" => "количество",
            _ => "деталь"
        };

    private static string JoinNodeValues(JsonNode? node) =>
        DescribeNodeForInteractionDetail(node);

    private static string JoinInteractionDetails(params string?[] values) =>
        string.Join("; ", values.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value!.Trim()));

    private static string FirstInteractionNodeString(JsonNode? node, params string[] properties)
    {
        foreach (var property in properties)
        {
            var value = GetNodeString(node, property);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static bool IsVisibleInteractionNode(JsonObject node)
    {
        if (TryGetNodeBool(node, "visibleToPlayer", out var visibleToPlayer) && !visibleToPlayer)
            return false;

        var visibility = FirstInteractionNodeString(node, "visibility", "visible");
        return !visibility.Trim().Equals("hidden", StringComparison.OrdinalIgnoreCase) &&
               !visibility.Trim().Equals("gm_only", StringComparison.OrdinalIgnoreCase);
    }

    private static void AddInteractionReadWarnings(List<UiBlock> blocks, InteractionState state)
    {
        if (state.PlayerInteractions.FileExists &&
            state.PlayerInteractions.Node == null &&
            !string.IsNullOrWhiteSpace(state.PlayerInteractions.Error))
        {
            blocks.Add(Message(UiNotificationSeverity.Warning, "Взаимодействия игроков", "Запись взаимодействий найдена, но её не удалось прочитать как JSON."));
        }
    }

    private static void AddInteractionRawState(List<UiBlock> blocks, JsonReadResult read, string title)
    {
        if (read.Node != null)
            blocks.Add(Raw(title, read.Node));
    }

    private static async Task<ExplorerCommandResult> BuildCombat(string command, FileSystemManager fs)
    {
        var state = new CombatState(
            await ReadJson(fs, "game_state/combat/enemies.json"),
            await ReadJson(fs, "game_state/combat/allies.json"),
            await ReadJson(fs, "game_state/combat/combat_log.json"));
        var enemies = EnumerateCombatants(state.Enemies.Node, CombatantKind.Enemy).ToList();
        var allies = EnumerateCombatants(state.Allies.Node, CombatantKind.Ally).ToList();
        var logEntries = EnumerateCombatLogEntries(state.Log.Node).ToList();
        var request = ParseCombatDetailRequest(ExtractCommandRemainder(command));

        if (request.Kind != CombatDetailKind.Overview)
            return BuildCombatDetail(command, state, enemies, allies, logEntries, request);

        var blocks = new List<UiBlock>
        {
            new UiTableBlock
            {
                Title = "Боевая обстановка",
                Columns = ["Раздел", "Состояние"],
                Rows =
                [
                    new UiTableRow { Cells = ["Враги", DescribeCombatCount(enemies.Count, "враг", "врага", "врагов")] },
                    new UiTableRow { Cells = ["Союзники", DescribeCombatCount(allies.Count, "союзник", "союзника", "союзников")] },
                    new UiTableRow { Cells = ["Боевой журнал", DescribeCombatCount(logEntries.Count, "запись", "записи", "записей")] }
                ]
            }
        };

        if (enemies.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Враги",
                Columns = ["Враг", "Состояние", "Намерение", "Подробно"],
                Rows = enemies
                    .Select(static combatant => new UiTableRow
                    {
                        Cells =
                        [
                            combatant.Name,
                            DescribeCombatantOverview(combatant),
                            EmptyFallback(DescribeCombatantIntent(combatant.Node)),
                            BuildCombatDetailCommand(CombatantKind.Enemy, combatant.Selector)
                        ]
                    })
                    .ToList()
            });
        }

        if (allies.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Союзники",
                Columns = ["Союзник", "Состояние", "Действие", "Подробно"],
                Rows = allies
                    .Select(static combatant => new UiTableRow
                    {
                        Cells =
                        [
                            combatant.Name,
                            DescribeCombatantOverview(combatant),
                            EmptyFallback(DescribeCombatantIntent(combatant.Node)),
                            BuildCombatDetailCommand(CombatantKind.Ally, combatant.Selector)
                        ]
                    })
                    .ToList()
            });
        }

        if (logEntries.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Боевой журнал",
                Columns = ["Запись", "Событие", "Подробно"],
                Rows = logEntries
                    .Take(10)
                    .Select(static entry => new UiTableRow
                    {
                        Cells =
                        [
                            entry.Title,
                            EmptyFallback(entry.Summary),
                            BuildCombatLogDetailCommand(entry.Selector)
                        ]
                    })
                    .ToList()
            });
        }

        AddCombatReadWarnings(blocks, state);

        if (blocks.Count == 1 && enemies.Count == 0 && allies.Count == 0 && logEntries.Count == 0)
            blocks.Add(Message(UiNotificationSeverity.Info, "Бой", "Нет данных о бое. Вы не в сражении."));

        AddCombatRawState(blocks, state.Enemies, "Полная запись врагов");
        AddCombatRawState(blocks, state.Allies, "Полная запись союзников");
        AddCombatRawState(blocks, state.Log, "Полный боевой журнал");
        return Completed(command, blocks, BuildCombatOverviewActions(enemies, allies, logEntries));
    }

    private static ExplorerCommandResult BuildCombatDetail(
        string command,
        CombatState state,
        IReadOnlyList<CombatantSnapshot> enemies,
        IReadOnlyList<CombatantSnapshot> allies,
        IReadOnlyList<CombatLogSnapshot> logEntries,
        CombatDetailRequest request)
    {
        var blocks = new List<UiBlock>();
        switch (request.Kind)
        {
            case CombatDetailKind.Enemy:
            {
                var enemy = FindCombatant(enemies, request.Selector);
                if (enemy == null)
                    blocks.Add(Message(UiNotificationSeverity.Warning, "Враг не найден", "Такой враг не отмечен в текущей боевой обстановке."));
                else
                    blocks.Add(BuildCombatantDetailPanel(enemy, "Враг"));
                break;
            }
            case CombatDetailKind.Ally:
            {
                var ally = FindCombatant(allies, request.Selector);
                if (ally == null)
                    blocks.Add(Message(UiNotificationSeverity.Warning, "Союзник не найден", "Такой союзник не отмечен в текущей боевой обстановке."));
                else
                    blocks.Add(BuildCombatantDetailPanel(ally, "Союзник"));
                break;
            }
            case CombatDetailKind.Log:
            {
                var entry = FindCombatLogEntry(logEntries, request.Selector);
                if (entry == null)
                    blocks.Add(Message(UiNotificationSeverity.Warning, "Запись боя не найдена", "Такая запись не найдена в боевом журнале."));
                else
                    blocks.Add(BuildCombatLogDetailPanel(entry));
                break;
            }
            case CombatDetailKind.Unknown:
                blocks.Add(Message(
                    UiNotificationSeverity.Warning,
                    "Бой",
                    "Не удалось понять, что осмотреть. Используйте /бой враг <метка>, /бой союзник <метка> или /бой журнал <метка>."));
                break;
        }

        AddCombatReadWarnings(blocks, state);
        blocks.Add(new UiTextBlock { Text = "Вернуться к обзору можно командой /бой.", Tone = UiTone.Muted });
        return Completed(command, blocks, [
            new UiAction
            {
                Id = "combat-back",
                Label = "Назад к боевой обстановке",
                Command = "/бой",
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false
            }
        ]);
    }

    private static UiPanelBlock BuildCombatantDetailPanel(CombatantSnapshot combatant, string titlePrefix)
    {
        var detailItems = new List<UiKeyValueItem>
        {
            new() { Key = "Состояние", Value = EmptyFallback(DescribeCombatantStatus(combatant.Node)) },
            new() { Key = "Роль / угроза", Value = EmptyFallback(DescribeCombatantRole(combatant.Node)) },
            new() { Key = "Здоровье", Value = EmptyFallback(DescribeCombatantHealth(combatant.Node)) },
            new() { Key = "Стойкость", Value = EmptyFallback(DescribeCombatantPoise(combatant.Node)) },
            new() { Key = "Намерение", Value = EmptyFallback(DescribeCombatantIntent(combatant.Node)) }
        };

        var description = FirstCombatNodeString(combatant.Node, "description", "notes", "summary");
        if (!string.IsNullOrWhiteSpace(description))
            detailItems.Add(new UiKeyValueItem { Key = "Заметки", Value = description });

        var blocks = new List<UiBlock>
        {
            new UiKeyValueGridBlock { Items = detailItems }
        };

        var effectRows = BuildCombatEffectRows(combatant.Node);
        if (effectRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Эффекты",
                Columns = ["Раздел", "Эффект", "Сила", "Длительность", "Источник"],
                Rows = effectRows
            });
        }

        var actionRows = BuildCombatActionRows(combatant.Node);
        if (actionRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Действия",
                Columns = ["Действие", "Цена", "Эффект"],
                Rows = actionRows
            });
        }

        return new UiPanelBlock
        {
            Title = $"{titlePrefix}: {combatant.Name}",
            Blocks = blocks
        };
    }

    private static UiPanelBlock BuildCombatLogDetailPanel(CombatLogSnapshot entry)
    {
        var items = new List<UiKeyValueItem>
        {
            new() { Key = "Событие", Value = EmptyFallback(entry.Summary) }
        };

        if (!string.IsNullOrWhiteSpace(entry.Turn))
            items.Add(new UiKeyValueItem { Key = "Ход", Value = entry.Turn });
        if (entry.Participants.Count > 0)
            items.Add(new UiKeyValueItem { Key = "Участники", Value = string.Join(", ", entry.Participants) });
        if (!string.IsNullOrWhiteSpace(entry.Result))
            items.Add(new UiKeyValueItem { Key = "Итог", Value = entry.Result });
        if (entry.Consequences.Count > 0)
            items.Add(new UiKeyValueItem { Key = "Последствия", Value = string.Join("; ", entry.Consequences) });

        return new UiPanelBlock
        {
            Title = $"Запись боя: {entry.Title}",
            Blocks = [new UiKeyValueGridBlock { Items = items }]
        };
    }

    private static List<UiTableRow> BuildCombatEffectRows(JsonObject combatant)
    {
        var rows = new List<UiTableRow>();
        AddCombatEffectRows(rows, combatant["activeBuffs"] as JsonArray, "Усиление");
        AddCombatEffectRows(rows, combatant["activeDebuffs"] as JsonArray, "Помеха");
        AddCombatEffectRows(rows, combatant["effects"] as JsonArray, "Эффект");
        AddCombatEffectRows(rows, combatant["statusEffects"] as JsonArray, "Состояние");
        return rows;
    }

    private static void AddCombatEffectRows(List<UiTableRow> rows, JsonArray? effects, string section)
    {
        if (effects == null)
            return;

        foreach (var effect in effects.OfType<JsonObject>())
        {
            var source = FirstCombatNodeString(effect, "sourceSkill", "source");
            var note = FirstCombatNodeString(effect, "effectDescription", "description");
            rows.Add(new UiTableRow
            {
                Cells =
                [
                    section,
                    DescribeCombatEffectType(FirstCombatNodeString(effect, "effectType", "type", "name", "effectName", "description")),
                    EmptyFallback(FirstCombatNodeString(effect, "value", "amount", "effectValue")),
                    EmptyFallback(FirstCombatNodeString(effect, "duration", "expiresIn", "remainingTurns")),
                    EmptyFallback(JoinCombatDetails(source, note))
                ]
            });
        }
    }

    private static List<UiTableRow> BuildCombatActionRows(JsonObject combatant)
    {
        var actions = combatant["actions"] as JsonArray;
        if (actions == null)
            return [];

        var rows = new List<UiTableRow>();
        foreach (var action in actions.OfType<JsonObject>())
        {
            rows.Add(new UiTableRow
            {
                Cells =
                [
                    EmptyFallback(FirstCombatNodeString(action, "actionName", "name", "title")),
                    DescribeCombatActionCost(FirstCombatNodeString(action, "actionCost", "cost")),
                    DescribeCombatActionEffects(action)
                ]
            });
        }

        return rows;
    }

    private static string DescribeCombatActionEffects(JsonObject action)
    {
        if (action["effects"] is not JsonArray effects || effects.Count == 0)
            return EmptyFallback(FirstCombatNodeString(action, "description", "effectDescription", "summary"));

        var parts = new List<string>();
        foreach (var effect in effects.OfType<JsonObject>())
        {
            var type = DescribeCombatEffectType(FirstCombatNodeString(effect, "effectType", "type", "name"));
            var value = FirstCombatNodeString(effect, "value", "amount");
            var target = DescribeCombatTarget(FirstCombatNodeString(effect, "targetTypeDisplayName", "targetType"));
            var description = FirstCombatNodeString(effect, "effectDescription", "description");
            parts.Add(JoinCombatDetails(type, value, target, description));
        }

        return EmptyFallback(string.Join("; ", parts.Where(static part => !string.IsNullOrWhiteSpace(part))));
    }

    private static IReadOnlyList<UiAction> BuildCombatOverviewActions(
        IReadOnlyList<CombatantSnapshot> enemies,
        IReadOnlyList<CombatantSnapshot> allies,
        IReadOnlyList<CombatLogSnapshot> logEntries)
    {
        var actions = new List<UiAction>();
        foreach (var enemy in enemies)
        {
            actions.Add(new UiAction
            {
                Id = "combat-enemy-" + ToActionIdPart(enemy.Selector),
                Label = $"Осмотреть врага «{enemy.Name}»",
                Command = BuildCombatDetailCommand(CombatantKind.Enemy, enemy.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["kind"] = "enemy",
                    ["selector"] = enemy.Selector,
                    ["name"] = enemy.Name
                }
            });
        }

        foreach (var ally in allies)
        {
            actions.Add(new UiAction
            {
                Id = "combat-ally-" + ToActionIdPart(ally.Selector),
                Label = $"Осмотреть союзника «{ally.Name}»",
                Command = BuildCombatDetailCommand(CombatantKind.Ally, ally.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["kind"] = "ally",
                    ["selector"] = ally.Selector,
                    ["name"] = ally.Name
                }
            });
        }

        foreach (var entry in logEntries.Take(10))
        {
            actions.Add(new UiAction
            {
                Id = "combat-log-" + ToActionIdPart(entry.Selector),
                Label = $"Открыть запись боя «{entry.Title}»",
                Command = BuildCombatLogDetailCommand(entry.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["kind"] = "log",
                    ["selector"] = entry.Selector,
                    ["title"] = entry.Title
                }
            });
        }

        return actions;
    }

    private static IEnumerable<CombatantSnapshot> EnumerateCombatants(JsonNode? node, CombatantKind kind)
    {
        var index = 0;
        foreach (var combatant in EnumerateCombatantObjects(node, kind))
        {
            index++;
            var name = FirstNonEmpty(
                FirstCombatNodeString(combatant, "name", "displayName"),
                FirstCombatNodeString(combatant, kind == CombatantKind.Enemy ? "enemyName" : "allyName"),
                kind == CombatantKind.Enemy ? $"Враг {index}" : $"Союзник {index}");
            var identity = FirstNonEmpty(
                FirstCombatNodeString(combatant, kind == CombatantKind.Enemy ? "enemyId" : "allyId"),
                FirstCombatNodeString(combatant, "combatantId", "actorId", "npcId", "id"),
                index.ToString());
            yield return new CombatantSnapshot(kind, index, NormalizeCombatSelector(identity), name, combatant);
        }
    }

    private static IEnumerable<JsonObject> EnumerateCombatantObjects(JsonNode? node, CombatantKind kind)
    {
        if (node is JsonArray array)
        {
            foreach (var combatant in array.OfType<JsonObject>())
                yield return combatant;
            yield break;
        }

        if (node is not JsonObject root)
            yield break;

        var propertyNames = kind == CombatantKind.Enemy
            ? new[] { "enemiesData", "enemies", "UpdateEnemies", "enemyUpdates" }
            : new[] { "alliesData", "allies", "UpdateAllies", "allyUpdates" };
        foreach (var propertyName in propertyNames)
        {
            if (root[propertyName] is JsonArray nested)
            {
                foreach (var combatant in nested.OfType<JsonObject>())
                    yield return combatant;
            }
            else if (root[propertyName] is JsonObject obj)
            {
                yield return obj;
            }
        }
    }

    private static IEnumerable<CombatLogSnapshot> EnumerateCombatLogEntries(JsonNode? node)
    {
        var index = 0;
        foreach (var logNode in EnumerateCombatLogNodes(node))
        {
            index++;
            if (logNode.Object != null)
            {
                var obj = logNode.Object;
                var round = FirstCombatNodeString(obj, "round", "roundNumber");
                var turn = FirstCombatNodeString(obj, "turn", "turnNumber", "timestamp");
                var selector = NormalizeCombatSelector(FirstNonEmpty(
                    FirstCombatNodeString(obj, "entryId", "logId", "eventId", "id"),
                    index.ToString()));
                var title = !string.IsNullOrWhiteSpace(round)
                    ? $"Раунд {round}"
                    : FirstNonEmpty(FirstCombatNodeString(obj, "title", "eventTitle"), $"Запись {index}");
                var summary = FirstNonEmpty(
                    FirstCombatNodeString(obj, "summary", "description", "message", "narrative"),
                    FirstCombatNodeString(obj, "result", "outcome"));
                yield return new CombatLogSnapshot(
                    selector,
                    title,
                    summary,
                    turn,
                    EnumerateStringValues(obj["participants"]).ToList(),
                    FirstCombatNodeString(obj, "result", "outcome"),
                    EnumerateStringValues(obj["consequences"]).ToList());
            }
            else if (!string.IsNullOrWhiteSpace(logNode.Line))
            {
                yield return new CombatLogSnapshot(
                    index.ToString(),
                    $"Строка {index}",
                    logNode.Line,
                    string.Empty,
                    [],
                    string.Empty,
                    []);
            }
        }
    }

    private static IEnumerable<(JsonObject? Object, string Line)> EnumerateCombatLogNodes(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is JsonObject obj)
                    yield return (obj, string.Empty);
                else if (TryGetScalarString(item, out var line) && !string.IsNullOrWhiteSpace(line))
                    yield return (null, line.Trim());
            }
            yield break;
        }

        if (node is not JsonObject root)
            yield break;

        foreach (var propertyName in new[] { "entries", "combatLog", "combat_log", "logEntries", "combatLogEntries" })
        {
            if (root[propertyName] is not JsonArray entries)
                continue;

            foreach (var entry in entries)
            {
                if (entry is JsonObject obj)
                    yield return (obj, string.Empty);
                else if (TryGetScalarString(entry, out var text) && !string.IsNullOrWhiteSpace(text))
                    yield return (null, text.Trim());
            }
        }

        var markdown = FirstCombatNodeString(root, "combat_log_markdown", "markdown", "log");
        if (!string.IsNullOrWhiteSpace(markdown))
        {
            foreach (var line in markdown.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    yield return (null, line);
            }
        }
    }

    private static CombatantSnapshot? FindCombatant(IReadOnlyList<CombatantSnapshot> combatants, string selector)
    {
        var normalized = NormalizeCombatSelector(selector);
        return combatants.FirstOrDefault(combatant =>
            string.Equals(combatant.Selector, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(combatant.Index.ToString(), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeCombatSelector(combatant.Name), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static CombatLogSnapshot? FindCombatLogEntry(IReadOnlyList<CombatLogSnapshot> entries, string selector)
    {
        var normalized = NormalizeCombatSelector(selector);
        return entries.FirstOrDefault(entry =>
            string.Equals(entry.Selector, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeCombatSelector(entry.Title), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static CombatDetailRequest ParseCombatDetailRequest(string remainder)
    {
        if (string.IsNullOrWhiteSpace(remainder))
            return new CombatDetailRequest(CombatDetailKind.Overview, string.Empty);

        var (kindToken, selector) = SplitFirstCombatArgument(remainder);
        var kind = kindToken.Trim().ToLowerInvariant() switch
        {
            "enemy" or "enemies" or "враг" or "врага" or "враги" => CombatDetailKind.Enemy,
            "ally" or "allies" or "союзник" or "союзника" or "союзники" => CombatDetailKind.Ally,
            "log" or "journal" or "entry" or "журнал" or "запись" or "событие" => CombatDetailKind.Log,
            _ => CombatDetailKind.Unknown
        };

        return new CombatDetailRequest(kind, NormalizeCombatSelector(selector));
    }

    private static (string First, string Remainder) SplitFirstCombatArgument(string value)
    {
        var parts = value.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1])
        };
    }

    private static string DescribeCombatantOverview(CombatantSnapshot combatant) =>
        EmptyFallback(JoinCombatDetails(
            DescribeCombatantStatus(combatant.Node),
            DescribeCombatantHealth(combatant.Node),
            DescribeCombatantPoise(combatant.Node)));

    private static string DescribeCombatantStatus(JsonObject combatant) =>
        DescribeCombatStatus(FirstCombatNodeString(combatant, "status", "currentCondition", "state"));

    private static string DescribeCombatantRole(JsonObject combatant) =>
        FirstNonEmpty(
            DescribeCombatRole(FirstCombatNodeString(combatant, "type", "role", "threat", "rank")),
            FirstCombatNodeString(combatant, "roleDescription", "threatDescription"));

    private static string DescribeCombatantHealth(JsonObject combatant)
    {
        if (combatant["healthStates"] is JsonArray states && states.Count > 0)
            return "группа: " + string.Join(", ", states.Select(static state => state?.ToString()).Where(static text => !string.IsNullOrWhiteSpace(text)));

        var current = FirstCombatNodeString(combatant, "currentHealth", "health", "hp", "healthPercentage");
        var max = FirstCombatNodeString(combatant, "maxHealth", "maxHp");
        if (!string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(max))
            return $"{current}/{max}";

        return current;
    }

    private static string DescribeCombatantPoise(JsonObject combatant)
    {
        var current = FirstCombatNodeString(combatant, "currentPoise", "poise", "poisePercentage");
        var max = FirstCombatNodeString(combatant, "maxPoise");
        if (!string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(max))
            return $"{current}/{max}";

        return current;
    }

    private static string DescribeCombatantIntent(JsonObject combatant) =>
        FirstNonEmpty(
            FirstCombatNodeString(combatant, "intent", "currentIntent", "currentAction", "plannedAction"),
            DescribeCombatTarget(FirstCombatNodeString(combatant, "targetPriority")));

    private static string DescribeCombatCount(int count, string singular, string paucal, string plural)
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

    private static string DescribeCombatStatus(string status) =>
        status.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "hostile" => "враждебен",
            "active" => "в бою",
            "wounded" => "ранен",
            "stunned" => "оглушён",
            "dead" or "defeated" => "повержен",
            "hidden" => "скрыт",
            "guarding" => "держит оборону",
            _ => status.Trim()
        };

    private static string DescribeCombatRole(string role) =>
        role.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "boss" => "главная угроза",
            "elite" => "опасный противник",
            "strong" => "сильный противник",
            "moderate" => "средняя угроза",
            "weak" => "слабая угроза",
            "frail" => "хрупкая цель",
            "shield" => "щит",
            "support" => "поддержка",
            "healer" => "целитель",
            "damage" => "ударная роль",
            _ => role.Trim()
        };

    private static string DescribeCombatActionCost(string cost) =>
        cost.Trim().ToLowerInvariant() switch
        {
            "" => "не указано",
            "main" => "основное действие",
            "fast" => "быстрое действие",
            "free" => "свободное действие",
            "reaction" => "реакция",
            _ => cost.Trim()
        };

    private static string DescribeCombatEffectType(string effectType) =>
        effectType.Trim().ToLowerInvariant() switch
        {
            "" => "эффект",
            "damage" => "урон",
            "burn" => "горение",
            "bleed" => "кровотечение",
            "stun" => "оглушение",
            "guard" => "защита",
            "inspire" => "воодушевление",
            "heal" => "лечение",
            "buff" => "усиление",
            "debuff" => "помеха",
            _ => effectType.Trim()
        };

    private static string DescribeCombatTarget(string target) =>
        target.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "caster" => "заклинатель",
            "player" => "игрок",
            "ally" => "союзник",
            "allies" => "союзники",
            "enemy" => "противник",
            "single_enemy" => "одна цель",
            "all_enemies" => "все противники",
            _ => target.Trim()
        };

    private static string BuildCombatDetailCommand(CombatantKind kind, string selector) =>
        kind == CombatantKind.Enemy
            ? "/бой враг " + FormatCombatCommandArgument(selector)
            : "/бой союзник " + FormatCombatCommandArgument(selector);

    private static string BuildCombatLogDetailCommand(string selector) =>
        "/бой журнал " + FormatCombatCommandArgument(selector);

    private static string FormatCombatCommandArgument(string selector)
    {
        if (selector.All(static ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.'))
            return selector;

        return "\"" + selector.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static string NormalizeCombatSelector(string selector)
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

    private static string FirstCombatNodeString(JsonNode? node, params string[] properties)
    {
        foreach (var property in properties)
        {
            var value = GetNodeString(node, property);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumerateStringValues(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (TryGetScalarString(item, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                    continue;
                }

                if (item is JsonObject obj)
                {
                    var label = FirstCombatNodeString(obj, "displayName", "name", "actorName", "participantName", "id");
                    if (!string.IsNullOrWhiteSpace(label))
                        yield return label;
                }
            }
        }
        else if (TryGetScalarString(node, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            yield return value;
        }
    }

    private static string JoinCombatDetails(params string[] parts) =>
        string.Join("; ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)).Select(static part => part.Trim()));

    private static void AddCombatReadWarnings(List<UiBlock> blocks, CombatState state)
    {
        AddCombatReadWarning(blocks, state.Enemies, "врагов");
        AddCombatReadWarning(blocks, state.Allies, "союзников");
        AddCombatReadWarning(blocks, state.Log, "боевого журнала");
    }

    private static void AddCombatReadWarning(List<UiBlock> blocks, JsonReadResult read, string section)
    {
        if (read.FileExists && read.Node == null && !string.IsNullOrWhiteSpace(read.Error))
            blocks.Add(Message(UiNotificationSeverity.Warning, "Бой", $"Запись {section} найдена, но не разобрана как JSON."));
    }

    private static void AddCombatRawState(List<UiBlock> blocks, JsonReadResult read, string title)
    {
        if (read.Node != null)
            blocks.Add(Raw(title, read.Node));
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

    private enum CombatantKind
    {
        Enemy,
        Ally
    }

    private enum CombatDetailKind
    {
        Overview,
        Enemy,
        Ally,
        Log,
        Unknown
    }

    private sealed record CombatState(
        JsonReadResult Enemies,
        JsonReadResult Allies,
        JsonReadResult Log);

    private sealed record CombatantSnapshot(
        CombatantKind Kind,
        int Index,
        string Selector,
        string Name,
        JsonObject Node);

    private sealed record CombatLogSnapshot(
        string Selector,
        string Title,
        string Summary,
        string Turn,
        IReadOnlyList<string> Participants,
        string Result,
        IReadOnlyList<string> Consequences);

    private sealed record CombatDetailRequest(
        CombatDetailKind Kind,
        string Selector);

    private enum InteractionDetailKind
    {
        Overview,
        Player,
        Record,
        Unknown
    }

    private sealed record InteractionState(JsonReadResult PlayerInteractions);

    private sealed record InteractionPlayerSnapshot(
        int Index,
        string Selector,
        string Name,
        JsonObject Node,
        IReadOnlyList<InteractionRecordSnapshot> Records);

    private sealed record InteractionRecordSnapshot(
        int Index,
        string Selector,
        string Title,
        string PlayerSelector,
        string PlayerName,
        JsonObject Node);

    private sealed record InteractionDetailRequest(
        InteractionDetailKind Kind,
        string Selector);

    private enum ReferenceDetailKind
    {
        Overview,
        Detail,
        Unknown
    }

    private sealed record ReferenceCommandDefinition(
        string Title,
        string DetailTitlePrefix,
        string ActionIdPrefix,
        string EnglishCommand,
        string EnglishDetailToken,
        string RussianDetailToken,
        string NotFoundTitle,
        string NotFoundMessage,
        IReadOnlyList<SummarySpec> Specs);

    private sealed record ReferenceEntrySnapshot(
        int Index,
        string Selector,
        string Title,
        string Section,
        string Summary,
        JsonObject Node);

    private sealed record ReferenceDetailRequest(
        ReferenceDetailKind Kind,
        string Selector);

    private sealed record SummarySpec(string Path, string PropertyName, string Label);

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);
}
