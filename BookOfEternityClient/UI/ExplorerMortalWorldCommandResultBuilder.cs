using System.Globalization;
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
            CommandKind.Inventory => await BuildInventory(normalizedCommand, fs),
            CommandKind.Npcs => await BuildNpcs(normalizedCommand, fs),
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
            CommandKind.CurrentLocation => await BuildCurrentLocation(commandToken, fs),
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
            CommandKind.Effects => await BuildEffects(normalizedCommand, fs, stateManager),
            CommandKind.Combat => await BuildCombat(normalizedCommand, fs),
            CommandKind.Weather => await BuildWeather(commandToken, fs),
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

        await AddStatsTableIfPresent(
            blocks,
            fs,
            "game_state/misc/characteristics.json",
            "Базовые характеристики",
            TranslateBaseCharacteristicKey);
        await AddStatsTableIfPresent(
            blocks,
            fs,
            "game_state/player/computed_characteristics.json",
            "Расчётные показатели",
            TranslateComputedCharacteristicKey);
        return Completed(command, blocks);
    }

    private static async Task AddStatsTableIfPresent(
        List<UiBlock> blocks,
        FileSystemManager fs,
        string path,
        string title,
        Func<string, string> translateKey)
    {
        var read = await ReadJson(fs, path);
        if (!read.FileExists)
            return;

        if (read.Node is not JsonObject obj)
        {
            blocks.Add(Message(UiNotificationSeverity.Warning, title, "Запись характеристик найдена, но её не удалось прочитать."));
            return;
        }

        var rows = EnumerateStatsProperties(obj)
            .Where(static property => !IsTechnicalStatsProperty(property.Key))
            .Select(property => new UiTableRow
            {
                Cells = [translateKey(property.Key), FormatStatsValue(property.Value)]
            })
            .Where(static row => row.Cells.Count >= 2 && !string.IsNullOrWhiteSpace(row.Cells[1]))
            .ToList();

        if (rows.Count == 0)
            return;

        blocks.Add(new UiTableBlock
        {
            Title = title,
            Columns = ["Показатель", "Значение"],
            Rows = rows
        });
    }

    private static IEnumerable<KeyValuePair<string, JsonNode?>> EnumerateStatsProperties(JsonObject obj)
    {
        var expandedContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in obj)
        {
            if (IsStatsContainerProperty(property.Key) && property.Value is JsonObject nested)
            {
                expandedContainers.Add(property.Key);
                foreach (var nestedProperty in nested)
                    yield return nestedProperty;
            }
        }

        foreach (var property in obj)
        {
            if (expandedContainers.Contains(property.Key))
                continue;

            yield return property;
        }
    }

    private static bool IsStatsContainerProperty(string propertyName) =>
        propertyName.Equals("setCharacteristics", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("characteristics", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("baseCharacteristics", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("computedCharacteristics", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("setComputedCharacteristics", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("stats", StringComparison.OrdinalIgnoreCase);

    private static bool IsTechnicalStatsProperty(string propertyName) =>
        propertyName.Equals("schemaVersion", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("updatedAt", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Equals("lastUpdatedAt", StringComparison.OrdinalIgnoreCase) ||
        propertyName.StartsWith("_", StringComparison.OrdinalIgnoreCase);

    private static string FormatStatsValue(JsonNode? node)
    {
        if (TryGetScalarString(node, out var scalar))
            return FormatInventoryProtocolValue(scalar);

        if (node is JsonArray array)
        {
            var values = array
                .Select(FormatStatsValue)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            return values.Count == 0 ? $"{array.Count} записей" : string.Join("; ", values);
        }

        if (node is JsonObject obj)
        {
            var values = obj
                .Where(static property => !IsTechnicalStatsProperty(property.Key))
                .Select(static property => $"{TranslateComputedCharacteristicKey(property.Key)}: {FormatStatsValue(property.Value)}")
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            return values.Count == 0 ? string.Empty : string.Join("; ", values);
        }

        return string.Empty;
    }

    private static string TranslateBaseCharacteristicKey(string key) =>
        key switch
        {
            "strength" => "Сила",
            "dexterity" => "Ловкость",
            "agility" => "Проворство",
            "endurance" or "constitution" => "Выносливость",
            "perception" => "Восприятие",
            "intelligence" => "Интеллект",
            "wisdom" => "Мудрость",
            "willpower" or "will" => "Воля",
            "charisma" => "Харизма",
            "luck" => "Удача",
            "arcana" => "Аркана",
            "faith" => "Вера",
            "spirit" => "Дух",
            "attractiveness" => "Привлекательность",
            "trade" => "Торговля",
            "persuasion" => "Убеждение",
            "speed" => "Скорость",
            _ => HumanizeStatsKey(key)
        };

    private static string TranslateComputedCharacteristicKey(string key) =>
        key switch
        {
            "health" or "healthCurrent" => "Здоровье",
            "healthMax" or "maxHealth" => "Максимум здоровья",
            "energy" or "energyCurrent" => "Энергия",
            "energyMax" or "maxEnergy" => "Максимум энергии",
            "poise" or "poiseCurrent" => "Равновесие",
            "poiseMax" or "maxPoise" => "Максимум равновесия",
            "carryWeight" or "maxCarryWeight" => "Грузоподъёмность",
            "inventoryWeight" or "currentWeight" => "Вес снаряжения",
            "arcaneFocus" => "Магический фокус",
            "physicalDamage" => "Физический урон",
            "magicDamage" => "Магический урон",
            "defense" or "armor" => "Защита",
            "resistance" => "Сопротивление",
            "initiative" => "Инициатива",
            "speed" => "Скорость",
            _ => HumanizeStatsKey(key)
        };

    private static string HumanizeStatsKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "Показатель";

        return string.Concat(key.Select((ch, index) =>
                index > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString()))
            .Replace('_', ' ')
            .Trim();
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
                    new UiTableRow { Cells = ["Царство", ExplorerPlayerFacingLabels.Realm(map.Realm)] },
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
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(command.Trim());
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
        var effectEntries = BuildEffectSnapshots(read.Node);
        var detailRequest = ParseEffectDetailRequest(ExtractCommandRemainder(command));
        if (detailRequest.Kind == EffectDetailKind.Unknown)
        {
            return Completed(command, [
                Message(UiNotificationSeverity.Warning, title, "Для подробностей используйте команду вида /эффекты эффект <идентификатор>.")
            ], BuildEffectsBackActions(commandToken));
        }

        if (detailRequest.Kind == EffectDetailKind.Effect)
        {
            var selected = FindEffectSnapshot(effectEntries, detailRequest.Selector);
            if (selected == null)
            {
                return Completed(command, [
                    Message(UiNotificationSeverity.Warning, title, "Такой эффект не найден.")
                ], BuildEffectsBackActions(commandToken));
            }

            return Completed(command, BuildEffectDetailBlocks(selected), BuildEffectsBackActions(commandToken));
        }

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

        return Completed(command, blocks, BuildEffectDetailActions(commandToken, effectEntries));
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

    private static List<EffectSnapshot> BuildEffectSnapshots(JsonNode? node)
    {
        var entries = new List<EffectSnapshot>();
        var usedSelectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (node is JsonArray array)
        {
            AddEffectSnapshots(entries, usedSelectors, "Активный эффект", array);
            return entries;
        }

        if (node is not JsonObject root)
            return entries;

        AddEffectSnapshots(entries, usedSelectors, "Активный эффект", root["activeEffects"] as JsonArray);
        AddEffectSnapshots(entries, usedSelectors, "Рана", root["wounds"] as JsonArray);
        AddEffectSnapshots(entries, usedSelectors, "Временное состояние", root["temporaryConditions"] as JsonArray);
        return entries;
    }

    private static void AddEffectSnapshots(
        List<EffectSnapshot> entries,
        HashSet<string> usedSelectors,
        string section,
        JsonArray? effects)
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
            var identity = FirstNonEmpty(
                GetNodeString(effect, "effectId"),
                GetNodeString(effect, "conditionId"),
                GetNodeString(effect, "woundId"),
                GetNodeString(effect, "id"),
                name);
            var selector = BuildUniqueEffectSelector(identity, entries.Count, usedSelectors);
            entries.Add(new EffectSnapshot(entries.Count + 1, selector, section, name, effect));
        }
    }

    private static EffectDetailRequest ParseEffectDetailRequest(string remainder)
    {
        if (string.IsNullOrWhiteSpace(remainder))
            return new EffectDetailRequest(EffectDetailKind.Overview, string.Empty);

        var (kindToken, selector) = SplitFirstCombatArgument(remainder);
        if (string.IsNullOrWhiteSpace(selector) || !IsEffectDetailToken(kindToken))
            return new EffectDetailRequest(EffectDetailKind.Unknown, string.Empty);

        return new EffectDetailRequest(EffectDetailKind.Effect, NormalizeCombatSelector(selector));
    }

    private static bool IsEffectDetailToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        return normalized is "effect" or "эффект" or "condition" or "состояние" or "рана" or "wound" or "detail" or "подробнее";
    }

    private static EffectSnapshot? FindEffectSnapshot(IReadOnlyList<EffectSnapshot> entries, string selector)
    {
        var normalized = NormalizeInventoryLookup(selector);
        return entries.FirstOrDefault(entry =>
            string.Equals(entry.Selector, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.Index.ToString(), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeInventoryLookup(entry.Name), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<UiBlock> BuildEffectDetailBlocks(EffectSnapshot effect)
    {
        var blocks = new List<UiBlock>();
        var detailBlocks = new List<UiBlock>();
        var description = FirstNonEmpty(
            GetNodeString(effect.Node, "effectDescription"),
            GetNodeString(effect.Node, "description"),
            GetNodeString(effect.Node, "summary"));
        if (!string.IsNullOrWhiteSpace(description))
            detailBlocks.Add(new UiTextBlock { Text = description, Tone = UiTone.Default });

        var facts = new List<UiKeyValueItem>
        {
            new() { Key = "Раздел", Value = effect.Section }
        };
        AddInventoryFact(facts, "Длительность", FirstNonEmpty(GetNodeString(effect.Node, "duration"), GetNodeString(effect.Node, "expiresAt")));
        AddInventoryFact(facts, "Осталось ходов", GetNodeString(effect.Node, "remainingTurns"));
        AddInventoryFact(facts, "Источник", GetNodeString(effect.Node, "source"));
        AddInventoryFact(facts, "Серьёзность", GetNodeString(effect.Node, "severity"));
        AddInventoryFact(facts, "Состояние", FirstNonEmpty(GetNodeString(effect.Node, "status"), GetNodeString(effect.Node, "state")));
        if (facts.Count > 0)
            detailBlocks.Add(new UiKeyValueGridBlock { Items = facts });

        AddStructuredBonusBlock(detailBlocks, effect.Node["structuredBonuses"] as JsonArray);
        AddInventoryCombatEffectBlock(detailBlocks, effect.Node["combatEffect"]);
        AddInventoryCustomPropertiesBlock(detailBlocks, effect.Node["customProperties"]);

        var hiddenNotes = CollectEffectNarrativeEntries(effect.Node["notes"])
            .Concat(CollectEffectNarrativeEntries(effect.Node["journalEntries"]))
            .ToList();
        if (hiddenNotes.Count > 0)
        {
            detailBlocks.Add(new UiPanelBlock
            {
                Title = "Заметки",
                Blocks = hiddenNotes.Select(static note => (UiBlock)new UiTextBlock { Text = note, Tone = UiTone.Muted }).ToList()
            });
        }

        if (detailBlocks.Count == 0)
            detailBlocks.Add(new UiTextBlock { Text = "Подробности эффекта пока не заполнены.", Tone = UiTone.Muted });

        blocks.Add(new UiPanelBlock
        {
            Title = $"{effect.Section}: {effect.Name}",
            Blocks = detailBlocks
        });
        blocks.Add(new UiTextBlock { Text = "Вернуться к списку можно командой /эффекты.", Tone = UiTone.Muted });
        return blocks;
    }

    private static IReadOnlyList<UiAction> BuildEffectDetailActions(string commandToken, IReadOnlyList<EffectSnapshot> entries)
    {
        var actions = new List<UiAction>();
        foreach (var entry in entries)
        {
            actions.Add(new UiAction
            {
                Id = "effects-detail-" + ToActionIdPart(entry.Selector),
                Label = $"Подробнее: «{entry.Name}»",
                Command = BuildEffectDetailCommand(commandToken, entry.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["selector"] = entry.Selector,
                    ["name"] = entry.Name,
                    ["section"] = entry.Section
                }
            });
        }

        return actions;
    }

    private static IReadOnlyList<UiAction> BuildEffectsBackActions(string commandToken) =>
    [
        new UiAction
        {
            Id = "effects-back",
            Label = "Назад к эффектам",
            Command = commandToken,
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false
        }
    ];

    private static string BuildEffectDetailCommand(string commandToken, string selector)
    {
        var detailToken = string.Equals(commandToken, "/effects", StringComparison.OrdinalIgnoreCase)
            ? "effect"
            : "эффект";
        return commandToken + " " + detailToken + " " + FormatCombatCommandArgument(selector);
    }

    private static IEnumerable<string> CollectEffectNarrativeEntries(JsonNode? node)
    {
        if (node is not JsonArray array)
            yield break;

        foreach (var entry in array)
        {
            var text = FormatInventoryNodeValue(entry);
            if (!string.IsNullOrWhiteSpace(text))
                yield return text;
        }
    }

    private static string BuildUniqueEffectSelector(string value, int index, HashSet<string> usedSelectors)
    {
        var baseSelector = NormalizeReferenceSelector(value);
        if (string.IsNullOrWhiteSpace(baseSelector))
            baseSelector = $"effect-{index + 1}";

        var selector = baseSelector;
        var suffix = 2;
        while (!usedSelectors.Add(selector))
        {
            selector = $"{baseSelector}-{suffix}";
            suffix++;
        }

        return selector;
    }

    private static async Task<ExplorerCommandResult> BuildNpcs(string command, FileSystemManager fs)
    {
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(command.Trim());
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
                var journalRequest = ParseNpcJournalDetailRequest(ExtractCommandRemainder(command));
                if (journalRequest.Kind == NpcJournalDetailKind.Unknown)
                {
                    return Completed(command, [
                        Message(UiNotificationSeverity.Warning, "Персонажи", "Для подробностей используйте команду вида /нпс журнал <нпс>.")
                    ], BuildNpcBackActions(commandToken));
                }

                if (journalRequest.Kind == NpcJournalDetailKind.Journal)
                {
                    var selected = FindNpcJournalFallbackEntry(fallbackEntries, journalRequest.NpcSelector);
                    if (selected == null)
                    {
                        return Completed(command, [
                            Message(UiNotificationSeverity.Warning, "Персонажи", "Такой журнал НПС не найден.")
                        ], BuildNpcBackActions(commandToken));
                    }

                    return Completed(command, NpcJournalFallbackProjection.BuildDetailBlocks(selected), BuildNpcBackActions(commandToken));
                }

                var rows = NpcJournalFallbackProjection.BuildConsoleRows(fallbackEntries);
                return Completed(command, NpcJournalFallbackProjection.BuildBlocks(rows), BuildNpcJournalFallbackActions(commandToken, fallbackEntries));
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

        var sectionRequest = ParseNpcSectionDetailRequest(ExtractCommandRemainder(command));
        if (sectionRequest.Kind == NpcSectionDetailKind.Unknown)
        {
            return Completed(command, [
                Message(UiNotificationSeverity.Warning, "Персонажи", "Для подробностей используйте команду вида /npc section <нпс> <раздел>.")
            ], BuildNpcBackActions(commandToken));
        }

        if (sectionRequest.Kind == NpcSectionDetailKind.Section)
        {
            var selected = FindNpcSection(projections, sectionRequest.NpcSelector, sectionRequest.SectionSelector);
            if (selected == null)
            {
                return Completed(command, [
                    Message(UiNotificationSeverity.Warning, "Персонажи", "Такой раздел НПС не найден.")
                ], BuildNpcBackActions(commandToken));
            }

            return Completed(command, BuildNpcSectionDetailBlocks(selected.Value.Projection, selected.Value.Section), BuildNpcBackActions(commandToken));
        }

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

        return Completed(command, blocks, BuildNpcSectionActions(commandToken, projections));
    }

    private static NpcJournalDetailRequest ParseNpcJournalDetailRequest(string remainder)
    {
        if (string.IsNullOrWhiteSpace(remainder))
            return new NpcJournalDetailRequest(NpcJournalDetailKind.Overview, string.Empty);

        var (kindToken, selector) = SplitFirstCombatArgument(remainder);
        if (!IsNpcJournalDetailToken(kindToken) || string.IsNullOrWhiteSpace(selector))
            return new NpcJournalDetailRequest(NpcJournalDetailKind.Unknown, string.Empty);

        return new NpcJournalDetailRequest(NpcJournalDetailKind.Journal, NormalizeCombatSelector(selector));
    }

    private static bool IsNpcJournalDetailToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        return normalized is "journal" or "журнал" or "дневник" or "заметки" or "detail" or "подробнее";
    }

    private static NpcJournalFallbackEntry? FindNpcJournalFallbackEntry(
        IReadOnlyList<NpcJournalFallbackEntry> entries,
        string selector)
    {
        var normalizedSelector = NormalizeInventoryLookup(selector);
        return entries.FirstOrDefault(entry =>
            string.Equals(NormalizeInventoryLookup(entry.NpcId), normalizedSelector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeInventoryLookup(entry.NpcName), normalizedSelector, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeInventoryLookup(entry.DisplayName), normalizedSelector, StringComparison.OrdinalIgnoreCase));
    }

    private static NpcSectionDetailRequest ParseNpcSectionDetailRequest(string remainder)
    {
        if (string.IsNullOrWhiteSpace(remainder))
            return new NpcSectionDetailRequest(NpcSectionDetailKind.Overview, string.Empty, string.Empty);

        var (kindToken, detailRemainder) = SplitFirstCombatArgument(remainder);
        if (!IsNpcSectionDetailToken(kindToken))
            return new NpcSectionDetailRequest(NpcSectionDetailKind.Unknown, string.Empty, string.Empty);

        var (npcSelector, sectionSelector) = SplitFirstCombatArgument(detailRemainder);
        if (string.IsNullOrWhiteSpace(npcSelector) || string.IsNullOrWhiteSpace(sectionSelector))
            return new NpcSectionDetailRequest(NpcSectionDetailKind.Unknown, string.Empty, string.Empty);

        return new NpcSectionDetailRequest(
            NpcSectionDetailKind.Section,
            NormalizeCombatSelector(npcSelector),
            NormalizeCombatSelector(sectionSelector));
    }

    private static bool IsNpcSectionDetailToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        return normalized is "section" or "раздел" or "detail" or "подробнее";
    }

    private static (NpcDetailProjection Projection, NpcDetailSection Section)? FindNpcSection(
        IReadOnlyList<NpcDetailProjection> projections,
        string npcSelector,
        string sectionSelector)
    {
        var normalizedNpc = NormalizeInventoryLookup(npcSelector);
        var normalizedSection = NormalizeInventoryLookup(sectionSelector);
        foreach (var projection in projections)
        {
            var npcMatches =
                string.Equals(NormalizeInventoryLookup(projection.NpcId), normalizedNpc, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeInventoryLookup(projection.NpcName), normalizedNpc, StringComparison.OrdinalIgnoreCase);
            if (!npcMatches)
                continue;

            var section = projection.Sections.FirstOrDefault(section =>
                string.Equals(NormalizeInventoryLookup(section.Id), normalizedSection, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeInventoryLookup(section.Label), normalizedSection, StringComparison.OrdinalIgnoreCase));
            if (section != null)
                return (projection, section);
        }

        return null;
    }

    private static IReadOnlyList<UiBlock> BuildNpcSectionDetailBlocks(NpcDetailProjection projection, NpcDetailSection section)
    {
        var blocks = new List<UiBlock>
        {
            new UiKeyValueGridBlock
            {
                Items =
                [
                    new UiKeyValueItem { Key = "НПС", Value = projection.NpcName },
                    new UiKeyValueItem { Key = "Раздел", Value = section.Label },
                    new UiKeyValueItem { Key = "Состояние", Value = section.Hint }
                ]
            }
        };
        blocks.AddRange(section.Blocks);
        blocks.Add(new UiTextBlock { Text = "Вернуться к списку можно командой /npc.", Tone = UiTone.Muted });
        return blocks;
    }

    private static IReadOnlyList<UiAction> BuildNpcSectionActions(
        string commandToken,
        IReadOnlyList<NpcDetailProjection> projections)
    {
        var actions = new List<UiAction>();
        foreach (var projection in projections)
        {
            var npcSelector = FirstNonEmpty(projection.NpcId, projection.NpcName);
            foreach (var section in projection.Sections)
            {
                actions.Add(new UiAction
                {
                    Id = "npc-section-" + ToActionIdPart(npcSelector) + "-" + ToActionIdPart(section.Id),
                    Label = $"{projection.NpcName}: {section.Label}",
                    Command = BuildNpcSectionCommand(commandToken, npcSelector, section.Id),
                    Style = UiActionStyle.Secondary,
                    RequiresConfirmation = false,
                    Payload = new JsonObject
                    {
                        ["npcSelector"] = npcSelector,
                        ["npcName"] = projection.NpcName,
                        ["section"] = section.Id,
                        ["sectionLabel"] = section.Label
                    }
                });
            }
        }

        return actions;
    }

    private static IReadOnlyList<UiAction> BuildNpcJournalFallbackActions(
        string commandToken,
        IReadOnlyList<NpcJournalFallbackEntry> entries)
    {
        return entries
            .Select(entry =>
            {
                var npcSelector = FirstNonEmpty(entry.NpcId, entry.NpcName, entry.DisplayName);
                return new UiAction
                {
                    Id = "npc-journal-" + ToActionIdPart(npcSelector),
                    Label = $"{entry.DisplayName}: журнал",
                    Command = BuildNpcJournalFallbackCommand(commandToken, npcSelector),
                    Style = UiActionStyle.Secondary,
                    RequiresConfirmation = false,
                    Payload = new JsonObject
                    {
                        ["npcSelector"] = npcSelector,
                        ["npcName"] = entry.DisplayName,
                        ["section"] = "journal",
                        ["sectionLabel"] = "Журнал"
                    }
                };
            })
            .ToList();
    }

    private static IReadOnlyList<UiAction> BuildNpcBackActions(string commandToken) =>
    [
        new UiAction
        {
            Id = "npc-back",
            Label = "Назад к персонажам",
            Command = commandToken,
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false
        }
    ];

    private static string BuildNpcJournalFallbackCommand(string commandToken, string npcSelector)
    {
        var detailToken = string.Equals(commandToken, "/нпс", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(commandToken, "/персонажи", StringComparison.OrdinalIgnoreCase)
            ? "журнал"
            : "journal";
        return commandToken + " " + detailToken + " " + FormatCombatCommandArgument(npcSelector);
    }

    private static string BuildNpcSectionCommand(string commandToken, string npcSelector, string sectionSelector)
    {
        var detailToken = string.Equals(commandToken, "/нпс", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(commandToken, "/персонажи", StringComparison.OrdinalIgnoreCase)
            ? "раздел"
            : "section";
        return commandToken + " " + detailToken + " " + FormatCombatCommandArgument(npcSelector) + " " + FormatCombatCommandArgument(sectionSelector);
    }

    private static async Task<ExplorerCommandResult> BuildCurrentLocation(string command, FileSystemManager fs)
    {
        var locationRead = await ReadJson(fs, "game_state/world/current_location.json");
        var timeRead = await ReadJson(fs, "game_state/world/world_time.json");
        var weatherRead = await ReadJson(fs, "game_state/world/weather.json");
        var location = UnwrapCurrentLocationNode(locationRead.Node);
        var weather = UnwrapWeatherNode(weatherRead.Node);
        var blocks = new List<UiBlock>();

        if (location == null)
        {
            blocks.Add(Message(UiNotificationSeverity.Info, "Где я", "Местоположение неизвестно."));
        }
        else
        {
            var title = FirstNonEmpty(
                GetLocationNodeString(location, "name", "locationName", "displayName"),
                "Текущая локация");
            var facts = new List<UiKeyValueItem>();
            AddLocationFact(facts, "Локация", title);
            AddLocationFact(facts, "Регион", GetLocationNodeString(location, "region"));
            AddLocationFact(facts, "Тип", GetLocationNodeString(location, "locationType", "type"));
            AddLocationFact(facts, "Биом", GetLocationNodeString(location, "biome"));
            AddLocationFact(facts, "Опасность", DescribeLocationDifficulty(location));

            var panelBlocks = new List<UiBlock>();
            var description = GetLocationNodeString(location, "description", "shortDescription");
            if (!string.IsNullOrWhiteSpace(description))
                panelBlocks.Add(new UiTextBlock { Text = description, Tone = UiTone.Default });
            if (facts.Count > 0)
                panelBlocks.Add(new UiKeyValueGridBlock { Items = facts });

            blocks.Add(new UiPanelBlock
            {
                Title = title,
                Blocks = panelBlocks.Count > 0 ? panelBlocks : [new UiTextBlock { Text = "Описание локации пока не заполнено.", Tone = UiTone.Muted }]
            });

            AddLocationFeatureBlock(blocks, location);
            AddLocationFactionControlBlock(blocks, location);
            AddLocationThreatBlock(blocks, location);
            AddLocationEventBlock(blocks, location);
        }

        AddWorldTimeBlock(blocks, timeRead.Node);
        AddWeatherSummaryBlock(blocks, weather, includeBiome: false, currentLocation: location);
        AddReadWarning(blocks, "Где я", locationRead);
        AddReadWarning(blocks, "Где я", timeRead);
        AddReadWarning(blocks, "Где я", weatherRead);
        if (locationRead.Node != null)
            blocks.Add(Raw("Полная запись: местоположение", locationRead.Node));
        if (timeRead.Node != null)
            blocks.Add(Raw("Полная запись: время мира", timeRead.Node));
        if (weatherRead.Node != null)
            blocks.Add(Raw("Полная запись: погода", weatherRead.Node));

        return Completed(command, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildWeather(string command, FileSystemManager fs)
    {
        var timeRead = await ReadJson(fs, "game_state/world/world_time.json");
        var weatherRead = await ReadJson(fs, "game_state/world/weather.json");
        var locationRead = await ReadJson(fs, "game_state/world/current_location.json");
        var location = UnwrapCurrentLocationNode(locationRead.Node);
        var weather = UnwrapWeatherNode(weatherRead.Node);
        var blocks = new List<UiBlock>();

        AddWorldTimeBlock(blocks, timeRead.Node);
        AddWeatherSummaryBlock(blocks, weather, includeBiome: true, currentLocation: location);

        if (blocks.Count == 0)
            blocks.Add(Message(UiNotificationSeverity.Info, "Время и погода", "Данные ещё не созданы."));

        AddReadWarning(blocks, "Время и погода", timeRead);
        AddReadWarning(blocks, "Время и погода", weatherRead);
        AddReadWarning(blocks, "Время и погода", locationRead);
        if (timeRead.Node != null)
            blocks.Add(Raw("Полная запись: время мира", timeRead.Node));
        if (weatherRead.Node != null)
            blocks.Add(Raw("Полная запись: погода", weatherRead.Node));

        return Completed(command, blocks);
    }

    private static JsonObject? UnwrapWeatherNode(JsonNode? node)
    {
        if (node is not JsonObject root)
            return null;

        return root["weatherChange"] as JsonObject ??
               root["normalizedWeatherState"] as JsonObject ??
               root;
    }

    private static void AddWorldTimeBlock(List<UiBlock> blocks, JsonNode? node)
    {
        var source = UnwrapWorldTimeNode(node);
        var items = BuildWorldTimeItems(source);
        if (items.Count == 0)
            return;

        blocks.Add(Panel("Время", new UiKeyValueGridBlock { Items = items }));
    }

    private static JsonObject? UnwrapWorldTimeNode(JsonNode? node)
    {
        if (node is not JsonObject root)
            return null;

        return root["setWorldTime"] as JsonObject ?? root;
    }

    private static List<UiKeyValueItem> BuildWorldTimeItems(JsonObject? source)
    {
        var items = new List<UiKeyValueItem>();
        if (source == null)
            return items;

        var year = GetLocationNodeString(source, "year");
        var month = GetLocationNodeString(source, "monthName", "month");
        var day = GetLocationNodeString(source, "dayOfMonth", "day");
        var timeOfDay = GetLocationNodeString(source, "timeOfDay", "currentTime");
        var absolute = JoinLocationDetails(
            string.IsNullOrWhiteSpace(day) ? string.Empty : day,
            month,
            string.IsNullOrWhiteSpace(year) ? string.Empty : $"{year} г.",
            timeOfDay);

        AddLocationFact(items, "Сейчас", absolute);
        AddLocationFact(items, "Прошло за ход", FormatMinutes(GetLocationNodeString(source, "timeChange")));
        AddLocationFact(items, "Минут от начала суток", GetLocationNodeString(source, "currentTimeInMinutes"));
        return items;
    }

    private static void AddWeatherSummaryBlock(
        List<UiBlock> blocks,
        JsonObject? weather,
        bool includeBiome,
        JsonObject? currentLocation)
    {
        if (weather == null)
            return;

        var items = new List<UiKeyValueItem>();
        if (includeBiome && currentLocation != null)
            AddLocationFact(items, "Биом", GetLocationNodeString(currentLocation, "biome"));
        AddLocationFact(items, "Состояние", GetLocationNodeString(weather, "currentState", "state"));
        AddLocationFact(items, "Сезон", GetLocationNodeString(weather, "season"));
        AddLocationFact(items, "Температура", GetLocationNodeString(weather, "temperature"));
        AddLocationFact(items, "Ветер", GetLocationNodeString(weather, "windSpeed", "wind"));
        AddLocationFact(items, "Видимость", GetLocationNodeString(weather, "visibility"));
        AddLocationFact(items, "Тенденция", DescribeWeatherTendency(GetLocationNodeString(weather, "tendency")));
        AddLocationFact(items, "Эффекты", GetLocationNodeString(weather, "mechanicalEffects"));

        var panelBlocks = new List<UiBlock>();
        var description = GetLocationNodeString(weather, "description");
        if (!string.IsNullOrWhiteSpace(description))
            panelBlocks.Add(new UiTextBlock { Text = description, Tone = UiTone.Default });
        if (items.Count > 0)
            panelBlocks.Add(new UiKeyValueGridBlock { Items = items });
        if (panelBlocks.Count == 0)
            return;

        blocks.Add(new UiPanelBlock
        {
            Title = "Погода",
            Blocks = panelBlocks
        });
    }

    private static void AddLocationFeatureBlock(List<UiBlock> blocks, JsonObject location)
    {
        var features = EnumerateLocationTextEntries(location["features"]).ToList();
        if (features.Count == 0)
            return;

        blocks.Add(Panel("Особенности", new UiListBlock
        {
            Ordered = false,
            Items = features
        }));
    }

    private static void AddLocationFactionControlBlock(List<UiBlock> blocks, JsonObject location)
    {
        if (location["factionControl"] is not JsonArray factions)
            return;

        var rows = new List<UiTableRow>();
        foreach (var faction in factions.OfType<JsonObject>())
        {
            var name = FirstNonEmpty(
                GetLocationNodeString(faction, "factionName", "name"),
                GetLocationNodeString(faction, "factionId"));
            var control = GetLocationNodeString(faction, "controlLevel");
            var type = GetLocationNodeString(faction, "controlType", "type");
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(control) && string.IsNullOrWhiteSpace(type))
                continue;

            rows.Add(new UiTableRow
            {
                Cells = [EmptyFallback(name), EmptyFallback(type), FormatPercent(control)]
            });
        }

        if (rows.Count == 0)
            return;

        blocks.Add(new UiTableBlock
        {
            Title = "Контроль фракций",
            Columns = ["Фракция", "Вид контроля", "Уровень"],
            Rows = rows
        });
    }

    private static void AddLocationThreatBlock(List<UiBlock> blocks, JsonObject location)
    {
        if (location["activeThreats"] is not JsonArray threats)
            return;

        var rows = new List<UiTableRow>();
        foreach (var threat in threats.OfType<JsonObject>())
        {
            var name = FirstNonEmpty(
                GetLocationNodeString(threat, "name", "threatName"),
                "Неизвестная угроза");
            var danger = FirstNonEmpty(
                DescribeThreatDanger(GetLocationNodeString(threat, "dangerLevel")),
                FormatThreatIntensity(GetLocationNodeString(threat, "intensity")));
            var activity = FirstNonEmpty(
                GetLocationNodeString(threat["currentActivity"], "activityName", "name"),
                GetLocationNodeString(threat, "currentActivity"));
            var details = JoinLocationDetails(
                activity,
                GetLocationNodeString(threat, "longTermGoal"),
                GetLocationNodeString(threat, "description", "summary"));

            rows.Add(new UiTableRow
            {
                Cells = [name, EmptyFallback(danger), EmptyFallback(details)]
            });
        }

        if (rows.Count == 0)
            return;

        blocks.Add(new UiTableBlock
        {
            Title = "Активные угрозы",
            Columns = ["Угроза", "Опасность", "Что известно"],
            Rows = rows
        });
    }

    private static void AddLocationEventBlock(List<UiBlock> blocks, JsonObject location)
    {
        var events = GetLocationNodeString(location, "lastEventsDescription", "lastEvent", "recentEvents");
        if (string.IsNullOrWhiteSpace(events))
            return;

        blocks.Add(Panel("Последние события", new UiTextBlock { Text = events, Tone = UiTone.Default }));
    }

    private static IEnumerable<string> EnumerateLocationTextEntries(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                var text = FormatLocationTextEntry(item);
                if (!string.IsNullOrWhiteSpace(text))
                    yield return text;
            }
        }
        else
        {
            var text = FormatLocationTextEntry(node);
            if (!string.IsNullOrWhiteSpace(text))
                yield return text;
        }
    }

    private static string FormatLocationTextEntry(JsonNode? node)
    {
        if (TryGetScalarString(node, out var scalar))
            return scalar;

        if (node is JsonObject obj)
            return FirstNonEmpty(
                GetLocationNodeString(obj, "name", "title"),
                GetLocationNodeString(obj, "description", "summary"));

        return string.Empty;
    }

    private static void AddLocationFact(List<UiKeyValueItem> items, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            items.Add(new UiKeyValueItem { Key = key, Value = value.Trim() });
    }

    private static string DescribeLocationDifficulty(JsonObject location)
    {
        var simple = GetLocationNodeString(location, "difficulty", "danger", "dangerLevel");
        if (!string.IsNullOrWhiteSpace(simple))
            return DescribeThreatDanger(simple);

        var profile = location["externalDifficultyProfile"] as JsonObject ??
                      location["internalDifficultyProfile"] as JsonObject;
        if (profile == null)
            return string.Empty;

        var parts = profile
            .Select(property =>
            {
                var value = FormatLocationTextEntry(property.Value);
                return string.IsNullOrWhiteSpace(value)
                    ? string.Empty
                    : $"{StructuredBonusDisplay.FieldLabel(property.Key)}: {value}";
            })
            .Where(static part => !string.IsNullOrWhiteSpace(part));
        return string.Join("; ", parts);
    }

    private static string DescribeWeatherTendency(string tendency) =>
        tendency.Trim().ToUpperInvariant() switch
        {
            "" or "NO_CHANGE" => string.Empty,
            "IMPROVE" => "Улучшение ↑",
            "WORSEN" => "Ухудшение ↓",
            var value when value.StartsWith("JUMP_TO_", StringComparison.Ordinal) => "Переход к: " + value["JUMP_TO_".Length..].Trim(),
            _ => tendency.Trim()
        };

    private static string DescribeThreatDanger(string danger) =>
        danger.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "low" => "низкая",
            "medium" or "moderate" => "средняя",
            "high" => "высокая",
            "critical" or "extreme" => "критическая",
            _ => danger.Trim()
        };

    private static string FormatThreatIntensity(string intensity) =>
        string.IsNullOrWhiteSpace(intensity) ? string.Empty : $"сила {intensity.Trim()}";

    private static string FormatMinutes(string minutes) =>
        string.IsNullOrWhiteSpace(minutes) ? string.Empty : $"{minutes.Trim()} мин.";

    private static string FormatPercent(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "не указано";

        return value.Trim().EndsWith('%') ? value.Trim() : value.Trim() + "%";
    }

    private static void AddReadWarning(List<UiBlock> blocks, string title, JsonReadResult read)
    {
        if (read.FileExists && read.Node == null && !string.IsNullOrWhiteSpace(read.Error))
            blocks.Add(Message(UiNotificationSeverity.Warning, title, "Одна из записей найдена, но её не удалось прочитать как JSON."));
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
        if (string.Equals(definition.DetailTitlePrefix, "Фракция", StringComparison.OrdinalIgnoreCase))
            return BuildFactionReferenceDetailPanel(definition, entry);

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
        AddReferenceDetailItem(detailItems, "Масштабирование", FormatReferenceCharacteristic(FirstReferenceNodeString(entry.Node, "scalingCharacteristic")));
        AddReferenceDetailItem(detailItems, "Награда", DescribeNodeForReferenceDetail(entry.Node["rewardInfo"] ?? entry.Node["rewards"] ?? entry.Node["reward"]));
        AddReferenceDetailItem(detailItems, "Подробности", DescribeReferencePayload(entry.Node));

        var detailBlocks = new List<UiBlock> { new UiKeyValueGridBlock { Items = detailItems } };
        AddStructuredBonusBlock(detailBlocks, entry.Node["structuredBonuses"] as JsonArray);

        return new UiPanelBlock
        {
            Title = $"{definition.DetailTitlePrefix}: {entry.Title}",
            Blocks = detailBlocks
        };
    }

    private static UiPanelBlock BuildFactionReferenceDetailPanel(
        ReferenceCommandDefinition definition,
        ReferenceEntrySnapshot entry)
    {
        var detailItems = new List<UiKeyValueItem>
        {
            new() { Key = "Раздел", Value = entry.Section },
            new() { Key = "Метка", Value = entry.Selector }
        };

        AddReferenceDetailItem(detailItems, "Описание", FirstNonEmpty(FirstReferenceNodeString(entry.Node, "description", "summary"), entry.Summary));
        AddReferenceDetailItem(detailItems, "Состояние", DescribeReferenceStatus(FirstReferenceNodeString(entry.Node, "status", "state", "phase")));
        AddReferenceDetailItem(detailItems, "Уровень", FirstReferenceNodeString(entry.Node, "level", "tier"));
        AddReferenceDetailItem(detailItems, "Репутация", DescribeFactionReputation(entry.Node));
        AddReferenceDetailItem(detailItems, "Отношение", FirstReferenceNodeString(entry.Node, "reputationDescription", "attitude", "publicStatus"));
        AddReferenceDetailItem(detailItems, "Ранг героя", FirstReferenceNodeString(entry.Node, "playerRank", "rankName"));
        AddReferenceDetailItem(detailItems, "Ветвь героя", FirstReferenceNodeString(entry.Node, "playerBranch", "branch"));
        AddReferenceDetailItem(detailItems, "Архетип развития", TranslateFactionDevelopmentArchetype(FirstReferenceNodeString(entry.Node, "developmentArchetype")));
        AddReferenceDetailItem(detailItems, "Сила фракции", FirstReferenceNodeString(entry.Node, "factionStrength", "strength", "power"));
        AddReferenceDetailItem(detailItems, "Цель", FirstReferenceNodeString(entry.Node, "currentObjective", "objective", "strategy"));

        var blocks = new List<UiBlock> { new UiKeyValueGridBlock { Items = detailItems } };

        var powerRows = BuildFactionPowerRows(entry.Node["powerProfile"]);
        if (powerRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Профиль силы",
                Columns = ["Параметр", "Значение"],
                Rows = powerRows
            });
        }

        var resourceRows = BuildFactionResourceRows(entry.Node["metaResources"] ?? entry.Node["resources"] ?? entry.Node["strategicGoods"]);
        if (resourceRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Ресурсы",
                Columns = ["Ресурс", "Запас", "Доход/ход", "Содержание/ход"],
                Rows = resourceRows
            });
        }

        var rankRows = BuildFactionRankRows(entry.Node["ranks"] ?? entry.Node["rankLadder"]);
        if (rankRows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Ранги и доступ",
                Columns = ["Ранг", "Ветвь", "Преимущества"],
                Rows = rankRows
            });
        }

        return new UiPanelBlock
        {
            Title = $"{definition.DetailTitlePrefix}: {entry.Title}",
            Blocks = blocks
        };
    }

    private static string DescribeFactionReputation(JsonObject node)
    {
        var value = FirstReferenceNodeString(node, "reputation", "standing");
        var label = FirstReferenceNodeString(node, "reputationName", "reputationTier");
        return JoinReferenceDetails(value, label);
    }

    private static List<UiTableRow> BuildFactionPowerRows(JsonNode? node)
    {
        if (node is not JsonObject obj)
            return [];

        var rows = new List<UiTableRow>();
        foreach (var property in obj)
        {
            if (!TryGetScalarString(property.Value, out var value) || string.IsNullOrWhiteSpace(value))
                continue;

            rows.Add(new UiTableRow
            {
                Cells = [TranslateFactionPowerKey(property.Key), value]
            });
        }

        return rows;
    }

    private static List<UiTableRow> BuildFactionResourceRows(JsonNode? node)
    {
        if (node is null)
            return [];

        var rows = new List<UiTableRow>();
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                if (property.Value is JsonObject resource)
                    AddFactionResourceRow(rows, property.Key, resource);
                else if (TryGetScalarString(property.Value, out var value) && !string.IsNullOrWhiteSpace(value))
                    rows.Add(new UiTableRow { Cells = [TranslateFactionResourceKey(property.Key), value, string.Empty, string.Empty] });
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var resource in array.OfType<JsonObject>())
                AddFactionResourceRow(rows, FirstReferenceNodeString(resource, "resourceType", "resourceId", "key"), resource);
        }

        return rows;
    }

    private static void AddFactionResourceRow(List<UiTableRow> rows, string key, JsonObject resource)
    {
        var name = FirstNonEmpty(
            FirstReferenceNodeString(resource, "displayName", "name", "resourceName"),
            TranslateFactionResourceKey(key));
        var stock = FirstReferenceNodeString(resource, "currentStock", "stock", "amount", "balanceAfter", "quantity");
        var income = FirstReferenceNodeString(resource, "incomePerTurn", "income", "delta");
        var upkeep = FirstReferenceNodeString(resource, "upkeepPerTurn", "upkeep", "costPerTurn");

        if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(stock) && string.IsNullOrWhiteSpace(income) && string.IsNullOrWhiteSpace(upkeep))
            return;

        rows.Add(new UiTableRow
        {
            Cells =
            [
                EmptyFallback(name),
                EmptyFallback(stock),
                EmptyFallback(income),
                EmptyFallback(upkeep)
            ]
        });
    }

    private static List<UiTableRow> BuildFactionRankRows(JsonNode? node)
    {
        if (node is not JsonArray array)
            return [];

        var rows = new List<UiTableRow>();
        foreach (var rank in array.OfType<JsonObject>())
        {
            var name = FirstReferenceNodeString(rank, "rankName", "name", "title");
            var branch = FirstReferenceNodeString(rank, "branch", "branchName");
            var benefits = DescribeNodeForReferenceDetail(rank["benefits"] ?? rank["perks"] ?? rank["permissions"]);
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(branch) && string.IsNullOrWhiteSpace(benefits))
                continue;

            rows.Add(new UiTableRow
            {
                Cells =
                [
                    EmptyFallback(name),
                    EmptyFallback(branch),
                    EmptyFallback(benefits)
                ]
            });
        }

        return rows;
    }

    private static string TranslateFactionDevelopmentArchetype(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "economic" => "экономическое развитие",
            "military" => "военное развитие",
            "social" => "социальное влияние",
            "covert" => "скрытое влияние",
            "arcane" or "arcane_tech" => "магическое развитие",
            "exploration" => "исследования",
            _ => value.Trim()
        };

    private static string TranslateFactionPowerKey(string key) =>
        key switch
        {
            "military" => "Военная сила",
            "economic" => "Экономика",
            "social" => "Социальное влияние",
            "covert" => "Скрытые операции",
            "logistics" => "Логистика",
            "stability" => "Устойчивость",
            "arcane" or "arcaneTech" or "arcane_tech" => "Магия и техника",
            "exploration" => "Разведка",
            _ => HumanizeReferenceKey(key)
        };

    private static string TranslateFactionResourceKey(string key) =>
        key switch
        {
            "coins" => "Монеты",
            "gold" => "Золото",
            "influence" => "Влияние",
            "reputation" => "Репутация",
            "lightSparks" => "Искры Света",
            "supplies" => "Припасы",
            "manpower" => "Люди",
            "arcaneDust" => "Магическая пыль",
            _ => HumanizeReferenceKey(key)
        };

    private static string HumanizeReferenceKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var parts = new List<char>();
        var previousWasSeparator = true;
        foreach (var ch in key.Trim())
        {
            if (ch is '_' or '-' or '.')
            {
                if (!previousWasSeparator)
                    parts.Add(' ');
                previousWasSeparator = true;
                continue;
            }

            if (char.IsUpper(ch) && parts.Count > 0 && !previousWasSeparator)
                parts.Add(' ');

            parts.Add(parts.Count == 0 ? char.ToUpperInvariant(ch) : ch);
            previousWasSeparator = false;
        }

        return new string(parts.ToArray()).Trim();
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

            var value = DescribeNodeForReferenceDetail(property.Value, property.Key);
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{DescribeReferenceFieldLabel(property.Key)}: {value}");
        }

        return string.Join("; ", parts);
    }

    private static string DescribeNodeForReferenceDetail(JsonNode? node, string? fieldName = null)
    {
        if (node == null)
            return string.Empty;

        if (TryGetScalarString(node, out var scalar))
            return StructuredBonusDisplay.FormatScalar(scalar, fieldName);

        if (node is JsonArray array)
            return string.Join("; ", array
                .Select(item => DescribeNodeForReferenceDetail(item, fieldName))
                .Where(static part => !string.IsNullOrWhiteSpace(part)));

        if (node is JsonObject obj)
        {
            var parts = new List<string>();
            foreach (var property in obj)
            {
                if (IsTechnicalReferenceProperty(property.Key))
                    continue;

                var value = DescribeNodeForReferenceDetail(property.Value, property.Key);
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add($"{DescribeReferenceNestedFieldLabel(property.Key)}: {value}");
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
            "rewardInfo" or "rewards" or "reward" or "structuredBonuses" or "playerStatBonus" or
            "scalingCharacteristic";

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

    private static string DescribeReferenceNestedFieldLabel(string propertyName)
    {
        var referenceLabel = DescribeReferenceFieldLabel(propertyName);
        return string.Equals(referenceLabel, "деталь", StringComparison.OrdinalIgnoreCase)
            ? StructuredBonusDisplay.FieldLabel(propertyName)
            : referenceLabel;
    }

    private static string FormatReferenceCharacteristic(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : StructuredBonusDisplay.FormatCharacteristicName(value);

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

        var label = ExplorerPlayerFacingLabels.LocationLinkState(linkState);
        return string.IsNullOrWhiteSpace(label) ? string.Empty : $"состояние пути: {label}";
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
        var commandToken = ExplorerCommandCatalog.ExtractCommandToken(command.Trim());
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

        var detailRequest = ParseInventoryDetailRequest(ExtractCommandRemainder(command));
        if (detailRequest.Kind == InventoryDetailKind.Unknown)
        {
            return Completed(command, [
                Message(UiNotificationSeverity.Warning, "Инвентарь", "Для подробностей используйте команду вида /инв предмет <идентификатор>.")
            ], BuildInventoryBackActions(commandToken));
        }

        if (detailRequest.Kind == InventoryDetailKind.Item)
        {
            var selected = FindInventoryItemNode(root, detailRequest.Selector);
            if (selected == null)
            {
                return Completed(command, [
                    Message(UiNotificationSeverity.Warning, "Инвентарь", "Такой предмет не найден в инвентаре.")
                ], BuildInventoryBackActions(commandToken));
            }

            var sidecars = await ReadInventoryItemSidecarsAsync(fs, selected);
            return Completed(command, BuildInventoryItemDetailBlocks(selected, sidecars), BuildInventoryBackActions(commandToken));
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

        return Completed(command, blocks, BuildInventoryActions(commandToken, inventoryContext));
    }

    private static IReadOnlyList<UiAction> BuildInventoryActions(string commandToken, InventoryEquipmentContext? inventory)
    {
        if (inventory == null)
            return [];

        var actions = new List<UiAction>();
        foreach (var item in inventory.Items)
        {
            var identity = FirstNonEmpty(item.Identity, item.Name);
            actions.Add(new UiAction
            {
                Id = InventoryEquipmentService.BuildActionId("inventory-detail", identity),
                Label = $"Подробнее: «{item.Name}»",
                Command = BuildInventoryItemDetailCommand(commandToken, identity),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["itemIdentity"] = item.Identity,
                    ["itemName"] = item.Name,
                    ["itemType"] = item.Type,
                    ["slot"] = item.ResolvedSlot
                }
            });
        }

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

    private static IReadOnlyList<UiAction> BuildInventoryBackActions(string commandToken) =>
    [
        new UiAction
        {
            Id = "inventory-back",
            Label = "Назад к инвентарю",
            Command = commandToken,
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false
        }
    ];

    private static InventoryDetailRequest ParseInventoryDetailRequest(string remainder)
    {
        if (string.IsNullOrWhiteSpace(remainder))
            return new InventoryDetailRequest(InventoryDetailKind.Overview, string.Empty);

        var (kindToken, selector) = SplitFirstCombatArgument(remainder);
        if (string.IsNullOrWhiteSpace(selector) || !IsInventoryItemDetailToken(kindToken))
            return new InventoryDetailRequest(InventoryDetailKind.Unknown, string.Empty);

        return new InventoryDetailRequest(InventoryDetailKind.Item, NormalizeCombatSelector(selector));
    }

    private static bool IsInventoryItemDetailToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        return normalized is "item" or "предмет" or "вещь" or "detail" or "подробнее";
    }

    private static string BuildInventoryItemDetailCommand(string commandToken, string selector)
    {
        var detailToken = string.Equals(commandToken, "/inv", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(commandToken, "/inventory", StringComparison.OrdinalIgnoreCase)
            ? "item"
            : "предмет";
        return commandToken + " " + detailToken + " " + FormatCombatCommandArgument(selector);
    }

    private static JsonObject? FindInventoryItemNode(JsonObject root, string selector)
    {
        var items = GetInventoryItemsArrayNode(root);
        if (items == null)
            return null;

        var normalizedSelector = NormalizeInventoryLookup(selector);
        for (var index = 0; index < items.Count; index++)
        {
            if (items[index] is not JsonObject item)
                continue;

            if (string.Equals((index + 1).ToString(), normalizedSelector, StringComparison.OrdinalIgnoreCase))
                return item;

            var identities = new[]
            {
                GetInventoryItemIdentity(item),
                GetInventoryItemName(item),
                NormalizeReferenceSelector(GetInventoryItemName(item))
            };

            if (identities.Any(identity =>
                    !string.IsNullOrWhiteSpace(identity) &&
                    string.Equals(NormalizeInventoryLookup(identity), normalizedSelector, StringComparison.OrdinalIgnoreCase)))
            {
                return item;
            }
        }

        return null;
    }

    private static async Task<InventoryItemSidecars> ReadInventoryItemSidecarsAsync(FileSystemManager fs, JsonObject item)
    {
        var identity = GetInventoryItemIdentity(item);
        var name = GetInventoryItemName(item);
        var resources = await ReadJson(fs, "game_state/inventory/item_resources.json");
        var bonds = await ReadJson(fs, "game_state/inventory/item_bonds.json");
        var texts = await ReadJson(fs, "game_state/inventory/item_text_updates.json");
        var journals = await ReadJson(fs, "game_state/npcs/item_journals.json");

        return new InventoryItemSidecars(
            Resource: FindInventorySidecarEntryNode(resources.Node, identity, name, "entries", "inventoryItemsResources"),
            Bond: FindInventorySidecarEntryNode(bonds.Node, identity, name, "entries", "itemBondLevelChanges", "itemFateCardUnlocks"),
            Text: FindInventorySidecarEntryNode(texts.Node, identity, name, "entries", "updateItemTextContents"),
            Journal: FindInventorySidecarEntryNode(journals.Node, identity, name, "entries", "itemJournals", "itemJournalUpdates"));
    }

    private static IReadOnlyList<UiBlock> BuildInventoryItemDetailBlocks(JsonObject item, InventoryItemSidecars sidecars)
    {
        var itemName = GetInventoryItemName(item);
        var blocks = new List<UiBlock>();
        var detailBlocks = new List<UiBlock>();
        var description = FirstNonEmpty(GetNodeString(item, "description"), GetNodeString(item, "lore"));
        if (!string.IsNullOrWhiteSpace(description))
            detailBlocks.Add(new UiTextBlock { Text = description, Tone = UiTone.Default });

        var facts = new List<UiKeyValueItem>();
        AddInventoryFact(facts, "Тип", FormatInventoryProtocolValue(GetNodeString(item, "type") ?? string.Empty));
        AddInventoryFact(facts, "Качество", FormatInventoryProtocolValue(FirstNonEmpty(GetNodeString(item, "quality"), GetNodeString(item, "rarity"))));
        AddInventoryFact(facts, "Вес", FormatInventoryMeasure(GetNodeString(item, "weight"), "кг"));
        AddInventoryFact(facts, "Цена", GetNodeString(item, "price"));
        AddInventoryFact(facts, "Прочность", FormatInventoryDurability(item));
        AddInventoryFact(facts, "Количество", FirstNonEmpty(GetNodeString(item, "count"), GetNodeString(item, "quantity")));
        AddInventoryFact(facts, "Слот", FormatInventorySlot(FirstNonEmpty(
            GetNodeString(item, "equipmentSlot"),
            GetNodeString(item, "slot"),
            GetNodeString(item, "equipSlot"))));
        AddInventoryFact(facts, "Аксессуар для", GetNodeString(item, "accessoryForSlot"));
        AddInventoryFact(facts, "Группа", GetNodeString(item, "group"));
        if (TryGetNodeBool(item, "requiresTwoHands", out var requiresTwoHands) && requiresTwoHands)
            facts.Add(new UiKeyValueItem { Key = "Хват", Value = "двуручное" });
        if (TryGetNodeBool(item, "isBroken", out var isBroken) && isBroken)
            facts.Add(new UiKeyValueItem { Key = "Состояние", Value = "сломано" });
        if (TryGetNodeBool(item, "isSentient", out var isSentient) && isSentient)
            facts.Add(new UiKeyValueItem { Key = "Разумность", Value = "разумный предмет" });
        if (facts.Count > 0)
            detailBlocks.Add(new UiKeyValueGridBlock { Items = facts });

        AddInventorySummaryList(detailBlocks, "Бонусы", item["bonuses"]);
        AddInventorySummaryList(detailBlocks, "Эффекты", item["effects"]);
        AddInventorySummaryList(detailBlocks, "Особые свойства", item["specialProperties"]);
        AddStructuredBonusBlock(detailBlocks, item["structuredBonuses"] as JsonArray);
        AddInventoryCombatEffectBlock(detailBlocks, item["combatEffect"]);
        AddInventoryCustomPropertiesBlock(detailBlocks, item["customProperties"]);
        AddInventoryContainerBlock(detailBlocks, item);
        AddInventoryDisassemblyBlock(detailBlocks, item["disassembleTo"] as JsonArray);
        AddInventoryResourceBlock(detailBlocks, item, sidecars.Resource);
        AddInventoryBondBlock(detailBlocks, sidecars.Bond);
        AddInventoryContentBlock(detailBlocks, item["textContent"], sidecars.Text?["textContent"]);
        AddInventoryJournalBlock(detailBlocks, item["journalEntries"], sidecars.Journal?["journalEntries"]);

        if (detailBlocks.Count == 0)
            detailBlocks.Add(new UiTextBlock { Text = "Подробная информация по предмету пока не заполнена.", Tone = UiTone.Muted });

        blocks.Add(new UiPanelBlock
        {
            Title = $"Предмет: {itemName}",
            Blocks = detailBlocks
        });
        blocks.Add(new UiTextBlock { Text = "Вернуться к списку можно командой /инв.", Tone = UiTone.Muted });
        return blocks;
    }

    private static void AddInventoryFact(List<UiKeyValueItem> items, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && value.Trim() != "1")
            items.Add(new UiKeyValueItem { Key = key, Value = value.Trim() });
    }

    private static void AddInventorySummaryList(List<UiBlock> blocks, string title, JsonNode? node)
    {
        var items = EnumerateInventorySummaryTexts(node).ToList();
        if (items.Count == 0)
            return;

        blocks.Add(new UiPanelBlock
        {
            Title = title,
            Blocks =
            [
                new UiListBlock
                {
                    Items = items,
                    Ordered = false
                }
            ]
        });
    }

    private static IEnumerable<string> EnumerateInventorySummaryTexts(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                var text = DescribeInventorySummaryNode(item);
                if (!string.IsNullOrWhiteSpace(text))
                    yield return text;
            }
        }
        else
        {
            var text = DescribeInventorySummaryNode(node);
            if (!string.IsNullOrWhiteSpace(text))
                yield return text;
        }
    }

    private static string DescribeInventorySummaryNode(JsonNode? node)
    {
        if (TryGetScalarString(node, out var scalar))
            return FormatInventoryProtocolValue(scalar);

        if (node is not JsonObject obj)
            return string.Empty;

        return FirstNonEmpty(
            GetNodeString(obj, "effectDescription"),
            GetNodeString(obj, "description"),
            GetNodeString(obj, "summary"),
            GetNodeString(obj, "name"),
            GetNodeString(obj, "effect"),
            GetNodeString(obj, "stat"));
    }

    private static void AddStructuredBonusBlock(List<UiBlock> blocks, JsonArray? bonuses)
    {
        if (bonuses == null || bonuses.Count == 0)
            return;

        var rows = new List<UiTableRow>();
        var index = 0;
        foreach (var bonus in bonuses)
        {
            index++;
            if (bonus is not JsonObject obj)
            {
                rows.Add(new UiTableRow
                {
                    Cells = [$"Бонус {index}", "Значение", FormatInventoryNodeValue(bonus)]
                });
                continue;
            }

            var title = FirstNonEmpty(GetNodeString(obj, "summary"), $"Бонус {index}");
            foreach (var property in obj)
            {
                rows.Add(new UiTableRow
                {
                    Cells = [title, GetStructuredBonusFieldLabel(property.Key), FormatInventoryNodeValue(property.Value, property.Key)]
                });
            }
        }

        if (rows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Структурные бонусы",
                Columns = ["Бонус", "Поле", "Значение"],
                Rows = rows
            });
        }
    }

    private static void AddInventoryCombatEffectBlock(List<UiBlock> blocks, JsonNode? node)
    {
        var rows = new List<UiTableRow>();
        AddInventoryCombatEffectRows(rows, node, "Эффект");
        if (rows.Count == 0)
            return;

        blocks.Add(new UiTableBlock
        {
            Title = "Боевые эффекты",
            Columns = ["Источник", "Тип", "Значение", "Описание"],
            Rows = rows
        });
    }

    private static void AddInventoryCombatEffectRows(List<UiTableRow> rows, JsonNode? node, string source)
    {
        if (node is JsonArray array)
        {
            foreach (var child in array)
                AddInventoryCombatEffectRows(rows, child, source);
            return;
        }

        if (node is not JsonObject obj)
            return;

        var actionName = FirstNonEmpty(GetNodeString(obj, "actionName"), source);
        if (obj["effects"] is JsonArray nestedEffects)
        {
            var actionDetails = BuildInventoryCombatActionDetails(obj);
            if (!string.IsNullOrWhiteSpace(actionDetails))
            {
                rows.Add(new UiTableRow
                {
                    Cells = [actionName, "Действие", "", actionDetails]
                });
            }

            foreach (var effect in nestedEffects)
                AddInventoryCombatEffectRows(rows, effect, actionName);
            return;
        }

        rows.Add(new UiTableRow
        {
            Cells =
            [
                actionName,
                FormatInventoryProtocolValue(FirstNonEmpty(GetNodeString(obj, "effectType"), GetNodeString(obj, "type"), "Эффект")),
                FormatInventoryCombatEffectValue(obj),
                FirstNonEmpty(GetNodeString(obj, "effectDescription"), GetNodeString(obj, "description"))
            ]
        });
    }

    private static string BuildInventoryCombatActionDetails(JsonObject action)
    {
        var parts = new List<string>();
        if (TryGetNodeBool(action, "isActivatedEffect", out var activated))
            parts.Add(activated ? "активируемый" : "пассивный");
        var cost = GetNodeString(action, "actionCost");
        if (!string.IsNullOrWhiteSpace(cost))
            parts.Add("стоимость: " + FormatInventoryProtocolValue(cost));
        return string.Join("; ", parts);
    }

    private static string FormatInventoryCombatEffectValue(JsonObject effect)
    {
        var parts = new List<string>();
        var value = GetNodeString(effect, "value");
        if (!string.IsNullOrWhiteSpace(value) && value != "0")
            parts.Add(value);
        var poise = GetNodeString(effect, "poiseDamage");
        if (!string.IsNullOrWhiteSpace(poise) && poise != "0")
            parts.Add($"равновесие -{poise}");
        var target = FirstNonEmpty(GetNodeString(effect, "targetTypeDisplayName"), GetNodeString(effect, "targetType"));
        if (!string.IsNullOrWhiteSpace(target))
            parts.Add("цель: " + FormatInventoryProtocolValue(target));
        var duration = GetNodeString(effect, "duration");
        if (!string.IsNullOrWhiteSpace(duration) && duration != "0")
            parts.Add("длительность: " + duration);
        return string.Join("; ", parts);
    }

    private static void AddInventoryCustomPropertiesBlock(List<UiBlock> blocks, JsonNode? node)
    {
        if (node is not JsonArray array || array.Count == 0)
            return;

        var rows = array
            .OfType<JsonObject>()
            .Select(static item => new UiTableRow
            {
                Cells =
                [
                    FormatInventoryProtocolValue(FirstNonEmpty(GetNodeString(item, "interactionType"), "Эффект")),
                    FirstNonEmpty(GetNodeString(item, "targetStateName"), GetNodeString(item, "target")),
                    GetNodeString(item, "changeValue") ?? string.Empty,
                    FirstNonEmpty(GetNodeString(item, "description"), GetNodeString(item, "summary"))
                ]
            })
            .Where(static row => row.Cells.Any(static cell => !string.IsNullOrWhiteSpace(cell)))
            .ToList();

        if (rows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Особые свойства",
                Columns = ["Когда", "Цель", "Изменение", "Описание"],
                Rows = rows
            });
        }
    }

    private static void AddInventoryContainerBlock(List<UiBlock> blocks, JsonObject item)
    {
        if (!TryGetNodeBool(item, "isContainer", out var isContainer) || !isContainer)
            return;

        var facts = new List<UiKeyValueItem>();
        AddInventoryFact(facts, "Вместимость", GetNodeString(item, "capacity"));
        AddInventoryFact(facts, "Объём", FormatInventoryMeasure(GetNodeString(item, "volume"), "дм³"));
        AddInventoryFact(facts, "Вес пустого", FormatInventoryMeasure(GetNodeString(item, "containerWeight"), "кг"));
        AddInventoryFact(facts, "Снижение веса", FormatInventoryMeasure(GetNodeString(item, "weightReduction"), "%"));
        if (facts.Count > 0)
            blocks.Add(Panel("Контейнер", new UiKeyValueGridBlock { Items = facts }));
    }

    private static void AddInventoryDisassemblyBlock(List<UiBlock> blocks, JsonArray? materials)
    {
        if (materials == null || materials.Count == 0)
            return;

        var rows = new List<UiTableRow>();
        foreach (var material in materials)
        {
            if (TryGetScalarString(material, out var text))
            {
                rows.Add(new UiTableRow { Cells = [text, "", "", ""] });
                continue;
            }

            if (material is not JsonObject obj)
                continue;

            rows.Add(new UiTableRow
            {
                Cells =
                [
                    FirstNonEmpty(GetNodeString(obj, "materialName"), GetNodeString(obj, "name"), "Материал"),
                    FirstNonEmpty(GetNodeString(obj, "quantity"), "1"),
                    FormatInventoryMeasure(GetNodeString(obj, "weight"), "кг"),
                    FirstNonEmpty(GetNodeString(obj, "description"), GetNodeString(obj, "price"))
                ]
            });
        }

        if (rows.Count > 0)
        {
            blocks.Add(new UiTableBlock
            {
                Title = "Разбирается на",
                Columns = ["Материал", "Кол-во", "Вес", "Описание"],
                Rows = rows
            });
        }
    }

    private static void AddInventoryResourceBlock(List<UiBlock> blocks, JsonObject item, JsonObject? resourceEntry)
    {
        var resource = FirstNonEmpty(GetNodeString(resourceEntry, "resource"), GetNodeString(item, "resource"));
        if (string.IsNullOrWhiteSpace(resource))
            return;

        var max = FirstNonEmpty(GetNodeString(resourceEntry, "maximumResource"), GetNodeString(item, "maximumResource"));
        var resourceType = FirstNonEmpty(GetNodeString(resourceEntry, "resourceType"), GetNodeString(item, "resourceType"), "заряды");
        var value = string.IsNullOrWhiteSpace(max) ? resource : $"{resource} / {max}";
        blocks.Add(Panel("Ресурсы предмета", new UiKeyValueGridBlock
        {
            Items =
            [
                new UiKeyValueItem { Key = resourceType, Value = value }
            ]
        }));
    }

    private static void AddInventoryBondBlock(List<UiBlock> blocks, JsonObject? bondEntry)
    {
        if (bondEntry == null)
            return;

        var panelBlocks = new List<UiBlock>();
        var bondLevel = GetNodeString(bondEntry, "ownerBondLevelCurrent");
        if (!string.IsNullOrWhiteSpace(bondLevel))
        {
            panelBlocks.Add(new UiKeyValueGridBlock
            {
                Items =
                [
                    new UiKeyValueItem { Key = "Уровень", Value = $"{bondLevel}/100" }
                ]
            });
        }

        var reason = GetNodeString(bondEntry, "lastBondChangeReason");
        if (!string.IsNullOrWhiteSpace(reason))
            panelBlocks.Add(new UiTextBlock { Text = reason, Tone = UiTone.Muted });

        if (bondEntry["fateCards"] is JsonArray fateCards && fateCards.Count > 0)
        {
            panelBlocks.Add(new UiTableBlock
            {
                Title = "Карты судьбы предмета",
                Columns = ["Карта", "Статус", "Описание"],
                Rows = fateCards
                    .OfType<JsonObject>()
                    .Select(static card => new UiTableRow
                    {
                        Cells =
                        [
                            FirstNonEmpty(GetNodeString(card, "name"), GetNodeString(card, "cardName"), "Карта"),
                            TryGetNodeBool(card, "isUnlocked", out var unlocked) && unlocked ? "разблокирована" : "закрыта",
                            FirstNonEmpty(GetNodeString(card, "description"), GetNodeString(card, "summary"))
                        ]
                    })
                    .ToList()
            });
        }

        if (panelBlocks.Count > 0)
            blocks.Add(new UiPanelBlock { Title = "Связь с владельцем", Blocks = panelBlocks });
    }

    private static void AddInventoryContentBlock(List<UiBlock> blocks, JsonNode? embedded, JsonNode? sidecar)
    {
        var entries = CollectInventoryTextEntries(embedded)
            .Concat(CollectInventoryTextEntries(sidecar))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (entries.Count == 0)
            return;

        blocks.Add(new UiPanelBlock
        {
            Title = "Содержимое",
            Blocks = entries.Select(static entry => (UiBlock)new UiTextBlock { Text = entry, Tone = UiTone.Default }).ToList()
        });
    }

    private static void AddInventoryJournalBlock(List<UiBlock> blocks, JsonNode? embedded, JsonNode? sidecar)
    {
        var entries = CollectInventoryJournalEntries(embedded)
            .Concat(CollectInventoryJournalEntries(sidecar))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (entries.Count == 0)
            return;

        blocks.Add(new UiPanelBlock
        {
            Title = "Записи",
            Blocks = entries.Select(static entry => (UiBlock)new UiTextBlock { Text = entry, Tone = UiTone.Muted }).ToList()
        });
    }

    private static IEnumerable<string> CollectInventoryTextEntries(JsonNode? node)
    {
        if (node is not JsonArray array)
            yield break;

        foreach (var entry in array)
        {
            var text = FormatInventoryNodeValue(entry);
            if (!string.IsNullOrWhiteSpace(text))
                yield return text;
        }
    }

    private static IEnumerable<string> CollectInventoryJournalEntries(JsonNode? node)
    {
        if (node is not JsonArray array)
            yield break;

        foreach (var entry in array)
        {
            if (TryGetScalarString(entry, out var scalar))
            {
                if (!string.IsNullOrWhiteSpace(scalar))
                    yield return scalar;
                continue;
            }

            if (entry is not JsonObject obj)
                continue;

            var main = FirstNonEmpty(
                JoinReferenceDetails(GetNodeString(obj, "timestamp"), GetNodeString(obj, "event")),
                GetNodeString(obj, "event"),
                "Запись");
            var body = FirstNonEmpty(
                GetNodeString(obj, "description"),
                GetNodeString(obj, "text"),
                GetNodeString(obj, "content"),
                GetNodeString(obj, "entry"));
            var line = !string.IsNullOrWhiteSpace(body) ? $"{main}: {body}" : main;
            var voice = GetNodeString(obj, "spiritVoice");
            if (!string.IsNullOrWhiteSpace(voice))
                line += $" Голос: {voice}";
            yield return line;
        }
    }

    private static JsonArray? GetInventoryItemsArrayNode(JsonObject root)
    {
        if (root["items"] is JsonArray items)
            return items;

        if (root["UpdateInventory"] is JsonArray updateInventory)
            return updateInventory;

        return null;
    }

    private static JsonObject? FindInventorySidecarEntryNode(
        JsonNode? root,
        string itemIdentity,
        string itemName,
        params string[] propertyNames)
    {
        foreach (var item in EnumerateInventorySidecarEntryNodes(root, propertyNames))
        {
            if (InventoryNodeMatches(item, itemIdentity, itemName))
                return item;
        }

        return null;
    }

    private static IEnumerable<JsonObject> EnumerateInventorySidecarEntryNodes(JsonNode? root, params string[] propertyNames)
    {
        if (root is JsonArray array)
        {
            foreach (var item in array.OfType<JsonObject>())
                yield return item;
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var propertyName in propertyNames)
        {
            if (obj[propertyName] is not JsonArray items)
                continue;

            foreach (var item in items.OfType<JsonObject>())
                yield return item;
        }
    }

    private static bool InventoryNodeMatches(JsonObject item, string itemIdentity, string itemName)
    {
        var identity = GetInventoryItemIdentity(item);
        if (!string.IsNullOrWhiteSpace(itemIdentity) &&
            string.Equals(identity, itemIdentity, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var name = GetInventoryItemName(item);
        return !string.IsNullOrWhiteSpace(itemName) &&
               string.Equals(name, itemName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetInventoryItemIdentity(JsonNode? item)
    {
        return FirstNonEmpty(
            GetNodeString(item, "existedId"),
            GetNodeString(item, "itemId"),
            GetNodeString(item, "id"),
            GetNodeString(item, "relicId"));
    }

    private static string GetInventoryItemName(JsonNode? item) =>
        FirstNonEmpty(GetNodeString(item, "name"), GetNodeString(item, "itemName"), "Безымянный предмет");

    private static string FormatInventoryDurability(JsonObject item)
    {
        var durability = GetNodeString(item, "durability");
        var maxDurability = GetNodeString(item, "maxDurability");
        if (string.IsNullOrWhiteSpace(durability))
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(maxDurability))
            return $"{durability}/{maxDurability}";

        return durability;
    }

    private static string FormatInventoryMeasure(string? value, string unit)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Trim().EndsWith(unit, StringComparison.OrdinalIgnoreCase)
            ? value.Trim()
            : $"{value.Trim()} {unit}";
    }

    private static string FormatInventorySlot(string? slot)
    {
        if (string.IsNullOrWhiteSpace(slot))
            return string.Empty;

        return FormatSlotName(slot);
    }

    private static string FormatInventoryNodeValue(JsonNode? node, string? fieldName = null)
    {
        if (TryGetScalarString(node, out var scalar))
            return StructuredBonusDisplay.FormatScalar(scalar, fieldName);

        if (node is JsonArray array)
        {
            return string.Join("; ", array
                .Select(item => FormatInventoryNodeValue(item, fieldName))
                .Where(static value => !string.IsNullOrWhiteSpace(value)));
        }

        if (node is JsonObject obj)
        {
            return FirstNonEmpty(
                GetNodeString(obj, "summary"),
                GetNodeString(obj, "description"),
                GetNodeString(obj, "effectDescription"),
                GetNodeString(obj, "name"),
                string.Join("; ", obj
                    .Select(property => $"{GetStructuredBonusFieldLabel(property.Key)}: {FormatInventoryNodeValue(property.Value, property.Key)}")
                    .Where(static value => !string.IsNullOrWhiteSpace(value))));
        }

        return string.Empty;
    }

    private static string FormatInventoryProtocolValue(string value) =>
        StructuredBonusDisplay.FormatScalar(value);

    private static string GetStructuredBonusFieldLabel(string fieldName) =>
        StructuredBonusDisplay.FieldLabel(fieldName);

    private static string HumanizeInventoryFieldName(string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            return "Поле";

        var spaced = string.Concat(fieldName.Select((ch, index) =>
            index > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString()));
        return spaced.Replace('_', ' ').Trim();
    }

    private static string NormalizeInventoryLookup(string value) =>
        NormalizeReferenceSelector(NormalizeCombatSelector(value));

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

        if (jsonValue.TryGetValue<double>(out var doubleValue))
        {
            value = doubleValue.ToString("G", CultureInfo.InvariantCulture);
            return true;
        }

        if (jsonValue.TryGetValue<decimal>(out var decimalValue))
        {
            value = decimalValue.ToString("G", CultureInfo.InvariantCulture);
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

    private enum InventoryDetailKind
    {
        Overview,
        Item,
        Unknown
    }

    private sealed record InventoryDetailRequest(
        InventoryDetailKind Kind,
        string Selector);

    private sealed record InventoryItemSidecars(
        JsonObject? Resource,
        JsonObject? Bond,
        JsonObject? Text,
        JsonObject? Journal);

    private enum EffectDetailKind
    {
        Overview,
        Effect,
        Unknown
    }

    private sealed record EffectSnapshot(
        int Index,
        string Selector,
        string Section,
        string Name,
        JsonObject Node);

    private sealed record EffectDetailRequest(
        EffectDetailKind Kind,
        string Selector);

    private enum NpcSectionDetailKind
    {
        Overview,
        Section,
        Unknown
    }

    private sealed record NpcSectionDetailRequest(
        NpcSectionDetailKind Kind,
        string NpcSelector,
        string SectionSelector);

    private enum NpcJournalDetailKind
    {
        Overview,
        Journal,
        Unknown
    }

    private sealed record NpcJournalDetailRequest(
        NpcJournalDetailKind Kind,
        string NpcSelector);

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
