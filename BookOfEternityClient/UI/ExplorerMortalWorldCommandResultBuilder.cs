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

    public static bool CanBuild(string command) => CommandKinds.ContainsKey(command.Trim());

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs)
    {
        var normalizedCommand = command.Trim();
        if (!CommandKinds.TryGetValue(normalizedCommand, out var kind))
            return null;

        await stateManager.RefreshGameStateAsync();

        return kind switch
        {
            CommandKind.Inventory => await BuildInventory(normalizedCommand, fs),
            CommandKind.Npcs => await BuildBundle(normalizedCommand, fs, "Персонажи", [
                new("game_state/npcs/npc_core.json", "npcs", "NPC"),
                new("game_state/npcs/npc_relationships.json", "entries", "Отношений"),
                new("game_state/npcs/npc_activities.json", "entries", "Активностей"),
                new("game_state/npcs/npc_custom_states.json", "entries", "Особых состояний")
            ]),
            CommandKind.Quests => await BuildBundle(normalizedCommand, fs, "Квесты", [
                new("game_state/quests/regular_quests.json", "activeQuests", "Активных"),
                new("game_state/quests/regular_quests.json", "completedQuests", "Завершённых"),
                new("game_state/quests/quest_history.json", "entries", "Исторических записей"),
                new("game_state/quests/plot_outline.json", "entries", "Сюжетных записей")
            ]),
            CommandKind.Map => await BuildMap(normalizedCommand, fs),
            CommandKind.CurrentLocation => await BuildBundle(normalizedCommand, fs, "Где я", [
                new("game_state/world/current_location.json", "locationName", "Локация"),
                new("game_state/world/current_location.json", "region", "Регион"),
                new("game_state/world/current_location.json", "description", "Описание")
            ]),
            CommandKind.Factions => await BuildBundle(normalizedCommand, fs, "Фракции", [
                new("game_state/factions/faction_core.json", "factions", "Фракций"),
                new("game_state/factions/faction_projects.json", "entries", "Проектов"),
                new("game_state/factions/faction_chronicles.json", "entries", "Хроник"),
                new("game_state/factions/faction_custom.json", "entries", "Особых состояний")
            ]),
            CommandKind.Skills => await BuildBundle(normalizedCommand, fs, "Навыки", [
                new("game_state/player/skills_active.json", "skills", "Активных"),
                new("game_state/player/skills_passive.json", "skills", "Пассивных")
            ]),
            CommandKind.Stats => await BuildStats(normalizedCommand, fs, stateManager),
            CommandKind.WorldNews => await BuildBundle(normalizedCommand, fs, "Новости мира", [
                new("game_state/world/world_events.json", "events", "Событий"),
                new("game_state/world/world_flags.json", "flags", "Флагов"),
                new("game_state/world/progression.json", "entries", "Записей прогресса")
            ]),
            CommandKind.RivalThreads => await BuildBundle(normalizedCommand, fs, "Чужие нити", [
                new(RivalSoulArcService.StatePath, "rivalSoulArcs", "Арк соперников"),
                new(RivalSoulArcService.StatePath, "arcs", "Арк")
            ]),
            CommandKind.GuardianCorrections => await BuildBundle(normalizedCommand, fs, "Коррективы Хранителя", [
                new(GuardianCorrectionService.StatePath, "corrections", "Корректив")
            ]),
            CommandKind.Locations => await BuildBundle(normalizedCommand, fs, "Локации", [
                new("game_state/world/world_map.json", "newLocations", "Открытых"),
                new("game_state/world/world_map.json", "locationUpdates", "Обновлений")
            ]),
            CommandKind.Transport => await BuildBundle(normalizedCommand, fs, "Транспорт", [
                new("game_state/world/world_map.json", "transportRoutes", "Маршрутов"),
                new("game_state/world/current_location.json", "availableTransport", "Доступного транспорта")
            ]),
            CommandKind.Effects => await BuildEffects(normalizedCommand, fs, stateManager),
            CommandKind.Combat => await BuildBundle(normalizedCommand, fs, "Бой", [
                new("game_state/combat/enemies.json", "enemies", "Врагов"),
                new("game_state/combat/allies.json", "allies", "Союзников"),
                new("game_state/combat/combat_log.json", "entries", "Записей журнала")
            ]),
            CommandKind.Weather => await BuildBundle(normalizedCommand, fs, "Время и погода", [
                new("game_state/world/world_time.json", "currentTime", "Время"),
                new("game_state/world/weather.json", "currentState", "Погода"),
                new("game_state/world/weather.json", "description", "Описание")
            ]),
            CommandKind.Books => await BuildBundle(normalizedCommand, fs, "Книги и тексты", [
                new("game_state/inventory/item_text_updates.json", "entries", "Текстов"),
                new("game_state/npcs/item_journals.json", "entries", "Журнальных записей")
            ]),
            CommandKind.StorageAccess => await BuildBundle(normalizedCommand, fs, "Доступ к хранилищам", [
                new("game_state/misc/storage_access.json", "storages", "Хранилищ"),
                new("game_state/misc/storage_access.json", "entries", "Записей")
            ]),
            CommandKind.Interactions => await BuildBundle(normalizedCommand, fs, "Взаимодействия игроков", [
                new("game_state/misc/player_interactions.json", "interactions", "Взаимодействий"),
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

        var node = read.Node[propertyName];
        return node switch
        {
            JsonArray array => array.Count.ToString(),
            JsonObject obj => $"{obj.Count} полей",
            JsonValue value when TryGetScalarString(value, out var text) => EmptyFallback(text),
            _ => "найдено"
        };
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
