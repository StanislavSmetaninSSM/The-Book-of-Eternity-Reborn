using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.WebUi;

public sealed class BrowserAfterlifeWriteService
{
    private const string SoulStatePath = "game_state/meta/soul_state.json";
    private const string DirectChaosSeaGachaBanner = "direct_chaos_sea";

    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly BrowserLocalWriteCoordinator _coordinator;

    public BrowserAfterlifeWriteService(
        FileSystemManager fs,
        StateManager stateManager,
        BrowserLocalWriteCoordinator coordinator)
    {
        _fs = fs;
        _stateManager = stateManager;
        _coordinator = coordinator;
    }

    public async Task<BrowserPromptWriteResult> TryApplyAsync(
        string command,
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var token = NormalizeCommand(command);
        return token switch
        {
            "/shining_treasury" or "/казначейство" => await ApplyShiningTreasuryAsync(answers, owner),
            "/source_of_light" or "/источник_света" => await ApplySourceOfLightAsync(answers, owner),
            "/afterlife_inbox" or "/уведомления_загробья" => await ApplyAfterlifeInboxAsync(answers, owner),
            "/spiritual_arts" or "/духовные_искусства" => await ApplySpiritualArtsAsync(answers, owner),
            "/spiritual_action" or "/духовное_действие" => await BuildSpiritualActionPayloadAsync(answers),
            "/gacha" or "/гача" => await ApplyGachaPullAsync(answers, owner),
            "/abode_offering" or "/подношение_обители" => await ApplyAbodeOfferingAsync(answers, owner),
            "/found_guardian_mantle" or "/учредить_хранителя" => await ApplyPlayerGuardianFoundationAsync(answers, owner),
            "/soul_relic_equip" or "/экипировать_реликвию" => await ApplySoulRelicEquipAsync(answers, owner),
            "/soul_relic_unequip" or "/снять_реликвию" => await ApplySoulRelicUnequipAsync(answers, owner),
            _ => BrowserPromptWriteResult.NotHandled()
        };
    }

    private async Task<BrowserPromptWriteResult> ApplyShiningTreasuryAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var operation = ReadAnswer(answers, "treasury_operation").Trim().ToLowerInvariant();
        var amount = ReadIntAnswer(answers, "treasury_amount", 0);
        if (operation is not ("deposit" or "withdraw" or "claim_interest" or "exchange"))
            return BrowserPromptWriteResult.ValidationError("Выберите операцию казначейства.");
        if (operation != "claim_interest" && amount <= 0)
            return BrowserPromptWriteResult.ValidationError("Для операции нужна положительная целая сумма.");

