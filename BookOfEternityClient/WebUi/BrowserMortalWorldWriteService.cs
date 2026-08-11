using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserMortalWorldWriteService
{
    public const string PendingCraftRequestPath = CraftRequestState.PendingRequestPath;

    private const string StatPointsPath = "game_state/player/stat_points.json";
    private const string CharacteristicsPath = "game_state/misc/characteristics.json";
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private const string FactionCorePath = "game_state/factions/faction_core.json";
    private const string SoulStatePath = "game_state/meta/soul_state.json";

    private readonly FileSystemManager _fs;
    private readonly BrowserLocalWriteCoordinator _coordinator;
    private readonly ScenarioCoreService _scenarioCoreService;
    private readonly TimeProvider _timeProvider;

    public BrowserMortalWorldWriteService(
        FileSystemManager fs,
        BrowserLocalWriteCoordinator coordinator,
        ScenarioCoreService scenarioCoreService,
        TimeProvider? timeProvider = null)
    {
        _fs = fs;
        _coordinator = coordinator;
        _scenarioCoreService = scenarioCoreService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<BrowserPromptWriteResult> TryApplyAsync(
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        try
        {
            return await RunMortalWorldTransactionAsync(
                writeLease => TryApplyBoundAsync(writeLease, command, answers, owner));
        }
        catch (SessionReplacedException)
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Failed,
                UiNotificationSeverity.Error,
                "Сессия заменена",
                "Игровая сессия изменилась во время действия. Повторите действие в текущей сессии.");
        }
    }

    internal async Task<BrowserPromptWriteResult> TryApplyAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        try
        {
            var generation = _fs.GetOrCreateSessionGeneration(writeLease);
            return await SessionOperationContext.RunBoundAsync(
                _fs,
                generation,
                writeLease,
                () => TryApplyBoundAsync(writeLease, command, answers, owner));
        }
        catch (SessionReplacedException)
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Failed,
                UiNotificationSeverity.Error,
                "Сессия заменена",
                "Игровая сессия изменилась во время действия. Повторите действие в текущей сессии.");
        }
    }

    private Task<BrowserPromptWriteResult> RunMortalWorldTransactionAsync(
        Func<FileSystemManager.CanonicalWriteLease, Task<BrowserPromptWriteResult>> operation)
    {
        if (SessionOperationContext.TryGetExpectedGeneration(_fs.BasePath, out _))
            return _coordinator.RunBoundTransactionAsync(operation);

        return _coordinator.RunBoundAsync(
            () => _coordinator.RunBoundTransactionAsync(operation));
    }

    private async Task<BrowserPromptWriteResult> TryApplyBoundAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var token = NormalizeCommand(command);
        return token switch
        {
            "/world_setup" or "/настройка_мира" => await ApplyWorldSetupAsync(writeLease, answers, owner),
            "/distribute" or "/распределить" => await ApplyStatDistributionAsync(writeLease, answers, owner),
            "/companion_directive" or "/директива_компаньону" => await ApplyCompanionDirectiveAsync(writeLease, answers, owner),
            "/faction_directive" or "/директива_фракции" => await ApplyFactionDirectiveAsync(writeLease, answers, owner),
            "/npc_talk" or "/talk_npc" or "/поговорить_с_нпс" or "/разговор_с_нпс" => await ApplyNpcTalkAsync(writeLease, command, answers, owner),
            "/equip" or "/экипировать" => await ApplyInventoryEquipAsync(writeLease, command, answers, owner),
            "/unequip" or "/снять" => await ApplyInventoryUnequipAsync(writeLease, command, answers, owner),
            "/inventory_drop" or "/выбросить_предмет" => await ApplyInventoryDropAsync(writeLease, command, answers, owner),
            "/inventory_split" or "/разделить_стопку" => await ApplyInventorySplitAsync(writeLease, command, answers, owner),
            "/inventory_merge" or "/объединить_стопки" => await ApplyInventoryMergeAsync(writeLease, command, answers, owner),
            "/storage_move" or "/хранилище_предметы" => await ApplyStorageItemMoveAsync(writeLease, answers, owner),
            "/vehicle_move" or "/транспорт_предметы" => await ApplyVehicleItemMoveAsync(writeLease, answers, owner),
            "/npc_trade" or "/торговля_нпс" => await ApplyNpcTradeAsync(writeLease, command, answers, owner),
            "/craft" or "/ремесло" => await ApplyCraftAsync(writeLease, answers, owner),
            _ => BrowserPromptWriteResult.NotHandled()
        };
    }

    private async Task<BrowserPromptWriteResult> ApplyWorldSetupAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var mode = ReadAnswer(answers, "world_setup_mode");
        if (string.Equals(mode, "clear", StringComparison.OrdinalIgnoreCase))
        {
            return await ExecuteAtomicAsync(
                writeLease,
                owner,
                "Очистка подготовки следующего мира",
                [WorldDirectiveService.PendingSetupPath, ScenarioCoreService.ManifestPath],
                async writeLease =>
                {
                    _fs.DeleteFile(writeLease, WorldDirectiveService.PendingSetupPath);
                    await _scenarioCoreService.ClearAsync(writeLease);
                },
                "Подготовка мира очищена",
                "Файлы подготовки следующей смертной жизни и сценарного ядра удалены.",
                new JsonObject { ["mode"] = "clear" });
        }

        if (!string.Equals(mode, "create_or_edit", StringComparison.OrdinalIgnoreCase))
        {
            return BrowserPromptWriteResult.ValidationError(
                "Поддерживаются только режимы create_or_edit и clear. Профили мира пока выбираются в консольном интерфейсе.");
        }

        var title = ReadAnswer(answers, "world_title");
        var directivesText = ReadAnswer(answers, "world_directives");
        if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(directivesText))
            return BrowserPromptWriteResult.ValidationError("Укажите название мира или директивы мира.");

        var now = NowText();
        var setup = new WorldDirectiveService.PendingWorldSetup
        {
            Mode = "manual",
            WorldDirectives = new WorldDirectiveService.WorldDirectives
            {
                WorldTitle = title,
                SettingSummary = directivesText,
                DetailedWorldDescription = directivesText,
                LastUpdated = now
            },
            LastUpdated = now
        };

        var payload = PendingWorldSetupToJson(setup);
        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Запись подготовки следующего мира",
            [WorldDirectiveService.PendingSetupPath, ScenarioCoreService.ManifestPath],
            async writeLease =>
            {
                await _fs.WriteFileAtomicAsync(
                    writeLease,
                    WorldDirectiveService.PendingSetupPath,
                    JsonSerializer.Serialize(setup, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                await _scenarioCoreService.RefreshFromPendingSetupAsync(writeLease);
            },
            "Подготовка мира записана",
            "Браузерная форма обновила client-owned подготовку следующей смертной жизни.",
            payload);
    }

    private async Task<BrowserPromptWriteResult> ApplyStatDistributionAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var raw = ReadAnswer(answers, "stat_allocation_json");
        Dictionary<string, int> allocation;
        string error;
        if (!string.IsNullOrWhiteSpace(raw))
        {
            if (!TryParseAllocation(raw, out allocation, out error))
                return BrowserPromptWriteResult.ValidationError(error);
        }
        else if (!TryParsePromptAllocation(answers, out allocation, out error))
        {
            return BrowserPromptWriteResult.ValidationError(error);
        }

        if (allocation.Count == 0)
            return BrowserPromptWriteResult.ValidationError("Распределение не содержит положительных значений.");

        var statPoints = await ReadObjectAsync(writeLease, StatPointsPath) ?? new JsonObject();
        var available = ReadInt(statPoints, "unspentStatPoints", 0);
        var total = allocation.Values.Sum();
        if (total > available)
            return BrowserPromptWriteResult.ValidationError($"Недостаточно очков характеристик: доступно {available}, запрошено {total}.");

        var characteristics = await ReadObjectAsync(writeLease, CharacteristicsPath) ?? new JsonObject();
        foreach (var (stat, amount) in allocation)
        {
            var current = ReadInt(characteristics, stat, 1);
            if (current + amount > 100)
                return BrowserPromptWriteResult.ValidationError($"Характеристика {stat} превысит максимум 100.");
        }

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Распределение характеристик",
            [StatPointsPath, CharacteristicsPath, "game_state/player/computed_characteristics.json"],
            async writeLease =>
            {
                foreach (var (stat, amount) in allocation)
                {
                    var current = ReadInt(characteristics, stat, 1);
                    characteristics[stat] = current + amount;
                }

                statPoints["unspentStatPoints"] = available - total;
                await _fs.WriteFileAtomicAsync(
                    writeLease,
                    CharacteristicsPath,
                    characteristics.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
                await _fs.WriteFileAtomicAsync(
                    writeLease,
                    StatPointsPath,
                    statPoints.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            },
            "Характеристики распределены",
            $"Потрачено очков: {total}. Осталось: {available - total}.",
            AllocationToJson(allocation));
    }

    private async Task<BrowserPromptWriteResult> ApplyCompanionDirectiveAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var companionId = ReadAnswer(answers, "companion_id");
        var directive = ReadAnswer(answers, "companion_directive");
        if (string.IsNullOrWhiteSpace(companionId))
            return BrowserPromptWriteResult.ValidationError("Укажите ID компаньона.");

        var root = await ReadNodeAsync(writeLease, NpcCorePath);
        if (root == null)
            return BrowserPromptWriteResult.ValidationError("Файл npc_core.json не найден или пуст.");

        var target = FindObjectById(root, ["npcId", "id"], companionId);
        if (target == null)
            return BrowserPromptWriteResult.ValidationError($"Компаньон {companionId} не найден.");

        if (!string.Equals(ReadString(target, "progressionType"), "Companion", StringComparison.OrdinalIgnoreCase))
            return BrowserPromptWriteResult.ValidationError($"НПС {companionId} не является активным компаньоном.");

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Запись директивы компаньона",
            [NpcCorePath],
            async lease =>
            {
                target["playerCompanionDirective"] = string.IsNullOrWhiteSpace(directive) ? null : directive.Trim();
                await _fs.WriteFileAtomicAsync(
                    lease,
                    NpcCorePath,
                    root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            },
            "Директива компаньона записана",
            string.IsNullOrWhiteSpace(directive)
                ? $"Директива компаньона {companionId} очищена."
                : $"Директива компаньона {companionId} обновлена.",
            new JsonObject
            {
                ["companionId"] = companionId,
                ["playerCompanionDirective"] = string.IsNullOrWhiteSpace(directive) ? null : directive.Trim()
            });
    }

    private async Task<BrowserPromptWriteResult> ApplyFactionDirectiveAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var factionId = ReadAnswer(answers, "faction_id");
        var directive = ReadAnswer(answers, "faction_directive");
        if (string.IsNullOrWhiteSpace(factionId))
            return BrowserPromptWriteResult.ValidationError("Укажите ID фракции.");

        var root = await ReadNodeAsync(writeLease, FactionCorePath);
        if (root == null)
            return BrowserPromptWriteResult.ValidationError("Файл faction_core.json не найден или пуст.");

        var target = FindObjectById(root, ["factionId", "id"], factionId);
        if (target == null)
            return BrowserPromptWriteResult.ValidationError($"Фракция {factionId} не найдена.");

        if (!ReadBool(target, "isPlayerFaction") && !ReadBool(target, "isPlayerMember"))
            return BrowserPromptWriteResult.ValidationError($"Фракция {factionId} не является фракцией игрока или членством игрока.");

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Запись директивы фракции",
            [FactionCorePath],
            async lease =>
            {
                target["playerStrategyDirective"] = string.IsNullOrWhiteSpace(directive) ? null : directive.Trim();
                await _fs.WriteFileAtomicAsync(
                    lease,
                    FactionCorePath,
                    root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            },
            "Директива фракции записана",
            string.IsNullOrWhiteSpace(directive)
                ? $"Стратегическая директива фракции {factionId} очищена."
                : $"Стратегическая директива фракции {factionId} обновлена.",
            new JsonObject
            {
                ["factionId"] = factionId,
                ["playerStrategyDirective"] = string.IsNullOrWhiteSpace(directive) ? null : directive.Trim()
            });
    }

    private async Task<BrowserPromptWriteResult> ApplyNpcTalkAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var currentRealm = await ReadCurrentRealmAsync(writeLease);
        if (!RealmSemantics.IsMortalRealm(currentRealm))
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Blocked,
                UiNotificationSeverity.Warning,
                "Разговор с НПС недоступен",
                "Разговор с НПС можно отправить только в смертном мире. Сейчас действие недоступно для текущего царства.");
        }

        var npcId = ReadAnswer(answers, "npc_id");
        if (string.IsNullOrWhiteSpace(npcId))
            npcId = ReadFirstCommandArgument(command);
        if (string.IsNullOrWhiteSpace(npcId))
            return BrowserPromptWriteResult.ValidationError("Выберите собеседника.");

        var topic = ReadAnswer(answers, "npc_conversation_topic");
        if (string.IsNullOrWhiteSpace(topic))
            return BrowserPromptWriteResult.ValidationError("Опишите тему разговора.");

        var root = await ReadNodeAsync(writeLease, NpcCorePath);
        if (root == null)
            return BrowserPromptWriteResult.ValidationError("Список известных персонажей сейчас недоступен.");

        var target = FindObjectById(root, ["npcId", "id"], npcId);
        if (target == null)
            return BrowserPromptWriteResult.ValidationError("Такого собеседника сейчас нет среди известных персонажей.");

        var stableNpcId = FirstNonEmpty(ReadString(target, "npcId"), ReadString(target, "id"), npcId);
        var npcName = FirstNonEmpty(ReadString(target, "name"), ReadString(target, "npcName"), ReadString(target, "displayName"), stableNpcId);
        var pendingState = await ActorSocialInteractionRequestState.ReadNpcRequestsStateAsync(_fs, writeLease);
        if (pendingState.IsMalformed)
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Failed,
                UiNotificationSeverity.Error,
                "Разговор не отправлен",
                "Запрос разговора временно ждёт проверки состояния. Повторите действие после восстановления игрового состояния.");
        }

        var existing = pendingState.Requests.FirstOrDefault(request =>
            string.Equals(request.NpcId, stableNpcId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(request.InteractionType, ActorSocialInteractionRequestState.NpcInteractionTypeTalk, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return BrowserPromptWriteResult.Failed(
                CommandExecutionState.Pending,
                UiNotificationSeverity.Warning,
                "Разговор уже ожидает ГМ",
                $"Разговор с {npcName} уже ожидает ответа ГМ. Дождитесь результата, затем начните новый разговор.");
        }

        var currentTurn = await ReadCurrentTurnNumberAsync(writeLease);
        var request = new ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest
        {
            NpcId = stableNpcId,
            NpcName = npcName,
            InteractionType = ActorSocialInteractionRequestState.NpcInteractionTypeTalk,
            Topic = topic.Trim(),
            CreatedAtTurn = currentTurn,
            CreatedAtUtc = NowText()
        };

        var duplicateDuringWrite = false;
        var writeResult = await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Разговор с НПС",
            [ActorSocialInteractionRequestState.PendingNpcRequestPath],
            async lease =>
            {
                duplicateDuringWrite = !await ActorSocialInteractionRequestState.TryWriteNpcRequestIfAbsentAsync(
                    _fs,
                    lease,
                    request);
            },
            "Разговор отправлен ГМ",
            $"ГМ получит запрос разговора с {npcName} и тему: {request.Topic}.",
            payload: null);

        return duplicateDuringWrite
            ? BrowserPromptWriteResult.Failed(
                CommandExecutionState.Pending,
                UiNotificationSeverity.Warning,
                "Разговор уже ожидает ГМ",
                $"Разговор с {npcName} уже ожидает ответа ГМ. Дождитесь результата, затем начните новый разговор.")
            : writeResult;
    }

    private async Task<BrowserPromptWriteResult> ApplyInventoryEquipAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_inventory_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите изменение экипировки.");

        var itemIdentity = ReadAnswer(answers, "item_identity");
        if (string.IsNullOrWhiteSpace(itemIdentity))
            itemIdentity = InventoryEquipmentService.ReadFirstCommandArgument(command);
        if (string.IsNullOrWhiteSpace(itemIdentity))
            return BrowserPromptWriteResult.ValidationError("Выберите предмет для экипировки.");

        var slotKey = ReadAnswer(answers, "equipment_slot");
        if (string.IsNullOrWhiteSpace(slotKey))
            return BrowserPromptWriteResult.ValidationError("Выберите слот экипировки.");

        var validation = await InventoryEquipmentService.ValidateEquipAsync(_fs, writeLease, itemIdentity, slotKey);
        if (!validation.Success)
            return BrowserPromptWriteResult.ValidationError(validation.Message);

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Экипировка предмета",
            [InventoryEquipmentService.ItemsPath],
            async lease =>
            {
                var outcome = await InventoryEquipmentService.EquipAsync(_fs, lease, itemIdentity, slotKey);
                if (!outcome.Success)
                    throw new InventoryEquipmentWriteException(outcome.Message);
            },
            "Предмет экипирован",
            validation.Message,
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyInventoryUnequipAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_inventory_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите снятие предмета.");

        var slotKey = ReadAnswer(answers, "equipment_slot");
        if (string.IsNullOrWhiteSpace(slotKey))
            slotKey = InventoryEquipmentService.ReadFirstCommandArgument(command);
        if (string.IsNullOrWhiteSpace(slotKey))
            return BrowserPromptWriteResult.ValidationError("Выберите слот, с которого нужно снять предмет.");

        var validation = await InventoryEquipmentService.ValidateUnequipAsync(_fs, writeLease, slotKey);
        if (!validation.Success)
            return BrowserPromptWriteResult.ValidationError(validation.Message);

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Снятие предмета",
            [InventoryEquipmentService.ItemsPath],
            async lease =>
            {
                var outcome = await InventoryEquipmentService.UnequipAsync(_fs, lease, slotKey);
                if (!outcome.Success)
                    throw new InventoryEquipmentWriteException(outcome.Message);
            },
            "Предмет снят",
            validation.Message,
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyInventoryDropAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_inventory_drop"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите выброс предмета.");

        var itemIdentity = ReadAnswer(answers, "item_identity");
        if (string.IsNullOrWhiteSpace(itemIdentity))
            itemIdentity = InventoryEquipmentService.ReadFirstCommandArgument(command);
        if (string.IsNullOrWhiteSpace(itemIdentity))
            return BrowserPromptWriteResult.ValidationError("Выберите предмет.");

        var validation = await InventoryManagementService.ValidateDropAsync(_fs, writeLease, itemIdentity);
        if (!validation.Success)
            return BrowserPromptWriteResult.ValidationError(validation.Message);

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Выброс предмета",
            [InventoryEquipmentService.ItemsPath, MortalItemIdentityState.StatePath],
            async lease =>
            {
                var outcome = await InventoryManagementService.DropAsync(_fs, lease, itemIdentity);
                if (!outcome.Success)
                    throw new InventoryManagementWriteException(outcome.Message);
            },
            "Предмет выброшен",
            validation.Count > 1
                ? $"«{validation.ItemName}» выброшен ({validation.Count} шт.)."
                : $"«{validation.ItemName}» выброшен.",
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyInventorySplitAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_inventory_split"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите разделение стопки.");

        var itemIdentity = ReadAnswer(answers, "item_identity");
        if (string.IsNullOrWhiteSpace(itemIdentity))
            itemIdentity = InventoryEquipmentService.ReadFirstCommandArgument(command);
        if (string.IsNullOrWhiteSpace(itemIdentity))
            return BrowserPromptWriteResult.ValidationError("Выберите стопку.");

        if (!TryReadIntAnswer(answers, "split_quantity", out var splitQuantity))
            return BrowserPromptWriteResult.ValidationError("Введите количество целым числом.");

        var validation = await InventoryManagementService.ValidateSplitAsync(
            _fs,
            writeLease,
            itemIdentity,
            splitQuantity);
        if (!validation.Success)
            return BrowserPromptWriteResult.ValidationError(validation.Message);

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Разделение стопки",
            [InventoryEquipmentService.ItemsPath, MortalItemIdentityState.StatePath],
            async lease =>
            {
                var outcome = await InventoryManagementService.SplitAsync(
                    _fs,
                    lease,
                    itemIdentity,
                    splitQuantity);
                if (!outcome.Success)
                    throw new InventoryManagementWriteException(outcome.Message);
            },
            "Стопка разделена",
            $"Стопка «{validation.ItemName}» разделена: отделено {validation.Count}.",
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyInventoryMergeAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_inventory_merge"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите объединение стопок.");

        var itemIdentity = ReadAnswer(answers, "item_identity");
        if (string.IsNullOrWhiteSpace(itemIdentity))
            itemIdentity = InventoryEquipmentService.ReadFirstCommandArgument(command);
        if (string.IsNullOrWhiteSpace(itemIdentity))
            return BrowserPromptWriteResult.ValidationError("Выберите стопки.");

        var validation = await InventoryManagementService.ValidateMergeAsync(_fs, writeLease, itemIdentity);
        if (!validation.Success)
            return BrowserPromptWriteResult.ValidationError(validation.Message);

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Объединение стопок",
            [InventoryEquipmentService.ItemsPath, MortalItemIdentityState.StatePath],
            async lease =>
            {
                var outcome = await InventoryManagementService.MergeAsync(_fs, lease, itemIdentity);
                if (!outcome.Success)
                    throw new InventoryManagementWriteException(outcome.Message);
            },
            "Стопки объединены",
            $"Стопки «{validation.ItemName}» объединены: {validation.Count} шт.",
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyStorageItemMoveAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_storage_move"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите перемещение предмета.");

        var currentRealm = await ReadCurrentRealmAsync(writeLease);
        if (!RealmSemantics.IsMortalRealm(currentRealm))
            return StorageTransportMortalRealmBlocker();

        var direction = ReadAnswer(answers, "storage_move_direction");
        var storageKey = ReadAnswer(answers, "storage_key");
        var itemKey = string.Equals(direction, StorageTransportMoveService.DirectionRetrieve, StringComparison.OrdinalIgnoreCase)
            ? ReadAnswer(answers, "storage_item_key")
            : ReadAnswer(answers, "inventory_item_key");

        var validation = await StorageTransportMoveService.ValidateStorageMoveAsync(
            _fs,
            writeLease,
            direction,
            storageKey,
            itemKey);
        if (!validation.Success)
            return BrowserPromptWriteResult.ValidationError(validation.Message);

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Перемещение предмета в хранилище",
            [
                StorageTransportMoveService.InventoryPath,
                StorageTransportMoveService.CurrentLocationPath,
                MortalItemIdentityState.StatePath
            ],
            async writeLease =>
            {
                var outcome = await StorageTransportMoveService.MoveStorageItemAsync(
                    _fs,
                    writeLease,
                    direction,
                    storageKey,
                    itemKey);
                if (!outcome.Success)
                    throw new StorageTransportMoveWriteException(outcome.Message);
            },
            "Предмет перемещён",
            validation.Message,
            payload: null);
    }

    private async Task<BrowserPromptWriteResult> ApplyVehicleItemMoveAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_vehicle_move"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите перемещение предмета.");

        var currentRealm = await ReadCurrentRealmAsync(writeLease);
        if (!RealmSemantics.IsMortalRealm(currentRealm))
            return StorageTransportMortalRealmBlocker();

        var direction = ReadAnswer(answers, "vehicle_move_direction");
        var vehicleKey = ReadAnswer(answers, "vehicle_key");
        var itemKey = string.Equals(direction, StorageTransportMoveService.DirectionRetrieve, StringComparison.OrdinalIgnoreCase)
            ? ReadAnswer(answers, "vehicle_item_key")
            : ReadAnswer(answers, "inventory_item_key");

        var validation = await StorageTransportMoveService.ValidateVehicleMoveAsync(
            _fs,
            writeLease,
            direction,
            vehicleKey,
            itemKey);
        if (!validation.Success)
            return BrowserPromptWriteResult.ValidationError(validation.Message);

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Перемещение предмета в транспорт",
            [
                StorageTransportMoveService.InventoryPath,
                StorageTransportMoveService.VehiclesPath,
                MortalItemIdentityState.StatePath
            ],
            async writeLease =>
            {
                var outcome = await StorageTransportMoveService.MoveVehicleItemAsync(
                    _fs,
                    writeLease,
                    direction,
                    vehicleKey,
                    itemKey);
                if (!outcome.Success)
                    throw new StorageTransportMoveWriteException(outcome.Message);
            },
            "Предмет перемещён",
            validation.Message,
            payload: null);
    }

    private static BrowserPromptWriteResult StorageTransportMortalRealmBlocker() =>
        BrowserPromptWriteResult.Failed(
            CommandExecutionState.Blocked,
            UiNotificationSeverity.Warning,
            "Перемещение предметов недоступно",
            "Перемещение предметов между рюкзаком, хранилищем и транспортом доступно только в смертном мире. Сейчас действие недоступно для текущего царства.");

    private async Task<BrowserPromptWriteResult> ApplyNpcTradeAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_trade_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите сделку.");

        var npcId = ReadFirstCommandArgument(command);
        if (string.IsNullOrWhiteSpace(npcId))
            npcId = ReadAnswer(answers, "npc_id");
        if (string.IsNullOrWhiteSpace(npcId))
            return BrowserPromptWriteResult.ValidationError("Выберите торговца.");

        var choice = ReadAnswer(answers, "npc_trade_choice");
        if (!TryParseTradeChoice(choice, out var operation, out var targetId))
            return BrowserPromptWriteResult.ValidationError("Выберите покупку, продажу, обратный выкуп или запрос витрины.");
        if (operation == "request" && string.Equals(targetId, "__selected__", StringComparison.OrdinalIgnoreCase))
            targetId = npcId;

        var currentTurn = await ReadCurrentTurnNumberAsync(writeLease);
        var service = new NpcTradeService(_fs, NullLogger<NpcTradeService>.Instance);
        var rollbackPaths = new[]
        {
            NpcCorePath,
            "game_state/inventory/items.json",
            "game_state/core/player_status.json",
            NpcTradeRequestState.PendingRequestPath
        };

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Торговля с НПС",
            rollbackPaths,
            async lease =>
            {
                var result = operation switch
                {
                    "request" => await BuildNpcTradeRequestResultAsync(service, lease, targetId, currentTurn),
                    "buy" => await service.BuyAsync(lease, npcId, targetId, currentTurn),
                    "sell" => await service.SellAsync(lease, npcId, targetId, currentTurn),
                    "buyback" => await service.BuyBackAsync(lease, npcId, targetId, currentTurn),
                    _ => new NpcTradeService.NpcTradeOperationResult(false, false, "Выберите поддерживаемую сделку.")
                };

                if (!result.Success)
                    throw new InvalidOperationException(result.Message);
            },
            "Торговля завершена",
            operation switch
            {
                "request" => "Запрос ассортимента торговца отправлен.",
                "buy" => "Покупка у торговца выполнена.",
                "sell" => "Продажа торговцу выполнена.",
                "buyback" => "Обратный выкуп выполнен.",
                _ => "Сделка выполнена."
            },
            payload: null);
    }

    private static async Task<NpcTradeService.NpcTradeOperationResult> BuildNpcTradeRequestResultAsync(
        NpcTradeService service,
        FileSystemManager.CanonicalWriteLease writeLease,
        string npcId,
        int currentTurn)
    {
        var view = await service.EnsureTradeInventoryAsync(
            writeLease,
            npcId,
            currentTurn,
            createPendingRequests: true);
        if (view == null)
            return new NpcTradeService.NpcTradeOperationResult(false, false, "Торговец не найден.");
        if (view.TradeBlocked)
            return new NpcTradeService.NpcTradeOperationResult(false, false, view.BlockReason ?? "Торговля недоступна.");
        if (view.InventoryReady)
            return new NpcTradeService.NpcTradeOperationResult(true, false, "Витрина торговца уже готова.");
        if (view.InventoryRequestPending)
            return new NpcTradeService.NpcTradeOperationResult(true, view.InventoryRequestCreatedThisCall, view.InventoryStatusMessage ?? "Витрина торговца запрошена.");
        return new NpcTradeService.NpcTradeOperationResult(false, false, view.InventoryStatusMessage ?? "Не удалось запросить витрину торговца.");
    }

    private async Task<BrowserPromptWriteResult> ApplyCraftAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var recipeId = ReadAnswer(answers, "recipe_id");
        var intent = ReadAnswer(answers, "craft_intent");
        if (string.IsNullOrWhiteSpace(recipeId))
            return BrowserPromptWriteResult.ValidationError("Укажите рецепт или название рецепта.");
        if (string.IsNullOrWhiteSpace(intent))
            return BrowserPromptWriteResult.ValidationError("Опишите ремесленное действие.");

        var request = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["requestId"] = "craft_" + Guid.NewGuid().ToString("N"),
            ["createdAtUtc"] = NowText(),
            ["source"] = "browser",
            ["status"] = "pending_gm_resolution",
            ["recipeId"] = recipeId.Trim(),
            ["craftIntent"] = intent.Trim()
        };

        return await ExecuteAtomicAsync(
            writeLease,
            owner,
            "Запись ремесленного запроса",
            [PendingCraftRequestPath],
            async lease => await _fs.WriteFileAtomicAsync(
                lease,
                PendingCraftRequestPath,
                request.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)),
            "Ремесленный запрос записан",
            "Создан pending-запрос для разрешения ремесленного действия ГМ.",
            request);
    }

    private async Task<BrowserPromptWriteResult> ExecuteAtomicAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        LocalUiSessionLockOwner owner,
        string operationLabel,
        IReadOnlyCollection<string> rollbackPaths,
        Func<FileSystemManager.CanonicalWriteLease, Task> writeOperation,
        string title,
        string message,
        JsonObject? payload)
    {
        var result = await _coordinator.ExecuteAtomicWithinTransactionAsync(
            writeLease,
            new BrowserLocalWriteRequest(
                owner.OwnerId,
                owner.OwnerLabel,
                operationLabel,
                owner.Lease),
            rollbackPaths,
            writeOperation);

        if (result.Success)
            return BrowserPromptWriteResult.Completed(title, message, payload);

        var failureMessage = SanitizeLocalWriteMessage(result.Message);
        return BrowserPromptWriteResult.Failed(
            result.IsBlocked ? CommandExecutionState.Blocked : CommandExecutionState.Failed,
            result.IsBlocked ? UiNotificationSeverity.Warning : UiNotificationSeverity.Error,
            result.IsBlocked ? "Запись заблокирована" : "Ошибка записи",
            failureMessage);
    }

    private static string SanitizeLocalWriteMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return "Локальная запись не выполнена.";

        var safeFallback = "Локальная запись не выполнена: состояние требует исправления.";
        var prelocalized = MortalItemPlayerFailureMessages.Sanitize(message, safeFallback);
        if (!string.Equals(prelocalized, message, StringComparison.Ordinal))
            return prelocalized;

        var localized = message
            .Replace("Browser-write", "Локальная запись", StringComparison.Ordinal)
            .Replace("GM-turn", "ход ГМ", StringComparison.Ordinal)
            .Replace("rollback/snapshot artifact", "восстановление состояния", StringComparison.Ordinal)
            .Replace("rollback", "восстановление состояния", StringComparison.Ordinal)
            .Replace(LocalUiSessionLockService.LockPath, "локальную блокировку интерфейса", StringComparison.OrdinalIgnoreCase)
            .Replace(ActorSocialInteractionRequestState.PendingNpcRequestPath, "состояние запросов разговора с НПС", StringComparison.OrdinalIgnoreCase)
            .Replace(ActorSocialInteractionRequestState.PendingGuardianRequestPath, "состояние запросов разговора с хранителем", StringComparison.OrdinalIgnoreCase)
            .Replace("game_session", "текущая игровая сессия", StringComparison.Ordinal)
            .Replace("game_state/", "игровое состояние/", StringComparison.OrdinalIgnoreCase)
            .Replace("pending_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(".json", " файл", StringComparison.OrdinalIgnoreCase)
            .Replace("lease", "срока блокировки", StringComparison.Ordinal);
        return MortalItemPlayerFailureMessages.Sanitize(localized, safeFallback);
    }

    private async Task<JsonNode?> ReadNodeAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string path)
    {
        var raw = await _fs.ReadFileAsync(writeLease, path);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonNode.Parse(raw);
        }
        catch
        {
            return null;
        }
    }

    private async Task<JsonObject?> ReadObjectAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        string path) =>
        await ReadNodeAsync(writeLease, path) as JsonObject;

    private static bool TryParseAllocation(string raw, out Dictionary<string, int> allocation, out string error)
    {
        allocation = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(raw);
        }
        catch (Exception ex)
        {
            error = $"Распределение характеристик не читается: {ex.Message}";
            return false;
        }

        if (node is not JsonObject obj)
        {
            error = "Распределение характеристик должно быть набором характеристик и положительных чисел.";
            return false;
        }

        var validStats = Characteristics.All.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in obj)
        {
            if (!validStats.Contains(key))
            {
                error = $"Неизвестная характеристика: {key}.";
                return false;
            }

            if (!TryReadInt(value, out var amount) || amount <= 0)
            {
                error = $"Значение для {key} должно быть положительным целым числом.";
                return false;
            }

            allocation[key] = amount;
        }

        return true;
    }

    private static bool TryParsePromptAllocation(
        IReadOnlyDictionary<string, JsonNode?> answers,
        out Dictionary<string, int> allocation,
        out string error)
    {
        allocation = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        error = string.Empty;

        foreach (var stat in Characteristics.All)
        {
            var raw = ReadAnswer(answers, $"stat_{stat}");
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var statName = Characteristics.RussianNames.GetValueOrDefault(stat, stat);
            if (!int.TryParse(raw.Trim(), out var amount) || amount < 0)
            {
                error = $"Значение для «{statName}» должно быть неотрицательным целым числом.";
                return false;
            }

            if (amount > 0)
                allocation[stat] = amount;
        }

        return true;
    }

    private static JsonObject? FindObjectById(JsonNode node, IReadOnlyCollection<string> idProperties, string expectedId)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in idProperties)
            {
                if (string.Equals(ReadString(obj, property), expectedId, StringComparison.OrdinalIgnoreCase))
                    return obj;
            }

            foreach (var (_, child) in obj)
            {
                if (child == null)
                    continue;

                var found = FindObjectById(child, idProperties, expectedId);
                if (found != null)
                    return found;
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item == null)
                    continue;

                var found = FindObjectById(item, idProperties, expectedId);
                if (found != null)
                    return found;
            }
        }

        return null;
    }

    private static JsonObject PendingWorldSetupToJson(WorldDirectiveService.PendingWorldSetup setup)
    {
        var serialized = JsonSerializer.Serialize(setup, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        return JsonNode.Parse(serialized)!.AsObject();
    }

    private static JsonObject AllocationToJson(Dictionary<string, int> allocation)
    {
        var obj = new JsonObject();
        foreach (var (key, value) in allocation)
            obj[key] = value;
        return obj;
    }

    private static string NormalizeCommand(string command)
    {
        var trimmed = command.Trim();
        var split = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return split.Length == 0 ? string.Empty : split[0].ToLowerInvariant();
    }

    private static string ReadFirstCommandArgument(string command)
    {
        var split = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return split.Length < 2 ? string.Empty : split[1].Trim();
    }

    private static bool TryParseTradeChoice(string choice, out string operation, out string targetId)
    {
        operation = string.Empty;
        targetId = string.Empty;
        var parts = choice.Trim().Split(':', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        operation = parts[0].ToLowerInvariant();
        targetId = parts[1];
        return operation is "request" or "buy" or "sell" or "buyback" &&
               !string.IsNullOrWhiteSpace(targetId);
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private async Task<string?> ReadCurrentRealmAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var soul = await ReadObjectAsync(writeLease, SoulStatePath);
        return soul == null ? null : ReadString(soul, "currentRealm");
    }

    private async Task<int> ReadCurrentTurnNumberAsync(
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var storiesRoot = _fs.ResolvePath("stories");
        if (!Directory.Exists(storiesRoot))
            return 1;

        var latestTurn = 0;
        foreach (var path in Directory.EnumerateFiles(storiesRoot, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var relativePath = Path.GetRelativePath(_fs.GameSessionPath, path).Replace('\\', '/');
                var json = await _fs.ReadFileAsync(writeLease, relativePath);
                if (string.IsNullOrWhiteSpace(json))
                    continue;
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.TryGetProperty("turnNumber", out var turnNode) &&
                    turnNode.ValueKind == JsonValueKind.Number &&
                    turnNode.TryGetInt32(out var turn))
                {
                    latestTurn = Math.Max(latestTurn, turn);
                }
            }
            catch
            {
                // Ignore unrelated or partial story files while deriving a safe browser write turn.
            }
        }

        return Math.Max(1, latestTurn);
    }

    private static string ReadAnswer(IReadOnlyDictionary<string, JsonNode?> answers, string key)
    {
        if (!answers.TryGetValue(key, out var node) || node == null)
            return string.Empty;

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text?.Trim() ?? string.Empty;
            if (value.TryGetValue<int>(out var number))
                return number.ToString();
            if (value.TryGetValue<bool>(out var flag))
                return flag ? "true" : "false";
        }

        return node.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
    }

    private static bool ReadBoolAnswer(IReadOnlyDictionary<string, JsonNode?> answers, string key)
    {
        if (!answers.TryGetValue(key, out var node) || node is not JsonValue value)
            return false;

        if (value.TryGetValue<bool>(out var flag))
            return flag;
        return value.TryGetValue<string>(out var text) &&
               bool.TryParse(text, out flag) &&
               flag;
    }

    private static bool TryReadIntAnswer(
        IReadOnlyDictionary<string, JsonNode?> answers,
        string key,
        out int result)
    {
        result = 0;
        if (!answers.TryGetValue(key, out var node) || node is not JsonValue value)
            return false;

        if (value.TryGetValue<int>(out result))
            return true;

        return value.TryGetValue<string>(out var text) &&
               int.TryParse(text, out result);
    }

    private static string ReadString(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            return string.Empty;

        if (value.TryGetValue<string>(out var text))
            return text ?? string.Empty;
        if (value.TryGetValue<int>(out var number))
            return number.ToString();
        return string.Empty;
    }

    private static int ReadInt(JsonObject obj, string propertyName, int fallback)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || !TryReadInt(node, out var value))
            return fallback;
        return value;
    }

    private static bool TryReadInt(JsonNode? node, out int value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<int>(out value))
            return true;

        if (jsonValue.TryGetValue<string>(out var text) && int.TryParse(text, out value))
            return true;

        return false;
    }

    private static bool ReadBool(JsonObject obj, string propertyName)
    {
        if (!obj.TryGetPropertyValue(propertyName, out var node) || node is not JsonValue value)
            return false;
        return value.TryGetValue<bool>(out var parsed) && parsed;
    }

    private string NowText() => _timeProvider.GetUtcNow().UtcDateTime.ToString("O");

    private sealed class InventoryEquipmentWriteException(string message) : Exception(message);
    private sealed class InventoryManagementWriteException(string message) : Exception(message);
    private sealed class StorageTransportMoveWriteException(string message) : Exception(message);
}

public sealed record BrowserPromptWriteResult(
    bool Handled,
    bool Success,
    bool KeepSessionOpen,
    CommandExecutionState State,
    UiNotificationSeverity Severity,
    string Title,
    string Message,
    JsonObject? Payload = null)
{
    public static BrowserPromptWriteResult NotHandled() =>
        new(false, false, false, CommandExecutionState.Completed, UiNotificationSeverity.Info, string.Empty, string.Empty);

    public static BrowserPromptWriteResult Completed(string title, string message, JsonObject? payload = null) =>
        new(true, true, false, CommandExecutionState.Completed, UiNotificationSeverity.Success, title, message, payload);

    public static BrowserPromptWriteResult ValidationError(string message) =>
        new(true, false, true, CommandExecutionState.RequiresInput, UiNotificationSeverity.Error, "Ошибка формы", message);

    public static BrowserPromptWriteResult Failed(
        CommandExecutionState state,
        UiNotificationSeverity severity,
        string title,
        string message) =>
        new(true, false, false, state, severity, title, message);
}
