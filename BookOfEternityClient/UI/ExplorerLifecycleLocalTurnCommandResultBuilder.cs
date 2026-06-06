using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.UI;

public static class ExplorerLifecycleLocalTurnCommandResultBuilder
{
    private const string TurnRequestPath = "input/turn_request.json";
    private const string TurnCompletePath = "ready/turn_complete.json";
    private const string TurnErrorPath = "ready/turn_error.json";
    private const string PendingTurnSnapshotManifestPath = "game_state/control/pending_turn_snapshot.json";
    private const string PendingTurnSnapshotDirectory = "game_state/control/pending_turn_snapshot";
    private const string ExplorerRollbackDirectory = "game_state/control/explorer_local_turn_rollback";
    private const string SoulStatePath = "game_state/meta/soul_state.json";

    public static bool CanBuild(string command) => NormalizeCommand(command) switch
    {
        "/validate" or "/валидация" or
        "/world_setup" or "/настройка_мира" or
        "/distribute" or "/распределить" or
        "/companion_directive" or "/директива_компаньону" or
        "/faction_directive" or "/директива_фракции" or
        "/npc_talk" or "/talk_npc" or "/поговорить_с_нпс" or "/разговор_с_нпс" or
        "/npc_trade" or "/торговля_нпс" or
        "/equip" or "/экипировать" or
        "/unequip" or "/снять" or
        "/inventory_drop" or "/выбросить_предмет" or
        "/inventory_split" or "/разделить_стопку" or
        "/inventory_merge" or "/объединить_стопки" or
        "/craft" or "/ремесло" or
        "/gacha" or "/гача" or
        "/abode_offering" or "/подношение_обители" or
        "/found_guardian_mantle" or "/учредить_хранителя" or
        "/guardian_trade" or "/торговля_хранителя" or
        "/shining_trade" or "/сияющая_торговля" or
        "/spiritual_action" or "/духовное_действие" or
        "/soul_relic_equip" or "/экипировать_реликвию" or
        "/soul_relic_unequip" or "/снять_реликвию" => true,
        _ => false
    };

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs,
        ValidationService validationService)
    {
        var normalized = NormalizeCommand(command);
        return normalized switch
        {
            "/validate" or "/валидация" => await BuildValidationAsync(command, fs, validationService),
            "/world_setup" or "/настройка_мира" => await BuildWorldSetupAsync(command, fs, stateManager),
            "/distribute" or "/распределить" => await BuildStatDistributionAsync(command, fs),
            "/companion_directive" or "/директива_компаньону" => await BuildCompanionDirectiveAsync(command, fs),
            "/faction_directive" or "/директива_фракции" => await BuildFactionDirectiveAsync(command, fs),
            "/npc_talk" or "/talk_npc" or "/поговорить_с_нпс" or "/разговор_с_нпс" => await BuildNpcTalkAsync(command, fs),
            "/npc_trade" or "/торговля_нпс" => await BuildNpcTradeAsync(command, fs, stateManager),
            "/equip" or "/экипировать" => await BuildInventoryEquipAsync(command, fs),
            "/unequip" or "/снять" => await BuildInventoryUnequipAsync(command, fs),
            "/inventory_drop" or "/выбросить_предмет" => await BuildInventoryDropAsync(command, fs),
            "/inventory_split" or "/разделить_стопку" => await BuildInventorySplitAsync(command, fs),
            "/inventory_merge" or "/объединить_стопки" => await BuildInventoryMergeAsync(command, fs),
            "/craft" or "/ремесло" => await BuildCraftAsync(command, fs),
            "/gacha" or "/гача" => await BuildGachaAsync(command, fs, stateManager),
            "/abode_offering" or "/подношение_обители" => await BuildAbodeOfferingAsync(command, fs, stateManager),
            "/found_guardian_mantle" or "/учредить_хранителя" => await BuildPlayerGuardianFoundationAsync(command, fs),
            "/guardian_trade" or "/торговля_хранителя" => await BuildGuardianTradeAsync(command, fs, stateManager),
            "/shining_trade" or "/сияющая_торговля" => await BuildShiningTradeAsync(command, fs, stateManager),
            "/spiritual_action" or "/духовное_действие" => await BuildSpiritualActionAsync(command, fs, stateManager),
            "/soul_relic_equip" or "/экипировать_реликвию" => await BuildSoulRelicEquipAsync(command, fs),
            "/soul_relic_unequip" or "/снять_реликвию" => await BuildSoulRelicUnequipAsync(command, fs),
            _ => null
        };
    }

    private static async Task<ExplorerCommandResult> BuildValidationAsync(
        string command,
        FileSystemManager fs,
        ValidationService validationService)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var issues = await validationService.ValidateGameStateAsync();
        var blocks = new List<UiBlock> { localTurn.Panel };

        if (issues.Count == 0)
        {
            blocks.Add(Message(
                UiNotificationSeverity.Success,
                "Валидация",
                "Все проверки пройдены. Файлы состояния не содержат найденных нарушений."));
        }
        else
        {
            blocks.Add(Message(
                issues.Any(static issue => issue.Severity == IssueSeverity.Error)
                    ? UiNotificationSeverity.Error
                    : UiNotificationSeverity.Warning,
                $"Валидация: найдено проблем {issues.Count}",
                "Браузерный режим выполняет тот же ValidationService, что и консольная команда /validate."));

            blocks.Add(new UiTableBlock
            {
                Title = "Сводка проблем",
                Columns = ["Уровень", "Категория", "Раздел", "Количество"],
                Rows = issues
                    .GroupBy(static issue => new
                    {
                        issue.Severity,
                        issue.Category,
                        Section = string.IsNullOrWhiteSpace(issue.Section) ? "General" : issue.Section
                    })
                    .OrderByDescending(static group => group.Count())
                    .ThenBy(static group => group.Key.Severity.ToString(), StringComparer.OrdinalIgnoreCase)
                    .Take(12)
                    .Select(static group => Row(
                        group.Key.Severity.ToString(),
                        group.Key.Category.ToString(),
                        group.Key.Section,
                        group.Count().ToString()))
                    .ToList()
            });

            blocks.Add(new UiTableBlock
            {
                Title = "Первые проблемы",
                Columns = ["Файл", "Код", "Проблема", "Подсказка"],
                Rows = issues.Take(20)
                    .Select(static issue => Row(
                        issue.FilePath,
                        string.IsNullOrWhiteSpace(issue.Code) ? "-" : issue.Code!,
                        issue.Message,
                        string.IsNullOrWhiteSpace(issue.RepairHint) ? "-" : issue.RepairHint!))
                    .ToList()
            });
        }

        return Result(command, CommandExecutionState.Completed, blocks);
    }

    private static async Task<ExplorerCommandResult> BuildWorldSetupAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var pending = await ReadJson(fs, WorldDirectiveService.PendingSetupPath);
        var scenario = await ReadJson(fs, ScenarioCoreService.ManifestPath);
        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Подготовка следующего мира",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = stateManager.CurrentState.IsInAfterlifeRealm
                            ? "Настройка следующей смертной жизни доступна в загробье. Браузер показывает client-owned контракт и форму редактирования/очистки."
                            : "Подготовка следующего мира доступна только в Море Хаоса или Сияющей Обители; в смертной жизни используется /world_rules.",
                        Tone = stateManager.CurrentState.IsInAfterlifeRealm ? UiTone.Accent : UiTone.Warning
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Файл подготовки", WorldDirectiveService.PendingSetupPath),
                            KeyValue("Сценарное ядро", ScenarioCoreService.ManifestPath),
                            KeyValue("Текущий realm", stateManager.CurrentState.CurrentRealm ?? "?")
                        ]
                    }
                ]
            }
        };
        AddRawOrWarning(blocks, "JSON: incarnation_world_setup", pending);
        AddRawOrWarning(blocks, "JSON: next_life_scenario_core", scenario);

        var prompts = new List<UiPrompt>
        {
            new UiSelectionPrompt
            {
                Id = "world_setup_mode",
                Prompt = "Режим подготовки мира",
                Required = true,
                Options =
                [
                    Option("create_or_edit", "Создать / редактировать", "Записать или заменить подготовку следующей смертной жизни."),
                    Option("apply_profile", "Применить профиль", "Использовать профиль из папки world_profiles."),
                    Option("clear", "Очистить", "Удалить client-owned подготовку мира и сценарное ядро.")
                ]
            },
            new UiTextInputPrompt
            {
                Id = "world_title",
                Prompt = "Название мира",
                Placeholder = "Например: Королевство пепельных колоколов"
            },
            new UiLongTextInputPrompt
            {
                Id = "world_directives",
                Prompt = "Директивы мира",
                Placeholder = "Опишите жанр, запреты, обязательные темы, стартовые обстоятельства и роль персонажа.",
                MinLines = 4,
                MaxLines = 12
            }
        };

        return Result(command, localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput, blocks, prompts: prompts);
    }

    private static async Task<ExplorerCommandResult> BuildStatDistributionAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var statPoints = await ReadJson(fs, "game_state/player/stat_points.json");
        var characteristics = await ReadJson(fs, "game_state/misc/characteristics.json");
        var rows = new List<UiTableRow>();
        foreach (var stat in Characteristics.All)
        {
            var value = TryGetInt(characteristics.Node as JsonObject, stat, 1);
            rows.Add(Row(
                Characteristics.RussianNames.GetValueOrDefault(stat, stat),
                stat,
                value.ToString()));
        }

        var unspent = TryGetInt(statPoints.Node as JsonObject, "unspentStatPoints", 0);
        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Распределение характеристик",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = unspent > 0
                            ? $"Доступно очков характеристик: {unspent}. Браузерная форма безопасно распределяет очки без console-key навигации."
                            : "Нераспределённых очков характеристик сейчас нет.",
                        Tone = unspent > 0 ? UiTone.Accent : UiTone.Muted
                    },
                    new UiTableBlock
                    {
                        Title = "Текущие характеристики",
                        Columns = ["Название", "Ключ", "Значение"],
                        Rows = rows
                    }
                ]
            }
        };
        AddRawOrWarning(blocks, "JSON: stat_points", statPoints);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiLongTextInputPrompt
                {
                    Id = "stat_allocation_json",
                    Prompt = "Распределение очков JSON",
                    Placeholder = "{ \"strength\": 1, \"wisdom\": 2 }",
                    MinLines = 2,
                    MaxLines = 8
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildCompanionDirectiveAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var npcCore = await ReadJson(fs, "game_state/npcs/npc_core.json");
        var companions = CollectObjects(npcCore.Node)
            .Where(static item => string.Equals(GetString(item, "progressionType"), "Companion", StringComparison.OrdinalIgnoreCase))
            .Select(static item => Row(
                FirstNonEmpty(GetString(item, "name"), GetString(item, "displayName"), "?"),
                FirstNonEmpty(GetString(item, "npcId"), GetString(item, "id"), "-"),
                FirstNonEmpty(GetString(item, "playerCompanionDirective"), "не задана")))
            .ToList();

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Директивы компаньонов",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = companions.Count == 0
                            ? "Активные компаньоны не найдены."
                            : "Выберите компаньона и задайте короткую стратегическую директиву. Это client-authored поле playerCompanionDirective.",
                        Tone = companions.Count == 0 ? UiTone.Muted : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Компаньоны",
                        Columns = ["Имя", "ID", "Текущая директива"],
                        Rows = companions
                    }
                ]
            }
        };
        AddRawOrWarning(blocks, "JSON: npc_core", npcCore);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiTextInputPrompt { Id = "companion_id", Prompt = "ID компаньона", Placeholder = "npcId" },
                new UiLongTextInputPrompt
                {
                    Id = "companion_directive",
                    Prompt = "Новая директива",
                    Placeholder = "Кратко опишите, как компаньон должен действовать.",
                    MinLines = 2,
                    MaxLines = 6
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildFactionDirectiveAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var factionsRoot = await ReadJson(fs, "game_state/factions/faction_core.json");
        var factions = CollectObjects(factionsRoot.Node)
            .Where(static item => TryGetBool(item, "isPlayerFaction") || TryGetBool(item, "isPlayerMember"))
            .Select(static item => Row(
                FirstNonEmpty(GetString(item, "name"), GetString(item, "factionName"), "?"),
                FirstNonEmpty(GetString(item, "factionId"), "-"),
                TryGetBool(item, "isPlayerFaction") ? "лидер" : "участник",
                FirstNonEmpty(GetString(item, "playerStrategyDirective"), "не задана")))
            .ToList();

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Директивы фракций",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = factions.Count == 0
                            ? "Фракции игрока или членство во фракциях не найдены."
                            : "Выберите фракцию и задайте стратегическую директиву. Это client-authored поле playerStrategyDirective.",
                        Tone = factions.Count == 0 ? UiTone.Muted : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Фракции",
                        Columns = ["Название", "ID", "Роль", "Текущая директива"],
                        Rows = factions
                    }
                ]
            }
        };
        AddRawOrWarning(blocks, "JSON: faction_core", factionsRoot);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiTextInputPrompt { Id = "faction_id", Prompt = "ID фракции", Placeholder = "factionId" },
                new UiLongTextInputPrompt
                {
                    Id = "faction_directive",
                    Prompt = "Новая стратегическая директива",
                    Placeholder = "Опишите приоритеты фракции.",
                    MinLines = 2,
                    MaxLines = 6
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildInventoryEquipAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var inventory = await InventoryEquipmentService.ReadContextAsync(fs);
        var requestedItem = InventoryEquipmentService.ReadFirstCommandArgument(command);
        var candidates = inventory?.Items
            .Where(static item => item.IsEquippable &&
                                  string.IsNullOrWhiteSpace(item.EquippedSlot) &&
                                  !item.IsSoulRelic &&
                                  !item.IsBroken)
            .ToList() ?? [];
        var matchedRequestedItem = InventoryEquipmentService.FindItem(candidates, requestedItem);
        if (matchedRequestedItem != null)
            candidates = [matchedRequestedItem];

        var rows = candidates
            .Select(static item => Row(
                item.Name,
                FirstNonEmpty(item.Type, "тип не указан"),
                string.IsNullOrWhiteSpace(item.ResolvedSlot)
                    ? "выберите слот"
                    : InventoryEquipmentService.FormatSlotName(item.ResolvedSlot)))
            .ToList();

        var statusText = candidates.Count == 0
            ? "В рюкзаке нет обычных предметов, которые можно экипировать."
            : "Выберите предмет, слот и подтвердите экипировку. Запись выполняется локально после проверки хода и блокировки интерфейса.";

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Экипировка предмета",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = statusText,
                        Tone = candidates.Count == 0 ? UiTone.Muted : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Доступные предметы",
                        Columns = ["Предмет", "Тип", "Слот"],
                        Rows = rows
                    }
                ]
            }
        };

        if (candidates.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        var selected = candidates.Count == 1 ? candidates[0] : null;
        IReadOnlyList<string> slotKeys = selected is { ResolvedSlot.Length: > 0 }
            ? [selected.ResolvedSlot]
            : InventoryEquipmentService.SlotLabels.Keys.ToArray();

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "item_identity",
                    Prompt = "Предмет",
                    Required = true,
                    Options = candidates
                        .Select(static item => Option(
                            FirstNonEmpty(item.Identity, item.Name),
                            item.Name,
                            string.IsNullOrWhiteSpace(item.ResolvedSlot)
                                ? "Слот нужно выбрать вручную."
                                : $"Подходит: {InventoryEquipmentService.FormatSlotName(item.ResolvedSlot)}."))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "equipment_slot",
                    Prompt = "Слот экипировки",
                    Required = true,
                    Options = slotKeys
                        .Select(static slot => Option(slot, InventoryEquipmentService.FormatSlotName(slot), ""))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_inventory_write",
                    Prompt = "Подтвердить изменение экипировки",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildInventoryUnequipAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var inventory = await InventoryEquipmentService.ReadContextAsync(fs);
        var requestedSlot = InventoryEquipmentService.ReadFirstCommandArgument(command);
        var equipped = inventory?.Equipped
            .Where(static item => item.IsOrdinaryInventoryItem)
            .ToList() ?? [];
        if (InventoryEquipmentService.TryNormalizeSlot(requestedSlot, out var normalizedSlot))
        {
            var matched = equipped.FirstOrDefault(item =>
                string.Equals(item.SlotKey, normalizedSlot, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
                equipped = [matched];
        }

        var rows = equipped
            .Select(static item => Row(item.SlotLabel, item.ItemName))
            .ToList();

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Снятие предмета",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = equipped.Count == 0
                            ? "Обычные экипированные предметы не найдены."
                            : "Выберите слот и подтвердите снятие предмета в рюкзак.",
                        Tone = equipped.Count == 0 ? UiTone.Muted : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Экипировано",
                        Columns = ["Слот", "Предмет"],
                        Rows = rows
                    }
                ]
            }
        };

        if (equipped.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "equipment_slot",
                    Prompt = "Что снять",
                    Required = true,
                    Options = equipped
                        .Select(static item => Option(item.SlotKey, $"{item.SlotLabel}: {item.ItemName}", ""))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_inventory_write",
                    Prompt = "Подтвердить снятие предмета",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildInventoryDropAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var inventory = await InventoryManagementService.ReadContextAsync(fs);
        var requestedItem = InventoryEquipmentService.ReadFirstCommandArgument(command);
        var allItems = inventory?.Items.ToList() ?? [];
        var matchedRequestedItem = InventoryManagementService.FindItem(allItems, requestedItem);
        var candidates = new List<InventoryManagementItem>();
        if (!string.IsNullOrWhiteSpace(requestedItem))
        {
            if (matchedRequestedItem != null)
                candidates.Add(matchedRequestedItem);
        }
        else
        {
            candidates.AddRange(allItems);
        }

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Выброс предмета",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = BuildInventoryDropStatusText(candidates.Count, requestedItem),
                        Tone = candidates.Count == 0 ? UiTone.Muted : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Доступные предметы",
                        Columns = ["Предмет", "Количество", "Состояние"],
                        Rows = candidates
                            .Select(static item => Row(item.Name, FormatInventoryCount(item), FormatInventoryPlacement(item)))
                            .ToList()
                    }
                ]
            }
        };

        if (candidates.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "item_identity",
                    Prompt = "Предмет",
                    Required = true,
                    Options = candidates.Select(BuildInventoryOption).ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_inventory_drop",
                    Prompt = "Подтвердить выброс предмета",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildInventorySplitAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var inventory = await InventoryManagementService.ReadContextAsync(fs);
        var requestedItem = InventoryEquipmentService.ReadFirstCommandArgument(command);
        var allItems = inventory?.Items.ToList() ?? [];
        var matchedRequestedItem = InventoryManagementService.FindItem(allItems, requestedItem);
        var candidates = new List<InventoryManagementItem>();
        if (!string.IsNullOrWhiteSpace(requestedItem))
        {
            if (matchedRequestedItem is { Count: > 1 })
                candidates.Add(matchedRequestedItem);
        }
        else
        {
            candidates.AddRange(allItems.Where(static item => item.Count > 1));
        }

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Разделение стопки",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = BuildInventorySplitStatusText(candidates.Count, matchedRequestedItem, requestedItem),
                        Tone = candidates.Count == 0 ? UiTone.Muted : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Стопки для разделения",
                        Columns = ["Предмет", "Количество", "Можно отделить"],
                        Rows = candidates
                            .Select(static item => Row(item.Name, item.Count.ToString(), $"1-{item.Count - 1}"))
                            .ToList()
                    }
                ]
            }
        };

        if (candidates.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        var selected = candidates.Count == 1 ? candidates[0] : null;
        var quantityPrompt = selected == null
            ? "Сколько отделить"
            : $"Сколько отделить (1-{selected.Count - 1})";

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "item_identity",
                    Prompt = "Стопка",
                    Required = true,
                    Options = candidates.Select(BuildInventorySplitOption).ToList()
                },
                new UiTextInputPrompt
                {
                    Id = "split_quantity",
                    Prompt = quantityPrompt,
                    Placeholder = selected == null ? "Количество меньше выбранной стопки" : "1",
                    Required = true
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_inventory_split",
                    Prompt = "Подтвердить разделение стопки",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildInventoryMergeAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var inventory = await InventoryManagementService.ReadContextAsync(fs);
        var requestedItem = InventoryEquipmentService.ReadFirstCommandArgument(command);
        var allItems = inventory?.Items.ToList() ?? [];
        var matchedRequestedItem = InventoryManagementService.FindItem(allItems, requestedItem);
        var candidates = new List<InventoryManagementItem>();

        if (inventory != null)
        {
            if (!string.IsNullOrWhiteSpace(requestedItem))
            {
                if (matchedRequestedItem != null &&
                    InventoryManagementService.FindCompatibleStacks(inventory, matchedRequestedItem).Count > 1)
                {
                    candidates.Add(matchedRequestedItem);
                }
            }
            else
            {
                candidates.AddRange(allItems.Where(item =>
                    InventoryManagementService.FindCompatibleStacks(inventory, item).Count > 1));
            }
        }

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Объединение стопок",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = BuildInventoryMergeStatusText(candidates.Count, matchedRequestedItem, requestedItem),
                        Tone = candidates.Count == 0 ? UiTone.Muted : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Совместимые стопки",
                        Columns = ["Предмет", "Стопок", "Итог"],
                        Rows = inventory == null
                            ? []
                            : candidates
                                .Select(item =>
                                {
                                    var compatible = InventoryManagementService.FindCompatibleStacks(inventory, item);
                                    return Row(item.Name, compatible.Count.ToString(), compatible.Sum(static match => match.Count).ToString());
                                })
                                .ToList()
                    }
                ]
            }
        };

        if (candidates.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "item_identity",
                    Prompt = "Какие стопки объединить",
                    Required = true,
                    Options = candidates.Select(item => BuildInventoryMergeOption(inventory!, item)).ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_inventory_merge",
                    Prompt = "Подтвердить объединение стопок",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static string BuildInventoryDropStatusText(int candidateCount, string requestedItem)
    {
        if (candidateCount == 0)
            return string.IsNullOrWhiteSpace(requestedItem)
                ? "В инвентаре нет предметов для выброса."
                : "Выбранный предмет не найден в инвентаре.";

        return "Выберите предмет и подтвердите выброс. Если предмет сейчас экипирован, его слот будет освобождён.";
    }

    private static string BuildInventorySplitStatusText(
        int candidateCount,
        InventoryManagementItem? requestedItem,
        string requestedItemText)
    {
        if (candidateCount > 0)
            return "Выберите стопку, укажите количество и подтвердите разделение.";

        if (!string.IsNullOrWhiteSpace(requestedItemText) && requestedItem != null)
            return "У выбранного предмета нет стопки, которую можно разделить.";

        return string.IsNullOrWhiteSpace(requestedItemText)
            ? "В инвентаре нет стопок, которые можно разделить."
            : "Выбранная стопка не найдена.";
    }

    private static string BuildInventoryMergeStatusText(
        int candidateCount,
        InventoryManagementItem? requestedItem,
        string requestedItemText)
    {
        if (candidateCount > 0)
            return "Выберите совместимые стопки и подтвердите объединение.";

        if (!string.IsNullOrWhiteSpace(requestedItemText) && requestedItem != null)
            return "Для выбранного предмета нет другой совместимой стопки.";

        return string.IsNullOrWhiteSpace(requestedItemText)
            ? "В инвентаре нет совместимых стопок для объединения."
            : "Выбранная стопка не найдена.";
    }

    private static UiSelectionOption BuildInventoryOption(InventoryManagementItem item) =>
        Option(
            FirstNonEmpty(item.Identity, item.Name),
            item.Name,
            $"{FormatInventoryCount(item)}. {FormatInventoryPlacement(item)}.");

    private static UiSelectionOption BuildInventorySplitOption(InventoryManagementItem item) =>
        Option(
            FirstNonEmpty(item.Identity, item.Name),
            item.Name,
            $"Сейчас {item.Count} шт.; можно отделить 1-{item.Count - 1}.");

    private static UiSelectionOption BuildInventoryMergeOption(
        InventoryManagementContext inventory,
        InventoryManagementItem item)
    {
        var compatible = InventoryManagementService.FindCompatibleStacks(inventory, item);
        return Option(
            FirstNonEmpty(item.Identity, item.Name),
            item.Name,
            $"Совместимых стопок: {compatible.Count}; после объединения: {compatible.Sum(static match => match.Count)} шт.");
    }

    private static string FormatInventoryCount(InventoryManagementItem item) =>
        item.Count == 1 ? "1 шт." : $"{item.Count} шт.";

    private static string FormatInventoryPlacement(InventoryManagementItem item) =>
        string.IsNullOrWhiteSpace(item.EquippedSlot)
            ? "В рюкзаке"
            : $"Экипирован: {InventoryEquipmentService.FormatSlotName(item.EquippedSlot)}";

    private static async Task<ExplorerCommandResult> BuildNpcTalkAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var currentRealm = GetString((await ReadJson(fs, SoulStatePath)).Node as JsonObject, "currentRealm");
        if (!RealmSemantics.IsMortalRealm(currentRealm))
        {
            return Result(
                command,
                CommandExecutionState.Blocked,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Warning,
                        "Разговор с НПС недоступен",
                        "Разговор с НПС можно начать только в смертном мире. Сейчас действие недоступно для текущего царства.")
                ]);
        }

        var requestedNpcId = ReadCommandArguments(command);
        var npcRead = await ReadJson(fs, "game_state/npcs/npc_core.json");
        if (!string.IsNullOrWhiteSpace(npcRead.Error))
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Warning,
                        "Разговор с НПС",
                        "Список известных персонажей временно недоступен. Повторите действие после проверки состояния.")
                ]);
        }

        var npcs = CollectNpcConversationOptions(npcRead.Node).ToList();
        if (npcs.Count == 0)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Info,
                        "Разговор с НПС",
                        "Сейчас нет известных персонажей, с которыми можно начать отдельный разговор.")
                ]);
        }

        var selected = ResolveNpcConversationOption(npcs, requestedNpcId);
        var orderedNpcs = npcs
            .OrderByDescending(npc => selected != null && string.Equals(npc.NpcId, selected.NpcId, StringComparison.OrdinalIgnoreCase))
            .ThenBy(npc => npc.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var statusText = selected == null
            ? string.IsNullOrWhiteSpace(requestedNpcId)
                ? "Выберите собеседника и кратко опишите тему разговора. ГМ разыграет сцену в следующем принятом ходе."
                : "Указанный собеседник не найден среди известных персонажей. Выберите персонажа из списка или уточните ввод."
            : $"Выбран собеседник: {selected.Name}. Можно выбрать другого персонажа из списка.";

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Разговор с НПС",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = statusText,
                        Tone = selected == null && !string.IsNullOrWhiteSpace(requestedNpcId) ? UiTone.Warning : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Известные персонажи",
                        Columns = ["Имя", "Где находится", "Отношение"],
                        Rows = orderedNpcs
                            .Take(20)
                            .Select(static npc => Row(
                                npc.Name,
                                FirstNonEmpty(npc.Location, "-"),
                                string.IsNullOrWhiteSpace(npc.Relationship) ? "-" : npc.Relationship))
                            .ToList()
                    }
                ]
            }
        };

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "npc_id",
                    Prompt = "С кем поговорить",
                    Required = selected == null,
                    AllowCustom = true,
                    Options = orderedNpcs
                        .Select(static npc => Option(
                            npc.NpcId,
                            npc.Name,
                            FirstNonEmpty(npc.Location, "Известный персонаж")))
                        .ToList()
                },
                new UiLongTextInputPrompt
                {
                    Id = "npc_conversation_topic",
                    Prompt = "Опишите тему разговора",
                    Required = true,
                    Placeholder = "Кратко опишите, о чём спросить или что обсудить.",
                    MinLines = 2,
                    MaxLines = 6
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildNpcTradeAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var npcId = ReadCommandArguments(command);
        if (string.IsNullOrWhiteSpace(npcId))
        {
            return Result(
                command,
                localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Info,
                        "Торговля с НПС",
                        "Укажите торговца командой /npc_trade <npc_id> или введите ID торговца в форме.")
                ],
                prompts:
                [
                    new UiTextInputPrompt
                    {
                        Id = "npc_id",
                        Prompt = "ID торговца",
                        Required = true,
                        Placeholder = "npcId"
                    },
                    new UiSelectionPrompt
                    {
                        Id = "npc_trade_choice",
                        Prompt = "Действие торговли",
                        Required = true,
                        Options =
                        [
                            Option("request:__selected__", "Запросить витрину", "Попросить ГМа подготовить ассортимент выбранного торговца.")
                        ]
                    },
                    TradeConfirmationPrompt()
                ]);
        }

        var service = new NpcTradeService(fs, NullLogger<NpcTradeService>.Instance);
        var currentTurn = Math.Max(1, stateManager.CurrentState.TurnNumber);
        var view = await service.EnsureTradeInventoryAsync(npcId, currentTurn, createPendingRequests: false);
        if (view == null)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Торговец не найден", "Такого торговца сейчас нет среди известных персонажей.")
                ]);
        }

        var sellOffers = await service.GetSellableItemsAsync(view.NpcId);
        var options = new List<UiSelectionOption>();
        if (!view.TradeBlocked && !view.InventoryReady && !view.InventoryRequestPending)
            options.Add(Option($"request:{view.NpcId}", "Запросить витрину", "Попросить ГМа подготовить ассортимент текущего цикла."));
        options.AddRange(view.Offers.Select(offer => new UiSelectionOption
        {
            Value = $"buy:{offer.SlotId}",
            Label = $"Купить: {offer.Name}",
            Description = $"{offer.Price} монет; редкость: {FirstNonEmpty(offer.Rarity, "не указана")}",
            Disabled = offer.SoldOut || view.CurrentMoney < offer.Price
        }));
        options.AddRange(sellOffers.Select(offer => Option(
            $"sell:{offer.ItemId}",
            $"Продать: {offer.Name}",
            $"+{offer.Price} монет; редкость: {FirstNonEmpty(offer.Rarity, "не указана")}")));
        options.AddRange(view.BuybackOffers.Select(offer => new UiSelectionOption
        {
            Value = $"buyback:{offer.BuybackEntryId}",
            Label = $"Выкупить: {offer.Name}",
            Description = $"{offer.Price} монет; ранее продано за {offer.SoldForPrice}",
            Disabled = view.CurrentMoney < offer.Price
        }));

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Торговля с НПС",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = BuildTradeStatusText(view.TradeBlocked, view.BlockReason, view.InventoryReady, view.InventoryRequestPending, view.InventoryStatusMessage, "Витрина торговца готова."),
                        Tone = view.TradeBlocked ? UiTone.Warning : view.InventoryReady ? UiTone.Accent : UiTone.Muted
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Торговец", view.NpcName),
                            KeyValue("Профиль", view.MerchantProfileDisplay),
                            KeyValue("Деньги", view.CurrentMoney.ToString()),
                            KeyValue("Витрина", view.InventoryReady ? "готова" : view.InventoryRequestPending ? "ожидает ГМ" : "нужно запросить")
                        ]
                    },
                    new UiTableBlock
                    {
                        Title = "Покупка",
                        Columns = ["Товар", "Редкость", "Цена", "Статус"],
                        Rows = view.Offers
                            .Select(offer => Row(
                                offer.Name,
                                FirstNonEmpty(offer.Rarity, "-"),
                                offer.Price.ToString(),
                                offer.SoldOut ? "куплено" : view.CurrentMoney < offer.Price ? "не хватает денег" : "доступно"))
                            .ToList()
                    },
                    new UiTableBlock
                    {
                        Title = "Продажа из рюкзака",
                        Columns = ["Предмет", "Редкость", "Цена"],
                        Rows = sellOffers
                            .Select(offer => Row(offer.Name, FirstNonEmpty(offer.Rarity, "-"), offer.Price.ToString()))
                            .ToList()
                    },
                    new UiTableBlock
                    {
                        Title = "Обратный выкуп",
                        Columns = ["Предмет", "Редкость", "Цена"],
                        Rows = view.BuybackOffers
                            .Select(offer => Row(offer.Name, FirstNonEmpty(offer.Rarity, "-"), offer.Price.ToString()))
                            .ToList()
                    }
                ]
            }
        };

        if (options.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "npc_trade_choice",
                    Prompt = "Сделка",
                    Required = true,
                    Options = options
                },
                TradeConfirmationPrompt()
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildShiningTradeAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var factionId = ReadCommandArguments(command);
        if (string.IsNullOrWhiteSpace(factionId))
        {
            return Result(
                command,
                localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Info, "Сияющая торговля", "Укажите фракцию командой /shining_trade <faction_id> или введите ID фракции в форме.")
                ],
                prompts:
                [
                    new UiTextInputPrompt
                    {
                        Id = "faction_id",
                        Prompt = "ID сияющей фракции",
                        Required = true,
                        Placeholder = "factionId"
                    },
                    new UiSelectionPrompt
                    {
                        Id = "shining_trade_choice",
                        Prompt = "Действие торговли",
                        Required = true,
                        Options =
                        [
                            Option("request:__selected__", "Запросить витрину", "Попросить ГМа подготовить ассортимент выбранной сияющей фракции.")
                        ]
                    },
                    TradeConfirmationPrompt()
                ]);
        }

        var view = await ShiningTradeService.ReadTradeViewAsync(fs, factionId);
        if (view == null)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Фракция не найдена", "Такой сияющей фракции сейчас нет в Обители.")
                ]);
        }

        var feathers = GetSoulInkFeathers((await ReadJson(fs, SoulStatePath)).Node as JsonObject, stateManager.CurrentState.InkFeathers);
        var options = new List<UiSelectionOption>();
        if (!view.TradeBlocked && !view.InventoryReady && !view.InventoryRequestPending)
            options.Add(Option($"request:{view.FactionId}", "Запросить витрину", "Попросить ГМа подготовить ассортимент сияющей фракции."));
        options.AddRange(view.Offers.Select(offer => new UiSelectionOption
        {
            Value = $"buy:{offer.SlotId}",
            Label = $"Купить: {offer.Name}",
            Description = $"{offer.PriceInFeathers} Чернильных Перьев; редкость: {FirstNonEmpty(offer.Rarity, "не указана")}",
            Disabled = offer.SoldOut || feathers < offer.PriceInFeathers
        }));

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Сияющая торговля",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = BuildTradeStatusText(view.TradeBlocked, view.BlockReason, view.InventoryReady, view.InventoryRequestPending, view.InventoryStatusMessage, "Витрина сияющей фракции готова."),
                        Tone = view.TradeBlocked ? UiTone.Warning : view.InventoryReady ? UiTone.Accent : UiTone.Muted
                    },
                    new UiTextBlock
                    {
                        Text = "Продажа сияющим фракциям пока не поддержана текущими правилами торговли; браузер не создаёт отдельную механику продажи.",
                        Tone = UiTone.Muted
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Фракция", view.FactionName),
                            KeyValue("Чернильные Перья", feathers.ToString()),
                            KeyValue("Уровень торговли", view.TradeTier.ToString()),
                            KeyValue("Витрина", view.InventoryReady ? "готова" : view.InventoryRequestPending ? "ожидает ГМ" : "нужно запросить")
                        ]
                    },
                    new UiTableBlock
                    {
                        Title = "Покупка",
                        Columns = ["Реликвия", "Редкость", "Цена", "Статус"],
                        Rows = view.Offers
                            .Select(offer => Row(
                                offer.Name,
                                FirstNonEmpty(offer.Rarity, "-"),
                                offer.PriceInFeathers.ToString(),
                                offer.SoldOut ? "куплено" : feathers < offer.PriceInFeathers ? "не хватает перьев" : "доступно"))
                            .ToList()
                    }
                ]
            }
        };

        if (options.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "shining_trade_choice",
                    Prompt = "Действие торговли",
                    Required = true,
                    Options = options
                },
                TradeConfirmationPrompt()
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildGuardianTradeAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var guardianId = ReadCommandArguments(command);
        if (string.IsNullOrWhiteSpace(guardianId))
        {
            return Result(
                command,
                localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Info, "Торговля хранителя", "Укажите Хранителя командой /guardian_trade <guardian_id> или введите ID Хранителя в форме.")
                ],
                prompts:
                [
                    new UiTextInputPrompt
                    {
                        Id = "guardian_id",
                        Prompt = "ID хранителя",
                        Required = true,
                        Placeholder = "guardianId"
                    },
                    new UiSelectionPrompt
                    {
                        Id = "guardian_trade_choice",
                        Prompt = "Действие торговли",
                        Required = true,
                        Options =
                        [
                            Option("request:__selected__", "Запросить витрину", "Попросить ГМа подготовить ассортимент выбранного Хранителя.")
                        ]
                    },
                    TradeConfirmationPrompt()
                ]);
        }

        var service = new GuardianTradeService(fs, NullLogger<GuardianTradeService>.Instance);
        var currentTurn = Math.Max(1, stateManager.CurrentState.TurnNumber);
        var currentIncarnation = Math.Max(1, stateManager.CurrentState.Incarnation);
        var view = await service.EnsureTradeInventoryAsync(guardianId, currentIncarnation, currentTurn, createPendingRequests: false);
        if (view == null)
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(UiNotificationSeverity.Warning, "Хранитель не найден", "Такого Хранителя сейчас нет в доступной Обители.")
                ]);
        }

        var sellOffers = await service.GetSellableRelicsAsync(view.GuardianId);
        var soul = (await ReadJson(fs, SoulStatePath)).Node as JsonObject;
        var feathers = GetSoulInkFeathers(soul, stateManager.CurrentState.InkFeathers);
        var options = new List<UiSelectionOption>();
        if (!view.TradeBlocked && !view.InventoryReady && !view.InventoryRequestPending)
            options.Add(Option($"request:{view.GuardianId}", "Запросить витрину", "Попросить ГМа подготовить ассортимент Хранителя."));
        options.AddRange(view.Offers.Select(offer => new UiSelectionOption
        {
            Value = $"buy:{offer.SlotId}",
            Label = $"Купить: {offer.Name}",
            Description = $"{offer.PriceInFeathers} Чернильных Перьев; редкость: {FirstNonEmpty(offer.Rarity, "не указана")}",
            Disabled = offer.SoldOut || feathers < offer.PriceInFeathers
        }));
        options.AddRange(sellOffers.Select(offer => Option(
            $"sell:{offer.RelicId}",
            $"Продать: {offer.Name}",
            $"+{offer.PriceInFeathers} Чернильных Перьев; редкость: {FirstNonEmpty(offer.Rarity, "не указана")}")));
        options.AddRange(view.BuybackOffers.Select(offer => new UiSelectionOption
        {
            Value = $"buyback:{offer.BuybackEntryId}",
            Label = $"Выкупить: {offer.Name}",
            Description = $"{offer.PriceInFeathers} Чернильных Перьев; ранее продано за {offer.SoldForPrice}",
            Disabled = feathers < offer.PriceInFeathers
        }));

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Торговля хранителя",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = BuildTradeStatusText(view.TradeBlocked, view.BlockReason, view.InventoryReady, view.InventoryRequestPending, view.InventoryStatusMessage, "Витрина Хранителя готова."),
                        Tone = view.TradeBlocked ? UiTone.Warning : view.InventoryReady ? UiTone.Accent : UiTone.Muted
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Хранитель", view.GuardianName),
                            KeyValue("Домен", view.DomainDisplay),
                            KeyValue("Чернильные Перья", feathers.ToString()),
                            KeyValue("Витрина", view.InventoryReady ? "готова" : view.InventoryRequestPending ? "ожидает ГМ" : "нужно запросить")
                        ]
                    },
                    new UiTableBlock
                    {
                        Title = "Покупка",
                        Columns = ["Реликвия", "Редкость", "Цена", "Статус"],
                        Rows = view.Offers
                            .Select(offer => Row(
                                offer.Name,
                                FirstNonEmpty(offer.Rarity, "-"),
                                offer.PriceInFeathers.ToString(),
                                offer.SoldOut ? "куплено" : feathers < offer.PriceInFeathers ? "не хватает перьев" : "доступно"))
                            .ToList()
                    },
                    new UiTableBlock
                    {
                        Title = "Продажа реликвий",
                        Columns = ["Реликвия", "Редкость", "Цена"],
                        Rows = sellOffers
                            .Select(offer => Row(offer.Name, FirstNonEmpty(offer.Rarity, "-"), offer.PriceInFeathers.ToString()))
                            .ToList()
                    },
                    new UiTableBlock
                    {
                        Title = "Обратный выкуп",
                        Columns = ["Реликвия", "Редкость", "Цена"],
                        Rows = view.BuybackOffers
                            .Select(offer => Row(offer.Name, FirstNonEmpty(offer.Rarity, "-"), offer.PriceInFeathers.ToString()))
                            .ToList()
                    }
                ]
            }
        };

        if (options.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "guardian_trade_choice",
                    Prompt = "Действие торговли",
                    Required = true,
                    Options = options
                },
                TradeConfirmationPrompt()
            ]);
    }

    private static UiConfirmationPrompt TradeConfirmationPrompt() => new()
    {
        Id = "confirm_trade_write",
        Prompt = "Подтвердить сделку",
        Required = true,
        DefaultValue = false
    };

    private static string BuildTradeStatusText(
        bool tradeBlocked,
        string? blockReason,
        bool inventoryReady,
        bool inventoryRequestPending,
        string? inventoryStatusMessage,
        string readyMessage)
    {
        if (tradeBlocked)
            return SanitizeBrowserTradeStatusText(FirstNonEmpty(blockReason, "Торговля сейчас недоступна."));
        if (inventoryReady)
            return readyMessage;
        if (!string.IsNullOrWhiteSpace(inventoryStatusMessage))
            return SanitizeBrowserTradeStatusText(inventoryStatusMessage!);
        return inventoryRequestPending
            ? "Витрина уже запрошена и ждёт ответа ГМ."
            : "Витрина ещё не подготовлена; можно отправить запрос ассортимента.";
    }

    private static string SanitizeBrowserTradeStatusText(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Торговля сейчас недоступна.";

        return ContainsBrowserTradeDiagnosticFragment(message)
            ? "Торговля временно ждёт проверки ГМ. Завершите или обновите текущий торговый запрос, затем повторите действие."
            : message;
    }

    private static bool ContainsBrowserTradeDiagnosticFragment(string value) =>
        value.Contains(".json", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("pending_", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("canonical", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("contract", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("requestId=", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("slotId", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("repair", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("cleanup", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("raw", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("debug", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("game_state/", StringComparison.OrdinalIgnoreCase);

    private static async Task<ExplorerCommandResult> BuildSoulRelicEquipAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var context = await SoulRelicEquipmentService.ReadContextAsync(fs);
        var requestedRelic = SoulRelicEquipmentService.ReadFirstCommandArgument(command);
        var stored = context?.Stored.ToList() ?? [];
        if (!string.IsNullOrWhiteSpace(requestedRelic))
        {
            var matched = stored.FirstOrDefault(relic =>
                string.Equals(relic.RelicId, requestedRelic, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(relic.Name, requestedRelic, StringComparison.OrdinalIgnoreCase));
            if (matched != null)
                stored = [matched];
        }

        var rows = stored
            .Select(static relic => Row(
                string.IsNullOrWhiteSpace(relic.Name) ? relic.RelicId : relic.Name,
                string.IsNullOrWhiteSpace(relic.Rarity) ? "-" : relic.Rarity,
                string.IsNullOrWhiteSpace(relic.RelicId) ? "-" : relic.RelicId))
            .ToList();
        var slotOptions = BuildSoulRelicEquipSlotOptions(stored);

        var statusText = stored.Count == 0
            ? "В хранилище нет реликвий, доступных для экипировки."
            : "Выберите реликвию и слот. Запись выполняется локально после проверки хода и блокировки интерфейса.";

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Экипировка реликвии души",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = statusText,
                        Tone = stored.Count == 0 ? UiTone.Muted : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Доступные реликвии",
                        Columns = ["Название", "Редкость", "ID"],
                        Rows = rows
                    }
                ]
            }
        };

        if (stored.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "soul_relic_identity",
                    Prompt = "Реликвия",
                    Required = true,
                    Options = stored
                        .Select(static relic => Option(
                            string.IsNullOrWhiteSpace(relic.RelicId) ? relic.Name : relic.RelicId,
                            string.IsNullOrWhiteSpace(relic.Name) ? relic.RelicId : relic.Name,
                            "Редкость: " + (string.IsNullOrWhiteSpace(relic.Rarity) ? "не указана" : relic.Rarity)))
                        .ToList()
                },
                new UiSelectionPrompt
                {
                    Id = "soul_relic_slot",
                    Prompt = "Слот реликвии",
                    Required = true,
                    Options = slotOptions
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_soul_relic_write",
                    Prompt = "Подтвердить экипировку реликвии",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildSoulRelicUnequipAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var context = await SoulRelicEquipmentService.ReadContextAsync(fs);
        var equipped = context?.Equipped
            .Where(static relic => !string.IsNullOrWhiteSpace(relic.CurrentSlot))
            .ToList() ?? [];

        var rows = equipped
            .Select(static relic => Row(
                string.IsNullOrWhiteSpace(relic.Name) ? relic.RelicId : relic.Name,
                SoulRelicEquipmentService.SlotLabels.TryGetValue(relic.CurrentSlot, out var slotLabel)
                    ? slotLabel
                    : (string.IsNullOrWhiteSpace(relic.CurrentSlot) ? "-" : relic.CurrentSlot)))
            .ToList();

        var statusText = equipped.Count == 0
            ? "Сейчас нет экипированных реликвий."
            : "Выберите слот и подтвердите снятие реликвии в хранилище.";

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Снятие реликвии души",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = statusText,
                        Tone = equipped.Count == 0 ? UiTone.Muted : UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Экипировано",
                        Columns = ["Реликвия", "Слот"],
                        Rows = rows
                    }
                ]
            }
        };

        if (equipped.Count == 0)
            return Result(command, CommandExecutionState.Completed, blocks);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "soul_relic_slot",
                    Prompt = "Какой слот освободить",
                    Required = true,
                    Options = equipped
                        .Select(relic => Option(
                            relic.CurrentSlot,
                            $"{SoulRelicEquipmentService.SlotLabels.GetValueOrDefault(relic.CurrentSlot, relic.CurrentSlot)}: {(string.IsNullOrWhiteSpace(relic.Name) ? relic.RelicId : relic.Name)}",
                            ""))
                        .ToList()
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_soul_relic_write",
                    Prompt = "Подтвердить снятие реликвии",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static List<UiSelectionOption> BuildSoulRelicEquipSlotOptions(IReadOnlyList<SoulRelicItem> stored)
    {
        var slots = new List<string>();
        foreach (var relic in stored)
        {
            foreach (var compatibleSlot in relic.CompatibleSlots)
            {
                if (!string.IsNullOrWhiteSpace(compatibleSlot) &&
                    !slots.Contains(compatibleSlot, StringComparer.OrdinalIgnoreCase))
                {
                    slots.Add(compatibleSlot);
                }
            }
        }

        if (slots.Count == 0 || stored.Count != 1)
        {
            foreach (var slot in SoulRelicEquipmentService.SlotLabels.Keys)
            {
                if (!slots.Contains(slot, StringComparer.OrdinalIgnoreCase))
                    slots.Add(slot);
            }
        }

        return slots
            .Select(static slot => Option(slot, SoulRelicEquipmentService.FormatSlotLabel(slot), ""))
            .ToList();
    }

    private static async Task<ExplorerCommandResult> BuildCraftAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var recipes = await ReadJson(fs, "game_state/inventory/recipes.json");
        var recipeRows = CollectObjects(recipes.Node)
            .Where(static item => !string.IsNullOrWhiteSpace(FirstNonEmpty(GetString(item, "recipeName"), GetString(item, "name"))))
            .Select(static item => Row(
                FirstNonEmpty(GetString(item, "recipeName"), GetString(item, "name"), "?"),
                FirstNonEmpty(GetString(item, "craftedItemName"), GetString(item, "resultItemName"), "-"),
                FirstNonEmpty(GetString(item, "recipeRank"), "-")))
            .ToList();

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Ремесло",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = "Браузерная команда показывает рецепты и отправляет pending-запрос ремесла через безопасный interactive/write protocol.",
                        Tone = UiTone.Accent
                    },
                    new UiTableBlock
                    {
                        Title = "Известные рецепты",
                        Columns = ["Рецепт", "Результат", "Ранг"],
                        Rows = recipeRows
                    }
                ]
            }
        };
        AddRawOrWarning(blocks, "JSON: recipes", recipes);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiTextInputPrompt { Id = "recipe_id", Prompt = "Рецепт или название", Placeholder = "recipeId / recipeName" },
                new UiLongTextInputPrompt
                {
                    Id = "craft_intent",
                    Prompt = "Описание ремесленного действия",
                    Placeholder = "Что именно создать, какие материалы использовать, какие ограничения соблюсти.",
                    MinLines = 2,
                    MaxLines = 6
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildGachaAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs, playerFacing: true);
        var soul = await ReadJson(fs, SoulStatePath);
        var soulRoot = soul.Node as JsonObject;
        var arguments = ReadCommandArguments(command);
        var currentRealm = FirstNonEmpty(GetString(soulRoot, "currentRealm"), stateManager.CurrentState.CurrentRealm);
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            return Result(
                command,
                CommandExecutionState.Failed,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Error,
                        "Некорректные аргументы",
                        "Команда /gacha не принимает аргументы. Выберите поддерживаемый прямой призыв Моря Хаоса через браузерную форму.")
                ]);
        }

        if (!RealmSemantics.IsChaosSea(currentRealm))
        {
            return Result(
                command,
                CommandExecutionState.Completed,
                [
                    localTurn.Panel,
                    Message(
                        UiNotificationSeverity.Warning,
                        "Призыв недоступен",
                        $"Прямой призыв Моря Хаоса доступен только в Ordinary Chaos Sea (currentRealm=Chaos Sea/Море Хаоса). Текущий realm: {FirstNonEmpty(currentRealm, "не определён")}.")
                ]);
        }

        var availableFeathers = Math.Max(0, GetSoulInkFeathers(soulRoot, stateManager.CurrentState.InkFeathers));
        var pendingTurnState = new PendingTurnStateService(fs, NullLogger<PendingTurnStateService>.Instance);
        var pendingState = await pendingTurnState.GetOrCreateAsync();
        var gachaBase = pendingState.GachaBaseResult;
        var baseRarity = FirstNonEmpty(gachaBase?.BaseRarity, "Common");
        var baseScore = (gachaBase?.BaseScore ?? 0).ToString();
        var formula = FirstNonEmpty(gachaBase?.Formula, "client-computed gacha base (range 4-80)");
        var diceUsed = FormatDiceUsed(gachaBase?.DiceUsed);

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Призыв судьбы",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = availableFeathers > 0
                            ? "Доступен прямой нейтральный призыв из Моря Хаоса. Хранители, репутация, заряды и скидки в этом баннере не участвуют."
                            : "Чернильных Перьев сейчас нет. Прямой призыв из Моря Хаоса недоступен.",
                        Tone = availableFeathers > 0 ? UiTone.Accent : UiTone.Warning
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Чернильные Перья", availableFeathers.ToString()),
                            KeyValue("Поддерживаемые баннеры", "Прямой призыв Моря Хаоса"),
                            KeyValue("Результат", "ГМ материализует ровно одну Реликвию Души после обычного хода")
                        ]
                    },
                    new UiTableBlock
                    {
                        Title = "Доступные баннеры",
                        Columns = ["Баннер", "Стоимость", "Шансы", "Разрешение"],
                        Rows =
                        [
                            Row(
                                "Прямой призыв Моря Хаоса",
                                availableFeathers > 0 ? $"1-{availableFeathers} Чернильных Перьев" : "нет доступных Перьев",
                                "Пороги: 4-48 Common, 49-67 Uncommon, 68-75 Rare, 76-79 Epic, 80 Legendary",
                                "Итоговая редкость точно равна базовой; без модификаторов Хранителей")
                        ]
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Кубики базового результата", diceUsed),
                            KeyValue("Базовый счёт", baseScore),
                            KeyValue("Базовая редкость", baseRarity),
                            KeyValue("Формула", formula)
                        ]
                    }
                ]
            }
        };

        if (availableFeathers <= 0)
        {
            blocks.Add(Message(
                UiNotificationSeverity.Warning,
                "Призыв недоступен",
                "Нужно хотя бы 1 Чернильное Перо."));
            return Result(command, CommandExecutionState.Completed, blocks);
        }

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "gacha_banner",
                    Prompt = "Баннер призыва",
                    Required = true,
                    Options =
                    [
                        Option(
                            "direct_chaos_sea",
                            "Прямой призыв Моря Хаоса",
                            $"Нейтральный призыв без Хранителя. Стоимость: 1-{availableFeathers} Чернильных Перьев.")
                    ]
                },
                new UiTextInputPrompt
                {
                    Id = "feather_cost",
                    Prompt = "Сколько Чернильных Перьев потратить",
                    Required = true,
                    Placeholder = $"1-{availableFeathers}",
                    DefaultValue = "1"
                },
                new UiConfirmationPrompt
                {
                    Id = "confirm_gacha_pull",
                    Prompt = "Подтвердить списание Перьев и подготовить прямой призыв",
                    Required = true,
                    DefaultValue = false
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildAbodeOfferingAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var guardians = await ReadJson(fs, "game_state/meta/guardians.json");
        var pending = await ReadJson(fs, GuardianAbodeOfferingState.PendingRequestPath);
        var guardianRows = CollectObjects(guardians.Node)
            .Where(static item => !string.IsNullOrWhiteSpace(FirstNonEmpty(GetString(item, "guardianId"), GetString(item, "id"))))
            .Select(static item => Row(
                FirstNonEmpty(GetString(item, "canonicalName"), GetString(item, "guardianName"), GetString(item, "name"), "?"),
                FirstNonEmpty(GetString(item, "guardianId"), GetString(item, "id"), "-"),
                FirstNonEmpty(GetString(item["abode"] as JsonObject, "name"), GetString(item, "abodeName"), "-")))
            .ToList();

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Подношение Обители",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = stateManager.CurrentState.IsInAfterlifeRealm
                            ? "Браузерный протокол показывает цель, тип подношения и pending-contract состояние. Запись destructive pending-файла выполняется только через безопасный interactive/write flow."
                            : "Подношение Обители доступно только в загробье.",
                        Tone = stateManager.CurrentState.IsInAfterlifeRealm ? UiTone.Accent : UiTone.Warning
                    },
                    new UiTableBlock
                    {
                        Title = "Известные Хранители",
                        Columns = ["Хранитель", "ID", "Обитель"],
                        Rows = guardianRows
                    }
                ]
            }
        };
        AddRawOrWarning(blocks, "JSON: pending_abode_offering", pending);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiTextInputPrompt { Id = "guardian_id", Prompt = "ID Хранителя / Обители", Placeholder = "guardianId" },
                new UiSelectionPrompt
                {
                    Id = "offering_type",
                    Prompt = "Тип подношения",
                    Required = true,
                    Options =
                    [
                        Option("ink_feathers", "Чернильные Перья", "Подношение валютой в пределах лимита возвращения."),
                        Option("soul_relic", "Реликвия Души", "Destructive offering: реликвия будет изъята клиентом."),
                        Option("lore_fragment", "Фрагмент Знания", "Destructive offering: запись Архива будет изъята клиентом."),
                        Option("secret_record", "Запись Тайны", "Destructive offering: запись Архива будет изъята клиентом.")
                    ]
                },
                new UiTextInputPrompt { Id = "offering_value", Prompt = "Сумма или ID предмета/записи", Placeholder = "50 / relicId / archiveId" }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildPlayerGuardianFoundationAsync(string command, FileSystemManager fs)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var context = await PlayerGuardianFoundationState.ReadContextAsync(fs);
        var pending = await ReadJson(fs, PlayerGuardianFoundationState.PendingRequestPath);
        var status = context.PendingRequest != null
            ? "Ритуал уже подготовлен и ждёт хода GM."
            : context.HasCompletedFoundation
                ? "Ветка основания собственной мантии уже завершена."
                : context.CanCreateRequest
                    ? "Можно подготовить новую Хранительскую мантию."
                    : context.BlockingReason;

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Основание собственной мантии",
                Blocks =
                [
                    new UiTextBlock { Text = status, Tone = context.CanCreateRequest ? UiTone.Accent : UiTone.Warning },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Имя души", context.SoulName),
                            KeyValue("Прежний покровитель", context.PreviousGuardianName),
                            KeyValue("Завершено", context.HasCompletedFoundation ? "да" : "нет")
                        ]
                    }
                ]
            }
        };
        AddRawOrWarning(blocks, "JSON: pending_player_guardian_foundation", pending);

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiTextInputPrompt { Id = "proposed_display_name", Prompt = "Имя новой мантии", Required = true },
                new UiLongTextInputPrompt { Id = "mantle_summary", Prompt = "Краткая сущность", Required = true, MinLines = 2, MaxLines = 6 },
                new UiLongTextInputPrompt { Id = "mantle_creed", Prompt = "Кредо", Required = true, MinLines = 2, MaxLines = 6 },
                new UiTextInputPrompt { Id = "appearance_motifs", Prompt = "Образные мотивы через запятую", Required = true },
                new UiSelectionPrompt
                {
                    Id = "dominant_aspect",
                    Prompt = "Доминирующий аспект",
                    Options =
                    [
                        Option("", "Без доминирующего аспекта", ""),
                        Option("memory", "Память", ""),
                        Option("forge", "Кузня", ""),
                        Option("knowledge", "Знание", ""),
                        Option("patronage", "Покровительство", ""),
                        Option("power", "Власть", ""),
                        Option("path", "Путь", "")
                    ],
                    AllowCustom = true
                }
            ]);
    }

    private static async Task<ExplorerCommandResult> BuildSpiritualActionAsync(
        string command,
        FileSystemManager fs,
        StateManager stateManager)
    {
        var localTurn = BuildLocalTurnStatus(fs);
        var conflictRoot = await ReadJson(fs, AfterlifeSpiritualConflictState.StatePath);
        var active = (conflictRoot.Node as JsonObject)?["activeConflict"] as JsonObject;
        var conflictId = FirstNonEmpty(GetString(active, "conflictId"), "нет активного конфликта");

        var blocks = new List<UiBlock>
        {
            localTurn.Panel,
            new UiPanelBlock
            {
                Title = "Духовное действие",
                Blocks =
                [
                    new UiTextBlock
                    {
                        Text = active == null
                            ? "Нет активного духовного конфликта. ГМ должен начать конфликт через принятый ход, либо игрок может описывать обычную прозу без явного route tag."
                            : "Опишите одно намерение в активном духовном конфликте. Браузерный DTO фиксирует route tag и форму, но не вызывает console-bound отправку хода.",
                        Tone = active == null ? UiTone.Warning : UiTone.Accent
                    },
                    new UiKeyValueGridBlock
                    {
                        Items =
                        [
                            KeyValue("Текущий realm", stateManager.CurrentState.CurrentRealm ?? "?"),
                            KeyValue("Активный конфликт", conflictId),
                            KeyValue("Response surface", AfterlifeSpiritualConflictState.ResponseField),
                            KeyValue("State file", AfterlifeSpiritualConflictState.StatePath)
                        ]
                    }
                ]
            }
        };
        AddRawOrWarning(blocks, "JSON: active afterlife spiritual conflict", new JsonReadResult(AfterlifeSpiritualConflictState.StatePath, conflictRoot.FileExists, active?.DeepClone(), conflictRoot.Error));

        return Result(
            command,
            localTurn.HasActiveGmTurn ? CommandExecutionState.Pending : CommandExecutionState.RequiresInput,
            blocks,
            prompts:
            [
                new UiSelectionPrompt
                {
                    Id = "operation_type",
                    Prompt = "Духовное искусство / действие",
                    Required = true,
                    Options =
                    [
                        Option("pressure", "Давление", "Ухудшить напряжение противника."),
                        Option("guard", "Защита", "Снизить или предотвратить входящий вред."),
                        Option("maneuver", "Манёвр", "Улучшить позицию конфликта."),
                        Option("counter", "Контрприём", "Рискованно развернуть конкретное входящее действие."),
                        Option("binding", "Оковы", "Создать или усилить контроль при наличии преимущества/подготовки."),
                        Option("break_binding", "Разрыв оков", "Ослабить или снять контроль противника."),
                        Option("recover_spiritual_power", "Восстановить духовную силу", "Восстановить ОД, рискуя попасть под активное давление."),
                        Option("negotiate", "Переговоры", "Попытаться завершить или изменить условия конфликта.")
                    ],
                    AllowCustom = true
                },
                new UiLongTextInputPrompt
                {
                    Id = "spiritual_action_text",
                    Prompt = "Художественное описание действия",
                    Required = true,
                    Placeholder = "Опишите, что именно делает душа игрока.",
                    MinLines = 3,
                    MaxLines = 10
                }
            ]);
    }

    private static LocalTurnStatus BuildLocalTurnStatus(FileSystemManager fs, bool playerFacing = false)
    {
        var entries = new List<UiTableRow>();
        AddArtifact(entries, fs, TurnRequestPath, "Запрос хода GM", playerFacing);
        AddArtifact(entries, fs, TurnCompletePath, "Готов успешный ответ", playerFacing);
        AddArtifact(entries, fs, TurnErrorPath, playerFacing ? "Готова ошибка хода" : "Готов terminal error", playerFacing);
        AddArtifact(entries, fs, PendingTurnSnapshotManifestPath, playerFacing ? "Снимок состояния хода" : "Validated pending snapshot", playerFacing);
        AddDirectoryArtifact(entries, fs, PendingTurnSnapshotDirectory, playerFacing ? "Копии файлов текущего хода" : "Копии snapshot файлов", playerFacing);
        AddDirectoryArtifact(entries, fs, ExplorerRollbackDirectory, playerFacing ? "Копии восстановления локальной записи" : "Локальные rollback backup", playerFacing);

        var hasActive = fs.FileExists(TurnRequestPath) ||
                        fs.FileExists(TurnCompletePath) ||
                        fs.FileExists(TurnErrorPath) ||
                        fs.FileExists(PendingTurnSnapshotManifestPath) ||
                        DirectoryHasContent(fs, PendingTurnSnapshotDirectory) ||
                        DirectoryHasContent(fs, ExplorerRollbackDirectory);

        var message = playerFacing
            ? hasActive
                ? "Активный ход GM или локальное восстановление обнаружены. Дождитесь завершения, ошибки или отмены текущего хода перед локальной формой."
                : "Активный ход GM не обнаружен. Можно открыть форму локального действия."
            : hasActive
                ? "Активный ход GM или локальный rollback/snapshot обнаружен. Browser-write команды должны дождаться завершения, ошибки или отмены этого протокола."
                : "Активный ход GM не обнаружен. Browser DTO может безопасно показать форму локального действия.";

        return new LocalTurnStatus(
            hasActive,
            new UiPanelBlock
            {
                Title = playerFacing ? "Локальный ход" : "Локальный ход / GM-turn protocol",
                Blocks =
                [
                    Message(hasActive ? UiNotificationSeverity.Warning : UiNotificationSeverity.Info, hasActive ? "Активный ход GM" : "Локальный ход свободен", message),
                    new UiTableBlock
                    {
                        Title = playerFacing ? "Состояние локальной записи" : "Артефакты протокола",
                        Columns = playerFacing ? ["Проверка", "Статус"] : ["Артефакт", "Путь", "Статус"],
                        Rows = entries
                    }
                ]
            });
    }

    private static void AddArtifact(List<UiTableRow> rows, FileSystemManager fs, string path, string label, bool playerFacing) =>
        rows.Add(playerFacing
            ? Row(label, fs.FileExists(path) ? "есть" : "нет")
            : Row(label, path, fs.FileExists(path) ? "есть" : "нет"));

    private static void AddDirectoryArtifact(List<UiTableRow> rows, FileSystemManager fs, string path, string label, bool playerFacing) =>
        rows.Add(playerFacing
            ? Row(label, DirectoryHasContent(fs, path) ? "есть файлы" : "нет файлов")
            : Row(label, path, DirectoryHasContent(fs, path) ? "есть файлы" : "нет файлов"));

    private static bool DirectoryHasContent(FileSystemManager fs, string path)
    {
        var fullPath = fs.ResolvePath(path);
        return Directory.Exists(fullPath) && Directory.EnumerateFileSystemEntries(fullPath, "*", SearchOption.AllDirectories).Any();
    }

    private static async Task<JsonReadResult> ReadJson(FileSystemManager fs, string path)
    {
        var raw = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return new JsonReadResult(path, fs.FileExists(path), null, string.Empty);

        try
        {
            return new JsonReadResult(path, true, JsonNode.Parse(raw), string.Empty);
        }
        catch (Exception ex)
        {
            return new JsonReadResult(path, true, null, ex.Message);
        }
    }

    private static void AddRawOrWarning(List<UiBlock> blocks, string title, JsonReadResult read)
    {
        if (!read.FileExists)
            blocks.Add(Message(UiNotificationSeverity.Info, title, $"Файл не найден: {read.Path}"));
        else if (!string.IsNullOrWhiteSpace(read.Error))
            blocks.Add(Message(UiNotificationSeverity.Error, title, $"JSON повреждён: {read.Error}"));
        else if (read.Node != null)
            blocks.Add(Raw(title, read.Node));
        else
            blocks.Add(Message(UiNotificationSeverity.Warning, title, $"Файл пуст или не содержит JSON: {read.Path}"));
    }

    private static IEnumerable<JsonObject> CollectObjects(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var item in EnumerateKnownArrays(obj))
                yield return item;

            if (obj.Count > 0)
                yield return obj;
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr.OfType<JsonObject>())
                yield return item;
        }
    }

    private static IEnumerable<JsonObject> EnumerateKnownArrays(JsonObject obj)
    {
        foreach (var prop in obj)
        {
            if (prop.Value is JsonArray arr)
            {
                foreach (var item in arr.OfType<JsonObject>())
                    yield return item;
            }
            else if (prop.Value is JsonObject child)
            {
                foreach (var item in EnumerateKnownArrays(child))
                    yield return item;
            }
        }
    }

    private static IEnumerable<NpcConversationOption> CollectNpcConversationOptions(JsonNode? node)
    {
        return CollectObjects(node)
            .Select(static obj =>
            {
                var stableId = FirstNonEmpty(GetString(obj, "npcId"), GetString(obj, "id"));
                var name = FirstNonEmpty(GetString(obj, "name"), GetString(obj, "npcName"), GetString(obj, "displayName"), stableId);
                if (string.IsNullOrWhiteSpace(stableId) || string.IsNullOrWhiteSpace(name))
                    return null;

                return new NpcConversationOption(
                    stableId,
                    name,
                    FirstNonEmpty(GetString(obj, "currentLocation"), GetString(obj, "currentLocationId"), GetString(obj, "location")),
                    FirstNonEmpty(GetString(obj, "relationshipLevel"), GetString(obj, "relationship"), GetString(obj, "attitude")));
            })
            .Where(static npc => npc != null)
            .Cast<NpcConversationOption>()
            .DistinctBy(static npc => npc.NpcId, StringComparer.OrdinalIgnoreCase);
    }

    private static NpcConversationOption? ResolveNpcConversationOption(
        IReadOnlyList<NpcConversationOption> npcs,
        string requestedNpcId)
    {
        if (string.IsNullOrWhiteSpace(requestedNpcId))
            return null;

        return npcs.FirstOrDefault(npc =>
                   string.Equals(npc.NpcId, requestedNpcId, StringComparison.OrdinalIgnoreCase)) ??
               npcs.FirstOrDefault(npc =>
                   string.Equals(npc.Name, requestedNpcId, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeCommand(string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[0].ToLowerInvariant();
    }

    private static string ReadCommandArguments(string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length < 2 ? string.Empty : parts[1].Trim();
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string GetString(JsonObject? obj, string propertyName)
    {
        if (obj == null || !obj.TryGetPropertyValue(propertyName, out var node) || node == null)
            return string.Empty;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text ?? string.Empty;
            if (value.TryGetValue<int>(out var number))
                return number.ToString();
            if (value.TryGetValue<bool>(out var boolean))
                return boolean ? "true" : "false";
        }

        return node.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
    }

    private static int TryGetInt(JsonObject? obj, string propertyName, int fallback)
    {
        if (obj == null || !obj.TryGetPropertyValue(propertyName, out var node) || node == null)
            return fallback;

        if (node is JsonValue value && value.TryGetValue<int>(out var parsed))
            return parsed;

        return fallback;
    }

    private static int GetSoulInkFeathers(JsonObject? soulRoot, int fallback)
    {
        if (soulRoot == null)
            return fallback;

        if (!soulRoot.TryGetPropertyValue("inkFeathers", out var node) || node == null)
            return fallback;

        if (node is JsonObject inkFeathers)
            return TryGetInt(inkFeathers, "current", fallback);

        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number))
                return number;
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed))
                return parsed;
        }

        return fallback;
    }

    private static string FormatDiceUsed(JsonArray? diceUsed)
    {
        if (diceUsed == null || diceUsed.Count == 0)
            return "[]";

        var values = diceUsed
            .Select(static node => node is JsonValue value
                ? value.TryGetValue<int>(out var number)
                    ? number.ToString()
                    : value.TryGetValue<string>(out var text)
                        ? text
                        : node.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)
                : node?.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) ?? "null")
            .ToArray();
        return "[" + string.Join(", ", values) + "]";
    }

    private static string FormatDiceUsed(IReadOnlyCollection<int>? diceUsed)
    {
        if (diceUsed == null || diceUsed.Count == 0)
            return "[]";

        return "[" + string.Join(", ", diceUsed) + "]";
    }

    private static bool TryGetBool(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            return false;

        return value.TryGetValue<bool>(out var parsed) && parsed;
    }

    private static ExplorerCommandResult Result(
        string command,
        CommandExecutionState state,
        IEnumerable<UiBlock> blocks,
        IEnumerable<UiAction>? actions = null,
        IEnumerable<UiPrompt>? prompts = null) =>
        new()
        {
            Command = command,
            State = state,
            Blocks = blocks.ToList(),
            Actions = actions?.ToList() ?? [],
            Prompts = prompts?.ToList() ?? []
        };

    private static UiTableRow Row(params string[] cells) => new() { Cells = cells.ToList() };

    private static UiKeyValueItem KeyValue(string key, string value) => new() { Key = key, Value = value };

    private static UiSelectionOption Option(string value, string label, string description) =>
        new() { Value = value, Label = label, Description = description };

    private static UiMessageBlock Message(UiNotificationSeverity severity, string title, string message) =>
        new() { Severity = severity, Title = title, Message = message };

    private static UiRawJsonBlock Raw(string title, JsonNode node) =>
        new() { Title = title, Json = node.DeepClone() };

    private sealed record NpcConversationOption(
        string NpcId,
        string Name,
        string Location,
        string Relationship);

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);

    private sealed record LocalTurnStatus(bool HasActiveGmTurn, UiPanelBlock Panel);
}
