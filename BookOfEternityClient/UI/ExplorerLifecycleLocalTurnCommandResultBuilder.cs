using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public static class ExplorerLifecycleLocalTurnCommandResultBuilder
{
    private const string TurnRequestPath = "input/turn_request.json";
    private const string TurnCompletePath = "ready/turn_complete.json";
    private const string TurnErrorPath = "ready/turn_error.json";
    private const string PendingTurnSnapshotManifestPath = "game_state/control/pending_turn_snapshot.json";
    private const string PendingTurnSnapshotDirectory = "game_state/control/pending_turn_snapshot";
    private const string ExplorerRollbackDirectory = "game_state/control/explorer_local_turn_rollback";

    public static bool CanBuild(string command) => NormalizeCommand(command) switch
    {
        "/validate" or "/валидация" or
        "/world_setup" or "/настройка_мира" or
        "/distribute" or "/распределить" or
        "/companion_directive" or "/директива_компаньону" or
        "/faction_directive" or "/директива_фракции" or
        "/equip" or "/экипировать" or
        "/unequip" or "/снять" or
        "/craft" or "/ремесло" or
        "/abode_offering" or "/подношение_обители" or
        "/found_guardian_mantle" or "/учредить_хранителя" or
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
            "/equip" or "/экипировать" => await BuildInventoryEquipAsync(command, fs),
            "/unequip" or "/снять" => await BuildInventoryUnequipAsync(command, fs),
            "/craft" or "/ремесло" => await BuildCraftAsync(command, fs),
            "/abode_offering" or "/подношение_обители" => await BuildAbodeOfferingAsync(command, fs, stateManager),
            "/found_guardian_mantle" or "/учредить_хранителя" => await BuildPlayerGuardianFoundationAsync(command, fs),
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

    private static string NormalizeCommand(string command)
    {
        var parts = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[0].ToLowerInvariant();
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

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);

    private sealed record LocalTurnStatus(bool HasActiveGmTurn, UiPanelBlock Panel);
}
