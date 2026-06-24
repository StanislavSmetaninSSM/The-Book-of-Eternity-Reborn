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
                new("game_state/quests/plot_outline.json", "plotOutline.entries|entries", "Сюжетных записей")
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

        await AddStatsBlockIfPresent(
            blocks,
            fs,
            "game_state/misc/characteristics.json",
            "Базовые характеристики",
            TranslateBaseCharacteristicKey);
        await AddStatsBlockIfPresent(
            blocks,
            fs,
            "game_state/player/computed_characteristics.json",
            "Расчётные показатели",
            TranslateComputedCharacteristicKey);
        return Completed(command, blocks);
    }

    private static async Task AddStatsBlockIfPresent(
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

        var structuredBlock = BuildStructuredStatsDossier(obj, title, translateKey);
        if (structuredBlock != null)
        {
            blocks.Add(structuredBlock);
            return;
        }
    }

    private static UiEntityDossierBlock? BuildStructuredStatsDossier(
        JsonObject obj,
        string title,
        Func<string, string> translateKey)
    {
        var scalarItems = new List<UiKeyValueItem>();
        var sections = new List<UiEntityDossierSection>();

        foreach (var property in EnumerateStatsProperties(obj).Where(static property => !IsTechnicalStatsProperty(property.Key)))
        {
            var label = translateKey(property.Key);
            switch (property.Value)
            {
                case JsonObject nested:
                {
                    var section = BuildStatsObjectSection(label, nested);
                    if (section != null)
                        sections.Add(section);

                    break;
                }
                case JsonArray array:
                {
                    var section = BuildStatsArraySection(label, array, property.Key);
                    if (section != null)
                        sections.Add(section);
                    break;
                }
                default:
                {
                    var value = FormatStatsValue(property.Value, property.Key);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        scalarItems.Add(new UiKeyValueItem
                        {
                            Key = label,
                            Value = value
                        });
                    }

                    break;
                }
            }
        }

        if (scalarItems.Count > 0)
        {
            sections.Insert(0, new UiEntityDossierSection
            {
                Id = "primary",
                Title = "Основные показатели",
                Summary = "Короткие числовые параметры без дополнительных условий.",
                Icon = "stats",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiKeyValueGridBlock { Items = scalarItems }]
            });
        }

        return sections.Count == 0
            ? null
            : new UiEntityDossierBlock
            {
                EntityType = "stats",
                Title = title,
                Subtitle = string.Equals(title, "Базовые характеристики", StringComparison.OrdinalIgnoreCase)
                    ? "Основа персонажа"
                    : "Расчётные значения персонажа",
                Summary = string.Equals(title, "Базовые характеристики", StringComparison.OrdinalIgnoreCase)
                    ? "Постоянная основа, от которой считаются проверки и производные параметры."
                    : "Итоговые параметры с учетом снаряжения, эффектов и временных модификаторов.",
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = string.Equals(title, "Базовые характеристики", StringComparison.OrdinalIgnoreCase)
                            ? "База"
                            : "Расчёт",
                        Tone = UiTone.Accent,
                        Icon = "stats"
                    }
                ],
                Sections = sections
            };
    }

    private static UiEntityDossierSection? BuildStatsObjectSection(string title, JsonObject obj)
    {
        var grid = BuildStatsObjectGrid(obj);
        if (grid == null)
            return null;

        return new UiEntityDossierSection
        {
            Id = StableId(title),
            Title = title,
            Summary = "Показатели этой группы разложены по отдельным полям.",
            Icon = "stats",
            Collapsible = true,
            InitiallyExpanded = true,
            Blocks = [grid]
        };
    }

    private static UiEntityDossierSection? BuildStatsArraySection(string title, JsonArray array, string fieldName)
    {
        var listItems = new List<string>();
        var sectionBlocks = new List<UiBlock>();
        var index = 0;

        foreach (var item in array)
        {
            index++;
            if (item is JsonObject obj)
            {
                var card = BuildStatsNestedDossier($"{title}: запись {index}", fieldName, obj);
                if (card != null)
                    sectionBlocks.Add(card);

                continue;
            }

            var value = FormatStatsValue(item, fieldName);
            if (!string.IsNullOrWhiteSpace(value))
                listItems.Add(value);
        }

        if (listItems.Count > 0)
            sectionBlocks.Insert(0, new UiListBlock { Items = listItems });

        return sectionBlocks.Count == 0
            ? null
            : new UiEntityDossierSection
            {
                Id = StableId(title),
                Title = title,
                Summary = "Каждая запись показана отдельно, чтобы не склеивать несколько полей в одну строку.",
                Icon = "stats",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = sectionBlocks
            };
    }

    private static UiEntityDossierBlock? BuildStatsNestedDossier(string title, string entityType, JsonObject obj)
    {
        var grid = BuildStatsObjectGrid(obj);
        if (grid == null)
            return null;

        return new UiEntityDossierBlock
        {
            EntityType = string.IsNullOrWhiteSpace(entityType) ? "stats-entry" : entityType,
            Title = title,
            Subtitle = "Запись",
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "fields",
                    Title = "Поля",
                    Summary = "Игровые данные записи без технических ключей.",
                    Icon = "stats",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = [grid]
                }
            ]
        };
    }

    private static UiKeyValueGridBlock? BuildStatsObjectGrid(JsonObject obj)
    {
        var items = obj
            .Where(static property => !IsTechnicalStatsProperty(property.Key))
            .Select(static property => new UiKeyValueItem
            {
                Key = TranslateComputedCharacteristicKey(property.Key),
                Value = FormatStatsValue(property.Value, property.Key)
            })
            .Where(static item => !string.IsNullOrWhiteSpace(item.Value))
            .ToList();

        return items.Count == 0
            ? null
            : new UiKeyValueGridBlock { Items = items };
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

    private static string FormatStatsValue(JsonNode? node, string? fieldName = null)
    {
        if (TryGetScalarString(node, out var scalar))
            return StructuredBonusDisplay.FormatScalar(scalar, fieldName);

        if (node is JsonArray array)
        {
            var values = array
                .Select(item => FormatStatsValue(item, fieldName))
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            return values.Count == 0 ? $"{array.Count} записей" : string.Join("\n", values);
        }

        if (node is JsonObject obj)
        {
            var values = obj
                .Where(static property => !IsTechnicalStatsProperty(property.Key))
                .Select(static property => $"{TranslateComputedCharacteristicKey(property.Key)}: {FormatStatsValue(property.Value, property.Key)}")
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList();
            return values.Count == 0 ? string.Empty : string.Join("\n", values);
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
            "base" => "Базовое значение",
            "standard" or "standardCharacteristics" => "Стандартное значение",
            "permanent" or "permanentCharacteristics" or "permanentlyModified" => "Постоянные модификаторы",
            "equipmentBonus" or "equipmentBonuses" => "Бонусы снаряжения",
            "temporaryModifier" or "temporaryModifiers" => "Временные модификаторы",
            "final" or "computed" or "fullyModified" => "Итоговое значение",
            "source" => "Источник",
            "target" => "Цель",
            "value" => "Значение",
            "expiresAt" or "expires" or "duration" => "Действует до",
            "magicFlowSense" => "Чувство магических потоков",
            "arcaneLore" => "Аркановедение",
            "stealth" => "Скрытность",
            "aristocraticReputation" => "Аристократическая репутация",
            "strength" or "dexterity" or "agility" or "endurance" or "constitution" or
                "perception" or "intelligence" or "wisdom" or "willpower" or "will" or
                "charisma" or "luck" or "arcana" or "faith" or "spirit" or
                "attractiveness" or "trade" or "persuasion" => TranslateBaseCharacteristicKey(key),
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

    private static string StableId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "section";

        var chars = value
            .Trim()
            .Select(static ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-')
            .ToArray();
        var id = new string(chars).Trim('-');
        return string.IsNullOrWhiteSpace(id) ? "section" : id;
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
            BuildMapSummaryDossier(map)
        };

        return Completed(command, blocks);
    }

    private static UiEntityDossierBlock BuildMapSummaryDossier(MapViewDto map)
    {
        var currentNode = map.Nodes.FirstOrDefault(node => node.IsCurrent)
            ?? map.Nodes.FirstOrDefault(node => string.Equals(node.Id, map.CurrentNodeId, StringComparison.OrdinalIgnoreCase));
        var knownLocations = map.Nodes.Count(static node => !node.IsPlaceholder);
        var plannedExits = map.Nodes.Count(static node => node.IsPlaceholder);
        var facts = new List<UiKeyValueItem>
        {
            new() { Key = "Царство", Value = ExplorerPlayerFacingLabels.Realm(map.Realm) },
            new() { Key = "Текущая точка", Value = FirstNonEmpty(currentNode?.Label, ExplorerPlayerFacingLabels.CurrentMapNode(map)) },
            new() { Key = "Созданные локации", Value = knownLocations.ToString() },
            new() { Key = "Намеченные выходы", Value = plannedExits.ToString() },
            new() { Key = "Переходы", Value = map.Links.Count.ToString() },
            new() { Key = "Уровни высоты", Value = map.ZLevels.Count.ToString() }
        };

        if (map.Regions.Count > 0)
            facts.Add(new UiKeyValueItem { Key = "Области влияния", Value = map.Regions.Count.ToString() });

        return new UiEntityDossierBlock
        {
            EntityType = "map-summary",
            Title = "Сводка карты",
            Subtitle = ExplorerPlayerFacingLabels.Realm(map.Realm),
            Summary = "Карта показывает уже созданные места отдельно от выходов, которые только намечены текущей сценой.",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = DescribeInventoryCount(knownLocations, "локация", "локации", "локаций"),
                    Tone = UiTone.Accent,
                    Icon = "map"
                },
                new UiEntityBadge
                {
                    Label = DescribeInventoryCount(plannedExits, "выход", "выхода", "выходов"),
                    Tone = plannedExits > 0 ? UiTone.Warning : UiTone.Muted,
                    Icon = "route"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "summary",
                    Title = "Ориентиры",
                    Summary = "Ключевые числа карты без технических исходников.",
                    Icon = "map",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = [new UiKeyValueGridBlock { Items = facts }]
                }
            ]
        };
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
        var effectEntries = ExplorerMortalEffectDetailActions.BuildEffectSnapshots(read.Node);
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

        var sections = new List<UiEntityDossierSection>();
        var rows = BuildEffectsSummaryRows(read, specs);
        if (rows.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "summary",
                Title = "Сводка",
                Summary = "Сколько эффектов, ран и временных состояний сейчас видно игроку.",
                Icon = "effect",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks =
                [
                    new UiKeyValueGridBlock
                    {
                        Items = rows
                            .Where(static row => row.Cells.Count >= 2)
                            .Select(static row => new UiKeyValueItem { Key = row.Cells[0], Value = row.Cells[1] })
                            .ToList()
                    }
                ]
            });
        }

        if (effectEntries.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "active-effects",
                Title = "Активные записи",
                Summary = "Краткие карточки эффектов. Полные данные открываются отдельным действием.",
                Icon = "effect",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = effectEntries.Select(static effect => (UiBlock)BuildEffectOverviewCard(effect)).ToList()
            });
        }

        if (sections.Count > 0)
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "effects",
                Title = title,
                Subtitle = "Состояния персонажа",
                Summary = "Список видимых эффектов, ран и временных состояний.",
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = DescribeInventoryCount(effectEntries.Count, "запись", "записи", "записей"),
                        Tone = effectEntries.Count > 0 ? UiTone.Accent : UiTone.Muted,
                        Icon = "effect"
                    }
                ],
                Sections = sections
            });
        }

        if (!HasVisibleStructuredEffectDetails(read.Node))
            blocks.AddRange(BuildMortalStatusFallbackBlocks(stateManager.CurrentState.PlayerStatus));

        if (blocks.Count == 0)
            blocks.Add(Message(UiNotificationSeverity.Info, title, "Данные ещё не созданы."));

        if (read.FileExists && read.Node == null && !string.IsNullOrWhiteSpace(read.Error))
            blocks.Add(Message(UiNotificationSeverity.Warning, title, $"Запись эффектов найдена, но не разобрана как JSON. {read.Error}"));

        return Completed(command, blocks, ExplorerMortalEffectDetailActions.Build(commandToken, read.Node));
    }

    private static UiEntityDossierBlock BuildEffectOverviewCard(ExplorerMortalEffectDetailActions.EffectSnapshot effect)
    {
        var description = FirstNonEmpty(
            GetNodeString(effect.Node, "effectDescription"),
            GetNodeString(effect.Node, "description"),
            GetNodeString(effect.Node, "summary"),
            GetNodeString(effect.Node, "source"));
        var duration = FirstNonEmpty(
            GetNodeString(effect.Node, "duration"),
            GetNodeString(effect.Node, "expiresAt"));
        var facts = new List<UiKeyValueItem>
        {
            new() { Key = "Раздел", Value = effect.Section }
        };
        AddInventoryFact(facts, "Длительность", duration);
        AddInventoryFact(facts, "Источник", GetNodeString(effect.Node, "source"));

        return new UiEntityDossierBlock
        {
            EntityType = "effect-summary",
            Title = effect.Name,
            Subtitle = effect.Section,
            Summary = FirstNonEmpty(description, duration, "Подробности доступны в карточке эффекта."),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = effect.Section,
                    Tone = UiTone.Accent,
                    Icon = "effect"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "facts",
                    Title = "Кратко",
                    Icon = "effect",
                    Collapsible = true,
                    InitiallyExpanded = false,
                    Blocks = [new UiKeyValueGridBlock { Items = facts }]
                }
            ]
        };
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
            new UiEntityDossierBlock
            {
                EntityType = "visible-status-effects",
                Title = "Видимые состояния",
                Subtitle = "Самочувствие персонажа",
                Summary = "Эти состояния уже видны в статусе персонажа, но для них ещё нет отдельной полной записи эффекта.",
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = DescribeInventoryCount(rows.Count, "запись", "записи", "записей"),
                        Tone = UiTone.Warning,
                        Icon = "effect"
                    }
                ],
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "visible-status",
                        Title = "Что чувствует персонаж",
                        Summary = "Краткая расшифровка состояний без технического файла эффектов.",
                        Icon = "effect",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks =
                        [
                            new UiKeyValueGridBlock
                            {
                                Items = rows
                                    .Select(static row => new UiKeyValueItem { Key = row.Label, Value = row.Details })
                                    .ToList()
                            }
                        ]
                    }
                ]
            }
        ];
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

    private static ExplorerMortalEffectDetailActions.EffectSnapshot? FindEffectSnapshot(
        IReadOnlyList<ExplorerMortalEffectDetailActions.EffectSnapshot> entries,
        string selector)
    {
        var normalized = NormalizeInventoryLookup(selector);
        return entries.FirstOrDefault(entry =>
            string.Equals(entry.Selector, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.Index.ToString(), normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(NormalizeInventoryLookup(entry.Name), normalized, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<UiBlock> BuildEffectDetailBlocks(ExplorerMortalEffectDetailActions.EffectSnapshot effect)
    {
        var blocks = new List<UiBlock>();
        var sections = new List<UiEntityDossierSection>();
        var overviewBlocks = new List<UiBlock>();
        var description = FirstNonEmpty(
            GetNodeString(effect.Node, "effectDescription"),
            GetNodeString(effect.Node, "description"),
            GetNodeString(effect.Node, "summary"));
        if (!string.IsNullOrWhiteSpace(description))
            overviewBlocks.Add(new UiTextBlock { Text = description, Tone = UiTone.Default });

        var facts = new List<UiKeyValueItem>
        {
            new() { Key = "Раздел", Value = effect.Section }
        };
        AddInventoryFact(facts, "Длительность", FirstNonEmpty(GetNodeString(effect.Node, "duration"), GetNodeString(effect.Node, "expiresAt")));
        AddInventoryFact(facts, "Осталось ходов", GetNodeString(effect.Node, "remainingTurns"));
        AddInventoryFact(facts, "Источник", GetNodeString(effect.Node, "source"));
        AddInventoryFact(facts, "Серьёзность", TranslateEffectSeverity(GetNodeString(effect.Node, "severity")));
        AddInventoryFact(facts, "Состояние", FirstNonEmpty(GetNodeString(effect.Node, "status"), GetNodeString(effect.Node, "state")));
        if (facts.Count > 0)
            overviewBlocks.Add(new UiKeyValueGridBlock { Items = facts });

        if (overviewBlocks.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "overview",
                Title = "Сведения",
                Summary = "Основные игровые свойства эффекта.",
                Icon = "effect",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = overviewBlocks
            });
        }

        AddStructuredBonusSection(sections, effect.Node["structuredBonuses"] as JsonArray);

        var detailBlocks = new List<UiBlock>();
        AddInventoryCombatEffectBlock(detailBlocks, effect.Node["combatEffect"]);
        AddInventoryCustomPropertiesBlock(detailBlocks, effect.Node["customProperties"]);
        if (detailBlocks.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "details",
                Title = "Дополнительно",
                Summary = "Боевые и особые свойства эффекта.",
                Icon = "effect",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = detailBlocks
            });
        }

        var hiddenNotes = CollectEffectNarrativeEntries(effect.Node["notes"])
            .Concat(CollectEffectNarrativeEntries(effect.Node["journalEntries"]))
            .ToList();
        if (hiddenNotes.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "notes",
                Title = "Заметки",
                Icon = "effect",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = hiddenNotes.Select(static note => (UiBlock)new UiTextBlock { Text = note, Tone = UiTone.Muted }).ToList()
            });
        }

        if (sections.Count == 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "empty",
                Title = "Сведения",
                Icon = "effect",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiTextBlock { Text = "Подробности эффекта пока не заполнены.", Tone = UiTone.Muted }]
            });
        }

        blocks.Add(new UiEntityDossierBlock
        {
            EntityType = "effect",
            Title = $"{effect.Section}: {effect.Name}",
            Subtitle = effect.Section,
            Summary = description,
            Badges =
            [
                new UiEntityBadge
                {
                    Label = effect.Section,
                    Tone = UiTone.Accent,
                    Icon = "effect"
                }
            ],
            Sections = sections
        });
        blocks.Add(new UiTextBlock { Text = "Вернуться к списку можно командой /эффекты.", Tone = UiTone.Muted });
        return blocks;
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

    private static string TranslateEffectSeverity(string? severity) =>
        (severity ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "minor" => "незначительная",
            "light" => "лёгкая",
            "moderate" or "medium" => "умеренная",
            "major" or "severe" => "серьёзная",
            "critical" => "критическая",
            _ => EmptyFallback(severity)
        };

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
        var npcNames = EnumerateNpcCoreDisplayNames(coreRead.Node).ToList();
        var summaryItems = BuildNpcOverviewItems(npcNames).ToList();
        foreach (var spec in specs)
        {
            var read = reads[spec.Path];
            var status = DescribeSpec(read, spec.PropertyName);
            if (status == "отсутствует")
                continue;

            summaryItems.Add(new UiKeyValueItem
            {
                Key = spec.Label,
                Value = status
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

        var commandRemainder = ExtractCommandRemainder(command);
        var questRequest = ParseNpcQuestDetailRequest(commandRemainder);
        if (questRequest.Kind == NpcQuestDetailKind.Invalid)
        {
            return Completed(command, [
                Message(UiNotificationSeverity.Warning, "Персонажи", "Для подробностей квеста используйте команду вида /npc quest <нпс> <квест>.")
            ], BuildNpcBackActions(commandToken));
        }

        if (questRequest.Kind == NpcQuestDetailKind.Quest)
        {
            var selected = FindNpcQuest(projections, questRequest.NpcSelector, questRequest.QuestSelector);
            if (selected == null)
            {
                return Completed(command, [
                    Message(UiNotificationSeverity.Warning, "Персонажи", "Такой квест НПС не найден.")
                ], BuildNpcBackActions(commandToken));
            }

            return Completed(
                command,
                selected.Value.Quest.Blocks,
                BuildNpcQuestBackActions(commandToken, selected.Value.Projection));
        }

        var sectionRequest = ParseNpcSectionDetailRequest(commandRemainder);
        if (sectionRequest.Kind == NpcSectionDetailKind.Unknown)
        {
            return Completed(command, [
                Message(UiNotificationSeverity.Warning, "Персонажи", "Для подробностей используйте команду вида /npc section <нпс> <раздел>.")
            ], BuildNpcBackActions(commandToken));
        }

        if (sectionRequest.Kind == NpcSectionDetailKind.Profile)
        {
            var selected = FindNpcProjection(projections, sectionRequest.NpcSelector);
            if (selected == null)
            {
                return Completed(command, [
                    Message(UiNotificationSeverity.Warning, "Персонажи", "Такой персонаж не найден.")
                ], BuildNpcBackActions(commandToken));
            }

            return Completed(
                command,
                [BuildNpcOverviewCard(commandToken, selected, includePrimaryAction: false)],
                BuildNpcSectionActions(commandToken, [selected])
                    .Concat(BuildNpcBackActions(commandToken))
                    .ToList());
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

            var actions = selected.Value.Section.Id.Equals("personal-quests", StringComparison.OrdinalIgnoreCase)
                ? BuildNpcQuestActions(commandToken, selected.Value.Projection)
                    .Concat(BuildNpcBackActions(commandToken))
                    .ToList()
                : BuildNpcBackActions(commandToken);

            return Completed(command, BuildNpcSectionDetailBlocks(selected.Value.Projection, selected.Value.Section), actions);
        }

        blocks.AddRange(BuildNpcOverviewBlocks(commandToken, summaryItems, projections, npcNames));

        foreach (var read in reads.Values)
        {
            if (read.FileExists && read.Node == null && !string.IsNullOrWhiteSpace(read.Error))
                blocks.Add(Message(UiNotificationSeverity.Warning, "Персонажи", "Одна из записей НПС найдена, но не разобрана как JSON."));
        }

        if (blocks.Count == 0)
            blocks.Add(Message(UiNotificationSeverity.Info, "Персонажи", "Данные ещё не созданы."));

        return Completed(command, blocks, BuildNpcSectionActions(commandToken, projections));
    }

    private static IReadOnlyList<UiBlock> BuildNpcOverviewBlocks(
        string commandToken,
        IReadOnlyList<UiKeyValueItem> summaryItems,
        IReadOnlyList<NpcDetailProjection> projections,
        IReadOnlyList<string> npcNames)
    {
        var blocks = new List<UiBlock>();
        var projectedNames = projections
            .Select(static projection => projection.NpcName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var npcCards = new List<UiBlock>();
        npcCards.AddRange(projections.Select(projection => BuildNpcOverviewCard(commandToken, projection, includePrimaryAction: true)));
        npcCards.AddRange(npcNames
            .Where(npcName => !projectedNames.Contains(npcName))
            .Select(npcName => BuildNpcOverviewCard(commandToken, npcName)));

        if (npcCards.Count > 0)
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "npc-collection",
                Title = "Персонажи",
                Subtitle = "Досье людей в сцене",
                Summary = "Лица, чьи решения, связи и заметки уже оставили след в текущей главе.",
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = FormatRussianCount(npcCards.Count, "персонаж", "персонажа", "персонажей"),
                        Tone = UiTone.Accent,
                        Icon = "npc"
                    }
                ],
                Facts = summaryItems
                    .Select(static item => new UiEntityFact { Label = item.Key, Value = item.Value })
                    .ToList(),
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "npcs",
                        Title = "Персонажи",
                        Summary = "Известные участники текущей сцены и связанных событий.",
                        Icon = "npc",
                        CollectionLabel = FormatRussianCount(npcCards.Count, "персонаж", "персонажа", "персонажей"),
                        Presentation = "collection",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks = npcCards
                    }
                ]
            });
        }

        return blocks;
    }

    private static UiEntityDossierBlock BuildNpcOverviewCard(
        string commandToken,
        NpcDetailProjection projection,
        bool includePrimaryAction)
    {
        var npcSelector = FirstNonEmpty(projection.NpcId, projection.NpcName);
        var badges = new List<UiEntityBadge>
        {
            new()
            {
                Label = "персонаж мира",
                Tone = projection.Sections.Count > 0 ? UiTone.Accent : UiTone.Muted,
                Icon = "npc"
            }
        };
        var facts = new List<UiEntityFact>();
        var hints = new List<UiEntityHint>();
        AddNpcTradePresentation(projection, facts, badges, hints);

        return new UiEntityDossierBlock
        {
            EntityType = "npc",
            Title = projection.NpcName,
            Subtitle = "Персонаж мира",
            Summary = BuildNpcOverviewSummary(projection),
            Badges = badges,
            Facts = facts,
            Hints = hints,
            Sections = projection.Sections
                .Select(static section => new UiEntityDossierSection
                {
                    Id = section.Id,
                    Title = section.Label,
                    Summary = string.Empty,
                    Icon = "npc",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = BuildNpcOverviewSectionBlocks(section)
                })
                .ToList(),
            PrimaryAction = includePrimaryAction
                ? BuildNpcProfileAction(commandToken, projection.NpcName, npcSelector)
                : null
        };
    }

    private static void AddNpcTradePresentation(
        NpcDetailProjection projection,
        List<UiEntityFact> facts,
        List<UiEntityBadge> badges,
        List<UiEntityHint> hints)
    {
        var trade = projection.Trade;
        if (trade == null)
            return;

        badges.Add(new UiEntityBadge
        {
            Label = "Торговец",
            Tone = trade.CanTrade ? UiTone.Accent : UiTone.Warning,
            Icon = "coin"
        });
        facts.Add(new UiEntityFact
        {
            Label = "Торговля",
            Value = trade.CanTrade ? "Можно торговать" : "Недоступна"
        });
        if (!string.IsNullOrWhiteSpace(trade.MerchantProfile))
        {
            facts.Add(new UiEntityFact
            {
                Label = "Профиль торговли",
                Value = NpcTradeService.GetMerchantProfileDisplayName(trade.MerchantProfile)
            });
        }
        if (trade.OfferCount > 0)
        {
            facts.Add(new UiEntityFact
            {
                Label = "Товаров в витрине",
                Value = trade.OfferCount.ToString(CultureInfo.InvariantCulture)
            });
        }
        if (!trade.CanTrade && !string.IsNullOrWhiteSpace(trade.BlockReason))
        {
            facts.Add(new UiEntityFact
            {
                Label = "Причина",
                Value = trade.BlockReason
            });
        }

        if (trade.CanTrade)
        {
            hints.Add(new UiEntityHint
            {
                Title = "Доступна торговля",
                Text = "В списке действий есть отдельная команда торговли с этим персонажем.",
                Tone = UiTone.Accent
            });
        }
    }

    private static string BuildNpcOverviewSummary(NpcDetailProjection projection)
    {
        foreach (var section in projection.Sections)
        foreach (var block in section.Blocks)
        {
            var summary = FirstNpcPlayableText(block);
            if (!string.IsNullOrWhiteSpace(summary))
                return BuildNpcTradeSummaryPrefix(projection, summary);
        }

        return BuildNpcTradeSummaryPrefix(projection, string.Empty);
    }

    private static string BuildNpcTradeSummaryPrefix(NpcDetailProjection projection, string playableSummary)
    {
        var trade = projection.Trade;
        if (trade == null)
            return playableSummary;

        var prefix = trade.CanTrade
            ? "Торговец: можно торговать напрямую."
            : "Торговец: торговля сейчас недоступна.";
        if (!trade.CanTrade && !string.IsNullOrWhiteSpace(trade.BlockReason))
            prefix = "Торговец: " + trade.BlockReason.Trim();

        return string.IsNullOrWhiteSpace(playableSummary)
            ? prefix
            : prefix + " " + playableSummary;
    }

    private static string FirstNpcPlayableText(UiBlock block)
    {
        switch (block)
        {
            case UiTextBlock text:
                return CleanNpcPlayableText(text.Text);

            case UiKeyValueGridBlock grid:
                return grid.Items
                    .Select(static item => item.Value)
                    .Select(CleanNpcPlayableText)
                    .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

            case UiListBlock list:
                return list.Items
                    .Select(CleanNpcPlayableText)
                    .FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

            case UiPanelBlock panel:
                foreach (var child in panel.Blocks)
                {
                    var value = FirstNpcPlayableText(child);
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                break;

            case UiEntityDossierBlock dossier:
                var dossierSummary = CleanNpcPlayableText(dossier.Summary);
                if (!string.IsNullOrWhiteSpace(dossierSummary))
                    return dossierSummary;

                foreach (var fact in dossier.Facts)
                {
                    var value = CleanNpcPlayableText(fact.Value);
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }

                foreach (var card in dossier.Cards)
                {
                    var value = FirstNpcPlayableText(card);
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }

                foreach (var section in dossier.Sections)
                foreach (var child in section.Blocks)
                {
                    var value = FirstNpcPlayableText(child);
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                break;
        }

        return string.Empty;
    }

    private static string FirstNpcPlayableText(UiEntityCard card)
    {
        var summary = CleanNpcPlayableText(card.Summary);
        if (!string.IsNullOrWhiteSpace(summary))
            return summary;

        foreach (var fact in card.Facts)
        {
            var value = CleanNpcPlayableText(fact.Value);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        foreach (var item in card.List)
        {
            var value = CleanNpcPlayableText(item);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        foreach (var child in card.Nested)
        {
            var value = FirstNpcPlayableText(child);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        foreach (var child in card.Cards)
        {
            var value = FirstNpcPlayableText(child);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string CleanNpcPlayableText(string? value)
    {
        if (!IsNpcPlayableText(value))
            return string.Empty;

        var clean = value!.Trim();
        if (clean.Contains(';', StringComparison.Ordinal))
        {
            clean = clean.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(IsNpcPlayableText) ?? string.Empty;
        }

        return clean;
    }

    private static bool IsNpcPlayableText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var clean = value.Trim();
        if (clean is "—" or "-")
            return false;

        if (clean.Contains("запис", StringComparison.OrdinalIgnoreCase) &&
            clean.Length < 24)
            return false;

        if (clean.Contains("раскрыт", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("выберите", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains("доступ", StringComparison.OrdinalIgnoreCase) && clean.Contains("карточ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static List<UiBlock> BuildNpcOverviewSectionBlocks(NpcDetailSection section) =>
        section.Blocks
            .SelectMany(FlattenNpcOverviewBlock)
            .ToList();

    private static IEnumerable<UiBlock> FlattenNpcOverviewBlock(UiBlock block)
    {
        if (block is UiEntityDossierBlock dossier &&
            string.Equals(dossier.EntityType, "npc-section", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var section in dossier.Sections)
            {
                foreach (var child in section.Blocks)
                {
                    var sanitized = SanitizeNpcOverviewBlock(child);
                    if (sanitized != null)
                        yield return sanitized;
                }
            }

            yield break;
        }

        var normalized = SanitizeNpcOverviewBlock(block);
        if (normalized != null)
            yield return normalized;
    }

    private static UiBlock? SanitizeNpcOverviewBlock(UiBlock block) =>
        block switch
        {
            UiEntityDossierBlock dossier => SanitizeNpcOverviewDossier(dossier),
            UiPanelBlock panel => new UiPanelBlock
            {
                Title = panel.Title,
                Blocks = panel.Blocks
                    .Select(SanitizeNpcOverviewBlock)
                    .Where(static child => child != null)
                    .Cast<UiBlock>()
                    .ToList()
            },
            UiKeyValueGridBlock grid => SanitizeNpcOverviewGrid(grid),
            UiListBlock list => new UiListBlock
            {
                Ordered = list.Ordered,
                Items = list.Items
                    .Select(RemoveNpcOverviewHiddenSegments)
                    .Where(static item => !string.IsNullOrWhiteSpace(item))
                    .ToList()
            },
            UiTextBlock text => string.IsNullOrWhiteSpace(RemoveNpcOverviewHiddenSegments(text.Text))
                ? null
                : new UiTextBlock { Text = RemoveNpcOverviewHiddenSegments(text.Text), Tone = text.Tone },
            _ => block
        };

    private static UiEntityDossierBlock SanitizeNpcOverviewDossier(UiEntityDossierBlock dossier) =>
        new()
        {
            EntityType = dossier.EntityType,
            Title = dossier.Title,
            Subtitle = dossier.Subtitle,
            Summary = IsNpcOverviewSectionContainer(dossier)
                ? string.Empty
                : RemoveNpcOverviewHiddenSegments(dossier.Summary),
            Badges = dossier.Badges,
            Media = dossier.Media,
            Facts = dossier.Facts
                .Select(static fact => new UiEntityFact
                {
                    Label = fact.Label,
                    Value = RemoveNpcOverviewHiddenSegments(fact.Value)
                })
                .Where(static fact => !string.IsNullOrWhiteSpace(fact.Value))
                .ToList(),
            Metrics = dossier.Metrics,
            Hints = dossier.Hints
                .Select(static hint => new UiEntityHint
                {
                    Title = hint.Title,
                    Text = RemoveNpcOverviewHiddenSegments(hint.Text),
                    Tone = hint.Tone
                })
                .Where(static hint => !string.IsNullOrWhiteSpace(hint.Text))
                .ToList(),
            List = dossier.List
                .Select(RemoveNpcOverviewHiddenSegments)
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .ToList(),
            Cards = dossier.Cards
                .Select(SanitizeNpcOverviewCard)
                .Where(static card => card != null)
                .Cast<UiEntityCard>()
                .ToList(),
            Sections = dossier.Sections
                .Select(SanitizeNpcOverviewSection)
                .Where(static section => section != null)
                .Cast<UiEntityDossierSection>()
                .ToList()
        };

    private static bool IsNpcOverviewSectionContainer(UiEntityDossierBlock dossier) =>
        string.Equals(dossier.EntityType, "npc-section", StringComparison.OrdinalIgnoreCase);

    private static UiEntityDossierSection? SanitizeNpcOverviewSection(UiEntityDossierSection section)
    {
        var blocks = section.Blocks
            .Select(SanitizeNpcOverviewBlock)
            .Where(static block => block != null)
            .Cast<UiBlock>()
            .ToList();
        if (blocks.Count == 0 &&
            section.Facts.Count == 0 &&
            section.Cards.Count == 0 &&
            section.List.Count == 0)
        {
            return null;
        }

        return new UiEntityDossierSection
        {
            Id = section.Id,
            Title = section.Title,
            Summary = RemoveNpcOverviewHiddenSegments(section.Summary),
            Icon = section.Icon,
            CollectionLabel = section.CollectionLabel,
            Presentation = section.Presentation,
            Collapsible = section.Collapsible,
            InitiallyExpanded = section.InitiallyExpanded,
            Facts = section.Facts
                .Select(static fact => new UiEntityFact
                {
                    Label = fact.Label,
                    Value = RemoveNpcOverviewHiddenSegments(fact.Value)
                })
                .Where(static fact => !string.IsNullOrWhiteSpace(fact.Value))
                .ToList(),
            Metrics = section.Metrics,
            Hints = section.Hints
                .Select(static hint => new UiEntityHint
                {
                    Title = hint.Title,
                    Text = RemoveNpcOverviewHiddenSegments(hint.Text),
                    Tone = hint.Tone
                })
                .Where(static hint => !string.IsNullOrWhiteSpace(hint.Text))
                .ToList(),
            List = section.List
                .Select(RemoveNpcOverviewHiddenSegments)
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .ToList(),
            Cards = section.Cards
                .Select(SanitizeNpcOverviewCard)
                .Where(static card => card != null)
                .Cast<UiEntityCard>()
                .ToList(),
            Blocks = blocks
        };
    }

    private static UiEntityCard? SanitizeNpcOverviewCard(UiEntityCard card)
    {
        var summary = RemoveNpcOverviewHiddenSegments(card.Summary);
        return new UiEntityCard
        {
            Title = card.Title,
            Subtitle = card.Subtitle,
            Summary = summary,
            Icon = card.Icon,
            Badges = card.Badges,
            Media = card.Media,
            Facts = card.Facts
                .Select(static fact => new UiEntityFact
                {
                    Label = fact.Label,
                    Value = RemoveNpcOverviewHiddenSegments(fact.Value)
                })
                .Where(static fact => !string.IsNullOrWhiteSpace(fact.Value))
                .ToList(),
            Metrics = card.Metrics,
            Hints = card.Hints
                .Select(static hint => new UiEntityHint
                {
                    Title = hint.Title,
                    Text = RemoveNpcOverviewHiddenSegments(hint.Text),
                    Tone = hint.Tone
                })
                .Where(static hint => !string.IsNullOrWhiteSpace(hint.Text))
                .ToList(),
            List = card.List
                .Select(RemoveNpcOverviewHiddenSegments)
                .Where(static item => !string.IsNullOrWhiteSpace(item))
                .ToList(),
            Nested = card.Nested
                .Select(SanitizeNpcOverviewCard)
                .Where(static nested => nested != null)
                .Cast<UiEntityCard>()
                .ToList(),
            Cards = card.Cards
                .Select(SanitizeNpcOverviewCard)
                .Where(static nested => nested != null)
                .Cast<UiEntityCard>()
                .ToList()
        };
    }

    private static UiKeyValueGridBlock SanitizeNpcOverviewGrid(UiKeyValueGridBlock grid) =>
        new()
        {
            Items = grid.Items
                .Select(static item => new UiKeyValueItem
                {
                    Key = item.Key,
                    Value = RemoveNpcOverviewHiddenSegments(item.Value)
                })
                .Where(static item => !string.IsNullOrWhiteSpace(item.Value))
                .ToList()
        };

    private static string RemoveNpcOverviewHiddenSegments(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var parts = value
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(static part => !IsNpcOverviewHiddenSegment(part))
            .ToList();

        if (parts.Count == 0)
            return string.Empty;

        return string.Join("; ", parts);
    }

    private static bool IsNpcOverviewHiddenSegment(string part)
    {
        var normalized = part.Trim().ToLowerInvariant();
        return normalized.StartsWith("награда:", StringComparison.Ordinal) ||
               normalized.StartsWith("провал:", StringComparison.Ordinal) ||
               normalized.StartsWith("reward:", StringComparison.Ordinal) ||
               normalized.StartsWith("failure", StringComparison.Ordinal);
    }

    private static UiEntityDossierBlock BuildNpcOverviewCard(string commandToken, string npcName)
    {
        return new UiEntityDossierBlock
        {
            EntityType = "npc",
            Title = npcName,
            Subtitle = "Персонаж мира",
            Summary = "Персонаж найден в основных данных, но подробное досье пока не заполнено.",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = "нет подробностей",
                    Tone = UiTone.Muted,
                    Icon = "npc"
                }
            ],
            Hints =
            [
                new UiEntityHint
                {
                    Title = "Пока без досье",
                    Text = "ГМ ещё не добавил мысли, отношения, задачи или игровые свойства этого персонажа.",
                    Tone = UiTone.Muted
                }
            ],
            PrimaryAction = BuildNpcProfileAction(commandToken, npcName, npcName)
        };
    }

    private static IEnumerable<UiKeyValueItem> BuildNpcOverviewItems(IReadOnlyList<string> names)
    {
        if (names.Count == 0)
            yield break;

        yield return new UiKeyValueItem
        {
            Key = "Найдено",
            Value = FormatRussianCount(names.Count, "персонаж", "персонажа", "персонажей")
        };
        yield return new UiKeyValueItem
        {
            Key = "В кадре",
            Value = BuildNpcNamePreview(names)
        };
    }

    private static IEnumerable<string> EnumerateNpcCoreDisplayNames(JsonNode? root)
    {
        foreach (var npc in EnumerateNpcCoreObjects(root))
        {
            var name = FirstNonEmpty(
                GetNodeString(npc, "NPCName"),
                GetNodeString(npc, "npcName"),
                GetNodeString(npc, "displayName"),
                GetNodeString(npc, "name"));
            if (!string.IsNullOrWhiteSpace(name))
                yield return name;
        }
    }

    private static IEnumerable<JsonObject> EnumerateNpcCoreObjects(JsonNode? root)
    {
        if (root is JsonArray array)
        {
            foreach (var npc in array.OfType<JsonObject>())
                yield return npc;
            yield break;
        }

        if (root is not JsonObject obj)
            yield break;

        foreach (var sectionName in GuardianPolicyContracts.NpcCoreCanonicalNpcObjectSections
                     .Concat(GuardianPolicyContracts.NpcCoreLegacyAliasSections))
        {
            if (obj[sectionName] is not JsonArray section)
                continue;

            foreach (var npc in section.OfType<JsonObject>())
            {
                if (HasVisibleNpcIdentity(npc))
                    yield return npc;
            }
        }
    }

    private static bool HasVisibleNpcIdentity(JsonObject npc) =>
        !string.IsNullOrWhiteSpace(FirstNonEmpty(
            GetNodeString(npc, "NPCId"),
            GetNodeString(npc, "npcId"),
            GetNodeString(npc, "id"),
            GetNodeString(npc, "NPCName"),
            GetNodeString(npc, "npcName"),
            GetNodeString(npc, "name")));

    private static string BuildNpcNamePreview(IReadOnlyList<string> names)
    {
        var preview = names.Take(5).ToList();
        var suffix = names.Count > preview.Count
            ? $" и ещё {names.Count - preview.Count}"
            : string.Empty;
        return string.Join(", ", preview) + suffix;
    }

    private static string FormatRussianCount(int count, string one, string few, string many)
    {
        var mod100 = Math.Abs(count) % 100;
        var mod10 = Math.Abs(count) % 10;
        var noun = mod100 is >= 11 and <= 14
            ? many
            : mod10 switch
            {
                1 => one,
                >= 2 and <= 4 => few,
                _ => many
            };
        return $"{count} {noun}";
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
        if (IsNpcProfileDetailToken(kindToken))
        {
            if (string.IsNullOrWhiteSpace(detailRemainder))
                return new NpcSectionDetailRequest(NpcSectionDetailKind.Unknown, string.Empty, string.Empty);

            return new NpcSectionDetailRequest(
                NpcSectionDetailKind.Profile,
                NormalizeCombatSelector(detailRemainder),
                string.Empty);
        }

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

    private static NpcQuestDetailRequest ParseNpcQuestDetailRequest(string remainder)
    {
        if (string.IsNullOrWhiteSpace(remainder))
            return new NpcQuestDetailRequest(NpcQuestDetailKind.None, string.Empty, string.Empty);

        var (kindToken, detailRemainder) = SplitFirstCombatArgument(remainder);
        if (!IsNpcQuestDetailToken(kindToken))
            return new NpcQuestDetailRequest(NpcQuestDetailKind.None, string.Empty, string.Empty);

        var (npcSelector, questSelector) = SplitFirstCombatArgument(detailRemainder);
        if (string.IsNullOrWhiteSpace(npcSelector) || string.IsNullOrWhiteSpace(questSelector))
            return new NpcQuestDetailRequest(NpcQuestDetailKind.Invalid, string.Empty, string.Empty);

        return new NpcQuestDetailRequest(
            NpcQuestDetailKind.Quest,
            NormalizeCombatSelector(npcSelector),
            NormalizeCombatSelector(questSelector));
    }

    private static bool IsNpcQuestDetailToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        return normalized is "quest" or "квест" or "личный_квест" or "personal-quest" or "personal_quest";
    }

    private static bool IsNpcProfileDetailToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        return normalized is "npc" or "profile" or "персонаж" or "досье" or "карточка";
    }

    private static bool IsNpcSectionDetailToken(string token)
    {
        var normalized = token.Trim().ToLowerInvariant();
        return normalized is "section" or "раздел" or "detail" or "подробнее";
    }

    private static NpcDetailProjection? FindNpcProjection(
        IReadOnlyList<NpcDetailProjection> projections,
        string npcSelector)
    {
        var normalizedNpc = NormalizeInventoryLookup(npcSelector);
        foreach (var projection in projections)
        {
            if (string.Equals(NormalizeInventoryLookup(projection.NpcId), normalizedNpc, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeInventoryLookup(projection.NpcName), normalizedNpc, StringComparison.OrdinalIgnoreCase))
            {
                return projection;
            }
        }

        return null;
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

    private static (NpcDetailProjection Projection, NpcQuestDetail Quest)? FindNpcQuest(
        IReadOnlyList<NpcDetailProjection> projections,
        string npcSelector,
        string questSelector)
    {
        var normalizedNpc = NormalizeInventoryLookup(npcSelector);
        var normalizedQuest = NormalizeInventoryLookup(questSelector);
        foreach (var projection in projections)
        {
            var npcMatches =
                string.Equals(NormalizeInventoryLookup(projection.NpcId), normalizedNpc, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeInventoryLookup(projection.NpcName), normalizedNpc, StringComparison.OrdinalIgnoreCase);
            if (!npcMatches)
                continue;

            var quest = projection.PersonalQuests.FirstOrDefault(quest =>
                string.Equals(NormalizeInventoryLookup(quest.Selector), normalizedQuest, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(NormalizeInventoryLookup(quest.QuestName), normalizedQuest, StringComparison.OrdinalIgnoreCase));
            if (quest != null)
                return (projection, quest);
        }

        return null;
    }

    private static IReadOnlyList<UiBlock> BuildNpcSectionDetailBlocks(NpcDetailProjection projection, NpcDetailSection section)
    {
        var panelBlocks = new List<UiBlock>
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
        panelBlocks.AddRange(section.Blocks.SelectMany(ProjectNpcSectionBlockForBrowser));
        return
        [
            new UiPanelBlock
            {
                Title = $"{projection.NpcName} — {section.Label}",
                Blocks = panelBlocks
            },
            new UiTextBlock { Text = "Вернуться к списку можно командой /npc.", Tone = UiTone.Muted }
        ];
    }

    private static IEnumerable<UiBlock> ProjectNpcSectionBlockForBrowser(UiBlock block)
    {
        if (block is UiTableBlock table && table.Columns.Any(IsNpcGenericDetailsColumn))
            return BuildNpcDetailPanels(table);

        if (block is UiPanelBlock panel)
        {
            return
            [
                new UiPanelBlock
                {
                    Title = panel.Title,
                    Blocks = panel.Blocks.SelectMany(ProjectNpcSectionBlockForBrowser).ToList()
                }
            ];
        }

        return [block];
    }

    private static IReadOnlyList<UiBlock> BuildNpcDetailPanels(UiTableBlock table)
    {
        var detailIndexes = table.Columns
            .Select((column, index) => new { Column = column, Index = index })
            .Where(item => IsNpcGenericDetailsColumn(item.Column))
            .Select(static item => item.Index)
            .ToHashSet();
        if (detailIndexes.Count == 0)
            return [table];

        var rowPanels = new List<UiBlock>();
        foreach (var row in table.Rows)
        {
            var title = BuildNpcDetailRowTitle(table, row, detailIndexes);
            var rowBlocks = new List<UiBlock>();
            var facts = new List<UiKeyValueItem>();
            for (var i = 0; i < table.Columns.Count && i < row.Cells.Count; i++)
            {
                if (detailIndexes.Contains(i) || i == 0)
                    continue;

                var value = FormatNpcDetailScalar(row.Cells[i]);
                if (!string.IsNullOrWhiteSpace(value))
                    facts.Add(new UiKeyValueItem { Key = table.Columns[i], Value = value });
            }

            if (facts.Count > 0)
                rowBlocks.Add(new UiKeyValueGridBlock { Items = facts });

            foreach (var detailIndex in detailIndexes.Order())
            {
                if (detailIndex >= row.Cells.Count)
                    continue;

                rowBlocks.AddRange(BuildNpcDetailValueBlocks(row.Cells[detailIndex], table.Title, title));
            }

            if (rowBlocks.Count == 0)
            {
                rowBlocks.Add(new UiTextBlock
                {
                    Text = "Подробности не указаны.",
                    Tone = UiTone.Muted
                });
            }

            rowPanels.Add(new UiPanelBlock
            {
                Title = title,
                Blocks = rowBlocks
            });
        }

        return rowPanels.Count == 0
            ? []
            :
            [
                new UiPanelBlock
                {
                    Title = table.Title,
                    Blocks = rowPanels
                }
            ];
    }

    private static string BuildNpcDetailRowTitle(
        UiTableBlock table,
        UiTableRow row,
        HashSet<int> detailIndexes)
    {
        for (var i = 0; i < table.Columns.Count && i < row.Cells.Count; i++)
        {
            if (detailIndexes.Contains(i))
                continue;

            var value = row.Cells[i].Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return FormatNpcDetailScalar(value);
        }

        return "Запись";
    }

    private static IReadOnlyList<UiBlock> BuildNpcDetailValueBlocks(string value, string tableTitle, string rowTitle)
    {
        var listItems = new List<string>();
        var facts = new List<UiKeyValueItem>();
        var scalarIndex = 0;
        foreach (var part in SplitNpcDetailParts(value))
        {
            if (TrySplitNpcDetailPair(part, out var key, out var pairValue))
            {
                var formatted = FormatNpcDetailScalar(pairValue);
                if (!string.IsNullOrWhiteSpace(formatted))
                    facts.Add(new UiKeyValueItem { Key = FormatNpcDetailKey(key, tableTitle, rowTitle), Value = formatted });
            }
            else
            {
                var formatted = FormatNpcDetailScalar(part);
                if (IsEmptyNpcDetailValue(formatted))
                    continue;

                var label = InferNpcDetailScalarLabel(tableTitle, rowTitle, scalarIndex, formatted);
                if (string.IsNullOrWhiteSpace(label))
                {
                    listItems.Add(formatted);
                }
                else
                {
                    facts.Add(new UiKeyValueItem { Key = label, Value = formatted });
                }

                scalarIndex++;
            }
        }

        var blocks = new List<UiBlock>();
        if (facts.Count > 0)
            blocks.Add(new UiKeyValueGridBlock { Items = facts });
        if (listItems.Count > 0)
            blocks.Add(new UiListBlock { Items = listItems });
        return blocks;
    }

    private static IEnumerable<string> SplitNpcDetailParts(string value) =>
        value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static part => !string.IsNullOrWhiteSpace(part));

    private static bool TrySplitNpcDetailPair(string value, out string key, out string pairValue)
    {
        key = string.Empty;
        pairValue = string.Empty;
        var separator = value.IndexOf(':', StringComparison.Ordinal);
        if (separator <= 0 || separator > 48)
            return false;

        key = value[..separator].Trim();
        pairValue = value[(separator + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(pairValue);
    }

    private static bool IsNpcGenericDetailsColumn(string column)
    {
        var normalized = column.Trim().ToLowerInvariant();
        return normalized is "подробности" or "детали" or "detail" or "details";
    }

    private static bool IsEmptyNpcDetailValue(string value)
    {
        var clean = value.Trim();
        return string.IsNullOrWhiteSpace(clean) ||
               clean is "—" or "-" ||
               clean.Equals("не указано", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatNpcDetailScalar(string value)
    {
        var clean = value.Trim();
        return clean.ToLowerInvariant() switch
        {
            "" => string.Empty,
            "active" => "Активен",
            "pending" => "Ожидает",
            "completed" or "complete" => "Завершён",
            "failed" => "Провален",
            "rival" => "соперник",
            "ally" => "союзник",
            "neutral" => "нейтрально",
            "knowledgebased" => "основан на знаниях",
            "utility" => "утилитарный",
            "combat" => "боевой",
            "social" => "социальный",
            "common" => "обычный",
            "uncommon" => "необычный",
            "rare" => "редкий",
            "epic" => "эпический",
            "legendary" => "легендарный",
            "document" => "документ",
            "container" => "контейнер",
            "tool" => "инструмент",
            "buff" => "усиление",
            "debuff" => "ослабление",
            "wound" => "рана",
            _ => clean
        };
    }

    private static string FormatNpcDetailKey(string key, string tableTitle, string rowTitle)
    {
        var clean = key.Trim();
        var normalized = clean.ToLowerInvariant();
        if (normalized is "type" or "skilltype")
            return IsNpcSkillContext(tableTitle, rowTitle) ? "Тип навыка" : "Тип";
        if (normalized is "category" or "group")
            return "Категория";
        if (normalized is "rarity" or "quality")
            return "Редкость";
        if (normalized is "rank" or "level" or "tier")
            return IsNpcSkillContext(tableTitle, rowTitle) ? "Ранг" : "Уровень";
        if (normalized is "summary" or "description" or "narrativesummary")
            return "Описание";
        if (normalized is "status" or "activestate")
            return "Состояние";
        if (normalized is "value")
            return "Значение";

        return clean;
    }

    private static string InferNpcDetailScalarLabel(string tableTitle, string rowTitle, int scalarIndex, string value)
    {
        if (IsNpcSkillContext(tableTitle, rowTitle))
        {
            if (IsNpcSkillTypeValue(value))
                return "Тип навыка";
            if (IsNpcRarityValue(value))
                return "Редкость";
            if (IsIntegerText(value))
                return "Ранг";

            return scalarIndex switch
            {
                0 => "Название навыка",
                1 => "Описание",
                _ => "Свойство навыка"
            };
        }

        if (IsNpcFateCardContext(tableTitle, rowTitle))
        {
            if (IsNpcRarityValue(value))
                return "Редкость";

            return scalarIndex switch
            {
                0 => "Название карты",
                1 => "Описание",
                _ => "Свойство карты"
            };
        }

        if (IsNpcMemoryContext(tableTitle, rowTitle))
        {
            if (IsNpcRarityValue(value))
                return "Редкость";

            return scalarIndex switch
            {
                0 => "Название воспоминания",
                1 => "Описание",
                _ => "Свойство воспоминания"
            };
        }

        if (IsNpcStateContext(tableTitle, rowTitle))
        {
            if (IsNpcRarityValue(value))
                return "Редкость";

            return scalarIndex switch
            {
                0 => "Название состояния",
                1 => "Описание",
                _ => "Свойство состояния"
            };
        }

        return string.Empty;
    }

    private static bool IsNpcSkillContext(string tableTitle, string rowTitle)
    {
        var context = (tableTitle + " " + rowTitle).ToLowerInvariant();
        return context.Contains("навык", StringComparison.Ordinal) ||
               context.Contains("skills", StringComparison.Ordinal) ||
               context.Contains("skill", StringComparison.Ordinal);
    }

    private static bool IsNpcFateCardContext(string tableTitle, string rowTitle)
    {
        var context = (tableTitle + " " + rowTitle).ToLowerInvariant();
        return context.Contains("карта судьбы", StringComparison.Ordinal) ||
               context.Contains("fate", StringComparison.Ordinal);
    }

    private static bool IsNpcMemoryContext(string tableTitle, string rowTitle)
    {
        var context = (tableTitle + " " + rowTitle).ToLowerInvariant();
        return context.Contains("воспомин", StringComparison.Ordinal) ||
               context.Contains("memory", StringComparison.Ordinal);
    }

    private static bool IsNpcStateContext(string tableTitle, string rowTitle)
    {
        var context = (tableTitle + " " + rowTitle).ToLowerInvariant();
        return context.Contains("особое состояние", StringComparison.Ordinal) ||
               context.Contains("состояние", StringComparison.Ordinal) ||
               context.Contains("state", StringComparison.Ordinal);
    }

    private static bool IsNpcSkillTypeValue(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "основан на знаниях" or "утилитарный" or "боевой" or "социальный";
    }

    private static bool IsNpcRarityValue(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "обычный" or "обычное" or "необычный" or "необычное" or "редкий" or "редкое" or
            "эпический" or "эпическое" or "легендарный" or "легендарное" or "уникальный" or "уникальное";
    }

    private static bool IsIntegerText(string value) =>
        int.TryParse(value.Trim(), out _);

    private static IReadOnlyList<UiAction> BuildNpcSectionActions(
        string commandToken,
        IReadOnlyList<NpcDetailProjection> projections)
    {
        var actions = new List<UiAction>();
        foreach (var projection in projections)
        {
            var npcSelector = FirstNonEmpty(projection.NpcId, projection.NpcName);
            if (projection.Trade?.CanTrade == true)
            {
                actions.Add(BuildNpcTradeAction(projection, npcSelector));
            }

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

    private static UiAction BuildNpcTradeAction(NpcDetailProjection projection, string npcSelector) =>
        new()
        {
            Id = "npc-trade-" + ToActionIdPart(npcSelector),
            Label = "Торговать: " + projection.NpcName,
            Command = "/npc_trade " + FormatCombatCommandArgument(npcSelector),
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false,
            Payload = new JsonObject
            {
                ["npcSelector"] = npcSelector,
                ["npcName"] = projection.NpcName,
                ["action"] = "trade"
            }
        };

    private static IReadOnlyList<UiAction> BuildNpcQuestActions(
        string commandToken,
        NpcDetailProjection projection)
    {
        if (projection.PersonalQuests.Count == 0)
            return [];

        var npcSelector = FirstNonEmpty(projection.NpcId, projection.NpcName);
        return projection.PersonalQuests
            .Select(quest => new UiAction
            {
                Id = "npc-quest-" + ToActionIdPart(npcSelector) + "-" + ToActionIdPart(quest.Selector),
                Label = "Открыть квест: " + quest.QuestName,
                Command = BuildNpcQuestCommand(commandToken, npcSelector, quest.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["npcSelector"] = npcSelector,
                    ["npcName"] = projection.NpcName,
                    ["questSelector"] = quest.Selector,
                    ["questName"] = quest.QuestName
                }
            })
            .ToList();
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

    private static UiAction BuildNpcProfileAction(string commandToken, string npcName, string npcSelector) =>
        new()
        {
            Id = "npc-profile-" + ToActionIdPart(npcSelector),
            Label = "Открыть отдельно: " + npcName,
            Command = BuildNpcProfileCommand(commandToken, npcSelector),
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false,
            Payload = new JsonObject
            {
                ["npcSelector"] = npcSelector,
                ["npcName"] = npcName
            }
        };

    private static IReadOnlyList<UiAction> BuildNpcQuestBackActions(string commandToken, NpcDetailProjection projection)
    {
        var npcSelector = FirstNonEmpty(projection.NpcId, projection.NpcName);
        return
        [
            new UiAction
            {
                Id = "npc-quest-back-to-personal-quests",
                Label = "Назад к личным квестам",
                Command = BuildNpcSectionCommand(commandToken, npcSelector, "personal-quests"),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false
            },
            new UiAction
            {
                Id = "npc-back",
                Label = "Назад к персонажам",
                Command = commandToken,
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false
            }
        ];
    }

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

    private static string BuildNpcProfileCommand(string commandToken, string npcSelector)
    {
        var detailToken = string.Equals(commandToken, "/нпс", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(commandToken, "/персонажи", StringComparison.OrdinalIgnoreCase)
            ? "персонаж"
            : "profile";
        return commandToken + " " + detailToken + " " + FormatCombatCommandArgument(npcSelector);
    }

    private static string BuildNpcQuestCommand(string commandToken, string npcSelector, string questSelector)
    {
        var detailToken = string.Equals(commandToken, "/нпс", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(commandToken, "/персонажи", StringComparison.OrdinalIgnoreCase)
            ? "квест"
            : "quest";
        return commandToken + " " + detailToken + " " + FormatCombatCommandArgument(npcSelector) + " " + FormatCombatCommandArgument(questSelector);
    }

    private static async Task<ExplorerCommandResult> BuildCurrentLocation(string command, FileSystemManager fs)
    {
        var locationRead = await ReadJson(fs, "game_state/world/current_location.json");
        var timeRead = await ReadJson(fs, "game_state/world/world_time.json");
        var weatherRead = await ReadJson(fs, "game_state/world/weather.json");
        var location = UnwrapCurrentLocationNode(locationRead.Node);
        var weather = UnwrapWeatherNode(weatherRead.Node);
        var blocks = new List<UiBlock>();
        var sections = new List<UiEntityDossierSection>();
        var locationTitle = "Текущая локация";

        if (location == null)
        {
            blocks.Add(Message(UiNotificationSeverity.Info, "Где я", "Местоположение неизвестно."));
        }
        else
        {
            var title = FirstNonEmpty(
                GetLocationNodeString(location, "name", "locationName", "displayName"),
                "Текущая локация");
            locationTitle = title;
            var facts = new List<UiKeyValueItem>();
            AddLocationFact(facts, "Локация", title);
            AddLocationFact(facts, "Регион", GetLocationNodeString(location, "region"));
            AddLocationFact(facts, "Тип", TranslateLocationType(GetLocationNodeString(location, "locationType", "type")));
            AddLocationFact(facts, "Внутренний тип", TranslateIndoorLocationType(GetLocationNodeString(location, "indoorType")));
            AddLocationFact(facts, "Биом", TranslateLocationBiome(GetLocationNodeString(location, "biome")));
            AddLocationFact(facts, "Опасность", DescribeLocationDifficulty(location));

            var panelBlocks = new List<UiBlock>();
            var description = GetLocationNodeString(location, "description", "shortDescription");
            if (!string.IsNullOrWhiteSpace(description))
                panelBlocks.Add(new UiTextBlock { Text = description, Tone = UiTone.Default });
            if (facts.Count > 0)
                panelBlocks.Add(new UiKeyValueGridBlock { Items = facts });

            AddDossierSectionIfAny(
                sections,
                "location",
                title,
                "Описание, регион и важные свойства текущего места.",
                "map",
                panelBlocks.Count > 0 ? panelBlocks : [new UiTextBlock { Text = "Описание локации пока не заполнено.", Tone = UiTone.Muted }]);

            var locationBlocks = new List<UiBlock>();
            AddLocationFeatureBlock(locationBlocks, location);
            AddLocationFactionControlBlock(locationBlocks, location);
            AddLocationThreatBlock(locationBlocks, location);
            AddLocationEventBlock(locationBlocks, location);
            AddDossierSectionIfAny(
                sections,
                "local-context",
                "Местный контекст",
                "Особенности, контроль, угрозы и недавние события в локации.",
                "map",
                locationBlocks);
        }

        var timeBlocks = new List<UiBlock>();
        AddWorldTimeBlock(timeBlocks, timeRead.Node);
        AddDossierSectionIfAny(sections, "time", "Время", "Текущее игровое время.", "time", timeBlocks);

        var weatherBlocks = new List<UiBlock>();
        AddWeatherSummaryBlock(weatherBlocks, weather, includeBiome: false, currentLocation: location);
        AddDossierSectionIfAny(sections, "weather", "Погода", "Условия вокруг текущей локации.", "weather", weatherBlocks);

        if (sections.Count > 0)
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "current-location",
                Title = locationTitle,
                Subtitle = "Где я",
                Summary = location == null
                    ? "Местоположение неизвестно."
                    : FirstNonEmpty(GetLocationNodeString(location, "description", "shortDescription"), "Текущий контекст персонажа."),
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = "Местоположение",
                        Tone = UiTone.Accent,
                        Icon = "map"
                    }
                ],
                Facts = BuildWorldTimeItems(UnwrapWorldTimeNode(timeRead.Node))
                    .Select(static item => new UiEntityFact { Label = item.Key, Value = item.Value })
                    .ToList(),
                Sections = sections
            });
        }

        AddReadWarning(blocks, "Где я", locationRead);
        AddReadWarning(blocks, "Где я", timeRead);
        AddReadWarning(blocks, "Где я", weatherRead);

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
        var sections = new List<UiEntityDossierSection>();

        var timeBlocks = new List<UiBlock>();
        AddWorldTimeBlock(timeBlocks, timeRead.Node);
        AddDossierSectionIfAny(sections, "time", "Время", "Текущее игровое время.", "time", timeBlocks);

        var weatherBlocks = new List<UiBlock>();
        AddWeatherSummaryBlock(weatherBlocks, weather, includeBiome: true, currentLocation: location);
        AddDossierSectionIfAny(sections, "weather", "Погода", "Погодные условия и их игровые эффекты.", "weather", weatherBlocks);

        if (sections.Count > 0)
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "weather",
                Title = "Время и погода",
                Subtitle = location == null
                    ? "Окружение"
                    : FirstNonEmpty(GetLocationNodeString(location, "name", "locationName", "displayName"), "Окружение"),
                Summary = FirstNonEmpty(GetLocationNodeString(weather, "description"), "Текущее время и погодные условия."),
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = "Окружение",
                        Tone = UiTone.Accent,
                        Icon = "weather"
                    }
                ],
                Facts = BuildWorldTimeItems(UnwrapWorldTimeNode(timeRead.Node))
                    .Select(static item => new UiEntityFact { Label = item.Key, Value = item.Value })
                    .ToList(),
                Sections = sections
            });
        }
        else
        {
            blocks.Add(Message(UiNotificationSeverity.Info, "Время и погода", "Данные ещё не созданы."));
        }

        AddReadWarning(blocks, "Время и погода", timeRead);
        AddReadWarning(blocks, "Время и погода", weatherRead);
        AddReadWarning(blocks, "Время и погода", locationRead);

        return Completed(command, blocks);
    }

    private static void AddDossierSectionIfAny(
        List<UiEntityDossierSection> sections,
        string id,
        string title,
        string summary,
        string icon,
        IReadOnlyList<UiBlock> blocks)
    {
        if (blocks.Count == 0)
            return;

        sections.Add(new UiEntityDossierSection
        {
            Id = id,
            Title = title,
            Summary = summary,
            Icon = icon,
            Collapsible = true,
            InitiallyExpanded = true,
            Blocks = blocks.ToList()
        });
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

        blocks.Add(new UiEntityDossierBlock
        {
            EntityType = "location-faction-control",
            Title = "Контроль фракций",
            Subtitle = "Локальная власть",
            Summary = DescribeInventoryCount(rows.Count, "фракция", "фракции", "фракций"),
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "entries",
                    Title = "Фракции",
                    Icon = "factions",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = rows
                        .Select(row => (UiBlock)BuildReferenceRowDossier(
                            "location-faction-control-entry",
                            "Фракция",
                            ["Фракция", "Вид контроля", "Уровень"],
                            row,
                            "factions"))
                        .ToList()
                }
            ]
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

        blocks.Add(new UiEntityDossierBlock
        {
            EntityType = "location-threats",
            Title = "Активные угрозы",
            Subtitle = "Риски места",
            Summary = DescribeInventoryCount(rows.Count, "угроза", "угрозы", "угроз"),
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "entries",
                    Title = "Угрозы",
                    Icon = "warning",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = rows
                        .Select(row => (UiBlock)BuildReferenceRowDossier(
                            "location-threat",
                            "Угроза",
                            ["Угроза", "Опасность", "Что известно"],
                            row,
                            "warning"))
                        .ToList()
                }
            ]
        });
    }

    private static void AddLocationEventBlock(List<UiBlock> blocks, JsonObject location)
    {
        var events = GetLocationNodeString(location, "lastEventsDescription", "lastEvent", "recentEvents");
        if (string.IsNullOrWhiteSpace(events))
            return;

        blocks.Add(Panel("Последние события", new UiTextBlock
        {
            Text = ExplorerPlayerFacingLabels.WorldTime(events),
            Tone = UiTone.Default
        }));
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
        return string.Join("\n", parts);
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

        var summaryItems = new List<UiKeyValueItem>();
        foreach (var spec in definition.Specs)
        {
            var read = reads[spec.Path];
            var status = DescribeSpec(read, spec.PropertyName);
            if (status == "отсутствует")
                continue;

            summaryItems.Add(new UiKeyValueItem
            {
                Key = spec.Label,
                Value = status
            });
        }

        var blocks = new List<UiBlock>();
        var sections = new List<UiEntityDossierSection>();
        if (summaryItems.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "summary",
                Title = "Сводка",
                Summary = "Что уже отмечено в книге.",
                Icon = "reference",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiKeyValueGridBlock { Items = summaryItems }]
            });
        }

        if (entries.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "entries",
                    Title = "Записи",
                    Summary = "Известные записи этого раздела.",
                    Icon = "reference",
                    Presentation = "collection",
                    Collapsible = true,
                InitiallyExpanded = true,
                Blocks = entries
                    .Select(entry => (UiBlock)BuildReferenceOverviewCard(definition, entry, entries))
                    .ToList()
            });
        }

        if (sections.Count > 0)
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "reference-bundle",
                Title = definition.Title,
                Subtitle = "Обзор записей",
                Summary = entries.Count > 0
                    ? DescribeInventoryCount(entries.Count, "запись", "записи", "записей")
                    : "Записи этого раздела пока не найдены.",
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = entries.Count > 0
                            ? DescribeInventoryCount(entries.Count, "запись", "записи", "записей")
                            : "пусто",
                        Tone = entries.Count > 0 ? UiTone.Accent : UiTone.Muted,
                        Icon = "reference"
                    }
                ],
                Sections = sections
            });
        }
        else
        {
            blocks.Add(Message(UiNotificationSeverity.Info, definition.Title, "Данные ещё не созданы."));
        }

        AddReferenceReadWarnings(blocks, definition.Title, reads.Values);
        return Completed(command, blocks, BuildReferenceDetailActions(commandToken, definition, entries));
    }

    private static UiEntityDossierBlock BuildReferenceOverviewCard(
        ReferenceCommandDefinition definition,
        ReferenceEntrySnapshot entry,
        IReadOnlyList<ReferenceEntrySnapshot> entries)
    {
        if (string.Equals(definition.DetailTitlePrefix, "Фракция", StringComparison.OrdinalIgnoreCase))
            return BuildFactionReferenceOverviewCard(entry, entries);

        var facts = new List<UiKeyValueItem>
        {
            new() { Key = "Раздел", Value = entry.Section }
        };

        return new UiEntityDossierBlock
        {
            EntityType = "reference-entry",
            Title = entry.Title,
            Subtitle = entry.Section,
            Summary = FirstNonEmpty(entry.Summary, FirstReferenceNodeString(entry.Node, "description", "summary", "objective", "visibleReason", "scenarioCore")),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = entry.Section,
                    Tone = UiTone.Accent,
                    Icon = "reference"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "facts",
                    Title = "Кратко",
                    Icon = "reference",
                    Collapsible = true,
                    InitiallyExpanded = false,
                    Blocks = [new UiKeyValueGridBlock { Items = facts }]
                }
            ]
        };
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
                : BuildReferenceDetailPanel(commandToken, definition, entry, entries));
        }

        AddReferenceReadWarnings(blocks, definition.Title, reads);
        var actions = new List<UiAction>
        {
            new UiAction
            {
                Id = definition.ActionIdPrefix + "-back",
                Label = $"Назад: {definition.Title}",
                Command = commandToken,
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false
            }
        };

        if (request.Kind == ReferenceDetailKind.Detail &&
            string.Equals(definition.DetailTitlePrefix, "Локация", StringComparison.OrdinalIgnoreCase) &&
            FindReferenceEntry(entries, request.Selector) is { } locationEntry &&
            HasLocationStorages(locationEntry.Node))
        {
            actions.Insert(0, BuildLocationStoragesAction(commandToken, locationEntry));
        }

        return Completed(command, blocks, actions);
    }

    private static UiBlock BuildReferenceDetailPanel(
        string commandToken,
        ReferenceCommandDefinition definition,
        ReferenceEntrySnapshot entry,
        IReadOnlyList<ReferenceEntrySnapshot> entries)
    {
        if (string.Equals(definition.DetailTitlePrefix, "Фракция", StringComparison.OrdinalIgnoreCase))
            return BuildFactionReferenceDetailPanel(definition, entry, entries);
        if (string.Equals(definition.DetailTitlePrefix, "Локация", StringComparison.OrdinalIgnoreCase))
            return BuildLocationReferenceDetailPanel(commandToken, definition, entry);
        if (string.Equals(definition.DetailTitlePrefix, "Навык", StringComparison.OrdinalIgnoreCase))
            return BuildSkillReferenceDetailPanel(definition, entry);

        var detailItems = new List<UiKeyValueItem>
        {
            new() { Key = "Раздел", Value = entry.Section }
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

        var detailBlocks = new List<UiBlock> { new UiKeyValueGridBlock { Items = detailItems } };
        AddReferenceDetailSection(detailBlocks, "Награда", entry.Node["rewardInfo"] ?? entry.Node["rewards"] ?? entry.Node["reward"]);
        AddReferenceAdditionalDetailBlocks(detailBlocks, entry.Node);
        AddStructuredBonusBlock(detailBlocks, entry.Node["structuredBonuses"] as JsonArray);

        return new UiEntityDossierBlock
        {
            EntityType = "reference-detail",
            Title = $"{definition.DetailTitlePrefix}: {entry.Title}",
            Subtitle = entry.Section,
            Summary = FirstNonEmpty(entry.Summary, FirstReferenceNodeString(entry.Node, "description", "summary", "objective", "visibleReason", "scenarioCore")),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = entry.Section,
                    Tone = UiTone.Accent,
                    Icon = "reference"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "details",
                    Title = "Сведения",
                    Summary = "Ключевые сведения этой записи.",
                    Icon = "reference",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = detailBlocks
                }
            ]
        };
    }

    private static UiEntityDossierBlock BuildLocationReferenceDetailPanel(
        string commandToken,
        ReferenceCommandDefinition definition,
        ReferenceEntrySnapshot entry)
    {
        var location = entry.Node;
        var facts = new List<UiKeyValueItem>
        {
            new() { Key = "Раздел", Value = entry.Section }
        };

        AddLocationFact(facts, "Регион", GetLocationNodeString(location, "region"));
        AddLocationFact(facts, "Тип", TranslateLocationType(GetLocationNodeString(location, "locationType", "type")));
        AddLocationFact(facts, "Внутренний тип", TranslateIndoorLocationType(GetLocationNodeString(location, "indoorType")));
        AddLocationFact(facts, "Биом", TranslateLocationBiome(GetLocationNodeString(location, "biome")));
        AddLocationFact(facts, "Направление", TranslateLocationDirection(GetLocationNodeString(location, "direction")));
        AddLocationFact(facts, "Дистанция", GetLocationNodeString(location, "distance"));
        AddLocationFact(facts, "Путь", DescribeLinkState(GetLocationNodeString(location, "linkState")));
        AddLocationFact(facts, "Опасность", DescribeLocationDifficulty(location));

        var sections = new List<UiEntityDossierSection>();
        var descriptionBlocks = new List<UiBlock>();
        var description = FirstNonEmpty(
            GetLocationNodeString(location, "description", "shortDescription"),
            entry.Summary);
        if (!string.IsNullOrWhiteSpace(description))
            descriptionBlocks.Add(new UiTextBlock { Text = description, Tone = UiTone.Default });
        if (facts.Count > 0)
            descriptionBlocks.Add(new UiKeyValueGridBlock { Items = facts });
        AddDossierSectionIfAny(
            sections,
            "details",
            "Сведения",
            "Что известно об этом месте сейчас.",
            "map",
            descriptionBlocks);

        var exits = BuildLocationExitCards(location["adjacencyMap"] as JsonArray);
        if (exits.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "exits",
                Title = "Выходы",
                Summary = "Переходы из этого места и их состояние.",
                Icon = "map",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = exits
            });
        }

        var contextBlocks = new List<UiBlock>();
        AddLocationFeatureBlock(contextBlocks, location);
        AddLocationFactionControlBlock(contextBlocks, location);
        AddLocationThreatBlock(contextBlocks, location);
        AddLocationEventBlock(contextBlocks, location);
        AddDossierSectionIfAny(
            sections,
            "local-context",
            "Местный контекст",
            "Особенности, контроль, угрозы и недавние события.",
            "map",
            contextBlocks);

        if (HasLocationStorages(location))
        {
            var storageCount = EnumerateLocationStorages(location).Count();
            sections.Add(new UiEntityDossierSection
            {
                Id = "storages",
                Title = "Хранилища",
                Summary = "Сундуки, столы и тайники вынесены в отдельный просмотр, чтобы карточка места оставалась читаемой.",
                Icon = "inventory",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks =
                [
                    new UiEntityDossierBlock
                    {
                        EntityType = "location-storage-link",
                        Title = "Хранилища локации",
                        Subtitle = entry.Title,
                        Summary = DescribeInventoryCount(storageCount, "хранилище", "хранилища", "хранилищ"),
                        Badges =
                        [
                            new UiEntityBadge
                            {
                                Label = DescribeInventoryCount(storageCount, "хранилище", "хранилища", "хранилищ"),
                                Tone = UiTone.Accent,
                                Icon = "inventory"
                            }
                        ],
                        PrimaryAction = BuildLocationStoragesAction(commandToken, entry)
                    }
                ]
            });
        }

        return new UiEntityDossierBlock
        {
            EntityType = "location-detail",
            Title = $"{definition.DetailTitlePrefix}: {entry.Title}",
            Subtitle = entry.Section,
            Summary = description,
            Badges =
            [
                new UiEntityBadge
                {
                    Label = entry.Section,
                    Tone = UiTone.Accent,
                    Icon = "map"
                }
            ],
            Facts = facts
                .Select(static item => new UiEntityFact { Label = item.Key, Value = item.Value })
                .ToList(),
            PrimaryAction = HasLocationStorages(location) ? BuildLocationStoragesAction(commandToken, entry) : null,
            Sections = sections
        };
    }

    private static UiEntityDossierBlock BuildSkillReferenceDetailPanel(
        ReferenceCommandDefinition definition,
        ReferenceEntrySnapshot entry)
    {
        var skillType = TranslateSkillProtocolValue(FirstReferenceNodeString(entry.Node, "category", "type"));
        var detailItems = new List<UiKeyValueItem>
        {
            new() { Key = "Раздел", Value = entry.Section }
        };

        AddReferenceDetailItem(detailItems, "Тип", skillType);
        AddReferenceDetailItem(detailItems, "Группа", FirstReferenceNodeString(entry.Node, "group", "skillGroup", "school"));
        AddReferenceDetailItem(detailItems, "Уровень", FirstReferenceNodeString(entry.Node, "level", "skillLevel"));
        AddReferenceDetailItem(detailItems, "Мастерство", FirstReferenceNodeString(entry.Node, "masteryLevel", "mastery"));
        AddReferenceDetailItem(detailItems, "Контекст мастерства", FirstReferenceNodeString(entry.Node, "masteryContext", "context"));
        AddReferenceDetailItem(detailItems, "Масштабирование", FormatReferenceCharacteristic(FirstReferenceNodeString(entry.Node, "scalingCharacteristic")));
        AddReferenceDetailItem(detailItems, "Кратко", FirstNonEmpty(entry.Summary, FirstReferenceNodeString(entry.Node, "playerStatBonus", "summary")));

        var detailBlocks = new List<UiBlock>();
        var description = FirstReferenceNodeString(entry.Node, "skillDescription", "description", "details");
        if (!string.IsNullOrWhiteSpace(description))
        {
            detailBlocks.Add(new UiTextBlock
            {
                Text = description,
                Tone = UiTone.Default
            });
        }

        if (detailItems.Count > 0)
            detailBlocks.Add(new UiKeyValueGridBlock { Items = detailItems });

        var sections = new List<UiEntityDossierSection>();
        if (detailBlocks.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "overview",
                Title = "Сведения",
                Summary = "Основное описание навыка и его игровые параметры.",
                Icon = "skills",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = detailBlocks
            });
        }

        var actionRows = BuildSkillActionRows(entry.Node);
        if (actionRows.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "combat",
                Title = "Боевые свойства",
                Summary = "Параметры действия, которые важны при применении навыка.",
                Icon = "combat",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks =
                [
                    new UiKeyValueGridBlock
                    {
                        Items = actionRows
                            .Where(static row => row.Cells.Count >= 2)
                            .Select(static row => new UiKeyValueItem { Key = row.Cells[0], Value = row.Cells[1] })
                            .ToList()
                    }
                ]
            });
        }

        var bonusBlocks = new List<UiBlock>();
        AddStructuredBonusBlock(bonusBlocks, entry.Node["structuredBonuses"] as JsonArray);
        if (bonusBlocks.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "bonuses",
                Title = "Структурные бонусы",
                Summary = "Постоянные числовые эффекты навыка, разложенные по полям.",
                Icon = "skills",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = bonusBlocks
            });
        }

        return new UiEntityDossierBlock
        {
            EntityType = "skill",
            Title = $"{definition.DetailTitlePrefix}: {entry.Title}",
            Subtitle = FirstNonEmpty(skillType, "Навык"),
            Summary = FirstNonEmpty(entry.Summary, FirstReferenceNodeString(entry.Node, "skillDescription", "description", "playerStatBonus", "summary")),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = string.IsNullOrWhiteSpace(entry.Section) ? "Навык" : entry.Section,
                    Tone = UiTone.Accent,
                    Icon = "skills"
                }
            ],
            Sections = sections
        };
    }

    private static List<UiTableRow> BuildSkillActionRows(JsonObject node)
    {
        var rows = new List<UiTableRow>();
        AddSkillActionRow(rows, "Название действия", node["actionName"]);
        AddSkillActionRow(rows, "Описание действия", node["actionDescription"]);
        AddSkillActionRow(rows, "Активируемый эффект", node["isActivatedEffect"], FormatSkillBool);
        AddSkillActionRow(rows, "Тип урона", node["damageType"], TranslateSkillProtocolValue);
        AddSkillActionRow(rows, "Базовый урон", node["baseDamage"]);
        AddSkillActionRow(rows, "Дистанция", node["range"], TranslateSkillProtocolValue);
        AddSkillActionRow(rows, "Стоимость действия", node["actionCost"], TranslateSkillProtocolValue);
        AddSkillActionRow(rows, "Стоимость ОД", node["actionPointCost"]);
        AddSkillActionRow(rows, "Перезарядка", node["cooldown"], FormatSkillCooldown);
        AddSkillActionRow(rows, "Длительность", node["duration"]);
        AddSkillActionRow(rows, "Масштабирует значение", node["scalesValue"], FormatSkillBool);
        AddSkillActionRow(rows, "Масштабирует длительность", node["scalesDuration"], FormatSkillBool);
        AddSkillActionRow(rows, "Масштабирует шанс", node["scalesChance"], FormatSkillBool);
        return rows;
    }

    private static void AddSkillActionRow(
        List<UiTableRow> rows,
        string label,
        JsonNode? node,
        Func<string, string>? formatter = null)
    {
        if (!TryGetScalarString(node, out var value))
            return;

        var display = formatter == null
            ? StructuredBonusDisplay.FormatScalar(value)
            : formatter(value);
        if (string.IsNullOrWhiteSpace(display))
            return;

        rows.Add(new UiTableRow { Cells = [label, display] });
    }

    private static string FormatSkillBool(string value) =>
        value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            ? "да"
            : value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase)
                ? "нет"
                : StructuredBonusDisplay.FormatScalar(value);

    private static string FormatSkillCooldown(string value)
    {
        var clean = value.Trim();
        if ((clean is "0" or "0.0") || clean.Equals("none", StringComparison.OrdinalIgnoreCase))
            return "нет";

        return StructuredBonusDisplay.FormatScalar(clean);
    }

    private static string TranslateSkillProtocolValue(string value)
    {
        var clean = value.Trim();
        if (string.IsNullOrWhiteSpace(clean))
            return string.Empty;

        return clean.ToLowerInvariant() switch
        {
            "utility" => "утилитарный",
            "combat" => "боевой",
            "social" => "социальный",
            "exploration" => "исследование",
            "psychic" => "психическое воздействие",
            "piercing" => "колющий",
            "slashing" => "рубящий",
            "blunt" or "bludgeoning" => "дробящий",
            "melee" => "ближняя дистанция",
            "ranged" => "дальняя дистанция",
            "conversation" => "разговор",
            "main" => "основное действие",
            "fast" or "quick" => "быстрое действие",
            "free" => "свободное действие",
            _ => StructuredBonusDisplay.FormatScalar(clean)
        };
    }

    private static UiEntityDossierBlock BuildFactionReferenceOverviewCard(
        ReferenceEntrySnapshot entry,
        IReadOnlyList<ReferenceEntrySnapshot> entries)
    {
        var related = FindFactionRelatedEntries(entry, entries).ToList();
        var sections = BuildFactionSections(entry, related, includePowerProfile: false);
        var overviewItems = BuildFactionOverviewItems(entry, related);
        var summary = FirstNonEmpty(
            FirstReferenceNodeString(entry.Node, "description", "summary", "reputationDescription"),
            related.Select(static relatedEntry => FirstReferenceNodeString(relatedEntry.Node, "summary", "description")).FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
            entry.Summary);

        return new UiEntityDossierBlock
        {
            EntityType = "faction",
            Title = entry.Title,
            Subtitle = "Фракция",
            Summary = summary,
            Badges =
            [
                new UiEntityBadge
                {
                    Label = "Фракция",
                    Tone = UiTone.Accent,
                    Icon = "factions"
                }
            ],
            Facts = overviewItems
                .Select(static item => new UiEntityFact { Label = item.Key, Value = item.Value })
                .ToList(),
            Cards =
            [
                new UiEntityCard
                {
                    Title = entry.Title,
                    Subtitle = "Фракция",
                    Summary = summary,
                    Icon = "factions",
                    Facts = overviewItems
                        .Select(static item => new UiEntityFact { Label = item.Key, Value = item.Value })
                        .ToList()
                }
            ],
            Sections = sections
        };
    }

    private static UiEntityDossierBlock BuildFactionReferenceDetailPanel(
        ReferenceCommandDefinition definition,
        ReferenceEntrySnapshot entry,
        IReadOnlyList<ReferenceEntrySnapshot> entries)
    {
        var related = FindFactionRelatedEntries(entry, entries).ToList();
        var sections = BuildFactionSections(entry, related, includePowerProfile: true);

        return new UiEntityDossierBlock
        {
            EntityType = "faction",
            Title = $"{definition.DetailTitlePrefix}: {entry.Title}",
            Subtitle = entry.Section,
            Summary = FirstNonEmpty(
                FirstReferenceNodeString(entry.Node, "description", "summary"),
                entry.Summary),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = "Фракция",
                    Tone = UiTone.Accent,
                    Icon = "factions"
                }
            ],
            Facts = BuildFactionOverviewItems(entry, related)
                .Select(static item => new UiEntityFact { Label = item.Key, Value = item.Value })
                .ToList(),
            Sections = sections
        };
    }

    private static List<UiEntityDossierSection> BuildFactionSections(
        ReferenceEntrySnapshot entry,
        IReadOnlyList<ReferenceEntrySnapshot> related,
        bool includePowerProfile)
    {
        var detailItems = BuildFactionOverviewItems(entry, related);
        var sections = new List<UiEntityDossierSection>();
        if (detailItems.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "overview",
                Title = "Сведения",
                Summary = "Основное положение фракции и отношение к герою.",
                Icon = "factions",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiKeyValueGridBlock { Items = detailItems }]
            });
        }

        if (includePowerProfile)
        {
            var powerRows = BuildFactionPowerRows(entry.Node["powerProfile"]);
            if (powerRows.Count > 0)
            {
                sections.Add(new UiEntityDossierSection
                {
                    Id = "power",
                    Title = "Профиль силы",
                    Summary = "Как фракция распределяет влияние между военной, экономической и скрытой силой.",
                    Icon = "factions",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks =
                    [
                        new UiKeyValueGridBlock
                        {
                            Items = powerRows
                                .Where(static row => row.Cells.Count >= 2)
                                .Select(static row => new UiKeyValueItem { Key = row.Cells[0], Value = row.Cells[1] })
                                .ToList()
                        }
                    ]
                });
            }
        }

        var resourceRows = BuildFactionResourceRows(entry.Node["metaResources"] ?? entry.Node["resources"] ?? entry.Node["strategicGoods"]);
        if (resourceRows.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "resources",
                Title = "Ресурсы",
                Summary = "Запасы и поток ресурсов, которыми фракция может распоряжаться.",
                Icon = "factions",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = resourceRows
                    .Select(row => (UiBlock)BuildReferenceRowDossier(
                        "faction-resource",
                        "Ресурс",
                        ["Ресурс", "Запас", "Доход за ход", "Содержание за ход"],
                        row,
                        "factions"))
                    .ToList()
            });
        }

        var rankRows = BuildFactionRankRows(entry.Node["ranks"] ?? entry.Node["rankLadder"]);
        if (rankRows.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "ranks",
                Title = "Ранги и ветви",
                Summary = "Что дают ранги и ветви отношений внутри фракции.",
                Icon = "factions",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = rankRows
                    .Select(row => (UiBlock)BuildReferenceRowDossier(
                        "faction-rank",
                        "Ранг",
                        ["Ранг", "Ветвь", "Преимущества"],
                        row,
                        "factions"))
                    .ToList()
            });
        }

        var chronicleRows = BuildFactionChronicleRows(related);
        if (chronicleRows.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "chronicle",
                Title = "Хроника",
                Summary = "Записи о том, как фракция изменила отношение к герою и миру.",
                Icon = "factions",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = chronicleRows
                    .Select(row => (UiBlock)BuildReferenceRowDossier(
                        "faction-chronicle",
                        "Запись хроники",
                        ["Событие", "Когда", "Кратко"],
                        row,
                        "factions"))
                    .ToList()
            });
        }

        var projectRows = BuildFactionProjectRows(entry.Node["activeProjects"]);
        if (projectRows.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "projects",
                Title = "Проекты",
                Summary = "Текущие дела фракции, которые могут затронуть героя.",
                Icon = "factions",
                Collapsible = true,
                InitiallyExpanded = false,
                Blocks = projectRows
                    .Select(row => (UiBlock)BuildReferenceRowDossier(
                        "faction-project",
                        "Проект",
                        ["Проект", "Состояние", "Прогресс", "Что даст"],
                        row,
                        "factions"))
                    .ToList()
            });
        }

        return sections;
    }

    private static List<UiKeyValueItem> BuildFactionOverviewItems(
        ReferenceEntrySnapshot entry,
        IReadOnlyList<ReferenceEntrySnapshot> related)
    {
        var detailItems = new List<UiKeyValueItem>
        {
            new() { Key = "Раздел", Value = entry.Section }
        };

        var custom = related.FirstOrDefault(static relatedEntry =>
            relatedEntry.Node.ContainsKey("state") ||
            relatedEntry.Node.ContainsKey("publicStatus") ||
            relatedEntry.Node.ContainsKey("currentObjective"));

        AddReferenceDetailItem(detailItems, "Описание", FirstNonEmpty(FirstReferenceNodeString(entry.Node, "description", "summary"), entry.Summary));
        AddReferenceDetailItem(detailItems, "Состояние", FirstNonEmpty(
            DescribeReferenceStatus(FirstReferenceNodeString(entry.Node, "status", "state", "phase")),
            DescribeReferenceStatus(FirstReferenceNodeString(custom?.Node, "status", "state", "phase"))));
        AddReferenceDetailItem(detailItems, "Уровень", FirstReferenceNodeString(entry.Node, "level", "tier"));
        AddReferenceDetailItem(detailItems, "Репутация", DescribeFactionReputation(entry.Node));
        AddReferenceDetailItem(detailItems, "Отношение", FirstNonEmpty(
            FirstReferenceNodeString(entry.Node, "reputationDescription", "attitude", "publicStatus"),
            FirstReferenceNodeString(custom?.Node, "reputationDescription", "attitude", "publicStatus"),
            FirstReferenceNodeString(custom?.Node, "summary")));
        AddReferenceDetailItem(detailItems, "Ранг героя", FirstReferenceNodeString(entry.Node, "playerRank", "rankName"));
        AddReferenceDetailItem(detailItems, "Ветвь героя", FirstReferenceNodeString(entry.Node, "playerBranch", "branch"));
        AddReferenceDetailItem(detailItems, "Архетип развития", TranslateFactionDevelopmentArchetype(FirstReferenceNodeString(entry.Node, "developmentArchetype")));
        AddReferenceDetailItem(detailItems, "Сила фракции", FirstReferenceNodeString(entry.Node, "factionStrength", "strength", "power"));
        AddReferenceDetailItem(detailItems, "Цель", FirstNonEmpty(
            FirstReferenceNodeString(entry.Node, "currentObjective", "objective", "strategy", "playerStrategyDirective"),
            FirstReferenceNodeString(custom?.Node, "currentObjective", "objective", "strategy")));
        AddReferenceDetailItem(detailItems, "Ресурсы", PreviewRows(BuildFactionResourceRows(entry.Node["metaResources"] ?? entry.Node["resources"] ?? entry.Node["strategicGoods"])));
        AddReferenceDetailItem(detailItems, "Ранги", PreviewRows(BuildFactionRankRows(entry.Node["ranks"] ?? entry.Node["rankLadder"])));
        AddReferenceDetailItem(detailItems, "Хроника", PreviewRows(BuildFactionChronicleRows(related)));
        return detailItems;
    }

    private static string PreviewRows(IReadOnlyList<UiTableRow> rows)
    {
        if (rows.Count == 0)
            return string.Empty;

        return string.Join("\n", rows
            .Take(3)
            .Select(static row => string.Join(" — ", row.Cells.Where(static cell => !string.IsNullOrWhiteSpace(cell) && cell != "—"))));
    }

    private static UiEntityDossierBlock BuildReferenceRowDossier(
        string entityType,
        string subtitle,
        IReadOnlyList<string> columns,
        UiTableRow row,
        string icon = "reference")
    {
        var title = row.Cells.Count > 0 && !string.IsNullOrWhiteSpace(row.Cells[0])
            ? row.Cells[0]
            : subtitle;
        var facts = new List<UiKeyValueItem>();
        for (var index = 1; index < row.Cells.Count; index++)
        {
            var value = row.Cells[index];
            if (string.IsNullOrWhiteSpace(value))
                continue;

            facts.Add(new UiKeyValueItem
            {
                Key = index < columns.Count ? columns[index] : $"Поле {index + 1}",
                Value = value
            });
        }

        return new UiEntityDossierBlock
        {
            EntityType = entityType,
            Title = title,
            Subtitle = subtitle,
            Summary = facts.Count == 1 ? facts[0].Value : string.Empty,
            Sections = facts.Count == 0
                ? []
                :
                [
                    new UiEntityDossierSection
                    {
                        Id = "fields",
                        Title = "Сведения",
                        Icon = icon,
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks = [new UiKeyValueGridBlock { Items = facts }]
                    }
                ]
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
                if (property.Value is JsonArray resourceArray)
                {
                    foreach (var resource in resourceArray.OfType<JsonObject>())
                        AddFactionResourceRow(rows, property.Key, resource);
                }
                else if (property.Value is JsonObject resource)
                {
                    AddFactionResourceRow(rows, property.Key, resource);
                }
                else if (TryGetScalarString(property.Value, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    rows.Add(new UiTableRow { Cells = [TranslateFactionResourceKey(property.Key), value, string.Empty, string.Empty] });
                }
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
        var stock = FirstReferenceNodeString(resource, "currentStockpile", "currentStock", "stock", "amount", "balanceAfter", "quantity");
        var income = FirstReferenceNodeString(resource, "incomePerCycle", "incomePerTurn", "income", "delta");
        var upkeep = FirstReferenceNodeString(resource, "upkeepPerCycle", "upkeepPerTurn", "upkeep", "costPerTurn");

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
        var rows = new List<UiTableRow>();
        if (node is JsonObject obj)
        {
            if (obj["branches"] is JsonArray branches)
            {
                foreach (var branch in branches.OfType<JsonObject>())
                    AddFactionBranchRankRows(rows, branch);
            }
            else if (obj["ranks"] is JsonArray branchlessRanks)
            {
                AddFactionRankRows(rows, branchlessRanks, FirstReferenceNodeString(obj, "branchName", "displayName", "name"));
            }

            return rows;
        }

        if (node is not JsonArray array)
            return rows;

        AddFactionRankRows(rows, array, string.Empty);
        return rows;
    }

    private static void AddFactionBranchRankRows(List<UiTableRow> rows, JsonObject branch)
    {
        var branchName = FirstNonEmpty(
            FirstReferenceNodeString(branch, "branchName", "displayName", "name"),
            FirstReferenceNodeString(branch, "branchId"));
        if (branch["ranks"] is JsonArray ranks)
            AddFactionRankRows(rows, ranks, branchName);
    }

    private static void AddFactionRankRows(List<UiTableRow> rows, JsonArray array, string branchName)
    {
        foreach (var rank in array.OfType<JsonObject>())
        {
            var name = FirstNonEmpty(
                FirstReferenceNodeString(rank, "rankName", "rankNameMale", "rankNameFemale", "name", "title"),
                JoinReferenceDetails(
                    FirstReferenceNodeString(rank, "rankNameMale"),
                    FirstReferenceNodeString(rank, "rankNameFemale")));
            var branch = FirstNonEmpty(FirstReferenceNodeString(rank, "branch", "branchName"), branchName);
            var benefits = JoinReferenceDetails(
                DescribeNodeForReferenceDetail(rank["benefits"] ?? rank["perks"] ?? rank["permissions"]),
                FormatFactionRequiredReputation(FirstReferenceNodeString(rank, "requiredReputation")),
                FirstReferenceNodeString(rank, "unlockCondition"));
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
    }

    private static string FormatFactionRequiredReputation(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : $"нужно репутации: {value.Trim()}";

    private static List<UiTableRow> BuildFactionChronicleRows(IReadOnlyList<ReferenceEntrySnapshot> related)
    {
        var rows = new List<UiTableRow>();
        foreach (var entry in related)
        {
            if (!entry.Section.Contains("Хроник", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(FirstReferenceNodeString(entry.Node, "timestamp", "time", "date")))
            {
                continue;
            }

            var title = FirstNonEmpty(FirstReferenceNodeString(entry.Node, "title", "name"), entry.Title);
            var when = FirstReferenceNodeString(entry.Node, "timestamp", "time", "date");
            var summary = FirstNonEmpty(FirstReferenceNodeString(entry.Node, "summary", "description"), entry.Summary);
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(summary))
                continue;

            rows.Add(new UiTableRow { Cells = [EmptyFallback(title), EmptyFallback(when), EmptyFallback(summary)] });
        }

        return rows;
    }

    private static List<UiTableRow> BuildFactionProjectRows(JsonNode? node)
    {
        if (node is not JsonArray projects)
            return [];

        var rows = new List<UiTableRow>();
        foreach (var project in projects.OfType<JsonObject>())
        {
            var name = FirstReferenceNodeString(project, "projectName", "name", "title");
            var state = DescribeReferenceStatus(FirstReferenceNodeString(project, "activeState", "state", "status"));
            var progress = JoinReferenceDetails(
                FormatProgressPair(
                    FirstReferenceNodeString(project, "currentStep", "step"),
                    FirstReferenceNodeString(project, "totalSteps")),
                FormatProgressPair(
                    FirstReferenceNodeString(project, "timeSpentMinutes"),
                    FirstReferenceNodeString(project, "totalTimeCostMinutes"),
                    "мин."));
            var reward = FirstReferenceNodeString(project, "visibleReward", "reward", "outcome");
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(state) && string.IsNullOrWhiteSpace(progress) && string.IsNullOrWhiteSpace(reward))
                continue;

            rows.Add(new UiTableRow { Cells = [EmptyFallback(name), EmptyFallback(state), EmptyFallback(progress), EmptyFallback(reward)] });
        }

        return rows;
    }

    private static string FormatProgressPair(string current, string total, string unit = "")
    {
        if (string.IsNullOrWhiteSpace(current) && string.IsNullOrWhiteSpace(total))
            return string.Empty;

        var suffix = string.IsNullOrWhiteSpace(unit) ? string.Empty : " " + unit.Trim();
        if (!string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(total))
            return $"{current.Trim()}/{total.Trim()}{suffix}";

        return FirstNonEmpty(current, total) + suffix;
    }

    private static IEnumerable<ReferenceEntrySnapshot> FindFactionRelatedEntries(
        ReferenceEntrySnapshot source,
        IReadOnlyList<ReferenceEntrySnapshot> entries)
    {
        var aliases = BuildFactionAliasSet(source);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var entry in entries)
            {
                if (ReferenceEquals(entry.Node, source.Node))
                    continue;

                var candidateAliases = BuildFactionAliasSet(entry);
                if (!candidateAliases.Overlaps(aliases))
                    continue;

                foreach (var alias in candidateAliases)
                    changed |= aliases.Add(alias);
            }
        }

        foreach (var entry in entries)
        {
            if (ReferenceEquals(entry.Node, source.Node))
                continue;

            var candidateAliases = BuildFactionAliasSet(entry);
            if (candidateAliases.Overlaps(aliases))
                yield return entry;
        }
    }

    private static HashSet<string> BuildFactionAliasSet(ReferenceEntrySnapshot entry)
    {
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFactionAlias(aliases, entry.Title);
        AddFactionAlias(aliases, entry.Selector);
        foreach (var property in new[]
                 {
                     "factionId",
                     "initialId",
                     "id",
                     "key",
                     "name",
                     "factionName",
                     "displayName",
                     "title"
                 })
        {
            AddFactionAlias(aliases, FirstReferenceNodeString(entry.Node, property));
        }

        return aliases;
    }

    private static void AddFactionAlias(HashSet<string> aliases, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        var normalized = NormalizeReferenceSelector(value);
        if (!string.IsNullOrWhiteSpace(normalized) && normalized != "item")
            aliases.Add(normalized);
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
                var seenKey = string.Equals(definition.DetailTitlePrefix, "Фракция", StringComparison.OrdinalIgnoreCase)
                    ? spec.Label + ":" + selector + ":" + NormalizeReferenceSelector(title)
                    : selector;
                if (!seen.Add(seenKey))
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

    private static string DescribeReferenceSummary(JsonObject node)
    {
        var status = DescribeReferenceStatus(FirstReferenceNodeString(node, "status", "state", "stage", "phase", "accessLevel", "availability"));
        var summary = FirstReferenceNodeString(node, "summary", "description", "skillDescription", "objective", "visibleReason", "scenarioCore");
        if (!string.IsNullOrWhiteSpace(status) && !string.IsNullOrWhiteSpace(summary))
            return $"Состояние: {status}. {summary}";

        return FirstNonEmpty(
            status,
            summary,
            DescribeNodeForReferenceDetail(node["objectives"]));
    }

    private static void AddReferenceAdditionalDetailBlocks(
        List<UiBlock> blocks,
        JsonObject node,
        params string[] consumedFields)
    {
        var consumed = new HashSet<string>(consumedFields, StringComparer.OrdinalIgnoreCase);
        var scalarItems = new List<UiKeyValueItem>();
        foreach (var property in node)
        {
            if (consumed.Contains(property.Key) ||
                IsKnownReferenceDetailProperty(property.Key) ||
                IsTechnicalReferenceProperty(property.Key))
            {
                continue;
            }

            var label = DescribeReferenceNestedFieldLabel(property.Key);
            if (TryGetScalarString(property.Value, out var scalar))
            {
                var value = StructuredBonusDisplay.FormatScalar(scalar, property.Key);
                if (!string.IsNullOrWhiteSpace(value))
                    scalarItems.Add(new UiKeyValueItem { Key = label, Value = value });
                continue;
            }

            AddReferenceDetailSection(blocks, ToReferenceDetailSectionTitle(label, property.Key), property.Value);
        }

        if (scalarItems.Count > 0)
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "reference-extra",
                Title = "Дополнительные сведения",
                Subtitle = "Раздел",
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "fields",
                        Title = "Поля",
                        Icon = "reference",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks = [new UiKeyValueGridBlock { Items = scalarItems }]
                    }
                ]
            });
        }
    }

    private static void AddReferenceDetailSection(List<UiBlock> blocks, string title, JsonNode? node)
    {
        var sectionBlocks = BuildReferenceDetailBlocks(node);
        if (sectionBlocks.Count == 0)
            return;

        blocks.Add(new UiEntityDossierBlock
        {
            EntityType = "reference-section",
            Title = title,
            Subtitle = "Вложенный раздел",
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = StableId(title),
                    Title = "Сведения",
                    Icon = "reference",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = sectionBlocks
                }
            ]
        });
    }

    private static List<UiBlock> BuildReferenceDetailBlocks(JsonNode? node)
    {
        if (node == null)
            return [];

        if (TryGetScalarString(node, out var scalar))
        {
            var value = StructuredBonusDisplay.FormatScalar(scalar);
            return string.IsNullOrWhiteSpace(value)
                ? []
                : [new UiTextBlock { Text = value, Tone = UiTone.Default }];
        }

        if (node is JsonArray array)
        {
            var blocks = new List<UiBlock>();
            var listItems = new List<string>();
            var index = 0;
            foreach (var item in array)
            {
                index++;
                if (item is JsonObject itemObj)
                {
                    var card = BuildReferenceObjectCard(itemObj, $"Запись {index}");
                    if (card != null)
                        blocks.Add(card);
                    continue;
                }

                var value = DescribeNodeForReferenceDetail(item);
                if (!string.IsNullOrWhiteSpace(value))
                    listItems.Add(value);
            }

            if (listItems.Count > 0)
                blocks.Insert(0, new UiListBlock { Items = listItems });
            return blocks;
        }

        return node is JsonObject objectNode ? BuildReferenceObjectBlocks(objectNode) : [];
    }

    private static UiEntityDossierBlock? BuildReferenceObjectCard(JsonObject obj, string fallbackTitle)
    {
        var blocks = BuildReferenceObjectBlocks(obj);
        if (blocks.Count == 0)
            return null;

        var title = FirstNonEmpty(
            FirstReferenceNodeString(obj, "displayName", "displayNameOrMoniker", "name", "title", "questName", "stepTitle", "stepName", "npcName", "characterName", "itemName", "vehicleName"),
            fallbackTitle);
        return new UiEntityDossierBlock
        {
            EntityType = "reference-entry-detail",
            Title = title,
            Subtitle = "Запись",
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "fields",
                    Title = "Сведения",
                    Icon = "reference",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = blocks
                }
            ]
        };
    }

    private static List<UiBlock> BuildReferenceObjectBlocks(JsonObject obj)
    {
        var scalarItems = new List<UiKeyValueItem>();
        var nestedBlocks = new List<UiBlock>();
        foreach (var property in obj)
        {
            if (IsTechnicalReferenceProperty(property.Key))
                continue;

            var label = DescribeReferenceNestedFieldLabel(property.Key);
            if (TryGetScalarString(property.Value, out var scalar))
            {
                var value = StructuredBonusDisplay.FormatScalar(scalar, property.Key);
                if (!string.IsNullOrWhiteSpace(value))
                    scalarItems.Add(new UiKeyValueItem { Key = label, Value = value });
                continue;
            }

            AddReferenceDetailSection(nestedBlocks, ToReferenceDetailSectionTitle(label, property.Key), property.Value);
        }

        var blocks = new List<UiBlock>();
        if (scalarItems.Count > 0)
            blocks.Add(new UiKeyValueGridBlock { Items = scalarItems });
        blocks.AddRange(nestedBlocks);
        return blocks;
    }

    private static string ToReferenceDetailSectionTitle(string label, string propertyName)
    {
        var title = string.Equals(label, "деталь", StringComparison.OrdinalIgnoreCase)
            ? HumanizeReferenceKey(propertyName)
            : label.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = "Сведения";

        return char.ToUpperInvariant(title[0]) + title[1..];
    }

    private static string DescribeNodeForReferenceDetail(JsonNode? node, string? fieldName = null)
    {
        if (node == null)
            return string.Empty;

        if (TryGetScalarString(node, out var scalar))
            return StructuredBonusDisplay.FormatScalar(scalar, fieldName);

        if (node is JsonArray array)
            return string.Join("\n", array
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

            return string.Join("\n", parts);
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
            "available" => "доступно",
            "contested" => "оспаривается",
            "rising" => "нарастает",
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
        propertyName.StartsWith("_", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("hidden", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("internal", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("debug", StringComparison.OrdinalIgnoreCase) ||
        propertyName.Contains("secret", StringComparison.OrdinalIgnoreCase);

    private static string DescribeReferenceFieldLabel(string propertyName) =>
        propertyName switch
        {
            "objectives" => "задачи",
            "steps" or "questSteps" => "этапы",
            "stepTitle" or "stepName" => "этап",
            "objective" => "цель",
            "description" or "skillDescription" => "описание",
            "summary" => "кратко",
            "status" or "state" or "stage" or "phase" => "состояние",
            "questGiver" => "выдал",
            "rewardInfo" or "rewards" or "reward" => "награда",
            "visibleReward" => "награда",
            "failureConditions" or "failConditions" => "условия провала",
            "completionConditions" or "successConditions" => "условия завершения",
            "visibleClues" or "clues" or "evidence" => "улики",
            "relatedNpcRefs" or "relatedNpcs" or "relatedPeople" => "связанные лица",
            "recommendedActions" or "availableActions" or "playerOptions" => "возможные действия",
            "requirements" or "requiredItems" => "требования",
            "deadline" or "timeLimit" => "срок",
            "priority" => "приоритет",
            "difficulty" => "сложность",
            "risks" or "stakes" => "ставки",
            "notes" or "playerNotes" => "заметки",
            "progress" or "currentProgress" => "прогресс",
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
            "npcName" or "characterName" => "персонаж",
            "role" => "роль",
            "result" or "outcome" => "итог",
            "condition" => "условие",
            "isCompleted" => "выполнено",
            "isOptional" => "необязательно",
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
        string.Join("\n", values.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value!.Trim()));

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
            var value = ResolveSpecCandidate(obj, candidate);
            if (value != null)
                return value;
        }

        return null;
    }

    private static JsonNode? ResolveSpecCandidate(JsonObject root, string candidate)
    {
        JsonNode? current = root;
        foreach (var segment in candidate.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (current is not JsonObject obj ||
                !obj.TryGetPropertyValue(segment, out current) ||
                current == null)
            {
                return null;
            }
        }

        return current;
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

            foreach (var preferredChild in new[]
                     {
                         "activityUpdate",
                         "currentActivity",
                         "itemUpdate",
                         "skill",
                         "effect",
                         "relationshipData",
                         "relationshipLock"
                     })
            {
                if (!obj.TryGetPropertyValue(preferredChild, out var child) || child == null)
                    continue;

                var nested = PreviewSummaryNode(child);
                if (!string.IsNullOrWhiteSpace(nested))
                    return nested;
            }

            foreach (var child in obj)
            {
                if (IsTechnicalReferenceProperty(child.Key))
                    continue;

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

        var remainder = ExtractCommandRemainder(command);
        if (TryParseLocationStoragesRequest(remainder, out var storageSelector))
            return BuildLocationStoragesDetail(command, commandToken, definition, [currentRead, mapRead], entries, storageSelector);

        var request = ParseReferenceDetailRequest(remainder, definition);
        if (request.Kind != ReferenceDetailKind.Overview)
            return BuildReferenceDetail(command, commandToken, definition, [currentRead, mapRead], entries, request);

        var blocks = new List<UiBlock>();
        if (rows.Count > 0)
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "locations",
                Title = title,
                Subtitle = "Обзор мест",
                Summary = DescribeInventoryCount(rows.Count, "локация", "локации", "локаций"),
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = DescribeInventoryCount(rows.Count, "локация", "локации", "локаций"),
                        Tone = UiTone.Accent,
                        Icon = "map"
                    }
                ],
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "locations",
                        Title = "Доступные места",
                        Summary = "Текущая локация, соседние переходы и уже открытые места.",
                        Icon = "map",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks = entries.Select(static entry => (UiBlock)BuildLocationOverviewCard(entry)).ToList()
                    }
                ]
            });
        }
        else
        {
            blocks.Add(Message(UiNotificationSeverity.Info, title, "Локации пока не обнаружены."));
        }

        AddReferenceReadWarnings(blocks, title, [currentRead, mapRead]);
        return Completed(command, blocks, BuildReferenceDetailActions(commandToken, definition, entries));
    }

    private static UiEntityDossierBlock BuildLocationOverviewCard(ReferenceEntrySnapshot entry)
    {
        var section = entry.Section;
        var title = string.IsNullOrWhiteSpace(entry.Title) ? "Безымянная локация" : entry.Title;
        var location = entry.Node;
        var description = FirstNonEmpty(
            GetLocationNodeString(location, "description", "shortDescription"),
            IsAdjacentLocationSection(section) ? GetLocationNodeString(location, "shortDescription") : string.Empty,
            "Сведения о месте пока не уточнены.");
        var facts = new List<UiKeyValueItem>
        {
            new() { Key = "Раздел", Value = section }
        };
        AddLocationFact(facts, "Регион", GetLocationNodeString(location, "region"));
        AddLocationFact(facts, "Тип", TranslateLocationType(GetLocationNodeString(location, "locationType", "type")));
        AddLocationFact(facts, "Внутренний тип", TranslateIndoorLocationType(GetLocationNodeString(location, "indoorType")));
        AddLocationFact(facts, "Биом", TranslateLocationBiome(GetLocationNodeString(location, "biome")));
        AddLocationFact(facts, "Направление", TranslateLocationDirection(GetLocationNodeString(location, "direction")));
        AddLocationFact(facts, "Дистанция", GetLocationNodeString(location, "distance"));
        AddLocationFact(facts, "Путь", DescribeLinkState(GetLocationNodeString(location, "linkState")));

        return new UiEntityDossierBlock
        {
            EntityType = "location-summary",
            Title = title,
            Subtitle = section,
            Summary = description,
            Badges =
            [
                new UiEntityBadge
                {
                    Label = section,
                    Tone = UiTone.Accent,
                    Icon = "map"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "facts",
                    Title = "Кратко",
                    Icon = "map",
                    Collapsible = true,
                    InitiallyExpanded = false,
                    Blocks = [new UiKeyValueGridBlock { Items = facts }]
                }
            ]
        };
    }

    private static bool IsAdjacentLocationSection(string section) =>
        section.Contains("рядом", StringComparison.OrdinalIgnoreCase);

    private static ExplorerCommandResult BuildLocationStoragesDetail(
        string command,
        string commandToken,
        ReferenceCommandDefinition definition,
        IEnumerable<JsonReadResult> reads,
        IReadOnlyList<ReferenceEntrySnapshot> entries,
        string selector)
    {
        var blocks = new List<UiBlock>();
        var entry = FindReferenceEntry(entries, selector);
        if (entry == null)
        {
            blocks.Add(Message(UiNotificationSeverity.Warning, definition.NotFoundTitle, definition.NotFoundMessage));
        }
        else
        {
            var storages = EnumerateLocationStorages(entry.Node).ToList();
            if (storages.Count == 0)
            {
                blocks.Add(Message(UiNotificationSeverity.Info, "Хранилища", "У этой локации не отмечены доступные хранилища."));
            }
            else
            {
                blocks.Add(new UiEntityDossierBlock
                {
                    EntityType = "location-storages",
                    Title = $"Хранилища: {entry.Title}",
                    Subtitle = entry.Section,
                    Summary = DescribeInventoryCount(storages.Count, "хранилище", "хранилища", "хранилищ"),
                    Facts =
                    [
                        new UiEntityFact
                        {
                            Label = "Хранилища",
                            Value = string.Join("\n", storages.Select(GetLocationStorageDisplayName))
                        },
                        new UiEntityFact
                        {
                            Label = "Содержимое",
                            Value = PreviewLocationStorageContents(storages)
                        }
                    ],
                    Badges =
                    [
                        new UiEntityBadge
                        {
                            Label = DescribeInventoryCount(storages.Count, "хранилище", "хранилища", "хранилищ"),
                            Tone = UiTone.Accent,
                            Icon = "inventory"
                        }
                    ],
                    Sections =
                    [
                        new UiEntityDossierSection
                        {
                            Id = "storages",
                            Title = "Хранилища",
                            Summary = "Отдельный список столов, сундуков и тайников этой локации.",
                            Icon = "inventory",
                            Presentation = "collection",
                            Collapsible = true,
                            InitiallyExpanded = true,
                            Blocks = storages
                                .Select(storage => (UiBlock)BuildLocationStorageCard(storage))
                                .ToList()
                        }
                    ]
                });
            }
        }

        AddReferenceReadWarnings(blocks, definition.Title, reads);
        return Completed(command, blocks, [
            new UiAction
            {
                Id = definition.ActionIdPrefix + "-back-detail-" + ToActionIdPart(selector),
                Label = entry == null ? $"Назад: {definition.Title}" : $"Назад: {entry.Title}",
                Command = entry == null
                    ? commandToken
                    : BuildReferenceDetailCommand(commandToken, definition, entry.Selector),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false
            },
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

    private static bool TryParseLocationStoragesRequest(string remainder, out string selector)
    {
        selector = string.Empty;
        if (string.IsNullOrWhiteSpace(remainder))
            return false;

        var (kindToken, detailRemainder) = SplitFirstCombatArgument(remainder);
        var normalized = kindToken.Trim().ToLowerInvariant();
        if (normalized is not ("storage" or "storages" or "хранилище" or "хранилища" or "тайник" or "тайники"))
            return false;

        if (string.IsNullOrWhiteSpace(detailRemainder))
            return false;

        selector = NormalizeCombatSelector(detailRemainder);
        return true;
    }

    private static List<UiBlock> BuildLocationExitCards(JsonArray? exits)
    {
        if (exits == null)
            return [];

        var blocks = new List<UiBlock>();
        foreach (var exit in exits.OfType<JsonObject>())
        {
            var name = FirstNonEmpty(
                GetLocationNodeString(exit, "name", "targetLocationName"),
                GetLocationNodeString(exit, "targetLocationId"),
                "Выход");
            var facts = new List<UiKeyValueItem>();
            AddLocationFact(facts, "Описание", GetLocationNodeString(exit, "shortDescription", "description"));
            AddLocationFact(facts, "Направление", TranslateLocationDirection(GetLocationNodeString(exit, "direction")));
            AddLocationFact(facts, "Тип перехода", TranslateLocationLinkType(GetLocationNodeString(exit, "linkType", "type")));
            AddLocationFact(facts, "Состояние", FirstNonEmpty(
                DescribeLinkState(GetLocationNodeString(exit, "linkState")),
                "открыт"));
            AddLocationFact(facts, "Куда ведёт", GetLocationNodeString(exit, "targetLocationName", "targetLocationId"));

            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "location-exit",
                Title = name,
                Subtitle = "Выход",
                Summary = FirstNonEmpty(GetLocationNodeString(exit, "shortDescription", "description"), DescribeLinkState(GetLocationNodeString(exit, "linkState"))),
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = "Выход",
                        Tone = UiTone.Accent,
                        Icon = "map"
                    }
                ],
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "details",
                        Title = "Сведения",
                        Icon = "map",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks = facts.Count > 0 ? [new UiKeyValueGridBlock { Items = facts }] : []
                    }
                ]
            });
        }

        return blocks;
    }

    private static bool HasLocationStorages(JsonObject location) =>
        EnumerateLocationStorages(location).Any();

    private static IEnumerable<JsonObject> EnumerateLocationStorages(JsonObject location)
    {
        foreach (var storage in EnumerateLocationStorageArray(location["locationStorages"]))
            yield return storage;
        foreach (var storage in EnumerateLocationStorageArray(location["storages"]))
            yield return storage;
    }

    private static IEnumerable<JsonObject> EnumerateLocationStorageArray(JsonNode? node)
    {
        if (node is not JsonArray storages)
            yield break;

        foreach (var storage in storages.OfType<JsonObject>())
            yield return storage;
    }

    private static UiAction BuildLocationStoragesAction(string commandToken, ReferenceEntrySnapshot entry) =>
        new()
        {
            Id = "location-storages-" + ToActionIdPart(entry.Selector),
            Label = "Открыть хранилища: " + entry.Title,
            Command = BuildLocationStoragesCommand(commandToken, entry.Selector),
            Style = UiActionStyle.Secondary,
            RequiresConfirmation = false,
            Payload = new JsonObject
            {
                ["selector"] = entry.Selector,
                ["title"] = entry.Title
            }
        };

    private static string BuildLocationStoragesCommand(string commandToken, string selector)
    {
        var detailToken = string.Equals(commandToken, "/локации", StringComparison.OrdinalIgnoreCase)
            ? "хранилища"
            : "storages";
        return commandToken + " " + detailToken + " " + FormatCombatCommandArgument(selector);
    }

    private static string GetLocationStorageDisplayName(JsonObject storage) =>
        FirstNonEmpty(
            GetLocationNodeString(storage, "name", "storageName", "displayName"),
            "Хранилище");

    private static string PreviewLocationStorageContents(IReadOnlyList<JsonObject> storages)
    {
        var itemNames = storages
            .SelectMany(static storage => storage["contents"] is JsonArray contents
                ? contents.OfType<JsonObject>()
                : [])
            .Select(static item => FirstNonEmpty(
                GetLocationNodeString(item, "name", "itemName", "displayName"),
                "Предмет"))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Take(6)
            .ToList();

        return itemNames.Count == 0 ? "не отмечено" : string.Join("\n", itemNames);
    }

    private static UiEntityDossierBlock BuildLocationStorageCard(JsonObject storage)
    {
        var name = GetLocationStorageDisplayName(storage);
        var facts = new List<UiKeyValueItem>();
        AddLocationFact(facts, "Доступ", DescribeReferenceStatus(GetLocationNodeString(storage, "accessLevel", "access")));
        AddLocationFact(facts, "Полный доступ", FormatBooleanYesNo(storage["hasFullAccess"]));
        AddLocationFact(facts, "Описание", GetLocationNodeString(storage, "description", "summary"));

        var contents = BuildLocationStorageContentBlocks(storage["contents"] as JsonArray);
        var sections = new List<UiEntityDossierSection>
        {
            new()
            {
                Id = "details",
                Title = "Сведения",
                Icon = "inventory",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = facts.Count > 0 ? [new UiKeyValueGridBlock { Items = facts }] : []
            }
        };
        if (contents.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "contents",
                Title = "Содержимое",
                Summary = "Предметы, которые сейчас отмечены внутри хранилища.",
                Icon = "inventory",
                Presentation = "collection",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = contents
            });
        }

        return new UiEntityDossierBlock
        {
            EntityType = "location-storage",
            Title = name,
            Subtitle = "Хранилище",
            Summary = FirstNonEmpty(GetLocationNodeString(storage, "description", "summary"), DescribeInventoryCount(contents.Count, "предмет", "предмета", "предметов")),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = DescribeReferenceStatus(GetLocationNodeString(storage, "accessLevel", "access")),
                    Tone = UiTone.Accent,
                    Icon = "inventory"
                }
            ],
            Sections = sections
        };
    }

    private static List<UiBlock> BuildLocationStorageContentBlocks(JsonArray? contents)
    {
        if (contents == null)
            return [];

        var blocks = new List<UiBlock>();
        foreach (var item in contents.OfType<JsonObject>())
        {
            var name = FirstNonEmpty(GetLocationNodeString(item, "name", "itemName", "displayName"), "Предмет");
            var facts = new List<UiKeyValueItem>();
            AddLocationFact(facts, "Тип", GetLocationNodeString(item, "type"));
            AddLocationFact(facts, "Группа", GetLocationNodeString(item, "group"));
            AddLocationFact(facts, "Количество", FirstNonEmpty(GetLocationNodeString(item, "count", "quantity"), "1"));
            AddLocationFact(facts, "Описание", GetLocationNodeString(item, "description", "summary"));
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "location-storage-item",
                Title = name,
                Subtitle = FirstNonEmpty(GetLocationNodeString(item, "type"), "Предмет"),
                Summary = GetLocationNodeString(item, "description", "summary"),
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "details",
                        Title = "Сведения",
                        Icon = "inventory",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks = facts.Count > 0 ? [new UiKeyValueGridBlock { Items = facts }] : []
                    }
                ]
            });
        }

        return blocks;
    }

    private static string FormatBooleanYesNo(JsonNode? node)
    {
        if (node == null)
            return string.Empty;

        return node.GetValueKind() switch
        {
            JsonValueKind.True => "да",
            JsonValueKind.False => "нет",
            _ when TryGetScalarString(node, out var value) => StructuredBonusDisplay.FormatScalar(value),
            _ => string.Empty
        };
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
            TranslateLocationType(GetLocationNodeString(location, "locationType")),
            GetLocationNodeString(location, "description", "shortDescription"));

    private static string DescribeWorldMapLocation(JsonObject location) =>
        JoinLocationDetails(
            TranslateLocationType(GetLocationNodeString(location, "locationType")),
            TranslateIndoorLocationType(GetLocationNodeString(location, "indoorType")),
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

    private static string TranslateLocationType(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "indoor" => "помещение",
            "outdoor" => "открытая местность",
            "city" => "городская локация",
            "gate" => "ворота",
            "market" => "рынок",
            "district" => "квартал",
            "building" => "здание",
            "dungeon" => "подземелье",
            "cave" or "cavesystem" or "cave_system" => "пещерная система",
            "vehicle" => "транспорт",
            "uniqueindoor" or "unique_indoor" => "особое помещение",
            _ => StructuredBonusDisplay.FormatScalar(value)
        };

    private static string TranslateIndoorLocationType(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "building" => "здание",
            "dungeon" => "подземелье",
            "cavesystem" or "cave_system" => "пещерная система",
            "vehicle" => "транспорт",
            "uniqueindoor" or "unique_indoor" => "особое помещение",
            _ => StructuredBonusDisplay.FormatScalar(value)
        };

    private static string TranslateLocationBiome(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "urban" => "город",
            "forest" => "лес",
            "mountain" => "горы",
            "swamp" => "болото",
            "desert" => "пустошь",
            "coast" => "побережье",
            _ => StructuredBonusDisplay.FormatScalar(value)
        };

    private static string TranslateLocationLinkType(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "door" => "дверь",
            "hiddendoor" or "hidden_door" => "скрытая дверь",
            "stairs" => "лестница",
            "route" => "маршрут",
            "road" => "дорога",
            "bridge" => "мост",
            "portal" => "портал",
            _ => StructuredBonusDisplay.FormatScalar(value)
        };

    private static string TranslateLocationDirection(string value) =>
        value.Trim().ToLowerInvariant() switch
        {
            "" => string.Empty,
            "north" or "n" => "север",
            "south" or "s" => "юг",
            "east" or "e" => "восток",
            "west" or "w" => "запад",
            "up" => "вверх",
            "down" => "вниз",
            "inside" or "in" => "внутрь",
            "outside" or "out" => "наружу",
            _ => StructuredBonusDisplay.FormatScalar(value)
        };

    private static string JoinLocationDetails(params string[] parts) =>
        string.Join("\n", parts.Where(static part => !string.IsNullOrWhiteSpace(part)).Select(static part => part.Trim()));

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

        var sections = new List<UiEntityDossierSection>();
        if (entries.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "vehicles",
                Title = title,
                Summary = "Доступные средства перемещения и их текущее состояние.",
                Icon = "transport",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = entries.Select(static entry => (UiBlock)BuildTransportOverviewCard(entry)).ToList()
            });
        }

        var summaryItems = new List<UiKeyValueItem>();
        AddTransportSummaryItem(summaryItems, vehiclesRead, "vehicles|UpdateVehicles", "Транспорта");
        AddTransportSummaryItem(summaryItems, mapRead, "transportRoutes", "Маршрутов");
        AddTransportSummaryItem(summaryItems, currentRead, "availableTransport", "Доступного транспорта");
        if (summaryItems.Count > 0)
        {
            sections.Insert(0, new UiEntityDossierSection
            {
                Id = "summary",
                Title = "Сводка транспорта",
                Summary = "Короткая сводка по транспорту и маршрутам.",
                Icon = "transport",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiKeyValueGridBlock { Items = summaryItems }]
            });
        }

        if (sections.Count > 0)
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "transport",
                Title = title,
                Subtitle = "Средства перемещения",
                Summary = entries.Count > 0
                    ? DescribeInventoryCount(entries.Count, "средство", "средства", "средств")
                    : "Транспорт пока не обнаружен.",
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = entries.Count > 0
                            ? DescribeInventoryCount(entries.Count, "средство", "средства", "средств")
                            : "пусто",
                        Tone = entries.Count > 0 ? UiTone.Accent : UiTone.Muted,
                        Icon = "transport"
                    }
                ],
                Sections = sections
            });
        }
        else
        {
            blocks.Add(Message(UiNotificationSeverity.Info, title, "Транспорт пока не обнаружен."));
        }

        AddReferenceReadWarnings(blocks, title, [vehiclesRead, mapRead, currentRead]);
        return Completed(command, blocks, BuildReferenceDetailActions(commandToken, definition, entries));
    }

    private static UiEntityDossierBlock BuildTransportOverviewCard(ReferenceEntrySnapshot entry)
    {
        var vehicle = entry.Node;
        var title = string.IsNullOrWhiteSpace(entry.Title) ? "Транспорт" : entry.Title;
        var type = DescribeVehicleType(vehicle);
        var facts = new List<UiKeyValueItem>();
        AddReferenceDetailItem(facts, "Тип", type);
        AddReferenceDetailItem(facts, "Состояние", DescribeVehicleAvailability(vehicle));
        AddReferenceDetailItem(facts, "Местоположение", FirstReferenceNodeString(vehicle, "currentLocation", "currentLocationId", "locationName"));
        AddReferenceDetailItem(facts, "Маршрут", FirstReferenceNodeString(vehicle, "route", "currentRoute"));
        AddReferenceDetailItem(facts, "Вместимость", FirstReferenceNodeString(vehicle, "capacity"));
        AddReferenceDetailItem(facts, "Прочность", FormatVehicleHealth(vehicle));
        AddReferenceDetailItem(facts, "Описание", FirstReferenceNodeString(vehicle, "description", "summary", "notes"));

        return new UiEntityDossierBlock
        {
            EntityType = "transport-summary",
            Title = title,
            Subtitle = FirstNonEmpty(type, "Транспорт"),
            Summary = FirstNonEmpty(
                FirstReferenceNodeString(vehicle, "description", "summary", "notes"),
                DescribeVehicleAvailability(vehicle),
                "Подробности доступны в карточке транспорта."),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = FirstNonEmpty(type, "Транспорт"),
                    Tone = UiTone.Accent,
                    Icon = "transport"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "facts",
                    Title = "Кратко",
                    Icon = "transport",
                    Collapsible = true,
                    InitiallyExpanded = false,
                    Blocks = [new UiKeyValueGridBlock { Items = facts }]
                }
            ]
        };
    }

    private static string FormatVehicleHealth(JsonObject vehicle)
    {
        var current = FirstReferenceNodeString(vehicle, "currentHealth", "health");
        var max = FirstReferenceNodeString(vehicle, "maxHealth", "maxHp");
        if (!string.IsNullOrWhiteSpace(current) && !string.IsNullOrWhiteSpace(max))
            return $"{current}/{max}";

        return current;
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

    private static UiEntityDossierBlock BuildVehicleDetailPanel(ReferenceEntrySnapshot entry)
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

        var blocks = new List<UiBlock>
        {
            new UiKeyValueGridBlock { Items = items }
        };
        AddReferenceAdditionalDetailBlocks(
            blocks,
            vehicle,
            "name",
            "vehicleName",
            "type",
            "vehicleType",
            "availability",
            "status",
            "isActive",
            "currentLocation",
            "currentLocationId",
            "locationName",
            "capacity",
            "description",
            "summary",
            "notes");

        return new UiEntityDossierBlock
        {
            EntityType = "transport-detail",
            Title = $"Транспорт: {entry.Title}",
            Subtitle = DescribeVehicleType(vehicle),
            Summary = FirstNonEmpty(DescribeVehicleNode(vehicle), FirstReferenceNodeString(vehicle, "description", "summary", "notes")),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = DescribeVehicleType(vehicle),
                    Tone = UiTone.Accent,
                    Icon = "transport"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "details",
                    Title = "Сведения",
                    Summary = "Состояние транспорта и полезные игровые параметры.",
                    Icon = "transport",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = blocks
                }
            ]
        };
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

    private static void AddTransportSummaryItem(
        List<UiKeyValueItem> items,
        JsonReadResult read,
        string propertyName,
        string label)
    {
        var status = DescribeSpec(read, propertyName);
        if (status == "отсутствует")
            return;

        items.Add(new UiKeyValueItem { Key = label, Value = status });
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
        var sections = new List<UiEntityDossierSection>
        {
            new()
            {
                Id = "summary",
                Title = "Взаимодействия игроков",
                Summary = "Короткая сводка по игрокам и записям взаимодействий.",
                Icon = "interactions",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks =
                [
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            new UiKeyValueItem { Key = "Игроки", Value = DescribeCombatCount(players.Count, "игрок", "игрока", "игроков") },
                            new UiKeyValueItem { Key = "Записи", Value = DescribeCombatCount(records.Count, "запись", "записи", "записей") }
                        ]
                    }
                ]
            }
        };

        if (players.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "players",
                Title = "Игроки",
                Summary = "Игроки, с которыми есть видимые взаимодействия.",
                Icon = "interactions",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = players.Select(static player => (UiBlock)BuildInteractionPlayerOverviewCard(player)).ToList()
            });
        }

        if (records.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "records",
                Title = "Записи взаимодействий",
                Summary = "Последние видимые записи. Полные сведения открываются отдельной карточкой.",
                Icon = "interactions",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = records.Take(12).Select(static record => (UiBlock)BuildInteractionRecordOverviewCard(record)).ToList()
            });
        }

        var blocks = new List<UiBlock>();
        if (sections.Count > 0)
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "interactions",
                Title = "Взаимодействия игроков",
                Subtitle = "Совместные сцены",
                Summary = DescribeCombatCount(records.Count, "запись", "записи", "записей"),
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = DescribeCombatCount(players.Count, "игрок", "игрока", "игроков"),
                        Tone = players.Count > 0 ? UiTone.Accent : UiTone.Muted,
                        Icon = "interactions"
                    }
                ],
                Sections = sections
            });
        }

        AddInteractionReadWarnings(blocks, state);
        if (players.Count == 0 && records.Count == 0)
            blocks.Add(Message(UiNotificationSeverity.Info, "Взаимодействия игроков", "Данные взаимодействий ещё не созданы."));

        return Completed(command, blocks, BuildInteractionOverviewActions(commandToken, players, records));
    }

    private static UiEntityDossierBlock BuildInteractionPlayerOverviewCard(InteractionPlayerSnapshot player)
    {
        var facts = new List<UiKeyValueItem>();
        AddInteractionDetailItem(facts, "Связь / контекст", DescribeInteractionPlayerContext(player.Node));
        AddInteractionDetailItem(facts, "Состояние", DescribeInteractionPlayerStatus(player.Node));
        AddInteractionDetailItem(facts, "Записи", DescribeCombatCount(player.Records.Count, "запись", "записи", "записей"));

        return new UiEntityDossierBlock
        {
            EntityType = "interaction-player-summary",
            Title = player.Name,
            Subtitle = "Игрок",
            Summary = FirstNonEmpty(DescribeInteractionPlayerContext(player.Node), DescribeInteractionPlayerStatus(player.Node), "Есть видимые взаимодействия."),
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "facts",
                    Title = "Кратко",
                    Icon = "interactions",
                    Collapsible = true,
                    InitiallyExpanded = false,
                    Blocks = [new UiKeyValueGridBlock { Items = facts }]
                }
            ]
        };
    }

    private static UiEntityDossierBlock BuildInteractionRecordOverviewCard(InteractionRecordSnapshot record)
    {
        var facts = new List<UiKeyValueItem>
        {
            new() { Key = "Игрок", Value = record.PlayerName }
        };
        AddInteractionDetailItem(facts, "Состояние", DescribeInteractionRecordStatus(record.Node));

        return new UiEntityDossierBlock
        {
            EntityType = "interaction-record-summary",
            Title = record.Title,
            Subtitle = record.PlayerName,
            Summary = FirstNonEmpty(DescribeInteractionRecordSummary(record.Node), "Подробности доступны в карточке записи."),
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "facts",
                    Title = "Кратко",
                    Icon = "interactions",
                    Collapsible = true,
                    InitiallyExpanded = false,
                    Blocks = [new UiKeyValueGridBlock { Items = facts }]
                }
            ]
        };
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
                    blocks.Add(BuildInteractionPlayerDetailPanel(player));
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

    private static UiEntityDossierBlock BuildInteractionPlayerDetailPanel(InteractionPlayerSnapshot player)
    {
        var detailItems = new List<UiKeyValueItem>();

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
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "interaction-player-entries",
                Title = "Записи этого игрока",
                Subtitle = player.Name,
                Summary = DescribeCombatCount(player.Records.Count, "запись", "записи", "записей"),
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "entries",
                        Title = "Записи",
                        Icon = "interactions",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks = player.Records
                            .Select(static record => (UiBlock)BuildInteractionRecordOverviewCard(record))
                            .ToList()
                    }
                ]
            });
        }

        return new UiEntityDossierBlock
        {
            EntityType = "interaction-player",
            Title = $"Игрок: {player.Name}",
            Subtitle = "Взаимодействия",
            Summary = FirstNonEmpty(DescribeInteractionPlayerContext(player.Node), DescribeInteractionPlayerStatus(player.Node), "Видимая запись игрока."),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = DescribeCombatCount(player.Records.Count, "запись", "записи", "записей"),
                    Tone = player.Records.Count > 0 ? UiTone.Accent : UiTone.Muted,
                    Icon = "interactions"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "details",
                    Title = "Сведения",
                    Summary = "Контекст игрока и связанные записи.",
                    Icon = "interactions",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = blocks
                }
            ]
        };
    }

    private static UiEntityDossierBlock BuildInteractionRecordDetailPanel(InteractionRecordSnapshot record)
    {
        var detailItems = new List<UiKeyValueItem>
        {
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
        AddInteractionDetailItem(detailItems, "Метки", JoinNodeValues(record.Node["tags"]));

        var blocks = new List<UiBlock>
        {
            new UiKeyValueGridBlock { Items = detailItems }
        };
        AddInteractionAdditionalDetailBlocks(blocks, record.Node);

        return new UiEntityDossierBlock
        {
            EntityType = "interaction-record",
            Title = $"Запись взаимодействия: {record.Title}",
            Subtitle = record.PlayerName,
            Summary = FirstNonEmpty(DescribeInteractionRecordSummary(record.Node), DescribeInteractionRecordStatus(record.Node), "Видимая запись взаимодействия."),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = record.PlayerName,
                    Tone = UiTone.Accent,
                    Icon = "interactions"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "details",
                    Title = "Сведения",
                    Summary = "Контекст, участники, последствия и следующий шаг.",
                    Icon = "interactions",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = blocks
                }
            ]
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
            var name = BuildInteractionPlayerDisplayName(
                FirstInteractionNodeString(playerNode, "displayName", "playerName", "name", "characterName", "targetPlayerName"),
                key,
                index);
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
                clone["displayName"] = BuildInteractionPlayerDisplayName(string.Empty, key, 1);
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
                ["displayName"] = BuildInteractionPlayerDisplayName(string.Empty, key, 1),
                ["summary"] = scalar
            };
        }

        return null;
    }

    private static JsonObject WrapInteractionRecords(string key, JsonArray records) =>
        new()
        {
            ["playerId"] = key,
            ["displayName"] = BuildInteractionPlayerDisplayName(string.Empty, key, 1),
            ["records"] = records.DeepClone()
        };

    private static string BuildInteractionPlayerDisplayName(string candidate, string key, int index)
    {
        var value = FirstNonEmpty(candidate, key);
        if (string.IsNullOrWhiteSpace(value) || IsLikelyTechnicalIdentifier(value))
            return $"Игрок {Math.Max(1, index)}";

        return value;
    }

    private static bool IsLikelyTechnicalIdentifier(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.Contains('_', StringComparison.Ordinal) &&
            !trimmed.Contains("::", StringComparison.Ordinal))
        {
            return false;
        }

        return trimmed.All(static ch =>
            ch is '_' or '-' or ':' ||
            char.IsAsciiLetterLower(ch) ||
            char.IsAsciiDigit(ch));
    }

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

    private static void AddInteractionAdditionalDetailBlocks(List<UiBlock> blocks, JsonObject record)
    {
        var scalarItems = new List<UiKeyValueItem>();
        foreach (var property in record)
        {
            if (IsKnownInteractionRecordDetailProperty(property.Key) || IsTechnicalInteractionProperty(property.Key))
                continue;

            var label = DescribeInteractionNestedFieldLabel(property.Key);
            if (TryGetScalarString(property.Value, out var scalar))
            {
                if (!string.IsNullOrWhiteSpace(scalar))
                    scalarItems.Add(new UiKeyValueItem { Key = label, Value = scalar.Trim() });
                continue;
            }

            AddInteractionDetailSection(blocks, ToInteractionDetailSectionTitle(label, property.Key), property.Value);
        }

        if (scalarItems.Count > 0)
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "interaction-extra",
                Title = "Дополнительные сведения",
                Subtitle = "Раздел",
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "fields",
                        Title = "Поля",
                        Icon = "interactions",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks = [new UiKeyValueGridBlock { Items = scalarItems }]
                    }
                ]
            });
        }
    }

    private static void AddInteractionDetailSection(List<UiBlock> blocks, string title, JsonNode? node)
    {
        var sectionBlocks = BuildInteractionDetailBlocks(node);
        if (sectionBlocks.Count == 0)
            return;

        blocks.Add(new UiEntityDossierBlock
        {
            EntityType = "interaction-section",
            Title = title,
            Subtitle = "Вложенный раздел",
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = StableId(title),
                    Title = "Сведения",
                    Icon = "interactions",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = sectionBlocks
                }
            ]
        });
    }

    private static List<UiBlock> BuildInteractionDetailBlocks(JsonNode? node)
    {
        if (node == null)
            return [];

        if (TryGetScalarString(node, out var scalar))
            return string.IsNullOrWhiteSpace(scalar)
                ? []
                : [new UiTextBlock { Text = scalar.Trim(), Tone = UiTone.Default }];

        if (node is JsonArray array)
        {
            var blocks = new List<UiBlock>();
            var listItems = new List<string>();
            var index = 0;
            foreach (var item in array)
            {
                index++;
                if (item is JsonObject itemObj)
                {
                    var card = BuildInteractionObjectCard(itemObj, $"Запись {index}");
                    if (card != null)
                        blocks.Add(card);
                    continue;
                }

                var value = DescribeNodeForInteractionDetail(item);
                if (!string.IsNullOrWhiteSpace(value))
                    listItems.Add(value);
            }

            if (listItems.Count > 0)
                blocks.Insert(0, new UiListBlock { Items = listItems });
            return blocks;
        }

        return node is JsonObject objectNode ? BuildInteractionObjectBlocks(objectNode) : [];
    }

    private static UiEntityDossierBlock? BuildInteractionObjectCard(JsonObject obj, string fallbackTitle)
    {
        var blocks = BuildInteractionObjectBlocks(obj);
        if (blocks.Count == 0)
            return null;

        var title = FirstNonEmpty(
            FirstInteractionNodeString(obj, "displayName", "name", "title", "questName", "actionName", "itemName", "characterName", "npcName"),
            fallbackTitle);
        return new UiEntityDossierBlock
        {
            EntityType = "interaction-entry-detail",
            Title = title,
            Subtitle = "Запись",
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "fields",
                    Title = "Сведения",
                    Icon = "interactions",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = blocks
                }
            ]
        };
    }

    private static List<UiBlock> BuildInteractionObjectBlocks(JsonObject obj)
    {
        var scalarItems = new List<UiKeyValueItem>();
        var nestedBlocks = new List<UiBlock>();
        foreach (var property in obj)
        {
            if (IsTechnicalInteractionProperty(property.Key))
                continue;

            var label = DescribeInteractionNestedFieldLabel(property.Key);
            if (TryGetScalarString(property.Value, out var scalar))
            {
                if (!string.IsNullOrWhiteSpace(scalar))
                    scalarItems.Add(new UiKeyValueItem { Key = label, Value = scalar.Trim() });
                continue;
            }

            AddInteractionDetailSection(nestedBlocks, ToInteractionDetailSectionTitle(label, property.Key), property.Value);
        }

        var blocks = new List<UiBlock>();
        if (scalarItems.Count > 0)
            blocks.Add(new UiKeyValueGridBlock { Items = scalarItems });
        blocks.AddRange(nestedBlocks);
        return blocks;
    }

    private static string DescribeInteractionNestedFieldLabel(string propertyName)
    {
        var label = DescribeInteractionFieldLabel(propertyName);
        return string.Equals(label, "деталь", StringComparison.OrdinalIgnoreCase)
            ? StructuredBonusDisplay.FieldLabel(propertyName)
            : label;
    }

    private static string ToInteractionDetailSectionTitle(string label, string propertyName)
    {
        var title = string.Equals(label, "деталь", StringComparison.OrdinalIgnoreCase)
            ? HumanizeReferenceKey(propertyName)
            : label.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = "Сведения";

        return char.ToUpperInvariant(title[0]) + title[1..];
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
            return string.Join("\n", array.Select(DescribeNodeForInteractionDetail).Where(static part => !string.IsNullOrWhiteSpace(part)));

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

            return string.Join("\n", parts);
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
        string.Join("\n", values.Where(static value => !string.IsNullOrWhiteSpace(value)).Select(static value => value!.Trim()));

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
            BuildCombatOverviewDossier(enemies, allies, logEntries)
        };

        AddCombatReadWarnings(blocks, state);

        if (blocks.Count == 1 && enemies.Count == 0 && allies.Count == 0 && logEntries.Count == 0)
            blocks.Add(Message(UiNotificationSeverity.Info, "Бой", "Нет данных о бое. Вы не в сражении."));

        return Completed(command, blocks, BuildCombatOverviewActions(enemies, allies, logEntries));
    }

    private static UiEntityDossierBlock BuildCombatOverviewDossier(
        IReadOnlyList<CombatantSnapshot> enemies,
        IReadOnlyList<CombatantSnapshot> allies,
        IReadOnlyList<CombatLogSnapshot> logEntries)
    {
        var sections = new List<UiEntityDossierSection>
        {
            new()
            {
                Id = "summary",
                Title = "Сводка",
                Summary = "Кто сейчас участвует в бою и сколько записей уже есть в журнале.",
                Icon = "combat",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks =
                [
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            new UiKeyValueItem { Key = "Враги", Value = DescribeCombatCount(enemies.Count, "враг", "врага", "врагов") },
                            new UiKeyValueItem { Key = "Союзники", Value = DescribeCombatCount(allies.Count, "союзник", "союзника", "союзников") },
                            new UiKeyValueItem { Key = "Боевой журнал", Value = DescribeCombatCount(logEntries.Count, "запись", "записи", "записей") }
                        ]
                    }
                ]
            }
        };

        if (enemies.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "enemies",
                Title = "Враги",
                Summary = "Краткие карточки противников. Подробности открываются отдельным действием.",
                Icon = "combat",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = enemies.Select(static combatant => (UiBlock)BuildCombatantOverviewCard(combatant, "Враг")).ToList()
            });
        }

        if (allies.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "allies",
                Title = "Союзники",
                Summary = "Союзники и их текущие намерения.",
                Icon = "character",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = allies.Select(static combatant => (UiBlock)BuildCombatantOverviewCard(combatant, "Союзник")).ToList()
            });
        }

        if (logEntries.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "combat-log",
                Title = "Боевой журнал",
                Summary = "Последние записи боя. Полная запись открывается отдельным действием.",
                Icon = "book",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = logEntries.Take(10).Select(static entry => (UiBlock)BuildCombatLogOverviewCard(entry)).ToList()
            });
        }

        return new UiEntityDossierBlock
        {
            EntityType = "combat-overview",
            Title = "Боевая обстановка",
            Subtitle = enemies.Count > 0 ? "Сражение активно" : "Обзор боя",
            Summary = enemies.Count > 0
                ? "Здесь собраны участники текущего столкновения, их намерения и последние события боя."
                : "Данных о текущем сражении пока нет.",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = DescribeCombatCount(enemies.Count, "враг", "врага", "врагов"),
                    Tone = enemies.Count > 0 ? UiTone.Warning : UiTone.Muted,
                    Icon = "combat"
                },
                new UiEntityBadge
                {
                    Label = DescribeCombatCount(allies.Count, "союзник", "союзника", "союзников"),
                    Tone = allies.Count > 0 ? UiTone.Success : UiTone.Muted,
                    Icon = "character"
                }
            ],
            Sections = sections
        };
    }

    private static UiEntityDossierBlock BuildCombatantOverviewCard(CombatantSnapshot combatant, string role)
    {
        var facts = BuildCombatantFacts(combatant);
        return new UiEntityDossierBlock
        {
            EntityType = "combatant-summary",
            Title = combatant.Name,
            Subtitle = role,
            Summary = FirstNonEmpty(DescribeCombatantIntent(combatant.Node), DescribeCombatantOverview(combatant), "Подробности доступны в карточке участника боя."),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = role,
                    Tone = combatant.Kind == CombatantKind.Enemy ? UiTone.Warning : UiTone.Success,
                    Icon = combatant.Kind == CombatantKind.Enemy ? "combat" : "character"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "state",
                    Title = "Состояние",
                    Icon = "combat",
                    Collapsible = true,
                    InitiallyExpanded = false,
                    Blocks = [new UiKeyValueGridBlock { Items = facts }]
                }
            ]
        };
    }

    private static UiEntityDossierBlock BuildCombatLogOverviewCard(CombatLogSnapshot entry)
    {
        return new UiEntityDossierBlock
        {
            EntityType = "combat-log-summary",
            Title = entry.Title,
            Subtitle = "Запись боя",
            Summary = FirstNonEmpty(entry.Summary, entry.Result, "Подробности доступны в записи боя."),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = "журнал",
                    Tone = UiTone.Accent,
                    Icon = "book"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "summary",
                    Title = "Кратко",
                    Icon = "book",
                    Collapsible = true,
                    InitiallyExpanded = false,
                    Blocks =
                    [
                        new UiKeyValueGridBlock
                        {
                            Items =
                            [
                                new UiKeyValueItem { Key = "Событие", Value = EmptyFallback(entry.Summary) },
                                new UiKeyValueItem { Key = "Итог", Value = EmptyFallback(entry.Result) }
                            ]
                        }
                    ]
                }
            ]
        };
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
                    blocks.Add(BuildCombatantDetailDossier(enemy, "Враг"));
                break;
            }
            case CombatDetailKind.Ally:
            {
                var ally = FindCombatant(allies, request.Selector);
                if (ally == null)
                    blocks.Add(Message(UiNotificationSeverity.Warning, "Союзник не найден", "Такой союзник не отмечен в текущей боевой обстановке."));
                else
                    blocks.Add(BuildCombatantDetailDossier(ally, "Союзник"));
                break;
            }
            case CombatDetailKind.Log:
            {
                var entry = FindCombatLogEntry(logEntries, request.Selector);
                if (entry == null)
                    blocks.Add(Message(UiNotificationSeverity.Warning, "Запись боя не найдена", "Такая запись не найдена в боевом журнале."));
                else
                    blocks.Add(BuildCombatLogDetailDossier(entry));
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

    private static UiEntityDossierBlock BuildCombatantDetailDossier(CombatantSnapshot combatant, string titlePrefix)
    {
        var sections = new List<UiEntityDossierSection>
        {
            new()
            {
                Id = "state",
                Title = "Состояние",
                Summary = "Ключевые боевые параметры участника.",
                Icon = "combat",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiKeyValueGridBlock { Items = BuildCombatantFacts(combatant) }]
            }
        };

        var effectCards = BuildCombatEffectCards(combatant.Node);
        if (effectCards.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "effects",
                Title = "Эффекты",
                Summary = "Усиления, помехи и статусы, влияющие на участника боя.",
                Icon = "effect",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = effectCards
            });
        }

        var actionCards = BuildCombatActionCards(combatant.Node);
        if (actionCards.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "actions",
                Title = "Действия",
                Summary = "Доступные или отмеченные боевые действия участника.",
                Icon = "combat",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = actionCards
            });
        }

        return new UiEntityDossierBlock
        {
            EntityType = "combatant",
            Title = $"{titlePrefix}: {combatant.Name}",
            Subtitle = titlePrefix,
            Summary = FirstNonEmpty(DescribeCombatantIntent(combatant.Node), FirstCombatNodeString(combatant.Node, "description", "notes", "summary")),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = titlePrefix,
                    Tone = combatant.Kind == CombatantKind.Enemy ? UiTone.Warning : UiTone.Success,
                    Icon = combatant.Kind == CombatantKind.Enemy ? "combat" : "character"
                }
            ],
            Sections = sections
        };
    }

    private static List<UiKeyValueItem> BuildCombatantFacts(CombatantSnapshot combatant)
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

        return detailItems;
    }

    private static UiEntityDossierBlock BuildCombatLogDetailDossier(CombatLogSnapshot entry)
    {
        var items = new List<UiKeyValueItem>
        {
            new() { Key = "Событие", Value = EmptyFallback(entry.Summary) }
        };

        if (!string.IsNullOrWhiteSpace(entry.Turn))
            items.Add(new UiKeyValueItem { Key = "Ход", Value = entry.Turn });
        if (!string.IsNullOrWhiteSpace(entry.Result))
            items.Add(new UiKeyValueItem { Key = "Итог", Value = entry.Result });

        var sections = new List<UiEntityDossierSection>
        {
            new()
            {
                Id = "summary",
                Title = "Событие",
                Summary = "Что произошло в этой записи боя.",
                Icon = "book",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiKeyValueGridBlock { Items = items }]
            }
        };

        if (entry.Participants.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "participants",
                Title = "Участники",
                Icon = "character",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiListBlock { Items = entry.Participants.ToList(), Ordered = false }]
            });
        }

        if (entry.Consequences.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "consequences",
                Title = "Последствия",
                Icon = "effect",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiListBlock { Items = entry.Consequences.ToList(), Ordered = false }]
            });
        }

        return new UiEntityDossierBlock
        {
            EntityType = "combat-log-entry",
            Title = $"Запись боя: {entry.Title}",
            Subtitle = "Боевой журнал",
            Summary = FirstNonEmpty(entry.Summary, entry.Result),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = "журнал",
                    Tone = UiTone.Accent,
                    Icon = "book"
                }
            ],
            Sections = sections
        };
    }

    private static List<UiBlock> BuildCombatEffectCards(JsonObject combatant) =>
        BuildCombatEffectRows(combatant)
            .Select(static row => (UiBlock)new UiEntityDossierBlock
            {
                EntityType = "combat-effect",
                Title = row.Cells.ElementAtOrDefault(1) ?? "Эффект",
                Subtitle = row.Cells.ElementAtOrDefault(0) ?? "Эффект",
                Summary = FirstNonEmpty(row.Cells.ElementAtOrDefault(4), row.Cells.ElementAtOrDefault(2), row.Cells.ElementAtOrDefault(3)),
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "fields",
                        Title = "Подробности",
                        Icon = "effect",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks =
                        [
                            new UiKeyValueGridBlock
                            {
                                Items =
                                [
                                    new UiKeyValueItem { Key = "Раздел", Value = EmptyFallback(row.Cells.ElementAtOrDefault(0) ?? string.Empty) },
                                    new UiKeyValueItem { Key = "Сила", Value = EmptyFallback(row.Cells.ElementAtOrDefault(2) ?? string.Empty) },
                                    new UiKeyValueItem { Key = "Длительность", Value = EmptyFallback(row.Cells.ElementAtOrDefault(3) ?? string.Empty) },
                                    new UiKeyValueItem { Key = "Источник", Value = EmptyFallback(row.Cells.ElementAtOrDefault(4) ?? string.Empty) }
                                ]
                            }
                        ]
                    }
                ]
            })
            .ToList();

    private static List<UiBlock> BuildCombatActionCards(JsonObject combatant) =>
        BuildCombatActionRows(combatant)
            .Select(static row => (UiBlock)new UiEntityDossierBlock
            {
                EntityType = "combat-action",
                Title = row.Cells.ElementAtOrDefault(0) ?? "Действие",
                Subtitle = "Боевой приём",
                Summary = FirstNonEmpty(row.Cells.ElementAtOrDefault(2), row.Cells.ElementAtOrDefault(1)),
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "fields",
                        Title = "Подробности",
                        Icon = "combat",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks =
                        [
                            new UiKeyValueGridBlock
                            {
                                Items =
                                [
                                    new UiKeyValueItem { Key = "Цена", Value = EmptyFallback(row.Cells.ElementAtOrDefault(1) ?? string.Empty) },
                                    new UiKeyValueItem { Key = "Эффект", Value = EmptyFallback(row.Cells.ElementAtOrDefault(2) ?? string.Empty) }
                                ]
                            }
                        ]
                    }
                ]
            })
            .ToList();

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

        return EmptyFallback(string.Join("\n", parts.Where(static part => !string.IsNullOrWhiteSpace(part))));
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
        var trimmed = value.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return (string.Empty, string.Empty);

        if (trimmed[0] == '"')
            return SplitQuotedCombatArgument(trimmed);

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1])
        };
    }

    private static (string First, string Remainder) SplitQuotedCombatArgument(string value)
    {
        var chars = new List<char>();
        var escaped = false;
        for (var i = 1; i < value.Length; i++)
        {
            var ch = value[i];
            if (escaped)
            {
                chars.Add(ch);
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                var first = new string(chars.ToArray());
                var remainder = i + 1 < value.Length ? value[(i + 1)..].TrimStart() : string.Empty;
                return (first, remainder);
            }

            chars.Add(ch);
        }

        return (value, string.Empty);
    }

    private static string DescribeCombatantOverview(CombatantSnapshot combatant)
    {
        var parts = new List<string>();
        var status = DescribeCombatantStatus(combatant.Node);
        if (!string.IsNullOrWhiteSpace(status))
            parts.Add("Состояние: " + status);

        var health = DescribeCombatantHealth(combatant.Node);
        if (!string.IsNullOrWhiteSpace(health))
            parts.Add("Здоровье: " + health);

        var poise = DescribeCombatantPoise(combatant.Node);
        if (!string.IsNullOrWhiteSpace(poise))
            parts.Add("Стойкость: " + poise);

        return EmptyFallback(string.Join(". ", parts));
    }

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
        string.Join("\n", parts.Where(static part => !string.IsNullOrWhiteSpace(part)).Select(static part => part.Trim()));

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
            blocks.Add(BuildBookShelfDossier(title, shelf));
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

    private static UiEntityDossierBlock BuildBookShelfDossier(string title, ReadableDocumentShelf shelf)
    {
        var documentCards = shelf.Items
            .Select(static item => (UiBlock)BuildBookShelfItemCard(item))
            .ToList();

        return new UiEntityDossierBlock
        {
            EntityType = "document-shelf",
            Title = title,
            Subtitle = "Документы и книги",
            Summary = "Выберите документ из действий, чтобы открыть полный текст.",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = DescribeInventoryCount(shelf.Items.Count, "документ", "документа", "документов"),
                    Tone = UiTone.Accent,
                    Icon = "book"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "documents",
                    Title = "Документы",
                    Summary = "Краткие карточки показывают источник, доступ и объем текста без раскрытия полного содержимого.",
                    Icon = "book",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = documentCards
                }
            ]
        };
    }

    private static UiEntityDossierBlock BuildBookShelfItemCard(ReadableDocumentShelfItem item) =>
        new()
        {
            EntityType = "document-summary",
            Title = item.Title,
            Subtitle = item.Source,
            Summary = string.Join(". ", new[] { item.AccessStatus, item.CountLabel, item.Summary }
                .Where(static value => !string.IsNullOrWhiteSpace(value))),
            Badges =
            [
                new UiEntityBadge
                {
                    Label = item.AccessStatus,
                    Tone = item.HasReadableContent ? UiTone.Success : UiTone.Warning,
                    Icon = "book"
                }
            ]
        };

    private static UiEntityDossierBlock BuildBookDetailBlock(ReadableDocumentShelfItem item)
    {
        var sections = new List<UiEntityDossierSection>
        {
            new()
            {
                Id = "facts",
                Title = "Сведения",
                Summary = "Откуда взят документ и доступен ли полный текст.",
                Icon = "book",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks =
                [
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            new UiKeyValueItem { Key = "Источник", Value = item.Source },
                            new UiKeyValueItem { Key = "Доступ", Value = item.AccessStatus }
                        ]
                    }
                ]
            }
        };

        var contentBlocks = new List<UiBlock>();
        if (item.HasReadableContent)
        {
            for (var index = 0; index < item.Entries.Count; index++)
            {
                var prefix = item.Entries.Count == 1
                    ? string.Empty
                    : $"Запись {index + 1}. ";
                contentBlocks.Add(new UiTextBlock { Text = prefix + item.Entries[index], Tone = UiTone.Default });
            }
        }
        else
        {
            contentBlocks.Add(new UiMessageBlock
            {
                Severity = UiNotificationSeverity.Warning,
                Title = "Текст недоступен",
                Message = item.UnreadableReason ?? "Текст пока недоступен."
            });
        }

        sections.Add(new UiEntityDossierSection
        {
            Id = "content",
            Title = "Текст",
            Summary = item.HasReadableContent ? "Содержимое выбранного документа." : "Причина, по которой документ пока нельзя прочитать.",
            Icon = "book",
            Collapsible = true,
            InitiallyExpanded = true,
            Blocks = contentBlocks
        });

        return new UiEntityDossierBlock
        {
            EntityType = "document",
            Title = $"Чтение: {item.Title}",
            Subtitle = item.Source,
            Summary = item.Summary,
            Badges =
            [
                new UiEntityBadge
                {
                    Label = item.AccessStatus,
                    Tone = item.HasReadableContent ? UiTone.Success : UiTone.Warning,
                    Icon = "book"
                }
            ],
            Sections = sections
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

        var sections = new List<UiEntityDossierSection>();
        var inventoryFacts = new List<UiKeyValueItem>();
        var totalWeight = GetNodeString(root, "totalWeight");
        var maxWeight = GetNodeString(root, "maxWeight");
        if (!string.IsNullOrEmpty(totalWeight))
        {
            var weightText = !string.IsNullOrEmpty(maxWeight)
                ? $"{totalWeight} / {maxWeight}"
                : totalWeight;
            inventoryFacts.Add(new UiKeyValueItem { Key = "⚖ Нагрузка", Value = weightText });
        }

        var money = GetNodeString(root, "money");
        if (!string.IsNullOrEmpty(money) && money != "0")
            inventoryFacts.Add(new UiKeyValueItem { Key = "💰 Деньги", Value = money });

        if (inventoryFacts.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "inventory-summary",
                Title = "Сводка",
                Summary = "Общая нагрузка и переносимые ценности.",
                Icon = "inventory",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiKeyValueGridBlock { Items = inventoryFacts }]
            });
        }

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
            {
                sections.Add(new UiEntityDossierSection
                {
                    Id = "resources",
                    Title = "Ресурсы",
                    Summary = "Материалы и валюты, доступные персонажу.",
                    Icon = "inventory",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = [new UiKeyValueGridBlock { Items = resourceItems }]
                });
            }
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
            {
                sections.Add(new UiEntityDossierSection
                {
                    Id = "equipment",
                    Title = "Экипировка",
                    Summary = "Что сейчас надето или закреплено в слотах.",
                    Icon = "inventory",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = [new UiKeyValueGridBlock { Items = equipmentRows }]
                });
            }
        }

        if (root["items"] is JsonArray itemsArray && itemsArray.Count > 0)
        {
            var itemCards = new List<UiBlock>();
            foreach (var item in itemsArray)
            {
                if (item == null)
                    continue;

                if (item is JsonObject obj)
                    itemCards.Add(BuildInventoryOverviewItemCard(commandToken, obj));
            }

            if (itemCards.Count > 0)
            {
                sections.Add(new UiEntityDossierSection
                {
                    Id = "items",
                    Title = "Предметы",
                    Summary = $"{itemCards.Count} предметов в инвентаре.",
                    Icon = "inventory",
                    Presentation = "collection",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = itemCards
                });
            }
        }
        else
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "items",
                Title = "Предметы",
                Icon = "inventory",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiTextBlock { Text = "Инвентарь пуст.", Tone = UiTone.Muted }]
            });
        }

        if (sections.Count > 0)
        {
            blocks.Add(new UiEntityDossierBlock
            {
                EntityType = "inventory",
                Title = "Инвентарь",
                Subtitle = "Снаряжение и переносимые вещи",
                Summary = "Здесь собраны ресурсы, экипировка и предметы, доступные персонажу.",
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = root["items"] is JsonArray itemArray
                            ? DescribeInventoryCount(itemArray.Count, "предмет", "предмета", "предметов")
                            : "без предметов",
                        Tone = UiTone.Accent,
                        Icon = "inventory"
                    }
                ],
                Sections = sections
            });
        }

        return Completed(command, blocks, BuildInventoryActions(commandToken, inventoryContext));
    }

    private static UiEntityDossierBlock BuildInventoryOverviewItemCard(string commandToken, JsonObject item)
    {
        var identity = FirstNonEmpty(GetInventoryItemIdentity(item), GetInventoryItemName(item));
        var name = GetNodeString(item, "name") ?? GetNodeString(item, "itemName") ?? "???";
        var type = FormatInventoryProtocolValue(GetNodeString(item, "type") ?? string.Empty);
        var quality = FormatInventoryProtocolValue(FirstNonEmpty(GetNodeString(item, "quality"), GetNodeString(item, "rarity")));
        var quantity = FirstNonEmpty(GetNodeString(item, "count"), GetNodeString(item, "quantity"), "1");
        var durability = GetNodeString(item, "durability") ?? string.Empty;
        var description = FirstNonEmpty(GetNodeString(item, "description"), GetNodeString(item, "lore"));
        var facts = new List<UiKeyValueItem>
        {
            new() { Key = "Тип", Value = string.IsNullOrWhiteSpace(type) ? "предмет" : type },
            new() { Key = "Количество", Value = string.IsNullOrWhiteSpace(quantity) ? "1" : quantity }
        };

        AddInventoryFact(facts, "Качество", quality);
        AddInventoryFact(facts, "Вес", FormatInventoryMeasure(GetNodeString(item, "weight"), "кг"));
        AddInventoryFact(facts, "Цена", GetNodeString(item, "price"));
        if (!string.IsNullOrWhiteSpace(durability))
            facts.Add(new UiKeyValueItem { Key = "Прочность", Value = durability });
        AddInventoryFact(facts, "Слот", FormatInventorySlot(FirstNonEmpty(
            GetNodeString(item, "equipmentSlot"),
            GetNodeString(item, "slot"),
            GetNodeString(item, "equipSlot"))));
        AddInventoryFact(facts, "Аксессуар для", GetNodeString(item, "accessoryForSlot"));
        AddInventoryFact(facts, "Группа", GetNodeString(item, "group"));

        var broken = item["isBroken"]?.GetValueKind() == JsonValueKind.True;
        var empty = item["isEmpty"]?.GetValueKind() == JsonValueKind.True;
        if (!string.IsNullOrEmpty(durability))
        {
            var durabilityText = durability.Replace("%", string.Empty).Trim();
            if (int.TryParse(durabilityText, out var durabilityValue) && durabilityValue == 0)
                broken = true;
        }

        var status = broken
            ? "сломано"
            : empty
                ? "пусто"
                : "в порядке";
        facts.Add(new UiKeyValueItem { Key = "Состояние", Value = status });

        var summary = FirstNonEmpty(
            description,
            JoinCombatDetails(
                string.IsNullOrWhiteSpace(type) ? string.Empty : "Тип: " + type,
                string.IsNullOrWhiteSpace(quality) ? string.Empty : "Качество: " + quality,
                string.IsNullOrWhiteSpace(GetNodeString(item, "group")) ? string.Empty : "Группа: " + GetNodeString(item, "group")));

        return new UiEntityDossierBlock
        {
            EntityType = "inventory-item-summary",
            Title = name,
            Subtitle = string.IsNullOrWhiteSpace(type) ? "Предмет" : type,
            Summary = summary,
            Badges =
            [
                new UiEntityBadge
                {
                    Label = FirstNonEmpty(quality, status),
                    Tone = broken ? UiTone.Warning : UiTone.Accent,
                    Icon = "inventory"
                },
                new UiEntityBadge
                {
                    Label = status,
                    Tone = broken ? UiTone.Warning : UiTone.Success,
                    Icon = "inventory"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "facts",
                    Title = "Сведения",
                    Summary = "Ключевые свойства предмета для выбора и использования.",
                    Icon = "inventory",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = [new UiKeyValueGridBlock { Items = facts }]
                }
            ],
            PrimaryAction = new UiAction
            {
                Id = InventoryEquipmentService.BuildActionId("inventory-detail", identity),
                Label = $"Открыть отдельно: «{name}»",
                Command = BuildInventoryItemDetailCommand(commandToken, identity),
                Style = UiActionStyle.Secondary,
                RequiresConfirmation = false,
                Payload = new JsonObject
                {
                    ["itemIdentity"] = identity,
                    ["itemName"] = name,
                    ["itemType"] = type
                }
            }
        };
    }

    private static string DescribeInventoryCount(int count, string singular, string paucal, string plural)
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
        var sections = new List<UiEntityDossierSection>();
        var overviewBlocks = new List<UiBlock>();
        var description = FirstNonEmpty(GetNodeString(item, "description"), GetNodeString(item, "lore"));
        if (!string.IsNullOrWhiteSpace(description))
            overviewBlocks.Add(new UiTextBlock { Text = description, Tone = UiTone.Default });

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
            overviewBlocks.Add(new UiKeyValueGridBlock { Items = facts });

        if (overviewBlocks.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "overview",
                Title = "Сведения",
                Summary = "Основные свойства предмета, которые влияют на использование и экипировку.",
                Icon = "inventory",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = overviewBlocks
            });
        }

        AddInventorySummarySection(sections, "Бонусы", item["bonuses"]);
        AddInventorySummarySection(sections, "Эффекты", item["effects"]);
        AddInventorySummarySection(sections, "Особые свойства", item["specialProperties"]);
        AddStructuredBonusSection(sections, item["structuredBonuses"] as JsonArray);

        var detailBlocks = new List<UiBlock>();
        AddInventoryCombatEffectBlock(detailBlocks, item["combatEffect"]);
        AddInventoryCustomPropertiesBlock(detailBlocks, item["customProperties"]);
        AddInventoryContainerBlock(detailBlocks, item);
        AddInventoryDisassemblyBlock(detailBlocks, item["disassembleTo"] as JsonArray);
        AddInventoryResourceBlock(detailBlocks, item, sidecars.Resource);
        AddInventoryBondBlock(detailBlocks, sidecars.Bond);
        AddInventoryContentBlock(detailBlocks, item["textContent"], sidecars.Text?["textContent"]);
        AddInventoryJournalBlock(detailBlocks, item["journalEntries"], sidecars.Journal?["journalEntries"]);

        if (detailBlocks.Count > 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "details",
                Title = "Дополнительно",
                Summary = "Боевые, текстовые и служебные сведения предмета.",
                Icon = "inventory",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = detailBlocks
            });
        }

        if (sections.Count == 0)
        {
            sections.Add(new UiEntityDossierSection
            {
                Id = "empty",
                Title = "Сведения",
                Icon = "inventory",
                Collapsible = true,
                InitiallyExpanded = true,
                Blocks = [new UiTextBlock { Text = "Подробная информация по предмету пока не заполнена.", Tone = UiTone.Muted }]
            });
        }

        blocks.Add(new UiEntityDossierBlock
        {
            EntityType = "inventory-item",
            Title = $"Предмет: {itemName}",
            Subtitle = FormatInventoryProtocolValue(GetNodeString(item, "type") ?? "Предмет"),
            Summary = description,
            Badges =
            [
                new UiEntityBadge
                {
                    Label = FirstNonEmpty(FormatInventoryProtocolValue(FirstNonEmpty(GetNodeString(item, "quality"), GetNodeString(item, "rarity"))), "предмет"),
                    Tone = UiTone.Accent,
                    Icon = "inventory"
                }
            ],
            Sections = sections
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

    private static void AddInventorySummarySection(List<UiEntityDossierSection> sections, string title, JsonNode? node)
    {
        var items = EnumerateInventorySummaryTexts(node).ToList();
        if (items.Count == 0)
            return;

        sections.Add(new UiEntityDossierSection
        {
            Id = StableId(title),
            Title = title,
            Summary = "Краткое игровое описание эффектов предмета.",
            Icon = "inventory",
            Collapsible = true,
            InitiallyExpanded = true,
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
        var cards = BuildStructuredBonusCards(bonuses);
        if (cards.Count == 0)
            return;

        blocks.Add(new UiEntityDossierBlock
        {
            EntityType = "structured-bonuses",
            Title = "Структурные бонусы",
            Subtitle = "Механические свойства",
            Summary = "Каждый бонус показан отдельной карточкой, а его поля разложены по строкам.",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = DescribeInventoryCount(cards.Count, "бонус", "бонуса", "бонусов"),
                    Tone = UiTone.Success,
                    Icon = "inventory"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "bonuses",
                    Title = "Бонусы",
                    Icon = "inventory",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = cards
                }
            ]
        });
    }

    private static void AddStructuredBonusSection(List<UiEntityDossierSection> sections, JsonArray? bonuses)
    {
        var cards = BuildStructuredBonusCards(bonuses);

        if (cards.Count == 0)
            return;

        sections.Add(new UiEntityDossierSection
        {
            Id = "structured-bonuses",
            Title = "Структурные бонусы",
            Summary = "Механические бонусы предмета, разложенные по полям.",
            Icon = "inventory",
            Collapsible = true,
            InitiallyExpanded = true,
            Blocks = cards
        });
    }

    private static List<UiBlock> BuildStructuredBonusCards(JsonArray? bonuses)
    {
        if (bonuses == null || bonuses.Count == 0)
            return [];

        var cards = new List<UiBlock>();
        var index = 0;
        foreach (var bonus in bonuses)
        {
            index++;
            if (bonus is not JsonObject obj)
            {
                var value = FormatInventoryNodeValue(bonus);
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                cards.Add(new UiEntityDossierBlock
                {
                    EntityType = "structured-bonus",
                    Title = $"Бонус {index}",
                    Subtitle = "Структурный бонус",
                    Summary = value
                });
                continue;
            }

            var title = FirstNonEmpty(GetNodeString(obj, "summary"), $"Бонус {index}");
            var facts = obj
                .Select(property => new UiKeyValueItem
                {
                    Key = GetStructuredBonusFieldLabel(property.Key),
                    Value = FormatInventoryNodeValue(property.Value, property.Key)
                })
                .Where(static item => !string.IsNullOrWhiteSpace(item.Value))
                .ToList();
            if (facts.Count == 0)
                continue;

            cards.Add(new UiEntityDossierBlock
            {
                EntityType = "structured-bonus",
                Title = title,
                Subtitle = "Структурный бонус",
                Summary = FirstNonEmpty(GetNodeString(obj, "description"), GetNodeString(obj, "condition")),
                Badges =
                [
                    new UiEntityBadge
                    {
                        Label = FirstNonEmpty(FormatInventoryProtocolValue(GetNodeString(obj, "bonusType") ?? string.Empty), "бонус"),
                        Tone = UiTone.Success,
                        Icon = "inventory"
                    }
                ],
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "fields",
                        Title = "Поля бонуса",
                        Summary = "Каждое поле бонуса показано отдельно, без технической таблицы.",
                        Icon = "inventory",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks = [new UiKeyValueGridBlock { Items = facts }]
                    }
                ]
            });
        }

        return cards;
    }

    private static void AddInventoryCombatEffectBlock(List<UiBlock> blocks, JsonNode? node)
    {
        var rows = new List<UiTableRow>();
        AddInventoryCombatEffectRows(rows, node, "Эффект");
        if (rows.Count == 0)
            return;

        blocks.Add(new UiEntityDossierBlock
        {
            EntityType = "inventory-combat-effects",
            Title = "Боевые эффекты",
            Subtitle = "Боевые свойства",
            Summary = "Эффекты, которые предмет или состояние добавляет в бою.",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = DescribeInventoryCount(rows.Count, "эффект", "эффекта", "эффектов"),
                    Tone = UiTone.Warning,
                    Icon = "combat"
                }
            ],
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "effects",
                    Title = "Эффекты",
                    Icon = "combat",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = rows.Select(static row => (UiBlock)new UiEntityDossierBlock
                    {
                        EntityType = "inventory-combat-effect",
                        Title = row.Cells.ElementAtOrDefault(1) ?? "Эффект",
                        Subtitle = row.Cells.ElementAtOrDefault(0) ?? "Источник",
                        Summary = FirstNonEmpty(row.Cells.ElementAtOrDefault(3), row.Cells.ElementAtOrDefault(2)),
                        Sections =
                        [
                            new UiEntityDossierSection
                            {
                                Id = "fields",
                                Title = "Подробности",
                                Icon = "combat",
                                Collapsible = true,
                                InitiallyExpanded = true,
                                Blocks =
                                [
                                    new UiKeyValueGridBlock
                                    {
                                        Items =
                                        [
                                            new UiKeyValueItem { Key = "Источник", Value = EmptyFallback(row.Cells.ElementAtOrDefault(0) ?? string.Empty) },
                                            new UiKeyValueItem { Key = "Тип", Value = EmptyFallback(row.Cells.ElementAtOrDefault(1) ?? string.Empty) },
                                            new UiKeyValueItem { Key = "Значение", Value = EmptyFallback(row.Cells.ElementAtOrDefault(2) ?? string.Empty) },
                                            new UiKeyValueItem { Key = "Описание", Value = EmptyFallback(row.Cells.ElementAtOrDefault(3) ?? string.Empty) }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }).ToList()
                }
            ]
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
        return string.Join("\n", parts);
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
        return string.Join("\n", parts);
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

        if (rows.Count == 0)
            return;

        blocks.Add(new UiEntityDossierBlock
        {
            EntityType = "inventory-custom-properties",
            Title = "Особые свойства",
            Subtitle = "Контекстные эффекты",
            Summary = "Дополнительные правила взаимодействия предмета или состояния.",
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "properties",
                    Title = "Свойства",
                    Icon = "inventory",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = rows.Select(static row => (UiBlock)new UiEntityDossierBlock
                    {
                        EntityType = "inventory-custom-property",
                        Title = row.Cells.ElementAtOrDefault(0) ?? "Свойство",
                        Summary = FirstNonEmpty(row.Cells.ElementAtOrDefault(3), row.Cells.ElementAtOrDefault(2), row.Cells.ElementAtOrDefault(1)),
                        Sections =
                        [
                            new UiEntityDossierSection
                            {
                                Id = "fields",
                                Title = "Подробности",
                                Icon = "inventory",
                                Collapsible = true,
                                InitiallyExpanded = true,
                                Blocks =
                                [
                                    new UiKeyValueGridBlock
                                    {
                                        Items =
                                        [
                                            new UiKeyValueItem { Key = "Когда", Value = EmptyFallback(row.Cells.ElementAtOrDefault(0) ?? string.Empty) },
                                            new UiKeyValueItem { Key = "Цель", Value = EmptyFallback(row.Cells.ElementAtOrDefault(1) ?? string.Empty) },
                                            new UiKeyValueItem { Key = "Изменение", Value = EmptyFallback(row.Cells.ElementAtOrDefault(2) ?? string.Empty) },
                                            new UiKeyValueItem { Key = "Описание", Value = EmptyFallback(row.Cells.ElementAtOrDefault(3) ?? string.Empty) }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }).ToList()
                }
            ]
        });
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

        if (rows.Count == 0)
            return;

        blocks.Add(new UiEntityDossierBlock
        {
            EntityType = "inventory-disassembly",
            Title = "Разбирается на",
            Subtitle = "Материалы",
            Summary = "Что можно получить при разборе предмета.",
            Sections =
            [
                new UiEntityDossierSection
                {
                    Id = "materials",
                    Title = "Материалы",
                    Icon = "inventory",
                    Collapsible = true,
                    InitiallyExpanded = true,
                    Blocks = rows.Select(static row => (UiBlock)new UiEntityDossierBlock
                    {
                        EntityType = "inventory-material",
                        Title = row.Cells.ElementAtOrDefault(0) ?? "Материал",
                        Summary = FirstNonEmpty(row.Cells.ElementAtOrDefault(3), row.Cells.ElementAtOrDefault(2), row.Cells.ElementAtOrDefault(1)),
                        Sections =
                        [
                            new UiEntityDossierSection
                            {
                                Id = "fields",
                                Title = "Подробности",
                                Icon = "inventory",
                                Collapsible = true,
                                InitiallyExpanded = true,
                                Blocks =
                                [
                                    new UiKeyValueGridBlock
                                    {
                                        Items =
                                        [
                                            new UiKeyValueItem { Key = "Количество", Value = EmptyFallback(row.Cells.ElementAtOrDefault(1) ?? string.Empty) },
                                            new UiKeyValueItem { Key = "Вес", Value = EmptyFallback(row.Cells.ElementAtOrDefault(2) ?? string.Empty) },
                                            new UiKeyValueItem { Key = "Описание", Value = EmptyFallback(row.Cells.ElementAtOrDefault(3) ?? string.Empty) }
                                        ]
                                    }
                                ]
                            }
                        ]
                    }).ToList()
                }
            ]
        });
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
            panelBlocks.Add(new UiEntityDossierBlock
            {
                EntityType = "inventory-fate-cards",
                Title = "Карты судьбы предмета",
                Subtitle = "Связь с владельцем",
                Summary = "Сюжетные вехи, которые предмет открывает через связь с владельцем.",
                Sections =
                [
                    new UiEntityDossierSection
                    {
                        Id = "cards",
                        Title = "Карты",
                        Icon = "inventory",
                        Collapsible = true,
                        InitiallyExpanded = true,
                        Blocks = fateCards
                            .OfType<JsonObject>()
                            .Select(static card => (UiBlock)new UiEntityDossierBlock
                            {
                                EntityType = "inventory-fate-card",
                                Title = FirstNonEmpty(GetNodeString(card, "name"), GetNodeString(card, "cardName"), "Карта"),
                                Subtitle = TryGetNodeBool(card, "isUnlocked", out var unlocked) && unlocked ? "разблокирована" : "закрыта",
                                Summary = FirstNonEmpty(GetNodeString(card, "description"), GetNodeString(card, "summary"))
                            })
                            .ToList()
                    }
                ]
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
            return string.Join("\n", array
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
                string.Join("\n", obj
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

    private sealed record EffectDetailRequest(
        EffectDetailKind Kind,
        string Selector);

    private enum NpcSectionDetailKind
    {
        Overview,
        Profile,
        Section,
        Unknown
    }

    private sealed record NpcSectionDetailRequest(
        NpcSectionDetailKind Kind,
        string NpcSelector,
        string SectionSelector);

    private enum NpcQuestDetailKind
    {
        None,
        Quest,
        Invalid
    }

    private sealed record NpcQuestDetailRequest(
        NpcQuestDetailKind Kind,
        string NpcSelector,
        string QuestSelector);

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