        return await ExecuteAsync(
            owner,
            "Browser Shining Treasury",
            [ShiningAbodeState.StatePath, SoulStatePath],
            async () =>
            {
                var shiningRoot = await ReadRequiredObjectAsync(ShiningAbodeState.StatePath, "shining_abode_state.json недоступен.");
                var soulRoot = await ReadRequiredObjectAsync(SoulStatePath, "soul_state.json недоступен.");
                var blocker = await TryDescribeTreasuryCostBlockerAsync(shiningRoot);
                if (blocker != null)
                    throw new InvalidOperationException(blocker);
                var issue = ShiningAbodeState.ValidateTreasuryShape(shiningRoot);
                if (!string.IsNullOrWhiteSpace(issue))
                    throw new InvalidOperationException(issue);

                var result = operation switch
                {
                    "deposit" => ShiningAbodeState.DepositTreasuryInkFeathers(shiningRoot, soulRoot, amount),
                    "withdraw" => ShiningAbodeState.WithdrawTreasuryInkFeathers(shiningRoot, soulRoot, amount),
                    "claim_interest" => ShiningAbodeState.ClaimTreasuryInterest(shiningRoot, soulRoot),
                    "exchange" => ShiningAbodeState.ExchangeTreasuryInkFeathersForLightSparks(shiningRoot, soulRoot, amount),
                    _ => new ShiningAbodeState.TreasuryOperationResult(false, "Операция не выбрана.")
                };
                if (!result.Success)
                    throw new InvalidOperationException(result.Message);

                ShiningAbodeState.NormalizeStateRoot(shiningRoot, null, null);
                await WriteObjectAsync(ShiningAbodeState.StatePath, shiningRoot);
                await WriteObjectAsync(SoulStatePath, soulRoot);
                await _stateManager.RefreshGameStateAsync();
            },
            "Казначейство обновлено",
            "Браузер выполнил локальную операцию казначейства через общий протокол блокировки и отката.",
            new JsonObject
            {
                ["sourceSurface"] = "shining_treasury_browser_write",
                ["operation"] = operation,
                ["amount"] = amount,
                ["affectedFiles"] = new JsonArray(ShiningAbodeState.StatePath, SoulStatePath)
            });
    }

    private async Task<BrowserPromptWriteResult> ApplySourceOfLightAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var action = ReadAnswer(answers, "source_of_light_action");
        if (!string.Equals(action, "open", StringComparison.OrdinalIgnoreCase))
            return BrowserPromptWriteResult.ValidationError("Источник Света поддерживает только действие open.");

        JsonObject? payload = null;
        return await ExecuteAsync(
            owner,
            "Browser Source of Light request",
            [SourceOfLightCapstoneState.PendingRequestPath],
            async () =>
            {
                var shiningRoot = await ReadRequiredObjectAsync(ShiningAbodeState.StatePath, "shining_abode_state.json недоступен.");
                var soulRoot = await ReadRequiredObjectAsync(SoulStatePath, "soul_state.json недоступен.");
                var pending = await SourceOfLightCapstoneState.ReadRequestStateAsync(_fs);
                if (pending.IsMalformed)
                    throw new InvalidOperationException($"{SourceOfLightCapstoneState.PendingRequestPath} повреждён: {pending.Error}.");
                if (pending.Request != null)
                    throw new InvalidOperationException("Источник Света уже ожидает закрытия ГМ.");
                if (SourceOfLightCapstoneState.HasCompletedCapstone(shiningRoot) ||
                    SourceOfLightCapstoneState.HasLightIncarnate(soulRoot) ||
                    SourceOfLightCapstoneState.CountIncarnatedLightRelics(soulRoot) > 0)
                {
                    throw new InvalidOperationException("Источник Света уже завершён для этой души.");
                }
                if (!SourceOfLightCapstoneState.IsUnlockSatisfied(soulRoot, shiningRoot, out var blocker))
                    throw new InvalidOperationException(blocker);

                var pendingBlocker = await SourceOfLightCapstoneState.TryDescribeBlockingPendingContractAsync(_fs, shiningRoot);
                if (pendingBlocker != null)
                    throw new InvalidOperationException($"Источник Света заблокирован: есть {pendingBlocker}.");

                var request = SourceOfLightCapstoneState.CreateRequest(
                    Math.Max(1, _stateManager.CurrentState.TurnNumber + 1),
                    SourceOfLightCapstoneState.GetNodeInt(shiningRoot["radiance"]?["experience"]),
                    SourceOfLightCapstoneState.GetNodeInt(shiningRoot["radiance"]?["tier"]));
                await SourceOfLightCapstoneState.WriteRequestAsync(_fs, request);
                payload = JsonSerializer.SerializeToNode(request, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!.AsObject();
            },
            "Источник Света открыт",
            "Создан клиентский ожидающий запрос для закрытия сцены Источника Света ГМ.",
            payload ?? new JsonObject());
    }

    private async Task<BrowserPromptWriteResult> ApplyAfterlifeInboxAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var action = ReadAnswer(answers, "notification_action").Trim().ToLowerInvariant();
        var notificationId = ReadAnswer(answers, "notification_id");
        if (action is not ("mark_read" or "mark_all_read"))
            return BrowserPromptWriteResult.ValidationError("Выберите mark_read или mark_all_read.");
        if (action == "mark_read" && string.IsNullOrWhiteSpace(notificationId))
            return BrowserPromptWriteResult.ValidationError("Для mark_read нужен notification_id.");

        return await ExecuteAsync(
            owner,
            "Browser afterlife inbox",
            [AfterlifeNotificationState.NotificationsPath],
            async () =>
            {
                if (action == "mark_all_read")
                    await AfterlifeNotificationState.MarkAllReadAsync(_fs);
                else
                    await AfterlifeNotificationState.MarkReadAsync(_fs, notificationId);
            },
            "Уведомления обновлены",
            action == "mark_all_read"
                ? "Все уведомления загробья отмечены как прочитанные."
                : $"Уведомление {notificationId} отмечено как прочитанное.",
            new JsonObject
            {
                ["sourceSurface"] = "afterlife_inbox_browser_write",
                ["operation"] = action,
                ["notificationId"] = notificationId
            });
    }

    private async Task<BrowserPromptWriteResult> ApplySpiritualArtsAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var target = ReadAnswer(answers, "upgrade_target").Trim();
        var currency = ReadAnswer(answers, "upgrade_currency").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(target))
            return BrowserPromptWriteResult.ValidationError("Укажите духовное искусство или spirit_focus.");
        if (currency is not ("ink_feathers" or "light_sparks"))
            return BrowserPromptWriteResult.ValidationError("Валюта должна быть ink_feathers или light_sparks.");
        var targetIsSpiritFocus = string.Equals(target, "spirit_focus", StringComparison.OrdinalIgnoreCase);
        var targetIsStandardArt = AfterlifeSpiritualConflictState.SpiritualArts.Any(item =>
            string.Equals(item.ArtId, target, StringComparison.OrdinalIgnoreCase));
        var targetIsSpecialArt = !targetIsSpiritFocus && !targetIsStandardArt;

        return await ExecuteAsync(
            owner,
            "Browser spiritual art upgrade",
            [SoulStatePath, ShiningAbodeState.StatePath, AfterlifeEntityProfileState.StatePath],
            async () =>
            {
                var blocker = await TryDescribeSpiritualArtUpgradeBlockerAsync();
                if (blocker != null)
                    throw new InvalidOperationException(blocker);

                var soulRoot = await ReadRequiredObjectAsync(SoulStatePath, "soul_state.json недоступен.");
                var shiningRoot = await ReadObjectAsync(ShiningAbodeState.StatePath);
                var entityProfilesRoot = await ReadObjectAsync(AfterlifeEntityProfileState.StatePath);
                var profile = BuildSyncedAfterlifeCombatProfile(soulRoot, shiningRoot);
                var isShining = RealmSemantics.IsShiningRealm(GetNodeString(soulRoot["currentRealm"]));
                var result = targetIsSpiritFocus
                    ? ApplySpiritFocusUpgrade(soulRoot, shiningRoot, profile, currency, isShining)
                    : targetIsStandardArt
                        ? ApplyStandardSpiritualArtUpgrade(soulRoot, shiningRoot, profile, target, currency, isShining)
                        : ApplySpecialSpiritualArtUpgrade(soulRoot, shiningRoot, entityProfilesRoot, profile, target, currency, isShining);
                if (!result.Success)
                    throw new InvalidOperationException(result.Message);

                await WriteObjectAsync(SoulStatePath, soulRoot);
                if (currency == "light_sparks" && shiningRoot != null)
                    await WriteObjectAsync(ShiningAbodeState.StatePath, shiningRoot);
                if (targetIsSpecialArt && entityProfilesRoot != null)
                    await WriteObjectAsync(AfterlifeEntityProfileState.StatePath, entityProfilesRoot);
                await _stateManager.RefreshGameStateAsync();
            },
            "Духовное искусство прокачано",
            $"Браузерная форма прокачала {target} за {DescribeCurrency(currency)}.",
            new JsonObject
            {
                ["sourceSurface"] = "spiritual_arts_browser_write",
                ["target"] = target,
                ["currency"] = currency,
                ["affectedFiles"] = BuildSpiritualArtBrowserAffectedFiles(currency, targetIsSpecialArt)
            });
    }

    private async Task<BrowserPromptWriteResult> BuildSpiritualActionPayloadAsync(
        IReadOnlyDictionary<string, JsonNode?> answers)
    {
        var operation = ReadAnswer(answers, "operation_type");
        var text = ReadAnswer(answers, "spiritual_action_text");
        if (string.IsNullOrWhiteSpace(operation))
            return BrowserPromptWriteResult.ValidationError("Выберите духовное действие.");
        if (string.IsNullOrWhiteSpace(text))
            return BrowserPromptWriteResult.ValidationError("Опишите действие.");

        var root = await ReadObjectAsync(AfterlifeSpiritualConflictState.StatePath);
        var active = root?["activeConflict"] as JsonObject;
        var conflictId = GetNodeString(active?["conflictId"]) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(conflictId))
            return BrowserPromptWriteResult.ValidationError("Нет активного духовного конфликта для духовного действия.");

        var actionText =
            $"[AFTERLIFE_SPIRITUAL_ACTION: {conflictId}] {text.Trim()}\n\n" +
            "Разреши это как обмен действиями активного духовного конфликта посмертия. " +
            $"Игрок выбрал духовное действие `{operation.Trim()}`. " +
            $"Если конфликт меняется, запиши `{AfterlifeSpiritualConflictState.ResponseField}` с `mode=exchange` или `mode=resolve`. " +
            "Не используй файлы смертного боя, здоровье, энергию, списки enemiesData/alliesData, смертные поверхности NPC/world/faction или прямые награды валютой.";
        return BrowserPromptWriteResult.Completed(
            "Духовное действие подготовлено",
            "Браузер сформировал полезную нагрузку действия для ГМа по активному духовному конфликту. Постановка turn_request остаётся обязанностью общего lifecycle API.",
            new JsonObject
            {
                ["playerActionTag"] = "AFTERLIFE_SPIRITUAL_ACTION",
                ["operationType"] = operation.Trim(),
                ["conflictId"] = conflictId,
                ["playerAction"] = text.Trim(),
                ["expectedResponseSurface"] = AfterlifeSpiritualConflictState.ResponseField,
                ["stateFile"] = AfterlifeSpiritualConflictState.StatePath,
                ["gmAction"] = actionText
            });
    }

    private async Task<BrowserPromptWriteResult> ApplyGachaPullAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var banner = ReadAnswer(answers, "gacha_banner").Trim().ToLowerInvariant();
        var cost = ReadIntAnswer(answers, "feather_cost", 0);
        if (!ReadBoolAnswer(answers, "confirm_gacha_pull"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите прямой призыв судьбы.");
        if (!string.Equals(banner, DirectChaosSeaGachaBanner, StringComparison.OrdinalIgnoreCase))
            return BrowserPromptWriteResult.ValidationError("Браузер поддерживает только прямой призыв Моря Хаоса.");
        if (cost <= 0)
            return BrowserPromptWriteResult.ValidationError("Стоимость призыва должна быть положительным целым числом.");

        var available = await TryReadSoulInkFeathersForValidationAsync();
        if (available.HasValue && available.Value < cost)
            return BrowserPromptWriteResult.ValidationError($"Недостаточно Чернильных Перьев: доступно {available.Value}, нужно {cost}.");

        var payload = new JsonObject
        {
            ["sourceSurface"] = "gacha_browser_write",
            ["banner"] = DirectChaosSeaGachaBanner,
            ["bannerLabel"] = "Прямой призыв Моря Хаоса",
            ["spentInkFeathers"] = cost
        };

        return await ExecuteAsync(
            owner,
            "Browser direct Chaos Sea gacha",
            [SoulStatePath, PendingTurnStateService.PendingDiceStatePath],
            async () =>
            {
                var soulRoot = await ReadRequiredObjectAsync(SoulStatePath, "soul_state.json недоступен.");
                var currentFeathers = GetSoulInkFeathers(soulRoot);
                if (currentFeathers < cost)
                    throw new InvalidOperationException($"Недостаточно Чернильных Перьев: доступно {currentFeathers}, нужно {cost}.");

                var pendingTurnState = new PendingTurnStateService(
                    _fs,
                    NullLogger<PendingTurnStateService>.Instance);
                var pending = await pendingTurnState.GetOrCreateAsync();
                var remainingFeathers = currentFeathers - cost;
                SetSoulInkFeathers(soulRoot, remainingFeathers);
                await WriteObjectAsync(SoulStatePath, soulRoot);
                await _stateManager.RefreshGameStateAsync();

                var gachaBase = BuildGachaBaseResultPayload(pending.GachaBaseResult);
                var gmAction = BuildDirectGachaGmAction(cost);
                payload["remainingInkFeathers"] = remainingFeathers;
                payload["currentInkFeathersBeforeSpend"] = currentFeathers;
                payload["playerActionTag"] = "CHAOS_SEA_DIRECT_GACHA";
                payload["gachaBaseResult"] = gachaBase;
                payload["rarityRule"] = "finalRarity exactly equals gachaBaseResult.baseRarity; no guardian modifiers";
                payload["expectedRelicMaterialization"] = "GM appends exactly one new Soul Relic; the browser does not materialize a concrete relic locally.";
                payload["gmAction"] = gmAction;
                payload["affectedFiles"] = new JsonArray(SoulStatePath, PendingTurnStateService.PendingDiceStatePath);
            },
            "Прямой призыв подготовлен",
            "Браузер списал Чернильные Перья и подготовил действие для ГМ: результатом должна стать ровно одна материализованная Реликвия Души без локального выбора имени.",
            payload);
    }

    private async Task<BrowserPromptWriteResult> ApplySoulRelicEquipAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_soul_relic_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите экипировку реликвии.");

        var relicIdOrName = ReadAnswer(answers, "soul_relic_identity");
        var slotKey = ReadAnswer(answers, "soul_relic_slot");

        return await ExecuteAsync(
            owner,
            "Browser soul relic equip",
            [SoulStatePath],
            async () =>
            {
                var outcome = await SoulRelicEquipmentService.EquipAsync(_fs, relicIdOrName, slotKey);
                if (!outcome.Success)
                    throw new InvalidOperationException(outcome.Message);
                await _stateManager.RefreshGameStateAsync();
            },
            "Реликвия души экипирована",
            "Браузер переместил реликвию души из хранилища в слот экипировки.",
            new JsonObject
            {
                ["sourceSurface"] = "soul_relic_equip_browser_write",
                ["relicIdentity"] = relicIdOrName,
                ["slot"] = slotKey,
                ["affectedFiles"] = new JsonArray(SoulStatePath)
            });
    }

    private async Task<BrowserPromptWriteResult> ApplySoulRelicUnequipAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        if (!ReadBoolAnswer(answers, "confirm_soul_relic_write"))
            return BrowserPromptWriteResult.ValidationError("Подтвердите снятие реликвии.");

        var slotKey = ReadAnswer(answers, "soul_relic_slot");

        return await ExecuteAsync(
            owner,
            "Browser soul relic unequip",
            [SoulStatePath],
            async () =>
            {
                var outcome = await SoulRelicEquipmentService.UnequipAsync(_fs, slotKey);
                if (!outcome.Success)
                    throw new InvalidOperationException(outcome.Message);
                await _stateManager.RefreshGameStateAsync();
            },
            "Реликвия души снята",
            "Браузер вернул реликвию души из слота в хранилище.",
            new JsonObject
            {
                ["sourceSurface"] = "soul_relic_unequip_browser_write",
                ["slot"] = slotKey,
                ["affectedFiles"] = new JsonArray(SoulStatePath)
            });
    }

    private async Task<BrowserPromptWriteResult> ApplyAbodeOfferingAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        var guardianId = ReadAnswer(answers, "guardian_id");
        var offeringType = NormalizeOfferingType(ReadAnswer(answers, "offering_type"));
        var offeringValue = ReadAnswer(answers, "offering_value");
        if (string.IsNullOrWhiteSpace(guardianId))
            return BrowserPromptWriteResult.ValidationError("Укажите ID Хранителя.");
        if (string.IsNullOrWhiteSpace(offeringType))
            return BrowserPromptWriteResult.ValidationError("Выберите тип подношения.");

        return await ExecuteAsync(
            owner,
            "Browser abode offering",
            [GuardianAbodeOfferingState.PendingRequestPath, SoulStatePath],
            async () =>
            {
                var soulRoot = await ReadRequiredObjectAsync(SoulStatePath, "soul_state.json недоступен.");
                var guardiansRoot = await ReadRequiredObjectAsync("game_state/meta/guardians.json", "guardians.json недоступен.");
                var guardian = FindObjectById(guardiansRoot, ["guardianId", "id"], guardianId)
                    ?? throw new InvalidOperationException($"Хранитель {guardianId} не найден.");
                var guardianName = FirstNonEmpty(
                    GetNodeString(guardian["canonicalName"]),
                    GetNodeString(guardian["guardianName"]),
                    GetNodeString(guardian["name"]),
                    guardianId);
                var request = new GuardianAbodeOfferingState.PendingAbodeOfferingRequest
                {
                    GuardianId = guardianId.Trim(),
                    GuardianName = guardianName,
                    OfferingType = offeringType,
                    ReturnCycleId = GuardianAbodeOfferingState.BuildReturnCycleId(GetNodeInt(soulRoot["currentIncarnation"]))
                };

                if (offeringType == GuardianAbodeOfferingState.OfferingTypeInkFeathers)
                {
                    var amount = ReadIntAnswer(answers, "offering_value", 0);
                    if (amount < 50 || amount % 50 != 0)
                        throw new InvalidOperationException("Подношение Перьями должно быть положительным и кратным 50, минимум 50.");
                    var spendable = GetSoulInkFeathers(soulRoot);
                    if (spendable < amount)
                        throw new InvalidOperationException($"Недостаточно Чернильных Перьев: доступно {spendable}, нужно {amount}.");
                    request.InkFeathersOffered = amount;
                    SetSoulInkFeathers(soulRoot, spendable - amount);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(offeringValue))
                        throw new InvalidOperationException("Для этого типа подношения нужен ID реликвии или записи Архива.");
                    FillNonCurrencyOfferingRequest(soulRoot, request, offeringValue);
                }

                await GuardianAbodeOfferingState.WriteAsync(_fs, request);
                await WriteObjectAsync(SoulStatePath, soulRoot);
            },
            "Подношение Обители подготовлено",
            "Браузер создал ожидающий запрос подношения и применил локальное изъятие ресурса.",
            new JsonObject
            {
                ["sourceSurface"] = "abode_offering_browser_write",
                ["guardianId"] = guardianId,
                ["offeringType"] = offeringType
            });
    }

    private async Task<BrowserPromptWriteResult> ApplyPlayerGuardianFoundationAsync(
        IReadOnlyDictionary<string, JsonNode?> answers,
        LocalUiSessionLockOwner owner)
    {
        return await ExecuteAsync(
            owner,
            "Browser player guardian foundation",
            [PlayerGuardianFoundationState.PendingRequestPath],
            async () =>
            {
                var context = await PlayerGuardianFoundationState.ReadContextAsync(_fs);
                if (!context.CanCreateRequest)
                    throw new InvalidOperationException(context.BlockingReason);

                var motifs = ReadAnswer(answers, "appearance_motifs")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(static motif => !string.IsNullOrWhiteSpace(motif))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                var request = new PlayerGuardianFoundationState.PendingPlayerGuardianFoundationRequest
                {
                    FounderSoulName = context.SoulName,
                    PreviousGuardianId = context.PreviousGuardianId,
                    PreviousGuardianName = context.PreviousGuardianName,
                    SourceShiningAvailability = context.ShiningAvailability,
                    ProposedDisplayName = ReadAnswer(answers, "proposed_display_name"),
                    MantleSummary = ReadAnswer(answers, "mantle_summary"),
                    MantleCreed = ReadAnswer(answers, "mantle_creed"),
                    AppearanceMotifs = motifs,
                    DominantAspect = ReadAnswer(answers, "dominant_aspect"),
                    CreatedAtTurn = Math.Max(0, _stateManager.CurrentState.TurnNumber)
                };
                var validation = await PlayerGuardianFoundationState.ValidateRequestAgainstCurrentStateAsync(_fs, request);
                if (!string.IsNullOrWhiteSpace(validation))
                    throw new InvalidOperationException(validation);

                await PlayerGuardianFoundationState.WriteAsync(_fs, request);
            },
            "Основание Хранителя подготовлено",
            "Браузер создал ожидающий запрос основания собственной мантии.",
            new JsonObject { ["sourceSurface"] = "found_guardian_mantle_browser_write" });
    }

    private async Task<BrowserPromptWriteResult> ExecuteAsync(
        LocalUiSessionLockOwner owner,
        string operationLabel,
        IReadOnlyCollection<string> rollbackPaths,
        Func<Task> writeOperation,
        string title,
        string message,
        JsonObject payload)
    {
        var result = await _coordinator.ExecuteAsync(
            new BrowserLocalWriteRequest(owner.OwnerId, owner.OwnerLabel, operationLabel),
            rollbackPaths,
            writeOperation);

        if (result.Success)
            return BrowserPromptWriteResult.Completed(title, message, payload);

        return BrowserPromptWriteResult.Failed(
            result.IsBlocked ? CommandExecutionState.Blocked : CommandExecutionState.Failed,
            result.IsBlocked ? UiNotificationSeverity.Warning : UiNotificationSeverity.Error,
            result.IsBlocked ? "Запись заблокирована" : "Ошибка записи",
            result.Message);
    }

    private async Task<string?> TryDescribeTreasuryCostBlockerAsync(JsonObject shiningRoot)
    {
        var coreBlocker = await TryDescribeTreasuryCoreActionPendingCostBlockerAsync();
        if (coreBlocker != null)
            return coreBlocker;

        var foundingMalformed = await ShiningFactionRequestState.IsRequestFileMalformedAsync(
            _fs,
            ShiningFactionRequestState.PendingFoundingsRequestPath,
            static json => JsonSerializer.Deserialize<ShiningFactionRequestState.PendingShiningFactionFoundingRequest>(
                json,
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        if (foundingMalformed || (await ShiningFactionRequestState.ReadFoundingRequestsAsync(_fs)).Count > 0)
            return $"{ShiningFactionRequestState.PendingFoundingsRequestPath} содержит активный или повреждённый контракт основания фракции.";

        if (shiningRoot.TryGetPropertyValue("pendingNativeFactionDiscovery", out var legacy) && legacy != null)
            return $"{ShiningAbodeState.StatePath}.pendingNativeFactionDiscovery содержит незакрытый legacy discovery-контракт.";

        return null;
    }

    private async Task<string?> TryDescribeTreasuryCoreActionPendingCostBlockerAsync()
    {
        var pendingState = await ShiningCoreActionRequestState.ReadRequestsStateAsync(_fs);
        if (pendingState.IsMalformed)
            return $"{ShiningCoreActionRequestState.PendingActionsRequestPath} повреждён.";
        if (pendingState.Requests.Count == 0)
            return null;

        var rawJson = await _fs.ReadFileAsync(ShiningCoreActionRequestState.PendingActionsRequestPath);
        if (string.IsNullOrWhiteSpace(rawJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            if (!document.RootElement.TryGetProperty(ShiningCoreActionRequestState.RequestsProperty, out var requestsNode) ||
                requestsNode.ValueKind != JsonValueKind.Array)
            {
                return $"{ShiningCoreActionRequestState.PendingActionsRequestPath} не содержит читаемый requests[].";
            }

            foreach (var requestNode in requestsNode.EnumerateArray())
            {
                if (requestNode.ValueKind != JsonValueKind.Object)
                    return $"{ShiningCoreActionRequestState.PendingActionsRequestPath} содержит повреждённую запись request.";

                var requestId = ReadJsonString(requestNode, "requestId") ?? "unknown";
                var actionType = ReadJsonString(requestNode, "actionType") ?? string.Empty;
                var hasFeathers = TryReadNonNegativeJsonInt(requestNode, "quotedCostFeathers", out var feathers);
                var hasLightSparks = TryReadNonNegativeJsonInt(requestNode, "quotedCostLightSparks", out var lightSparks);

                if ((hasFeathers && feathers > 0) || (hasLightSparks && lightSparks > 0))
                    return $"ожидающее основное действие Сияющей Обители {requestId} уже зафиксировало стоимость {feathers} Чернильных Перьев / {lightSparks} Искр.";

                if ((!hasFeathers || !hasLightSparks) && IsPotentiallyCostBearingShiningCoreAction(actionType))
                    return $"ожидающее основное действие Сияющей Обители {requestId} не имеет читаемых quotedCostFeathers/quotedCostLightSparks.";
            }
        }
        catch
        {
            return $"{ShiningCoreActionRequestState.PendingActionsRequestPath} не удалось прочитать как JSON.";
        }

        return null;
    }

    private async Task<string?> TryDescribeSpiritualArtUpgradeBlockerAsync()
    {
        var conflictRead = await ReadObjectAsync(AfterlifeSpiritualConflictState.StatePath);
        if (conflictRead?["activeConflict"] is JsonObject)
            return "Прокачка духовных искусств заблокирована: сейчас активен духовный конфликт посмертия.";

        if (_fs.FileExists(GuardianAbodeOfferingState.PendingRequestPath))
            return $"Прокачка духовных искусств заблокирована: найден {GuardianAbodeOfferingState.PendingRequestPath}.";

        foreach (var path in new[] { AfterlifeArchiveActionState.ConsultationRequestPath, AfterlifeArchiveActionState.ProjectFuelRequestPath })
        {
            if (_fs.FileExists(path))
                return $"Прокачка духовных искусств заблокирована: найден {path}.";
        }

        return null;
    }

    private static (bool Success, string Message) ApplyStandardSpiritualArtUpgrade(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        JsonObject profile,
        string artId,
        string currency,
        bool isShining)
    {
        var art = AfterlifeSpiritualConflictState.SpiritualArts.FirstOrDefault(item =>
            string.Equals(item.ArtId, artId, StringComparison.OrdinalIgnoreCase));
        if (art == null)
            return (false, $"Неизвестное стандартное духовное искусство: {artId}.");

        var artTiers = profile["artTiers"] as JsonObject ?? new JsonObject();
        var currentTier = Math.Clamp(GetNodeInt(artTiers[art.ArtId]), 0, 5);
        if (currentTier >= 5)
            return (false, "Искусство уже достигло максимального уровня 5.");
        var nextTier = currentTier + 1;
        var maxUnlocked = ResolveMaxUnlockedSpiritualArtTier(profile);
        if (maxUnlocked < art.MinUnlockTier || nextTier > maxUnlocked)
            return (false, $"Недостаточный ранг Просветления/Сияния для уровня {nextTier}.");

        var cost = currency == "light_sparks"
            ? ComputeSpiritualArtLightSparkCost(art, nextTier)
            : ComputeSpiritualArtInkFeatherCost(art, nextTier);
        if (!SpendCurrency(soulRoot, shiningRoot, currency, cost, isShining, out var reason))
            return (false, reason);

        artTiers[art.ArtId] = nextTier;
        profile["artTiers"] = artTiers;
        soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = profile;
        return (true, "ok");
    }

    private static (bool Success, string Message) ApplySpiritFocusUpgrade(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        JsonObject profile,
        string currency,
        bool isShining)
    {
        var currentTier = Math.Clamp(GetNodeInt(profile[AfterlifeSpiritualConflictState.SpiritFocusTierProperty]), 0, AfterlifeSpiritualConflictState.SpiritFocusMaxTier);
        if (currentTier >= AfterlifeSpiritualConflictState.SpiritFocusMaxTier)
            return (false, "Средоточие Души уже достигло максимального уровня.");
        var nextTier = currentTier + 1;
        if (nextTier > ResolveMaxUnlockedSpiritualArtTier(profile))
            return (false, $"Недостаточный ранг Просветления/Сияния для Средоточия Души {nextTier}.");
        var cost = currency == "light_sparks"
            ? ComputeSpiritFocusLightSparkCost(nextTier)
            : ComputeSpiritFocusInkFeatherCost(nextTier);
        if (!SpendCurrency(soulRoot, shiningRoot, currency, cost, isShining, out var reason))
            return (false, reason);

        profile[AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = nextTier;
        soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = profile;
        return (true, "ok");
    }

    private static (bool Success, string Message) ApplySpecialSpiritualArtUpgrade(
        JsonObject soulRoot,
        JsonObject? shiningRoot,
        JsonObject? entityProfilesRoot,
        JsonObject combatProfile,
        string artId,
        string currency,
        bool isShining)
    {
        if (entityProfilesRoot == null)
            return (false, $"Неизвестное стандартное духовное искусство: {artId}.");

        var playerProfile = FindPlayerSoulEntityProfile(entityProfilesRoot);
        var specialArt = FindSpecialArtById(playerProfile, artId);
        if (specialArt == null)
            return (false, $"Неизвестное стандартное или особое духовное искусство: {artId}.");

        var baseOperation = GetNodeString(specialArt["baseOperation"]);
        var baseArt = AfterlifeSpiritualConflictState.SpiritualArts.FirstOrDefault(art =>
            string.Equals(art.ArtId, baseOperation, StringComparison.OrdinalIgnoreCase));
        if (baseArt == null)
            return (false, $"Особое духовное искусство {artId} ссылается на неизвестное базовое действие.");

        var currentTier = Math.Clamp(GetNodeInt(specialArt["tier"]), 0, 5);
        if (currentTier >= 5)
            return (false, "Особое духовное искусство уже достигло максимального уровня 5.");
        var nextTier = currentTier + 1;
        var maxUnlocked = ResolveMaxUnlockedSpiritualArtTier(combatProfile);
        if (maxUnlocked < baseArt.MinUnlockTier || nextTier > maxUnlocked)
            return (false, $"Недостаточный ранг Просветления/Сияния для уровня {nextTier}.");

        var upgradeCost = specialArt["upgradeCost"] as JsonObject;
        var inkCost = Math.Max(0, GetNodeInt(upgradeCost?["inkFeathers"]));
        var sparkCost = Math.Max(0, GetNodeInt(upgradeCost?["lightSparks"]));
        if (inkCost <= 0 && sparkCost <= 0)
            return (false, "У особого духовного искусства должна быть положительная цена прокачки.");

        var cost = currency == "light_sparks" ? sparkCost : inkCost;
        if (cost <= 0)
            return (false, $"Особое духовное искусство {artId} нельзя прокачать за {DescribeCurrency(currency)}.");
        if (!SpendCurrency(soulRoot, shiningRoot, currency, cost, isShining, out var reason))
            return (false, reason);

        specialArt["tier"] = nextTier;
        var ledger = EnsureArray(playerProfile!, "ledger");
        ledger.Add(new JsonObject
        {
            ["entryId"] = $"special_art_browser_upgrade_{artId}_{nextTier}",
            ["reason"] = "special_art_local_upgrade",
            ["sourceSurface"] = "spiritual_arts_browser_write",
            ["artId"] = artId,
            ["displayName"] = GetNodeString(specialArt["displayName"]) ?? artId,
            ["tierBefore"] = currentTier,
            ["tierAfter"] = nextTier,
            ["currency"] = currency,
            ["cost"] = cost
        });
        return (true, "ok");
    }

    private static bool SpendCurrency(JsonObject soulRoot, JsonObject? shiningRoot, string currency, int cost, bool isShining, out string reason)
    {
        reason = string.Empty;
        if (currency == "ink_feathers")
        {
            var current = GetSoulInkFeathers(soulRoot);
            if (current < cost)
            {
                reason = $"Недостаточно Чернильных Перьев: нужно {cost}, доступно {current}.";
                return false;
            }
            SetSoulInkFeathers(soulRoot, current - cost);
            return true;
        }

        if (!isShining || shiningRoot == null)
        {
            reason = "Искры Света доступны только в Сияющей Обители.";
            return false;
        }

        var sparks = GetNodeInt(shiningRoot["lightSparks"]);
        if (sparks < cost)
        {
            reason = $"Недостаточно Искр Света: нужно {cost}, доступно {sparks}.";
            return false;
        }

        shiningRoot["lightSparks"] = sparks - cost;
        return true;
    }

    private static JsonObject BuildSyncedAfterlifeCombatProfile(JsonObject soulRoot, JsonObject? shiningRoot)
    {
        var profile = soulRoot[AfterlifeSpiritualConflictState.SoulStateProfileProperty]?.DeepClone() as JsonObject
                      ?? AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
        if (profile["schemaVersion"] is not JsonValue)
            profile["schemaVersion"] = 1;
        if (profile["artTiers"] is not JsonObject)
            profile["artTiers"] = new JsonObject();
        if (!profile.ContainsKey(AfterlifeSpiritualConflictState.SpiritFocusTierProperty))
            profile[AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 0;

        var enlightenmentRank = ResolveEnlightenmentRank(soulRoot);
        var radianceRank = ResolveRadianceRank(shiningRoot);
        var retainedRadianceRank = GetNodeInt(profile["retainedRadianceRank"]);
        profile["enlightenmentRank"] = enlightenmentRank;
        profile["radianceRank"] = radianceRank;
        profile["retainedRadianceRank"] = shiningRoot != null
            ? Math.Max(retainedRadianceRank, radianceRank)
            : retainedRadianceRank;
        if (!profile.ContainsKey("lastRecoveryTurn"))
            profile["lastRecoveryTurn"] = 0;
        return profile;
    }

    private static int ResolveMaxUnlockedSpiritualArtTier(JsonObject profile)
    {
        var enlightenmentRank = GetNodeInt(profile["enlightenmentRank"]);
        var radianceRank = GetNodeInt(profile["radianceRank"]);
        var retainedRadianceRank = GetNodeInt(profile["retainedRadianceRank"]);
        return Math.Clamp(
            Math.Max(
                ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.EnlightenmentRanks, enlightenmentRank),
                Math.Max(
                    ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.RadianceRanks, radianceRank),
                    ResolveUnlockedTierFromRanks(AfterlifeSpiritualConflictState.RadianceRanks, retainedRadianceRank))),
            0,
            5);
    }

    private static int ResolveUnlockedTierFromRanks(IReadOnlyList<AfterlifeSpiritualConflictState.RankDefinition> ranks, int rank) =>
        ranks.Where(item => item.Rank <= rank).Select(item => item.UnlocksArtTier).DefaultIfEmpty(0).Max();

    private static int ResolveEnlightenmentRank(JsonObject soulRoot)
    {
        var directProgress = GetNodeInt(soulRoot["enlightenment"]);
        var enlightenment = soulRoot["enlightenment"] as JsonObject;
        var soulProgression = soulRoot["soulProgression"] as JsonObject;
        var progress = Math.Max(
            Math.Max(directProgress, GetNodeInt(enlightenment?["experience"])),
            Math.Max(GetNodeInt(soulProgression?["totalExperience"]), GetNodeInt(soulProgression?["progressPercent"])));
        var tier = Math.Max(GetNodeInt(enlightenment?["level"]), GetNodeInt(soulProgression?["tier"]));
        return Math.Clamp(
            Math.Max(tier, ResolveRankFromProgress(AfterlifeSpiritualConflictState.EnlightenmentRanks, progress)),
            0,
            AfterlifeSpiritualConflictState.EnlightenmentRanks.Max(item => item.Rank));
    }

    private static int ResolveRadianceRank(JsonObject? shiningRoot)
    {
        var radiance = shiningRoot?["radiance"] as JsonObject;
        var progress = GetNodeInt(radiance?["experience"]);
        var tier = GetNodeInt(radiance?["tier"]);
        return Math.Clamp(
            Math.Max(tier, ResolveRankFromProgress(AfterlifeSpiritualConflictState.RadianceRanks, progress)),
            0,
            AfterlifeSpiritualConflictState.RadianceRanks.Max(item => item.Rank));
    }

    private static int ResolveRankFromProgress(IReadOnlyList<AfterlifeSpiritualConflictState.RankDefinition> ranks, int progress) =>
        ranks.Where(item => progress >= item.RequiredProgress).Select(item => item.Rank).DefaultIfEmpty(0).Max();

    private static int ComputeSpiritualArtInkFeatherCost(AfterlifeSpiritualConflictState.SpiritualArtDefinition art, int nextTier) =>
        checked(50 + nextTier * 50 + art.MinUnlockTier * 25);

    private static int ComputeSpiritualArtLightSparkCost(AfterlifeSpiritualConflictState.SpiritualArtDefinition art, int nextTier) =>
        checked(4 + nextTier * 3 + art.MinUnlockTier);

    private static int ComputeSpiritFocusInkFeatherCost(int nextTier) => checked(100 + nextTier * 100);

    private static int ComputeSpiritFocusLightSparkCost(int nextTier) => checked(8 + nextTier * 4);

    private static JsonArray BuildSpiritualArtBrowserAffectedFiles(string currency, bool isSpecialArt)
    {
        var files = new JsonArray(SoulStatePath);
        if (currency == "light_sparks")
            files.Add(ShiningAbodeState.StatePath);
        if (isSpecialArt)
            files.Add(AfterlifeEntityProfileState.StatePath);

        return files;
    }

    private void FillNonCurrencyOfferingRequest(JsonObject soulRoot, GuardianAbodeOfferingState.PendingAbodeOfferingRequest request, string offeringValue)
    {
        if (request.OfferingType == GuardianAbodeOfferingState.OfferingTypeSoulRelic)
        {
            var relic = FindSoulRelic(soulRoot, offeringValue)
                ?? throw new InvalidOperationException($"Реликвия {offeringValue} не найдена в soulRelics.stored[].");
            request.RelicId = GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]) ?? offeringValue;
            request.RelicName = GetNodeString(relic["name"]) ?? GetNodeString(relic["displayNameRu"]) ?? request.RelicId;
            request.RelicRarity = FirstNonEmpty(GetNodeString(relic["rarity"]), GetNodeString(relic["quality"]), GetNodeString(relic["relicRarity"]));
            if (!GuardianAbodeOfferingState.IsCanonicalSoulRelicRarity(request.RelicRarity))
                throw new InvalidOperationException($"Реликвия должна иметь каноническую редкость: {GuardianAbodeOfferingState.DescribeCanonicalSoulRelicRarities()}.");
            RemoveFirstArrayObject(soulRoot["soulRelics"]?["stored"] as JsonArray, relic);
            return;
        }

        var archiveEntry = FindArchiveEntry(soulRoot, offeringValue)
            ?? throw new InvalidOperationException($"Запись Архива {offeringValue} не найдена в afterlifeArchive.stored[].");
        request.ArchiveId = GetNodeString(archiveEntry["archiveId"]) ?? GetNodeString(archiveEntry["id"]) ?? offeringValue;
        request.ArchiveTitle = GetNodeString(archiveEntry["title"]) ?? request.ArchiveId;
        request.ArchiveEntryType = GetNodeString(archiveEntry["entryType"]) ?? "";
        request.ArchiveRarity = GetNodeString(archiveEntry["rarity"]) ?? "";
        RemoveFirstArrayObject(soulRoot["afterlifeArchive"]?["stored"] as JsonArray, archiveEntry);
    }

    private static JsonObject? FindSoulRelic(JsonObject soulRoot, string relicId)
    {
        if (soulRoot["soulRelics"]?["stored"] is not JsonArray stored)
            return null;
        return stored.OfType<JsonObject>().FirstOrDefault(relic =>
            string.Equals(GetNodeString(relic["relicId"]) ?? GetNodeString(relic["id"]), relicId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject? FindArchiveEntry(JsonObject soulRoot, string archiveId)
    {
        if (soulRoot["afterlifeArchive"]?["stored"] is not JsonArray stored)
            return null;
        return stored.OfType<JsonObject>().FirstOrDefault(entry =>
            string.Equals(GetNodeString(entry["archiveId"]) ?? GetNodeString(entry["id"]), archiveId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject? FindPlayerSoulEntityProfile(JsonObject? entityProfilesRoot)
    {
        if (entityProfilesRoot?[AfterlifeEntityProfileState.ProfilesProperty] is not JsonArray profiles)
            return null;

        return profiles.OfType<JsonObject>().FirstOrDefault(profile =>
            string.Equals(GetNodeString(profile["actorType"]), "player_soul", StringComparison.OrdinalIgnoreCase));
    }

    private static JsonObject? FindSpecialArtById(JsonObject? profile, string artId)
    {
        if (profile?["specialArts"] is not JsonArray specialArts)
            return null;

        return specialArts.OfType<JsonObject>().FirstOrDefault(art =>
            string.Equals(GetNodeString(art["artId"]), artId, StringComparison.OrdinalIgnoreCase));
    }

    private static JsonArray EnsureArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonArray array)
            return array;

        array = new JsonArray();
        root[propertyName] = array;
        return array;
    }

    private static void RemoveFirstArrayObject(JsonArray? array, JsonObject target)
    {
        if (array == null)
            return;
        for (var i = 0; i < array.Count; i++)
        {
            if (ReferenceEquals(array[i], target))
            {
                array.RemoveAt(i);
                return;
            }
        }
    }

    private async Task<int?> TryReadSoulInkFeathersForValidationAsync()
    {
        try
        {
            var soulRoot = await ReadObjectAsync(SoulStatePath);
            return soulRoot == null ? null : GetSoulInkFeathers(soulRoot);
        }
        catch
        {
            return null;
        }
    }

    private static JsonObject BuildGachaBaseResultPayload(GachaResult? gacha)
    {
        var dice = new JsonArray();
        foreach (var die in gacha?.DiceUsed ?? Array.Empty<int>())
            dice.Add(die);

        return new JsonObject
        {
            ["diceUsed"] = dice,
            ["baseScore"] = gacha?.BaseScore ?? 0,
            ["baseRarity"] = string.IsNullOrWhiteSpace(gacha?.BaseRarity) ? "Common" : gacha!.BaseRarity,
            ["formula"] = string.IsNullOrWhiteSpace(gacha?.Formula)
                ? "client-computed gacha base (range 4-80)"
                : gacha!.Formula
        };
    }

    private static string BuildDirectGachaGmAction(int cost) =>
        $"[CHAOS_SEA_DIRECT_GACHA] Игрок напрямую тянет Реликвию Души из Моря Хаоса и тратит {cost} Чернильных Перьев. " +
        "Это НЕ гача через текущего Хранителя: не применять репутацию Хранителя, его скидки, штрафы, социальные факторы, улучшенные или ухудшенные шансы. " +
        "Результат должен быть нейтральным: finalRarity обязан точно совпадать с turn_request.gachaBaseResult.baseRarity, без апгрейдов или даунгрейдов. " +
        "Реликвию нужно добавить напрямую в soul state игрока через metaStateUpdates.soulRelicOperations.addRelic как ровно одну новую Soul Relic; существующие реликвии не удалять. " +
        "Перья уже списаны клиентом, GM не списывает их второй раз.";

    private async Task<JsonObject?> ReadObjectAsync(string path)
    {
        var raw = await _fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        return JsonNode.Parse(raw) as JsonObject;
    }

    private async Task<JsonObject> ReadRequiredObjectAsync(string path, string error)
    {
        var root = await ReadObjectAsync(path);
        return root ?? throw new InvalidOperationException(error);
    }

    private async Task WriteObjectAsync(string path, JsonObject root) =>
        await _fs.WriteFileAtomicAsync(path, root.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

    private static string NormalizeCommand(string command)
    {
        var split = command.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return split.Length == 0 ? string.Empty : split[0].ToLowerInvariant();
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
        }
        return node.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
    }

    private static int ReadIntAnswer(IReadOnlyDictionary<string, JsonNode?> answers, string key, int fallback)
    {
        var text = ReadAnswer(answers, key);
        return int.TryParse(text, out var value) ? value : fallback;
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

    private static string? ReadJsonString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool TryReadNonNegativeJsonInt(JsonElement root, string propertyName, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
            return value >= 0;

        if (property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), out value) &&
            value >= 0)
        {
            return true;
        }

        return false;
    }

    private static bool IsPotentiallyCostBearingShiningCoreAction(string? actionType)
    {
        return actionType?.Trim().ToLowerInvariant() is
            ShiningCoreActionRequestState.ActionTypeDiscoverNativeFaction or
            ShiningCoreActionRequestState.ActionTypeInvestInFaction or
            ShiningCoreActionRequestState.ActionTypeCompleteProject or
            ShiningCoreActionRequestState.ActionTypePullRelicGacha or
            ShiningCoreActionRequestState.ActionTypeForgeRelicReshape or
            ShiningCoreActionRequestState.ActionTypeForgeRelicRetuneProperty or
            ShiningCoreActionRequestState.ActionTypeForgeRelicStrengthenBand or
            ShiningCoreActionRequestState.ActionTypeForgeRelicStabilizeEcho or
            ShiningCoreActionRequestState.ActionTypeForgeRelicUpliftRarity;
    }

    private static string NormalizeOfferingType(string raw) =>
        raw.Trim().ToLowerInvariant() switch
        {
            "ink_feathers" => GuardianAbodeOfferingState.OfferingTypeInkFeathers,
            "soul_relic" => GuardianAbodeOfferingState.OfferingTypeSoulRelic,
            "lore_fragment" or "archive_lore_fragment" => GuardianAbodeOfferingState.OfferingTypeArchiveLoreFragment,
            "secret_record" or "archive_secret_record" => GuardianAbodeOfferingState.OfferingTypeArchiveSecretRecord,
            _ => string.Empty
        };

    private static JsonObject? FindObjectById(JsonNode node, IReadOnlyCollection<string> idProperties, string expectedId)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in idProperties)
            {
                if (string.Equals(GetNodeString(obj[property]), expectedId, StringComparison.OrdinalIgnoreCase))
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

    private static int GetSoulInkFeathers(JsonObject soulRoot)
    {
        var node = soulRoot["inkFeathers"];
        return node is JsonObject obj ? GetNodeInt(obj["current"]) : GetNodeInt(node);
    }

    private static void SetSoulInkFeathers(JsonObject soulRoot, int current)
    {
        var safeCurrent = Math.Max(0, current);
        if (soulRoot["inkFeathers"] is JsonObject obj)
        {
            obj["current"] = safeCurrent;
            obj["total"] = Math.Max(GetNodeInt(obj["total"], safeCurrent), safeCurrent);
            return;
        }
        soulRoot["inkFeathers"] = new JsonObject
        {
            ["current"] = safeCurrent,
            ["total"] = safeCurrent
        };
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var text))
                return text;
            if (value.TryGetValue<int>(out var number))
                return number.ToString();
        }
        return null;
    }

    private static int GetNodeInt(JsonNode? node, int fallback = 0)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var number))
                return number;
            if (value.TryGetValue<long>(out var longValue) && longValue is >= int.MinValue and <= int.MaxValue)
                return (int)longValue;
            if (value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed))
                return parsed;
        }
        return fallback;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string DescribeCurrency(string currency) =>
        currency == "light_sparks" ? "Искры Света" : "Чернильные Перья";
}
